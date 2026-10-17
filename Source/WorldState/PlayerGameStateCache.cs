using Ustas.RimAI.Communication.Relations.Diagnostics;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Communication.Relations.WorldState
{
    /// <summary>
    /// Responsibility: one shared answer to "is the player busy" for the faction push's
    /// busy detection - are hostiles on a player map, is any colonist drafted - refreshed
    /// by a single pawn scan at most every RefreshIntervalTicks.
    /// </summary>
    internal sealed class PlayerGameStateCache
    {
        private const int RefreshIntervalTicks = 100;

        public static PlayerGameStateCache Instance { get; } = new PlayerGameStateCache();

        private int lastRefreshTick = -1;
        private bool _hasHostiles;
        private bool _hasDrafted;

        public bool HasHostiles => _hasHostiles;
        public bool HasDrafted => _hasDrafted;

        public void EnsureFresh(int currentTick)
        {
            if (currentTick - lastRefreshTick < RefreshIntervalTicks)
            {
                return;
            }

            lastRefreshTick = currentTick;
            Refresh();
        }

        private void Refresh()
        {
            using var _ = PerfScope.Measure("PlayerGameState.Refresh");
            _hasHostiles = false;
            _hasDrafted = false;

            if (Find.Maps == null)
            {
                return;
            }

            foreach (Map map in Find.Maps)
            {
                if (map?.mapPawns?.AllPawnsSpawned == null)
                {
                    continue;
                }

                foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
                {
                    if (p == null || p.Dead || p.Destroyed)
                    {
                        continue;
                    }

                    if (p.Faction == Faction.OfPlayer)
                    {
                        if (p.Drafted)
                        {
                            _hasDrafted = true;
                        }

                        continue;
                    }

                    if (p.HostileTo(Faction.OfPlayer))
                    {
                        _hasHostiles = true;
                    }
                }
            }
        }
    }
}
