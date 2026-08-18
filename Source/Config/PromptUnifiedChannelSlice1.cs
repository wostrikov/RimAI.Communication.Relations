using System;
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Persistence;
using Verse;
using Ustas.RimAI.Communication.Relations.Prompting.Transfer;

namespace Ustas.RimAI.Communication.Relations.Config
{
    internal sealed class PromptUnifiedChannelSlice1 : PromptUnifiedChannelConfigCollaborator
    {
        internal PromptUnifiedChannelSlice1(PromptUnifiedChannelConfig owner) : base(owner)
        {
        }

public PromptUnifiedChannelConfig Clone()
        {
            return new PromptUnifiedChannelConfig
            {
                PromptChannel = RimTalkPromptEntryChannelCatalog.NormalizeLoose(PromptChannel),
                Sections = Sections?.Where(s => s != null).Select(s => s.Clone()).ToList() ?? new List<PromptUnifiedSectionContent>(),
                Nodes = Nodes?.Where(n => n != null).Select(n => n.Clone()).ToList() ?? new List<PromptUnifiedNodeContent>(),
                NodeLayout = NodeLayout?.Where(n => n != null).Select(n => n.Clone()).ToList() ?? new List<PromptUnifiedNodeLayoutConfig>(),
                TemplateAliases = TemplateAliases?.Where(a => a != null).Select(a => a.Clone()).ToList() ?? new List<PromptUnifiedTemplateAliasConfig>(),
                CustomNodes = CustomNodes?.Where(c => c != null).Select(c => c.Clone()).ToList() ?? new List<PromptUnifiedNodeRegistration>()
            };
        }

public PromptUnifiedCatalogNormalizeReport NormalizeWithReport()
        {
            var report = new PromptUnifiedCatalogNormalizeReport();
            PromptChannel = RimTalkPromptEntryChannelCatalog.NormalizeLoose(PromptChannel);
            Sections = PromptUnifiedChannelConfig.NormalizeSections(Sections);
            Nodes = PromptUnifiedChannelConfig.NormalizeNodes(PromptChannel, Nodes, report);
            NodeLayout = PromptUnifiedChannelConfig.NormalizeNodeLayout(PromptChannel, NodeLayout, report);
            TemplateAliases = PromptUnifiedChannelConfig.NormalizeTemplateAliases(TemplateAliases);
            return report;
        }

public string ResolveSection(string sectionId)
        {
            string normalized = PromptSectionSchemaCatalog.NormalizeSectionId(sectionId);
            return Sections?.FirstOrDefault(s =>
                s != null && string.Equals(s.SectionId, normalized, StringComparison.OrdinalIgnoreCase))?.Content ?? string.Empty;
        }

public bool TryResolveSection(string sectionId, out string content)
        {
            string normalized = PromptSectionSchemaCatalog.NormalizeSectionId(sectionId);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                content = string.Empty;
                return false;
            }

            PromptUnifiedSectionContent section = Sections?.FirstOrDefault(s =>
                s != null && string.Equals(s.SectionId, normalized, StringComparison.OrdinalIgnoreCase));
            if (section == null)
            {
                content = string.Empty;
                return false;
            }

            content = section.Content ?? string.Empty;
            return true;
        }

public string ResolveNode(string nodeId)
        {
            string normalized = PromptUnifiedNodeSchemaCatalog.NormalizeId(nodeId);
            return Nodes?.FirstOrDefault(n =>
                n != null && string.Equals(n.NodeId, normalized, StringComparison.OrdinalIgnoreCase))?.Content ?? string.Empty;
        }

public bool TryResolveNode(string nodeId, out string content)
        {
            string normalized = PromptUnifiedNodeSchemaCatalog.NormalizeId(nodeId);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                content = string.Empty;
                return false;
            }

            PromptUnifiedNodeContent node = Nodes?.FirstOrDefault(n =>
                n != null && string.Equals(n.NodeId, normalized, StringComparison.OrdinalIgnoreCase));
            if (node == null)
            {
                content = string.Empty;
                return false;
            }

            content = node.Content ?? string.Empty;
            return true;
        }

public PromptUnifiedNodeLayoutConfig ResolveNodeLayout(string nodeId)
        {
            string normalized = PromptUnifiedNodeSchemaCatalog.NormalizeId(nodeId);
            return NodeLayout?.FirstOrDefault(n =>
                n != null && string.Equals(n.NodeId, normalized, StringComparison.OrdinalIgnoreCase))?.Clone();
        }

public List<PromptUnifiedTemplateAliasConfig> GetTemplateAliases()
        {
            Owner.Normalize();
            return TemplateAliases ?? new List<PromptUnifiedTemplateAliasConfig>();
        }

public PromptUnifiedTemplateAliasConfig ResolveTemplateAlias(string templateId)
        {
            string normalized = PromptUnifiedTemplateAliasConfig.NormalizeTemplateId(templateId);
            if (normalized.Length == 0)
            {
                return null;
            }

            return TemplateAliases?.FirstOrDefault(alias =>
                alias != null &&
                string.Equals(alias.TemplateId, normalized, StringComparison.OrdinalIgnoreCase));
        }

public PromptUnifiedTemplateAliasConfig ResolvePreferredTemplateAlias(string preferredTemplateId)
        {
            string preferred = PromptUnifiedTemplateAliasConfig.NormalizeTemplateId(preferredTemplateId);
            if (preferred.Length > 0)
            {
                PromptUnifiedTemplateAliasConfig preferredAlias = Owner.ResolveTemplateAlias(preferred);
                if (preferredAlias != null && preferredAlias.Enabled)
                {
                    return preferredAlias;
                }
            }

            PromptUnifiedTemplateAliasConfig firstEnabled = TemplateAliases?.FirstOrDefault(alias =>
                alias != null &&
                alias.Enabled &&
                !string.IsNullOrWhiteSpace(alias.TemplateId));
            if (firstEnabled != null)
            {
                return firstEnabled;
            }

            return TemplateAliases?.FirstOrDefault(alias =>
                alias != null &&
                !string.IsNullOrWhiteSpace(alias.TemplateId));
        }

public void SetSection(string sectionId, string content)
        {
            string normalized = PromptSectionSchemaCatalog.NormalizeSectionId(sectionId);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            Sections ??= new List<PromptUnifiedSectionContent>();
            PromptUnifiedSectionContent existing = Sections.FirstOrDefault(s =>
                s != null && string.Equals(s.SectionId, normalized, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                Sections.Add(PromptUnifiedSectionContent.Create(normalized, content));
                return;
            }

            existing.Content = content?.Trim() ?? string.Empty;
        }

public void SetSectionLayout(string sectionId, int order)
        {
            string normalized = PromptSectionSchemaCatalog.NormalizeSectionId(sectionId);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            SectionLayout ??= new List<PromptSectionLayoutConfig>();
            PromptSectionLayoutConfig existing = SectionLayout.FirstOrDefault(s =>
                s != null && string.Equals(s.SectionId, normalized, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                SectionLayout.Add(PromptSectionLayoutConfig.Create(normalized, order));
                return;
            }

            existing.Order = order;
        }

public List<PromptSectionLayoutConfig> GetOrderedSectionLayouts()
        {
            Owner.Normalize();
            return SectionLayout
                .Where(item => item != null)
                .Select(item => item.Clone())
                .OrderBy(item => item.Order)
                .ThenBy(item => item.SectionId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

public void SetNode(string nodeId, string content)
        {
            string normalized = PromptUnifiedNodeSchemaCatalog.NormalizeId(nodeId);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            Nodes ??= new List<PromptUnifiedNodeContent>();
            PromptUnifiedNodeContent existing = Nodes.FirstOrDefault(n =>
                n != null && string.Equals(n.NodeId, normalized, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                Nodes.Add(PromptUnifiedNodeContent.Create(normalized, content));
                return;
            }

            existing.Content = content?.Trim() ?? string.Empty;
        }

public void SetNodeLayout(string nodeId, PromptUnifiedNodeSlot slot, int order, bool enabled)
        {
            string normalized = PromptUnifiedNodeSchemaCatalog.NormalizeId(nodeId);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            NodeLayout ??= new List<PromptUnifiedNodeLayoutConfig>();
            PromptUnifiedNodeLayoutConfig existing = NodeLayout.FirstOrDefault(n =>
                n != null && string.Equals(n.NodeId, normalized, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                NodeLayout.Add(PromptUnifiedNodeLayoutConfig.Create(normalized, PromptUnifiedNodeSlot.MainChainBefore, order, enabled));
                return;
            }

            existing.Slot = PromptUnifiedNodeSlot.MainChainBefore.ToSerializedValue();
            existing.Order = order;
            existing.Enabled = enabled;
        }

public void SetTemplateAlias(
            string templateId,
            string name,
            string description,
            string content,
            bool enabled)
        {
            string normalizedId = PromptUnifiedTemplateAliasConfig.NormalizeTemplateId(templateId);
            if (normalizedId.Length == 0)
            {
                return;
            }

            TemplateAliases ??= new List<PromptUnifiedTemplateAliasConfig>();
            PromptUnifiedTemplateAliasConfig existing = TemplateAliases.FirstOrDefault(alias =>
                alias != null &&
                string.Equals(alias.TemplateId, normalizedId, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                TemplateAliases.Add(PromptUnifiedTemplateAliasConfig.Create(
                    normalizedId,
                    name,
                    description,
                    content,
                    enabled));
                return;
            }

            existing.Name = name?.Trim() ?? string.Empty;
            existing.Description = description?.Trim() ?? string.Empty;
            existing.Content = content?.Trim() ?? string.Empty;
            existing.Enabled = enabled;
        }

public List<PromptUnifiedNodeLayoutConfig> GetOrderedNodeLayouts(string promptChannel)
        {
            Owner.Normalize();
            return NodeLayout
                .Where(item => item != null)
                .Select(item => item.Clone())
                .OrderBy(item => item.Order)
                .ThenBy(item => item.NodeId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

public void Merge(PromptUnifiedChannelConfig source)
        {
            if (source == null)
            {
                return;
            }

            foreach (PromptUnifiedSectionContent section in source.Sections ?? new List<PromptUnifiedSectionContent>())
            {
                if (section != null)
                {
                    Owner.SetSection(section.SectionId, section.Content);
                }
            }

            foreach (PromptUnifiedNodeContent node in source.Nodes ?? new List<PromptUnifiedNodeContent>())
            {
                if (node != null)
                {
                    Owner.SetNode(node.NodeId, node.Content);
                }
            }

            foreach (PromptUnifiedNodeLayoutConfig layout in source.NodeLayout ?? new List<PromptUnifiedNodeLayoutConfig>())
            {
                if (layout == null)
                {
                    continue;
                }

                Owner.SetNodeLayout(layout.NodeId, layout.GetSlot(), layout.Order, layout.Enabled);
            }

            foreach (PromptSectionLayoutConfig sectionLayout in source.SectionLayout ?? new List<PromptSectionLayoutConfig>())
            {
                if (sectionLayout == null)
                {
                    continue;
                }

                Owner.SetSectionLayout(sectionLayout.SectionId, sectionLayout.Order);
            }

            foreach (PromptUnifiedTemplateAliasConfig alias in source.TemplateAliases ?? new List<PromptUnifiedTemplateAliasConfig>())
            {
                if (alias == null)
                {
                    continue;
                }

                Owner.SetTemplateAlias(alias.TemplateId, alias.Name, alias.Description, alias.Content, alias.Enabled);
            }

            foreach (PromptUnifiedNodeRegistration customNode in source.CustomNodes ?? new List<PromptUnifiedNodeRegistration>())
            {
                if (customNode == null || string.IsNullOrWhiteSpace(customNode.NodeId))
                {
                    continue;
                }

                if (!CustomNodes.Any(c => c != null && string.Equals(c.NodeId, customNode.NodeId, StringComparison.OrdinalIgnoreCase)))
                {
                    CustomNodes.Add(customNode.Clone());
                }
            }
        }

internal static List<PromptUnifiedSectionContent> NormalizeSections(List<PromptUnifiedSectionContent> source)
        {
            var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (PromptUnifiedSectionContent section in source ?? new List<PromptUnifiedSectionContent>())
            {
                if (section == null)
                {
                    continue;
                }

                string id = PromptSectionSchemaCatalog.NormalizeSectionId(section.SectionId);
                string content = section.Content?.Trim() ?? string.Empty;
                if (id.Length == 0)
                {
                    continue;
                }

                merged[id] = content;
            }

            return merged.Select(i => PromptUnifiedSectionContent.Create(i.Key, i.Value)).ToList();
        }

internal static List<PromptUnifiedNodeContent> NormalizeNodes(
            string promptChannel,
            List<PromptUnifiedNodeContent> source,
            PromptUnifiedCatalogNormalizeReport report)
        {
            var allowedNodes = new HashSet<string>(
                PromptUnifiedNodeSchemaCatalog.GetAllowedNodes(promptChannel).Select(item => item.Id),
                StringComparer.OrdinalIgnoreCase);
            var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int sourceCount = (source ?? new List<PromptUnifiedNodeContent>()).Count(node => node != null);
            foreach (PromptUnifiedNodeContent node in source ?? new List<PromptUnifiedNodeContent>())
            {
                if (node == null)
                {
                    continue;
                }

                string id = PromptUnifiedNodeSchemaCatalog.NormalizeId(node.NodeId);
                string content = node.Content?.Trim() ?? string.Empty;
                if (id.Length == 0)
                {
                    continue;
                }

                if (!allowedNodes.Contains(id))
                {
                    continue;
                }

                string migrated = PromptUnifiedChannelConfig.MigrateLegacyRpgRelationshipProfileTemplate(promptChannel, id, content);
                if (!string.Equals(migrated, content, StringComparison.Ordinal))
                {
                    content = migrated;
                    report.MarkChanged();
                }

                merged[id] = content;
            }

            int removedCount = Math.Max(0, sourceCount - merged.Count);
            if (removedCount > 0)
            {
                report.RemovedNodeCount += removedCount;
                report.MarkChanged();
            }

            return merged.Select(i => PromptUnifiedNodeContent.Create(i.Key, i.Value)).ToList();
        }
    }
}
