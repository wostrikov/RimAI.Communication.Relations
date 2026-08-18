using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using Verse;
using Ustas.RimAI.Communication.Relations.Prompting.Transfer;
using Ustas.RimAI.Communication.Relations.Serialization;
using System.Text;
using Ustas.RimAI.Communication.Relations.Prompting;

namespace Ustas.RimAI.Communication.Relations.Persistence
{
internal sealed class PromptDomainStore
    {
        internal PromptDomainStoreParts Parts;

        internal readonly PromptPersistenceService host;

        internal PromptDomainStore(PromptPersistenceService host)
        {
            Parts = new PromptDomainStoreParts(this);
            this.host = host ?? throw new System.ArgumentNullException(nameof(host));
            _configJsonCodec = new PromptConfigJsonCodec();
        }

        internal const int CurrentPromptDomainSchemaVersion = 1;
        internal readonly PromptConfigJsonCodec _configJsonCodec;
        internal SystemPromptConfig _cachedConfig;
        internal DateTime _cachedConfigWriteTimeUtc = DateTime.MinValue;
        internal bool _hasPendingPromptDomainRepairs;
        internal readonly object _typedParseWarningLock = new object();
        internal readonly HashSet<int> _typedParseIncompleteWarningHashes = new HashSet<int>();
        internal readonly HashSet<int> _typedParseFailureWarningHashes = new HashSet<int>();
        internal readonly HashSet<int> _typedParseRecoveredInfoHashes = new HashSet<int>();

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

        


        internal static readonly string[] CustomPromptDomainFiles =
        {
            PromptDomainFileCatalog.SystemPromptCustomFileName,
            PromptDomainFileCatalog.DiplomacyPromptCustomFileName,
            PromptDomainFileCatalog.PawnPromptCustomFileName,
            PromptDomainFileCatalog.SocialCirclePromptCustomFileName
        };

        

        

        

        

        internal string ReadDomainJson(string path)
        {
            return PromptConfigStore.ReadAllText(path);
        }

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        


        #region Facade forwards
        public bool ConfigExists() => Parts.Lifecycle.ConfigExists();
        internal bool TryGetConfigLastWriteTimeUtc(out DateTime writeTimeUtc) => Parts.Lifecycle.TryGetConfigLastWriteTimeUtc(out writeTimeUtc);
        internal bool IsConfigCacheFresh(out DateTime writeTimeUtc) => Parts.Lifecycle.IsConfigCacheFresh(out writeTimeUtc);
        internal bool IsPlaceholderGlobalSystemPrompt(SystemPromptConfig config) => Parts.Lifecycle.IsPlaceholderGlobalSystemPrompt(config);
        internal bool TryRepairPlaceholderGlobalSystemPrompt(ref SystemPromptConfig config, string sourceLabel) => Parts.Lifecycle.TryRepairPlaceholderGlobalSystemPrompt(ref config, sourceLabel);
        public SystemPromptConfig LoadConfig() => Parts.Lifecycle.LoadConfig();
        public SystemPromptConfig LoadConfigReadOnly() => Parts.Lifecycle.LoadConfigReadOnly();
        public bool RepairAndRewritePromptDomains() => Parts.Lifecycle.RepairAndRewritePromptDomains();
        internal SystemPromptConfig LoadConfigInternal(bool saveRepairsWhenNeeded, out bool repaired) => Parts.Lifecycle.LoadConfigInternal(saveRepairsWhenNeeded, out repaired);
        public void SaveConfig(SystemPromptConfig config) => Parts.Lifecycle.SaveConfig(config);
        public void ResetToDefault() => Parts.Lifecycle.ResetToDefault();
        internal void EnsureDirectoryExists() => Parts.Serialization.EnsureDirectoryExists();
        internal bool HasAnyPromptCustomOverrideFile() => Parts.Serialization.HasAnyPromptCustomOverrideFile();
        internal IEnumerable<string> EnumeratePromptDomainCustomOverridePaths() => Parts.Serialization.EnumeratePromptDomainCustomOverridePaths();
        internal SystemPromptConfig CreateDefaultConfig() => Parts.Serialization.CreateDefaultConfig();
        internal PromptRenderException CreateDefaultConfigLoadFailureException(string reason, Exception innerException) => Parts.Serialization.CreateDefaultConfigLoadFailureException(reason, innerException);
        internal string BuildDefaultDomainDiagnosticSnapshot() => Parts.Serialization.BuildDefaultDomainDiagnosticSnapshot();
        internal string BuildPathSummary(string path) => Parts.Serialization.BuildPathSummary(path);
        internal string SerializeConfigToJson(SystemPromptConfig config, bool prettyPrint = false) => Parts.Serialization.SerializeConfigToJson(config, prettyPrint);
        internal bool IsTypedJsonComplete(string json, SystemPromptConfig config) => Parts.Serialization.IsTypedJsonComplete(json, config);
        internal void SerializeApiActions(StringBuilder sb, List<ApiActionConfig> actions, bool prettyPrint) => Parts.Serialization.SerializeApiActions(sb, actions, prettyPrint);
        internal void SerializeResponseFormat(StringBuilder sb, ResponseFormatConfig format, bool prettyPrint) => Parts.Serialization.SerializeResponseFormat(sb, format, prettyPrint);
        internal void SerializeDecisionRules(StringBuilder sb, List<DecisionRuleConfig> rules, bool prettyPrint) => Parts.Serialization.SerializeDecisionRules(sb, rules, prettyPrint);
        internal void SerializeDynamicDataInjection(StringBuilder sb, DynamicDataInjectionConfig config, bool prettyPrint) => Parts.Serialization.SerializeDynamicDataInjection(sb, config, prettyPrint);
        internal void SerializePromptTemplates(StringBuilder sb, PromptTemplateTextConfig templates, bool prettyPrint) => Parts.Serialization.SerializePromptTemplates(sb, templates, prettyPrint);
        internal void SerializePromptPolicy(StringBuilder sb, PromptPolicyConfig policy, bool prettyPrint) => Parts.Serialization.SerializePromptPolicy(sb, policy, prettyPrint);
        internal void SerializeEnvironmentPrompt(StringBuilder sb, EnvironmentPromptConfig environment, bool prettyPrint) => Parts.Serialization.SerializeEnvironmentPrompt(sb, environment, prettyPrint);
        internal string SerializeStringList(List<string> values) => Parts.Serialization.SerializeStringList(values);
        internal SystemPromptConfig ParseJsonToConfigInternal(string json, string sourceContext = "unknown") => Parts.Serialization.ParseJsonToConfigInternal(json, sourceContext);
        internal SystemPromptConfig TryParseCurrentSchemaTextFallback(string json, bool hasSchemaAnchors, bool hasApiActionsKey, bool hasResponseFormatKey, bool hasDecisionRulesKey, bool hasPromptTemplatesKey) => Parts.Serialization.TryParseCurrentSchemaTextFallback(json, hasSchemaAnchors, hasApiActionsKey, hasResponseFormatKey, hasDecisionRulesKey, hasPromptTemplatesKey);
        internal bool IsConfigStructurallyComplete(SystemPromptConfig config, bool hasSchemaAnchors, bool hasApiActionsKey, bool hasResponseFormatKey, bool hasDecisionRulesKey, bool hasPromptTemplatesKey) => Parts.Serialization.IsConfigStructurallyComplete(config, hasSchemaAnchors, hasApiActionsKey, hasResponseFormatKey, hasDecisionRulesKey, hasPromptTemplatesKey);
        internal bool ContainsJsonKey(string json, string key) => Parts.Serialization.ContainsJsonKey(json, key);
        internal void LogTypedParseIncompleteWarningOnce(bool hasSchemaAnchors, bool hasApiActionsKey, bool hasResponseFormatKey, bool hasDecisionRulesKey, bool hasPromptTemplatesKey, SystemPromptConfig config, string sourceContext) => Parts.Serialization.LogTypedParseIncompleteWarningOnce(hasSchemaAnchors, hasApiActionsKey, hasResponseFormatKey, hasDecisionRulesKey, hasPromptTemplatesKey, config, sourceContext);
        internal void LogTypedParseFailureWarningOnce(string sourceContext, string typedError) => Parts.Serialization.LogTypedParseFailureWarningOnce(sourceContext, typedError);
        internal void LogTypedParseRecoveredInfoOnce(string sourceContext, string typedError) => Parts.Serialization.LogTypedParseRecoveredInfoOnce(sourceContext, typedError);
        internal void LogTypedParseWarningOnce(HashSet<int> warningHashes, string signature, string message, bool logAsWarning = true) => Parts.Serialization.LogTypedParseWarningOnce(warningHashes, signature, message, logAsWarning);
        internal void ParseApiActions(string json, SystemPromptConfig config) => Parts.Serialization.ParseApiActions(json, config);
        internal void ParseResponseFormat(string json, SystemPromptConfig config) => Parts.Serialization.ParseResponseFormat(json, config);
        internal void ParseDecisionRules(string json, SystemPromptConfig config) => Parts.Serialization.ParseDecisionRules(json, config);
        internal void ParseDynamicDataInjection(string json, SystemPromptConfig config) => Parts.Serialization.ParseDynamicDataInjection(json, config);
        internal void ParsePromptTemplates(string json, SystemPromptConfig config) => Parts.Serialization.ParsePromptTemplates(json, config);
        internal void ParsePromptPolicy(string json, SystemPromptConfig config) => Parts.Serialization.ParsePromptPolicy(json, config);
        internal void ParseEnvironmentPrompt(string json, SystemPromptConfig config) => Parts.Serialization.ParseEnvironmentPrompt(json, config);
        internal bool TryExtractJsonObject(string json, string key, out string objectContent) => Parts.Serialization.TryExtractJsonObject(json, key, out objectContent);
        internal bool TryExtractJsonArray(string json, string key, out string arrayContent) => Parts.Serialization.TryExtractJsonArray(json, key, out arrayContent);
        internal bool TryFindJsonBlockEnd(string json, int blockStart, char openChar, char closeChar, out int endIndex) => Parts.Serialization.TryFindJsonBlockEnd(json, blockStart, openChar, closeChar, out endIndex);
        internal List<string> SplitJsonObjects(string arrayContent) => Parts.Serialization.SplitJsonObjects(arrayContent);
        internal string ExtractString(string json, string key) => Parts.Serialization.ExtractString(json, key);
        internal string ExtractValue(string json, string key) => Parts.Serialization.ExtractValue(json, key);
        internal List<string> ExtractStringArray(string json, string key) => Parts.Serialization.ExtractStringArray(json, key);
        internal string EscapeJson(string str) => Parts.Serialization.EscapeJson(str);
        #endregion
    
        #region Cluster forwards
        internal void InvalidateCache() => Parts.Slice1.InvalidateCache();
        internal bool TryLoadPromptDomains(out SystemPromptConfig config) => Parts.Slice1.TryLoadPromptDomains(out config);
        internal bool TryLoadPromptDomains(bool includeCustom, out SystemPromptConfig config, out int loadedDomainSchemaVersion, out List<string> validationErrors) => Parts.Slice1.TryLoadPromptDomains(includeCustom, out config, out loadedDomainSchemaVersion, out validationErrors);
        internal bool TryRehydrateFromAggregateDomainJson(bool includeCustom, out SystemPromptConfig config, out List<string> validationErrors) => Parts.Slice1.TryRehydrateFromAggregateDomainJson(includeCustom, out config, out validationErrors);
        internal string BuildAggregateConfigJsonFromDomainFiles(bool includeCustom) => Parts.Slice1.BuildAggregateConfigJsonFromDomainFiles(includeCustom);
        internal string SelectStringField(string customJson, string defaultJson, string key, string fallback) => Parts.Slice1.SelectStringField(customJson, defaultJson, key, fallback);
        internal string SelectValueField(string customJson, string defaultJson, string key, string fallback) => Parts.Slice1.SelectValueField(customJson, defaultJson, key, fallback);
        internal string SelectObjectSection(string customJson, string defaultJson, string key, string fallback) => Parts.Slice1.SelectObjectSection(customJson, defaultJson, key, fallback);
        internal string SelectArraySection(string customJson, string defaultJson, string key, string fallback) => Parts.Slice1.SelectArraySection(customJson, defaultJson, key, fallback);
        internal string BuildPromptTemplatesJson(string diplomacyCustom, string diplomacyDefault, string socialCustom, string socialDefault, RpgPromptCustomConfig pawnPrompt) => Parts.Slice1.BuildPromptTemplatesJson(diplomacyCustom, diplomacyDefault, socialCustom, socialDefault, pawnPrompt);
        internal void ApplyPawnPromptTemplates(SystemPromptConfig config, RpgPromptCustomConfig pawnPrompt) => Parts.Slice1.ApplyPawnPromptTemplates(config, pawnPrompt);
        internal SystemPromptConfig ComposeConfigFromDomains(SystemPromptDomainConfig systemPrompt, DiplomacyDialoguePromptDomainConfig diplomacyPrompt, RpgPromptCustomConfig pawnPrompt, SocialCirclePromptDomainConfig socialPrompt) => Parts.Slice1.ComposeConfigFromDomains(systemPrompt, diplomacyPrompt, pawnPrompt, socialPrompt);
        internal SystemPromptDomainConfig LoadSystemPromptDomain(bool includeCustom) => Parts.Slice1.LoadSystemPromptDomain(includeCustom);
        internal DiplomacyDialoguePromptDomainConfig LoadDiplomacyPromptDomain(bool includeCustom) => Parts.Slice1.LoadDiplomacyPromptDomain(includeCustom);
        internal SocialCirclePromptDomainConfig LoadSocialCirclePromptDomain(bool includeCustom) => Parts.Slice1.LoadSocialCirclePromptDomain(includeCustom);
        internal List<string> ValidateDomainConfigSemantics(SystemPromptConfig config) => Parts.Slice1.ValidateDomainConfigSemantics(config);
        internal HashSet<string> ResolveDiplomacyCoreActionNamesFromDefault() => Parts.Slice1.ResolveDiplomacyCoreActionNamesFromDefault();
        internal PromptTemplateTextConfig BuildPromptTemplates(DiplomacyDialoguePromptDomainConfig diplomacyPrompt, RpgPromptCustomConfig pawnPrompt, SocialCirclePromptDomainConfig socialPrompt) => Parts.Slice1.BuildPromptTemplates(diplomacyPrompt, pawnPrompt, socialPrompt);
        internal List<ApiActionConfig> BuildApiActions(DiplomacyDialoguePromptDomainConfig diplomacyPrompt) => Parts.Slice2.BuildApiActions(diplomacyPrompt);
        internal void EnsureRequiredRaidVariantActions(List<ApiActionConfig> actions) => Parts.Slice2.EnsureRequiredRaidVariantActions(actions);
        internal void EnsureAction(List<ApiActionConfig> actions, string actionName, string description, string parameters, string requirement) => Parts.Slice2.EnsureAction(actions, actionName, description, parameters, requirement);
        internal List<ApiActionConfig> CloneApiActions(IEnumerable<ApiActionConfig> actions) => Parts.Slice2.CloneApiActions(actions);
        internal List<DecisionRuleConfig> CloneDecisionRules(IEnumerable<DecisionRuleConfig> rules) => Parts.Slice2.CloneDecisionRules(rules);
        internal SystemPromptDomainConfig BuildSystemPromptDomain(SystemPromptConfig config) => Parts.Slice2.BuildSystemPromptDomain(config);
        internal DiplomacyDialoguePromptDomainConfig BuildDiplomacyPromptDomain(SystemPromptConfig config) => Parts.Slice2.BuildDiplomacyPromptDomain(config);
        internal List<ApiActionConfig> CloneDiplomacyActions(IEnumerable<ApiActionConfig> actions) => Parts.Slice2.CloneDiplomacyActions(actions);
        internal SocialCirclePromptDomainConfig BuildSocialCirclePromptDomain(SystemPromptConfig config) => Parts.Slice2.BuildSocialCirclePromptDomain(config);
        internal void SavePromptDomainFiles(SystemPromptConfig config) => Parts.Slice2.SavePromptDomainFiles(config);
        internal void DeletePromptDomainCustomFiles() => Parts.Slice2.DeletePromptDomainCustomFiles();
        internal void DeleteCustomPromptFile(string fileName) => Parts.Slice2.DeleteCustomPromptFile(fileName);
        internal bool HasAnyCustomDomainFile() => Parts.Slice2.HasAnyCustomDomainFile();
        internal bool TryGetDomainConfigLastWriteTimeUtc(out DateTime writeTimeUtc) => Parts.Slice2.TryGetDomainConfigLastWriteTimeUtc(out writeTimeUtc);
        internal IEnumerable<string> GetTrackedPromptPaths() => Parts.Slice2.GetTrackedPromptPaths();
        #endregion
}
    internal sealed class PromptDomainStoreSlice2 : PromptDomainStoreCollaborator
    {
        internal PromptDomainStoreSlice2(PromptDomainStore owner) : base(owner)
        {
        }

internal List<ApiActionConfig> BuildApiActions(
            DiplomacyDialoguePromptDomainConfig diplomacyPrompt)
        {
            List<ApiActionConfig> actions = Owner.CloneApiActions(diplomacyPrompt?.ApiActions);
            Owner.EnsureRequiredRaidVariantActions(actions);
            return actions;
        }

internal void EnsureRequiredRaidVariantActions(List<ApiActionConfig> actions)
        {
            if (actions == null)
            {
                return;
            }

            Owner.EnsureAction(
                actions,
                "request_raid_call_everyone",
                PromptTextConstants.RequestRaidCallEveryoneActionDescription,
                string.Empty,
                PromptTextConstants.RequestRaidCallEveryoneActionRequirement);

            Owner.EnsureAction(
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
                DecisionRules = Owner.CloneDecisionRules(config?.DecisionRules),
                ApiActions = Owner.CloneDiplomacyActions(config?.ApiActions),
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
                Owner.BuildSystemPromptDomain(config));
            PromptDomainJsonUtility.WriteToFile(
                PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.DiplomacyPromptCustomFileName),
                Owner.BuildDiplomacyPromptDomain(config));
            PromptDomainJsonUtility.WriteToFile(
                PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.SocialCirclePromptCustomFileName),
                Owner.BuildSocialCirclePromptDomain(config));
        }

internal void DeletePromptDomainCustomFiles()
        {
            foreach (string fileName in CustomPromptDomainFiles)
            {
                Owner.DeleteCustomPromptFile(fileName);
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
            foreach (string path in Owner.GetTrackedPromptPaths())
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

    internal sealed class PromptDomainStoreParts
    {
        internal readonly PromptDomainStore Owner;
        internal readonly PromptDomainStoreLifecycle Lifecycle;
        internal readonly PromptDomainStoreSerialization Serialization;
        internal readonly PromptDomainStoreSlice1 Slice1;
        internal readonly PromptDomainStoreSlice2 Slice2;
        internal PromptDomainStoreParts(PromptDomainStore owner)
        {
            Owner = owner;
            Lifecycle = new PromptDomainStoreLifecycle(owner);
            Serialization = new PromptDomainStoreSerialization(owner);
            Slice1 = new PromptDomainStoreSlice1(owner);
            Slice2 = new PromptDomainStoreSlice2(owner);
        }
    }


}
