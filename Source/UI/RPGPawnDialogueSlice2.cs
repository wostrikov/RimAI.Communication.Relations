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

using ActionFeedbackEntry = Ustas.RimAI.Communication.Relations.UI.RPGPawnDialogueFeedbackOverlay.ActionFeedbackEntry;

using DialoguePage = Ustas.RimAI.Communication.Relations.UI.Dialog_RPGPawnDialogue.DialoguePage;
using InitialRequestPromptCache = Ustas.RimAI.Communication.Relations.UI.Dialog_RPGPawnDialogue.InitialRequestPromptCache;

namespace Ustas.RimAI.Communication.Relations.UI
{
    internal sealed class RPGPawnDialogueSlice2 : Dialog_RPGPawnDialogueCollaborator
    {
        internal RPGPawnDialogueSlice2(Dialog_RPGPawnDialogue owner) : base(owner)
        {
        }

internal void DrawDialogueBox(Rect inRect)
        {
            // Check State Transitions
            if (isShowingUserText && isWaitingForDelayAfterUser && !isViewingHistory)
            {
                if (Time.realtimeSinceStartup - timeUserTextFinished >= 1.0f)
                {
                    if (aiResponseReady)
                    {
                        // Switch to AI text
                        isShowingUserText = false;
                        isWaitingForDelayAfterUser = false;
                        
                        currentSpeakerName = target.LabelShort;
                        currentDialogueText = Owner.NormalizeEnvelopeVisibleDialogueForDisplay(pendingResponseEnvelope, "live_display");
                        if (string.IsNullOrWhiteSpace(currentDialogueText))
                        {
                            currentDialogueText = Owner.NormalizeVisibleNpcDialogueText(aiResponseText);
                        }

                        displayedText = "";
                        visibleChars = 0;
                        isTyping = true;
                        lastCharTime = Time.realtimeSinceStartup;
                        Owner.ResetDialogueTextPaging();
                        dialogPages.Add(new DialoguePage { speakerName = target.LabelShort, text = currentDialogueText });
                        Owner.RecordSessionDialogueTurn(target.LabelShort, currentDialogueText, false);
                        Owner.TryApplyPendingEnvelope();
                    }
                }
            }

            Rect boxRect = new Rect(0, inRect.height - DialogueBoxHeight, inRect.width, DialogueBoxHeight);
            
            Widgets.DrawBoxSolid(boxRect, dialogueBoxCurrentColor);
            GUI.color = new Color(0.3f, 0.3f, 0.35f, 1f);
            Widgets.DrawBox(boxRect, 2);
            GUI.color = Color.white;

            Rect contentRect = boxRect.ContractedBy(35f);
            
            bool drawLive = !isViewingHistory;
            string renderSpeaker = drawLive ? currentSpeakerName : dialogPages[historyViewIndex].speakerName;
            string renderText = drawLive ? currentDialogueText : dialogPages[historyViewIndex].text;

            // Speaker Name Header (interactive: hover tooltip + click FloatMenu)
            Pawn speakerPawn = renderSpeaker == initiator.LabelShort ? initiator : target;
            bool speakerRightAligned = renderSpeaker == initiator.LabelShort;
            Rect nameRect = speakerRightAligned
                ? new Rect(contentRect.xMax - 600f, contentRect.y - 35f, 600f, 55f)
                : new Rect(contentRect.x, contentRect.y - 35f, 600f, 55f);
            Owner.DrawPawnNameWithMenu(nameRect, speakerPawn, renderSpeaker, speakerRightAligned);

            // Text Label Box
            Rect textArea = new Rect(contentRect.x, contentRect.y + 20f, contentRect.width, contentRect.height - 70f);
            
            // If the player is speaking, set right alignment by adjusting Rect
            if (renderSpeaker == initiator.LabelShort)
            {
                string calcText = drawLive ? currentDialogueText : renderText;
                // Strip tags for accurate measurement
                string measureText = System.Text.RegularExpressions.Regex.Replace(calcText, "<.*?>", "");
                
                GameFont prevFont = Text.Font;
                Text.Font = GameFont.Medium;
                Vector2 size = Text.CalcSize(measureText);
                Text.Font = prevFont;
                
                // Scale factor: size=34 is ~1.5x of Medium. Add buffer to prevent wrap-around 'two columns' issue.
                float clampedWidth = Mathf.Min(size.x * 1.6f + 40f, contentRect.width * 0.85f);
                
                textArea.x = contentRect.xMax - clampedWidth;
                textArea.width = clampedWidth;
                // Use UpperLeft to maintain steady 'left-to-right' typing without text jumping
                Text.Anchor = TextAnchor.UpperLeft;
            }
            
            if (drawLive)
            {
                if (isSendingInitialMessage)
                {
                    string dots = new string('.', (int)(Time.time * 2) % 4);
                    Widgets.Label(textArea, $"<size=34><color=#b0b0b0>{Dialog_RPGPawnDialogue.BuildRpgThinkingText(dots)}</color></size>");
                }
                else if (isShowingUserText && isWaitingForDelayAfterUser && !aiResponseReady && Time.realtimeSinceStartup - timeUserTextFinished >= 3.0f)
                {
                    // The player text fully printed, delayed 3s, waiting for AI.
                    string dots = new string('.', (int)(Time.time * 2) % 4);
                    Widgets.Label(textArea, $"<size=34>{displayedText}\n<color=#b0b0b0>{Dialog_RPGPawnDialogue.BuildRpgOpponentThinkingText(dots)}</color></size>");
                }
                else
                {
                    Owner.UpdateTyping();
                    string liveText = Owner.ResolveDialogueTextForDisplay(drawLive, renderSpeaker, currentDialogueText, textArea);
                    string visibleText = isTyping ? displayedText : liveText;
                    Widgets.Label(textArea, $"<size=34>{visibleText}</size>");
                }
            }
            else
            {
                string historyText = Owner.ResolveDialogueTextForDisplay(drawLive, renderSpeaker, renderText, textArea);
                Widgets.Label(textArea, $"<size=34>{historyText}</size>");
            }

            // Restore anchor
            if (renderSpeaker == initiator.LabelShort)
            {
                Text.Anchor = TextAnchor.UpperLeft;
            }
            
            // Input Mode Display
            if (!isTyping && !isSendingInitialMessage && !isShowingUserText && drawLive && !isDialogueEndedByNpc)
            {
                float inputHeight = 45f;
                Rect bottomArea = new Rect(contentRect.x, contentRect.yMax - inputHeight, contentRect.width, inputHeight);
                
                // Update dynamic alpha for animation
                // Stay fully visible if either mouse is over OR if the input field has focus
                bool isFocused = GUI.GetNameOfFocusedControl() == UserReplyInputControlName;
                bool mouseInBottom = Mouse.IsOver(bottomArea);
                float targetAlpha = (mouseInBottom || isFocused) ? 1.0f : 0.25f;
                // Use Real-time delta for smooth transition regardless of frame rate
                inputAlpha = Mathf.Lerp(inputAlpha, targetAlpha, 0.12f);

                GUI.color = new Color(1f, 1f, 1f, inputAlpha);
                
                Rect inputRect = new Rect(bottomArea.x, bottomArea.y, bottomArea.width - 150f, inputHeight);
                
                // Draw a more subtle background for the input if not active
                if (inputAlpha < 0.9f) {
                    Widgets.DrawBoxSolid(inputRect, new Color(1f, 1f, 1f, 0.05f));
                }
                
                GUI.SetNextControlName(UserReplyInputControlName);
                if (Owner.ShouldSendFromKeyboard(Event.current))
                {
                    Event.current.Use();
                    Owner.TrySendMessage();
                }
                userReplyText = Widgets.TextField(inputRect, userReplyText);
                
                Rect sendRect = new Rect(bottomArea.xMax - 135f, bottomArea.y, 135f, inputHeight);
                string sendLabel = "RimChat_SendButton".Translate();
                
                // Custom-styled button for 'inconspicuous' look
                Color savedGuiColor = GUI.color;
                if (inputAlpha < 0.5f) {
                    // Just draw text when alpha is low
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(sendRect, sendLabel);
                    Text.Anchor = TextAnchor.UpperLeft;
                    if (Widgets.ButtonInvisible(sendRect)) Owner.TrySendMessage();
                } else {
                    if (Widgets.ButtonText(sendRect, sendLabel)) Owner.TrySendMessage();
                }

                Owner.DrawRpgPotentialActionsHint(sendRect, inputAlpha);

                GUI.color = Color.white;
            }
            else if (!isTyping && !isSendingInitialMessage && !isShowingUserText && drawLive && isDialogueEndedByNpc)
            {
                Rect blockedRect = new Rect(contentRect.x, contentRect.yMax - 42f, contentRect.width, 32f);
                GUI.color = new Color(0.95f, 0.55f, 0.55f, 0.95f);
                string blockText = string.IsNullOrEmpty(dialogueEndReason)
                    ? "RimChat_RPGDialogue_EndedByNpc".Translate()
                    : "RimChat_RPGDialogue_EndedByNpcReason".Translate(dialogueEndReason);
                Widgets.Label(blockedRect, blockText);
                GUI.color = Color.white;
            }
            
            Owner.DrawDialogueNavigation(boxRect);
        }

internal bool ShouldSendFromKeyboard(Event current)
        {
            if (!Dialog_RPGPawnDialogue.IsSubmitKeyPressed(current) || current.alt || Dialog_RPGPawnDialogue.IsImeComposing())
            {
                return false;
            }

            if (!Dialog_RPGPawnDialogue.IsUserReplyInputFocused())
            {
                return false;
            }

            return Owner.CanSendUserReplyFromKeyboard();
        }

internal static bool IsSubmitKeyPressed(Event current)
        {
            if (current == null)
            {
                return false;
            }

            if (current.keyCode != KeyCode.Return && current.keyCode != KeyCode.KeypadEnter)
            {
                return false;
            }

            return current.type == EventType.KeyDown || current.rawType == EventType.KeyDown;
        }

internal void TrySendMessage()
        {
            if (isDialogueEndedByNpc)
            {
                return;
            }

            var rpgManager = Current.Game?.GetComponent<Ustas.RimAI.Communication.Relations.DiplomacySystem.GameComponent_RPGManager>();
            if (rpgManager != null && rpgManager.IsRpgDialogueOnCooldown(target, out int remainingTicks))
            {
                float remainingHours = Math.Max(0f, remainingTicks / 2500f);
                string cooldownText = "RimChat_RPGDialogue_CooldownBlockedWithHours".Translate(remainingHours.ToString("F1"));
                Messages.Message(
                    cooldownText,
                    MessageTypeDefOf.RejectInput,
                    false);
                isDialogueEndedByNpc = true;
                dialogueEndReason = cooldownText;
                return;
            }

            if (!string.IsNullOrWhiteSpace(userReplyText))
            {
                string textToSend = userReplyText.Trim();
                chatHistory.Add(new ChatMessageData { role = "user", content = textToSend });
                dialogPages.Add(new DialoguePage { speakerName = initiator.LabelShort, text = textToSend });
                Owner.RecordSessionDialogueTurn(initiator.LabelShort, textToSend, true);
                RpgDialogueTraceTracker.RegisterTurn(initiator, target, true, textToSend, dialogueSessionId);
                userReplyText = "";
                GUI.FocusControl(null); // Release focus so it can fade out
                
                isViewingHistory = false; // Snap back to live mode
                Owner.ResetDialogueTextPaging();
                
                // Switch to User typing mode
                currentSpeakerName = initiator.LabelShort;
                currentDialogueText = textToSend;
                displayedText = "";
                visibleChars = 0;
                
                isTyping = true;
                isShowingUserText = true;
                isWaitingForDelayAfterUser = false;
                
                aiResponseReady = false;
                aiResponseText = "";

                List<ChatMessageData> requestMessages;
                try
                {
                    requestMessages = Owner.BuildCompressedRpgRequestMessages();
                }
                catch (PromptRenderException ex)
                {
                    Owner.ApplyPromptRenderFailure(ex);
                    return;
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
                        aiResponseText = envelope.DialogueText ?? string.Empty;
                        aiResponseReady = true;
                        string visibleHistoryContent = Owner.NormalizeHistoryAssistantContent(envelope, aiResponseText);
                        if (!string.IsNullOrWhiteSpace(visibleHistoryContent))
                        {
                            chatHistory.Add(new ChatMessageData { role = "assistant", content = visibleHistoryContent });
                        }
                        RpgDialogueTraceTracker.RegisterTurn(initiator, target, false, aiResponseText, dialogueSessionId);
                    },
                    onError: error =>
                    {
                        if (isWindowClosing)
                        {
                            return;
                        }

                        aiResponseReady = true;
                        aiResponseText = "Error: " + error;
                        Owner.ReleaseActiveRequestLease();
                    },
                    onDropped: reason =>
                    {
                        Owner.HandleDroppedResponse(reason);
                        aiResponseReady = true;
                    });

                if (activeRequestLease == null)
                {
                    aiResponseReady = true;
                    aiResponseText = "Error: " + "RimChat_DialogueRequestUnavailable".Translate().ToString();
                }
            }
        }

internal void ApplyPromptRenderFailure(PromptRenderException ex)
        {
            if (ex == null)
            {
                return;
            }

            string message = "RimChat_PromptRenderBlocked".Translate(ex.TemplateId, ex.Channel, ex.ErrorLine, ex.ErrorColumn).ToString();
            Log.Error("[RimAI.Relations] RPG prompt rendering aborted request: " + ex.Message);
            currentDialogueText = message;
            aiResponseReady = true;
            aiResponseText = message;
            isSendingInitialMessage = false;
            isTyping = true;
            isDialogueEndedByNpc = true;
            dialogueEndReason = message;
            Messages.Message(message, MessageTypeDefOf.RejectInput, false);
        }

internal List<ChatMessageData> BuildCompressedRpgRequestMessages()
        {
            var request = new List<ChatMessageData>();
            bool openingTurn = !Dialog_RPGPawnDialogue.HasVisibleAssistantReply(chatHistory);
            string currentTurnUserIntent = Dialog_RPGPawnDialogue.ExtractLatestVisibleUserIntent(chatHistory);
            request.Add(new ChatMessageData
            {
                role = "system",
                content = Owner.BuildRpgSystemPromptForRequest(openingTurn, currentTurnUserIntent)
            });
            List<ChatMessageData> conversation = chatHistory
                .Where(message => !Dialog_RPGPawnDialogue.IsSystemRole(message?.role))
                .ToList();
            request.AddRange(DialogueContextCompressionService.BuildFromChatMessages(conversation));
            if (openingTurn && conversation.Count == 0 && !string.IsNullOrWhiteSpace(currentTurnUserIntent))
            {
                request.Add(new ChatMessageData
                {
                    role = "user",
                    content = currentTurnUserIntent
                });
            }
            return request;
        }

internal Color ResolveDialogueBoxTargetColor()
        {
            if (target == null)
                return DialogueBoxDefaultColor;

            // 1. Romantic relationship → pink
            if (target.relations?.DirectRelationExists(PawnRelationDefOf.Lover, initiator) == true
                || target.relations?.DirectRelationExists(PawnRelationDefOf.Fiance, initiator) == true
                || target.relations?.DirectRelationExists(PawnRelationDefOf.Spouse, initiator) == true)
            {
                return DialogueBoxRomanceColor;
            }

            // Colony pawn → default
            if (target.Faction == Faction.OfPlayer)
                return DialogueBoxDefaultColor;

            // 2. Prisoner or slave → yellow
            if (target.IsPrisoner || target.IsSlave)
                return DialogueBoxPrisonerColor;

            // 3. Hostile non-colony pawn (not prisoner/slave) → red
            if (target.Faction?.HostileTo(Faction.OfPlayer) == true)
                return DialogueBoxHostileColor;

            // 4. Non-colony, neutral/friendly → blue
            return DialogueBoxNeutralColor;
        }

internal void UpdateTyping()
        {
            if (isTyping && visibleChars < currentDialogueText.Length)
            {
                float interval = 0.02f;
                if (Time.realtimeSinceStartup - lastCharTime > interval)
                {
                    visibleChars++;
                    
                    // Skip rich text tags <...> instantaneously
                    if (visibleChars < currentDialogueText.Length && currentDialogueText[visibleChars - 1] == '<')
                    {
                        int closeTagIndex = currentDialogueText.IndexOf('>', visibleChars - 1);
                        if (closeTagIndex != -1)
                        {
                            visibleChars = closeTagIndex + 1;
                        }
                    }

                    displayedText = currentDialogueText.Substring(0, Mathf.Min(visibleChars, currentDialogueText.Length));
                    lastCharTime = Time.realtimeSinceStartup;
                    
                    if (visibleChars % 3 == 0)
                    {
                        SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                    }
                }
                
                if (visibleChars >= currentDialogueText.Length)
                {
                    isTyping = false;
                    
                    // Trigger player pawn fade-in when target's first sentence is done
                    if (!firstTargetSentenceDone && currentSpeakerName == target.LabelShort)
                    {
                        firstTargetSentenceDone = true;
                    }

                    if (isShowingUserText)
                    {
                        isWaitingForDelayAfterUser = true;
                        timeUserTextFinished = Time.realtimeSinceStartup;
                    }
                }
            }
        }
    }
}
