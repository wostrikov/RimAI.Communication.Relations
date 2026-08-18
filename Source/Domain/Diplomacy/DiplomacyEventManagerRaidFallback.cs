using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Communication.Relations.DiplomacySystem
{
        internal static class DiplomacyEventManagerRaidFallback
    {

        internal static readonly string[] MiliraRaidIncidentDefCandidates =
        {
            "milira-raid",
            "milira_raid",
            "MiliraRaid",
            "Milira_Raid",
            "RimChat_MiliraRaid"
        };

        internal static bool EnsureRaidTemplates(Faction faction, out string reason)
        {
            reason = string.Empty;
            if (DiplomacyEventManager.HasUsableCombatPawnGroupMaker(faction, out _))
            {
                return true;
            }

            if (!DiplomacyEventManager.TryInjectDefaultCombatPawnGroupMaker(faction, out string injectReason))
            {
                reason = injectReason;
                return false;
            }

            if (DiplomacyEventManager.HasUsableCombatPawnGroupMaker(faction, out _))
            {
                Log.Warning($"[RimAI.Relations] Applied default raid combat template for faction {faction?.Name}: {injectReason}");
                return true;
            }

            reason = "default raid combat template injection did not produce a usable Combat pawnGroupMaker.";
            return false;
        }

        internal static bool TryInjectDefaultCombatPawnGroupMaker(Faction faction, out string reason)
        {
            reason = string.Empty;
            FactionDef factionDef = faction?.def;
            if (factionDef == null)
            {
                reason = "faction def is null.";
                return false;
            }

            List<PawnGroupMaker> makers = factionDef.pawnGroupMakers;
            if (makers == null)
            {
                makers = new List<PawnGroupMaker>();
                factionDef.pawnGroupMakers = makers;
            }

            List<PawnGenOption> fallbackOptions = DiplomacyEventManager.BuildDefaultCombatOptions(faction, makers);
            if (fallbackOptions.Count == 0)
            {
                reason = "cannot resolve pawn kind for default Combat pawnGroupMaker.";
                return false;
            }

            var fallbackMaker = new PawnGroupMaker
            {
                kindDef = PawnGroupKindDefOf.Combat,
                commonality = 1f,
                options = fallbackOptions,
                maxTotalPoints = 100000f
            };
            makers.Add(fallbackMaker);
            reason = $"injected Combat pawnGroupMaker with {fallbackOptions.Count} option(s).";
            return true;
        }

        internal static List<PawnGenOption> BuildDefaultCombatOptions(Faction faction, List<PawnGroupMaker> makers)
        {
            List<PawnGenOption> cloned = makers
                .Where(m => m?.options != null && m.options.Count > 0)
                .SelectMany(m => m.options)
                .Where(o => o?.kind != null)
                .Take(12)
                .Select(ClonePawnGenOption)
                .Where(o => o != null)
                .ToList();
            if (cloned.Count > 0)
            {
                return cloned;
            }

            PawnKindDef fallbackKind = DiplomacyEventManager.ResolveFallbackRaidPawnKind(faction, makers);
            if (fallbackKind == null)
            {
                return new List<PawnGenOption>();
            }

            return new List<PawnGenOption>
            {
                new PawnGenOption
                {
                    kind = fallbackKind,
                    selectionWeight = 1f
                }
            };
        }

        internal static PawnGenOption ClonePawnGenOption(PawnGenOption source)
        {
            if (source?.kind == null)
            {
                return null;
            }

            float weight = source.selectionWeight > 0f ? source.selectionWeight : 1f;
            return new PawnGenOption
            {
                kind = source.kind,
                selectionWeight = weight
            };
        }

        internal static PawnKindDef ResolveFallbackRaidPawnKind(Faction faction, List<PawnGroupMaker> makers)
        {
            PawnKindDef kindFromFaction = faction?.def?.basicMemberKind;
            if (kindFromFaction != null)
            {
                return kindFromFaction;
            }

            PawnKindDef leaderKind = faction?.def?.fixedLeaderKinds?.FirstOrDefault(k => k != null);
            if (leaderKind != null)
            {
                return leaderKind;
            }

            PawnKindDef existingKind = makers?
                .Where(m => m?.options != null)
                .SelectMany(m => m.options)
                .Select(o => o?.kind)
                .FirstOrDefault(k => k != null);
            if (existingKind != null)
            {
                return existingKind;
            }

            PawnKindDef factionOwnedKind = DefDatabase<PawnKindDef>.AllDefsListForReading
                .FirstOrDefault(k => k != null && k.defaultFactionDef == faction?.def);
            if (factionOwnedKind != null)
            {
                return factionOwnedKind;
            }

            PawnKindDef villager = DefDatabase<PawnKindDef>.GetNamedSilentFail("Villager");
            if (villager != null)
            {
                return villager;
            }

            return DefDatabase<PawnKindDef>.AllDefsListForReading
                .FirstOrDefault(k => k?.RaceProps != null && k.RaceProps.Humanlike);
        }

        internal static IncidentParms BuildRaidIncidentParmsWithDefaults(
            IncidentDef incidentDef,
            Map map,
            Faction faction,
            float raidPoints,
            RaidStrategyDef strategy,
            PawnsArrivalModeDef arrivalMode)
        {
            IncidentParms parms = null;
            try
            {
                parms = StorytellerUtility.DefaultParmsNow(incidentDef.category, map);
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimAI.Relations] Failed to build storyteller raid parms: {ex.Message}");
            }

            if (parms == null)
            {
                parms = new IncidentParms();
            }

            parms.target = map;
            parms.faction = faction;
            parms.points = raidPoints > 0f ? raidPoints : DiplomacyEventManager.ResolveBaseRaidPointsFromStoryteller(map);
            parms.raidStrategy = strategy;
            parms.raidArrivalMode = arrivalMode;
            if (parms.raidStrategy != null && parms.raidArrivalMode == null)
            {
                parms.raidArrivalMode = DiplomacyEventManager.GetFallbackArrivalMode(parms.raidStrategy);
            }
            parms.forced = true;
            return parms;
        }

        internal static bool EnsureUsableCombatPawnGroupMakerForParms(Faction faction, IncidentParms raidParms, out string reason)
        {
            reason = string.Empty;
            if (DiplomacyEventManager.HasUsableCombatPawnGroupMakerForParms(faction, raidParms, out _))
            {
                return true;
            }

            if (DiplomacyEventManager.TryRaiseRaidPointsToMeetCombatMinimum(faction, raidParms, out string pointAdjustReason)
                && DiplomacyEventManager.HasUsableCombatPawnGroupMakerForParms(faction, raidParms, out _))
            {
                Log.Warning($"[RimAI.Relations] Raised raid points for faction {faction?.Name}: {pointAdjustReason}");
                return true;
            }

            if (!DiplomacyEventManager.TryInjectEmergencyCombatPawnGroupMakerForParms(faction, raidParms, out string injectReason))
            {
                reason = !string.IsNullOrEmpty(injectReason)
                    ? injectReason
                    : pointAdjustReason;
                return false;
            }

            if (DiplomacyEventManager.HasUsableCombatPawnGroupMakerForParms(faction, raidParms, out _))
            {
                Log.Warning($"[RimAI.Relations] Injected emergency raid combat template for faction {faction?.Name}: {injectReason}");
                return true;
            }

            reason = "emergency raid combat template injection did not produce a usable combat maker for current parms.";
            return false;
        }

        internal static bool HasUsableCombatPawnGroupMakerForParms(Faction faction, IncidentParms raidParms, out string reason)
        {
            reason = string.Empty;
            if (faction?.def == null)
            {
                reason = "faction def is null.";
                return false;
            }

            PawnGroupMakerParms groupParms = DiplomacyEventManager.BuildRaidGroupMakerParms(raidParms, out string buildReason);
            if (groupParms == null)
            {
                reason = buildReason;
                return false;
            }

            if (groupParms.faction == null)
            {
                reason = "group parms faction is null.";
                return false;
            }

            List<PawnGroupMaker> combatMakers = faction.def.pawnGroupMakers?
                .Where(m => m?.kindDef == PawnGroupKindDefOf.Combat && m.options != null && m.options.Count > 0)
                .ToList() ?? new List<PawnGroupMaker>();
            if (combatMakers.Count == 0)
            {
                reason = "no combat makers with options.";
                return false;
            }

            if (DiplomacyEventManager.SafeHasAnyPreviewKinds(groupParms))
            {
                return true;
            }

            reason = $"combat makers exist ({combatMakers.Count}) but none can generate for current raid parms.";
            return false;
        }

        internal static PawnGroupMakerParms BuildRaidGroupMakerParms(IncidentParms raidParms, out string reason)
        {
            reason = string.Empty;
            if (raidParms == null)
            {
                reason = "raid parms is null.";
                return null;
            }

            try
            {
                PawnGroupMakerParms groupParms = IncidentParmsUtility.GetDefaultPawnGroupMakerParms(PawnGroupKindDefOf.Combat, raidParms, true);
                if (groupParms == null)
                {
                    reason = "IncidentParmsUtility returned null.";
                    return null;
                }

                groupParms.groupKind = PawnGroupKindDefOf.Combat;
                if (groupParms.faction == null)
                {
                    groupParms.faction = raidParms.faction;
                }

                if (groupParms.points <= 0f)
                {
                    groupParms.points = raidParms.points > 0f ? raidParms.points : 35f;
                }

                return groupParms;
            }
            catch (Exception ex)
            {
                reason = $"failed to build PawnGroupMakerParms: {ex.Message}";
                return null;
            }
        }

        internal static bool SafeCanGenerateFrom(PawnGroupMaker maker, PawnGroupMakerParms parms)
        {
            if (maker == null || parms == null)
            {
                return false;
            }

            try
            {
                return maker.CanGenerateFrom(parms);
            }
            catch
            {
                return false;
            }
        }

        internal static bool SafeHasPreviewKinds(PawnGroupMaker maker, PawnGroupMakerParms parms)
        {
            if (maker == null || parms == null)
            {
                return false;
            }

            try
            {
                IEnumerable<PawnKindDef> preview = maker.GeneratePawnKindsExample(parms);
                return preview != null && preview.Any(k => k != null);
            }
            catch
            {
                return false;
            }
        }

        internal static bool SafeHasAnyPreviewKinds(PawnGroupMakerParms parms)
        {
            if (parms == null)
            {
                return false;
            }

            try
            {
                IEnumerable<PawnKindDef> preview = PawnGroupMakerUtility.GeneratePawnKindsExample(parms);
                return preview != null && preview.Any(k => k != null);
            }
            catch
            {
                return false;
            }
        }

        internal static bool TryRaiseRaidPointsToMeetCombatMinimum(Faction faction, IncidentParms raidParms, out string reason)
        {
            reason = string.Empty;
            if (faction?.def == null || raidParms == null)
            {
                reason = "faction def or raid parms is null.";
                return false;
            }

            PawnGroupMakerParms groupParms = DiplomacyEventManager.BuildRaidGroupMakerParms(raidParms, out string buildReason);
            if (groupParms == null)
            {
                reason = buildReason;
                return false;
            }

            List<float> mins = faction.def.pawnGroupMakers?
                .Where(m => m?.kindDef == PawnGroupKindDefOf.Combat && m.options != null && m.options.Count > 0)
                .Select(m => DiplomacyEventManager.SafeMinPointsToGenerateAnything(m, faction.def, groupParms))
                .Where(v => v > 0f && !float.IsNaN(v) && !float.IsInfinity(v))
                .ToList() ?? new List<float>();
            if (mins.Count == 0)
            {
                reason = "no combat makers reported min points.";
                return false;
            }

            float currentPoints = raidParms.points > 0f ? raidParms.points : 35f;
            float minRequired = mins.Min() + 1f;
            float factionMin = DiplomacyEventManager.SafeMinPointsToGeneratePawnGroup(faction.def, groupParms);
            if (factionMin > 0f)
            {
                minRequired = Math.Max(minRequired, factionMin + 1f);
            }
            float[] multipliers = { 1f, 1.5f, 2.5f, 4f, 6f };
            for (int i = 0; i < multipliers.Length; i++)
            {
                float candidate = Math.Max(minRequired, currentPoints * multipliers[i]);
                candidate = Math.Max(candidate, 120f);
                raidParms.points = candidate;
                if (DiplomacyEventManager.HasUsableCombatPawnGroupMakerForParms(faction, raidParms, out _))
                {
                    reason = $"raised points to {candidate:F1} (minRequired={minRequired:F1}, x{multipliers[i]:F1}).";
                    return true;
                }
            }

            reason = $"tried point escalation from {currentPoints:F1} with minRequired={minRequired:F1}, but no usable combat maker.";
            return false;
        }

        internal static float SafeMinPointsToGenerateAnything(PawnGroupMaker maker, FactionDef factionDef, PawnGroupMakerParms parms)
        {
            if (maker == null || factionDef == null || parms == null)
            {
                return 0f;
            }

            try
            {
                return maker.MinPointsToGenerateAnything(factionDef, parms);
            }
            catch
            {
                return 0f;
            }
        }

        internal static float SafeMinPointsToGeneratePawnGroup(FactionDef factionDef, PawnGroupMakerParms parms)
        {
            if (factionDef == null || parms == null)
            {
                return 0f;
            }

            try
            {
                return factionDef.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat, parms);
            }
            catch
            {
                return 0f;
            }
        }

        internal static bool TryInjectEmergencyCombatPawnGroupMakerForParms(Faction faction, IncidentParms raidParms, out string reason)
        {
            return DiplomacyEventManagerRaidEmergencyOps.TryInjectEmergencyCombatPawnGroupMakerForParms(faction, raidParms, out reason);
        }

        internal static List<PawnGenOption> BuildEmergencyCombatOptions(Faction faction, PawnGroupMakerParms groupParms)
        {
            return DiplomacyEventManagerRaidEmergencyOps.BuildEmergencyCombatOptions(faction, groupParms);
        }

        internal static List<PawnKindDef> BuildEmergencyCombatKinds(Faction faction)
        {
            return DiplomacyEventManagerRaidEmergencyOps.BuildEmergencyCombatKinds(faction);
        }

        internal static bool CanKindGenerateForParms(PawnKindDef kind, PawnGroupMakerParms groupParms)
        {
            return DiplomacyEventManagerRaidEmergencyOps.CanKindGenerateForParms(kind, groupParms);
        }

        internal static bool IsEmergencyRaidKindCandidate(PawnKindDef kind)
        {
            return DiplomacyEventManagerRaidEmergencyOps.IsEmergencyRaidKindCandidate(kind);
        }

        internal static bool TryExecuteMiliraRaidFallback(Map map, Faction faction, float raidPoints, out string reason)
        {
            return DiplomacyEventManagerRaidMiliraOps.TryExecuteMiliraRaidFallback(map, faction, raidPoints, out reason);
        }

        internal static List<float> BuildMiliraFallbackPointCandidates(float requestedPoints, float minRequiredPoints)
        {
            return DiplomacyEventManagerRaidMiliraOps.BuildMiliraFallbackPointCandidates(requestedPoints, minRequiredPoints);
        }

        internal static IncidentDef GetMiliraRaidIncidentDef(out string reason)
        {
            return DiplomacyEventManagerRaidMiliraOps.GetMiliraRaidIncidentDef(out reason);
        }

        internal static bool IsMiliraFaction(Faction faction)
        {
            return DiplomacyEventManagerRaidMiliraOps.IsMiliraFaction(faction);
        }

        internal static bool ContainsIgnoreCase(string source, string token)
        {
            return DiplomacyEventManagerRaidMiliraOps.ContainsIgnoreCase(source, token);
        }


        }

}
