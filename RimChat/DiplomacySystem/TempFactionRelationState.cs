using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimChat.DiplomacySystem
{
    /// <summary>
    /// Responsibility: track temporary faction relation overrides for CallEveryone windows.
    /// Uses SetRelationDirect rather than Harmony patching for reliable cross-faction peace.
    /// </summary>
    public class TempFactionRelationState : IExposable
    {
        public Dictionary<string, FactionRelationKind> originalRelations = new Dictionary<string, FactionRelationKind>();
        public int restoreAtTick;

        public void ExposeData()
        {
            Scribe_Values.Look(ref restoreAtTick, "restoreAtTick", 0);
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                var savable = new List<string>();
                var kinds = new List<int>();
                foreach (var kv in originalRelations)
                {
                    savable.Add(kv.Key);
                    kinds.Add((int)kv.Value);
                }
                Scribe_Collections.Look(ref savable, "tempPeaceKeys", LookMode.Value);
                Scribe_Collections.Look(ref kinds, "tempPeaceKinds", LookMode.Value);
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                var savable = new List<string>();
                var kinds = new List<int>();
                Scribe_Collections.Look(ref savable, "tempPeaceKeys", LookMode.Value);
                Scribe_Collections.Look(ref kinds, "tempPeaceKinds", LookMode.Value);
                originalRelations.Clear();
                if (savable != null && kinds != null)
                {
                    for (int i = 0; i < savable.Count && i < kinds.Count; i++)
                        originalRelations[savable[i]] = (FactionRelationKind)kinds[i];
                }
            }
        }
    }
}
