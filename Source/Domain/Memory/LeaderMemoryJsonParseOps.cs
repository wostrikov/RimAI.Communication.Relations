using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Memory
{
    /// <summary>
    /// JSON parse helpers for faction leader memory codec.
    /// </summary>
    internal static class LeaderMemoryJsonParseOps
    {
        internal static void ParseFactionMemories(string json, FactionLeaderMemory memory)
        {
            memory.FactionMemories = new List<FactionMemoryEntry>();
            if (!LeaderMemoryJsonUtil.TryExtractJsonArray(json, "factionMemories", out string content))
            {
                return;
            }

            foreach (string obj in LeaderMemoryJsonUtil.SplitJsonObjects(content))
            {
                string factionId = LeaderMemoryJsonUtil.ExtractJsonString(obj, "factionId");
                if (string.IsNullOrWhiteSpace(factionId))
                {
                    continue;
                }

                memory.FactionMemories.Add(new FactionMemoryEntry
                {
                    FactionId = factionId,
                    FactionName = LeaderMemoryJsonUtil.ExtractJsonString(obj, "factionName"),
                    FirstContactTick = LeaderMemoryJsonUtil.ExtractJsonInt(obj, "firstContactTick"),
                    LastMentionedTick = LeaderMemoryJsonUtil.ExtractJsonInt(obj, "lastMentionedTick"),
                    MentionCount = LeaderMemoryJsonUtil.ExtractJsonInt(obj, "mentionCount"),
                    PositiveInteractions = LeaderMemoryJsonUtil.ExtractJsonInt(obj, "positiveInteractions"),
                    NegativeInteractions = LeaderMemoryJsonUtil.ExtractJsonInt(obj, "negativeInteractions"),
                    RelationHistory = ParseRelationHistory(obj)
                });
            }
        }

        internal static void ParseSignificantEvents(string json, FactionLeaderMemory memory)
        {
            memory.SignificantEvents = new List<SignificantEventMemory>();
            if (!LeaderMemoryJsonUtil.TryExtractJsonArray(json, "significantEvents", out string content))
            {
                return;
            }

            foreach (string obj in LeaderMemoryJsonUtil.SplitJsonObjects(content))
            {
                string eventTypeRaw = LeaderMemoryJsonUtil.ExtractJsonString(obj, "eventType");
                if (!Enum.TryParse(eventTypeRaw, true, out SignificantEventType eventType))
                {
                    continue;
                }

                memory.SignificantEvents.Add(new SignificantEventMemory
                {
                    EventType = eventType,
                    InvolvedFactionId = LeaderMemoryJsonUtil.ExtractJsonString(obj, "involvedFactionId"),
                    InvolvedFactionName = LeaderMemoryJsonUtil.ExtractJsonString(obj, "involvedFactionName"),
                    Description = LeaderMemoryJsonUtil.ExtractJsonString(obj, "description"),
                    OccurredTick = LeaderMemoryJsonUtil.ExtractJsonInt(obj, "occurredTick"),
                    Timestamp = LeaderMemoryJsonUtil.ExtractJsonLong(obj, "timestamp")
                });
            }
        }

        internal static void ParseDialogueHistory(string json, FactionLeaderMemory memory)
        {
            memory.DialogueHistory = new List<DialogueRecord>();
            if (!LeaderMemoryJsonUtil.TryExtractJsonArray(json, "dialogueHistory", out string content))
            {
                return;
            }

            foreach (string obj in LeaderMemoryJsonUtil.SplitJsonObjects(content))
            {
                bool isPlayer = LeaderMemoryJsonUtil.ExtractJsonBool(obj, "isPlayer");
                string message = LeaderMemoryJsonUtil.ExtractJsonString(obj, "message");
                int tick = LeaderMemoryJsonUtil.ExtractJsonInt(obj, "gameTick");
                if (string.IsNullOrWhiteSpace(message))
                {
                    continue;
                }

                memory.DialogueHistory.Add(new DialogueRecord
                {
                    IsPlayer = isPlayer,
                    Message = message,
                    GameTick = tick
                });
            }
        }

        internal static void ParseCrossChannelSummaries(string json, FactionLeaderMemory memory)
        {
            memory.RpgDepartSummaries = ParseSummaryArray(json, "rpgDepartSummaries");
            memory.DiplomacySessionSummaries = ParseSummaryArray(json, "diplomacySessionSummaries");
        }

        internal static List<CrossChannelSummaryRecord> ParseSummaryArray(string json, string key)
        {
            var result = new List<CrossChannelSummaryRecord>();
            if (!LeaderMemoryJsonUtil.TryExtractJsonArray(json, key, out string content))
            {
                return result;
            }

            foreach (string obj in LeaderMemoryJsonUtil.SplitJsonObjects(content))
            {
                string summary = LeaderMemoryJsonUtil.ExtractJsonString(obj, "summaryText");
                if (string.IsNullOrWhiteSpace(summary))
                {
                    continue;
                }

                string sourceRaw = LeaderMemoryJsonUtil.ExtractJsonString(obj, "source");
                if (!Enum.TryParse(sourceRaw, true, out CrossChannelSummarySource source))
                {
                    source = CrossChannelSummarySource.Unknown;
                }

                result.Add(new CrossChannelSummaryRecord
                {
                    Source = source,
                    FactionId = LeaderMemoryJsonUtil.ExtractJsonString(obj, "factionId"),
                    PawnLoadId = LeaderMemoryJsonUtil.ExtractJsonInt(obj, "pawnLoadId"),
                    PawnName = LeaderMemoryJsonUtil.ExtractJsonString(obj, "pawnName"),
                    SummaryText = summary,
                    GameTick = LeaderMemoryJsonUtil.ExtractJsonInt(obj, "gameTick"),
                    Confidence = LeaderMemoryJsonUtil.ExtractJsonFloat(obj, "confidence"),
                    ContentHash = LeaderMemoryJsonUtil.ExtractJsonString(obj, "contentHash"),
                    IsLlmFallback = LeaderMemoryJsonUtil.ExtractJsonBool(obj, "isLlmFallback"),
                    CreatedTimestamp = LeaderMemoryJsonUtil.ExtractJsonLong(obj, "createdTimestamp"),
                    KeyFacts = ParseStringArrayField(obj, "keyFacts")
                });
            }

            return result;
        }

        internal static List<string> ParseStringArrayField(string json, string key)
        {
            if (!LeaderMemoryJsonUtil.TryExtractJsonArray(json, key, out string content))
            {
                return new List<string>();
            }

            var list = new List<string>();
            MatchCollection matches = Regex.Matches(content, "\"((?:\\\\.|[^\"])*)\"");
            foreach (Match match in matches)
            {
                if (!match.Success || match.Groups.Count < 2)
                {
                    continue;
                }

                string value = match.Groups[1].Value
                    .Replace("\\\\", "\\")
                    .Replace("\\\"", "\"")
                    .Replace("\\n", "\n")
                    .Replace("\\r", "\r")
                    .Replace("\\t", "\t")
                    .Replace("\\b", "\b")
                    .Replace("\\f", "\f");

                value = Regex.Replace(value, @"\\u([0-9a-fA-F]{4})", m =>
                {
                    int code = int.Parse(m.Groups[1].Value, NumberStyles.HexNumber);
                    return ((char)code).ToString();
                });
                if (!string.IsNullOrWhiteSpace(value))
                {
                    list.Add(value.Trim());
                }
            }

            return list;
        }

        internal static List<RelationSnapshot> ParseRelationHistory(string json)
        {
            var result = new List<RelationSnapshot>();
            if (!LeaderMemoryJsonUtil.TryExtractJsonArray(json, "relationHistory", out string content))
            {
                return result;
            }

            foreach (string obj in LeaderMemoryJsonUtil.SplitJsonObjects(content))
            {
                int tick = LeaderMemoryJsonUtil.ExtractJsonInt(obj, "tick");
                string relation = LeaderMemoryJsonUtil.ExtractJsonString(obj, "relation");
                int goodwill = LeaderMemoryJsonUtil.ExtractJsonInt(obj, "goodwill");
                if (tick == 0 && string.IsNullOrWhiteSpace(relation) && goodwill == 0)
                {
                    continue;
                }

                result.Add(new RelationSnapshot
                {
                    Tick = tick,
                    Relation = relation,
                    Goodwill = goodwill
                });
            }

            return result;
        }
    }
}
