using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Prompting;
using RimWorld;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Memory
{
    /// <summary>/// Dependencies: AIChatServiceAsync, LeaderMemoryManager, diplomacy/RPG dialogue message models.
 /// Responsibility: create and persist cross-channel summaries with rule-first strategy and LLM fallback.
 ///</summary>
    public static class DialogueSummaryService
    {
        public const int MaxSummaryPoolPerType = 20;
        public const int MaxInjectedSummaryItems = 6;
        public const int MaxInjectedChars = 2200;

        internal const float LowConfidenceThreshold = 0.65f;
        internal const int MaxKeyFactsPerSummary = 3;

        public static void TryRecordDiplomacySessionSummary(
            Faction faction,
            List<DialogueMessageData> allMessages,
            int baselineMessageCount)
        {
            if (faction == null || faction.IsPlayer || allMessages == null || allMessages.Count <= baselineMessageCount)
            {
                return;
            }

            int start = Mathf.Clamp(baselineMessageCount, 0, allMessages.Count);
            List<DialogueMessageData> delta = allMessages.Skip(start).ToList();
            if (delta.Count == 0)
            {
                return;
            }

            CrossChannelSummaryRecord record = BuildRuleDiplomacySummary(faction, delta);
            if (record == null || string.IsNullOrWhiteSpace(record.SummaryText))
            {
                return;
            }

            LeaderMemoryManager.Instance.AddDiplomacySessionSummary(faction, record, MaxSummaryPoolPerType);
            TryQueueLlmFallback(faction, record, BuildDiplomacyFallbackContext(faction, delta));
        }

        public static void TryRecordRpgDepartSummary(Pawn pawn, RpgDialogueTraceSnapshot trace)
        {
            if (pawn == null || trace == null || trace.Faction == null || trace.Faction.IsPlayer || trace.Faction.defeated)
            {
                return;
            }

            CrossChannelSummaryRecord record = BuildRuleRpgDepartSummary(trace);
            if (record == null || string.IsNullOrWhiteSpace(record.SummaryText))
            {
                return;
            }

            LeaderMemoryManager.Instance.AddRpgDepartSummary(trace.Faction, record, MaxSummaryPoolPerType);
            TryQueueLlmFallback(trace.Faction, record, BuildRpgFallbackContext(trace));
        }

        public static void TryPushRpgSessionSummaryOnClose(Pawn initiator, Pawn target, List<ChatMessageData> chatHistory)
        {
            if (!TryBuildRpgSessionSummaryOnClose(initiator, target, chatHistory, out CrossChannelSummaryRecord record))
            {
                return;
            }

            if (record == null || string.IsNullOrWhiteSpace(record.SummaryText))
            {
                return;
            }

            Faction faction = target?.Faction ?? initiator?.Faction;
            if (faction == null || faction.IsPlayer)
            {
                return;
            }

            LeaderMemoryManager.Instance.AddRpgDepartSummary(faction, record, MaxSummaryPoolPerType);
            TryQueueLlmFallback(faction, record, BuildRpgSessionCloseContext(initiator, target, chatHistory));
        }

        public static string BuildRpgDynamicFactionMemoryBlock(Faction faction, Pawn targetPawn)
        {
            if (faction == null || faction.IsPlayer || faction.defeated || targetPawn == null)
            {
                return string.Empty;
            }

            FactionLeaderMemory memory = LeaderMemoryManager.Instance.GetMemory(faction);
            if (memory == null)
            {
                return string.Empty;
            }

            int targetPawnId = targetPawn.thingIDNumber;
            if (targetPawnId <= 0)
            {
                return string.Empty;
            }

            List<CrossChannelSummaryRecord> summaries = CollectSortedSummaries(memory, targetPawnId);
            if (summaries.Count == 0 && (memory.SignificantEvents == null || memory.SignificantEvents.Count == 0))
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            sb.AppendLine("=== DYNAMIC FACTION MEMORY (CROSS-CHANNEL, TARGET-PAWN SCOPED) ===");
            sb.AppendLine("Use only target-pawn scoped memories to maintain continuity with the player. Do not overwrite your persona.");

            int remain = MaxInjectedChars - sb.Length;
            int emitted = 0;
            for (int i = 0; i < summaries.Count && emitted < MaxInjectedSummaryItems && remain > 60; i++)
            {
                CrossChannelSummaryRecord item = summaries[i];
                if (item == null || string.IsNullOrWhiteSpace(item.SummaryText))
                {
                    continue;
                }

                string line = FormatSummaryLine(item);
                if (line.Length > remain)
                {
                    line = TrimToMax(line, remain);
                }

                if (!string.IsNullOrWhiteSpace(line))
                {
                    sb.AppendLine(line);
                    remain = MaxInjectedChars - sb.Length;
                    emitted++;
                }
            }

            AppendSignificantEventHints(sb, memory, ref remain);
            return sb.ToString().Trim();
        }

        private static void AppendSignificantEventHints(StringBuilder sb, FactionLeaderMemory memory, ref int remain)
        {
            if (remain <= 40 || memory?.SignificantEvents == null || memory.SignificantEvents.Count == 0)
            {
                return;
            }

            var events = memory.SignificantEvents
                .OrderByDescending(e => e.OccurredTick)
                .Take(3)
                .ToList();
            if (events.Count == 0)
            {
                return;
            }

            string header = "Recent major events:";
            if (header.Length < remain)
            {
                sb.AppendLine(header);
                remain = MaxInjectedChars - sb.Length;
            }

            for (int i = 0; i < events.Count && remain > 30; i++)
            {
                SignificantEventMemory evt = events[i];
                string line = $"- {evt.EventType}: {TrimToMax(evt.Description, 120)}";
                if (line.Length > remain)
                {
                    line = TrimToMax(line, remain);
                }

                if (!string.IsNullOrWhiteSpace(line))
                {
                    sb.AppendLine(line);
                    remain = MaxInjectedChars - sb.Length;
                }
            }
        }

        private static List<CrossChannelSummaryRecord> CollectSortedSummaries(FactionLeaderMemory memory, int targetPawnId)
        {
            var combined = new List<CrossChannelSummaryRecord>();
            if (memory.DiplomacySessionSummaries != null)
            {
                combined.AddRange(memory.DiplomacySessionSummaries);
            }
            if (memory.RpgDepartSummaries != null)
            {
                combined.AddRange(memory.RpgDepartSummaries);
            }

            int nowTick = Find.TickManager?.TicksGame ?? 0;

            return combined
                .Where(x => IsSummaryScopedToTargetPawn(x, targetPawnId))
                .OrderByDescending(x => ScoreForRpgInjection(x, nowTick, targetPawnId))
                .ThenByDescending(x => x.GameTick)
                .ToList();
        }

        private static bool IsSummaryScopedToTargetPawn(CrossChannelSummaryRecord record, int targetPawnId)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.SummaryText) || targetPawnId <= 0)
            {
                return false;
            }

            return record.PawnLoadId == targetPawnId;
        }

        private static float ScoreForRpgInjection(CrossChannelSummaryRecord record, int nowTick, int targetPawnId)
        {
            float sourceWeight = record.Source == CrossChannelSummarySource.DiplomacySession ? 1000f : 800f;
            float agePenalty = Mathf.Max(0f, nowTick - record.GameTick) / 1800f;
            float pawnBonus = (targetPawnId >= 0 && record.PawnLoadId == targetPawnId) ? 120f : 0f;
            return sourceWeight + record.Confidence * 100f + pawnBonus - agePenalty;
        }

        private static string FormatSummaryLine(CrossChannelSummaryRecord record)
        {
            string source = record.Source == CrossChannelSummarySource.DiplomacySession ? "Diplomacy" : "RPG-Depart";
            string text = TrimToMax(record.SummaryText, 220);
            if (record.Source == CrossChannelSummarySource.RpgDepart)
            {
                text = SanitizeRpgDepartSummaryText(text);
                return $"- [{source}] {text}";
            }

            if (record.KeyFacts == null || record.KeyFacts.Count == 0)
            {
                return $"- [{source}] {text}";
            }

            string facts = string.Join("; ", record.KeyFacts.Where(f => !string.IsNullOrWhiteSpace(f)).Take(2).Select(f => TrimToMax(f, 70)));
            if (string.IsNullOrWhiteSpace(facts))
            {
                return $"- [{source}] {text}";
            }

            return $"- [{source}] {text} | facts: {facts}";
        }

        private static CrossChannelSummaryRecord BuildRuleDiplomacySummary(Faction faction, List<DialogueMessageData> deltaMessages)
        {
            return DialogueSummaryRuleBuilders.BuildRuleDiplomacySummary(faction, deltaMessages);
        }

        private static CrossChannelSummaryRecord BuildRuleRpgDepartSummary(RpgDialogueTraceSnapshot trace)
        {
            return DialogueSummaryRuleBuilders.BuildRuleRpgDepartSummary(trace);
        }

        private static bool TryBuildRpgSessionSummaryOnClose(Pawn initiator,
            Pawn target,
            List<ChatMessageData> chatHistory,
            out CrossChannelSummaryRecord record)
        {
            return DialogueSummaryRuleBuilders.TryBuildRpgSessionSummaryOnClose(initiator, target, chatHistory, out record);
        }

        private static string CleanupRpgCloseTurnText(string rawText)
        {
            return DialogueSummaryRuleBuilders.CleanupRpgCloseTurnText(rawText);
        }

        private static string StripParserJsonTail(string text)
        {
            return DialogueSummaryRuleBuilders.StripParserJsonTail(text);
        }

        private static int FindFirstJsonMarkerIndex(string text)
        {
            return DialogueSummaryRuleBuilders.FindFirstJsonMarkerIndex(text);
        }

        private static List<string> BuildKeyFacts(IEnumerable<string> lines)
        {
            return DialogueSummaryRuleBuilders.BuildKeyFacts(lines);
        }

        private static List<string> BuildRpgSummaryFacts(List<string> topics, string playerIntent, string npcFinalTone)
        {
            return DialogueSummaryRuleBuilders.BuildRpgSummaryFacts(topics, playerIntent, npcFinalTone);
        }

        private static string DescribePlayerIntent(List<RpgDialogueTurn> turns)
        {
            return DialogueSummaryRuleBuilders.DescribePlayerIntent(turns);
        }

        private static string DescribeNpcTone(string text)
        {
            return DialogueSummaryRuleBuilders.DescribeNpcTone(text);
        }

        private static bool ContainsAny(string text, params string[] keywords)
        {
            return DialogueSummaryRuleBuilders.ContainsAny(text, keywords);
        }

        private static string SanitizeRpgDepartSummaryText(string text)
        {
            return DialogueSummaryRuleBuilders.SanitizeRpgDepartSummaryText(text);
        }

        private static List<string> ExtractTopics(IEnumerable<string> texts)
        {
            return DialogueSummaryRuleBuilders.ExtractTopics(texts);
        }

        private static void AddTopicIfContains(string lowerText, HashSet<string> tags, string label, params string[] keywords)
        {
            DialogueSummaryRuleBuilders.AddTopicIfContains(lowerText, tags, label, keywords);
        }

        private static void TryQueueLlmFallback(Faction faction, CrossChannelSummaryRecord record, string context)
        {
            DialogueSummaryLlmFallback.TryQueueLlmFallback(faction, record, context);
        }

        private static void ParseFallbackText(string raw, out string summary, out List<string> facts)
        {
            DialogueSummaryLlmFallback.ParseFallbackText(raw, out summary, out facts);
        }

        private static string BuildDiplomacyFallbackContext(Faction faction, List<DialogueMessageData> delta)
        {
            return DialogueSummaryLlmFallback.BuildDiplomacyFallbackContext(faction, delta);
        }

        private static string BuildRpgFallbackContext(RpgDialogueTraceSnapshot trace)
        {
            return DialogueSummaryLlmFallback.BuildRpgFallbackContext(trace);
        }

        private static string BuildRpgSessionCloseContext(Pawn initiator, Pawn target, List<ChatMessageData> chatHistory)
        {
            return DialogueSummaryLlmFallback.BuildRpgSessionCloseContext(initiator, target, chatHistory);
        }

        internal static float EstimateConfidence(int turnCount, int topicCount, bool hasPlayer, bool hasNpc)
        {
            float score = 0.32f;
            score += Mathf.Min(0.42f, turnCount * 0.08f);
            score += Mathf.Min(0.12f, topicCount * 0.04f);
            if (hasPlayer) score += 0.06f;
            if (hasNpc) score += 0.08f;
            return Mathf.Clamp(score, 0.05f, 0.95f);
        }

        internal static string BuildFactionId(Faction faction)
        {
            if (faction == null)
            {
                return string.Empty;
            }

            if (faction.def != null && !string.IsNullOrEmpty(faction.def.defName))
            {
                return $"{faction.def.defName}_{faction.loadID}";
            }

            return $"custom_{faction.loadID}";
        }

        internal static string ComputeHash(string text)
        {
            string input = text ?? string.Empty;
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(input);
                byte[] hash = sha.ComputeHash(bytes);
                var sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        internal static string TrimToMax(string value, int maxLen)
        {
            if (string.IsNullOrWhiteSpace(value) || maxLen <= 0)
            {
                return string.Empty;
            }

            string text = value.Trim();
            if (text.Length <= maxLen)
            {
                return text;
            }

            if (maxLen <= 3)
            {
                return text.Substring(0, maxLen);
            }

            return text.Substring(0, maxLen - 3) + "...";
        }
    }
}
