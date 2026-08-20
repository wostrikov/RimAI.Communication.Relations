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

internal sealed class RelationsPromptLegacyPreview
{
    internal readonly RelationsPromptLegacyEditors Owner;

    internal RelationsPromptLegacyPreview(RelationsPromptLegacyEditors owner)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    internal RelationsSettingsPages Pages => Owner.Pages;
    internal RelationsSettings Settings => Owner.Settings;
    internal SystemPromptConfig SystemPromptConfigData => Owner.SystemPromptConfigData;

        internal void DrawPreviewRight(Rect rect)
        {
            // Update animation time
            if (Owner._previewFoldAnimTime > 0f)
            {
                Owner._previewFoldAnimTime -= Time.deltaTime;
            }

            Rect titleBarRect = new Rect(rect.x, rect.y, rect.width, 22f);
            Widgets.DrawBoxSolid(titleBarRect, new Color(0.15f, 0.15f, 0.15f));
            
            Rect titleRect = new Rect(rect.x + 5f, rect.y + 2f, rect.width - 30f, 20f);
            GUI.color = new Color(0.5f, 0.8f, 0.5f);
            Text.Font = GameFont.Small;
            Widgets.Label(titleRect, "RimChat_PreviewTitleShort".Translate());
            GUI.color = Color.white;

            float foldBtnSize = 18f;
            Rect foldBtnRect = new Rect(rect.xMax - foldBtnSize - 5f, rect.y + 2f, foldBtnSize, foldBtnSize);
            
            GUI.color = new Color(0.25f, 0.25f, 0.25f);
            if (Mouse.IsOver(foldBtnRect))
            {
                GUI.color = new Color(0.35f, 0.35f, 0.35f);
            }
            Widgets.DrawBoxSolid(foldBtnRect, GUI.color);
            Widgets.DrawBox(foldBtnRect);
            
            if (Widgets.ButtonInvisible(foldBtnRect))
            {
                Owner._previewCollapsed = !Owner._previewCollapsed;
                Owner._previewFoldAnimTime = 0.2f;
            }

            // Use ASCII arrow glyphs to avoid font/encoding issues.
            string arrow = Owner._previewCollapsed ? ">" : "v";
            TextAnchor oldAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = Color.white;
            Widgets.Label(foldBtnRect, arrow);
            Text.Anchor = oldAnchor;
            GUI.color = Color.white;

            if (!Owner._previewCollapsed || Owner._previewFoldAnimTime > 0f)
            {
                float contentHeightFactor = 1f;
                if (Owner._previewFoldAnimTime > 0f)
                {
                    float t = 1f - (Owner._previewFoldAnimTime / 0.2f);
                    contentHeightFactor = Owner._previewCollapsed ? 1f - t : t;
                }

                if (contentHeightFactor > 0.01f)
                {
                    float actualContentHeight = (rect.height - 24f) * contentHeightFactor;
                    Rect contentRect = new Rect(rect.x, rect.y + 24f, rect.width, actualContentHeight);

                    if (contentHeightFactor >= 0.95f)
                    {
                        Widgets.DrawBoxSolid(contentRect, new Color(0.08f, 0.1f, 0.08f));
                        Widgets.DrawBox(contentRect);

                        Rect innerRect = contentRect.ContractedBy(4f);
                        DrawPreviewContextControls(innerRect);

                        const float controlsHeight = 112f;
                        float textStartY = innerRect.y + controlsHeight;
                        float textHeight = Mathf.Max(20f, innerRect.height - controlsHeight);
                        Rect textRect = new Rect(innerRect.x, textStartY, innerRect.width, textHeight);

                        UpdatePreviewText();

                        float contentHeight = Text.CalcHeight(Owner._cachedPreviewText, textRect.width - 20f);
                        contentHeight = Mathf.Max(contentHeight, textRect.height);

                        Rect viewRect = new Rect(0f, 0f, textRect.width - 20f, contentHeight);
                        Owner._previewScroll = GUI.BeginScrollView(textRect, Owner._previewScroll, viewRect);

                        Text.Font = GameFont.Tiny;
                        GUI.color = new Color(0.6f, 0.7f, 0.6f);
                        Widgets.Label(viewRect, Owner._cachedPreviewText);
                        GUI.color = Color.white;
                        Text.Font = GameFont.Small;

                        GUI.EndScrollView();
                    }
                }
            }
            else if (Owner._previewCollapsed)
            {
                Rect collapsedRect = new Rect(rect.x, rect.y + 24f, rect.width, 16f);
                Widgets.DrawBoxSolid(collapsedRect, new Color(0.15f, 0.15f, 0.15f, 0.5f));
                GUI.color = Color.gray;
                Text.Font = GameFont.Tiny;
                Widgets.Label(collapsedRect, "RimChat_PreviewCollapsedHint".Translate());
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }
        }

        internal string GetSectionLabel(string sectionName)
        {
            return sectionName switch
            {
                "GlobalPrompt" => "RimChat_GlobalSystemPromptSection".Translate(),
                "EnvironmentPrompts" => "RimChat_EnvironmentPromptsSection".Translate(),
                "FactionPrompts" => "RimChat_FactionPromptsSection".Translate(),
                "ApiActions" => "RimChat_ApiActionsSection".Translate(),
                "JsonTemplate" => "RimChat_JsonTemplateLabel".Translate(),
                "ImportantRules" => "RimChat_ImportantRulesLabel".Translate(),
                "PromptTemplates" => "RimChat_PromptTemplatesSection".Translate(),
                "SocialCirclePrompts" => "RimChat_SocialCirclePromptSection".Translate(),
                "DecisionRules" => "RimChat_DecisionRulesSection".Translate(),
                "DynamicData" => "RimChat_DynamicDataInjectionSection".Translate(),
                _ => sectionName
            };
        }

        internal void SyncBuffersToData()
        {
            Owner._globalPromptBuffer = SystemPromptConfigData.GlobalSystemPrompt ?? "";
            Owner._globalDialoguePromptBuffer = SystemPromptConfigData.GlobalDialoguePrompt ?? "";
            Owner._jsonTemplateBuffer = SystemPromptConfigData.ResponseFormat?.JsonTemplate ?? "";
            Owner._importantRulesBuffer = SystemPromptConfigData.ResponseFormat?.ImportantRules ?? "";
        }

        internal void UpdatePreviewText()
        {
            Owner._previewUpdateCooldown--;
            if (Owner._previewUpdateCooldown <= 0)
            {
                Owner._cachedPreviewText = GeneratePreviewText();
                Owner._previewUpdateCooldown = 60;
            }
        }

        internal string GeneratePreviewText()
        {
            try
            {
                var config = SystemPromptConfigData;
                Faction sampleFaction = Find.FactionManager?.AllFactionsVisible?.FirstOrDefault(f => f != null && !f.IsPlayer);
                if (sampleFaction == null)
                {
                    return "RimChat_EnvironmentPreviewNoContext".Translate();
                }

                var settings = RelationsMod.Settings;
                List<string> tags = RelationsPromptLegacyEditors.ParseSceneTagsCsv(settings?.PromptPreviewSceneTagsCsv);
                string fullPrompt = PromptPersistenceService.Instance.BuildFullSystemPrompt(
                    sampleFaction,
                    config,
                    settings?.PromptPreviewUseProactiveContext == true,
                    tags);

                string diagnostics = Pages.PromptLegacyValidation.BuildEnvironmentPreviewDiagnostics(
                    config,
                    sampleFaction,
                    settings?.PromptPreviewUseProactiveContext == true,
                    tags);

                if (!string.IsNullOrWhiteSpace(diagnostics))
                {
                    fullPrompt += "\n\n=== PREVIEW DIAGNOSTICS ===\n" + diagnostics;
                }

                return fullPrompt;
            }
            catch (Exception ex)
            {
                return $"Settings.Error: {ex.Message}";
            }
        }

        internal void DrawPreviewContextControls(Rect rect)
        {
            var settings = RelationsMod.Settings;
            if (settings == null)
            {
                return;
            }

            Rect proactiveRect = new Rect(rect.x, rect.y, rect.width, 24f);
            bool proactive = settings.PromptPreviewUseProactiveContext;
            Widgets.CheckboxLabeled(proactiveRect, "RimChat_PreviewUseProactiveContext".Translate(), ref proactive);
            if (proactive != settings.PromptPreviewUseProactiveContext)
            {
                settings.PromptPreviewUseProactiveContext = proactive;
                Owner._previewUpdateCooldown = 0;
            }

            Rect tagsRect = new Rect(rect.x, rect.y + 26f, rect.width, 24f);
            string tags = settings.PromptPreviewSceneTagsCsv ?? string.Empty;
            Widgets.Label(new Rect(tagsRect.x, tagsRect.y, 120f, tagsRect.height), "RimChat_PreviewSceneTags".Translate());
            string edited = Widgets.TextField(new Rect(tagsRect.x + 124f, tagsRect.y, tagsRect.width - 124f, tagsRect.height), tags);
            if (!string.Equals(edited, tags, StringComparison.Ordinal))
            {
                settings.PromptPreviewSceneTagsCsv = edited;
                Owner._previewUpdateCooldown = 0;
            }

            Rect actionsRect = new Rect(rect.x, rect.y + 52f, rect.width, 24f);
            DrawPreviewActionButtons(actionsRect);

            Rect statusRect = new Rect(rect.x, rect.y + 80f, rect.width, 24f);
            Pages.PromptLegacyValidation.DrawLiveValidationStatus(statusRect);
        }

        internal void DrawPreviewActionButtons(Rect actionsRect)
        {
            float buttonWidth = (actionsRect.width - 16f) / 3f;
            Rect variableRect = new Rect(actionsRect.x, actionsRect.y, buttonWidth, actionsRect.height);
            Rect validateRect = new Rect(variableRect.xMax + 8f, actionsRect.y, buttonWidth, actionsRect.height);
            Rect migrationRect = new Rect(validateRect.xMax + 8f, actionsRect.y, buttonWidth, actionsRect.height);

            if (Widgets.ButtonText(variableRect, "RimChat_PromptVariables".Translate()))
            {
                Pages.PromptLegacyValidation.OpenPromptVariablePicker();
            }

            if (Widgets.ButtonText(validateRect, "RimChat_ValidateVariables".Translate()))
            {
                Pages.PromptLegacyValidation.ValidateCurrentSectionVariables();
            }

            if (Widgets.ButtonText(migrationRect, "RimChat_PromptMigrationResultButton".Translate()))
            {
                Pages.PromptLegacyValidation.OpenPromptMigrationResultDialog();
            }
        }
}
