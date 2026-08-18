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

namespace Ustas.RimAI.Communication.Relations.DiplomacySystem
{
        internal abstract class GameComponent_DiplomacyManagerCollaborator
    {
        internal readonly GameComponent_DiplomacyManager Owner;

        protected GameComponent_DiplomacyManagerCollaborator(GameComponent_DiplomacyManager owner)
        {
            Owner = owner;
        }
        protected GameComponent_DiplomacyManagerParts Parts => Owner.Parts;


        protected HashSet<Faction> aiControlledFactions
        {
            get => Owner.aiControlledFactions;
            set => Owner.aiControlledFactions = value;
        }
        protected List<FactionDialogueSession> dialogueSessions
        {
            get => Owner.dialogueSessions;
            set => Owner.dialogueSessions = value;
        }
        protected Dictionary<Faction, FactionDialogueSession> dialogueSessionsByFaction
        {
            get => Owner.dialogueSessionsByFaction;
            set => Owner.dialogueSessionsByFaction = value;
        }
        protected List<FactionPresenceState> presenceStates
        {
            get => Owner.presenceStates;
            set => Owner.presenceStates = value;
        }
        protected Dictionary<Faction, FactionPresenceState> presenceStatesByFaction
        {
            get => Owner.presenceStatesByFaction;
            set => Owner.presenceStatesByFaction = value;
        }
        protected List<DelayedDiplomacyEvent> delayedEvents
        {
            get => Owner.delayedEvents;
            set => Owner.delayedEvents = value;
        }
        protected int lastNegotiatorThingId
        {
            get => Owner.lastNegotiatorThingId;
            set => Owner.lastNegotiatorThingId = value;
        }
        protected const int ForcedOfflineDurationHours = 1;
        protected const int ForcedDoNotDisturbDurationHours = 2;
        protected List<DelayedDiplomacyEvent> delayedEventsPendingAdd => Owner.delayedEventsPendingAdd;
        protected bool isProcessingDelayedEvents
        {
            get => Owner.isProcessingDelayedEvents;
            set => Owner.isProcessingDelayedEvents = value;
        }
        protected int lastProcessedDelayedEventsTick
        {
            get => Owner.lastProcessedDelayedEventsTick;
            set => Owner.lastProcessedDelayedEventsTick = value;
        }
        protected TempFactionRelationState tempFactionRelations
        {
            get => Owner.tempFactionRelations;
            set => Owner.tempFactionRelations = value;
        }
        protected int _lastAiToAiGenerationTick
        {
            get => Owner._lastAiToAiGenerationTick;
            set => Owner._lastAiToAiGenerationTick = value;
        }
        protected const int AiToAiGenerationIntervalTicks = 120000;
        protected int lastDailyResetTick
        {
            get => Owner.lastDailyResetTick;
            set => Owner.lastDailyResetTick = value;
        }
        protected int lastPeriodicSnapshotTick
        {
            get => Owner.lastPeriodicSnapshotTick;
            set => Owner.lastPeriodicSnapshotTick = value;
        }
        protected const int PeriodicSnapshotIntervalTicks = 1500;
        protected Dictionary<Faction, int> presenceEvalCacheKey => Owner.presenceEvalCacheKey;
        protected Dictionary<Faction, FactionPresenceStatus> presenceEvalCacheResult => Owner.presenceEvalCacheResult;
        protected SocialCircleState socialCircleState
        {
            get => Owner.socialCircleState;
            set => Owner.socialCircleState = value;
        }
        protected List<AlbumImageEntry> albumEntries
        {
            get => Owner.albumEntries;
            set => Owner.albumEntries = value;
        }
        protected HashSet<Faction> manuallyVisibleHiddenFactions
        {
            get => Owner.manuallyVisibleHiddenFactions;
            set => Owner.manuallyVisibleHiddenFactions = value;
        }
        protected bool socialPostsCacheDirty
        {
            get => Owner.Parts.SocialCircle.socialPostsCacheDirty;
            set => Owner.Parts.SocialCircle.socialPostsCacheDirty = value;
        }
        protected int socialPostListVersion
        {
            get => Owner.Parts.SocialCircle.socialPostListVersion;
            set => Owner.Parts.SocialCircle.socialPostListVersion = value;
        }
    }

}
