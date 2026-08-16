using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Guards
{
    /// <summary>
    /// Safely accesses Quest.InvolvedFactions to guard against
    /// third-party mod QuestParts that throw during lazy iteration.
    /// </summary>
    public static class QuestInvolvedFactionsGuard
    {
        private static readonly HashSet<int> WarnedQuestIds = new HashSet<int>();

        public static bool HasInvolvedFaction(Quest quest, Faction faction)
        {
            if (quest == null || faction == null)
            {
                return false;
            }

            try
            {
                return quest.InvolvedFactions != null && quest.InvolvedFactions.Contains(faction);
            }
            catch (Exception ex)
            {
                WarnOnce(quest, ex);
                return false;
            }
        }

        public static List<Faction> GetInvolvedFactionsSafe(Quest quest)
        {
            if (quest == null)
            {
                return new List<Faction>();
            }

            try
            {
                List<Faction> result = new List<Faction>();
                IEnumerable<Faction> factions = quest.InvolvedFactions;
                if (factions == null)
                {
                    return result;
                }

                foreach (Faction faction in factions)
                {
                    if (faction != null)
                    {
                        result.Add(faction);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                WarnOnce(quest, ex);
                return new List<Faction>();
            }
        }

        private static void WarnOnce(Quest quest, Exception ex)
        {
            if (WarnedQuestIds.Add(quest.id))
            {
                Log.Warning(
                    $"[RimAI.Relations][QuestGuard] Third-party mod broke InvolvedFactions for " +
                    $"quest #{quest.id} (\"{quest.name}\"): {ex.GetType().Name}: {ex.Message}. " +
                    $"Diplomacy prompt continues without this quest data.");
            }
        }
    }
}
