using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Communication.Relations.UI
{
    public partial class Dialog_RPGPawnGroupChat
    {
        private void DrawPortraits(Rect inRect)
        {
            List<Rect> portraitRects = CalculateCascadingRects(inRect);
            int currentIdx = GetActiveSpeakerIndex();
            var drawOrder = BuildDrawOrder(currentIdx, portraitRects.Count);
            UpdateRoundTransition(currentIdx);

            for (int orderPos = 0; orderPos < drawOrder.Count; orderPos++)
            {
                int i = drawOrder[orderPos];
                Rect rect = portraitRects[i];
                GroupChatParticipant participant = participants[i];
                bool isCurrentSpeaker = (i == currentIdx);

                float progress = GetRoundTransitionProgress();
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

                bool shouldRender = isCurrentSpeaker || NeedsPortraitRefresh(i);
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
                DrawParticipantNameLabel(rect, participant.DisplayName, isCurrentSpeaker);
            }

            DrawInitiatorPortrait(inRect);
        }

        private int GetActiveSpeakerIndex()
        {
            return (!isPlayerTurn && currentSpeakerIndex >= 0 && currentSpeakerIndex < participants.Count)
                ? currentSpeakerIndex : -1;
        }

        private List<int> BuildDrawOrder(int speakerIndex, int count)
        {
            var order = new List<int>();
            for (int i = 0; i < count; i++)
                if (i != speakerIndex) order.Add(i);
            if (speakerIndex >= 0 && speakerIndex < count)
                order.Add(speakerIndex);
            return order;
        }

        private void UpdateRoundTransition(int newSpeakerIdx)
        {
            if (newSpeakerIdx != previousSpeakerIndex && newSpeakerIdx >= 0)
            {
                previousSpeakerIndex = newSpeakerIdx;
                roundTransitionTime = Time.time;
            }
        }

        private float GetRoundTransitionProgress()
        {
            float elapsed = Time.time - roundTransitionTime;
            return Mathf.Clamp01(elapsed / RoundTransitionDuration);
        }

        private bool NeedsPortraitRefresh(int index)
        {
            if (!_lastPortraitRenderTime.TryGetValue(index, out float lastTime))
                return true;
            return Time.realtimeSinceStartup - lastTime >= NonSpeakerRenderInterval;
        }

        private List<Rect> CalculateCascadingRects(Rect inRect)
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

        private void DrawInitiatorPortrait(Rect inRect)
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
            Dialog_RPGPawnDialogue.DrawPawnPortrait(initRect, initiator, true, ref initiatorPortraitRT, out created);

            // Invisible button: click to show forced-action menu
            if (Widgets.ButtonInvisible(initRect))
            {
                ShowForcedActionMenu();
            }

            GUI.color = Color.white;
        }

        // ── Forced action menu (click initiator portrait) ──

        private void ShowForcedActionMenu()
        {
            // Find a valid target: current speaker, or first valid participant
            int targetIdx = GetActiveSpeakerIndex();
            if (targetIdx < 0) targetIdx = 0;
            if (targetIdx >= participants.Count) return;

            Pawn targetPawn = participants[targetIdx].Pawn;
            if (targetPawn == null) return;

            var options = new List<FloatMenuOption>();

            if (!HasLoveRelation(targetPawn, initiator))
                AddMenuOption(options, "RimChat_DragMenu_Romance", "RomanceAttempt", targetPawn);
            if (!HasSpouseRelation(targetPawn, initiator))
                AddMenuOption(options, "RimChat_DragMenu_Marry", "MarriageProposal", targetPawn);
            if (HasLoveRelation(targetPawn, initiator))
                AddMenuOption(options, "RimChat_DragMenu_Breakup", "Breakup", targetPawn);
            if (HasSpouseRelation(targetPawn, initiator))
                AddMenuOption(options, "RimChat_DragMenu_Divorce", "Divorce", targetPawn);
            AddMenuOption(options, "RimChat_DragMenu_Date", "Date", targetPawn);
            AddMenuOption(options, "RimChat_DragMenu_Gift", "TryGainMemory", targetPawn);
            if (targetPawn.IsPrisoner)
            {
                AddMenuOption(options, "RimChat_DragMenu_ReduceResist", "ReduceResistance", targetPawn);
                AddMenuOption(options, "RimChat_DragMenu_ReduceWill", "ReduceWill", targetPawn);
            }
            if (targetPawn.Faction != Faction.OfPlayer)
                AddMenuOption(options, "RimChat_DragMenu_Recruit", "Recruit", targetPawn);
            if (ModsConfig.IdeologyActive && targetPawn?.ideo != null)
            {
                AddMenuOption(options, "RimChat_DragMenu_ConvertIdeo", "ConvertIdeology", targetPawn);
                AddMenuOption(options, "RimChat_DragMenu_AdjCertainty", "AdjustCertainty", targetPawn);
            }
            AddMenuOption(options, "RimChat_DragMenu_Inspiration", "GrantInspiration", targetPawn);
            AddMenuOption(options, "RimChat_DragMenu_Incident", "TriggerIncident", targetPawn);

            if (options.Count > 0)
                Find.WindowStack.Add(new FloatMenu(options));
        }

        private void AddMenuOption(List<FloatMenuOption> options, string labelKey, string actionName, Pawn targetPawn)
        {
            options.Add(new FloatMenuOption(labelKey.Translate() + " (" + targetPawn.LabelShort + ")", () =>
            {
                ExecuteActionDirect(actionName, targetPawn);
            }));
        }

        private void ExecuteActionDirect(string actionName, Pawn targetPawn)
        {
            var action = new AI.LLMRpgApiResponse.ApiAction { action = actionName, amount = 1, reason = "forced" };
            string normalized = Dialog_RPGPawnDialogue.NormalizeRpgActionName(actionName);
            if (string.IsNullOrEmpty(normalized)) return;

            // Find participant index for the target pawn
            for (int i = 0; i < participants.Count; i++)
            {
                if (participants[i].Pawn == targetPawn)
                {
                    ExecuteGroupAction(participants[i], normalized, action);
                    return;
                }
            }
        }

        private static bool HasLoveRelation(Pawn target, Pawn initiator)
        {
            return target?.relations?.DirectRelationExists(PawnRelationDefOf.Lover, initiator) == true
                || target?.relations?.DirectRelationExists(PawnRelationDefOf.Fiance, initiator) == true;
        }

        private static bool HasSpouseRelation(Pawn target, Pawn initiator)
        {
            return target?.relations?.DirectRelationExists(PawnRelationDefOf.Spouse, initiator) == true;
        }

        private void DrawParticipantNameLabel(Rect portraitRect, string name, bool isSpeaker)
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
