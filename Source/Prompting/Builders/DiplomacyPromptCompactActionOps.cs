using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Text;
using Ustas.RimAI.Communication.Relations.Config;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Prompting.Builders
{
    /// <summary>
    /// Compact action catalog line builders for diplomacy prompt contracts.
    /// </summary>
    internal static class DiplomacyPromptCompactActionOps
    {
        internal static void AppendCompactActionCatalog(DiplomacyPromptBuilderContract owner, StringBuilder sb, List<ApiActionConfig> availableActions)
        {
            if (availableActions == null || availableActions.Count == 0)
            {
                return;
            }

            sb.AppendLine(PromptTextConstants.ActionsHeader);
            sb.AppendLine("Use these actions actively. A response with actions creates gameplay impact; text-only responses do nothing.");
            foreach (ApiActionConfig action in availableActions)
            {
                if (!owner.IsPromptActionAllowedInCurrentBuild(action?.ActionName))
                {
                    continue;
                }

                string line = BuildCompactActionLine(owner, action);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    sb.AppendLine(line);
                }
            }
            sb.AppendLine();
        }

        internal static string BuildCompactActionLine(DiplomacyPromptBuilderContract owner, ApiActionConfig action)
        {
            if (action == null || string.IsNullOrWhiteSpace(action.ActionName))
            {
                return string.Empty;
            }

            string parameters = BuildCompactActionParameterHint(owner, action.ActionName);
            string requirement = BuildCompactActionRequirementHint(owner, action);
            string description = BuildCompactActionDescriptionHint(owner, action);
            string signature = string.IsNullOrWhiteSpace(parameters)
                ? action.ActionName
                : $"{action.ActionName}({parameters})";
            string gate = string.IsNullOrWhiteSpace(requirement)
                ? string.Empty
                : $" [{requirement}]";
            return $"- {signature}{gate}: {description}";
        }

        internal static string BuildCompactActionParameterHint(DiplomacyPromptBuilderContract owner, string actionName)
        {
            switch (actionName)
            {
                case "adjust_goodwill":
                    return "amount, reason";
                case "request_aid":
                    return "type";
                case "declare_war":
                    return "reason";
                case "make_peace":
                    return "cost?";
                case "request_caravan":
                    return "type?(General/BulkGoods/CombatSupplier/Exotic/Slaver)";
                case "request_visitor":
                    return string.Empty;
                case "request_raid":
                    return "strategy?(ImmediateAttack/ImmediateAttackSmart/StageThenAttack/ImmediateAttackSappers/Siege), arrival?(EdgeWalkIn/EdgeDrop/EdgeWalkInGroups/RandomDrop/CenterDrop)";
                case "request_raid_waves":
                    return "waves(2-6)";
                case "request_item_airdrop":
                    return "need, payment_items[{item(спершу defName),count}], scenario?(general/trade/ransom), constraints?, budget_silver?(лише аудит)";
                case "request_info":
                    return "info_type(prisoner)";
                case "pay_prisoner_ransom":
                    return "target_pawn_load_id, offer_silver, payment_mode?(optional; omit or silver only, target missing -> ask selection first)";
                case "trigger_incident":
                    return "defName, amount?";
                case "create_quest":
                    return "questDefName, points?";
                case "send_image":
                    return "template_id, extra_prompt?, caption?, size?, watermark?";
                case "reject_request":
                case "exit_dialogue":
                case "go_offline":
                case "set_dnd":
                    return "reason?";
                case "publish_public_post":
                    return "category, sentiment, summary?";
                default:
                    return string.Empty;
            }
        }

        internal static string BuildCompactActionRequirementHint(DiplomacyPromptBuilderContract owner, ApiActionConfig action)
        {
            string actionName = action?.ActionName;
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return string.Empty;
            }

            string configured = NormalizeCompactActionText(owner, action.Requirement, 120);
            if (actionName == "make_peace")
            {
                return MergeMakePeaceRequirement(owner, configured);
            }

            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured;
            }

            switch (actionName)
            {
                case "request_aid":
                    return "Поріг для допомоги досягнуто";
                case "declare_war":
                    return "Поріг для оголошення війни досягнуто";
                case "make_peace":
                    return "Уже стан війни";
                case "request_caravan":
                    return "Наразі не ворожі";
                case "request_raid":
                    return "Лише у ворожому стані";
                case "create_quest":
                    return "Дозволено лише точні questDefName зі списку доступних";
                case "request_item_airdrop":
                    return "need/payment_items обовʼязкові; бюджет виводиться як Floor від суми payment_items за ринковою ціною (мінімальна ціна 0.01; наявні множники зберігаються: без tradeTags x10, з ExoticMisc x2), budget_silver, якщо він є, потрібен лише для аудиту й у виконанні не бере участі; payment_items.item — спершу defName, label припустимий лише за однозначного збігу; якщо збігу нема, він неоднозначний або бракує запасів — одразу невдача; якщо гравець надав картку скидання з уже точно привʼязаним need_def, цей need_def є жорсткою ціллю: дозволено лише відмовити, перерахувати ціну або змінити кількість чи спосіб оплати, мовчки підміняти товар не можна; якщо ти лише перераховуєш ціну й цього ходу дію не виконуєш — скажи природною реплікою, який товар, скільки, за скільки срібла і коротко чому, без сухого шаблону.";
                case "request_info":
                    return "Підтримується лише info_type=prisoner; використовується тільки тоді, коли даних про ціль викупу бракує (нема чинного target_pawn_load_id)";
                case "pay_prisoner_ransom":
                    return "target_pawn_load_id/offer_silver обовʼязкові; якщо цілі нема або вона недійсна — спершу request_info(prisoner) для вибору; offer_silver має потрапляти в поточний діапазон пропозицій із системної підказки; payment_mode можна не вказувати, але якщо вказано — лише silver (приклад: payment_mode:silver; контрприклад: payment_mode:cash); у звичайному потоці оплата подається один раз; якщо є [RansomBatchSelection] і цього ходу виконується pay_prisoner_ransom — треба тим самим ходом охопити всі цілі списку, а сума має бути в діапазоні для партії; звільнення виконує гравець вручну; якщо в тексті ти пообіцяв, що викуп подано чи сплачено, у тій самій відповіді має бути дія pay_prisoner_ransom";
                case "send_image":
                    return "Потрібен API зображень + обовʼязковий template_id + одне зображення на хід";
                case "publish_public_post":
                    return "Публічно, звернено до світу, вмикати обережно";
                case "reject_request":
                    return "Лише для формальної відмови на явний запит";
                default:
                    return string.Empty;
            }
        }

        internal static string BuildCompactActionDescriptionHint(DiplomacyPromptBuilderContract owner, ApiActionConfig action)
        {
            string actionName = action?.ActionName;
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return string.Empty;
            }

            string configured = NormalizeCompactActionText(owner, action.Description, 180);
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured;
            }

            switch (actionName)
            {
                case "adjust_goodwill":
                    return "Змінити відносини з фракцією";
                case "request_aid":
                    return "Організувати допомогу (після успіху система знімає фіксовану прихильність)";
                case "declare_war":
                    return "Перейти до стану війни";
                case "make_peace":
                    return "Пропонувати мир лише за дуже високої щирості гравця";
                case "request_caravan":
                    return "Організувати торговий караван (після успіху система знімає фіксовану прихильність)";
                case "request_raid":
                    return "Організувати напад";
                case "request_item_airdrop":
                    return "Знайти справжній ThingDef і надіслати товар Top1 звичайним скиданням";
                case "request_info":
                    return "Дані, потрібні перед виконанням запиту (наразі лише вибір бранця для викупу)";
                case "pay_prisoner_ransom":
                    return "Подати разову оплату викупу сріблом і зареєструвати угоду; звільнення виконує гравець вручну";
                case "trigger_incident":
                    return "Запустити ігрову подію";
                case "create_quest":
                    return "Створити рідне завдання (після успіху система знімає фіксовані -10 прихильності)";
                case "send_image":
                    return "Згенерувати й повернути дипломатичну картку із зображенням через API зображень";
                case "reject_request":
                    return "Формальна відмова на явний запит гравця";
                case "publish_public_post":
                    return "Опублікувати публічний допис високого впливу";
                case "exit_dialogue":
                    return "Завершити поточну тему";
                case "go_offline":
                    return "Піти й перейти в офлайн";
                case "set_dnd":
                    return "Припинити подальші контакти";
                default:
                    return actionName;
            }
        }

        internal static string NormalizeCompactActionText(DiplomacyPromptBuilderContract owner, string text, int maxChars)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string normalized = Regex.Replace(text, "\\s+", " ").Trim();
            if (normalized.Length <= maxChars)
            {
                return normalized;
            }

            return normalized.Substring(0, maxChars).TrimEnd() + "...";
        }

        internal static string MergeMakePeaceRequirement(DiplomacyPromptBuilderContract owner, string configured)
        {
            const string hardRule = "Уже стан війни + лише за дуже високої щирості";
            if (string.IsNullOrWhiteSpace(configured))
            {
                return hardRule;
            }

            if (ContainsSincerityConstraint(owner, configured))
            {
                return configured;
            }

            return configured + "; лише за дуже високої щирості";
        }

        internal static bool ContainsSincerityConstraint(DiplomacyPromptBuilderContract owner, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string lower = text.ToLowerInvariant();
            return lower.Contains("sincer") || text.Contains("щирість") || text.Contains("добра воля");
        }
    }
}
