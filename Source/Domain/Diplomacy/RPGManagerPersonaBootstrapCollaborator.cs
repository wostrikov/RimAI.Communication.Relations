using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Dialogue;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.Diagnostics;
using RimWorld;
using Verse;
using Ustas.RimAI.Communication.Relations.Context;

namespace Ustas.RimAI.Communication.Relations.DiplomacySystem
{
    internal abstract class RPGManagerPersonaBootstrapCollaborator : GameComponent_RPGManagerCollaborator
    {
        internal new readonly RPGManagerPersonaBootstrap Owner;

        protected RPGManagerPersonaBootstrapCollaborator(RPGManagerPersonaBootstrap owner) : base(owner.Owner)
        {
            Owner = owner;
        }
        protected RPGManagerPersonaBootstrapParts Parts => Owner.Parts;


        protected const int PersonaBootstrapTickInterval = 150;
        protected const int PersonaRuntimeScanIntervalTicks = 9000;
        protected const int PersonaPromptMaxLength = 1200;
        protected const int CurrentNpcPersonaBootstrapVersion = 3;
        protected const string RimTalkPersonaServiceTypeName = "Ustas.RimAI.Communication.Data.PersonaService";
        protected const string RimTalkDependencyToken = "rimtalk";
        protected static Regex WhitespaceRegex => RPGManagerPersonaBootstrap.WhitespaceRegex;
        protected static Regex PersonaSentenceStartRegex => RPGManagerPersonaBootstrap.PersonaSentenceStartRegex;
        protected static Regex PersonaTemplateRegex => RPGManagerPersonaBootstrap.PersonaTemplateRegex;
        protected bool npcPersonaBootstrapCompleted
        {
            get => Owner.npcPersonaBootstrapCompleted;
            set => Owner.npcPersonaBootstrapCompleted = value;
        }
        protected int npcPersonaBootstrapVersion
        {
            get => Owner.npcPersonaBootstrapVersion;
            set => Owner.npcPersonaBootstrapVersion = value;
        }
        protected bool npcPersonaBootstrapQueued
        {
            get => Owner.npcPersonaBootstrapQueued;
            set => Owner.npcPersonaBootstrapQueued = value;
        }
        protected Queue<Pawn> npcPersonaBootstrapTargets => Owner.npcPersonaBootstrapTargets;
        protected Dictionary<string, RPGManagerPersonaBootstrap.PendingPersonaGenerationContext> npcPersonaPendingRequests => Owner.npcPersonaPendingRequests;
        protected HashSet<int> npcPersonaPendingThingIds => Owner.npcPersonaPendingThingIds;
        protected List<Pawn> cachedNpcPersonaTargets
        {
            get => Owner.cachedNpcPersonaTargets;
            set => Owner.cachedNpcPersonaTargets = value;
        }
        protected int npcPersonaTargetsCacheTick
        {
            get => Owner.npcPersonaTargetsCacheTick;
            set => Owner.npcPersonaTargetsCacheTick = value;
        }
        protected bool personaScanInProgress
        {
            get => Owner.personaScanInProgress;
            set => Owner.personaScanInProgress = value;
        }
        protected int personaScanMapIndex
        {
            get => Owner.personaScanMapIndex;
            set => Owner.personaScanMapIndex = value;
        }
        protected List<Pawn> personaScanAccumulatedTargets
        {
            get => Owner.personaScanAccumulatedTargets;
            set => Owner.personaScanAccumulatedTargets = value;
        }
        protected HashSet<int> personaScanSeenIds
        {
            get => Owner.personaScanSeenIds;
            set => Owner.personaScanSeenIds = value;
        }
        protected int nextPersonaBootstrapTick
        {
            get => Owner.nextPersonaBootstrapTick;
            set => Owner.nextPersonaBootstrapTick = value;
        }
        protected int nextPersonaRuntimeScanTick
        {
            get => Owner.nextPersonaRuntimeScanTick;
            set => Owner.nextPersonaRuntimeScanTick = value;
        }
        protected bool npcPersonaRuntimeScanDisabledNoRimTalk
        {
            get => Owner.npcPersonaRuntimeScanDisabledNoRimTalk;
            set => Owner.npcPersonaRuntimeScanDisabledNoRimTalk = value;
        }
        protected static object RimTalkPersonaResolverLock => RPGManagerPersonaBootstrap.RimTalkPersonaResolverLock;
        protected static bool rimTalkPersonaResolverInitialized
        {
            get => RPGManagerPersonaBootstrap.rimTalkPersonaResolverInitialized;
            set => RPGManagerPersonaBootstrap.rimTalkPersonaResolverInitialized = value;
        }
        protected static MethodInfo rimTalkGetPersonalityMethod
        {
            get => RPGManagerPersonaBootstrap.rimTalkGetPersonalityMethod;
            set => RPGManagerPersonaBootstrap.rimTalkGetPersonalityMethod = value;
        }
        protected static bool rimTalkPersonaResolverLoggedUnavailable
        {
            get => RPGManagerPersonaBootstrap.rimTalkPersonaResolverLoggedUnavailable;
            set => RPGManagerPersonaBootstrap.rimTalkPersonaResolverLoggedUnavailable = value;
        }
        protected static bool rimTalkPersonaAiBlockLogged
        {
            get => RPGManagerPersonaBootstrap.rimTalkPersonaAiBlockLogged;
            set => RPGManagerPersonaBootstrap.rimTalkPersonaAiBlockLogged = value;
        }
        protected string GetPawnPersonaPrompt(Pawn pawn) => Owner.Owner.GetPawnPersonaPrompt(pawn);
        protected void SetPawnPersonaPrompt(Pawn pawn, string prompt) => Owner.Owner.SetPawnPersonaPrompt(pawn, prompt);
    }
}
