using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Prompting;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Memory
{
    /// <summary>/// Dependencies: AIChatServiceAsync, RpgNpcDialogueArchive session model.
 /// Responsibility: orchestrate session-level compression and summary-first memory selection.
 ///</summary>
        internal sealed class RpgNpcDialogueArchiveManagerSessions : RpgNpcDialogueArchiveManagerCollaborator
    {
        internal RpgNpcDialogueArchiveManagerSessions(RpgNpcDialogueArchiveManager owner) : base(owner)
        {
        }


        internal void TryScheduleSessionCompression(RpgNpcDialogueArchive archive, int triggerTick)
        {
            if (archive?.Sessions == null || archive.Sessions.Count == 0)
            {
                return;
            }

            if (!AIChatServiceAsync.Instance.IsConfigured())
            {
                return;
            }

            if (!Owner.TryResolveCompressionSaveKey(nameof(TryScheduleSessionCompression), out string currentSaveKey))
            {
                return;
            }

            string retainedSessionId = RpgNpcDialogueArchiveManager.SelectLatestRetainedFullSession(archive)?.SessionId ?? string.Empty;
            List<RpgNpcDialogueSessionArchive> candidates = archive.Sessions
                .Where(session => Owner.ShouldScheduleCompressionForSession(
                    session,
                    retainedSessionId,
                    archive.PawnLoadId,
                    currentSaveKey,
                    triggerTick))
                .OrderByDescending(session => session.EndedTick)
                .ThenByDescending(session => session.StartedTick)
                .Take(MaxCompressionRequestsPerPass)
                .ToList();

            for (int i = 0; i < candidates.Count; i++)
            {
                Owner.RequestSessionCompression(archive, candidates[i], currentSaveKey, triggerTick);
            }
        }

        internal bool ShouldScheduleCompressionForSession(
            RpgNpcDialogueSessionArchive session,
            string retainedSessionId,
            int pawnLoadId,
            string saveKey,
            int triggerTick)
        {
            if (session == null || string.IsNullOrWhiteSpace(session.SessionId))
            {
                return false;
            }

            if (string.Equals(session.SessionId, retainedSessionId, StringComparison.Ordinal))
            {
                return false;
            }

            if (!session.IsFinalized)
            {
                return false;
            }

            if (session.Turns == null || session.Turns.Count == 0 || RpgNpcDialogueArchiveManager.CountDialogueTurns(session.Turns) <= 0)
            {
                return false;
            }

            if (string.Equals(session.SummaryState, RpgNpcDialogueSessionSummaryState.Compressed, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string compressionKey = RpgNpcDialogueArchiveManager.BuildCompressionKey(saveKey, pawnLoadId, session.SessionId);
            if (_compressionInFlight.Contains(compressionKey))
            {
                return false;
            }

            bool failedRecently =
                string.Equals(session.SummaryState, RpgNpcDialogueSessionSummaryState.SummaryFailed, StringComparison.OrdinalIgnoreCase) &&
                session.LastSummaryAttemptTick > 0 &&
                triggerTick - session.LastSummaryAttemptTick < CompressionRetryCooldownTicks;
            return !failedRecently;
        }

        internal void RequestSessionCompression(
            RpgNpcDialogueArchive archive,
            RpgNpcDialogueSessionArchive session,
            string requestSaveKey,
            int triggerTick)
        {
            if (archive == null || session == null || string.IsNullOrWhiteSpace(session.SessionId))
            {
                return;
            }

            string compressionKey = RpgNpcDialogueArchiveManager.BuildCompressionKey(requestSaveKey, archive.PawnLoadId, session.SessionId);
            if (!_compressionInFlight.Add(compressionKey))
            {
                return;
            }

            session.LastSummaryAttemptTick = triggerTick;
            List<ChatMessageData> request = RpgNpcDialogueArchiveManager.BuildSessionSummaryRequestMessages(archive, session);
            if (request == null || request.Count == 0)
            {
                _compressionInFlight.Remove(compressionKey);
                Owner.MarkSummaryCompressionFailed(archive, session);
                return;
            }

            AIChatServiceAsync.Instance.SendChatRequestAsync(
                request,
                onSuccess: response =>
                {
                    lock (_syncRoot)
                    {
                        _compressionInFlight.Remove(compressionKey);
                        if (!_archiveCache.TryGetValue(archive.PawnLoadId, out RpgNpcDialogueArchive currentArchive) ||
                            currentArchive == null)
                        {
                            return;
                        }

                        RpgNpcDialogueSessionArchive currentSession = RpgNpcDialogueArchiveManager.FindSession(currentArchive, session.SessionId);
                        if (currentSession == null)
                        {
                            return;
                        }

                        if (!Owner.TryResolveCompressionSaveKey("compression_success_callback", out string currentSaveKey) ||
                            !string.Equals(requestSaveKey, currentSaveKey, StringComparison.Ordinal))
                        {
                            Log.Warning(
                                "[RimAI.Relations] rpg_archive_compression dropped due to saveKey mismatch. " +
                                $"request_save_key={requestSaveKey}, current_save_key={currentSaveKey}, " +
                                $"archive_pawn_load_id={archive.PawnLoadId}, session_id={session.SessionId}");
                            return;
                        }

                        if (!currentSession.IsFinalized)
                        {
                            return;
                        }

                        string retainedSessionId = RpgNpcDialogueArchiveManager.SelectLatestRetainedFullSession(currentArchive)?.SessionId ?? string.Empty;
                        if (string.Equals(currentSession.SessionId, retainedSessionId, StringComparison.Ordinal))
                        {
                            return;
                        }

                        string summary = RpgNpcDialogueArchiveManager.NormalizeToSingleSentenceSummary(response);
                        if (string.IsNullOrWhiteSpace(summary))
                        {
                            Owner.MarkSummaryCompressionFailed(currentArchive, currentSession);
                            return;
                        }

                        currentSession.SummaryText = summary;
                        currentSession.SummaryState = RpgNpcDialogueSessionSummaryState.Compressed;
                        currentSession.LastSummaryAttemptTick = Find.TickManager?.TicksGame ?? currentSession.LastSummaryAttemptTick;
                        currentSession.TurnCount = Math.Max(currentSession.TurnCount, RpgNpcDialogueArchiveManager.CountDialogueTurns(currentSession.Turns));
                        currentSession.IsFinalized = true;
                        currentSession.Turns.Clear();
                        RpgNpcDialogueArchiveManager.NormalizeArchiveTurns(currentArchive);
                        Owner.InvalidatePromptMemoryCacheLockless();
                        Owner.SaveArchiveToFile(currentArchive);
                    }
                },
                onError: _ =>
                {
                    lock (_syncRoot)
                    {
                        _compressionInFlight.Remove(compressionKey);
                        if (!_archiveCache.TryGetValue(archive.PawnLoadId, out RpgNpcDialogueArchive currentArchive) ||
                            currentArchive == null)
                        {
                            return;
                        }

                        RpgNpcDialogueSessionArchive currentSession = RpgNpcDialogueArchiveManager.FindSession(currentArchive, session.SessionId);
                        if (currentSession == null)
                        {
                            return;
                        }

                        if (!Owner.TryResolveCompressionSaveKey("compression_error_callback", out string currentSaveKey) ||
                            !string.Equals(requestSaveKey, currentSaveKey, StringComparison.Ordinal))
                        {
                            Log.Warning(
                                "[RimAI.Relations] rpg_archive_compression error callback dropped due to saveKey mismatch. " +
                                $"request_save_key={requestSaveKey}, current_save_key={currentSaveKey}, " +
                                $"archive_pawn_load_id={archive.PawnLoadId}, session_id={session.SessionId}");
                            return;
                        }

                        Owner.MarkSummaryCompressionFailed(currentArchive, currentSession);
                    }
                },
                usageChannel: DialogueUsageChannel.Rpg,
                debugSource: AIRequestDebugSource.ArchiveCompression);
        }

        internal void MarkSummaryCompressionFailed(RpgNpcDialogueArchive archive, RpgNpcDialogueSessionArchive session)
        {
            if (archive == null || session == null)
            {
                return;
            }

            session.SummaryState = RpgNpcDialogueSessionSummaryState.SummaryFailed;
            session.LastSummaryAttemptTick = Find.TickManager?.TicksGame ?? session.LastSummaryAttemptTick;
            session.TurnCount = Math.Max(session.TurnCount, RpgNpcDialogueArchiveManager.CountDialogueTurns(session.Turns));
            RpgNpcDialogueArchiveManager.NormalizeArchiveTurns(archive);
            Owner.InvalidatePromptMemoryCacheLockless();
            Owner.SaveArchiveToFile(archive);
        }

        internal static List<ChatMessageData> BuildSessionSummaryRequestMessages(
            RpgNpcDialogueArchive archive,
            RpgNpcDialogueSessionArchive session)
        {
            bool contractReady = RelationsMod.Settings?.EnsureRpgArchiveCompressionContractReady() ?? false;
            if (!contractReady)
            {
                Log.Warning(
                    "[RimAI.Relations] rpg_archive_compression skipped: output contract is invalid after repair. " +
                    $"archive_pawn_load_id={(archive?.PawnLoadId ?? -1)}, session_id={session?.SessionId ?? string.Empty}");
                return new List<ChatMessageData>();
            }

            List<RpgNpcDialogueTurnArchive> turns = RpgNpcDialogueArchiveManager.GetSessionTurns(session);
            if (turns.Count == 0)
            {
                return new List<ChatMessageData>();
            }

            Pawn npcPawn = RpgNpcDialogueArchiveManager.ResolveArchiveNpcPawn(archive);
            if (npcPawn == null)
            {
                Log.Warning(
                    "[RimAI.Relations] rpg_archive_compression skipped: archive NPC pawn is missing. " +
                    $"archive_pawn_load_id={(archive?.PawnLoadId ?? -1)}, session_id={session?.SessionId ?? string.Empty}");
                return new List<ChatMessageData>();
            }

            Pawn interlocutorPawn = RpgNpcDialogueArchiveManager.ResolveArchiveInterlocutorPawn(archive, session, npcPawn);
            string npcName = RpgNpcDialogueArchiveManager.ResolvePromptPawnName(npcPawn, archive?.PawnName, "NPC");
            string interlocutorName = RpgNpcDialogueArchiveManager.ResolvePromptPawnName(
                interlocutorPawn,
                session?.InterlocutorName ?? archive?.LastInterlocutorName,
                "Interlocutor");
            string transcript = RpgNpcDialogueArchiveManager.BuildSessionTranscript(turns);
            string systemPrompt = ToolPromptRenderer.RenderArchiveCompressionPrompt(
                npcName,
                interlocutorName,
                transcript);
            return new List<ChatMessageData>
            {
                new ChatMessageData
                {
                    role = "system",
                    content = systemPrompt
                }
            };
        }

        internal static Pawn ResolveArchiveNpcPawn(RpgNpcDialogueArchive archive)
        {
            int pawnLoadId = archive?.PawnLoadId ?? -1;
            return RpgNpcDialogueArchiveManager.FindPawnByLoadId(pawnLoadId);
        }

        internal static Pawn ResolveArchiveInterlocutorPawn(
            RpgNpcDialogueArchive archive,
            RpgNpcDialogueSessionArchive session,
            Pawn npcPawn)
        {
            Pawn sessionPawn = RpgNpcDialogueArchiveManager.FindPawnByLoadId(session?.InterlocutorPawnLoadId ?? -1);
            if (sessionPawn != null && sessionPawn != npcPawn)
            {
                return sessionPawn;
            }

            Pawn archivePawn = RpgNpcDialogueArchiveManager.FindPawnByLoadId(archive?.LastInterlocutorPawnLoadId ?? -1);
            if (archivePawn != null && archivePawn != npcPawn)
            {
                return archivePawn;
            }

            Log.Warning(
                "[RimAI.Relations] rpg_archive_compression has no bindable interlocutor pawn; bind NPC only. " +
                $"archive_pawn_load_id={(archive?.PawnLoadId ?? -1)}, " +
                $"session_interlocutor_load_id={(session?.InterlocutorPawnLoadId ?? -1)}, " +
                $"archive_last_interlocutor_load_id={(archive?.LastInterlocutorPawnLoadId ?? -1)}, " +
                $"session_id={session?.SessionId ?? string.Empty}");
            return null;
        }

        internal static string ResolvePromptPawnName(Pawn pawn, string fallback, string defaultName)
        {
            string pawnName = pawn?.LabelShortCap ?? pawn?.LabelShort ?? pawn?.Name?.ToStringShort;
            if (!string.IsNullOrWhiteSpace(pawnName))
            {
                return pawnName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(fallback))
            {
                return fallback.Trim();
            }

            return defaultName;
        }

        internal static string BuildSessionTranscript(List<RpgNpcDialogueTurnArchive> turns)
        {
            var sb = new StringBuilder();
            int maxTurns = Math.Min(40, turns?.Count ?? 0);
            int start = Math.Max(0, (turns?.Count ?? 0) - maxTurns);
            for (int i = start; i < turns.Count; i++)
            {
                RpgNpcDialogueTurnArchive turn = turns[i];
                if (turn == null || string.IsNullOrWhiteSpace(turn.Text))
                {
                    continue;
                }

                string role = turn.IsPlayer ? "Player" : "NPC";
                string speaker = !string.IsNullOrWhiteSpace(turn.SpeakerName) ? turn.SpeakerName : role;
                sb.Append("- ")
                    .Append(speaker)
                    .Append(": ")
                    .Append(RpgNpcDialogueArchiveManager.TrimForPrompt(turn.Text, 160))
                    .Append('\n');
            }

            return sb.ToString().Trim();
        }

        internal static string NormalizeToSingleSentenceSummary(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            string text = raw
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();
            while (text.Contains("  "))
            {
                text = text.Replace("  ", " ");
            }

            if (text.StartsWith("\"", StringComparison.Ordinal) && text.EndsWith("\"", StringComparison.Ordinal) && text.Length > 1)
            {
                text = text.Substring(1, text.Length - 2).Trim();
            }

            int sentenceEnd = RpgNpcDialogueArchiveManager.FindFirstSentenceEnd(text);
            if (sentenceEnd > 0 && sentenceEnd < text.Length - 1)
            {
                text = text.Substring(0, sentenceEnd + 1).Trim();
            }

            if (text.Length > CompressedSummaryMaxChars)
            {
                text = text.Substring(0, CompressedSummaryMaxChars - 3).TrimEnd() + "...";
            }

            return text;
        }

        internal static int FindFirstSentenceEnd(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return -1;
            }

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '.' || c == '!' || c == '?' || c == '。' || c == '！' || c == '？')
                {
                    return i;
                }
            }

            return -1;
        }

        internal static string BuildCompressionKey(string saveKey, int pawnLoadId, string sessionId)
        {
            return $"{saveKey ?? string.Empty}|{pawnLoadId}|{sessionId}";
        }

        internal bool TryResolveCompressionSaveKey(string operationName, out string saveKey)
        {
            saveKey = string.Empty;
            try
            {
                saveKey = CurrentSaveKey;
                return !string.IsNullOrWhiteSpace(saveKey);
            }
            catch (InvalidOperationException ex)
            {
                Log.Warning($"[RimAI.Relations] rpg_archive_compression skipped in {operationName}: {ex.Message}");
                return false;
            }
        }

        internal static RpgNpcDialogueSessionArchive SelectLatestRetainedFullSession(RpgNpcDialogueArchive archive)
        {
            if (archive?.Sessions == null || archive.Sessions.Count == 0)
            {
                return null;
            }

            return archive.Sessions
                .Where(session =>
                    session != null &&
                    session.TurnCount >= 2 &&
                    session.Turns != null &&
                    session.Turns.Count > 0 &&
                    !string.Equals(session.SummaryState, RpgNpcDialogueSessionSummaryState.Compressed, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(session => session.EndedTick)
                .ThenByDescending(session => session.StartedTick)
                .ThenByDescending(session => session.Turns.Max(turn => turn?.TurnSequence ?? 0L))
                .FirstOrDefault();
        }

        internal static List<RpgNpcDialogueTurnArchive> GetSessionTurns(RpgNpcDialogueSessionArchive session)
        {
            return session?.Turns?
                .Where(turn => turn != null && !string.IsNullOrWhiteSpace(turn.Text))
                .OrderBy(turn => turn.GameTick)
                .ThenBy(turn => turn.TurnSequence)
                .ToList() ?? new List<RpgNpcDialogueTurnArchive>();
        }

        internal static List<RpgNpcDialogueSessionArchive> GetCompressedSessionsForInjection(RpgNpcDialogueArchive archive)
        {
            return archive?.Sessions?
                .Where(session =>
                    session != null &&
                    string.Equals(session.SummaryState, RpgNpcDialogueSessionSummaryState.Compressed, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(session.SummaryText))
                .OrderByDescending(session => session.EndedTick)
                .ThenByDescending(session => session.StartedTick)
                .ToList() ?? new List<RpgNpcDialogueSessionArchive>();
        }

        internal static void AppendCompressedSessionSummaries(
            StringBuilder sb,
            List<RpgNpcDialogueSessionArchive> compressedSessions,
            int maxItems,
            int maxChars)
        {
            if (sb == null || compressedSessions == null || compressedSessions.Count == 0)
            {
                return;
            }

            int itemLimit = Math.Max(1, maxItems);
            int charLimit = Math.Max(120, maxChars);
            int usedChars = 0;
            int emitted = 0;
            for (int i = 0; i < compressedSessions.Count && emitted < itemLimit; i++)
            {
                RpgNpcDialogueSessionArchive session = compressedSessions[i];
                if (session == null || string.IsNullOrWhiteSpace(session.SummaryText))
                {
                    continue;
                }

                string line = $"- {RpgNpcDialogueArchiveManager.TrimForPrompt(session.SummaryText, 180)}";
                if (usedChars + line.Length > charLimit)
                {
                    break;
                }

                if (emitted == 0)
                {
                    sb.AppendLine("Historical session summaries:");
                }

                sb.AppendLine(line);
                usedChars += line.Length;
                emitted++;
            }
        }
        }

}
