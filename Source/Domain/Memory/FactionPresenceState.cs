using System.Linq;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Memory
{
    public enum FactionPresenceStatus
    {
        Online = 0,
        Offline = 1,
        DoNotDisturb = 2
    }

    public class FactionPresenceState : IExposable
    {
        public Faction faction;
        public FactionPresenceStatus status = FactionPresenceStatus.Online;
        public int lastResolvedTick = 0;
        public int cacheUntilTick = 0;
        public int forcedOfflineUntilTick = 0;
        public int doNotDisturbUntilTick = 0;
        public string lastReason = "";

        public FactionPresenceState()
        {
        }

        public FactionPresenceState(Faction faction)
        {
            this.faction = faction;
        }

        public bool IsForcedOffline(int currentTick)
        {
            return forcedOfflineUntilTick > currentTick;
        }

        public bool IsDoNotDisturb(int currentTick)
        {
            return doNotDisturbUntilTick > currentTick;
        }

        public bool IsCacheValid(int currentTick)
        {
            return lastResolvedTick > 0 && cacheUntilTick > currentTick;
        }

        public void ExposeData()
        {
            string factionId = faction?.GetUniqueLoadID() ?? string.Empty;
            Scribe_Values.Look(ref factionId, "factionId", string.Empty);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (!string.IsNullOrEmpty(factionId))
                {
                    faction = Find.FactionManager.AllFactions.FirstOrDefault(f => f.GetUniqueLoadID() == factionId);
                }
                // If factionId is empty, faction remains null and will be cleaned up
                // by CleanupInvalidPresenceStates() in LoadedGame().
            }
            Scribe_Values.Look(ref status, "status", FactionPresenceStatus.Online);
            Scribe_Values.Look(ref lastResolvedTick, "lastResolvedTick", 0);
            Scribe_Values.Look(ref cacheUntilTick, "cacheUntilTick", 0);
            Scribe_Values.Look(ref forcedOfflineUntilTick, "forcedOfflineUntilTick", 0);
            Scribe_Values.Look(ref doNotDisturbUntilTick, "doNotDisturbUntilTick", 0);
            Scribe_Values.Look(ref lastReason, "lastReason", "");
        }
    }
}
