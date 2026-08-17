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
    internal sealed partial class DiplomacyPromptBuilder
    {        internal void AppendSimpleConfig(StringBuilder sb, SystemPromptConfig config, Faction faction)
        {
            AppendCompactDiplomacyResponseContract(sb, config, faction);
        }

        internal void AppendAdvancedConfig(StringBuilder sb, SystemPromptConfig config, Faction faction)
        {
            AppendCompactDiplomacyResponseContract(sb, config, faction);
        }

        internal void AppendCompactDiplomacyResponseContract(StringBuilder sb, SystemPromptConfig config, Faction faction)
        {
            List<ApiActionConfig> availableActions = GetAvailableActionsForFaction(config, faction);
            AppendOutputSpecificationAuthoritySection(sb, config);
            AppendDiplomacyResponseFormatSection(sb, config);
            AppendDiplomacyCriticalActionRules(sb);
            AppendCompactActionCatalog(sb, availableActions);
            AppendBlockedActionHints(sb, config, faction);
            AppendGoodwillPeacePolicyHints(sb, faction);
            AppendPresenceActionGuidance(sb, availableActions);
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

            AppendStrictJsonFormatPreamble(sb);
            sb.AppendLine(PromptTextConstants.OutputSpecificationAuthorityHeader);
            sb.AppendLine(PromptTextConstants.OutputSpecificationAuthorityReference);
            AppendOutputSpecificationAuthorityRules(sb);
            AppendOutputSpecificationAuthorityTemplate(sb, jsonTemplate);
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
                "- 若存在 actions，则必填键为 actions[].action；actions[].parameters 可选。",
                "- visible_dialogue 只能包含角色内可见对白，不得包含思维链、解释、标题、规则复述或调试信息。",
                PromptTextConstants.OutputSpecificationAuthorityLegacyRule,
                PromptTextConstants.OutputSpecificationAuthorityBoundaryRule,
                PromptTextConstants.OutputSpecificationAuthorityHistoryStyleRule,
                "- [Caravan role direction -- highest priority] When you dispatch request_caravan, the caravan travels to the player colony. You (the AI faction) are the SELLER: the caravan carries goods from YOUR faction's trade list for the player to buy with silver. The player is the BUYER. Direction is NEVER reversible -- you are NOT buying from the player, and the player is NOT selling to the caravan.",
                "- [Direction constraint -- highest priority] need is what YOU (the AI faction) sell and airdrop to the PLAYER colony (AI -> player). payment_items is what the PLAYER pays to YOU, deducted from player beacon inventory (player -> AI). The direction of need and payment_items is NEVER reversible -- you are NOT the buyer. If the player says they want 1000 wood, need is 1000 wood (YOU send to PLAYER), NOT something you receive.",
                "- 除非同条回复包含匹配 actions 动作，否则禁止把 gameplay 效果叙述为“已执行”。",
                "- request_caravan/request_visitor/request_aid/request_raid/request_item_airdrop/request_info/pay_prisoner_ransom/create_quest/trigger_incident 属于延迟或系统调度动作；表述应是意图或安排，不是已到达/已完成结果。",
                "- 物资交换/发送常识：能且只能通过 request_item_airdrop 实现即时物资交换；request_caravan 属于延时交易。",
                "- 空投交易硬约束：单次 request_item_airdrop 只能一种物品换一种物品（一个 need 对应一组 payment_items）。禁止在 need 中写多种物品（如“1000原木和50钢铁”），禁止在 payment_items 中混入需求物资。need 只能包含一种物品及其数量。",
                "- need 字段必须忠实反映玩家需求：若玩家消息中包含明确数量（如“1000原木”“50个钢铁”），need 必须携带该数量（格式：数字+物品名），禁止忽略玩家指定的数量。",
                "- payment_items 格式：数组，每项必须同时含 item（string）和 count（正整数）。示例：[{\"item\":\"Silver\",\"count\":220}]。缺失 item 或 count 将导致动作执行失败。",
                "- 若玩家准确命中你掌握的交易事实（库存、价格区间、需求），可在不违背成本底线时考虑让步并打折。",
                "- 商队硬约束：request_caravan 派出的商队所携带的物资由派系交易清单决定，你无法指定、修改或承诺商队携带的具体物资。玩家也无法通过商队请求指定物资交易。当玩家要求特定物资时，必须引导其改用 request_item_airdrop（空投可指定物资）。request_caravan 的合法类型只有 General / BulkGoods / CombatSupplier / Exotic / Slaver；必须使用这些精确值之一，不能自造值。",
                "- 轨道商硬约束：轨道商不具备地面定居点履约能力，禁止承诺“带着指定物资进入我们的据点/定居点完成订单”。若玩家提出这类需求，只能解释限制并引导改走 request_item_airdrop。",
                "- 通信语境硬约束：当前是通信终端在线聊天，不是线下会面；禁止写“我已到场/当面处理/带人离开”。",
                "- 赎金语义约束：仅在缺少有效 target_pawn_load_id 时使用 request_info(info_type=prisoner)；目标已明确时可直接 pay_prisoner_ransom。",
                "- 若可见文本出现“我会安排/我已提交/这就派出/马上下单”等明确执行承诺，必须同条回复附带匹配的 {\"actions\":[...]}；否则必须改写为澄清提问或不确定表达。",
                "- 当玩家消息包含空投交易信息卡字段（need/count/payment_items/scenario）且信息卡已精确绑定 need_def 时，该 need_def 是强绑定执行目标；你可以拒绝、重报价，或调整数量/付款方式，但不得静默改成别的物资。",
                "- 若你选择重报价且本轮不执行动作，请用沉浸式自然对白表达，但必须在可见文本中明确说出目标物资、数量、银币价格，且最好补一句简短原因（例如库存、风险、路程、损耗）；避免使用生硬的“重报价: item=... count=... silver=...”硬编码句式。",
                "- 若你决定执行 request_item_airdrop，且当前存在交易卡绑定的 need_def，则动作中的 need 必须仍指向该物资；若你想改物资，只能先自然语言提出更换并等待玩家重新选品或重新提交交易卡。",
                "- 赎金专用硬约束：若文本出现“已提交/已支付/钱货两清/已放人离开”等完成态措辞，必须同条包含 pay_prisoner_ransom；否则必须回退为待确认措辞。",
                "- 对“再发一次/发送请求/还是没收到”等催单型模糊意图，若缺少关键参数（need/type/questDefName/defName），优先追问确认，不得直接宣称已提交。",
                "- 只有 adjust_goodwill 可根据对话语气或上下文直接改变好感。",
                "- request_caravan 与 request_aid 成功时系统已自动扣除固定好感，不要额外调用 adjust_goodwill 去重复表达成本。",
                "- create_quest 成功时系统已自动应用固定 -10 好感，不要额外调用 adjust_goodwill 去重复表达发布成本。",
                "- 除非提示事实中明确提供，否则不要编造精确到达时间、坐标、频率、货物清单或确认信息。",
                "- 若可见文本涉及具体货物清单、精确时间、任务坐标或其他可核验细节，但本轮未提供可验证事实支撑，必须立即改写为“安排中”或“待确认”的意图级表达。",
                "- 外交通道强制意图级口径：仅可表达“我会安排商队/任务/支援”等计划性承诺，禁止透露详细执行细节。",
                "- reject_request 仅用于正式拒绝玩家的明确请求。普通分歧或谨慎回应应以角色内自然拒绝，不附带该动作。",
                "- publish_public_post 属于高影响的世界面向动作，应谨慎使用，不用于日常闲聊或私下讨价还价。",
                "- 简短低信息回复本身不要求立即触发在线状态动作。除非存在明显骚扰、越界或强敌意，否则应渐进式收束。"
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
            if (availableActions == null || availableActions.Count == 0)
            {
                return;
            }

            sb.AppendLine(PromptTextConstants.ActionsHeader);
            sb.AppendLine("Use these actions actively. A response with actions creates gameplay impact; text-only responses do nothing.");
            foreach (ApiActionConfig action in availableActions)
            {
                if (!IsPromptActionAllowedInCurrentBuild(action?.ActionName))
                {
                    continue;
                }

                string line = BuildCompactActionLine(action);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    sb.AppendLine(line);
                }
            }
            sb.AppendLine();
        }

        internal string BuildCompactActionLine(ApiActionConfig action)
        {
            if (action == null || string.IsNullOrWhiteSpace(action.ActionName))
            {
                return string.Empty;
            }

            string parameters = BuildCompactActionParameterHint(action.ActionName);
            string requirement = BuildCompactActionRequirementHint(action);
            string description = BuildCompactActionDescriptionHint(action);
            string signature = string.IsNullOrWhiteSpace(parameters)
                ? action.ActionName
                : $"{action.ActionName}({parameters})";
            string gate = string.IsNullOrWhiteSpace(requirement)
                ? string.Empty
                : $" [{requirement}]";
            return $"- {signature}{gate}: {description}";
        }

        internal string BuildCompactActionParameterHint(string actionName)
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

        internal string BuildCompactActionRequirementHint(ApiActionConfig action)
        {
            string actionName = action?.ActionName;
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return string.Empty;
            }

            string configured = NormalizeCompactActionText(action.Requirement, 120);
            if (actionName == "make_peace")
            {
                return MergeMakePeaceRequirement(configured);
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

        internal string BuildCompactActionDescriptionHint(ApiActionConfig action)
        {
            string actionName = action?.ActionName;
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return string.Empty;
            }

            string configured = NormalizeCompactActionText(action.Description, 180);
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

        internal string NormalizeCompactActionText(string text, int maxChars)
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

        internal string MergeMakePeaceRequirement(string configured)
        {
            const string hardRule = "已处于战争状态 + 仅限很高诚意";
            if (string.IsNullOrWhiteSpace(configured))
            {
                return hardRule;
            }

            if (ContainsSincerityConstraint(configured))
            {
                return configured;
            }

            return configured + "；仅限很高诚意";
        }

        internal bool ContainsSincerityConstraint(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string lower = text.ToLowerInvariant();
            return lower.Contains("sincer") || text.Contains("真诚") || text.Contains("诚意");
        }

        internal void AppendStrategySuggestionGuidance(StringBuilder sb)
        {
            sb.AppendLine("策略建议（可选）：");
            sb.AppendLine("- 仅当策略能力可用时，才可添加 strategy_suggestions，且必须正好 3 项。");
            sb.AppendLine("- 每项格式必须为 {\"strategy_name\":\"\",\"reason\":\"\",\"content\":\"\"}。");
            sb.AppendLine("- 内容要基于事实、保持紧凑，并且只出现在 JSON 中。");
            sb.AppendLine("- 禁止在可见对白中打印策略项目符号列表。");
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
                .Where(a => a.IsEnabled && IsPromptActionAllowedInCurrentBuild(a.ActionName))
                .Select(a => a.Clone())
                .ToList();
            if (faction == null)
            {
                return enabledActions;
            }

            var eligibility = ApiActionEligibilityService.Instance.GetAllowedActions(faction);
            return enabledActions
                .Where(a => !ShouldHideActionFromPromptByProjectedGoodwill(faction, a.ActionName))
                .Where(a => !eligibility.ContainsKey(a.ActionName) || ShouldKeepActionVisibleInPrompt(a.ActionName, eligibility[a.ActionName]))
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
                    ProjectedGoodwillReason = GetProjectedGoodwillBlockReason(faction, a.ActionName),
                    Eligibility = eligibility.ContainsKey(a.ActionName) ? eligibility[a.ActionName] : null
                })
                .Where(item => !ShouldHideBlockedActionHint(item.ActionName, item.Eligibility))
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
            if (sb == null || faction == null)
            {
                return;
            }

            if (faction.RelationKindWith(Faction.OfPlayer) != FactionRelationKind.Hostile)
            {
                return;
            }

            int goodwill = faction.PlayerGoodwill;
            if (goodwill > 0)
            {
                return;
            }

            const int peaceTalkOnlyMin = -50;
            const int makePeaceReenabledMin = -20;
            const string peaceTalkQuest = "OpportunitySite_PeaceTalks";

            sb.AppendLine(PromptTextConstants.GoodwillPeacePolicyHeader);
            if (goodwill < peaceTalkOnlyMin)
            {
                AppendVeryLowGoodwillPeacePolicy(sb, goodwill, peaceTalkOnlyMin);
            }
            else if (goodwill < makePeaceReenabledMin)
            {
                AppendPeaceTalkOnlyPolicy(sb, goodwill, peaceTalkOnlyMin, makePeaceReenabledMin, peaceTalkQuest);
            }
            else
            {
                AppendMakePeaceReenabledPolicy(sb, goodwill, peaceTalkQuest);
            }
            sb.AppendLine();
        }

        internal void AppendVeryLowGoodwillPeacePolicy(StringBuilder sb, int goodwill, int peaceTalkOnlyMin)
        {
            sb.AppendLine(string.Format(PromptTextConstants.GoodwillPeacePolicyVeryLowLine1, goodwill));
            sb.AppendLine(string.Format(PromptTextConstants.GoodwillPeacePolicyVeryLowLine2, peaceTalkOnlyMin));
        }

        internal void AppendPeaceTalkOnlyPolicy(
            StringBuilder sb,
            int goodwill,
            int peaceTalkOnlyMin,
            int makePeaceReenabledMin,
            string peaceTalkQuest)
        {
            sb.AppendLine(string.Format(PromptTextConstants.GoodwillPeacePolicyTalkOnlyLine1, goodwill));
            sb.AppendLine(string.Format(PromptTextConstants.GoodwillPeacePolicyTalkOnlyLine2, peaceTalkQuest));
            sb.AppendLine(string.Format(
                PromptTextConstants.GoodwillPeacePolicyTalkOnlyLine3,
                peaceTalkOnlyMin,
                makePeaceReenabledMin - 1));
        }

        internal void AppendMakePeaceReenabledPolicy(StringBuilder sb, int goodwill, string peaceTalkQuest)
        {
            sb.AppendLine(string.Format(PromptTextConstants.GoodwillPeacePolicyReenabledLine1, goodwill));
            sb.AppendLine(string.Format(PromptTextConstants.GoodwillPeacePolicyReenabledLine2, peaceTalkQuest));
        }

        internal bool ShouldHideActionFromPromptByProjectedGoodwill(Faction faction, string actionName)
        {
            return false;
        }

        internal string GetProjectedGoodwillBlockReason(Faction faction, string actionName)
        {
            return string.Empty;
        }

        internal string GetRelationLabel(int goodwill)
        {
            if (goodwill >= 80) return "Ally";
            if (goodwill >= 40) return "Friend";
            if (goodwill >= 0) return "Neutral";
            if (goodwill >= -40) return "Hostile";
            return "Enemy";
        }

        internal string GetEventIcon(SignificantEventType eventType)
        {
            return eventType switch
            {
                SignificantEventType.WarDeclared => "⚔️",
                SignificantEventType.PeaceMade => "🕊️",
                SignificantEventType.TradeCaravan => "📦",
                SignificantEventType.GiftSent => "🎁",
                SignificantEventType.AidRequested => "🆘",
                SignificantEventType.QuestIssued => "📜",
                SignificantEventType.GoodwillChanged => "📊",
                SignificantEventType.AllianceFormed => "🤝",
                SignificantEventType.Betrayal => "🗡️",
                _ => "📌"
            };
        }

        internal string GetEventTypeName(SignificantEventType eventType)
        {
            return eventType switch
            {
                SignificantEventType.WarDeclared => "宣战",
                SignificantEventType.PeaceMade => "议和",
                SignificantEventType.TradeCaravan => "贸易商队",
                SignificantEventType.GiftSent => "赠送礼物",
                SignificantEventType.AidRequested => "请求援助",
                SignificantEventType.QuestIssued => "发布任务",
                SignificantEventType.GoodwillChanged => "好感度变化",
                SignificantEventType.AllianceFormed => "结盟",
                SignificantEventType.Betrayal => "背叛",
                _ => "事件"
            };
        }

        internal string GetRelationImpression(FactionMemoryEntry memory)
        {
            if (memory.NegativeInteractions > memory.PositiveInteractions * 2)
            {
                return "危险的敌人，多次敌对行为让我们充满警惕";
            }
            else if (memory.NegativeInteractions > memory.PositiveInteractions)
            {
                return "关系紧张，存在较多冲突";
            }
            else if (memory.PositiveInteractions > memory.NegativeInteractions * 2)
            {
                return "可靠的盟友，长期友好合作建立了信任";
            }
            else if (memory.PositiveInteractions > memory.NegativeInteractions)
            {
                return "友好的派系，互动以合作为主";
            }
            else
            {
                return "关系复杂，既有合作也有冲突";
            }
        }

        internal string GetRelationTrend(List<RelationSnapshot> history)
        {
            if (history.Count < 2) return string.Empty;

            var recent = history.Skip(Math.Max(0, history.Count - 3)).ToList();
            if (recent.Count < 2) return string.Empty;

            int firstGoodwill = recent.First().Goodwill;
            int lastGoodwill = recent.Last().Goodwill;
            int change = lastGoodwill - firstGoodwill;

            if (change > 10) return "关系显著改善 ↑";
            else if (change > 0) return "关系缓慢改善 ↑";
            else if (change < -10) return "关系急剧恶化 ↓";
            else if (change < 0) return "关系缓慢恶化 ↓";
            else return "关系稳定 →";
        }
    }
}
