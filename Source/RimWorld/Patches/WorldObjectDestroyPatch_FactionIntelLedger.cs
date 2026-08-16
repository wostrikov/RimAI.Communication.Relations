using HarmonyLib;
using Ustas.RimAI.Communication.Relations.WorldState;
using RimWorld.Planet;

namespace Ustas.RimAI.Communication.Relations.Patches
{
    /// <summary>/// Dependencies: RimWorld.Planet.WorldObject.Destroy.
 /// Responsibility: record faction settlement destruction history for fixed intel injection.
 ///</summary>
    [HarmonyPatch(typeof(WorldObject), nameof(WorldObject.Destroy))]
    public static class WorldObjectDestroyPatch_FactionIntelLedger
    {
        private static void Postfix(WorldObject __instance)
        {
            if (!RelationsTrackedEntityRegistry.IsWorldObjectTracked(__instance))
            {
                return;
            }

            FactionIntelLedgerComponent.Instance?.RecordSettlementDestroyed(__instance);
        }
    }
}
