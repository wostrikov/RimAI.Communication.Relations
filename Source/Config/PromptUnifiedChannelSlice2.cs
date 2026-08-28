using System;
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Persistence;
using Verse;
using Ustas.RimAI.Communication.Relations.Prompting.Transfer;

namespace Ustas.RimAI.Communication.Relations.Config
{
    internal sealed class PromptUnifiedChannelSlice2 : PromptUnifiedChannelConfigCollaborator
    {
        internal PromptUnifiedChannelSlice2(PromptUnifiedChannelConfig owner) : base(owner)
        {
        }

internal static List<PromptUnifiedNodeLayoutConfig> NormalizeNodeLayout(
            string promptChannel,
            List<PromptUnifiedNodeLayoutConfig> source,
            PromptUnifiedCatalogNormalizeReport report)
        {
            var allowedNodes = new HashSet<string>(
                PromptUnifiedNodeSchemaCatalog.GetAllowedNodes(promptChannel).Select(item => item.Id),
                StringComparer.OrdinalIgnoreCase);
            int sourceCount = (source ?? new List<PromptUnifiedNodeLayoutConfig>()).Count(layout => layout != null);
            if (allowedNodes.Count == 0)
            {
                if (sourceCount > 0)
                {
                    report.RemovedLayoutCount += sourceCount;
                    report.MarkChanged();
                }

                return new List<PromptUnifiedNodeLayoutConfig>();
            }

            var merged = new Dictionary<string, PromptUnifiedNodeLayoutConfig>(StringComparer.OrdinalIgnoreCase);
            foreach (PromptUnifiedNodeLayoutConfig layout in source ?? new List<PromptUnifiedNodeLayoutConfig>())
            {
                if (layout == null)
                {
                    continue;
                }

                string id = PromptUnifiedNodeSchemaCatalog.NormalizeId(layout.NodeId);
                if (id.Length == 0)
                {
                    continue;
                }

                if (!allowedNodes.Contains(id))
                {
                    continue;
                }

                merged[id] = PromptUnifiedNodeLayoutConfig.Create(
                    id,
                    PromptUnifiedNodeSlot.MainChainBefore,
                    layout.Order,
                    layout.Enabled);
            }

            int removedCount = Math.Max(0, sourceCount - merged.Count);
            if (removedCount > 0)
            {
                report.RemovedLayoutCount += removedCount;
                report.MarkChanged();
            }

            int filledDefaultCount = 0;
            foreach (string nodeId in allowedNodes)
            {
                if (merged.ContainsKey(nodeId))
                {
                    continue;
                }

                PromptUnifiedNodeLayoutConfig fallback = PromptUnifiedNodeLayoutDefaults.BuildDefaultLayout(promptChannel, nodeId);
                merged[nodeId] = fallback;
                filledDefaultCount++;
            }

            if (filledDefaultCount > 0)
            {
                report.FilledDefaultLayoutCount += filledDefaultCount;
                report.MarkChanged();
            }

            return merged.Values
                .OrderBy(item => item.Order)
                .ThenBy(item => item.NodeId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

internal static List<PromptUnifiedTemplateAliasConfig> NormalizeTemplateAliases(
            List<PromptUnifiedTemplateAliasConfig> source)
        {
            var merged = new Dictionary<string, PromptUnifiedTemplateAliasConfig>(StringComparer.OrdinalIgnoreCase);
            foreach (PromptUnifiedTemplateAliasConfig alias in source ?? new List<PromptUnifiedTemplateAliasConfig>())
            {
                if (alias == null)
                {
                    continue;
                }

                string id = PromptUnifiedTemplateAliasConfig.NormalizeTemplateId(alias.TemplateId);
                if (id.Length == 0)
                {
                    continue;
                }

                merged[id] = PromptUnifiedTemplateAliasConfig.Create(
                    id,
                    alias.Name,
                    alias.Description,
                    alias.Content,
                    alias.Enabled);
            }

            return merged.Values
                .OrderBy(item => item.TemplateId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

internal static string MigrateLegacyRpgRelationshipProfileTemplate(
            string promptChannel,
            string nodeId,
            string template)
        {
            if (!string.Equals(
                    PromptUnifiedNodeSchemaCatalog.NormalizeId(nodeId),
                    "rpg_relationship_profile",
                    StringComparison.OrdinalIgnoreCase))
            {
                return template ?? string.Empty;
            }

            string channel = RimTalkPromptEntryChannelCatalog.NormalizeLoose(promptChannel);
            bool supportedChannel =
                string.Equals(channel, RimTalkPromptEntryChannelCatalog.RpgDialogue, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(channel, RimTalkPromptEntryChannelCatalog.ProactiveRpgDialogue, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(channel, RimTalkPromptEntryChannelCatalog.Any, StringComparison.OrdinalIgnoreCase);
            if (!supportedChannel)
            {
                return template ?? string.Empty;
            }

            string current = template ?? string.Empty;
            if (current.Length == 0 ||
                current.IndexOf("{{ dialogue.guidance }}", StringComparison.OrdinalIgnoreCase) < 0 ||
                current.IndexOf("{{ if dialogue.guidance", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return current;
            }

            string migrated = current;
            migrated = PromptUnifiedChannelConfig.WrapLegacyGuidanceLine(migrated, "Настанова: {{ dialogue.guidance }}");
            migrated = PromptUnifiedChannelConfig.WrapLegacyGuidanceLine(migrated, "Guidance: {{ dialogue.guidance }}");
            return migrated;
        }

internal static string WrapLegacyGuidanceLine(string template, string lineText)
        {
            if (string.IsNullOrEmpty(template) || string.IsNullOrEmpty(lineText))
            {
                return template ?? string.Empty;
            }

            string wrapped = "{{ if dialogue.guidance != \"\" }}\n" + lineText + "{{ end }}";
            string migrated = template
                .Replace("\r\n" + lineText, wrapped)
                .Replace("\n" + lineText, wrapped);

            if (string.Equals(migrated, lineText, StringComparison.Ordinal))
            {
                return wrapped;
            }

            return migrated;
        }
    }
}
