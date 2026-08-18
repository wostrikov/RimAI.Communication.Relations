using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ustas.RimAI.Communication.Relations.Prompting;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Config
{
    using LegacyTemplateSeed = PromptLegacyCompatMigration.LegacyTemplateSeed;
    internal static class PromptLegacyCompatSlice2
    {
internal static List<RimTalkPromptEntryConfig> ExtractLegacyEntries(
            RimTalkChannelCompatConfig config,
            RimTalkPromptChannel rootChannel)
        {
            var extracted = new List<RimTalkPromptEntryConfig>();
            List<RimTalkPromptEntryConfig> sourceEntries = config?.PromptEntries?
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.Content))
                .Select(entry => entry.Clone())
                .ToList() ?? new List<RimTalkPromptEntryConfig>();
            if (sourceEntries.Count > 0)
            {
                return sourceEntries;
            }

            List<LegacyTemplateSeed> seeds = PromptLegacyCompatMigration.SplitCompatTemplate(config?.CompatTemplate);
            if (seeds.Count == 0)
            {
                return extracted;
            }

            string fallbackChannel = RimTalkPromptEntryChannelCatalog.GetDefaultChannel(rootChannel);
            for (int i = 0; i < seeds.Count; i++)
            {
                LegacyTemplateSeed seed = seeds[i];
                extracted.Add(new RimTalkPromptEntryConfig
                {
                    Id = Guid.NewGuid().ToString("N"),
                    SectionId = seed.SectionId,
                    Name = seed.Name,
                    Role = "System",
                    CustomRole = string.Empty,
                    Position = "Relative",
                    InChatDepth = 0,
                    Enabled = true,
                    PromptChannel = string.IsNullOrWhiteSpace(seed.PromptChannel) ? fallbackChannel : seed.PromptChannel,
                    Content = seed.Content
                });
            }

            return extracted;
        }

internal static List<LegacyTemplateSeed> SplitCompatTemplate(string compatTemplate)
        {
            var result = new List<LegacyTemplateSeed>();
            string normalized = compatTemplate?.Replace("\r\n", "\n").Replace('\r', '\n').Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized) ||
                RelationsSettings.IsShippedCompatTemplateDefault(normalized))
            {
                return result;
            }

            string[] lines = normalized.Split('\n');
            var buffer = new StringBuilder();
            string currentHeader = string.Empty;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i] ?? string.Empty;
                if (PromptLegacyCompatMigration.IsSectionHeader(line.Trim()))
                {
                    PromptLegacyCompatMigration.FlushTemplateSeed(result, currentHeader, buffer);
                    buffer.Clear();
                    currentHeader = line.Trim();
                }

                if (buffer.Length > 0)
                {
                    buffer.Append('\n');
                }

                buffer.Append(line);
            }

            PromptLegacyCompatMigration.FlushTemplateSeed(result, currentHeader, buffer);
            if (result.Count == 0 && !PromptLegacyCompatMigration.ShouldRejectMigratedContent(normalized))
            {
                result.Add(new LegacyTemplateSeed
                {
                    Name = "Compat Template",
                    PromptChannel = RimTalkPromptEntryChannelCatalog.Any,
                    Content = normalized
                });
            }

            return result;
        }

internal static void FlushTemplateSeed(
            ICollection<LegacyTemplateSeed> target,
            string header,
            StringBuilder buffer)
        {
            if (target == null || buffer == null)
            {
                return;
            }

            string content = buffer.ToString().Trim();
            if (string.IsNullOrWhiteSpace(content))
            {
                return;
            }

            target.Add(new LegacyTemplateSeed
            {
                Name = string.IsNullOrWhiteSpace(header) ? "Compat Template" : PromptLegacyCompatMigration.CleanupHeader(header),
                SectionId = PromptLegacyCompatMigration.ResolveSectionId(PromptLegacyCompatMigration.CleanupHeader(header)),
                PromptChannel = RimTalkPromptEntryChannelCatalog.Any,
                Content = content
            });
        }

internal static string ResolveSectionId(RimTalkPromptEntryConfig entry, int index)
        {
            string resolved = PromptLegacyCompatMigration.ResolveSectionId(entry?.SectionId);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                return resolved;
            }

            resolved = PromptLegacyCompatMigration.ResolveSectionId(entry?.Name);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                return resolved;
            }

            return index >= 0 && index < PromptLegacyCompatMigration.SectionDefinitions.Length
                ? PromptLegacyCompatMigration.SectionDefinitions[index].Id
                : string.Empty;
        }

internal static string ResolveSectionId(string candidate)
        {
            string normalized = PromptLegacyCompatMigration.NormalizeToken(candidate);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            for (int i = 0; i < PromptLegacyCompatMigration.SectionDefinitions.Length; i++)
            {
                if (PromptLegacyCompatMigration.SectionDefinitions[i].Matches(normalized))
                {
                    return PromptLegacyCompatMigration.SectionDefinitions[i].Id;
                }
            }

            return string.Empty;
        }

internal static bool LooksLikeRenderedStructuredPrompt(string content)
        {
            string value = content?.Trim() ?? string.Empty;
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

            string[] markers =
            {
                "<channel>",
                "<mode>",
                "<environment>",
                "<instruction_stack>",
                "<response_contract>"
            };
            return PromptLegacyCompatMigration.CountMarkerHits(value, markers) >= 3 && value.Length >= 300;
        }

internal static bool LooksLikeCompiledPromptPreview(string content)
        {
            string value = content?.Trim() ?? string.Empty;
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

internal static int CountMarkerHits(string content, IEnumerable<string> markers)
        {
            if (string.IsNullOrWhiteSpace(content) || markers == null)
            {
                return 0;
            }

            int hits = 0;
            foreach (string marker in markers)
            {
                if (!string.IsNullOrWhiteSpace(marker) &&
                    content.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hits++;
                }
            }

            return hits;
        }

internal static bool IsSectionHeader(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            if (line.Length > 2 && line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
            {
                return true;
            }

            if (line.Length > 6 && line.StartsWith("===", StringComparison.Ordinal) && line.EndsWith("===", StringComparison.Ordinal))
            {
                return true;
            }

            return line.StartsWith("## ", StringComparison.Ordinal) || line.StartsWith("### ", StringComparison.Ordinal);
        }

internal static string CleanupHeader(string header)
        {
            if (string.IsNullOrWhiteSpace(header))
            {
                return string.Empty;
            }

            string cleaned = header.Trim().Trim('[', ']').Trim('=').Trim('#').Trim();
            return cleaned.Length > 48 ? cleaned.Substring(0, 48).Trim() : cleaned;
        }

internal static string NormalizeToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsLetterOrDigit(c) || c == '_')
                {
                    sb.Append(char.ToLowerInvariant(c));
                }
            }

            return sb.ToString();
        }
    }
}
