using System;
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Persistence;
using Verse;
using Ustas.RimAI.Communication.Relations.Prompting.Transfer;

namespace Ustas.RimAI.Communication.Relations.Config
{
    public sealed class PromptUnifiedCatalogNormalizeReport
    {
        public int RemovedNodeCount;
        public int RemovedLayoutCount;
        public int FilledDefaultLayoutCount;
        public int UnknownChannelCount;
        public bool HasStructuralChange;

        internal void Merge(PromptUnifiedCatalogNormalizeReport other)
        {
            if (other == null)
            {
                return;
            }

            RemovedNodeCount += other.RemovedNodeCount;
            RemovedLayoutCount += other.RemovedLayoutCount;
            FilledDefaultLayoutCount += other.FilledDefaultLayoutCount;
            UnknownChannelCount += other.UnknownChannelCount;
            HasStructuralChange |= other.HasStructuralChange;
        }

        internal void MarkChanged()
        {
            HasStructuralChange = true;
        }
    }

    /// <summary>
    /// Dependencies: Verse Scribe and prompt section/node schema catalogs.
    /// Responsibility: single prompt source of truth for channel sections and non-section nodes.
    /// </summary>
    [Serializable]
    public sealed class PromptUnifiedCatalog : IExposable
    {
        public const int CurrentSchemaVersion = 3;

        public int SchemaVersion = CurrentSchemaVersion;
        public int MigrationVersion = 1;
        public bool LegacyMigrated;
        public List<PromptUnifiedChannelConfig> Channels = new List<PromptUnifiedChannelConfig>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref SchemaVersion, "schemaVersion", CurrentSchemaVersion);
            Scribe_Values.Look(ref MigrationVersion, "migrationVersion", 1);
            Scribe_Values.Look(ref LegacyMigrated, "legacyMigrated", false);
            Scribe_Collections.Look(ref Channels, "channels", LookMode.Deep);
            Channels ??= new List<PromptUnifiedChannelConfig>();
        }

        public PromptUnifiedCatalog Clone()
        {
            return new PromptUnifiedCatalog
            {
                SchemaVersion = SchemaVersion,
                MigrationVersion = MigrationVersion,
                LegacyMigrated = LegacyMigrated,
                Channels = Channels?
                    .Where(c => c != null)
                    .Select(c => c.Clone())
                    .ToList() ?? new List<PromptUnifiedChannelConfig>()
            };
        }

        public void NormalizeWith(PromptUnifiedCatalog fallback)
        {
            _ = NormalizeWithReport(fallback);
        }

        public PromptUnifiedCatalogNormalizeReport NormalizeWithReport(PromptUnifiedCatalog fallback)
        {
            var report = new PromptUnifiedCatalogNormalizeReport();
            fallback ??= CreateFallback();
            Channels ??= new List<PromptUnifiedChannelConfig>();
            var merged = new Dictionary<string, PromptUnifiedChannelConfig>(StringComparer.OrdinalIgnoreCase);
            MergeChannels(merged, fallback.Channels, report);
            MergeChannels(merged, Channels, report);
            Channels = merged.Values.ToList();
            for (int i = 0; i < Channels.Count; i++)
            {
                if (Channels[i] == null)
                {
                    report.MarkChanged();
                    continue;
                }

                PromptUnifiedCatalogNormalizeReport channelReport = Channels[i].NormalizeWithReport();
                report.Merge(channelReport);
            }

            if (SchemaVersion <= 0)
            {
                SchemaVersion = CurrentSchemaVersion;
                report.MarkChanged();
            }

            return report;
        }

        public void ValidateInvariantsOrThrow()
        {
            if (Channels == null)
            {
                throw new InvalidOperationException("[RimAI.Relations] Unified prompt catalog channels list cannot be null.");
            }

            foreach (PromptUnifiedChannelConfig channel in Channels)
            {
                if (channel == null)
                {
                    throw new InvalidOperationException("[RimAI.Relations] Unified prompt catalog contains a null channel entry.");
                }

                string channelId = PromptUnifiedNodeSchemaCatalog.NormalizeStrictChannelOrThrow(channel.PromptChannel);
                var allowedNodes = new HashSet<string>(
                    PromptUnifiedNodeSchemaCatalog.GetAllowedNodesStrict(channelId).Select(item => item.Id),
                    StringComparer.OrdinalIgnoreCase);
                var layoutNodeSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                List<PromptUnifiedNodeContent> nodes = channel.Nodes ?? new List<PromptUnifiedNodeContent>();
                List<PromptUnifiedNodeLayoutConfig> layouts = channel.NodeLayout ?? new List<PromptUnifiedNodeLayoutConfig>();
                if (allowedNodes.Count == 0)
                {
                    if (HasAnyNodeEntry(nodes) || HasAnyLayoutEntry(layouts))
                    {
                        throw new InvalidOperationException(
                            $"[RimAI.Relations] Channel '{channelId}' must not contain node content or layout entries.");
                    }

                    continue;
                }

                foreach (PromptUnifiedNodeContent node in nodes)
                {
                    if (node == null)
                    {
                        continue;
                    }

                    string nodeId = PromptUnifiedNodeSchemaCatalog.NormalizeId(node.NodeId);
                    if (nodeId.Length == 0)
                    {
                        throw new InvalidOperationException(
                            $"[RimAI.Relations] Channel '{channelId}' contains an empty node id.");
                    }

                    if (!allowedNodes.Contains(nodeId))
                    {
                        throw new InvalidOperationException(
                            $"[RimAI.Relations] Channel '{channelId}' contains disallowed node '{nodeId}'.");
                    }
                }

                foreach (PromptUnifiedNodeLayoutConfig layout in layouts)
                {
                    if (layout == null)
                    {
                        continue;
                    }

                    string nodeId = PromptUnifiedNodeSchemaCatalog.NormalizeId(layout.NodeId);
                    if (nodeId.Length == 0)
                    {
                        throw new InvalidOperationException(
                            $"[RimAI.Relations] Channel '{channelId}' contains an empty node layout id.");
                    }

                    if (!allowedNodes.Contains(nodeId))
                    {
                        throw new InvalidOperationException(
                            $"[RimAI.Relations] Channel '{channelId}' contains disallowed node layout '{nodeId}'.");
                    }

                    if (!layoutNodeSet.Add(nodeId))
                    {
                        throw new InvalidOperationException(
                            $"[RimAI.Relations] Channel '{channelId}' contains duplicate node layout '{nodeId}'.");
                    }
                }
            }
        }

        public string ResolveSection(string promptChannel, string sectionId)
        {
            string channel = RimTalkPromptEntryChannelCatalog.NormalizeLoose(promptChannel);
            string section = PromptSectionSchemaCatalog.NormalizeSectionId(sectionId);
            if (string.IsNullOrWhiteSpace(section))
            {
                return string.Empty;
            }

            PromptUnifiedChannelConfig channelConfig = ResolveChannel(channel);
            if (channelConfig != null && channelConfig.TryResolveSection(section, out string text))
            {
                return text;
            }

            PromptUnifiedChannelConfig anyConfig = ResolveChannel(RimTalkPromptEntryChannelCatalog.Any);
            return anyConfig != null && anyConfig.TryResolveSection(section, out string anyText)
                ? anyText
                : string.Empty;
        }

        public string ResolveNode(string promptChannel, string nodeId)
        {
            string channel = PromptUnifiedNodeSchemaCatalog.NormalizeStrictChannelOrThrow(promptChannel);
            string normalizedNode = RequireNodeIdOrThrow(nodeId, nameof(ResolveNode), channel);
            PromptUnifiedNodeSchemaCatalog.EnsureNodeAllowedForChannelOrThrow(channel, normalizedNode, nameof(ResolveNode));

            PromptUnifiedChannelConfig channelConfig = ResolveChannel(channel);
            if (channelConfig != null && channelConfig.TryResolveNode(normalizedNode, out string text))
            {
                return text;
            }

            PromptUnifiedChannelConfig anyConfig = ResolveChannel(RimTalkPromptEntryChannelCatalog.Any);
            return anyConfig != null && anyConfig.TryResolveNode(normalizedNode, out string anyText)
                ? anyText
                : string.Empty;
        }

        public PromptUnifiedNodeLayoutConfig ResolveNodeLayout(string promptChannel, string nodeId)
        {
            string channel = PromptUnifiedNodeSchemaCatalog.NormalizeStrictChannelOrThrow(promptChannel);
            string normalizedNode = RequireNodeIdOrThrow(nodeId, nameof(ResolveNodeLayout), channel);
            PromptUnifiedNodeSchemaCatalog.EnsureNodeAllowedForChannelOrThrow(channel, normalizedNode, nameof(ResolveNodeLayout));

            PromptUnifiedNodeLayoutConfig layout = ResolveChannel(channel)?.ResolveNodeLayout(normalizedNode);
            if (layout != null)
            {
                return layout;
            }

            layout = ResolveChannel(RimTalkPromptEntryChannelCatalog.Any)?.ResolveNodeLayout(normalizedNode);
            if (layout != null)
            {
                return layout;
            }

            return PromptUnifiedNodeLayoutDefaults.BuildDefaultLayout(channel, normalizedNode);
        }

        public void SetSection(string promptChannel, string sectionId, string content)
        {
            string channel = RimTalkPromptEntryChannelCatalog.NormalizeLoose(promptChannel);
            string section = PromptSectionSchemaCatalog.NormalizeSectionId(sectionId);
            if (string.IsNullOrWhiteSpace(section))
            {
                return;
            }

            GetOrCreateChannel(channel).SetSection(section, content);
        }

        public void SetNode(string promptChannel, string nodeId, string content)
        {
            string channel = PromptUnifiedNodeSchemaCatalog.NormalizeStrictChannelOrThrow(promptChannel);
            string normalizedNode = RequireNodeIdOrThrow(nodeId, nameof(SetNode), channel);
            PromptUnifiedNodeSchemaCatalog.EnsureNodeAllowedForChannelOrThrow(channel, normalizedNode, nameof(SetNode));

            GetOrCreateChannel(channel).SetNode(normalizedNode, content);
        }

        public void SetNodeLayout(string promptChannel, string nodeId, PromptUnifiedNodeSlot slot, int order, bool enabled)
        {
            string channel = PromptUnifiedNodeSchemaCatalog.NormalizeStrictChannelOrThrow(promptChannel);
            string normalizedNode = RequireNodeIdOrThrow(nodeId, nameof(SetNodeLayout), channel);
            PromptUnifiedNodeSchemaCatalog.EnsureNodeAllowedForChannelOrThrow(channel, normalizedNode, nameof(SetNodeLayout));

            GetOrCreateChannel(channel).SetNodeLayout(normalizedNode, slot, order, enabled);
        }

        public List<PromptUnifiedNodeLayoutConfig> GetOrderedNodeLayouts(string promptChannel)
        {
            string channel = RimTalkPromptEntryChannelCatalog.NormalizeLoose(promptChannel);
            return GetOrCreateChannel(channel).GetOrderedNodeLayouts(channel);
        }

        public List<PromptSectionLayoutConfig> GetOrderedSectionLayouts(string promptChannel)
        {
            string channel = RimTalkPromptEntryChannelCatalog.NormalizeLoose(promptChannel);
            return GetOrCreateChannel(channel).GetOrderedSectionLayouts();
        }

        public void SetSectionLayout(string promptChannel, string sectionId, int order)
        {
            string channel = RimTalkPromptEntryChannelCatalog.NormalizeLoose(promptChannel);
            string normalizedSection = PromptSectionSchemaCatalog.NormalizeSectionId(sectionId);
            if (string.IsNullOrWhiteSpace(normalizedSection))
            {
                return;
            }

            GetOrCreateChannel(channel).SetSectionLayout(normalizedSection, order);
        }

        public List<PromptUnifiedTemplateAliasConfig> GetTemplateAliases(string promptChannel)
        {
            string channel = RimTalkPromptEntryChannelCatalog.NormalizeLoose(promptChannel);
            return GetOrCreateChannel(channel)
                .GetTemplateAliases()
                .Select(item => item.Clone())
                .ToList();
        }

        public PromptUnifiedTemplateAliasConfig ResolveTemplateAlias(string promptChannel, string templateId)
        {
            string channel = RimTalkPromptEntryChannelCatalog.NormalizeLoose(promptChannel);
            PromptUnifiedTemplateAliasConfig alias = ResolveChannel(channel)?.ResolveTemplateAlias(templateId);
            if (alias != null)
            {
                return alias.Clone();
            }

            return ResolveChannel(RimTalkPromptEntryChannelCatalog.Any)?.ResolveTemplateAlias(templateId)?.Clone();
        }

        public PromptUnifiedTemplateAliasConfig ResolvePreferredTemplateAlias(
            string promptChannel,
            string preferredTemplateId)
        {
            string channel = RimTalkPromptEntryChannelCatalog.NormalizeLoose(promptChannel);
            PromptUnifiedTemplateAliasConfig alias = ResolveChannel(channel)
                ?.ResolvePreferredTemplateAlias(preferredTemplateId);
            if (alias != null)
            {
                return alias.Clone();
            }

            return ResolveChannel(RimTalkPromptEntryChannelCatalog.Any)
                ?.ResolvePreferredTemplateAlias(preferredTemplateId)
                ?.Clone();
        }

        public void SetTemplateAlias(
            string promptChannel,
            string templateId,
            string name,
            string description,
            string content,
            bool enabled)
        {
            string channel = RimTalkPromptEntryChannelCatalog.NormalizeLoose(promptChannel);
            GetOrCreateChannel(channel).SetTemplateAlias(
                templateId,
                name,
                description,
                content,
                enabled);
        }

        public RimTalkPromptEntryDefaultsConfig ToSectionCatalog()
        {
            var sectionConfig = new RimTalkPromptEntryDefaultsConfig
            {
                Channels = new List<RimTalkPromptChannelDefaultsConfig>()
            };

            foreach (PromptUnifiedChannelConfig channel in Channels ?? Enumerable.Empty<PromptUnifiedChannelConfig>())
            {
                if (channel == null)
                {
                    continue;
                }

                var sections = channel.Sections?
                    .Where(s => s != null && !string.IsNullOrWhiteSpace(s.SectionId))
                    .Select(s => RimTalkPromptSectionDefaultConfig.Create(s.SectionId, s.Content))
                    .ToList() ?? new List<RimTalkPromptSectionDefaultConfig>();
                sectionConfig.Channels.Add(RimTalkPromptChannelDefaultsConfig.Create(channel.PromptChannel, sections));
            }

            sectionConfig.NormalizeWith(RimTalkPromptEntryDefaultsConfig.CreateFallback());
            return sectionConfig;
        }

        public static PromptUnifiedCatalog FromLegacy(
            RimTalkPromptEntryDefaultsConfig sections,
            PromptTemplateTextConfig templates)
        {
            var catalog = CreateFallback();
            catalog.LegacyMigrated = true;
            RimTalkPromptEntryDefaultsConfig normalizedSections = sections?.Clone() ?? RimTalkPromptEntryDefaultsProvider.GetDefaultsSnapshot();
            normalizedSections.NormalizeWith(RimTalkPromptEntryDefaultsProvider.GetDefaultsSnapshot());
            foreach (RimTalkPromptChannelDefaultsConfig channel in normalizedSections.Channels ?? new List<RimTalkPromptChannelDefaultsConfig>())
            {
                if (channel == null)
                {
                    continue;
                }

                foreach (RimTalkPromptSectionDefaultConfig section in channel.Sections ?? new List<RimTalkPromptSectionDefaultConfig>())
                {
                    if (section == null || string.IsNullOrWhiteSpace(section.SectionId))
                    {
                        continue;
                    }

                    catalog.SetSection(channel.PromptChannel, section.SectionId, section.Content);
                }
            }

            if (templates != null)
            {
                ApplyLegacyTemplatesToNodes(catalog, templates);
            }

            return catalog;
        }

        public static PromptUnifiedCatalog CreateFallback()
        {
            var fallback = new PromptUnifiedCatalog();
            foreach (RimTalkPromptChannelDefaultsConfig channel in RimTalkPromptEntryDefaultsProvider.GetDefaultsSnapshot().Channels ?? new List<RimTalkPromptChannelDefaultsConfig>())
            {
                if (channel == null)
                {
                    continue;
                }

                foreach (RimTalkPromptSectionDefaultConfig section in channel.Sections ?? new List<RimTalkPromptSectionDefaultConfig>())
                {
                    if (section == null)
                    {
                        continue;
                    }

                    fallback.SetSection(channel.PromptChannel, section.SectionId, section.Content);
                }
            }

            PromptUnifiedDefaults.ApplyFallbackNodes(fallback);
            return fallback;
        }

        internal static void ApplyLegacyTemplatesToNodes(PromptUnifiedCatalog catalog, PromptTemplateTextConfig templates)
        {
            if (catalog == null || templates == null)
            {
                return;
            }

            SetNodeIfNotEmpty(catalog, RimTalkPromptEntryChannelCatalog.Any, "fact_grounding", templates.FactGroundingTemplate);
            SetNodeIfNotEmpty(catalog, RimTalkPromptEntryChannelCatalog.Any, "output_language", templates.OutputLanguageTemplate);
            SetNodeIfNotEmpty(catalog, RimTalkPromptEntryChannelCatalog.Any, "decision_policy", templates.DecisionPolicyTemplate);
            SetNodeIfNotEmpty(catalog, RimTalkPromptEntryChannelCatalog.Any, "turn_objective", templates.TurnObjectiveTemplate);
            SetNodeIfNotEmpty(catalog, RimTalkPromptEntryChannelCatalog.Any, "opening_objective", templates.OpeningObjectiveTemplate);
            SetNodeIfNotEmpty(catalog, RimTalkPromptEntryChannelCatalog.Any, "topic_shift_rule", templates.TopicShiftRuleTemplate);
            SetNodeIfNotEmpty(catalog, RimTalkPromptEntryChannelCatalog.Any, "rpg_proactive_romance", templates.ProactiveRomanceRuleTemplate);
            SetNodeIfNotEmpty(catalog, RimTalkPromptEntryChannelCatalog.Any, "rpg_proactive_social", templates.ProactiveSocialActionRuleTemplate);
            SetNodeIfNotEmpty(catalog, RimTalkPromptEntryChannelCatalog.Any, "diplomacy_fallback_role", templates.DiplomacyFallbackRoleTemplate);
            SetNodeIfNotEmpty(catalog, RimTalkPromptEntryChannelCatalog.Any, "social_circle_action_rule", templates.SocialCircleActionRuleTemplate);
            SetNodeIfNotEmpty(catalog, RimTalkPromptEntryChannelCatalog.Any, "api_limits_node_template", templates.ApiLimitsNodeTemplate);
            SetNodeIfNotEmpty(catalog, RimTalkPromptEntryChannelCatalog.Any, "quest_guidance_node_template", templates.QuestGuidanceNodeTemplate);
            SetNodeIfNotEmpty(catalog, RimTalkPromptEntryChannelCatalog.Any, "response_contract_node_template", templates.ResponseContractNodeTemplate);
            SetNodeIfNotEmpty(catalog, RimTalkPromptEntryChannelCatalog.Any, "social_news_style", templates.SocialCircleNewsStyleTemplate);
            SetNodeIfNotEmpty(catalog, RimTalkPromptEntryChannelCatalog.Any, "social_news_json_contract", templates.SocialCircleNewsJsonContractTemplate);
            SetNodeIfNotEmpty(catalog, RimTalkPromptEntryChannelCatalog.Any, "social_news_fact", templates.SocialCircleNewsFactTemplate);
            SetNodeIfNotEmpty(catalog, RimTalkPromptEntryChannelCatalog.Any, "rpg_role_setting_fallback", templates.RpgRoleSettingTemplate);
        }

        internal static void SetNodeIfNotEmpty(PromptUnifiedCatalog catalog, string channel, string nodeId, string content)
        {
            string text = content?.Trim() ?? string.Empty;
            if (text.Length == 0)
            {
                return;
            }

            catalog.SetNode(channel, nodeId, text);
        }

        internal static bool HasAnyNodeEntry(IEnumerable<PromptUnifiedNodeContent> nodes)
        {
            return (nodes ?? Enumerable.Empty<PromptUnifiedNodeContent>())
                .Any(node => node != null && PromptUnifiedNodeSchemaCatalog.NormalizeId(node.NodeId).Length > 0);
        }

        internal static bool HasAnyLayoutEntry(IEnumerable<PromptUnifiedNodeLayoutConfig> layouts)
        {
            return (layouts ?? Enumerable.Empty<PromptUnifiedNodeLayoutConfig>())
                .Any(layout => layout != null && PromptUnifiedNodeSchemaCatalog.NormalizeId(layout.NodeId).Length > 0);
        }

        internal static string RequireNodeIdOrThrow(string nodeId, string operation, string channel)
        {
            string normalizedNode = PromptUnifiedNodeSchemaCatalog.NormalizeId(nodeId);
            if (normalizedNode.Length > 0)
            {
                return normalizedNode;
            }

            throw new InvalidOperationException(
                $"[RimAI.Relations] {operation} requires a non-empty nodeId for channel '{channel}'.");
        }

        internal PromptUnifiedChannelConfig ResolveChannel(string promptChannel)
        {
            return Channels?.FirstOrDefault(c =>
                c != null && string.Equals(c.PromptChannel, promptChannel, StringComparison.OrdinalIgnoreCase));
        }

        internal PromptUnifiedChannelConfig GetOrCreateChannel(string promptChannel)
        {
            Channels ??= new List<PromptUnifiedChannelConfig>();
            PromptUnifiedChannelConfig existing = ResolveChannel(promptChannel);
            if (existing != null)
            {
                return existing;
            }

            existing = new PromptUnifiedChannelConfig { PromptChannel = promptChannel };
            Channels.Add(existing);
            return existing;
        }

        internal static void MergeChannels(
            IDictionary<string, PromptUnifiedChannelConfig> target,
            IEnumerable<PromptUnifiedChannelConfig> source,
            PromptUnifiedCatalogNormalizeReport report)
        {
            if (source == null)
            {
                return;
            }

            foreach (PromptUnifiedChannelConfig channel in source)
            {
                if (channel == null)
                {
                    continue;
                }

                if (IsUnknownChannel(channel.PromptChannel))
                {
                    if (report != null)
                    {
                        report.UnknownChannelCount++;
                        report.MarkChanged();
                    }
                }

                string channelId = RimTalkPromptEntryChannelCatalog.NormalizeLoose(channel.PromptChannel);
                if (!target.TryGetValue(channelId, out PromptUnifiedChannelConfig merged))
                {
                    merged = new PromptUnifiedChannelConfig { PromptChannel = channelId };
                    target[channelId] = merged;
                }

                merged.Merge(channel);
            }
        }

        internal static bool IsUnknownChannel(string channelId)
        {
            if (string.IsNullOrWhiteSpace(channelId))
            {
                return true;
            }

            string normalized = channelId.Trim().ToLowerInvariant();
            string loose = RimTalkPromptEntryChannelCatalog.NormalizeLoose(normalized);
            return loose == RimTalkPromptEntryChannelCatalog.Any &&
                !string.Equals(normalized, RimTalkPromptEntryChannelCatalog.Any, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Serializable]
    public sealed class PromptUnifiedChannelConfig : IExposable
    {
        internal PromptUnifiedChannelConfigParts Parts;

        public PromptUnifiedChannelConfig()
        {
            Parts = new PromptUnifiedChannelConfigParts(this);
        }

        public string PromptChannel = RimTalkPromptEntryChannelCatalog.Any;
        public List<PromptUnifiedSectionContent> Sections = new List<PromptUnifiedSectionContent>();
        public List<PromptUnifiedNodeContent> Nodes = new List<PromptUnifiedNodeContent>();
        public List<PromptUnifiedNodeLayoutConfig> NodeLayout = new List<PromptUnifiedNodeLayoutConfig>();
        public List<PromptSectionLayoutConfig> SectionLayout = new List<PromptSectionLayoutConfig>();
        public List<PromptUnifiedTemplateAliasConfig> TemplateAliases = new List<PromptUnifiedTemplateAliasConfig>();
        public List<PromptUnifiedNodeRegistration> CustomNodes = new List<PromptUnifiedNodeRegistration>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref PromptChannel, "promptChannel", RimTalkPromptEntryChannelCatalog.Any);
            Scribe_Collections.Look(ref Sections, "sections", LookMode.Deep);
            Scribe_Collections.Look(ref Nodes, "nodes", LookMode.Deep);
            Scribe_Collections.Look(ref NodeLayout, "nodeLayout", LookMode.Deep);
            Scribe_Collections.Look(ref SectionLayout, "sectionLayout", LookMode.Deep);
            Scribe_Collections.Look(ref TemplateAliases, "templateAliases", LookMode.Deep);
            Scribe_Collections.Look(ref CustomNodes, "customNodes", LookMode.Deep);
            PromptChannel = RimTalkPromptEntryChannelCatalog.NormalizeLoose(PromptChannel);
            Sections ??= new List<PromptUnifiedSectionContent>();
            Nodes ??= new List<PromptUnifiedNodeContent>();
            NodeLayout ??= new List<PromptUnifiedNodeLayoutConfig>();
            SectionLayout ??= new List<PromptSectionLayoutConfig>();
            TemplateAliases ??= new List<PromptUnifiedTemplateAliasConfig>();
            CustomNodes ??= new List<PromptUnifiedNodeRegistration>();
        }

        

        public void Normalize()
        {
            _ = NormalizeWithReport();
        }

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        
    
        #region Cluster forwards
        public PromptUnifiedChannelConfig Clone() => Parts.Slice1.Clone();
        public PromptUnifiedCatalogNormalizeReport NormalizeWithReport() => Parts.Slice1.NormalizeWithReport();
        public string ResolveSection(string sectionId) => Parts.Slice1.ResolveSection(sectionId);
        public bool TryResolveSection(string sectionId, out string content) => Parts.Slice1.TryResolveSection(sectionId, out content);
        public string ResolveNode(string nodeId) => Parts.Slice1.ResolveNode(nodeId);
        public bool TryResolveNode(string nodeId, out string content) => Parts.Slice1.TryResolveNode(nodeId, out content);
        public PromptUnifiedNodeLayoutConfig ResolveNodeLayout(string nodeId) => Parts.Slice1.ResolveNodeLayout(nodeId);
        public List<PromptUnifiedTemplateAliasConfig> GetTemplateAliases() => Parts.Slice1.GetTemplateAliases();
        public PromptUnifiedTemplateAliasConfig ResolveTemplateAlias(string templateId) => Parts.Slice1.ResolveTemplateAlias(templateId);
        public PromptUnifiedTemplateAliasConfig ResolvePreferredTemplateAlias(string preferredTemplateId) => Parts.Slice1.ResolvePreferredTemplateAlias(preferredTemplateId);
        public void SetSection(string sectionId, string content) => Parts.Slice1.SetSection(sectionId, content);
        public void SetSectionLayout(string sectionId, int order) => Parts.Slice1.SetSectionLayout(sectionId, order);
        public List<PromptSectionLayoutConfig> GetOrderedSectionLayouts() => Parts.Slice1.GetOrderedSectionLayouts();
        public void SetNode(string nodeId, string content) => Parts.Slice1.SetNode(nodeId, content);
        public void SetNodeLayout(string nodeId, PromptUnifiedNodeSlot slot, int order, bool enabled) => Parts.Slice1.SetNodeLayout(nodeId, slot, order, enabled);
        public void SetTemplateAlias(string templateId, string name, string description, string content, bool enabled) => Parts.Slice1.SetTemplateAlias(templateId, name, description, content, enabled);
        public List<PromptUnifiedNodeLayoutConfig> GetOrderedNodeLayouts(string promptChannel) => Parts.Slice1.GetOrderedNodeLayouts(promptChannel);
        public void Merge(PromptUnifiedChannelConfig source) => Parts.Slice1.Merge(source);
        internal static List<PromptUnifiedSectionContent> NormalizeSections(List<PromptUnifiedSectionContent> source) => PromptUnifiedChannelSlice1.NormalizeSections(source);
        internal static List<PromptUnifiedNodeContent> NormalizeNodes(string promptChannel, List<PromptUnifiedNodeContent> source, PromptUnifiedCatalogNormalizeReport report) => PromptUnifiedChannelSlice1.NormalizeNodes(promptChannel, source, report);
        internal static List<PromptUnifiedNodeLayoutConfig> NormalizeNodeLayout(string promptChannel, List<PromptUnifiedNodeLayoutConfig> source, PromptUnifiedCatalogNormalizeReport report) => PromptUnifiedChannelSlice2.NormalizeNodeLayout(promptChannel, source, report);
        internal static List<PromptUnifiedTemplateAliasConfig> NormalizeTemplateAliases(List<PromptUnifiedTemplateAliasConfig> source) => PromptUnifiedChannelSlice2.NormalizeTemplateAliases(source);
        internal static string MigrateLegacyRpgRelationshipProfileTemplate(string promptChannel, string nodeId, string template) => PromptUnifiedChannelSlice2.MigrateLegacyRpgRelationshipProfileTemplate(promptChannel, nodeId, template);
        internal static string WrapLegacyGuidanceLine(string template, string lineText) => PromptUnifiedChannelSlice2.WrapLegacyGuidanceLine(template, lineText);
        #endregion
}
    internal sealed class PromptUnifiedChannelConfigParts
    {
        internal readonly PromptUnifiedChannelConfig Owner;
        internal readonly PromptUnifiedChannelSlice1 Slice1;
        internal readonly PromptUnifiedChannelSlice2 Slice2;
        internal PromptUnifiedChannelConfigParts(PromptUnifiedChannelConfig owner)
        {
            Owner = owner;
            Slice1 = new PromptUnifiedChannelSlice1(owner);
            Slice2 = new PromptUnifiedChannelSlice2(owner);
        }
    }


    [Serializable]
    public sealed class PromptUnifiedSectionContent : IExposable
    {
        public string SectionId = string.Empty;
        public string Content = string.Empty;

        public void ExposeData()
        {
            Scribe_Values.Look(ref SectionId, "sectionId", string.Empty);
            Scribe_Values.Look(ref Content, "content", string.Empty);
            SectionId = PromptSectionSchemaCatalog.NormalizeSectionId(SectionId);
            Content = Content?.Trim() ?? string.Empty;
        }

        public PromptUnifiedSectionContent Clone()
        {
            return Create(SectionId, Content);
        }

        public static PromptUnifiedSectionContent Create(string sectionId, string content)
        {
            return new PromptUnifiedSectionContent
            {
                SectionId = PromptSectionSchemaCatalog.NormalizeSectionId(sectionId),
                Content = content?.Trim() ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class PromptUnifiedNodeContent : IExposable
    {
        public string NodeId = string.Empty;
        public string Content = string.Empty;

        public void ExposeData()
        {
            Scribe_Values.Look(ref NodeId, "nodeId", string.Empty);
            Scribe_Values.Look(ref Content, "content", string.Empty);
            NodeId = PromptUnifiedNodeSchemaCatalog.NormalizeId(NodeId);
            Content = Content?.Trim() ?? string.Empty;
        }

        public PromptUnifiedNodeContent Clone()
        {
            return Create(NodeId, Content);
        }

        public static PromptUnifiedNodeContent Create(string nodeId, string content)
        {
            return new PromptUnifiedNodeContent
            {
                NodeId = PromptUnifiedNodeSchemaCatalog.NormalizeId(nodeId),
                Content = content?.Trim() ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class PromptUnifiedTemplateAliasConfig : IExposable
    {
        public string TemplateId = string.Empty;
        public string Name = string.Empty;
        public string Description = string.Empty;
        public string Content = string.Empty;
        public bool Enabled = true;

        public void ExposeData()
        {
            Scribe_Values.Look(ref TemplateId, "templateId", string.Empty);
            Scribe_Values.Look(ref Name, "name", string.Empty);
            Scribe_Values.Look(ref Description, "description", string.Empty);
            Scribe_Values.Look(ref Content, "content", string.Empty);
            Scribe_Values.Look(ref Enabled, "enabled", true);
            TemplateId = NormalizeTemplateId(TemplateId);
            Name = Name?.Trim() ?? string.Empty;
            Description = Description?.Trim() ?? string.Empty;
            Content = Content?.Trim() ?? string.Empty;
        }

        public PromptUnifiedTemplateAliasConfig Clone()
        {
            return Create(TemplateId, Name, Description, Content, Enabled);
        }

        public static PromptUnifiedTemplateAliasConfig Create(
            string templateId,
            string name,
            string description,
            string content,
            bool enabled)
        {
            return new PromptUnifiedTemplateAliasConfig
            {
                TemplateId = NormalizeTemplateId(templateId),
                Name = name?.Trim() ?? string.Empty,
                Description = description?.Trim() ?? string.Empty,
                Content = content?.Trim() ?? string.Empty,
                Enabled = enabled
            };
        }

        public static string NormalizeTemplateId(string templateId)
        {
            return string.IsNullOrWhiteSpace(templateId)
                ? string.Empty
                : templateId.Trim().ToLowerInvariant();
        }
    }

    public enum PromptUnifiedNodeSlot
    {
        MetadataAfter = 0,
        MainChainBefore = 1,
        MainChainAfter = 2,
        DynamicDataAfter = 3,
        ContractBeforeEnd = 4
    }

    [Serializable]
    public sealed class PromptUnifiedNodeLayoutConfig : IExposable
    {
        public string NodeId = string.Empty;
        public string Slot = PromptUnifiedNodeSlot.MainChainAfter.ToSerializedValue();
        public int Order = int.MaxValue;
        public bool Enabled = true;

        public void ExposeData()
        {
            Scribe_Values.Look(ref NodeId, "nodeId", string.Empty);
            Scribe_Values.Look(ref Slot, "slot", PromptUnifiedNodeSlot.MainChainAfter.ToSerializedValue());
            Scribe_Values.Look(ref Order, "order", int.MaxValue);
            Scribe_Values.Look(ref Enabled, "enabled", true);
            NodeId = PromptUnifiedNodeSchemaCatalog.NormalizeId(NodeId);
            Slot = PromptUnifiedNodeSlotExtensions.NormalizeSerializedValue(Slot);
            if (Order < 0)
            {
                Order = 0;
            }
        }

        public PromptUnifiedNodeSlot GetSlot()
        {
            return Slot.ToPromptUnifiedNodeSlot();
        }

        public PromptUnifiedNodeLayoutConfig Clone()
        {
            return Create(NodeId, GetSlot(), Order, Enabled);
        }

        public static PromptUnifiedNodeLayoutConfig Create(string nodeId, PromptUnifiedNodeSlot slot, int order, bool enabled)
        {
            return new PromptUnifiedNodeLayoutConfig
            {
                NodeId = PromptUnifiedNodeSchemaCatalog.NormalizeId(nodeId),
                Slot = slot.ToSerializedValue(),
                Order = Math.Max(0, order),
                Enabled = enabled
            };
        }
    }

    internal static class PromptUnifiedNodeLayoutDefaults
    {
        internal static PromptUnifiedNodeLayoutConfig BuildDefaultLayout(string promptChannel, string nodeId)
        {
            string channel = RimTalkPromptEntryChannelCatalog.NormalizeLoose(promptChannel);
            string id = PromptUnifiedNodeSchemaCatalog.NormalizeId(nodeId);
            return PromptUnifiedNodeLayoutConfig.Create(
                id,
                ResolveDefaultSlot(channel, id),
                ResolveDefaultOrder(channel, id),
                true);
        }

        internal static PromptUnifiedNodeSlot ResolveDefaultSlot(string promptChannel, string nodeId)
        {
            return PromptUnifiedNodeSlot.MainChainBefore;
        }

        internal static int ResolveDefaultOrder(string promptChannel, string nodeId)
        {
            switch (nodeId)
            {
                case "fact_grounding": return 10;
                case "output_language": return 20;
                case "decision_policy": return 30;
                case "turn_objective": return 40;
                case "topic_shift_rule": return 50;
                case "opening_objective": return 60;
                case "diplomacy_fallback_role": return 110;
                case "social_circle_action_rule": return 120;
                case "rpg_role_setting_fallback": return 130;
                case "rpg_relationship_profile": return 140;
                case "rpg_kinship_boundary": return 150;
                case "rpg_proactive_romance": return 151;
                case "rpg_proactive_social": return 152;
                case "social_news_style": return 160;
                case "social_news_json_contract": return 170;
                case "social_news_fact": return 180;
                case "api_limits_node_template": return 210;
                case "quest_guidance_node_template": return 220;
                case "response_contract_node_template": return 230;
                case "diplomacy_state_override": return 235;
                case "rpg_body_emotion_override": return 235;
                case "rpg_state_anchor": return 236;
                case "rpg_survival_instinct": return 237;
                case "diplomacy_alive_feeling": return 236;
                case "rpg_alive_feeling": return 238;
                case "strategy_output_contract": return 240;
                case "strategy_player_negotiator_context_template": return 250;
                case "strategy_fact_pack_template": return 260;
                case "strategy_scenario_dossier_template": return 270;
                default: return 1000;
            }
        }
    }

    [Serializable]
    public sealed class PromptSectionLayoutConfig : IExposable
    {
        public string SectionId = string.Empty;
        public int Order = int.MaxValue;
        public bool Enabled = true;

        public PromptSectionLayoutConfig() { }

        internal PromptSectionLayoutConfig(string sectionId, int order, bool enabled = true)
        {
            SectionId = sectionId ?? string.Empty;
            Order = Math.Max(0, order);
            Enabled = enabled;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref SectionId, "sectionId", string.Empty);
            Scribe_Values.Look(ref Order, "order", int.MaxValue);
            Scribe_Values.Look(ref Enabled, "enabled", true);
            SectionId = PromptSectionSchemaCatalog.NormalizeSectionId(SectionId);
            if (Order < 0)
            {
                Order = 0;
            }
        }

        public PromptSectionLayoutConfig Clone()
        {
            return new PromptSectionLayoutConfig(SectionId, Order, Enabled);
        }

        public static PromptSectionLayoutConfig Create(string sectionId, int order, bool enabled = true)
        {
            return new PromptSectionLayoutConfig(
                PromptSectionSchemaCatalog.NormalizeSectionId(sectionId),
                order,
                enabled);
        }
    }

    internal static class PromptSectionLayoutDefaults
    {
        internal static PromptSectionLayoutConfig BuildDefaultLayout(string sectionId, int defaultOrder)
        {
            return PromptSectionLayoutConfig.Create(sectionId, defaultOrder);
        }
    }

    internal static class PromptUnifiedNodeSlotExtensions
    {
        internal static PromptUnifiedNodeSlot ToPromptUnifiedNodeSlot(this string serializedValue)
        {
            string normalized = string.IsNullOrWhiteSpace(serializedValue)
                ? string.Empty
                : serializedValue.Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "metadata_after":
                    return PromptUnifiedNodeSlot.MetadataAfter;
                case "main_chain_before":
                    return PromptUnifiedNodeSlot.MainChainBefore;
                case "main_chain_after":
                    return PromptUnifiedNodeSlot.MainChainAfter;
                case "dynamic_data_after":
                    return PromptUnifiedNodeSlot.DynamicDataAfter;
                case "contract_before_end":
                    return PromptUnifiedNodeSlot.ContractBeforeEnd;
                default:
                    return PromptUnifiedNodeSlot.MainChainAfter;
            }
        }

        internal static string ToSerializedValue(this PromptUnifiedNodeSlot slot)
        {
            switch (slot)
            {
                case PromptUnifiedNodeSlot.MetadataAfter:
                    return "metadata_after";
                case PromptUnifiedNodeSlot.MainChainBefore:
                    return "main_chain_before";
                case PromptUnifiedNodeSlot.MainChainAfter:
                    return "main_chain_after";
                case PromptUnifiedNodeSlot.DynamicDataAfter:
                    return "dynamic_data_after";
                case PromptUnifiedNodeSlot.ContractBeforeEnd:
                    return "contract_before_end";
                default:
                    return "main_chain_after";
            }
        }

        internal static string NormalizeSerializedValue(string serializedValue)
        {
            return ToPromptUnifiedNodeSlot(serializedValue).ToSerializedValue();
        }
    }
}
