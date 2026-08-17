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
    {        internal void AppendApiLimits(StringBuilder sb, Faction faction = null)
        {
            var settings = RelationsMod.Settings ?? RelationsMod.Instance?.InstanceSettings;
            if (settings == null) return;

            sb.AppendLine();
            sb.AppendLine("=== 当前 API 限制（必须遵守） ===");

            // Check current cooldown for specific faction
            if (faction != null)
            {
                int questCooldownSec = GameAIInterface.Instance.GetRemainingCooldownSeconds(faction, "CreateQuest");
                if (questCooldownSec > 0)
                {
                    // GameAIInterface.GetRemainingCooldownSeconds returns total remaining seconds (ticks/60)
                    // One RimWorld day is 60,000 ticks = 1000 seconds.
                    float remainingDays = questCooldownSec / 1000f;
                    sb.AppendLine($"- [关键] {faction.Name} 的 create_quest 当前处于冷却中。剩余：{remainingDays:F1} 天。冷却结束前禁止创建任何任务/委托。若玩家请求任务，你必须拒绝并以角色内理由说明（例如整备中、资源补充中、或先前承诺尚在执行）。");
                }
            }

            sb.AppendLine($"- 单次好感调整上限：{settings.MaxGoodwillAdjustmentPerCall}（范围：0 到 {settings.MaxGoodwillAdjustmentPerCall}）");
            sb.AppendLine($"- 每日好感调整上限：{settings.MaxDailyGoodwillAdjustment}");
            sb.AppendLine($"- 好感冷却：{settings.GoodwillCooldownTicks / 2500f:F1} 小时");
            sb.AppendLine($"- 请求援助最低好感：{settings.MinGoodwillForAid}");
            sb.AppendLine($"- 宣战最大好感阈值：{settings.MaxGoodwillForWarDeclaration}");
            sb.AppendLine($"- 和平费用上限：{settings.MaxPeaceCost}");
            sb.AppendLine($"- 和平后的好感重置值：{settings.PeaceGoodwillReset}");
            sb.AppendLine($"- create_quest 冷却：{settings.MinQuestCooldownDays} 到 {settings.MaxQuestCooldownDays} 天");
            sb.AppendLine();
            sb.AppendLine("已启用功能：");
            sb.AppendLine($"- 好感调整：{(settings.EnableAIGoodwillAdjustment ? "是" : "否")}");
            sb.AppendLine($"- 宣战：{(settings.EnableAIWarDeclaration ? "是" : "否")}");
            sb.AppendLine($"- 和平：{(settings.EnableAIPeaceMaking ? "是" : "否")}");
            sb.AppendLine($"- 贸易商队：{(settings.EnableAITradeCaravan ? "是" : "否")}");
            sb.AppendLine($"- 请求援助：{(settings.EnableAIAidRequest ? "是" : "否")}");
            sb.AppendLine("- 任务创建：是");
            sb.AppendLine();

            // Airdrop trade rules
            if (settings.EnableAIItemAirdrop && faction != null)
            {
                AppendAirdropTradeRules(sb, faction);
            }
        }

        internal void AppendAirdropTradeRules(StringBuilder sb, Faction faction)
        {
            float wealthItems = Find.AnyPlayerHomeMap?.wealthWatcher?.WealthItems ?? 0f;
            float factionTradeTotalSilver = GameAIInterface.Instance.GetAirdropFactionTradeTotalForPolicy(faction);
            AirdropTradeRuleSnapshot rule = ItemAirdropTradePolicy.ResolveRuleSnapshot(faction, wealthItems, factionTradeTotalSilver);
            TechLevel techLevel = faction.def?.techLevel ?? TechLevel.Archotech;

            sb.AppendLine("=== 空投以物易物规则（必须遵守） ===");
            sb.AppendLine($"- 派系科技等级：{techLevel}。禁止交易科技等级高于此的商品。");
            sb.AppendLine($"- 当前好感度：{rule.Goodwill}。交易总额上限：{rule.TradeLimitSilver} 银币。");
            sb.AppendLine($"- 交易限额规则：{rule.TradeLimitRuleText}。");
            sb.AppendLine($"- 每个空投仓运费：{rule.ShippingCostPerPod} 银币。运费从玩家出价中扣除，不在报价中单独列出。");
            sb.AppendLine("- [Role -- highest priority economic constraint] You are the SELLER/SUPPLIER: the need field is what you sell and air-drop DELIVER TO the player (you ship -> player receives). The player is the BUYER/PAYER: payment_items is what the player pays to you (player ships -> you receive). Direction is NEVER reversible -- you are NOT the buyer, the player is NOT the supplier.");
            sb.AppendLine("- 需求物资与支付物资都按市场价计算（ThingDef.BaseMarketValue，最低按 0.01）。");
            sb.AppendLine("- 需求物资倍率规则：tradeTags 包含 ExoticMisc 时 x3.0，其余物资 x1.6；金银仍按市场价固定计算。");
            sb.AppendLine("- 支付物资倍率规则：除金银外统一按市场价 x0.6 计算；金银仍按市场价固定计算。");
            sb.AppendLine("- 特殊商品倍率覆盖：若交易卡标记为 special_item_discount，该商品按 x0.4 倍率计价（折扣优惠）；若标记为 special_item_scarce，按 x2.0 倍率计价（稀缺加价）。特殊倍率优先于通用倍率。");
            AppendFactionSpecialItemInventory(sb, faction);
            sb.AppendLine("- 允许在市场价基础上溢价（紧急以物易物场景）。若玩家出价低于参考价，应拒绝或还价。");
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
                    sb.AppendLine($"- 当前折扣商品：{itemSet.DiscountItem.Label}（参考单价 {discountPrice:F1}，special_item_discount）");
                }
                else
                {
                    sb.AppendLine($"- 当前折扣商品：{itemSet.DiscountItem.Label}（special_item_discount）");
                }
                hasAny = true;
            }
            if (itemSet.ScarceItem != null && itemSet.ScarceItem.IsAvailable && !string.IsNullOrEmpty(itemSet.ScarceItem.DefName))
            {
                ThingDef scarceDef = DefDatabase<ThingDef>.GetNamedSilentFail(itemSet.ScarceItem.DefName);
                if (scarceDef != null && ItemAirdropTradePolicy.TryResolveSpecialItemPrice(scarceDef, SpecialItemType.Scarce, out float scarcePrice, out _))
                {
                    sb.AppendLine($"- 当前稀缺商品：{itemSet.ScarceItem.Label}（参考单价 {scarcePrice:F1}，special_item_scarce）");
                }
                else
                {
                    sb.AppendLine($"- 当前稀缺商品：{itemSet.ScarceItem.Label}（special_item_scarce）");
                }
                hasAny = true;
            }
            if (!hasAny)
            {
                sb.AppendLine("- 当前无可用特殊商品。");
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
            sb.AppendLine("=== 动态任务可用性（按当前派系自动生成） ===");
            sb.AppendLine($"派系：{faction.Name} | 科技：{faction.def?.techLevel} | 类型：{faction.def?.defName}");
            if (isOrbitalTraderContext)
            {
                sb.AppendLine("当前会话：轨道商通信。禁止生成需要地面据点履约的订单任务；涉及具体物资交换时，只允许引导到 request_item_airdrop。");
            }
            if (isMerchantFaction)
            {
                sb.AppendLine("当前派系：商会派系。禁止生成 TradeRequest 订单任务；涉及具体物资交换时，只允许引导到 request_item_airdrop。");
            }
            sb.AppendLine();

            if (!allowed.Any())
            {
                sb.AppendLine("[阻止] 当前派系没有可用的合规任务模板。");
                if (blocked.Any())
                {
                    sb.AppendLine("阻止原因：");
                    foreach (var item in blocked)
                    {
                        sb.AppendLine($"  - {item.QuestDefName}: {item.Message}");
                    }
                }
                sb.AppendLine();
                return;
            }

            sb.AppendLine("当前派系可用任务（只能使用以下精确 defName，括号内为任务内容描述）：");
            foreach (var item in allowed)
            {
                sb.AppendLine($"  - {item.QuestDefName} {GetQuestTemplateDescription(item.QuestDefName)}");
            }

            if (blocked.Any())
            {
                sb.AppendLine();
                sb.AppendLine("当前派系被阻止的任务模板（禁止使用）：");
                foreach (var item in blocked)
                {
                    sb.AppendLine($"  - {item.QuestDefName}: {item.Message}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("重要：你只能从上面的可用列表中选择 questDefName。");
            if (allowed.Any(item => string.Equals(item.QuestDefName, "PawnLend", StringComparison.Ordinal)))
            {
                sb.AppendLine("PawnLend 严格约束：仅当当前殖民地确实有可借调殖民者，且系统能在运行时构建完整借调合同（人数、天数、职责、目标、是否派穿梭机）时，才可选择 PawnLend。若无法满足，必须改选其他可用模板。");
            }
            sb.AppendLine("方向匹配提醒：请仔细阅读每个任务模板描述中的【玩家→派系】或【派系→玩家】方向标记。方向选择错误等于功能执行错误，会破坏沉浸感。");
            sb.AppendLine();
        }

        internal void AppendQuestSelectionHardRules(StringBuilder sb)
        {
            sb.AppendLine("=== 任务模板严格覆盖规则 ===");
            sb.AppendLine("你必须将“动态任务可用性（按当前派系自动生成）”视为唯一有效任务来源。");
            sb.AppendLine("禁止使用其他分段中的静态/回忆型任务推荐。");
            sb.AppendLine("若任务出现在 blocked templates 或 blocked actions 中，必须禁止调用 create_quest。");
            sb.AppendLine("任务方向匹配规则：【玩家→派系】=玩家出人/出物资/出力；【派系→玩家】=派系出人/出任务/出情报；【双向】=双方共同参与。所选 questDefName 的方向必须与会话上下文一致。例如：玩家说'我可以借人给你'→PawnLend【玩家→派系】；玩家说'能不能派人过来帮忙'≠PawnLend，非帝国派系无人派模板，应引导到 request_aid 或其他可用动作。");
            sb.AppendLine("安全策略可能禁用高风险模板（例如 OpportunitySite_ItemStash）。如被禁用，必须以角色内方式拒绝并说明约束。");
            sb.AppendLine("若当前是轨道商通信，禁止使用 create_quest 生成要求玩家携带指定物资进入地面定居点的订单任务；遇到这类请求时，必须说明轨道商没有该履约链路，并引导玩家改用 request_item_airdrop。");
            sb.AppendLine("若当前派系是商会派系（OutlanderCivil / OutlanderRough），禁止使用 create_quest 生成 TradeRequest；涉及物资交换时，必须直接改用 request_item_airdrop。");
            sb.AppendLine();
        }

        internal string GetQuestTemplateDescription(string questDefName)
        {
            if (string.IsNullOrEmpty(questDefName)) return string.Empty;
            switch (questDefName)
            {
                case "TradeRequest":
                    return "（贸易订单：【玩家→派系】派系发起物资请求，玩家提供指定物资换取报酬）";
                case "OpportunitySite_PeaceTalks":
                    return "（和平谈判：【双向】派系邀请玩家代表出席和谈，可能化解敌对关系）";
                case "PawnLend":
                    return "（借调请求：【玩家→派系】派系请求借用玩家的殖民者。玩家出人→派系接收。派穿梭机接送，完成后给予报酬和好感）";
                case "ThreatReward_Raid_MiscReward":
                    return "（威胁悬赏：【派系→玩家】帝国派系发布悬赏令，玩家消灭目标后获得皇家声望或物品奖励）";
                case "Hospitality_Refugee":
                    return "（难民接待：【派系→玩家】帝国派系派遣难民到玩家基地暂住。派系出人→玩家接收。接待期结束后帝国给予奖励）";
                case "OpportunitySite_ItemStash":
                    return "（物品藏匿点：【派系→玩家】派系提供敌占据点情报，玩家前往清剿获取战利品）";
                default:
                    return string.Empty;
            }
        }

        private static readonly string[] PresenceBehaviorSectionTitles =
        {
            "【在线状态策略】",
            "Online Status Strategy:",
            "Online Status Strategy"
        };

        private static readonly string[] PresenceBehaviorActionAnchors =
        {
            "[exit_dialogue]",
            "[go_offline",
            "[set_dnd]"
        };

    }
}
