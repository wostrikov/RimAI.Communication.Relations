using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.Diagnostics;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Prompting.Diplomacy;
using Ustas.RimAI.Communication.Relations.NpcDialogue;
using Ustas.RimAI.Communication.Relations.Context;

using PendingSocialNewsRequest = Ustas.RimAI.Communication.Relations.DiplomacySystem.DiplomacyManagerSocialCircleNewsRequests.PendingSocialNewsRequest;

namespace Ustas.RimAI.Communication.Relations.DiplomacySystem
{
    public class GameComponent_DiplomacyManager : GameComponent
    {
        internal GameComponent_DiplomacyManagerParts Parts;
        internal const int MaxSocialPosts = DiplomacyManagerSocialCircle.MaxSocialPosts;
        public const int ManualSocialPostTitleMaxLength = DiplomacyManagerSocialCircleManualPost.ManualSocialPostTitleMaxLength;
        public const int ManualSocialPostBodyMaxLength = DiplomacyManagerSocialCircleManualPost.ManualSocialPostBodyMaxLength;

        internal HashSet<Faction> aiControlledFactions = new HashSet<Faction>();
        internal HashSet<Faction> manuallyVisibleHiddenFactions = new HashSet<Faction>();
        internal List<AlbumImageEntry> albumEntries = new List<AlbumImageEntry>();
        internal SocialCircleState socialCircleState = new SocialCircleState();
        internal List<FactionDialogueSession> dialogueSessions = new List<FactionDialogueSession>();
        internal Dictionary<Faction, FactionDialogueSession> dialogueSessionsByFaction = new Dictionary<Faction, FactionDialogueSession>();
        internal List<FactionPresenceState> presenceStates = new List<FactionPresenceState>();
        internal Dictionary<Faction, FactionPresenceState> presenceStatesByFaction = new Dictionary<Faction, FactionPresenceState>();
        internal List<DelayedDiplomacyEvent> delayedEvents = new List<DelayedDiplomacyEvent>();
        internal int lastNegotiatorThingId = -1;
        internal const int ForcedOfflineDurationHours = 1;
        internal const int ForcedDoNotDisturbDurationHours = 2;
        internal readonly List<DelayedDiplomacyEvent> delayedEventsPendingAdd = new List<DelayedDiplomacyEvent>();
        internal bool isProcessingDelayedEvents = false;
        internal int lastProcessedDelayedEventsTick = -1;

        // Temporary cross-faction peace during CallEveryone windows (persisted)
        public TempFactionRelationState tempFactionRelations = new TempFactionRelationState();

        internal int _lastAiToAiGenerationTick = 0;
        internal const int AiToAiGenerationIntervalTicks = 120000; // 2 game days

        public static GameComponent_DiplomacyManager Instance = null;

        public GameComponent_DiplomacyManager(Game game)
        {
            Parts = new GameComponent_DiplomacyManagerParts(this);
            Instance = this;
        }

        public int GetLastNegotiatorThingId() => lastNegotiatorThingId;

        public void SetLastNegotiatorThingId(int thingId)
        {
            lastNegotiatorThingId = thingId;
        }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            AIChatServiceAsync.NotifyGameContextChanged("Started new game");
            GameAIInterface.Instance?.ResetPrisonerRansomRuntimeState();
            InitializeAIControlledFactions();
            InitializeDialogueSessions();
            InitializePresenceStates();
            RebuildDialogueSessionIndex();
            RebuildPresenceStateIndex();
            presenceEvalCacheKey.Clear();
            presenceEvalCacheResult.Clear();
            InitializeSocialCircleOnNewGame();
            LeaderMemoryManager.Instance.OnNewGame();
            DiplomacyPromptSnapshotCache.Instance.WarmupOnLoad();
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            AIChatServiceAsync.NotifyGameContextChanged("Loaded game");
            GameAIInterface.Instance?.ResetPrisonerRansomRuntimeState();
            if (aiControlledFactions == null)
            {
                aiControlledFactions = new HashSet<Faction>();
                InitializeAIControlledFactions();
            }
            if (dialogueSessions == null)
            {
                dialogueSessions = new List<FactionDialogueSession>();
                InitializeDialogueSessions();
            }
            if (presenceStates == null)
            {
                presenceStates = new List<FactionPresenceState>();
            }
            InitializePresenceStates();
            RebuildDialogueSessionIndex();
            RebuildPresenceStateIndex();
            presenceEvalCacheKey.Clear();
            presenceEvalCacheResult.Clear();
            InitializeSocialCircleOnLoadedGame();
            CleanupInvalidSessions();
            CleanupInvalidPresenceStates();
            LeaderMemoryManager.Instance.OnLoadedGame();
            DiplomacyPromptSnapshotCache.Instance.WarmupOnLoad();
        }

        

        

        

        

        

        

        

        

        

        

        

        public List<FactionDialogueSession> GetAllDialogueSessions()
        {
            return dialogueSessions ?? new List<FactionDialogueSession>();
        }

        

        

        

        

        

        public bool CanSendMessage(Faction faction)
        {
            return GetPresenceStatus(faction) == FactionPresenceStatus.Online;
        }

        

        

        

        

        

        

        

        

        

        

        internal int lastDailyResetTick = 0;
        internal int lastPeriodicSnapshotTick = 0;
        internal const int PeriodicSnapshotIntervalTicks = 1500; // ~30 seconds

        // Per-faction presence evaluation cache keyed by (faction.loadID, dayIndex, hour).
        // Avoids repeated Rand.PushState/PopState when the same faction is resolved multiple times within the same game hour.
        internal readonly Dictionary<Faction, int> presenceEvalCacheKey = new Dictionary<Faction, int>();
        internal readonly Dictionary<Faction, FactionPresenceStatus> presenceEvalCacheResult = new Dictionary<Faction, FactionPresenceStatus>();

        public override void GameComponentTick()
        {
            int currentTick = Find.TickManager.TicksGame;

            if (currentTick % 60 == 0)
            {
                using (PerfScope.Measure("DiplomacyMgr.SnapshotCacheTick"))
                    DiplomacyPromptSnapshotCache.Instance.Tick(currentTick, maxBuildsPerTick: 1);
            }

            if (currentTick % 2000 == 0)
            {
                using (PerfScope.Measure("DiplomacyMgr.AIDecisions"))
                    ProcessAIDecisions();
            }

            if (currentTick - lastPeriodicSnapshotTick >= PeriodicSnapshotIntervalTicks)
            {
                using (PerfScope.Measure("DiplomacyMgr.PeriodicSnapshot"))
                    ProcessPeriodicDiplomacySnapshots();
                lastPeriodicSnapshotTick = currentTick;
            }

            if (currentTick - lastDailyResetTick >= 60000)
            {
                DailyReset();
                lastDailyResetTick = currentTick;
            }

            if (currentTick % 60000 == 0)
            {
                FactionSpecialItemsManager.Instance.Tick();
            }

            if (currentTick % 2500 == 0)
                TryRestoreTempFactionRelations(currentTick);

        }

        

        

        

        

        

        

        

        public bool IsAIControlled(Faction faction)
        {
            return aiControlledFactions.Contains(faction);
        }

        public List<DelayedDiplomacyEvent> GetDelayedEvents()
        {
            return delayedEvents;
        }

        public override void ExposeData()
        {
            base.ExposeData();

            // Save all leader memory to file before saving game
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                LeaderMemoryManager.Instance.OnBeforeGameSave();
            }

            try
            {
                Scribe_Collections.Look(ref aiControlledFactions, "aiControlledFactions", LookMode.Reference);
                
                Scribe_Collections.Look(ref dialogueSessions, "dialogueSessions", LookMode.Deep);
                Scribe_Collections.Look(ref presenceStates, "presenceStates", LookMode.Deep);
                Scribe_Collections.Look(ref delayedEvents, "delayedEvents", LookMode.Deep);
                Scribe_Collections.Look(ref manuallyVisibleHiddenFactions, "manuallyVisibleHiddenFactions", LookMode.Reference);
                Scribe_Collections.Look(ref albumEntries, "albumEntries", LookMode.Deep);
                Scribe_Deep.Look(ref socialCircleState, "socialCircleState");
                Scribe_Values.Look(ref lastDailyResetTick, "lastDailyResetTick", 0);
                Scribe_Values.Look(ref lastNegotiatorThingId, "lastNegotiatorThingId", -1);
                Scribe_Values.Look(ref _lastAiToAiGenerationTick, "lastAiToAiGenerationTick", 0);
                Scribe_Deep.Look(ref tempFactionRelations, "tempFactionRelations");

                // Save/load GameAIInterface data
                GameAIInterface.Instance?.ExposeData();
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAI.Relations] Error loading DiplomacyManager data from save: {ex.Message}\n{ex.StackTrace}");
                // Ensure collections are non-null to prevent NullReferenceException later
                aiControlledFactions ??= new HashSet<Faction>();
                dialogueSessions ??= new List<FactionDialogueSession>();
                dialogueSessionsByFaction ??= new Dictionary<Faction, FactionDialogueSession>();
                presenceStates ??= new List<FactionPresenceState>();
                presenceStatesByFaction ??= new Dictionary<Faction, FactionPresenceState>();
                delayedEvents ??= new List<DelayedDiplomacyEvent>();
                manuallyVisibleHiddenFactions ??= new HashSet<Faction>();
                albumEntries ??= new List<AlbumImageEntry>();
                socialCircleState ??= new SocialCircleState();
                tempFactionRelations ??= new TempFactionRelationState();
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (aiControlledFactions == null)
                    aiControlledFactions = new HashSet<Faction>();
                if (dialogueSessions == null)
                    dialogueSessions = new List<FactionDialogueSession>();
                dialogueSessionsByFaction ??= new Dictionary<Faction, FactionDialogueSession>();
                if (presenceStates == null)
                    presenceStates = new List<FactionPresenceState>();
                presenceStatesByFaction ??= new Dictionary<Faction, FactionPresenceState>();
                if (delayedEvents == null)
                    delayedEvents = new List<DelayedDiplomacyEvent>();
                if (manuallyVisibleHiddenFactions == null)
                    manuallyVisibleHiddenFactions = new HashSet<Faction>();
                if (albumEntries == null)
                    albumEntries = new List<AlbumImageEntry>();
                if (socialCircleState == null)
                    socialCircleState = new SocialCircleState();
                delayedEventsPendingAdd.Clear();
                isProcessingDelayedEvents = false;
                lastProcessedDelayedEventsTick = -1;

                // Clean up factions excluded by mod compatibility config
                if (aiControlledFactions != null)
                {
                    var toRemove = new List<Faction>();
                    foreach (var f in aiControlledFactions)
                    {
                        if (ShouldExcludeFactionFromAI(f))
                            toRemove.Add(f);
                    }
                    foreach (var f in toRemove)
                        aiControlledFactions.Remove(f);
                }

                EnsureHiddenFactionVisibilityState();
                RebuildDialogueSessionIndex();
                RebuildPresenceStateIndex();

                if (delayedEvents != null && Find.TickManager != null)
                {
                    int currentTick = Find.TickManager.TicksGame;
                    int baseAidDelay = RelationsMod.Instance?.InstanceSettings?.AidDelayBaseTicks ?? 90000;
                    int baseCaravanDelay = RelationsMod.Instance?.InstanceSettings?.CaravanDelayBaseTicks ?? 135000;
                    
                    foreach (var evt in delayedEvents)
                    {
                        int baseDelay = evt.EventType == DelayedEventType.Aid ? baseAidDelay : baseCaravanDelay;
                        
                        if (evt.ExecuteTick <= currentTick)
                        {
                            int minDelay = (int)(baseDelay * 0.2f);
                            int maxDelay = baseDelay;
                            evt.ExecuteTick = currentTick + Rand.Range(minDelay, maxDelay);
                            ModuleLog.Message($"[RimAI.Relations] Adjusted delayed {evt.EventType} from {evt.Faction?.Name}: tick was in past, new tick={evt.ExecuteTick}");
                        }
                        else if (evt.ExecuteTick - currentTick > baseDelay * 2)
                        {
                            evt.ExecuteTick = currentTick + Rand.Range(baseDelay, baseDelay * 2);
                            ModuleLog.Message($"[RimAI.Relations] Adjusted delayed {evt.EventType} from {evt.Faction?.Name}: delay was too long, new tick={evt.ExecuteTick}");
                        }
                    }

                    MigrateLegacyRaidCallEveryoneEvents(currentTick);

                    // Re-apply temp faction peace after save load
                    if (tempFactionRelations != null
                        && tempFactionRelations.originalRelations.Count > 0
                        && tempFactionRelations.restoreAtTick > currentTick)
                    {
                        ModuleLog.Message($"[RimAI.Relations] Re-applying {tempFactionRelations.originalRelations.Count} temp faction peace overrides after load (restoreAtTick={tempFactionRelations.restoreAtTick})");
                        foreach (var kv in tempFactionRelations.originalRelations)
                        {
                            string[] ids = kv.Key.Split(':');
                            if (ids.Length != 2) continue;
                            Faction fa = Find.FactionManager?.AllFactions?.FirstOrDefault(f => f?.loadID.ToString() == ids[0]);
                            Faction fb = Find.FactionManager?.AllFactions?.FirstOrDefault(f => f?.loadID.ToString() == ids[1]);
                            if (fa == null || fb == null || fa.defeated || fb.defeated) continue;
                            fa.SetRelationDirect(fb, FactionRelationKind.Neutral);
                            ModuleLog.Message($"[RimAI.Relations] Re-applied temp peace: {fa.Name} <-> {fb.Name} (was {kv.Value})");
                        }
                    }
                }
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                LeaderMemoryManager.Instance.OnAfterGameLoad(dialogueSessions);
            }
        }

        internal bool IsPresenceEnabled()
        {
            return RelationsMod.Instance?.InstanceSettings?.EnableFactionPresenceStatus ?? true;
        }

        

        internal int GetPresenceForcedOfflineTicks()
        {
            return ForcedOfflineDurationHours * GenDate.TicksPerHour;
        }

        

        internal int GetPresenceDoNotDisturbTicks()
        {
            return ForcedDoNotDisturbDurationHours * GenDate.TicksPerHour;
        }

        

        internal bool IsNightBiasEnabled()
        {
            return RelationsMod.Instance?.InstanceSettings?.PresenceNightBiasEnabled ?? true;
        }

        

        

        

        

        

        

        

        

        // ── Temporary cross-faction peace for CallEveryone windows ──

        

        

        


        #region Facade forwards
        public bool AddAlbumEntry(AlbumImageEntry entry) => Parts.Album.AddAlbumEntry(entry);
        public List<AlbumImageEntry> GetAlbumEntries() => Parts.Album.GetAlbumEntries();
        public int PruneMissingAlbumFiles() => Parts.Album.PruneMissingAlbumFiles();
        public bool RemoveAlbumEntry(string id) => Parts.Album.RemoveAlbumEntry(id);
        public bool HasCaravanDispatchedNow(Faction faction) => Parts.EventQueries.HasCaravanDispatchedNow(faction);
        public bool HasRaidScheduledNow(Faction faction) => Parts.EventQueries.HasRaidScheduledNow(faction);
        public List<Faction> GetManuallyVisibleHiddenFactions() => Parts.HiddenFactionVisibility.GetManuallyVisibleHiddenFactions();
        public bool IsHiddenFactionManuallyVisible(Faction faction) => Parts.HiddenFactionVisibility.IsHiddenFactionManuallyVisible(faction);
        public void SetManuallyVisibleHiddenFactions(IEnumerable<Faction> factions) => Parts.HiddenFactionVisibility.SetManuallyVisibleHiddenFactions(factions);
        internal void EnsureHiddenFactionVisibilityState() => Parts.HiddenFactionVisibility.EnsureHiddenFactionVisibilityState();
        internal void CleanupManuallyVisibleHiddenFactions() => Parts.HiddenFactionVisibility.CleanupManuallyVisibleHiddenFactions();
        internal static bool IsSelectableHiddenFaction(Faction faction) => DiplomacyManagerHiddenFactionVisibility.IsSelectableHiddenFaction(faction);
        internal void InitializeSocialCircleOnNewGame() => Parts.SocialCircle.InitializeSocialCircleOnNewGame();
        internal void InitializeSocialCircleOnLoadedGame() => Parts.SocialCircle.InitializeSocialCircleOnLoadedGame();
        public void ProcessSocialCircleTick() => Parts.SocialCircle.ProcessSocialCircleTick();
        internal void OnSocialCircleDailyReset() => Parts.SocialCircle.OnSocialCircleDailyReset();
        public bool IsSocialCircleEnabled() => Parts.SocialCircle.IsSocialCircleEnabled();
        public bool ForceGeneratePublicPost(DebugGenerateReason reason = DebugGenerateReason.ManualButton) => Parts.SocialCircle.ForceGeneratePublicPost(reason);
        public bool TryForceGeneratePublicPost(DebugGenerateReason reason, out SocialForceGenerateFailureReason failureReason) => Parts.SocialCircle.TryForceGeneratePublicPost(reason, out failureReason);
        public bool EnqueuePublicPost(Faction sourceFaction, Faction targetFaction, SocialPostCategory category, int sentiment, string summary, bool isFromPlayerDialogue, string intentHint = "", DebugGenerateReason reason = DebugGenerateReason.DialogueExplicit) => Parts.SocialCircle.EnqueuePublicPost(sourceFaction, targetFaction, category, sentiment, summary, isFromPlayerDialogue, intentHint, reason);
        public bool EnqueuePublicPost(Faction sourceFaction, Faction targetFaction, SocialPostCategory category, int sentiment, string summary, bool isFromPlayerDialogue, out SocialPostEnqueueResult enqueueResult, string intentHint = "", DebugGenerateReason reason = DebugGenerateReason.DialogueExplicit) => Parts.SocialCircle.EnqueuePublicPost(sourceFaction, targetFaction, category, sentiment, summary, isFromPlayerDialogue, out enqueueResult, intentHint, reason);
        public bool TryCreateKeywordDialoguePost(Faction sourceFaction, string playerMessage, string aiResponse) => Parts.SocialCircle.TryCreateKeywordDialoguePost(sourceFaction, playerMessage, aiResponse);
        public bool TryCreateKeywordDialoguePost(Faction sourceFaction, string playerMessage, string aiResponse, out SocialPostEnqueueResult enqueueResult) => Parts.SocialCircle.TryCreateKeywordDialoguePost(sourceFaction, playerMessage, aiResponse, out enqueueResult);
        public Faction ResolveSocialTargetFaction(string token, Faction sourceFaction = null) => Parts.SocialCircle.ResolveSocialTargetFaction(token, sourceFaction);
        public List<PublicSocialPost> GetSocialPosts(int maxCount = MaxSocialPosts) => Parts.SocialCircle.GetSocialPosts(maxCount);
        public int GetSocialPostListVersion() => Parts.SocialCircle.GetSocialPostListVersion();
        public int GetUnreadSocialPostCount() => Parts.SocialCircle.GetUnreadSocialPostCount();
        public void MarkSocialPostsRead() => Parts.SocialCircle.MarkSocialPostsRead();
        internal void EnsureSocialCircleState() => Parts.SocialCircle.EnsureSocialCircleState();
        internal void EnsureNextSocialPostTick(int currentTick) => Parts.SocialCircle.EnsureNextSocialPostTick(currentTick);
        internal void ScheduleNextSocialPost(int currentTick) => Parts.SocialCircle.ScheduleNextSocialPost(currentTick);
        internal void TryGenerateScheduledSocialPost(int currentTick) => Parts.SocialCircle.TryGenerateScheduledSocialPost(currentTick);
        internal void TrimSocialPosts() => Parts.SocialCircle.TrimSocialPosts();
        internal List<Faction> GetEligibleSocialFactions() => Parts.SocialCircle.GetEligibleSocialFactions();
        internal Faction ResolveMentionedFaction(string text, Faction sourceFaction) => Parts.SocialCircle.ResolveMentionedFaction(text, sourceFaction);
        internal void AddSocialSystemMessage(Faction sourceFaction, string message) => Parts.SocialCircle.AddSocialSystemMessage(sourceFaction, message);
        public void RecordScheduledSocialEvent(ScheduledSocialEventType eventType, Faction sourceFaction, Faction targetFaction, string summary, string detail, int value, string sourceKey) => Parts.SocialCircle.RecordScheduledSocialEvent(eventType, sourceFaction, targetFaction, summary, detail, value, sourceKey);
        public List<ScheduledSocialEventRecord> GetRecentScheduledSocialEvents(int daysWindow) => Parts.SocialCircle.GetRecentScheduledSocialEvents(daysWindow);
        internal void AddSocialGenerationMessage(SocialNewsSeed seed, bool success, SocialPostGenerationFailureReason failureReason = SocialPostGenerationFailureReason.None) => Parts.SocialCircle.AddSocialGenerationMessage(seed, success, failureReason);
        public static string GetSocialFailureReasonLabel(SocialPostEnqueueFailureReason reason) => DiplomacyManagerSocialCircle.GetSocialFailureReasonLabel(reason);
        public static string GetSocialFailureReasonLabel(SocialPostGenerationFailureReason reason) => DiplomacyManagerSocialCircle.GetSocialFailureReasonLabel(reason);
        internal static string GetSocialFailureReasonKey(SocialPostEnqueueFailureReason reason) => DiplomacyManagerSocialCircle.GetSocialFailureReasonKey(reason);
        internal static string GetSocialFailureReasonKey(SocialPostGenerationFailureReason reason) => DiplomacyManagerSocialCircle.GetSocialFailureReasonKey(reason);
        internal void TryProcessAiToAiInteraction(int currentTick) => Parts.SocialCircle.TryProcessAiToAiInteraction(currentTick);
        public bool TryGenerateAiToAiSocialPost(DebugGenerateReason reason, int currentTick) => Parts.SocialCircle.TryGenerateAiToAiSocialPost(reason, currentTick);
        internal static SocialPostCategory PickRandomAiToAiCategory(Faction source, Faction target) => DiplomacyManagerSocialCircle.PickRandomAiToAiCategory(source, target);
        internal static int PickRandomAiToAiSentiment(Faction source, Faction target, SocialPostCategory category) => DiplomacyManagerSocialCircle.PickRandomAiToAiSentiment(source, target, category);
        internal static string BuildAiToAiSummary(Faction source, Faction target, SocialPostCategory category, int sentiment) => DiplomacyManagerSocialCircle.BuildAiToAiSummary(source, target, category, sentiment);
        public ManualSocialPostResult TryPublishManualPlayerSocialPost(string title, string body) => Parts.SocialCircleManualPost.TryPublishManualPlayerSocialPost(title, body);
        public static string GetManualSocialPostFailureReasonLabel(ManualSocialPostFailureReason reason) => DiplomacyManagerSocialCircleManualPost.GetManualSocialPostFailureReasonLabel(reason);
        internal PublicSocialPost CreateManualPlayerSocialPost(string title, string body, int currentTick) => Parts.SocialCircleManualPost.CreateManualPlayerSocialPost(title, body, currentTick);
        internal List<Faction> SelectManualReactionFactions(string title, string body) => Parts.SocialCircleManualPost.SelectManualReactionFactions(title, body);
        internal void TriggerManualPostResponses(PublicSocialPost post, List<Faction> targetFactions) => Parts.SocialCircleManualPost.TriggerManualPostResponses(post, targetFactions);
        internal NpcDialogueTriggerContext BuildManualPostTriggerContext(Faction faction, PublicSocialPost post) => Parts.SocialCircleManualPost.BuildManualPostTriggerContext(faction, post);
        internal bool IsEligibleManualReactionFaction(Faction faction) => Parts.SocialCircleManualPost.IsEligibleManualReactionFaction(faction);
        internal float ScoreManualReactionFaction(Faction faction, string content, SocialPostCategory category, int sentiment) => Parts.SocialCircleManualPost.ScoreManualReactionFaction(faction, content, category, sentiment);
        internal static int CountMentionHits(string content, string token) => DiplomacyManagerSocialCircleManualPost.CountMentionHits(content, token);
        internal static string SanitizeManualReasonSegment(string text) => DiplomacyManagerSocialCircleManualPost.SanitizeManualReasonSegment(text);
        internal void ClearSocialTransientState() => Parts.SocialCircleNewsRequests.ClearSocialTransientState();
        public void ProcessDeferredSocialNewsSeeds(int currentTick) => Parts.SocialCircleNewsRequests.ProcessDeferredSocialNewsSeeds(currentTick);
        internal bool TryQueueNextScheduledNews(DebugGenerateReason reason, int currentTick, bool bypassSimulationToggle) => Parts.SocialCircleNewsRequests.TryQueueNextScheduledNews(reason, currentTick, bypassSimulationToggle);
        internal bool TryQueueNextScheduledNews(DebugGenerateReason reason, int currentTick, bool bypassSimulationToggle, out SocialForceGenerateFailureReason failureReason) => Parts.SocialCircleNewsRequests.TryQueueNextScheduledNews(reason, currentTick, bypassSimulationToggle, out failureReason);
        internal bool TryQueueNewsSeed(SocialNewsSeed seed, int currentTick, bool allowFailedRetry = false) => Parts.SocialCircleNewsRequests.TryQueueNewsSeed(seed, currentTick, allowFailedRetry);
        internal bool TryQueueNewsSeed(SocialNewsSeed seed, int currentTick, out string requestId, out SocialPostEnqueueFailureReason failureReason, bool allowFailedRetry = false) => Parts.SocialCircleNewsRequests.TryQueueNewsSeed(seed, currentTick, out requestId, out failureReason, allowFailedRetry);
        internal bool TryResolvePromptSnapshotOrDefer(SocialNewsSeed seed, int currentTick, bool allowFailedRetry, out DiplomacyPromptRuntimeSnapshot snapshot) => Parts.SocialCircleNewsRequests.TryResolvePromptSnapshotOrDefer(seed, currentTick, allowFailedRetry, out snapshot);
        internal void EnqueueDeferredSocialNewsSeed(SocialNewsSeed seed, int dueTick, bool allowFailedRetry) => Parts.SocialCircleNewsRequests.EnqueueDeferredSocialNewsSeed(seed, dueTick, allowFailedRetry);
        internal static string BuildDeferredSocialSeedKey(SocialNewsSeed seed) => DiplomacyManagerSocialCircleNewsRequests.BuildDeferredSocialSeedKey(seed);
        internal bool CanGenerateSocialNews() => Parts.SocialCircleNewsRequests.CanGenerateSocialNews();
        internal bool CanGenerateSocialNews(out SocialForceGenerateFailureReason failureReason) => Parts.SocialCircleNewsRequests.CanGenerateSocialNews(out failureReason);
        internal SocialNewsSeed SelectNextScheduledSeed(bool allowFailedRetry, int currentTick) => Parts.SocialCircleNewsRequests.SelectNextScheduledSeed(allowFailedRetry, currentTick);
        internal bool IsOriginBlocked(SocialNewsSeed seed, bool allowFailedRetry, int currentTick) => Parts.SocialCircleNewsRequests.IsOriginBlocked(seed, allowFailedRetry, currentTick);
        internal SocialProcessedOrigin FindProcessedOrigin(SocialNewsSeed seed) => Parts.SocialCircleNewsRequests.FindProcessedOrigin(seed);
        internal bool HasPublishedOrigin(SocialNewsSeed seed) => Parts.SocialCircleNewsRequests.HasPublishedOrigin(seed);
        internal void OnSocialNewsRequestSuccess(string requestId, string response) => Parts.SocialCircleNewsRequests.OnSocialNewsRequestSuccess(requestId, response);
        internal void OnSocialNewsRequestError(string requestId, string error) => Parts.SocialCircleNewsRequests.OnSocialNewsRequestError(requestId, error);
        internal static string BuildResponsePreview(string response, int maxLength) => DiplomacyManagerSocialCircleNewsRequests.BuildResponsePreview(response, maxLength);
        internal bool TryTakePendingSocialRequest(string requestId, out PendingSocialNewsRequest pending) => Parts.SocialCircleNewsRequests.TryTakePendingSocialRequest(requestId, out pending);
        internal void AddCompletedSocialPost(PublicSocialPost post, SocialNewsSeed seed, int currentTick) => Parts.SocialCircleNewsRequests.AddCompletedSocialPost(post, seed, currentTick);
        internal static bool ShouldSendSocialNewsLetter(PublicSocialPost post) => DiplomacyManagerSocialCircleNewsRequests.ShouldSendSocialNewsLetter(post);
        internal void MirrorSocialPostSummaryToLeaderMemories(PublicSocialPost post, int fallbackTick) => Parts.SocialCircleNewsRequests.MirrorSocialPostSummaryToLeaderMemories(post, fallbackTick);
        internal static bool ShouldMirrorSocialPostSummary(PublicSocialPost post) => DiplomacyManagerSocialCircleNewsRequests.ShouldMirrorSocialPostSummary(post);
        internal List<Faction> GetSummaryMirrorTargetFactions() => Parts.SocialCircleNewsRequests.GetSummaryMirrorTargetFactions();
        internal static string BuildSocialPostSummaryText(PublicSocialPost post) => DiplomacyManagerSocialCircleNewsRequests.BuildSocialPostSummaryText(post);
        internal static string BuildSocialPostContentHash(PublicSocialPost post, int tick) => DiplomacyManagerSocialCircleNewsRequests.BuildSocialPostContentHash(post, tick);
        internal static CrossChannelSummaryRecord CreateSocialPostSummaryRecord(PublicSocialPost post, Faction targetFaction, string summary, int tick, string contentHash) => DiplomacyManagerSocialCircleNewsRequests.CreateSocialPostSummaryRecord(post, targetFaction, summary, tick, contentHash);
        internal static List<string> BuildSocialPostSummaryFacts(PublicSocialPost post) => DiplomacyManagerSocialCircleNewsRequests.BuildSocialPostSummaryFacts(post);
        internal void TrySendSocialNewsLetter(PublicSocialPost post) => Parts.SocialCircleNewsRequests.TrySendSocialNewsLetter(post);
        internal static LetterDef ResolveSocialNewsLetterDef(PublicSocialPost post) => DiplomacyManagerSocialCircleNewsRequests.ResolveSocialNewsLetterDef(post);
        internal static SocialPostEnqueueFailureReason MapForceFailureToEnqueueFailure(SocialForceGenerateFailureReason failureReason) => DiplomacyManagerSocialCircleNewsRequests.MapForceFailureToEnqueueFailure(failureReason);
        #endregion
    
        #region Cluster forwards
        public static bool ShouldExcludeFactionFromAI(Faction faction) => DiplomacyManagerSlice1.ShouldExcludeFactionFromAI(faction);
        internal static HashSet<string> ParseFactionExclusionCsv(string csv) => DiplomacyManagerSlice1.ParseFactionExclusionCsv(csv);
        internal void InitializeAIControlledFactions() => Parts.Slice1.InitializeAIControlledFactions();
        internal void InitializeDialogueSessions() => Parts.Slice1.InitializeDialogueSessions();
        internal void InitializePresenceStates() => Parts.Slice1.InitializePresenceStates();
        internal void CleanupInvalidSessions() => Parts.Slice1.CleanupInvalidSessions();
        internal void CleanupInvalidPresenceStates() => Parts.Slice1.CleanupInvalidPresenceStates();
        internal void RebuildDialogueSessionIndex() => Parts.Slice1.RebuildDialogueSessionIndex();
        internal void RebuildPresenceStateIndex() => Parts.Slice1.RebuildPresenceStateIndex();
        public FactionDialogueSession GetOrCreateSession(Faction faction) => Parts.Slice1.GetOrCreateSession(faction);
        public FactionDialogueSession GetSession(Faction faction) => Parts.Slice1.GetSession(faction);
        public bool HandleInboundFactionMessage(Faction faction, string sender, string message, DialogueMessageType messageType, Pawn speakerPawn = null, bool markUnread = true, bool forcePresenceOnline = true) => Parts.Slice1.HandleInboundFactionMessage(faction, sender, message, messageType, speakerPawn, markUnread, forcePresenceOnline);
        internal void EnsureConversationReopenedOnInbound(FactionDialogueSession session, Faction faction) => Parts.Slice1.EnsureConversationReopenedOnInbound(session, faction);
        public FactionPresenceState GetOrCreatePresenceState(Faction faction) => Parts.Slice1.GetOrCreatePresenceState(faction);
        public FactionPresenceState GetPresenceState(Faction faction) => Parts.Slice1.GetPresenceState(faction);
        public FactionPresenceStatus GetPresenceStatus(Faction faction) => Parts.Slice1.GetPresenceStatus(faction);
        public void ForcePresenceOnlineForNpcInitiated(Faction faction) => Parts.Slice1.ForcePresenceOnlineForNpcInitiated(faction);
        public void RefreshPresenceOnDialogueOpen(Faction faction) => Parts.Slice1.RefreshPresenceOnDialogueOpen(faction);
        internal static void HandlePresenceRecoveryQueueCleanup(Faction faction, FactionPresenceStatus previousStatus, FactionPresenceStatus currentStatus) => DiplomacyManagerSlice1.HandlePresenceRecoveryQueueCleanup(faction, previousStatus, currentStatus);
        internal void EnforcePresenceForcedDurationCaps(FactionPresenceState state, int currentTick) => Parts.Slice1.EnforcePresenceForcedDurationCaps(state, currentTick);
        public void RefreshPresenceForFactions(IEnumerable<Faction> factions) => Parts.Slice1.RefreshPresenceForFactions(factions);
        public void LockPresenceCacheOnDialogueClose(Faction faction) => Parts.Slice1.LockPresenceCacheOnDialogueClose(faction);
        public void LockPresenceCacheOnDialogueClose(IEnumerable<Faction> factions) => Parts.Slice1.LockPresenceCacheOnDialogueClose(factions);
        public void ApplyPresenceAction(Faction faction, string actionType, string reason, FactionDialogueSession session) => Parts.Slice1.ApplyPresenceAction(faction, actionType, reason, session);
        public bool HasUnreadMessages(Faction faction) => Parts.Slice1.HasUnreadMessages(faction);
        public List<Faction> GetFactionsWithDialogue() => Parts.Slice1.GetFactionsWithDialogue();
        internal void ProcessPeriodicDiplomacySnapshots() => Parts.Slice2.ProcessPeriodicDiplomacySnapshots();
        internal Pawn GetLastNegotiatorForSession(FactionDialogueSession session) => Parts.Slice2.GetLastNegotiatorForSession(session);
        public void ProcessDelayedEvents() => Parts.Slice2.ProcessDelayedEvents();
        public void AddDelayedEvent(DelayedDiplomacyEvent evt) => Parts.Slice2.AddDelayedEvent(evt);
        internal void FlushPendingDelayedEvents() => Parts.Slice2.FlushPendingDelayedEvents();
        internal void DailyReset() => Parts.Slice2.DailyReset();
        internal void ProcessAIDecisions() => Parts.Slice2.ProcessAIDecisions();
        internal int GetPresenceCacheTicks() => Parts.Slice2.GetPresenceCacheTicks();
        internal void MigrateLegacyRaidCallEveryoneEvents(int currentTick) => Parts.Slice2.MigrateLegacyRaidCallEveryoneEvents(currentTick);
        internal FactionPresenceStatus EvaluateScheduledPresence(Faction faction, int currentTick, out string reason) => Parts.Slice2.EvaluateScheduledPresence(faction, currentTick, out reason);
        internal int GetCurrentHourOfDay() => Parts.Slice2.GetCurrentHourOfDay();
        internal void GetPresenceScheduleForTechLevel(TechLevel techLevel, out int startHour, out int durationHours) => Parts.Slice2.GetPresenceScheduleForTechLevel(techLevel, out startHour, out durationHours);
        internal bool IsHourWithinWindow(int hour, int startHour, int durationHours) => Parts.Slice2.IsHourWithinWindow(hour, startHour, durationHours);
        internal bool IsInNightWindow(int hour) => Parts.Slice2.IsInNightWindow(hour);
        internal float GetDeterministicRoll(Faction faction, int dayIndex, int hour) => Parts.Slice2.GetDeterministicRoll(faction, dayIndex, hour);
        internal int GetScheduleOffsetHours(Faction faction, int dayIndex) => Parts.Slice2.GetScheduleOffsetHours(faction, dayIndex);
        internal int ModHour(int hour) => Parts.Slice2.ModHour(hour);
        internal float GetOffWindowOnlineChance(TechLevel techLevel) => Parts.Slice2.GetOffWindowOnlineChance(techLevel);
        internal static string GetTempPeaceKey(Faction a, Faction b) => DiplomacyManagerSlice2.GetTempPeaceKey(a, b);
        public void ApplyTempCrossFactionPeace(Faction a, Faction b, int untilTick) => Parts.Slice2.ApplyTempCrossFactionPeace(a, b, untilTick);
        public void TryRestoreTempFactionRelations(int currentTick) => Parts.Slice2.TryRestoreTempFactionRelations(currentTick);
        #endregion
}
    internal sealed class GameComponent_DiplomacyManagerParts
    {
        internal readonly GameComponent_DiplomacyManager Owner;
        internal readonly DiplomacyManagerAlbum Album;
        internal readonly DiplomacyManagerEventQueries EventQueries;
        internal readonly DiplomacyManagerHiddenFactionVisibility HiddenFactionVisibility;
        internal readonly DiplomacyManagerSocialCircle SocialCircle;
        internal readonly DiplomacyManagerSocialCircleManualPost SocialCircleManualPost;
        internal readonly DiplomacyManagerSocialCircleNewsRequests SocialCircleNewsRequests;
        internal readonly DiplomacyManagerSlice1 Slice1;
        internal readonly DiplomacyManagerSlice2 Slice2;
        internal GameComponent_DiplomacyManagerParts(GameComponent_DiplomacyManager owner)
        {
            Owner = owner;
            Album = new DiplomacyManagerAlbum(owner);
            EventQueries = new DiplomacyManagerEventQueries(owner);
            HiddenFactionVisibility = new DiplomacyManagerHiddenFactionVisibility(owner);
            SocialCircle = new DiplomacyManagerSocialCircle(owner);
            SocialCircleManualPost = new DiplomacyManagerSocialCircleManualPost(owner);
            SocialCircleNewsRequests = new DiplomacyManagerSocialCircleNewsRequests(owner);
            Slice1 = new DiplomacyManagerSlice1(owner);
            Slice2 = new DiplomacyManagerSlice2(owner);
        }
    }


}
