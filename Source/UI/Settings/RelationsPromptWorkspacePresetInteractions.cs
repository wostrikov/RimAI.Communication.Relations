using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.UI;
using Ustas.RimAI.Core.UI;

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class RelationsPromptWorkspacePresetInteractions
{
    readonly RelationsSettingsPages Pages;

    internal RelationsPromptWorkspacePresetInteractions(RelationsSettingsPages pages)
    {
        Pages = pages;
    }

    RelationsSettings Settings => Pages.Settings;

        internal const int PromptWorkspacePresetDoubleClickMs = 360;
        internal string _promptWorkspaceRenamingPresetId = string.Empty;
        internal string _promptWorkspacePresetRenameBuffer = string.Empty;
        internal bool _promptWorkspacePresetRenameFocusRequested;
        internal bool _promptWorkspacePresetRenameHadFocus;
        internal string _promptWorkspaceLastClickedPresetId = string.Empty;
        internal DateTime _promptWorkspaceLastPresetClickUtc = DateTime.MinValue;

        internal void DrawPromptWorkspacePresetActions(Rect rect)
        {
            float w = (rect.width - 6f) * 0.5f;

            Rect createRect = new Rect(rect.x, rect.y, w, rect.height);
            Rect dupRect = new Rect(rect.x + w + 6f, rect.y, w, rect.height);

            if (Widgets.ButtonText(createRect, "+"))
            {
                try
                {
                    PromptPresetConfig created = Pages.PromptWorkbench._promptPresetService.CreateFromLegacy(Settings, Pages.PromptWorkbenchPresets.NextPresetName("Preset"));
                    Pages.PromptWorkbench._promptPresetStore.Presets.Add(created);
                    Pages.PromptWorkbenchPresets.InvalidatePresetSummariesCache();
                    Pages.PromptWorkbench._selectedPromptPresetId = created.Id;
                    Pages.PromptWorkbench._presetRenameBuffer = created.Name;
                    CancelPromptWorkspaceInlineRename();
                    if (!Pages.PromptWorkbenchPresets.TryActivatePresetById(created.Id, showSuccessMessage: false))
                    {
                        Pages.PromptWorkbench._promptPresetService.SaveAll(Pages.PromptWorkbench._promptPresetStore);
                    }
                    Messages.Message("RimChat_PromptPreset_CreateSuccess".Translate(created.Name), MessageTypeDefOf.NeutralEvent, false);
                }
                catch (Exception ex)
                {
                    Log.Error($"[RimAI.Relations][PresetDiag] Workspace create failed: {ex}");
                    Messages.Message("RimChat_PromptPreset_ActivateFailed".Translate(ex.Message), MessageTypeDefOf.RejectInput, false);
                }
            }

            TooltipHandler.TipRegion(createRect, "RimChat_PromptPreset_CreateTip".Translate());

            PromptPresetConfig selected = Pages.PromptWorkbenchPresets.GetSelectedPreset();
            if (selected != null && Widgets.ButtonText(dupRect, "D"))
            {
                try
                {
                    PromptPresetConfig duplicated = Pages.PromptWorkbench._promptPresetService.Duplicate(Settings, selected, Pages.PromptWorkbenchPresets.NextPresetName(selected.Name));
                    Pages.PromptWorkbench._promptPresetStore.Presets.Add(duplicated);
                    Pages.PromptWorkbenchPresets.InvalidatePresetSummariesCache();
                    Pages.PromptWorkbench._selectedPromptPresetId = duplicated.Id;
                    Pages.PromptWorkbench._presetRenameBuffer = duplicated.Name;
                    CancelPromptWorkspaceInlineRename();
                    if (!Pages.PromptWorkbenchPresets.TryActivatePresetById(duplicated.Id, showSuccessMessage: false))
                    {
                        Pages.PromptWorkbench._promptPresetService.SaveAll(Pages.PromptWorkbench._promptPresetStore);
                    }
                    Messages.Message("RimChat_PromptPreset_DuplicateSuccess".Translate(duplicated.Name), MessageTypeDefOf.NeutralEvent, false);
                }
                catch (Exception ex)
                {
                    Log.Error($"[RimAI.Relations][PresetDiag] Workspace duplicate failed: {ex}");
                    Messages.Message("RimChat_PromptPreset_ActivateFailed".Translate(ex.Message), MessageTypeDefOf.RejectInput, false);
                }
            }

            TooltipHandler.TipRegion(dupRect, "RimChat_PromptPreset_DuplicateTip".Translate());
        }

        internal void DrawPromptWorkspacePresetList(Rect rect)
        {
            List<PromptPresetSummary> rows = Pages.PromptWorkbenchPresets.GetCachedPresetSummaries();
            const float rowStep = 26f;
            Rect view = new Rect(0f, 0f, rect.width - 16f, Mathf.Max(rect.height, rows.Count * rowStep));
            Widgets.BeginScrollView(rect, ref Pages.PromptWorkbench._promptPresetScroll, view);

            int firstVisible = Mathf.Max(0, Mathf.FloorToInt(Pages.PromptWorkbench._promptPresetScroll.y / rowStep) - 1);
            int lastVisible = Mathf.Min(rows.Count - 1, Mathf.CeilToInt((Pages.PromptWorkbench._promptPresetScroll.y + rect.height) / rowStep) + 1);
            for (int i = firstVisible; i <= lastVisible; i++)
            {
                DrawPromptWorkspacePresetRow(rows[i], new Rect(0f, i * rowStep, view.width, rowStep - 2f));
            }

            Widgets.EndScrollView();
        }

        internal void DrawPromptWorkspacePresetRow(PromptPresetSummary row, Rect rowRect)
        {
            bool selected = string.Equals(row.Id, Pages.PromptWorkbench._selectedPromptPresetId, StringComparison.Ordinal);
            if (selected)
            {
                Widgets.DrawBoxSolid(rowRect, new Color(0.27f, 0.38f, 0.56f));
            }
            else if (Mouse.IsOver(rowRect))
            {
                Widgets.DrawBoxSolid(rowRect, new Color(0.18f, 0.18f, 0.20f));
            }

            float iconW = 20f;
            float iconGap = 4f;
            Rect deleteRect = new Rect(rowRect.xMax - iconW, rowRect.y + 2f, iconW, rowRect.height - 4f);
            Rect duplicateRect = new Rect(deleteRect.x - iconGap - iconW, rowRect.y + 2f, iconW, rowRect.height - 4f);
            Rect labelRect = new Rect(rowRect.x + 20f, rowRect.y + 2f, duplicateRect.x - rowRect.x - 24f, rowRect.height - 4f);
            Rect clickRect = new Rect(rowRect.x, rowRect.y, labelRect.xMax - rowRect.x, rowRect.height);

            if (row.IsActive)
            {
                GUI.color = Color.green;
                Widgets.Label(new Rect(rowRect.x + 4f, rowRect.y, 14f, rowRect.height), "▶");
                GUI.color = Color.white;
            }

            DrawPromptWorkspacePresetRowLabel(row, labelRect);
            HandlePromptWorkspacePresetRowClicks(row, clickRect);
            DrawPromptWorkspacePresetRowActions(row, duplicateRect, deleteRect);
        }

        internal void DrawPromptWorkspacePresetRowLabel(PromptPresetSummary row, Rect rect)
        {
            if (string.Equals(_promptWorkspaceRenamingPresetId, row.Id, StringComparison.Ordinal))
            {
                DrawPromptWorkspaceInlineRenameField(row, rect);
                return;
            }

            bool oldWrap = Text.WordWrap;
            Text.WordWrap = false;
            string title = row.Name ?? string.Empty;
            if (row.IsDefault)
            {
                title = $"{title}  {("RimChat_PromptPreset_DefaultReadonlyTag".Translate())}";
            }

            Widgets.Label(rect, title.Truncate(rect.width));
            Text.WordWrap = oldWrap;
        }

        internal void HandlePromptWorkspacePresetRowClicks(PromptPresetSummary row, Rect clickRect)
        {
            if (!Widgets.ButtonInvisible(clickRect))
            {
                return;
            }

            bool shouldRename = IsPromptWorkspacePresetDoubleClick(row.Id);
            Pages.PromptWorkspace.SchedulePromptWorkspaceNavigation(() =>
            {
                if (!Pages.PromptWorkspaceBuffers.PersistPromptWorkspaceBufferNow(force: true))
                {
                    return;
                }

                bool changedSelection = !string.Equals(Pages.PromptWorkbench._selectedPromptPresetId, row.Id, StringComparison.Ordinal);
                Pages.PromptWorkbench._selectedPromptPresetId = row.Id;
                Pages.PromptWorkbench._presetRenameBuffer = row.Name;
                if (changedSelection && !row.IsActive)
                {
                    Pages.PromptWorkbenchPresets.TryActivatePresetById(row.Id, showSuccessMessage: false);
                }

                if (!shouldRename)
                {
                    return;
                }

                BeginPromptWorkspaceInlineRename(row);
            });
        }

        internal void DrawPromptWorkspacePresetRowActions(PromptPresetSummary row, Rect duplicateRect, Rect deleteRect)
        {
            if (Widgets.ButtonText(duplicateRect, "D"))
            {
                DuplicatePromptWorkspacePreset(row.Id);
            }

            TooltipHandler.TipRegion(duplicateRect, "RimChat_PromptPreset_RowDuplicateTip".Translate());

            bool canDelete = !row.IsDefault && Pages.PromptWorkbench._promptPresetStore?.Presets?.Count > 1;
            bool oldEnabled = GUI.enabled;
            if (!canDelete)
            {
                GUI.enabled = false;
            }

            if (Widgets.ButtonText(deleteRect, "X"))
            {
                DeletePromptWorkspacePreset(row.Id);
            }

            GUI.enabled = oldEnabled;
            TooltipHandler.TipRegion(
                deleteRect,
                canDelete
                    ? "RimChat_PromptPreset_RowDeleteTip".Translate()
                    : "RimChat_PromptPreset_RowDeleteDefaultBlocked".Translate());
        }

        internal void DuplicatePromptWorkspacePreset(string sourcePresetId)
        {
            PromptPresetConfig source = Pages.PromptWorkbench._promptPresetStore?.Presets?.FirstOrDefault(p => string.Equals(p.Id, sourcePresetId, StringComparison.Ordinal));
            if (source == null)
            {
                return;
            }

            PromptPresetConfig duplicated = Pages.PromptWorkbench._promptPresetService.Duplicate(Settings, source, Pages.PromptWorkbenchPresets.NextPresetName(source.Name));
            Pages.PromptWorkbench._promptPresetStore.Presets.Add(duplicated);
            Pages.PromptWorkbenchPresets.InvalidatePresetSummariesCache();
            Pages.PromptWorkbench._selectedPromptPresetId = duplicated.Id;
            Pages.PromptWorkbench._presetRenameBuffer = duplicated.Name;
            CancelPromptWorkspaceInlineRename();
            if (!Pages.PromptWorkbenchPresets.TryActivatePresetById(duplicated.Id, showSuccessMessage: false))
            {
                Pages.PromptWorkbench._promptPresetService.SaveAll(Pages.PromptWorkbench._promptPresetStore);
            }
            Messages.Message("RimChat_PromptPreset_DuplicateSuccess".Translate(duplicated.Name), MessageTypeDefOf.NeutralEvent, false);
        }

        internal void DeletePromptWorkspacePreset(string presetId)
        {
            PromptPresetConfig selected = Pages.PromptWorkbench._promptPresetStore?.Presets?.FirstOrDefault(p => string.Equals(p.Id, presetId, StringComparison.Ordinal));
            if (selected == null || Pages.PromptWorkbench._promptPresetService.IsDefaultPreset(Pages.PromptWorkbench._promptPresetStore, selected.Id))
            {
                return;
            }

            string deletedName = selected.Name ?? string.Empty;
            bool deletedActive = selected.IsActive;
            Pages.PromptWorkbench._promptPresetStore.Presets.RemoveAll(p => string.Equals(p.Id, selected.Id, StringComparison.Ordinal));
            Pages.PromptWorkbenchPresets.InvalidatePresetSummariesCache();
            Pages.PromptWorkbench._selectedPromptPresetId = Pages.PromptWorkbench._promptPresetStore.Presets.FirstOrDefault()?.Id ?? string.Empty;
            if (deletedActive && !string.IsNullOrWhiteSpace(Pages.PromptWorkbench._selectedPromptPresetId))
            {
                Pages.PromptWorkbenchPresets.TryActivatePresetById(Pages.PromptWorkbench._selectedPromptPresetId, showSuccessMessage: false);
            }
            else
            {
                Pages.PromptWorkbench._promptPresetStore.ActivePresetId = Pages.PromptWorkbench._selectedPromptPresetId;
                Pages.PromptWorkbench._promptPresetService.SaveAll(Pages.PromptWorkbench._promptPresetStore);
            }

            if (string.Equals(_promptWorkspaceRenamingPresetId, presetId, StringComparison.Ordinal))
            {
                CancelPromptWorkspaceInlineRename();
            }

            Messages.Message("RimChat_PromptPreset_DeleteSuccess".Translate(deletedName), MessageTypeDefOf.NeutralEvent, false);
        }

        internal void BeginPromptWorkspaceInlineRename(PromptPresetSummary row)
        {
            if (row == null)
            {
                return;
            }

            Pages.PromptWorkbench._selectedPromptPresetId = row.Id;
            if (!row.IsActive)
            {
                Pages.PromptWorkbenchPresets.TryActivatePresetById(row.Id, showSuccessMessage: false);
            }

            if (row.IsDefault && !EnsurePromptWorkspaceEditablePresetForMutation("preset.rename"))
            {
                return;
            }

            PromptPresetConfig target = Pages.PromptWorkbenchPresets.GetSelectedPreset();
            if (target == null)
            {
                return;
            }

            _promptWorkspaceRenamingPresetId = target.Id;
            _promptWorkspacePresetRenameBuffer = target.Name ?? string.Empty;
            _promptWorkspacePresetRenameFocusRequested = true;
            _promptWorkspacePresetRenameHadFocus = false;
        }

        internal void DrawPromptWorkspaceInlineRenameField(PromptPresetSummary row, Rect rect)
        {
            const string controlName = "RimChat_PromptWorkspacePresetInlineRename";
            GUI.SetNextControlName(controlName);
            _promptWorkspacePresetRenameBuffer = Widgets.TextField(rect, _promptWorkspacePresetRenameBuffer ?? string.Empty);
            if (_promptWorkspacePresetRenameFocusRequested)
            {
                GUI.FocusControl(controlName);
                _promptWorkspacePresetRenameFocusRequested = false;
            }

            bool focused = string.Equals(GUI.GetNameOfFocusedControl(), controlName, StringComparison.Ordinal);
            Event evt = Event.current;
            if (focused)
            {
                _promptWorkspacePresetRenameHadFocus = true;
                if (evt != null && evt.type == EventType.KeyDown)
                {
                    if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                    {
                        CommitPromptWorkspaceInlineRename();
                        evt.Use();
                        return;
                    }

                    if (evt.keyCode == KeyCode.Escape)
                    {
                        CancelPromptWorkspaceInlineRename();
                        evt.Use();
                        return;
                    }
                }
            }
            else if (_promptWorkspacePresetRenameHadFocus)
            {
                CommitPromptWorkspaceInlineRename();
            }

            TooltipHandler.TipRegion(rect, "RimChat_PromptPreset_InlineRenameHint".Translate());
        }

        internal void CommitPromptWorkspaceInlineRename()
        {
            PromptPresetConfig target = Pages.PromptWorkbench._promptPresetStore?.Presets?.FirstOrDefault(p =>
                string.Equals(p.Id, _promptWorkspaceRenamingPresetId, StringComparison.Ordinal));
            if (target == null)
            {
                CancelPromptWorkspaceInlineRename();
                return;
            }

            string beforeName = target.Name ?? string.Empty;
            string next = (_promptWorkspacePresetRenameBuffer ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(next))
            {
                target.Name = next;
                target.UpdatedAtUtc = DateTime.UtcNow.ToString("o");
                Pages.PromptWorkbench._presetRenameBuffer = target.Name;
                Pages.PromptWorkbenchPresets.InvalidatePresetSummariesCache();
                Pages.PromptWorkbench._promptPresetService.SaveAll(Pages.PromptWorkbench._promptPresetStore);
                if (!string.Equals(beforeName, target.Name, StringComparison.Ordinal))
                {
                    Messages.Message("RimChat_PromptPreset_RenameSuccess".Translate(target.Name), MessageTypeDefOf.NeutralEvent, false);
                }
            }

            CancelPromptWorkspaceInlineRename();
        }

        internal void CancelPromptWorkspaceInlineRename()
        {
            _promptWorkspaceRenamingPresetId = string.Empty;
            _promptWorkspacePresetRenameBuffer = string.Empty;
            _promptWorkspacePresetRenameFocusRequested = false;
            _promptWorkspacePresetRenameHadFocus = false;
        }

        internal bool IsPromptWorkspacePresetDoubleClick(string presetId)
        {
            DateTime nowUtc = DateTime.UtcNow;
            bool doubled = string.Equals(_promptWorkspaceLastClickedPresetId, presetId, StringComparison.Ordinal) &&
                           _promptWorkspaceLastPresetClickUtc != DateTime.MinValue &&
                           (nowUtc - _promptWorkspaceLastPresetClickUtc).TotalMilliseconds <= PromptWorkspacePresetDoubleClickMs;
            _promptWorkspaceLastClickedPresetId = presetId ?? string.Empty;
            _promptWorkspaceLastPresetClickUtc = nowUtc;
            return doubled;
        }

        internal bool EnsurePromptWorkspaceEditablePresetForMutation(string mutationReason)
        {
            if (Pages.PromptWorkbench._promptPresetService == null)
            {
                Pages.PromptWorkbenchPresets.EnsurePresetStoreReady();
            }

            if (Pages.PromptWorkbench._promptPresetService == null || Pages.PromptWorkbench._promptPresetStore == null)
            {
                Messages.Message("RimChat_PromptPreset_AutoForkFailed".Translate(mutationReason ?? string.Empty), MessageTypeDefOf.RejectInput, false);
                return false;
            }

            if (!Pages.PromptWorkbench._promptPresetService.EnsureEditablePresetForMutation(
                    Settings,
                    Pages.PromptWorkbench._promptPresetStore,
                    Pages.PromptWorkbench._selectedPromptPresetId,
                    "Custom",
                    out PromptPresetConfig editablePreset,
                    out bool forked,
                    out string error))
            {
                Messages.Message("RimChat_PromptPreset_AutoForkFailed".Translate(error ?? string.Empty), MessageTypeDefOf.RejectInput, false);
                return false;
            }

            if (editablePreset != null)
            {
                Pages.PromptWorkbench._selectedPromptPresetId = editablePreset.Id;
                Pages.PromptWorkbench._presetRenameBuffer = editablePreset.Name;
            }

            if (!forked)
            {
                return true;
            }

            Pages.PromptWorkbench.InvalidateWorkbenchEditingChannelConfig();
            Pages.RimTalkTab.ResetRimTalkEntryContentBuffer();
            Pages.PromptWorkspace.InvalidatePromptWorkspaceNodeUiCaches();
            Pages.PromptWorkspace.InvalidatePromptWorkspacePreviewCache();
            Pages.PromptWorkspaceBuffers.EnsurePromptWorkspaceBuffer();
            Messages.Message("RimChat_PromptPreset_AutoForked".Translate(editablePreset?.Name ?? string.Empty), MessageTypeDefOf.NeutralEvent, false);
            return true;
        }
    
}
