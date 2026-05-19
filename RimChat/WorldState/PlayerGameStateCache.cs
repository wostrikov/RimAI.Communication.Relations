using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
        private int _eligibleColonistCount;
        private int _downedColonistCount;
        private bool _hasActiveHomeAlerts;
        private readonly HashSet<Faction> _activeFactionsOnPlayerMaps = new HashSet<Faction>();

        public bool HasHostiles => _hasHostiles;
        public bool HasDrafted => _hasDrafted;
        public bool HasHiveThreat => _hasHiveThreat;
        public int EligibleColonistCount => _eligibleColonistCount;
        public int DownedColonistCount => _downedColonistCount;
        public bool HasActiveHomeAlerts => _hasActiveHomeAlerts;
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
            _eligibleColonistCount = 0;
            _downedColonistCount = 0;
            _hasActiveHomeAlerts = false;
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

                        if (PawnDialogueRoutingPolicy.IsRpgDialogueEligibleRace(p)
                            && p.Spawned && !p.Dead && !p.Destroyed && !p.Downed && !p.IsPrisoner)
                        {
                            _eligibleColonistCount++;
                        }

                        if (p.Downed && p.Spawned && !p.Dead && !p.Destroyed)
                        {
                            _downedColonistCount++;
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
                        for (int i = 0; i < hives.Count; i++)
                        {
                            if (!hives[i].Destroyed)
                            {
                                _hasHiveThreat = true;
                                break;
                            }
                        }
                    }
                }
            }

            _hasActiveHomeAlerts = false;
            try
            {
                AlertsReadout readout = Find.Alerts;
                if (readout != null)
                {
                    FieldInfo field = typeof(AlertsReadout).GetField("activeAlerts", BindingFlags.Instance | BindingFlags.NonPublic);
                    IEnumerable alerts = field?.GetValue(readout) as IEnumerable;
                    if (alerts != null)
                    {
                        foreach (object alertItem in alerts)
                        {
                            _hasActiveHomeAlerts = true;
                            break;
                        }
                    }
                }
            }
            catch
            {
                // Silently ignore reflection failures
            }
        }
    }
}
