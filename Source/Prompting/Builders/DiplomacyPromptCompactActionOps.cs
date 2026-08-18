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
                    return "need, payment_items[{item(defName优先),count}], scenario?(general/trade/ransom), constraints?, budget_silver?(仅审计)";
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
                    return "满足援助阈值";
                case "declare_war":
                    return "满足宣战阈值";
                case "make_peace":
                    return "已处于战争状态";
                case "request_caravan":
                    return "当前非敌对";
                case "request_raid":
                    return "仅限敌对状态";
                case "create_quest":
                    return "仅允许使用可用列表中的精确 questDefName";
                case "request_item_airdrop":
                    return "need/payment_items 必填；预算由 payment_items 按市场价求和后 Floor 派生（最低价 0.01；保留既有倍率：无 tradeTags 时 x10，含 ExoticMisc 时 x2），budget_silver 若存在仅用于审计且不参与执行；payment_items.item 优先 defName、label 仅在可唯一匹配时可用；找不到匹配/歧义/库存不足直接失败；若玩家给出已精确绑定 need_def 的空投信息卡，则 need_def 为强绑定目标，只允许拒绝、重报价或调整数量/付款方式，不允许静默改物资；若只做重报价且本轮不执行动作，请用自然对白明确说出物资、数量、银币价格与简短原因，不要输出生硬模板。";
                case "request_info":
                    return "仅支持 info_type=prisoner；仅在赎金目标信息不足（缺少有效 target_pawn_load_id）时使用";
                case "pay_prisoner_ransom":
                    return "target_pawn_load_id/offer_silver 必填；缺少或失效目标时先 request_info(prisoner) 选人；offer_silver 必须落在系统提示的当前可报价区间内；payment_mode 可省略，若提供必须是 silver（示例：payment_mode:silver；反例：payment_mode:cash）；常规流程仅执行一次付款提交；若存在 [RansomBatchSelection] 且本轮执行 pay_prisoner_ransom，必须同轮覆盖列表全部目标且总报价在批量区间内；放人由玩家手动操作；若文本承诺已提交/已支付赎金，必须同条携带 pay_prisoner_ransom 动作";
                case "send_image":
                    return "需配置图片 API + 必填 template_id + 每回合仅一张";
                case "publish_public_post":
                    return "公开、面向世界且谨慎触发";
                case "reject_request":
                    return "仅用于明确请求的正式拒绝";
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
                    return "调整派系关系";
                case "request_aid":
                    return "安排援助（成功后系统固定扣除好感）";
                case "declare_war":
                    return "切换为战争状态";
                case "make_peace":
                    return "仅在玩家诚意很高时提出和平";
                case "request_caravan":
                    return "安排贸易商队（成功后系统固定扣除好感）";
                case "request_raid":
                    return "安排袭击";
                case "request_item_airdrop":
                    return "检索真实 ThingDef 并通过原版空投发送 Top1 物资";
                case "request_info":
                    return "请求执行前所需信息（当前仅囚犯赎金选人）";
                case "pay_prisoner_ransom":
                    return "提交单次银币赎金支付并登记合约，放人由玩家手动操作";
                case "trigger_incident":
                    return "触发游戏事件";
                case "create_quest":
                    return "创建原生任务（成功后系统固定 -10 好感）";
                case "send_image":
                    return "通过图片 API 生成并返回一张外交图片卡";
                case "reject_request":
                    return "正式拒绝玩家的明确请求";
                case "publish_public_post":
                    return "发布高影响力公开社交动态";
                case "exit_dialogue":
                    return "结束当前话题";
                case "go_offline":
                    return "离开并切到离线";
                case "set_dnd":
                    return "停止后续联系";
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
            const string hardRule = "已处于战争状态 + 仅限很高诚意";
            if (string.IsNullOrWhiteSpace(configured))
            {
                return hardRule;
            }

            if (ContainsSincerityConstraint(owner, configured))
            {
                return configured;
            }

            return configured + "；仅限很高诚意";
        }

        internal static bool ContainsSincerityConstraint(DiplomacyPromptBuilderContract owner, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string lower = text.ToLowerInvariant();
            return lower.Contains("sincer") || text.Contains("真诚") || text.Contains("诚意");
        }
    }
}
