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
    internal readonly RelationsSettingsPages Pages;

    internal RelationsPromptLegacyEditors(RelationsSettingsPages pages)
    {
        Pages = pages;
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

        // 鏂囨湰缂撳啿鍖?
        internal string _globalPromptBuffer = "";
        internal string _globalDialoguePromptBuffer = "";
        internal string _jsonTemplateBuffer = "";
        internal string _importantRulesBuffer = "";

        // 婊氬姩浣嶇疆
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

        // 鍒嗗尯瀹氫箟 - 绠€鍗曟ā寮忓拰楂樼骇妯″紡鍏辩敤
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
            // 鑳屾櫙
            Widgets.DrawBoxSolid(rect, new Color(0.12f, 0.12f, 0.14f));


            Rect innerRect = rect.ContractedBy(8f);
            float y = innerRect.y;

            // 妯″紡鍒囨崲灏忔寜閽紙鏀惧湪宸︿笂瑙掞級
            Rect toggleRect = new Rect(innerRect.x, y, innerRect.width, 24f);
            DrawModeToggleSmall(toggleRect);
            y += 30f;

            // 鍒嗛殧绾?
            Widgets.DrawLineHorizontal(innerRect.x, y, innerRect.width);
            y += 10f;

            // 鏍规嵁妯″紡鑾峰彇鍒嗗尯鍒楄〃
            string[] sections = _advancedPromptMode ? AdvancedSectionNames : SimpleSectionNames;

            // 璁＄畻鍒嗗尯鍒楄〃鍖哄煙楂樺害锛堥鐣欐寜閽尯鍩燂級
            float buttonAreaHeight = 210f;
            float listHeight = innerRect.height - y - buttonAreaHeight;

            // 鍒嗗尯鍒楄〃鍖哄煙锛堝甫婊氬姩锛?
            Rect listRect = new Rect(innerRect.x, y, innerRect.width, listHeight);
            
            // 璁＄畻鍐呭楂樺害
            float contentHeight = sections.Length * 32f;
            Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, Mathf.Max(contentHeight, listHeight));
            
            // 浣跨敤鐙珛鐨勬粴鍔ㄤ綅缃?
            _navigationSectionScroll = GUI.BeginScrollView(listRect, _navigationSectionScroll, viewRect);
            
            // 缁樺埗鍒嗗尯鎸夐挳
            for (int i = 0; i < sections.Length; i++)
            {
                string sectionName = sections[i];
                bool isSelected = _selectedSectionIndex == i;

                Rect btnRect = new Rect(0f, i * 32f, viewRect.width, 28f);

                // 閫変腑鐘舵€佽儗鏅?
                if (isSelected)
                {
                    Widgets.DrawBoxSolid(btnRect, new Color(0.25f, 0.35f, 0.55f));
                }
                else if (Mouse.IsOver(btnRect))
                {
                    Widgets.DrawBoxSolid(btnRect, new Color(0.2f, 0.22f, 0.28f));
                }

                // 宸﹁竟妗嗗己璋?
                if (isSelected)
                {
                    Rect accentRect = new Rect(btnRect.x, btnRect.y, 3f, btnRect.height);
                    Widgets.DrawBoxSolid(accentRect, new Color(0.4f, 0.7f, 1f));
                }

                // 鏂囧瓧
                GUI.color = isSelected ? Color.white : new Color(0.7f, 0.7f, 0.75f);
                TextAnchor oldAnchor = Text.Anchor;
                Text.Anchor = TextAnchor.MiddleLeft;
                string label = Pages.PromptLegacyPreview.GetSectionLabel(sectionName);
                Widgets.Label(new Rect(btnRect.x + 8f, btnRect.y, btnRect.width - 16f, btnRect.height), label);
                Text.Anchor = oldAnchor;
                GUI.color = Color.white;
                Pages.Tooltips.Register(btnRect, RelationsSettingsTooltips.GetPromptSectionTooltipKey(sectionName));

                // 鐐瑰嚮澶勭悊
                if (Widgets.ButtonInvisible(btnRect))
                {
                    _selectedSectionIndex = i;
                    _selectedApiActionIndex = -1;
                    _selectedDecisionRuleIndex = -1;
                }
            }
            
            GUI.EndScrollView();

            // 鎸夐挳鍖哄煙锛堝湪瀵艰埅鏍忓簳閮級
            y += listHeight + 10f;
            Rect buttonAreaRect = new Rect(innerRect.x, y, innerRect.width, buttonAreaHeight - 10f);
            
            // 鍒嗛殧绾?
            Widgets.DrawLineHorizontal(innerRect.x, y - 5f, innerRect.width);
            
            // 缁樺埗鎸夐挳
            DrawPromptActionButtonsVertical(buttonAreaRect);
        }

        internal void DrawPromptActionButtonsVertical(Rect rect)
        {
            float btnHeight = 26f;
            float gap = 6f;
            float y = rect.y;

            Rect saveRect = new Rect(rect.x, y, rect.width, btnHeight);
            if (Widgets.ButtonText(saveRect, "RimChat_SavePrompt".Translate()))
            {
                Pages.PromptLegacyIo.SaveSystemPromptConfig();
                Messages.Message("RimChat_PromptSaved".Translate(), MessageTypeDefOf.NeutralEvent, false);
            }
            y += btnHeight + gap;

            Rect resetRect = new Rect(rect.x, y, rect.width, btnHeight);
            if (Widgets.ButtonText(resetRect, "RimChat_ResetToDefault".Translate()))
            {
                Pages.PromptLegacyIo.ShowResetPromptConfigConfirmation();
            }
            y += btnHeight + gap;

            Rect exportRect = new Rect(rect.x, y, rect.width, btnHeight);
            if (Widgets.ButtonText(exportRect, "RimChat_ExportPrompts".Translate()))
            {
                Pages.PromptLegacyIo.ShowExportSystemPromptDialog();
            }
            y += btnHeight + gap;

            Rect importRect = new Rect(rect.x, y, rect.width, btnHeight);
            if (Widgets.ButtonText(importRect, "RimChat_ImportPrompts".Translate()))
            {
                Pages.PromptLegacyIo.ShowImportSystemPromptDialog();
            }
        }

        internal void DrawModeToggleSmall(Rect rect)
        {
            float btnWidth = rect.width / 2 - 2f;

            // 绠€鍗曟ā寮忔寜閽?
            Rect simpleRect = new Rect(rect.x, rect.y, btnWidth, rect.height);
            bool isSimple = !_advancedPromptMode;

            GUI.color = isSimple ? new Color(0.3f, 0.6f, 0.9f) : new Color(0.18f, 0.18f, 0.2f);
            Widgets.DrawBoxSolid(simpleRect, GUI.color);
            GUI.color = isSimple ? Color.white : Color.gray;
            TextAnchor oldAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(simpleRect, "RimChat_SimpleModeShort".Translate());
            Text.Anchor = oldAnchor;
            GUI.color = Color.white;

            if (Widgets.ButtonInvisible(simpleRect))
            {
                _advancedPromptMode = false;
                _selectedSectionIndex = 0;
                Pages.PromptLegacyPreview.SyncBuffersToData();
            }

            // 楂樼骇妯″紡鎸夐挳
            Rect advancedRect = new Rect(rect.x + btnWidth + 4f, rect.y, btnWidth, rect.height);
            bool isAdvanced = _advancedPromptMode;

            GUI.color = isAdvanced ? new Color(0.9f, 0.5f, 0.25f) : new Color(0.18f, 0.18f, 0.2f);
            Widgets.DrawBoxSolid(advancedRect, GUI.color);
            GUI.color = isAdvanced ? Color.white : Color.gray;
            TextAnchor oldAnchor2 = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(advancedRect, "RimChat_AdvancedModeShort".Translate());
            Text.Anchor = oldAnchor2;
            GUI.color = Color.white;

            if (Widgets.ButtonInvisible(advancedRect))
            {
                _advancedPromptMode = true;
                _selectedSectionIndex = 0;
                Pages.PromptLegacyPreview.SyncBuffersToData();
            }
        }

        internal void DrawEditorPanelWithPreview(Rect rect)
        {
            // 鑳屾櫙
            Widgets.DrawBoxSolid(rect, new Color(0.1f, 0.1f, 0.12f));


            Rect innerRect = rect.ContractedBy(10f);

            // 鑾峰彇褰撳墠鍒嗗尯
            string[] sections = _advancedPromptMode ? AdvancedSectionNames : SimpleSectionNames;
            if (_selectedSectionIndex >= sections.Length)
                _selectedSectionIndex = 0;

            string currentSection = sections[_selectedSectionIndex];

            // 璁＄畻甯冨眬锛氱紪杈戝尯 + 棰勮鍖?
            float titleHeight = 30f;
            float previewHeight = _previewCollapsed ? 40f : 300f;
            float previewGap = 10f;
            float editorHeight = innerRect.height - titleHeight - previewGap - previewHeight;

            // 鍒嗗尯鏍囬
            Rect titleRect = new Rect(innerRect.x, innerRect.y, innerRect.width, titleHeight);
            GUI.color = SectionHeaderColor;
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, Pages.PromptLegacyPreview.GetSectionLabel(currentSection));
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            // 缂栬緫鍖哄煙锛堝埌搴曢儴锛?
            Rect contentRect = new Rect(innerRect.x, innerRect.y + titleHeight, innerRect.width, editorHeight);
            switch (currentSection)
            {
                case "GlobalPrompt":
                    DrawGlobalPromptEditorScrollable(contentRect);
                    break;
                case "FactionPrompts":
                    Pages.PromptLegacyRules.DrawFactionPromptsEditorScrollable(contentRect);
                    break;
                case "EnvironmentPrompts":
                    Pages.PromptEnvironment.DrawEnvironmentPromptsEditorScrollable(contentRect);
                    break;
                case "ApiActions":
                    Pages.PromptLegacyApi.DrawApiActionsEditorScrollable(contentRect);
                    break;
                case "JsonTemplate":
                    DrawJsonTemplateEditorScrollable(contentRect);
                    break;
                case "ImportantRules":
                    DrawImportantRulesEditorScrollable(contentRect);
                    break;
                case "PromptTemplates":
                    Pages.PromptTemplates.DrawPromptTemplatesEditorScrollable(contentRect);
                    break;
                case "SocialCirclePrompts":
                    Pages.PromptSocialCircle.DrawSocialCirclePromptEditorScrollable(contentRect);
                    break;
                case "DecisionRules":
                    Pages.PromptLegacyRules.DrawDecisionRulesEditorScrollable(contentRect);
                    break;
                case "DynamicData":
                    DrawDynamicDataEditor(contentRect);
                    break;
            }

            // 棰勮鍖哄煙锛堝湪鍙充晶涓嬫柟锛屽缁堟樉绀猴級
            float previewY = innerRect.y + titleHeight + editorHeight + previewGap;
            Rect previewRect = new Rect(innerRect.x, previewY, innerRect.width, previewHeight);
            Pages.PromptLegacyPreview.DrawPreviewRight(previewRect);
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

            // JSON 妯℃澘 - 甯︽粴鍔ㄦ潯锛堝～婊″墿浣欑┖闂达級
            Widgets.Label(new Rect(rect.x, y, rect.width, 20f), "RimChat_JsonTemplateLabel".Translate());
            y += 22f;

            float textHeight = rect.yMax - y - 29f; // 棰勭暀澶嶉€夋绌洪棿
            Rect textRect = new Rect(rect.x, y, rect.width - 16f, textHeight);

            // 璁＄畻瀹為檯鍐呭楂樺害锛岀‘淇濆畬鏁存樉绀?
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

            // 鏍囬
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 20f), "RimChat_JsonTemplateLabel".Translate());

            // 甯︽粴鍔ㄦ潯鐨勬枃鏈锛堝～婊″墿浣欑┖闂达級
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

            // 鏍囬
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 20f), "RimChat_ImportantRulesLabel".Translate());

            // 甯︽粴鍔ㄦ潯鐨勬枃鏈锛堝～婊″墿浣欑┖闂达級
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
            var dynConfig = SystemPromptConfigData.DynamicDataInjection;
            if (dynConfig == null)
            {
                dynConfig = new DynamicDataInjectionConfig();
                SystemPromptConfigData.DynamicDataInjection = dynConfig;
            }

            float y = rect.y;

            Rect check1 = new Rect(rect.x, y, rect.width, 24f);
            Widgets.CheckboxLabeled(check1, "RimChat_InjectMemoryData".Translate(), ref dynConfig.InjectMemoryData);
            Pages.Tooltips.Register(check1, "RimChat_InjectMemoryDataTooltip");
            y += 28f;

            Rect check2 = new Rect(rect.x, y, rect.width, 24f);
            Widgets.CheckboxLabeled(check2, "RimChat_InjectFactionInfo".Translate(), ref dynConfig.InjectFactionInfo);
            Pages.Tooltips.Register(check2, "RimChat_InjectFactionInfoTooltip");
            y += 28f;

            Rect check3 = new Rect(rect.x, y, rect.width, 24f);
            Widgets.CheckboxLabeled(check3, "RimChat_UseHierarchicalPromptFormat".Translate(), ref SystemPromptConfigData.UseHierarchicalPromptFormat);
            Pages.Tooltips.Register(check3, "RimChat_UseHierarchicalPromptFormatTooltip");
            y += 30f;

            RelationsSettings settings = RelationsMod.Settings;
            if (settings != null)
            {
                Rect compressionEnabledRect = new Rect(rect.x, y, rect.width, 24f);
                Widgets.CheckboxLabeled(
                    compressionEnabledRect,
                    "RimChat_DialogueCompressionEnabled".Translate(),
                    ref settings.EnableDialogueContextCompression);
                Pages.Tooltips.Register(compressionEnabledRect, "RimChat_DialogueCompressionEnabledTooltip");
                y += 28f;

                if (settings.EnableDialogueContextCompression)
                {
                    Widgets.Label(new Rect(rect.x, y, rect.width, 22f), "RimChat_DialogueCompressionProfile102025".Translate());
                    y += 24f;

                    y = DrawCompressionSlider(
                        rect,
                        y,
                        "RimChat_DialogueCompressionKeepRecent".Translate(settings.DialogueCompressionKeepRecentTurns),
                        ref settings.DialogueCompressionKeepRecentTurns,
                        6,
                        30);

                    int tier2Min = settings.DialogueCompressionKeepRecentTurns + 1;
                    y = DrawCompressionSlider(
                        rect,
                        y,
                        "RimChat_DialogueCompressionTier2Start".Translate(settings.DialogueCompressionSecondaryTierStart),
                        ref settings.DialogueCompressionSecondaryTierStart,
                        tier2Min,
                        120);

                    int tier3Min = settings.DialogueCompressionSecondaryTierStart + 1;
                    y = DrawCompressionSlider(
                        rect,
                        y,
                        "RimChat_DialogueCompressionTier3Start".Translate(settings.DialogueCompressionTertiaryTierStart),
                        ref settings.DialogueCompressionTertiaryTierStart,
                        tier3Min,
                        180);

                    y = DrawCompressionSlider(
                        rect,
                        y,
                        "RimChat_DialogueCompressionMaxEvents".Translate(settings.DialogueCompressionMaxEventsPerSegment),
                        ref settings.DialogueCompressionMaxEventsPerSegment,
                        1,
                        3);

                    settings.DialogueCompressionMaxMark = 3;
                    Widgets.Label(
                        new Rect(rect.x, y, rect.width, 22f),
                        "RimChat_DialogueCompressionMaxMark".Translate(settings.DialogueCompressionMaxMark));
                    y += 24f;

                    settings.DialogueCompressionSecondaryTriggerTurns = settings.DialogueCompressionKeepRecentTurns + 10;
                    settings.DialogueCompressionSecondaryWindowMinRecency = settings.DialogueCompressionSecondaryTierStart;
                    settings.DialogueCompressionSecondaryWindowMaxRecency = settings.DialogueCompressionTertiaryTierStart - 1;
                }
            }

            Rect tagsLabelRect = new Rect(rect.x, y, 180f, 24f);
            Widgets.Label(tagsLabelRect, "RimChat_DiplomacySceneTags".Translate());
            Pages.Tooltips.Register(tagsLabelRect, "RimChat_DiplomacySceneTagsTooltip");
            string currentTags = RelationsMod.Settings?.DiplomacyManualSceneTagsCsv ?? string.Empty;
            string editedTags = Widgets.TextField(new Rect(rect.x + 184f, y, rect.width - 184f, 24f), currentTags);
            if (RelationsMod.Settings != null && !string.Equals(editedTags, currentTags, StringComparison.Ordinal))
            {
                RelationsMod.Settings.DiplomacyManualSceneTagsCsv = editedTags;
                _previewUpdateCooldown = 0;
            }
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
            if (string.IsNullOrWhiteSpace(csv))
            {
                return null;
            }

            return csv
                .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(tag => tag.Trim().ToLowerInvariant())
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct()
                .ToList();
        }








    
}
