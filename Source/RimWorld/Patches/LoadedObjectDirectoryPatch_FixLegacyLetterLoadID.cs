using HarmonyLib;
using Ustas.RimAI.Communication.Relations.NpcDialogue;
using Ustas.RimAI.Communication.Relations.PawnRpgPush;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Patches
{
    /// <summary>
    /// Give one of our letters a real identity before the loader files it away.
    ///
    /// A letter saved by a build that never assigned an ID comes back with ID 0
    /// and reports itself as "Letter_0", so two of them collide in the loaded
    /// object directory and again in the next save.
    ///
    /// This whole patch was inert until now. It reached the identity field
    /// through AccessTools.Field(typeof(Letter), "loadID"), and Verse.Letter has
    /// no field of that name - the identity is the public ID. The lookup
    /// returned null, the read produced 0 for every letter including correct
    /// ones, and the write did nothing while logging that it had. Reading a
    /// public field directly removes the way that could happen again.
    /// </summary>
    [HarmonyPatch(typeof(LoadedObjectDirectory), "RegisterLoaded")]
    public static class LoadedObjectDirectoryPatch_FixLegacyLetterLoadID
    {
        [HarmonyPrefix]
        static bool FixLegacyLetterLoadID(ILoadReferenceable __0, LoadedObjectDirectory __instance)
        {
            if (__0 is ChoiceLetter_NpcInitiatedDialogue npcLetter && npcLetter.ID <= 0)
            {
                return Reidentify(__instance, npcLetter,
                    ChoiceLetter_NpcInitiatedDialogue.AssignNextUniqueLoadID(), "NpcInitiatedDialogue");
            }

            if (__0 is ChoiceLetter_PawnRpgInitiatedDialogue rpgLetter && rpgLetter.ID <= 0)
            {
                return Reidentify(__instance, rpgLetter,
                    ChoiceLetter_PawnRpgInitiatedDialogue.AssignNextUniqueLoadID(), "PawnRpgInitiatedDialogue");
            }

            return true;
        }

        static bool Reidentify(LoadedObjectDirectory directory, Letter letter, int assigned, string what)
        {
            letter.ID = assigned;
            RegisterDirectly(directory, letter.GetUniqueLoadID(), letter);
            // A warning rather than chatter: a save carrying letters with no
            // identity is a save that was written by a broken build, and the
            // player is entitled to know their letters were renumbered.
            Log.Warning($"[RimAI.Relations] Re-identified {what} with no load ID -> {assigned}");
            return false;
        }

        private static void RegisterDirectly(LoadedObjectDirectory dir, string key, ILoadReferenceable obj)
        {
            var allObjectsField = AccessTools.Field(typeof(LoadedObjectDirectory), "allObjectsByUsername");
            if (allObjectsField?.GetValue(dir) is System.Collections.IDictionary dict)
            {
                dict[key] = obj;
            }
        }
    }
}
