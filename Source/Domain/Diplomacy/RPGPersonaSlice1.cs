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

using PersonaPronouns = Ustas.RimAI.Communication.Relations.DiplomacySystem.RPGManagerPersonaBootstrap.PersonaPronouns;
using PendingPersonaGenerationContext = Ustas.RimAI.Communication.Relations.DiplomacySystem.RPGManagerPersonaBootstrap.PendingPersonaGenerationContext;

namespace Ustas.RimAI.Communication.Relations.DiplomacySystem
{
    internal sealed class RPGPersonaSlice1 : RPGManagerPersonaBootstrapCollaborator
    {
        internal RPGPersonaSlice1(RPGManagerPersonaBootstrap owner) : base(owner)
        {
        }

internal void ExposeData_NpcPersonaBootstrap()
        {
            Scribe_Values.Look(ref Owner.npcPersonaBootstrapCompleted, "npcPersonaBootstrapCompleted", false);
            Scribe_Values.Look(ref Owner.npcPersonaBootstrapVersion, "npcPersonaBootstrapVersion", 0);
        }

internal void MarkNpcPersonaBootstrapAsNewGame()
        {
            npcPersonaBootstrapCompleted = true;
            npcPersonaBootstrapVersion = CurrentNpcPersonaBootstrapVersion;
            Owner.ResetNpcPersonaBootstrapRuntimeState();
        }

internal void ScheduleNpcPersonaBootstrapOnLoad()
        {
            if (!Owner.ShouldRunNpcPersonaBootstrap())
            {
                Owner.ResetNpcPersonaBootstrapRuntimeState();
                return;
            }

            npcPersonaBootstrapQueued = false;
            nextPersonaBootstrapTick = Find.TickManager?.TicksGame ?? 0;
            nextPersonaRuntimeScanTick = nextPersonaBootstrapTick;
        }

internal void OnPostLoadInit_NpcPersonaBootstrap()
        {
            if (!Owner.ShouldRunNpcPersonaBootstrap())
            {
                Owner.ResetNpcPersonaBootstrapRuntimeState();
                return;
            }

            npcPersonaBootstrapQueued = false;
            nextPersonaBootstrapTick = Find.TickManager?.TicksGame ?? 0;
            nextPersonaRuntimeScanTick = nextPersonaBootstrapTick;
        }

internal void ProcessNpcPersonaBootstrapTick()
        {
            if (npcPersonaBootstrapCompleted || Current.ProgramState != ProgramState.Playing || Find.TickManager == null)
            {
                return;
            }

            int currentTick = Find.TickManager.TicksGame;
            if (currentTick < nextPersonaBootstrapTick)
            {
                return;
            }

            if (npcPersonaPendingRequests.Count > 0)
            {
                return;
            }

            if (!RPGManagerPersonaBootstrap.IsRimTalkLoadedForPersonaBlock())
            {
                Owner.CompleteNpcPersonaBootstrap();
                return;
            }

            if (!npcPersonaBootstrapQueued)
            {
                Owner.InitializeNpcPersonaBootstrapQueue();
            }

            if (npcPersonaBootstrapCompleted)
            {
                return;
            }

            if (Owner.TryApplyRimTalkPersonaFromBootstrapQueue())
            {
                nextPersonaBootstrapTick = currentTick + PersonaBootstrapTickInterval;
                return;
            }

            Owner.CompleteNpcPersonaBootstrap();
        }

internal void ProcessNpcPersonaRuntimeTick()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.TickManager == null)
                return;
            if (npcPersonaPendingRequests.Count > 0 || npcPersonaRuntimeScanDisabledNoRimTalk)
                return;

            int currentTick = Find.TickManager.TicksGame;

            // Process ongoing multi-frame scan: one map per tick to avoid peaks.
            if (personaScanInProgress)
            {
                Owner.ProcessPersonaScanOneFrame(currentTick);
                return;
            }

            if (currentTick < nextPersonaRuntimeScanTick)
                return;

            if (!RPGManagerPersonaBootstrap.IsRimTalkLoadedForPersonaBlock())
            {
                npcPersonaRuntimeScanDisabledNoRimTalk = true;
                return;
            }

            nextPersonaRuntimeScanTick = currentTick + PersonaRuntimeScanIntervalTicks;

            // Kick off multi-frame scan instead of blocking single-frame collection.
            var maps = Find.Maps;
            if (maps == null || maps.Count == 0)
            {
                cachedNpcPersonaTargets = new List<Pawn>(0);
                npcPersonaTargetsCacheTick = currentTick;
                return;
            }

            personaScanInProgress = true;
            personaScanMapIndex = 0;
            personaScanAccumulatedTargets = new List<Pawn>();
            personaScanSeenIds = new HashSet<int>();
            Owner.ProcessPersonaScanOneFrame(currentTick);
        }

internal void ProcessPersonaScanOneFrame(int currentTick)
        {
            var maps = Find.Maps;
            if (maps == null || personaScanMapIndex >= maps.Count)
            {
                Owner.FinishPersonaScan(currentTick);
                return;
            }

            // Scan exactly one map this tick.
            Map map = maps[personaScanMapIndex];
            personaScanMapIndex++;
            if (map?.mapPawns?.AllPawnsSpawned == null)
                return;

            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                RPGManagerPersonaBootstrap.AppendUniqueNpcTarget(personaScanAccumulatedTargets, personaScanSeenIds, pawn);
            }

            // If this was the last map, finish and trigger persona application.
            if (personaScanMapIndex >= maps.Count)
            {
                // Also include faction leaders (cheap, single pass).
                foreach (Faction faction in Find.FactionManager?.AllFactionsVisible ?? System.Linq.Enumerable.Empty<Faction>())
                {
                    RPGManagerPersonaBootstrap.AppendUniqueNpcTarget(personaScanAccumulatedTargets, personaScanSeenIds, faction?.leader);
                }
                Owner.FinishPersonaScan(currentTick);
            }
        }

internal void FinishPersonaScan(int currentTick)
        {
            cachedNpcPersonaTargets = personaScanAccumulatedTargets;
            npcPersonaTargetsCacheTick = currentTick;
            personaScanInProgress = false;
            personaScanAccumulatedTargets = null;
            personaScanSeenIds = null;

            Owner.TryApplyRimTalkPersonaFromRuntimeScan();
        }

internal void InitializeNpcPersonaBootstrapQueue()
        {
            Owner.ResetNpcPersonaBootstrapRuntimeState();
            npcPersonaBootstrapQueued = true;

            List<Pawn> targets = Owner.CollectNpcPersonaBootstrapTargets();
            foreach (Pawn pawn in targets)
            {
                if (!Owner.HasPersonaPrompt(pawn))
                {
                    npcPersonaBootstrapTargets.Enqueue(pawn);
                }
            }

            if (npcPersonaBootstrapTargets.Count == 0)
            {
                Owner.CompleteNpcPersonaBootstrap();
                return;
            }

            Log.Message($"[RimAI.Relations] NPC persona bootstrap queued {npcPersonaBootstrapTargets.Count} existing NPC pawn(s).");
        }

internal List<Pawn> CollectNpcPersonaBootstrapTargets()
        {
            var result = new List<Pawn>();
            var ids = new HashSet<int>();

            foreach (Map map in Find.Maps ?? Enumerable.Empty<Map>())
            {
                if (map?.mapPawns?.AllPawnsSpawned == null)
                {
                    continue;
                }

                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                {
                    RPGManagerPersonaBootstrap.AppendUniqueNpcTarget(result, ids, pawn);
                }
            }

            foreach (Faction faction in Find.FactionManager?.AllFactionsVisible ?? Enumerable.Empty<Faction>())
            {
                RPGManagerPersonaBootstrap.AppendUniqueNpcTarget(result, ids, faction?.leader);
            }

            return result;
        }

internal static void AppendUniqueNpcTarget(List<Pawn> target, HashSet<int> ids, Pawn pawn)
        {
            if (target == null || ids == null || pawn == null)
            {
                return;
            }

            if (pawn.thingIDNumber <= 0 || !ids.Add(pawn.thingIDNumber))
            {
                return;
            }

            target.Add(pawn);
        }

internal static bool IsEligibleNpcPersonaTarget(Pawn pawn)
        {
            return pawn != null &&
                   PawnDialogueRoutingPolicy.IsRpgDialogueEligibleRace(pawn) &&
                   !pawn.Dead &&
                   !pawn.Destroyed;
        }

internal bool TryGetNextBootstrapPawn(out Pawn pawn)
        {
            pawn = null;
            while (npcPersonaBootstrapTargets.Count > 0)
            {
                Pawn candidate = npcPersonaBootstrapTargets.Dequeue();
                if (!RPGManagerPersonaBootstrap.IsEligibleNpcPersonaTarget(candidate) || Owner.HasPersonaPrompt(candidate))
                {
                    continue;
                }

                pawn = candidate;
                return true;
            }

            return false;
        }

internal bool TryApplyRimTalkPersonaFromRuntimeScan()
        {
            var targets = cachedNpcPersonaTargets;
            if (targets == null || targets.Count == 0)
                return false;

            string template = RPGManagerPersonaBootstrap.ResolveRimTalkPersonaCopyTemplateOrDefaultCached();
            bool anySynced = false;
            for (int i = 0; i < targets.Count; i++)
            {
                Pawn candidate = targets[i];
                if (!RPGManagerPersonaBootstrap.CanCopyPawnPersonaFromRimTalk(candidate) ||
                    Owner.IsPawnPersonaGenerationPending(candidate))
                {
                    continue;
                }

                if (Owner.TrySyncPawnPersonaFromRimTalk(candidate, template))
                {
                    anySynced = true;
                }
            }

            return anySynced;
        }

internal bool TryFindMissingPersonaPawn(out Pawn pawn)
        {
            var targets = cachedNpcPersonaTargets;
            if (targets != null)
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    Pawn candidate = targets[i];
                    if (RPGManagerPersonaBootstrap.IsEligibleNpcPersonaTarget(candidate) &&
                        !RPGManagerPersonaBootstrap.CanCopyPawnPersonaFromRimTalk(candidate) &&
                        !Owner.HasPersonaPrompt(candidate) &&
                        !Owner.IsPawnPersonaGenerationPending(candidate))
                    {
                        pawn = candidate;
                        return true;
                    }
                }
            }
            pawn = null;
            return false;
        }

internal bool IsPawnPersonaGenerationPending(Pawn pawn)
        {
            return pawn != null
                && pawn.thingIDNumber > 0
                && npcPersonaPendingRequests.Count > 0
                && npcPersonaPendingThingIds.Contains(pawn.thingIDNumber);
        }

internal static bool CanStartPersonaGeneration()
        {
            AIChatServiceAsync service = AIChatServiceAsync.Instance;
            return service != null && service.IsConfigured();
        }

internal static bool ShouldBlockAiPersonaGeneration()
        {
            if (!RPGManagerPersonaBootstrap.IsRimTalkLoadedForPersonaBlock())
            {
                return false;
            }

            if (!rimTalkPersonaAiBlockLogged)
            {
                rimTalkPersonaAiBlockLogged = true;
                Log.Message("[RimAI.Relations] RimTalk detected; AI persona generation blocked at runtime.");
            }

            return true;
        }

internal void StartNpcPersonaGeneration(Pawn pawn, int attempt)
        {
            _ = attempt;
            if (!RPGManagerPersonaBootstrap.IsEligibleNpcPersonaTarget(pawn) || Owner.IsPawnPersonaGenerationPending(pawn))
            {
                return;
            }

            if (RPGManagerPersonaBootstrap.ShouldBlockAiPersonaGeneration())
            {
                return;
            }

            if (Owner.TrySyncPawnPersonaFromRimTalk(pawn))
            {
                return;
            }

            if (RPGManagerPersonaBootstrap.CanCopyPawnPersonaFromRimTalk(pawn))
            {
                return;
            }
        }

internal bool TryApplyRimTalkPersonaFromBootstrapQueue()
        {
            int count = npcPersonaBootstrapTargets.Count;
            bool copied = false;
            string template = RPGManagerPersonaBootstrap.ResolveRimTalkPersonaCopyTemplateOrDefaultCached();
            for (int i = 0; i < count; i++)
            {
                Pawn candidate = npcPersonaBootstrapTargets.Dequeue();
                if (!RPGManagerPersonaBootstrap.IsEligibleNpcPersonaTarget(candidate) || Owner.HasPersonaPrompt(candidate))
                {
                    continue;
                }

                if (!copied && Owner.TryCopyPawnPersonaFromRimTalk(candidate, template))
                {
                    copied = true;
                    continue;
                }

                npcPersonaBootstrapTargets.Enqueue(candidate);
            }

            return copied;
        }

internal bool TryCopyPawnPersonaFromRimTalk(Pawn pawn, string template)
        {
            if (!RPGManagerPersonaBootstrap.IsEligibleRimTalkPersonaCopyTarget(pawn) || Owner.HasPersonaPrompt(pawn))
            {
                return false;
            }

            if (!RPGManagerPersonaBootstrap.TryGetRimTalkSourcePersona(pawn, out string sourcePersona))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(template))
            {
                DebugLogger.Debug("RimTalk persona copy skipped: template is empty.");
                return false;
            }

            string rendered = Owner.RenderPersonaCopyTemplateOrThrow(pawn, template, sourcePersona);
            string normalized = RPGManagerPersonaBootstrap.NormalizeCopiedPersonaPrompt(rendered);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw RPGManagerPersonaBootstrap.BuildPersonaCopyRenderException(
                    "prompt_templates.rpg_persona_copy",
                    "rpg",
                    $"Persona copy template returned empty normalized text for pawn '{pawn?.LabelShortCap ?? "unknown"}'.");
            }

            SetPawnPersonaPrompt(pawn, normalized);
            RPGManagerPersonaBootstrap.TryEnsureRpgPersonaTokenCoverageSafe();
            DebugLogger.Debug($"RimTalk persona copied for pawn '{pawn?.LabelShortCap}'.");
            return true;
        }
    }
}
