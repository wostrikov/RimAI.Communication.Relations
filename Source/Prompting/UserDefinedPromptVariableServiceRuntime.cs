using System;
using System.Collections.Generic;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Persistence;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Prompting
{
    /// <summary>
    /// Dependencies: prompt rendering services and runtime prompt contexts.
    /// Responsibility: render `system.custom.*` values and apply the effective pawn personality export chain.
    /// </summary>
        internal static class UserDefinedPromptVariableServiceRuntime
    {

        public static void PopulateRuntimeValues(IDictionary<string, object> values, PromptRuntimeVariableContext context)
        {
            if (values == null)
            {
                return;
            }

            Dictionary<string, string> cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Stack<string> resolving = new Stack<string>();
            foreach (UserDefinedPromptVariableConfig item in UserDefinedPromptVariableService.GetVariables())
            {
                string path = UserDefinedPromptVariableService.BuildPath(item?.Key);
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                values[path] = UserDefinedPromptVariableService.ResolveVariableValue(path, values, context, cache, resolving);
            }

            UserDefinedPromptVariableService.ApplyEffectivePawnPersonality(values, context, cache, resolving);
        }

        internal static string ResolveVariableValue(
            string path,
            IDictionary<string, object> values,
            PromptRuntimeVariableContext context,
            IDictionary<string, string> cache,
            Stack<string> resolving)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            if (cache.TryGetValue(path, out string cached))
            {
                return cached ?? string.Empty;
            }

            if (resolving.Contains(path))
            {
                Log.Warning($"[RimAI.Relations] Detected recursive custom variable render cycle at {path}.");
                cache[path] = string.Empty;
                return string.Empty;
            }

            resolving.Push(path);
            string rendered = string.Empty;
            try
            {
                UserDefinedPromptVariableConfig variable = UserDefinedPromptVariableService.FindVariableByPath(path);
                if (variable == null || !variable.Enabled)
                {
                    rendered = string.Empty;
                }
                else
                {
                    UserDefinedPromptVariableRuleMatcher.ResolvedRule rule = UserDefinedPromptVariableRuleMatcher.ResolveRule(
                        variable,
                        UserDefinedPromptVariableService.GetFactionRulesForKey(variable.Key),
                        UserDefinedPromptVariableService.GetPawnRulesForKey(variable.Key),
                        context);
                    string template = rule?.TemplateText ?? variable.DefaultTemplateText ?? string.Empty;
                    rendered = UserDefinedPromptVariableService.RenderTemplate(template, values, context, cache, resolving);
                }
            }
            catch (PromptRenderException ex)
            {
                Log.Warning($"[RimAI.Relations] Failed to render custom variable {path}: {ex.Message}");
                rendered = string.Empty;
            }
            finally
            {
                resolving.Pop();
            }

            cache[path] = rendered ?? string.Empty;
            return rendered ?? string.Empty;
        }

        internal static string RenderTemplate(
            string templateText,
            IDictionary<string, object> values,
            PromptRuntimeVariableContext context,
            IDictionary<string, string> cache,
            Stack<string> resolving)
        {
            if (string.IsNullOrWhiteSpace(templateText))
            {
                return string.Empty;
            }

            TemplateVariableValidationResult validation = PromptPersistenceService.Instance.ValidateTemplateVariables(templateText);
            foreach (string dependency in validation.UsedVariables)
            {
                if (!UserDefinedPromptVariableService.IsUserDefinedPath(dependency))
                {
                    continue;
                }

                values[dependency] = UserDefinedPromptVariableService.ResolveVariableValue(dependency, values, context, cache, resolving);
            }

            PromptRenderContext renderContext = PromptRenderContext.Create(
                context?.TemplateId ?? "custom.variable",
                context?.Channel ?? "runtime");
            renderContext.SetValues(new Dictionary<string, object>(values, StringComparer.OrdinalIgnoreCase));
            return PromptTemplateRenderer.RenderOrThrow(
                context?.TemplateId ?? "custom.variable",
                context?.Channel ?? "runtime",
                templateText,
                renderContext);
        }

        internal static void ApplyEffectivePawnPersonality(
            IDictionary<string, object> values,
            PromptRuntimeVariableContext context,
            IDictionary<string, string> cache,
            Stack<string> resolving)
        {
            if (values == null)
            {
                return;
            }

            string raw = values.TryGetValue("pawn.personality", out object rawValue)
                ? rawValue?.ToString() ?? string.Empty
                : string.Empty;
            string overridePath = UserDefinedPromptVariableService.BuildPath("pawn_personality_override");
            string appendPath = UserDefinedPromptVariableService.BuildPath("pawn_personality_append");
            string quickPawnPath = UserDefinedPromptVariableService.BuildPath("quick_pawn_persona");
            string overrideText = UserDefinedPromptVariableService.ResolveOptionalVariableValue(overridePath, values, context, cache, resolving);
            string quickPawnText = UserDefinedPromptVariableService.ResolveOptionalVariableValue(quickPawnPath, values, context, cache, resolving);
            string appendText = UserDefinedPromptVariableService.ResolveOptionalVariableValue(appendPath, values, context, cache, resolving);

            string effective = !string.IsNullOrWhiteSpace(overrideText)
                ? overrideText.Trim()
                : !string.IsNullOrWhiteSpace(quickPawnText)
                    ? quickPawnText.Trim()
                    : raw;
            if (!string.IsNullOrWhiteSpace(appendText))
            {
                effective = string.IsNullOrWhiteSpace(effective)
                    ? appendText.Trim()
                    : effective + "\n" + appendText.Trim();
            }

            values["pawn.personality"] = effective ?? string.Empty;
        }

        internal static string ResolveOptionalVariableValue(
            string path,
            IDictionary<string, object> values,
            PromptRuntimeVariableContext context,
            IDictionary<string, string> cache,
            Stack<string> resolving)
        {
            if (!UserDefinedPromptVariableService.IsUserDefinedPath(path))
            {
                return string.Empty;
            }

            UserDefinedPromptVariableConfig variable = UserDefinedPromptVariableService.FindVariableByPath(path);
            if (variable == null || !variable.Enabled)
            {
                return string.Empty;
            }

            string rendered = UserDefinedPromptVariableService.ResolveVariableValue(path, values, context, cache, resolving);
            values[path] = rendered;
            return rendered;
        }
        }

}
