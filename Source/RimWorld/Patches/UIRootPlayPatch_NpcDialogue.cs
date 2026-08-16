using HarmonyLib;
using Ustas.RimAI.Communication.Relations.NpcDialogue;
using Ustas.RimAI.Communication.Relations.PawnRpgPush;
using RimWorld;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Patches
{
    /// <summary>/// Dependencies: Verse.UIRoot_Play, Verse.Event.
 /// Responsibility: Capture left-click cadence for both proactive channels busy detection.
 ///</summary>
    [HarmonyPatch(typeof(UIRoot_Play), "UIRootOnGUI")]
    public static class UIRootPlayPatch_NpcDialogue
    {
        private static void Postfix()
        {
            Event current = Event.current;
            if (current == null || current.type != EventType.MouseDown || current.button != 0)
            {
                return;
            }

            GameComponent_NpcDialoguePushManager.Instance?.RegisterPlayerLeftClick();
            GameComponent_PawnRpgDialoguePushManager.Instance?.RegisterPlayerLeftClick();
        }
    }
}
