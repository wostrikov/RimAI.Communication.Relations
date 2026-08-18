using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Communication.Relations.Guards;
using Ustas.RimAI.Core.Storage;
using Ustas.RimAI.Core.Relations;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Memory
{
    internal sealed class RpgNpcArchiveSlice6 : RpgNpcDialogueArchiveManagerCollaborator
    {
        internal RpgNpcArchiveSlice6(RpgNpcDialogueArchiveManager owner) : base(owner)
        {
        }

internal static bool IsDiplomacySummaryTurn(string text)
        {
            return !string.IsNullOrWhiteSpace(text) &&
                text.StartsWith(DiplomacySummaryPrefix, StringComparison.Ordinal);
        }

internal static string StripDiplomacySummaryPrefix(string text)
        {
            if (!RpgNpcDialogueArchiveManager.IsDiplomacySummaryTurn(text))
            {
                return text?.Trim() ?? string.Empty;
            }

            return text.Substring(DiplomacySummaryPrefix.Length).Trim();
        }

internal static Pawn ResolveFactionLeaderPawn(Faction faction)
        {
            Pawn leader = faction?.leader;
            if (leader == null || leader.Dead || leader.Destroyed)
            {
                return null;
            }

            return leader;
        }

internal static Pawn ResolveCounterpartForDiplomacySummary(Pawn participant, Pawn negotiator, Pawn factionLeader)
        {
            if (participant != null && negotiator != null && participant.thingIDNumber == negotiator.thingIDNumber)
            {
                return factionLeader;
            }

            if (participant != null && factionLeader != null && participant.thingIDNumber == factionLeader.thingIDNumber)
            {
                return negotiator;
            }

            return negotiator ?? factionLeader;
        }

internal static string ResolveFallbackCounterpartName(Pawn counterpart, Faction faction)
        {
            if (counterpart != null)
            {
                return RpgNpcDialogueArchiveManager.ResolvePawnName(counterpart);
            }

            if (!string.IsNullOrWhiteSpace(faction?.Name))
            {
                return faction.Name;
            }

            return "FactionCounterpart";
        }

internal static string BuildDiplomacySummaryText(
            Faction faction,
            List<DialogueMessageData> allMessages,
            int baselineMessageCount)
        {
            if (allMessages == null || allMessages.Count <= baselineMessageCount)
            {
                return string.Empty;
            }

            int start = Math.Max(0, Math.Min(baselineMessageCount, allMessages.Count));
            List<DialogueMessageData> delta = allMessages
                .Skip(start)
                .Where(m => m != null && !m.IsSystemMessage() && !string.IsNullOrWhiteSpace(m.message))
                .ToList();
            if (delta.Count == 0)
            {
                return string.Empty;
            }

            string playerLast = delta.LastOrDefault(m => m.isPlayer)?.message ?? string.Empty;
            string factionLast = delta.LastOrDefault(m => !m.isPlayer)?.message ?? string.Empty;
            string topic = RpgNpcDialogueArchiveManager.DetectDiplomacyTopic(delta.Select(m => m.message));
            string factionName = !string.IsNullOrWhiteSpace(faction?.Name) ? faction.Name : "the faction";
            return
                $"Diplomacy session with {factionName} on topic '{topic}'. " +
                $"Player intent: {RpgNpcDialogueArchiveManager.TrimForPrompt(playerLast, 70)}. " +
                $"Faction stance: {RpgNpcDialogueArchiveManager.TrimForPrompt(factionLast, 70)}.";
        }

internal static string DetectDiplomacyTopic(IEnumerable<string> lines)
        {
            if (lines == null)
            {
                return "general";
            }

            string joined = string.Join(" ", lines.Where(l => !string.IsNullOrWhiteSpace(l))).ToLowerInvariant();
            if (joined.Contains("trade") || joined.Contains("交易") || joined.Contains("商队")) return "trade";
            if (joined.Contains("peace") || joined.Contains("war") || joined.Contains("和平") || joined.Contains("宣战")) return "war-peace";
            if (joined.Contains("aid") || joined.Contains("help") || joined.Contains("援助") || joined.Contains("支援")) return "aid";
            if (joined.Contains("gift") || joined.Contains("礼物")) return "gift";
            return "general";
        }

internal static List<RpgNpcDialogueTurnArchive> BuildRelevantSelfTurns(
            List<RpgNpcDialogueTurnArchive> sourceTurns,
            RpgNpcDialogueArchive archive,
            Pawn targetNpc,
            Pawn currentInterlocutor,
            string interlocutorName)
        {
            var allTurns = sourceTurns?
                .Where(turn =>
                    turn != null &&
                    !string.IsNullOrWhiteSpace(turn.Text) &&
                    !RpgNpcDialogueArchiveManager.IsDiplomacySummaryTurn(turn.Text))
                .OrderBy(turn => turn.GameTick)
                .ThenBy(turn => turn.TurnSequence)
                .ToList() ?? new List<RpgNpcDialogueTurnArchive>();
            if (allTurns.Count == 0)
            {
                return allTurns;
            }

            int selfId = targetNpc?.thingIDNumber ?? archive?.PawnLoadId ?? -1;
            int interlocutorId = currentInterlocutor?.thingIDNumber ?? -1;
            if (selfId > 0)
            {
                IEnumerable<RpgNpcDialogueTurnArchive> selfById = allTurns
                    .Where(turn => turn.SpeakerPawnLoadId == selfId)
                    .ToList();

                if (interlocutorId > 0)
                {
                    List<RpgNpcDialogueTurnArchive> pairById = selfById
                        .Where(turn => turn.InterlocutorPawnLoadId == interlocutorId)
                        .ToList();
                    if (pairById.Count > 0)
                    {
                        return pairById;
                    }
                }

                List<RpgNpcDialogueTurnArchive> byId = selfById.ToList();
                if (byId.Count > 0)
                {
                    return byId;
                }
            }

            string selfName = targetNpc?.LabelShort ?? archive?.PawnName ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(selfName))
            {
                IEnumerable<RpgNpcDialogueTurnArchive> selfByName = allTurns
                    .Where(turn => string.Equals(turn.SpeakerName, selfName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (!RpgNpcDialogueArchiveManager.IsPlaceholderInterlocutorName(interlocutorName))
                {
                    List<RpgNpcDialogueTurnArchive> pairByName = selfByName
                        .Where(turn => string.Equals(turn.InterlocutorName, interlocutorName, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    if (pairByName.Count > 0)
                    {
                        return pairByName;
                    }
                }

                List<RpgNpcDialogueTurnArchive> byName = selfByName.ToList();
                if (byName.Count > 0)
                {
                    return byName;
                }
            }

            return allTurns.Where(turn => !turn.IsPlayer).ToList();
        }

internal static List<RpgNpcDialogueTurnArchive> BuildChronologicalDialogueTurns(
            List<RpgNpcDialogueTurnArchive> selfTurns,
            List<RpgNpcDialogueTurnArchive> interlocutorTurns)
        {
            IEnumerable<RpgNpcDialogueTurnArchive> merged = (selfTurns ?? new List<RpgNpcDialogueTurnArchive>())
                .Concat(interlocutorTurns ?? new List<RpgNpcDialogueTurnArchive>());

            return merged
                .Where(turn => turn != null && !string.IsNullOrWhiteSpace(turn.Text))
                .GroupBy(turn =>
                    $"{turn.GameTick}|{turn.TurnSequence}|{turn.SpeakerPawnLoadId}|{turn.InterlocutorPawnLoadId}|{turn.Text.Trim()}")
                .Select(group => group.First())
                .OrderBy(turn => turn.GameTick)
                .ThenBy(turn => turn.TurnSequence)
                .ToList();
        }

internal static List<RpgNpcDialogueTurnArchive> BuildRelevantInterlocutorTurns(
            List<RpgNpcDialogueTurnArchive> sourceTurns,
            RpgNpcDialogueArchive archive,
            Pawn currentInterlocutor,
            string interlocutorName)
        {
            var allTurns = sourceTurns?
                .Where(turn =>
                    turn != null &&
                    !string.IsNullOrWhiteSpace(turn.Text) &&
                    !RpgNpcDialogueArchiveManager.IsDiplomacySummaryTurn(turn.Text))
                .OrderBy(turn => turn.GameTick)
                .ThenBy(turn => turn.TurnSequence)
                .ToList() ?? new List<RpgNpcDialogueTurnArchive>();

            if (allTurns.Count == 0)
            {
                return allTurns;
            }

            int interlocutorId = currentInterlocutor?.thingIDNumber ?? -1;
            if (interlocutorId > 0)
            {
                List<RpgNpcDialogueTurnArchive> strictById = allTurns
                    .Where(turn => turn.SpeakerPawnLoadId == interlocutorId)
                    .ToList();
                if (strictById.Count > 0)
                {
                    return strictById;
                }
            }

            if (!RpgNpcDialogueArchiveManager.IsPlaceholderInterlocutorName(interlocutorName))
            {
                List<RpgNpcDialogueTurnArchive> byName = allTurns
                    .Where(turn => string.Equals(turn.SpeakerName, interlocutorName, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (byName.Count > 0)
                {
                    return byName;
                }
            }

            List<RpgNpcDialogueTurnArchive> playerTurns = allTurns.Where(turn => turn.IsPlayer).ToList();
            if (playerTurns.Count > 0)
            {
                return playerTurns;
            }

            return allTurns.Where(turn => RpgNpcDialogueArchiveManager.IsInterlocutorTurnFallback(turn, archive)).ToList();
        }

internal static string ResolvePromptSpeakerName(
            RpgNpcDialogueTurnArchive turn,
            Pawn selfPawn,
            string selfName,
            Pawn currentInterlocutor,
            string interlocutorName)
        {
            if (turn == null)
            {
                return "UnknownSpeaker";
            }

            int selfId = selfPawn?.thingIDNumber ?? -1;
            if (selfId > 0 && turn.SpeakerPawnLoadId == selfId)
            {
                return string.IsNullOrWhiteSpace(selfName) ? "You" : selfName;
            }

            int interlocutorId = currentInterlocutor?.thingIDNumber ?? -1;
            if (interlocutorId > 0 && turn.SpeakerPawnLoadId == interlocutorId)
            {
                return RpgNpcDialogueArchiveManager.IsPlaceholderInterlocutorName(interlocutorName) ? "Interlocutor" : interlocutorName;
            }

            return RpgNpcDialogueArchiveManager.ResolveTurnSpeakerName(turn, interlocutorName);
        }

internal static bool IsInterlocutorTurnFallback(RpgNpcDialogueTurnArchive turn, RpgNpcDialogueArchive archive)
        {
            if (turn == null || string.IsNullOrWhiteSpace(turn.Text))
            {
                return false;
            }

            if (turn.IsPlayer)
            {
                return true;
            }

            if (archive == null)
            {
                return false;
            }

            if (archive.LastInterlocutorPawnLoadId > 0 && turn.SpeakerPawnLoadId > 0)
            {
                return archive.LastInterlocutorPawnLoadId == turn.SpeakerPawnLoadId;
            }

            return !string.IsNullOrWhiteSpace(archive.LastInterlocutorName) &&
                string.Equals(archive.LastInterlocutorName, turn.SpeakerName, StringComparison.OrdinalIgnoreCase);
        }

internal static string ResolveInterlocutorName(
            RpgNpcDialogueArchive archive,
            Pawn currentInterlocutor,
            List<RpgNpcDialogueTurnArchive> sourceTurns)
        {
            string currentName = RpgNpcDialogueArchiveManager.ResolveOptionalPawnName(currentInterlocutor);
            if (!string.IsNullOrWhiteSpace(currentName))
            {
                return currentName;
            }

            if (!RpgNpcDialogueArchiveManager.IsPlaceholderInterlocutorName(archive?.LastInterlocutorName))
            {
                return archive.LastInterlocutorName;
            }

            RpgNpcDialogueTurnArchive lastTurn = sourceTurns?
                .Where(turn => turn != null && !RpgNpcDialogueArchiveManager.IsPlaceholderInterlocutorName(turn.SpeakerName))
                .OrderByDescending(turn => turn.GameTick)
                .ThenByDescending(turn => turn.TurnSequence)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(lastTurn?.SpeakerName))
            {
                return lastTurn.SpeakerName;
            }

            return "CurrentInterlocutor";
        }

internal static string ResolveTurnSpeakerName(RpgNpcDialogueTurnArchive turn, string fallbackName)
        {
            if (!string.IsNullOrWhiteSpace(turn?.SpeakerName) &&
                !RpgNpcDialogueArchiveManager.IsPlaceholderInterlocutorName(turn.SpeakerName))
            {
                return turn.SpeakerName;
            }

            return string.IsNullOrWhiteSpace(fallbackName) ? "CurrentInterlocutor" : fallbackName;
        }

internal static string ResolveOptionalPawnName(Pawn pawn)
        {
            if (pawn == null)
            {
                return string.Empty;
            }

            return pawn.LabelShort ?? pawn.Name?.ToStringShort ?? pawn.Name?.ToStringFull ?? string.Empty;
        }

internal static bool IsPlaceholderInterlocutorName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ||
                string.Equals(value, "Interlocutor", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "CurrentInterlocutor", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "UnknownPawn", StringComparison.OrdinalIgnoreCase);
        }

internal static bool IsHostileIntent(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string lower = text.ToLowerInvariant();
            string[] keywords =
            {
                "kill", "murder", "attack", "hurt", "destroy", "threat", "hate",
                "杀", "死", "干掉", "攻击", "伤害", "威胁", "仇恨"
            };

            for (int i = 0; i < keywords.Length; i++)
            {
                if (lower.Contains(keywords[i]))
                {
                    return true;
                }
            }

            return false;
        }

internal static string TrimForPrompt(string text, int maxLen)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string value = text.Trim();
            if (value.Length <= maxLen)
            {
                return value;
            }

            if (maxLen <= 3)
            {
                return value.Substring(0, maxLen);
            }

            return value.Substring(0, maxLen - 3) + "...";
        }

internal void LogDebugMissingArchive(Pawn targetNpc, Pawn currentInterlocutor)
        {
            if (RelationsMod.Settings?.EnableDebugLogging != true)
            {
                return;
            }

            int targetId = targetNpc?.thingIDNumber ?? -1;
            int interlocutorId = currentInterlocutor?.thingIDNumber ?? -1;
            string targetName = RpgNpcDialogueArchiveManager.ResolvePawnName(targetNpc);
            string interlocutorName = RpgNpcDialogueArchiveManager.ResolveOptionalPawnName(currentInterlocutor);
            bool hasSaveContext = Owner.TryResolveArchiveDebugContext(out string saveKey, out string archiveDir);
            string contextSuffix = hasSaveContext
                ? $"saveKey={saveKey}, dir={archiveDir}"
                : "saveKey=<unresolved>, dir=<unresolved>";
            Log.Message(
                $"[RimAI.Relations] RPG memory skipped: no archive sessions for target={targetName}({targetId}), " +
                $"interlocutor={interlocutorName}({interlocutorId}), {contextSuffix}");
        }

internal bool TryResolveArchiveDebugContext(out string saveKey, out string archiveDir)
        {
            saveKey = string.Empty;
            archiveDir = string.Empty;
            try
            {
                saveKey = CurrentSaveKey;
                archiveDir = CurrentArchiveDirPath;
                return true;
            }
            catch (InvalidOperationException ex)
            {
                Log.Warning($"[RimAI.Relations] RPG memory debug context unresolved: {ex.Message}");
                return false;
            }
        }
    }
}
