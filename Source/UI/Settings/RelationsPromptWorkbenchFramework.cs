using System;
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.UI;
using RimWorld;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Core.UI;

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class RelationsPromptWorkbenchFramework
{
    internal readonly RelationsSettingsPages Pages;

    internal RelationsPromptWorkbenchFramework(RelationsSettingsPages pages)
    {
        Pages = pages;
    }

    internal RelationsSettings Settings => Pages.Settings;

        internal IPromptPresetService _promptPresetService;
        internal PromptPresetStoreConfig _promptPresetStore;
        internal string _selectedPromptPresetId = string.Empty;
        internal string _presetRenameBuffer = string.Empty;
        internal Vector2 _promptPresetScroll = Vector2.zero;

        // Preset summaries cache to eliminate per-frame JSON serialization
        internal List<PromptPresetSummary> _cachedPresetSummaries;
        internal int _cachedPresetSummariesCount = -1;
        internal string _cachedPresetSummariesActiveId;
        internal PromptWorkbenchChannel _workbenchChannel = PromptWorkbenchChannel.Diplomacy;
        internal PromptWorkbenchInfoPanel _workbenchSidePanelTab = PromptWorkbenchInfoPanel.Preview;
        internal string _workbenchVariableInsertToken = string.Empty;
        internal string _workbenchHintSearch = string.Empty;
        internal int _workbenchRpgSubTab;
        internal RimTalkPromptChannel? _workbenchSeededEntryChannel;
        internal RimTalkChannelCompatConfig _workbenchEditingConfig;
        internal RimTalkPromptChannel _workbenchEditingConfigChannel = RimTalkPromptChannel.Diplomacy;
        internal bool _workbenchEditingConfigReady;
        internal string _workbenchPromptChannel = string.Empty;



        internal void FlushPromptEditorsToStorageForPreset(bool persistToFiles = false)
        {
            Pages.PromptLegacyPreview.SyncBuffersToData();
            Pages.PromptWorkspaceBuffers.FlushPromptWorkspaceEdits(persistToDisk: persistToFiles);
            if (!persistToFiles)
            {
                return;
            }

            Pages.PromptLegacyIo.SaveSystemPromptConfig();
        }

        internal void RefreshPromptEditorStateFromStorage()
        {
            Pages.PromptLegacy._systemPromptConfig = PromptPersistenceService.Instance.LoadConfigReadOnly();
            Settings.ReloadPromptUnifiedCatalogFromStorage();
            Settings.EnsureRpgPromptTextsLoaded();
            Pages.PromptLegacyPreview.SyncBuffersToData();
            _workbenchSeededEntryChannel = null;
            InvalidateWorkbenchEditingChannelConfig();
            Pages.RimTalkTab.ResetRimTalkEntryContentBuffer();
            Pages.PromptWorkspace.InvalidatePromptWorkspaceNodeUiCaches();
            Pages.PromptWorkspace.InvalidatePromptWorkspacePreviewCache();
            Pages.PromptLegacy._previewUpdateCooldown = 0;
            Pages.RpgEditors._rpgPreviewUpdateCooldown = 0;
        }

        internal void DrawAdvancedPromptWorkbench(Listing_Standard listing)
        {
            DrawAdvancedPromptWorkbench(listing.GetRect(620f));
        }

        internal void DrawAdvancedPromptWorkbench(Rect root)
        {
            Pages.PromptWorkbenchPresets.EnsurePresetStoreReady();
            Pages.PromptLegacy.InitBuffers();
            EnsureWorkbenchPromptChannelSelection();
            ApplyWorkbenchEntryChannelSelection(_workbenchChannel);

            Widgets.DrawBoxSolid(root, new Color(0.08f, 0.09f, 0.11f));
            Rect frame = root.ContractedBy(8f);
            Rect headerRect = new Rect(frame.x, frame.y, frame.width, 78f);
            Rect bodyRect = new Rect(frame.x, headerRect.yMax + 6f, frame.width, frame.height - headerRect.height - 6f);

            DrawWorkbenchHeader(headerRect);
            DrawWorkbenchBody(bodyRect);
        }

        internal void DrawWorkbenchHeader(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.07f, 0.08f, 0.10f));
            Rect inner = rect.ContractedBy(8f);

            TextAnchor oldAnchor = Text.Anchor;
            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = new Color(0.95f, 0.74f, 0.26f);
            Widgets.Label(new Rect(inner.x, inner.y, inner.width * 0.45f, 28f), "RimChat_Tab_PromptWorkbench".Translate());
            GUI.color = Color.white;
            Text.Font = oldFont;
            Text.Anchor = oldAnchor;

            float tabsY = inner.y + 34f;
            Rect channelHeaderRect = new Rect(inner.x, tabsY + 2f, 92f, 26f);
            Widgets.Label(channelHeaderRect, "RimChat_PromptWorkbench_ChannelHeader".Translate());
            Rect channelDropdownRect = new Rect(channelHeaderRect.xMax + 4f, tabsY, 260f, 30f);
            DrawWorkbenchChannelDropdown(channelDropdownRect);

            Rect importRect = new Rect(inner.xMax - 180f, tabsY, 84f, 30f);
            Rect exportRect = new Rect(inner.xMax - 90f, tabsY, 84f, 30f);
            if (Widgets.ButtonText(importRect, "RimChat_Import".Translate()))
            {
                Pages.PromptWorkbenchPresets.ShowImportPresetDialog();
            }

            if (Widgets.ButtonText(exportRect, "RimChat_Export".Translate()))
            {
                Pages.PromptWorkbenchPresets.ShowExportPresetDialog();
            }
        }

        internal void DrawWorkbenchChannelDropdown(Rect rect)
        {
            string selectedChannel = EnsureWorkbenchPromptChannelSelection();
            string label = RimTalkPromptEntryChannelCatalog.GetLabel(selectedChannel);
            Widgets.DrawBoxSolid(rect, new Color(0.25f, 0.18f, 0.08f));
            Widgets.DrawBox(rect, 1);
            TextAnchor oldAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = new Color(1f, 0.88f, 0.55f);
            Widgets.Label(new Rect(rect.x + 8f, rect.y, rect.width - 30f, rect.height), label);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(rect.xMax - 22f, rect.y, 18f, rect.height), "▼");
            GUI.color = Color.white;
            Text.Anchor = oldAnchor;

            if (Widgets.ButtonInvisible(rect))
            {
                ShowWorkbenchPromptChannelMenu();
            }
        }

        internal void ShowWorkbenchPromptChannelMenu()
        {
            List<FloatMenuOption> options = PromptSectionSchemaCatalog.GetAllWorkspaceChannels()
                .Select(channelId => new FloatMenuOption(
                    RimTalkPromptEntryChannelCatalog.GetLabel(channelId),
                    () => SetWorkbenchPromptChannel(channelId)))
                .ToList();
            Find.WindowStack.Add(new FloatMenu(options));
        }

        internal string EnsureWorkbenchPromptChannelSelection()
        {
            string normalized = RimTalkPromptEntryChannelCatalog.NormalizeLoose(_workbenchPromptChannel);
            if (!PromptSectionSchemaCatalog.GetAllWorkspaceChannels().Contains(normalized, StringComparer.Ordinal))
            {
                normalized = GetDefaultWorkbenchPromptChannelForRoot(_workbenchChannel);
            }

            if (!DoesPromptChannelBelongToWorkbenchRoot(normalized, _workbenchChannel))
            {
                normalized = GetDefaultWorkbenchPromptChannelForRoot(_workbenchChannel);
            }

            _workbenchPromptChannel = normalized;
            return _workbenchPromptChannel;
        }

        internal static string GetDefaultWorkbenchPromptChannelForRoot(PromptWorkbenchChannel root)
        {
            return PromptSectionSchemaCatalog.GetDefaultWorkspaceChannel(ToRimTalkPromptChannel(root));
        }

        internal static bool DoesPromptChannelBelongToWorkbenchRoot(string channelId, PromptWorkbenchChannel root)
        {
            return PromptSectionSchemaCatalog.DoesChannelBelongToRoot(channelId, ToRimTalkPromptChannel(root));
        }

        internal void SetWorkbenchPromptChannel(string channelId)
        {
            string normalized = RimTalkPromptEntryChannelCatalog.NormalizeLoose(channelId);
            if (!PromptSectionSchemaCatalog.GetAllWorkspaceChannels().Contains(normalized, StringComparer.Ordinal))
            {
                normalized = GetDefaultWorkbenchPromptChannelForRoot(_workbenchChannel);
            }

            PromptWorkbenchChannel desiredRoot = ResolveWorkbenchRootChannel(normalized);
            bool rootChanged = _workbenchChannel != desiredRoot;
            _workbenchPromptChannel = normalized;
            _workbenchChannel = desiredRoot;
            _workbenchRpgSubTab = 0;
            Pages.RimTalkTab._rimTalkSelectedEntryId = string.Empty;
            ApplyWorkbenchEntryChannelSelection(_workbenchChannel);
            if (rootChanged)
            {
                InvalidateWorkbenchEditingChannelConfig();
            }

            FocusWorkbenchEntryByPromptChannel(normalized);
            Pages.RimTalkTab.ResetRimTalkEntryContentBuffer();
        }

        internal PromptWorkbenchChannel ResolveWorkbenchRootChannel(string channelId)
        {
            RimTalkPromptChannel root = PromptSectionSchemaCatalog.ResolveRootChannel(
                channelId,
                ToRimTalkPromptChannel(_workbenchChannel));
            return ToWorkbenchChannel(root);
        }

        internal static RimTalkPromptChannel ToRimTalkPromptChannel(PromptWorkbenchChannel channel)
        {
            return channel == PromptWorkbenchChannel.Rpg
                ? RimTalkPromptChannel.Rpg
                : RimTalkPromptChannel.Diplomacy;
        }

        internal static PromptWorkbenchChannel ToWorkbenchChannel(RimTalkPromptChannel channel)
        {
            return channel == RimTalkPromptChannel.Rpg
                ? PromptWorkbenchChannel.Rpg
                : PromptWorkbenchChannel.Diplomacy;
        }

        internal void FocusWorkbenchEntryByPromptChannel(string channelId)
        {
            RimTalkChannelCompatConfig config = GetWorkbenchEditingChannelConfig();
            if (config?.PromptEntries == null || config.PromptEntries.Count == 0)
            {
                Pages.RimTalkTab._rimTalkSelectedEntryId = string.Empty;
                Pages.RimTalkTab._rimTalkDepthBuffer = string.Empty;
                return;
            }

            string normalized = RimTalkPromptEntryChannelCatalog.NormalizeForRoot(channelId, Pages.RimTalkTab._rimTalkEditorChannel);
            RimTalkPromptEntryConfig matched = config.PromptEntries.FirstOrDefault(entry =>
                entry != null && string.Equals(
                    RimTalkPromptEntryChannelCatalog.NormalizeForRoot(entry.PromptChannel, Pages.RimTalkTab._rimTalkEditorChannel),
                    normalized,
                    StringComparison.OrdinalIgnoreCase));

            if (matched == null)
            {
                Pages.RimTalkTab.EnsureRimTalkEntrySelection(config);
                return;
            }

            Pages.RimTalkTab._rimTalkSelectedEntryId = matched.Id ?? string.Empty;
            Pages.RimTalkTab._rimTalkDepthBuffer = matched.InChatDepth.ToString();
        }

        internal void DrawWorkbenchBody(Rect rect)
        {
            float gap = 6f;
            float leftWidth = Mathf.Clamp(rect.width * 0.2f, 200f, 220f);
            Rect leftRect = new Rect(rect.x, rect.y, leftWidth, rect.height);
            Rect workspaceRect = new Rect(leftRect.xMax + gap, rect.y, rect.width - leftWidth - gap, rect.height);
            float sideWidth = Mathf.Clamp(workspaceRect.width * 0.36f, 260f, 380f);
            if (workspaceRect.width - sideWidth - gap < 320f)
            {
                sideWidth = Mathf.Max(220f, workspaceRect.width - 320f - gap);
            }

            Rect centerRect = new Rect(workspaceRect.x, workspaceRect.y, workspaceRect.width - sideWidth - gap, workspaceRect.height);
            Rect rightRect = new Rect(centerRect.xMax + gap, workspaceRect.y, sideWidth, workspaceRect.height);
            GetWorkbenchEditingChannelConfig();
            DrawWorkbenchPresetPanel(leftRect);
            DrawWorkbenchMainPanel(centerRect);
            DrawWorkbenchSidePanelContainer(rightRect);
        }

        internal RimTalkChannelCompatConfig GetWorkbenchEditingChannelConfig()
        {
            ApplyWorkbenchEntryChannelSelection(_workbenchChannel);
            RimTalkPromptChannel channel = Pages.RimTalkTab._rimTalkEditorChannel;
            if (_workbenchEditingConfigReady &&
                _workbenchEditingConfig != null &&
                _workbenchEditingConfigChannel == channel)
            {
                return _workbenchEditingConfig;
            }

            RimTalkChannelCompatConfig config = Settings.GetRimTalkChannelConfigClone(channel);
            config.NormalizeWith(RimTalkChannelCompatConfig.CreateDefault());
            Pages.RimTalkTab.EnsureRimTalkEntrySelection(config);
            _workbenchEditingConfig = config;
            _workbenchEditingConfigChannel = channel;
            _workbenchEditingConfigReady = true;
            return _workbenchEditingConfig;
        }

        internal void SyncWorkbenchEditingChannelConfig(RimTalkPromptChannel channel, RimTalkChannelCompatConfig config)
        {
            if (config == null)
            {
                InvalidateWorkbenchEditingChannelConfig();
                return;
            }

            RimTalkChannelCompatConfig cloned = config.Clone();
            cloned.NormalizeWith(RimTalkChannelCompatConfig.CreateDefault());
            Pages.RimTalkTab.EnsureRimTalkEntrySelection(cloned);
            _workbenchEditingConfig = cloned;
            _workbenchEditingConfigChannel = channel;
            _workbenchEditingConfigReady = true;
        }

        internal void InvalidateWorkbenchEditingChannelConfig()
        {
            _workbenchEditingConfig = null;
            _workbenchEditingConfigReady = false;
            Pages.PromptWorkspace.MarkWorkspaceAllDirty();
        }

        internal void DrawWorkbenchPresetPanel(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.09f, 0.10f, 0.12f));
            Rect inner = rect.ContractedBy(8f);
            float topHeight = Mathf.Clamp(inner.height * 0.44f, 250f, 330f);
            Rect presetRect = new Rect(inner.x, inner.y, inner.width, topHeight);
            Rect lowerRect = new Rect(inner.x, presetRect.yMax + 8f, inner.width, inner.height - topHeight - 8f);

            Widgets.Label(new Rect(presetRect.x, presetRect.y, presetRect.width, 22f), "RimChat_PromptWorkbench_PresetHeader".Translate());
            float y = presetRect.y + 24f;
            Pages.PromptWorkbenchPresets.DrawPresetActions(new Rect(presetRect.x, y, presetRect.width, 24f));
            y += 28f;
            float listHeight = Mathf.Clamp(presetRect.height - 140f, 96f, 160f);
            Pages.PromptWorkbenchPresets.DrawPresetList(new Rect(presetRect.x, y, presetRect.width, listHeight));
            y += listHeight + 6f;
            Pages.PromptWorkbenchPresets.DrawPresetBottomActions(new Rect(presetRect.x, y, presetRect.width, presetRect.yMax - y));

            RimTalkChannelCompatConfig config = GetWorkbenchEditingChannelConfig();
            Pages.RimTalkEntries.DrawRimTalkPromptEntryList(lowerRect, config);
        }

        internal void DrawWorkbenchMainPanel(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.06f, 0.07f, 0.09f));
            Rect inner = rect.ContractedBy(8f);
            float y = inner.y;

            DrawWorkbenchPresetNameRow(inner, ref y);

            if (_workbenchChannel == PromptWorkbenchChannel.Rpg)
            {
                DrawRpgWorkbenchSubTabs(inner, ref y);
            }

            Rect contentRect = new Rect(inner.x, y, inner.width, inner.yMax - y);
            if (_workbenchChannel == PromptWorkbenchChannel.Rpg && _workbenchRpgSubTab == 1)
            {
                Pages.RpgEditors.DrawRPGPawnPersonaEditor(contentRect);
                return;
            }

            RimTalkChannelCompatConfig config = GetWorkbenchEditingChannelConfig();
            Pages.RimTalkTab.DrawRimTalkPromptEntryEditor(contentRect, config, useChipEditor: true);
        }

        internal void DrawWorkbenchPresetNameRow(Rect inner, ref float y)
        {
            Rect row = new Rect(inner.x, y, inner.width, 24f);
            float labelWidth = 86f;
            Rect labelRect = new Rect(row.x, row.y, labelWidth, row.height);
            Rect valueRect = new Rect(labelRect.xMax + 4f, row.y, row.width - labelWidth - 4f, row.height);
            Widgets.Label(labelRect, "RimChat_PromptWorkbench_SelectedPresetName".Translate());
            Widgets.DrawBoxSolid(valueRect, new Color(0.03f, 0.03f, 0.04f));
            Widgets.DrawBox(valueRect, 1);
            PromptPresetConfig selected = Pages.PromptWorkbenchPresets.GetSelectedPreset();
            string name = selected?.Name ?? "RimChat_PromptPreset_NoSelection".Translate().ToString();
            Widgets.Label(new Rect(valueRect.x + 6f, valueRect.y + 2f, valueRect.width - 12f, valueRect.height), name);
            y += 30f;
        }

        internal void DrawRpgWorkbenchSubTabs(Rect inner, ref float y)
        {
            Rect row = new Rect(inner.x, y, inner.width, 26f);
            float width = (row.width - 6f) * 0.5f;
            Rect entriesRect = new Rect(row.x, row.y, width, row.height);
            Rect personaRect = new Rect(entriesRect.xMax + 6f, row.y, width, row.height);
            DrawWorkbenchSubTab(entriesRect, 0, "RimChat_PromptWorkbench_RpgSubEntries");
            DrawWorkbenchSubTab(personaRect, 1, "RimChat_PromptWorkbench_RpgSubPersona");
            y += 30f;
        }

        internal void DrawWorkbenchSubTab(Rect rect, int index, string key)
        {
            bool selected = _workbenchRpgSubTab == index;
            Color color = selected ? new Color(0.45f, 0.33f, 0.15f) : new Color(0.19f, 0.15f, 0.10f);
            Widgets.DrawBoxSolid(rect, color);
            Widgets.DrawBox(rect, 1);
            TextAnchor oldAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = selected ? new Color(1f, 0.88f, 0.55f) : Color.white;
            Widgets.Label(rect, key.Translate());
            GUI.color = Color.white;
            Text.Anchor = oldAnchor;
            if (Widgets.ButtonInvisible(rect))
            {
                _workbenchRpgSubTab = index;
            }
        }

        internal void DrawWorkbenchSidePanelContainer(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.09f, 0.10f, 0.12f));
            Rect inner = rect.ContractedBy(8f);
            float buttonWidth = (inner.width - 6f) / 2f;
            Rect previewRect = new Rect(inner.x, inner.y, buttonWidth, 24f);
            Rect varsRect = new Rect(previewRect.xMax + 6f, inner.y, buttonWidth, 24f);

            DrawWorkbenchSideButton(previewRect, PromptWorkbenchInfoPanel.Preview, "RimChat_PreviewTitleShort");
            DrawWorkbenchSideButton(varsRect, PromptWorkbenchInfoPanel.Variables, "RimChat_PromptWorkbench_VariablesTab");

            Rect contentRect = new Rect(inner.x, previewRect.yMax + 6f, inner.width, inner.height - 30f);
            switch (_workbenchSidePanelTab)
            {
                case PromptWorkbenchInfoPanel.Variables:
                    DrawWorkbenchVariables(contentRect);
                    break;
                default:
                    DrawWorkbenchPreview(contentRect);
                    break;
            }
        }

        internal void DrawWorkbenchSideButton(Rect rect, PromptWorkbenchInfoPanel panel, string key)
        {
            bool selected = _workbenchSidePanelTab == panel;
            Widgets.DrawBoxSolid(rect, selected ? new Color(0.45f, 0.33f, 0.15f) : new Color(0.19f, 0.15f, 0.10f));
            Widgets.DrawBox(rect, 1);
            TextAnchor oldAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = selected ? new Color(1f, 0.88f, 0.55f) : Color.white;
            Widgets.Label(rect, key.Translate());
            GUI.color = Color.white;
            Text.Anchor = oldAnchor;
            if (Widgets.ButtonInvisible(rect))
            {
                _workbenchSidePanelTab = panel;
                Pages.PromptWorkspace.MarkWorkspaceDirty(RelationsPromptSectionWorkspace.WorkspaceDirtySidePanel);
            }
        }

        internal void DrawWorkbenchPreview(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.03f, 0.03f, 0.04f));
            Rect inner = rect.ContractedBy(6f);
            string preview = BuildWorkbenchPreviewText();
            float contentHeight = Mathf.Max(inner.height, Text.CalcHeight(preview, inner.width - 16f) + 12f);
            Rect view = new Rect(0f, 0f, inner.width - 16f, contentHeight);
            Pages.PromptLegacy._previewScroll = GUI.BeginScrollView(inner, Pages.PromptLegacy._previewScroll, view);
            GUI.color = Color.white;
            Widgets.Label(new Rect(0f, 0f, view.width, contentHeight), preview);
            GUI.EndScrollView();
        }

        internal string BuildWorkbenchPreviewText()
        {
            if (_workbenchChannel == PromptWorkbenchChannel.Rpg && _workbenchRpgSubTab == 1)
            {
                return "RimChat_PromptWorkbench_PersonaPreviewHint".Translate();
            }

            RimTalkChannelCompatConfig config = GetWorkbenchEditingChannelConfig();
            string merged = RelationsSettingsPromptLanguage.ComposePromptEntryTextByRole(config?.PromptEntries, includeSystemRole: true, includeNonSystemRole: true);
            if (string.IsNullOrWhiteSpace(merged))
            {
                return "RimChat_PromptWorkbench_PreviewEmpty".Translate();
            }

            return merged;
        }




        internal void ApplyWorkbenchEntryChannelSelection(PromptWorkbenchChannel channel)
        {
            Pages.RimTalkTab._rimTalkEditorChannel = channel == PromptWorkbenchChannel.Diplomacy
                ? RimTalkPromptChannel.Diplomacy
                : RimTalkPromptChannel.Rpg;
            if (_workbenchSeededEntryChannel == Pages.RimTalkTab._rimTalkEditorChannel)
            {
                return;
            }

            Settings.EnsurePromptEntrySeedForChannel(Pages.RimTalkTab._rimTalkEditorChannel);
            _workbenchSeededEntryChannel = Pages.RimTalkTab._rimTalkEditorChannel;
        }

        internal bool IsEntryDrivenWorkbenchChannelActive()
        {
            return Settings._promptWorkbenchExperimentalEnabled &&
                   (_workbenchChannel == PromptWorkbenchChannel.Diplomacy ||
                    _workbenchChannel == PromptWorkbenchChannel.Rpg);
        }

        internal bool TryInsertVariableTokenToEntryChannel(string token)
        {
            if (Pages.PromptWorkspaceBuffers.TryInsertVariableTokenToPromptWorkspace(token))
            {
                return true;
            }

            if (!IsEntryDrivenWorkbenchChannelActive())
            {
                return false;
            }

            string variableName = NormalizeVariableNameToken(token);
            if (string.IsNullOrWhiteSpace(variableName))
            {
                return false;
            }

            if (_workbenchChannel == PromptWorkbenchChannel.Rpg && _workbenchRpgSubTab == 1)
            {
                return false;
            }

            ApplyWorkbenchEntryChannelSelection(_workbenchChannel);
            Pages.RimTalkTemplates.AppendVariableToCurrentRimTalkTemplate(variableName);
            return true;
        }

        internal static string NormalizeVariableNameToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return string.Empty;
            }

            string normalized = token.Trim();
            if (normalized.StartsWith("{{", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(2);
            }

            if (normalized.EndsWith("}}", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(0, normalized.Length - 2);
            }

            return normalized.Trim().Trim('{', '}', ' ');
        }

        internal void DrawWorkbenchVariables(Rect rect)
        {
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 22f), "RimChat_PromptWorkbench_VariablesTitle".Translate());
            Rect contentRect = new Rect(rect.x, rect.y + 24f, rect.width, rect.height - 24f);
            RimTalkChannelCompatConfig config = GetWorkbenchEditingChannelConfig();
            RimTalkPromptEntryConfig selectedEntry = Pages.RimTalkTab.GetSelectedRimTalkPromptEntry(config);
            Pages.VariableBrowser.DrawRimTalkWorkbenchVariableBrowser(contentRect, selectedEntry?.Content);
        }

        internal string[] GetCurrentSourceHints()
        {
            if (_workbenchChannel == PromptWorkbenchChannel.Diplomacy)
            {
                return new[] { "DiplomacyDialogue", "SocialNews", "SendImage", "StrategySuggestion" };
            }

            return new[] { "RpgDialogue", "NpcPush", "PawnRpgPush", "PersonaBootstrap", "MemorySummary", "ArchiveCompression" };
        }





        internal void DrawPromptWorkbenchWindow(Rect rect)
        {
            try
            {
                DrawAdvancedPromptWorkbench(rect);
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAI.Relations] Prompt workbench window render failed: {ex}");
                Widgets.Label(rect, "RimChat_PromptRenderFailed".Translate());
            }
        }

        internal void OpenPromptWorkbenchWindow()
        {
            OpenPromptWorkbenchWindow(PromptWorkbenchChannel.Diplomacy);
        }

        internal void OpenPromptWorkbenchWindowForRpg()
        {
            OpenPromptWorkbenchWindow(PromptWorkbenchChannel.Rpg);
        }

        internal void OpenPromptWorkbenchWindow(PromptWorkbenchChannel initialChannel)
        {
            _workbenchChannel = initialChannel;
            Pages.PromptLegacy._advancedPromptMode = false;
            Settings.SetPromptWorkbenchExperimentalEnabled(false);

            // Close existing popup if open, then open a fresh large-size workbench window.
            Dialog_PromptWorkbenchLarge existing = Find.WindowStack.WindowOfType<Dialog_PromptWorkbenchLarge>();
            if (existing != null)
            {
                Find.WindowStack.TryRemove(existing);
            }

            Pages.PromptWorkspaceBuffers.EnsurePromptWorkspaceSelection();
            Find.WindowStack.Add(new Dialog_PromptWorkbenchLarge(Settings));
        }

        internal void SetWorkbenchChannelRimTalkRpg()
        {
            OpenPromptWorkbenchWindowForRpg();
        }


    
}
