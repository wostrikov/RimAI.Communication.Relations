using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Persistence;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Prompting
{
    using PromptTemplateReferenceCandidate = UserDefinedPromptVariableService.PromptTemplateReferenceCandidate;
    internal static class UserDefinedPromptVariableSlice1
    {
public static bool IsUserDefinedPath(string path)
        {
            string normalized = (path ?? string.Empty).Trim();
            return normalized.StartsWith(UserDefinedPromptVariableService.NamespaceRoot + ".", StringComparison.OrdinalIgnoreCase);
        }

public static string NormalizeKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(key.Length);
            string trimmed = key.Trim();
            for (int i = 0; i < trimmed.Length; i++)
            {
                char current = trimmed[i];
                if (char.IsLetterOrDigit(current))
                {
                    builder.Append(char.ToLowerInvariant(current));
                }
                else if (current == '_')
                {
                    builder.Append('_');
                }
            }

            return builder.ToString();
        }

public static bool IsValidKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            string normalized = key.Trim();
            for (int i = 0; i < normalized.Length; i++)
            {
                char current = normalized[i];
                if (!(char.IsLower(current) || char.IsDigit(current) || current == '_'))
                {
                    return false;
                }
            }

            return true;
        }

public static string BuildPath(string key)
        {
            string normalized = UserDefinedPromptVariableService.NormalizeKey(key);
            return string.IsNullOrWhiteSpace(normalized)
                ? string.Empty
                : UserDefinedPromptVariableService.NamespaceRoot + "." + normalized;
        }

public static string ExtractKeyFromPath(string path)
        {
            if (!UserDefinedPromptVariableService.IsUserDefinedPath(path))
            {
                return string.Empty;
            }

            string normalized = path.Trim();
            return normalized.Substring(UserDefinedPromptVariableService.NamespaceRoot.Length + 1).Trim().ToLowerInvariant();
        }

public static IReadOnlyList<UserDefinedPromptVariableConfig> GetVariables(Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings = null)
        {
            Ustas.RimAI.Communication.Relations.Config.RelationsSettings resolved = settings ?? RelationsMod.Settings;
            return resolved?.UserDefinedPromptVariables != null
                ? resolved.UserDefinedPromptVariables
                : (IReadOnlyList<UserDefinedPromptVariableConfig>)Array.Empty<UserDefinedPromptVariableConfig>();
        }

public static IReadOnlyList<FactionPromptVariableRuleConfig> GetFactionRules(Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings = null)
        {
            Ustas.RimAI.Communication.Relations.Config.RelationsSettings resolved = settings ?? RelationsMod.Settings;
            return resolved?.UserDefinedPromptVariableFactionRules != null
                ? resolved.UserDefinedPromptVariableFactionRules
                : (IReadOnlyList<FactionPromptVariableRuleConfig>)Array.Empty<FactionPromptVariableRuleConfig>();
        }

public static IReadOnlyList<PawnPromptVariableRuleConfig> GetPawnRules(Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings = null)
        {
            Ustas.RimAI.Communication.Relations.Config.RelationsSettings resolved = settings ?? RelationsMod.Settings;
            return resolved?.UserDefinedPromptVariablePawnRules != null
                ? resolved.UserDefinedPromptVariablePawnRules
                : (IReadOnlyList<PawnPromptVariableRuleConfig>)Array.Empty<PawnPromptVariableRuleConfig>();
        }

public static IReadOnlyList<FactionScopedPromptVariableOverrideConfig> GetLegacyOverrides(Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings = null)
        {
            Ustas.RimAI.Communication.Relations.Config.RelationsSettings resolved = settings ?? RelationsMod.Settings;
            return resolved?.FactionScopedPromptVariableOverrides != null
                ? resolved.FactionScopedPromptVariableOverrides
                : (IReadOnlyList<FactionScopedPromptVariableOverrideConfig>)Array.Empty<FactionScopedPromptVariableOverrideConfig>();
        }

public static UserDefinedPromptVariableConfig FindVariableByPath(string path, Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings = null)
        {
            string key = UserDefinedPromptVariableService.ExtractKeyFromPath(path);
            return UserDefinedPromptVariableService.FindVariableByKey(key, settings);
        }

public static UserDefinedPromptVariableConfig FindVariableByKey(string key, Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings = null)
        {
            string normalized = UserDefinedPromptVariableService.NormalizeKey(key);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            return UserDefinedPromptVariableService.GetVariables(settings).FirstOrDefault(item =>
                item != null &&
                string.Equals(UserDefinedPromptVariableService.NormalizeKey(item.Key), normalized, StringComparison.Ordinal));
        }

public static List<FactionPromptVariableRuleConfig> GetFactionRulesForKey(string key, Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings = null)
        {
            string normalized = UserDefinedPromptVariableService.NormalizeKey(key);
            return UserDefinedPromptVariableService.GetFactionRules(settings)
                .Where(item => item != null && string.Equals(UserDefinedPromptVariableService.NormalizeKey(item.VariableKey), normalized, StringComparison.Ordinal))
                .Select(item => item.Clone())
                .ToList();
        }

public static List<PawnPromptVariableRuleConfig> GetPawnRulesForKey(string key, Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings = null)
        {
            string normalized = UserDefinedPromptVariableService.NormalizeKey(key);
            return UserDefinedPromptVariableService.GetPawnRules(settings)
                .Where(item => item != null && string.Equals(UserDefinedPromptVariableService.NormalizeKey(item.VariableKey), normalized, StringComparison.Ordinal))
                .Select(item => item.Clone())
                .ToList();
        }

public static PromptRuntimeVariableDefinition BuildDefinition(UserDefinedPromptVariableConfig config)
        {
            string path = UserDefinedPromptVariableService.BuildPath(config?.Key);
            string description = UserDefinedPromptVariableService.BuildDefinitionDescription(
                config,
                UserDefinedPromptVariableService.GetFactionRulesForKey(config?.Key),
                UserDefinedPromptVariableService.GetPawnRulesForKey(config?.Key));
            return new PromptRuntimeVariableDefinition(path, UserDefinedPromptVariableService.SourceId, UserDefinedPromptVariableService.SourceLabel, description, true);
        }

public static string BuildDefinitionDescription(
            UserDefinedPromptVariableConfig config,
            IReadOnlyCollection<FactionPromptVariableRuleConfig> factionRules,
            IReadOnlyCollection<PawnPromptVariableRuleConfig> pawnRules)
        {
            if (config == null)
            {
                return string.Empty;
            }

            string displayName = string.IsNullOrWhiteSpace(config.DisplayName)
                ? UserDefinedPromptVariableService.BuildPath(config.Key)
                : config.DisplayName.Trim();
            string stateText = config.Enabled ? "enabled" : "disabled";
            string summary = string.IsNullOrWhiteSpace(config.Description) ? displayName : config.Description.Trim();
            int enabledFactionRules = factionRules?.Count(item => item != null && item.Enabled) ?? 0;
            int enabledPawnRules = pawnRules?.Count(item => item != null && item.Enabled) ?? 0;
            return $"{summary} ({stateText}, faction rules: {enabledFactionRules}, pawn rules: {enabledPawnRules})";
        }

public static PromptVariableTooltipInfo BuildTooltipInfo(string path)
        {
            UserDefinedPromptVariableConfig config = UserDefinedPromptVariableService.FindVariableByPath(path);
            if (config == null)
            {
                return null;
            }

            List<FactionPromptVariableRuleConfig> factionRules = UserDefinedPromptVariableService.GetFactionRulesForKey(config.Key);
            List<PawnPromptVariableRuleConfig> pawnRules = UserDefinedPromptVariableService.GetPawnRulesForKey(config.Key);
            List<string> typicalValues = new List<string>();
            if (!string.IsNullOrWhiteSpace(config.DefaultTemplateText))
            {
                typicalValues.Add(config.DefaultTemplateText.Trim());
            }

            foreach (FactionPromptVariableRuleConfig rule in factionRules.Where(item => item != null && item.Enabled))
            {
                if (string.IsNullOrWhiteSpace(rule.TemplateText))
                {
                    continue;
                }

                typicalValues.Add($"{UserDefinedPromptVariableRuleMatcher.BuildLayerLabel(UserDefinedPromptVariableRuleMatcher.RuleLayer.Faction)}: {rule.FactionDefName} -> {UserDefinedPromptVariableRuleMatcher.BuildTemplateSummary(rule.TemplateText)}");
                if (typicalValues.Count >= 4)
                {
                    break;
                }
            }

            foreach (PawnPromptVariableRuleConfig rule in pawnRules.Where(item => item != null && item.Enabled))
            {
                if (string.IsNullOrWhiteSpace(rule.TemplateText))
                {
                    continue;
                }

                UserDefinedPromptVariableRuleMatcher.RuleLayer layer = string.IsNullOrWhiteSpace(rule.NameExact)
                    ? UserDefinedPromptVariableRuleMatcher.RuleLayer.PawnConditional
                    : UserDefinedPromptVariableRuleMatcher.RuleLayer.PawnExact;
                typicalValues.Add($"{UserDefinedPromptVariableRuleMatcher.BuildLayerLabel(layer)}: {UserDefinedPromptVariableRuleMatcher.BuildTemplateSummary(rule.TemplateText)}");
                if (typicalValues.Count >= 4)
                {
                    break;
                }
            }

            string name = string.IsNullOrWhiteSpace(config.DisplayName)
                ? UserDefinedPromptVariableService.BuildPath(config.Key)
                : config.DisplayName.Trim();
            string description = UserDefinedPromptVariableService.BuildDefinitionDescription(config, factionRules, pawnRules);
            return new PromptVariableTooltipInfo(name, "Scriban text", description, typicalValues);
        }

public static void NormalizeSettingsCollections(Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            settings.UserDefinedPromptVariables ??= new List<UserDefinedPromptVariableConfig>();
            settings.UserDefinedPromptVariableFactionRules ??= new List<FactionPromptVariableRuleConfig>();
            settings.UserDefinedPromptVariablePawnRules ??= new List<PawnPromptVariableRuleConfig>();
            settings.FactionScopedPromptVariableOverrides ??= new List<FactionScopedPromptVariableOverrideConfig>();

            UserDefinedPromptVariableService.NormalizeVariables(settings);
            UserDefinedPromptVariableService.MigrateLegacyOverrides(settings);
            UserDefinedPromptVariableService.NormalizeFactionRules(settings);
            UserDefinedPromptVariableService.NormalizePawnRules(settings);
        }

public static List<UserDefinedPromptVariableReferenceLocation> FindReferences(string path, Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings = null)
        {
            Ustas.RimAI.Communication.Relations.Config.RelationsSettings resolved = settings ?? RelationsMod.Settings;
            var matches = new List<UserDefinedPromptVariableReferenceLocation>();
            if (resolved == null || string.IsNullOrWhiteSpace(path))
            {
                return matches;
            }

            string normalized = path.Trim();
            foreach (PromptTemplateReferenceCandidate candidate in UserDefinedPromptVariableService.EnumerateReferenceCandidates(resolved))
            {
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.TemplateText))
                {
                    continue;
                }

                TemplateVariableValidationResult validation = PromptPersistenceService.Instance.ValidateTemplateVariables(candidate.TemplateText);
                if (validation.UsedVariables.Any(item => string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase)))
                {
                    matches.Add(new UserDefinedPromptVariableReferenceLocation
                    {
                        LocationId = candidate.LocationId,
                        DisplayText = candidate.DisplayText
                    });
                }
            }

            return matches
                .GroupBy(item => item.LocationId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

public static bool TryDeleteVariable(
            Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings,
            string path,
            out List<UserDefinedPromptVariableReferenceLocation> references)
        {
            references = UserDefinedPromptVariableService.FindReferences(path, settings);
            if (references.Count > 0)
            {
                return false;
            }

            UserDefinedPromptVariableConfig config = UserDefinedPromptVariableService.FindVariableByPath(path, settings);
            if (config == null)
            {
                return true;
            }

            string normalizedKey = UserDefinedPromptVariableService.NormalizeKey(config.Key);
            settings.UserDefinedPromptVariables.RemoveAll(item =>
                item != null && string.Equals(UserDefinedPromptVariableService.NormalizeKey(item.Key), normalizedKey, StringComparison.OrdinalIgnoreCase));
            settings.UserDefinedPromptVariableFactionRules.RemoveAll(item =>
                item != null && string.Equals(UserDefinedPromptVariableService.NormalizeKey(item.VariableKey), normalizedKey, StringComparison.OrdinalIgnoreCase));
            settings.UserDefinedPromptVariablePawnRules.RemoveAll(item =>
                item != null && string.Equals(UserDefinedPromptVariableService.NormalizeKey(item.VariableKey), normalizedKey, StringComparison.OrdinalIgnoreCase));
            UserDefinedPromptVariableService.NormalizeSettingsCollections(settings);
            return true;
        }

public static UserDefinedPromptVariableEditModel CreateSuggestedModel(string key)
        {
            string normalized = UserDefinedPromptVariableService.NormalizeKey(key);
            var model = new UserDefinedPromptVariableEditModel();
            model.Variable.Key = normalized;
            model.Variable.DisplayName = UserDefinedPromptVariableService.BuildPath(normalized);
            model.Variable.Description = UserDefinedPromptVariableService.BuildSuggestedDescription(normalized);
            model.Variable.DefaultTemplateText = UserDefinedPromptVariableService.BuildSuggestedTemplate(normalized);
            return model;
        }

internal static void NormalizeVariables(Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings)
        {
            var normalizedVariables = new List<UserDefinedPromptVariableConfig>();
            var seenVariableKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (UserDefinedPromptVariableConfig item in settings.UserDefinedPromptVariables)
            {
                if (item == null)
                {
                    continue;
                }

                item.Id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("N") : item.Id.Trim();
                item.Key = UserDefinedPromptVariableService.NormalizeKey(item.Key);
                item.DisplayName = item.DisplayName?.Trim() ?? string.Empty;
                item.Description = item.Description?.Trim() ?? string.Empty;
                item.DefaultTemplateText = item.DefaultTemplateText ?? string.Empty;
                if (string.IsNullOrWhiteSpace(item.Key) || !seenVariableKeys.Add(item.Key))
                {
                    continue;
                }

                normalizedVariables.Add(item);
            }

            settings.UserDefinedPromptVariables = normalizedVariables;
        }

internal static void MigrateLegacyOverrides(Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            settings.UserDefinedPromptVariableFactionRules ??= new List<FactionPromptVariableRuleConfig>();
            foreach (FactionScopedPromptVariableOverrideConfig legacy in UserDefinedPromptVariableService.GetLegacyOverrides(settings))
            {
                if (legacy == null)
                {
                    continue;
                }

                string variableKey = UserDefinedPromptVariableService.NormalizeKey(legacy.VariableKey);
                if (string.IsNullOrWhiteSpace(variableKey) ||
                    UserDefinedPromptVariableService.FindVariableByKey(variableKey, settings) == null ||
                    string.IsNullOrWhiteSpace(legacy.FactionDefName))
                {
                    continue;
                }

                bool exists = settings.UserDefinedPromptVariableFactionRules.Any(item =>
                    item != null &&
                    string.Equals(UserDefinedPromptVariableService.NormalizeKey(item.VariableKey), variableKey, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(item.FactionDefName, legacy.FactionDefName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(item.TemplateText ?? string.Empty, legacy.TemplateText ?? string.Empty, StringComparison.Ordinal) &&
                    item.Priority == 0);
                if (exists)
                {
                    continue;
                }

                settings.UserDefinedPromptVariableFactionRules.Add(new FactionPromptVariableRuleConfig
                {
                    Id = string.IsNullOrWhiteSpace(legacy.Id) ? Guid.NewGuid().ToString("N") : legacy.Id.Trim(),
                    VariableKey = variableKey,
                    FactionDefName = legacy.FactionDefName?.Trim() ?? string.Empty,
                    Priority = 0,
                    TemplateText = legacy.TemplateText ?? string.Empty,
                    Enabled = legacy.Enabled,
                    Order = settings.UserDefinedPromptVariableFactionRules.Count
                });
            }

            settings.FactionScopedPromptVariableOverrides = new List<FactionScopedPromptVariableOverrideConfig>();
        }

internal static void NormalizeFactionRules(Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings)
        {
            var normalizedRules = new List<FactionPromptVariableRuleConfig>();
            int order = 0;
            foreach (FactionPromptVariableRuleConfig item in settings.UserDefinedPromptVariableFactionRules)
            {
                if (item == null)
                {
                    continue;
                }

                item.Id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("N") : item.Id.Trim();
                item.VariableKey = UserDefinedPromptVariableService.NormalizeKey(item.VariableKey);
                item.FactionDefName = item.FactionDefName?.Trim() ?? string.Empty;
                item.TemplateText = item.TemplateText ?? string.Empty;
                item.Order = item.Order >= 0 ? item.Order : order;
                if (string.IsNullOrWhiteSpace(item.VariableKey) ||
                    string.IsNullOrWhiteSpace(item.FactionDefName) ||
                    UserDefinedPromptVariableService.FindVariableByKey(item.VariableKey, settings) == null)
                {
                    continue;
                }

                normalizedRules.Add(item);
                order++;
            }

            settings.UserDefinedPromptVariableFactionRules = normalizedRules
                .OrderBy(item => item.Order)
                .ToList();
        }
    }
}
