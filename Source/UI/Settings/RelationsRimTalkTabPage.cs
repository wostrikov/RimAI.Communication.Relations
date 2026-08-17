using System;
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.UI;
using RimWorld;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Core.UI;

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class RelationsRimTalkTabPage
{
    internal readonly RelationsSettingsPages Pages;

    internal RelationsRimTalkTabPage(RelationsSettingsPages pages)
    {
        Pages = pages;
    }

    internal RelationsSettings Settings => Pages.Settings;

        internal Vector2 _rimTalkTabScroll = Vector2.zero;
        internal RimTalkPromptChannel _rimTalkEditorChannel = RimTalkPromptChannel.Rpg;
        internal Vector2 _rimTalkPersonaCopyTemplateScroll = Vector2.zero;
        internal Vector2 _rimTalkEntryListScroll = Vector2.zero;
        internal Vector2 _rimTalkEntryContentScroll = Vector2.zero;
        internal string _rimTalkSelectedEntryId = string.Empty;
        internal string _rimTalkDepthBuffer = string.Empty;
        internal string _rimTalkEntryContentBuffer = string.Empty;
        internal string _rimTalkEntryContentBufferEntryId = string.Empty;
        internal string _rimTalkEntryContentSnapshot = string.Empty;
        internal PromptWorkbenchChipEditor _workbenchChipEditor;
        internal bool _workbenchChipEditorDisabledForSession;
        internal const int ChipEditorContentLengthSoftLimit = 24000;
        internal const int ChipEditorTokenCountSoftLimit = 320;
        internal static readonly string[] RimTalkEntryRoles = { "System", "User", "Assistant" };
        internal static readonly string[] RimTalkEntryPositions = { "Relative", "InChat" };

        internal void DrawTab_RimTalk(Rect rect)
        {
            Pages.RimTalkBridge.DrawRimTalkBridgePage(rect);
        }

        internal void DrawRimTalkChannelSelector(Listing_Standard listing)
        {
            listing.Label("RimChat_RimTalkChannelTitle".Translate());
            Rect row = listing.GetRect(26f);
            float half = (row.width - 8f) * 0.5f;
            Rect diplomacyRect = new Rect(row.x, row.y, half, row.height);
            Rect rpgRect = new Rect(diplomacyRect.xMax + 8f, row.y, half, row.height);

            bool diplomacySelected = _rimTalkEditorChannel == RimTalkPromptChannel.Diplomacy;
            bool rpgSelected = _rimTalkEditorChannel == RimTalkPromptChannel.Rpg;
            Widgets.DrawBoxSolid(diplomacyRect, diplomacySelected ? new Color(0.25f, 0.35f, 0.55f) : new Color(0.18f, 0.18f, 0.2f));
            Widgets.DrawBoxSolid(rpgRect, rpgSelected ? new Color(0.25f, 0.35f, 0.55f) : new Color(0.18f, 0.18f, 0.2f));
            Widgets.Label(diplomacyRect, "RimChat_RimTalkChannelDiplomacy".Translate());
            Widgets.Label(rpgRect, "RimChat_RimTalkChannelRpg".Translate());

            if (Widgets.ButtonInvisible(diplomacyRect))
            {
                _rimTalkEditorChannel = RimTalkPromptChannel.Diplomacy;
            }

            if (Widgets.ButtonInvisible(rpgRect))
            {
                _rimTalkEditorChannel = RimTalkPromptChannel.Rpg;
            }

            listing.Gap(6f);
        }

        internal void DrawRimTalkChannelEditor(Listing_Standard listing)
        {
            RimTalkChannelCompatConfig config = Settings.GetRimTalkChannelConfigClone(_rimTalkEditorChannel);
            bool enabled = config.EnablePromptCompat;
            listing.CheckboxLabeled("RimChat_RimTalkCompatEnable".Translate(), ref enabled);
            if (enabled != config.EnablePromptCompat)
            {
                config.EnablePromptCompat = enabled;
                Settings.SetRimTalkChannelConfig(_rimTalkEditorChannel, config);
            }
            GUI.color = Color.gray;
            listing.Label("RimChat_RimTalkCompatEnableHint".Translate());
            GUI.color = Color.white;

            bool autoPushSummary = Settings.RimTalkAutoPushSessionSummary;
            listing.CheckboxLabeled("RimChat_RimTalkAutoPushSummary".Translate(), ref autoPushSummary);
            if (autoPushSummary != Settings.RimTalkAutoPushSessionSummary)
            {
                Settings.RimTalkAutoPushSessionSummary = autoPushSummary;
            }

            bool autoInjectPreset = Settings.RimTalkAutoInjectCompatPreset;
            listing.CheckboxLabeled("RimChat_RimTalkAutoInjectPreset".Translate(), ref autoInjectPreset);
            if (autoInjectPreset != Settings.RimTalkAutoInjectCompatPreset)
            {
                Settings.RimTalkAutoInjectCompatPreset = autoInjectPreset;
            }

            listing.Label("RimChat_RimTalkSummaryHistoryLimit".Translate(Settings.GetRimTalkSummaryHistoryLimitClamped()));
            string editedHistory = listing.TextEntry(Settings.RimTalkSummaryHistoryLimit.ToString());
            if (int.TryParse(editedHistory, out int parsedHistory))
            {
                Settings.RimTalkSummaryHistoryLimit = Mathf.Clamp(parsedHistory, RelationsSettings.RimTalkSummaryHistoryMin, RelationsSettings.RimTalkSummaryHistoryMax);
            }

            int currentEntries = config.PresetInjectionMaxEntries;
            string entriesValue = RelationsRpgRimTalkCompatUi.FormatUnlimitedAwareLimit(Settings.GetRimTalkPresetInjectionMaxEntriesClamped(Pages.RimTalkTemplates.GetCurrentChannelToken()));
            listing.Label("RimChat_RimTalkPresetInjectionMaxEntries".Translate(entriesValue));
            string editedEntries = listing.TextEntry(currentEntries.ToString());
            if (int.TryParse(editedEntries, out int parsedEntries))
            {
                config.PresetInjectionMaxEntries = Mathf.Clamp(
                    parsedEntries,
                    RelationsSettings.RimTalkPresetInjectionMaxEntriesMin,
                    RelationsSettings.RimTalkPresetInjectionMaxEntriesMax);
                Settings.SetRimTalkChannelConfig(_rimTalkEditorChannel, config);
            }

            int currentChars = config.PresetInjectionMaxChars;
            string charsValue = RelationsRpgRimTalkCompatUi.FormatUnlimitedAwareLimit(Settings.GetRimTalkPresetInjectionMaxCharsClamped(Pages.RimTalkTemplates.GetCurrentChannelToken()));
            listing.Label("RimChat_RimTalkPresetInjectionMaxChars".Translate(charsValue));
            string editedChars = listing.TextEntry(currentChars.ToString());
            if (int.TryParse(editedChars, out int parsedChars))
            {
                config.PresetInjectionMaxChars = Mathf.Clamp(
                    parsedChars,
                    RelationsSettings.RimTalkPresetInjectionMaxCharsMin,
                    RelationsSettings.RimTalkPresetInjectionMaxCharsMax);
                Settings.SetRimTalkChannelConfig(_rimTalkEditorChannel, config);
            }

            DrawRimTalkPromptEntryWorkbench(listing, config);
            if (_rimTalkEditorChannel == RimTalkPromptChannel.Rpg)
            {
                Pages.RimTalkTemplates.DrawRimTalkPersonaCopyTemplateEditor(listing);
            }

            GUI.color = Color.gray;
            listing.Label("RimChat_RimTalkCompatTemplateHint".Translate());
            listing.Label("RimChat_RimTalkPresetInjectionLimitHint".Translate());
            listing.Label("RimChat_RimTalkIsolationHint".Translate());
            if (_rimTalkEditorChannel == RimTalkPromptChannel.Rpg)
            {
                listing.Label("RimChat_RimTalkPersonaCopyTemplateHint".Translate());
            }
            GUI.color = Color.white;
            listing.GapLine();
        }

        internal void DrawRimTalkPromptEntryWorkbench(Listing_Standard listing, RimTalkChannelCompatConfig config)
        {
            if (config == null)
            {
                return;
            }

            config.NormalizeWith(RimTalkChannelCompatConfig.CreateDefault());
            EnsureRimTalkEntrySelection(config);
            listing.Label("RimChat_RimTalkCompatTemplate".Translate());
            Rect workRect = listing.GetRect(312f);
            float leftWidth = Mathf.Clamp(workRect.width * 0.38f, 250f, 340f);
            Rect leftRect = new Rect(workRect.x, workRect.y, leftWidth, workRect.height);
            Rect rightRect = new Rect(leftRect.xMax + 8f, workRect.y, workRect.width - leftWidth - 8f, workRect.height);
            Widgets.DrawBoxSolid(leftRect, new Color(0.12f, 0.12f, 0.14f));
            Widgets.DrawBoxSolid(rightRect, new Color(0.10f, 0.10f, 0.12f));
            Pages.RimTalkEntries.DrawRimTalkPromptEntryList(leftRect.ContractedBy(6f), config);
            DrawRimTalkPromptEntryEditor(rightRect.ContractedBy(6f), config, useChipEditor: false);
        }







        internal void DrawRimTalkPromptEntryEditor(Rect rect, RimTalkChannelCompatConfig config, bool useChipEditor = false)
        {
            Pages.RimTalkEntries.EnsureSelectedEntryInVisibleScope(config, Pages.RimTalkEntries.CollectVisiblePromptEntryIndices(config, Pages.RimTalkEntries.GetScopedPromptChannelOrEmpty()));
            RimTalkPromptEntryConfig entry = EnsureRimTalkEditableEntry(config);
            if (entry == null)
            {
                ResetRimTalkEntryContentBuffer();
                Widgets.Label(rect, "RimChat_RimTalkEntryNone".Translate());
                return;
            }

            SyncRimTalkEntryContentBuffer(entry);
            bool dirty = false;
            string normalizedPromptChannel = RimTalkPromptEntryChannelCatalog.NormalizeForRoot(entry.PromptChannel, _rimTalkEditorChannel);
            if (!string.Equals(normalizedPromptChannel, entry.PromptChannel, StringComparison.OrdinalIgnoreCase))
            {
                entry.PromptChannel = normalizedPromptChannel;
                dirty = true;
            }
            float y = rect.y;
            float nameLabelWidth = Mathf.Clamp(Text.CalcSize("RimChat_RimTalkEntryName".Translate()).x + 8f, 72f, 140f);
            Widgets.Label(new Rect(rect.x, y, nameLabelWidth, 24f), "RimChat_RimTalkEntryName".Translate());
            string editedName = Widgets.TextField(new Rect(rect.x + nameLabelWidth + 4f, y, rect.width - nameLabelWidth - 4f, 24f), entry.Name ?? string.Empty);
            if (!string.Equals(editedName, entry.Name, StringComparison.Ordinal))
            {
                entry.Name = editedName;
                dirty = true;
            }

            y += 28f;
            bool enabled = entry.Enabled;
            float enabledWidth = Mathf.Clamp(rect.width * 0.34f, 140f, 180f);
            Widgets.CheckboxLabeled(new Rect(rect.x, y, enabledWidth, 24f), "RimChat_RimTalkCompatEnable".Translate(), ref enabled);
            if (enabled != entry.Enabled)
            {
                entry.Enabled = enabled;
                dirty = true;
            }

            float actionStart = rect.x + enabledWidth + 6f;
            float actionWidth = rect.xMax - actionStart;
            Rect roleRect;
            Rect positionRect;
            if (actionWidth >= 140f)
            {
                float roleWidth = Mathf.Max(58f, (actionWidth - 6f) * 0.5f);
                roleRect = new Rect(actionStart, y, roleWidth, 24f);
                positionRect = new Rect(roleRect.xMax + 6f, y, Mathf.Max(56f, rect.xMax - (roleRect.xMax + 6f)), 24f);
            }
            else
            {
                y += 28f;
                roleRect = new Rect(rect.x, y, rect.width, 24f);
                y += 28f;
                positionRect = new Rect(rect.x, y, rect.width, 24f);
            }

            if (Widgets.ButtonText(roleRect, "RimChat_RimTalkEntryRole".Translate() + ": " + GetRimTalkRoleLabel(entry.Role)))
            {
                ShowRimTalkRoleMenu(_rimTalkEditorChannel, entry.Id);
            }

            if (Widgets.ButtonText(positionRect, "RimChat_RimTalkEntryPosition".Translate() + ": " + GetRimTalkPositionLabel(entry.Position)))
            {
                ShowRimTalkPositionMenu(_rimTalkEditorChannel, entry.Id);
            }

            y += 28f;
            float customRoleLabelWidth = Mathf.Clamp(Text.CalcSize("RimChat_RimTalkEntryCustomRole".Translate()).x + 8f, 72f, 160f);
            Widgets.Label(new Rect(rect.x, y, customRoleLabelWidth, 24f), "RimChat_RimTalkEntryCustomRole".Translate());
            string customRole = Widgets.TextField(new Rect(rect.x + customRoleLabelWidth + 4f, y, rect.width - customRoleLabelWidth - 4f, 24f), entry.CustomRole ?? string.Empty);
            if (!string.Equals(customRole, entry.CustomRole, StringComparison.Ordinal))
            {
                entry.CustomRole = string.IsNullOrWhiteSpace(customRole) ? string.Empty : customRole.Trim();
                dirty = true;
            }

            y += 28f;
            if (string.Equals(entry.Position, "InChat", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(_rimTalkDepthBuffer))
                {
                    _rimTalkDepthBuffer = entry.InChatDepth.ToString();
                }

                float depthLabelWidth = Mathf.Clamp(Text.CalcSize("RimChat_RimTalkEntryDepth".Translate()).x + 8f, 72f, 140f);
                Widgets.Label(new Rect(rect.x, y, depthLabelWidth, 24f), "RimChat_RimTalkEntryDepth".Translate());
                _rimTalkDepthBuffer = Widgets.TextField(new Rect(rect.x + depthLabelWidth + 4f, y, 64f, 24f), _rimTalkDepthBuffer ?? "0");
                if (int.TryParse(_rimTalkDepthBuffer, out int depth))
                {
                    int clamped = Mathf.Clamp(depth, 0, 32);
                    if (clamped != entry.InChatDepth)
                    {
                        entry.InChatDepth = clamped;
                        dirty = true;
                    }
                }

                y += 28f;
            }

            Widgets.Label(new Rect(rect.x, y, rect.width, 22f), "RimChat_RimTalkEntryContent".Translate());
            y += 22f;
            const float validationStatusHeight = 24f;
            const float validationGap = 2f;
            float contentAreaHeight = Mathf.Max(24f, rect.yMax - y - validationStatusHeight - validationGap);
            Rect contentRect = new Rect(rect.x, y, rect.width, contentAreaHeight);
            string bufferedContent = _rimTalkEntryContentBuffer ?? string.Empty;
            string editedContent = DrawPromptEntryContentEditor(contentRect, bufferedContent, useChipEditor);
            Rect validationRect = new Rect(rect.x, contentRect.yMax + validationGap, rect.width, validationStatusHeight);
            Pages.RimTalkTemplates.DrawRimTalkTemplateValidationStatus(validationRect, editedContent);
            if (!string.Equals(editedContent, bufferedContent, StringComparison.Ordinal))
            {
                _rimTalkEntryContentBuffer = editedContent;
            }

            if (!string.Equals(_rimTalkEntryContentBuffer, entry.Content ?? string.Empty, StringComparison.Ordinal))
            {
                entry.Content = _rimTalkEntryContentBuffer;
                dirty = true;
            }

            if (dirty)
            {
                Settings.SetRimTalkChannelConfig(_rimTalkEditorChannel, config);
            }

            _rimTalkEntryContentSnapshot = entry.Content ?? string.Empty;
        }

        internal string DrawPromptEntryContentEditor(Rect contentRect, string text, bool useChipEditor)
        {
            if (!useChipEditor || _workbenchChipEditorDisabledForSession || ExceedsChipEditorSoftLimits(text))
            {
                return DrawLegacyPromptEntryTextArea(contentRect, text);
            }

            try
            {
                _workbenchChipEditor ??= new PromptWorkbenchChipEditor("RimChat_WorkbenchPromptEntryContentEditor");
                return _workbenchChipEditor.Draw(contentRect, text, ref _rimTalkEntryContentScroll);
            }
            catch (Exception ex)
            {
                _workbenchChipEditorDisabledForSession = true;
                Log.Warning($"[RimAI.Relations] Prompt workbench chip editor fallback activated: {ex.GetType().Name}: {ex.Message}");
                return DrawLegacyPromptEntryTextArea(contentRect, text);
            }
        }

        internal static bool ExceedsChipEditorSoftLimits(string text)
        {
            string content = text ?? string.Empty;
            if (content.Length > ChipEditorContentLengthSoftLimit)
            {
                return true;
            }

            int markers = CountTokenMarkers(content);
            return markers > ChipEditorTokenCountSoftLimit;
        }

        internal static int CountTokenMarkers(string text)
        {
            int count = 0;
            for (int i = 0; i < text.Length - 1; i++)
            {
                if (text[i] == '{' && text[i + 1] == '{')
                {
                    count++;
                    i++;
                }
            }

            return count;
        }

        internal string DrawLegacyPromptEntryTextArea(Rect contentRect, string text)
        {
            string source = text ?? string.Empty;
            var textAreaStyle = new GUIStyle(GUI.skin.textArea)
            {
                wordWrap = true,
                richText = false
            };
            float contentWidth = Mathf.Max(1f, contentRect.width - 16f);
            float contentHeight = Mathf.Max(contentRect.height, textAreaStyle.CalcHeight(new GUIContent(source), contentWidth) + 4f);
            Rect viewRect = new Rect(0f, 0f, contentWidth, contentHeight);
            _rimTalkEntryContentScroll = new Vector2(
                0f,
                Mathf.Clamp(_rimTalkEntryContentScroll.y, 0f, Mathf.Max(0f, viewRect.height - contentRect.height)));
            _rimTalkEntryContentScroll = GUI.BeginScrollView(contentRect, _rimTalkEntryContentScroll, viewRect, false, true);
            string editedContent = GUI.TextArea(new Rect(0f, 0f, contentWidth, contentHeight), source, textAreaStyle);
            GUI.EndScrollView();
            return editedContent;
        }

        internal RimTalkPromptEntryConfig EnsureRimTalkEditableEntry(RimTalkChannelCompatConfig config)
        {
            RimTalkPromptEntryConfig entry = GetSelectedRimTalkPromptEntry(config);
            if (entry != null)
            {
                return entry;
            }

            if (config == null)
            {
                return null;
            }

            config.PromptEntries ??= new List<RimTalkPromptEntryConfig>();
            var created = new RimTalkPromptEntryConfig
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = "RimChat_RimTalkEntryDefaultName".Translate(),
                Role = "System",
                CustomRole = string.Empty,
                Position = "Relative",
                InChatDepth = 0,
                Enabled = true,
                PromptChannel = Pages.RimTalkEntries.ResolveEntryCreationPromptChannel(Pages.RimTalkEntries.GetScopedPromptChannelOrEmpty()),
                Content = string.Empty
            };
            config.PromptEntries.Add(created);
            _rimTalkSelectedEntryId = created.Id;
            _rimTalkDepthBuffer = created.InChatDepth.ToString();
            Settings.SetRimTalkChannelConfig(_rimTalkEditorChannel, config);
            return created;
        }

        internal void SyncRimTalkEntryContentBuffer(RimTalkPromptEntryConfig entry)
        {
            string entryId = entry?.Id ?? string.Empty;
            string entryContent = entry?.Content ?? string.Empty;
            bool switchedEntry = !string.Equals(_rimTalkEntryContentBufferEntryId, entryId, StringComparison.Ordinal);
            bool externallyUpdated = !switchedEntry &&
                                     !string.Equals(_rimTalkEntryContentSnapshot, entryContent, StringComparison.Ordinal) &&
                                     !string.Equals(_rimTalkEntryContentBuffer, entryContent, StringComparison.Ordinal);
            if (switchedEntry || externallyUpdated)
            {
                _rimTalkEntryContentBufferEntryId = entryId;
                _rimTalkEntryContentBuffer = entryContent;
                if (switchedEntry)
                {
                    _rimTalkEntryContentScroll = Vector2.zero;
                }
            }

            _rimTalkEntryContentSnapshot = entryContent;
        }

        internal void ResetRimTalkEntryContentBuffer()
        {
            _rimTalkEntryContentBuffer = string.Empty;
            _rimTalkEntryContentBufferEntryId = string.Empty;
            _rimTalkEntryContentSnapshot = string.Empty;
            _rimTalkEntryContentScroll = Vector2.zero;
        }

        internal static string NextPromptEntryName(RimTalkChannelCompatConfig config, string baseName)
        {
            string stem = string.IsNullOrWhiteSpace(baseName)
                ? "RimChat_RimTalkEntryDefaultName".Translate().ToString()
                : baseName.Trim();
            int suffix = 2;
            string candidate = stem + " Copy";
            while (config?.PromptEntries?.Any(entry =>
                       entry != null && string.Equals(entry.Name, candidate, StringComparison.OrdinalIgnoreCase)) == true)
            {
                candidate = $"{stem} Copy {suffix}";
                suffix++;
            }

            return candidate;
        }

        internal void EnsureRimTalkEntrySelection(RimTalkChannelCompatConfig config)
        {
            config.NormalizeWith(RimTalkChannelCompatConfig.CreateDefault());
            if (config.PromptEntries == null || config.PromptEntries.Count == 0)
            {
                _rimTalkSelectedEntryId = string.Empty;
                _rimTalkDepthBuffer = string.Empty;
                return;
            }

            if (config.PromptEntries.Any(entry => string.Equals(entry?.Id, _rimTalkSelectedEntryId, StringComparison.Ordinal)))
            {
                return;
            }

            RimTalkPromptEntryConfig first = config.PromptEntries.FirstOrDefault(entry => entry != null);
            _rimTalkSelectedEntryId = first?.Id ?? string.Empty;
            _rimTalkDepthBuffer = first?.InChatDepth.ToString() ?? string.Empty;
        }

        internal RimTalkPromptEntryConfig GetSelectedRimTalkPromptEntry(RimTalkChannelCompatConfig config)
        {
            if (config?.PromptEntries == null)
            {
                return null;
            }

            EnsureRimTalkEntrySelection(config);
            return config.PromptEntries.FirstOrDefault(entry =>
                entry != null && string.Equals(entry.Id, _rimTalkSelectedEntryId, StringComparison.Ordinal));
        }

        internal void ShowRimTalkRoleMenu(RimTalkPromptChannel channel, string entryId)
        {
            List<FloatMenuOption> options = RimTalkEntryRoles
                .Select(role => new FloatMenuOption(GetRimTalkRoleLabel(role), () =>
                {
                    TryUpdatePromptEntryById(channel, entryId, selected => selected.Role = role);
                }))
                .ToList();
            Find.WindowStack.Add(new FloatMenu(options));
        }

        internal void ShowRimTalkPositionMenu(RimTalkPromptChannel channel, string entryId)
        {
            List<FloatMenuOption> options = RimTalkEntryPositions
                .Select(position => new FloatMenuOption(GetRimTalkPositionLabel(position), () =>
                {
                    TryUpdatePromptEntryById(channel, entryId, selected =>
                    {
                        selected.Position = position;
                        if (!string.Equals(position, "InChat", StringComparison.OrdinalIgnoreCase))
                        {
                            selected.InChatDepth = 0;
                        }
                    });
                }))
                .ToList();
            Find.WindowStack.Add(new FloatMenu(options));
        }

        internal void ShowRimTalkPromptChannelMenu(RimTalkPromptChannel channel, string entryId)
        {
            IReadOnlyList<string> selectableChannels = RimTalkPromptEntryChannelCatalog.GetSelectableChannels(channel);
            List<FloatMenuOption> options = selectableChannels
                .Select(channelId => new FloatMenuOption(
                    GetRimTalkPromptChannelLabel(channelId),
                    () =>
                {
                    TryUpdatePromptEntryById(channel, entryId, selected =>
                    {
                        selected.PromptChannel = channelId;
                    });
                }))
                .ToList();
            Find.WindowStack.Add(new FloatMenu(options));
        }

        internal bool TryUpdatePromptEntryById(
            RimTalkPromptChannel channel,
            string entryId,
            Action<RimTalkPromptEntryConfig> updateAction)
        {
            if (string.IsNullOrWhiteSpace(entryId))
            {
                return false;
            }

            RimTalkChannelCompatConfig config = Settings.GetRimTalkChannelConfigClone(channel);
            RimTalkPromptEntryConfig selected = config?.PromptEntries?.FirstOrDefault(entry =>
                entry != null && string.Equals(entry.Id, entryId, StringComparison.Ordinal));
            if (selected == null)
            {
                return false;
            }

            updateAction?.Invoke(selected);
            Settings.SetRimTalkChannelConfig(channel, config);

            if (channel == _rimTalkEditorChannel)
            {
                _rimTalkSelectedEntryId = selected.Id;
                _rimTalkDepthBuffer = selected.InChatDepth.ToString();
            }

            return true;
        }

        internal static bool IsDefaultPromptEntry(RimTalkPromptEntryConfig entry)
        {
            if (entry == null)
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(entry.SectionId);
        }

        internal bool TryRestoreDefaultEntriesForScopedChannel(RimTalkChannelCompatConfig config, string scopedPromptChannel)
        {
            if (config == null)
            {
                return false;
            }

            string normalizedPromptChannel = string.IsNullOrWhiteSpace(scopedPromptChannel)
                ? RimTalkPromptEntryChannelCatalog.GetDefaultChannel(_rimTalkEditorChannel)
                : RimTalkPromptEntryChannelCatalog.NormalizeForRoot(scopedPromptChannel, _rimTalkEditorChannel);
            config.PromptEntries ??= new List<RimTalkPromptEntryConfig>();

            List<RimTalkPromptEntryConfig> restored = RelationsSettingsPromptOps.BuildDefaultSectionEntriesForChannel(normalizedPromptChannel);
            if (restored == null || restored.Count == 0)
            {
                return false;
            }

            RelationsSettingsPromptOps.ReplacePromptChannelEntries(config.PromptEntries, normalizedPromptChannel, restored);
            RimTalkPromptEntryConfig first = restored[0];
            _rimTalkSelectedEntryId = first?.Id ?? string.Empty;
            _rimTalkDepthBuffer = first?.InChatDepth.ToString() ?? string.Empty;
            _rimTalkEntryListScroll = Vector2.zero;
            ResetRimTalkEntryContentBuffer();
            Messages.Message(
                "RimChat_RimTalkEntryRestoreDefaultsSuccess".Translate(GetRimTalkPromptChannelLabel(normalizedPromptChannel)),
                MessageTypeDefOf.NeutralEvent,
                false);
            return true;
        }

        internal static string GetRimTalkRoleLabel(string role)
        {
            if (string.Equals(role, "User", StringComparison.OrdinalIgnoreCase))
            {
                return "RimChat_RimTalkEntryRoleUser".Translate();
            }

            if (string.Equals(role, "Assistant", StringComparison.OrdinalIgnoreCase))
            {
                return "RimChat_RimTalkEntryRoleAssistant".Translate();
            }

            if (!string.IsNullOrWhiteSpace(role) &&
                !string.Equals(role, "System", StringComparison.OrdinalIgnoreCase))
            {
                return role.Trim();
            }

            return "RimChat_RimTalkEntryRoleSystem".Translate();
        }

        internal static string GetRimTalkPositionLabel(string position)
        {
            return string.Equals(position, "InChat", StringComparison.OrdinalIgnoreCase)
                ? "RimChat_RimTalkEntryPositionInChat".Translate()
                : "RimChat_RimTalkEntryPositionRelative".Translate();
        }

        internal static string GetRimTalkPromptChannelLabel(string channelId)
        {
            return RimTalkPromptEntryChannelCatalog.GetLabel(channelId);
        }











    
}
