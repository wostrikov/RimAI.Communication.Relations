using Ustas.RimAI.Communication.Relations.AI;

namespace Ustas.RimAI.Communication.Relations.Settings
{
    /// <summary>
    /// Token accounting is player-action only: Diplomacy and RPG player
    /// conversations. Proactive/unknown generation must not increment usage.
    /// </summary>
    public static class RelationsTokenAccountingPolicy
    {
        public static bool ShouldTrack(DialogueUsageChannel usageChannel) =>
            usageChannel == DialogueUsageChannel.Diplomacy || usageChannel == DialogueUsageChannel.Rpg;
    }
}
