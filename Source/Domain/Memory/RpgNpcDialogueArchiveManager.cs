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
using Ustas.RimAI.Communication.Relations.Diagnostics;

namespace Ustas.RimAI.Communication.Relations.Memory
{
    /// <summary>/// Dependencies: GameComponent_RPGManager, RimWorld save path, NPC dialogue turn feed.
 /// Responsibility: persist RPG dialogue archives per NPC into independent JSON files.
 ///</summary>
    public sealed class RpgNpcDialogueArchiveManager
    {
        internal RpgNpcDialogueArchiveManagerParts Parts;

        internal RpgNpcDialogueArchiveManager()
        {
            Parts = new RpgNpcDialogueArchiveManagerParts(this);
        }

        internal const string SaveRootDir = "Ustas.RimAI.Communication.Relations";
        internal const string SaveSubDir = "save_data";
        internal const string NpcArchiveSubDir = "rpg_npc_dialogues";
        internal const string PromptFolderName = "Prompt";
        internal const string NpcPromptSubDir = "NPC";
        internal const string DefaultSaveName = "Default";
        internal const string LegacyMigrationBackupDirName = "_migration_backup";
        internal const string LegacyDefaultBucketClaimMarker = ".legacy_default_bucket_claimed";
        internal const BindingFlags InstanceStringMemberBinding =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;
        internal const BindingFlags StaticStringMemberBinding =
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;
        internal const string DiplomacySummaryPrefix = "[DiplomacySummary] ";
        internal const int MaxTurnsPerNpc = 300;
        internal const int MaxSessionsPerNpc = 96;
        internal const int CompressionRetryCooldownTicks = 2500;
        internal const int MaxCompressionRequestsPerPass = 2;
        internal const int CompressedSummaryMaxChars = 220;
        internal const int MaxInjectedCompressedSessionSummaries = 4;
        internal const int MaxInjectedCompressedSessionSummaryChars = 900;

        internal static RpgNpcDialogueArchiveManager _instance;
        public static RpgNpcDialogueArchiveManager Instance => _instance ?? (_instance = new RpgNpcDialogueArchiveManager());

        internal readonly Dictionary<int, RpgNpcDialogueArchive> _archiveCache = new Dictionary<int, RpgNpcDialogueArchive>();
        internal readonly HashSet<string> _compressionInFlight = new HashSet<string>(StringComparer.Ordinal);
        internal readonly HashSet<string> _warmupInFlightSaveKeys = new HashSet<string>(StringComparer.Ordinal);
        internal readonly HashSet<int> _pendingWarmupCompressionTargets = new HashSet<int>();
        internal readonly object _syncRoot = new object();
        internal bool _cacheLoaded;
        internal bool _diplomacyMemorySubscribed;
        internal string _loadedSaveKey = string.Empty;
        internal string _resolvedSaveKey = string.Empty;
        internal string _lastResolvedSaveName = string.Empty;

        

        

        

        

        

        

        

        

        

        

        internal static void PublishRpgSessionFinalized(Pawn initiator, Pawn targetNpc, List<ChatMessageData> chatHistory)
        {
            string transcript = BuildRpgTranscript(initiator, targetNpc, chatHistory);
            if (string.IsNullOrWhiteSpace(transcript))
                return;
            RelationsDialogueLifecycle.PublishRpgSessionFinalized(new RpgSessionFinalizedArgs
            {
                Initiator = initiator,
                TargetNpc = targetNpc,
                Transcript = transcript,
                Participants = new object[] { initiator, targetNpc }
            });
        }

        internal static void PublishDiplomacySummaryRecorded(Pawn negotiator, Faction faction, List<DialogueMessageData> allMessages)
        {
            string transcript = BuildDiplomacyRoundMemoryTranscript(negotiator, faction, allMessages);
            if (string.IsNullOrWhiteSpace(transcript))
                return;
            RelationsDialogueLifecycle.PublishDiplomacySummaryRecorded(new DiplomacySummaryRecordedArgs
            {
                Negotiator = negotiator,
                Faction = faction,
                Transcript = transcript
            });
        }

        static string BuildRpgTranscript(Pawn initiator, Pawn targetNpc, List<ChatMessageData> chatHistory)
        {
            if (chatHistory == null || chatHistory.Count == 0)
                return string.Empty;
            string playerName = initiator?.LabelShort ?? "???";
            string npcName = targetNpc?.LabelShort ?? "???";
            var sb = new StringBuilder();
            foreach (ChatMessageData message in chatHistory)
            {
                if (message == null)
                    continue;
                if (string.Equals(message.role, "user", StringComparison.OrdinalIgnoreCase))
                    sb.Append(playerName).Append(": ").AppendLine(message.content);
                else if (string.Equals(message.role, "assistant", StringComparison.OrdinalIgnoreCase))
                    sb.Append(npcName).Append(": ").AppendLine(message.content);
            }
            return sb.ToString().TrimEnd();
        }

        static string BuildDiplomacyRoundMemoryTranscript(Pawn negotiator, Faction faction, List<DialogueMessageData> allMessages)
        {
            if (allMessages == null || allMessages.Count == 0)
                return string.Empty;
            string playerName = negotiator?.LabelShort ?? "???";
            string factionName = faction?.Name ?? "???";
            var sb = new StringBuilder();
            foreach (DialogueMessageData message in allMessages)
            {
                if (message == null || message.IsSystemMessage())
                    continue;
                if (message.isPlayer)
                    sb.Append(playerName).Append(": ").AppendLine(message.message);
                else
                    sb.Append(factionName).Append(": ").AppendLine(message.message);
            }
            return sb.ToString().TrimEnd();
        }

        internal string CurrentSaveKey
        {
            get
            {
                string resolved = ResolveCurrentSaveKey();
                if (!string.Equals(_resolvedSaveKey, resolved, StringComparison.Ordinal))
                {
                    _resolvedSaveKey = resolved;
                }
                return _resolvedSaveKey;
            }
        }

        internal string CurrentArchiveDirPath =>
            Path.Combine(CurrentPromptNpcRootPath, CurrentSaveKey, NpcArchiveSubDir);

        internal string CurrentPromptNpcRootPath
        {
            get
            {
                try
                {
                    ModContentPack mod = LoadedModManager.GetMod<RelationsMod>()?.Content;
                    if (mod != null)
                    {
                        string path = Path.Combine(mod.RootDir, PromptFolderName, NpcPromptSubDir);
                        if (!LocalStorage.Current.DirectoryExists(path))
                        {
                            LocalStorage.Current.CreateDirectory(path);
                        }
                        return path;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[RimAI.Relations] Failed to create archive directory: {ex.Message}");
                }

                string fallback = Path.Combine(GenFilePaths.ConfigFolderPath, SaveRootDir, PromptFolderName, NpcPromptSubDir);
                if (!LocalStorage.Current.DirectoryExists(fallback))
                {
                    LocalStorage.Current.CreateDirectory(fallback);
                }
                return fallback;
            }
        }

        

        

        

        internal string ResolveArchiveSourceDirectory()
        {
            return CurrentArchiveDirPath;
        }

        internal static bool DirectoryHasJsonFiles(string dir)
        {
            return LocalStorage.Current.DirectoryExists(dir) && LocalStorage.Current.GetFiles(dir, "*.json").Length > 0;
        }

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        internal static Pawn GetPlayerPawn(Pawn pawn)
        {
            return pawn != null && pawn.Faction != null && pawn.Faction.IsPlayer ? pawn : null;
        }

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        internal string ResolveCurrentSaveKey()
        {
            return SaveScopeKeyResolver.ResolveOrThrow();
        }

        

        

        

        

        

        

        

        

        

        internal string GetHashSaveKey(string saveName)
        {
            return $"Save_{ComputeStableHash(saveName).ToString(CultureInfo.InvariantCulture)}".SanitizeFileName();
        }

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        #region Facade forwards
        internal void ResetPromptMemoryCacheLockless() => Parts.PromptCache.ResetPromptMemoryCacheLockless();
        internal void InvalidatePromptMemoryCacheLockless() => Parts.PromptCache.InvalidatePromptMemoryCacheLockless();
        internal bool TryGetPromptMemoryCacheLockless(string cacheKey, out string memoryBlock) => Parts.PromptCache.TryGetPromptMemoryCacheLockless(cacheKey, out memoryBlock);
        internal void SetPromptMemoryCacheLockless(string cacheKey, string memoryBlock) => Parts.PromptCache.SetPromptMemoryCacheLockless(cacheKey, memoryBlock);
        internal static string BuildPromptMemoryCacheKey(int targetPawnLoadId, int interlocutorPawnLoadId, int summaryTurnLimit, int summaryCharBudget, int dayStamp) => RpgNpcDialogueArchiveManagerPromptCache.BuildPromptMemoryCacheKey(targetPawnLoadId, interlocutorPawnLoadId, summaryTurnLimit, summaryCharBudget, dayStamp);
        internal void TryScheduleSessionCompression(RpgNpcDialogueArchive archive, int triggerTick) => Parts.Sessions.TryScheduleSessionCompression(archive, triggerTick);
        internal bool ShouldScheduleCompressionForSession(RpgNpcDialogueSessionArchive session, string retainedSessionId, int pawnLoadId, string saveKey, int triggerTick) => Parts.Sessions.ShouldScheduleCompressionForSession(session, retainedSessionId, pawnLoadId, saveKey, triggerTick);
        internal void RequestSessionCompression(RpgNpcDialogueArchive archive, RpgNpcDialogueSessionArchive session, string requestSaveKey, int triggerTick) => Parts.Sessions.RequestSessionCompression(archive, session, requestSaveKey, triggerTick);
        internal void MarkSummaryCompressionFailed(RpgNpcDialogueArchive archive, RpgNpcDialogueSessionArchive session) => Parts.Sessions.MarkSummaryCompressionFailed(archive, session);
        internal static List<ChatMessageData> BuildSessionSummaryRequestMessages(RpgNpcDialogueArchive archive, RpgNpcDialogueSessionArchive session) => RpgNpcDialogueArchiveManagerSessions.BuildSessionSummaryRequestMessages(archive, session);
        internal static Pawn ResolveArchiveNpcPawn(RpgNpcDialogueArchive archive) => RpgNpcDialogueArchiveManagerSessions.ResolveArchiveNpcPawn(archive);
        internal static Pawn ResolveArchiveInterlocutorPawn(RpgNpcDialogueArchive archive, RpgNpcDialogueSessionArchive session, Pawn npcPawn) => RpgNpcDialogueArchiveManagerSessions.ResolveArchiveInterlocutorPawn(archive, session, npcPawn);
        internal static string ResolvePromptPawnName(Pawn pawn, string fallback, string defaultName) => RpgNpcDialogueArchiveManagerSessions.ResolvePromptPawnName(pawn, fallback, defaultName);
        internal static string BuildSessionTranscript(List<RpgNpcDialogueTurnArchive> turns) => RpgNpcDialogueArchiveManagerSessions.BuildSessionTranscript(turns);
        internal static string NormalizeToSingleSentenceSummary(string raw) => RpgNpcDialogueArchiveManagerSessions.NormalizeToSingleSentenceSummary(raw);
        internal static int FindFirstSentenceEnd(string text) => RpgNpcDialogueArchiveManagerSessions.FindFirstSentenceEnd(text);
        internal static string BuildCompressionKey(string saveKey, int pawnLoadId, string sessionId) => RpgNpcDialogueArchiveManagerSessions.BuildCompressionKey(saveKey, pawnLoadId, sessionId);
        internal bool TryResolveCompressionSaveKey(string operationName, out string saveKey) => Parts.Sessions.TryResolveCompressionSaveKey(operationName, out saveKey);
        internal static RpgNpcDialogueSessionArchive SelectLatestRetainedFullSession(RpgNpcDialogueArchive archive) => RpgNpcDialogueArchiveManagerSessions.SelectLatestRetainedFullSession(archive);
        internal static List<RpgNpcDialogueTurnArchive> GetSessionTurns(RpgNpcDialogueSessionArchive session) => RpgNpcDialogueArchiveManagerSessions.GetSessionTurns(session);
        internal static List<RpgNpcDialogueSessionArchive> GetCompressedSessionsForInjection(RpgNpcDialogueArchive archive) => RpgNpcDialogueArchiveManagerSessions.GetCompressedSessionsForInjection(archive);
        internal static void AppendCompressedSessionSummaries(StringBuilder sb, List<RpgNpcDialogueSessionArchive> compressedSessions, int maxItems, int maxChars) => RpgNpcDialogueArchiveManagerSessions.AppendCompressedSessionSummaries(sb, compressedSessions, maxItems, maxChars);
        public void BeginPromptMemoryWarmup(Pawn targetNpc, Pawn currentInterlocutor = null) => Parts.Warmup.BeginPromptMemoryWarmup(targetNpc, currentInterlocutor);
        internal void WarmupCacheInBackground(string saveKey, string sourceDir, int targetPawnLoadId) => Parts.Warmup.WarmupCacheInBackground(saveKey, sourceDir, targetPawnLoadId);
        internal static Dictionary<int, RpgNpcDialogueArchive> LoadArchiveSnapshot(string sourceDir, string saveKey) => RpgNpcDialogueArchiveManagerWarmup.LoadArchiveSnapshot(sourceDir, saveKey);
        internal static bool IsArchiveOwnedBySaveKey(RpgNpcDialogueArchive archive, string saveKey) => RpgNpcDialogueArchiveManagerWarmup.IsArchiveOwnedBySaveKey(archive, saveKey);
        internal static void MergeArchiveSnapshot(RpgNpcDialogueArchive target, RpgNpcDialogueArchive incoming) => RpgNpcDialogueArchiveManagerWarmup.MergeArchiveSnapshot(target, incoming);
        internal void FlushPendingWarmupCompressionLockless(int tick) => Parts.Warmup.FlushPendingWarmupCompressionLockless(tick);
        #endregion
    
        #region Cluster forwards
        public void OnNewGame() => Parts.Slice1.OnNewGame();
        public void OnLoadedGame() => Parts.Slice1.OnLoadedGame();
        public void OnAfterGameLoad() => Parts.Slice1.OnAfterGameLoad();
        internal void SubscribeToDiplomacyMemoryEvents() => Parts.Slice1.SubscribeToDiplomacyMemoryEvents();
        internal void OnDiplomacyMemoryChanged(DiplomacyMemoryChangedEventArgs args) => Parts.Slice1.OnDiplomacyMemoryChanged(args);
        internal static bool RemoveDiplomacySummaryTurnsFromArchive(RpgNpcDialogueArchive archive) => RpgNpcArchiveSlice1.RemoveDiplomacySummaryTurnsFromArchive(archive);
        public void OnBeforeGameSave() => Parts.Slice1.OnBeforeGameSave();
        public void RecordTurn(Pawn initiator, Pawn targetNpc, bool isPlayerSpeaker, string text, int tick, string sessionId = null) => Parts.Slice1.RecordTurn(initiator, targetNpc, isPlayerSpeaker, text, tick, sessionId);
        public void FinalizeSession(Pawn initiator, Pawn targetNpc, string sessionId, List<ChatMessageData> chatHistory) => Parts.Slice1.FinalizeSession(initiator, targetNpc, sessionId, chatHistory);
        public void RecordDiplomacySummary(Pawn negotiator, Faction faction, List<DialogueMessageData> allMessages, int baselineMessageCount) => Parts.Slice1.RecordDiplomacySummary(negotiator, faction, allMessages, baselineMessageCount);
        internal void EnsureCacheLoaded() => Parts.Slice1.EnsureCacheLoaded();
        internal void EnsureDataDirectoryExists() => Parts.Slice1.EnsureDataDirectoryExists();
        internal void LoadAllArchivesFromFiles() => Parts.Slice1.LoadAllArchivesFromFiles();
        internal bool IsArchiveOwnedByCurrentSave(RpgNpcDialogueArchive archive) => Parts.Slice1.IsArchiveOwnedByCurrentSave(archive);
        internal bool TryValidatePersistenceContext(string operationName) => Parts.Slice2.TryValidatePersistenceContext(operationName);
        internal void TryMigrateLegacyArchives(string currentSaveKey) => Parts.Slice2.TryMigrateLegacyArchives(currentSaveKey);
        internal List<string> CollectLegacyArchiveSourceDirectories(string targetDir) => Parts.Slice2.CollectLegacyArchiveSourceDirectories(targetDir);
        internal static void TryAddLegacySourceDir(List<string> dirs, string sourceDir, string targetDir) => RpgNpcArchiveSlice2.TryAddLegacySourceDir(dirs, sourceDir, targetDir);
        internal static int CopyJsonFiles(string sourceDir, string targetDir, bool overwrite) => RpgNpcArchiveSlice2.CopyJsonFiles(sourceDir, targetDir, overwrite);
        internal bool HasClaimedDefaultBucketForAnotherSave(string currentSaveKey, List<string> legacyDirs) => Parts.Slice2.HasClaimedDefaultBucketForAnotherSave(currentSaveKey, legacyDirs);
        internal void TryClaimDefaultBucket(string currentSaveKey, List<string> legacyDirs) => Parts.Slice2.TryClaimDefaultBucket(currentSaveKey, legacyDirs);
        internal static bool IsDefaultBucketPath(string path) => RpgNpcArchiveSlice2.IsDefaultBucketPath(path);
        internal RpgNpcDialogueArchive GetOrCreateArchive(Pawn pawn, int tick) => Parts.Slice2.GetOrCreateArchive(pawn, tick);
        internal void CaptureRuntimeRpgState(Pawn pawn, RpgNpcDialogueArchive archive) => Parts.Slice2.CaptureRuntimeRpgState(pawn, archive);
        internal static long AllocateTurnSequence(RpgNpcDialogueArchive archive) => RpgNpcArchiveSlice2.AllocateTurnSequence(archive);
        internal static RpgNpcDialogueTurnArchive BuildTurnArchive(Pawn initiator, Pawn targetNpc, bool isPlayerSpeaker, string text, int tick, long turnSequence) => RpgNpcArchiveSlice2.BuildTurnArchive(initiator, targetNpc, isPlayerSpeaker, text, tick, turnSequence);
        internal static Pawn ResolveDialogueSpeakerPawn(Pawn initiator, Pawn targetNpc, bool isPlayerSpeaker) => RpgNpcArchiveSlice2.ResolveDialogueSpeakerPawn(initiator, targetNpc, isPlayerSpeaker);
        internal static Pawn ResolveCounterpartPawn(Pawn self, Pawn initiator, Pawn targetNpc) => RpgNpcArchiveSlice2.ResolveCounterpartPawn(self, initiator, targetNpc);
        internal static RpgNpcDialogueSessionArchive GetOrCreateSession(RpgNpcDialogueArchive archive, string sessionId, Pawn counterpart, int tick) => RpgNpcArchiveSlice2.GetOrCreateSession(archive, sessionId, counterpart, tick);
        internal static RpgNpcDialogueSessionArchive FindSession(RpgNpcDialogueArchive archive, string sessionId) => RpgNpcArchiveSlice2.FindSession(archive, sessionId);
        internal static string BuildSystemSessionId(string source, Pawn participant, int tick) => RpgNpcArchiveSlice2.BuildSystemSessionId(source, participant, tick);
        internal static int CountDialogueTurns(List<RpgNpcDialogueTurnArchive> turns) => RpgNpcArchiveSlice2.CountDialogueTurns(turns);
        internal static void PrepareSessionForTurnAppend(RpgNpcDialogueSessionArchive session) => RpgNpcArchiveSlice2.PrepareSessionForTurnAppend(session);
        internal static int CountDialogueTurnsFromChatHistory(List<ChatMessageData> chatHistory) => RpgNpcArchiveSlice2.CountDialogueTurnsFromChatHistory(chatHistory);
        internal void SaveArchiveToFile(RpgNpcDialogueArchive archive) => Parts.Slice3.SaveArchiveToFile(archive);
        public bool HasPromptMemory(Pawn targetNpc, Pawn currentInterlocutor = null, bool allowCacheLoad = true) => Parts.Slice3.HasPromptMemory(targetNpc, currentInterlocutor, allowCacheLoad);
        public string BuildPromptMemoryBlock(Pawn targetNpc, Pawn currentInterlocutor = null, int summaryTurnLimit = 8, int summaryCharBudget = 1200, bool allowCompressionScheduling = true, bool allowCacheLoad = true) => Parts.Slice3.BuildPromptMemoryBlock(targetNpc, currentInterlocutor, summaryTurnLimit, summaryCharBudget, allowCompressionScheduling, allowCacheLoad);
        public string BuildUnresolvedIntentSummary(Pawn targetNpc, Pawn currentInterlocutor = null) => Parts.Slice3.BuildUnresolvedIntentSummary(targetNpc, currentInterlocutor);
        internal static bool ShouldForgetLatestUnresolvedIntent(RpgNpcDialogueArchive archive, Pawn targetNpc, int currentTick) => RpgNpcArchiveSlice3.ShouldForgetLatestUnresolvedIntent(archive, targetNpc, currentTick);
        internal static int ResolveAbsoluteDayStamp(int tick, Pawn targetNpc) => RpgNpcArchiveSlice3.ResolveAbsoluteDayStamp(tick, targetNpc);
        internal static float ResolveLongitude(Pawn pawn) => RpgNpcArchiveSlice3.ResolveLongitude(pawn);
        internal static string ExtractLatestUnresolvedIntent(List<RpgNpcDialogueTurnArchive> interlocutorTurns, List<RpgNpcDialogueTurnArchive> timelineTurns) => RpgNpcArchiveSlice3.ExtractLatestUnresolvedIntent(interlocutorTurns, timelineTurns);
        internal static string BuildRecentDialogueSummaryText(List<RpgNpcDialogueTurnArchive> timelineTurns, Pawn targetNpc, Pawn currentInterlocutor, string npcName, string interlocutorName, int turnLimit, int charBudget) => RpgNpcArchiveSlice3.BuildRecentDialogueSummaryText(timelineTurns, targetNpc, currentInterlocutor, npcName, interlocutorName, turnLimit, charBudget);
        internal static void AppendRecentRawQuotes(StringBuilder sb, List<RpgNpcDialogueTurnArchive> timelineTurns, Pawn targetNpc, Pawn currentInterlocutor, string npcName, string interlocutorName) => RpgNpcArchiveSlice3.AppendRecentRawQuotes(sb, timelineTurns, targetNpc, currentInterlocutor, npcName, interlocutorName);
        internal void ApplyArchivesToRuntime() => Parts.Slice3.ApplyArchivesToRuntime();
        internal static Pawn FindPawnByLoadId(int pawnLoadId) => RpgNpcArchiveSlice4.FindPawnByLoadId(pawnLoadId);
        internal static string ResolvePawnName(Pawn pawn) => RpgNpcArchiveSlice4.ResolvePawnName(pawn);
        internal static string BuildFactionId(Faction faction) => RpgNpcArchiveSlice4.BuildFactionId(faction);
        internal static List<Pawn> CollectArchiveParticipants(Pawn initiator, Pawn targetNpc) => RpgNpcArchiveSlice4.CollectArchiveParticipants(initiator, targetNpc);
        internal static void TryAddParticipant(List<Pawn> participants, Pawn pawn, bool includePlayerFaction) => RpgNpcArchiveSlice4.TryAddParticipant(participants, pawn, includePlayerFaction);
        internal string GetCurrentSaveName() => Parts.Slice4.GetCurrentSaveName();
        internal static string ReadStringMember(object target, string memberName) => RpgNpcArchiveSlice4.ReadStringMember(target, memberName);
        internal static string TryResolveNameFromAnyStringMember(object target) => RpgNpcArchiveSlice4.TryResolveNameFromAnyStringMember(target);
        internal static bool IsLikelySaveNameMember(string memberName) => RpgNpcArchiveSlice4.IsLikelySaveNameMember(memberName);
        internal static string TryResolveLoadedGameNameFromMetaHeader() => RpgNpcArchiveSlice4.TryResolveLoadedGameNameFromMetaHeader();
        internal static string TryResolveLoadedGameNameFromKnownVerseStatics() => RpgNpcArchiveSlice4.TryResolveLoadedGameNameFromKnownVerseStatics();
        internal static string ReadStaticStringMember(Type targetType, string memberName) => RpgNpcArchiveSlice4.ReadStaticStringMember(targetType, memberName);
        internal string BuildSaveNameResolutionDiagnostic() => Parts.Slice4.BuildSaveNameResolutionDiagnostic();
        internal static Type FindTypeInLoadedAssemblies(string fullName) => RpgNpcArchiveSlice4.FindTypeInLoadedAssemblies(fullName);
        internal static string ResolvePersistentRpgSaveSlotId() => RpgNpcArchiveSlice4.ResolvePersistentRpgSaveSlotId();
        internal static string BuildArchiveFileName(RpgNpcDialogueArchive archive) => RpgNpcArchiveSlice4.BuildArchiveFileName(archive);
        internal void CleanupLegacyArchiveFiles(int pawnLoadId, string keepFileName) => Parts.Slice5.CleanupLegacyArchiveFiles(pawnLoadId, keepFileName);
        internal static void NormalizeArchiveTurns(RpgNpcDialogueArchive archive) => RpgNpcArchiveSlice5.NormalizeArchiveTurns(archive);
        internal static void EnsureTurnSequenceState(RpgNpcDialogueArchive archive) => RpgNpcArchiveSlice5.EnsureTurnSequenceState(archive);
        internal static void TrimArchiveSessions(RpgNpcDialogueArchive archive) => RpgNpcArchiveSlice5.TrimArchiveSessions(archive);
        internal static void MergeArchiveData(RpgNpcDialogueArchive existing, RpgNpcDialogueArchive incoming) => RpgNpcArchiveSlice5.MergeArchiveData(existing, incoming);
        internal static RpgNpcDialogueSessionArchive CloneSession(RpgNpcDialogueSessionArchive session) => RpgNpcArchiveSlice5.CloneSession(session);
        internal static RpgNpcDialogueTurnArchive CloneTurn(RpgNpcDialogueTurnArchive turn) => RpgNpcArchiveSlice5.CloneTurn(turn);
        internal static uint ComputeStableHash(string text) => RpgNpcArchiveSlice5.ComputeStableHash(text);
        internal static List<RpgNpcDialogueTurnArchive> BuildRelevantSummaryTurns(List<RpgNpcDialogueTurnArchive> sourceTurns, Pawn currentInterlocutor, string interlocutorName) => RpgNpcArchiveSlice5.BuildRelevantSummaryTurns(sourceTurns, currentInterlocutor, interlocutorName);
        internal static void AppendDiplomacySummaryMemoryLines(StringBuilder sb, List<RpgNpcDialogueTurnArchive> summaryTurns) => RpgNpcArchiveSlice5.AppendDiplomacySummaryMemoryLines(sb, summaryTurns);
        internal static bool IsDiplomacySummaryTurn(string text) => RpgNpcArchiveSlice6.IsDiplomacySummaryTurn(text);
        internal static string StripDiplomacySummaryPrefix(string text) => RpgNpcArchiveSlice6.StripDiplomacySummaryPrefix(text);
        internal static Pawn ResolveFactionLeaderPawn(Faction faction) => RpgNpcArchiveSlice6.ResolveFactionLeaderPawn(faction);
        internal static Pawn ResolveCounterpartForDiplomacySummary(Pawn participant, Pawn negotiator, Pawn factionLeader) => RpgNpcArchiveSlice6.ResolveCounterpartForDiplomacySummary(participant, negotiator, factionLeader);
        internal static string ResolveFallbackCounterpartName(Pawn counterpart, Faction faction) => RpgNpcArchiveSlice6.ResolveFallbackCounterpartName(counterpart, faction);
        internal static string BuildDiplomacySummaryText(Faction faction, List<DialogueMessageData> allMessages, int baselineMessageCount) => RpgNpcArchiveSlice6.BuildDiplomacySummaryText(faction, allMessages, baselineMessageCount);
        internal static string DetectDiplomacyTopic(IEnumerable<string> lines) => RpgNpcArchiveSlice6.DetectDiplomacyTopic(lines);
        internal static List<RpgNpcDialogueTurnArchive> BuildRelevantSelfTurns(List<RpgNpcDialogueTurnArchive> sourceTurns, RpgNpcDialogueArchive archive, Pawn targetNpc, Pawn currentInterlocutor, string interlocutorName) => RpgNpcArchiveSlice6.BuildRelevantSelfTurns(sourceTurns, archive, targetNpc, currentInterlocutor, interlocutorName);
        internal static List<RpgNpcDialogueTurnArchive> BuildChronologicalDialogueTurns(List<RpgNpcDialogueTurnArchive> selfTurns, List<RpgNpcDialogueTurnArchive> interlocutorTurns) => RpgNpcArchiveSlice6.BuildChronologicalDialogueTurns(selfTurns, interlocutorTurns);
        internal static List<RpgNpcDialogueTurnArchive> BuildRelevantInterlocutorTurns(List<RpgNpcDialogueTurnArchive> sourceTurns, RpgNpcDialogueArchive archive, Pawn currentInterlocutor, string interlocutorName) => RpgNpcArchiveSlice6.BuildRelevantInterlocutorTurns(sourceTurns, archive, currentInterlocutor, interlocutorName);
        internal static string ResolvePromptSpeakerName(RpgNpcDialogueTurnArchive turn, Pawn selfPawn, string selfName, Pawn currentInterlocutor, string interlocutorName) => RpgNpcArchiveSlice6.ResolvePromptSpeakerName(turn, selfPawn, selfName, currentInterlocutor, interlocutorName);
        internal static bool IsInterlocutorTurnFallback(RpgNpcDialogueTurnArchive turn, RpgNpcDialogueArchive archive) => RpgNpcArchiveSlice6.IsInterlocutorTurnFallback(turn, archive);
        internal static string ResolveInterlocutorName(RpgNpcDialogueArchive archive, Pawn currentInterlocutor, List<RpgNpcDialogueTurnArchive> sourceTurns) => RpgNpcArchiveSlice6.ResolveInterlocutorName(archive, currentInterlocutor, sourceTurns);
        internal static string ResolveTurnSpeakerName(RpgNpcDialogueTurnArchive turn, string fallbackName) => RpgNpcArchiveSlice6.ResolveTurnSpeakerName(turn, fallbackName);
        internal static string ResolveOptionalPawnName(Pawn pawn) => RpgNpcArchiveSlice6.ResolveOptionalPawnName(pawn);
        internal static bool IsPlaceholderInterlocutorName(string value) => RpgNpcArchiveSlice6.IsPlaceholderInterlocutorName(value);
        internal static bool IsHostileIntent(string text) => RpgNpcArchiveSlice6.IsHostileIntent(text);
        internal static string TrimForPrompt(string text, int maxLen) => RpgNpcArchiveSlice6.TrimForPrompt(text, maxLen);
        internal void LogDebugMissingArchive(Pawn targetNpc, Pawn currentInterlocutor) => Parts.Slice6.LogDebugMissingArchive(targetNpc, currentInterlocutor);
        internal bool TryResolveArchiveDebugContext(out string saveKey, out string archiveDir) => Parts.Slice6.TryResolveArchiveDebugContext(out saveKey, out archiveDir);
        #endregion
}
    internal sealed class RpgNpcArchiveSlice1 : RpgNpcDialogueArchiveManagerCollaborator
    {
        internal RpgNpcArchiveSlice1(RpgNpcDialogueArchiveManager owner) : base(owner)
        {
        }

public void OnNewGame()
        {
            lock (_syncRoot)
            {
                _archiveCache.Clear();
                _compressionInFlight.Clear();
                Owner.ResetPromptMemoryCacheLockless();
                _cacheLoaded = false;
                _loadedSaveKey = string.Empty;
                _resolvedSaveKey = string.Empty;
                Owner.EnsureCacheLoaded();
                Owner.SubscribeToDiplomacyMemoryEvents();
            }
        }

public void OnLoadedGame()
        {
            lock (_syncRoot)
            {
                _archiveCache.Clear();
                _compressionInFlight.Clear();
                _warmupInFlightSaveKeys.Clear();
                _pendingWarmupCompressionTargets.Clear();
                Owner.ResetPromptMemoryCacheLockless();
                _cacheLoaded = false;
                _loadedSaveKey = string.Empty;
                _resolvedSaveKey = string.Empty;
            }
        }

public void OnAfterGameLoad()
        {
            lock (_syncRoot)
            {
                Owner.EnsureCacheLoaded();
                Owner.ApplyArchivesToRuntime();
                Owner.SubscribeToDiplomacyMemoryEvents();
            }
        }

internal void SubscribeToDiplomacyMemoryEvents()
        {
            if (_diplomacyMemorySubscribed || LeaderMemoryManager.Instance == null)
                return;

            LeaderMemoryManager.Instance.DiplomacyMemoryChanged += OnDiplomacyMemoryChanged;
            _diplomacyMemorySubscribed = true;
        }

internal void OnDiplomacyMemoryChanged(DiplomacyMemoryChangedEventArgs args)
        {
            if (args == null || !args.AffectsAiPrompt)
                return;

            string affectedFactionId = args.FactionId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(affectedFactionId))
                return;

            lock (_syncRoot)
            {
                Owner.EnsureCacheLoaded();
                bool anyMutated = false;

                foreach (int pawnId in new List<int>(_archiveCache.Keys))
                {
                    if (!_archiveCache.TryGetValue(pawnId, out RpgNpcDialogueArchive archive)
                        || archive == null)
                        continue;

                    if (!string.Equals(archive.FactionId, affectedFactionId, StringComparison.Ordinal))
                        continue;

                    if (RpgNpcDialogueArchiveManager.RemoveDiplomacySummaryTurnsFromArchive(archive))
                    {
                        RpgNpcDialogueArchiveManager.NormalizeArchiveTurns(archive);
                        Owner.SaveArchiveToFile(archive);
                        anyMutated = true;
                    }
                }

                if (anyMutated)
                    Owner.InvalidatePromptMemoryCacheLockless();
            }
        }

internal static bool RemoveDiplomacySummaryTurnsFromArchive(RpgNpcDialogueArchive archive)
        {
            if (archive?.Sessions == null || archive.Sessions.Count == 0)
                return false;

            bool anyRemoved = false;
            for (int i = archive.Sessions.Count - 1; i >= 0; i--)
            {
                RpgNpcDialogueSessionArchive session = archive.Sessions[i];
                if (session?.Turns == null || session.Turns.Count == 0)
                    continue;

                int removedCount = session.Turns.RemoveAll(
                    turn => turn != null && RpgNpcDialogueArchiveManager.IsDiplomacySummaryTurn(turn.Text));
                if (removedCount <= 0)
                    continue;

                session.TurnCount = RpgNpcDialogueArchiveManager.CountDialogueTurns(session.Turns);
                anyRemoved = true;

                if (session.Turns.Count == 0
                    && !string.Equals(session.SummaryState, "Compressed", StringComparison.OrdinalIgnoreCase))
                {
                    archive.Sessions.RemoveAt(i);
                }
            }

            return anyRemoved;
        }

public void OnBeforeGameSave()
        {
            if (!Owner.TryValidatePersistenceContext(nameof(OnBeforeGameSave)))
            {
                return;
            }

            lock (_syncRoot)
            {
                Owner.EnsureCacheLoaded();
                Owner.InvalidatePromptMemoryCacheLockless();
                foreach (RpgNpcDialogueArchive archive in _archiveCache.Values)
                {
                    Owner.TryScheduleSessionCompression(archive, triggerTick: Find.TickManager?.TicksGame ?? 0);
                    Owner.SaveArchiveToFile(archive);
                }
            }
        }

public void RecordTurn(Pawn initiator, Pawn targetNpc, bool isPlayerSpeaker, string text, int tick, string sessionId = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }
            if (!Owner.TryValidatePersistenceContext(nameof(RecordTurn)))
            {
                return;
            }

            lock (_syncRoot)
            {
                Owner.EnsureCacheLoaded();
                bool archiveMutated = false;
                List<Pawn> participants = RpgNpcDialogueArchiveManager.CollectArchiveParticipants(initiator, targetNpc);
                for (int i = 0; i < participants.Count; i++)
                {
                    Pawn participant = participants[i];
                    RpgNpcDialogueArchive archive = Owner.GetOrCreateArchive(participant, tick);
                    if (archive == null)
                    {
                        continue;
                    }

                    archive.LastInteractionTick = tick;
                    archive.PawnName = RpgNpcDialogueArchiveManager.ResolvePawnName(participant);
                    archive.FactionId = RpgNpcDialogueArchiveManager.BuildFactionId(participant.Faction);
                    archive.FactionName = participant.Faction?.Name ?? string.Empty;
                    Pawn counterpart = RpgNpcDialogueArchiveManager.ResolveCounterpartPawn(participant, initiator, targetNpc);
                    archive.LastInterlocutorPawnLoadId = counterpart?.thingIDNumber ?? -1;
                    archive.LastInterlocutorName = RpgNpcDialogueArchiveManager.ResolvePawnName(counterpart);
                    long sequence = RpgNpcDialogueArchiveManager.AllocateTurnSequence(archive);
                    RpgNpcDialogueSessionArchive session = RpgNpcDialogueArchiveManager.GetOrCreateSession(archive, sessionId, counterpart, tick);
                    RpgNpcDialogueArchiveManager.PrepareSessionForTurnAppend(session);
                    session.Turns.Add(RpgNpcDialogueArchiveManager.BuildTurnArchive(initiator, targetNpc, isPlayerSpeaker, text, tick, sequence));
                    session.EndedTick = Math.Max(session.EndedTick, tick);
                    session.TurnCount = RpgNpcDialogueArchiveManager.CountDialogueTurns(session.Turns);
                    RpgNpcDialogueArchiveManager.NormalizeArchiveTurns(archive);
                    Owner.CaptureRuntimeRpgState(participant, archive);
                    Owner.SaveArchiveToFile(archive);
                    archiveMutated = true;
                }

                if (archiveMutated)
                {
                    Owner.InvalidatePromptMemoryCacheLockless();
                }
            }
        }

public void FinalizeSession(Pawn initiator, Pawn targetNpc, string sessionId, List<ChatMessageData> chatHistory)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return;
            }
            if (!Owner.TryValidatePersistenceContext(nameof(FinalizeSession)))
            {
                return;
            }

            int tick = Find.TickManager?.TicksGame ?? 0;
            int historyTurnCount = RpgNpcDialogueArchiveManager.CountDialogueTurnsFromChatHistory(chatHistory);

            lock (_syncRoot)
            {
                Owner.EnsureCacheLoaded();
                bool archiveMutated = false;
                List<Pawn> participants = RpgNpcDialogueArchiveManager.CollectArchiveParticipants(initiator, targetNpc);
                for (int i = 0; i < participants.Count; i++)
                {
                    Pawn participant = participants[i];
                    if (participant == null || !_archiveCache.TryGetValue(participant.thingIDNumber, out RpgNpcDialogueArchive archive))
                    {
                        continue;
                    }

                    RpgNpcDialogueSessionArchive session = RpgNpcDialogueArchiveManager.FindSession(archive, sessionId);
                    if (session == null)
                    {
                        continue;
                    }

                    session.EndedTick = Math.Max(session.EndedTick, tick);
                    if (session.StartedTick <= 0)
                    {
                        session.StartedTick = tick;
                    }

                    if (historyTurnCount > 0)
                    {
                        session.TurnCount = Math.Max(session.TurnCount, historyTurnCount);
                    }
                    else
                    {
                        session.TurnCount = RpgNpcDialogueArchiveManager.CountDialogueTurns(session.Turns);
                    }

                    session.IsFinalized = true;
                    RpgNpcDialogueArchiveManager.NormalizeArchiveTurns(archive);
                    Owner.TryScheduleSessionCompression(archive, tick);
                    Owner.SaveArchiveToFile(archive);
                    archiveMutated = true;
                }

                if (archiveMutated)
                {
                    Owner.InvalidatePromptMemoryCacheLockless();
                }
            }

            RpgNpcDialogueArchiveManager.PublishRpgSessionFinalized(initiator, targetNpc, chatHistory);
        }

public void RecordDiplomacySummary(
            Pawn negotiator,
            Faction faction,
            List<DialogueMessageData> allMessages,
            int baselineMessageCount)
        {
            string summary = RpgNpcDialogueArchiveManager.BuildDiplomacySummaryText(faction, allMessages, baselineMessageCount);
            if (string.IsNullOrWhiteSpace(summary))
            {
                return;
            }
            if (!Owner.TryValidatePersistenceContext(nameof(RecordDiplomacySummary)))
            {
                return;
            }

            int tick = Find.TickManager?.TicksGame ?? 0;
            Pawn factionLeader = RpgNpcDialogueArchiveManager.ResolveFactionLeaderPawn(faction);
            var participants = new List<Pawn>(2);
            RpgNpcDialogueArchiveManager.TryAddParticipant(participants, negotiator, includePlayerFaction: true);
            RpgNpcDialogueArchiveManager.TryAddParticipant(participants, factionLeader, includePlayerFaction: true);

            if (participants.Count == 0)
            {
                return;
            }

            lock (_syncRoot)
            {
                Owner.EnsureCacheLoaded();
                bool archiveMutated = false;
                for (int i = 0; i < participants.Count; i++)
                {
                    Pawn participant = participants[i];
                    RpgNpcDialogueArchive archive = Owner.GetOrCreateArchive(participant, tick);
                    if (archive == null)
                    {
                        continue;
                    }

                    Pawn counterpart = RpgNpcDialogueArchiveManager.ResolveCounterpartForDiplomacySummary(participant, negotiator, factionLeader);
                    string counterpartName = RpgNpcDialogueArchiveManager.ResolveFallbackCounterpartName(counterpart, faction);
                    archive.LastInteractionTick = Math.Max(archive.LastInteractionTick, tick);
                    archive.PawnName = RpgNpcDialogueArchiveManager.ResolvePawnName(participant);
                    archive.FactionId = RpgNpcDialogueArchiveManager.BuildFactionId(participant.Faction);
                    archive.FactionName = participant.Faction?.Name ?? string.Empty;
                    archive.LastInterlocutorPawnLoadId = counterpart?.thingIDNumber ?? -1;
                    archive.LastInterlocutorName = counterpartName;
                    long sequence = RpgNpcDialogueArchiveManager.AllocateTurnSequence(archive);
                    string sessionId = RpgNpcDialogueArchiveManager.BuildSystemSessionId("diplomacy", participant, tick);
                    RpgNpcDialogueSessionArchive session = RpgNpcDialogueArchiveManager.GetOrCreateSession(archive, sessionId, counterpart, tick);
                    session.Turns.Add(new RpgNpcDialogueTurnArchive
                    {
                        IsPlayer = false,
                        TurnSequence = sequence,
                        SpeakerPawnLoadId = participant.thingIDNumber,
                        SpeakerName = RpgNpcDialogueArchiveManager.ResolvePawnName(participant),
                        InterlocutorPawnLoadId = counterpart?.thingIDNumber ?? -1,
                        InterlocutorName = counterpartName,
                        Text = DiplomacySummaryPrefix + summary,
                        GameTick = tick
                    });
                    session.EndedTick = Math.Max(session.EndedTick, tick);
                    session.TurnCount = RpgNpcDialogueArchiveManager.CountDialogueTurns(session.Turns);
                    session.IsFinalized = true;
                    RpgNpcDialogueArchiveManager.NormalizeArchiveTurns(archive);
                    Owner.TryScheduleSessionCompression(archive, tick);
                    Owner.SaveArchiveToFile(archive);
                    archiveMutated = true;
                }

                if (archiveMutated)
                {
                    Owner.InvalidatePromptMemoryCacheLockless();
                }
            }

            RpgNpcDialogueArchiveManager.PublishDiplomacySummaryRecorded(negotiator, faction, allMessages);
        }

internal void EnsureCacheLoaded()
        {
            string currentSaveKey;
            try
            {
                currentSaveKey = CurrentSaveKey;
            }
            catch (InvalidOperationException ex)
            {
                _archiveCache.Clear();
                _cacheLoaded = false;
                _loadedSaveKey = string.Empty;
                Owner.InvalidatePromptMemoryCacheLockless();
                Log.Error($"[RimAI.Relations] RPG NPC archive cache load blocked: {ex.Message}");
                return;
            }

            if (_cacheLoaded && string.Equals(_loadedSaveKey, currentSaveKey, StringComparison.Ordinal))
            {
                return;
            }

            _archiveCache.Clear();
            Owner.InvalidatePromptMemoryCacheLockless();
            Owner.TryMigrateLegacyArchives(currentSaveKey);
            Owner.EnsureDataDirectoryExists();
            Owner.LoadAllArchivesFromFiles();
            _loadedSaveKey = currentSaveKey;
            _cacheLoaded = true;
        }

internal void EnsureDataDirectoryExists()
        {
            if (!LocalStorage.Current.DirectoryExists(CurrentArchiveDirPath))
            {
                LocalStorage.Current.CreateDirectory(CurrentArchiveDirPath);
            }
        }

internal void LoadAllArchivesFromFiles()
        {
            string sourceDir = Owner.ResolveArchiveSourceDirectory();
            if (!LocalStorage.Current.DirectoryExists(sourceDir))
            {
                Owner.InvalidatePromptMemoryCacheLockless();
                return;
            }

            string[] files = LocalStorage.Current.GetFiles(sourceDir, "*.json");
            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    string json = LocalStorage.Current.ReadAllText(files[i]);
                    RpgNpcDialogueArchive archive = RpgNpcDialogueArchiveJsonCodec.ParseJson(json);
                    if (archive != null && archive.PawnLoadId > 0 && Owner.IsArchiveOwnedByCurrentSave(archive))
                    {
                        RpgNpcDialogueArchiveManager.NormalizeArchiveTurns(archive);
                        if (_archiveCache.TryGetValue(archive.PawnLoadId, out RpgNpcDialogueArchive existing))
                        {
                            RpgNpcDialogueArchiveManager.MergeArchiveData(existing, archive);
                        }
                        else
                        {
                            _archiveCache[archive.PawnLoadId] = archive;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[RimAI.Relations] Failed to load RPG NPC archive file '{files[i]}': {ex.Message}");
                }
            }

            Owner.InvalidatePromptMemoryCacheLockless();
        }

internal bool IsArchiveOwnedByCurrentSave(RpgNpcDialogueArchive archive)
        {
            if (archive == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(archive.SaveKey))
            {
                return true;
            }

            return string.Equals(archive.SaveKey, CurrentSaveKey, StringComparison.Ordinal);
        }
    }

    internal sealed class RpgNpcArchiveSlice2 : RpgNpcDialogueArchiveManagerCollaborator
    {
        internal RpgNpcArchiveSlice2(RpgNpcDialogueArchiveManager owner) : base(owner)
        {
        }

internal bool TryValidatePersistenceContext(string operationName)
        {
            try
            {
                _ = CurrentSaveKey;
                return true;
            }
            catch (InvalidOperationException ex)
            {
                Log.Error($"[RimAI.Relations] RPG NPC archive persistence blocked in {operationName}: {ex.Message}");
                return false;
            }
        }

internal void TryMigrateLegacyArchives(string currentSaveKey)
        {
            if (string.IsNullOrWhiteSpace(currentSaveKey))
            {
                return;
            }

            string targetDir = CurrentArchiveDirPath;
            string markerPath = Path.Combine(targetDir, $".migration_complete_{currentSaveKey}.marker");
            if (LocalStorage.Current.FileExists(markerPath))
            {
                return;
            }

            LocalStorage.Current.CreateDirectory(targetDir);
            List<string> legacyDirs = Owner.CollectLegacyArchiveSourceDirectories(targetDir);
            if (legacyDirs.Count == 0)
            {
                return;
            }

            if (Owner.HasClaimedDefaultBucketForAnotherSave(currentSaveKey, legacyDirs))
            {
                return;
            }

            string backupRoot = Path.Combine(
                CurrentPromptNpcRootPath,
                LegacyMigrationBackupDirName,
                $"{DateTime.UtcNow:yyyyMMddHHmmss}_{currentSaveKey}");

            int migratedCount = 0;
            for (int i = 0; i < legacyDirs.Count; i++)
            {
                string sourceDir = legacyDirs[i];
                string backupDir = Path.Combine(backupRoot, $"source_{i}");
                LocalStorage.Current.CreateDirectory(backupDir);
                RpgNpcDialogueArchiveManager.CopyJsonFiles(sourceDir, backupDir, overwrite: true);
                migratedCount += RpgNpcDialogueArchiveManager.CopyJsonFiles(sourceDir, targetDir, overwrite: false);
            }

            if (migratedCount > 0)
            {
                LocalStorage.Current.WriteAllText(markerPath, DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                Owner.TryClaimDefaultBucket(currentSaveKey, legacyDirs);
                ModuleLog.Message($"[RimAI.Relations] Migrated {migratedCount} legacy NPC archive file(s) to {currentSaveKey}.");
            }
        }

internal List<string> CollectLegacyArchiveSourceDirectories(string targetDir)
        {
            var dirs = new List<string>();
            string rootLevelLegacyDir = Path.Combine(CurrentPromptNpcRootPath, NpcArchiveSubDir);
            RpgNpcDialogueArchiveManager.TryAddLegacySourceDir(dirs, rootLevelLegacyDir, targetDir);

            string[] saveDirs = LocalStorage.Current.DirectoryExists(CurrentPromptNpcRootPath)
                ? LocalStorage.Current.GetDirectories(CurrentPromptNpcRootPath, "Save_*")
                : Array.Empty<string>();
            for (int i = 0; i < saveDirs.Length; i++)
            {
                string dirName = Path.GetFileName(saveDirs[i]);
                if (!dirName.EndsWith($"_{DefaultSaveName}", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string legacyArchiveDir = Path.Combine(saveDirs[i], NpcArchiveSubDir);
                RpgNpcDialogueArchiveManager.TryAddLegacySourceDir(dirs, legacyArchiveDir, targetDir);
            }

            return dirs;
        }

internal static void TryAddLegacySourceDir(List<string> dirs, string sourceDir, string targetDir)
        {
            if (string.IsNullOrWhiteSpace(sourceDir) || !RpgNpcDialogueArchiveManager.DirectoryHasJsonFiles(sourceDir))
            {
                return;
            }

            if (string.Equals(sourceDir, targetDir, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (dirs.Any(existing => string.Equals(existing, sourceDir, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            dirs.Add(sourceDir);
        }

internal static int CopyJsonFiles(string sourceDir, string targetDir, bool overwrite)
        {
            if (!RpgNpcDialogueArchiveManager.DirectoryHasJsonFiles(sourceDir))
            {
                return 0;
            }

            LocalStorage.Current.CreateDirectory(targetDir);
            int copied = 0;
            string[] files = LocalStorage.Current.GetFiles(sourceDir, "*.json");
            for (int i = 0; i < files.Length; i++)
            {
                string fileName = Path.GetFileName(files[i]);
                string targetPath = Path.Combine(targetDir, fileName);
                if (!overwrite && LocalStorage.Current.FileExists(targetPath))
                {
                    continue;
                }

                LocalStorage.Current.CopyFile(files[i], targetPath, overwrite);
                copied++;
            }

            return copied;
        }

internal bool HasClaimedDefaultBucketForAnotherSave(string currentSaveKey, List<string> legacyDirs)
        {
            if (legacyDirs == null || legacyDirs.Count == 0 || !legacyDirs.Any(IsDefaultBucketPath))
            {
                return false;
            }

            string claimPath = Path.Combine(CurrentPromptNpcRootPath, LegacyDefaultBucketClaimMarker);
            if (!LocalStorage.Current.FileExists(claimPath))
            {
                return false;
            }

            string claimedSaveKey = LocalStorage.Current.ReadAllText(claimPath).Trim();
            if (string.IsNullOrWhiteSpace(claimedSaveKey))
            {
                return false;
            }

            return !string.Equals(claimedSaveKey, currentSaveKey, StringComparison.Ordinal);
        }

internal void TryClaimDefaultBucket(string currentSaveKey, List<string> legacyDirs)
        {
            if (legacyDirs == null || legacyDirs.Count == 0 || !legacyDirs.Any(IsDefaultBucketPath))
            {
                return;
            }

            string claimPath = Path.Combine(CurrentPromptNpcRootPath, LegacyDefaultBucketClaimMarker);
            if (!LocalStorage.Current.FileExists(claimPath))
            {
                LocalStorage.Current.WriteAllText(claimPath, currentSaveKey);
            }
        }

internal static bool IsDefaultBucketPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string normalized = path.Replace('\\', '/');
            return normalized.Contains("/Save_") &&
                normalized.EndsWith($"/{NpcArchiveSubDir}", StringComparison.OrdinalIgnoreCase) &&
                normalized.Contains($"_{DefaultSaveName}/");
        }

internal RpgNpcDialogueArchive GetOrCreateArchive(Pawn pawn, int tick)
        {
            int pawnId = pawn?.thingIDNumber ?? -1;
            if (pawnId <= 0)
            {
                return null;
            }

            if (_archiveCache.TryGetValue(pawnId, out RpgNpcDialogueArchive existing))
            {
                return existing;
            }

            var archive = new RpgNpcDialogueArchive
            {
                SaveKey = CurrentSaveKey,
                PawnLoadId = pawnId,
                PawnName = RpgNpcDialogueArchiveManager.ResolvePawnName(pawn),
                FactionId = RpgNpcDialogueArchiveManager.BuildFactionId(pawn?.Faction),
                FactionName = pawn?.Faction?.Name ?? string.Empty,
                CreatedTimestamp = DateTime.UtcNow.Ticks,
                LastInteractionTick = tick
            };
            _archiveCache[pawnId] = archive;
            return archive;
        }

internal void CaptureRuntimeRpgState(Pawn pawn, RpgNpcDialogueArchive archive)
        {
            GameComponent_RPGManager rpgManager = GameComponent_RPGManager.Instance;
            if (rpgManager == null || pawn == null || archive == null)
            {
                return;
            }

            archive.PersonaPrompt = rpgManager.GetPawnPersonaPrompt(pawn) ?? string.Empty;
            archive.CooldownUntilTick = rpgManager.GetDialogueCooldownUntilTick(pawn);
        }

internal static long AllocateTurnSequence(RpgNpcDialogueArchive archive)
        {
            if (archive == null)
            {
                return 0L;
            }

            long next = archive.NextTurnSequence > 0 ? archive.NextTurnSequence : 1L;
            archive.NextTurnSequence = next + 1L;
            return next;
        }

internal static RpgNpcDialogueTurnArchive BuildTurnArchive(
            Pawn initiator,
            Pawn targetNpc,
            bool isPlayerSpeaker,
            string text,
            int tick,
            long turnSequence)
        {
            Pawn speaker = RpgNpcDialogueArchiveManager.ResolveDialogueSpeakerPawn(initiator, targetNpc, isPlayerSpeaker);
            Pawn interlocutor = RpgNpcDialogueArchiveManager.ResolveCounterpartPawn(speaker, initiator, targetNpc);
            return new RpgNpcDialogueTurnArchive
            {
                IsPlayer = isPlayerSpeaker,
                TurnSequence = turnSequence,
                SpeakerPawnLoadId = speaker?.thingIDNumber ?? -1,
                SpeakerName = RpgNpcDialogueArchiveManager.ResolvePawnName(speaker),
                InterlocutorPawnLoadId = interlocutor?.thingIDNumber ?? -1,
                InterlocutorName = RpgNpcDialogueArchiveManager.ResolvePawnName(interlocutor),
                Text = text.Trim(),
                GameTick = tick
            };
        }

internal static Pawn ResolveDialogueSpeakerPawn(Pawn initiator, Pawn targetNpc, bool isPlayerSpeaker)
        {
            if (isPlayerSpeaker)
            {
                return initiator ?? targetNpc;
            }

            return targetNpc ?? initiator;
        }

internal static Pawn ResolveCounterpartPawn(Pawn self, Pawn initiator, Pawn targetNpc)
        {
            if (self != null && initiator != null && self.thingIDNumber == initiator.thingIDNumber)
            {
                return targetNpc;
            }

            if (self != null && targetNpc != null && self.thingIDNumber == targetNpc.thingIDNumber)
            {
                return initiator;
            }

            Pawn playerPawn = RpgNpcDialogueArchiveManager.GetPlayerPawn(initiator) ?? RpgNpcDialogueArchiveManager.GetPlayerPawn(targetNpc);
            if (playerPawn != null && (self == null || playerPawn.thingIDNumber != self.thingIDNumber))
            {
                return playerPawn;
            }

            if (initiator != null && (self == null || initiator.thingIDNumber != self.thingIDNumber))
            {
                return initiator;
            }

            if (targetNpc != null && (self == null || targetNpc.thingIDNumber != self.thingIDNumber))
            {
                return targetNpc;
            }

            return null;
        }

internal static RpgNpcDialogueSessionArchive GetOrCreateSession(
            RpgNpcDialogueArchive archive,
            string sessionId,
            Pawn counterpart,
            int tick)
        {
            if (archive == null)
            {
                return null;
            }

            string normalizedSessionId = string.IsNullOrWhiteSpace(sessionId)
                ? $"session_{tick}_{Guid.NewGuid():N}"
                : sessionId.Trim();

            RpgNpcDialogueSessionArchive existing = RpgNpcDialogueArchiveManager.FindSession(archive, normalizedSessionId);
            if (existing != null)
            {
                if (existing.StartedTick <= 0)
                {
                    existing.StartedTick = tick;
                }

                if (counterpart != null)
                {
                    existing.InterlocutorPawnLoadId = counterpart.thingIDNumber;
                    existing.InterlocutorName = RpgNpcDialogueArchiveManager.ResolvePawnName(counterpart);
                }

                if (string.IsNullOrWhiteSpace(existing.SummaryState))
                {
                    existing.SummaryState = RpgNpcDialogueSessionSummaryState.Pending;
                }

                existing.IsFinalized = false;
                return existing;
            }

            var session = new RpgNpcDialogueSessionArchive
            {
                SessionId = normalizedSessionId,
                StartedTick = tick,
                EndedTick = tick,
                TurnCount = 0,
                IsFinalized = false,
                InterlocutorPawnLoadId = counterpart?.thingIDNumber ?? -1,
                InterlocutorName = RpgNpcDialogueArchiveManager.ResolvePawnName(counterpart),
                SummaryText = string.Empty,
                SummaryState = RpgNpcDialogueSessionSummaryState.Pending,
                LastSummaryAttemptTick = 0,
                Turns = new List<RpgNpcDialogueTurnArchive>()
            };

            if (archive.Sessions == null)
            {
                archive.Sessions = new List<RpgNpcDialogueSessionArchive>();
            }

            archive.Sessions.Add(session);
            return session;
        }

internal static RpgNpcDialogueSessionArchive FindSession(RpgNpcDialogueArchive archive, string sessionId)
        {
            if (archive?.Sessions == null || string.IsNullOrWhiteSpace(sessionId))
            {
                return null;
            }

            return archive.Sessions.FirstOrDefault(session =>
                session != null &&
                string.Equals(session.SessionId, sessionId, StringComparison.Ordinal));
        }

internal static string BuildSystemSessionId(string source, Pawn participant, int tick)
        {
            int participantId = participant?.thingIDNumber ?? -1;
            return $"sys_{source}_{participantId}_{tick}_{Guid.NewGuid():N}";
        }

internal static int CountDialogueTurns(List<RpgNpcDialogueTurnArchive> turns)
        {
            return turns?
                .Count(turn => turn != null && !string.IsNullOrWhiteSpace(turn.Text))
                ?? 0;
        }

internal static void PrepareSessionForTurnAppend(RpgNpcDialogueSessionArchive session)
        {
            if (session == null)
            {
                return;
            }

            session.IsFinalized = false;

            bool hadTerminalSummaryState =
                string.Equals(session.SummaryState, RpgNpcDialogueSessionSummaryState.Compressed, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(session.SummaryState, RpgNpcDialogueSessionSummaryState.SummaryFailed, StringComparison.OrdinalIgnoreCase);
            if (hadTerminalSummaryState)
            {
                session.SummaryState = RpgNpcDialogueSessionSummaryState.Pending;
                session.LastSummaryAttemptTick = 0;
            }

            if (!string.IsNullOrWhiteSpace(session.SummaryText) &&
                !string.Equals(session.SummaryState, RpgNpcDialogueSessionSummaryState.Compressed, StringComparison.OrdinalIgnoreCase))
            {
                session.SummaryText = string.Empty;
            }
        }

internal static int CountDialogueTurnsFromChatHistory(List<ChatMessageData> chatHistory)
        {
            if (chatHistory == null || chatHistory.Count == 0)
            {
                return 0;
            }

            return chatHistory.Count(message =>
                message != null &&
                !string.IsNullOrWhiteSpace(message.content) &&
                !string.Equals(message.role, "system", StringComparison.OrdinalIgnoreCase));
        }
    }

    internal sealed class RpgNpcArchiveSlice3 : RpgNpcDialogueArchiveManagerCollaborator
    {
        internal RpgNpcArchiveSlice3(RpgNpcDialogueArchiveManager owner) : base(owner)
        {
        }

internal void SaveArchiveToFile(RpgNpcDialogueArchive archive)
        {
            if (archive == null || archive.PawnLoadId <= 0)
            {
                return;
            }

            try
            {
                Owner.EnsureDataDirectoryExists();
                archive.SaveKey = CurrentSaveKey;
                archive.LastSavedTimestamp = DateTime.UtcNow.Ticks;
                string fileName = RpgNpcDialogueArchiveManager.BuildArchiveFileName(archive);
                string filePath = Path.Combine(CurrentArchiveDirPath, fileName);
                Owner.CleanupLegacyArchiveFiles(archive.PawnLoadId, fileName);
                string json = RpgNpcDialogueArchiveJsonCodec.ConvertToJson(archive);
                AtomicFileWriter.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimAI.Relations] Failed to save RPG NPC archive {archive.PawnLoadId}: {ex.Message}");
            }
        }

public bool HasPromptMemory(Pawn targetNpc, Pawn currentInterlocutor = null, bool allowCacheLoad = true)
        {
            if (targetNpc == null || targetNpc.Destroyed || targetNpc.Dead)
            {
                return false;
            }

            lock (_syncRoot)
            {
                if (allowCacheLoad)
                {
                    Owner.EnsureCacheLoaded();
                }
                else if (!_cacheLoaded)
                {
                    return false;
                }

                Owner.FlushPendingWarmupCompressionLockless(Find.TickManager?.TicksGame ?? 0);
                if (!_archiveCache.TryGetValue(targetNpc.thingIDNumber, out RpgNpcDialogueArchive archive) ||
                    archive == null)
                {
                    return false;
                }

                RpgNpcDialogueArchiveManager.NormalizeArchiveTurns(archive);
                List<RpgNpcDialogueTurnArchive> retainedTurns = RpgNpcDialogueArchiveManager.GetSessionTurns(RpgNpcDialogueArchiveManager.SelectLatestRetainedFullSession(archive));
                if (retainedTurns.Count > 0)
                {
                    return true;
                }

                List<RpgNpcDialogueSessionArchive> compressedSessions = RpgNpcDialogueArchiveManager.GetCompressedSessionsForInjection(archive);
                return compressedSessions.Count > 0;
            }
        }

public string BuildPromptMemoryBlock(
            Pawn targetNpc,
            Pawn currentInterlocutor = null,
            int summaryTurnLimit = 8,
            int summaryCharBudget = 1200,
            bool allowCompressionScheduling = true,
            bool allowCacheLoad = true)
        {
            if (targetNpc == null || targetNpc.Destroyed || targetNpc.Dead)
            {
                return string.Empty;
            }

            lock (_syncRoot)
            {
                if (allowCacheLoad)
                {
                    Owner.EnsureCacheLoaded();
                }
                else if (!_cacheLoaded)
                {
                    return string.Empty;
                }

                int clampedSummaryTurnLimit = Math.Max(3, Math.Min(16, summaryTurnLimit));
                int clampedSummaryBudget = Math.Max(500, Math.Min(4000, summaryCharBudget));
                int tick = Find.TickManager?.TicksGame ?? 0;
                Owner.FlushPendingWarmupCompressionLockless(tick);
                int dayStamp = RpgNpcDialogueArchiveManager.ResolveAbsoluteDayStamp(tick, targetNpc);
                int interlocutorId = currentInterlocutor?.thingIDNumber ?? -1;
                string cacheKey = RpgNpcDialogueArchiveManager.BuildPromptMemoryCacheKey(
                    targetNpc.thingIDNumber,
                    interlocutorId,
                    clampedSummaryTurnLimit,
                    clampedSummaryBudget,
                    dayStamp);
                if (Owner.TryGetPromptMemoryCacheLockless(cacheKey, out string cachedMemoryBlock))
                {
                    return cachedMemoryBlock;
                }

                if (!_archiveCache.TryGetValue(targetNpc.thingIDNumber, out RpgNpcDialogueArchive archive) ||
                    archive == null)
                {
                    Owner.LogDebugMissingArchive(targetNpc, currentInterlocutor);
                    Owner.SetPromptMemoryCacheLockless(cacheKey, string.Empty);
                    return string.Empty;
                }

                RpgNpcDialogueArchiveManager.NormalizeArchiveTurns(archive);
                if (allowCompressionScheduling)
                {
                    Owner.TryScheduleSessionCompression(archive, tick);
                }

                RpgNpcDialogueSessionArchive retainedSession = RpgNpcDialogueArchiveManager.SelectLatestRetainedFullSession(archive);
                List<RpgNpcDialogueTurnArchive> retainedTurns = RpgNpcDialogueArchiveManager.GetSessionTurns(retainedSession);
                List<RpgNpcDialogueSessionArchive> compressedSessions = RpgNpcDialogueArchiveManager.GetCompressedSessionsForInjection(archive);
                if ((retainedTurns == null || retainedTurns.Count == 0) &&
                    (compressedSessions == null || compressedSessions.Count == 0))
                {
                    Owner.LogDebugMissingArchive(targetNpc, currentInterlocutor);
                    Owner.SetPromptMemoryCacheLockless(cacheKey, string.Empty);
                    return string.Empty;
                }

                string npcName = RpgNpcDialogueArchiveManager.ResolvePawnName(targetNpc);
                string interlocutorName = RpgNpcDialogueArchiveManager.ResolveInterlocutorName(archive, currentInterlocutor, retainedTurns);
                var sb = new StringBuilder();
                sb.AppendLine("=== NPC PERSONAL MEMORY (RPG DIALOGUE) ===");
                sb.AppendLine($"You are {npcName}. Keep continuity with your own previous conversations.");
                sb.AppendLine($"Current interlocutor in this scene: {interlocutorName}.");
                sb.AppendLine("Continuity rules:");
                sb.AppendLine("- Resolve latest unresolved player intent first.");
                sb.AppendLine("- Keep relationship tone continuous; do not reset to neutral.");
                sb.AppendLine("- Never reuse previous wording verbatim; paraphrase.");

                RpgNpcDialogueArchiveManager.AppendCompressedSessionSummaries(
                    sb,
                    compressedSessions,
                    MaxInjectedCompressedSessionSummaries,
                    MaxInjectedCompressedSessionSummaryChars);

                if (retainedTurns != null && retainedTurns.Count > 0)
                {
                    List<RpgNpcDialogueTurnArchive> summaryTurns = RpgNpcDialogueArchiveManager.BuildRelevantSummaryTurns(retainedTurns, currentInterlocutor, interlocutorName);
                    RpgNpcDialogueArchiveManager.AppendDiplomacySummaryMemoryLines(sb, summaryTurns);

                    List<RpgNpcDialogueTurnArchive> interlocutorTurns = RpgNpcDialogueArchiveManager.BuildRelevantInterlocutorTurns(
                        retainedTurns,
                        archive,
                        currentInterlocutor,
                        interlocutorName);
                    List<RpgNpcDialogueTurnArchive> selfTurns = RpgNpcDialogueArchiveManager.BuildRelevantSelfTurns(
                        retainedTurns,
                        archive,
                        targetNpc,
                        currentInterlocutor,
                        interlocutorName);
                    List<RpgNpcDialogueTurnArchive> timelineTurns = RpgNpcDialogueArchiveManager.BuildChronologicalDialogueTurns(selfTurns, interlocutorTurns);
                    bool shouldInjectUnresolvedIntent = !RpgNpcDialogueArchiveManager.ShouldForgetLatestUnresolvedIntent(archive, targetNpc, tick);
                    if (shouldInjectUnresolvedIntent)
                    {
                        string unresolvedIntent = RpgNpcDialogueArchiveManager.ExtractLatestUnresolvedIntent(interlocutorTurns, timelineTurns);
                        bool hostileIntent = RpgNpcDialogueArchiveManager.IsHostileIntent(unresolvedIntent);
                        if (!string.IsNullOrWhiteSpace(unresolvedIntent))
                        {
                            sb.AppendLine($"Latest unresolved player intent: {RpgNpcDialogueArchiveManager.TrimForPrompt(unresolvedIntent, 150)}");
                            sb.AppendLine($"Latest intent tone (hostile={hostileIntent.ToString().ToLowerInvariant()}).");
                        }
                    }

                    string recentSummary = RpgNpcDialogueArchiveManager.BuildRecentDialogueSummaryText(
                        timelineTurns,
                        targetNpc,
                        currentInterlocutor,
                        npcName,
                        interlocutorName,
                        clampedSummaryTurnLimit,
                        clampedSummaryBudget);
                    if (!string.IsNullOrWhiteSpace(recentSummary))
                    {
                        sb.AppendLine("Recent dialogue summary (summary-first):");
                        sb.AppendLine(recentSummary);
                    }

                    RpgNpcDialogueArchiveManager.AppendRecentRawQuotes(sb, timelineTurns, targetNpc, currentInterlocutor, npcName, interlocutorName);
                }

                string memoryBlock = sb.ToString().Trim();
                Owner.SetPromptMemoryCacheLockless(cacheKey, memoryBlock);
                return memoryBlock;
            }
        }

public string BuildUnresolvedIntentSummary(Pawn targetNpc, Pawn currentInterlocutor = null)
        {
            if (targetNpc == null || targetNpc.Destroyed || targetNpc.Dead)
            {
                return string.Empty;
            }

            lock (_syncRoot)
            {
                Owner.EnsureCacheLoaded();
                Owner.FlushPendingWarmupCompressionLockless(Find.TickManager?.TicksGame ?? 0);
                if (!_archiveCache.TryGetValue(targetNpc.thingIDNumber, out RpgNpcDialogueArchive archive) ||
                    archive == null)
                {
                    return string.Empty;
                }

                RpgNpcDialogueArchiveManager.NormalizeArchiveTurns(archive);
                int tick = Find.TickManager?.TicksGame ?? 0;
                Owner.TryScheduleSessionCompression(archive, tick);
                if (RpgNpcDialogueArchiveManager.ShouldForgetLatestUnresolvedIntent(archive, targetNpc, tick))
                {
                    return string.Empty;
                }

                RpgNpcDialogueSessionArchive retainedSession = RpgNpcDialogueArchiveManager.SelectLatestRetainedFullSession(archive);
                List<RpgNpcDialogueTurnArchive> retainedTurns = RpgNpcDialogueArchiveManager.GetSessionTurns(retainedSession);
                if (retainedTurns == null || retainedTurns.Count == 0)
                {
                    return string.Empty;
                }

                string interlocutorName = RpgNpcDialogueArchiveManager.ResolveInterlocutorName(archive, currentInterlocutor, retainedTurns);
                List<RpgNpcDialogueTurnArchive> interlocutorTurns = RpgNpcDialogueArchiveManager.BuildRelevantInterlocutorTurns(
                    retainedTurns,
                    archive,
                    currentInterlocutor,
                    interlocutorName);
                List<RpgNpcDialogueTurnArchive> selfTurns = RpgNpcDialogueArchiveManager.BuildRelevantSelfTurns(
                    retainedTurns,
                    archive,
                    targetNpc,
                    currentInterlocutor,
                    interlocutorName);
                List<RpgNpcDialogueTurnArchive> timelineTurns = RpgNpcDialogueArchiveManager.BuildChronologicalDialogueTurns(selfTurns, interlocutorTurns);
                return RpgNpcDialogueArchiveManager.TrimForPrompt(RpgNpcDialogueArchiveManager.ExtractLatestUnresolvedIntent(interlocutorTurns, timelineTurns), 160);
            }
        }

internal static bool ShouldForgetLatestUnresolvedIntent(
            RpgNpcDialogueArchive archive,
            Pawn targetNpc,
            int currentTick)
        {
            if (archive == null || archive.LastInteractionTick <= 0 || currentTick <= archive.LastInteractionTick)
            {
                return false;
            }

            int previousDayStamp = RpgNpcDialogueArchiveManager.ResolveAbsoluteDayStamp(archive.LastInteractionTick, targetNpc);
            int currentDayStamp = RpgNpcDialogueArchiveManager.ResolveAbsoluteDayStamp(currentTick, targetNpc);
            return currentDayStamp > previousDayStamp;
        }

internal static int ResolveAbsoluteDayStamp(int tick, Pawn targetNpc)
        {
            float longitude = RpgNpcDialogueArchiveManager.ResolveLongitude(targetNpc);
            int year = GenDate.Year(tick, longitude);
            int dayOfYear = GenDate.DayOfYear(tick, longitude);
            return checked((year * 60) + dayOfYear);
        }

internal static float ResolveLongitude(Pawn pawn)
        {
            Map map = pawn?.MapHeld ?? Find.CurrentMap;
            if (map != null && WorldTileGuard.IsValidTile(map.Tile))
            {
                return Find.WorldGrid.LongLatOf(map.Tile).x;
            }

            return 0f;
        }

internal static string ExtractLatestUnresolvedIntent(
            List<RpgNpcDialogueTurnArchive> interlocutorTurns,
            List<RpgNpcDialogueTurnArchive> timelineTurns)
        {
            RpgNpcDialogueTurnArchive lastInterlocutorTurn = interlocutorTurns?
                .OrderByDescending(turn => turn.GameTick)
                .ThenByDescending(turn => turn.TurnSequence)
                .FirstOrDefault();
            if (lastInterlocutorTurn == null || string.IsNullOrWhiteSpace(lastInterlocutorTurn.Text))
            {
                return string.Empty;
            }

            if (timelineTurns == null || timelineTurns.Count == 0)
            {
                return lastInterlocutorTurn.Text.Trim();
            }

            RpgNpcDialogueTurnArchive lastTimeline = timelineTurns[timelineTurns.Count - 1];
            bool interlocutorIsLatest =
                lastTimeline != null &&
                (lastTimeline.IsPlayer ||
                 lastTimeline.SpeakerPawnLoadId == lastInterlocutorTurn.SpeakerPawnLoadId ||
                 string.Equals(lastTimeline.SpeakerName, lastInterlocutorTurn.SpeakerName, StringComparison.OrdinalIgnoreCase));
            if (interlocutorIsLatest)
            {
                return lastInterlocutorTurn.Text.Trim();
            }

            return lastInterlocutorTurn.Text.Trim();
        }

internal static string BuildRecentDialogueSummaryText(
            List<RpgNpcDialogueTurnArchive> timelineTurns,
            Pawn targetNpc,
            Pawn currentInterlocutor,
            string npcName,
            string interlocutorName,
            int turnLimit,
            int charBudget)
        {
            if (timelineTurns == null || timelineTurns.Count == 0)
            {
                return string.Empty;
            }

            int start = Math.Max(0, timelineTurns.Count - turnLimit);
            var summaryLines = new List<string>();
            int usedChars = 0;
            for (int i = start; i < timelineTurns.Count; i++)
            {
                RpgNpcDialogueTurnArchive turn = timelineTurns[i];
                string speaker = RpgNpcDialogueArchiveManager.ResolvePromptSpeakerName(turn, targetNpc, npcName, currentInterlocutor, interlocutorName);
                string gist = RpgNpcDialogueArchiveManager.TrimForPrompt(turn?.Text, 90);
                if (string.IsNullOrWhiteSpace(gist))
                {
                    continue;
                }

                string line = $"- {speaker}: {gist}";
                if (usedChars + line.Length > charBudget)
                {
                    break;
                }

                summaryLines.Add(line);
                usedChars += line.Length;
            }

            return string.Join("\n", summaryLines);
        }

internal static void AppendRecentRawQuotes(
            StringBuilder sb,
            List<RpgNpcDialogueTurnArchive> timelineTurns,
            Pawn targetNpc,
            Pawn currentInterlocutor,
            string npcName,
            string interlocutorName)
        {
            if (sb == null || timelineTurns == null || timelineTurns.Count == 0)
            {
                return;
            }

            int keep = Math.Min(3, timelineTurns.Count);
            int start = timelineTurns.Count - keep;
            sb.AppendLine("Recent raw snippets (limited):");
            for (int i = start; i < timelineTurns.Count; i++)
            {
                RpgNpcDialogueTurnArchive turn = timelineTurns[i];
                string speaker = RpgNpcDialogueArchiveManager.ResolvePromptSpeakerName(turn, targetNpc, npcName, currentInterlocutor, interlocutorName);
                sb.AppendLine($"- {speaker}: {RpgNpcDialogueArchiveManager.TrimForPrompt(turn?.Text, 80)}");
            }
        }

internal void ApplyArchivesToRuntime()
        {
            GameComponent_RPGManager rpgManager = GameComponent_RPGManager.Instance;
            if (rpgManager == null || _archiveCache.Count == 0)
            {
                return;
            }

            foreach (RpgNpcDialogueArchive archive in _archiveCache.Values)
            {
                if (archive == null || archive.PawnLoadId <= 0)
                {
                    continue;
                }

                Pawn pawn = RpgNpcDialogueArchiveManager.FindPawnByLoadId(archive.PawnLoadId);
                if (pawn == null || pawn.Destroyed || pawn.Dead)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(archive.PersonaPrompt))
                {
                    rpgManager.SetPawnPersonaPrompt(pawn, archive.PersonaPrompt);
                }

                rpgManager.SetDialogueCooldownUntilTick(pawn, archive.CooldownUntilTick);
            }
        }
    }

    internal sealed class RpgNpcArchiveSlice4 : RpgNpcDialogueArchiveManagerCollaborator
    {
        internal RpgNpcArchiveSlice4(RpgNpcDialogueArchiveManager owner) : base(owner)
        {
        }

internal static Pawn FindPawnByLoadId(int pawnLoadId)
        {
            if (pawnLoadId <= 0)
            {
                return null;
            }

            IEnumerable<Pawn> worldPawns = Find.WorldPawns?.AllPawnsAliveOrDead;
            if (worldPawns != null)
            {
                Pawn found = worldPawns.FirstOrDefault(pawn => pawn != null && pawn.thingIDNumber == pawnLoadId);
                if (found != null)
                {
                    return found;
                }
            }

            if (Find.Maps == null)
            {
                return null;
            }

            foreach (Map map in Find.Maps)
            {
                Pawn found = map?.mapPawns?.AllPawnsSpawned?.FirstOrDefault(pawn => pawn != null && pawn.thingIDNumber == pawnLoadId);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

internal static string ResolvePawnName(Pawn pawn)
        {
            if (pawn == null)
            {
                return "UnknownPawn";
            }

            return pawn.LabelShort ?? pawn.Name?.ToStringShort ?? pawn.Name?.ToStringFull ?? "UnknownPawn";
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

internal static List<Pawn> CollectArchiveParticipants(Pawn initiator, Pawn targetNpc)
        {
            var participants = new List<Pawn>(2);
            RpgNpcDialogueArchiveManager.TryAddParticipant(participants, targetNpc, includePlayerFaction: true);

            bool includeInitiator =
                initiator != null &&
                targetNpc != null &&
                initiator.thingIDNumber != targetNpc.thingIDNumber;
            if (includeInitiator)
            {
                RpgNpcDialogueArchiveManager.TryAddParticipant(participants, initiator, includePlayerFaction: true);
            }
            return participants;
        }

internal static void TryAddParticipant(List<Pawn> participants, Pawn pawn, bool includePlayerFaction)
        {
            if (pawn == null || pawn.Destroyed || pawn.Dead)
            {
                return;
            }

            if (!includePlayerFaction && pawn.Faction != null && pawn.Faction.IsPlayer)
            {
                return;
            }

            if (participants.Any(existing => existing != null && existing.thingIDNumber == pawn.thingIDNumber))
            {
                return;
            }

            participants.Add(pawn);
        }

internal string GetCurrentSaveName()
        {
            string trackedSaveName = SaveContextTracker.GetCurrentSaveName();
            if (!string.IsNullOrWhiteSpace(trackedSaveName))
            {
                _lastResolvedSaveName = trackedSaveName;
                return trackedSaveName;
            }

            object gameInfo = Current.Game?.Info;
            if (gameInfo == null)
            {
                string loadedGameName = RpgNpcDialogueArchiveManager.TryResolveLoadedGameNameFromMetaHeader();
                return string.IsNullOrWhiteSpace(loadedGameName)
                    ? DefaultSaveName
                    : loadedGameName.SanitizeFileName();
            }

            string name = RpgNpcDialogueArchiveManager.ReadStringMember(gameInfo, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                name = RpgNpcDialogueArchiveManager.ReadStringMember(gameInfo, "Name");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                name = RpgNpcDialogueArchiveManager.ReadStringMember(gameInfo, "fileName");
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                name = RpgNpcDialogueArchiveManager.ReadStringMember(gameInfo, "FileName");
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                name = RpgNpcDialogueArchiveManager.TryResolveNameFromAnyStringMember(gameInfo);
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                name = RpgNpcDialogueArchiveManager.TryResolveLoadedGameNameFromMetaHeader();
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                name = RpgNpcDialogueArchiveManager.TryResolveLoadedGameNameFromKnownVerseStatics();
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                name = _lastResolvedSaveName;
            }

            return string.IsNullOrWhiteSpace(name) ? DefaultSaveName : name.SanitizeFileName();
        }

internal static string ReadStringMember(object target, string memberName)
        {
            if (target == null || string.IsNullOrWhiteSpace(memberName))
            {
                return string.Empty;
            }

            try
            {
                PropertyInfo prop = target.GetType().GetProperty(memberName, InstanceStringMemberBinding);
                if (prop?.PropertyType == typeof(string))
                {
                    string value = prop.GetValue(target) as string;
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }

                FieldInfo field = target.GetType().GetField(memberName, InstanceStringMemberBinding);
                if (field?.FieldType == typeof(string))
                {
                    string value = field.GetValue(target) as string;
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }
            // RimAI.catch-boundary: ALLOWED_TOP_LEVEL_BOUNDARY - archive field probe fell through to the next strategy
            catch (System.Exception ex)
            {
                ModuleLog.Message("[RimAI.Relations] archive field probe fell through to the next strategy: " + ex.Message);
            }

            return string.Empty;
        }

internal static string TryResolveNameFromAnyStringMember(object target)
        {
            if (target == null)
            {
                return string.Empty;
            }

            try
            {
                Type type = target.GetType();
                foreach (PropertyInfo prop in type.GetProperties(InstanceStringMemberBinding))
                {
                    if (prop.PropertyType != typeof(string) || prop.GetIndexParameters().Length > 0)
                    {
                        continue;
                    }

                    string value = prop.GetValue(target) as string;
                    if (!string.IsNullOrWhiteSpace(value) && RpgNpcDialogueArchiveManager.IsLikelySaveNameMember(prop.Name))
                    {
                        return value;
                    }
                }

                foreach (FieldInfo field in type.GetFields(InstanceStringMemberBinding))
                {
                    if (field.FieldType != typeof(string))
                    {
                        continue;
                    }

                    string value = field.GetValue(target) as string;
                    if (!string.IsNullOrWhiteSpace(value) && RpgNpcDialogueArchiveManager.IsLikelySaveNameMember(field.Name))
                    {
                        return value;
                    }
                }
            }
            // RimAI.catch-boundary: ALLOWED_TOP_LEVEL_BOUNDARY - archive field probe fell through to the next strategy
            catch (System.Exception ex)
            {
                ModuleLog.Message("[RimAI.Relations] archive field probe fell through to the next strategy: " + ex.Message);
            }

            return string.Empty;
        }

internal static bool IsLikelySaveNameMember(string memberName)
        {
            if (string.IsNullOrWhiteSpace(memberName))
            {
                return false;
            }

            string lower = memberName.ToLowerInvariant();
            return lower.Contains("name") || lower.Contains("file");
        }

internal static string TryResolveLoadedGameNameFromMetaHeader()
        {
            try
            {
                Type headerType = RpgNpcDialogueArchiveManager.FindTypeInLoadedAssemblies("Verse.ScribeMetaHeaderUtility");
                if (headerType == null)
                {
                    return string.Empty;
                }

                PropertyInfo prop = headerType.GetProperty("loadedGameName", StaticStringMemberBinding);
                if (prop != null)
                {
                    string value = prop.GetValue(null, null) as string;
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }

                FieldInfo field = headerType.GetField("loadedGameName", StaticStringMemberBinding);
                if (field != null)
                {
                    string value = field.GetValue(null) as string;
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }
            // RimAI.catch-boundary: ALLOWED_TOP_LEVEL_BOUNDARY - archive field probe fell through to the next strategy
            catch (System.Exception ex)
            {
                ModuleLog.Message("[RimAI.Relations] archive field probe fell through to the next strategy: " + ex.Message);
            }

            return string.Empty;
        }

internal static string TryResolveLoadedGameNameFromKnownVerseStatics()
        {
            string[] typeNames =
            {
                "Verse.SavedGameLoaderNow",
                "Verse.GameDataSaveLoader",
                "Verse.ScribeMetaHeaderUtility"
            };

            string[] memberNames =
            {
                "loadedGameName",
                "loadingFromSaveFileName",
                "loadingSaveFileName",
                "currentSaveFileName",
                "curSaveFileName",
                "curFileName",
                "saveFileName",
                "fileName",
                "lastLoadedFileName",
                "lastSaveName"
            };

            for (int i = 0; i < typeNames.Length; i++)
            {
                Type type = RpgNpcDialogueArchiveManager.FindTypeInLoadedAssemblies(typeNames[i]);
                if (type == null)
                {
                    continue;
                }

                for (int j = 0; j < memberNames.Length; j++)
                {
                    string value = RpgNpcDialogueArchiveManager.ReadStaticStringMember(type, memberNames[j]);
                    if (!string.IsNullOrWhiteSpace(value) &&
                        !string.Equals(value, DefaultSaveName, StringComparison.OrdinalIgnoreCase))
                    {
                        return value;
                    }
                }
            }

            return string.Empty;
        }

internal static string ReadStaticStringMember(Type targetType, string memberName)
        {
            if (targetType == null || string.IsNullOrWhiteSpace(memberName))
            {
                return string.Empty;
            }

            try
            {
                PropertyInfo prop = targetType.GetProperty(memberName, StaticStringMemberBinding);
                if (prop?.PropertyType == typeof(string))
                {
                    string value = prop.GetValue(null, null) as string;
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }

                FieldInfo field = targetType.GetField(memberName, StaticStringMemberBinding);
                if (field?.FieldType == typeof(string))
                {
                    string value = field.GetValue(null) as string;
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }
            // RimAI.catch-boundary: ALLOWED_TOP_LEVEL_BOUNDARY - archive field probe fell through to the next strategy
            catch (System.Exception ex)
            {
                ModuleLog.Message("[RimAI.Relations] archive field probe fell through to the next strategy: " + ex.Message);
            }

            return string.Empty;
        }

internal string BuildSaveNameResolutionDiagnostic()
        {
            object gameInfo = Current.Game?.Info;
            string[] instanceMembers = { "name", "Name", "fileName", "FileName", "permadeathModeUniqueName" };
            string[] staticMembers = { "loadedGameName", "loadingFromSaveFileName", "curFileName", "saveFileName" };

            string gameInfoType = gameInfo?.GetType().FullName ?? "null";
            string gameInfoValues = string.Join(", ",
                instanceMembers.Select(member => $"{member}='{RpgNpcDialogueArchiveManager.ReadStringMember(gameInfo, member)}'"));

            string scribeValue = RpgNpcDialogueArchiveManager.TryResolveLoadedGameNameFromMetaHeader();
            string trackedSaveName = SaveContextTracker.GetCurrentSaveName();
            string persistentSlotId = RpgNpcDialogueArchiveManager.ResolvePersistentRpgSaveSlotId();
            string staticValues = string.Join(", ", staticMembers.Select(member =>
            {
                string savedGameLoaderNow = RpgNpcDialogueArchiveManager.ReadStaticStringMember(RpgNpcDialogueArchiveManager.FindTypeInLoadedAssemblies("Verse.SavedGameLoaderNow"), member);
                string gameDataSaveLoader = RpgNpcDialogueArchiveManager.ReadStaticStringMember(RpgNpcDialogueArchiveManager.FindTypeInLoadedAssemblies("Verse.GameDataSaveLoader"), member);
                return $"{member}:[SavedGameLoaderNow='{savedGameLoaderNow}',GameDataSaveLoader='{gameDataSaveLoader}']";
            }));

            return $"gameInfoType={gameInfoType}; gameInfo={gameInfoValues}; tracked='{trackedSaveName}'; slot='{persistentSlotId}'; metaHeader='{scribeValue}'; static={staticValues}";
        }

internal static Type FindTypeInLoadedAssemblies(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return null;
            }

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type found = assembly.GetType(fullName, false, true);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

internal static string ResolvePersistentRpgSaveSlotId()
        {
            try
            {
                string slotId = GameComponent_RPGManager.Instance?.GetPersistentRpgSaveSlotId();
                return string.IsNullOrWhiteSpace(slotId) ? string.Empty : slotId.SanitizeFileName();
            }
            catch
            {
                return string.Empty;
            }
        }

internal static string BuildArchiveFileName(RpgNpcDialogueArchive archive)
        {
            string safeName = (archive?.PawnName ?? "UnknownPawn").SanitizeFileName();
            return $"npc_{archive.PawnLoadId}_{safeName}.json";
        }
    }

    internal sealed class RpgNpcArchiveSlice5 : RpgNpcDialogueArchiveManagerCollaborator
    {
        internal RpgNpcArchiveSlice5(RpgNpcDialogueArchiveManager owner) : base(owner)
        {
        }

internal void CleanupLegacyArchiveFiles(int pawnLoadId, string keepFileName)
        {
            if (!LocalStorage.Current.DirectoryExists(CurrentArchiveDirPath))
            {
                return;
            }

            string keepPath = Path.Combine(CurrentArchiveDirPath, keepFileName);
            IEnumerable<string> candidates = LocalStorage.Current.GetFiles(CurrentArchiveDirPath, $"npc_{pawnLoadId}.json")
                .Concat(LocalStorage.Current.GetFiles(CurrentArchiveDirPath, $"npc_{pawnLoadId}_*.json"))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (string path in candidates)
            {
                if (string.Equals(path, keepPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    LocalStorage.Current.DeleteFile(path);
                }
                catch (Exception ex)
                {
                    Log.Warning($"[RimAI.Relations] Failed to create archive directory: {ex.Message}");
                }
            }
        }

internal static void NormalizeArchiveTurns(RpgNpcDialogueArchive archive)
        {
            if (archive == null)
            {
                return;
            }

            if (archive.Sessions == null)
            {
                archive.Sessions = new List<RpgNpcDialogueSessionArchive>();
            }

            archive.Sessions = archive.Sessions
                .Where(session => session != null)
                .OrderBy(session => session.StartedTick > 0 ? session.StartedTick : int.MaxValue)
                .ThenBy(session => session.EndedTick > 0 ? session.EndedTick : int.MaxValue)
                .ThenBy(session => session.SessionId ?? string.Empty, StringComparer.Ordinal)
                .ToList();

            RpgNpcDialogueArchiveManager.EnsureTurnSequenceState(archive);
            RpgNpcDialogueArchiveManager.TrimArchiveSessions(archive);
        }

internal static void EnsureTurnSequenceState(RpgNpcDialogueArchive archive)
        {
            if (archive?.Sessions == null)
            {
                return;
            }

            long next = archive.NextTurnSequence > 0 ? archive.NextTurnSequence : 1L;
            for (int i = 0; i < archive.Sessions.Count; i++)
            {
                RpgNpcDialogueSessionArchive session = archive.Sessions[i];
                if (session == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(session.SessionId))
                {
                    session.SessionId = Guid.NewGuid().ToString("N");
                }

                if (string.IsNullOrWhiteSpace(session.SummaryState))
                {
                    session.SummaryState = RpgNpcDialogueSessionSummaryState.Pending;
                }

                List<RpgNpcDialogueTurnArchive> turns = session.Turns ?? new List<RpgNpcDialogueTurnArchive>();
                turns = turns
                    .Where(turn => turn != null && !string.IsNullOrWhiteSpace(turn.Text))
                    .GroupBy(turn =>
                        $"{turn.GameTick}|{turn.IsPlayer}|{turn.SpeakerPawnLoadId}|{turn.InterlocutorPawnLoadId}|{turn.Text.Trim()}")
                    .Select(group => group.OrderBy(turn => turn.TurnSequence).First())
                    .OrderBy(turn => turn.GameTick)
                    .ThenBy(turn => turn.TurnSequence)
                    .ToList();

                if (string.Equals(session.SummaryState, RpgNpcDialogueSessionSummaryState.Compressed, StringComparison.OrdinalIgnoreCase))
                {
                    session.IsFinalized = true;
                }

                if (string.Equals(session.SummaryState, RpgNpcDialogueSessionSummaryState.Compressed, StringComparison.OrdinalIgnoreCase) &&
                    turns.Count > 0)
                {
                    // Heal invalid mixed state generated by early compression of active sessions.
                    session.SummaryState = RpgNpcDialogueSessionSummaryState.Pending;
                    session.SummaryText = string.Empty;
                    session.LastSummaryAttemptTick = 0;
                }

                for (int turnIndex = 0; turnIndex < turns.Count; turnIndex++)
                {
                    RpgNpcDialogueTurnArchive turn = turns[turnIndex];
                    if (turn.TurnSequence <= 0)
                    {
                        turn.TurnSequence = next;
                        next++;
                        continue;
                    }

                    if (turn.TurnSequence >= next)
                    {
                        next = turn.TurnSequence + 1L;
                    }
                }

                session.Turns = turns;
                session.TurnCount = Math.Max(session.TurnCount, RpgNpcDialogueArchiveManager.CountDialogueTurns(session.Turns));
                if (session.StartedTick <= 0 && session.Turns.Count > 0)
                {
                    session.StartedTick = session.Turns.Min(turn => turn.GameTick);
                }

                if (session.EndedTick <= 0 && session.Turns.Count > 0)
                {
                    session.EndedTick = session.Turns.Max(turn => turn.GameTick);
                }
            }

            archive.NextTurnSequence = Math.Max(archive.NextTurnSequence, next);
        }

internal static void TrimArchiveSessions(RpgNpcDialogueArchive archive)
        {
            if (archive?.Sessions == null || archive.Sessions.Count == 0)
            {
                return;
            }

            string retainedId = RpgNpcDialogueArchiveManager.SelectLatestRetainedFullSession(archive)?.SessionId ?? string.Empty;

            while (archive.Sessions.Count > MaxSessionsPerNpc)
            {
                RpgNpcDialogueSessionArchive removable = archive.Sessions
                    .Where(session =>
                        session != null &&
                        !string.Equals(session.SessionId, retainedId, StringComparison.Ordinal))
                    .OrderBy(session =>
                        string.Equals(session.SummaryState, RpgNpcDialogueSessionSummaryState.Compressed, StringComparison.OrdinalIgnoreCase)
                            ? 0
                            : 1)
                    .ThenBy(session => session.EndedTick > 0 ? session.EndedTick : int.MaxValue)
                    .ThenBy(session => session.StartedTick > 0 ? session.StartedTick : int.MaxValue)
                    .FirstOrDefault();

                if (removable == null)
                {
                    break;
                }

                archive.Sessions.Remove(removable);
            }

            int totalTurns = archive.Sessions.Sum(session => session?.Turns?.Count ?? 0);
            if (totalTurns <= MaxTurnsPerNpc)
            {
                return;
            }

            while (totalTurns > MaxTurnsPerNpc)
            {
                RpgNpcDialogueSessionArchive target = archive.Sessions
                    .Where(session =>
                        session != null &&
                        session.Turns != null &&
                        session.Turns.Count > 0 &&
                        !string.Equals(session.SessionId, retainedId, StringComparison.Ordinal))
                    .OrderBy(session => session.EndedTick > 0 ? session.EndedTick : int.MaxValue)
                    .ThenBy(session => session.StartedTick > 0 ? session.StartedTick : int.MaxValue)
                    .FirstOrDefault();

                if (target == null)
                {
                    break;
                }

                int trimCount = Math.Min(totalTurns - MaxTurnsPerNpc, target.Turns.Count);
                if (trimCount <= 0)
                {
                    break;
                }

                target.Turns.RemoveRange(0, trimCount);
                target.TurnCount = Math.Max(target.TurnCount, RpgNpcDialogueArchiveManager.CountDialogueTurns(target.Turns));
                totalTurns = archive.Sessions.Sum(session => session?.Turns?.Count ?? 0);
            }
        }

internal static void MergeArchiveData(RpgNpcDialogueArchive existing, RpgNpcDialogueArchive incoming)
        {
            if (existing == null || incoming == null)
            {
                return;
            }

            RpgNpcDialogueArchiveManager.EnsureTurnSequenceState(existing);
            RpgNpcDialogueArchiveManager.EnsureTurnSequenceState(incoming);
            if (string.IsNullOrWhiteSpace(existing.SaveKey) && !string.IsNullOrWhiteSpace(incoming.SaveKey))
            {
                existing.SaveKey = incoming.SaveKey;
            }

            if (incoming.LastInteractionTick > existing.LastInteractionTick)
            {
                existing.LastInteractionTick = incoming.LastInteractionTick;
                existing.PawnName = incoming.PawnName;
                existing.FactionId = incoming.FactionId;
                existing.FactionName = incoming.FactionName;
                existing.LastInterlocutorPawnLoadId = incoming.LastInterlocutorPawnLoadId;
                existing.LastInterlocutorName = incoming.LastInterlocutorName;
                existing.PersonaPrompt = incoming.PersonaPrompt;
                existing.CooldownUntilTick = incoming.CooldownUntilTick;
                existing.CreatedTimestamp = Math.Min(existing.CreatedTimestamp, incoming.CreatedTimestamp);
                existing.LastSavedTimestamp = Math.Max(existing.LastSavedTimestamp, incoming.LastSavedTimestamp);
                existing.NextTurnSequence = Math.Max(existing.NextTurnSequence, incoming.NextTurnSequence);
            }

            if (incoming.Sessions != null && incoming.Sessions.Count > 0)
            {
                if (existing.Sessions == null)
                {
                    existing.Sessions = new List<RpgNpcDialogueSessionArchive>();
                }

                for (int i = 0; i < incoming.Sessions.Count; i++)
                {
                    RpgNpcDialogueSessionArchive incomingSession = incoming.Sessions[i];
                    if (incomingSession == null)
                    {
                        continue;
                    }

                    string sessionId = string.IsNullOrWhiteSpace(incomingSession.SessionId)
                        ? Guid.NewGuid().ToString("N")
                        : incomingSession.SessionId;
                    RpgNpcDialogueSessionArchive existingSession = existing.Sessions.FirstOrDefault(session =>
                        session != null &&
                        string.Equals(session.SessionId, sessionId, StringComparison.Ordinal));

                    if (existingSession == null)
                    {
                        existingSession = RpgNpcDialogueArchiveManager.CloneSession(incomingSession);
                        existingSession.SessionId = sessionId;
                        existing.Sessions.Add(existingSession);
                        continue;
                    }

                    existingSession.StartedTick = existingSession.StartedTick > 0
                        ? Math.Min(existingSession.StartedTick, incomingSession.StartedTick > 0 ? incomingSession.StartedTick : existingSession.StartedTick)
                        : incomingSession.StartedTick;
                    existingSession.EndedTick = Math.Max(existingSession.EndedTick, incomingSession.EndedTick);
                    existingSession.TurnCount = Math.Max(existingSession.TurnCount, incomingSession.TurnCount);
                    if (incomingSession.InterlocutorPawnLoadId > 0)
                    {
                        existingSession.InterlocutorPawnLoadId = incomingSession.InterlocutorPawnLoadId;
                    }
                    if (!string.IsNullOrWhiteSpace(incomingSession.InterlocutorName))
                    {
                        existingSession.InterlocutorName = incomingSession.InterlocutorName;
                    }
                    if (!string.IsNullOrWhiteSpace(incomingSession.SummaryText))
                    {
                        existingSession.SummaryText = incomingSession.SummaryText;
                    }
                    if (!string.IsNullOrWhiteSpace(incomingSession.SummaryState))
                    {
                        existingSession.SummaryState = incomingSession.SummaryState;
                    }
                    existingSession.IsFinalized = existingSession.IsFinalized || incomingSession.IsFinalized;
                    existingSession.LastSummaryAttemptTick = Math.Max(existingSession.LastSummaryAttemptTick, incomingSession.LastSummaryAttemptTick);
                    if (incomingSession.Turns != null && incomingSession.Turns.Count > 0)
                    {
                        if (existingSession.Turns == null)
                        {
                            existingSession.Turns = new List<RpgNpcDialogueTurnArchive>();
                        }

                        existingSession.Turns.AddRange(incomingSession.Turns.Where(turn => turn != null).Select(CloneTurn));
                    }
                }

                RpgNpcDialogueArchiveManager.NormalizeArchiveTurns(existing);
            }
            else
            {
                existing.NextTurnSequence = Math.Max(existing.NextTurnSequence, incoming.NextTurnSequence);
            }
        }

internal static RpgNpcDialogueSessionArchive CloneSession(RpgNpcDialogueSessionArchive session)
        {
            if (session == null)
            {
                return null;
            }

            return new RpgNpcDialogueSessionArchive
            {
                SessionId = session.SessionId ?? string.Empty,
                StartedTick = session.StartedTick,
                EndedTick = session.EndedTick,
                TurnCount = session.TurnCount,
                IsFinalized = session.IsFinalized,
                InterlocutorPawnLoadId = session.InterlocutorPawnLoadId,
                InterlocutorName = session.InterlocutorName ?? string.Empty,
                SummaryText = session.SummaryText ?? string.Empty,
                SummaryState = session.SummaryState ?? RpgNpcDialogueSessionSummaryState.Pending,
                LastSummaryAttemptTick = session.LastSummaryAttemptTick,
                Turns = session.Turns?.Where(turn => turn != null).Select(CloneTurn).ToList() ?? new List<RpgNpcDialogueTurnArchive>()
            };
        }

internal static RpgNpcDialogueTurnArchive CloneTurn(RpgNpcDialogueTurnArchive turn)
        {
            if (turn == null)
            {
                return null;
            }

            return new RpgNpcDialogueTurnArchive
            {
                IsPlayer = turn.IsPlayer,
                TurnSequence = turn.TurnSequence,
                SpeakerPawnLoadId = turn.SpeakerPawnLoadId,
                SpeakerName = turn.SpeakerName ?? string.Empty,
                InterlocutorPawnLoadId = turn.InterlocutorPawnLoadId,
                InterlocutorName = turn.InterlocutorName ?? string.Empty,
                Text = turn.Text ?? string.Empty,
                GameTick = turn.GameTick
            };
        }

internal static uint ComputeStableHash(string text)
        {
            string input = string.IsNullOrWhiteSpace(text) ? "Default" : text;
            uint hash = 2166136261;
            for (int i = 0; i < input.Length; i++)
            {
                hash ^= input[i];
                hash *= 16777619;
            }
            return hash;
        }

internal static List<RpgNpcDialogueTurnArchive> BuildRelevantSummaryTurns(
            List<RpgNpcDialogueTurnArchive> sourceTurns,
            Pawn currentInterlocutor,
            string interlocutorName)
        {
            var summaryTurns = sourceTurns?
                .Where(turn => turn != null && RpgNpcDialogueArchiveManager.IsDiplomacySummaryTurn(turn.Text))
                .OrderByDescending(turn => turn.GameTick)
                .ThenByDescending(turn => turn.TurnSequence)
                .ToList() ?? new List<RpgNpcDialogueTurnArchive>();

            if (summaryTurns.Count == 0)
            {
                return summaryTurns;
            }

            int interlocutorId = currentInterlocutor?.thingIDNumber ?? -1;
            if (interlocutorId > 0)
            {
                List<RpgNpcDialogueTurnArchive> byId = summaryTurns
                    .Where(turn => turn.InterlocutorPawnLoadId == interlocutorId || turn.SpeakerPawnLoadId == interlocutorId)
                    .ToList();
                if (byId.Count > 0)
                {
                    return byId;
                }
            }

            if (!RpgNpcDialogueArchiveManager.IsPlaceholderInterlocutorName(interlocutorName))
            {
                List<RpgNpcDialogueTurnArchive> byName = summaryTurns
                    .Where(turn =>
                        string.Equals(turn.InterlocutorName, interlocutorName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(turn.SpeakerName, interlocutorName, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (byName.Count > 0)
                {
                    return byName;
                }
            }

            return summaryTurns;
        }

internal static void AppendDiplomacySummaryMemoryLines(StringBuilder sb, List<RpgNpcDialogueTurnArchive> summaryTurns)
        {
            if (sb == null || summaryTurns == null || summaryTurns.Count == 0)
            {
                return;
            }

            List<RpgNpcDialogueTurnArchive> picked = summaryTurns
                .Take(3)
                .OrderBy(turn => turn.GameTick)
                .ThenBy(turn => turn.TurnSequence)
                .ToList();
            if (picked.Count == 0)
            {
                return;
            }

            sb.AppendLine("Recent diplomacy summary memories:");
            for (int i = 0; i < picked.Count; i++)
            {
                string text = RpgNpcDialogueArchiveManager.StripDiplomacySummaryPrefix(picked[i].Text);
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                sb.AppendLine($"- {RpgNpcDialogueArchiveManager.TrimForPrompt(text, 180)}");
            }
        }
    }

    internal sealed class RpgNpcDialogueArchiveManagerParts
    {
        internal readonly RpgNpcDialogueArchiveManager Owner;
        internal readonly RpgNpcDialogueArchiveManagerPromptCache PromptCache;
        internal readonly RpgNpcDialogueArchiveManagerSessions Sessions;
        internal readonly RpgNpcDialogueArchiveManagerWarmup Warmup;
        internal readonly RpgNpcArchiveSlice1 Slice1;
        internal readonly RpgNpcArchiveSlice2 Slice2;
        internal readonly RpgNpcArchiveSlice3 Slice3;
        internal readonly RpgNpcArchiveSlice4 Slice4;
        internal readonly RpgNpcArchiveSlice5 Slice5;
        internal readonly RpgNpcArchiveSlice6 Slice6;
        internal RpgNpcDialogueArchiveManagerParts(RpgNpcDialogueArchiveManager owner)
        {
            Owner = owner;
            PromptCache = new RpgNpcDialogueArchiveManagerPromptCache(owner);
            Sessions = new RpgNpcDialogueArchiveManagerSessions(owner);
            Warmup = new RpgNpcDialogueArchiveManagerWarmup(owner);
            Slice1 = new RpgNpcArchiveSlice1(owner);
            Slice2 = new RpgNpcArchiveSlice2(owner);
            Slice3 = new RpgNpcArchiveSlice3(owner);
            Slice4 = new RpgNpcArchiveSlice4(owner);
            Slice5 = new RpgNpcArchiveSlice5(owner);
            Slice6 = new RpgNpcArchiveSlice6(owner);
        }
    }


}
