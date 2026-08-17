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

internal sealed class RelationsPromptLegacyValidation
{
    internal readonly RelationsPromptLegacyEditors Owner;

    internal RelationsPromptLegacyValidation(RelationsPromptLegacyEditors owner)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    internal RelationsSettingsPages Pages => Owner.Pages;
    internal RelationsSettings Settings => Owner.Settings;
    internal SystemPromptConfig SystemPromptConfigData => Owner.SystemPromptConfigData;

        internal void DrawLiveValidationStatus(Rect rect)
        {
            UpdateLiveValidationState();
            string currentText = GetCurrentSectionEditableText();
            string statusText = BuildLiveValidationStatusText(Owner._liveValidationResult, currentText);
            Color oldColor = GUI.color;
            GUI.color = ResolveLiveValidationStatusColor(Owner._liveValidationResult, currentText);
            Widgets.Label(rect, statusText);
            GUI.color = oldColor;
        }

        internal void OpenPromptMigrationResultDialog()
        {
            PromptTemplateAutoRewriteResult result = PromptPersistenceService.Instance.GetLastSchemaRewriteResult();
            Find.WindowStack.Add(new Dialog_PromptMigrationResult(result));
        }

        internal void UpdateLiveValidationState()
        {
            string section = GetCurrentValidationSectionName();
            string text = GetCurrentSectionEditableText();
            string signature = section + "\n" + (text ?? string.Empty);
            Owner._liveValidationCooldown = Math.Max(0, Owner._liveValidationCooldown - 1);
            if (Owner._liveValidationCooldown > 0 &&
                string.Equals(signature, Owner._liveValidationSignature, StringComparison.Ordinal))
            {
                return;
            }

            Owner._liveValidationSignature = signature;
            Owner._liveValidationCooldown = RelationsPromptLegacyEditors.LiveValidationRefreshTicks;
            Owner._liveValidationResult = string.IsNullOrWhiteSpace(text)
                ? new TemplateVariableValidationResult()
                : PromptPersistenceService.Instance.ValidateTemplateVariables(text);
        }

        internal string GetCurrentValidationSectionName()
        {
            string[] sections = Owner._advancedPromptMode ? RelationsPromptLegacyEditors.AdvancedSectionNames : RelationsPromptLegacyEditors.SimpleSectionNames;
            if (Owner._selectedSectionIndex < 0 || Owner._selectedSectionIndex >= sections.Length)
            {
                return string.Empty;
            }

            return sections[Owner._selectedSectionIndex];
        }

        internal Color ResolveLiveValidationStatusColor(
            TemplateVariableValidationResult result,
            string currentText)
        {
            if (string.IsNullOrWhiteSpace(currentText))
            {
                return Color.gray;
            }

            if (result?.HasScribanError == true || result?.UnknownVariables?.Count > 0)
            {
                return new Color(1f, 0.55f, 0.55f);
            }

            return new Color(0.55f, 0.95f, 0.55f);
        }

        internal string BuildLiveValidationStatusText(
            TemplateVariableValidationResult result,
            string currentText)
        {
            if (string.IsNullOrWhiteSpace(currentText))
            {
                return "RimChat_PromptLiveValidationIdle".Translate();
            }

            if (result?.HasScribanError == true)
            {
                return "RimChat_PromptLiveValidationError".Translate(
                    result.ScribanErrorCode,
                    result.ScribanErrorLine,
                    result.ScribanErrorColumn);
            }

            if (result?.UnknownVariables?.Count > 0)
            {
                return "RimChat_PromptLiveValidationUnknown".Translate(BuildUnknownVariableSummary(result.UnknownVariables));
            }

            int usedCount = result?.UsedVariables?.Count ?? 0;
            return "RimChat_PromptLiveValidationOk".Translate(usedCount);
        }

        internal string BuildUnknownVariableSummary(IReadOnlyList<string> unknownVariables)
        {
            if (unknownVariables == null || unknownVariables.Count == 0)
            {
                return string.Empty;
            }

            int shownCount = Math.Min(4, unknownVariables.Count);
            string joined = string.Join(", ", unknownVariables.Take(shownCount));
            if (unknownVariables.Count <= shownCount)
            {
                return joined;
            }

            return joined + $" +{unknownVariables.Count - shownCount}";
        }

        internal string BuildEnvironmentPreviewDiagnostics(
            SystemPromptConfig config,
            Faction sampleFaction,
            bool proactive,
            List<string> tags)
        {
            DialogueScenarioContext context = DialogueScenarioContext.CreateDiplomacy(sampleFaction, proactive, tags);
            PromptPersistenceService.Instance.BuildEnvironmentPromptBlocksWithDiagnostics(config, context, out EnvironmentPromptBuildDiagnostics diagnostics);
            if (diagnostics == null)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Tags: {(diagnostics.ScenarioTags.Count > 0 ? string.Join(", ", diagnostics.ScenarioTags) : "none")}");

            List<EnvironmentSceneEntryDiagnostic> topEntries = diagnostics.SceneEntries
                .Take(16)
                .ToList();

            for (int i = 0; i < topEntries.Count; i++)
            {
                EnvironmentSceneEntryDiagnostic item = topEntries[i];
                string state = item.Included
                    ? $"included ({item.AppliedChars}/{item.OriginalChars})"
                    : $"skipped ({item.SkipReason})";

                string truncation = item.TruncatedByPerSceneLimit || item.TruncatedByTotalLimit
                    ? $" trunc:{(item.TruncatedByPerSceneLimit ? "per_scene " : string.Empty)}{(item.TruncatedByTotalLimit ? "total" : string.Empty)}"
                    : string.Empty;

                string unknownVariables = item.UnknownVariables.Count > 0
                    ? $" unknown_vars:{string.Join(",", item.UnknownVariables)}"
                    : string.Empty;

                sb.AppendLine($"- P{item.Priority} [{item.Name}] {state}{truncation}{unknownVariables}");
            }

            if (diagnostics.SceneEntries.Count > topEntries.Count)
            {
                sb.AppendLine($"... {diagnostics.SceneEntries.Count - topEntries.Count} more entries");
            }

            return sb.ToString().TrimEnd();
        }

        internal void OpenPromptVariablePicker()
        {
            IReadOnlyList<PromptTemplateVariableDefinition> defs = PromptPersistenceService.Instance.GetTemplateVariableDefinitions();
            Find.WindowStack.Add(new Dialog_PromptVariablePicker(defs, token =>
            {
                if (!TryInsertVariableToken(token))
                {
                    Messages.Message("RimChat_VariableInsertFailed".Translate(), MessageTypeDefOf.RejectInput, false);
                }
                else
                {
                    Owner._previewUpdateCooldown = 0;
                }
            }));
        }

        internal void ValidateCurrentSectionVariables()
        {
            string text = GetCurrentSectionEditableText();
            if (string.IsNullOrWhiteSpace(text))
            {
                Messages.Message("RimChat_VariableValidationNoTemplate".Translate(), MessageTypeDefOf.NeutralEvent, false);
                return;
            }

            TemplateVariableValidationResult result = PromptPersistenceService.Instance.ValidateTemplateVariables(text);
            if (result.HasScribanError)
            {
                Messages.Message(
                    "RimChat_VariableValidationCompileError".Translate(
                        result.ScribanErrorCode,
                        result.ScribanErrorLine,
                        result.ScribanErrorColumn,
                        result.ScribanErrorMessage),
                    MessageTypeDefOf.RejectInput,
                    false);
                return;
            }

            if (result.UnknownVariables.Count > 0)
            {
                string unknown = string.Join(", ", result.UnknownVariables);
                Messages.Message("RimChat_VariableValidationUnknown".Translate(unknown), MessageTypeDefOf.RejectInput, false);
                return;
            }

            Messages.Message("RimChat_VariableValidationPass".Translate(result.UsedVariables.Count), MessageTypeDefOf.NeutralEvent, false);
        }

        internal bool TryInsertVariableToken(string token)
        {
            if (Pages.PromptWorkbench.TryInsertVariableTokenToEntryChannel(token))
            {
                return true;
            }

            string[] sections = Owner._advancedPromptMode ? RelationsPromptLegacyEditors.AdvancedSectionNames : RelationsPromptLegacyEditors.SimpleSectionNames;
            if (Owner._selectedSectionIndex < 0 || Owner._selectedSectionIndex >= sections.Length)
            {
                return false;
            }

            string section = sections[Owner._selectedSectionIndex];
            switch (section)
            {
                case "GlobalPrompt":
                    Owner._globalPromptBuffer = (Owner._globalPromptBuffer ?? string.Empty) + token;
                    SystemPromptConfigData.GlobalSystemPrompt = Owner._globalPromptBuffer;
                    return true;
                case "JsonTemplate":
                    Owner._jsonTemplateBuffer = (Owner._jsonTemplateBuffer ?? string.Empty) + token;
                    if (SystemPromptConfigData.ResponseFormat == null) SystemPromptConfigData.ResponseFormat = new ResponseFormatConfig();
                    SystemPromptConfigData.ResponseFormat.JsonTemplate = Owner._jsonTemplateBuffer;
                    return true;
                case "ImportantRules":
                    Owner._importantRulesBuffer = (Owner._importantRulesBuffer ?? string.Empty) + token;
                    if (SystemPromptConfigData.ResponseFormat == null) SystemPromptConfigData.ResponseFormat = new ResponseFormatConfig();
                    SystemPromptConfigData.ResponseFormat.ImportantRules = Owner._importantRulesBuffer;
                    return true;
                case "EnvironmentPrompts":
                    return Pages.PromptEnvironment.TryAppendVariableToSelectedEnvironmentScene(token);
                case "PromptTemplates":
                    PromptTemplateTextConfig templates = Pages.PromptTemplates.EnsurePromptTemplateConfig();
                    if (Pages.PromptTemplates._selectedPromptTemplateFieldIndex < 0 || Pages.PromptTemplates._selectedPromptTemplateFieldIndex >= RelationsPromptTemplateEditors.PromptTemplateFieldKeys.Length)
                    {
                        Pages.PromptTemplates._selectedPromptTemplateFieldIndex = 0;
                    }

                    string key = RelationsPromptTemplateEditors.PromptTemplateFieldKeys[Pages.PromptTemplates._selectedPromptTemplateFieldIndex];
                    if (!string.Equals(Pages.PromptTemplates._promptTemplateEditingKey, key, StringComparison.Ordinal))
                    {
                        Pages.PromptTemplates._promptTemplateEditingKey = key;
                        Pages.PromptTemplates._promptTemplateEditorBuffer = RelationsPromptTemplateEditors.GetPromptTemplateFieldValue(templates, key);
                    }

                    Pages.PromptTemplates._promptTemplateEditorBuffer = (Pages.PromptTemplates._promptTemplateEditorBuffer ?? string.Empty) + token;
                    RelationsPromptTemplateEditors.SetPromptTemplateFieldValue(templates, key, Pages.PromptTemplates._promptTemplateEditorBuffer);
                    return true;
                case "SocialCirclePrompts":
                    return Pages.PromptSocialCircle.TryAppendVariableToSocialCircleSection(token);
                default:
                    return false;
            }
        }

        internal string GetCurrentSectionEditableText()
        {
            string[] sections = Owner._advancedPromptMode ? RelationsPromptLegacyEditors.AdvancedSectionNames : RelationsPromptLegacyEditors.SimpleSectionNames;
            if (Owner._selectedSectionIndex < 0 || Owner._selectedSectionIndex >= sections.Length)
            {
                return string.Empty;
            }

            string section = sections[Owner._selectedSectionIndex];
            return section switch
            {
                "GlobalPrompt" => Owner._globalPromptBuffer ?? string.Empty,
                "JsonTemplate" => Owner._jsonTemplateBuffer ?? string.Empty,
                "ImportantRules" => Owner._importantRulesBuffer ?? string.Empty,
                "EnvironmentPrompts" => Pages.PromptEnvironment.GetSelectedEnvironmentSceneContent(),
                "PromptTemplates" => Pages.PromptTemplates.GetCurrentPromptTemplateEditorText(),
                "SocialCirclePrompts" => Pages.PromptSocialCircle.GetSocialCircleEditableText(),
                _ => string.Empty
            };
        }
}
