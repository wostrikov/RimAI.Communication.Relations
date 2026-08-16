using HarmonyLib;
using Ustas.RimAI.Communication.Relations.WorldState;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Patches
{
    /// <summary>/// Dependencies: Verse.Thing.TakeDamage.
 /// Responsibility: feed player-building loss intel for raid damage aggregation.
 ///</summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    public static class ThingTakeDamagePatch_FactionIntelLedger
    {
        private static void Postfix(Thing __instance, DamageInfo dinfo)
        {
            if (__instance == null || !__instance.Destroyed || !RelationsTrackedEntityRegistry.IsThingTracked(__instance))
            {
                return;
            }

            FactionIntelLedgerComponent.Instance?.NotifyBuildingDestroyed(__instance, dinfo);
        }
    }
}
