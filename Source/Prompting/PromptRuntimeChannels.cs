namespace Ustas.RimAI.Communication.Relations.Prompting
{
    /// <summary>
    /// Dependencies: none.
    /// Responsibility: resolve runtime Diplomacy/RPG prompt-channel ids without touching persistence or template engines.
    /// </summary>
    internal static class PromptRuntimeChannels
    {
        public const string DiplomacyDialogue = "diplomacy_dialogue";
        public const string ProactiveDiplomacyDialogue = "proactive_diplomacy_dialogue";
        public const string DiplomacyStrategy = "diplomacy_strategy";
        public const string RpgDialogue = "rpg_dialogue";
        public const string ProactiveRpgDialogue = "proactive_rpg_dialogue";
        public const string PlayerNegotiatorValueKey = "pawn.player_negotiator";

        public static string ResolveDiplomacy(bool isProactive)
        {
            return isProactive ? ProactiveDiplomacyDialogue : DiplomacyDialogue;
        }

        public static string ResolveRpg(bool isProactive)
        {
            return isProactive ? ProactiveRpgDialogue : RpgDialogue;
        }
    }
}
