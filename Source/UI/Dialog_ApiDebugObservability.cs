using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Prompting;
using RimWorld;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Communication.Relations.UI
{
    /// <summary>
    /// Dependencies: AIChatServiceAsync telemetry snapshot API and RimWorld Window/Widgets.
    /// Responsibility: present in-memory API debug observability with summary, trend, list, detail, and JSON copy.
    /// </summary>
    public sealed class Dialog_ApiDebugObservability : Window
    {
        internal Dialog_ApiDebugObservabilityParts Parts;

        internal const float RefreshIntervalSeconds = 2f;
        internal const float HeaderHeight = 30f;
        internal const float SummaryHeight = 128f;
        internal const float TrendHeight = 180f;
        internal const float SectionGap = 8f;
        internal const float RowHeight = 24f;
        internal const float TrendStatsPanelWidth = 300f;
        internal const string DetailSearchHighlightColor = "#0539A3";
        internal static readonly GUIStyle DetailTextStyle = new GUIStyle(GUI.skin.textArea)
        {
            wordWrap = true,
            richText = true
        };
        internal static readonly float[] TableColumnWeights = { 0.11f, 0.18f, 0.11f, 0.26f, 0.12f, 0.12f, 0.10f };

        internal enum SourceFilterMode
        {
            All = 0,
            PriorityOnly = 1,
            BackgroundOnly = 2
        }

        internal enum StatusFilterMode
        {
            All = 0,
            Success = 1,
            Error = 2,
            Cancelled = 3
        }

        internal AIRequestDebugSnapshot snapshot;
        internal float nextRefreshAtRealtime;
        internal Vector2 detailScrollPosition = Vector2.zero;
        internal string selectedRequestId = string.Empty;
        internal SourceFilterMode sourceFilter = SourceFilterMode.All;
        internal StatusFilterMode statusFilter = StatusFilterMode.All;
        internal int currentPageIndex;
        internal string detailSearchInput = string.Empty;
        internal string detailSearchApplied = string.Empty;
        internal float detailSearchApplyAtRealtime;
        internal string detailCacheRequestId = string.Empty;
        internal string detailCacheSearchQuery = string.Empty;
        internal string detailCacheContent = string.Empty;

        public override Vector2 InitialSize => new Vector2(1400f, 860f);

        public Dialog_ApiDebugObservability()
        {
            Parts = new Dialog_ApiDebugObservabilityParts(this);
            forcePause = false;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            doCloseX = true;
            doCloseButton = false;
        }

        public override void PreOpen()
        {
            base.PreOpen();
            RefreshSnapshot(force: true);
        }

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        
    
        #region Cluster forwards
        public override void DoWindowContents(Rect inRect) => Parts.Slice1.DoWindowContents(inRect);
        internal void RefreshSnapshot(bool force) => Parts.Slice1.RefreshSnapshot(force);
        internal void EnsureSelectionStillValid() => Parts.Slice1.EnsureSelectionStillValid();
        internal void DrawHeader(Rect rect) => Parts.Slice1.DrawHeader(rect);
        internal static void TryOpenRelationsSettingsWindow() => ApiDebugObservabilitySlice1.TryOpenRelationsSettingsWindow();
        internal void DrawSummaryCards(Rect rect) => Parts.Slice1.DrawSummaryCards(rect);
        internal static void DrawPromptTelemetryStrip(Rect rect, PromptRenderTelemetrySnapshot telemetry) => ApiDebugObservabilitySlice1.DrawPromptTelemetryStrip(rect, telemetry);
        internal void DrawSummaryCard(Rect rect, string label, string value) => Parts.Slice1.DrawSummaryCard(rect, label, value);
        internal void DrawTrendSection(Rect rect) => Parts.Slice1.DrawTrendSection(rect);
        internal void DrawTrendChart(Rect rect) => Parts.Slice1.DrawTrendChart(rect);
        internal void DrawSessionStatsPanel(Rect rect) => Parts.Slice1.DrawSessionStatsPanel(rect);
        internal static void DrawSessionStatLine(Rect rect, string label, string value) => ApiDebugObservabilitySlice1.DrawSessionStatLine(rect, label, value);
        internal void DrawTrendBars(Rect chartRect, List<AIRequestDebugBucket> buckets) => Parts.Slice1.DrawTrendBars(chartRect, buckets);
        internal List<AIRequestDebugRecord> DrawRecordsTable(Rect rect) => Parts.Slice1.DrawRecordsTable(rect);
        internal void DrawFilterRow(Rect rect) => Parts.Slice1.DrawFilterRow(rect);
        internal static void DrawFilterButton(Rect rect, string label, bool selected, Action onClick) => ApiDebugObservabilitySlice1.DrawFilterButton(rect, label, selected, onClick);
        internal void DrawCopyButtons(Rect rect) => Parts.Slice1.DrawCopyButtons(rect);
        internal void DrawTableHeader(Rect rect) => Parts.Slice1.DrawTableHeader(rect);
        internal void DrawTableRows(Rect rect, List<AIRequestDebugRecord> records) => Parts.Slice1.DrawTableRows(rect, records);
        internal List<AIRequestDebugRecord> GetPagedRecords(List<AIRequestDebugRecord> filtered, float listHeight, out int totalPages) => Parts.Slice1.GetPagedRecords(filtered, listHeight, out totalPages);
        internal void DrawPaginationRow(Rect rect, int totalCount, int totalPages) => Parts.Slice1.DrawPaginationRow(rect, totalCount, totalPages);
        internal void DrawTableRow(Rect rect, AIRequestDebugRecord record) => Parts.Slice2.DrawTableRow(rect, record);
        internal static void DrawTableCell(Rect rect, string text, Color color) => ApiDebugObservabilitySlice2.DrawTableCell(rect, text, color);
        internal static Rect[] BuildTableColumns(Rect rect) => ApiDebugObservabilitySlice2.BuildTableColumns(rect);
        internal static int CalculateMaxChars(float width, float avgCharWidth) => ApiDebugObservabilitySlice2.CalculateMaxChars(width, avgCharWidth);
        internal void DrawDetailPanel(Rect rect, List<AIRequestDebugRecord> filtered) => Parts.Slice2.DrawDetailPanel(rect, filtered);
        internal void DrawDetailSearchBar(Rect rect) => Parts.Slice2.DrawDetailSearchBar(rect);
        internal void UpdateDetailSearchDebounced() => Parts.Slice2.UpdateDetailSearchDebounced();
        internal string GetDetailContentForView(AIRequestDebugRecord selected) => Parts.Slice2.GetDetailContentForView(selected);
        internal string BuildDetailSearchResultText(string rawText, string query) => Parts.Slice2.BuildDetailSearchResultText(rawText, query);
        internal static string HighlightSearchMatches(string line, string query) => ApiDebugObservabilitySlice2.HighlightSearchMatches(line, query);
        internal string BuildDetailText(AIRequestDebugRecord record) => Parts.Slice2.BuildDetailText(record);
        internal static void AppendDetailField(StringBuilder sb, string key, string value) => ApiDebugObservabilitySlice2.AppendDetailField(sb, key, value);
        internal static string FormatPayloadForDetail(string payload) => ApiDebugObservabilitySlice2.FormatPayloadForDetail(payload);
        internal static string TryPrettyPrintJson(string json) => ApiDebugObservabilitySlice2.TryPrettyPrintJson(json);
        internal static void AppendIndent(StringBuilder sb, int indent) => ApiDebugObservabilitySlice2.AppendIndent(sb, indent);
        internal List<AIRequestDebugRecord> GetFilteredRecords() => Parts.Slice2.GetFilteredRecords();
        internal AIRequestDebugRecord GetSelectedRecord(List<AIRequestDebugRecord> filtered) => Parts.Slice2.GetSelectedRecord(filtered);
        internal static string GetSourceLabel(AIRequestDebugSource source) => ApiDebugObservabilitySlice3.GetSourceLabel(source);
        internal static string GetStatusLabel(AIRequestDebugStatus status) => ApiDebugObservabilitySlice3.GetStatusLabel(status);
        internal static Color GetStatusColor(AIRequestDebugStatus status, Color fallback) => ApiDebugObservabilitySlice3.GetStatusColor(status, fallback);
        internal static string Shorten(string value, int maxLength) => ApiDebugObservabilitySlice3.Shorten(value, maxLength);
        internal void TryCopySelectedRecordJson() => Parts.Slice3.TryCopySelectedRecordJson();
        internal void TryCopyFilteredJson() => Parts.Slice3.TryCopyFilteredJson();
        internal string BuildFilteredJson(List<AIRequestDebugRecord> records) => Parts.Slice3.BuildFilteredJson(records);
        internal static string BuildRecordJson(AIRequestDebugRecord record) => ApiDebugObservabilitySlice3.BuildRecordJson(record);
        internal static string IndentRecordJson(string json, string indent) => ApiDebugObservabilitySlice3.IndentRecordJson(json, indent);
        internal static string EscapeJson(string value) => ApiDebugObservabilitySlice3.EscapeJson(value);
        #endregion
}
    internal sealed class ApiDebugObservabilitySlice2 : Dialog_ApiDebugObservabilityCollaborator
    {
        internal ApiDebugObservabilitySlice2(Dialog_ApiDebugObservability owner) : base(owner)
        {
        }

internal void DrawTableRow(Rect rect, AIRequestDebugRecord record)
        {
            bool selected = string.Equals(selectedRequestId, record.RequestId, StringComparison.Ordinal);
            Color rowBackground = selected ? new Color(0.16f, 0.28f, 0.44f, 0.95f) : new Color(0f, 0f, 0f, 0f);
            if (selected)
            {
                Widgets.DrawBoxSolid(rect, rowBackground);
            }
            else if (Mouse.IsOver(rect))
            {
                Widgets.DrawBoxSolid(rect, new Color(0.2f, 0.2f, 0.2f, 0.2f));
            }

            Color textColor = record.IsHighPrioritySource ? Color.white : new Color(0.62f, 0.62f, 0.62f);
            Rect[] columns = Dialog_ApiDebugObservability.BuildTableColumns(rect);
            Dialog_ApiDebugObservability.DrawTableCell(columns[0], record.RecordedAtUtc.ToLocalTime().ToString("HH:mm:ss"), textColor);
            Dialog_ApiDebugObservability.DrawTableCell(columns[1], Dialog_ApiDebugObservability.GetSourceLabel(record.Source), textColor);
            Dialog_ApiDebugObservability.DrawTableCell(columns[2], Dialog_ApiDebugObservability.GetStatusLabel(record.Status), Dialog_ApiDebugObservability.GetStatusColor(record.Status, textColor));
            Dialog_ApiDebugObservability.DrawTableCell(columns[3], Dialog_ApiDebugObservability.Shorten(record.Model, Dialog_ApiDebugObservability.CalculateMaxChars(columns[3].width, 7f)), textColor);
            Dialog_ApiDebugObservability.DrawTableCell(columns[4], record.TotalTokens.ToString("N0"), textColor);
            Dialog_ApiDebugObservability.DrawTableCell(columns[5], $"{record.DurationMs} ms", textColor);
            Dialog_ApiDebugObservability.DrawTableCell(columns[6], record.HttpStatusCode > 0 ? record.HttpStatusCode.ToString() : "-", textColor);

            if (Widgets.ButtonInvisible(rect))
            {
                selectedRequestId = record.RequestId;
            }
        }

internal static void DrawTableCell(Rect rect, string text, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            Widgets.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 2f), text ?? string.Empty);
            GUI.color = old;
        }

internal static Rect[] BuildTableColumns(Rect rect)
        {
            var columns = new Rect[TableColumnWeights.Length];
            float x = rect.x;
            for (int i = 0; i < TableColumnWeights.Length; i++)
            {
                float width = i == TableColumnWeights.Length - 1
                    ? rect.xMax - x
                    : Mathf.Floor(rect.width * TableColumnWeights[i]);
                width = Mathf.Max(24f, width);
                columns[i] = new Rect(x, rect.y, width, rect.height);
                x += width;
            }

            return columns;
        }

internal static int CalculateMaxChars(float width, float avgCharWidth)
        {
            int chars = Mathf.FloorToInt(Mathf.Max(6f, width - 10f) / Mathf.Max(1f, avgCharWidth));
            return Mathf.Clamp(chars, 6, 64);
        }

internal void DrawDetailPanel(Rect rect, List<AIRequestDebugRecord> filtered)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(8f);
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 22f), "RimChat_ApiDebugDetailTitle".Translate());
            AIRequestDebugRecord selected = Owner.GetSelectedRecord(filtered);
            if (selected == null)
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(inner.x, inner.y + 28f, inner.width, inner.height - 28f), "RimChat_ApiDebugNoSelection".Translate());
                GUI.color = Color.white;
                return;
            }

            Owner.UpdateDetailSearchDebounced();
            Rect searchRect = new Rect(inner.x, inner.y + 28f, inner.width, 24f);
            Owner.DrawDetailSearchBar(searchRect);
            string content = Owner.GetDetailContentForView(selected);
            Rect scrollRect = new Rect(inner.x, searchRect.yMax + 6f, inner.width, inner.yMax - searchRect.yMax - 6f);
            float viewWidth = Mathf.Max(1f, scrollRect.width - 16f);
            float textHeight = DetailTextStyle.CalcHeight(new GUIContent(content), viewWidth);
            float contentHeight = Mathf.Max(scrollRect.height, textHeight + 24f);
            Rect viewRect = new Rect(0f, 0f, viewWidth, contentHeight);
            Widgets.BeginScrollView(scrollRect, ref Owner.detailScrollPosition, viewRect);
            GUI.TextArea(new Rect(0f, 0f, viewWidth, textHeight + 8f), content, DetailTextStyle);
            Widgets.EndScrollView();
        }

internal void DrawDetailSearchBar(Rect rect)
        {
            string nextValue = Widgets.TextField(rect, detailSearchInput ?? string.Empty);
            if (string.Equals(nextValue, detailSearchInput, StringComparison.Ordinal))
            {
                return;
            }

            detailSearchInput = nextValue ?? string.Empty;
            detailSearchApplyAtRealtime = Time.realtimeSinceStartup + 0.2f;
        }

internal void UpdateDetailSearchDebounced()
        {
            if (string.Equals(detailSearchApplied, detailSearchInput, StringComparison.Ordinal))
            {
                return;
            }

            if (Time.realtimeSinceStartup < detailSearchApplyAtRealtime)
            {
                return;
            }

            detailSearchApplied = detailSearchInput ?? string.Empty;
            detailScrollPosition = Vector2.zero;
            detailCacheRequestId = string.Empty;
            detailCacheSearchQuery = string.Empty;
            detailCacheContent = string.Empty;
        }

internal string GetDetailContentForView(AIRequestDebugRecord selected)
        {
            if (selected == null)
            {
                return string.Empty;
            }

            string requestId = selected.RequestId ?? string.Empty;
            string query = detailSearchApplied ?? string.Empty;
            if (string.Equals(detailCacheRequestId, requestId, StringComparison.Ordinal) &&
                string.Equals(detailCacheSearchQuery, query, StringComparison.Ordinal))
            {
                return detailCacheContent ?? string.Empty;
            }

            string rawText = Owner.BuildDetailText(selected);
            string content = string.IsNullOrWhiteSpace(query)
                ? rawText
                : Owner.BuildDetailSearchResultText(rawText, query);
            detailCacheRequestId = requestId;
            detailCacheSearchQuery = query;
            detailCacheContent = content ?? string.Empty;
            return detailCacheContent;
        }

internal string BuildDetailSearchResultText(string rawText, string query)
        {
            string normalizedQuery = (query ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedQuery))
            {
                return rawText ?? string.Empty;
            }

            string[] lines = (rawText ?? string.Empty).Replace("\r", string.Empty).Split('\n');
            var matched = new List<int>();
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i]?.IndexOf(normalizedQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    matched.Add(i);
                }
            }

            if (matched.Count == 0)
            {
                return "RimChat_ApiDebugDetailSearchNoMatch".Translate(normalizedQuery).ToString();
            }

            var sb = new StringBuilder();
            sb.AppendLine("RimChat_ApiDebugDetailSearchSummary".Translate(normalizedQuery, matched.Count).ToString());
            sb.AppendLine();
            var emitted = new HashSet<int>();
            foreach (int index in matched.Take(50))
            {
                int start = Math.Max(0, index - 2);
                int end = Math.Min(lines.Length - 1, index + 2);
                if (start > 0)
                {
                    sb.AppendLine("...");
                }

                for (int lineIndex = start; lineIndex <= end; lineIndex++)
                {
                    if (!emitted.Add(lineIndex))
                    {
                        continue;
                    }

                    sb.AppendLine(Dialog_ApiDebugObservability.HighlightSearchMatches(lines[lineIndex] ?? string.Empty, normalizedQuery));
                }
            }

            return sb.ToString().TrimEnd();
        }

internal static string HighlightSearchMatches(string line, string query)
        {
            string source = line ?? string.Empty;
            string normalizedQuery = (query ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(normalizedQuery))
            {
                return source;
            }

            // Escape existing angle brackets in source text to prevent them from
            // being interpreted as rich-text tags by Unity's GUI.TextArea parser,
            // which would break the search highlight color rendering.
            string safeSource = source
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");

            string escapedQuery = Regex.Escape(normalizedQuery);
            return Regex.Replace(
                safeSource,
                escapedQuery,
                match => $"<color={DetailSearchHighlightColor}>{match.Value}</color>",
                RegexOptions.IgnoreCase);
        }

internal string BuildDetailText(AIRequestDebugRecord record)
        {
            var sb = new StringBuilder();
            Dialog_ApiDebugObservability.AppendDetailField(sb, "RimChat_ApiDebugColumnTime".Translate().ToString(), record.RecordedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
            Dialog_ApiDebugObservability.AppendDetailField(sb, "RimChat_ApiDebugColumnSource".Translate().ToString(), Dialog_ApiDebugObservability.GetSourceLabel(record.Source));
            Dialog_ApiDebugObservability.AppendDetailField(sb, "RimChat_ApiDebugColumnStatus".Translate().ToString(), Dialog_ApiDebugObservability.GetStatusLabel(record.Status));
            Dialog_ApiDebugObservability.AppendDetailField(sb, "RimChat_ApiDebugColumnModel".Translate().ToString(), record.Model ?? string.Empty);
            Dialog_ApiDebugObservability.AppendDetailField(sb, "RimChat_ApiDebugColumnTokens".Translate().ToString(), record.TotalTokens.ToString("N0"));
            Dialog_ApiDebugObservability.AppendDetailField(sb, "Prompt", record.PromptTokens.ToString("N0"));
            Dialog_ApiDebugObservability.AppendDetailField(sb, "Completion", record.CompletionTokens.ToString("N0"));
            Dialog_ApiDebugObservability.AppendDetailField(sb, "Estimated", record.IsEstimatedTokens ? "true" : "false");
            Dialog_ApiDebugObservability.AppendDetailField(sb, "RimChat_ApiDebugColumnLatency".Translate().ToString(), record.DurationMs + " ms");
            Dialog_ApiDebugObservability.AppendDetailField(sb, "RimChat_ApiDebugColumnHttp".Translate().ToString(), record.HttpStatusCode > 0 ? record.HttpStatusCode.ToString() : "-");
            if (!string.IsNullOrWhiteSpace(record.ErrorText))
            {
                Dialog_ApiDebugObservability.AppendDetailField(sb, "RimChat_ApiDebugErrorLabel".Translate().ToString(), record.ErrorText);
            }

            sb.AppendLine();
            sb.AppendLine("=== " + "RimChat_ApiDebugRequestLabel".Translate() + " ===");
            sb.AppendLine(Dialog_ApiDebugObservability.FormatPayloadForDetail(record.RequestText));
            sb.AppendLine();
            sb.AppendLine("=== " + "RimChat_ApiDebugResponseLabel".Translate() + " ===");
            sb.AppendLine(Dialog_ApiDebugObservability.FormatPayloadForDetail(record.ResponseText));
            return sb.ToString();
        }

internal static void AppendDetailField(StringBuilder sb, string key, string value)
        {
            sb.Append('[')
                .Append(key ?? string.Empty)
                .Append("] ")
                .AppendLine(value ?? string.Empty);
        }

internal static string FormatPayloadForDetail(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return "RimChat_ApiDebugEmptyPayload".Translate().ToString();
            }

            string text = payload.Trim();
            text = WebUtility.HtmlDecode(text);
            string pretty = Dialog_ApiDebugObservability.TryPrettyPrintJson(text);
            if (!string.IsNullOrWhiteSpace(pretty))
            {
                text = pretty;
            }

            text = text
                .Replace("\\r\\n", "\n")
                .Replace("\\n", "\n")
                .Replace("\\r", "\n")
                .Replace("\\t", "    ")
                .Replace("\\\"", "\"");

            text = text.Replace("\r\n", "\n").Replace('\r', '\n');
            return text;
        }

internal static string TryPrettyPrintJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return string.Empty;
            }

            string source = json.Trim();
            if (source.Length < 2)
            {
                return source;
            }

            char first = source[0];
            if (first != '{' && first != '[')
            {
                return source;
            }

            try
            {
                var sb = new StringBuilder(source.Length + 64);
                int indent = 0;
                bool inString = false;
                bool escaped = false;
                for (int i = 0; i < source.Length; i++)
                {
                    char ch = source[i];
                    if (inString)
                    {
                        sb.Append(ch);
                        if (escaped)
                        {
                            escaped = false;
                        }
                        else if (ch == '\\')
                        {
                            escaped = true;
                        }
                        else if (ch == '"')
                        {
                            inString = false;
                        }

                        continue;
                    }

                    switch (ch)
                    {
                        case '"':
                            inString = true;
                            sb.Append(ch);
                            break;
                        case '{':
                        case '[':
                            sb.Append(ch);
                            sb.Append('\n');
                            indent++;
                            Dialog_ApiDebugObservability.AppendIndent(sb, indent);
                            break;
                        case '}':
                        case ']':
                            sb.Append('\n');
                            indent = Math.Max(0, indent - 1);
                            Dialog_ApiDebugObservability.AppendIndent(sb, indent);
                            sb.Append(ch);
                            break;
                        case ',':
                            sb.Append(ch);
                            sb.Append('\n');
                            Dialog_ApiDebugObservability.AppendIndent(sb, indent);
                            break;
                        case ':':
                            sb.Append(": ");
                            break;
                        case '\r':
                        case '\n':
                        case '\t':
                            break;
                        default:
                            sb.Append(ch);
                            break;
                    }
                }

                return sb.ToString();
            }
            catch
            {
                return source;
            }
        }

internal static void AppendIndent(StringBuilder sb, int indent)
        {
            for (int i = 0; i < indent; i++)
            {
                sb.Append("  ");
            }
        }

internal List<AIRequestDebugRecord> GetFilteredRecords()
        {
            IEnumerable<AIRequestDebugRecord> query = snapshot?.Records ?? Enumerable.Empty<AIRequestDebugRecord>();
            switch (sourceFilter)
            {
                case Dialog_ApiDebugObservability.SourceFilterMode.PriorityOnly:
                    query = query.Where(record => record.IsHighPrioritySource);
                    break;
                case Dialog_ApiDebugObservability.SourceFilterMode.BackgroundOnly:
                    query = query.Where(record => !record.IsHighPrioritySource);
                    break;
            }

            switch (statusFilter)
            {
                case Dialog_ApiDebugObservability.StatusFilterMode.Success:
                    query = query.Where(record => record.Status == AIRequestDebugStatus.Success);
                    break;
                case Dialog_ApiDebugObservability.StatusFilterMode.Error:
                    query = query.Where(record => record.Status == AIRequestDebugStatus.Error);
                    break;
                case Dialog_ApiDebugObservability.StatusFilterMode.Cancelled:
                    query = query.Where(record => record.Status == AIRequestDebugStatus.Cancelled);
                    break;
            }

            return query.ToList();
        }

internal AIRequestDebugRecord GetSelectedRecord(List<AIRequestDebugRecord> filtered)
        {
            if (filtered == null || filtered.Count == 0)
            {
                return null;
            }

            return filtered.FirstOrDefault(record => string.Equals(record.RequestId, selectedRequestId, StringComparison.Ordinal))
                ?? filtered[0];
        }
    }

    internal sealed class Dialog_ApiDebugObservabilityParts
    {
        internal readonly Dialog_ApiDebugObservability Owner;
        internal readonly ApiDebugObservabilitySlice1 Slice1;
        internal readonly ApiDebugObservabilitySlice2 Slice2;
        internal readonly ApiDebugObservabilitySlice3 Slice3;
        internal Dialog_ApiDebugObservabilityParts(Dialog_ApiDebugObservability owner)
        {
            Owner = owner;
            Slice1 = new ApiDebugObservabilitySlice1(owner);
            Slice2 = new ApiDebugObservabilitySlice2(owner);
            Slice3 = new ApiDebugObservabilitySlice3(owner);
        }
    }

}
