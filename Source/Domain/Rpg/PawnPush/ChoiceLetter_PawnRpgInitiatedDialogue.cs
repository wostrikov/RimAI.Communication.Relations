using System.Collections.Generic;
using Verse;

namespace Ustas.RimAI.Communication.Relations.PawnRpgPush
{
    /// <summary>
    /// A letter from the removed pawn-initiated dialogue push ("X wants to talk with Y").
    ///
    /// Nothing posts these any more. The class stays, under the same full name, because
    /// saves hold such letters in the letter stack and in the archive by class name, and a
    /// letter class that cannot be found is a load error. An old letter keeps the title and
    /// text the base class saved and offers only Close. The npcLoadId and playerLoadId it
    /// also carries are no longer read; Scribe skips nodes nobody asks for.
    /// GameComponent_PawnRpgDialoguePushManager takes any that are still on the letter
    /// stack off it after a load.
    /// </summary>
    public class ChoiceLetter_PawnRpgInitiatedDialogue : ChoiceLetter
    {
        private static int nextUniqueLoadID = 800001;

        /// <summary>
        /// Hand out an ID for a letter that was saved without one.
        /// LoadedObjectDirectoryPatch_FixLegacyLetterLoadID calls this before the letter is
        /// registered, so two such letters do not both claim "Letter_0" while a save loads.
        /// </summary>
        public static int AssignNextUniqueLoadID()
        {
            return nextUniqueLoadID++;
        }

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                yield return Option_Close;
            }
        }
    }
}
