using System;
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.NpcDialogue;
using Ustas.RimAI.Communication.Relations.Diagnostics;
using Ustas.RimAI.Communication.Relations.Guards;
using Ustas.RimAI.Communication.Relations.WorldState;
using RimWorld;
using UnityEngine;
using Verse;

using System.Collections;

namespace Ustas.RimAI.Communication.Relations.PawnRpgPush
{
    public class GameComponent_PawnRpgDialoguePushManager : GameComponent
    {
        internal GameComponent_PawnRpgDialoguePushManagerParts Parts;

        internal sealed class PendingGenerationContext
        {
            public PawnRpgTriggerContext Context;
            public Pawn NpcPawn;
            public Pawn PlayerPawn;
            public List<ChatMessageData> Messages;
            public int Attempt;
        }

        internal const int TickPerHour = 2500;
        internal const int TickPerDay = 60000;
        internal const int RegularEvaluationInterval = 36000;
        internal const int QueueProcessInterval = 600;
        internal const int IncomingDrainInterval = 120;
        internal const int ThreatScanInterval = 600;
        internal const int ClickWindowTicks = 360;
        internal const int ClickBusyThreshold = 12;
        internal const int CausalMinDelayTicks = 250;
        internal const int CausalMaxDelayTicks = 1000;
        internal const int NpcEvaluateCooldownTicks = 150000;
        internal const int ColonyDeliveryCooldownTicks = TickPerHour * 3;
        internal const int ColonistPairCooldownTicks = TickPerHour;
        internal const int BlockedRetryTicks = 300;
        internal const int MissingProtagonistLogIntervalTicks = 6000;
        internal const float LowMoodThreshold = 0.30f;
        internal const int QuestDeadlineWindowTicks = TickPerDay;
        internal const int QuestTriggerRepeatTicks = 15000;
        internal const int MessageDedupWindowTicks = 150000;
        internal const int RpgWindowMaxMessages = 1;
        internal const int RpgWindowTicks = 60000;
        internal const int HomeEventCooldownTicks = 150000;
        internal const int EventDedupWindowTicks = 75000;

        public static GameComponent_PawnRpgDialoguePushManager Instance;

        internal List<PawnRpgNpcPushState> npcPushStates = new List<PawnRpgNpcPushState>();
        internal Dictionary<Pawn, PawnRpgNpcPushState> _npcStateByPawn;
        internal List<PawnRpgThreatState> threatStates = new List<PawnRpgThreatState>();
        internal List<QueuedPawnRpgTrigger> queuedTriggers = new List<QueuedPawnRpgTrigger>();
        internal List<PawnRpgProtagonistEntry> proactiveProtagonists = new List<PawnRpgProtagonistEntry>();

        internal readonly Queue<PawnRpgTriggerContext> incomingTriggers = new Queue<PawnRpgTriggerContext>();
        internal readonly Dictionary<string, PendingGenerationContext> pendingRequests = new Dictionary<string, PendingGenerationContext>();
        internal readonly HashSet<Faction> factionsWithPendingRequests = new HashSet<Faction>();
        internal readonly Queue<int> clickTicks = new Queue<int>();
        internal readonly Dictionary<string, int> recentQuestTriggerTicks = new Dictionary<string, int>();
        internal Dictionary<string, int> recentMessageHashes = new Dictionary<string, int>();
        internal readonly List<int> rpgDeliveryTicks = new List<int>();
        internal Dictionary<string, int> recentEventDeliveries = new Dictionary<string, int>();
        internal int lastHomeEventTriggerTick = -1;
        internal int lastColonyDeliveredTick = -ColonyDeliveryCooldownTicks;
        internal int lastColonistPairDeliveredTick = -ColonyDeliveryCooldownTicks;
        internal bool _colonistPairHadThreat;
        internal int lastMissingProtagonistLogTick = -MissingProtagonistLogIntervalTicks;

        // Per-tick cache to avoid repeated ResolveConfiguredProtagonists() allocations
        internal List<Pawn> _cachedProtagonists;
        internal int _cachedProtagonistsTick = -1;

        // Per-tick cache to avoid repeated GetFactionNpcCandidates() scans
        internal Dictionary<Faction, List<Pawn>> _cachedFactionNpcs;
        internal int _cachedFactionNpcsTick = -1;

        // System prompt cache keyed by pawn-pair + scene tags, TTL-limited
        internal const int SystemPromptCacheTtlTicks = 3000;
        internal readonly Dictionary<string, (int builtTick, string prompt)> _systemPromptCache =
            new Dictionary<string, (int builtTick, string prompt)>();

        public GameComponent_PawnRpgDialoguePushManager(Game game) : base()
        {
            Parts = new GameComponent_PawnRpgDialoguePushManagerParts(this);
            Instance = this;
        }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            Instance = this;
            ClearTransientState();
            AutoSelectDefaultProtagonist();
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            Instance = this;
            ClearTransientState();
            CleanupInvalidState();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            try
            {
                Scribe_Collections.Look(ref npcPushStates, "pawnRpgNpcPushStates", LookMode.Deep);
                Scribe_Collections.Look(ref threatStates, "pawnRpgThreatStates", LookMode.Deep);
                Scribe_Collections.Look(ref queuedTriggers, "pawnRpgQueuedTriggers", LookMode.Deep);
                Scribe_Collections.Look(ref proactiveProtagonists, "pawnRpgProactiveProtagonists", LookMode.Deep);
                Scribe_Values.Look(ref lastColonyDeliveredTick, "pawnRpgLastColonyDeliveredTick", -ColonyDeliveryCooldownTicks);
                Scribe_Values.Look(ref lastColonistPairDeliveredTick, "pawnRpgLastColonistPairDeliveredTick", -ColonyDeliveryCooldownTicks);
                Scribe_Values.Look(ref _colonistPairHadThreat, "pawnRpgColonistPairHadThreat", false);
                Scribe_Values.Look(ref lastHomeEventTriggerTick, "lastHomeEventTriggerTick", -1);
                Scribe_Collections.Look(ref recentEventDeliveries, "recentEventDeliveries", LookMode.Value, LookMode.Value);
                if (Scribe.mode == LoadSaveMode.Saving)
                {
                    CleanupExpiredMessageHashes(Find.TickManager?.TicksGame ?? 0);
                }
                Scribe_Collections.Look(ref recentMessageHashes, "recentMessageHashes", LookMode.Value, LookMode.Value);
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAI.Relations] Error loading PawnRpg data from save: {ex.Message}\n{ex.StackTrace}");
                npcPushStates ??= new List<PawnRpgNpcPushState>();
                threatStates ??= new List<PawnRpgThreatState>();
                queuedTriggers ??= new List<QueuedPawnRpgTrigger>();
                proactiveProtagonists ??= new List<PawnRpgProtagonistEntry>();
                recentMessageHashes ??= new Dictionary<string, int>();
                recentEventDeliveries ??= new Dictionary<string, int>();
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                npcPushStates ??= new List<PawnRpgNpcPushState>();
                threatStates ??= new List<PawnRpgThreatState>();
                queuedTriggers ??= new List<QueuedPawnRpgTrigger>();
                proactiveProtagonists ??= new List<PawnRpgProtagonistEntry>();
                recentMessageHashes ??= new Dictionary<string, int>();
                recentEventDeliveries ??= new Dictionary<string, int>();
                _cachedProtagonists = null;
                _npcStateByPawn = npcPushStates
                    .Where(s => s?.pawn != null)
                    .GroupBy(s => s.pawn)
                    .ToDictionary(g => g.Key, g => g.First());
                CleanupInvalidState();
                AutoSelectDefaultProtagonist();
            }
        }

        public override void GameComponentTick()
        {
            _cachedProtagonists = null;
            _cachedFactionNpcs = null;

            if (Current.ProgramState != ProgramState.Playing || Find.TickManager == null)
            {
                return;
            }

            int currentTick = Find.TickManager.TicksGame;
            TrackClickSignal(currentTick);

            if (!IsFeatureEnabled())
            {
                return;
            }

            // First-tick fallback: auto-select protagonist if list is still empty
            if (proactiveProtagonists == null || proactiveProtagonists.Count == 0)
            {
                AutoSelectDefaultProtagonist();
            }

            if (currentTick % IncomingDrainInterval == 0)
            {
                using (PerfScope.Measure("RpgPush.Drain"))
                    DrainIncomingTriggers(currentTick);
            }

            if (currentTick % QueueProcessInterval == 0)
            {
                using (PerfScope.Measure("RpgPush.QueueProcess"))
                    ProcessQueuedTriggers(currentTick);
            }

            if (currentTick % ThreatScanInterval == 0)
            {
                using (PerfScope.Measure("RpgPush.EvaluateThreat"))
                    EvaluateThreatTriggers(currentTick);
            }

            if (currentTick % RegularEvaluationInterval == 0)
            {
                using (PerfScope.Measure("RpgPush.EvaluateRegular"))
                    EvaluateRegularTriggers(currentTick);
            }
        }

        

        

        

        

        

        public List<Pawn> GetRpgProactiveProtagonists()
        {
            return ResolveConfiguredProtagonists();
        }

        public bool ContainsRpgProactiveProtagonist(Pawn pawn)
        {
            return ResolveConfiguredProtagonists().Contains(pawn);
        }

        

        

        

        

        

        /// <summary>
        /// Auto-select the colonist with the highest total skills as default protagonist.
        /// Called on PostLoadInit when protagonist list is empty (backward compatibility).
        /// </summary>
        

        

        

        

        

        

        internal void RecordRpgDelivery(int currentTick)
        {
            rpgDeliveryTicks.Add(currentTick);
        }

        

        

        

        

        

        

        

        

        

        

        

        

        

        internal bool CanBypassGlobalCooldown(PawnRpgTriggerContext context)
        {
            return context != null && context.Category == NpcDialogueCategory.WarningThreat;
        }

        

        

        

        

        internal bool IsFactionPending(Faction faction)
        {
            return faction != null && factionsWithPendingRequests.Contains(faction);
        }

        

        

        

        

        

        

        

        

        #region Facade forwards
        internal List<Pawn> GetFactionNpcCandidates(Faction faction) => Parts.Candidates.GetFactionNpcCandidates(faction);
        internal IReadOnlyCollection<Faction> GetActiveCandidateFactionsOnPlayerMaps(int currentTick) => Parts.Candidates.GetActiveCandidateFactionsOnPlayerMaps(currentTick);
        internal bool IsEligibleNpcPawn(Pawn pawn) => Parts.Candidates.IsEligibleNpcPawn(pawn);
        internal bool TryResolvePairForFaction(Faction faction, int currentTick, bool bypassAvailability, bool bypassCooldown, bool bypassRelation, out Pawn npcPawn, out Pawn playerPawn) => Parts.Candidates.TryResolvePairForFaction(faction, currentTick, bypassAvailability, bypassCooldown, bypassRelation, out npcPawn, out playerPawn);
        internal bool TrySelectPlayerPawn(Pawn npcPawn, bool bypassAvailability, bool bypassRelation, out Pawn playerPawn) => Parts.Candidates.TrySelectPlayerPawn(npcPawn, bypassAvailability, bypassRelation, out playerPawn);
        internal List<Pawn> GetPlayerDialogueTargets(Map map) => Parts.Candidates.GetPlayerDialogueTargets(map);
        internal bool IsEligiblePlayerPawn(Pawn pawn) => Parts.Candidates.IsEligiblePlayerPawn(pawn);
        internal bool HasQualifiedPlayerRelation(Pawn npcPawn) => Parts.Candidates.HasQualifiedPlayerRelation(npcPawn);
        internal bool HasIntimateRelation(Pawn npcPawn, Pawn playerPawn) => Parts.Candidates.HasIntimateRelation(npcPawn, playerPawn);
        internal bool HasDirectRelation(Pawn npcPawn, Pawn playerPawn, PawnRelationDef relationDef) => Parts.Candidates.HasDirectRelation(npcPawn, playerPawn, relationDef);
        internal int GetOpinion(Pawn npcPawn, Pawn playerPawn) => Parts.Candidates.GetOpinion(npcPawn, playerPawn);
        internal int GetFactionNpcReadyTick(Faction faction, int currentTick) => Parts.Candidates.GetFactionNpcReadyTick(faction, currentTick);
        internal bool IsNpcOnCooldown(Pawn pawn, int currentTick) => Parts.Candidates.IsNpcOnCooldown(pawn, currentTick);
        internal int GetNpcReadyTick(Pawn pawn) => Parts.Candidates.GetNpcReadyTick(pawn);
        internal PawnRpgNpcPushState GetOrCreateNpcState(Pawn pawn) => Parts.Candidates.GetOrCreateNpcState(pawn);
        internal bool IsPlayerBusy() => Parts.Candidates.IsPlayerBusy();
        internal void TrackClickSignal(int currentTick) => Parts.Candidates.TrackClickSignal(currentTick);
        internal bool IsPawnUnavailable(Pawn pawn) => Parts.Candidates.IsPawnUnavailable(pawn);
        internal bool IsPawnWorking(Pawn pawn) => Parts.Candidates.IsPawnWorking(pawn);
        internal bool TryGetMoodPercent(Pawn pawn, out float mood) => Parts.Candidates.TryGetMoodPercent(pawn, out mood);
        internal static bool IsColonistPairContext(PawnRpgTriggerContext context) => PawnRpgDialoguePushManagerCandidates.IsColonistPairContext(context);
        internal bool IsColonistPairDialogueEnabled() => Parts.Candidates.IsColonistPairDialogueEnabled();
        internal float GetColonistPairTriggerChance(NpcPushFrequencyMode mode) => Parts.Candidates.GetColonistPairTriggerChance(mode);
        internal void EvaluateColonistPairAmbientTriggers(int currentTick, float chance) => Parts.Candidates.EvaluateColonistPairAmbientTriggers(currentTick, chance);
        internal void EvaluateColonistPairLowMoodTriggers(int currentTick) => Parts.Candidates.EvaluateColonistPairLowMoodTriggers(currentTick);
        internal void EvaluateColonistPairThreatTriggers(int currentTick, bool hasHive, bool hasHostiles) => Parts.Candidates.EvaluateColonistPairThreatTriggers(currentTick, hasHive, hasHostiles);
        internal void EvaluateHomeEventTriggers(int currentTick) => Parts.Candidates.EvaluateHomeEventTriggers(currentTick);
        internal bool TryResolveColonistPair(int currentTick, out Pawn initiator, out Pawn receiver, bool bypassAvailability = false) => Parts.Candidates.TryResolveColonistPair(currentTick, out initiator, out receiver, bypassAvailability);
        internal bool TryResolveColonistPairForTarget(Pawn target, out Pawn partner) => Parts.Candidates.TryResolveColonistPairForTarget(target, out partner);
        internal void StartGeneration(PawnRpgTriggerContext context, Pawn npcPawn, Pawn playerPawn) => Parts.Generation.StartGeneration(context, npcPawn, playerPawn);
        internal IEnumerator BuildAndSendRoutine(PawnRpgTriggerContext context, Pawn npcPawn, Pawn playerPawn) => Parts.Generation.BuildAndSendRoutine(context, npcPawn, playerPawn);
        internal void OnGenerationSuccess(string requestId, string response) => Parts.Generation.OnGenerationSuccess(requestId, response);
        internal void OnGenerationError(string requestId, string error) => Parts.Generation.OnGenerationError(requestId, error);
        internal void RetryGeneration(PendingGenerationContext pending) => Parts.Generation.RetryGeneration(pending);
        internal void DeliverMessage(PawnRpgTriggerContext context, Pawn npcPawn, Pawn playerPawn, string text) => Parts.Generation.DeliverMessage(context, npcPawn, playerPawn, text);
        internal TaggedString GetLetterTitle(PawnRpgTriggerContext context, Pawn npcPawn, Pawn playerPawn) => Parts.Generation.GetLetterTitle(context, npcPawn, playerPawn);
        internal LetterDef GetLetterDef(PawnRpgTriggerContext context) => Parts.Generation.GetLetterDef(context);
        internal List<ChatMessageData> BuildGenerationMessages(PawnRpgTriggerContext context, Pawn npcPawn, Pawn playerPawn) => Parts.Generation.BuildGenerationMessages(context, npcPawn, playerPawn);
        internal List<string> BuildProactiveSceneTags(NpcDialogueCategory category) => Parts.Generation.BuildProactiveSceneTags(category);
        internal void AppendRecentRpgContext(List<ChatMessageData> messages, Pawn npcPawn, Pawn playerPawn) => Parts.Generation.AppendRecentRpgContext(messages, npcPawn, playerPawn);
        internal string BuildReasonText(PawnRpgTriggerContext context) => Parts.Generation.BuildReasonText(context);
        internal string SanitizeModelOutput(string output) => Parts.Generation.SanitizeModelOutput(output);
        #endregion
    
        #region Cluster forwards
        public void RegisterTradeCompletedTrigger(Faction faction, int soldCount, int boughtCount) => Parts.Slice1.RegisterTradeCompletedTrigger(faction, soldCount, boughtCount);
        public void RegisterGoodwillShiftTrigger(Faction faction, int goodwillDelta, string reason, bool likelyHostile) => Parts.Slice1.RegisterGoodwillShiftTrigger(faction, goodwillDelta, reason, likelyHostile);
        public void RegisterThreatStateTrigger(Faction faction, bool hasHive, bool hasHostiles) => Parts.Slice1.RegisterThreatStateTrigger(faction, hasHive, hasHostiles);
        public void RegisterPlayerLeftClick() => Parts.Slice1.RegisterPlayerLeftClick();
        public bool DebugForcePawnRpgProactiveDialogue() => Parts.Slice1.DebugForcePawnRpgProactiveDialogue();
        public bool TryAddRpgProactiveProtagonist(Pawn pawn) => Parts.Slice1.TryAddRpgProactiveProtagonist(pawn);
        public bool RemoveRpgProactiveProtagonist(Pawn pawn) => Parts.Slice1.RemoveRpgProactiveProtagonist(pawn);
        public void ClearRpgProactiveProtagonists() => Parts.Slice1.ClearRpgProactiveProtagonists();
        public int GetConfiguredProtagonistCount() => Parts.Slice1.GetConfiguredProtagonistCount();
        public int GetRpgProactiveProtagonistCap() => Parts.Slice1.GetRpgProactiveProtagonistCap();
        internal void AutoSelectDefaultProtagonist() => Parts.Slice1.AutoSelectDefaultProtagonist();
        internal static Pawn FindBestSkillColonist() => PawnRpgPushSlice1.FindBestSkillColonist();
        public void SetRpgProactiveProtagonistCap(int value) => Parts.Slice1.SetRpgProactiveProtagonistCap(value);
        public List<Pawn> GetEligibleRpgProactiveTargetsOnMap(Map map) => Parts.Slice1.GetEligibleRpgProactiveTargetsOnMap(map);
        internal void ClearTransientState() => Parts.Slice1.ClearTransientState();
        internal bool IsRpgDeliveryWindowFull(int currentTick) => Parts.Slice1.IsRpgDeliveryWindowFull(currentTick);
        internal void CleanupExpiredMessageHashes(int currentTick) => Parts.Slice1.CleanupExpiredMessageHashes(currentTick);
        internal static string ComputeContentHash(string text) => PawnRpgPushSlice1.ComputeContentHash(text);
        internal void EnqueueIncoming(PawnRpgTriggerContext context) => Parts.Slice1.EnqueueIncoming(context);
        internal void DrainIncomingTriggers(int currentTick) => Parts.Slice1.DrainIncomingTriggers(currentTick);
        internal void HandleTriggerContext(PawnRpgTriggerContext context, int currentTick) => Parts.Slice1.HandleTriggerContext(context, currentTick);
        internal void ProcessQueuedTriggers(int currentTick) => Parts.Slice2.ProcessQueuedTriggers(currentTick);
        internal void EvaluateRegularTriggers(int currentTick) => Parts.Slice2.EvaluateRegularTriggers(currentTick);
        internal void EvaluateThreatTriggers(int currentTick) => Parts.Slice2.EvaluateThreatTriggers(currentTick);
        internal bool TryStartGenerationForContext(PawnRpgTriggerContext context, int currentTick) => Parts.Slice2.TryStartGenerationForContext(context, currentTick);
        internal bool TryCreateLowMoodContext(Faction faction, int currentTick, out PawnRpgTriggerContext context) => Parts.Slice2.TryCreateLowMoodContext(faction, currentTick, out context);
        internal bool TryCreateQuestDeadlineContext(Faction faction, int currentTick, out PawnRpgTriggerContext context) => Parts.Slice2.TryCreateQuestDeadlineContext(faction, currentTick, out context);
        internal void CleanupQuestTriggerCache(int currentTick) => Parts.Slice2.CleanupQuestTriggerCache(currentTick);
        internal int GetNextAllowedTickForContext(PawnRpgTriggerContext context, int currentTick) => Parts.Slice2.GetNextAllowedTickForContext(context, currentTick);
        internal void QueueTrigger(PawnRpgTriggerContext context, int dueTick, int nowTick) => Parts.Slice2.QueueTrigger(context, dueTick, nowTick);
        internal void CleanupExpiredQueue(int currentTick) => Parts.Slice2.CleanupExpiredQueue(currentTick);
        internal bool IsFeatureEnabled() => Parts.Slice2.IsFeatureEnabled();
        internal bool IsValidTargetFaction(Faction faction) => Parts.Slice2.IsValidTargetFaction(faction);
        internal void CleanupInvalidState() => Parts.Slice2.CleanupInvalidState();
        internal bool HasConfiguredProtagonists() => Parts.Slice2.HasConfiguredProtagonists();
        internal List<Pawn> ResolveConfiguredProtagonists() => Parts.Slice2.ResolveConfiguredProtagonists();
        internal bool CanConfigureAsProtagonist(Pawn pawn) => Parts.Slice2.CanConfigureAsProtagonist(pawn);
        internal static bool IsSamePawn(PawnRpgProtagonistEntry entry, Pawn pawn) => PawnRpgPushSlice2.IsSamePawn(entry, pawn);
        internal void LogMissingProtagonists(int currentTick) => Parts.Slice2.LogMissingProtagonists(currentTick);
        internal PawnRpgThreatState GetOrCreateThreatState(Faction faction) => Parts.Slice2.GetOrCreateThreatState(faction);
        internal float GetRegularTriggerChance(NpcPushFrequencyMode mode) => Parts.Slice2.GetRegularTriggerChance(mode);
        #endregion
}
    internal sealed class GameComponent_PawnRpgDialoguePushManagerParts
    {
        internal readonly GameComponent_PawnRpgDialoguePushManager Owner;
        internal readonly PawnRpgDialoguePushManagerCandidates Candidates;
        internal readonly PawnRpgDialoguePushManagerGeneration Generation;
        internal readonly PawnRpgPushSlice1 Slice1;
        internal readonly PawnRpgPushSlice2 Slice2;
        internal GameComponent_PawnRpgDialoguePushManagerParts(GameComponent_PawnRpgDialoguePushManager owner)
        {
            Owner = owner;
            Candidates = new PawnRpgDialoguePushManagerCandidates(owner);
            Generation = new PawnRpgDialoguePushManagerGeneration(owner);
            Slice1 = new PawnRpgPushSlice1(owner);
            Slice2 = new PawnRpgPushSlice2(owner);
        }
    }


}
