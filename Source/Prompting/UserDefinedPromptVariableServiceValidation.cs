using System;
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Persistence;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Prompting
{
    /// <summary>
    /// Dependencies: prompt validation service and RimWorld defs.
    /// Responsibility: validate, save, and dependency-check unified custom-variable rule edits.
    /// </summary>
        internal static class UserDefinedPromptVariableServiceValidation
    {

        public static UserDefinedPromptVariableValidationResult ValidateEdit(
            Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings,
            UserDefinedPromptVariableEditModel editModel,
            UserDefinedPromptVariableConfig originalVariable = null)
        {
            var result = new UserDefinedPromptVariableValidationResult();
            if (settings == null)
            {
                result.Errors.Add("Settings unavailable.");
                return result;
            }

            UserDefinedPromptVariableConfig variable = editModel?.Variable ?? new UserDefinedPromptVariableConfig();
            string normalizedKey = UserDefinedPromptVariableService.NormalizeKey(variable.Key);
            string path = UserDefinedPromptVariableService.BuildPath(normalizedKey);
            if (string.IsNullOrWhiteSpace(normalizedKey) || !UserDefinedPromptVariableService.IsValidKey(normalizedKey))
            {
                result.Errors.Add("RimChat_CustomVariableValidation_InvalidKey".Translate().ToString());
                return result;
            }

            bool keyTaken = UserDefinedPromptVariableService.GetVariables(settings).Any(item =>
                item != null &&
                !string.Equals(item.Id ?? string.Empty, originalVariable?.Id ?? string.Empty, StringComparison.Ordinal) &&
                string.Equals(UserDefinedPromptVariableService.NormalizeKey(item.Key), normalizedKey, StringComparison.Ordinal));
            if (keyTaken)
            {
                result.Errors.Add("RimChat_CustomVariableValidation_DuplicateKey".Translate(normalizedKey).ToString());
            }

            bool pathConflict = PromptRuntimeVariableRegistry.ContainsReservedPath(
                path,
                originalVariable == null ? string.Empty : UserDefinedPromptVariableService.BuildPath(originalVariable.Key));
            if (pathConflict)
            {
                result.Errors.Add("RimChat_CustomVariableValidation_PathConflict".Translate(path).ToString());
            }

            UserDefinedPromptVariableService.ValidateTemplate(result, settings, "default", variable.DefaultTemplateText, path, originalVariable);
            UserDefinedPromptVariableService.ValidateFactionRules(result, settings, editModel, normalizedKey, path, originalVariable);
            UserDefinedPromptVariableService.ValidatePawnRules(result, settings, editModel, normalizedKey, path, originalVariable);
            UserDefinedPromptVariableService.DetectCycleErrors(result, settings, editModel, originalVariable);
            return result;
        }

        public static bool TrySaveEdit(
            Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings,
            UserDefinedPromptVariableEditModel editModel,
            UserDefinedPromptVariableConfig originalVariable,
            out UserDefinedPromptVariableValidationResult validationResult)
        {
            validationResult = UserDefinedPromptVariableService.ValidateEdit(settings, editModel, originalVariable);
            if (!validationResult.IsValid)
            {
                return false;
            }

            string originalId = originalVariable?.Id ?? string.Empty;
            UserDefinedPromptVariableConfig target = settings.UserDefinedPromptVariables.FirstOrDefault(item =>
                item != null && string.Equals(item.Id ?? string.Empty, originalId, StringComparison.Ordinal));
            if (target == null)
            {
                target = new UserDefinedPromptVariableConfig();
                settings.UserDefinedPromptVariables.Add(target);
            }

            UserDefinedPromptVariableService.ApplyVariable(target, editModel.Variable);

            settings.UserDefinedPromptVariableFactionRules.RemoveAll(item =>
                item != null &&
                string.Equals(UserDefinedPromptVariableService.NormalizeKey(item.VariableKey), UserDefinedPromptVariableService.NormalizeKey(target.Key), StringComparison.OrdinalIgnoreCase));
            foreach (FactionPromptVariableRuleConfig rule in editModel.FactionRules.Where(item => item != null))
            {
                FactionPromptVariableRuleConfig clone = rule.Clone();
                clone.VariableKey = target.Key;
                settings.UserDefinedPromptVariableFactionRules.Add(clone);
            }

            settings.UserDefinedPromptVariablePawnRules.RemoveAll(item =>
                item != null &&
                string.Equals(UserDefinedPromptVariableService.NormalizeKey(item.VariableKey), UserDefinedPromptVariableService.NormalizeKey(target.Key), StringComparison.OrdinalIgnoreCase));
            foreach (PawnPromptVariableRuleConfig rule in editModel.PawnRules.Where(item => item != null))
            {
                PawnPromptVariableRuleConfig clone = rule.Clone();
                clone.VariableKey = target.Key;
                settings.UserDefinedPromptVariablePawnRules.Add(clone);
            }

            UserDefinedPromptVariableService.NormalizeSettingsCollections(settings);
            return true;
        }

        internal static void ValidateFactionRules(
            UserDefinedPromptVariableValidationResult result,
            Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings,
            UserDefinedPromptVariableEditModel editModel,
            string normalizedKey,
            string currentPath,
            UserDefinedPromptVariableConfig originalVariable)
        {
            int fallbackOrder = 0;
            foreach (FactionPromptVariableRuleConfig rule in editModel?.FactionRules ?? Enumerable.Empty<FactionPromptVariableRuleConfig>())
            {
                if (rule == null)
                {
                    continue;
                }

                rule.VariableKey = normalizedKey;
                rule.Order = rule.Order >= 0 ? rule.Order : fallbackOrder;
                if (string.IsNullOrWhiteSpace(rule.FactionDefName) ||
                    DefDatabase<FactionDef>.GetNamedSilentFail(rule.FactionDefName) == null)
                {
                    result.Errors.Add("RimChat_CustomVariableValidation_InvalidFaction".Translate(rule.FactionDefName ?? string.Empty).ToString());
                }

                UserDefinedPromptVariableService.ValidateTemplate(
                    result,
                    settings,
                    $"faction:{rule.Order}:{rule.FactionDefName}",
                    rule.TemplateText,
                    currentPath,
                    originalVariable);
                fallbackOrder++;
            }
        }

        internal static void ValidatePawnRules(
            UserDefinedPromptVariableValidationResult result,
            Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings,
            UserDefinedPromptVariableEditModel editModel,
            string normalizedKey,
            string currentPath,
            UserDefinedPromptVariableConfig originalVariable)
        {
            int fallbackOrder = 0;
            foreach (PawnPromptVariableRuleConfig rule in editModel?.PawnRules ?? Enumerable.Empty<PawnPromptVariableRuleConfig>())
            {
                if (rule == null)
                {
                    continue;
                }

                rule.VariableKey = normalizedKey;
                rule.Order = rule.Order >= 0 ? rule.Order : fallbackOrder;
                rule.NameExact = UserDefinedPromptVariableRuleMatcher.NormalizePawnName(rule.NameExact);
                rule.TraitsAny = UserDefinedPromptVariableRuleMatcher.NormalizeValues(rule.TraitsAny);
                rule.TraitsAll = UserDefinedPromptVariableRuleMatcher.NormalizeValues(rule.TraitsAll);
                rule.PlayerControlled = UserDefinedPromptVariableService.NormalizeBoolToken(rule.PlayerControlled);

                UserDefinedPromptVariableService.ValidatePawnRuleConditions(result, rule);
                UserDefinedPromptVariableService.ValidateTemplate(
                    result,
                    settings,
                    $"pawn:{rule.Order}",
                    rule.TemplateText,
                    currentPath,
                    originalVariable);
                fallbackOrder++;
            }
        }

        internal static void ValidatePawnRuleConditions(UserDefinedPromptVariableValidationResult result, PawnPromptVariableRuleConfig rule)
        {
            if (!string.IsNullOrWhiteSpace(rule.FactionDefName) &&
                DefDatabase<FactionDef>.GetNamedSilentFail(rule.FactionDefName) == null)
            {
                result.Errors.Add("RimChat_CustomVariableValidation_InvalidFaction".Translate(rule.FactionDefName).ToString());
            }

            if (!string.IsNullOrWhiteSpace(rule.RaceDefName) &&
                DefDatabase<ThingDef>.GetNamedSilentFail(rule.RaceDefName) == null)
            {
                result.Errors.Add("RimChat_CustomVariableValidation_InvalidRace".Translate(rule.RaceDefName).ToString());
            }

            if (!string.IsNullOrWhiteSpace(rule.XenotypeDefName) &&
                DefDatabase<XenotypeDef>.GetNamedSilentFail(rule.XenotypeDefName) == null)
            {
                result.Errors.Add("RimChat_CustomVariableValidation_InvalidXenotype".Translate(rule.XenotypeDefName).ToString());
            }

            if (!string.IsNullOrWhiteSpace(rule.Gender) &&
                !Enum.GetNames(typeof(Gender)).Any(item => string.Equals(item, rule.Gender, StringComparison.OrdinalIgnoreCase)))
            {
                result.Errors.Add("RimChat_CustomVariableValidation_InvalidGender".Translate(rule.Gender).ToString());
            }

            if (!string.IsNullOrWhiteSpace(rule.AgeStage) &&
                DefDatabase<LifeStageDef>.GetNamedSilentFail(rule.AgeStage) == null)
            {
                result.Errors.Add("RimChat_CustomVariableValidation_InvalidAgeStage".Translate(rule.AgeStage).ToString());
            }

            foreach (string trait in rule.TraitsAny.Concat(rule.TraitsAll).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (DefDatabase<TraitDef>.GetNamedSilentFail(trait) == null)
                {
                    result.Errors.Add("RimChat_CustomVariableValidation_InvalidTrait".Translate(trait).ToString());
                }
            }

            if (!string.IsNullOrWhiteSpace(rule.PlayerControlled) &&
                !string.Equals(rule.PlayerControlled, "true", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(rule.PlayerControlled, "false", StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add("RimChat_CustomVariableValidation_InvalidPlayerControlled".Translate(rule.PlayerControlled).ToString());
            }
        }

        internal static void ApplyVariable(UserDefinedPromptVariableConfig target, UserDefinedPromptVariableConfig source)
        {
            target.Id = string.IsNullOrWhiteSpace(source?.Id) ? Guid.NewGuid().ToString("N") : source.Id.Trim();
            target.Key = UserDefinedPromptVariableService.NormalizeKey(source?.Key);
            target.DisplayName = source?.DisplayName?.Trim() ?? string.Empty;
            target.Description = source?.Description?.Trim() ?? string.Empty;
            target.DefaultTemplateText = source?.DefaultTemplateText ?? string.Empty;
            target.Enabled = source?.Enabled ?? true;
        }

        internal static void ValidateTemplate(
            UserDefinedPromptVariableValidationResult result,
            Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings,
            string templateId,
            string templateText,
            string currentPath,
            UserDefinedPromptVariableConfig originalVariable)
        {
            TemplateVariableValidationResult validation = PromptPersistenceService.Instance.ValidateTemplateVariables(
                templateText ?? string.Empty,
                UserDefinedPromptVariableService.BuildAdditionalKnownPaths(settings, currentPath, originalVariable));
            result.TemplateResults[templateId] = validation;

            if (validation.HasScribanError)
            {
                result.Errors.Add("RimChat_CustomVariableValidation_TemplateCompile".Translate(templateId, validation.ScribanErrorMessage).ToString());
            }

            if (validation.UnknownVariables.Count > 0)
            {
                result.Errors.Add("RimChat_CustomVariableValidation_UnknownVariables".Translate(templateId, string.Join(", ", validation.UnknownVariables)).ToString());
            }
        }

        internal static IEnumerable<string> BuildAdditionalKnownPaths(
            Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings,
            string currentPath,
            UserDefinedPromptVariableConfig originalVariable)
        {
            HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                currentPath
            };

            if (originalVariable != null)
            {
                string originalPath = UserDefinedPromptVariableService.BuildPath(originalVariable.Key);
                if (!string.IsNullOrWhiteSpace(originalPath))
                {
                    paths.Add(originalPath);
                }
            }

            foreach (UserDefinedPromptVariableConfig item in UserDefinedPromptVariableService.GetVariables(settings))
            {
                string path = UserDefinedPromptVariableService.BuildPath(item?.Key);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    paths.Add(path);
                }
            }

            return paths;
        }

        internal static void DetectCycleErrors(
            UserDefinedPromptVariableValidationResult result,
            Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings,
            UserDefinedPromptVariableEditModel editModel,
            UserDefinedPromptVariableConfig originalVariable)
        {
            string draftKey = UserDefinedPromptVariableService.NormalizeKey(editModel?.Variable?.Key);
            Dictionary<string, HashSet<string>> graph = BuildDependencyGraph(settings, editModel, originalVariable);
            if (string.IsNullOrWhiteSpace(draftKey) || !graph.ContainsKey(draftKey))
            {
                return;
            }

            List<string> path = new List<string>();
            HashSet<string> visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (UserDefinedPromptVariableService.TryFindCycle(draftKey, graph, visiting, visited, path, out List<string> cycle))
            {
                result.Errors.Add("RimChat_CustomVariableValidation_Cycle".Translate(string.Join(" -> ", cycle)).ToString());
            }
        }

        internal static Dictionary<string, HashSet<string>> BuildDependencyGraph(
            Ustas.RimAI.Communication.Relations.Config.RelationsSettings settings,
            UserDefinedPromptVariableEditModel editModel,
            UserDefinedPromptVariableConfig originalVariable)
        {
            var graph = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (UserDefinedPromptVariableConfig item in UserDefinedPromptVariableService.GetVariables(settings))
            {
                if (item == null)
                {
                    continue;
                }

                bool isCurrent = originalVariable != null &&
                                 string.Equals(item.Id ?? string.Empty, originalVariable.Id ?? string.Empty, StringComparison.Ordinal);
                if (isCurrent)
                {
                    continue;
                }

                string key = UserDefinedPromptVariableService.NormalizeKey(item.Key);
                UserDefinedPromptVariableService.AddDependencies(graph, key, item.DefaultTemplateText);
                foreach (FactionPromptVariableRuleConfig rule in UserDefinedPromptVariableService.GetFactionRulesForKey(item.Key, settings))
                {
                    UserDefinedPromptVariableService.AddDependencies(graph, key, rule.TemplateText);
                }

                foreach (PawnPromptVariableRuleConfig rule in UserDefinedPromptVariableService.GetPawnRulesForKey(item.Key, settings))
                {
                    UserDefinedPromptVariableService.AddDependencies(graph, key, rule.TemplateText);
                }
            }

            if (editModel?.Variable != null)
            {
                string key = UserDefinedPromptVariableService.NormalizeKey(editModel.Variable.Key);
                UserDefinedPromptVariableService.AddDependencies(graph, key, editModel.Variable.DefaultTemplateText);
                foreach (FactionPromptVariableRuleConfig rule in editModel.FactionRules ?? Enumerable.Empty<FactionPromptVariableRuleConfig>())
                {
                    UserDefinedPromptVariableService.AddDependencies(graph, key, rule?.TemplateText);
                }

                foreach (PawnPromptVariableRuleConfig rule in editModel.PawnRules ?? Enumerable.Empty<PawnPromptVariableRuleConfig>())
                {
                    UserDefinedPromptVariableService.AddDependencies(graph, key, rule?.TemplateText);
                }
            }

            return graph;
        }
        }

}
