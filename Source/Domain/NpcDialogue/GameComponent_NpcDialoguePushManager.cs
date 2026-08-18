using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Communication.Relations.Dialogue;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Diagnostics;
using Ustas.RimAI.Communication.Relations.WorldState;
using RimWorld;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Context;
using Ustas.RimAI.Communication.Relations.Prompting.Diplomacy;

namespace Ustas.RimAI.Communication.Relations.NpcDialogue
{
    /// <summary>/// Dependencies: AIChatServiceAsync, GameComponent_DiplomacyManager, Verse.GameComponent.
 /// Responsibility: End-to-end orchestration for NPC proactive dialogue triggers, queueing, generation and delivery.
 ///</summary>
    public class GameComponent_NpcDialoguePushManager : GameComponent
    {
        internal GameComponent_NpcDialoguePushManagerParts Parts;

        internal sealed class PendingGenerationContext
        {
            public NpcDialogueTriggerContext Context;
            public List<ChatMessageData> Messages;
            public int Attempt;
        }

        internal const int TickPerHour = 2500;
        internal const int TickPerDay = 60000;
        internal const int RegularEvaluationInterval = 36000;
        internal const int QueueProcessInterval = 600;
        internal const int IncomingDrainInterval = 120;
        internal const int ClickWindowTicks = 360;
        internal const int ClickBusyThreshold = 12;
        internal const int CausalMinDelayTicks = 250;
        internal const int CausalMaxDelayTicks = 1000;
        internal const int RecentInteractionWindowTicks = TickPerDay * 7;
        internal const int DefaultGlobalDeliveryCooldownTicks = TickPerHour * 3;
        internal const int DefaultFactionCooldownMinTicks = TickPerDay * 3;
        internal const int DefaultFactionCooldownMaxTicks = TickPerDay * 7;
        internal const int CandidateCacheMaintenanceIntervalTicks = 15000;
        internal const int CandidateSessionSyncIntervalTicks = 30000;
        internal const int MaxCandidateFactions = 20;
        internal const int SnapshotRetryDelayTicks = 250;

        public static GameComponent_NpcDialoguePushManager Instance;

        internal List<FactionNpcPushState> factionPushStates = new List<FactionNpcPushState>();
        internal Dictionary<Faction, FactionNpcPushState> factionPushStatesByFaction = new Dictionary<Faction, FactionNpcPushState>();
        internal List<QueuedNpcDialogueTrigger> queuedTriggers = new List<QueuedNpcDialogueTrigger>();

        internal readonly Queue<NpcDialogueTriggerContext> incomingTriggers = new Queue<NpcDialogueTriggerContext>();
        internal readonly Dictionary<string, PendingGenerationContext> pendingRequests = new Dictionary<string, PendingGenerationContext>();
        internal readonly HashSet<Faction> factionsWithPendingRequests = new HashSet<Faction>();
        internal readonly HashSet<Faction> factionsInQueue = new HashSet<Faction>();
        internal readonly Queue<int> clickTicks = new Queue<int>();
        internal readonly HashSet<Faction> activeCandidateFactions = new HashSet<Faction>();
        internal readonly List<Faction> _reusableCandidateResults = new List<Faction>();
        internal readonly Dictionary<Faction, int> candidateTouchTicks = new Dictionary<Faction, int>();
        internal readonly List<int> globalDeliveryTicks = new List<int>();
        internal readonly Dictionary<int, List<int>> factionDeliveryTicks = new Dictionary<int, List<int>>();
        internal int globalDeliveryOldestInWindow;
        internal int lastGlobalDeliveredTick = -DefaultGlobalDeliveryCooldownTicks;
        internal const int FactionWindowMaxMessages = 2;
        internal const int SystemPromptCacheTtlTicks = 3000;
        internal readonly Dictionary<string, (int builtTick, string prompt)> _systemPromptCache =
            new Dictionary<string, (int builtTick, string prompt)>();
        internal const int FactionWindowTicks = 60000;
        internal int lastCandidateCacheMaintenanceTick;
        internal int lastCandidateSessionSyncTick;

        public GameComponent_NpcDialoguePushManager(Game game) : base()
        {
            Parts = new GameComponent_NpcDialoguePushManagerParts(this);
            Instance = this;
        }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            Instance = this;
            incomingTriggers.Clear();
            pendingRequests.Clear();
            factionsWithPendingRequests.Clear();
            factionsInQueue.Clear();
            clickTicks.Clear();
            activeCandidateFactions.Clear();
            candidateTouchTicks.Clear();
            globalDeliveryTicks.Clear();
            globalDeliveryOldestInWindow = 0;
            lastGlobalDeliveredTick = -DefaultGlobalDeliveryCooldownTicks;
            lastCandidateCacheMaintenanceTick = 0;
            lastCandidateSessionSyncTick = 0;
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            Instance = this;
            incomingTriggers.Clear();
            pendingRequests.Clear();
            factionsWithPendingRequests.Clear();
            factionsInQueue.Clear();
            clickTicks.Clear();
            globalDeliveryTicks.Clear();
            globalDeliveryOldestInWindow = 0;
            CleanupInvalidState();
            RebuildCandidateCache();
        }

        public override void ExposeData()
        {
            base.ExposeData();

            try
            {
                Scribe_Collections.Look(ref factionPushStates, "npcPushFactionStates", LookMode.Deep);
                Scribe_Collections.Look(ref queuedTriggers, "npcPushQueuedTriggers", LookMode.Deep);
                Scribe_Values.Look(ref lastGlobalDeliveredTick, "npcPushLastGlobalDeliveredTick", -DefaultGlobalDeliveryCooldownTicks);
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAI.Relations] Error loading NpcDialogue data from save: {ex.Message}\n{ex.StackTrace}");
                factionPushStates ??= new List<FactionNpcPushState>();
                queuedTriggers ??= new List<QueuedNpcDialogueTrigger>();
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                factionPushStates ??= new List<FactionNpcPushState>();
                queuedTriggers ??= new List<QueuedNpcDialogueTrigger>();
                if (lastGlobalDeliveredTick < -DefaultGlobalDeliveryCooldownTicks)
                {
                    lastGlobalDeliveredTick = -DefaultGlobalDeliveryCooldownTicks;
                }
                CleanupInvalidState();
                RebuildAllRuntimeIndexes();
                RebuildCandidateCache();
            }
        }

        public override void GameComponentTick()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.TickManager == null)
            {
                return;
            }

            int currentTick = Find.TickManager.TicksGame;
            TrackClickSignal(currentTick);

            if (currentTick % IncomingDrainInterval == 0)
            {
                using (PerfScope.Measure("NpcPush.Drain"))
                    DrainIncomingTriggers(currentTick);
            }

            if (currentTick % QueueProcessInterval == 0)
            {
                using (PerfScope.Measure("NpcPush.QueueProcess"))
                    ProcessQueuedTriggers(currentTick);
            }

            if (currentTick % RegularEvaluationInterval == 0)
            {
                using (PerfScope.Measure("NpcPush.EvaluateRegular"))
                    EvaluateRegularTriggers(currentTick);
            }
        }

        

        

        /// <summary>/// 注册自定义触发器（用于袭击消息等场景）
        ///</summary>
        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        // Overload that uses a pre-built (potentially cached) system prompt
        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        internal bool IsFactionPending(Faction faction)
        {
            return faction != null && factionsWithPendingRequests.Contains(faction);
        }

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        #region Facade forwards
        internal void AppendManualSocialPostPrompt(List<ChatMessageData> messages, NpcDialogueTriggerContext context) => Parts.ManualSocialPost.AppendManualSocialPostPrompt(messages, context);
        internal static bool TryParseManualSocialPostReason(string reason, out string title, out string body) => NpcDialoguePushManagerManualSocialPost.TryParseManualSocialPostReason(reason, out title, out body);
        #endregion
    
        #region Cluster forwards
        public void RegisterLowQualityTradeTrigger(Faction faction, int lowQualityCount, QualityCategory worstQuality) => Parts.Slice1.RegisterLowQualityTradeTrigger(faction, lowQualityCount, worstQuality);
        public void RegisterGoodwillShiftTrigger(Faction faction, int goodwillDelta, string reason, bool likelyHostile) => Parts.Slice1.RegisterGoodwillShiftTrigger(faction, goodwillDelta, reason, likelyHostile);
        public void RegisterCustomTrigger(NpcDialogueTriggerContext context) => Parts.Slice1.RegisterCustomTrigger(context);
        internal void AccumulateGoodwillLoss(Faction faction, int goodwillDelta) => Parts.Slice1.AccumulateGoodwillLoss(faction, goodwillDelta);
        public bool DebugForceRandomProactiveDialogue() => Parts.Slice1.DebugForceRandomProactiveDialogue();
        internal void EnqueueIncoming(NpcDialogueTriggerContext context) => Parts.Slice1.EnqueueIncoming(context);
        internal void DrainIncomingTriggers(int currentTick) => Parts.Slice1.DrainIncomingTriggers(currentTick);
        internal void HandleTriggerContext(NpcDialogueTriggerContext context, int currentTick) => Parts.Slice1.HandleTriggerContext(context, currentTick);
        internal void ProcessQueuedTriggers(int currentTick) => Parts.Slice1.ProcessQueuedTriggers(currentTick);
        internal void EvaluateRegularTriggers(int currentTick) => Parts.Slice1.EvaluateRegularTriggers(currentTick);
        internal NpcDialogueTriggerContext BuildRegularTrigger(Faction faction, int currentTick) => Parts.Slice1.BuildRegularTrigger(faction, currentTick);
        internal void StartGeneration(NpcDialogueTriggerContext context) => Parts.Slice1.StartGeneration(context);
        internal IEnumerator BuildAndSendRoutine(NpcDialogueTriggerContext context, DiplomacyPromptRuntimeSnapshot runtimeSnapshot) => Parts.Slice2.BuildAndSendRoutine(context, runtimeSnapshot);
        internal void OnGenerationSuccess(string requestId, string response) => Parts.Slice2.OnGenerationSuccess(requestId, response);
        internal void OnGenerationError(string requestId, string error) => Parts.Slice2.OnGenerationError(requestId, error);
        internal void RetryGeneration(PendingGenerationContext pending) => Parts.Slice2.RetryGeneration(pending);
        internal void UpdatePendingFactionIndex(Faction faction) => Parts.Slice2.UpdatePendingFactionIndex(faction);
        internal bool TryGetPromptRuntimeSnapshotOrDefer(NpcDialogueTriggerContext context, out DiplomacyPromptRuntimeSnapshot snapshot) => Parts.Slice2.TryGetPromptRuntimeSnapshotOrDefer(context, out snapshot);
        internal void DeliverMessage(NpcDialogueTriggerContext context, string text) => Parts.Slice2.DeliverMessage(context, text);
        internal void AddMessageToSession(Faction faction, string text) => Parts.Slice2.AddMessageToSession(faction, text);
        internal void SendProactiveLetter(NpcDialogueTriggerContext context, string text) => Parts.Slice2.SendProactiveLetter(context, text);
        internal TaggedString GetLetterTitle(NpcDialogueTriggerContext context) => Parts.Slice2.GetLetterTitle(context);
        internal LetterDef GetLetterDef(NpcDialogueTriggerContext context) => Parts.Slice2.GetLetterDef(context);
        internal List<ChatMessageData> BuildGenerationMessages(NpcDialogueTriggerContext context, DiplomacyPromptRuntimeSnapshot runtimeSnapshot) => Parts.Slice2.BuildGenerationMessages(context, runtimeSnapshot);
        internal List<ChatMessageData> BuildGenerationMessagesWithPrompt(NpcDialogueTriggerContext context, DiplomacyPromptRuntimeSnapshot runtimeSnapshot, string basePrompt, List<string> sceneTags) => Parts.Slice2.BuildGenerationMessagesWithPrompt(context, runtimeSnapshot, basePrompt, sceneTags);
        internal int GetAccumulatedGoodwillLoss(Faction faction) => Parts.Slice2.GetAccumulatedGoodwillLoss(faction);
        internal List<string> BuildProactiveSceneTags(NpcDialogueCategory category) => Parts.Slice2.BuildProactiveSceneTags(category);
        internal void AppendRecentSessionContext(List<ChatMessageData> messages, Faction faction) => Parts.Slice2.AppendRecentSessionContext(messages, faction);
        internal string SanitizeModelOutput(string output) => Parts.Slice2.SanitizeModelOutput(output);
        internal void QueueTrigger(NpcDialogueTriggerContext context, int dueTick, int nowTick) => Parts.Slice3.QueueTrigger(context, dueTick, nowTick);
        internal static int GetQueueItemPriority(QueuedNpcDialogueTrigger item) => NpcDialoguePushSlice3.GetQueueItemPriority(item);
        internal void CleanupExpiredQueue(int currentTick) => Parts.Slice3.CleanupExpiredQueue(currentTick);
        public int CancelQueuedTriggersForFaction(Faction faction, string reason = "manual") => Parts.Slice3.CancelQueuedTriggersForFaction(faction, reason);
        internal bool ShouldRespectCooldown(NpcDialogueTriggerContext context, int currentTick) => Parts.Slice3.ShouldRespectCooldown(context, currentTick);
        internal int GetReinitiateCooldownRemainingTicks(Faction faction, int currentTick) => Parts.Slice3.GetReinitiateCooldownRemainingTicks(faction, currentTick);
        internal int GetGlobalNextAllowedTick(int currentTick) => Parts.Slice3.GetGlobalNextAllowedTick(currentTick);
        internal bool IsGlobalWindowLimitReached(int currentTick) => Parts.Slice3.IsGlobalWindowLimitReached(currentTick);
        internal int GetGlobalWindowNextAvailableTick(int currentTick) => Parts.Slice3.GetGlobalWindowNextAvailableTick(currentTick);
        internal bool IsFactionWindowFull(Faction faction, int currentTick) => Parts.Slice3.IsFactionWindowFull(faction, currentTick);
        internal void RecordFactionDelivery(Faction faction, int currentTick) => Parts.Slice3.RecordFactionDelivery(faction, currentTick);
        internal bool CanBypassCooldown(NpcDialogueTriggerContext context) => Parts.Slice3.CanBypassCooldown(context);
        internal bool IsBypassHardLimitReached(int currentTick) => Parts.Slice3.IsBypassHardLimitReached(currentTick);
        internal bool IsFactionUnavailable(Faction faction) => Parts.Slice3.IsFactionUnavailable(faction);
        internal bool IsValidTargetFaction(Faction faction) => Parts.Slice3.IsValidTargetFaction(faction);
        internal bool IsPlayerBusy() => Parts.Slice3.IsPlayerBusy();
        internal void TrackClickSignal(int currentTick) => Parts.Slice3.TrackClickSignal(currentTick);
        public void RegisterPlayerLeftClick() => Parts.Slice3.RegisterPlayerLeftClick();
        internal List<Faction> GetActiveCandidateFactions(int currentTick) => Parts.Slice3.GetActiveCandidateFactions(currentTick);
        internal FactionNpcPushState GetOrCreateState(Faction faction) => Parts.Slice3.GetOrCreateState(faction);
        internal void CleanupInvalidState() => Parts.Slice3.CleanupInvalidState();
        internal void RebuildAllRuntimeIndexes() => Parts.Slice3.RebuildAllRuntimeIndexes();
        internal void MaintainCandidateCache(int currentTick) => Parts.Slice3.MaintainCandidateCache(currentTick);
        internal void SyncCandidateCacheFromRecentSessions(int currentTick) => Parts.Slice4.SyncCandidateCacheFromRecentSessions(currentTick);
        internal bool IsCandidateStillActive(Faction faction, int currentTick) => Parts.Slice4.IsCandidateStillActive(faction, currentTick);
        internal void MarkFactionCandidate(Faction faction, int tick) => Parts.Slice4.MarkFactionCandidate(faction, tick);
        internal void RebuildCandidateCache() => Parts.Slice4.RebuildCandidateCache();
        internal int GetGlobalDeliveryCooldownTicks() => Parts.Slice4.GetGlobalDeliveryCooldownTicks();
        internal int GetFactionCooldownMinTicks() => Parts.Slice4.GetFactionCooldownMinTicks();
        internal int GetFactionCooldownMaxTicks() => Parts.Slice4.GetFactionCooldownMaxTicks();
        internal void LogThrottleDebug(string message) => Parts.Slice4.LogThrottleDebug(message);
        internal bool TryDeliverFallbackMessage(NpcDialogueTriggerContext context) => Parts.Slice4.TryDeliverFallbackMessage(context);
        internal float GetRegularTriggerChance(NpcPushFrequencyMode mode) => Parts.Slice4.GetRegularTriggerChance(mode);
        #endregion
}
    internal sealed class GameComponent_NpcDialoguePushManagerParts
    {
        internal readonly GameComponent_NpcDialoguePushManager Owner;
        internal readonly NpcDialoguePushManagerManualSocialPost ManualSocialPost;
        internal readonly NpcDialoguePushSlice1 Slice1;
        internal readonly NpcDialoguePushSlice2 Slice2;
        internal readonly NpcDialoguePushSlice3 Slice3;
        internal readonly NpcDialoguePushSlice4 Slice4;
        internal GameComponent_NpcDialoguePushManagerParts(GameComponent_NpcDialoguePushManager owner)
        {
            Owner = owner;
            ManualSocialPost = new NpcDialoguePushManagerManualSocialPost(owner);
            Slice1 = new NpcDialoguePushSlice1(owner);
            Slice2 = new NpcDialoguePushSlice2(owner);
            Slice3 = new NpcDialoguePushSlice3(owner);
            Slice4 = new NpcDialoguePushSlice4(owner);
        }
    }


}
