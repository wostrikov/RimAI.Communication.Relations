using System;

namespace Ustas.RimAI.Communication.Relations.AI
{
    /// <summary>
    /// RPG action whitelist. Unknown names are not coerced and must not reach
    /// the executor.
    /// </summary>
    public static class RpgActionCatalog
    {
        public static readonly string[] Canonical =
        {
            "ExitDialogue",
            "ExitDialogueCooldown",
            "RomanceAttempt",
            "MarriageProposal",
            "Breakup",
            "Divorce",
            "Date",
            "TryGainMemory",
            "TryAffectSocialGoodwill",
            "ReduceResistance",
            "ReduceWill",
            "Recruit",
            "TryTakeOrderedJob",
            "TriggerIncident",
            "GrantInspiration",
            "ConvertIdeology",
            "AdjustCertainty"
        };

        public static bool IsValidAction(string action)
        {
            string canonical = NormalizeActionName(action);
            if (string.IsNullOrWhiteSpace(canonical))
            {
                return false;
            }

            return Array.Exists(Canonical, item => string.Equals(item, canonical, StringComparison.Ordinal));
        }

        public static string NormalizeActionName(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return null;
            }

            string normalized = actionName.Trim().Replace("-", "_").ToLowerInvariant();
            switch (normalized)
            {
                case "romanceattempt":
                case "romance_attempt":
                case "romance":
                case "fall_in_love":
                case "start_romance":
                case "кохання":
                    return "RomanceAttempt";
                case "marriageproposal":
                case "marriage_proposal":
                case "propose_marriage":
                case "marry":
                case "одруження":
                    return "MarriageProposal";
                case "breakup":
                case "break_up":
                case "split_up":
                case "розрив":
                    return "Breakup";
                case "divorce":
                case "розлучення":
                    return "Divorce";
                case "date":
                case "dating":
                case "побачення":
                    return "Date";
                case "trygainmemory":
                case "try_gain_memory":
                    return "TryGainMemory";
                case "tryaffectsocialgoodwill":
                case "try_affect_social_goodwill":
                    return "TryAffectSocialGoodwill";
                case "reduceresistance":
                case "reduce_resistance":
                    return "ReduceResistance";
                case "reducewill":
                case "reduce_will":
                    return "ReduceWill";
                case "recruit":
                case "action4":
                case "action_4":
                case "action 4":
                case "Дія 4":
                case "Четверта дія":
                    return "Recruit";
                case "trytakeorderedjob":
                case "try_take_ordered_job":
                    return "TryTakeOrderedJob";
                case "triggerincident":
                case "trigger_incident":
                    return "TriggerIncident";
                case "grantinspiration":
                case "grant_inspiration":
                    return "GrantInspiration";
                case "exitdialoguecooldown":
                case "exit_dialogue_cooldown":
                case "exit_dialogue_with_cooldown":
                    return "ExitDialogueCooldown";
                case "exitdialogue":
                case "exit_dialogue":
                    return "ExitDialogue";
                case "convertideology":
                case "convert_ideology":
                case "change_ideology":
                case "Змінити ідеологію":
                case "навернення":
                case "змінити віру":
                    return "ConvertIdeology";
                case "adjustcertainty":
                case "adjust_certainty":
                case "change_certainty":
                case "Змінити побожність":
                case "похитнути віру":
                    return "AdjustCertainty";
                default:
                    return actionName.Trim();
            }
        }
    }
}
