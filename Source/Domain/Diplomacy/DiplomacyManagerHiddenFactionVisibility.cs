using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Communication.Relations.DiplomacySystem
{
    /// <summary>
    /// Dependencies: Verse.Scribe, RimWorld.Faction.
    /// Responsibility: persist and expose save-scoped hidden faction visibility overrides for diplomacy UI.
    /// </summary>
        internal sealed class DiplomacyManagerHiddenFactionVisibility : GameComponent_DiplomacyManagerCollaborator
    {
        internal DiplomacyManagerHiddenFactionVisibility(GameComponent_DiplomacyManager owner) : base(owner)
        {
        }


        internal HashSet<Faction> manuallyVisibleHiddenFactions
        {
            get => Owner.manuallyVisibleHiddenFactions;
            set => Owner.manuallyVisibleHiddenFactions = value;
        }

        public List<Faction> GetManuallyVisibleHiddenFactions()
        {
            Owner.CleanupManuallyVisibleHiddenFactions();
            return manuallyVisibleHiddenFactions.ToList();
        }

        public bool IsHiddenFactionManuallyVisible(Faction faction)
        {
            if (faction == null)
            {
                return false;
            }

            Owner.CleanupManuallyVisibleHiddenFactions();
            return manuallyVisibleHiddenFactions.Contains(faction);
        }

        public void SetManuallyVisibleHiddenFactions(IEnumerable<Faction> factions)
        {
            manuallyVisibleHiddenFactions.Clear();
            if (factions == null)
            {
                return;
            }

            foreach (Faction faction in factions)
            {
                if (GameComponent_DiplomacyManager.IsSelectableHiddenFaction(faction))
                {
                    manuallyVisibleHiddenFactions.Add(faction);
                }
            }
        }

        internal void EnsureHiddenFactionVisibilityState()
        {
            manuallyVisibleHiddenFactions ??= new HashSet<Faction>();
            Owner.CleanupManuallyVisibleHiddenFactions();
        }

        internal void CleanupManuallyVisibleHiddenFactions()
        {
            if (manuallyVisibleHiddenFactions == null || manuallyVisibleHiddenFactions.Count == 0)
            {
                return;
            }

            manuallyVisibleHiddenFactions.RemoveWhere(faction => !GameComponent_DiplomacyManager.IsSelectableHiddenFaction(faction));
        }

        internal static bool IsSelectableHiddenFaction(Faction faction)
        {
            return faction != null &&
                   !faction.IsPlayer &&
                   !faction.defeated &&
                   faction.Hidden;
        }
        }

}
