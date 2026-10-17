using RimWorld;
using Verse;
using System.Linq;

namespace Ustas.RimAI.Communication.Relations.PawnRpgPush
{
    /// <summary>
    /// One configured protagonist, saved by thing ID and resolved back to its pawn on load.
    /// </summary>
    public class PawnRpgProtagonistEntry : IExposable
    {
        public Pawn pawn;
        public int pawnThingId = -1;

        public bool HasConfiguredIdentifier =>
            pawn != null ||
            pawnThingId > 0;

        public static PawnRpgProtagonistEntry FromPawn(Pawn source)
        {
            return new PawnRpgProtagonistEntry
            {
                pawn = source,
                pawnThingId = source?.thingIDNumber ?? -1
            };
        }

        public Pawn TryResolvePawn()
        {
            if (pawn != null && !pawn.Destroyed)
            {
                return pawn;
            }

            if (pawnThingId <= 0)
            {
                return null;
            }

            Pawn resolved = PawnsFinder.AllMapsWorldAndTemporary_Alive
                .FirstOrDefault(p => p != null && p.thingIDNumber == pawnThingId);
            if (resolved != null)
            {
                pawn = resolved;
            }

            return pawn;
        }

        public bool Matches(Pawn other)
        {
            if (other == null)
            {
                return false;
            }

            return TryResolvePawn() == other ||
                   (pawnThingId > 0 && pawnThingId == other.thingIDNumber);
        }

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving && pawn != null && !pawn.Destroyed && !pawn.Dead)
            {
                pawnThingId = pawn.thingIDNumber;
            }

            Scribe_Values.Look(ref pawnThingId, "pawnThingId", -1);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Pawn resolved = PawnsFinder.AllMapsWorldAndTemporary_Alive
                    .FirstOrDefault(p => p != null && p.thingIDNumber == pawnThingId);
                if (resolved != null)
                {
                    pawn = resolved;
                }
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit && pawn != null && pawnThingId <= 0)
            {
                pawnThingId = pawn.thingIDNumber;
            }
        }
    }
}
