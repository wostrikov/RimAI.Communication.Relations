using RimWorld;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Core
{
    public static class MapUtility
    {
        public static bool IsOrbitalBaseMap(Map map)
        {
            if (map?.Parent == null)
            {
                return false;
            }

            string defName = map.Parent.def?.defName ?? string.Empty;
            return defName.Contains("OrbitalBase") ||
                   defName.Contains("SpaceSite") ||
                   defName.Contains("OrbitalTrade") ||
                   defName.Contains("Orbital");
        }
    }
}
