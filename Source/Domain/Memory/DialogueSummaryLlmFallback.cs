using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Prompting;
using RimWorld;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Memory
{
    /// <summary>
    /// LLM fallback queueing and context builders for cross-channel dialogue summaries.
    /// </summary>
    internal static class DialogueSummaryLlmFallback
    {
        internal static void TryQueueLlmFallback(Faction faction, CrossChannelSummaryRecord record, string context)
        {
            if (faction == null || record == null || record.Confidence >= DialogueSummaryService.LowConfidenceThreshold)
            {
                return;
            }

            if (!AIChatServiceAsync.Instance.IsConfigured())
            {
                return;
            }

            RimTalkPromptChannel rootChannel = record.Source == CrossChannelSummarySource.RpgDepart
                ? RimTalkPromptChannel.Rpg
                : RimTalkPromptChannel.Diplomacy;
            string systemPrompt = ToolPromptRenderer.RenderSummaryPrompt(
                context ?? string.Empty,
                faction?.Name ?? string.Empty);
            var messages = new List<ChatMessageData>
            {
                new ChatMessageData
                {
                    role = "system",
                    content = systemPrompt
                }
            };
            DialogueUsageChannel usageChannel = rootChannel == RimTalkPromptChannel.Rpg
                ? DialogueUsageChannel.Rpg
                : DialogueUsageChannel.Diplomacy;

            AIChatServiceAsync.Instance.SendChatRequestAsync(
                messages,
                onSuccess: response =>
                {
                    if (string.IsNullOrWhiteSpace(response))
                    {
                        return;
                    }

                    var upgraded = record.Clone();
                    ParseFallbackText(response, out string summary, out List<string> facts);
                    if (!string.IsNullOrWhiteSpace(summary))
                    {
                        upgraded.SummaryText = DialogueSummaryService.TrimToMax(summary, 280);
                    }
                    if (facts.Count > 0)
                    {
                        upgraded.KeyFacts = facts;
                    }

                    upgraded.Confidence = Mathf.Max(record.Confidence, 0.72f);
                    upgraded.IsLlmFallback = true;
                    upgraded.CreatedTimestamp = DateTime.UtcNow.Ticks;

                    if (upgraded.Source == CrossChannelSummarySource.DiplomacySession)
                    {
                        LeaderMemoryManager.Instance.UpsertDiplomacySessionSummary(faction, upgraded, DialogueSummaryService.MaxSummaryPoolPerType);
                    }
                    else if (upgraded.Source == CrossChannelSummarySource.RpgDepart)
                    {
                        LeaderMemoryManager.Instance.UpsertRpgDepartSummary(faction, upgraded, DialogueSummaryService.MaxSummaryPoolPerType);
                    }
                },
                onError: _ => { },
                usageChannel: usageChannel,
                debugSource: AIRequestDebugSource.MemorySummary);
        }

        internal static void ParseFallbackText(string raw, out string summary, out List<string> facts)
        {
            summary = string.Empty;
            facts = new List<string>();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return;
            }

            string[] lines = raw.Replace("\r", string.Empty).Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i]?.Trim() ?? string.Empty;
                if (line.Length == 0)
                {
                    continue;
                }

                if (line.StartsWith("Summary:", StringComparison.OrdinalIgnoreCase))
                {
                    summary = line.Substring("Summary:".Length).Trim();
                    continue;
                }

                if (line.StartsWith("-"))
                {
                    string fact = line.Substring(1).Trim();
                    if (!string.IsNullOrWhiteSpace(fact))
                    {
                        facts.Add(DialogueSummaryService.TrimToMax(fact, 80));
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(summary))
            {
                summary = DialogueSummaryService.TrimToMax(raw.Trim(), 220);
            }

            if (facts.Count > DialogueSummaryService.MaxKeyFactsPerSummary)
            {
                facts = facts.Take(DialogueSummaryService.MaxKeyFactsPerSummary).ToList();
            }
        }

        internal static string BuildDiplomacyFallbackContext(Faction faction, List<DialogueMessageData> delta)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Faction: {faction?.Name ?? "Unknown"}");
            sb.AppendLine("Context: diplomacy session closed; summarize new messages only.");
            List<DialogueMessageData> recentMessages = delta
                .Where(x => x != null && !x.IsSystemMessage() && !string.IsNullOrWhiteSpace(x.message))
                .ToList();
            int start = Math.Max(0, recentMessages.Count - 10);
            for (int i = start; i < recentMessages.Count; i++)
            {
                DialogueMessageData msg = recentMessages[i];
                string role = msg.isPlayer ? "Player" : "Faction";
                sb.AppendLine($"{role}: {DialogueSummaryService.TrimToMax(msg.message, 180)}");
            }
            return sb.ToString();
        }

        internal static string BuildRpgFallbackContext(RpgDialogueTraceSnapshot trace)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Faction: {trace.Faction?.Name ?? "Unknown"}");
            sb.AppendLine($"Pawn: {trace.Pawn?.LabelShort ?? "UnknownPawn"}");
            sb.AppendLine("Context: pawn is exiting map; summarize recent RPG interaction.");
            List<RpgDialogueTurn> turns = trace.Turns ?? new List<RpgDialogueTurn>();
            int start = Math.Max(0, turns.Count - 10);
            for (int i = start; i < turns.Count; i++)
            {
                RpgDialogueTurn turn = turns[i];
                string role = turn.IsPlayer ? "Player" : "NPC";
                sb.AppendLine($"{role}: {DialogueSummaryService.TrimToMax(turn.Text, 180)}");
            }
            return sb.ToString();
        }

        internal static string BuildRpgSessionCloseContext(Pawn initiator, Pawn target, List<ChatMessageData> chatHistory)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Faction: {target?.Faction?.Name ?? initiator?.Faction?.Name ?? "Unknown"}");
            sb.AppendLine($"NPC: {target?.LabelShort ?? target?.Name?.ToStringShort ?? "UnknownPawn"}");
            sb.AppendLine("Context: RPG dialogue session ended; summarize recent interaction.");
            if (chatHistory == null || chatHistory.Count == 0)
            {
                return sb.ToString();
            }

            List<ChatMessageData> recentMessages = chatHistory
                .Where(m => m != null &&
                    !string.IsNullOrWhiteSpace(m.content) &&
                    !string.Equals(m.role, "system", StringComparison.OrdinalIgnoreCase))
                .ToList();
            int start = Math.Max(0, recentMessages.Count - 10);
            for (int i = start; i < recentMessages.Count; i++)
            {
                ChatMessageData msg = recentMessages[i];
                string role = string.Equals(msg.role, "user", StringComparison.OrdinalIgnoreCase) ? "Player" : "NPC";
                sb.AppendLine($"{role}: {DialogueSummaryService.TrimToMax(msg.content, 180)}");
            }
            return sb.ToString();
        }
    }
}
