using Verse;

namespace Ustas.RimAI.Communication.Relations.Guards
{
    /// <summary>
    /// Validates tile IDs against the current world grid to prevent
    /// IndexOutOfRangeException after mid-game save loads where
    /// WorldObject.Tile can hold stale values.
    /// </summary>
    public static class WorldTileGuard
    {
        public static bool IsValidTile(int tile)
        {
            return Find.WorldGrid != null
                && tile >= 0
                && tile < Find.WorldGrid.TilesCount;
        }
    }
}
