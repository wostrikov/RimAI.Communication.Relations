using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Dialogue;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Relation;
using Ustas.RimAI.Communication.Relations.Guards;
using Ustas.RimAI.Communication.Relations.WorldState;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.Prompting.Builders;
using Ustas.RimAI.Communication.Relations.Context;
using Ustas.RimAI.Communication.Relations.Prompting.Transfer;
using Ustas.RimAI.Communication.Relations.Serialization;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Prompting.Diplomacy;

namespace Ustas.RimAI.Communication.Relations.Prompting.Builders
{
        internal sealed class DiplomacyPromptBuilderGuidance : DiplomacyPromptBuilderCollaborator
    {
        internal DiplomacyPromptBuilderGuidance(DiplomacyPromptBuilder owner) : base(owner)
        {
        }

        internal void AppendApiLimits(StringBuilder sb, Faction faction = null)
        {
            var settings = RelationsMod.Settings ?? RelationsMod.Instance?.InstanceSettings;
            if (settings == null) return;

            sb.AppendLine();
            sb.AppendLine("=== Поточні обмеження API (обовʼязкові) ===");

            // Check current cooldown for specific faction
            if (faction != null)
            {
                int questCooldownSec = GameAIInterface.Instance.GetRemainingCooldownSeconds(faction, "CreateQuest");
                if (questCooldownSec > 0)
                {
                    // GameAIInterface.GetRemainingCooldownSeconds returns total remaining seconds (ticks/60)
                    // One RimWorld day is 60,000 ticks = 1000 seconds.
                    float remainingDays = questCooldownSec / 1000f;
                    sb.AppendLine($"- [Ключове] create_quest для {faction.Name} зараз на перезарядці. Лишилося: {remainingDays:F1} дн. До кінця перезарядки створювати будь-які завдання чи доручення заборонено. Якщо гравець просить завдання — відмов і поясни причину в ролі (наприклад: перегрупування, поповнення запасів або ще не виконана попередня обіцянка).");
                }
            }

            sb.AppendLine($"- Межа зміни прихильності за раз: {settings.MaxGoodwillAdjustmentPerCall} (діапазон: від 0 до {settings.MaxGoodwillAdjustmentPerCall})");
            sb.AppendLine($"- Денна межа зміни прихильності: {settings.MaxDailyGoodwillAdjustment}");
            sb.AppendLine($"- Перезарядка прихильності: {settings.GoodwillCooldownTicks / 2500f:F1} год");
            sb.AppendLine($"- Мінімальна прихильність для запиту допомоги: {settings.MinGoodwillForAid}");
            sb.AppendLine($"- Максимальний поріг прихильності для оголошення війни: {settings.MaxGoodwillForWarDeclaration}");
            sb.AppendLine($"- Межа вартості миру: {settings.MaxPeaceCost}");
            sb.AppendLine($"- Значення прихильності після миру: {settings.PeaceGoodwillReset}");
            sb.AppendLine($"- Перезарядка create_quest: від {settings.MinQuestCooldownDays} до {settings.MaxQuestCooldownDays} дн.");
            sb.AppendLine();
            sb.AppendLine("Увімкнені можливості:");
            sb.AppendLine($"- Зміна прихильності: {(settings.EnableAIGoodwillAdjustment ? "是" : "否")}");
            sb.AppendLine($"- Оголошення війни: {(settings.EnableAIWarDeclaration ? "是" : "否")}");
            sb.AppendLine($"- Мир: {(settings.EnableAIPeaceMaking ? "是" : "否")}");
            sb.AppendLine($"- Торговий караван: {(settings.EnableAITradeCaravan ? "是" : "否")}");
            sb.AppendLine($"- Запит допомоги: {(settings.EnableAIAidRequest ? "是" : "否")}");
            sb.AppendLine("- Створення завдань: так");
            sb.AppendLine();

            // Airdrop trade rules
            if (settings.EnableAIItemAirdrop && faction != null)
            {
                Owner.AppendAirdropTradeRules(sb, faction);
            }
        }

        internal void AppendAirdropTradeRules(StringBuilder sb, Faction faction)
        {
            float wealthItems = Find.AnyPlayerHomeMap?.wealthWatcher?.WealthItems ?? 0f;
            float factionTradeTotalSilver = GameAIInterface.Instance.GetAirdropFactionTradeTotalForPolicy(faction);
            AirdropTradeRuleSnapshot rule = ItemAirdropTradePolicy.ResolveRuleSnapshot(faction, wealthItems, factionTradeTotalSilver);
            TechLevel techLevel = faction.def?.techLevel ?? TechLevel.Archotech;

            sb.AppendLine("=== Правила бартеру зі скиданням (обовʼязкові) ===");
            sb.AppendLine($"- Рівень технологій фракції: {techLevel}. Торгувати товарами вищого рівня технологій заборонено.");
            sb.AppendLine($"- Поточна прихильність: {rule.Goodwill}. Ліміт сукупної торгівлі: {rule.TradeLimitSilver} срібла.");
            sb.AppendLine($"- Правило торгового ліміту: {rule.TradeLimitRuleText}.");
            sb.AppendLine($"- Доставка однієї капсули скидання: {rule.ShippingCostPerPod} срібла. Доставка вираховується з пропозиції гравця й не вказується в ціні окремо.");
            sb.AppendLine("- [Role -- highest priority economic constraint] You are the SELLER/SUPPLIER: the need field is what you sell and air-drop DELIVER TO the player (you ship -> player receives). The player is the BUYER/PAYER: payment_items is what the player pays to you (player ships -> you receive). Direction is NEVER reversible -- you are NOT the buyer, the player is NOT the supplier.");
            sb.AppendLine("- І потрібні товари, і оплата рахуються за ринковою ціною (ThingDef.BaseMarketValue, мінімум 0.01).");
            sb.AppendLine("- Правило множника для потрібних товарів: якщо tradeTags містить ExoticMisc — x3.0, решта товарів — x1.6; золото й срібло далі за фіксованою ринковою ціною.");
            sb.AppendLine("- Правило множника для оплати: усе, крім золота й срібла, рахується за ринковою ціною x0.6; золото й срібло — далі за фіксованою ринковою ціною.");
            sb.AppendLine("- Перекриття множника для особливих товарів: якщо картка обміну позначена special_item_discount, товар рахується з множником x0.4 (знижка); якщо special_item_scarce — з множником x2.0 (націнка за дефіцит). Особливий множник має пріоритет над загальним.");
            Owner.AppendFactionSpecialItemInventory(sb, faction);
            sb.AppendLine("- Дозволено брати надбавку понад ринкову ціну (сценарій термінового бартеру). Якщо гравець пропонує менше за орієнтовну ціну, відмов або назви зустрічну ціну.");
            sb.AppendLine();
        }

        internal void AppendFactionSpecialItemInventory(StringBuilder sb, Faction faction)
        {
            if (faction == null) return;

            FactionSpecialItemSet itemSet = FactionSpecialItemsManager.Instance.GetOrCreate(faction);
            if (itemSet == null) return;

            bool hasAny = false;
            if (itemSet.DiscountItem != null && itemSet.DiscountItem.IsAvailable && !string.IsNullOrEmpty(itemSet.DiscountItem.DefName))
            {
                ThingDef discountDef = DefDatabase<ThingDef>.GetNamedSilentFail(itemSet.DiscountItem.DefName);
                if (discountDef != null && ItemAirdropTradePolicy.TryResolveSpecialItemPrice(discountDef, SpecialItemType.Discount, out float discountPrice, out _))
                {
                    sb.AppendLine($"- Поточний товар зі знижкою: {itemSet.DiscountItem.Label} (орієнтовна ціна {discountPrice:F1}, special_item_discount)");
                }
                else
                {
                    sb.AppendLine($"- Поточний товар зі знижкою: {itemSet.DiscountItem.Label} (special_item_discount)");
                }
                hasAny = true;
            }
            if (itemSet.ScarceItem != null && itemSet.ScarceItem.IsAvailable && !string.IsNullOrEmpty(itemSet.ScarceItem.DefName))
            {
                ThingDef scarceDef = DefDatabase<ThingDef>.GetNamedSilentFail(itemSet.ScarceItem.DefName);
                if (scarceDef != null && ItemAirdropTradePolicy.TryResolveSpecialItemPrice(scarceDef, SpecialItemType.Scarce, out float scarcePrice, out _))
                {
                    sb.AppendLine($"- Поточний дефіцитний товар: {itemSet.ScarceItem.Label} (орієнтовна ціна {scarcePrice:F1}, special_item_scarce)");
                }
                else
                {
                    sb.AppendLine($"- Поточний дефіцитний товар: {itemSet.ScarceItem.Label} (special_item_scarce)");
                }
                hasAny = true;
            }
            if (!hasAny)
            {
                sb.AppendLine("- Наразі особливих товарів нема.");
            }
        }

        /// <summary>/// Build dynamic quest availability from centralized eligibility service.
 ///</summary>
        internal Dictionary<string, object> BuildQuestPromptContext(DialogueScenarioContext context)
        {
            var parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (context == null)
            {
                return parameters;
            }

            if (context.Faction != null)
            {
                parameters["faction"] = context.Faction;
                parameters["askerFaction"] = context.Faction;
            }

            bool isOrbitalTrader = context.Tags.Contains("scene:orbital_trader") || context.Tags.Contains("source:orbital_trader");
            if (isOrbitalTrader)
            {
                parameters["orbital_trader_context"] = true;
                parameters["dialogue_source"] = "orbital_trader";
            }

            return parameters;
        }

        internal void AppendDynamicQuestGuidance(StringBuilder sb, Faction faction, Dictionary<string, object> parameters = null)
        {
            if (faction == null) return;

            FactionQuestAvailabilityReport availability = ApiActionEligibilityService.Instance.GetFactionQuestAvailabilityReport(faction, parameters);
            var report = availability.EvaluatedQuestDefs;
            var allowed = report.Where(x => x.Allowed).ToList();
            var blocked = report.Where(x => !x.Allowed).ToList();
            bool isOrbitalTraderContext = ApiActionEligibilityService.Instance.IsOrbitalTraderDialogueContext(faction, availability.Parameters);
            bool isMerchantFaction = string.Equals(faction.def?.defName, "OutlanderCivil", StringComparison.Ordinal) ||
                                     string.Equals(faction.def?.defName, "OutlanderRough", StringComparison.Ordinal);

            sb.AppendLine();
            sb.AppendLine("=== Динамічна доступність завдань (генерується автоматично за поточною фракцією) ===");
            sb.AppendLine($"Фракція: {faction.Name} | Технології: {faction.def?.techLevel} | Тип: {faction.def?.defName}");
            if (isOrbitalTraderContext)
            {
                sb.AppendLine("Поточна сесія: звʼязок з орбітальним торговцем. Заборонено створювати замовлення, які потребують виконання наземним поселенням; коли йдеться про обмін конкретними товарами, дозволено лише скеровувати до request_item_airdrop.");
            }
            if (isMerchantFaction)
            {
                sb.AppendLine("Поточна фракція: торгова гільдія. Заборонено створювати замовлення TradeRequest; коли йдеться про обмін конкретними товарами, дозволено лише скеровувати до request_item_airdrop.");
            }
            sb.AppendLine();

            if (!allowed.Any())
            {
                sb.AppendLine("[Заблоковано] Поточна фракція не має доступних придатних шаблонів завдань.");
                if (blocked.Any())
                {
                    sb.AppendLine("Причина блокування:");
                    foreach (var item in blocked)
                    {
                        sb.AppendLine($"  - {item.QuestDefName}: {item.Message}");
                    }
                }
                sb.AppendLine();
                return;
            }

            sb.AppendLine("Завдання, доступні поточній фракції (можна брати лише ці точні defName, у дужках — опис змісту завдання):");
            foreach (var item in allowed)
            {
                sb.AppendLine($"  - {item.QuestDefName} {Owner.GetQuestTemplateDescription(item.QuestDefName)}");
            }

            if (blocked.Any())
            {
                sb.AppendLine();
                sb.AppendLine("Шаблони завдань, заблоковані для поточної фракції (використовувати заборонено):");
                foreach (var item in blocked)
                {
                    sb.AppendLine($"  - {item.QuestDefName}: {item.Message}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("Важливо: questDefName можна брати лише зі списку доступних вище.");
            if (allowed.Any(item => string.Equals(item.QuestDefName, "PawnLend", StringComparison.Ordinal)))
            {
                sb.AppendLine("Жорсткі умови PawnLend: обирати PawnLend можна лише тоді, коли в поточній колонії справді є колоністи, яких можна відрядити, і система здатна під час виконання скласти повний контракт відрядження (кількість, дні, обовʼязки, ціль, чи надсилати шатл). Якщо це неможливо — треба взяти інший доступний шаблон.");
            }
            sb.AppendLine("Нагадування про напрям: уважно читай позначку [гравець→фракція] або [фракція→гравець] в описі кожного шаблону завдання. Хибний напрям — це хибне виконання функції, і воно руйнує занурення.");
            sb.AppendLine();
        }

        internal void AppendQuestSelectionHardRules(StringBuilder sb)
        {
            sb.AppendLine("=== Суворі правила перекриття шаблонів завдань ===");
            sb.AppendLine("Ти маєш вважати «динамічну доступність завдань (генерується автоматично за поточною фракцією)» єдиним чинним джерелом завдань.");
            sb.AppendLine("Заборонено користуватися статичними чи згаданими по памʼяті рекомендаціями завдань з інших розділів.");
            sb.AppendLine("Якщо завдання є в blocked templates або blocked actions, викликати create_quest заборонено.");
            sb.AppendLine("Правило відповідності напряму завдання: [гравець→фракція] = гравець дає людей, ресурси чи зусилля; [фракція→гравець] = фракція дає людей, завдання чи відомості; [двобічне] = беруть участь обидві сторони. Напрям обраного questDefName має збігатися з контекстом розмови. Наприклад: гравець каже 'можу позичити тобі людей' → PawnLend [гравець→фракція]; гравець каже 'чи не могли б ви прислати когось на допомогу' ≠ PawnLend, бо в неімперських фракцій шаблону надсилання людей нема — скеруй до request_aid або іншої доступної дії.");
            sb.AppendLine("Політика безпеки може вимикати шаблони високого ризику (наприклад OpportunitySite_ItemStash). Якщо шаблон вимкнено, відмов у ролі й поясни обмеження.");
            sb.AppendLine("Якщо це звʼязок з орбітальним торговцем, заборонено через create_quest створювати замовлення, які вимагають від гравця привезти вказаний товар у наземне поселення; на такий запит поясни, що в орбітального торговця нема цього ланцюга доставки, і скеруй гравця до request_item_airdrop.");
            sb.AppendLine("Якщо поточна фракція — торгова гільдія (OutlanderCivil / OutlanderRough), заборонено створювати TradeRequest через create_quest; коли йдеться про обмін товарами, треба одразу брати request_item_airdrop.");
            sb.AppendLine();
        }

        internal string GetQuestTemplateDescription(string questDefName)
        {
            if (string.IsNullOrEmpty(questDefName)) return string.Empty;
            switch (questDefName)
            {
                case "TradeRequest":
                    return "(Торгове замовлення: [гравець→фракція] фракція просить товари, гравець постачає вказане в обмін на винагороду)";
                case "OpportunitySite_PeaceTalks":
                    return "(Мирні перемовини: [двобічне] фракція запрошує представника гравця на переговори, що можуть зняти ворожість)";
                case "PawnLend":
                    return "(Запит на відрядження: [гравець→фракція] фракція просить позичити колоніста гравця. Гравець дає людину → фракція приймає. Доставка шатлом, після завершення — винагорода й прихильність)";
                case "ThreatReward_Raid_MiscReward":
                    return "(Винагорода за загрозу: [фракція→гравець] імперська фракція оголошує нагороду; знищивши ціль, гравець дістає імперську славу або предмети)";
                case "Hospitality_Refugee":
                    return "(Прийом біженців: [фракція→гравець] імперська фракція надсилає біженців пожити на базі гравця. Фракція дає людей → гравець приймає. Після завершення прийому Імперія дає винагороду)";
                case "OpportunitySite_ItemStash":
                    return "(Схованка з предметами: [фракція→гравець] фракція дає відомості про ворожий опорний пункт, гравець вирушає зачистити його заради здобичі)";
                default:
                    return string.Empty;
            }
        }

        internal static readonly string[] PresenceBehaviorSectionTitles =
        {
            "[Політика статусу присутності]",
            "Online Status Strategy:",
            "Online Status Strategy"
        };

        internal static readonly string[] PresenceBehaviorActionAnchors =
        {
            "[exit_dialogue]",
            "[go_offline",
            "[set_dnd]"
        };

        }

}
