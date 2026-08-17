namespace Ustas.RimAI.Communication.Relations.Context
{
    /// <summary>
    /// Dependencies: none.
    /// Responsibility: document the current Diplomacy snapshot contributor order.
    /// </summary>
    internal static class RelationsContextContributorOrder
    {
        public static readonly string[] DiplomacySnapshotBlocks =
        {
            "environment",
            "memory",
            "faction",
            "player_pawn",
            "royalty",
            "settlement",
            "quest"
        };
    }
}
