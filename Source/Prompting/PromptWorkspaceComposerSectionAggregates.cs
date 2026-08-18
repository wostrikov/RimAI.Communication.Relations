using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.Context;

using Ustas.RimAI.Communication.Relations.Persistence;

namespace Ustas.RimAI.Communication.Relations.Prompting
{
    /// <summary>
    /// Dependencies: prompt section aggregate builder, prompt render pipeline, and runtime prompt variable context.
    /// Responsibility: render canonical PromptSectionCatalog aggregates for diplomacy and RPG main-chain prompts.
    /// </summary>
    internal sealed class PromptWorkspaceComposerSectionAggregates : PromptWorkspaceComposerCollaborator
    {
        internal PromptWorkspaceComposerSectionAggregates(PromptWorkspaceComposer owner) : base(owner)
        {
        }


        internal PromptHierarchyNode BuildMainChainPromptSectionNode(
            RimTalkPromptChannel rootChannel,
            SystemPromptConfig config,
            DialogueScenarioContext context,
            EnvironmentPromptConfig environmentConfig)
        {
            string promptChannel = PromptSectionSchemaCatalog.ResolveRuntimePromptChannel(
                rootChannel,
                context?.IsProactive == true);
            return Owner.BuildPromptSectionAggregateNode(config, promptChannel, context, environmentConfig);
        }

        internal PromptHierarchyNode BuildPromptSectionAggregateNode(
            SystemPromptConfig config,
            string promptChannel,
            DialogueScenarioContext context,
            EnvironmentPromptConfig environmentConfig)
        {
            RimTalkPromptChannel rootChannel = context?.IsRpg == true
                ? RimTalkPromptChannel.Rpg
                : RimTalkPromptChannel.Diplomacy;
            PromptSectionAggregate aggregate = Owner.BuildPromptSectionAggregateForCompose(
                rootChannel,
                promptChannel,
                deterministicPreview: false,
                context,
                environmentConfig,
                additionalValues: null);

            var root = new PromptHierarchyNode("main_prompt_sections");
            for (int i = 0; i < aggregate.Sections.Count; i++)
            {
                PromptSectionAggregateSection section = aggregate.Sections[i];
                if (section == null || string.IsNullOrWhiteSpace(section.Content))
                {
                    continue;
                }

                root.AddChild(section.SectionId, section.Content.Trim());
            }

            return root.Children.Count > 0 ? root : null;
        }

        internal string BuildPromptSectionAggregatePreview(RimTalkPromptChannel rootChannel, string promptChannel)
        {
            PromptSectionAggregate aggregate = Owner.BuildPromptSectionAggregateForCompose(
                rootChannel,
                promptChannel,
                deterministicPreview: true,
                scenarioContext: null,
                environmentConfig: null,
                additionalValues: null);
            return aggregate?.RenderedText?.Trim() ?? string.Empty;
        }

        internal PromptWorkspaceStructuredPreview BuildPromptWorkspaceStructuredSectionPreview(
            RimTalkPromptChannel rootChannel,
            string promptChannel)
        {
            PromptWorkspaceComposeResult composed = Owner.ComposePromptWorkspace(
                rootChannel,
                promptChannel,
                includeNodes: false,
                deterministicPreview: true,
                scenarioContext: null,
                environmentConfig: null,
                additionalValues: null);
            return composed.Preview;
        }

        internal PromptWorkspaceStructuredPreview BuildPromptWorkspaceStructuredLayoutPreview(
            RimTalkPromptChannel rootChannel,
            string promptChannel,
            out List<ResolvedPromptNodePlacement> placements)
        {
            PromptWorkspaceComposeResult composed = Owner.ComposePromptWorkspace(
                rootChannel,
                promptChannel,
                includeNodes: true,
                deterministicPreview: true,
                scenarioContext: null,
                environmentConfig: null,
                additionalValues: null);
            placements = composed.Placements;
            return composed.Preview;
        }

        internal string BuildPromptWorkspaceLayoutPreview(
            RimTalkPromptChannel rootChannel,
            string promptChannel,
            out List<ResolvedPromptNodePlacement> placements)
        {
            PromptWorkspaceStructuredPreview preview = Owner.BuildPromptWorkspaceStructuredLayoutPreview(
                rootChannel,
                promptChannel,
                out placements);
            return Owner.RenderStructuredPreviewAsText(preview);
        }

        internal string BuildPromptWorkspaceContextBlock(string normalizedChannel)
        {
            return Owner.BuildPromptWorkspaceContextBlock(normalizedChannel, "manual", "{{ runtime.environment }}");
        }

        internal string BuildPromptWorkspaceContextBlock(
            string normalizedChannel,
            string mode,
            string environment)
        {
            return "<prompt_context>\n"
                + "  <channel>" + normalizedChannel + "</channel>\n"
                + "  <mode>" + (mode ?? "manual") + "</mode>\n"
                + "  <environment>" + (environment ?? "{{ runtime.environment }}") + "</environment>";
        }

        internal PromptSectionAggregate BuildPromptSectionAggregateForPreview(
            RimTalkPromptChannel rootChannel,
            string promptChannel)
        {
            string normalizedChannel = RimTalkPromptEntryChannelCatalog.NormalizeForRoot(promptChannel, rootChannel);
            RimTalkPromptEntryDefaultsConfig catalog = RelationsMod.Settings?.GetPromptSectionCatalogClone()
                                                   ?? RimTalkPromptEntryDefaultsProvider.GetDefaultsSnapshot();
            List<PromptSectionLayoutConfig> sectionLayouts =
                RelationsMod.Settings?.GetPromptSectionLayouts(normalizedChannel);
            return PromptSectionAggregateBuilder.Build(
                catalog,
                normalizedChannel,
                (_, template) => template,
                sectionLayouts);
        }

        internal PromptWorkspacePreviewBlock BuildSectionAggregateBlock(
            string promptChannel,
            string content,
            PromptSectionAggregate aggregate)
        {
            var block = new PromptWorkspacePreviewBlock
            {
                Kind = PromptWorkspacePreviewBlockKind.SectionAggregate,
                PromptChannel = promptChannel,
                Content = content ?? string.Empty
            };
            block.Subsections.AddRange(Owner.BuildSectionAggregateSubsections(aggregate));
            return block;
        }

        internal IEnumerable<PromptWorkspacePreviewSubsection> BuildSectionAggregateSubsections(
            PromptSectionAggregate aggregate)
        {
            foreach (PromptSectionAggregateSection section in aggregate?.Sections ?? Enumerable.Empty<PromptSectionAggregateSection>())
            {
                if (section == null || string.IsNullOrWhiteSpace(section.Content))
                {
                    continue;
                }

                yield return new PromptWorkspacePreviewSubsection
                {
                    SectionId = section.SectionId ?? string.Empty,
                    Content = section.Content.Trim()
                };
            }
        }

        internal void AddPromptWorkspaceNodeBlocks(
            ICollection<PromptWorkspacePreviewBlock> blocks,
            IEnumerable<ResolvedPromptNodePlacement> placements,
            PromptUnifiedNodeSlot slot)
        {
            foreach (ResolvedPromptNodePlacement placement in placements ?? Enumerable.Empty<ResolvedPromptNodePlacement>())
            {
                if (placement == null || placement.Slot != slot || !placement.Enabled)
                {
                    continue;
                }

                string nodeContent = placement.Content?.Trim() ?? string.Empty;
                if (nodeContent.Length == 0)
                {
                    continue;
                }

                string nodeId = placement.NodeId ?? "node";
                string wrappedContent = Owner.WrapNodeContentWithXml(nodeId, nodeContent);
                blocks.Add(new PromptWorkspacePreviewBlock
                {
                    Kind = PromptWorkspacePreviewBlockKind.Node,
                    PromptChannel = placement.PromptChannel,
                    NodeId = placement.NodeId,
                    Slot = placement.Slot,
                    Order = placement.Order,
                    Content = wrappedContent
                });
            }
        }

        internal string WrapNodeContentWithXml(string nodeId, string content)
        {
            if (string.IsNullOrWhiteSpace(nodeId) || string.IsNullOrWhiteSpace(content))
            {
                return content;
            }

            string normalizedTag = Owner.NormalizeNodeIdToXmlTag(nodeId);
            return $"<{normalizedTag}>\n{Owner.IndentMultilineContent(content, 2)}\n</{normalizedTag}>";
        }

        internal string NormalizeNodeIdToXmlTag(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return "node";
            }

            var sb = new System.Text.StringBuilder(nodeId.Length);
            foreach (char c in nodeId)
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
                {
                    sb.Append(char.ToLowerInvariant(c));
                }
                else if (c == '.' || c == ':')
                {
                    sb.Append('_');
                }
            }

            string result = sb.ToString().Trim('_');
            return string.IsNullOrEmpty(result) ? "node" : result;
        }

        internal string IndentMultilineContent(string content, int spaces)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            string indent = new string(' ', spaces);
            return string.Join("\n", content.Split('\n').Select(line => indent + line.TrimEnd()));
        }

        internal List<PromptWorkspacePreviewBlock> ReorderWorkspacePreviewBlocks(
            IEnumerable<PromptWorkspacePreviewBlock> blocks)
        {
            var contexts = new List<PromptWorkspacePreviewBlock>();
            var others = new List<PromptWorkspacePreviewBlock>();
            var bodies = new List<PromptWorkspacePreviewBlock>();
            var footers = new List<PromptWorkspacePreviewBlock>();

            foreach (PromptWorkspacePreviewBlock block in blocks ?? Enumerable.Empty<PromptWorkspacePreviewBlock>())
            {
                if (block == null)
                {
                    continue;
                }

                if (block.Kind == PromptWorkspacePreviewBlockKind.Context)
                {
                    contexts.Add(block);
                    continue;
                }

                if (block.Kind == PromptWorkspacePreviewBlockKind.Footer)
                {
                    footers.Add(block);
                    continue;
                }

                if (block.Kind == PromptWorkspacePreviewBlockKind.SectionAggregate)
                {
                    bodies.Add(block);
                    continue;
                }

                others.Add(block);
            }

            var ordered = new List<PromptWorkspacePreviewBlock>(
                contexts.Count + others.Count + bodies.Count + footers.Count);
            ordered.AddRange(contexts);
            ordered.AddRange(others);
            ordered.AddRange(bodies);
            ordered.AddRange(footers);
            return ordered;
        }

        internal string BuildPreviewSignature(
            string normalizedChannel,
            IEnumerable<PromptWorkspacePreviewBlock> blocks)
        {
            var sb = new StringBuilder();
            sb.Append("channel=").Append(normalizedChannel ?? string.Empty).Append('|');
            foreach (PromptWorkspacePreviewBlock block in blocks ?? Enumerable.Empty<PromptWorkspacePreviewBlock>())
            {
                if (block == null)
                {
                    continue;
                }

                sb.Append((int)block.Kind).Append(':')
                  .Append(block.NodeId ?? string.Empty).Append(':')
                  .Append(block.Slot.ToSerializedValue()).Append(':')
                  .Append(block.Order).Append(':')
                  .Append(Owner.BuildTextSignature(block.Content));

                foreach (PromptWorkspacePreviewSubsection subsection in block.Subsections ?? Enumerable.Empty<PromptWorkspacePreviewSubsection>())
                {
                    if (subsection == null)
                    {
                        continue;
                    }

                    sb.Append(":sub(")
                      .Append(subsection.SectionId ?? string.Empty)
                      .Append(',')
                      .Append(Owner.BuildTextSignature(subsection.Content))
                      .Append(')');
                }

                sb
                  .Append('|');
            }

            return sb.ToString();
        }

        internal string BuildTextSignature(string text)
        {
            string normalized = text ?? string.Empty;
            return normalized.Length + ":" + Owner.ComputeStableHash(normalized).ToString("X8");
        }

        internal int ComputeStableHash(string text)
        {
            unchecked
            {
                const int fnvOffset = unchecked((int)2166136261);
                const int fnvPrime = 16777619;
                int hash = fnvOffset;
                string source = text ?? string.Empty;
                for (int i = 0; i < source.Length; i++)
                {
                    hash ^= source[i];
                    hash *= fnvPrime;
                }

                return hash;
            }
        }

        internal string RenderStructuredPreviewAsText(PromptWorkspaceStructuredPreview preview)
        {
            if (preview?.Blocks == null || preview.Blocks.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            foreach (PromptWorkspacePreviewBlock block in preview.Blocks)
            {
                if (block == null || string.IsNullOrWhiteSpace(block.Content))
                {
                    continue;
                }

                if (sb.Length > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine();
                }

                sb.Append(block.Content.Trim());
            }

            return sb.ToString();
        }

        internal string RenderPromptSectionAggregateSection(
            string promptChannel,
            string sectionId,
            string templateText,
            DialogueScenarioContext context,
            EnvironmentPromptConfig environmentConfig)
        {
            string normalized = templateText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            string renderChannel = context?.IsRpg == true ? "rpg" : "diplomacy";
            string templateId = $"prompt_sections.{promptChannel}.{sectionId}";
            Dictionary<string, object> values = host.TemplateVariables.BuildTemplateVariableValues(
                templateId,
                renderChannel,
                context,
                environmentConfig);
            PromptRenderContext renderContext = PromptRenderContext.Create(templateId, renderChannel);
            renderContext.SetValues(values);
            return PromptTemplateRenderer.RenderOrThrow(templateId, renderChannel, normalized, renderContext).Trim();
        }

        internal RimTalkPromptEntryDefaultsConfig GetRuntimePromptSectionCatalog(SystemPromptConfig config)
        {
            return RelationsMod.Settings?.GetPromptSectionCatalogClone()
                ?? RimTalkPromptEntryDefaultsProvider.GetDefaultsSnapshot();
        }


        internal bool SyncLegacyPromptMirrorsFromSections(SystemPromptConfig config)
        {
            if (config == null)
            {
                return false;
            }

            string systemMirror = Owner.BuildLegacyPromptMirrorText(
                RimTalkPromptEntryChannelCatalog.DiplomacyDialogue,
                "system_rules",
                "action_rules",
                "output_specification");
            string dialogueMirror = Owner.BuildLegacyPromptMirrorText(
                RimTalkPromptEntryChannelCatalog.DiplomacyDialogue,
                "character_persona",
                "memory_system",
                "environment_perception",
                "context",
                "repetition_reinforcement");

            bool changed = false;
            if (!string.Equals(config.GlobalSystemPrompt ?? string.Empty, systemMirror, StringComparison.Ordinal))
            {
                config.GlobalSystemPrompt = systemMirror;
                changed = true;
            }

            if (!string.Equals(config.GlobalDialoguePrompt ?? string.Empty, dialogueMirror, StringComparison.Ordinal))
            {
                config.GlobalDialoguePrompt = dialogueMirror;
                changed = true;
            }

            config.UseHierarchicalPromptFormat = true;
            return changed;
        }

        internal string BuildLegacyPromptMirrorText(string promptChannel, params string[] sectionIds)
        {
            RimTalkPromptEntryDefaultsConfig catalog = RelationsMod.Settings?.GetPromptSectionCatalogClone()
                                                   ?? RimTalkPromptEntryDefaultsProvider.GetDefaultsSnapshot();
            var parts = new List<string>();
            for (int i = 0; i < sectionIds.Length; i++)
            {
                string text = catalog.ResolveContent(promptChannel, sectionIds[i])?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    parts.Add(text);
                }
            }

            return string.Join("\n\n", parts).Trim();
        }
        }

}
