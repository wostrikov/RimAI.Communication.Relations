using RimAI.Core.Runtime;
using Verse;
using Verse.AI.Group;

namespace Ustas.RimAI.Communication.Relations.Dialogue
{
    /// <summary>
    /// Dependencies: Pawn lord/duty runtime state.
    /// Responsibility: classify whether a pawn should route through RPG dialogue or vanilla caravan trade.
    /// </summary>
    internal static class PawnDialogueRoutingPolicy
    {
        /// <summary>
        /// Check if a pawn can participate in RPG dialogue.
        /// Humanlike/Mechanoid/Animal are allowed.
        /// ToolUser requires persona-related subsystems to exclude shell pawns (for example VehiclePawn).
        /// </summary>
        internal static bool IsRpgDialogueEligibleRace(Pawn pawn)
        {
            if (pawn?.RaceProps == null)
            {
                return false;
            }

            if (pawn.RaceProps.Humanlike || pawn.RaceProps.IsMechanoid || pawn.RaceProps.Animal)
            {
                return true;
            }

            if (!pawn.RaceProps.ToolUser)
            {
                return false;
            }

            return HasPersonaSubsystems(pawn);
        }

        internal static bool IsPersonaSyncEligible(Pawn pawn)
        {
            if (!IsRpgDialogueEligibleRace(pawn) || pawn?.RaceProps == null)
            {
                return false;
            }

            if (pawn.RaceProps.Animal)
            {
                return false;
            }

            return HasPersonaSubsystems(pawn);
        }

        private static bool HasPersonaSubsystems(Pawn pawn)
        {
            return pawn?.story != null && pawn.skills != null;
        }

        internal static bool ShouldUseRpgDialogue(Pawn initiator, Pawn target, out string reason)
        {
            reason = string.Empty;
            if (initiator == null)
            {
                reason = "initiator_null";
                return false;
            }

            if (target == null)
            {
                reason = "target_null";
                return false;
            }

            if (!IsRpgDialogueEligibleRace(target))
            {
                reason = "target_race_ineligible";
                return false;
            }

            var policy = RimAiRuntimeGateway.ResolveRelationsDialoguePolicy("rpg");
            if (!policy.AllowRpg)
            {
                reason = "runtime_policy_skip";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Returns a localized reason string if the pawn's race is ineligible for RPG dialogue, or null if eligible.
        /// </summary>
        internal static string GetIneligibleRaceReason(Pawn pawn)
        {
            if (pawn?.RaceProps == null)
            {
                return "RimChat_RaceNotEligible_NullRace".Translate();
            }

            if (IsRpgDialogueEligibleRace(pawn))
            {
                return null;
            }

            return "RimChat_RaceNotEligible_Incompatible".Translate(pawn.LabelShort ?? pawn.KindLabel);
        }
    }
}
