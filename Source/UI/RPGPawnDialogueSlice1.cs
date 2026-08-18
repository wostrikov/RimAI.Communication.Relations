using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.Dialogue;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.Rpg;

using DialoguePage = Ustas.RimAI.Communication.Relations.UI.Dialog_RPGPawnDialogue.DialoguePage;
using InitialRequestPromptCache = Ustas.RimAI.Communication.Relations.UI.Dialog_RPGPawnDialogue.InitialRequestPromptCache;

namespace Ustas.RimAI.Communication.Relations.UI
{
    internal sealed class RPGPawnDialogueSlice1 : Dialog_RPGPawnDialogueCollaborator
    {
        internal RPGPawnDialogueSlice1(Dialog_RPGPawnDialogue owner) : base(owner)
        {
        }

internal static float GetPortraitZoom(float bodySize, bool humanlike)
        {
            float b = humanlike ? 1.35f : 1.0f;
            return Mathf.Clamp(b / Mathf.Pow(Mathf.Max(bodySize, 0.5f), 0.3f), 0.5f, 1.35f);
        }

internal static List<string> ParseSceneTagsCsv(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv))
            {
                return null;
            }

            return csv
                .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(tag => tag.Trim().ToLowerInvariant())
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct()
                .ToList();
        }

internal static string BuildProactiveOpeningCarryOverPrompt(string proactiveOpening)
        {
            return "A proactive trigger opened this chat from NPC side.\n"
                + "Use it only as scene context. Do not copy previous opening wording.\n"
                + "Generate a fresh in-character line with continuity from personal memory.";
        }

internal bool TrySeedProactiveOpening(string proactiveOpening)
        {
            if (string.IsNullOrWhiteSpace(proactiveOpening))
            {
                return false;
            }

            string opening = Owner.NormalizeVisibleNpcDialogueText(proactiveOpening);
            currentSpeakerName = target.LabelShort;
            currentDialogueText = opening;
            displayedText = "";
            visibleChars = 0;
            isTyping = true;
            lastCharTime = Time.realtimeSinceStartup;
            Owner.ResetDialogueTextPaging();
            bool hasOpeningContent = !string.IsNullOrWhiteSpace(opening);
            if (hasOpeningContent)
            {
                chatHistory.Add(new ChatMessageData { role = "assistant", content = opening });
            }
            dialogPages.Add(new DialoguePage { speakerName = target.LabelShort, text = opening });
            Owner.RecordSessionDialogueTurn(target.LabelShort, opening, false);
            RpgDialogueTraceTracker.RegisterTurn(initiator, target, false, opening, dialogueSessionId);
            return true;
        }

internal void SendInitialMessage()
        {
            isSendingInitialMessage = true;
            currentDialogueText = "";
            displayedText = "";
            visibleChars = 0;
            currentSpeakerName = target.LabelShort;
            List<ChatMessageData> requestMessages;
            try
            {
                requestMessages = Owner.TryGetValidInitialRequestPromptMessages(out List<ChatMessageData> cached)
                    ? cached
                    : Owner.BuildCompressedRpgRequestMessages();
            }
            catch (PromptRenderException ex)
            {
                Owner.ApplyPromptRenderFailure(ex);
                return;
            }
            finally
            {
                initialRequestPromptCache = null;
            }

            Owner.CloseActiveRequestLease();
            activeRequestRuntimeContext = runtimeContext.WithCurrentRuntimeMarkers();
            activeRequestLease = conversationController.TrySend(
                activeRequestRuntimeContext,
                windowInstanceId,
                requestMessages,
                onReady: envelope =>
                {
                    if (isWindowClosing)
                    {
                        return;
                    }

                    Owner.PrepareEnvelopeForDisplay(envelope);
                    pendingResponseEnvelope = envelope;
                    currentDialogueText = envelope.DialogueText ?? string.Empty;
                    isSendingInitialMessage = false;
                    Owner.ResetDialogueTextPaging();
                    string visibleHistoryContent = Owner.NormalizeHistoryAssistantContent(envelope, currentDialogueText);
                    if (!string.IsNullOrWhiteSpace(visibleHistoryContent))
                    {
                        chatHistory.Add(new ChatMessageData { role = "assistant", content = visibleHistoryContent });
                    }
                    dialogPages.Add(new DialoguePage { speakerName = target.LabelShort, text = currentDialogueText });
                    Owner.RecordSessionDialogueTurn(target.LabelShort, currentDialogueText, false);
                    RpgDialogueTraceTracker.RegisterTurn(initiator, target, false, currentDialogueText, dialogueSessionId);
                    isTyping = true;
                    lastCharTime = Time.realtimeSinceStartup;
                    Owner.TryApplyPendingEnvelope();
                },
                onError: error =>
                {
                    if (isWindowClosing)
                    {
                        return;
                    }

                    isSendingInitialMessage = false;
                    currentDialogueText = "Error: " + error;
                    isTyping = true;
                    Owner.ReleaseActiveRequestLease();
                },
                onDropped: Owner.HandleDroppedResponse);

            if (activeRequestLease == null)
            {
                isSendingInitialMessage = false;
                Owner.HandleDroppedResponse("send_initial_blocked");
            }
        }

internal void PrepareInitialRequestPromptCache()
        {
            if (initialRequestPromptCache != null)
            {
                return;
            }

            List<ChatMessageData> requestMessages = Owner.BuildCompressedRpgRequestMessages();
            var markers = runtimeContext.WithCurrentRuntimeMarkers();
            initialRequestPromptCache = new InitialRequestPromptCache
            {
                ContextVersion = markers?.ContextVersion ?? runtimeContext?.ContextVersion ?? 0,
                WindowKey = windowLifecycleKey ?? string.Empty,
                OwnerWindowId = windowInstanceId ?? string.Empty,
                Messages = Dialog_RPGPawnDialogue.CloneChatMessages(requestMessages)
            };
        }

internal bool TryGetValidInitialRequestPromptMessages(out List<ChatMessageData> requestMessages)
        {
            requestMessages = null;
            if (initialRequestPromptCache == null)
            {
                return false;
            }

            var markers = runtimeContext.WithCurrentRuntimeMarkers();
            int currentContextVersion = markers?.ContextVersion ?? runtimeContext?.ContextVersion ?? 0;
            if (currentContextVersion != initialRequestPromptCache.ContextVersion)
            {
                return false;
            }

            if (!string.Equals(initialRequestPromptCache.WindowKey, windowLifecycleKey ?? string.Empty, StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.Equals(initialRequestPromptCache.OwnerWindowId, windowInstanceId ?? string.Empty, StringComparison.Ordinal))
            {
                return false;
            }

            requestMessages = Dialog_RPGPawnDialogue.CloneChatMessages(initialRequestPromptCache.Messages);
            return requestMessages.Count > 0;
        }

internal static List<ChatMessageData> CloneChatMessages(IEnumerable<ChatMessageData> source)
        {
            return source?
                .Where(item => item != null)
                .Select(item => new ChatMessageData
                {
                    role = item.role,
                    content = item.content
                })
                .ToList() ?? new List<ChatMessageData>();
        }

internal void DoWindowContents(Rect inRect)
        {
            // Update Alphas based on real time
            float deltaTime = Time.deltaTime;
            globalFadeAlpha = Mathf.Clamp01(globalFadeAlpha + deltaTime * FadeSpeed);
            targetFadeAlpha = Mathf.Clamp01(targetFadeAlpha + deltaTime * FadeSpeed);
            
            // Player pawn starts fading in when target finishes first sentence or player speaks
            if (firstTargetSentenceDone || currentSpeakerName == initiator.LabelShort || dialogPages.Any(p => p.speakerName == initiator.LabelShort))
            {
                initiatorFadeAlpha = Mathf.Clamp01(initiatorFadeAlpha + deltaTime * FadeSpeed);
            }

            // Update portrait drag physics (spring follow, spring-back, collision)
            Owner.UpdatePortraitDrag(inRect, deltaTime);

            // Dynamic dialogue box background color based on target pawn status
            dialogueBoxTargetColor = Owner.ResolveDialogueBoxTargetColor();
            dialogueBoxCurrentColor = Color.Lerp(dialogueBoxCurrentColor, dialogueBoxTargetColor, deltaTime * DialogueBoxColorBlendSpeed);

            // Inspect pane: let events fall through to the pane below when it was opened via our menu
            bool inspectPaneShowing = Owner.IsInspectPaneShowing();
            absorbInputAroundWindow = !inspectPaneShowing;

            // Smooth portrait transparency when mouse hovers over the inspect-pane overlap zone
            Rect inspectPaneOverlapRect = inspectPaneShowing ? Owner.GetInspectPaneOverlapRect() : Rect.zero;
            bool mouseOverInspectPane = inspectPaneShowing && Mouse.IsOver(inspectPaneOverlapRect);
            float targetInspectAlpha = mouseOverInspectPane ? 0f : 1f;
            inspectPaneAlpha = Mathf.Lerp(inspectPaneAlpha, targetInspectAlpha, deltaTime * InspectPaneAlphaSpeed);

            // Draw Portraits first (Portraits use their own alpha inside the methods)
            Owner.DrawPortraits(inRect);

            // Draw Dialogue Box with global alpha
            GUI.color = new Color(1f, 1f, 1f, globalFadeAlpha);
            Owner.DrawDialogueBox(inRect);
            GUI.color = Color.white;
            Owner.DrawActionFeedback(inRect);
            Owner.DrawSessionHistoryPanel(inRect);

            if (Event.current.type == EventType.MouseDown)
            {
                // Drag the initiator portrait
                if (Owner.TryStartInitiatorDrag(inRect))
                {
                    return;
                }

                if (Owner.TryHandleHistoryPanelMouseDown(Event.current))
                {
                    return;
                }

                Rect dialogueBoxRect = new Rect(0, inRect.height - DialogueBoxHeight, inRect.width, DialogueBoxHeight);
                bool insideDialogueBox = dialogueBoxRect.Contains(Event.current.mousePosition);

                // When the inspect pane was opened through our menu, never close on outside clicks.
                // Let the event fall through to the inspect pane below (absorbInputAroundWindow is false).
                if (!insideDialogueBox && inspectPaneShowing)
                {
                    return;
                }

                // Click outside dialogue box → close window (normal exit)
                if (!insideDialogueBox)
                {
                    Close();
                    Event.current.Use();
                }
                // Click inside dialogue box to skip text animation
                else if (isTyping)
                {
                    visibleChars = currentDialogueText.Length;
                    displayedText = currentDialogueText;
                    isTyping = false;

                    if (isShowingUserText)
                    {
                        isWaitingForDelayAfterUser = true;
                        timeUserTextFinished = Time.realtimeSinceStartup;
                    }

                    Event.current.Use();
                }
                else
                {
                    // Click inside dialogue box but not in input area -> clear focus
                    float inputHeight = 45f;
                    float dialogueBoxY = inRect.height - DialogueBoxHeight;
                    Rect bottomArea = new Rect(35f, dialogueBoxY + DialogueBoxHeight - 35f - inputHeight, inRect.width - 70f, inputHeight);

                    if (!bottomArea.Contains(Event.current.mousePosition))
                    {
                        if (GUI.GetNameOfFocusedControl() == UserReplyInputControlName)
                        {
                            GUI.FocusControl(null);
                        }
                    }
                    Event.current.Use();
                }
            }
        }

internal void TryFinalizeArchiveSessionOnClose()
        {
            if (archiveSessionFinalized)
            {
                return;
            }

            archiveSessionFinalized = true;
            RpgNpcDialogueArchiveManager.Instance.FinalizeSession(initiator, target, dialogueSessionId, chatHistory);
        }

internal void TryCommitRpgSessionSummaryOnClose()
        {
            if (sessionCloseSummaryCommitted)
            {
                return;
            }

            sessionCloseSummaryCommitted = true;
            DialogueSummaryService.TryPushRpgSessionSummaryOnClose(initiator, target, chatHistory);
        }

internal void DrawPawnPortrait(Rect rect, Pawn pawn, bool flip)
        {
            RenderTexture rt = flip ? initiatorRT : targetRT;
            bool created;
            Dialog_RPGPawnDialogue.DrawPawnPortrait(rect, pawn, flip, ref rt, out created);
            if (flip) initiatorRT = rt;
            else targetRT = rt;
        }

internal static void DrawPawnPortrait(Rect rect, Pawn pawn, bool flip, ref RenderTexture cachedRT, out bool created)
        {
            created = false;
            if (pawn == null) return;

            int texWidth = (int)(rect.width * 3f);
            int texHeight = (int)(rect.height * 3f);

            if (cachedRT == null || cachedRT.width != texWidth || cachedRT.height != texHeight)
            {
                if (cachedRT != null) { cachedRT.Release(); UnityEngine.Object.Destroy(cachedRT); }
                cachedRT = new RenderTexture(texWidth, texHeight, 24, RenderTextureFormat.ARGB32);
                cachedRT.antiAliasing = (QualitySettings.antiAliasing > 0) ? QualitySettings.antiAliasing : 8;
                cachedRT.filterMode = FilterMode.Bilinear;
                cachedRT.useMipMap = false;
                cachedRT.Create();
                created = true;
            }

            if (Event.current.type == EventType.Repaint)
            {
                Vector3 cameraOffset = new Vector3(0f, 0f, 0.15f);
                float zoom = Dialog_RPGPawnDialogue.GetPortraitZoom(pawn.BodySize, pawn.RaceProps.Humanlike);
                Find.PawnCacheRenderer.RenderPawn(pawn, cachedRT, cameraOffset, zoom, 0f, Rot4.South, true, true, true, true, default(Vector3), null, null, true);
            }

            if (cachedRT != null)
            {
                if (flip)
                {
                    Matrix4x4 savedMatrix = GUI.matrix;
                    Vector2 center = rect.center;
                    GUIUtility.ScaleAroundPivot(new Vector2(-1f, 1f), center);
                    GUI.DrawTexture(rect, cachedRT, ScaleMode.StretchToFill, true);
                    GUI.matrix = savedMatrix;
                }
                else
                {
                    GUI.DrawTexture(rect, cachedRT, ScaleMode.StretchToFill, true);
                }
            }
        }
    }
}
