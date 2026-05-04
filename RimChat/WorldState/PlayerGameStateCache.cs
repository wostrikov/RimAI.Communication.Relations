using System.Collections.Generic;
using RimChat.Dialogue;
using RimChat.Util;
using RimWorld;
using Verse;

namespace RimChat.WorldState
{
    /// <summary>
    /// Responsibility: single-source-of-truth cache for player game state queries
    /// (hostiles, drafted, hive threat, active NPC factions) that multiple
    /// GameComponents need every ~600 ticks. One pawn scan per refresh replaces
    /// 6 independent scans.
    /// </summary>
    internal sealed class PlayerGameStateCache
    {
        private const int RefreshIntervalTicks = 100;

        public static PlayerGameStateCache Instance { get; } = new PlayerGameStateCache();

        private int lastRefreshTick = -1;
        private bool _hasHostiles;
        private bool _hasDrafted;
        private bool _hasHiveThreat;
        private readonly HashSet<Faction> _activeFactionsOnPlayerMaps = new HashSet<Faction>();

        public bool HasHostiles => _hasHostiles;
        public bool HasDrafted => _hasDrafted;
        public bool HasHiveThreat => _hasHiveThreat;
        public IReadOnlyCollection<Faction> ActiveFactionsOnPlayerMaps => _activeFactionsOnPlayerMaps;

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
            _hasHiveThreat = false;
            _activeFactionsOnPlayerMaps.Clear();

            if (Find.Maps == null)
            {
                return;
            }

            ThingDef hiveDef = DefDatabase<ThingDef>.GetNamedSilentFail("Hive");

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

                    if (map.IsPlayerHome
                        && p.Spawned
                        && p.Faction != null
                        && !p.Faction.defeated
                        && !(p.Faction.def?.hidden ?? true)
                        && PawnDialogueRoutingPolicy.IsRpgDialogueEligibleRace(p))
                    {
                        _activeFactionsOnPlayerMaps.Add(p.Faction);
                    }
                }

                if (map.IsPlayerHome && hiveDef != null)
                {
                    List<Thing> hives = map.listerThings?.ThingsOfDef(hiveDef);
                    if (hives != null && hives.Count > 0)
                    {
                        _hasHiveThreat = true;
                    }
                }
            }
        }
    }
}
