using UnityEngine;
using Verse;

namespace Ustas.RimAI.Communication.Relations.UI
{
    /// <summary>/// Dependencies: dialogue window layout constants and pawn portrait renderer.
 /// Responsibility: centralize PawnRPG portrait layout so other overlays can share the same anchors.
 ///</summary>
        internal sealed class RPGPawnDialoguePortraits : Dialog_RPGPawnDialogueCollaborator
    {
        internal RPGPawnDialoguePortraits(Dialog_RPGPawnDialogue owner) : base(owner)
        {
        }


        internal const float PortraitHorizontalMargin = 50f;
        internal const float PortraitVerticalOverlap = 150f;

        internal void DrawPortraits(Rect inRect)
        {
            Rect targetPortraitRect = Owner.GetTargetPortraitRect(inRect);
            GUI.color = new Color(1f, 1f, 1f, globalFadeAlpha * targetFadeAlpha * inspectPaneAlpha);
            Owner.DrawPawnPortrait(targetPortraitRect, target, false);

            Owner.DrawInitiatorPortraitWithDrag(inRect);

            GUI.color = new Color(1f, 1f, 1f, globalFadeAlpha);
        }

        internal Rect GetTargetPortraitRect(Rect inRect)
        {
            float w = TargetPortraitWidth;
            float h = Dialog_RPGPawnDialogue.CappedHeight(TargetPortraitHeight, inRect);
            float overlap = Mathf.Min(PortraitVerticalOverlap, h * 0.3f);
            float topY = inRect.height - DialogueBoxHeight - h + overlap;
            return new Rect(PortraitHorizontalMargin, topY, w, h);
        }

        internal Rect GetInitiatorPortraitRect(Rect inRect)
        {
            float w = InitiatorPortraitWidth;
            float h = Dialog_RPGPawnDialogue.CappedHeight(InitiatorPortraitHeight, inRect);
            float overlap = Mathf.Min(PortraitVerticalOverlap, h * 0.3f);
            float topY = inRect.height - DialogueBoxHeight - h + overlap;
            return new Rect(inRect.width - w - PortraitHorizontalMargin, topY, w, h);
        }

        internal static float CappedHeight(float desired, Rect inRect)
        {
            float maxH = inRect.height - DialogueBoxHeight + PortraitVerticalOverlap;
            return Mathf.Min(desired, maxH);
        }
        }

}
