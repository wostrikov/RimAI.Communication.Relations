using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using RimWorld;
using UnityEngine;
using UnityEngine.Networking;
using Verse;
using Verse.Sound;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Context;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.Prompting.Transfer;
using Ustas.RimAI.Core.Player2;
using Ustas.RimAI.Core.UI;

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class RelationsPromptWorkbenchPresets
{
    internal readonly RelationsPromptWorkbenchFramework Owner;

    internal RelationsPromptWorkbenchPresets(RelationsPromptWorkbenchFramework owner)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    internal RelationsSettingsPages Pages => Owner.Pages;
    internal RelationsSettings Settings => Owner.Settings;

        /// <summary>
        /// Gets cached preset summaries. Rebuilds only when preset count or active preset changes.
        /// Eliminates per-frame JSON serialization in BuildSummaries.
        /// </summary>
        internal List<PromptPresetSummary> GetCachedPresetSummaries()
        {
            int currentCount = Owner._promptPresetStore?.Presets?.Count ?? 0;
            string currentActiveId = Owner._promptPresetStore?.ActivePresetId;

            if (Owner._cachedPresetSummaries == null ||
                Owner._cachedPresetSummariesCount != currentCount ||
                Owner._cachedPresetSummariesActiveId != currentActiveId)
            {
                Owner._cachedPresetSummaries = Owner._promptPresetService?.BuildSummaries(Owner._promptPresetStore)
                    ?? new List<PromptPresetSummary>();
                Owner._cachedPresetSummariesCount = currentCount;
                Owner._cachedPresetSummariesActiveId = currentActiveId;
            }

            return Owner._cachedPresetSummaries;
        }

        /// <summary>
        /// Invalidates the preset summaries cache. Call after any preset mutation.
        /// </summary>
        internal void InvalidatePresetSummariesCache()
        {
            Owner._cachedPresetSummaries = null;
            Owner._cachedPresetSummariesCount = -1;
            Owner._cachedPresetSummariesActiveId = null;
        }

        internal void DrawPresetActions(Rect rect)
        {
            float w = (rect.width - 6f) / 2f;
            if (Widgets.ButtonText(new Rect(rect.x, rect.y, w, rect.height), "RimChat_PromptPreset_Create".Translate()))
            {
                try
                {
                    PromptPresetConfig created = Owner._promptPresetService.CreateFromLegacy(Settings, NextPresetName("Preset"));
                    Owner._promptPresetStore.Presets.Add(created);
                    Owner._selectedPromptPresetId = created.Id;
                    Owner._presetRenameBuffer = created.Name;
                    Log.Message($"[RimAI.Relations][PresetDiag] Legacy workbench create clicked. add_id={created.Id}, count={Owner._promptPresetStore.Presets.Count}");
                    if (!TryActivatePresetById(created.Id, showSuccessMessage: false))
                    {
                        Owner._promptPresetService.SaveAll(Owner._promptPresetStore);
                    }
                    Messages.Message("RimChat_PromptPreset_CreateSuccess".Translate(created.Name), MessageTypeDefOf.NeutralEvent, false);
                }
                catch (Exception ex)
                {
                    Log.Error($"[RimAI.Relations][PresetDiag] Legacy workbench create failed: {ex}");
                    Messages.Message("RimChat_PromptPreset_ActivateFailed".Translate(ex.Message), MessageTypeDefOf.RejectInput, false);
                }
            }

            PromptPresetConfig selected = GetSelectedPreset();
            if (selected != null && Widgets.ButtonText(new Rect(rect.x + w + 6f, rect.y, w, rect.height), "RimChat_PromptPreset_Duplicate".Translate()))
            {
                try
                {
                    PromptPresetConfig duplicated = Owner._promptPresetService.Duplicate(Settings, selected, NextPresetName(selected.Name));
                    Owner._promptPresetStore.Presets.Add(duplicated);
                    Owner._selectedPromptPresetId = duplicated.Id;
                    Owner._presetRenameBuffer = duplicated.Name;
                    Log.Message($"[RimAI.Relations][PresetDiag] Legacy workbench duplicate clicked. add_id={duplicated.Id}, count={Owner._promptPresetStore.Presets.Count}");
                    if (!TryActivatePresetById(duplicated.Id, showSuccessMessage: false))
                    {
                        Owner._promptPresetService.SaveAll(Owner._promptPresetStore);
                    }
                    Messages.Message("RimChat_PromptPreset_DuplicateSuccess".Translate(duplicated.Name), MessageTypeDefOf.NeutralEvent, false);
                }
                catch (Exception ex)
                {
                    Log.Error($"[RimAI.Relations][PresetDiag] Legacy workbench duplicate failed: {ex}");
                    Messages.Message("RimChat_PromptPreset_ActivateFailed".Translate(ex.Message), MessageTypeDefOf.RejectInput, false);
                }
            }
        }

        internal void DrawPresetList(Rect rect)
        {
            List<PromptPresetSummary> rows = Owner._promptPresetService.BuildSummaries(Owner._promptPresetStore);
            const float rowStep = 25f;
            float contentHeight = Mathf.Max(rect.height, rows.Count * rowStep);
            Rect view = new Rect(0f, 0f, rect.width - 16f, contentHeight);
            Widgets.BeginScrollView(rect, ref Owner._promptPresetScroll, view);
            for (int i = 0; i < rows.Count; i++)
            {
                PromptPresetSummary row = rows[i];
                Rect r = new Rect(0f, i * rowStep, view.width, 24f);
                bool selected = string.Equals(row.Id, Owner._selectedPromptPresetId, StringComparison.Ordinal);
                if (selected)
                {
                    Widgets.DrawBoxSolid(r, new Color(0.27f, 0.38f, 0.56f));
                }
                else if (Mouse.IsOver(r))
                {
                    Widgets.DrawBoxSolid(r, new Color(0.18f, 0.18f, 0.20f));
                }

                if (row.IsActive)
                {
                    GUI.color = Color.green;
                    Widgets.Label(new Rect(r.x + 4f, r.y, 14f, 24f), "▶");
                    GUI.color = Color.white;
                }

                bool oldWrap = Text.WordWrap;
                Text.WordWrap = false;
                string title = row.Name ?? string.Empty;
                Widgets.Label(new Rect(r.x + 20f, r.y + 2f, r.width - 24f, 20f), title.Truncate(r.width - 24f));
                Text.WordWrap = oldWrap;
                if (Widgets.ButtonInvisible(r))
                {
                    bool changedSelection = !string.Equals(Owner._selectedPromptPresetId, row.Id, StringComparison.Ordinal);
                    Owner._selectedPromptPresetId = row.Id;
                    Owner._presetRenameBuffer = row.Name;
                    if (changedSelection && !row.IsActive)
                    {
                        TryActivatePresetById(row.Id, showSuccessMessage: false);
                    }
                }
            }

            Widgets.EndScrollView();
        }

        internal void DrawPresetBottomActions(Rect rect)
        {
            PromptPresetConfig selected = GetSelectedPreset();
            Owner._presetRenameBuffer = Widgets.TextField(new Rect(rect.x, rect.y, rect.width, 24f), Owner._presetRenameBuffer ?? string.Empty);
            float w = (rect.width - 6f) / 2f;
            float topY = rect.y + 28f;
            float bottomY = topY + 28f;
            if (selected != null && Widgets.ButtonText(new Rect(rect.x, topY, w, 24f), "RimChat_PromptPreset_Activate".Translate()))
            {
                TryActivatePresetById(selected.Id, showSuccessMessage: true);
            }

            if (selected != null && Widgets.ButtonText(new Rect(rect.x + w + 6f, topY, w, 24f), "RimChat_PromptPreset_Duplicate".Translate()))
            {
                PromptPresetConfig duplicated = Owner._promptPresetService.Duplicate(Settings, selected, NextPresetName(selected.Name));
                Owner._promptPresetStore.Presets.Add(duplicated);
                Owner._selectedPromptPresetId = duplicated.Id;
                Owner._presetRenameBuffer = duplicated.Name;
                Owner._promptPresetService.SaveAll(Owner._promptPresetStore);
                Messages.Message("RimChat_PromptPreset_DuplicateSuccess".Translate(duplicated.Name), MessageTypeDefOf.NeutralEvent, false);
            }

            if (selected != null && Widgets.ButtonText(new Rect(rect.x, bottomY, w, 24f), "RimChat_PromptPreset_Rename".Translate()))
            {
                string beforeName = selected.Name ?? string.Empty;
                selected.Name = string.IsNullOrWhiteSpace(Owner._presetRenameBuffer) ? selected.Name : Owner._presetRenameBuffer.Trim();
                selected.UpdatedAtUtc = DateTime.UtcNow.ToString("o");
                Owner._promptPresetService.SaveAll(Owner._promptPresetStore);
                if (!string.Equals(beforeName, selected.Name, StringComparison.Ordinal))
                {
                    Messages.Message("RimChat_PromptPreset_RenameSuccess".Translate(selected.Name), MessageTypeDefOf.NeutralEvent, false);
                }
            }

            if (selected != null && Owner._promptPresetStore.Presets.Count > 1 && Widgets.ButtonText(new Rect(rect.x + w + 6f, bottomY, w, 24f), "RimChat_PromptPreset_Delete".Translate()))
            {
                string deletedName = selected.Name ?? string.Empty;
                bool deletedActive = selected.IsActive;
                Owner._promptPresetStore.Presets.RemoveAll(p => string.Equals(p.Id, selected.Id, StringComparison.Ordinal));
                Owner._selectedPromptPresetId = Owner._promptPresetStore.Presets.FirstOrDefault()?.Id ?? string.Empty;
                if (deletedActive && !string.IsNullOrWhiteSpace(Owner._selectedPromptPresetId))
                {
                    TryActivatePresetById(Owner._selectedPromptPresetId, showSuccessMessage: false);
                }
                else
                {
                    Owner._promptPresetStore.ActivePresetId = Owner._selectedPromptPresetId;
                    Owner._promptPresetService.SaveAll(Owner._promptPresetStore);
                }

                Messages.Message("RimChat_PromptPreset_DeleteSuccess".Translate(deletedName), MessageTypeDefOf.NeutralEvent, false);
            }
        }

        internal void EnsurePresetStoreReady()
        {
            Owner._promptPresetService ??= new PromptPresetService();
            if (Owner._promptPresetStore != null)
            {
                return;
            }

            Owner._promptPresetStore = Owner._promptPresetService.LoadAll(Settings);
            PromptPresetConfig active = Owner._promptPresetStore.Presets.FirstOrDefault(p => p.IsActive)
                                      ?? Owner._promptPresetStore.Presets.FirstOrDefault();
            if (active?.ChannelPayloads != null)
            {
                Owner._promptPresetService.ApplyPayloadToSettings(Settings, active.ChannelPayloads, persistToFiles: false);
            }

            Owner._selectedPromptPresetId = active?.Id ?? string.Empty;
            Owner._presetRenameBuffer = active?.Name ?? string.Empty;
        }

        internal PromptPresetConfig GetSelectedPreset()
        {
            return Owner._promptPresetStore?.Presets?.FirstOrDefault(p => string.Equals(p.Id, Owner._selectedPromptPresetId, StringComparison.Ordinal));
        }

        internal string NextPresetName(string baseName)
        {
            string stem = string.IsNullOrWhiteSpace(baseName) ? "Preset" : baseName.Trim();
            int n = 1;
            string candidate = stem;
            while (Owner._promptPresetStore.Presets.Any(p => string.Equals(p.Name, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                n++;
                candidate = $"{stem} {n}";
            }

            return candidate;
        }

        internal bool TryActivatePresetById(string presetId, bool showSuccessMessage)
        {
            if (string.IsNullOrWhiteSpace(presetId))
            {
                return false;
            }

            if (Owner._promptPresetService.Activate(Settings, Owner._promptPresetStore, presetId, out string error))
            {
                Owner._promptPresetStore.ActivePresetId = presetId;
                Owner._promptPresetService.SaveAll(Owner._promptPresetStore);
                if (showSuccessMessage)
                {
                    PromptPresetConfig activated = Owner._promptPresetStore.Presets.FirstOrDefault(p => string.Equals(p.Id, presetId, StringComparison.Ordinal));
                    Messages.Message("RimChat_PromptPreset_ActivateSuccess".Translate(activated?.Name ?? string.Empty), MessageTypeDefOf.NeutralEvent, false);
                }

                Owner.InvalidateWorkbenchEditingChannelConfig();
                Pages.RimTalkTab.ResetRimTalkEntryContentBuffer();
                return true;
            }

            Log.Warning($"[RimAI.Relations] Prompt preset activation failed. id={presetId}, error={error}");
            Messages.Message("RimChat_PromptPreset_ActivateFailed".Translate(error ?? string.Empty), MessageTypeDefOf.RejectInput, false);
            return false;
        }

        internal void ShowImportPresetDialog()
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            Find.WindowStack.Add(new Dialog_LoadFile(System.IO.Path.Combine(desktop, "RimChatPromptPreset.json"), path =>
            {
                if (Owner._promptPresetService.ImportPreset(path, Owner._promptPresetStore, out PromptPresetConfig imported, out string error))
                {
                    Owner._promptPresetStore.Presets.Add(imported);
                    Owner._selectedPromptPresetId = imported.Id;
                    Owner._presetRenameBuffer = imported.Name;
                    if (!TryActivatePresetById(imported.Id, showSuccessMessage: false))
                    {
                        Owner._promptPresetStore.ActivePresetId = imported.Id;
                        Owner._promptPresetService.SaveAll(Owner._promptPresetStore);
                        Owner.InvalidateWorkbenchEditingChannelConfig();
                        Pages.RimTalkTab.ResetRimTalkEntryContentBuffer();
                    }
                    Messages.Message("RimChat_PromptPreset_ImportSuccess".Translate(imported.Name), MessageTypeDefOf.NeutralEvent, false);
                }
                else
                {
                    Messages.Message("RimChat_PromptPreset_ImportFailed".Translate(error), MessageTypeDefOf.RejectInput, false);
                }
            }));
        }

        internal void ShowExportPresetDialog()
        {
            PromptPresetConfig selected = GetSelectedPreset();
            if (selected == null)
            {
                Messages.Message("RimChat_PromptPreset_NoSelection".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            // Ensure the currently edited preset exports the latest workbench buffer, not stale store snapshot.
            if (string.Equals(selected.Id, Owner._selectedPromptPresetId, StringComparison.Ordinal))
            {
                try
                {
                    Owner.FlushPromptEditorsToStorageForPreset(persistToFiles: false);
                    if (Owner._promptPresetService != null && Owner._promptPresetStore != null)
                    {
                        string syncError = string.Empty;
                        bool syncOk = Owner._promptPresetService.SyncPresetPayloadFromSettings(
                            Settings,
                            Owner._promptPresetStore,
                            Owner._selectedPromptPresetId,
                            out syncError);
                        if (syncOk)
                        {
                            Owner._promptPresetService.SaveAll(Owner._promptPresetStore);
                            selected = GetSelectedPreset() ?? selected;
                        }
                        else
                        {
                            Messages.Message(
                                "RimChat_PromptPreset_ExportFailed".Translate(syncError ?? "workspace.sync_payload"),
                                MessageTypeDefOf.RejectInput,
                                false);
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Messages.Message("RimChat_PromptPreset_ExportFailed".Translate(ex.Message), MessageTypeDefOf.RejectInput, false);
                    return;
                }
            }

            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            Find.WindowStack.Add(new Dialog_SaveFile(System.IO.Path.Combine(desktop, $"RimChatPromptPreset_{selected.Name}.json"), path =>
            {
                if (!Owner._promptPresetService.ExportPreset(path, selected, out string error))
                {
                    Messages.Message("RimChat_PromptPreset_ExportFailed".Translate(error), MessageTypeDefOf.RejectInput, false);
                    return;
                }

                Messages.Message("RimChat_PromptPreset_ExportSuccess".Translate(path), MessageTypeDefOf.NeutralEvent, false);
            }));
        }
}
