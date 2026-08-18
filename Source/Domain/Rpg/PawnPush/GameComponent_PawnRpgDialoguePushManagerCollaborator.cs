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

namespace Ustas.RimAI.Communication.Relations.PawnRpgPush
{
        internal abstract class GameComponent_PawnRpgDialoguePushManagerCollaborator
    {
        internal readonly GameComponent_PawnRpgDialoguePushManager Owner;

        protected GameComponent_PawnRpgDialoguePushManagerCollaborator(GameComponent_PawnRpgDialoguePushManager owner)
        {
            Owner = owner;
        }
        protected GameComponent_PawnRpgDialoguePushManagerParts Parts => Owner.Parts;


        protected const int TickPerHour = 2500;
        protected const int TickPerDay = 60000;
        protected const int RegularEvaluationInterval = 36000;
        protected const int QueueProcessInterval = 600;
        protected const int IncomingDrainInterval = 120;
        protected const int ThreatScanInterval = 600;
        protected const int ClickWindowTicks = 360;
        protected const int ClickBusyThreshold = 12;
        protected const int CausalMinDelayTicks = 250;
        protected const int CausalMaxDelayTicks = 1000;
        protected const int NpcEvaluateCooldownTicks = 150000;
        protected const int ColonyDeliveryCooldownTicks = TickPerHour * 3;
        protected const int ColonistPairCooldownTicks = TickPerHour;
        protected const int BlockedRetryTicks = 300;
        protected const int MissingProtagonistLogIntervalTicks = 6000;
        protected const float LowMoodThreshold = 0.30f;
        protected const int QuestDeadlineWindowTicks = TickPerDay;
        protected const int QuestTriggerRepeatTicks = 15000;
        protected const int MessageDedupWindowTicks = 150000;
        protected const int RpgWindowMaxMessages = 1;
        protected const int RpgWindowTicks = 60000;
        protected const int HomeEventCooldownTicks = 150000;
        protected const int EventDedupWindowTicks = 75000;
        protected List<PawnRpgNpcPushState> npcPushStates
        {
            get => Owner.npcPushStates;
            set => Owner.npcPushStates = value;
        }
        protected Dictionary<Pawn, PawnRpgNpcPushState> _npcStateByPawn
        {
            get => Owner._npcStateByPawn;
            set => Owner._npcStateByPawn = value;
        }
        protected List<PawnRpgThreatState> threatStates
        {
            get => Owner.threatStates;
            set => Owner.threatStates = value;
        }
        protected List<QueuedPawnRpgTrigger> queuedTriggers
        {
            get => Owner.queuedTriggers;
            set => Owner.queuedTriggers = value;
        }
        protected List<PawnRpgProtagonistEntry> proactiveProtagonists
        {
            get => Owner.proactiveProtagonists;
            set => Owner.proactiveProtagonists = value;
        }
        protected Queue<PawnRpgTriggerContext> incomingTriggers => Owner.incomingTriggers;
        protected Dictionary<string, GameComponent_PawnRpgDialoguePushManager.PendingGenerationContext> pendingRequests => Owner.pendingRequests;
        protected HashSet<Faction> factionsWithPendingRequests => Owner.factionsWithPendingRequests;
        protected Queue<int> clickTicks => Owner.clickTicks;
        protected Dictionary<string, int> recentQuestTriggerTicks => Owner.recentQuestTriggerTicks;
        protected Dictionary<string, int> recentMessageHashes
        {
            get => Owner.recentMessageHashes;
            set => Owner.recentMessageHashes = value;
        }
        protected List<int> rpgDeliveryTicks => Owner.rpgDeliveryTicks;
        protected Dictionary<string, int> recentEventDeliveries
        {
            get => Owner.recentEventDeliveries;
            set => Owner.recentEventDeliveries = value;
        }
        protected int lastHomeEventTriggerTick
        {
            get => Owner.lastHomeEventTriggerTick;
            set => Owner.lastHomeEventTriggerTick = value;
        }
        protected int lastColonyDeliveredTick
        {
            get => Owner.lastColonyDeliveredTick;
            set => Owner.lastColonyDeliveredTick = value;
        }
        protected int lastColonistPairDeliveredTick
        {
            get => Owner.lastColonistPairDeliveredTick;
            set => Owner.lastColonistPairDeliveredTick = value;
        }
        protected bool _colonistPairHadThreat
        {
            get => Owner._colonistPairHadThreat;
            set => Owner._colonistPairHadThreat = value;
        }
        protected int lastMissingProtagonistLogTick
        {
            get => Owner.lastMissingProtagonistLogTick;
            set => Owner.lastMissingProtagonistLogTick = value;
        }
        protected List<Pawn> _cachedProtagonists
        {
            get => Owner._cachedProtagonists;
            set => Owner._cachedProtagonists = value;
        }
        protected int _cachedProtagonistsTick
        {
            get => Owner._cachedProtagonistsTick;
            set => Owner._cachedProtagonistsTick = value;
        }
        protected Dictionary<Faction, List<Pawn>> _cachedFactionNpcs
        {
            get => Owner._cachedFactionNpcs;
            set => Owner._cachedFactionNpcs = value;
        }
        protected int _cachedFactionNpcsTick
        {
            get => Owner._cachedFactionNpcsTick;
            set => Owner._cachedFactionNpcsTick = value;
        }
        protected const int SystemPromptCacheTtlTicks = 3000;
        protected Dictionary<string, (int builtTick, string prompt)> _systemPromptCache => Owner._systemPromptCache;
    }

}
