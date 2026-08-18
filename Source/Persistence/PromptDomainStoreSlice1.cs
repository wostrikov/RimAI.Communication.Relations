using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using Verse;
using Ustas.RimAI.Communication.Relations.Prompting.Transfer;
using Ustas.RimAI.Communication.Relations.Serialization;

namespace Ustas.RimAI.Communication.Relations.Persistence
{
    internal sealed class PromptDomainStoreSlice1 : PromptDomainStoreCollaborator
    {
        internal PromptDomainStoreSlice1(PromptDomainStore owner) : base(owner)
        {
        }

internal void InvalidateCache()
        {
            _cachedConfig = null;
            _cachedConfigWriteTimeUtc = DateTime.MinValue;
        }

internal bool TryLoadPromptDomains(out SystemPromptConfig config)
        {
            return Owner.TryLoadPromptDomains(
                includeCustom: true,
                out config,
                out _,
                out _);
        }

internal bool TryLoadPromptDomains(
            bool includeCustom,
            out SystemPromptConfig config,
            out int loadedDomainSchemaVersion,
            out List<string> validationErrors)
        {
            SystemPromptDomainConfig systemPrompt = Owner.LoadSystemPromptDomain(includeCustom);
            DiplomacyDialoguePromptDomainConfig diplomacyPrompt = Owner.LoadDiplomacyPromptDomain(includeCustom);
            SocialCirclePromptDomainConfig socialPrompt = Owner.LoadSocialCirclePromptDomain(includeCustom);
            RpgPromptCustomConfig pawnPrompt = includeCustom
                ? RpgPromptCustomStore.LoadOrDefault()
                : RpgPromptCustomStore.LoadDefaultsOnly();
            loadedDomainSchemaVersion = systemPrompt?.PromptDomainSchemaVersion ?? 0;
            config = Owner.ComposeConfigFromDomains(systemPrompt, diplomacyPrompt, pawnPrompt, socialPrompt);
            validationErrors = Owner.ValidateDomainConfigSemantics(config);

            if (!includeCustom && validationErrors.Count > 0)
            {
                if (Owner.TryRehydrateFromAggregateDomainJson(includeCustom: false, out SystemPromptConfig reparsedConfig, out List<string> reparsedErrors))
                {
                    config = reparsedConfig;
                    validationErrors = reparsedErrors;
                }
            }

            return validationErrors.Count == 0;
        }

internal bool TryRehydrateFromAggregateDomainJson(
            bool includeCustom,
            out SystemPromptConfig config,
            out List<string> validationErrors)
        {
            config = null;
            validationErrors = new List<string>();
            string aggregateJson = Owner.BuildAggregateConfigJsonFromDomainFiles(includeCustom);
            if (string.IsNullOrWhiteSpace(aggregateJson))
            {
                return false;
            }

            config = Owner.ParseJsonToConfigInternal(
                aggregateJson,
                includeCustom ? "aggregate_domains_custom" : "aggregate_domains_default_only");
            validationErrors = Owner.ValidateDomainConfigSemantics(config);
            return validationErrors.Count == 0;
        }

internal string BuildAggregateConfigJsonFromDomainFiles(bool includeCustom)
        {
            string systemDefault = Owner.ReadDomainJson(PromptDomainFileCatalog.GetDefaultPath(PromptDomainFileCatalog.SystemPromptDefaultFileName));
            string diplomacyDefault = Owner.ReadDomainJson(PromptDomainFileCatalog.GetDefaultPath(PromptDomainFileCatalog.DiplomacyPromptDefaultFileName));
            string socialDefault = Owner.ReadDomainJson(PromptDomainFileCatalog.GetDefaultPath(PromptDomainFileCatalog.SocialCirclePromptDefaultFileName));
            if (string.IsNullOrWhiteSpace(systemDefault) || string.IsNullOrWhiteSpace(diplomacyDefault) || string.IsNullOrWhiteSpace(socialDefault))
            {
                return string.Empty;
            }

            string systemCustom = includeCustom
                ? Owner.ReadDomainJson(PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.SystemPromptCustomFileName))
                : string.Empty;
            string diplomacyCustom = includeCustom
                ? Owner.ReadDomainJson(PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.DiplomacyPromptCustomFileName))
                : string.Empty;
            string socialCustom = includeCustom
                ? Owner.ReadDomainJson(PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.SocialCirclePromptCustomFileName))
                : string.Empty;
            RpgPromptCustomConfig pawnPrompt = includeCustom
                ? RpgPromptCustomStore.LoadOrDefault()
                : RpgPromptCustomStore.LoadDefaultsOnly();

            string configName = Owner.SelectStringField(systemCustom, systemDefault, "ConfigName", "Default");
            string globalSystemPrompt = Owner.SelectStringField(systemCustom, systemDefault, "GlobalSystemPrompt", string.Empty);
            string globalDialoguePrompt = Owner.SelectStringField(diplomacyCustom, diplomacyDefault, "GlobalDialoguePrompt", string.Empty);
            string useAdvancedMode = Owner.SelectValueField(systemCustom, systemDefault, "UseAdvancedMode", "false");
            string useHierarchical = Owner.SelectValueField(systemCustom, systemDefault, "UseHierarchicalPromptFormat", "true");
            string enabled = Owner.SelectValueField(systemCustom, systemDefault, "Enabled", "true");
            string promptDomainSchemaVersion = Owner.SelectValueField(systemCustom, systemDefault, "PromptDomainSchemaVersion", CurrentPromptDomainSchemaVersion.ToString());
            string promptSchemaVersion = Owner.SelectValueField(systemCustom, systemDefault, "PromptSchemaVersion", SystemPromptConfig.CurrentPromptSchemaVersion.ToString());
            string schemaVersion = Owner.SelectValueField(systemCustom, systemDefault, "PromptPolicySchemaVersion", SystemPromptConfig.CurrentPromptPolicySchemaVersion.ToString());
            string apiActions = Owner.SelectArraySection(diplomacyCustom, diplomacyDefault, "ApiActions", "[]");
            string responseFormat = Owner.SelectObjectSection(diplomacyCustom, diplomacyDefault, "ResponseFormat", "{}");
            string decisionRules = Owner.SelectArraySection(diplomacyCustom, diplomacyDefault, "DecisionRules", "[]");
            string environmentPrompt = Owner.SelectObjectSection(systemCustom, systemDefault, "EnvironmentPrompt", "{}");
            string promptTemplates = Owner.BuildPromptTemplatesJson(diplomacyCustom, diplomacyDefault, socialCustom, socialDefault, pawnPrompt);
            string promptPolicy = Owner.SelectObjectSection(systemCustom, systemDefault, "PromptPolicy", "{}");
            string dynamicData = Owner.SelectObjectSection(systemCustom, systemDefault, "DynamicDataInjection", "{}");

            return "{"
                + $"\"ConfigName\":\"{Owner.EscapeJson(configName)}\","
                + $"\"GlobalSystemPrompt\":\"{Owner.EscapeJson(globalSystemPrompt)}\","
                + $"\"GlobalDialoguePrompt\":\"{Owner.EscapeJson(globalDialoguePrompt)}\","
                + $"\"UseAdvancedMode\":{useAdvancedMode},"
                + $"\"UseHierarchicalPromptFormat\":{useHierarchical},"
                + $"\"PromptDomainSchemaVersion\":{promptDomainSchemaVersion},"
                + $"\"PromptSchemaVersion\":{promptSchemaVersion},"
                + $"\"PromptPolicySchemaVersion\":{schemaVersion},"
                + $"\"Enabled\":{enabled},"
                + $"\"ApiActions\":{apiActions},"
                + $"\"ResponseFormat\":{responseFormat},"
                + $"\"DecisionRules\":{decisionRules},"
                + $"\"EnvironmentPrompt\":{environmentPrompt},"
                + $"\"PromptTemplates\":{promptTemplates},"
                + $"\"PromptPolicy\":{promptPolicy},"
                + $"\"DynamicDataInjection\":{dynamicData}"
                + "}";
        }

internal string SelectStringField(string customJson, string defaultJson, string key, string fallback)
        {
            if (PromptJsonText.ContainsJsonKey(customJson, key))
            {
                return Owner.ExtractString(customJson, key);
            }

            if (PromptJsonText.ContainsJsonKey(defaultJson, key))
            {
                return Owner.ExtractString(defaultJson, key);
            }

            return fallback ?? string.Empty;
        }

internal string SelectValueField(string customJson, string defaultJson, string key, string fallback)
        {
            if (PromptJsonText.ContainsJsonKey(customJson, key))
            {
                return Owner.ExtractValue(customJson, key);
            }

            if (PromptJsonText.ContainsJsonKey(defaultJson, key))
            {
                return Owner.ExtractValue(defaultJson, key);
            }

            return fallback;
        }

internal string SelectObjectSection(string customJson, string defaultJson, string key, string fallback)
        {
            if (Owner.TryExtractJsonObject(customJson, key, out string customSection))
            {
                return "{" + customSection + "}";
            }

            if (Owner.TryExtractJsonObject(defaultJson, key, out string defaultSection))
            {
                return "{" + defaultSection + "}";
            }

            return fallback;
        }

internal string SelectArraySection(string customJson, string defaultJson, string key, string fallback)
        {
            if (Owner.TryExtractJsonArray(customJson, key, out string customSection))
            {
                return "[" + customSection + "]";
            }

            if (Owner.TryExtractJsonArray(defaultJson, key, out string defaultSection))
            {
                return "[" + defaultSection + "]";
            }

            return fallback;
        }

internal string BuildPromptTemplatesJson(
            string diplomacyCustom,
            string diplomacyDefault,
            string socialCustom,
            string socialDefault,
            RpgPromptCustomConfig pawnPrompt)
        {
            string enabled = Owner.SelectValueField(diplomacyCustom, diplomacyDefault, "PromptTemplatesEnabled", "true");
            string factGrounding = Owner.SelectStringField(diplomacyCustom, diplomacyDefault, "FactGroundingTemplate", string.Empty);
            string outputLanguage = Owner.SelectStringField(diplomacyCustom, diplomacyDefault, "OutputLanguageTemplate", string.Empty);
            string diplomacyFallback = Owner.SelectStringField(diplomacyCustom, diplomacyDefault, "DiplomacyFallbackRoleTemplate", string.Empty);
            string socialCircle = Owner.SelectStringField(socialCustom, socialDefault, "SocialCircleActionRuleTemplate", string.Empty);
            string socialNewsStyle = Owner.SelectStringField(socialCustom, socialDefault, "SocialCircleNewsStyleTemplate", string.Empty);
            string socialNewsContract = Owner.SelectStringField(socialCustom, socialDefault, "SocialCircleNewsJsonContractTemplate", string.Empty);
            string socialNewsFact = Owner.SelectStringField(socialCustom, socialDefault, "SocialCircleNewsFactTemplate", string.Empty);
            string decisionPolicy = Owner.SelectStringField(diplomacyCustom, diplomacyDefault, "DecisionPolicyTemplate", pawnPrompt?.DecisionPolicyTemplate ?? string.Empty);
            string turnObjective = Owner.SelectStringField(diplomacyCustom, diplomacyDefault, "TurnObjectiveTemplate", pawnPrompt?.TurnObjectiveTemplate ?? string.Empty);
            string openingObjective = pawnPrompt?.OpeningObjectiveTemplate ?? string.Empty;
            string topicShift = Owner.SelectStringField(diplomacyCustom, diplomacyDefault, "TopicShiftRuleTemplate", pawnPrompt?.TopicShiftRuleTemplate ?? string.Empty);
            string apiLimits = Owner.SelectStringField(diplomacyCustom, diplomacyDefault, "ApiLimitsNodeTemplate", PromptTextConstants.ApiLimitsNodeLiteralDefault);
            string questGuidance = Owner.SelectStringField(diplomacyCustom, diplomacyDefault, "QuestGuidanceNodeTemplate", PromptTextConstants.QuestGuidanceNodeLiteralDefault);
            string responseContract = Owner.SelectStringField(diplomacyCustom, diplomacyDefault, "ResponseContractNodeTemplate", PromptTextConstants.ResponseContractNodeLiteralDefault);
            string mandatoryRaceInjection = Owner.SelectStringField(diplomacyCustom, diplomacyDefault, "MandatoryRaceInjectionTemplate", string.Empty);

            return "{"
                + $"\"Enabled\":{enabled},"
                + $"\"FactGroundingTemplate\":\"{Owner.EscapeJson(factGrounding)}\","
                + $"\"OutputLanguageTemplate\":\"{Owner.EscapeJson(outputLanguage)}\","
                + $"\"DiplomacyFallbackRoleTemplate\":\"{Owner.EscapeJson(diplomacyFallback)}\","
                + $"\"SocialCircleActionRuleTemplate\":\"{Owner.EscapeJson(socialCircle)}\","
                + $"\"SocialCircleNewsStyleTemplate\":\"{Owner.EscapeJson(socialNewsStyle)}\","
                + $"\"SocialCircleNewsJsonContractTemplate\":\"{Owner.EscapeJson(socialNewsContract)}\","
                + $"\"SocialCircleNewsFactTemplate\":\"{Owner.EscapeJson(socialNewsFact)}\","
                + $"\"RpgRoleSettingTemplate\":\"{Owner.EscapeJson(pawnPrompt?.RpgRoleSettingTemplate ?? string.Empty)}\","
                + $"\"RpgCompactFormatConstraintTemplate\":\"{Owner.EscapeJson(pawnPrompt?.RpgCompactFormatConstraintTemplate ?? string.Empty)}\","
                + $"\"RpgActionReliabilityRuleTemplate\":\"{Owner.EscapeJson(pawnPrompt?.RpgActionReliabilityRuleTemplate ?? string.Empty)}\","
                + $"\"DecisionPolicyTemplate\":\"{Owner.EscapeJson(decisionPolicy)}\","
                + $"\"TurnObjectiveTemplate\":\"{Owner.EscapeJson(turnObjective)}\","
                + $"\"OpeningObjectiveTemplate\":\"{Owner.EscapeJson(openingObjective)}\","
                + $"\"TopicShiftRuleTemplate\":\"{Owner.EscapeJson(topicShift)}\","
                + $"\"ApiLimitsNodeTemplate\":\"{Owner.EscapeJson(apiLimits)}\","
                + $"\"QuestGuidanceNodeTemplate\":\"{Owner.EscapeJson(questGuidance)}\","
                + $"\"ResponseContractNodeTemplate\":\"{Owner.EscapeJson(responseContract)}\","
                + $"\"MandatoryRaceInjectionTemplate\":\"{Owner.EscapeJson(mandatoryRaceInjection)}\""
                + "}";
        }

internal void ApplyPawnPromptTemplates(SystemPromptConfig config, RpgPromptCustomConfig pawnPrompt)
        {
            if (config?.PromptTemplates == null || pawnPrompt == null)
            {
                return;
            }

            config.PromptTemplates.RpgRoleSettingTemplate = pawnPrompt.RpgRoleSettingTemplate ?? string.Empty;
            config.PromptTemplates.RpgCompactFormatConstraintTemplate = pawnPrompt.RpgCompactFormatConstraintTemplate ?? string.Empty;
            config.PromptTemplates.RpgActionReliabilityRuleTemplate = pawnPrompt.RpgActionReliabilityRuleTemplate ?? string.Empty;
            config.PromptTemplates.OpeningObjectiveTemplate = pawnPrompt.OpeningObjectiveTemplate ?? string.Empty;
        }

internal SystemPromptConfig ComposeConfigFromDomains(
            SystemPromptDomainConfig systemPrompt,
            DiplomacyDialoguePromptDomainConfig diplomacyPrompt,
            RpgPromptCustomConfig pawnPrompt,
            SocialCirclePromptDomainConfig socialPrompt)
        {
            var config = new SystemPromptConfig
            {
                ConfigName = systemPrompt?.ConfigName ?? "Default",
                GlobalSystemPrompt = systemPrompt?.GlobalSystemPrompt ?? string.Empty,
                GlobalDialoguePrompt = diplomacyPrompt?.GlobalDialoguePrompt ?? string.Empty,
                UseAdvancedMode = systemPrompt?.UseAdvancedMode ?? false,
                UseHierarchicalPromptFormat = systemPrompt?.UseHierarchicalPromptFormat ?? true,
                Enabled = systemPrompt?.Enabled ?? true,
                PromptSchemaVersion = systemPrompt?.PromptSchemaVersion ?? SystemPromptConfig.CurrentPromptSchemaVersion,
                PromptPolicySchemaVersion = systemPrompt?.PromptPolicySchemaVersion ?? SystemPromptConfig.CurrentPromptPolicySchemaVersion,
                ResponseFormat = diplomacyPrompt?.ResponseFormat?.Clone() ?? new ResponseFormatConfig(),
                EnvironmentPrompt = systemPrompt?.EnvironmentPrompt?.Clone() ?? new EnvironmentPromptConfig(),
                DynamicDataInjection = systemPrompt?.DynamicDataInjection?.Clone() ?? new DynamicDataInjectionConfig(),
                PromptTemplates = Owner.BuildPromptTemplates(diplomacyPrompt, pawnPrompt, socialPrompt),
                PromptPolicy = systemPrompt?.PromptPolicy?.Clone() ?? PromptPolicyConfig.CreateDefault(),
                ApiActions = Owner.BuildApiActions(diplomacyPrompt),
                DecisionRules = Owner.CloneDecisionRules(diplomacyPrompt?.DecisionRules)
            };

            return config;
        }

internal SystemPromptDomainConfig LoadSystemPromptDomain(bool includeCustom)
        {
            string customPath = includeCustom
                ? PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.SystemPromptCustomFileName)
                : string.Empty;
            return PromptDomainJsonUtility.LoadMerged<SystemPromptDomainConfig>(
                PromptDomainFileCatalog.GetDefaultPath(PromptDomainFileCatalog.SystemPromptDefaultFileName),
                customPath);
        }

internal DiplomacyDialoguePromptDomainConfig LoadDiplomacyPromptDomain(bool includeCustom)
        {
            string customPath = includeCustom
                ? PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.DiplomacyPromptCustomFileName)
                : string.Empty;
            return PromptDomainJsonUtility.LoadMerged<DiplomacyDialoguePromptDomainConfig>(
                PromptDomainFileCatalog.GetDefaultPath(PromptDomainFileCatalog.DiplomacyPromptDefaultFileName),
                customPath);
        }

internal SocialCirclePromptDomainConfig LoadSocialCirclePromptDomain(bool includeCustom)
        {
            string customPath = includeCustom
                ? PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.SocialCirclePromptCustomFileName)
                : string.Empty;
            return PromptDomainJsonUtility.LoadMerged<SocialCirclePromptDomainConfig>(
                PromptDomainFileCatalog.GetDefaultPath(PromptDomainFileCatalog.SocialCirclePromptDefaultFileName),
                customPath);
        }

internal List<string> ValidateDomainConfigSemantics(SystemPromptConfig config)
        {
            var errors = new List<string>();
            if (config == null)
            {
                errors.Add("ConfigMissing");
                return errors;
            }

            if (string.IsNullOrWhiteSpace(config.GlobalSystemPrompt))
            {
                errors.Add("GlobalSystemPromptMissing");
            }

            var validActions = (config.ApiActions ?? new List<ApiActionConfig>())
                .Where(action => action != null && !string.IsNullOrWhiteSpace(action.ActionName))
                .Select(action => action.ActionName.Trim())
                .ToList();
            if (validActions.Count == 0)
            {
                errors.Add("ApiActionsEmpty");
            }

            HashSet<string> requiredActions = Owner.ResolveDiplomacyCoreActionNamesFromDefault();
            if (requiredActions.Count == 0)
            {
                errors.Add("CoreApiActionsDefaultMissing");
            }

            foreach (string required in requiredActions)
            {
                bool exists = validActions.Any(actionName =>
                    string.Equals(actionName, required, StringComparison.OrdinalIgnoreCase));
                if (!exists)
                {
                    errors.Add("MissingApiAction:" + required);
                }
            }

            if (string.IsNullOrWhiteSpace(config.ResponseFormat?.JsonTemplate))
            {
                errors.Add("ResponseFormat.JsonTemplateMissing");
            }

            if (config.PromptTemplates == null)
            {
                errors.Add("PromptTemplatesMissing");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(config.PromptTemplates.ApiLimitsNodeTemplate))
                {
                    errors.Add("PromptTemplates.ApiLimitsNodeTemplateMissing");
                }

                if (string.IsNullOrWhiteSpace(config.PromptTemplates.QuestGuidanceNodeTemplate))
                {
                    errors.Add("PromptTemplates.QuestGuidanceNodeTemplateMissing");
                }

                if (string.IsNullOrWhiteSpace(config.PromptTemplates.ResponseContractNodeTemplate))
                {
                    errors.Add("PromptTemplates.ResponseContractNodeTemplateMissing");
                }
            }

            if (config.PromptPolicy == null)
            {
                errors.Add("PromptPolicyMissing");
            }

            return errors;
        }

internal HashSet<string> ResolveDiplomacyCoreActionNamesFromDefault()
        {
            DiplomacyDialoguePromptDomainConfig defaults = Owner.LoadDiplomacyPromptDomain(includeCustom: false);
            var required = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ApiActionConfig action in defaults?.ApiActions ?? Enumerable.Empty<ApiActionConfig>())
            {
                string name = action?.ActionName?.Trim();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    required.Add(name);
                }
            }

            return required;
        }

internal PromptTemplateTextConfig BuildPromptTemplates(
            DiplomacyDialoguePromptDomainConfig diplomacyPrompt,
            RpgPromptCustomConfig pawnPrompt,
            SocialCirclePromptDomainConfig socialPrompt)
        {
            return new PromptTemplateTextConfig
            {
                Enabled = diplomacyPrompt?.PromptTemplatesEnabled ?? true,
                FactGroundingTemplate = diplomacyPrompt?.FactGroundingTemplate ?? string.Empty,
                OutputLanguageTemplate = diplomacyPrompt?.OutputLanguageTemplate ?? string.Empty,
                DiplomacyFallbackRoleTemplate = diplomacyPrompt?.DiplomacyFallbackRoleTemplate ?? string.Empty,
                SocialCircleActionRuleTemplate = socialPrompt?.SocialCircleActionRuleTemplate ?? string.Empty,
                SocialCircleNewsStyleTemplate = socialPrompt?.SocialCircleNewsStyleTemplate ?? string.Empty,
                SocialCircleNewsJsonContractTemplate = socialPrompt?.SocialCircleNewsJsonContractTemplate ?? string.Empty,
                SocialCircleNewsFactTemplate = socialPrompt?.SocialCircleNewsFactTemplate ?? string.Empty,
                RpgRoleSettingTemplate = pawnPrompt?.RpgRoleSettingTemplate ?? string.Empty,
                RpgCompactFormatConstraintTemplate = pawnPrompt?.RpgCompactFormatConstraintTemplate ?? string.Empty,
                RpgActionReliabilityRuleTemplate = pawnPrompt?.RpgActionReliabilityRuleTemplate ?? string.Empty,
                DecisionPolicyTemplate = !string.IsNullOrWhiteSpace(diplomacyPrompt?.DecisionPolicyTemplate)
                    ? diplomacyPrompt.DecisionPolicyTemplate
                    : pawnPrompt?.DecisionPolicyTemplate ?? string.Empty,
                TurnObjectiveTemplate = !string.IsNullOrWhiteSpace(diplomacyPrompt?.TurnObjectiveTemplate)
                    ? diplomacyPrompt.TurnObjectiveTemplate
                    : pawnPrompt?.TurnObjectiveTemplate ?? string.Empty,
                OpeningObjectiveTemplate = pawnPrompt?.OpeningObjectiveTemplate ?? string.Empty,
                TopicShiftRuleTemplate = !string.IsNullOrWhiteSpace(diplomacyPrompt?.TopicShiftRuleTemplate)
                    ? diplomacyPrompt.TopicShiftRuleTemplate
                    : pawnPrompt?.TopicShiftRuleTemplate ?? string.Empty,
                ApiLimitsNodeTemplate = diplomacyPrompt?.ApiLimitsNodeTemplate ?? PromptTextConstants.ApiLimitsNodeLiteralDefault,
                QuestGuidanceNodeTemplate = diplomacyPrompt?.QuestGuidanceNodeTemplate ?? PromptTextConstants.QuestGuidanceNodeLiteralDefault,
                ResponseContractNodeTemplate = diplomacyPrompt?.ResponseContractNodeTemplate ?? PromptTextConstants.ResponseContractNodeLiteralDefault,
                MandatoryRaceInjectionTemplate = diplomacyPrompt?.MandatoryRaceInjectionTemplate ?? string.Empty
            };
        }
    }
}
