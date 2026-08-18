using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Ustas.RimAI.Communication.Relations.Persistence;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Context;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.UI;
using Ustas.RimAI.Core.UI;

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class RelationsPromptEnvironmentEditors
{
    readonly RelationsPromptEnvironmentSceneEditors Scene;
    internal readonly RelationsSettingsPages Pages;

    internal RelationsPromptEnvironmentEditors(RelationsSettingsPages pages)
    {
        Pages = pages;
        Scene = new RelationsPromptEnvironmentSceneEditors(this);
    }

    internal RelationsSettings Settings => Pages.Settings;

        internal Vector2 _envPageScroll = Vector2.zero;
        internal Vector2 _envSceneListScroll = Vector2.zero;
        internal Vector2 _envSceneContentScroll = Vector2.zero;
        internal Vector2 _envPreviewScroll = Vector2.zero;
        internal int _selectedEnvironmentSceneIndex = -1;
        internal string _selectedEnvironmentSceneId = string.Empty;
        internal string _sceneTagsBuffer = string.Empty;
        internal string _scenePriorityBuffer = "0";
        internal string _environmentPreviewCache = string.Empty;
        internal int _environmentPreviewCooldown = 0;

        internal const float EnvCardGap = 10f;
        internal const float EnvSceneRowHeight = 46f;
        internal const float EnvSceneRowGap = 4f;
        internal static readonly Color EnvCardBg = new Color(0.08f, 0.08f, 0.10f);
        internal static readonly Color EnvSectionBg = new Color(0.10f, 0.11f, 0.14f);

        internal void DrawEnvironmentPromptsEditorScrollable(Rect rect)
        {
            EnsureEnvironmentPromptConfig();
            EnvironmentPromptConfig envConfig = Pages.PromptLegacy.SystemPromptConfigData.EnvironmentPrompt;

            Widgets.DrawBoxSolid(rect, EnvSectionBg);

            Rect innerRect = rect.ContractedBy(8f);
            float contentHeight = CalculateEnvironmentPageHeight(envConfig);
            Rect viewRect = new Rect(0f, 0f, innerRect.width - 18f, contentHeight);
            _envPageScroll = GUI.BeginScrollView(innerRect, _envPageScroll, viewRect);

            float y = 0f;
            bool changed = false;

            Rect worldviewRect = new Rect(0f, y, viewRect.width, GetWorldviewCardHeight(envConfig));
            changed |= DrawEnvironmentWorldviewCard(worldviewRect, envConfig);
            y += worldviewRect.height + EnvCardGap;

            Rect sceneSystemRect = new Rect(0f, y, viewRect.width, GetSceneSystemCardHeight(envConfig));
            changed |= DrawEnvironmentSceneSystemCard(sceneSystemRect, envConfig);
            y += sceneSystemRect.height + EnvCardGap;

            Rect environmentParamsRect = new Rect(0f, y, viewRect.width, GetEnvironmentContextCardHeight(envConfig));
            changed |= DrawEnvironmentContextCard(environmentParamsRect, envConfig);
            y += environmentParamsRect.height + EnvCardGap;

            Rect eventIntelRect = new Rect(0f, y, viewRect.width, GetEventIntelCardHeight(envConfig));
            changed |= DrawEnvironmentEventIntelCard(eventIntelRect, envConfig);
            y += eventIntelRect.height + EnvCardGap;

            Rect sceneEntriesRect = new Rect(0f, y, viewRect.width, 390f);
            changed |= DrawEnvironmentSceneEntriesCard(sceneEntriesRect, envConfig);
            y += sceneEntriesRect.height + EnvCardGap;

            Rect rpgSwitchesRect = new Rect(0f, y, viewRect.width, GetRpgSwitchesCardHeight());
            changed |= DrawEnvironmentRpgSwitchesCard(rpgSwitchesRect, envConfig);
            y += rpgSwitchesRect.height + EnvCardGap;

            Rect previewRect = new Rect(0f, y, viewRect.width, 260f);
            DrawEnvironmentPreviewCard(previewRect);

            GUI.EndScrollView();

            if (changed)
            {
                _environmentPreviewCooldown = 0;
            }
        }

        internal float CalculateEnvironmentPageHeight(EnvironmentPromptConfig config)
        {
            return GetWorldviewCardHeight(config)
                + GetSceneSystemCardHeight(config)
                + GetEnvironmentContextCardHeight(config)
                + GetEventIntelCardHeight(config)
                + 390f
                + GetRpgSwitchesCardHeight()
                + 260f
                + EnvCardGap * 7f;
        }

        internal float GetWorldviewCardHeight(EnvironmentPromptConfig config)
        {
            return config.Worldview.Enabled ? 198f : 98f;
        }

        internal float GetSceneSystemCardHeight(EnvironmentPromptConfig config)
        {
            return config.SceneSystem.Enabled ? 172f : 98f;
        }

        internal float GetEnvironmentContextCardHeight(EnvironmentPromptConfig config)
        {
            return config.EnvironmentContextSwitches.Enabled ? 228f : 98f;
        }

        internal float GetEventIntelCardHeight(EnvironmentPromptConfig config)
        {
            return config.EventIntelPrompt?.Enabled == true ? 320f : 100f;
        }

        internal float GetRpgSwitchesCardHeight()
        {
            return 236f;
        }

        internal Rect DrawEnvironmentCard(Rect rect, string titleKey)
        {
            Widgets.DrawBoxSolid(rect, EnvCardBg);
            GUI.color = new Color(0.95f, 0.78f, 0.45f);
            Widgets.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, 22f), titleKey.Translate());
            GUI.color = Color.white;
            Widgets.DrawLineHorizontal(rect.x + 8f, rect.y + 28f, rect.width - 16f);
            return new Rect(rect.x + 8f, rect.y + 34f, rect.width - 16f, rect.height - 40f);
        }

        internal void EnsureEnvironmentPromptConfig()
        {
            if (Pages.PromptLegacy.SystemPromptConfigData.EnvironmentPrompt == null)
            {
                Pages.PromptLegacy.SystemPromptConfigData.EnvironmentPrompt = new EnvironmentPromptConfig();
            }

            if (Pages.PromptLegacy.SystemPromptConfigData.EnvironmentPrompt.Worldview == null)
            {
                Pages.PromptLegacy.SystemPromptConfigData.EnvironmentPrompt.Worldview = new WorldviewPromptConfig();
            }

            if (Pages.PromptLegacy.SystemPromptConfigData.EnvironmentPrompt.SceneSystem == null)
            {
                Pages.PromptLegacy.SystemPromptConfigData.EnvironmentPrompt.SceneSystem = new SceneSystemPromptConfig();
            }

            if (Pages.PromptLegacy.SystemPromptConfigData.EnvironmentPrompt.SceneEntries == null)
            {
                Pages.PromptLegacy.SystemPromptConfigData.EnvironmentPrompt.SceneEntries = new List<ScenePromptEntryConfig>();
            }

            if (Pages.PromptLegacy.SystemPromptConfigData.EnvironmentPrompt.EnvironmentContextSwitches == null)
            {
                Pages.PromptLegacy.SystemPromptConfigData.EnvironmentPrompt.EnvironmentContextSwitches = new EnvironmentContextSwitchesConfig();
            }

            if (Pages.PromptLegacy.SystemPromptConfigData.EnvironmentPrompt.RpgSceneParamSwitches == null)
            {
                Pages.PromptLegacy.SystemPromptConfigData.EnvironmentPrompt.RpgSceneParamSwitches = new RpgSceneParamSwitchesConfig();
            }

            if (Pages.PromptLegacy.SystemPromptConfigData.EnvironmentPrompt.EventIntelPrompt == null)
            {
                Pages.PromptLegacy.SystemPromptConfigData.EnvironmentPrompt.EventIntelPrompt = new EventIntelPromptConfig();
            }
        }

        internal bool DrawEnvironmentWorldviewCard(Rect rect, EnvironmentPromptConfig envConfig)

        {

            return Scene.DrawEnvironmentWorldviewCard(rect, envConfig);

        }


        internal bool DrawEnvironmentSceneSystemCard(Rect rect, EnvironmentPromptConfig envConfig)

        {

            return Scene.DrawEnvironmentSceneSystemCard(rect, envConfig);

        }


        internal bool DrawEnvironmentIntSlider(
            Rect contentRect,
            ref float y,
            ref int value,
            int min,
            int max,
            string labelKey)
        {
            Widgets.Label(
                new Rect(contentRect.x, y, contentRect.width, 20f),
                labelKey.Translate(value));
            y += 20f;

            int oldValue = value;
            value = Mathf.RoundToInt(Widgets.HorizontalSlider(
                new Rect(contentRect.x, y, contentRect.width, 22f),
                value,
                min,
                max));
            y += 28f;
            return oldValue != value;
        }

        internal bool DrawEnvironmentContextCard(Rect rect, EnvironmentPromptConfig envConfig)
        {
            Rect contentRect = DrawEnvironmentCard(rect, "RimChat_EnvironmentContextLabel");
            bool changed = false;
            EnvironmentContextSwitchesConfig switches = envConfig.EnvironmentContextSwitches;

            bool oldEnabled = switches.Enabled;
            Widgets.CheckboxLabeled(
                new Rect(contentRect.x, contentRect.y, contentRect.width, 24f),
                "RimChat_EnvironmentContextEnabled".Translate(),
                ref switches.Enabled);
            changed |= oldEnabled != switches.Enabled;

            if (!switches.Enabled)
            {
                return changed;
            }

            float colGap = 16f;
            float colWidth = (contentRect.width - colGap) / 2f;
            Rect leftCol = new Rect(contentRect.x, contentRect.y + 30f, colWidth, contentRect.height - 32f);
            Rect rightCol = new Rect(leftCol.xMax + colGap, leftCol.y, colWidth, leftCol.height);

            changed |= DrawEnvironmentContextLeftColumn(leftCol, switches);
            changed |= DrawEnvironmentContextRightColumn(rightCol, switches);
            return changed;
        }

        internal bool DrawEnvironmentContextLeftColumn(Rect rect, EnvironmentContextSwitchesConfig switches)
        {
            bool changed = false;
            float y = rect.y;
            changed |= DrawEnvironmentCheckbox(new Rect(rect.x, y, rect.width, 24f), "RimChat_EnvironmentContextTime", ref switches.IncludeTime); y += 24f;
            changed |= DrawEnvironmentCheckbox(new Rect(rect.x, y, rect.width, 24f), "RimChat_EnvironmentContextDate", ref switches.IncludeDate); y += 24f;
            changed |= DrawEnvironmentCheckbox(new Rect(rect.x, y, rect.width, 24f), "RimChat_EnvironmentContextSeason", ref switches.IncludeSeason); y += 24f;
            changed |= DrawEnvironmentCheckbox(new Rect(rect.x, y, rect.width, 24f), "RimChat_EnvironmentContextWeather", ref switches.IncludeWeather); y += 24f;
            changed |= DrawEnvironmentCheckbox(new Rect(rect.x, y, rect.width, 24f), "RimChat_EnvironmentContextLocationTemperature", ref switches.IncludeLocationAndTemperature);
            return changed;
        }

        internal bool DrawEnvironmentContextRightColumn(Rect rect, EnvironmentContextSwitchesConfig switches)
        {
            bool changed = false;
            float y = rect.y;
            changed |= DrawEnvironmentCheckbox(new Rect(rect.x, y, rect.width, 24f), "RimChat_EnvironmentContextTerrain", ref switches.IncludeTerrain); y += 24f;
            changed |= DrawEnvironmentCheckbox(new Rect(rect.x, y, rect.width, 24f), "RimChat_EnvironmentContextBeauty", ref switches.IncludeBeauty); y += 24f;
            changed |= DrawEnvironmentCheckbox(new Rect(rect.x, y, rect.width, 24f), "RimChat_EnvironmentContextCleanliness", ref switches.IncludeCleanliness); y += 24f;
            changed |= DrawEnvironmentCheckbox(new Rect(rect.x, y, rect.width, 24f), "RimChat_EnvironmentContextSurroundings", ref switches.IncludeSurroundings); y += 24f;
            changed |= DrawEnvironmentCheckbox(new Rect(rect.x, y, rect.width, 24f), "RimChat_EnvironmentContextWealth", ref switches.IncludeWealth);
            return changed;
        }

        internal bool DrawEnvironmentCheckbox(Rect rect, string key, ref bool value)
        {
            bool oldValue = value;
            Widgets.CheckboxLabeled(rect, key.Translate(), ref value);
            return oldValue != value;
        }

        internal bool DrawEnvironmentSceneEntriesCard(Rect rect, EnvironmentPromptConfig envConfig)

        {

            return Scene.DrawEnvironmentSceneEntriesCard(rect, envConfig);

        }


        internal bool DrawEnvironmentEventIntelCard(Rect rect, EnvironmentPromptConfig envConfig)

        {

            return Scene.DrawEnvironmentEventIntelCard(rect, envConfig);

        }


        internal bool DrawEnvironmentSceneList(Rect rect, EnvironmentPromptConfig envConfig)

        {

            return Scene.DrawEnvironmentSceneList(rect, envConfig);

        }


        internal void DrawEnvironmentSceneRow(Rect rowRect, ScenePromptEntryConfig entry, bool selected)

        {

            Scene.DrawEnvironmentSceneRow(rowRect, entry, selected);

        }


        internal string BuildEnvironmentSceneMeta(ScenePromptEntryConfig entry)

        {

            return Scene.BuildEnvironmentSceneMeta(entry);

        }


        internal bool DrawEnvironmentSceneEditor(Rect rect, EnvironmentPromptConfig envConfig)

        {

            return Scene.DrawEnvironmentSceneEditor(rect, envConfig);

        }


        internal bool DrawEnvironmentRpgSwitchesCard(Rect rect, EnvironmentPromptConfig envConfig)

        {

            return Scene.DrawEnvironmentRpgSwitchesCard(rect, envConfig);

        }


        internal void DrawEnvironmentPreviewCard(Rect rect)
        {
            Rect contentRect = DrawEnvironmentCard(rect, "RimChat_EnvironmentPreviewTitle");
            Rect refreshRect = new Rect(contentRect.xMax - 136f, contentRect.y, 136f, 24f);
            if (Widgets.ButtonText(refreshRect, "RimChat_EnvironmentPreviewRefresh".Translate()))
            {
                _environmentPreviewCooldown = 0;
            }

            if (--_environmentPreviewCooldown <= 0)
            {
                _environmentPreviewCache = BuildEnvironmentPreviewText();
                _environmentPreviewCooldown = 60;
            }

            Rect textRect = new Rect(contentRect.x, contentRect.y + 28f, contentRect.width, contentRect.height - 30f);
            float contentHeight = Mathf.Max(textRect.height, Text.CalcHeight(_environmentPreviewCache ?? string.Empty, textRect.width - 16f) + 10f);
            Rect viewRect = new Rect(0f, 0f, textRect.width - 16f, contentHeight);
            _envPreviewScroll = GUI.BeginScrollView(textRect, _envPreviewScroll, viewRect);
            GUI.color = new Color(0.78f, 0.82f, 0.88f);
            Widgets.Label(viewRect, _environmentPreviewCache ?? string.Empty);
            GUI.color = Color.white;
            GUI.EndScrollView();
        }

        internal string BuildEnvironmentPreviewText()
        {
            try
            {
                var sb = new StringBuilder();
                var service = PromptPersistenceService.Instance;
                var config = Pages.PromptLegacy.SystemPromptConfigData;

                Faction sampleFaction = Find.FactionManager?.AllFactionsVisible?.FirstOrDefault(f => f != null && !f.IsPlayer);
                if (sampleFaction != null)
                {
                    var diplomacyContext = DialogueScenarioContext.CreateDiplomacy(sampleFaction, false, new[] { "scene:social" });
                    sb.AppendLine("=== Diplomacy Preview ===");
                    sb.AppendLine(service.BuildEnvironmentPromptBlocks(config, diplomacyContext));
                    sb.AppendLine();
                }

                Pawn first = PawnsFinder.AllMapsWorldAndTemporary_Alive.FirstOrDefault(p => p?.RaceProps?.Humanlike == true);
                Pawn second = PawnsFinder.AllMapsWorldAndTemporary_Alive.FirstOrDefault(p => p?.RaceProps?.Humanlike == true && p != first);
                if (first != null && second != null)
                {
                    var rpgContext = DialogueScenarioContext.CreateRpg(first, second, false, new[] { "scene:daily" });
                    sb.AppendLine("=== RPG Preview ===");
                    sb.AppendLine(service.BuildEnvironmentPromptBlocks(config, rpgContext));
                }

                if (sb.Length == 0)
                {
                    sb.AppendLine("RimChat_EnvironmentPreviewNoContext".Translate());
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Preview Settings.Error: {ex.Message}";
            }
        }

        internal bool TryAppendVariableToSelectedEnvironmentScene(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            EnsureEnvironmentPromptConfig();
            List<ScenePromptEntryConfig> entries = Pages.PromptLegacy.SystemPromptConfigData.EnvironmentPrompt?.SceneEntries;
            if (entries == null || _selectedEnvironmentSceneIndex < 0 || _selectedEnvironmentSceneIndex >= entries.Count)
            {
                return false;
            }

            ScenePromptEntryConfig entry = entries[_selectedEnvironmentSceneIndex];
            entry.Content = (entry.Content ?? string.Empty) + token;
            _environmentPreviewCooldown = 0;
            Pages.PromptLegacy._previewUpdateCooldown = 0;
            return true;
        }

        internal string GetSelectedEnvironmentSceneContent()
        {
            EnsureEnvironmentPromptConfig();
            List<ScenePromptEntryConfig> entries = Pages.PromptLegacy.SystemPromptConfigData.EnvironmentPrompt?.SceneEntries;
            if (entries == null || _selectedEnvironmentSceneIndex < 0 || _selectedEnvironmentSceneIndex >= entries.Count)
            {
                return string.Empty;
            }

            return entries[_selectedEnvironmentSceneIndex]?.Content ?? string.Empty;
        }

        internal List<string> ParseTagCsv(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv))
            {
                return new List<string>();
            }

            return csv
                .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(tag => tag.Trim().ToLowerInvariant())
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct()
                .ToList();
        }

        internal void SyncEnvironmentSelection(EnvironmentPromptConfig envConfig)
        {
            if (_selectedEnvironmentSceneIndex < 0 || _selectedEnvironmentSceneIndex >= envConfig.SceneEntries.Count)
            {
                _selectedEnvironmentSceneId = string.Empty;
                _sceneTagsBuffer = string.Empty;
                _scenePriorityBuffer = "0";
                return;
            }

            ScenePromptEntryConfig entry = envConfig.SceneEntries[_selectedEnvironmentSceneIndex];
            _selectedEnvironmentSceneId = entry?.Id ?? string.Empty;
            _sceneTagsBuffer = entry?.MatchTags != null ? string.Join(", ", entry.MatchTags) : string.Empty;
            _scenePriorityBuffer = (entry?.Priority ?? 0).ToString();
        }
    
}
