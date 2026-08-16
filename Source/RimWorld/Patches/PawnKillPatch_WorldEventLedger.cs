using HarmonyLib;
using Ustas.RimAI.Communication.Relations.WorldState;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Patches
{
    /// <summary>/// Dependencies: Verse.Pawn.Kill.
 /// Responsibility: feed raid casualty aggregation in world-event ledger.
 ///</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class PawnKillPatch_WorldEventLedger
    {
        private static void Postfix(Pawn __instance, DamageInfo? dinfo, Hediff exactCulprit)
        {
            if (!RelationsTrackedEntityRegistry.IsPawnTracked(__instance))
            {
                return;
            }

            WorldEventLedgerComponent.Instance?.NotifyPawnKilled(__instance, dinfo);
            FactionIntelLedgerComponent.Instance?.NotifyPawnKilled(__instance, dinfo);
        }
    }
}
