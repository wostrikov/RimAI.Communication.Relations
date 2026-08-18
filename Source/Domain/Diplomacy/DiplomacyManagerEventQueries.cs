using RimWorld;

namespace Ustas.RimAI.Communication.Relations.DiplomacySystem
{
        internal sealed class DiplomacyManagerEventQueries : GameComponent_DiplomacyManagerCollaborator
    {
        internal DiplomacyManagerEventQueries(GameComponent_DiplomacyManager owner) : base(owner)
        {
        }


        public bool HasCaravanDispatchedNow(Faction faction)
        {
            return GameComponent_DelayedEventManager.Instance?.HasCaravanDispatchedNow(faction) ?? false;
        }

        public bool HasRaidScheduledNow(Faction faction)
        {
            return GameComponent_DelayedEventManager.Instance?.HasRaidScheduledNow(faction) ?? false;
        }
        }

}
