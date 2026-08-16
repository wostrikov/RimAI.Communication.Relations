using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Communication.Relations.UI
{
    /// <summary>
    /// Dependencies: Verse window stack and Unity GUI widgets.
    /// Responsibility: show version-log text in a scrollable read-only window.
    /// </summary>
    public class Dialog_VersionLogViewer : Window
    {
        private const float VersionBlockGap = 10f;
        private const float PageContentPadding = 8f;
        private const int MinLinesPerOverflowChunk = 8;

        private readonly string titleText;
        private readonly List<string> pagedContents;
        private Vector2 scrollPosition = Vector2.zero;
        private int currentPageIndex;

        public Dialog_VersionLogViewer(string title, string content)
        {
            titleText = string.IsNullOrWhiteSpace(title)
                ? "RimChat_VersionLogWindowTitle".Translate().ToString()
                : title;
            pagedContents = BuildPages(content ?? string.Empty);
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            doCloseX = true;
            doCloseButton = false;
        }

        public override Vector2 InitialSize => new Vector2(980f, 700f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Rect titleRect = new Rect(inRect.x, inRect.y, inRect.width, 32f);
            Widgets.Label(titleRect, titleText);
            Text.Font = GameFont.Small;

            Rect footerRect = new Rect(inRect.x, inRect.yMax - 30f, inRect.width, 28f);
            Rect bodyRect = new Rect(inRect.x, titleRect.yMax + 8f, inRect.width, footerRect.y - titleRect.yMax - 16f);
            DrawPagedBody(bodyRect);
            DrawFooter(footerRect);
        }

        private void DrawPagedBody(Rect bodyRect)
        {
            Widgets.DrawMenuSection(bodyRect);
            Rect viewportRect = bodyRect.ContractedBy(6f);
            string pageContent = GetCurrentPageContent();
            string renderedContent = RenderMarkdown(pageContent);
            float contentWidth = Mathf.Max(100f, viewportRect.width - 16f);
            float contentHeight = Mathf.Max(viewportRect.height, MeasureRenderedHeight(renderedContent, contentWidth) + 8f);
            Rect viewRect = new Rect(0f, 0f, contentWidth, contentHeight);
            Widgets.BeginScrollView(viewportRect, ref scrollPosition, viewRect);
            Widgets.Label(new Rect(0f, 0f, viewRect.width, viewRect.height), renderedContent);
            Widgets.EndScrollView();
        }

        private void DrawFooter(Rect footerRect)
        {
            bool hasPrev = currentPageIndex > 0;
            bool hasNext = currentPageIndex < pagedContents.Count - 1;

            Rect prevRect = new Rect(footerRect.x, footerRect.y, 120f, footerRect.height);
            GUI.color = hasPrev ? Color.white : Color.gray;
            if (Widgets.ButtonText(prevRect, "RimChat_ApiDebugPaginationPrev".Translate()) && hasPrev)
            {
                currentPageIndex--;
                scrollPosition = Vector2.zero;
            }

            GUI.color = Color.white;
            string pageInfo = "RimChat_VersionLogPaginationInfo".Translate(currentPageIndex + 1, pagedContents.Count);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(footerRect.center.x - 110f, footerRect.y, 220f, footerRect.height), pageInfo);
            Text.Anchor = TextAnchor.UpperLeft;

            Rect nextRect = new Rect(footerRect.xMax - 246f, footerRect.y, 120f, footerRect.height);
            GUI.color = hasNext ? Color.white : Color.gray;
            if (Widgets.ButtonText(nextRect, "RimChat_ApiDebugPaginationNext".Translate()) && hasNext)
            {
                currentPageIndex++;
                scrollPosition = Vector2.zero;
            }

            GUI.color = Color.white;
            Rect closeRect = new Rect(footerRect.xMax - 120f, footerRect.y, 120f, footerRect.height);
            if (Widgets.ButtonText(closeRect, "RimChat_CloseButton".Translate()))
            {
                Close();
            }
        }

        private string GetCurrentPageContent()
        {
            if (pagedContents == null || pagedContents.Count == 0)
            {
                return string.Empty;
            }

            int safeIndex = Mathf.Clamp(currentPageIndex, 0, pagedContents.Count - 1);
            return pagedContents[safeIndex] ?? string.Empty;
        }

        private List<string> BuildPages(string content)
        {
            List<VersionBlock> blocks = ParseBlocks(content);
            if (blocks.Count == 0)
            {
                return new List<string> { content ?? string.Empty };
            }

            float availableHeight = 560f;
            float availableWidth = 952f;
            var pages = new List<string>();
            var currentPageBlocks = new List<string>();
            float currentHeight = 0f;

            for (int i = 0; i < blocks.Count; i++)
            {
                List<string> fragments = SplitBlockIfNeeded(blocks[i], availableWidth, availableHeight);
                for (int j = 0; j < fragments.Count; j++)
                {
                    string fragment = fragments[j];
                    float fragmentHeight = MeasureBlockHeight(fragment, availableWidth);
                    float projectedHeight = currentPageBlocks.Count == 0
                        ? fragmentHeight
                        : currentHeight + VersionBlockGap + fragmentHeight;
                    if (currentPageBlocks.Count > 0 && projectedHeight > availableHeight)
                    {
                        pages.Add(string.Join("\n\n", currentPageBlocks));
                        currentPageBlocks.Clear();
                        currentHeight = 0f;
                    }

                    currentPageBlocks.Add(fragment);
                    currentHeight = currentPageBlocks.Count == 1
                        ? fragmentHeight
                        : currentHeight + VersionBlockGap + fragmentHeight;
                }
            }

            if (currentPageBlocks.Count > 0)
            {
                pages.Add(string.Join("\n\n", currentPageBlocks));
            }

            return pages.Count == 0 ? new List<string> { string.Empty } : pages;
        }

        private List<VersionBlock> ParseBlocks(string content)
        {
            var blocks = new List<VersionBlock>();
            string normalized = (content ?? string.Empty).Replace("\r", string.Empty);
            string[] lines = normalized.Split('\n');
            string currentHeader = null;
            var currentBody = new List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i] ?? string.Empty;
                if (IsVersionHeader(line))
                {
                    if (!string.IsNullOrWhiteSpace(currentHeader))
                    {
                        blocks.Add(new VersionBlock(currentHeader, currentBody));
                        currentBody = new List<string>();
                    }

                    currentHeader = line.Trim();
                    continue;
                }

                if (currentHeader == null)
                {
                    continue;
                }

                currentBody.Add(line);
            }

            if (!string.IsNullOrWhiteSpace(currentHeader))
            {
                blocks.Add(new VersionBlock(currentHeader, currentBody));
            }

            return blocks;
        }

        private List<string> SplitBlockIfNeeded(VersionBlock block, float width, float maxHeight)
        {
            string fullBlock = block.ToDisplayText();
            if (MeasureBlockHeight(fullBlock, width) <= maxHeight)
            {
                return new List<string> { fullBlock };
            }

            var fragments = new List<string>();
            var lines = new List<string>();
            lines.AddRange(block.BodyLines);
            int startIndex = 0;
            while (startIndex < lines.Count)
            {
                int taken = 0;
                string fragment = block.Header;
                for (int i = startIndex; i < lines.Count; i++)
                {
                    int candidateCount = i - startIndex + 1;
                    string candidate = BuildBlockFragment(block.Header, lines, startIndex, candidateCount);
                    if (MeasureBlockHeight(candidate, width) > maxHeight && taken >= MinLinesPerOverflowChunk)
                    {
                        break;
                    }

                    fragment = candidate;
                    taken = candidateCount;
                }

                if (taken <= 0)
                {
                    taken = Math.Min(MinLinesPerOverflowChunk, lines.Count - startIndex);
                    fragment = BuildBlockFragment(block.Header, lines, startIndex, taken);
                }

                fragments.Add(fragment);
                startIndex += taken;
            }

            return fragments;
        }

        private float MeasureBlockHeight(string blockText, float width)
        {
            Text.Font = GameFont.Small;
            string rendered = RenderMarkdown(blockText ?? string.Empty);
            float textHeight = MeasureRenderedHeight(rendered, Mathf.Max(100f, width - PageContentPadding * 2f));
            return textHeight + PageContentPadding * 2f;
        }

        private static float MeasureRenderedHeight(string renderedText, float width)
        {
            if (string.IsNullOrWhiteSpace(renderedText))
            {
                return Text.LineHeight;
            }

            string[] lines = renderedText.Replace("\r", string.Empty).Split('\n');
            float total = 0f;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i] ?? string.Empty;
                total += Text.CalcHeight(line, width);
            }

            return Mathf.Max(total, Text.LineHeight);
        }

        private static string RenderMarkdown(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            string normalized = text.Replace("\r", string.Empty);
            string[] lines = normalized.Split('\n');
            var sb = new StringBuilder(normalized.Length + 128);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i] ?? string.Empty;
                sb.AppendLine(RenderMarkdownLine(line));
            }

            return sb.ToString().TrimEnd();
        }

        private static string RenderMarkdownLine(string line)
        {
            string value = line ?? string.Empty;
            string trimmed = value.TrimStart();
            if (trimmed.StartsWith("### ", StringComparison.Ordinal))
            {
                return "<b>" + ApplyInlineMarkdown(trimmed.Substring(4).Trim()) + "</b>";
            }

            if (trimmed.StartsWith("## ", StringComparison.Ordinal))
            {
                return "<b>" + ApplyInlineMarkdown(trimmed.Substring(3).Trim()) + "</b>";
            }

            if (trimmed.StartsWith("# ", StringComparison.Ordinal))
            {
                return "<b>" + ApplyInlineMarkdown(trimmed.Substring(2).Trim()) + "</b>";
            }

            if (trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                return "• " + ApplyInlineMarkdown(trimmed.Substring(2));
            }

            return ApplyInlineMarkdown(value);
        }

        private static string ApplyInlineMarkdown(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            string value = text;
            while (TryReplaceDelimited(value, "**", "<b>", "</b>", out string replacedBold))
            {
                value = replacedBold;
            }

            while (TryReplaceDelimited(value, "`", "<color=#B7D5FF>", "</color>", out string replacedCode))
            {
                value = replacedCode;
            }

            return value;
        }

        private static bool TryReplaceDelimited(string text, string delimiter, string openTag, string closeTag, out string replaced)
        {
            replaced = text ?? string.Empty;
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(delimiter))
            {
                return false;
            }

            int start = text.IndexOf(delimiter, StringComparison.Ordinal);
            if (start < 0)
            {
                return false;
            }

            int end = text.IndexOf(delimiter, start + delimiter.Length, StringComparison.Ordinal);
            if (end < 0)
            {
                return false;
            }

            string inner = text.Substring(start + delimiter.Length, end - start - delimiter.Length);
            replaced = text.Substring(0, start) + openTag + inner + closeTag + text.Substring(end + delimiter.Length);
            return true;
        }

        private static string BuildBlockFragment(string header, List<string> lines, int startIndex, int count)
        {
            var sb = new StringBuilder();
            sb.AppendLine(header);
            for (int i = 0; i < count; i++)
            {
                sb.AppendLine(lines[startIndex + i]);
            }

            return sb.ToString().TrimEnd();
        }

        private static bool IsVersionHeader(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            string value = line.Trim();
            if (value.Length < 5)
            {
                return false;
            }

            int dotCount = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '.')
                {
                    dotCount++;
                    continue;
                }

                if (!char.IsDigit(c))
                {
                    return false;
                }
            }

            return dotCount == 2;
        }

        private sealed class VersionBlock
        {
            public VersionBlock(string header, List<string> bodyLines)
            {
                Header = header ?? string.Empty;
                BodyLines = bodyLines ?? new List<string>();
            }

            public string Header { get; }

            public List<string> BodyLines { get; }

            public string ToDisplayText()
            {
                return BuildBlockFragment(Header, BodyLines, 0, BodyLines.Count);
            }
        }
    }
}

