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
        internal abstract class GameComponent_NpcDialoguePushManagerCollaborator
    {
        internal readonly GameComponent_NpcDialoguePushManager Owner;

        protected GameComponent_NpcDialoguePushManagerCollaborator(GameComponent_NpcDialoguePushManager owner)
        {
            Owner = owner;
        }
        protected GameComponent_NpcDialoguePushManagerParts Parts => Owner.Parts;


        protected const int TickPerHour = 2500;
        protected const int TickPerDay = 60000;
        protected const int RegularEvaluationInterval = 36000;
        protected const int QueueProcessInterval = 600;
        protected const int IncomingDrainInterval = 120;
        protected const int ClickWindowTicks = 360;
        protected const int ClickBusyThreshold = 12;
        protected const int CausalMinDelayTicks = 250;
        protected const int CausalMaxDelayTicks = 1000;
        protected const int RecentInteractionWindowTicks = TickPerDay * 7;
        protected const int DefaultGlobalDeliveryCooldownTicks = TickPerHour * 3;
        protected const int DefaultFactionCooldownMinTicks = TickPerDay * 3;
        protected const int DefaultFactionCooldownMaxTicks = TickPerDay * 7;
        protected const int CandidateCacheMaintenanceIntervalTicks = 15000;
        protected const int CandidateSessionSyncIntervalTicks = 30000;
        protected const int MaxCandidateFactions = 20;
        protected const int SnapshotRetryDelayTicks = 250;
        protected List<FactionNpcPushState> factionPushStates
        {
            get => Owner.factionPushStates;
            set => Owner.factionPushStates = value;
        }
        protected Dictionary<Faction, FactionNpcPushState> factionPushStatesByFaction
        {
            get => Owner.factionPushStatesByFaction;
            set => Owner.factionPushStatesByFaction = value;
        }
        protected List<QueuedNpcDialogueTrigger> queuedTriggers
        {
            get => Owner.queuedTriggers;
            set => Owner.queuedTriggers = value;
        }
        protected Queue<NpcDialogueTriggerContext> incomingTriggers => Owner.incomingTriggers;
        protected Dictionary<string, GameComponent_NpcDialoguePushManager.PendingGenerationContext> pendingRequests => Owner.pendingRequests;
        protected HashSet<Faction> factionsWithPendingRequests => Owner.factionsWithPendingRequests;
        protected HashSet<Faction> factionsInQueue => Owner.factionsInQueue;
        protected Queue<int> clickTicks => Owner.clickTicks;
        protected HashSet<Faction> activeCandidateFactions => Owner.activeCandidateFactions;
        protected List<Faction> _reusableCandidateResults => Owner._reusableCandidateResults;
        protected Dictionary<Faction, int> candidateTouchTicks => Owner.candidateTouchTicks;
        protected List<int> globalDeliveryTicks => Owner.globalDeliveryTicks;
        protected Dictionary<int, List<int>> factionDeliveryTicks => Owner.factionDeliveryTicks;
        protected int globalDeliveryOldestInWindow
        {
            get => Owner.globalDeliveryOldestInWindow;
            set => Owner.globalDeliveryOldestInWindow = value;
        }
        protected int lastGlobalDeliveredTick
        {
            get => Owner.lastGlobalDeliveredTick;
            set => Owner.lastGlobalDeliveredTick = value;
        }
        protected const int FactionWindowMaxMessages = 2;
        protected const int SystemPromptCacheTtlTicks = 3000;
        protected const int FactionWindowTicks = 60000;
        protected int lastCandidateCacheMaintenanceTick
        {
            get => Owner.lastCandidateCacheMaintenanceTick;
            set => Owner.lastCandidateCacheMaintenanceTick = value;
        }
        protected int lastCandidateSessionSyncTick
        {
            get => Owner.lastCandidateSessionSyncTick;
            set => Owner.lastCandidateSessionSyncTick = value;
        }
        protected Dictionary<string, (int builtTick, string prompt)> _systemPromptCache => Owner._systemPromptCache;
    }

}
