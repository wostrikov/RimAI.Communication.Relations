namespace Ustas.RimAI.Communication.Relations.Settings
{
    /// <summary>
    /// Authoritative goodwill floors/ceilings for player diplomacy actions.
    /// </summary>
    public static class RelationsGoodwillGatePolicy
    {
        public static bool AllowAid(int playerGoodwill, int minGoodwillForAid) =>
            playerGoodwill >= minGoodwillForAid;

        public static bool AllowWarDeclaration(int playerGoodwill, int maxGoodwillForWar) =>
            playerGoodwill <= maxGoodwillForWar;
    }
}
