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
    using StatusFilterMode = Dialog_ApiDebugObservability.StatusFilterMode;
    using SourceFilterMode = Dialog_ApiDebugObservability.SourceFilterMode;
    internal sealed class ApiDebugObservabilitySlice1 : Dialog_ApiDebugObservabilityCollaborator
    {
        internal ApiDebugObservabilitySlice1(Dialog_ApiDebugObservability owner) : base(owner)
        {
        }

public void DoWindowContents(Rect inRect)
        {
            Owner.RefreshSnapshot(force: false);

            float y = inRect.y;
            Owner.DrawHeader(new Rect(inRect.x, y, inRect.width, HeaderHeight));
            y += HeaderHeight + SectionGap;

            Owner.DrawSummaryCards(new Rect(inRect.x, y, inRect.width, SummaryHeight));
            y += SummaryHeight + SectionGap;

            Owner.DrawTrendSection(new Rect(inRect.x, y, inRect.width, TrendHeight));
            y += TrendHeight + SectionGap;

            float bottomHeight = inRect.yMax - y;
            const float panelGap = 8f;
            const float baseDetailRatio = 0.34f;
            const float detailWidthMultiplier = 1.30f; // Widen detail panel by 30%.
            float detailWidth = Mathf.Clamp(inRect.width * baseDetailRatio * detailWidthMultiplier, 300f, inRect.width - 420f);
            float listWidth = inRect.width - detailWidth - panelGap;
            Rect listRect = new Rect(inRect.x, y, listWidth, bottomHeight);
            Rect detailRect = new Rect(listRect.xMax + panelGap, y, detailWidth, bottomHeight);
            List<AIRequestDebugRecord> filtered = Owner.DrawRecordsTable(listRect);
            Owner.DrawDetailPanel(detailRect, filtered);
        }

internal void RefreshSnapshot(bool force)
        {
            if (!force && Time.realtimeSinceStartup < nextRefreshAtRealtime)
            {
                return;
            }

            if (!AIChatServiceAsync.TryGetRequestDebugSnapshot(out AIRequestDebugSnapshot latest) || latest == null)
            {
                latest = new AIRequestDebugSnapshot
                {
                    GeneratedAtUtc = DateTime.UtcNow,
                    WindowMinutes = 30,
                    Buckets = new List<AIRequestDebugBucket>(),
                    Records = new List<AIRequestDebugRecord>(),
                    Summary = new AIRequestDebugSummary(),
                    SessionSummary = new AIRequestDebugSessionSummary()
                };
            }

            snapshot = latest;
            nextRefreshAtRealtime = Time.realtimeSinceStartup + RefreshIntervalSeconds;
            Owner.EnsureSelectionStillValid();
        }

internal void EnsureSelectionStillValid()
        {
            List<AIRequestDebugRecord> filtered = Owner.GetFilteredRecords();
            if (filtered.Count == 0)
            {
                selectedRequestId = string.Empty;
                return;
            }

            bool exists = filtered.Any(record => string.Equals(record.RequestId, selectedRequestId, StringComparison.Ordinal));
            if (!exists)
            {
                selectedRequestId = filtered[0].RequestId;
            }
        }

internal void DrawHeader(Rect rect)
        {
            const float settingsButtonWidth = 120f;
            const float updatedLabelWidth = 250f;
            const float rightGap = 8f;

            Text.Font = GameFont.Medium;
            Widgets.Label(
                new Rect(rect.x, rect.y, Mathf.Max(120f, rect.width - settingsButtonWidth - updatedLabelWidth - rightGap * 3f), rect.height),
                "RimChat_ApiDebugWindowTitle".Translate());
            Text.Font = GameFont.Small;

            Rect settingsButtonRect = new Rect(
                rect.xMax - settingsButtonWidth,
                rect.y,
                settingsButtonWidth,
                rect.height);
            if (Widgets.ButtonText(settingsButtonRect, "RimChat_ApiDebugOpenSettingsButton".Translate()))
            {
                Dialog_ApiDebugObservability.TryOpenRelationsSettingsWindow();
            }

            TooltipHandler.TipRegion(settingsButtonRect, "RimChat_ApiDebugOpenSettingsButtonTooltip".Translate());

            string updatedText = "RimChat_ApiDebugLastUpdated".Translate(snapshot?.GeneratedAtUtc.ToLocalTime().ToString("HH:mm:ss") ?? "--");
            TextAnchor oldAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = Color.gray;
            Widgets.Label(
                new Rect(
                    settingsButtonRect.xMin - updatedLabelWidth - rightGap,
                    rect.y,
                    updatedLabelWidth,
                    rect.height),
                updatedText);
            GUI.color = Color.white;
            Text.Anchor = oldAnchor;
        }

internal static void TryOpenRelationsSettingsWindow()
        {
            RelationsMod rimChatMod = RelationsMod.Instance ?? LoadedModManager.GetMod<RelationsMod>();
            if (rimChatMod == null)
            {
                Messages.Message("RimChat_ApiDebugOpenSettingsFailed".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            Find.WindowStack?.Add(new Dialog_ModSettings(rimChatMod));
        }

internal void DrawSummaryCards(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(8f);
            AIRequestDebugSummary summary = snapshot?.Summary ?? new AIRequestDebugSummary();
            PromptRenderTelemetrySnapshot promptTelemetry = ScribanPromptEngine.GetTelemetrySnapshot();

            const float telemetryHeight = 30f;
            float cardHeight = Mathf.Max(56f, inner.height - telemetryHeight - 6f);
            float cardWidth = (inner.width - 20f) / 5f;
            Owner.DrawSummaryCard(new Rect(inner.x + (cardWidth + 5f) * 0f, inner.y, cardWidth, cardHeight), "RimChat_ApiDebugCardTotalTokens".Translate(), summary.TotalTokens.ToString("N0"));
            Owner.DrawSummaryCard(new Rect(inner.x + (cardWidth + 5f) * 1f, inner.y, cardWidth, cardHeight), "RimChat_ApiDebugCardRequestCount".Translate(), summary.RequestCount.ToString());
            Owner.DrawSummaryCard(new Rect(inner.x + (cardWidth + 5f) * 2f, inner.y, cardWidth, cardHeight), "RimChat_ApiDebugCardSuccessRate".Translate(), $"{summary.SuccessRatePercent:F1}%");
            Owner.DrawSummaryCard(new Rect(inner.x + (cardWidth + 5f) * 3f, inner.y, cardWidth, cardHeight), "RimChat_ApiDebugCardAverageLatency".Translate(), $"{summary.AverageDurationMs:F0} ms");
            Owner.DrawSummaryCard(new Rect(inner.x + (cardWidth + 5f) * 4f, inner.y, cardWidth, cardHeight), "RimChat_ApiDebugCardPriorityShare".Translate(), $"{summary.HighPriorityTokenSharePercent:F1}%");

            Rect telemetryRect = new Rect(inner.x, inner.y + cardHeight + 6f, inner.width, telemetryHeight);
            Dialog_ApiDebugObservability.DrawPromptTelemetryStrip(telemetryRect, promptTelemetry);
        }

internal static void DrawPromptTelemetryStrip(Rect rect, PromptRenderTelemetrySnapshot telemetry)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.13f, 0.13f, 0.15f));
            Widgets.DrawBox(rect);
            string text = "RimChat_ApiDebugScribanTelemetry".Translate(
                telemetry.CacheHitRatePercent.ToString("F1"),
                telemetry.CacheHits.ToString("N0"),
                telemetry.CacheMisses.ToString("N0"),
                telemetry.CacheEvictions.ToString("N0"),
                telemetry.AverageParseMilliseconds.ToString("F3"),
                telemetry.AverageRenderMilliseconds.ToString("F3"));
            Color oldColor = GUI.color;
            GameFont oldFont = Text.Font;
            GUI.color = Color.gray;
            Text.Font = GameFont.Tiny;
            Widgets.Label(rect.ContractedBy(6f, 4f), text);
            Text.Font = oldFont;
            GUI.color = oldColor;
        }

internal void DrawSummaryCard(Rect rect, string label, string value)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.13f, 0.13f, 0.13f));
            Widgets.DrawBox(rect);
            GUI.color = Color.gray;
            Widgets.Label(new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, 20f), label);
            GUI.color = Color.white;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x + 6f, rect.y + 32f, rect.width - 12f, 32f), value);
            Text.Font = GameFont.Small;
        }

internal void DrawTrendSection(Rect rect)
        {
            float statsWidth = Mathf.Min(TrendStatsPanelWidth, rect.width * 0.35f);
            float chartWidth = Mathf.Max(260f, rect.width - statsWidth - SectionGap);
            statsWidth = Mathf.Max(200f, rect.width - chartWidth - SectionGap);

            Rect chartRect = new Rect(rect.x, rect.y, chartWidth, rect.height);
            Rect statsRect = new Rect(chartRect.xMax + SectionGap, rect.y, statsWidth, rect.height);
            Owner.DrawTrendChart(chartRect);
            Owner.DrawSessionStatsPanel(statsRect);
        }

internal void DrawTrendChart(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(8f);
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 22f), "RimChat_ApiDebugTrendTitle".Translate());
            Rect chartRect = new Rect(inner.x, inner.y + 26f, inner.width, inner.height - 26f);
            List<AIRequestDebugBucket> buckets = snapshot?.Buckets ?? new List<AIRequestDebugBucket>();
            if (buckets.Count == 0 || buckets.All(bucket => bucket.TotalTokens <= 0))
            {
                GUI.color = Color.gray;
                Widgets.Label(chartRect, "RimChat_ApiDebugNoData".Translate());
                GUI.color = Color.white;
                return;
            }

            Owner.DrawTrendBars(chartRect, buckets);
        }

internal void DrawSessionStatsPanel(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(8f);
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 22f), "RimChat_ApiDebugSessionStatsTitle".Translate());

            AIRequestDebugSessionSummary sessionSummary = snapshot?.SessionSummary ?? new AIRequestDebugSessionSummary();
            Rect contentRect = new Rect(inner.x, inner.y + 26f, inner.width, inner.height - 26f);
            Widgets.DrawBoxSolid(contentRect, new Color(0.13f, 0.13f, 0.13f));
            Widgets.DrawBox(contentRect);

            float lineHeight = Mathf.Floor((contentRect.height - 8f) / 3f);
            Dialog_ApiDebugObservability.DrawSessionStatLine(
                new Rect(contentRect.x + 6f, contentRect.y + 4f, contentRect.width - 12f, lineHeight),
                "RimChat_ApiDebugSessionAvgRequestsPerMinute".Translate(),
                sessionSummary.AverageRequestsPerMinute.ToString("F2"));
            Dialog_ApiDebugObservability.DrawSessionStatLine(
                new Rect(contentRect.x + 6f, contentRect.y + 4f + lineHeight, contentRect.width - 12f, lineHeight),
                "RimChat_ApiDebugSessionAvgTokensPerMinute".Translate(),
                sessionSummary.AverageTokensPerMinute.ToString("F2"));
            Dialog_ApiDebugObservability.DrawSessionStatLine(
                new Rect(contentRect.x + 6f, contentRect.y + 4f + lineHeight * 2f, contentRect.width - 12f, lineHeight),
                "RimChat_ApiDebugSessionAvgTokensPerRequest".Translate(),
                sessionSummary.AverageTokensPerRequest.ToString("F2"));
        }

internal static void DrawSessionStatLine(Rect rect, string label, string value)
        {
            GUI.color = Color.gray;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 18f), label ?? string.Empty);
            GUI.color = Color.white;
            Widgets.Label(new Rect(rect.x, rect.y + 18f, rect.width, Mathf.Max(18f, rect.height - 18f)), value ?? "0.00");
        }

internal void DrawTrendBars(Rect chartRect, List<AIRequestDebugBucket> buckets)
        {
            int maxTokens = Mathf.Max(1, buckets.Max(bucket => Mathf.Max(0, bucket.TotalTokens)));
            int count = buckets.Count;
            float barWidth = Mathf.Max(8f, chartRect.width / Mathf.Max(1, count));
            for (int i = 0; i < count; i++)
            {
                AIRequestDebugBucket bucket = buckets[i];
                float normalized = Mathf.Clamp01((float)bucket.TotalTokens / maxTokens);
                float highPriorityNormalized = bucket.TotalTokens > 0
                    ? Mathf.Clamp01((float)bucket.HighPriorityTokens / maxTokens)
                    : 0f;
                float barHeight = (chartRect.height - 24f) * normalized;
                float highPriorityHeight = (chartRect.height - 24f) * highPriorityNormalized;
                Rect bar = new Rect(chartRect.x + i * barWidth + 2f, chartRect.yMax - 22f - barHeight, barWidth - 4f, barHeight);
                Rect priorityBar = new Rect(bar.x, chartRect.yMax - 22f - highPriorityHeight, bar.width, highPriorityHeight);
                Widgets.DrawBoxSolid(bar, new Color(0.35f, 0.35f, 0.35f, 0.75f));
                Widgets.DrawBoxSolid(priorityBar, new Color(0.2f, 0.65f, 0.95f, 0.95f));

                DateTime localBucketTime = bucket.BucketStartUtc.ToLocalTime();
                bool shouldDrawLabel = localBucketTime.Minute % 5 == 0 || i == 0 || i == count - 1;
                if (shouldDrawLabel)
                {
                    string label = localBucketTime.ToString("HH:mm");
                    TextAnchor oldAnchor = Text.Anchor;
                    Text.Anchor = TextAnchor.UpperCenter;
                    GUI.color = Color.gray;
                    Widgets.Label(new Rect(chartRect.x + i * barWidth, chartRect.yMax - 20f, barWidth, 20f), label);
                    GUI.color = Color.white;
                    Text.Anchor = oldAnchor;
                }
            }
        }

internal List<AIRequestDebugRecord> DrawRecordsTable(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(8f);

            Owner.DrawFilterRow(new Rect(inner.x, inner.y, inner.width, 24f));
            Owner.DrawCopyButtons(new Rect(inner.x, inner.y + 28f, inner.width, 24f));

            Rect headerRect = new Rect(inner.x, inner.y + 56f, inner.width, 22f);
            Owner.DrawTableHeader(headerRect);

            List<AIRequestDebugRecord> filtered = Owner.GetFilteredRecords();
            Rect paginationRect = new Rect(inner.x, headerRect.yMax + 2f, inner.width, 24f);
            Rect listRect = new Rect(inner.x, paginationRect.yMax + 2f, inner.width, inner.yMax - paginationRect.yMax - 2f);
            List<AIRequestDebugRecord> paged = Owner.GetPagedRecords(filtered, listRect.height, out int totalPages);
            Owner.DrawPaginationRow(paginationRect, filtered.Count, totalPages);
            Owner.DrawTableRows(listRect, paged);
            return filtered;
        }

internal void DrawFilterRow(Rect rect)
        {
            SourceFilterMode oldSourceFilter = sourceFilter;
            StatusFilterMode oldStatusFilter = statusFilter;
            float width = (rect.width - 18f) / 7f;
            Dialog_ApiDebugObservability.DrawFilterButton(new Rect(rect.x + (width + 3f) * 0f, rect.y, width, rect.height), "RimChat_ApiDebugFilterAll".Translate(), sourceFilter == SourceFilterMode.All, () => sourceFilter = SourceFilterMode.All);
            Dialog_ApiDebugObservability.DrawFilterButton(new Rect(rect.x + (width + 3f) * 1f, rect.y, width, rect.height), "RimChat_ApiDebugFilterPriority".Translate(), sourceFilter == SourceFilterMode.PriorityOnly, () => sourceFilter = SourceFilterMode.PriorityOnly);
            Dialog_ApiDebugObservability.DrawFilterButton(new Rect(rect.x + (width + 3f) * 2f, rect.y, width, rect.height), "RimChat_ApiDebugFilterBackground".Translate(), sourceFilter == SourceFilterMode.BackgroundOnly, () => sourceFilter = SourceFilterMode.BackgroundOnly);
            Dialog_ApiDebugObservability.DrawFilterButton(new Rect(rect.x + (width + 3f) * 3f, rect.y, width, rect.height), "RimChat_ApiDebugStatusAll".Translate(), statusFilter == StatusFilterMode.All, () => statusFilter = StatusFilterMode.All);
            Dialog_ApiDebugObservability.DrawFilterButton(new Rect(rect.x + (width + 3f) * 4f, rect.y, width, rect.height), "RimChat_ApiDebugStatusSuccess".Translate(), statusFilter == StatusFilterMode.Success, () => statusFilter = StatusFilterMode.Success);
            Dialog_ApiDebugObservability.DrawFilterButton(new Rect(rect.x + (width + 3f) * 5f, rect.y, width, rect.height), "RimChat_ApiDebugStatusError".Translate(), statusFilter == StatusFilterMode.Error, () => statusFilter = StatusFilterMode.Error);
            Dialog_ApiDebugObservability.DrawFilterButton(new Rect(rect.x + (width + 3f) * 6f, rect.y, width, rect.height), "RimChat_ApiDebugStatusCancelled".Translate(), statusFilter == StatusFilterMode.Cancelled, () => statusFilter = StatusFilterMode.Cancelled);

            if (oldSourceFilter != sourceFilter || oldStatusFilter != statusFilter)
            {
                currentPageIndex = 0;
            }
        }

internal static void DrawFilterButton(Rect rect, string label, bool selected, Action onClick)
        {
            Color old = GUI.color;
            GUI.color = selected ? new Color(0.35f, 0.65f, 0.95f) : Color.white;
            if (Widgets.ButtonText(rect, label))
            {
                onClick?.Invoke();
            }

            GUI.color = old;
        }

internal void DrawCopyButtons(Rect rect)
        {
            float rightButtonWidth = 190f;
            Rect copySelectedRect = new Rect(rect.xMax - rightButtonWidth * 2f - 6f, rect.y, rightButtonWidth, rect.height);
            Rect copyFilteredRect = new Rect(rect.xMax - rightButtonWidth, rect.y, rightButtonWidth, rect.height);

            if (Widgets.ButtonText(copySelectedRect, "RimChat_ApiDebugCopySelectedJson".Translate()))
            {
                Owner.TryCopySelectedRecordJson();
            }

            if (Widgets.ButtonText(copyFilteredRect, "RimChat_ApiDebugCopyFilteredJson".Translate()))
            {
                Owner.TryCopyFilteredJson();
            }
        }

internal void DrawTableHeader(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.18f, 0.18f, 0.18f));
            Rect[] columns = Dialog_ApiDebugObservability.BuildTableColumns(rect);
            Dialog_ApiDebugObservability.DrawTableCell(columns[0], "RimChat_ApiDebugColumnTime".Translate(), Color.gray);
            Dialog_ApiDebugObservability.DrawTableCell(columns[1], "RimChat_ApiDebugColumnSource".Translate(), Color.gray);
            Dialog_ApiDebugObservability.DrawTableCell(columns[2], "RimChat_ApiDebugColumnStatus".Translate(), Color.gray);
            Dialog_ApiDebugObservability.DrawTableCell(columns[3], "RimChat_ApiDebugColumnModel".Translate(), Color.gray);
            Dialog_ApiDebugObservability.DrawTableCell(columns[4], "RimChat_ApiDebugColumnTokens".Translate(), Color.gray);
            Dialog_ApiDebugObservability.DrawTableCell(columns[5], "RimChat_ApiDebugColumnLatency".Translate(), Color.gray);
            Dialog_ApiDebugObservability.DrawTableCell(columns[6], "RimChat_ApiDebugColumnHttp".Translate(), Color.gray);
        }

internal void DrawTableRows(Rect rect, List<AIRequestDebugRecord> records)
        {
            if (records == null || records.Count == 0)
            {
                GUI.color = Color.gray;
                Widgets.Label(rect, "RimChat_ApiDebugNoData".Translate());
                GUI.color = Color.white;
                return;
            }

            for (int i = 0; i < records.Count; i++)
            {
                Owner.DrawTableRow(new Rect(rect.x, rect.y + i * RowHeight, rect.width, RowHeight), records[i]);
            }
        }

internal List<AIRequestDebugRecord> GetPagedRecords(List<AIRequestDebugRecord> filtered, float listHeight, out int totalPages)
        {
            int pageSize = Mathf.Max(1, Mathf.FloorToInt(Mathf.Max(RowHeight, listHeight) / RowHeight));
            int totalCount = filtered?.Count ?? 0;
            totalPages = Mathf.Max(1, Mathf.CeilToInt(totalCount / (float)pageSize));
            currentPageIndex = Mathf.Clamp(currentPageIndex, 0, totalPages - 1);
            if (totalCount == 0)
            {
                return new List<AIRequestDebugRecord>();
            }

            int startIndex = currentPageIndex * pageSize;
            int count = Mathf.Min(pageSize, totalCount - startIndex);
            if (count <= 0)
            {
                return new List<AIRequestDebugRecord>();
            }

            return filtered.GetRange(startIndex, count);
        }

internal void DrawPaginationRow(Rect rect, int totalCount, int totalPages)
        {
            const float buttonWidth = 72f;
            const float gap = 6f;
            Rect firstRect = new Rect(rect.x, rect.y, buttonWidth, rect.height);
            Rect prevRect = new Rect(firstRect.xMax + gap, rect.y, buttonWidth, rect.height);
            Rect nextRect = new Rect(rect.xMax - buttonWidth * 2f - gap, rect.y, buttonWidth, rect.height);
            Rect lastRect = new Rect(rect.xMax - buttonWidth, rect.y, buttonWidth, rect.height);
            Rect labelRect = new Rect(prevRect.xMax + gap, rect.y, nextRect.x - prevRect.xMax - gap * 2f, rect.height);

            bool canGoPrevious = currentPageIndex > 0;
            bool canGoNext = currentPageIndex < totalPages - 1;
            if (canGoPrevious && Widgets.ButtonText(firstRect, "RimChat_ApiDebugPaginationFirst".Translate()))
            {
                currentPageIndex = 0;
            }

            if (canGoPrevious && Widgets.ButtonText(prevRect, "RimChat_ApiDebugPaginationPrev".Translate()))
            {
                currentPageIndex = Mathf.Max(0, currentPageIndex - 1);
            }

            if (canGoNext && Widgets.ButtonText(nextRect, "RimChat_ApiDebugPaginationNext".Translate()))
            {
                currentPageIndex = Mathf.Min(totalPages - 1, currentPageIndex + 1);
            }

            if (canGoNext && Widgets.ButtonText(lastRect, "RimChat_ApiDebugPaginationLast".Translate()))
            {
                currentPageIndex = totalPages - 1;
            }

            GUI.color = Color.gray;
            Widgets.Label(
                labelRect,
                "RimChat_ApiDebugPaginationInfo".Translate((currentPageIndex + 1).ToString(), totalPages.ToString(), totalCount.ToString("N0")));
            GUI.color = Color.white;
        }
    }
}
