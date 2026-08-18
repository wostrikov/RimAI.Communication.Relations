using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Communication.Relations.DiplomacySystem
{
    /// <summary>
    /// Milira-specific raid fallback execution helpers.
    /// </summary>
    internal static class DiplomacyEventManagerRaidMiliraOps
    {
        internal static bool TryExecuteMiliraRaidFallback(Map map, Faction faction, float raidPoints, out string reason)
        {
            reason = "skipped";
            if (!DiplomacyEventManager.IsMiliraFaction(faction))
            {
                reason = "not milira faction";
                return false;
            }

            IncidentDef fallbackIncident = DiplomacyEventManager.GetMiliraRaidIncidentDef(out string incidentReason);
            if (fallbackIncident == null)
            {
                reason = incidentReason;
                return false;
            }

            if (fallbackIncident.Worker == null)
            {
                reason = $"incident {fallbackIncident.defName} has null worker";
                return false;
            }

            IncidentParms seedParms = DiplomacyEventManager.BuildRaidIncidentParmsWithDefaults(
                fallbackIncident,
                map,
                faction,
                raidPoints,
                strategy: null,
                arrivalMode: null);
            PawnGroupMakerParms seedGroupParms = DiplomacyEventManager.BuildRaidGroupMakerParms(seedParms, out _);
            float minRequiredPoints = DiplomacyEventManager.SafeMinPointsToGeneratePawnGroup(faction?.def, seedGroupParms);

            List<float> pointCandidates = DiplomacyEventManager.BuildMiliraFallbackPointCandidates(raidPoints, minRequiredPoints);
            var attemptNotes = new List<string>();
            for (int i = 0; i < pointCandidates.Count; i++)
            {
                float candidatePoints = pointCandidates[i];
                IncidentParms fallbackParms = DiplomacyEventManager.BuildRaidIncidentParmsWithDefaults(
                    fallbackIncident,
                    map,
                    faction,
                    candidatePoints,
                    strategy: null,
                    arrivalMode: null);

                bool ensureOk = DiplomacyEventManager.EnsureUsableCombatPawnGroupMakerForParms(faction, fallbackParms, out string ensureReason);
                if (string.IsNullOrEmpty(ensureReason))
                {
                    ensureReason = ensureOk ? "ok" : "failed";
                }

                if (!ensureOk)
                {
                    attemptNotes.Add($"points={fallbackParms.points:F1}, ensure={ensureReason}");
                    continue;
                }

                if (!fallbackIncident.Worker.CanFireNow(fallbackParms))
                {
                    attemptNotes.Add($"points={fallbackParms.points:F1}, canFire=false, ensure={ensureReason}");
                    continue;
                }

                if (fallbackIncident.Worker.TryExecute(fallbackParms))
                {
                    reason = $"executed incident {fallbackIncident.defName} at points={fallbackParms.points:F1}";
                    Log.Warning($"[RimAI.Relations] Milira raid fallback triggered: incident={fallbackIncident.defName}, faction={faction?.Name}, points={fallbackParms.points:F1}");
                    return true;
                }

                attemptNotes.Add($"points={fallbackParms.points:F1}, tryExecute=false, ensure={ensureReason}");
            }

            reason = $"incident {fallbackIncident.defName} failed. attempts={string.Join(" | ", attemptNotes)}";
            return false;
        }

        internal static List<float> BuildMiliraFallbackPointCandidates(float requestedPoints, float minRequiredPoints)
        {
            float basePoints = requestedPoints > 0f ? requestedPoints : 90f;
            float minFloor = minRequiredPoints > 0f ? minRequiredPoints + 1f : 0f;
            float[] raw = new[]
            {
                Math.Max(basePoints, minFloor),
                Math.Max(basePoints * 1.5f, Math.Max(120f, minFloor)),
                Math.Max(basePoints * 2.5f, Math.Max(220f, minFloor)),
                Math.Max(basePoints * 4f, Math.Max(400f, minFloor)),
                Math.Max(basePoints * 6f, Math.Max(700f, minFloor)),
                Math.Max(basePoints * 9f, Math.Max(1100f, minFloor))
            };

            var candidates = new List<float>();
            var seen = new HashSet<int>();
            for (int i = 0; i < raw.Length; i++)
            {
                float value = raw[i];
                int key = (int)Math.Round(value);
                if (seen.Add(key))
                {
                    candidates.Add(value);
                }
            }

            return candidates;
        }

        internal static IncidentDef GetMiliraRaidIncidentDef(out string reason)
        {
            reason = "not found";
            for (int i = 0; i < DiplomacyEventManagerRaidFallback.MiliraRaidIncidentDefCandidates.Length; i++)
            {
                string candidate = DiplomacyEventManagerRaidFallback.MiliraRaidIncidentDefCandidates[i];
                IncidentDef byName = DefDatabase<IncidentDef>.GetNamedSilentFail(candidate);
                if (byName != null)
                {
                    reason = $"resolved by defName={candidate}";
                    return byName;
                }
            }

            IncidentDef fuzzyMatch = DefDatabase<IncidentDef>.AllDefsListForReading
                .FirstOrDefault(def => DiplomacyEventManager.ContainsIgnoreCase(def?.defName, "milira") && DiplomacyEventManager.ContainsIgnoreCase(def?.defName, "raid"));
            if (fuzzyMatch != null)
            {
                reason = $"resolved by fuzzy defName={fuzzyMatch.defName}";
                return fuzzyMatch;
            }

            reason = $"missing candidates: {string.Join(", ", DiplomacyEventManagerRaidFallback.MiliraRaidIncidentDefCandidates)}";
            return null;
        }

        internal static bool IsMiliraFaction(Faction faction)
        {
            string defName = faction?.def?.defName;
            if (DiplomacyEventManager.ContainsIgnoreCase(defName, "milira") || DiplomacyEventManager.ContainsIgnoreCase(defName, "mirila"))
            {
                return true;
            }

            string factionName = faction?.Name;
            return DiplomacyEventManager.ContainsIgnoreCase(factionName, "milira") || DiplomacyEventManager.ContainsIgnoreCase(factionName, "mirila");
        }

        internal static bool ContainsIgnoreCase(string source, string token)
        {
            return !string.IsNullOrEmpty(source)
                && !string.IsNullOrEmpty(token)
                && source.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
