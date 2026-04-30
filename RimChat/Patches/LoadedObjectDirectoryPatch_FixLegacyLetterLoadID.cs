using HarmonyLib;
using RimChat.NpcDialogue;
using RimChat.PawnRpgPush;
using Verse;

namespace RimChat.Patches
{
    /// <summary>
    /// Dependencies: Verse.LoadedObjectDirectory, RimChat ChoiceLetter subclasses.
    /// Responsibility: Fix legacy ChoiceLetter objects that have loadID=0 by assigning
    /// unique IDs and manually registering them, preventing "Letter_0" duplicate key crashes.
    /// </summary>
    [HarmonyPatch(typeof(LoadedObjectDirectory), "RegisterLoaded")]
    public static class LoadedObjectDirectoryPatch_FixLegacyLetterLoadID
    {
        private static readonly System.Reflection.FieldInfo LetterLoadIDField =
            AccessTools.Field(typeof(Letter), "loadID");

        [HarmonyPrefix]
        static bool FixLegacyLetterLoadID(ILoadReferenceable __0, LoadedObjectDirectory __instance)
        {
            if (__0 is ChoiceLetter_NpcInitiatedDialogue npcLetter)
            {
                int current = LetterLoadIDField != null
                    ? (int)LetterLoadIDField.GetValue(npcLetter)
                    : 0;
                if (current <= 0)
                {
                    int assigned = ChoiceLetter_NpcInitiatedDialogue.AssignNextUniqueLoadID();
                    LetterLoadIDField?.SetValue(npcLetter, assigned);
                    string correctKey = npcLetter.GetUniqueLoadID();
                    RegisterDirectly(__instance, correctKey, __0);
                    Log.Warning(
                        $"[RimChat] Pre-register fix: NpcInitiatedDialogue loadID={current} -> {assigned}, key={correctKey}");
                    return false; // skip original RegisterLoaded
                }
            }
            else if (__0 is ChoiceLetter_PawnRpgInitiatedDialogue rpgLetter)
            {
                int current = LetterLoadIDField != null
                    ? (int)LetterLoadIDField.GetValue(rpgLetter)
                    : 0;
                if (current <= 0)
                {
                    int assigned = ChoiceLetter_PawnRpgInitiatedDialogue.AssignNextUniqueLoadID();
                    LetterLoadIDField?.SetValue(rpgLetter, assigned);
                    string correctKey = rpgLetter.GetUniqueLoadID();
                    RegisterDirectly(__instance, correctKey, __0);
                    Log.Warning(
                        $"[RimChat] Pre-register fix: PawnRpgInitiatedDialogue loadID={current} -> {assigned}, key={correctKey}");
                    return false;
                }
            }
            return true; // proceed with original RegisterLoaded
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
