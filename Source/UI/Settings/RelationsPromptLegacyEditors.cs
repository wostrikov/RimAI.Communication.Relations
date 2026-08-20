using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.UI;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.Context;
using Ustas.RimAI.Communication.Relations.Prompting.Transfer;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Core.UI;

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class RelationsPromptLegacyEditors
{
    readonly RelationsPromptLegacyEditorChrome Chrome;
    internal readonly RelationsSettingsPages Pages;

    internal RelationsPromptLegacyEditors(RelationsSettingsPages pages)
    {
        Pages = pages;
        Chrome = new RelationsPromptLegacyEditorChrome(this);
    }

    internal RelationsSettings Settings => Pages.Settings;

        internal SystemPromptConfig _systemPromptConfig;
        internal bool _advancedPromptMode = false;
        internal bool _promptWorkbenchFailed;

        internal int _selectedSectionIndex = 0;
        internal int _selectedApiActionIndex = -1;
        internal int _selectedDecisionRuleIndex = -1;
        internal int _selectedFactionPromptIndex = -1;

        internal string _editingApiActionName = "";
        internal string _editingApiActionDesc = "";
        internal string _editingApiActionParams = "";
        internal string _editingApiActionReq = "";
        internal string _editingRuleName = "";
        internal string _editingRuleContent = "";

        internal string _globalPromptBuffer = "";
        internal string _globalDialoguePromptBuffer = "";
        internal string _jsonTemplateBuffer = "";
        internal string _importantRulesBuffer = "";

        internal Vector2 _globalPromptScroll = Vector2.zero;
        internal Vector2 _globalDialoguePromptScroll = Vector2.zero;
        internal Vector2 _navigationSectionScroll = Vector2.zero;
        internal Vector2 _apiActionListScroll = Vector2.zero;
        internal Vector2 _apiActionDescScroll = Vector2.zero;
        internal Vector2 _jsonTemplateScroll = Vector2.zero;
        internal Vector2 _importantRulesScroll = Vector2.zero;
        internal Vector2 _ruleContentScroll = Vector2.zero;
        internal Vector2 _previewScroll = Vector2.zero;
        internal Vector2 _factionPromptScroll = Vector2.zero;

        internal string _cachedPreviewText = "";
        internal int _previewUpdateCooldown = 0;
        internal bool _previewCollapsed = true;
        internal float _previewFoldAnimTime = 0f;
        internal TemplateVariableValidationResult _liveValidationResult = new TemplateVariableValidationResult();
        internal string _liveValidationSignature = string.Empty;
        internal int _liveValidationCooldown = 0;
        internal const int LiveValidationRefreshTicks = 15;

        internal static readonly Color SectionHeaderColor = new Color(0.9f, 0.7f, 0.4f);

        internal static readonly string[] SimpleSectionNames = new string[]
        {
            "GlobalPrompt",
            "EnvironmentPrompts",
            "SocialCirclePrompts",
            "DynamicData"
        };

        internal static readonly string[] AdvancedSectionNames = new string[]
        {
            "GlobalPrompt",
            "EnvironmentPrompts",
            "FactionPrompts",
            "ApiActions",
            "JsonTemplate",
            "ImportantRules",
            "SocialCirclePrompts",
            "DecisionRules",
            "DynamicData"
        };

        public SystemPromptConfig SystemPromptConfigData
        {
            get
            {
                if (_systemPromptConfig == null)
                {
                    _systemPromptConfig = PromptPersistenceService.Instance.LoadConfigReadOnly();
                }
                return _systemPromptConfig;
            }
        }

        internal void DrawAdvancedPromptSettingsSection(Listing_Standard listing)
        {
            if (_promptWorkbenchFailed)
            {
                DrawLegacyAdvancedPromptSettingsSection(listing);
                return;
            }

            try
            {
                Pages.PromptWorkbench.DrawAdvancedPromptWorkbench(listing);
            }
            catch (Exception ex)
            {
                _promptWorkbenchFailed = true;
                Log.Error($"[RimAI.Relations] Prompt workbench render failed, fallback to legacy prompt UI: {ex}");
                DrawLegacyAdvancedPromptSettingsSection(listing);
            }
        }

        internal void DrawLegacyAdvancedPromptSettingsSection(Listing_Standard listing)
        {
            float totalHeight = 520f;
            Rect mainRect = listing.GetRect(totalHeight);

            InitBuffers();

            float navWidth = mainRect.width / 3.5f;
            float editorWidth = mainRect.width - navWidth - 10f;

            Rect navRect = new Rect(mainRect.x, mainRect.y, navWidth, totalHeight);
            Rect editorRect = new Rect(mainRect.x + navWidth + 10f, mainRect.y, editorWidth, totalHeight);

            DrawNavigationPanelWithButtons(navRect);
            DrawEditorPanelWithPreview(editorRect);
        }

        internal void DrawLegacyPromptPageDirect(Rect rect)
        {
            float totalHeight = Mathf.Min(620f, rect.height);
            Rect mainRect = new Rect(rect.x, rect.y, rect.width, totalHeight);

            InitBuffers();

            float navWidth = mainRect.width / 3.5f;
            float editorWidth = mainRect.width - navWidth - 10f;
            Rect navRect = new Rect(mainRect.x, mainRect.y, navWidth, totalHeight);
            Rect editorRect = new Rect(mainRect.x + navWidth + 10f, mainRect.y, editorWidth, totalHeight);
            DrawNavigationPanelWithButtons(navRect);
            DrawEditorPanelWithPreview(editorRect);
        }

        internal void InitBuffers()
        {
            if (string.IsNullOrEmpty(_globalPromptBuffer))
                _globalPromptBuffer = SystemPromptConfigData.GlobalSystemPrompt ?? "";
            if (string.IsNullOrEmpty(_globalDialoguePromptBuffer))
                _globalDialoguePromptBuffer = SystemPromptConfigData.GlobalDialoguePrompt ?? "";
            if (string.IsNullOrEmpty(_jsonTemplateBuffer))
                _jsonTemplateBuffer = SystemPromptConfigData.ResponseFormat?.JsonTemplate ?? "";
            if (string.IsNullOrEmpty(_importantRulesBuffer))
                _importantRulesBuffer = SystemPromptConfigData.ResponseFormat?.ImportantRules ?? "";
        }

        internal void DrawNavigationPanelWithButtons(Rect rect)
        {
            Chrome.DrawNavigationPanelWithButtons(rect);
        }


        internal void DrawPromptActionButtonsVertical(Rect rect)
        {
            Chrome.DrawPromptActionButtonsVertical(rect);
        }


        internal void DrawModeToggleSmall(Rect rect)
        {
            Chrome.DrawModeToggleSmall(rect);
        }


        internal void DrawEditorPanelWithPreview(Rect rect)
        {
            Chrome.DrawEditorPanelWithPreview(rect);
        }


        internal void DrawGlobalPromptEditorScrollable(Rect rect)
        {
            float labelHeight = 20f;
            float gap = 8f;
            float available = rect.height - labelHeight - (gap * 2f);
            float editorHeight = Mathf.Max(140f, available);
            float y = rect.y;

            Rect systemLabelRect = new Rect(rect.x, y, rect.width, labelHeight);
            Widgets.Label(systemLabelRect, "RimChat_GlobalSystemPromptSection".Translate());
            Pages.Tooltips.Register(systemLabelRect, "RimChat_GlobalSystemPromptSectionTooltip");
            y += labelHeight + 2f;
            DrawGlobalPromptTextArea(new Rect(rect.x, y, rect.width - 16f, editorHeight), ref _globalPromptBuffer, ref _globalPromptScroll, "GlobalPromptTextArea");

            SystemPromptConfigData.GlobalSystemPrompt = _globalPromptBuffer;
        }

        internal static void DrawGlobalPromptTextArea(Rect textRect, ref string buffer, ref Vector2 scroll, string controlName)
        {
            float contentHeight = Mathf.Max(textRect.height, Text.CalcHeight(buffer ?? string.Empty, textRect.width - 16f) + 10f);
            Rect viewRect = new Rect(0f, 0f, textRect.width - 16f, contentHeight);
            scroll = GUI.BeginScrollView(textRect, scroll, viewRect);
            GUI.SetNextControlName(controlName);
            buffer = GUI.TextArea(viewRect, buffer ?? string.Empty);
            GUI.EndScrollView();
        }


        internal void DrawResponseFormatEditorScrollable(Rect rect)
        {
            var format = SystemPromptConfigData.ResponseFormat;
            if (format == null)
            {
                format = new ResponseFormatConfig();
                SystemPromptConfigData.ResponseFormat = format;
            }

            float y = rect.y;

            Widgets.Label(new Rect(rect.x, y, rect.width, 20f), "RimChat_JsonTemplateLabel".Translate());
            y += 22f;

            float textHeight = rect.yMax - y - 29f;
            Rect textRect = new Rect(rect.x, y, rect.width - 16f, textHeight);

            float contentHeight = Mathf.Max(textRect.height, Text.CalcHeight(_jsonTemplateBuffer, textRect.width - 16f) + 10f);
            Rect viewRect = new Rect(0f, 0f, textRect.width - 16f, contentHeight);
            _jsonTemplateScroll = GUI.BeginScrollView(textRect, _jsonTemplateScroll, viewRect);

            _jsonTemplateBuffer = GUI.TextArea(viewRect, _jsonTemplateBuffer);

            GUI.EndScrollView();
            format.JsonTemplate = _jsonTemplateBuffer;
        }

        internal void DrawJsonTemplateEditorScrollable(Rect rect)
        {
            var format = SystemPromptConfigData.ResponseFormat;
            if (format == null)
            {
                format = new ResponseFormatConfig();
                SystemPromptConfigData.ResponseFormat = format;
            }

            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 20f), "RimChat_JsonTemplateLabel".Translate());

            float textY = rect.y + 22f;
            float textHeight = rect.yMax - textY;
            Rect textRect = new Rect(rect.x, textY, rect.width, textHeight);

            float contentHeight = Text.CalcHeight(_jsonTemplateBuffer, textRect.width - 20f);
            contentHeight = Mathf.Max(contentHeight, textRect.height);

            Rect viewRect = new Rect(0f, 0f, textRect.width - 20f, contentHeight);
            _jsonTemplateScroll = GUI.BeginScrollView(textRect, _jsonTemplateScroll, viewRect);

            _jsonTemplateBuffer = GUI.TextArea(viewRect, _jsonTemplateBuffer);

            GUI.EndScrollView();
            format.JsonTemplate = _jsonTemplateBuffer;
        }

        internal void DrawImportantRulesEditorScrollable(Rect rect)
        {
            var format = SystemPromptConfigData.ResponseFormat;
            if (format == null)
            {
                format = new ResponseFormatConfig();
                SystemPromptConfigData.ResponseFormat = format;
            }

            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 20f), "RimChat_ImportantRulesLabel".Translate());

            float textY = rect.y + 22f;
            float textHeight = rect.yMax - textY;
            Rect textRect = new Rect(rect.x, textY, rect.width, textHeight);

            float contentHeight = Text.CalcHeight(_importantRulesBuffer, textRect.width - 20f);
            contentHeight = Mathf.Max(contentHeight, textRect.height);

            Rect viewRect = new Rect(0f, 0f, textRect.width - 20f, contentHeight);
            _importantRulesScroll = GUI.BeginScrollView(textRect, _importantRulesScroll, viewRect);

            _importantRulesBuffer = GUI.TextArea(viewRect, _importantRulesBuffer);

            GUI.EndScrollView();
            format.ImportantRules = _importantRulesBuffer;
        }


        internal void DrawDynamicDataEditor(Rect rect)
        {
            Chrome.DrawDynamicDataEditor(rect);
        }


        internal static float DrawCompressionSlider(
            Rect rootRect,
            float y,
            string label,
            ref int value,
            int min,
            int max)
        {
            Rect labelRect = new Rect(rootRect.x, y, rootRect.width, 22f);
            Widgets.Label(labelRect, label);
            y += 20f;

            Rect sliderRect = new Rect(rootRect.x, y, rootRect.width, 20f);
            value = (int)Widgets.HorizontalSlider(sliderRect, value, min, max);
            return y + 24f;
        }


        internal static List<string> ParseSceneTagsCsv(string csv)
        {
            return RelationsPromptLegacyEditorChrome.ParseSceneTagsCsv(csv);
        }


    
}
