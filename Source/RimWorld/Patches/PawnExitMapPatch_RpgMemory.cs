using HarmonyLib;
using Ustas.RimAI.Communication.Relations.Dialogue;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.WorldState;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Patches
{
    /// <summary>/// Dependencies: Verse.Pawn.ExitMap(bool, Rot4).
 /// Responsibility: generate RPG departure summary into faction memory when qualified NPC exits a player map.
 ///</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.ExitMap), new[] { typeof(bool), typeof(Rot4) })]
    public static class PawnExitMapPatch_RpgMemory
    {
        private static void Prefix(Pawn __instance)
        {
            if (__instance == null || __instance.Dead || __instance.Destroyed)
            {
                return;
            }

            RansomContractManager.Instance?.HandlePawnExit(__instance);

            if (!RelationsTrackedEntityRegistry.IsPawnTracked(__instance))
            {
                return;
            }

            if (__instance.Faction == null || __instance.Faction.IsPlayer || __instance.Faction.defeated)
            {
                return;
            }

            if (!PawnDialogueRoutingPolicy.IsRpgDialogueEligibleRace(__instance))
            {
                return;
            }

            Map map = __instance.Map;
            if (map == null || !map.IsPlayerHome)
            {
                return;
            }

            if (!RpgDialogueTraceTracker.TryConsumeRecentForExit(__instance, out RpgDialogueTraceSnapshot trace))
            {
                return;
            }

            DialogueSummaryService.TryRecordRpgDepartSummary(__instance, trace);
        }
    }
}
