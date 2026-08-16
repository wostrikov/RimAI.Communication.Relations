using UnityEngine;
using Verse;

namespace Ustas.RimAI.Communication.Relations.UI
{
    /// <summary>/// Dependencies: dialogue window layout constants and pawn portrait renderer.
 /// Responsibility: centralize PawnRPG portrait layout so other overlays can share the same anchors.
 ///</summary>
    public partial class Dialog_RPGPawnDialogue
    {
        private const float PortraitHorizontalMargin = 50f;
        private const float PortraitVerticalOverlap = 150f;

        private void DrawPortraits(Rect inRect)
        {
            Rect targetPortraitRect = GetTargetPortraitRect(inRect);
            GUI.color = new Color(1f, 1f, 1f, globalFadeAlpha * targetFadeAlpha * inspectPaneAlpha);
            DrawPawnPortrait(targetPortraitRect, target, false);

            DrawInitiatorPortraitWithDrag(inRect);

            GUI.color = new Color(1f, 1f, 1f, globalFadeAlpha);
        }

        private Rect GetTargetPortraitRect(Rect inRect)
        {
            float w = TargetPortraitWidth;
            float h = CappedHeight(TargetPortraitHeight, inRect);
            float overlap = Mathf.Min(PortraitVerticalOverlap, h * 0.3f);
            float topY = inRect.height - DialogueBoxHeight - h + overlap;
            return new Rect(PortraitHorizontalMargin, topY, w, h);
        }

        private Rect GetInitiatorPortraitRect(Rect inRect)
        {
            float w = InitiatorPortraitWidth;
            float h = CappedHeight(InitiatorPortraitHeight, inRect);
            float overlap = Mathf.Min(PortraitVerticalOverlap, h * 0.3f);
            float topY = inRect.height - DialogueBoxHeight - h + overlap;
            return new Rect(inRect.width - w - PortraitHorizontalMargin, topY, w, h);
        }

        private static float CappedHeight(float desired, Rect inRect)
        {
            float maxH = inRect.height - DialogueBoxHeight + PortraitVerticalOverlap;
            return Mathf.Min(desired, maxH);
        }
    }
}
