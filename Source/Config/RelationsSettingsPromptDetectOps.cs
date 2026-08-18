using System;
using System.Collections.Generic;
using System.Linq;

namespace Ustas.RimAI.Communication.Relations.Config
{
    /// <summary>
    /// Prompt content reset/detection heuristics for Relations settings.
    /// </summary>
    internal static class RelationsSettingsPromptDetectOps
    {
        internal static bool ShouldResetPromptEntryContent(string content)
        {
            string value = content?.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return LooksLikeRenderedStructuredPrompt(value) || LooksLikeCompiledPromptPreview(value);
        }

        internal static bool LooksLikeRenderedStructuredPrompt(string content)
        {
            string value = content?.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (value.IndexOf("<prompt_context>", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("</prompt_context>", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("=== PREVIEW DIAGNOSTICS ===", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            string[] xmlMarkers =
            {
                "<channel>",
                "<mode>",
                "<environment>",
                "<fact_grounding>",
                "<instruction_stack>",
                "<response_contract>",
                "<dynamic_npc_personal_memory>",
                "<actor_state>"
            };
            int xmlHits = CountMarkerHits(value, xmlMarkers);
            if (xmlHits >= 3 && value.Length >= 300)
            {
                return true;
            }

            string[] blockMarkers =
            {
                "=== ENVIRONMENT PARAMETERS ===",
                "=== RECENT WORLD EVENTS & BATTLE INTEL ===",
                "=== SCENE PROMPT LAYERS ===",
                "=== FACT GROUNDING RULES ===",
                "=== CHARACTER STATUS (YOU) ==="
            };
            return CountMarkerHits(value, blockMarkers) >= 3 && value.Length >= 500;
        }

        internal static bool LooksLikeCompiledPromptPreview(string content)
        {
            string value = content?.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (value.IndexOf("========== FULL MESSAGE LOG ==========", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return value.IndexOf("[FILE]", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   value.IndexOf("[CODE]", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   value.IndexOf("{{", StringComparison.OrdinalIgnoreCase) < 0 &&
                   value.Length >= 500;
        }

        internal static int CountMarkerHits(string value, IEnumerable<string> markers)
        {
            if (string.IsNullOrWhiteSpace(value) || markers == null)
            {
                return 0;
            }

            int hits = 0;
            foreach (string marker in markers)
            {
                if (!string.IsNullOrWhiteSpace(marker) &&
                    value.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hits++;
                }
            }

            return hits;
        }
    }
}
