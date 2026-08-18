using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Dialogue;
using Ustas.RimAI.Communication.Relations.NpcDialogue;
using Ustas.RimAI.Communication.Relations.WorldState;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Ustas.RimAI.Communication.Relations.PawnRpgPush
{
    /// <summary>/// Dependencies: RimWorld pawn/map/job systems, RimChat settings, Verse utility APIs.
 /// Responsibility: Candidate discovery, relationship/availability gating, and busy-state checks for PawnRPG proactive flow.
 ///</summary>
        internal sealed class PawnRpgDialoguePushManagerCandidates : GameComponent_PawnRpgDialoguePushManagerCollaborator
    {
        internal PawnRpgDialoguePushManagerCandidates(GameComponent_PawnRpgDialoguePushManager owner) : base(owner)
        {
        }


        internal static bool _loggedNoEligibleReceivers;
        internal static bool _loggedNoValidPair;

        internal List<Pawn> GetFactionNpcCandidates(Faction faction)
        {
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            if (_cachedFactionNpcs != null && _cachedFactionNpcsTick == currentTick)
            {
                if (_cachedFactionNpcs.TryGetValue(faction, out List<Pawn> cached))
                    return cached;
            }
            else
            {
                _cachedFactionNpcs = new Dictionary<Faction, List<Pawn>>();
                _cachedFactionNpcsTick = currentTick;
            }

            var list = new List<Pawn>();
            if (Owner.IsValidTargetFaction(faction) && Find.Maps != null)
            {
                foreach (Map map in Find.Maps)
                {
                    if (map?.mapPawns?.AllPawnsSpawned == null || !map.IsPlayerHome) continue;
                    foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                    {
                        if (Owner.IsEligibleNpcPawn(pawn) && pawn.Faction == faction)
                            list.Add(pawn);
                    }
                }
            }
            _cachedFactionNpcs[faction] = list;
            return list;
        }

        internal IReadOnlyCollection<Faction> GetActiveCandidateFactionsOnPlayerMaps(int currentTick)
        {
            PlayerGameStateCache.Instance.EnsureFresh(currentTick);
            return PlayerGameStateCache.Instance.ActiveFactionsOnPlayerMaps;
        }

        internal bool IsEligibleNpcPawn(Pawn pawn)
        {
            return pawn != null &&
                   pawn.Spawned &&
                   PawnDialogueRoutingPolicy.IsRpgDialogueEligibleRace(pawn) &&
                   pawn.Faction != null &&
                   !pawn.Dead &&
                   !pawn.Destroyed;
        }

        internal bool TryResolvePairForFaction(Faction faction, int currentTick, bool bypassAvailability, bool bypassCooldown, bool bypassRelation, out Pawn npcPawn, out Pawn playerPawn)
        {
            npcPawn = null;
            playerPawn = null;
            if (!Owner.IsValidTargetFaction(faction))
            {
                return false;
            }

            foreach (Pawn candidate in Owner.GetFactionNpcCandidates(faction))
            {
                if (!bypassCooldown && Owner.IsNpcOnCooldown(candidate, currentTick))
                {
                    continue;
                }

                if (!Owner.TrySelectPlayerPawn(candidate, bypassAvailability, bypassRelation, out Pawn receiver))
                {
                    continue;
                }

                if (!bypassAvailability && Owner.IsPawnUnavailable(candidate))
                {
                    continue;
                }

                npcPawn = candidate;
                playerPawn = receiver;
                return true;
            }

            return false;
        }

        internal bool TrySelectPlayerPawn(Pawn npcPawn, bool bypassAvailability, bool bypassRelation, out Pawn playerPawn)
        {
            playerPawn = null;
            if (npcPawn?.Map?.mapPawns?.AllPawnsSpawned == null)
            {
                return false;
            }

            Pawn best = null;
            int bestScore = int.MinValue;
            foreach (Pawn colonist in Owner.GetPlayerDialogueTargets(npcPawn.Map))
            {
                if (colonist == npcPawn || !Owner.IsEligiblePlayerPawn(colonist))
                {
                    continue;
                }

                bool intimate = Owner.HasIntimateRelation(npcPawn, colonist);
                int opinion = Owner.GetOpinion(npcPawn, colonist);
                if (!bypassRelation && !intimate && opinion < 35)
                {
                    continue;
                }

                if (!bypassAvailability && Owner.IsPawnUnavailable(colonist))
                {
                    continue;
                }

                int score = intimate ? 1000 + opinion : opinion;
                if (score > bestScore)
                {
                    best = colonist;
                    bestScore = score;
                }
            }

            if (best == null)
            {
                return false;
            }

            playerPawn = best;
            return true;
        }

        internal List<Pawn> GetPlayerDialogueTargets(Map map)
        {
            return Owner.GetEligibleRpgProactiveTargetsOnMap(map);
        }

        internal bool IsEligiblePlayerPawn(Pawn pawn)
        {
            return pawn != null &&
                   pawn.Spawned &&
                   pawn.Faction == Faction.OfPlayer &&
                   !pawn.Dead &&
                   !pawn.Destroyed;
        }

        internal bool HasQualifiedPlayerRelation(Pawn npcPawn)
        {
            if (npcPawn?.Map?.mapPawns?.AllPawnsSpawned == null)
            {
                return false;
            }

            foreach (Pawn colonist in Owner.GetPlayerDialogueTargets(npcPawn.Map))
            {
                if (colonist == npcPawn || !Owner.IsEligiblePlayerPawn(colonist))
                {
                    continue;
                }

                if (Owner.HasIntimateRelation(npcPawn, colonist) || Owner.GetOpinion(npcPawn, colonist) >= 35)
                {
                    return true;
                }
            }

            return false;
        }

        internal bool HasIntimateRelation(Pawn npcPawn, Pawn playerPawn)
        {
            if (npcPawn?.relations == null || playerPawn == null)
            {
                return false;
            }

            return Owner.HasDirectRelation(npcPawn, playerPawn, PawnRelationDefOf.Spouse) ||
                   Owner.HasDirectRelation(npcPawn, playerPawn, PawnRelationDefOf.Fiance) ||
                   Owner.HasDirectRelation(npcPawn, playerPawn, PawnRelationDefOf.Lover);
        }

        internal bool HasDirectRelation(Pawn npcPawn, Pawn playerPawn, PawnRelationDef relationDef)
        {
            return relationDef != null && npcPawn.relations.DirectRelationExists(relationDef, playerPawn);
        }

        internal int GetOpinion(Pawn npcPawn, Pawn playerPawn)
        {
            return npcPawn?.relations == null || playerPawn == null ? 0 : npcPawn.relations.OpinionOf(playerPawn);
        }

        internal int GetFactionNpcReadyTick(Faction faction, int currentTick)
        {
            int earliest = int.MaxValue;
            bool foundNpc = false;
            foreach (Pawn npc in Owner.GetFactionNpcCandidates(faction))
            {
                foundNpc = true;
                int readyTick = Owner.GetNpcReadyTick(npc);
                if (readyTick < earliest)
                {
                    earliest = readyTick;
                }
            }

            if (!foundNpc)
            {
                return currentTick + BlockedRetryTicks;
            }

            return Mathf.Max(currentTick, earliest);
        }

        internal bool IsNpcOnCooldown(Pawn pawn, int currentTick)
        {
            return Owner.GetNpcReadyTick(pawn) > currentTick;
        }

        internal int GetNpcReadyTick(Pawn pawn)
        {
            if (pawn == null)
            {
                return int.MaxValue;
            }

            PawnRpgNpcPushState state = Owner.GetOrCreateNpcState(pawn);
            if (state.lastNpcEvaluateTick <= 0)
            {
                return 0;
            }

            return state.lastNpcEvaluateTick + NpcEvaluateCooldownTicks;
        }

        internal PawnRpgNpcPushState GetOrCreateNpcState(Pawn pawn)
        {
            if (_npcStateByPawn == null)
                _npcStateByPawn = new Dictionary<Pawn, PawnRpgNpcPushState>();

            if (_npcStateByPawn.TryGetValue(pawn, out PawnRpgNpcPushState state))
                return state;

            state = new PawnRpgNpcPushState { pawn = pawn };
            npcPushStates.Add(state);
            _npcStateByPawn[pawn] = state;
            return state;
        }

        internal bool IsPlayerBusy()
        {
            var settings = RelationsMod.Instance?.InstanceSettings;
            if (settings == null)
            {
                return false;
            }

            int currentTick = Find.TickManager?.TicksGame ?? 0;
            PlayerGameStateCache.Instance.EnsureFresh(currentTick);

            if (settings.EnableBusyByDrafted && PlayerGameStateCache.Instance.HasDrafted)
            {
                return true;
            }

            if (settings.EnableBusyByHostiles && PlayerGameStateCache.Instance.HasHostiles)
            {
                return true;
            }

            return settings.EnableBusyByClickRate && clickTicks.Count >= ClickBusyThreshold;
        }

        internal void TrackClickSignal(int currentTick)
        {
            var settings = RelationsMod.Instance?.InstanceSettings;
            if (settings?.EnableBusyByClickRate != true)
            {
                clickTicks.Clear();
                return;
            }

            while (clickTicks.Count > 0 && currentTick - clickTicks.Peek() > ClickWindowTicks)
            {
                clickTicks.Dequeue();
            }
        }

        internal bool IsPawnUnavailable(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.Destroyed || !pawn.Spawned || pawn.Downed)
            {
                return true;
            }

            if (!RestUtility.Awake(pawn))
            {
                return true;
            }

            return false;
        }

        internal bool IsPawnWorking(Pawn pawn)
        {
            Job currentJob = pawn?.CurJob;
            JobDef jobDef = currentJob?.def;
            if (jobDef == null)
            {
                return false;
            }

            if (jobDef == JobDefOf.LayDown || jobDef == JobDefOf.Wait || jobDef == JobDefOf.Wait_Combat)
            {
                return false;
            }

            return jobDef.joyKind == null;
        }

        internal bool TryGetMoodPercent(Pawn pawn, out float mood)
        {
            mood = 1f;
            if (pawn?.needs?.mood == null)
            {
                return false;
            }

            mood = pawn.needs.mood.CurLevelPercentage;
            return true;
        }

        internal static bool IsColonistPairContext(PawnRpgTriggerContext context)
        {
            return context?.Faction == Faction.OfPlayer;
        }

        internal bool IsColonistPairDialogueEnabled()
        {
            RelationsSettings settings = RelationsMod.Instance?.InstanceSettings;
            return settings != null
                && settings.EnableColonistToColonistDialogue
                && settings.EnablePawnRpgInitiatedDialogue
                && settings.EnableRPGDialogue;
        }

        internal float GetColonistPairTriggerChance(NpcPushFrequencyMode mode)
        {
            return mode switch
            {
                NpcPushFrequencyMode.High => 0.15f,
                NpcPushFrequencyMode.Medium => 0.10f,
                _ => 0.06f
            };
        }

        internal void EvaluateColonistPairAmbientTriggers(int currentTick, float chance)
        {
            if (!Owner.IsColonistPairDialogueEnabled())
            {
                return;
            }

            if (PlayerGameStateCache.Instance.EligibleColonistCount < 2)
            {
                return;
            }

            RelationsSettings settings = RelationsMod.Instance?.InstanceSettings;
            float colonistChance = Owner.GetColonistPairTriggerChance(settings?.ColonistPairFrequencyMode ?? NpcPushFrequencyMode.Low);
            if (Rand.Value > colonistChance)
            {
                return;
            }

            if (currentTick - lastColonistPairDeliveredTick < ColonistPairCooldownTicks)
            {
                return;
            }

            if (!Owner.TryResolveColonistPair(currentTick, out Pawn initiator, out Pawn receiver))
            {
                return;
            }

            var context = new PawnRpgTriggerContext
            {
                Faction = Faction.OfPlayer,
                TriggerType = NpcDialogueTriggerType.Ambient,
                Category = NpcDialogueCategory.Social,
                SourceTag = "colonist_ambient",
                Reason = "colonist_social",
                Severity = 1,
                CreatedTick = currentTick
            };
            Owner.HandleTriggerContext(context, currentTick);
        }

        internal void EvaluateColonistPairLowMoodTriggers(int currentTick)
        {
            if (!Owner.IsColonistPairDialogueEnabled())
            {
                return;
            }

            if (PlayerGameStateCache.Instance.EligibleColonistCount < 2)
            {
                return;
            }

            if (currentTick - lastColonistPairDeliveredTick < ColonistPairCooldownTicks)
            {
                return;
            }

            Pawn worstMoodColonist = null;
            float worstMood = 1f;
            foreach (Pawn colonist in Owner.ResolveConfiguredProtagonists())
            {
                if (!Owner.IsEligiblePlayerPawn(colonist) || Owner.IsPawnUnavailable(colonist))
                {
                    continue;
                }

                if (!Owner.TryGetMoodPercent(colonist, out float mood) || mood > LowMoodThreshold)
                {
                    continue;
                }

                if (!Owner.TryResolveColonistPairForTarget(colonist, out _))
                {
                    continue;
                }

                if (mood < worstMood)
                {
                    worstMood = mood;
                    worstMoodColonist = colonist;
                }
            }

            if (worstMoodColonist == null)
            {
                return;
            }

            var context = new PawnRpgTriggerContext
            {
                Faction = Faction.OfPlayer,
                TriggerType = NpcDialogueTriggerType.Conditional,
                Category = NpcDialogueCategory.Social,
                SourceTag = "colonist_low_mood",
                Reason = "colonist_low_mood",
                Severity = 1,
                CreatedTick = currentTick,
                Metadata = worstMood.ToString("F3")
            };
            Owner.HandleTriggerContext(context, currentTick);
        }

        internal void EvaluateColonistPairThreatTriggers(int currentTick, bool hasHive, bool hasHostiles)
        {
            if (!Owner.IsColonistPairDialogueEnabled())
            {
                return;
            }

            if (PlayerGameStateCache.Instance.EligibleColonistCount < 2)
            {
                return;
            }

            if (currentTick - lastColonistPairDeliveredTick < ColonistPairCooldownTicks)
            {
                return;
            }

            if (!Owner.TryResolveColonistPair(currentTick, out Pawn initiator, out Pawn receiver))
            {
                return;
            }

            var context = new PawnRpgTriggerContext
            {
                Faction = Faction.OfPlayer,
                TriggerType = NpcDialogueTriggerType.Causal,
                Category = NpcDialogueCategory.WarningThreat,
                SourceTag = hasHive ? "colonist_hive_nearby" : "colonist_hostiles_nearby",
                Reason = hasHive ? "colonist_hive_warning" : "colonist_hostile_warning",
                Severity = hasHive ? 3 : 2,
                CreatedTick = currentTick
            };
            Owner.HandleTriggerContext(context, currentTick);
        }

        internal void EvaluateHomeEventTriggers(int currentTick)
        {
            if (!Owner.IsColonistPairDialogueEnabled())
            {
                return;
            }

            if (lastHomeEventTriggerTick > 0 && currentTick - lastHomeEventTriggerTick < HomeEventCooldownTicks)
            {
                return;
            }

            if (PlayerGameStateCache.Instance.EligibleColonistCount < 2)
            {
                return;
            }

            if (currentTick - lastColonistPairDeliveredTick < ColonistPairCooldownTicks)
            {
                return;
            }

            if (!PlayerGameStateCache.Instance.HasActiveHomeAlerts)
            {
                return;
            }

            if (!Owner.TryResolveColonistPair(currentTick, out Pawn initiator, out Pawn receiver))
            {
                return;
            }

            var context = new PawnRpgTriggerContext
            {
                Faction = Faction.OfPlayer,
                TriggerType = NpcDialogueTriggerType.Conditional,
                Category = NpcDialogueCategory.Social,
                SourceTag = "colonist_home_alert",
                Reason = "home_alert",
                Severity = 1,
                CreatedTick = currentTick
            };
            lastHomeEventTriggerTick = currentTick;
            Owner.HandleTriggerContext(context, currentTick);
        }

        internal bool TryResolveColonistPair(int currentTick, out Pawn initiator, out Pawn receiver, bool bypassAvailability = false)
        {
            initiator = null;
            receiver = null;
            int threshold = RelationsMod.Instance?.InstanceSettings?.ColonistPairMinOpinion ?? 10;

            // Receiver: from protagonist list (player replies as receiver)
            List<Pawn> protagonists = Owner.ResolveConfiguredProtagonists();
            List<Pawn> receivers = new List<Pawn>(protagonists.Count);
            for (int i = 0; i < protagonists.Count; i++)
            {
                Pawn p = protagonists[i];
                if (Owner.IsEligiblePlayerPawn(p) && (bypassAvailability || !Owner.IsPawnUnavailable(p)))
                    receivers.Add(p);
            }
            if (receivers.Count == 0)
            {
                if (!_loggedNoEligibleReceivers)
                {
                    Log.Message("[RimAI.Relations] TryResolveColonistPair: No eligible receivers in protagonist list (all busy or unavailable).");
                    _loggedNoEligibleReceivers = true;
                }
                return false;
            }

            // Initiator: from ALL colonists on map (not limited to protagonist list)
            List<Pawn> allColonists = new List<Pawn>();
            foreach (Map map in Find.Maps)
            {
                if (map?.mapPawns?.FreeColonistsSpawned == null) continue;
                for (int i = 0; i < map.mapPawns.FreeColonistsSpawned.Count; i++)
                {
                    Pawn p = map.mapPawns.FreeColonistsSpawned[i];
                    if (Owner.IsEligiblePlayerPawn(p) && (bypassAvailability || !Owner.IsPawnUnavailable(p)))
                        allColonists.Add(p);
                }
            }

            // Find best pair with early exit on perfect score
            Pawn bestReceiver = null;
            Pawn bestInitiator = null;
            int bestScore = int.MinValue;
            const int perfectScore = 2000;

            for (int ri = 0; ri < receivers.Count; ri++)
            {
                Pawn recv = receivers[ri];
                for (int ci = 0; ci < allColonists.Count; ci++)
                {
                    Pawn init = allColonists[ci];
                    if (init == recv) continue;

                    bool intimate = Owner.HasIntimateRelation(recv, init) || Owner.HasIntimateRelation(init, recv);
                    int opinion = Owner.GetOpinion(recv, init);
                    if (!intimate && opinion < threshold) continue;

                    int score = (intimate ? 1000 : 0) + opinion;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestReceiver = recv;
                        bestInitiator = init;
                        if (score >= perfectScore) goto found;
                    }
                }
            }

        found:
            if (bestReceiver == null || bestInitiator == null)
            {
                if (!_loggedNoValidPair)
                {
                    Log.Message($"[RimAI.Relations] TryResolveColonistPair: No valid pair found. Receivers={receivers.Count}, AllColonists={allColonists.Count}, threshold={threshold}");
                    _loggedNoValidPair = true;
                }
                return false;
            }

            initiator = bestInitiator;
            receiver = bestReceiver;
            return true;
        }

        internal bool TryResolveColonistPairForTarget(Pawn target, out Pawn partner)
        {
            partner = null;
            if (target == null || !Owner.IsEligiblePlayerPawn(target))
            {
                return false;
            }

            int threshold = RelationsMod.Instance?.InstanceSettings?.ColonistPairMinOpinion ?? 10;
            Pawn best = null;
            int bestScore = int.MinValue;

            foreach (Pawn colonist in Owner.ResolveConfiguredProtagonists())
            {
                if (colonist == target || !Owner.IsEligiblePlayerPawn(colonist) || Owner.IsPawnUnavailable(colonist))
                {
                    continue;
                }

                bool intimate = Owner.HasIntimateRelation(target, colonist) || Owner.HasIntimateRelation(colonist, target);
                int opinion = Owner.GetOpinion(target, colonist);
                if (!intimate && opinion < threshold)
                {
                    continue;
                }

                int score = intimate ? 1000 + opinion : opinion;
                if (score > bestScore)
                {
                    best = colonist;
                    bestScore = score;
                }
            }

            if (best == null)
            {
                return false;
            }

            partner = best;
            return true;
        }
        }

}


