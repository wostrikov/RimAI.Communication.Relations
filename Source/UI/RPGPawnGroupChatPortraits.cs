using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

using GroupChatParticipant = Ustas.RimAI.Communication.Relations.UI.Dialog_RPGPawnGroupChat.GroupChatParticipant;

namespace Ustas.RimAI.Communication.Relations.UI
{
        internal sealed class RPGPawnGroupChatPortraits : Dialog_RPGPawnGroupChatCollaborator
    {
        internal RPGPawnGroupChatPortraits(Dialog_RPGPawnGroupChat owner) : base(owner)
        {
        }


        internal void DrawPortraits(Rect inRect)
        {
            List<Rect> portraitRects = Owner.CalculateCascadingRects(inRect);
            int currentIdx = Owner.GetActiveSpeakerIndex();
            var drawOrder = Owner.BuildDrawOrder(currentIdx, portraitRects.Count);
            Owner.UpdateRoundTransition(currentIdx);

            for (int orderPos = 0; orderPos < drawOrder.Count; orderPos++)
            {
                int i = drawOrder[orderPos];
                Rect rect = portraitRects[i];
                GroupChatParticipant participant = participants[i];
                bool isCurrentSpeaker = (i == currentIdx);

                float progress = Owner.GetRoundTransitionProgress();
                float currentScale = isCurrentSpeaker ? Mathf.Lerp(1f, 1.10f, progress) : Mathf.Lerp(1.10f, 1f, progress);
                float yLift = isCurrentSpeaker ? Mathf.Lerp(0f, 20f, progress) : Mathf.Lerp(20f, 0f, progress);

                float alpha = isCurrentSpeaker ? 1f : 0.6f;
                GUI.color = new Color(1f, 1f, 1f, globalFadeAlpha * alpha);

                Matrix4x4 savedMatrix = GUI.matrix;
                if (Mathf.Abs(currentScale - 1f) > 0.002f || Mathf.Abs(yLift) > 0.5f)
                {
                    Vector2 pivot = rect.center;
                    GUIUtility.ScaleAroundPivot(new Vector2(currentScale, currentScale), pivot);
                    rect.y -= yLift;
                }

                bool shouldRender = isCurrentSpeaker || Owner.NeedsPortraitRefresh(i);
                if (shouldRender)
                {
                    bool created;
                    RenderTexture rt = participant.PortraitRT;
                    Dialog_RPGPawnDialogue.DrawPawnPortrait(rect, participant.Pawn, false, ref rt, out created);
                    if (created || shouldRender)
                    {
                        participant.PortraitRT = rt;
                        participants[i] = participant;
                        _lastPortraitRenderTime[i] = Time.realtimeSinceStartup;
                    }
                }
                else if (participant.PortraitRT != null)
                {
                    GUI.DrawTexture(rect, participant.PortraitRT, ScaleMode.StretchToFill, true);
                }

                GUI.matrix = savedMatrix;

                if (Widgets.ButtonInvisible(rect))
                    Dialog_RPGPawnDialogue.ShowPawnMenuStatic(participant.Pawn);

                GUI.color = Color.white;
                Owner.DrawParticipantNameLabel(rect, participant.DisplayName, isCurrentSpeaker);
            }

            Owner.DrawInitiatorPortrait(inRect);
        }

        internal int GetActiveSpeakerIndex()
        {
            return (!isPlayerTurn && currentSpeakerIndex >= 0 && currentSpeakerIndex < participants.Count)
                ? currentSpeakerIndex : -1;
        }

        internal List<int> BuildDrawOrder(int speakerIndex, int count)
        {
            var order = new List<int>();
            for (int i = 0; i < count; i++)
                if (i != speakerIndex) order.Add(i);
            if (speakerIndex >= 0 && speakerIndex < count)
                order.Add(speakerIndex);
            return order;
        }

        internal void UpdateRoundTransition(int newSpeakerIdx)
        {
            if (newSpeakerIdx != previousSpeakerIndex && newSpeakerIdx >= 0)
            {
                previousSpeakerIndex = newSpeakerIdx;
                roundTransitionTime = Time.time;
            }
        }

        internal float GetRoundTransitionProgress()
        {
            float elapsed = Time.time - roundTransitionTime;
            return Mathf.Clamp01(elapsed / RoundTransitionDuration);
        }

        internal bool NeedsPortraitRefresh(int index)
        {
            if (!_lastPortraitRenderTime.TryGetValue(index, out float lastTime))
                return true;
            return Time.realtimeSinceStartup - lastTime >= NonSpeakerRenderInterval;
        }

        internal List<Rect> CalculateCascadingRects(Rect inRect)
        {
            var rects = new List<Rect>();
            int count = participants.Count;
            if (count == 0) return rects;

            float pw = Dialog_RPGPawnDialogue.PortraitWidth;
            float ph = Dialog_RPGPawnDialogue.PortraitHeight;
            float overlapWidth = pw * PortraitOverlapRatio;
            float startX = PortraitLeftMargin;
            float portraitTop = inRect.height - Dialog_RPGPawnDialogue.DialogueBoxHeight - ph + PortraitVerticalOverlap;

            for (int i = 0; i < count; i++)
            {
                float x = startX + i * (pw - overlapWidth);
                rects.Add(new Rect(x, portraitTop, pw, ph));
            }

            return rects;
        }

        // ── Initiator portrait (right side) ──

        internal void DrawInitiatorPortrait(Rect inRect)
        {
            float initWidth = Dialog_RPGPawnDialogue.PortraitWidth;
            float initHeight = Dialog_RPGPawnDialogue.PortraitHeight;
            float portraitTop = inRect.height - Dialog_RPGPawnDialogue.DialogueBoxHeight - initHeight + PortraitVerticalOverlap;
            float portraitX = inRect.width - initWidth - 50f;
            Rect initRect = new Rect(portraitX, portraitTop, initWidth, initHeight);

            bool isPlayerSpeaking = isPlayerTurn && !isTyping;
            float alpha = (globalFadeAlpha > 0.5f || isPlayerSpeaking) ? 1f : 0.4f;
            GUI.color = new Color(1f, 1f, 1f, alpha);

            bool created;
            Dialog_RPGPawnDialogue.DrawPawnPortrait(initRect, initiator, true, ref Owner.initiatorPortraitRT, out created);

            // Invisible button: click to show forced-action menu
            if (Widgets.ButtonInvisible(initRect))
            {
                Owner.ShowForcedActionMenu();
            }

            GUI.color = Color.white;
        }

        // ── Forced action menu (click initiator portrait) ──

        internal void ShowForcedActionMenu()
        {
            // Find a valid target: current speaker, or first valid participant
            int targetIdx = Owner.GetActiveSpeakerIndex();
            if (targetIdx < 0) targetIdx = 0;
            if (targetIdx >= participants.Count) return;

            Pawn targetPawn = participants[targetIdx].Pawn;
            if (targetPawn == null) return;

            var options = new List<FloatMenuOption>();

            if (!Dialog_RPGPawnGroupChat.HasLoveRelation(targetPawn, initiator))
                Owner.AddMenuOption(options, "RimChat_DragMenu_Romance", "RomanceAttempt", targetPawn);
            if (!Dialog_RPGPawnGroupChat.HasSpouseRelation(targetPawn, initiator))
                Owner.AddMenuOption(options, "RimChat_DragMenu_Marry", "MarriageProposal", targetPawn);
            if (Dialog_RPGPawnGroupChat.HasLoveRelation(targetPawn, initiator))
                Owner.AddMenuOption(options, "RimChat_DragMenu_Breakup", "Breakup", targetPawn);
            if (Dialog_RPGPawnGroupChat.HasSpouseRelation(targetPawn, initiator))
                Owner.AddMenuOption(options, "RimChat_DragMenu_Divorce", "Divorce", targetPawn);
            Owner.AddMenuOption(options, "RimChat_DragMenu_Date", "Date", targetPawn);
            Owner.AddMenuOption(options, "RimChat_DragMenu_Gift", "TryGainMemory", targetPawn);
            if (targetPawn.IsPrisoner)
            {
                Owner.AddMenuOption(options, "RimChat_DragMenu_ReduceResist", "ReduceResistance", targetPawn);
                Owner.AddMenuOption(options, "RimChat_DragMenu_ReduceWill", "ReduceWill", targetPawn);
            }
            if (targetPawn.Faction != Faction.OfPlayer)
                Owner.AddMenuOption(options, "RimChat_DragMenu_Recruit", "Recruit", targetPawn);
            if (ModsConfig.IdeologyActive && targetPawn?.ideo != null)
            {
                Owner.AddMenuOption(options, "RimChat_DragMenu_ConvertIdeo", "ConvertIdeology", targetPawn);
                Owner.AddMenuOption(options, "RimChat_DragMenu_AdjCertainty", "AdjustCertainty", targetPawn);
            }
            Owner.AddMenuOption(options, "RimChat_DragMenu_Inspiration", "GrantInspiration", targetPawn);
            Owner.AddMenuOption(options, "RimChat_DragMenu_Incident", "TriggerIncident", targetPawn);

            if (options.Count > 0)
                Find.WindowStack.Add(new FloatMenu(options));
        }

        internal void AddMenuOption(List<FloatMenuOption> options, string labelKey, string actionName, Pawn targetPawn)
        {
            options.Add(new FloatMenuOption(labelKey.Translate() + " (" + targetPawn.LabelShort + ")", () =>
            {
                Owner.ExecuteActionDirect(actionName, targetPawn);
            }));
        }

        internal void ExecuteActionDirect(string actionName, Pawn targetPawn)
        {
            var action = new AI.LLMRpgApiResponse.ApiAction { action = actionName, amount = 1, reason = "forced" };
            string normalized = Dialog_RPGPawnDialogue.NormalizeRpgActionName(actionName);
            if (string.IsNullOrEmpty(normalized)) return;

            // Find participant index for the target pawn
            for (int i = 0; i < participants.Count; i++)
            {
                if (participants[i].Pawn == targetPawn)
                {
                    Owner.ExecuteGroupAction(participants[i], normalized, action);
                    return;
                }
            }
        }

        internal static bool HasLoveRelation(Pawn target, Pawn initiator)
        {
            return target?.relations?.DirectRelationExists(PawnRelationDefOf.Lover, initiator) == true
                || target?.relations?.DirectRelationExists(PawnRelationDefOf.Fiance, initiator) == true;
        }

        internal static bool HasSpouseRelation(Pawn target, Pawn initiator)
        {
            return target?.relations?.DirectRelationExists(PawnRelationDefOf.Spouse, initiator) == true;
        }

        internal void DrawParticipantNameLabel(Rect portraitRect, string name, bool isSpeaker)
        {
            Rect nameRect = new Rect(portraitRect.x, portraitRect.yMax + 2f, portraitRect.width, 22f);
            Text.Anchor = TextAnchor.MiddleCenter;
            Color nameColor = isSpeaker ? new Color(1f, 0.92f, 0.55f, globalFadeAlpha) : new Color(0.7f, 0.7f, 0.7f, globalFadeAlpha * 0.8f);
            GUI.color = nameColor;
            Widgets.Label(nameRect, name);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }
        }

}
