using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Memory;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Prompting.Builders
{
    /// <summary>
    /// Goodwill/peace policy and relation impression helpers for diplomacy prompts.
    /// </summary>
    internal static class DiplomacyPromptGoodwillPolicyOps
    {
        internal static void AppendGoodwillPeacePolicyHints(DiplomacyPromptBuilderContract owner, StringBuilder sb, Faction faction)
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
                AppendVeryLowGoodwillPeacePolicy(owner, sb, goodwill, peaceTalkOnlyMin);
            }
            else if (goodwill < makePeaceReenabledMin)
            {
                AppendPeaceTalkOnlyPolicy(owner, sb, goodwill, peaceTalkOnlyMin, makePeaceReenabledMin, peaceTalkQuest);
            }
            else
            {
                AppendMakePeaceReenabledPolicy(owner, sb, goodwill, peaceTalkQuest);
            }
            sb.AppendLine();
        }

        internal static void AppendVeryLowGoodwillPeacePolicy(DiplomacyPromptBuilderContract owner, StringBuilder sb, int goodwill, int peaceTalkOnlyMin)
        {
            sb.AppendLine(string.Format(PromptTextConstants.GoodwillPeacePolicyVeryLowLine1, goodwill));
            sb.AppendLine(string.Format(PromptTextConstants.GoodwillPeacePolicyVeryLowLine2, peaceTalkOnlyMin));
        }

        internal static void AppendPeaceTalkOnlyPolicy(DiplomacyPromptBuilderContract owner, StringBuilder sb,
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

        internal static void AppendMakePeaceReenabledPolicy(DiplomacyPromptBuilderContract owner, StringBuilder sb, int goodwill, string peaceTalkQuest)
        {
            sb.AppendLine(string.Format(PromptTextConstants.GoodwillPeacePolicyReenabledLine1, goodwill));
            sb.AppendLine(string.Format(PromptTextConstants.GoodwillPeacePolicyReenabledLine2, peaceTalkQuest));
        }

        internal static bool ShouldHideActionFromPromptByProjectedGoodwill(DiplomacyPromptBuilderContract owner, Faction faction, string actionName)
        {
            return false;
        }

        internal static string GetProjectedGoodwillBlockReason(DiplomacyPromptBuilderContract owner, Faction faction, string actionName)
        {
            return string.Empty;
        }

        internal static string GetRelationLabel(DiplomacyPromptBuilderContract owner, int goodwill)
        {
            if (goodwill >= 80) return "Ally";
            if (goodwill >= 40) return "Friend";
            if (goodwill >= 0) return "Neutral";
            if (goodwill >= -40) return "Hostile";
            return "Enemy";
        }

        internal static string GetEventIcon(DiplomacyPromptBuilderContract owner, SignificantEventType eventType)
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

        internal static string GetEventTypeName(DiplomacyPromptBuilderContract owner, SignificantEventType eventType)
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

        internal static string GetRelationImpression(DiplomacyPromptBuilderContract owner, FactionMemoryEntry memory)
        {
            if (memory.NegativeInteractions > memory.PositiveInteractions * 2)
            {
                return "Небезпечний ворог: багаторазова ворожість тримає нас насторожі";
            }
            else if (memory.NegativeInteractions > memory.PositiveInteractions)
            {
                return "Напружені відносини, чимало конфліктів";
            }
            else if (memory.PositiveInteractions > memory.NegativeInteractions * 2)
            {
                return "Надійний союзник: тривала дружня співпраця збудувала довіру";
            }
            else if (memory.PositiveInteractions > memory.NegativeInteractions)
            {
                return "Приязна фракція: у взаємодії переважає співпраця";
            }
            else
            {
                return "Складні відносини: є і співпраця, і конфлікти";
            }
        }

        internal static string GetRelationTrend(DiplomacyPromptBuilderContract owner, List<RelationSnapshot> history)
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
