using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ustas.RimAI.Communication.Relations.AI;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Memory
{
    /// <summary>
    /// Rule-first summary builders and text cleanup helpers for cross-channel memory.
    /// </summary>
    internal static class DialogueSummaryRuleBuilders
    {
        internal static CrossChannelSummaryRecord BuildRuleDiplomacySummary(Faction faction, List<DialogueMessageData> deltaMessages)
        {
            List<DialogueMessageData> usable = deltaMessages
                .Where(m => m != null && !m.IsSystemMessage() && !string.IsNullOrWhiteSpace(m.message))
                .ToList();
            if (usable.Count == 0)
            {
                return null;
            }

            string factionId = DialogueSummaryService.BuildFactionId(faction);
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            string playerLast = usable.LastOrDefault(m => m.isPlayer)?.message ?? string.Empty;
            string aiLast = usable.LastOrDefault(m => !m.isPlayer)?.message ?? string.Empty;
            List<string> topics = ExtractTopics(usable.Select(m => m.message));
            List<string> facts = BuildKeyFacts(usable.Select(m => m.isPlayer ? $"Player: {m.message}" : $"Faction: {m.message}"));
            string topicText = topics.Count > 0 ? string.Join(", ", topics) : "general negotiation";

            string summary = $"Session touched {topicText}. " +
                             $"Last player intent: {DialogueSummaryService.TrimToMax(playerLast, 80)}. " +
                             $"Last faction stance: {DialogueSummaryService.TrimToMax(aiLast, 80)}.";

            float confidence = DialogueSummaryService.EstimateConfidence(usable.Count, topics.Count, !string.IsNullOrWhiteSpace(playerLast), !string.IsNullOrWhiteSpace(aiLast));
            string hashSeed = $"{factionId}|diplomacy|{usable.Count}|{usable.Last().GetGameTick()}|{summary}";

            return new CrossChannelSummaryRecord
            {
                Source = CrossChannelSummarySource.DiplomacySession,
                FactionId = factionId,
                PawnLoadId = -1,
                PawnName = string.Empty,
                SummaryText = summary,
                KeyFacts = facts,
                GameTick = currentTick,
                Confidence = confidence,
                ContentHash = DialogueSummaryService.ComputeHash(hashSeed),
                IsLlmFallback = false,
                CreatedTimestamp = DateTime.UtcNow.Ticks
            };
        }

        internal static CrossChannelSummaryRecord BuildRuleRpgDepartSummary(RpgDialogueTraceSnapshot trace)
        {
            List<RpgDialogueTurn> turns = trace.Turns?
                .Where(t => t != null && !string.IsNullOrWhiteSpace(t.Text))
                .ToList() ?? new List<RpgDialogueTurn>();
            if (turns.Count == 0)
            {
                return null;
            }

            int currentTick = Find.TickManager?.TicksGame ?? 0;
            string factionId = DialogueSummaryService.BuildFactionId(trace.Faction);
            string pawnName = trace.Pawn?.LabelShort ?? trace.Pawn?.Name?.ToStringShort ?? "UnknownPawn";
            List<string> topics = ExtractTopics(turns.Select(t => t.Text));
            string finalNpcText = turns.LastOrDefault(t => !t.IsPlayer)?.Text ?? string.Empty;
            string playerIntent = DescribePlayerIntent(turns);
            string npcFinalTone = DescribeNpcTone(finalNpcText);
            List<string> facts = BuildRpgSummaryFacts(topics, playerIntent, npcFinalTone);

            string summary = $"Pawn {pawnName} departed map after RPG dialogue. " +
                              $"Main topics: {(topics.Count > 0 ? string.Join(", ", topics) : "daily interaction")}. " +
                              $"Player intent trend: {playerIntent}. " +
                              $"NPC final tone: {npcFinalTone}.";

            float confidence = DialogueSummaryService.EstimateConfidence(turns.Count, topics.Count, true, !string.IsNullOrWhiteSpace(finalNpcText));
            string hashSeed = $"{factionId}|rpg_depart|{trace.Pawn?.thingIDNumber ?? -1}|{trace.LastInteractionTick}|{turns.Count}";

            return new CrossChannelSummaryRecord
            {
                Source = CrossChannelSummarySource.RpgDepart,
                FactionId = factionId,
                PawnLoadId = trace.Pawn?.thingIDNumber ?? -1,
                PawnName = pawnName,
                SummaryText = summary,
                KeyFacts = facts,
                GameTick = currentTick,
                Confidence = confidence,
                ContentHash = DialogueSummaryService.ComputeHash(hashSeed),
                IsLlmFallback = false,
                CreatedTimestamp = DateTime.UtcNow.Ticks
            };
        }

        internal static bool TryBuildRpgSessionSummaryOnClose(
            Pawn initiator,
            Pawn target,
            List<ChatMessageData> chatHistory,
            out CrossChannelSummaryRecord record)
        {
            record = null;
            if (target == null || chatHistory == null || chatHistory.Count == 0)
            {
                return false;
            }

            List<RpgDialogueTurn> turns = chatHistory
                .Where(message => message != null &&
                    !string.IsNullOrWhiteSpace(message.content) &&
                    !string.Equals(message.role, "system", StringComparison.OrdinalIgnoreCase))
                .Select(message => new RpgDialogueTurn
                {
                    IsPlayer = string.Equals(message.role, "user", StringComparison.OrdinalIgnoreCase),
                    Text = CleanupRpgCloseTurnText(message.content),
                    GameTick = Find.TickManager?.TicksGame ?? 0
                })
                .Where(turn => !string.IsNullOrWhiteSpace(turn.Text))
                .ToList();

            if (turns.Count == 0)
            {
                return false;
            }

            int currentTick = Find.TickManager?.TicksGame ?? 0;
            Faction faction = target.Faction ?? initiator?.Faction;
            string factionId = DialogueSummaryService.BuildFactionId(faction);
            string pawnName = target.LabelShort ?? target.Name?.ToStringShort ?? "UnknownPawn";
            List<string> topics = ExtractTopics(turns.Select(t => t.Text));
            string finalNpcText = turns.LastOrDefault(t => !t.IsPlayer)?.Text ?? string.Empty;
            string playerIntent = DescribePlayerIntent(turns);
            string npcFinalTone = DescribeNpcTone(finalNpcText);
            List<string> facts = BuildRpgSummaryFacts(topics, playerIntent, npcFinalTone);

            string summary = $"RPG dialogue session with {pawnName} ended. " +
                              $"Main topics: {(topics.Count > 0 ? string.Join(", ", topics) : "daily interaction")}. " +
                              $"Player intent trend: {playerIntent}. " +
                              $"NPC final tone: {npcFinalTone}.";

            float confidence = DialogueSummaryService.EstimateConfidence(turns.Count, topics.Count, true, !string.IsNullOrWhiteSpace(finalNpcText));
            string hashSeed = $"{factionId}|rpg_close|{target.thingIDNumber}|{turns.Count}|{summary}";

            record = new CrossChannelSummaryRecord
            {
                Source = CrossChannelSummarySource.RpgDepart,
                FactionId = factionId,
                PawnLoadId = target.thingIDNumber,
                PawnName = pawnName,
                SummaryText = summary,
                KeyFacts = facts,
                GameTick = currentTick,
                Confidence = confidence,
                ContentHash = DialogueSummaryService.ComputeHash(hashSeed),
                IsLlmFallback = false,
                CreatedTimestamp = DateTime.UtcNow.Ticks
            };

            return true;
        }

        internal static string CleanupRpgCloseTurnText(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText))
            {
                return string.Empty;
            }

            string text = rawText.Trim();
            int codeFence = text.IndexOf("```", StringComparison.Ordinal);
            if (codeFence > 0)
            {
                text = text.Substring(0, codeFence).Trim();
            }

            text = StripParserJsonTail(text);
            return DialogueSummaryService.TrimToMax(text, 180);
        }

        internal static string StripParserJsonTail(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            int cutIndex = FindFirstJsonMarkerIndex(text);
            if (cutIndex < 0)
            {
                return text;
            }

            return text.Substring(0, cutIndex).Trim();
        }

        internal static int FindFirstJsonMarkerIndex(string text)
        {
            string[] markers =
            {
                "{\"actions\"",
                "{ \"actions\"",
                "{\"action\"",
                "{ \"action\""
            };

            int hit = -1;
            for (int i = 0; i < markers.Length; i++)
            {
                int idx = text.IndexOf(markers[i], StringComparison.OrdinalIgnoreCase);
                if (idx >= 0 && (hit < 0 || idx < hit))
                {
                    hit = idx;
                }
            }

            return hit;
        }

        internal static List<string> BuildKeyFacts(IEnumerable<string> lines)
        {
            return lines
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => DialogueSummaryService.TrimToMax(x.Trim(), 90))
                .Distinct()
                .Take(DialogueSummaryService.MaxKeyFactsPerSummary)
                .ToList();
        }

        internal static List<string> BuildRpgSummaryFacts(List<string> topics, string playerIntent, string npcFinalTone)
        {
            return new List<string>
            {
                "topics: " + (topics != null && topics.Count > 0 ? string.Join(", ", topics.Take(3)) : "daily interaction"),
                "player_intent: " + (string.IsNullOrWhiteSpace(playerIntent) ? "neutral" : playerIntent),
                "npc_final_tone: " + (string.IsNullOrWhiteSpace(npcFinalTone) ? "neutral" : npcFinalTone)
            };
        }

        internal static string DescribePlayerIntent(List<RpgDialogueTurn> turns)
        {
            string lastPlayer = turns?
                .Where(t => t != null && t.IsPlayer && !string.IsNullOrWhiteSpace(t.Text))
                .Select(t => t.Text)
                .LastOrDefault() ?? string.Empty;
            if (ContainsAny(lastPlayer, "kill", "murder", "attack", "threat", "вбити", "атакувати", "погроза")) return "hostile";
            if (ContainsAny(lastPlayer, "help", "ally", "peace", "trade", "співпраця", "мир", "торгівля")) return "cooperative";
            return "neutral";
        }

        internal static string DescribeNpcTone(string text)
        {
            if (ContainsAny(text, "пильність", "назад", "відмова", "забирайся", "threat", "stay away", "guarded")) return "guarded";
            if (ContainsAny(text, "дружній", "вітаємо", "дякую", "happy", "glad", "friendly")) return "friendly";
            return "neutral";
        }

        internal static bool ContainsAny(string text, params string[] keywords)
        {
            string lower = (text ?? string.Empty).ToLowerInvariant();
            for (int i = 0; i < keywords.Length; i++)
            {
                if (lower.Contains((keywords[i] ?? string.Empty).ToLowerInvariant()))
                {
                    return true;
                }
            }
            return false;
        }

        internal static string SanitizeRpgDepartSummaryText(string text)
        {
            string value = text ?? string.Empty;
            int idx = value.IndexOf("Final NPC signal:", StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                idx = value.IndexOf("Last NPC signal:", StringComparison.OrdinalIgnoreCase);
            }

            if (idx >= 0)
            {
                value = value.Substring(0, idx).TrimEnd(' ', '.', ';') + ".";
            }

            return DialogueSummaryService.TrimToMax(value, 220);
        }

        internal static List<string> ExtractTopics(IEnumerable<string> texts)
        {
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string text in texts)
            {
                string lower = (text ?? string.Empty).ToLowerInvariant();
                AddTopicIfContains(lower, tags, "trade", "trade", "caravan", "goods", "торгівля", "караван");
                AddTopicIfContains(lower, tags, "peace", "peace", "ally", "ceasefire", "мир", "союзник");
                AddTopicIfContains(lower, tags, "threat", "threat", "war", "raid", "attack", "погроза", "війна", "напад");
                AddTopicIfContains(lower, tags, "aid", "aid", "help", "support", "рятувати", "підтримка");
                AddTopicIfContains(lower, tags, "trust", "trust", "respect", "favor", "довіра", "повага");
            }
            return tags.Take(4).ToList();
        }

        internal static void AddTopicIfContains(string lowerText, HashSet<string> tags, string label, params string[] keywords)
        {
            for (int i = 0; i < keywords.Length; i++)
            {
                if (lowerText.IndexOf(keywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    tags.Add(label);
                    return;
                }
            }
        }
    }
}
