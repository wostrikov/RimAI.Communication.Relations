using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Ustas.RimAI.Communication.Relations.UI
{
    /// <summary>/// Dependencies: RimWorld text measurement and Verse widgets.
    /// Responsibility: paginate oversized RPG dialogue text and draw message/history navigation.
    /// </summary>
        internal sealed class RPGPawnDialogueTextPaging : Dialog_RPGPawnDialogueCollaborator
    {
        internal RPGPawnDialogueTextPaging(Dialog_RPGPawnDialogue owner) : base(owner)
        {
        }


        internal const int DialogueRenderFontSize = 34;
        internal const float DialogueMeasureSafetyPadding = 8f;
        internal const float DialogueMeasureRenderScale = DialogueRenderFontSize / 22f;

        internal readonly List<string> currentTextPages = new List<string>();
        internal string pagedTextCache = string.Empty;
        internal string pagedSpeakerCache = string.Empty;
        internal float pagedWidthCache = -1f;
        internal float pagedHeightCache = -1f;
        internal bool pagedLiveCache;
        internal int currentTextPageIndex = 0;

        internal string ResolveDialogueTextForDisplay(bool drawLive, string speakerName, string fullText, Rect textArea)
        {
            if (!Owner.CanPageCurrentDialogue(drawLive))
            {
                Owner.ResetDialogueTextPaging();
                return fullText ?? string.Empty;
            }

            Owner.EnsureDialogueTextPages(fullText, speakerName, textArea, drawLive);
            currentTextPageIndex = Mathf.Clamp(currentTextPageIndex, 0, Math.Max(0, currentTextPages.Count - 1));
            return currentTextPages.Count == 0 ? fullText ?? string.Empty : currentTextPages[currentTextPageIndex];
        }

        internal bool CanPageCurrentDialogue(bool drawLive)
        {
            if (!drawLive)
            {
                return true;
            }

            bool waitingForNpc = isShowingUserText && isWaitingForDelayAfterUser && !aiResponseReady &&
                Time.realtimeSinceStartup - timeUserTextFinished >= 3.0f;
            return !isTyping && !isSendingInitialMessage && !waitingForNpc;
        }

        internal void EnsureDialogueTextPages(string fullText, string speakerName, Rect textArea, bool drawLive)
        {
            string normalizedText = fullText ?? string.Empty;
            if (!Owner.RequiresDialogueTextPageRefresh(normalizedText, speakerName, textArea, drawLive))
            {
                return;
            }

            currentTextPages.Clear();
            currentTextPages.AddRange(Owner.BuildDialogueTextPages(normalizedText, textArea.width, textArea.height));
            currentTextPageIndex = 0;
            Owner.UpdateDialogueTextPageCache(normalizedText, speakerName, textArea, drawLive);
        }

        internal bool RequiresDialogueTextPageRefresh(string fullText, string speakerName, Rect textArea, bool drawLive)
        {
            return !string.Equals(pagedTextCache, fullText, StringComparison.Ordinal) ||
                   !string.Equals(pagedSpeakerCache, speakerName, StringComparison.Ordinal) ||
                   Mathf.RoundToInt(pagedWidthCache) != Mathf.RoundToInt(textArea.width) ||
                   Mathf.RoundToInt(pagedHeightCache) != Mathf.RoundToInt(textArea.height) ||
                   pagedLiveCache != drawLive;
        }

        internal void UpdateDialogueTextPageCache(string fullText, string speakerName, Rect textArea, bool drawLive)
        {
            pagedTextCache = fullText ?? string.Empty;
            pagedSpeakerCache = speakerName ?? string.Empty;
            pagedWidthCache = textArea.width;
            pagedHeightCache = textArea.height;
            pagedLiveCache = drawLive;
        }

        internal List<string> BuildDialogueTextPages(string fullText, float width, float height)
        {
            var pages = new List<string>();
            if (string.IsNullOrWhiteSpace(fullText))
            {
                pages.Add(string.Empty);
                return pages;
            }

            int startIndex = 0;
            while (startIndex < fullText.Length)
            {
                int length = Owner.FindDialoguePageLength(fullText, startIndex, width, height);
                pages.Add(Owner.ExtractDialoguePageText(fullText, startIndex, length));
                startIndex = Owner.SkipDialoguePageSeparators(fullText, startIndex + length);
            }

            return pages.Count == 0 ? new List<string> { fullText.Trim() } : pages;
        }

        internal int FindDialoguePageLength(string fullText, int startIndex, float width, float height)
        {
            int remainingLength = Math.Max(1, fullText.Length - startIndex);
            int low = 1;
            int high = remainingLength;
            int best = 1;
            while (low <= high)
            {
                int mid = low + (high - low) / 2;
                if (Owner.DoesDialoguePageFit(fullText, startIndex, mid, width, height))
                {
                    best = mid;
                    low = mid + 1;
                    continue;
                }

                high = mid - 1;
            }

            return Owner.AdjustDialoguePageLength(fullText, startIndex, best);
        }

        internal bool DoesDialoguePageFit(string fullText, int startIndex, int length, float width, float height)
        {
            string candidate = Owner.ExtractDialoguePageText(fullText, startIndex, length);
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return true;
            }

            return Owner.CalcDialogueTextHeight(candidate, width) <= height;
        }

        internal float CalcDialogueTextHeight(string text, float width)
        {
            GameFont previousFont = Text.Font;
            Text.Font = GameFont.Medium;
            float measureWidth = Math.Max(1f, width - DialogueMeasureSafetyPadding);
            float baseHeight = Text.CalcHeight(text ?? string.Empty, measureWidth);
            float height = (baseHeight * DialogueMeasureRenderScale) + DialogueMeasureSafetyPadding;
            Text.Font = previousFont;
            return height;
        }

        internal int AdjustDialoguePageLength(string fullText, int startIndex, int rawLength)
        {
            if (startIndex + rawLength >= fullText.Length)
            {
                return rawLength;
            }

            int minLength = Math.Max(1, rawLength / 2);
            for (int offset = rawLength - 1; offset >= minLength; offset--)
            {
                if (Dialog_RPGPawnDialogue.IsDialoguePageBoundary(fullText[startIndex + offset - 1]))
                {
                    return offset;
                }
            }

            return rawLength;
        }

        internal static bool IsDialoguePageBoundary(char character)
        {
            return char.IsWhiteSpace(character) || ",.;:!?，。！？；：、)]}\"'".IndexOf(character) >= 0;
        }

        internal int SkipDialoguePageSeparators(string fullText, int startIndex)
        {
            int index = Math.Max(0, startIndex);
            while (index < fullText.Length && char.IsWhiteSpace(fullText[index]))
            {
                index++;
            }

            return index;
        }

        internal string ExtractDialoguePageText(string fullText, int startIndex, int length)
        {
            int safeLength = Math.Max(1, Math.Min(length, fullText.Length - startIndex));
            string text = fullText.Substring(startIndex, safeLength).Trim();
            return string.IsNullOrWhiteSpace(text) ? fullText.Substring(startIndex, safeLength) : text;
        }

        internal void ResetDialogueTextPaging()
        {
            currentTextPages.Clear();
            currentTextPageIndex = 0;
            pagedTextCache = string.Empty;
            pagedSpeakerCache = string.Empty;
            pagedWidthCache = -1f;
            pagedHeightCache = -1f;
            pagedLiveCache = false;
        }

        internal void DrawDialogueNavigation(Rect boxRect)
        {
            Owner.DrawTextPageNavigation(boxRect);
            Owner.DrawHistoryNavigation(boxRect);
            Owner.DrawHistoryToggleButton(boxRect);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        internal void DrawHistoryNavigation(Rect boxRect)
        {
            if (dialogPages.Count == 0)
            {
                return;
            }

            int currentDisplay = Owner.GetCurrentDialogueDisplayIndex();
            Rect historyBox = new Rect(boxRect.xMax - 110f, boxRect.yMax - 30f, 100f, 25f);
            Owner.DrawNavigationBox(historyBox, currentDisplay > 0, currentDisplay < dialogPages.Count - 1,
                $"{currentDisplay + 1}/{dialogPages.Count}",
                () => Owner.ShowDialogueHistoryAt(currentDisplay - 1),
                () => Owner.ShowDialogueHistoryAt(currentDisplay + 1));
        }

        internal void DrawTextPageNavigation(Rect boxRect)
        {
            if (currentTextPages.Count <= 1 || !Owner.CanPageCurrentDialogue(!isViewingHistory))
            {
                return;
            }

            Rect pageBox = new Rect(boxRect.xMax - 220f, boxRect.yMax - 30f, 100f, 25f);
            Owner.DrawNavigationBox(pageBox, currentTextPageIndex > 0, currentTextPageIndex < currentTextPages.Count - 1,
                $"{currentTextPageIndex + 1}/{currentTextPages.Count}",
                () => Owner.ChangeDialogueTextPage(-1),
                () => Owner.ChangeDialogueTextPage(1));
        }

        internal void DrawNavigationBox(Rect boxRect, bool canGoPrev, bool canGoNext, string counterLabel, Action onPrev, Action onNext)
        {
            GUI.color = Mouse.IsOver(boxRect)
                ? new Color(0.9f, 0.9f, 0.9f, 0.9f)
                : new Color(0.5f, 0.5f, 0.5f, 0.4f);

            Rect prevRect = new Rect(boxRect.x, boxRect.y, 30f, 25f);
            Rect countRect = new Rect(boxRect.x + 30f, boxRect.y, 40f, 25f);
            Rect nextRect = new Rect(boxRect.x + 70f, boxRect.y, 30f, 25f);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;

            Owner.DrawNavigationButton(prevRect, canGoPrev, "<", onPrev);
            Widgets.Label(countRect, counterLabel);
            Owner.DrawNavigationButton(nextRect, canGoNext, ">", onNext);
        }

        internal void DrawNavigationButton(Rect rect, bool enabled, string label, Action onClick)
        {
            if (!enabled)
            {
                return;
            }

            if (Widgets.ButtonInvisible(rect))
            {
                onClick?.Invoke();
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            }

            Widgets.Label(rect, label);
        }

        internal int GetCurrentDialogueDisplayIndex()
        {
            return isViewingHistory ? historyViewIndex : Math.Max(0, dialogPages.Count - 1);
        }

        internal void ShowDialogueHistoryAt(int displayIndex)
        {
            historyViewIndex = Mathf.Clamp(displayIndex, 0, Math.Max(0, dialogPages.Count - 1));
            isViewingHistory = historyViewIndex < dialogPages.Count - 1;
            Owner.ResetDialogueTextPaging();
        }

        internal void ChangeDialogueTextPage(int direction)
        {
            currentTextPageIndex = Mathf.Clamp(currentTextPageIndex + direction, 0, Math.Max(0, currentTextPages.Count - 1));
        }
        }

}
