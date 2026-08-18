using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Communication.Relations.DiplomacySystem
{
    /// <summary>
    /// Emergency combat pawn-group injection for raid fallback.
    /// </summary>
    internal static class DiplomacyEventManagerRaidEmergencyOps
    {
        internal static bool TryInjectEmergencyCombatPawnGroupMakerForParms(Faction faction, IncidentParms raidParms, out string reason)
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

            PawnGroupMakerParms groupParms = DiplomacyEventManager.BuildRaidGroupMakerParms(raidParms, out string buildReason);
            if (groupParms == null)
            {
                reason = buildReason;
                return false;
            }

            List<PawnGenOption> emergencyOptions = DiplomacyEventManager.BuildEmergencyCombatOptions(faction, groupParms);
            if (emergencyOptions.Count == 0)
            {
                reason = "cannot resolve any emergency pawn kinds.";
                return false;
            }

            var emergencyMaker = new PawnGroupMaker
            {
                kindDef = PawnGroupKindDefOf.Combat,
                commonality = 1000f,
                options = emergencyOptions,
                maxTotalPoints = 1000000f
            };
            makers.Add(emergencyMaker);

            if (DiplomacyEventManager.HasUsableCombatPawnGroupMakerForParms(faction, raidParms, out _))
            {
                reason = $"added emergency combat maker with {emergencyOptions.Count} options.";
                return true;
            }

            reason = "added emergency combat maker but it is still not usable for current raid parms.";
            return false;
        }

        internal static List<PawnGenOption> BuildEmergencyCombatOptions(Faction faction, PawnGroupMakerParms groupParms)
        {
            List<PawnKindDef> candidates = DiplomacyEventManager.BuildEmergencyCombatKinds(faction);
            return candidates
                .Where(kind => DiplomacyEventManager.CanKindGenerateForParms(kind, groupParms))
                .Take(12)
                .Select(kind => new PawnGenOption
                {
                    kind = kind,
                    selectionWeight = kind.combatPower > 0f ? kind.combatPower : 1f
                })
                .ToList();
        }

        internal static List<PawnKindDef> BuildEmergencyCombatKinds(Faction faction)
        {
            var result = new List<PawnKindDef>();
            var seen = new HashSet<PawnKindDef>();

            void AddCandidate(PawnKindDef kind)
            {
                if (!DiplomacyEventManager.IsEmergencyRaidKindCandidate(kind))
                {
                    return;
                }

                if (seen.Add(kind))
                {
                    result.Add(kind);
                }
            }

            AddCandidate(faction?.def?.basicMemberKind);
            if (faction?.def?.fixedLeaderKinds != null)
            {
                for (int i = 0; i < faction.def.fixedLeaderKinds.Count; i++)
                {
                    AddCandidate(faction.def.fixedLeaderKinds[i]);
                }
            }

            List<PawnKindDef> allKinds = DefDatabase<PawnKindDef>.AllDefsListForReading;
            for (int i = 0; i < allKinds.Count; i++)
            {
                PawnKindDef kind = allKinds[i];
                if (kind?.defaultFactionDef == faction?.def)
                {
                    AddCandidate(kind);
                }
            }

            for (int i = 0; i < allKinds.Count; i++)
            {
                PawnKindDef kind = allKinds[i];
                if (kind != null && kind.defaultFactionDef == null)
                {
                    AddCandidate(kind);
                }
            }

            AddCandidate(DefDatabase<PawnKindDef>.GetNamedSilentFail("Villager"));
            return result;
        }

        internal static bool CanKindGenerateForParms(PawnKindDef kind, PawnGroupMakerParms groupParms)
        {
            if (!DiplomacyEventManager.IsEmergencyRaidKindCandidate(kind) || groupParms == null)
            {
                return false;
            }

            var testMaker = new PawnGroupMaker
            {
                kindDef = PawnGroupKindDefOf.Combat,
                commonality = 1f,
                maxTotalPoints = 1000000f,
                options = new List<PawnGenOption>
                {
                    new PawnGenOption
                    {
                        kind = kind,
                        selectionWeight = kind.combatPower > 0f ? kind.combatPower : 1f
                    }
                }
            };
            return DiplomacyEventManager.SafeCanGenerateFrom(testMaker, groupParms) && DiplomacyEventManager.SafeHasPreviewKinds(testMaker, groupParms);
        }

        internal static bool IsEmergencyRaidKindCandidate(PawnKindDef kind)
        {
            return kind != null
                && kind.RaceProps != null
                && kind.RaceProps.Humanlike
                && kind.combatPower > 0f
                && !kind.factionLeader;
        }
    }
}
