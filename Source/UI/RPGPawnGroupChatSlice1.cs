using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Dialogue;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.Rpg;

using GroupChatParticipant = Ustas.RimAI.Communication.Relations.UI.Dialog_RPGPawnGroupChat.GroupChatParticipant;
using ActionFeedbackEntry = Ustas.RimAI.Communication.Relations.UI.Dialog_RPGPawnGroupChat.ActionFeedbackEntry;

namespace Ustas.RimAI.Communication.Relations.UI
{
    internal sealed class RPGPawnGroupChatSlice1 : Dialog_RPGPawnGroupChatCollaborator
    {
        internal RPGPawnGroupChatSlice1(Dialog_RPGPawnGroupChat owner) : base(owner)
        {
        }

internal void WarmupParticipantMemories()
        {
            foreach (var p in participants)
            {
                RpgNpcDialogueArchiveManager.Instance.BeginPromptMemoryWarmup(p.Pawn, initiator);
            }
        }

public bool MatchesWindowLifecycleKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            return string.Equals(windowLifecycleKey, key.Trim(), StringComparison.Ordinal);
        }

internal void DoWindowContents(Rect inRect)
        {
            float deltaTime = Time.deltaTime;
            globalFadeAlpha = Mathf.Clamp01(globalFadeAlpha + deltaTime * FadeSpeed);
            dialogueBoxTargetColor = Owner.ResolveDialogueBoxColor();
            dialogueBoxCurrentColor = Color.Lerp(dialogueBoxCurrentColor, dialogueBoxTargetColor, deltaTime * DialogueBoxColorBlendSpeed);

            Owner.UpdateFlowControl();
            Owner.DrawPortraits(inRect);
            Owner.DrawDialogueBox(inRect);
            Owner.DrawClickToContinueHint(inRect);
            Owner.DrawHistoryPanel(inRect);
            Owner.DrawActionFeedback(inRect);

            if (Event.current.type == EventType.MouseDown)
            {
                // Close history panel on outside click
                if (isHistoryPanelOpen)
                {
                    float sidePad = Mathf.Clamp(inRect.width * 0.15f, 160f, 300f);
                    float pw = Mathf.Clamp(inRect.width - sidePad * 2f, HistoryPanelMinW, HistoryPanelMaxW);
                    float ph = Mathf.Clamp(inRect.height * 0.7f, HistoryPanelMinH, HistoryPanelMaxH);
                    Rect panelR = new Rect((inRect.width - pw) / 2f, (inRect.height - ph) / 2f - 30f, pw, ph);
                    if (!panelR.Contains(Event.current.mousePosition))
                    {
                        isHistoryPanelOpen = false;
                        Event.current.Use();
                        return;
                    }
                }

                Rect dialogueBoxRect = new Rect(0, inRect.height - Dialog_RPGPawnDialogue.DialogueBoxHeight, inRect.width, Dialog_RPGPawnDialogue.DialogueBoxHeight);
                bool insideDialogueBox = dialogueBoxRect.Contains(Event.current.mousePosition);

                if (!insideDialogueBox)
                {
                    Close();
                    Event.current.Use();
                }
                else if (isTyping)
                {
                    visibleChars = currentDialogueText.Length;
                    displayedText = currentDialogueText;
                    isTyping = false;
                    if (!isPlayerTurn)
                        pauseForClick = true;
                    Event.current.Use();
                }
                else if (pauseForClick && !isPlayerTurn)
                {
                    pauseForClick = false;
                    Owner.AdvanceToNextSpeaker();
                    Event.current.Use();
                }
                else
                {
                    float inputHeight = 45f;
                    float dialogueBoxY = inRect.height - Dialog_RPGPawnDialogue.DialogueBoxHeight;
                    Rect bottomArea = new Rect(35f, dialogueBoxY + Dialog_RPGPawnDialogue.DialogueBoxHeight - 35f - inputHeight, inRect.width - 70f, inputHeight);
                    if (!bottomArea.Contains(Event.current.mousePosition))
                    {
                        if (GUI.GetNameOfFocusedControl() == UserReplyInputControlName)
                            GUI.FocusControl(null);
                    }
                    Event.current.Use();
                }
            }
        }

internal void DrawDialogueBox(Rect inRect)
        {
            Rect boxRect = new Rect(0, inRect.height - Dialog_RPGPawnDialogue.DialogueBoxHeight, inRect.width, Dialog_RPGPawnDialogue.DialogueBoxHeight);
            Widgets.DrawBoxSolid(boxRect, dialogueBoxCurrentColor);
            GUI.color = new Color(0.3f, 0.3f, 0.35f, 1f);
            Widgets.DrawBox(boxRect, 2);
            GUI.color = Color.white;

            Rect contentRect = boxRect.ContractedBy(35f);

            // Determine speaker name (live or history, matching 1v1)
            string renderSpeaker;
            string renderText;
            bool drawLive = !isViewingHistory;

            if (drawLive)
            {
                renderSpeaker = isShowingPlayerText ? initiator.LabelShort
                    : (isPlayerTurn ? initiator.LabelShort
                        : (currentSpeakerIndex >= 0 && currentSpeakerIndex < participants.Count
                            ? participants[currentSpeakerIndex].DisplayName : ""));
                renderText = isTyping ? displayedText : currentDialogueText;
            }
            else
            {
                renderSpeaker = historyViewIndex < dialogPages.Count ? dialogPages[historyViewIndex].speakerName : "";
                renderText = historyViewIndex < dialogPages.Count ? dialogPages[historyViewIndex].text : "";
            }

            bool rightAligned = renderSpeaker == initiator.LabelShort;

            if (!string.IsNullOrEmpty(renderSpeaker))
            {
                Rect nameRect = rightAligned
                    ? new Rect(contentRect.xMax - 600f, contentRect.y - 35f, 600f, 55f)
                    : new Rect(contentRect.x, contentRect.y - 35f, 600f, 55f);
                Pawn namePawn = rightAligned ? initiator
                    : (currentSpeakerIndex >= 0 && currentSpeakerIndex < participants.Count
                        ? participants[currentSpeakerIndex].Pawn : null);
                Owner.DrawSpeakerName(nameRect, renderSpeaker, rightAligned, namePawn);
            }

            Rect textArea = new Rect(contentRect.x, contentRect.y + 20f, contentRect.width, contentRect.height - 70f);

            // Right-align player text (reuse 1v1 pattern exactly)
            if (rightAligned)
            {
                string measureText = System.Text.RegularExpressions.Regex.Replace(renderText, "<.*?>", "");
                GameFont prevFont = Text.Font;
                Text.Font = GameFont.Medium;
                Vector2 size = Text.CalcSize(measureText);
                Text.Font = prevFont;
                float clampedWidth = Mathf.Min(size.x * 1.6f + 40f, contentRect.width * 0.85f);
                textArea.x = contentRect.xMax - clampedWidth;
                textArea.width = clampedWidth;
                Text.Anchor = TextAnchor.UpperLeft;
            }

            // State transition: player text done → wait for AI → show NPC response
            Owner.CheckPlayerTextTransition();

            if (drawLive)
            {
                if (isSendingRequest && !isShowingPlayerText)
                {
                    string dots = new string('.', (int)(Time.time * 2) % 4);
                    string thinkingText = "RimChat_RPGThinking".Translate(dots);
                    Widgets.Label(textArea, $"<size=34><color=#b0b0b0>{thinkingText}</color></size>");
                }
                else if (isShowingPlayerText && isWaitingForPlayerDelay && !aiResponseReady
                    && Time.realtimeSinceStartup - timePlayerTextFinished >= 3.0f)
                {
                    string dots = new string('.', (int)(Time.time * 2) % 4);
                    string waitingText = "RimChat_RPGOpponentThinking".Translate(dots);
                    Widgets.Label(textArea, $"<size=34>{displayedText}\n<color=#b0b0b0>{waitingText}</color></size>");
                }
                else
                {
                    Owner.UpdateTyping();
                    string rawText = isTyping ? displayedText : currentDialogueText;
                    if (string.IsNullOrWhiteSpace(rawText)) rawText = "…";
                    string visibleText = Owner.ResolvePagedText(rawText, textArea);
                    Widgets.Label(textArea, $"<size=34>{visibleText}</size>");
                }
            }
            else
            {
                Widgets.Label(textArea, $"<size=34>{renderText}</size>");
            }

            if (rightAligned)
                Text.Anchor = TextAnchor.UpperLeft;

            // Player input: only when NOT showing player text, NOT typing, NOT sending
            if (!isTyping && !isSendingRequest && !isShowingPlayerText && drawLive && isPlayerTurn)
            {
                float inputHeight = 45f;
                Rect bottomArea = new Rect(contentRect.x, contentRect.yMax - inputHeight, contentRect.width, inputHeight);
                bool isFocused = GUI.GetNameOfFocusedControl() == UserReplyInputControlName;
                bool mouseInBottom = Mouse.IsOver(bottomArea);
                float targetAlpha = (mouseInBottom || isFocused) ? 1.0f : 0.25f;
                inputAlpha = Mathf.Lerp(inputAlpha, targetAlpha, 0.12f);

                GUI.color = new Color(1f, 1f, 1f, inputAlpha);

                Rect inputRect = new Rect(bottomArea.x, bottomArea.y, bottomArea.width - 150f, inputHeight);
                if (inputAlpha < 0.9f)
                    Widgets.DrawBoxSolid(inputRect, new Color(1f, 1f, 1f, 0.05f));

                GUI.SetNextControlName(UserReplyInputControlName);
                if (Owner.ShouldSendFromKeyboard())
                {
                    Event.current.Use();
                    Owner.TrySendPlayerMessage();
                }
                userReplyText = Widgets.TextField(inputRect, userReplyText);

                Rect sendRect = new Rect(bottomArea.xMax - 135f, bottomArea.y, 135f, inputHeight);
                string sendLabel = "RimChat_SendButton".Translate();
                if (inputAlpha < 0.5f)
                {
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(sendRect, sendLabel);
                    Text.Anchor = TextAnchor.UpperLeft;
                    if (Widgets.ButtonInvisible(sendRect)) Owner.TrySendPlayerMessage();
                }
                else
                {
                    if (Widgets.ButtonText(sendRect, sendLabel)) Owner.TrySendPlayerMessage();
                }
                GUI.color = Color.white;
            }

            Owner.DrawDialogueNavigation(boxRect);
        }

internal void DrawDialogueNavigation(Rect boxRect)
        {
            // Page navigation (text too long)
            if (currentTextPages.Count > 1 && !isTyping && !isSendingRequest)
            {
                Rect pageBox = new Rect(boxRect.xMax - 220f, boxRect.yMax - 30f, 100f, 25f);
                Dialog_RPGPawnGroupChat.DrawNavBox(pageBox, currentTextPageIndex > 0, currentTextPageIndex < currentTextPages.Count - 1,
                    $"{currentTextPageIndex + 1}/{currentTextPages.Count}",
                    () => currentTextPageIndex = Mathf.Max(0, currentTextPageIndex - 1),
                    () => currentTextPageIndex = Mathf.Min(currentTextPages.Count - 1, currentTextPageIndex + 1));
            }

            // History toggle button (opens center panel like 1v1)
            Owner.DrawHistoryToggleButton(boxRect);
        }

internal static void DrawNavBox(Rect boxRect, bool canGoPrev, bool canGoNext, string counter, Action onPrev, Action onNext)
        {
            GUI.color = Mouse.IsOver(boxRect) ? new Color(0.9f, 0.9f, 0.9f, 0.9f) : new Color(0.5f, 0.5f, 0.5f, 0.4f);
            Rect prevRect = new Rect(boxRect.x, boxRect.y, 30f, 25f);
            Rect countRect = new Rect(boxRect.x + 30f, boxRect.y, 40f, 25f);
            Rect nextRect = new Rect(boxRect.x + 70f, boxRect.y, 30f, 25f);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            if (canGoPrev && Widgets.ButtonInvisible(prevRect)) { onPrev?.Invoke(); SoundDefOf.Tick_Tiny.PlayOneShotOnCamera(); }
            Widgets.Label(prevRect, "<");
            Widgets.Label(countRect, counter);
            if (canGoNext && Widgets.ButtonInvisible(nextRect)) { onNext?.Invoke(); SoundDefOf.Tick_Tiny.PlayOneShotOnCamera(); }
            Widgets.Label(nextRect, ">");
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

internal void DrawClickToContinueHint(Rect inRect)
        {
            if (!pauseForClick || isPlayerTurn || isTyping || isSendingRequest) return;

            float hintY = inRect.height - 50f;
            Rect hintRect = new Rect(0, hintY, inRect.width, 25f);
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = new Color(1f, 1f, 1f, ClickHintAlpha);
            Widgets.Label(hintRect, "RimChat_ClickToContinue".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

internal void DrawSpeakerName(Rect nameRect, string displayName, bool rightAligned, Pawn pawn = null)
        {
            bool hovered = Mouse.IsOver(nameRect);
            Color nameColor = hovered ? new Color(1f, 0.92f, 0.55f, 1f) : new Color(0.88f, 0.88f, 0.88f, 1f);
            string colorHex = ColorUtility.ToHtmlStringRGB(nameColor);

            GameFont prevFont = Text.Font;
            Text.Font = GameFont.Medium;
            Vector2 textSize = Text.CalcSize(displayName);
            Text.Font = prevFont;
            float measuredWidth = textSize.x * 2.1f + 20f;

            Rect clickRect;
            if (rightAligned)
            {
                Text.Anchor = TextAnchor.UpperRight;
                clickRect = new Rect(nameRect.xMax - measuredWidth, nameRect.y, measuredWidth, nameRect.height);
            }
            else
            {
                clickRect = new Rect(nameRect.x, nameRect.y, measuredWidth, nameRect.height);
            }

            Widgets.Label(nameRect, $"<size=44><b><color=#{colorHex}>{displayName}</color></b></size>");
            Text.Anchor = TextAnchor.UpperLeft;

            if (pawn != null && Widgets.ButtonInvisible(clickRect))
            {
                Dialog_RPGPawnDialogue.ShowPawnMenuStatic(pawn);
            }

            if (hovered)
                TooltipHandler.TipRegion(clickRect, "RimChat_PawnMenu_HoverTooltip".Translate());
        }

internal void UpdateTyping()
        {
            if (!isTyping || visibleChars >= currentDialogueText.Length) return;

            float interval = 0.02f;
            if (Time.realtimeSinceStartup - lastCharTime > interval)
            {
                visibleChars++;
                if (visibleChars < currentDialogueText.Length && currentDialogueText[visibleChars - 1] == '<')
                {
                    int closeTagIndex = currentDialogueText.IndexOf('>', visibleChars - 1);
                    if (closeTagIndex != -1) visibleChars = closeTagIndex + 1;
                }
                displayedText = currentDialogueText.Substring(0, Mathf.Min(visibleChars, currentDialogueText.Length));
                lastCharTime = Time.realtimeSinceStartup;
                if (visibleChars % 3 == 0)
                    SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            }

            if (visibleChars >= currentDialogueText.Length)
            {
                isTyping = false;
                if (!isPlayerTurn)
                    pauseForClick = true;
            }
        }

internal bool ShouldSendFromKeyboard()
        {
            Event current = Event.current;
            if (current == null) return false;
            if (current.keyCode != KeyCode.Return && current.keyCode != KeyCode.KeypadEnter) return false;
            if (current.type != EventType.KeyDown && current.rawType != EventType.KeyDown) return false;
            if (current.alt) return false;
            if (!string.IsNullOrEmpty(Input.compositionString)) return false;
            if (GUI.GetNameOfFocusedControl() != UserReplyInputControlName) return false;
            return !string.IsNullOrWhiteSpace(userReplyText);
        }

internal void AddActionFeedback(string text, Color color)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            feedbackEntries.Add(new ActionFeedbackEntry { Text = text, Color = color, CreatedAt = Time.realtimeSinceStartup });
            if (feedbackEntries.Count > FeedbackMaxCount) feedbackEntries.RemoveAt(0);
        }

internal void DrawActionFeedback(Rect inRect)
        {
            // Remove expired
            float now = Time.realtimeSinceStartup;
            feedbackEntries.RemoveAll(e => now - e.CreatedAt > FeedbackDuration);
            if (feedbackEntries.Count == 0) return;

            // Anchor: right side, near initiator portrait
            float anchorX = inRect.width - Dialog_RPGPawnDialogue.PortraitWidth - 120f;
            float anchorY = inRect.height - Dialog_RPGPawnDialogue.DialogueBoxHeight - 80f;
            float y = anchorY;

            for (int i = feedbackEntries.Count - 1; i >= 0; i--)
            {
                var entry = feedbackEntries[i];
                float age = now - entry.CreatedAt;
                float alpha = Mathf.Clamp01((FeedbackDuration - age) / 1f);
                float rise = Mathf.SmoothStep(0f, 30f, Mathf.Clamp01(age / (FeedbackDuration - 0.5f)));

                Rect textRect = new Rect(anchorX, y - rise, 280f, 28f);
                GUI.color = new Color(0f, 0f, 0f, 0.5f * alpha);
                Widgets.Label(new Rect(textRect.x + 1, textRect.y + 2, textRect.width, textRect.height), entry.Text);
                GUI.color = new Color(entry.Color.r, entry.Color.g, entry.Color.b, 0.95f * alpha);
                Widgets.Label(textRect, entry.Text);
                GUI.color = Color.white;
                y -= 32f;
            }
        }

internal Color ResolveDialogueBoxColor()
        {
            Pawn currentPawn = null;
            if (!isPlayerTurn && currentSpeakerIndex >= 0 && currentSpeakerIndex < participants.Count)
                currentPawn = participants[currentSpeakerIndex].Pawn;
            if (currentPawn == null) return BoxDefault;

            if (currentPawn.relations?.DirectRelationExists(PawnRelationDefOf.Lover, initiator) == true
                || currentPawn.relations?.DirectRelationExists(PawnRelationDefOf.Fiance, initiator) == true
                || currentPawn.relations?.DirectRelationExists(PawnRelationDefOf.Spouse, initiator) == true)
                return BoxRomance;
            if (currentPawn.Faction == Faction.OfPlayer) return BoxDefault;
            if (currentPawn.IsPrisoner || currentPawn.IsSlave) return BoxPrisoner;
            if (currentPawn.Faction?.HostileTo(Faction.OfPlayer) == true) return BoxHostile;
            return BoxNeutral;
        }

internal void CloseActiveRequestLease()
        {
            if (activeRequestLease == null) return;
            conversationController.CloseLease(activeRequestLease);
            activeRequestLease = null;
            activeRequestRuntimeContext = null;
        }

internal string ResolvePagedText(string fullText, Rect textArea)
        {
            if (string.IsNullOrEmpty(fullText)) return fullText;

            float areaHeight = textArea.height - 40f; // reserve space for nav
            if (!Owner.NeedsPaging(fullText, textArea))
            {
                currentTextPages.Clear();
                currentTextPageIndex = 0;
                return fullText;
            }

            Owner.EnsureTextPages(fullText, textArea.width, areaHeight);
            currentTextPageIndex = Mathf.Clamp(currentTextPageIndex, 0, Math.Max(0, currentTextPages.Count - 1));
            return currentTextPages.Count == 0 ? fullText : currentTextPages[currentTextPageIndex];
        }

internal bool NeedsPaging(string fullText, Rect textArea)
        {
            GameFont prevFont = Text.Font;
            Text.Font = GameFont.Medium;
            float measureWidth = Math.Max(1f, textArea.width - 8f);
            float height = Text.CalcHeight(fullText, measureWidth) + 4f;
            Text.Font = prevFont;
            return height > textArea.height - 40f;
        }

internal void EnsureTextPages(string fullText, float width, float height)
        {
            if (string.Equals(pagedTextCache, fullText) && Mathf.RoundToInt(pagedWidthCache) == Mathf.RoundToInt(width)
                && Mathf.RoundToInt(pagedHeightCache) == Mathf.RoundToInt(height))
                return;

            currentTextPages.Clear();
            currentTextPageIndex = 0;

            int startIndex = 0;
            float measureWidth = Math.Max(1f, width - 8f);
            GameFont prevFont = Text.Font;
            Text.Font = GameFont.Medium;

            while (startIndex < fullText.Length)
            {
                int low = 1;
                int high = fullText.Length - startIndex;
                int best = 1;
                while (low <= high)
                {
                    int mid = low + (high - low) / 2;
                    string candidate = fullText.Substring(startIndex, mid);
                    float h = Text.CalcHeight(candidate, measureWidth) + 4f;
                    if (h <= height) { best = mid; low = mid + 1; }
                    else high = mid - 1;
                }
                // Backtrack to sentence boundary
                int end = startIndex + best;
                if (end < fullText.Length)
                {
                    for (int i = end - 1; i > startIndex + best / 2; i--)
                    {
                        if (char.IsWhiteSpace(fullText[i]) || ",.;:!?，。！？；：".IndexOf(fullText[i]) >= 0)
                        { end = i; break; }
                    }
                }
                currentTextPages.Add(fullText.Substring(startIndex, Math.Min(end - startIndex, fullText.Length - startIndex)).Trim());
                startIndex = end;
                while (startIndex < fullText.Length && char.IsWhiteSpace(fullText[startIndex])) startIndex++;
            }
            Text.Font = prevFont;

            pagedTextCache = fullText;
            pagedWidthCache = width;
            pagedHeightCache = height;
        }
    }
}
