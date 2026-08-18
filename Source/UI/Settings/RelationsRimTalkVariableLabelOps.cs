using System;
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.UI;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Core.UI;

namespace Ustas.RimAI.Communication.Relations.UI
{
    /// <summary>
    /// Prompt variable label/tooltip/row helper formatting.
    /// </summary>
    internal static class RelationsRimTalkVariableLabelOps
    {
        internal static bool ContainsTerm(string value, string term)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static int ComparePromptVariables(PromptVariableDisplayEntry left, PromptVariableDisplayEntry right)
        {
            int scope = string.Compare(left?.Scope ?? string.Empty, right?.Scope ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            if (scope != 0)
            {
                return scope;
            }

            int source = string.Compare(
                ResolveGroupedSourceLabel(left),
                ResolveGroupedSourceLabel(right),
                StringComparison.OrdinalIgnoreCase);
            if (source != 0)
            {
                return source;
            }

            return string.Compare(left?.Path ?? string.Empty, right?.Path ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        internal static string BuildVariableTooltipText(PromptVariableDisplayEntry variable)
        {
            PromptVariableTooltipInfo info = PromptVariableTooltipCatalog.Resolve(variable?.Path);
            string name = "RimChat_PromptVariableTooltip_Name".Translate(info.Name);
            string dataType = "RimChat_PromptVariableTooltip_DataType".Translate(info.DataType);
            string description = "RimChat_PromptVariableTooltip_Description".Translate(info.Description);
            string typicalValues = "RimChat_PromptVariableTooltip_TypicalValues".Translate(BuildTypicalValuesText(info.TypicalValues));
            return $"{name}\n{dataType}\n{description}\n{typicalValues}";
        }

        internal static string BuildTypicalValuesText(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0)
            {
                return "RimChat_PromptVariableTooltip_NoTypicalValues".Translate().ToString();
            }

            var lines = new List<string>(values.Count);
            for (int i = 0; i < values.Count; i++)
            {
                lines.Add($"{i + 1}) {values[i]}");
            }

            return string.Join("\n", lines);
        }

        internal static string BuildVariableGroupKey(PromptVariableDisplayEntry variable)
        {
            string scope = string.IsNullOrWhiteSpace(variable?.Scope) ? "unknown" : variable.Scope;
            string source = ResolveGroupedSourceLabel(variable);
            return $"[{scope}] {source}";
        }

        internal static string ResolveGroupedSourceLabel(PromptVariableDisplayEntry variable)
        {
            return string.IsNullOrWhiteSpace(variable?.SourceLabel) ? "Unknown" : variable.SourceLabel;
        }

        internal static string BuildVariableToken(string variableName)
        {
            return "{{ " + (variableName ?? string.Empty) + " }}";
        }

        internal static string BuildVariableRowTokenLabel(PromptVariableDisplayEntry variable)
        {
            string raw = ResolveTokenFallback(variable?.RawToken, variable?.Path);
            string namespaced = ResolveTokenFallback(variable?.NamespacedToken, variable?.Path);
            if (string.Equals(raw, namespaced, StringComparison.Ordinal))
            {
                return raw;
            }

            return $"{raw} | {namespaced}";
        }

        internal static string BuildVariableDetailsTokenLabel(PromptVariableDisplayEntry variable)
        {
            string raw = ResolveTokenFallback(variable?.RawToken, variable?.Path);
            string namespaced = ResolveTokenFallback(variable?.NamespacedToken, variable?.Path);
            if (string.Equals(raw, namespaced, StringComparison.Ordinal))
            {
                return raw;
            }

            return $"raw: {raw}  ns: {namespaced}";
        }

        internal static string ResolveTokenFallback(string token, string variablePath)
        {
            if (!string.IsNullOrWhiteSpace(token))
            {
                return token;
            }

            return BuildVariableToken(variablePath);
        }

        internal static string ResolveDefaultInsertVariableName(PromptVariableDisplayEntry entry)
        {
            string token = entry?.DefaultInsertToken;
            string normalized = RelationsPromptWorkbenchFramework.NormalizeVariableNameToken(token);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }

            return entry?.Path?.Trim() ?? string.Empty;
        }

        internal static string BuildVariableInlineInfo(PromptVariableDisplayEntry variable, string currentContent)
        {
            string info = variable?.Description ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(info))
            {
                return info;
            }

            if (!string.IsNullOrWhiteSpace(variable?.DetailSummary))
            {
                return variable.DetailSummary;
            }

            if (!string.IsNullOrWhiteSpace(variable?.SourceLabel))
            {
                return variable.SourceLabel;
            }

            return string.Empty;
        }

        internal static string BuildAvailabilityLabel(PromptVariableDisplayEntry variable)
        {
            return variable?.IsAvailable == false
                ? "RimChat_PromptVariableDependencyMissing".Translate().ToString()
                : "RimChat_PromptVariableReady".Translate().ToString();
        }

        internal static void ResolveVisibleRowRange(
            float scrollY,
            float viewportHeight,
            int rowCount,
            out int firstRow,
            out int lastRow)
        {
            firstRow = Mathf.Max(0, Mathf.FloorToInt(scrollY / RelationsRimTalkVariableBrowser.VariableListRowStep) - 1);
            lastRow = Mathf.Min(rowCount - 1, Mathf.CeilToInt((scrollY + viewportHeight) / RelationsRimTalkVariableBrowser.VariableListRowStep) + 1);
        }

        internal static void DrawVariableGroupHeaderRow(Rect rect, string header)
        {
            GUI.color = Color.cyan;
            Text.Font = GameFont.Tiny;
            Widgets.Label(rect, "▼ " + (header ?? string.Empty));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }
    }
}
