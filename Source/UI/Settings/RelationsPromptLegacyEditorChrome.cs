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

internal sealed class RelationsPromptLegacyEditorChrome
{
    internal readonly RelationsPromptLegacyEditors Owner;

    internal RelationsPromptLegacyEditorChrome(RelationsPromptLegacyEditors owner)
    {
        Owner = owner;
    }


        internal void DrawDynamicDataEditor(Rect rect)
        {
            var dynConfig = Owner.SystemPromptConfigData.DynamicDataInjection;
            if (dynConfig == null)
            {
                dynConfig = new DynamicDataInjectionConfig();
                Owner.SystemPromptConfigData.DynamicDataInjection = dynConfig;
            }

            float y = rect.y;

            Rect check1 = new Rect(rect.x, y, rect.width, 24f);
            Widgets.CheckboxLabeled(check1, "RimChat_InjectMemoryData".Translate(), ref dynConfig.InjectMemoryData);
            Owner.Pages.Tooltips.Register(check1, "RimChat_InjectMemoryDataTooltip");
            y += 28f;

            Rect check2 = new Rect(rect.x, y, rect.width, 24f);
            Widgets.CheckboxLabeled(check2, "RimChat_InjectFactionInfo".Translate(), ref dynConfig.InjectFactionInfo);
            Owner.Pages.Tooltips.Register(check2, "RimChat_InjectFactionInfoTooltip");
            y += 28f;

            Rect check3 = new Rect(rect.x, y, rect.width, 24f);
            Widgets.CheckboxLabeled(check3, "RimChat_UseHierarchicalPromptFormat".Translate(), ref Owner.SystemPromptConfigData.UseHierarchicalPromptFormat);
            Owner.Pages.Tooltips.Register(check3, "RimChat_UseHierarchicalPromptFormatTooltip");
            y += 30f;

            RelationsSettings settings = RelationsMod.Settings;
            if (settings != null)
            {
                Rect compressionEnabledRect = new Rect(rect.x, y, rect.width, 24f);
                Widgets.CheckboxLabeled(
                    compressionEnabledRect,
                    "RimChat_DialogueCompressionEnabled".Translate(),
                    ref settings.EnableDialogueContextCompression);
                Owner.Pages.Tooltips.Register(compressionEnabledRect, "RimChat_DialogueCompressionEnabledTooltip");
                y += 28f;

                if (settings.EnableDialogueContextCompression)
                {
                    Widgets.Label(new Rect(rect.x, y, rect.width, 22f), "RimChat_DialogueCompressionProfile102025".Translate());
                    y += 24f;

                    y = RelationsPromptLegacyEditors.DrawCompressionSlider(
                        rect,
                        y,
                        "RimChat_DialogueCompressionKeepRecent".Translate(settings.DialogueCompressionKeepRecentTurns),
                        ref settings.DialogueCompressionKeepRecentTurns,
                        6,
                        30);

                    int tier2Min = settings.DialogueCompressionKeepRecentTurns + 1;
                    y = RelationsPromptLegacyEditors.DrawCompressionSlider(
                        rect,
                        y,
                        "RimChat_DialogueCompressionTier2Start".Translate(settings.DialogueCompressionSecondaryTierStart),
                        ref settings.DialogueCompressionSecondaryTierStart,
                        tier2Min,
                        120);

                    int tier3Min = settings.DialogueCompressionSecondaryTierStart + 1;
                    y = RelationsPromptLegacyEditors.DrawCompressionSlider(
                        rect,
                        y,
                        "RimChat_DialogueCompressionTier3Start".Translate(settings.DialogueCompressionTertiaryTierStart),
                        ref settings.DialogueCompressionTertiaryTierStart,
                        tier3Min,
                        180);

                    y = RelationsPromptLegacyEditors.DrawCompressionSlider(
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
            Owner.Pages.Tooltips.Register(tagsLabelRect, "RimChat_DiplomacySceneTagsTooltip");
            string currentTags = RelationsMod.Settings?.DiplomacyManualSceneTagsCsv ?? string.Empty;
            string editedTags = Widgets.TextField(new Rect(rect.x + 184f, y, rect.width - 184f, 24f), currentTags);
            if (RelationsMod.Settings != null && !string.Equals(editedTags, currentTags, StringComparison.Ordinal))
            {
                RelationsMod.Settings.DiplomacyManualSceneTagsCsv = editedTags;
                Owner._previewUpdateCooldown = 0;
            }
        }

        internal void DrawNavigationPanelWithButtons(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.12f, 0.12f, 0.14f));


            Rect innerRect = rect.ContractedBy(8f);
            float y = innerRect.y;

            Rect toggleRect = new Rect(innerRect.x, y, innerRect.width, 24f);
            DrawModeToggleSmall(toggleRect);
            y += 30f;

            Widgets.DrawLineHorizontal(innerRect.x, y, innerRect.width);
            y += 10f;

            string[] sections = Owner._advancedPromptMode ? RelationsPromptLegacyEditors.AdvancedSectionNames : RelationsPromptLegacyEditors.SimpleSectionNames;

            float buttonAreaHeight = 210f;
            float listHeight = innerRect.height - y - buttonAreaHeight;

            Rect listRect = new Rect(innerRect.x, y, innerRect.width, listHeight);
            
            float contentHeight = sections.Length * 32f;
            Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, Mathf.Max(contentHeight, listHeight));
            
            Owner._navigationSectionScroll = GUI.BeginScrollView(listRect, Owner._navigationSectionScroll, viewRect);
            
            for (int i = 0; i < sections.Length; i++)
            {
                string sectionName = sections[i];
                bool isSelected = Owner._selectedSectionIndex == i;

                Rect btnRect = new Rect(0f, i * 32f, viewRect.width, 28f);

                if (isSelected)
                {
                    Widgets.DrawBoxSolid(btnRect, new Color(0.25f, 0.35f, 0.55f));
                }
                else if (Mouse.IsOver(btnRect))
                {
                    Widgets.DrawBoxSolid(btnRect, new Color(0.2f, 0.22f, 0.28f));
                }

                if (isSelected)
                {
                    Rect accentRect = new Rect(btnRect.x, btnRect.y, 3f, btnRect.height);
                    Widgets.DrawBoxSolid(accentRect, new Color(0.4f, 0.7f, 1f));
                }

                GUI.color = isSelected ? Color.white : new Color(0.7f, 0.7f, 0.75f);
                TextAnchor oldAnchor = Text.Anchor;
                Text.Anchor = TextAnchor.MiddleLeft;
                string label = Owner.Pages.PromptLegacyPreview.GetSectionLabel(sectionName);
                Widgets.Label(new Rect(btnRect.x + 8f, btnRect.y, btnRect.width - 16f, btnRect.height), label);
                Text.Anchor = oldAnchor;
                GUI.color = Color.white;
                Owner.Pages.Tooltips.Register(btnRect, RelationsSettingsTooltips.GetPromptSectionTooltipKey(sectionName));

                if (Widgets.ButtonInvisible(btnRect))
                {
                    Owner._selectedSectionIndex = i;
                    Owner._selectedApiActionIndex = -1;
                    Owner._selectedDecisionRuleIndex = -1;
                }
            }
            
            GUI.EndScrollView();

            y += listHeight + 10f;
            Rect buttonAreaRect = new Rect(innerRect.x, y, innerRect.width, buttonAreaHeight - 10f);
            
            Widgets.DrawLineHorizontal(innerRect.x, y - 5f, innerRect.width);
            
            DrawPromptActionButtonsVertical(buttonAreaRect);
        }

        internal void DrawEditorPanelWithPreview(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.1f, 0.1f, 0.12f));


            Rect innerRect = rect.ContractedBy(10f);

            string[] sections = Owner._advancedPromptMode ? RelationsPromptLegacyEditors.AdvancedSectionNames : RelationsPromptLegacyEditors.SimpleSectionNames;
            if (Owner._selectedSectionIndex >= sections.Length)
                Owner._selectedSectionIndex = 0;

            string currentSection = sections[Owner._selectedSectionIndex];

            float titleHeight = 30f;
            float previewHeight = Owner._previewCollapsed ? 40f : 300f;
            float previewGap = 10f;
            float editorHeight = innerRect.height - titleHeight - previewGap - previewHeight;

            Rect titleRect = new Rect(innerRect.x, innerRect.y, innerRect.width, titleHeight);
            GUI.color = RelationsPromptLegacyEditors.SectionHeaderColor;
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, Owner.Pages.PromptLegacyPreview.GetSectionLabel(currentSection));
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            Rect contentRect = new Rect(innerRect.x, innerRect.y + titleHeight, innerRect.width, editorHeight);
            switch (currentSection)
            {
                case "GlobalPrompt":
                    Owner.DrawGlobalPromptEditorScrollable(contentRect);
                    break;
                case "FactionPrompts":
                    Owner.Pages.PromptLegacyRules.DrawFactionPromptsEditorScrollable(contentRect);
                    break;
                case "EnvironmentPrompts":
                    Owner.Pages.PromptEnvironment.DrawEnvironmentPromptsEditorScrollable(contentRect);
                    break;
                case "ApiActions":
                    Owner.Pages.PromptLegacyApi.DrawApiActionsEditorScrollable(contentRect);
                    break;
                case "JsonTemplate":
                    Owner.DrawJsonTemplateEditorScrollable(contentRect);
                    break;
                case "ImportantRules":
                    Owner.DrawImportantRulesEditorScrollable(contentRect);
                    break;
                case "PromptTemplates":
                    Owner.Pages.PromptTemplates.DrawPromptTemplatesEditorScrollable(contentRect);
                    break;
                case "SocialCirclePrompts":
                    Owner.Pages.PromptSocialCircle.DrawSocialCirclePromptEditorScrollable(contentRect);
                    break;
                case "DecisionRules":
                    Owner.Pages.PromptLegacyRules.DrawDecisionRulesEditorScrollable(contentRect);
                    break;
                case "DynamicData":
                    DrawDynamicDataEditor(contentRect);
                    break;
            }

            float previewY = innerRect.y + titleHeight + editorHeight + previewGap;
            Rect previewRect = new Rect(innerRect.x, previewY, innerRect.width, previewHeight);
            Owner.Pages.PromptLegacyPreview.DrawPreviewRight(previewRect);
        }

        internal void DrawModeToggleSmall(Rect rect)
        {
            float btnWidth = rect.width / 2 - 2f;

            Rect simpleRect = new Rect(rect.x, rect.y, btnWidth, rect.height);
            bool isSimple = !Owner._advancedPromptMode;

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
                Owner._advancedPromptMode = false;
                Owner._selectedSectionIndex = 0;
                Owner.Pages.PromptLegacyPreview.SyncBuffersToData();
            }

            Rect advancedRect = new Rect(rect.x + btnWidth + 4f, rect.y, btnWidth, rect.height);
            bool isAdvanced = Owner._advancedPromptMode;

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
                Owner._advancedPromptMode = true;
                Owner._selectedSectionIndex = 0;
                Owner.Pages.PromptLegacyPreview.SyncBuffersToData();
            }
        }

        internal void DrawPromptActionButtonsVertical(Rect rect)
        {
            float btnHeight = 26f;
            float gap = 6f;
            float y = rect.y;

            Rect saveRect = new Rect(rect.x, y, rect.width, btnHeight);
            if (Widgets.ButtonText(saveRect, "RimChat_SavePrompt".Translate()))
            {
                Owner.Pages.PromptLegacyIo.SaveSystemPromptConfig();
                Messages.Message("RimChat_PromptSaved".Translate(), MessageTypeDefOf.NeutralEvent, false);
            }
            y += btnHeight + gap;

            Rect resetRect = new Rect(rect.x, y, rect.width, btnHeight);
            if (Widgets.ButtonText(resetRect, "RimChat_ResetToDefault".Translate()))
            {
                Owner.Pages.PromptLegacyIo.ShowResetPromptConfigConfirmation();
            }
            y += btnHeight + gap;

            Rect exportRect = new Rect(rect.x, y, rect.width, btnHeight);
            if (Widgets.ButtonText(exportRect, "RimChat_ExportPrompts".Translate()))
            {
                Owner.Pages.PromptLegacyIo.ShowExportSystemPromptDialog();
            }
            y += btnHeight + gap;

            Rect importRect = new Rect(rect.x, y, rect.width, btnHeight);
            if (Widgets.ButtonText(importRect, "RimChat_ImportPrompts".Translate()))
            {
                Owner.Pages.PromptLegacyIo.ShowImportSystemPromptDialog();
            }
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
