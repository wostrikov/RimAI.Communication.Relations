using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Communication.Relations.Memory;
using RimWorld;
using UnityEngine;
using Verse;

using DiplomacyHistoryRow = Ustas.RimAI.Communication.Relations.Memory.LeaderMemoryManager.DiplomacyHistoryRow;
using DiplomacyHistorySessionGroup = Ustas.RimAI.Communication.Relations.Memory.LeaderMemoryManager.DiplomacyHistorySessionGroup;

namespace Ustas.RimAI.Communication.Relations.UI
{
    /// <summary>
    /// Dependencies: diplomacy runtime session state, leader-memory history grouping, and Verse widgets.
    /// Responsibility: render a high-fidelity diplomacy history panel aligned with the RPG history panel, with single-select, double-click edit, and contextual delete.
    /// </summary>
    public sealed class Dialog_DiplomacyHistory : Window
    {
        private static readonly Color HistoryPanelBg = new Color(0.07f, 0.07f, 0.09f, 0.95f);
        private static readonly Color HistoryPanelBorder = new Color(0.25f, 0.25f, 0.3f, 1f);
        private static readonly Color HistoryRecordOddBg = new Color(1f, 1f, 1f, 0.025f);
        private static readonly Color HistoryRecordEvenBg = new Color(1f, 1f, 1f, 0.055f);
        private static readonly Color HistoryRecordSelectedBg = new Color(0.2f, 0.34f, 0.58f, 0.28f);
        private static readonly Color HistorySpeakerText = new Color(0.95f, 0.95f, 0.95f, 0.98f);
        private static readonly Color HistoryBodyText = new Color(0.9f, 0.9f, 0.92f, 0.98f);
        private static readonly Color HistoryMetaText = new Color(0.82f, 0.89f, 1f, 0.96f);
        private static readonly Color HistorySessionHeaderText = new Color(0.9f, 0.92f, 0.98f, 0.94f);

        private const float PanelPadding = 12f;
        private const float HeaderHeight = 44f;
        private const float SectionHeaderHeight = 26f;
        private const float DeleteButtonWidth = 28f;

        private readonly Faction currentFaction;
        private readonly LeaderMemoryManager historyManager;
        private Vector2 scrollPosition = Vector2.zero;
        private List<DiplomacyHistorySessionGroup> groups = new List<DiplomacyHistorySessionGroup>();
        private string selectedRowKey = string.Empty;
        private string lastDataSignature = string.Empty;
        private int lastObservedRevision = -1;

        public override Vector2 InitialSize => new Vector2(920f, 640f);

        public Dialog_DiplomacyHistory(Faction currentFaction)
        {
            this.currentFaction = currentFaction;
            historyManager = LeaderMemoryManager.Instance;
            draggable = true;
            doCloseX = false;
            closeOnAccept = false;
            closeOnCancel = true;
            absorbInputAroundWindow = true;
            RefreshGroups(forceResetSelection: true);
        }

        public override void DoWindowContents(Rect inRect)
        {
            RefreshGroups(forceResetSelection: false);

            Rect panelRect = inRect.ContractedBy(4f);
            Widgets.DrawBoxSolid(panelRect, HistoryPanelBg);
            GUI.color = HistoryPanelBorder;
            Widgets.DrawBox(panelRect, 2);
            GUI.color = Color.white;

            DrawHeader(panelRect);
            DrawBody(new Rect(
                panelRect.x + PanelPadding,
                panelRect.y + HeaderHeight,
                panelRect.width - PanelPadding * 2f,
                panelRect.height - HeaderHeight - PanelPadding));
        }

        private void DrawHeader(Rect panelRect)
        {
            Rect titleRect = new Rect(panelRect.x + 16f, panelRect.y + 10f, panelRect.width - 80f, 28f);
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, "RimChat_DiplomacyHistoryWindowTitle".Translate(currentFaction?.Name ?? string.Empty).ToString());
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            int totalCount = groups?.Sum(g => g?.Rows?.Count ?? 0) ?? 0;
            if (totalCount > 0)
            {
                Rect clearAllRect = new Rect(panelRect.xMax - 130f, panelRect.y + 10f, 80f, 28f);
                if (Widgets.ButtonText(clearAllRect, "RimChat_DiplomacyHistoryClearAll".Translate().ToString()))
                {
                    ExecuteClearAllHistory();
                }
            }

            Rect closeRect = new Rect(panelRect.xMax - 44f, panelRect.y + 10f, 28f, 28f);
            if (Widgets.ButtonText(closeRect, "×"))
            {
                Close();
            }
        }

        private void DrawBody(Rect rect)
        {
            if (groups.Count == 0)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rect, "RimChat_DiplomacyHistoryEmpty".Translate().ToString());
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            float viewWidth = rect.width - 18f;
            float totalHeight = CalculateContentHeight(viewWidth);
            Rect viewRect = new Rect(0f, 0f, viewWidth, Mathf.Max(rect.height, totalHeight));
            scrollPosition = GUI.BeginScrollView(rect, scrollPosition, viewRect);
            DrawGroups(viewRect.width);
            GUI.EndScrollView();
        }

        private float CalculateContentHeight(float width)
        {
            float total = 4f;
            foreach (DiplomacyHistorySessionGroup group in groups)
            {
                total += SectionHeaderHeight;
                foreach (DiplomacyHistoryRow row in group.Rows)
                {
                    total += MeasureRowHeight(row, width) + 2f;
                }

                total += 8f;
            }

            return Mathf.Max(total, 40f);
        }

        private void DrawGroups(float width)
        {
            float y = 0f;
            foreach (DiplomacyHistorySessionGroup group in groups)
            {
                Rect headerRect = new Rect(0f, y, width, SectionHeaderHeight);
                DrawGroupHeader(headerRect, group);
                y += SectionHeaderHeight;

                for (int i = 0; i < group.Rows.Count; i++)
                {
                    DiplomacyHistoryRow row = group.Rows[i];
                    float height = MeasureRowHeight(row, width);
                    Rect rowRect = new Rect(0f, y, width, height);
                    DrawRow(rowRect, row, i);
                    y += height + 1f;
                }

                y += 8f;
            }
        }

        private void DrawGroupHeader(Rect rect, DiplomacyHistorySessionGroup group)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.06f);
            Widgets.DrawLineHorizontal(rect.x, rect.y + rect.height - 1f, rect.width);
            GUI.color = HistorySessionHeaderText;
            Text.Font = GameFont.Small;
            Widgets.Label(rect, BuildGroupTitle(group));
            GUI.color = Color.white;
        }

        private string BuildGroupTitle(DiplomacyHistorySessionGroup group)
        {
            if (group == null)
            {
                return string.Empty;
            }

            if (group.IsCurrentSession)
            {
                return "RimChat_DiplomacyHistoryCurrentSession".Translate(group.Rows.Count).ToString();
            }

            return "RimChat_DiplomacyHistoryPastSession".Translate(group.SessionOrdinal, group.Rows.Count).ToString();
        }

        private float MeasureRowHeight(DiplomacyHistoryRow row, float width)
        {
            float contentWidth = Math.Max(120f, width - 10f - DeleteButtonWidth - 4f);
            float textHeight = CalcHeightWithFont(row?.Message ?? string.Empty, contentWidth, GameFont.Tiny);
            return Math.Max(36f, 18f + textHeight + 18f);
        }

        private void DrawRow(Rect rect, DiplomacyHistoryRow row, int index)
        {
            bool selected = IsRowSelected(row);
            Rect deleteRect = selected
                ? new Rect(rect.xMax - DeleteButtonWidth - 4f, rect.y + 4f, DeleteButtonWidth, 20f)
                : Rect.zero;
            HandleRowInput(rect, row, deleteRect, selected);

            Widgets.DrawBoxSolid(rect, selected
                ? HistoryRecordSelectedBg
                : (index % 2 == 0 ? HistoryRecordOddBg : HistoryRecordEvenBg));
            GUI.color = new Color(1f, 1f, 1f, selected ? 0.2f : 0.12f);
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;

            Rect innerRect = rect.ContractedBy(4f);
            float deleteWidth = selected ? DeleteButtonWidth : 0f;
            Rect contentRect = new Rect(innerRect.x, innerRect.y, innerRect.width - deleteWidth, innerRect.height);
            DrawRowContent(contentRect, row);

            if (selected)
            {
                DrawDeleteButton(deleteRect, row);
            }
        }

        private void DrawRowContent(Rect rect, DiplomacyHistoryRow row)
        {
            Rect speakerRect = new Rect(rect.x, rect.y, rect.width, 16f);
            GUI.color = HistorySpeakerText;
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.Font = GameFont.Tiny;
            Widgets.Label(speakerRect, BuildSpeakerLine(row));

            GUI.color = HistoryBodyText;
            Rect textRect = new Rect(rect.x, speakerRect.yMax + 1f, rect.width, Text.CalcHeight(row?.Message ?? string.Empty, rect.width));
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.Label(textRect, row?.Message ?? string.Empty);

            GUI.color = HistoryMetaText;
            Rect metaRect = new Rect(rect.x, textRect.yMax + 2f, rect.width, 14f);
            Widgets.Label(metaRect, "RimChat_DiplomacyHistoryEntryMeta".Translate(
                row?.IsPlayer == true
                    ? "RimChat_DiplomacyHistorySpeakerPlayer".Translate().ToString()
                    : "RimChat_DiplomacyHistorySpeakerAi".Translate().ToString(),
                row?.GameTick ?? 0).ToString());

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        private string BuildSpeakerLine(DiplomacyHistoryRow row)
        {
            string speakerName = row?.IsPlayer == true
                ? "RimChat_DiplomacyHistorySpeakerPlayer".Translate().ToString()
                : ResolveAiSpeakerLabel(row);
            return "RimChat_RPGHistorySpeakerSays".Translate(speakerName).ToString();
        }

        private string ResolveAiSpeakerLabel(DiplomacyHistoryRow row)
        {
            if (!string.IsNullOrWhiteSpace(row?.SenderLabel))
            {
                return row.SenderLabel.Trim();
            }

            return currentFaction?.leader?.Name?.ToStringShort
                   ?? currentFaction?.Name
                   ?? "RimChat_DiplomacyHistorySpeakerAi".Translate().ToString();
        }

        private void DrawDeleteButton(Rect rect, DiplomacyHistoryRow row)
        {
            Color previous = GUI.color;
            GUI.color = new Color(0.3f, 0.12f, 0.12f, 0.9f);
            if (Widgets.ButtonText(rect, "×"))
            {
                ConfirmDelete(row);
            }

            GUI.color = previous;
            TooltipHandler.TipRegion(rect, "RimChat_DiplomacyHistoryDeleteButton".Translate().ToString());
        }

        private void HandleRowInput(Rect rect, DiplomacyHistoryRow row, Rect deleteRect, bool selected)
        {
            Event current = Event.current;
            if (current == null || current.type != EventType.MouseDown || current.button != 0 || !rect.Contains(current.mousePosition))
            {
                return;
            }

            if (selected && deleteRect.Contains(current.mousePosition))
            {
                return;
            }

            if (current.clickCount >= 2)
            {
                SelectRow(row);
                OpenEditDialog(row);
                current.Use();
                return;
            }

            SelectRow(row);
            current.Use();
        }

        private void SelectRow(DiplomacyHistoryRow row)
        {
            selectedRowKey = BuildRowKey(row);
        }

        private bool IsRowSelected(DiplomacyHistoryRow row)
        {
            return string.Equals(selectedRowKey, BuildRowKey(row), StringComparison.Ordinal);
        }

        private string BuildRowKey(DiplomacyHistoryRow row)
        {
            if (row == null)
            {
                return string.Empty;
            }

            return $"{row.SessionOrdinal}|{row.SessionRowOrdinal}|{row.LiveMessageIndex}|{row.HistoryRecordIndex}|{row.GameTick}|{row.IsPlayer}";
        }

        private void RefreshGroups(bool forceResetSelection)
        {
            string signature = BuildDataSignature();
            int currentRevision = historyManager.GetFactionMemoryRevision(currentFaction);
            if (!forceResetSelection &&
                currentRevision == lastObservedRevision &&
                string.Equals(signature, lastDataSignature, StringComparison.Ordinal))
            {
                return;
            }

            string previousSelection = forceResetSelection ? string.Empty : selectedRowKey;
            groups = historyManager.GetDialogueHistorySessionGroups(currentFaction);
            lastDataSignature = signature;
            lastObservedRevision = currentRevision;
            if (!TryRestoreSelection(previousSelection))
            {
                selectedRowKey = string.Empty;
            }
        }

        private string BuildDataSignature()
        {
            FactionDialogueSession session = GameComponent_DiplomacyManager.Instance?.GetSession(currentFaction);
            int revision = historyManager.GetFactionMemoryRevision(currentFaction);
            return $"{revision}|{BuildLiveSessionSignature(session)}";
        }

        private static string BuildLiveSessionSignature(FactionDialogueSession session)
        {
            if (session?.messages == null || session.messages.Count == 0)
            {
                return "live:none";
            }

            var builder = new StringBuilder();
            builder.Append("live:");
            for (int i = 0; i < session.messages.Count; i++)
            {
                DialogueMessageData message = session.messages[i];
                if (message == null)
                {
                    continue;
                }

                builder.Append(i);
                builder.Append('|');
                builder.Append(message.GetGameTick());
                builder.Append('|');
                builder.Append(message.isPlayer);
                builder.Append('|');
                builder.Append(message.message ?? string.Empty);
                builder.Append(';');
            }

            return builder.ToString();
        }

        private bool TryRestoreSelection(string previousSelection)
        {
            if (string.IsNullOrWhiteSpace(previousSelection))
            {
                return false;
            }

            foreach (DiplomacyHistorySessionGroup group in groups)
            {
                foreach (DiplomacyHistoryRow row in group.Rows)
                {
                    if (string.Equals(BuildRowKey(row), previousSelection, StringComparison.Ordinal))
                    {
                        selectedRowKey = previousSelection;
                        return true;
                    }
                }
            }

            return false;
        }

        private void OpenEditDialog(DiplomacyHistoryRow row)
        {
            if (row == null)
            {
                return;
            }

            Find.WindowStack.Add(new Dialog_DiplomacyHistoryEdit(row.Message, edited =>
            {
                if (!historyManager.TryUpdateDialogueHistoryRow(currentFaction, row, edited, out string error))
                {
                    ShowMutationError("RimChat_DiplomacyHistoryUpdateFailed".Translate(TranslateHistoryError(error)));
                    return;
                }

                RefreshGroups(forceResetSelection: false);
            }));
        }

        private void ConfirmDelete(DiplomacyHistoryRow row)
        {
            if (row == null)
            {
                return;
            }

            Find.WindowStack.Add(new Dialog_MessageBox(
                "RimChat_DiplomacyHistoryDeleteConfirmBody".Translate(currentFaction?.Name ?? string.Empty),
                "RimChat_DiplomacyHistoryDeleteConfirmAccept".Translate(),
                () => DeleteRow(row),
                "RimChat_DiplomacyHistoryDeleteConfirmCancel".Translate(),
                null,
                "RimChat_DiplomacyHistoryDeleteConfirmTitle".Translate()));
        }

        private void DeleteRow(DiplomacyHistoryRow row)
        {
            if (!historyManager.TryDeleteDialogueHistoryRow(currentFaction, row, out string error))
            {
                ShowMutationError("RimChat_DiplomacyHistoryDeleteFailed".Translate(TranslateHistoryError(error)));
                return;
            }

            selectedRowKey = string.Empty;
            RefreshGroups(forceResetSelection: false);
        }

        private string TranslateHistoryError(string error)
        {
            return error switch
            {
                "history_faction_missing" => "RimChat_DiplomacyHistoryErrorFactionMissing".Translate().ToString(),
                "history_record_missing" => "RimChat_DiplomacyHistoryErrorRecordMissing".Translate().ToString(),
                "history_message_empty" => "RimChat_DiplomacyHistoryErrorMessageEmpty".Translate().ToString(),
                _ => "RimChat_DiplomacyHistoryErrorUnknown".Translate().ToString()
            };
        }

        private void ExecuteClearAllHistory()
        {
            // Delete last-to-first to avoid index shifting
            var allRows = new List<DiplomacyHistoryRow>();
            foreach (var group in groups)
            {
                if (group?.Rows != null) allRows.AddRange(group.Rows);
            }
            allRows.Reverse();

            int cleared = 0;
            foreach (var row in allRows)
            {
                if (historyManager.TryDeleteDialogueHistoryRow(currentFaction, row, out _))
                    cleared++;
            }

            if (cleared > 0)
            {
                string message = "RimChat_DiplomacyHistoryClearAllSuccess".Translate(currentFaction?.Name ?? "Unknown").ToString();
                Messages.Message(message, MessageTypeDefOf.NeutralEvent, false);
            }
            else if (allRows.Count > 0)
            {
                string message = "RimChat_DiplomacyHistoryClearAllFailed".Translate("could not delete records").ToString();
                Messages.Message(message, MessageTypeDefOf.RejectInput, false);
            }

            selectedRowKey = string.Empty;
            RefreshGroups(forceResetSelection: true);
        }

        private static void ShowMutationError(string message)
        {
            Messages.Message(message, MessageTypeDefOf.RejectInput, false);
        }

        private static float CalcHeightWithFont(string text, float width, GameFont font)
        {
            GameFont previous = Text.Font;
            Text.Font = font;
            float height = Text.CalcHeight(text ?? string.Empty, width);
            Text.Font = previous;
            return height;
        }
    }
}
