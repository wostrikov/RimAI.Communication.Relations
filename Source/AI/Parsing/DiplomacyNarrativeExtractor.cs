using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ustas.RimAI.Communication.Relations.Diagnostics;

namespace Ustas.RimAI.Communication.Relations.AI
{
    /// <summary>Diplomacy visible-text extraction and JSON payload location.</summary>
    public static class DiplomacyNarrativeExtractor
    {
        public static string ExtractNarrativeText(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                return string.Empty;
            }

            List<JsonPayloadSegment> segments = ExtractJsonPayloadSegments(response, includeGenericJson: true);
            string candidate = segments.Count > 0
                ? RemoveJsonSegmentsFromText(response, segments)
                : response;

            int jsonFenceIndex = candidate.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
            if (jsonFenceIndex > 0)
            {
                candidate = candidate.Substring(0, jsonFenceIndex);
            }

            int firstBrace = candidate.IndexOf('{');
            if (firstBrace > 0)
            {
                candidate = candidate.Substring(0, firstBrace);
            }

            return candidate
                .Replace("```json", string.Empty)
                .Replace("```", string.Empty)
                .Trim();
        }

        internal static List<JsonPayloadSegment> ExtractJsonPayloadSegments(string response, bool includeGenericJson)
        {
            var segments = new List<JsonPayloadSegment>();
            if (string.IsNullOrWhiteSpace(response))
            {
                return segments;
            }

            foreach (JsonTextSpan span in JsonBoundedExtractor.EnumerateTopLevelObjects(response))
            {
                bool hasActions = span.Text.IndexOf("\"actions\"", StringComparison.OrdinalIgnoreCase) >= 0;
                bool hasStrategySuggestions = span.Text.IndexOf("\"strategy_suggestions\"", StringComparison.OrdinalIgnoreCase) >= 0;
                bool shouldInclude = hasActions || hasStrategySuggestions;
                if (!shouldInclude && includeGenericJson)
                {
                    shouldInclude = LooksLikeStructuredJsonObject(span.Text);
                }

                if (!shouldInclude)
                {
                    continue;
                }

                segments.Add(new JsonPayloadSegment
                {
                    Start = span.Start,
                    End = span.End,
                    Json = span.Text,
                    HasActions = hasActions,
                    HasStrategySuggestions = hasStrategySuggestions
                });
            }

            return segments;
        }

        internal static string RemoveJsonSegmentsFromText(string source, List<JsonPayloadSegment> segments)
        {
            if (string.IsNullOrWhiteSpace(source) || segments == null || segments.Count == 0)
            {
                return source ?? string.Empty;
            }

            var sb = new StringBuilder();
            int cursor = 0;
            foreach (JsonPayloadSegment segment in segments.OrderBy(item => item.Start))
            {
                if (segment.Start < cursor)
                {
                    continue;
                }

                if (segment.Start > cursor)
                {
                    sb.Append(source.Substring(cursor, segment.Start - cursor));
                }
                cursor = Math.Min(source.Length, segment.End + 1);
            }

            if (cursor < source.Length)
            {
                sb.Append(source.Substring(cursor));
            }

            return sb.ToString();
        }

        public static bool LooksLikeStructuredJsonObject(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            string trimmed = json.Trim();
            if (!trimmed.StartsWith("{", StringComparison.Ordinal) ||
                !trimmed.EndsWith("}", StringComparison.Ordinal))
            {
                return false;
            }

            return trimmed.IndexOf("\":", StringComparison.Ordinal) >= 0;
        }

        public static string NormalizeDialogueText(string text)
        {
            string normalized = (text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            normalized = ModelOutputSanitizer.StripReasoningTags(normalized).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            normalized = StripVisibleStrategySection(normalized);
            normalized = normalized.Replace("```json", string.Empty)
                                   .Replace("```", string.Empty)
                                   .Trim();

            string lower = normalized.ToLowerInvariant();
            if (lower == "i understand." ||
                lower == "i understand" ||
                lower == "your in-character response here" ||
                lower == "i have nothing to say at the moment.")
            {
                return string.Empty;
            }

            ImmersionGuardResult guardResult = ImmersionOutputGuard.ValidateVisibleDialogue(normalized);
            if (!guardResult.IsValid)
            {
                DebugLogger.WarningGated($"Immersion guard flagged diplomacy text (downgraded to warning, dialogue preserved): reason={ImmersionOutputGuard.BuildViolationTag(guardResult.ViolationReason)}, snippet={guardResult.ViolationSnippet}");
            }

            return guardResult.IsValid ? guardResult.VisibleDialogue : normalized;
        }

        public static string StripVisibleStrategySection(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string normalized = text.Replace("\r\n", "\n");
            string lower = normalized.ToLowerInvariant();
            string[] markers =
            {
                "\n**Стратегічні поради",
                "\nСтратегічні поради:",
                "\nСтратегічні поради:",
                "\n***\n\n**Стратегічні поради",
                "\n**strategy suggestions",
                "\nstrategy suggestions:",
                "\nstrategy suggestion:"
            };

            int cutIndex = -1;
            foreach (string marker in markers)
            {
                int idx = lower.IndexOf(marker, StringComparison.Ordinal);
                if (idx >= 0 && (cutIndex < 0 || idx < cutIndex))
                {
                    cutIndex = idx;
                }
            }

            if (cutIndex < 0)
            {
                string start = lower.TrimStart();
                if (start.StartsWith("**Стратегічні поради", StringComparison.Ordinal) ||
                    start.StartsWith("Стратегічні поради:", StringComparison.Ordinal) ||
                    start.StartsWith("Стратегічні поради:", StringComparison.Ordinal) ||
                    start.StartsWith("**strategy suggestions", StringComparison.Ordinal) ||
                    start.StartsWith("strategy suggestions:", StringComparison.Ordinal))
                {
                    return string.Empty;
                }

                return normalized.Trim();
            }

            return normalized.Substring(0, cutIndex).Trim();
        }
    }
}
