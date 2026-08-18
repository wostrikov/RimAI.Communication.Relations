using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.Diagnostics;
using RimWorld;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Context;

using Ustas.RimAI.Communication.Relations.Persistence;

namespace Ustas.RimAI.Communication.Relations.Prompting
{
    internal sealed class PromptWorkspaceSlice2 : PromptWorkspaceComposerCollaborator
    {
        internal PromptWorkspaceSlice2(PromptWorkspaceComposer owner) : base(owner)
        {
        }

internal void AddRuntimeDiplomacySupplementBlocks(
            ICollection<PromptWorkspacePreviewBlock> blocks,
            string promptChannel,
            DialogueScenarioContext scenarioContext,
            IReadOnlyDictionary<string, object> additionalValues)
        {
            if (blocks == null || scenarioContext == null)
            {
                return;
            }

            string normalized = RimTalkPromptEntryChannelCatalog.NormalizeLoose(promptChannel);
            if (normalized != RimTalkPromptEntryChannelCatalog.DiplomacyDialogue &&
                normalized != RimTalkPromptEntryChannelCatalog.ProactiveDiplomacyDialogue)
            {
                return;
            }

            SystemPromptConfig config = host.DomainStore.LoadConfigReadOnly() ?? host.DomainStore.CreateDefaultConfig();
            Pawn playerNegotiator = Owner.TryResolvePlayerNegotiator(additionalValues);
            string factionCharacteristics = host.NodeSupport.ResolveFactionPromptText(
                scenarioContext.Faction,
                config,
                scenarioContext)?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(factionCharacteristics))
            {
                var instructionNode = new PromptHierarchyNode("instruction_stack");
                instructionNode.AddChild("faction_characteristics", factionCharacteristics);
                blocks.Add(new PromptWorkspacePreviewBlock
                {
                    Kind = PromptWorkspacePreviewBlockKind.Node,
                    PromptChannel = normalized,
                    NodeId = "instruction_stack",
                    Slot = PromptUnifiedNodeSlot.MainChainBefore,
                    Order = -100,
                    Content = PromptHierarchyRenderer.Render(instructionNode)
                });
            }

            PromptHierarchyNode dynamicDataNode = host.NodeSupport.BuildDiplomacyDynamicDataNode(
                config,
                scenarioContext.Faction,
                playerNegotiator);
            if (dynamicDataNode == null)
            {
                return;
            }

            blocks.Add(new PromptWorkspacePreviewBlock
            {
                Kind = PromptWorkspacePreviewBlockKind.Node,
                PromptChannel = normalized,
                NodeId = "dynamic_data",
                Slot = PromptUnifiedNodeSlot.MainChainAfter,
                Order = -90,
                Content = PromptHierarchyRenderer.Render(dynamicDataNode)
            });
        }

internal Pawn TryResolvePlayerNegotiator(IReadOnlyDictionary<string, object> additionalValues)
        {
            if (additionalValues == null)
            {
                return null;
            }

            return additionalValues.TryGetValue("pawn.player_negotiator", out object value)
                ? value as Pawn
                : null;
        }

internal void AddRuntimeRpgMemorySupplementBlocks(
            ICollection<PromptWorkspacePreviewBlock> blocks,
            string promptChannel,
            DialogueScenarioContext scenarioContext)
        {
            if (blocks == null || scenarioContext == null)
            {
                return;
            }

            string normalized = RimTalkPromptEntryChannelCatalog.NormalizeLoose(promptChannel);
            if (normalized != RimTalkPromptEntryChannelCatalog.RpgDialogue &&
                normalized != RimTalkPromptEntryChannelCatalog.ProactiveRpgDialogue)
            {
                return;
            }

            Pawn target = scenarioContext.Target;
            if (target == null)
            {
                return;
            }

            SystemPromptConfig config = host.DomainStore.LoadConfigReadOnly() ?? host.DomainStore.CreateDefaultConfig();
            PromptPolicyConfig promptPolicy = host.NodeSupport.ResolvePromptPolicyConfig(config);
            string factionMemory = DialogueSummaryService
                .BuildRpgDynamicFactionMemoryBlock(target.Faction, target)
                ?.Trim() ?? string.Empty;
            string personalMemory = RpgNpcDialogueArchiveManager.Instance
                .BuildPromptMemoryBlock(
                    target,
                    scenarioContext.Initiator,
                    promptPolicy?.SummaryTimelineTurnLimit ?? 8,
                    promptPolicy?.SummaryCharBudget ?? 1200)
                ?.Trim() ?? string.Empty;

            Owner.TryAddSingleTextNodeBlock(
                blocks,
                normalized,
                "dynamic_faction_memory",
                factionMemory,
                PromptUnifiedNodeSlot.DynamicDataAfter,
                -100);
            Owner.TryAddSingleTextNodeBlock(
                blocks,
                normalized,
                "dynamic_npc_personal_memory",
                personalMemory,
                PromptUnifiedNodeSlot.DynamicDataAfter,
                -95);
        }

internal void TryAddSingleTextNodeBlock(
            ICollection<PromptWorkspacePreviewBlock> blocks,
            string promptChannel,
            string nodeId,
            string content,
            PromptUnifiedNodeSlot slot,
            int order)
        {
            if (blocks == null || string.IsNullOrWhiteSpace(nodeId) || string.IsNullOrWhiteSpace(content))
            {
                return;
            }

            var container = new PromptHierarchyNode("runtime_supplement");
            host.NodeSupport.AddTextNodeIfNotEmpty(container, nodeId, content);
            if (container.Children.Count == 0)
            {
                return;
            }

            blocks.Add(new PromptWorkspacePreviewBlock
            {
                Kind = PromptWorkspacePreviewBlockKind.Node,
                PromptChannel = promptChannel,
                NodeId = nodeId,
                Slot = slot,
                Order = order,
                Content = PromptHierarchyRenderer.Render(container.Children[0])
            });
        }

internal PromptSectionAggregate BuildPromptSectionAggregateForCompose(
            RimTalkPromptChannel rootChannel,
            string promptChannel,
            bool deterministicPreview,
            DialogueScenarioContext scenarioContext,
            EnvironmentPromptConfig environmentConfig,
            IReadOnlyDictionary<string, object> additionalValues,
            Dictionary<string, object> cachedComposeValues = null)
        {
            string normalizedChannel = PromptSectionSchemaCatalog.NormalizeWorkspaceChannel(promptChannel, rootChannel);
            var aggregate = new PromptSectionAggregate
            {
                PromptChannel = normalizedChannel
            };

            if (cachedComposeValues == null && !deterministicPreview)
            {
                using (PerfScope.Measure("RpgPush.QueueProcess.BuildComposeValuesBase"))
                    cachedComposeValues = Owner.BuildCachedComposeValues(normalizedChannel, rootChannel, scenarioContext, environmentConfig);
            }

            foreach (PromptSectionSchemaItem section in Owner.GetOrderedSectionsForCompose(normalizedChannel))
            {
                string template = RelationsMod.Settings?.ResolvePromptSectionText(normalizedChannel, section.Id) ?? string.Empty;
                bool rawModVariablesSection = Owner.IsRpgModVariablesRawOutputSection(
                    rootChannel,
                    normalizedChannel,
                    section.Id);
                string rendered = rawModVariablesSection
                    ? Owner.RenderRawModVariablesSection(
                        template,
                        rootChannel,
                        normalizedChannel,
                        deterministicPreview,
                        scenarioContext,
                        environmentConfig,
                        additionalValues)
                    : Owner.RenderUnifiedTemplate(
                        $"prompt_sections.{normalizedChannel}.{section.Id}",
                        normalizedChannel,
                        template,
                        rootChannel,
                        deterministicPreview,
                        scenarioContext,
                        environmentConfig,
                        additionalValues,
                        cachedComposeValues: cachedComposeValues);
                if (string.IsNullOrWhiteSpace(rendered))
                {
                    continue;
                }

                aggregate.Sections.Add(new PromptSectionAggregateSection
                {
                    SectionId = section.Id,
                    SectionLabel = section.EnglishName,
                    Content = rendered.Trim()
                });
            }

            aggregate.RenderedText = PromptHierarchyRenderer.Render(
                Owner.BuildMainPromptSectionNodeForAggregate(aggregate.Sections));
            return aggregate;
        }

internal IReadOnlyList<PromptSectionSchemaItem> GetOrderedSectionsForCompose(string promptChannel)
        {
            List<PromptSectionLayoutConfig> sectionLayouts =
                RelationsMod.Settings?.GetPromptSectionLayouts(promptChannel) ?? new List<PromptSectionLayoutConfig>();
            return PromptSectionSchemaCatalog.GetOrderedMainChainSections(sectionLayouts, enabledOnly: true);
        }

internal bool IsRpgModVariablesRawOutputSection(
            RimTalkPromptChannel rootChannel,
            string promptChannel,
            string sectionId)
        {
            return string.Equals(
                PromptSectionSchemaCatalog.NormalizeSectionId(sectionId),
                "mod_variables",
                StringComparison.Ordinal);
        }

internal string RenderRawModVariablesSection(
            string template,
            RimTalkPromptChannel rootChannel,
            string promptChannel,
            bool deterministicPreview,
            DialogueScenarioContext scenarioContext,
            EnvironmentPromptConfig environmentConfig,
            IReadOnlyDictionary<string, object> additionalValues,
            Dictionary<string, object> cachedComposeValues = null)
        {
            string source = template ?? string.Empty;
            if (source.IndexOf("{{", StringComparison.Ordinal) < 0)
            {
                return source.Trim();
            }

            string templateId = "prompt_sections." + (promptChannel ?? string.Empty) + ".mod_variables_raw";
            return Owner.RenderUnifiedTemplateLenient(
                templateId,
                promptChannel,
                source,
                rootChannel,
                deterministicPreview,
                scenarioContext,
                environmentConfig,
                additionalValues,
                cachedComposeValues);
        }

internal string ConvertRawModVariableValueToText(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (value is string text)
            {
                return text;
            }

            if (value is IEnumerable<string> lines)
            {
                return string.Join(", ", lines.Where(item => !string.IsNullOrWhiteSpace(item)));
            }

            if (value is IFormattable formattable)
            {
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            }

            return value.ToString() ?? string.Empty;
        }

internal List<ResolvedPromptNodePlacement> BuildPromptNodePlacementsForCompose(
            RimTalkPromptChannel rootChannel,
            string promptChannel,
            bool deterministicPreview,
            DialogueScenarioContext scenarioContext,
            EnvironmentPromptConfig environmentConfig,
            IReadOnlyDictionary<string, object> additionalValues,
            Dictionary<string, object> cachedComposeValues = null)
        {
            string normalizedChannel = PromptSectionSchemaCatalog.NormalizeWorkspaceChannel(promptChannel, rootChannel);
            if (Owner.TryBuildRuntimeAlignedPreviewNodePlacements(
                    rootChannel,
                    normalizedChannel,
                    deterministicPreview,
                    scenarioContext,
                    out List<ResolvedPromptNodePlacement> runtimePlacements))
            {
                return runtimePlacements;
            }

            List<PromptUnifiedNodeLayoutConfig> layouts =
                RelationsMod.Settings?.GetPromptNodeLayouts(normalizedChannel) ??
                PromptUnifiedNodeSchemaCatalog.GetAllowedNodes(normalizedChannel)
                    .Select(node => PromptUnifiedNodeLayoutDefaults.BuildDefaultLayout(normalizedChannel, node.Id))
                    .ToList();
            Owner.EnsureLayoutsContainAllowedNodes(normalizedChannel, layouts);
            bool suppressFallbackRoleNode = !deterministicPreview &&
                Owner.ShouldSuppressDiplomacyFallbackRoleNode(normalizedChannel, scenarioContext);

            string expandMemoryPlayerMessage = ExpandMemoryMatchContext.PlayerMessage;
            var placements = new List<ResolvedPromptNodePlacement>();
            foreach (PromptUnifiedNodeLayoutConfig layout in layouts
                         .Where(item => item != null)
                         .OrderBy(item => item.GetSlot())
                         .ThenBy(item => item.Order)
                         .ThenBy(item => item.NodeId, StringComparer.OrdinalIgnoreCase))
            {
                string nodeId = layout.NodeId ?? string.Empty;
                string template = RelationsMod.Settings?.ResolvePromptNodeText(normalizedChannel, nodeId) ?? string.Empty;
                string rendered;
                if (string.Equals(nodeId, "common_knowledge", StringComparison.OrdinalIgnoreCase))
                {
                    rendered = host.ContextAssembler.BuildCommonKnowledgeBlock(expandMemoryPlayerMessage);
                }
                else if (string.Equals(nodeId, "expandmemory_npc_memory", StringComparison.OrdinalIgnoreCase))
                {
                    rendered = host.ContextAssembler.BuildExpandMemoryPawnBlock(scenarioContext?.Target);
                }
                else
                {
                    rendered = Owner.RenderUnifiedTemplate(
                        $"prompt_nodes.{normalizedChannel}.{nodeId}",
                        normalizedChannel,
                        template,
                        rootChannel,
                        deterministicPreview,
                        scenarioContext,
                        environmentConfig,
                        additionalValues,
                        cachedComposeValues: cachedComposeValues);
                }
                if (suppressFallbackRoleNode &&
                    string.Equals(
                        PromptUnifiedNodeSchemaCatalog.NormalizeId(nodeId),
                        "diplomacy_fallback_role",
                        StringComparison.OrdinalIgnoreCase))
                {
                    rendered = string.Empty;
                }
                placements.Add(new ResolvedPromptNodePlacement
                {
                    PromptChannel = normalizedChannel,
                    NodeId = nodeId,
                    OutputTag = nodeId,
                    Slot = layout.GetSlot(),
                    Order = layout.Order,
                    Enabled = layout.Enabled,
                    Applied = layout.Enabled,
                    Content = rendered
                });
            }

            return placements;
        }

internal bool ShouldSuppressDiplomacyFallbackRoleNode(
            string normalizedChannel,
            DialogueScenarioContext scenarioContext)
        {
            if (scenarioContext == null)
            {
                return false;
            }

            if (normalizedChannel != RimTalkPromptEntryChannelCatalog.DiplomacyDialogue &&
                normalizedChannel != RimTalkPromptEntryChannelCatalog.ProactiveDiplomacyDialogue)
            {
                return false;
            }

            SystemPromptConfig config = host.DomainStore.LoadConfigReadOnly() ?? host.DomainStore.CreateDefaultConfig();
            string factionCharacteristics = host.NodeSupport.ResolveFactionPromptText(
                scenarioContext.Faction,
                config,
                scenarioContext)?.Trim() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(factionCharacteristics);
        }
    }
}
