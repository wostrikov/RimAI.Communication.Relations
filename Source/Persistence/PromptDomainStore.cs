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
internal sealed partial class PromptDomainStore
    {
        private readonly PromptPersistenceService host;

        internal PromptDomainStore(PromptPersistenceService host)
        {
            this.host = host ?? throw new System.ArgumentNullException(nameof(host));
            _configJsonCodec = new PromptConfigJsonCodec();
        }

        internal const int CurrentPromptDomainSchemaVersion = 1;
        private readonly PromptConfigJsonCodec _configJsonCodec;
        private SystemPromptConfig _cachedConfig;
        private DateTime _cachedConfigWriteTimeUtc = DateTime.MinValue;
        private bool _hasPendingPromptDomainRepairs;
        private readonly object _typedParseWarningLock = new object();
        private readonly HashSet<int> _typedParseIncompleteWarningHashes = new HashSet<int>();
        private readonly HashSet<int> _typedParseFailureWarningHashes = new HashSet<int>();
        private readonly HashSet<int> _typedParseRecoveredInfoHashes = new HashSet<int>();

        internal SystemPromptConfig CachedConfig
        {
            get => _cachedConfig;
            set => _cachedConfig = value;
        }

        internal DateTime CachedConfigWriteTimeUtc
        {
            get => _cachedConfigWriteTimeUtc;
            set => _cachedConfigWriteTimeUtc = value;
        }

        internal string BasePath => host.BasePath;
        internal string ConfigFilePath => host.ConfigFilePath;

        internal void InvalidateCache()
        {
            _cachedConfig = null;
            _cachedConfigWriteTimeUtc = DateTime.MinValue;
        }


        private static readonly string[] CustomPromptDomainFiles =
        {
            PromptDomainFileCatalog.SystemPromptCustomFileName,
            PromptDomainFileCatalog.DiplomacyPromptCustomFileName,
            PromptDomainFileCatalog.PawnPromptCustomFileName,
            PromptDomainFileCatalog.SocialCirclePromptCustomFileName
        };

        internal bool TryLoadPromptDomains(out SystemPromptConfig config)
        {
            return TryLoadPromptDomains(
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
            SystemPromptDomainConfig systemPrompt = LoadSystemPromptDomain(includeCustom);
            DiplomacyDialoguePromptDomainConfig diplomacyPrompt = LoadDiplomacyPromptDomain(includeCustom);
            SocialCirclePromptDomainConfig socialPrompt = LoadSocialCirclePromptDomain(includeCustom);
            RpgPromptCustomConfig pawnPrompt = includeCustom
                ? RpgPromptCustomStore.LoadOrDefault()
                : RpgPromptCustomStore.LoadDefaultsOnly();
            loadedDomainSchemaVersion = systemPrompt?.PromptDomainSchemaVersion ?? 0;
            config = ComposeConfigFromDomains(systemPrompt, diplomacyPrompt, pawnPrompt, socialPrompt);
            validationErrors = ValidateDomainConfigSemantics(config);

            if (!includeCustom && validationErrors.Count > 0)
            {
                if (TryRehydrateFromAggregateDomainJson(includeCustom: false, out SystemPromptConfig reparsedConfig, out List<string> reparsedErrors))
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
            string aggregateJson = BuildAggregateConfigJsonFromDomainFiles(includeCustom);
            if (string.IsNullOrWhiteSpace(aggregateJson))
            {
                return false;
            }

            config = ParseJsonToConfigInternal(
                aggregateJson,
                includeCustom ? "aggregate_domains_custom" : "aggregate_domains_default_only");
            validationErrors = ValidateDomainConfigSemantics(config);
            return validationErrors.Count == 0;
        }

        internal string BuildAggregateConfigJsonFromDomainFiles(bool includeCustom)
        {
            string systemDefault = ReadDomainJson(PromptDomainFileCatalog.GetDefaultPath(PromptDomainFileCatalog.SystemPromptDefaultFileName));
            string diplomacyDefault = ReadDomainJson(PromptDomainFileCatalog.GetDefaultPath(PromptDomainFileCatalog.DiplomacyPromptDefaultFileName));
            string socialDefault = ReadDomainJson(PromptDomainFileCatalog.GetDefaultPath(PromptDomainFileCatalog.SocialCirclePromptDefaultFileName));
            if (string.IsNullOrWhiteSpace(systemDefault) || string.IsNullOrWhiteSpace(diplomacyDefault) || string.IsNullOrWhiteSpace(socialDefault))
            {
                return string.Empty;
            }

            string systemCustom = includeCustom
                ? ReadDomainJson(PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.SystemPromptCustomFileName))
                : string.Empty;
            string diplomacyCustom = includeCustom
                ? ReadDomainJson(PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.DiplomacyPromptCustomFileName))
                : string.Empty;
            string socialCustom = includeCustom
                ? ReadDomainJson(PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.SocialCirclePromptCustomFileName))
                : string.Empty;
            RpgPromptCustomConfig pawnPrompt = includeCustom
                ? RpgPromptCustomStore.LoadOrDefault()
                : RpgPromptCustomStore.LoadDefaultsOnly();

            string configName = SelectStringField(systemCustom, systemDefault, "ConfigName", "Default");
            string globalSystemPrompt = SelectStringField(systemCustom, systemDefault, "GlobalSystemPrompt", string.Empty);
            string globalDialoguePrompt = SelectStringField(diplomacyCustom, diplomacyDefault, "GlobalDialoguePrompt", string.Empty);
            string useAdvancedMode = SelectValueField(systemCustom, systemDefault, "UseAdvancedMode", "false");
            string useHierarchical = SelectValueField(systemCustom, systemDefault, "UseHierarchicalPromptFormat", "true");
            string enabled = SelectValueField(systemCustom, systemDefault, "Enabled", "true");
            string promptDomainSchemaVersion = SelectValueField(systemCustom, systemDefault, "PromptDomainSchemaVersion", CurrentPromptDomainSchemaVersion.ToString());
            string promptSchemaVersion = SelectValueField(systemCustom, systemDefault, "PromptSchemaVersion", SystemPromptConfig.CurrentPromptSchemaVersion.ToString());
            string schemaVersion = SelectValueField(systemCustom, systemDefault, "PromptPolicySchemaVersion", SystemPromptConfig.CurrentPromptPolicySchemaVersion.ToString());
            string apiActions = SelectArraySection(diplomacyCustom, diplomacyDefault, "ApiActions", "[]");
            string responseFormat = SelectObjectSection(diplomacyCustom, diplomacyDefault, "ResponseFormat", "{}");
            string decisionRules = SelectArraySection(diplomacyCustom, diplomacyDefault, "DecisionRules", "[]");
            string environmentPrompt = SelectObjectSection(systemCustom, systemDefault, "EnvironmentPrompt", "{}");
            string promptTemplates = BuildPromptTemplatesJson(diplomacyCustom, diplomacyDefault, socialCustom, socialDefault, pawnPrompt);
            string promptPolicy = SelectObjectSection(systemCustom, systemDefault, "PromptPolicy", "{}");
            string dynamicData = SelectObjectSection(systemCustom, systemDefault, "DynamicDataInjection", "{}");

            return "{"
                + $"\"ConfigName\":\"{EscapeJson(configName)}\","
                + $"\"GlobalSystemPrompt\":\"{EscapeJson(globalSystemPrompt)}\","
                + $"\"GlobalDialoguePrompt\":\"{EscapeJson(globalDialoguePrompt)}\","
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

        internal string ReadDomainJson(string path)
        {
            return PromptConfigStore.ReadAllText(path);
        }

        internal string SelectStringField(string customJson, string defaultJson, string key, string fallback)
        {
            if (PromptJsonText.ContainsJsonKey(customJson, key))
            {
                return ExtractString(customJson, key);
            }

            if (PromptJsonText.ContainsJsonKey(defaultJson, key))
            {
                return ExtractString(defaultJson, key);
            }

            return fallback ?? string.Empty;
        }

        internal string SelectValueField(string customJson, string defaultJson, string key, string fallback)
        {
            if (PromptJsonText.ContainsJsonKey(customJson, key))
            {
                return ExtractValue(customJson, key);
            }

            if (PromptJsonText.ContainsJsonKey(defaultJson, key))
            {
                return ExtractValue(defaultJson, key);
            }

            return fallback;
        }

        internal string SelectObjectSection(string customJson, string defaultJson, string key, string fallback)
        {
            if (TryExtractJsonObject(customJson, key, out string customSection))
            {
                return "{" + customSection + "}";
            }

            if (TryExtractJsonObject(defaultJson, key, out string defaultSection))
            {
                return "{" + defaultSection + "}";
            }

            return fallback;
        }

        internal string SelectArraySection(string customJson, string defaultJson, string key, string fallback)
        {
            if (TryExtractJsonArray(customJson, key, out string customSection))
            {
                return "[" + customSection + "]";
            }

            if (TryExtractJsonArray(defaultJson, key, out string defaultSection))
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
            string enabled = SelectValueField(diplomacyCustom, diplomacyDefault, "PromptTemplatesEnabled", "true");
            string factGrounding = SelectStringField(diplomacyCustom, diplomacyDefault, "FactGroundingTemplate", string.Empty);
            string outputLanguage = SelectStringField(diplomacyCustom, diplomacyDefault, "OutputLanguageTemplate", string.Empty);
            string diplomacyFallback = SelectStringField(diplomacyCustom, diplomacyDefault, "DiplomacyFallbackRoleTemplate", string.Empty);
            string socialCircle = SelectStringField(socialCustom, socialDefault, "SocialCircleActionRuleTemplate", string.Empty);
            string socialNewsStyle = SelectStringField(socialCustom, socialDefault, "SocialCircleNewsStyleTemplate", string.Empty);
            string socialNewsContract = SelectStringField(socialCustom, socialDefault, "SocialCircleNewsJsonContractTemplate", string.Empty);
            string socialNewsFact = SelectStringField(socialCustom, socialDefault, "SocialCircleNewsFactTemplate", string.Empty);
            string decisionPolicy = SelectStringField(diplomacyCustom, diplomacyDefault, "DecisionPolicyTemplate", pawnPrompt?.DecisionPolicyTemplate ?? string.Empty);
            string turnObjective = SelectStringField(diplomacyCustom, diplomacyDefault, "TurnObjectiveTemplate", pawnPrompt?.TurnObjectiveTemplate ?? string.Empty);
            string openingObjective = pawnPrompt?.OpeningObjectiveTemplate ?? string.Empty;
            string topicShift = SelectStringField(diplomacyCustom, diplomacyDefault, "TopicShiftRuleTemplate", pawnPrompt?.TopicShiftRuleTemplate ?? string.Empty);
            string apiLimits = SelectStringField(diplomacyCustom, diplomacyDefault, "ApiLimitsNodeTemplate", PromptTextConstants.ApiLimitsNodeLiteralDefault);
            string questGuidance = SelectStringField(diplomacyCustom, diplomacyDefault, "QuestGuidanceNodeTemplate", PromptTextConstants.QuestGuidanceNodeLiteralDefault);
            string responseContract = SelectStringField(diplomacyCustom, diplomacyDefault, "ResponseContractNodeTemplate", PromptTextConstants.ResponseContractNodeLiteralDefault);
            string mandatoryRaceInjection = SelectStringField(diplomacyCustom, diplomacyDefault, "MandatoryRaceInjectionTemplate", string.Empty);

            return "{"
                + $"\"Enabled\":{enabled},"
                + $"\"FactGroundingTemplate\":\"{EscapeJson(factGrounding)}\","
                + $"\"OutputLanguageTemplate\":\"{EscapeJson(outputLanguage)}\","
                + $"\"DiplomacyFallbackRoleTemplate\":\"{EscapeJson(diplomacyFallback)}\","
                + $"\"SocialCircleActionRuleTemplate\":\"{EscapeJson(socialCircle)}\","
                + $"\"SocialCircleNewsStyleTemplate\":\"{EscapeJson(socialNewsStyle)}\","
                + $"\"SocialCircleNewsJsonContractTemplate\":\"{EscapeJson(socialNewsContract)}\","
                + $"\"SocialCircleNewsFactTemplate\":\"{EscapeJson(socialNewsFact)}\","
                + $"\"RpgRoleSettingTemplate\":\"{EscapeJson(pawnPrompt?.RpgRoleSettingTemplate ?? string.Empty)}\","
                + $"\"RpgCompactFormatConstraintTemplate\":\"{EscapeJson(pawnPrompt?.RpgCompactFormatConstraintTemplate ?? string.Empty)}\","
                + $"\"RpgActionReliabilityRuleTemplate\":\"{EscapeJson(pawnPrompt?.RpgActionReliabilityRuleTemplate ?? string.Empty)}\","
                + $"\"DecisionPolicyTemplate\":\"{EscapeJson(decisionPolicy)}\","
                + $"\"TurnObjectiveTemplate\":\"{EscapeJson(turnObjective)}\","
                + $"\"OpeningObjectiveTemplate\":\"{EscapeJson(openingObjective)}\","
                + $"\"TopicShiftRuleTemplate\":\"{EscapeJson(topicShift)}\","
                + $"\"ApiLimitsNodeTemplate\":\"{EscapeJson(apiLimits)}\","
                + $"\"QuestGuidanceNodeTemplate\":\"{EscapeJson(questGuidance)}\","
                + $"\"ResponseContractNodeTemplate\":\"{EscapeJson(responseContract)}\","
                + $"\"MandatoryRaceInjectionTemplate\":\"{EscapeJson(mandatoryRaceInjection)}\""
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
                PromptTemplates = BuildPromptTemplates(diplomacyPrompt, pawnPrompt, socialPrompt),
                PromptPolicy = systemPrompt?.PromptPolicy?.Clone() ?? PromptPolicyConfig.CreateDefault(),
                ApiActions = BuildApiActions(diplomacyPrompt),
                DecisionRules = CloneDecisionRules(diplomacyPrompt?.DecisionRules)
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

            HashSet<string> requiredActions = ResolveDiplomacyCoreActionNamesFromDefault();
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
            DiplomacyDialoguePromptDomainConfig defaults = LoadDiplomacyPromptDomain(includeCustom: false);
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

        internal List<ApiActionConfig> BuildApiActions(
            DiplomacyDialoguePromptDomainConfig diplomacyPrompt)
        {
            List<ApiActionConfig> actions = CloneApiActions(diplomacyPrompt?.ApiActions);
            EnsureRequiredRaidVariantActions(actions);
            return actions;
        }

        internal void EnsureRequiredRaidVariantActions(List<ApiActionConfig> actions)
        {
            if (actions == null)
            {
                return;
            }

            EnsureAction(
                actions,
                "request_raid_call_everyone",
                PromptTextConstants.RequestRaidCallEveryoneActionDescription,
                string.Empty,
                PromptTextConstants.RequestRaidCallEveryoneActionRequirement);

            EnsureAction(
                actions,
                "request_raid_waves",
                PromptTextConstants.RequestRaidWavesActionDescription,
                PromptTextConstants.RequestRaidWavesActionParameters,
                PromptTextConstants.RequestRaidWavesActionRequirement);
        }

        internal void EnsureAction(
            List<ApiActionConfig> actions,
            string actionName,
            string description,
            string parameters,
            string requirement)
        {
            ApiActionConfig existing = actions.FirstOrDefault(item =>
                string.Equals(item?.ActionName, actionName, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                if (string.IsNullOrWhiteSpace(existing.Description)) existing.Description = description;
                if (string.IsNullOrWhiteSpace(existing.Parameters)) existing.Parameters = parameters;
                if (string.IsNullOrWhiteSpace(existing.Requirement)) existing.Requirement = requirement;
                return;
            }

            actions.Add(new ApiActionConfig(actionName, description, parameters, requirement));
        }

        internal List<ApiActionConfig> CloneApiActions(IEnumerable<ApiActionConfig> actions)
        {
            return actions?.Where(item => item != null).Select(item => item.Clone()).ToList()
                ?? new List<ApiActionConfig>();
        }

        internal List<DecisionRuleConfig> CloneDecisionRules(IEnumerable<DecisionRuleConfig> rules)
        {
            return rules?.Where(item => item != null).Select(item => item.Clone()).ToList()
                ?? new List<DecisionRuleConfig>();
        }

        internal SystemPromptDomainConfig BuildSystemPromptDomain(SystemPromptConfig config)
        {
            return new SystemPromptDomainConfig
            {
                ConfigName = config?.ConfigName ?? "Default",
                GlobalSystemPrompt = config?.GlobalSystemPrompt ?? string.Empty,
                UseAdvancedMode = config?.UseAdvancedMode ?? false,
                UseHierarchicalPromptFormat = config?.UseHierarchicalPromptFormat ?? true,
                Enabled = config?.Enabled ?? true,
                PromptDomainSchemaVersion = CurrentPromptDomainSchemaVersion,
                PromptSchemaVersion = config?.PromptSchemaVersion ?? SystemPromptConfig.CurrentPromptSchemaVersion,
                PromptPolicySchemaVersion = config?.PromptPolicySchemaVersion ?? SystemPromptConfig.CurrentPromptPolicySchemaVersion,
                EnvironmentPrompt = config?.EnvironmentPrompt?.Clone() ?? new EnvironmentPromptConfig(),
                DynamicDataInjection = config?.DynamicDataInjection?.Clone() ?? new DynamicDataInjectionConfig(),
                PromptPolicy = config?.PromptPolicy?.Clone() ?? PromptPolicyConfig.CreateDefault()
            };
        }

        internal DiplomacyDialoguePromptDomainConfig BuildDiplomacyPromptDomain(SystemPromptConfig config)
        {
            return new DiplomacyDialoguePromptDomainConfig
            {
                GlobalDialoguePrompt = config?.GlobalDialoguePrompt ?? string.Empty,
                PromptTemplatesEnabled = config?.PromptTemplates?.Enabled ?? true,
                ResponseFormat = config?.ResponseFormat?.Clone() ?? new ResponseFormatConfig(),
                DecisionRules = CloneDecisionRules(config?.DecisionRules),
                ApiActions = CloneDiplomacyActions(config?.ApiActions),
                FactGroundingTemplate = config?.PromptTemplates?.FactGroundingTemplate ?? string.Empty,
                OutputLanguageTemplate = config?.PromptTemplates?.OutputLanguageTemplate ?? string.Empty,
                DiplomacyFallbackRoleTemplate = config?.PromptTemplates?.DiplomacyFallbackRoleTemplate ?? string.Empty,
                DecisionPolicyTemplate = config?.PromptTemplates?.DecisionPolicyTemplate ?? string.Empty,
                TurnObjectiveTemplate = config?.PromptTemplates?.TurnObjectiveTemplate ?? string.Empty,
                TopicShiftRuleTemplate = config?.PromptTemplates?.TopicShiftRuleTemplate ?? string.Empty,
                ApiLimitsNodeTemplate = config?.PromptTemplates?.ApiLimitsNodeTemplate ?? PromptTextConstants.ApiLimitsNodeLiteralDefault,
                QuestGuidanceNodeTemplate = config?.PromptTemplates?.QuestGuidanceNodeTemplate ?? PromptTextConstants.QuestGuidanceNodeLiteralDefault,
                ResponseContractNodeTemplate = config?.PromptTemplates?.ResponseContractNodeTemplate ?? PromptTextConstants.ResponseContractNodeLiteralDefault,
                MandatoryRaceInjectionTemplate = config?.PromptTemplates?.MandatoryRaceInjectionTemplate ?? string.Empty
            };
        }

        internal List<ApiActionConfig> CloneDiplomacyActions(IEnumerable<ApiActionConfig> actions)
        {
            return actions?
                .Where(item => item != null &&
                    !string.Equals(item.ActionName, "publish_public_post", StringComparison.OrdinalIgnoreCase))
                .Select(item => item.Clone())
                .ToList()
                ?? new List<ApiActionConfig>();
        }

        internal SocialCirclePromptDomainConfig BuildSocialCirclePromptDomain(SystemPromptConfig config)
        {
            return new SocialCirclePromptDomainConfig
            {
                SocialCircleActionRuleTemplate = config?.PromptTemplates?.SocialCircleActionRuleTemplate ?? string.Empty,
                SocialCircleNewsStyleTemplate = config?.PromptTemplates?.SocialCircleNewsStyleTemplate ?? string.Empty,
                SocialCircleNewsJsonContractTemplate = config?.PromptTemplates?.SocialCircleNewsJsonContractTemplate ?? string.Empty,
                SocialCircleNewsFactTemplate = config?.PromptTemplates?.SocialCircleNewsFactTemplate ?? string.Empty,
                PublishPublicPostAction = new ApiActionConfig(
                    "publish_public_post",
                    PromptTextConstants.PublishPublicPostActionDescription,
                    PromptTextConstants.PublishPublicPostActionParameters,
                    PromptTextConstants.PublishPublicPostActionRequirement)
            };
        }

        internal void SavePromptDomainFiles(SystemPromptConfig config)
        {
            PromptDomainFileCatalog.EnsureCustomDirectoryExists();
            PromptDomainJsonUtility.WriteToFile(
                PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.SystemPromptCustomFileName),
                BuildSystemPromptDomain(config));
            PromptDomainJsonUtility.WriteToFile(
                PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.DiplomacyPromptCustomFileName),
                BuildDiplomacyPromptDomain(config));
            PromptDomainJsonUtility.WriteToFile(
                PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.SocialCirclePromptCustomFileName),
                BuildSocialCirclePromptDomain(config));
        }

        internal void DeletePromptDomainCustomFiles()
        {
            foreach (string fileName in CustomPromptDomainFiles)
            {
                DeleteCustomPromptFile(fileName);
            }
        }

        internal void DeleteCustomPromptFile(string fileName)
        {
            string path = PromptDomainFileCatalog.GetCustomPath(fileName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        internal bool HasAnyCustomDomainFile()
        {
            return CustomPromptDomainFiles.Any(fileName =>
                File.Exists(PromptDomainFileCatalog.GetCustomPath(fileName)));
        }

        internal bool TryGetDomainConfigLastWriteTimeUtc(out DateTime writeTimeUtc)
        {
            writeTimeUtc = DateTime.MinValue;
            bool found = false;
            foreach (string path in GetTrackedPromptPaths())
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                DateTime current = File.GetLastWriteTimeUtc(path);
                if (!found || current > writeTimeUtc)
                {
                    writeTimeUtc = current;
                    found = true;
                }
            }

            return found;
        }

        internal IEnumerable<string> GetTrackedPromptPaths()
        {
            yield return PromptDomainFileCatalog.GetDefaultPath(PromptDomainFileCatalog.SystemPromptDefaultFileName);
            yield return PromptDomainFileCatalog.GetDefaultPath(PromptDomainFileCatalog.DiplomacyPromptDefaultFileName);
            yield return PromptDomainFileCatalog.GetDefaultPath(PromptDomainFileCatalog.PawnPromptDefaultFileName);
            yield return PromptDomainFileCatalog.GetDefaultPath(PromptDomainFileCatalog.SocialCirclePromptDefaultFileName);
            yield return PromptDomainFileCatalog.GetDefaultPath(PromptDomainFileCatalog.PromptUnifiedDefaultFileName);
            yield return PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.SystemPromptCustomFileName);
            yield return PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.DiplomacyPromptCustomFileName);
            yield return PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.PawnPromptCustomFileName);
            yield return PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.SocialCirclePromptCustomFileName);
            yield return PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.PromptUnifiedCustomFileName);
        }

    }
}
