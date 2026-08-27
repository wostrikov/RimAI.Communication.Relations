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
        internal sealed class DiplomacyPromptBuilderContract : DiplomacyPromptBuilderCollaborator
    {
        internal DiplomacyPromptBuilderContract(DiplomacyPromptBuilder owner) : base(owner)
        {
        }

        internal void AppendSimpleConfig(StringBuilder sb, SystemPromptConfig config, Faction faction)
        {
            Owner.AppendCompactDiplomacyResponseContract(sb, config, faction);
        }

        internal void AppendAdvancedConfig(StringBuilder sb, SystemPromptConfig config, Faction faction)
        {
            Owner.AppendCompactDiplomacyResponseContract(sb, config, faction);
        }

        internal void AppendCompactDiplomacyResponseContract(StringBuilder sb, SystemPromptConfig config, Faction faction)
        {
            List<ApiActionConfig> availableActions = Owner.GetAvailableActionsForFaction(config, faction);
            Owner.AppendOutputSpecificationAuthoritySection(sb, config);
            Owner.AppendDiplomacyResponseFormatSection(sb, config);
            Owner.AppendDiplomacyCriticalActionRules(sb);
            Owner.AppendCompactActionCatalog(sb, availableActions);
            Owner.AppendBlockedActionHints(sb, config, faction);
            Owner.AppendGoodwillPeacePolicyHints(sb, faction);
            Owner.AppendPresenceActionGuidance(sb, availableActions);
            sb.AppendLine(PromptTextConstants.NoActionResponseHint);
        }

        internal void AppendDiplomacyResponseFormatSection(StringBuilder sb, SystemPromptConfig config)
        {
            sb.AppendLine(PromptTextConstants.ResponseFormatHeader);
            sb.AppendLine(PromptTextConstants.ResponseFormatReference);
            sb.AppendLine();
        }

        internal void AppendDiplomacyCriticalActionRules(StringBuilder sb)
        {
            sb.AppendLine(PromptTextConstants.CriticalActionRulesHeader);
            sb.AppendLine(PromptTextConstants.CriticalActionRulesReference);
            sb.AppendLine();
        }

        internal void AppendOutputSpecificationAuthoritySection(StringBuilder sb, SystemPromptConfig config)
        {
            string jsonTemplate = config?.ResponseFormat?.JsonTemplate ?? string.Empty;
            if (string.IsNullOrWhiteSpace(jsonTemplate))
            {
                throw new PromptRenderException(
                    "prompt.response_format.json_template",
                    "diplomacy",
                    new PromptRenderDiagnostic
                    {
                        ErrorCode = PromptRenderErrorCode.TemplateMissing,
                        Message = "ResponseFormat.JsonTemplate is empty. Runtime prompt build aborted."
                    });
            }

            Owner.AppendStrictJsonFormatPreamble(sb);
            sb.AppendLine(PromptTextConstants.OutputSpecificationAuthorityHeader);
            sb.AppendLine(PromptTextConstants.OutputSpecificationAuthorityReference);
            Owner.AppendOutputSpecificationAuthorityRules(sb);
            Owner.AppendOutputSpecificationAuthorityTemplate(sb, jsonTemplate);
        }

        internal void AppendStrictJsonFormatPreamble(StringBuilder sb)
        {
            sb.AppendLine(PromptTextConstants.StrictJsonFormatHeader);
            sb.AppendLine(PromptTextConstants.StrictJsonFormatRequirement);
            sb.AppendLine();
            sb.AppendLine("No actions:");
            sb.AppendLine("```json");
            sb.AppendLine(PromptTextConstants.StrictJsonFormatTemplate);
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("With airdrop action (note: payment_items entries MUST have both item and count):");
            sb.AppendLine("```json");
            sb.AppendLine(PromptTextConstants.StrictJsonFormatTemplateWithAction);
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("WRONG (will be discarded):");
            sb.AppendLine("```");
            sb.AppendLine("好的，这就安排！\\n```json\\n{\"visible_dialogue\":\"...\"}\\n```");
            sb.AppendLine("```");
            sb.AppendLine("NEVER wrap in code blocks or add text before/after the JSON.");
            sb.AppendLine();
        }

        internal void AppendOutputSpecificationAuthorityRules(StringBuilder sb)
        {
            string[] lines =
            {
                "- [Highest Priority] Your entire reply MUST be a single top-level JSON object. First char {, last char }. No text, explanation, reasoning, or Markdown outside the JSON object. Violation will cause the reply to be discarded.",
                "- [Action engagement -- critical] You are a faction leader with real gameplay agency. Text-only responses produce ZERO effect. When the player makes a request matching an available action, you MUST include it in your JSON response. Skipping actions when the player expects them makes the interaction feel broken and unresponsive.",
                "- 必填键：visible_dialogue。",
                "- 可选键：actions、meta、debug。",
                "- Якщо actions є, обовʼязковий ключ actions[].action; actions[].parameters необовʼязковий.",
                "- visible_dialogue може містити лише видиму репліку в ролі, без ланцюжка міркувань, пояснень, заголовків, переказу правил чи налагоджувальних даних.",
                PromptTextConstants.OutputSpecificationAuthorityLegacyRule,
                PromptTextConstants.OutputSpecificationAuthorityBoundaryRule,
                PromptTextConstants.OutputSpecificationAuthorityHistoryStyleRule,
                "- [Caravan role direction -- highest priority] When you dispatch request_caravan, the caravan travels to the player colony. You (the AI faction) are the SELLER: the caravan carries goods from YOUR faction's trade list for the player to buy with silver. The player is the BUYER. Direction is NEVER reversible -- you are NOT buying from the player, and the player is NOT selling to the caravan.",
                "- [Direction constraint -- highest priority] need is what YOU (the AI faction) sell and airdrop to the PLAYER colony (AI -> player). payment_items is what the PLAYER pays to YOU, deducted from player beacon inventory (player -> AI). The direction of need and payment_items is NEVER reversible -- you are NOT the buyer. If the player says they want 1000 wood, need is 1000 wood (YOU send to PLAYER), NOT something you receive.",
                "- Забороняється описувати ігровий ефект як «уже виконано», якщо в тій самій відповіді нема відповідної дії в actions.",
                "- request_caravan/request_visitor/request_aid/request_raid/request_item_airdrop/request_info/pay_prisoner_ransom/create_quest/trigger_incident — це відкладені або системно сплановані дії; формулювати їх треба як намір чи домовленість, а не як уже прибулий чи завершений результат.",
                "- Здоровий глузд щодо обміну й надсилання товарів: миттєвий обмін можливий лише через request_item_airdrop; request_caravan — це відкладена торгівля.",
                "- Жорстке обмеження обміну зі скиданням: одне request_item_airdrop — це один товар в обмін на один (один need відповідає одному набору payment_items). Заборонено вписувати в need кілька товарів (наприклад «1000 деревини і 50 сталі») і заборонено домішувати потрібний товар у payment_items. need може містити лише один товар та його кількість.",
                "- Поле need має точно відображати запит гравця: якщо в повідомленні є конкретна кількість («1000 деревини», «50 сталі»), need мусить нести цю кількість (формат: число + назва товару); ігнорувати вказану гравцем кількість заборонено.",
                "- Формат payment_items: масив, кожен елемент має водночас містити item (string) і count (додатне ціле). Приклад: [{\"item\":\"Silver\",\"count\":220}]. Без item або count дія не виконається.",
                "- Якщо гравець влучно назвав відомі тобі торгові факти (запаси, діапазон цін, потребу), можеш піти на поступку й дати знижку, поки це не порушує межу собівартості.",
                "- Жорстке обмеження каравану: товари каравану, висланого через request_caravan, визначає торговий список фракції; ти не можеш ані вказати, ані змінити, ані пообіцяти конкретний вантаж. Гравець також не може замовити конкретні товари через запит каравану. Коли гравець просить певний товар, скеруй його на request_item_airdrop (у скиданні товар вказати можна). Допустимі типи request_caravan лише General / BulkGoods / CombatSupplier / Exotic / Slaver; треба взяти одне з цих точних значень, вигадувати власні не можна.",
                "- Жорстке обмеження орбітального торговця: орбітальний торговець не має змоги виконувати доставку в наземне поселення, тож обіцяти «привезу вказаний товар у ваше поселення й закрию замовлення» заборонено. Якщо гравець просить саме цього — поясни обмеження й скеруй на request_item_airdrop.",
                "- Жорстке обмеження контексту звʼязку: це чат через термінал звʼязку, а не зустріч наживо; заборонено писати «я вже на місці», «розберуся особисто», «заберу людей».",
                "- Семантичне обмеження викупу: request_info(info_type=prisoner) лише тоді, коли бракує чинного target_pawn_load_id; якщо ціль уже відома, можна одразу pay_prisoner_ransom.",
                "- Якщо у видимому тексті зʼявляється однозначна обіцянка виконати («організую», «уже подав», «зараз вишлю», «негайно оформлю»), у тій самій відповіді має бути відповідний {\"actions\":[...]}; інакше текст треба переписати як уточнювальне питання або невпевнене формулювання.",
                "- Коли повідомлення гравця містить поля картки обміну зі скиданням (need/count/payment_items/scenario) і картка вже точно привʼязана до need_def, цей need_def є жорсткою ціллю виконання; ти можеш відмовити, перерахувати ціну або змінити кількість чи спосіб оплати, але не можеш мовчки підмінити його іншим товаром.",
                "- Якщо ти обрав перерахунок ціни й цього ходу дію не виконуєш — скажи це зануреною природною реплікою, але у видимому тексті обовʼязково назви товар, кількість і ціну в сріблі, і бажано додай коротку причину (запаси, ризик, дорога, втрати); уникай сухих шаблонів на кшталт «нова ціна: item=... count=... silver=...».",
                "- Якщо ти вирішив виконати request_item_airdrop і зараз є need_def, привʼязаний карткою обміну, то need у дії має й далі вказувати на цей товар; якщо хочеш змінити товар — спершу запропонуй заміну природною мовою й чекай, поки гравець вибере інший товар або подасть нову картку обміну.",
                "- Жорстке обмеження для викупу: якщо в тексті зʼявляються формулювання завершеності («подано», «сплачено», «розрахувалися», «його вже відпустили»), у тій самій відповіді має бути pay_prisoner_ransom; інакше повертайся до формулювань «очікує підтвердження».",
                "- На нечіткі підганяльні наміри («надішли ще раз», «надсилаю запит», «досі не отримав»), якщо бракує ключових параметрів (need/type/questDefName/defName), спершу перепитай і уточни, а не заявляй, що вже подано.",
                "- Лише adjust_goodwill може прямо змінювати прихильність за тоном розмови чи контекстом.",
                "- Коли request_caravan і request_aid вдаються, система вже сама знімає фіксовану прихильність; не викликай adjust_goodwill додатково, щоб повторити цю вартість.",
                "- Коли create_quest вдається, система вже сама застосовує фіксовані -10 прихильності; не викликай adjust_goodwill додатково, щоб повторити цю вартість.",
                "- Не вигадуй точного часу прибуття, координат, частот, переліку вантажу чи підтверджень, якщо їх прямо нема в наданих фактах.",
                "- Якщо видимий текст стосується конкретного переліку вантажу, точного часу, координат завдання чи інших перевірних деталей, але цього ходу підтверджених фактів нема — негайно перепиши це як намір рівня «організовується» чи «уточнюється».",
                "- Обовʼязковий рівень формулювань у дипломатичному каналі: можна висловлювати лише планові обіцянки на кшталт «організую караван / завдання / підтримку», розкривати подробиці виконання заборонено.",
                "- reject_request використовується лише для формальної відмови на явний запит гравця. Звичайні розбіжності чи обережні відповіді слід висловлювати природною відмовою в ролі, без цієї дії.",
                "- publish_public_post — дія високого впливу, звернена до світу; користуйся нею обережно й не для щоденних балачок чи приватного торгу.",
                "- Коротка малоінформативна відповідь сама по собі не вимагає негайно змінювати статус присутності. Якщо нема явного докучання, виходу за межі чи різкої ворожості — згортай розмову поступово."
            };

            foreach (string line in lines)
            {
                sb.AppendLine(line);
            }
        }

        internal void AppendOutputSpecificationAuthorityTemplate(StringBuilder sb, string jsonTemplate)
        {
            sb.AppendLine("原始 JSON 模板：");
            sb.AppendLine(jsonTemplate);
            sb.AppendLine();
        }

        internal void AppendCompactActionCatalog(StringBuilder sb, List<ApiActionConfig> availableActions)
        {
            DiplomacyPromptCompactActionOps.AppendCompactActionCatalog(this, sb, availableActions);
        }

        internal string BuildCompactActionLine(ApiActionConfig action)
        {
            return DiplomacyPromptCompactActionOps.BuildCompactActionLine(this, action);
        }

        internal string BuildCompactActionParameterHint(string actionName)
        {
            return DiplomacyPromptCompactActionOps.BuildCompactActionParameterHint(this, actionName);
        }

        internal string BuildCompactActionRequirementHint(ApiActionConfig action)
        {
            return DiplomacyPromptCompactActionOps.BuildCompactActionRequirementHint(this, action);
        }

        internal string BuildCompactActionDescriptionHint(ApiActionConfig action)
        {
            return DiplomacyPromptCompactActionOps.BuildCompactActionDescriptionHint(this, action);
        }

        internal string NormalizeCompactActionText(string text, int maxChars)
        {
            return DiplomacyPromptCompactActionOps.NormalizeCompactActionText(this, text, maxChars);
        }

        internal string MergeMakePeaceRequirement(string configured)
        {
            return DiplomacyPromptCompactActionOps.MergeMakePeaceRequirement(this, configured);
        }

        internal bool ContainsSincerityConstraint(string text)
        {
            return DiplomacyPromptCompactActionOps.ContainsSincerityConstraint(this, text);
        }

        internal void AppendStrategySuggestionGuidance(StringBuilder sb)
        {
            sb.AppendLine("策略建议（可选）：");
            sb.AppendLine("- Додавати strategy_suggestions можна лише коли доступна стратегічна здатність, і рівно 3 пункти.");
            sb.AppendLine("- 每项格式必须为 {\"strategy_name\":\"\",\"reason\":\"\",\"content\":\"\"}。");
            sb.AppendLine("- Зміст має спиратися на факти, лишатися стислим і бути тільки в JSON.");
            sb.AppendLine("- Заборонено друкувати марковані списки стратегій у видимій репліці.");
            sb.AppendLine();
        }

        internal void AppendSendImageTemplateGuidance(StringBuilder sb, List<ApiActionConfig> availableActions)
        {
        }

        internal string ResolveSendImageCaptionStylePrompt()
        {
            var settings = RelationsMod.Instance?.InstanceSettings ?? RelationsMod.Settings;
            string configured = (settings?.SendImageCaptionStylePrompt ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured;
            }

            return PromptTextConstants.SendImageCaptionStylePromptDefault;
        }

        internal string ResolveCurrentGameLanguageLabel()
        {
            string native = LanguageDatabase.activeLanguage?.FriendlyNameNative;
            if (!string.IsNullOrWhiteSpace(native))
            {
                return native;
            }

            string english = LanguageDatabase.activeLanguage?.FriendlyNameEnglish;
            return string.IsNullOrWhiteSpace(english) ? "English" : english;
        }

        internal List<ImageTemplatePromptHint> GetEnabledImageTemplateHintsForPrompt()
        {
            var settings = RelationsMod.Instance?.InstanceSettings;
            if (settings == null)
            {
                return new List<ImageTemplatePromptHint>();
            }

            List<PromptUnifiedTemplateAliasConfig> aliases = settings.GetPromptTemplateAliases(
                RimTalkPromptEntryChannelCatalog.ImageGeneration);
            return aliases
                .Where(item => item != null && item.Enabled && !string.IsNullOrWhiteSpace(item.TemplateId))
                .GroupBy(item => item.TemplateId.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    PromptUnifiedTemplateAliasConfig template = group.First();
                    string description = (template.Description ?? string.Empty).Trim();
                    string fallback = (template.Name ?? string.Empty).Trim();
                    string hint = string.IsNullOrWhiteSpace(description)
                        ? (string.IsNullOrWhiteSpace(fallback) ? "No description." : fallback)
                        : description;

                    return new ImageTemplatePromptHint
                    {
                        Id = group.Key,
                        Hint = hint
                    };
                })
                .ToList();
        }

        internal sealed class ImageTemplatePromptHint
        {
            public string Id;
            public string Hint;
        }

        internal void AppendPresenceActionGuidance(StringBuilder sb, List<ApiActionConfig> availableActions)
        {
            if (availableActions == null)
            {
                return;
            }

            bool hasPresenceActions = availableActions.Any(a =>
                string.Equals(a.ActionName, "exit_dialogue", StringComparison.Ordinal) ||
                string.Equals(a.ActionName, "go_offline", StringComparison.Ordinal) ||
                string.Equals(a.ActionName, "set_dnd", StringComparison.Ordinal));

            if (!hasPresenceActions)
            {
                return;
            }

            sb.AppendLine("PRESENCE ACTION GUIDANCE:");
            sb.AppendLine("- Brief low-information replies can receive a short in-character answer without ending the conversation.");
            sb.AppendLine("- exit_dialogue: use for natural closure or repeated low-information replies after you have already responded.");
            sb.AppendLine("- go_offline / set_dnd: use for harassment, repeated boundary crossing, serious offense, or when you are clearly ending contact.");
            sb.AppendLine("- If you use a presence action, include a short in-character reason.");
            sb.AppendLine();
        }

        internal List<ApiActionConfig> GetAvailableActionsForFaction(SystemPromptConfig config, Faction faction)
        {
            if (config?.ApiActions == null)
            {
                return new List<ApiActionConfig>();
            }

            var enabledActions = config.ApiActions
                .Where(a => a.IsEnabled && Owner.IsPromptActionAllowedInCurrentBuild(a.ActionName))
                .Select(a => a.Clone())
                .ToList();
            if (faction == null)
            {
                return enabledActions;
            }

            var eligibility = ApiActionEligibilityService.Instance.GetAllowedActions(faction);
            return enabledActions
                .Where(a => !Owner.ShouldHideActionFromPromptByProjectedGoodwill(faction, a.ActionName))
                .Where(a => !eligibility.ContainsKey(a.ActionName) || Owner.ShouldKeepActionVisibleInPrompt(a.ActionName, eligibility[a.ActionName]))
                .ToList();
        }

        internal bool ShouldKeepActionVisibleInPrompt(string actionName, ActionValidationResult eligibility)
        {
            if (eligibility == null)
            {
                return true;
            }

            if (eligibility.Allowed)
            {
                return true;
            }

            return string.Equals(actionName, "request_raid_call_everyone", StringComparison.Ordinal) &&
                   string.Equals(eligibility.Code, "call_everyone_requires_post_raid_escalation", StringComparison.Ordinal);
        }

        internal bool IsPromptActionAllowedInCurrentBuild(string actionName)
        {
            if (string.Equals(actionName, "send_image", StringComparison.Ordinal) && ImageGenerationAvailability.IsBlocked())
            {
                return false;
            }

            return true;
        }

        internal void AppendBlockedActionHints(StringBuilder sb, SystemPromptConfig config, Faction faction)
        {
            if (config?.ApiActions == null || faction == null) return;

            var eligibility = ApiActionEligibilityService.Instance.GetAllowedActions(faction);
            var blocked = config.ApiActions
                .Where(a => a.IsEnabled)
                .Where(a => !string.Equals(a.ActionName, "send_image", StringComparison.Ordinal))
                .Select(a => new
                {
                    a.ActionName,
                    ProjectedGoodwillReason = Owner.GetProjectedGoodwillBlockReason(faction, a.ActionName),
                    Eligibility = eligibility.ContainsKey(a.ActionName) ? eligibility[a.ActionName] : null
                })
                .Where(item => !Owner.ShouldHideBlockedActionHint(item.ActionName, item.Eligibility))
                .Where(item => !string.IsNullOrWhiteSpace(item.ProjectedGoodwillReason) || (item.Eligibility != null && !item.Eligibility.Allowed))
                .ToList();

            if (!blocked.Any()) return;

            sb.AppendLine("=== TEMPORARILY UNAVAILABLE ACTIONS ===");
            sb.AppendLine("Informational only — do NOT avoid ALL actions because some are unavailable.");
            sb.AppendLine("You MUST still use the AVAILABLE actions listed above when the player's intent is actionable.");
            sb.AppendLine();
            foreach (var item in blocked)
            {
                if (!string.IsNullOrWhiteSpace(item.ProjectedGoodwillReason))
                {
                    sb.AppendLine($"- {item.ActionName}: {item.ProjectedGoodwillReason}");
                }
                else if (item.Eligibility.RemainingSeconds > 0)
                {
                    float remainingDays = item.Eligibility.RemainingSeconds / 1000f;
                    sb.AppendLine($"- {item.ActionName}: {item.Eligibility.Message} (Remaining: {remainingDays:F1} days)");
                }
                else
                {
                    sb.AppendLine($"- {item.ActionName}: {item.Eligibility.Message}");
                }
            }
            sb.AppendLine();
        }

        internal bool ShouldHideBlockedActionHint(string actionName, ActionValidationResult eligibility)
        {
            if (!string.Equals(actionName, "request_raid_call_everyone", StringComparison.Ordinal))
            {
                return false;
            }

            if (eligibility == null || eligibility.Allowed)
            {
                return false;
            }

            return string.Equals(
                eligibility.Code,
                "call_everyone_requires_post_raid_escalation",
                StringComparison.Ordinal);
        }

        internal void AppendGoodwillPeacePolicyHints(StringBuilder sb, Faction faction)
        {
            DiplomacyPromptGoodwillPolicyOps.AppendGoodwillPeacePolicyHints(this, sb, faction);
        }

        internal void AppendVeryLowGoodwillPeacePolicy(StringBuilder sb, int goodwill, int peaceTalkOnlyMin)
        {
            DiplomacyPromptGoodwillPolicyOps.AppendVeryLowGoodwillPeacePolicy(this, sb, goodwill, peaceTalkOnlyMin);
        }

        internal void AppendPeaceTalkOnlyPolicy(StringBuilder sb,
            int goodwill,
            int peaceTalkOnlyMin,
            int makePeaceReenabledMin,
            string peaceTalkQuest)
        {
            DiplomacyPromptGoodwillPolicyOps.AppendPeaceTalkOnlyPolicy(this, sb, goodwill, peaceTalkOnlyMin, makePeaceReenabledMin, peaceTalkQuest);
        }

        internal void AppendMakePeaceReenabledPolicy(StringBuilder sb, int goodwill, string peaceTalkQuest)
        {
            DiplomacyPromptGoodwillPolicyOps.AppendMakePeaceReenabledPolicy(this, sb, goodwill, peaceTalkQuest);
        }

        internal bool ShouldHideActionFromPromptByProjectedGoodwill(Faction faction, string actionName)
        {
            return DiplomacyPromptGoodwillPolicyOps.ShouldHideActionFromPromptByProjectedGoodwill(this, faction, actionName);
        }

        internal string GetProjectedGoodwillBlockReason(Faction faction, string actionName)
        {
            return DiplomacyPromptGoodwillPolicyOps.GetProjectedGoodwillBlockReason(this, faction, actionName);
        }

        internal string GetRelationLabel(int goodwill)
        {
            return DiplomacyPromptGoodwillPolicyOps.GetRelationLabel(this, goodwill);
        }

        internal string GetEventIcon(SignificantEventType eventType)
        {
            return DiplomacyPromptGoodwillPolicyOps.GetEventIcon(this, eventType);
        }

        internal string GetEventTypeName(SignificantEventType eventType)
        {
            return DiplomacyPromptGoodwillPolicyOps.GetEventTypeName(this, eventType);
        }

        internal string GetRelationImpression(FactionMemoryEntry memory)
        {
            return DiplomacyPromptGoodwillPolicyOps.GetRelationImpression(this, memory);
        }

        internal string GetRelationTrend(List<RelationSnapshot> history)
        {
            return DiplomacyPromptGoodwillPolicyOps.GetRelationTrend(this, history);
        }


        }

}
