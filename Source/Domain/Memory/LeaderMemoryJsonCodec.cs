using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Memory
{
    /// <summary>/// Dependencies: FactionLeaderMemory and cross-channel summary model.
 /// Responsibility: serialize/deserialize leader memory JSON with backward-compatible field mapping.
 ///</summary>
    internal static class LeaderMemoryJsonCodec
    {
        public static string ConvertMemoryToJson(FactionLeaderMemory memory)
        {
            if (memory == null)
            {
                return "{}";
            }

            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append($"  \"ownerFactionId\": \"{EscapeJson(memory.OwnerFactionId)}\",\n");
            sb.Append($"  \"ownerFactionName\": \"{EscapeJson(memory.OwnerFactionName)}\",\n");
            sb.Append($"  \"leaderName\": \"{EscapeJson(memory.LeaderName)}\",\n");
            sb.Append($"  \"lastUpdatedTick\": {memory.LastUpdatedTick},\n");
            sb.Append($"  \"createdTimestamp\": {memory.CreatedTimestamp},\n");
            sb.Append($"  \"lastSavedTimestamp\": {memory.LastSavedTimestamp},\n");

            sb.Append("  \"factionMemories\": [\n");
            bool firstFaction = true;
            List<FactionMemoryEntry> memories = memory.FactionMemories ?? new List<FactionMemoryEntry>();
            for (int i = 0; i < memories.Count; i++)
            {
                FactionMemoryEntry fm = memories[i];
                if (fm == null)
                {
                    continue;
                }

                bool hasInteraction = fm.MentionCount > 0 || fm.PositiveInteractions > 0 || fm.NegativeInteractions > 0;
                bool hasHistory = fm.RelationHistory != null && fm.RelationHistory.Count > 0;
                if ((!hasInteraction && !hasHistory) ||
                    fm.FactionId == memory.OwnerFactionId)
                {
                    continue;
                }

                if (!firstFaction) sb.Append(",\n");
                firstFaction = false;
                sb.Append("    {\n");
                sb.Append($"      \"factionName\": \"{EscapeJson(fm.FactionName)}\",\n");
                sb.Append($"      \"factionId\": \"{EscapeJson(fm.FactionId)}\",\n");
                sb.Append($"      \"firstContactTick\": {fm.FirstContactTick},\n");
                sb.Append($"      \"lastMentionedTick\": {fm.LastMentionedTick},\n");
                sb.Append($"      \"mentionCount\": {fm.MentionCount},\n");
                sb.Append($"      \"positiveInteractions\": {fm.PositiveInteractions},\n");
                sb.Append($"      \"negativeInteractions\": {fm.NegativeInteractions},\n");
                AppendRelationHistoryArray(sb, fm.RelationHistory);
                sb.Append("\n");
                sb.Append("    }");
            }
            sb.Append("\n  ],\n");

            sb.Append("  \"significantEvents\": [\n");
            List<SignificantEventMemory> events = (memory.SignificantEvents ?? new List<SignificantEventMemory>())
                .Where(evt => evt != null)
                .ToList();
            for (int i = 0; i < events.Count; i++)
            {
                SignificantEventMemory evt = events[i];
                sb.Append("    {\n");
                sb.Append($"      \"eventType\": \"{evt.EventType}\",\n");
                sb.Append($"      \"involvedFactionId\": \"{EscapeJson(evt.InvolvedFactionId)}\",\n");
                sb.Append($"      \"involvedFactionName\": \"{EscapeJson(evt.InvolvedFactionName)}\",\n");
                sb.Append($"      \"description\": \"{EscapeJson(evt.Description)}\",\n");
                sb.Append($"      \"occurredTick\": {evt.OccurredTick},\n");
                sb.Append($"      \"timestamp\": {evt.Timestamp}\n");
                sb.Append("    }");
                if (i < events.Count - 1) sb.Append(",");
                sb.Append("\n");
            }
            sb.Append("  ],\n");

            sb.Append("  \"dialogueHistory\": [\n");
            List<DialogueRecord> dialogues = (memory.DialogueHistory ?? new List<DialogueRecord>())
                .Where(dlg => dlg != null && !string.IsNullOrWhiteSpace(dlg.Message))
                .ToList();
            for (int i = 0; i < dialogues.Count; i++)
            {
                DialogueRecord dlg = dialogues[i];
                sb.Append("    {\n");
                sb.Append($"      \"isPlayer\": {dlg.IsPlayer.ToString().ToLower()},\n");
                sb.Append($"      \"message\": \"{EscapeJson(dlg.Message)}\",\n");
                sb.Append($"      \"gameTick\": {dlg.GameTick}\n");
                sb.Append("    }");
                if (i < dialogues.Count - 1) sb.Append(",");
                sb.Append("\n");
            }
            sb.Append("  ],\n");

            AppendSummaryArray(sb, "rpgDepartSummaries", memory.RpgDepartSummaries);
            sb.Append(",\n");
            AppendSummaryArray(sb, "diplomacySessionSummaries", memory.DiplomacySessionSummaries);
            sb.Append("\n");
            sb.Append("}");
            return sb.ToString();
        }

        public static FactionLeaderMemory ParseJsonToMemory(string json)
        {
            try
            {
                var memory = new FactionLeaderMemory();
                memory.OwnerFactionId = ExtractJsonString(json, "ownerFactionId");
                memory.OwnerFactionName = ExtractJsonString(json, "ownerFactionName");
                memory.LeaderName = ExtractJsonString(json, "leaderName");
                memory.LastUpdatedTick = ExtractJsonInt(json, "lastUpdatedTick");
                memory.CreatedTimestamp = FirstNonZeroLong(
                    ExtractJsonLong(json, "createdTimestamp"),
                    DateTime.UtcNow.Ticks);
                memory.LastSavedTimestamp = FirstNonZeroLong(
                    ExtractJsonLong(json, "lastSavedTimestamp"),
                    memory.CreatedTimestamp);

                ParseFactionMemories(json, memory);
                ParseSignificantEvents(json, memory);
                ParseDialogueHistory(json, memory);
                ParseCrossChannelSummaries(json, memory);
                return memory;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAI.Relations] Failed to parse JSON memory: {ex.Message}");
                return null;
            }
        }

        private static void AppendSummaryArray(StringBuilder sb, string key, List<CrossChannelSummaryRecord> records)
        {
            sb.Append($"  \"{key}\": [\n");
            List<CrossChannelSummaryRecord> source = records ?? new List<CrossChannelSummaryRecord>();
            for (int i = 0; i < source.Count; i++)
            {
                CrossChannelSummaryRecord record = source[i];
                if (record == null || string.IsNullOrWhiteSpace(record.SummaryText))
                {
                    continue;
                }

                sb.Append("    {\n");
                sb.Append($"      \"source\": \"{record.Source}\",\n");
                sb.Append($"      \"factionId\": \"{EscapeJson(record.FactionId)}\",\n");
                sb.Append($"      \"pawnLoadId\": {record.PawnLoadId},\n");
                sb.Append($"      \"pawnName\": \"{EscapeJson(record.PawnName)}\",\n");
                sb.Append($"      \"summaryText\": \"{EscapeJson(record.SummaryText)}\",\n");
                sb.Append($"      \"gameTick\": {record.GameTick},\n");
                sb.Append($"      \"confidence\": {record.Confidence.ToString(CultureInfo.InvariantCulture)},\n");
                sb.Append($"      \"contentHash\": \"{EscapeJson(record.ContentHash)}\",\n");
                sb.Append($"      \"isLlmFallback\": {record.IsLlmFallback.ToString().ToLower()},\n");
                sb.Append($"      \"createdTimestamp\": {record.CreatedTimestamp},\n");
                sb.Append("      \"keyFacts\": [");

                List<string> facts = record.KeyFacts ?? new List<string>();
                for (int factIndex = 0; factIndex < facts.Count; factIndex++)
                {
                    if (factIndex > 0) sb.Append(", ");
                    sb.Append($"\"{EscapeJson(facts[factIndex] ?? string.Empty)}\"");
                }
                sb.Append("]\n");
                sb.Append("    }");

                bool hasNext = false;
                for (int j = i + 1; j < source.Count; j++)
                {
                    if (source[j] != null && !string.IsNullOrWhiteSpace(source[j].SummaryText))
                    {
                        hasNext = true;
                        break;
                    }
                }

                if (hasNext)
                {
                    sb.Append(",");
                }
                sb.Append("\n");
            }

            sb.Append("  ]");
        }

        internal static void ParseFactionMemories(string json, FactionLeaderMemory memory)
        {
            LeaderMemoryJsonParseOps.ParseFactionMemories(json, memory);
        }

        internal static void ParseSignificantEvents(string json, FactionLeaderMemory memory)
        {
            LeaderMemoryJsonParseOps.ParseSignificantEvents(json, memory);
        }

        internal static void ParseDialogueHistory(string json, FactionLeaderMemory memory)
        {
            LeaderMemoryJsonParseOps.ParseDialogueHistory(json, memory);
        }

        internal static void ParseCrossChannelSummaries(string json, FactionLeaderMemory memory)
        {
            LeaderMemoryJsonParseOps.ParseCrossChannelSummaries(json, memory);
        }

        internal static List<CrossChannelSummaryRecord> ParseSummaryArray(string json, string key)
        {
            return LeaderMemoryJsonParseOps.ParseSummaryArray(json, key);
        }

        internal static List<string> ParseStringArrayField(string json, string key)
        {
            return LeaderMemoryJsonParseOps.ParseStringArrayField(json, key);
        }

        internal static List<RelationSnapshot> ParseRelationHistory(string json)
        {
            return LeaderMemoryJsonParseOps.ParseRelationHistory(json);
        }

        private static void AppendRelationHistoryArray(StringBuilder sb, List<RelationSnapshot> relationHistory)
        {
            List<RelationSnapshot> snapshots = relationHistory ?? new List<RelationSnapshot>();
            sb.Append("      \"relationHistory\": [");
            if (snapshots.Count == 0)
            {
                sb.Append("]");
                return;
            }

            sb.Append("\n");
            for (int i = 0; i < snapshots.Count; i++)
            {
                RelationSnapshot snapshot = snapshots[i] ?? new RelationSnapshot();
                sb.Append("        {\n");
                sb.Append($"          \"tick\": {snapshot.Tick},\n");
                sb.Append($"          \"relation\": \"{EscapeJson(snapshot.Relation)}\",\n");
                sb.Append($"          \"goodwill\": {snapshot.Goodwill}\n");
                sb.Append("        }");
                if (i < snapshots.Count - 1)
                {
                    sb.Append(",");
                }
                sb.Append("\n");
            }
            sb.Append("      ]");
        }

        internal static bool TryExtractJsonArray(string json, string key, out string arrayContent)
        {
            return LeaderMemoryJsonUtil.TryExtractJsonArray(json, key, out arrayContent);
        }

        internal static bool TryExtractJsonObject(string json, string key, out string objectContent)
        {
            return LeaderMemoryJsonUtil.TryExtractJsonObject(json, key, out objectContent);
        }

        internal static bool TryFindJsonBlockEnd(string json, int blockStart, char openChar, char closeChar, out int endIndex)
        {
            return LeaderMemoryJsonUtil.TryFindJsonBlockEnd(json, blockStart, openChar, closeChar, out endIndex);
        }

        internal static List<string> SplitJsonObjects(string arrayJson)
        {
            return LeaderMemoryJsonUtil.SplitJsonObjects(arrayJson);
        }

        internal static string EscapeJson(string value)
        {
            return LeaderMemoryJsonUtil.EscapeJson(value);
        }

        internal static string ExtractJsonString(string json, string key)
        {
            return LeaderMemoryJsonUtil.ExtractJsonString(json, key);
        }

        internal static int ExtractJsonInt(string json, string key)
        {
            return LeaderMemoryJsonUtil.ExtractJsonInt(json, key);
        }

        internal static long ExtractJsonLong(string json, string key)
        {
            return LeaderMemoryJsonUtil.ExtractJsonLong(json, key);
        }

        internal static float ExtractJsonFloat(string json, string key)
        {
            return LeaderMemoryJsonUtil.ExtractJsonFloat(json, key);
        }

        internal static bool ExtractJsonBool(string json, string key)
        {
            return LeaderMemoryJsonUtil.ExtractJsonBool(json, key);
        }

        internal static string FirstNonEmpty(params string[] values)
        {
            return LeaderMemoryJsonUtil.FirstNonEmpty(values);
        }

        internal static int FirstNonZero(params int[] values)
        {
            return LeaderMemoryJsonUtil.FirstNonZero(values);
        }

        internal static long FirstNonZeroLong(params long[] values)
        {
            return LeaderMemoryJsonUtil.FirstNonZeroLong(values);
        }

        internal static float FirstNonZeroFloat(params float[] values)
        {
            return LeaderMemoryJsonUtil.FirstNonZeroFloat(values);
        }


    }
}
