using System;

namespace Ustas.RimAI.Communication.Relations.AI
{
    /// <summary>
    /// Diplomacy action discriminator aliases and whitelist.
    /// Unknown actions are not coerced into another type.
    /// </summary>
    public static class DiplomacyActionCatalog
    {
        public static bool IsValidAction(string action)
        {
            action = NormalizeActionName(action);
            string[] validActions = new string[]
            {
                "adjust_goodwill",
                "send_gift",
                "request_aid",
                "declare_war",
                "make_peace",
                "request_caravan",
                "request_visitor",
                "request_raid",
                "request_raid_call_everyone",
                "request_raid_waves",
                "request_item_airdrop",
                "request_info",
                "pay_prisoner_ransom",
                "trigger_incident",
                "create_quest",
                "reject_request",
                "publish_public_post",
                "exit_dialogue",
                "go_offline",
                "set_dnd"
            };

            return Array.Exists(validActions, a => a == action);
        }

        public static string NormalizeActionName(string action)
        {
            if (string.IsNullOrWhiteSpace(action))
            {
                return string.Empty;
            }

            string normalized = action.Trim().Trim('"').ToLowerInvariant().Replace("-", "_").Replace(" ", "_");
            switch (normalized)
            {
                case "none":
                    return "none";
                case "exit":
                case "exitdialogue":
                case "enddialogue":
                case "end_dialogue":
                    return "exit_dialogue";
                case "gooffline":
                case "offline":
                    return "go_offline";
                case "setdnd":
                case "dnd":
                case "do_not_disturb":
                case "donotdisturb":
                    return "set_dnd";
                case "publishpublicpost":
                case "publicpost":
                case "publish_post":
                case "social_post":
                    return "publish_public_post";
                case "requestinfo":
                case "ask_info":
                case "requestinformation":
                    return "request_info";
                case "requestvisitor":
                case "visitor_request":
                case "request_visit":
                case "visitorgroup":
                    return "request_visitor";
                case "requestraidcalleveryone":
                case "raid_call_everyone":
                case "call_everyone":
                case "call_all_factions":
                case "everyone_attack":
                case "joint_raid":
                case "all_in":
                case "спільний напад":
                case "гуртом":
                case "усіх клич":
                case "гуртом на них":
                    return "request_raid_call_everyone";
                case "requestraidwaves":
                case "raid_waves":
                case "multi_raid":
                    return "request_raid_waves";
                default:
                    return normalized;
            }
        }
    }
}
