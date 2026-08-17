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
    /// <summary>
    /// Dependencies: unified prompt catalog, prompt template renderer, and workspace preview models.
    /// Responsibility: provide one shared composer for workbench preview and side-channel runtime prompts.
    /// </summary>
internal sealed partial class PromptWorkspaceComposer
    {
        private readonly PromptPersistenceService host;

        internal PromptWorkspaceComposer(PromptPersistenceService host)
        {
            this.host = host ?? throw new System.ArgumentNullException(nameof(host));
        }

        internal string BuildUnifiedChannelSystemPrompt(
            RimTalkPromptChannel rootChannel,
            string promptChannel,
            DialogueScenarioContext scenarioContext,
            EnvironmentPromptConfig environmentConfig,
            IReadOnlyDictionary<string, object> additionalValues = null,
            string payloadTag = "",
            string payloadText = "",
            bool deterministicPreview = false,
            bool allowMemoryCompressionScheduling = true,
            bool allowMemoryColdLoad = true,
            DiplomacyPromptRuntimeSnapshot runtimeSnapshot = null)
        {
            string currentTurnUserIntent = RpgPromptTurnContextScope.Current?.CurrentTurnUserIntent ?? string.Empty;
            bool resolvedAllowMemoryCompressionScheduling =
                RpgPromptTurnContextScope.Current?.AllowMemoryCompressionScheduling ?? allowMemoryCompressionScheduling;
            bool resolvedAllowMemoryColdLoad =
                RpgPromptTurnContextScope.Current?.AllowMemoryColdLoad ?? allowMemoryColdLoad;
            IDisposable turnScope = null;
            IDisposable runtimeScope = null;
            if (rootChannel == RimTalkPromptChannel.Rpg && !deterministicPreview)
            {
                turnScope = RpgPromptTurnContextScope.Push(
                    currentTurnUserIntent,
                    resolvedAllowMemoryCompressionScheduling,
                    resolvedAllowMemoryColdLoad);
            }
            if (runtimeSnapshot != null)
            {
                runtimeScope = host.SnapshotService.PushRuntimeSnapshotScope(runtimeSnapshot);
            }

            try
            {
                PromptWorkspaceComposeResult composed;
                using (PerfScope.Measure("RpgPush.QueueProcess.ComposeWorkspace"))
                    composed = ComposePromptWorkspace(
                    rootChannel,
                    promptChannel,
                    includeNodes: !IsSectionOnlyChannel(promptChannel),
                    deterministicPreview,
                    scenarioContext,
                    environmentConfig,
                    additionalValues);
                if (!deterministicPreview)
                {
                    ValidateRuntimePromptComposition(composed);
                }

                string prompt = RenderStructuredPreviewAsText(composed.Preview);
                if (!string.IsNullOrWhiteSpace(payloadTag) && !string.IsNullOrWhiteSpace(payloadText))
                {
                    prompt = InjectPromptPayloadBlock(prompt, payloadTag, payloadText);
                }

                if (rootChannel == RimTalkPromptChannel.Rpg && !deterministicPreview)
                {
                    // Only inject via post-processing if the expandmemory_npc_memory node is NOT already enabled.
                    // When the node is enabled, BuildPromptNodePlacementsForCompose already rendered ExpandMemory content.
                    bool nodeEnabled = RelationsMod.Settings?.ResolvePromptNodeLayout(
                        composed?.PromptChannel ?? promptChannel,
                        "expandmemory_npc_memory")?.Enabled ?? false;
                    if (!nodeEnabled)
                    {
                        prompt = host.ContextAssembler.InjectExpandMemoryIntoPrompt(prompt, scenarioContext?.Target);
                    }
                }

                return ApplyRuntimePromptPostProcessing(
                    prompt,
                    rootChannel,
                    composed?.PromptChannel ?? promptChannel,
                    deterministicPreview);
            }
            finally
            {
                runtimeScope?.Dispose();
                turnScope?.Dispose();
            }
        }

        internal string ApplyRuntimePromptPostProcessing(
            string prompt,
            RimTalkPromptChannel rootChannel,
            string promptChannel,
            bool deterministicPreview)
        {
            if (deterministicPreview || string.IsNullOrWhiteSpace(prompt))
            {
                return prompt ?? string.Empty;
            }

            string withStyle = InjectDialogueStyleDirective(prompt, rootChannel, promptChannel);
            return DeduplicatePromptAuthorityLines(withStyle);
        }

        internal string InjectDialogueStyleDirective(
            string prompt,
            RimTalkPromptChannel rootChannel,
            string promptChannel)
        {
            RelationsSettings settings = RelationsMod.Settings ?? RelationsMod.Instance?.InstanceSettings;
            DialogueStyleMode styleMode = settings?.DialogueStyleMode ?? DialogueStyleMode.NaturalConcise;
            string styleLine = styleMode switch
            {
                DialogueStyleMode.Immersive =>
                    "STYLE PRIORITY: Keep immersive in-character tone; avoid policy narration and system wording.",
                DialogueStyleMode.Balanced =>
                    "STYLE PRIORITY: Keep in-character tone with concise human phrasing; avoid mechanical/system wording.",
                _ =>
                    "STYLE PRIORITY: Keep natural human in-character dialogue; prefer 1-2 concise sentences and avoid mechanical/system wording."
            };

            string channel = RimTalkPromptEntryChannelCatalog.NormalizeLoose(promptChannel);
            bool dialogueChannel =
                rootChannel == RimTalkPromptChannel.Rpg ||
                channel == RimTalkPromptEntryChannelCatalog.DiplomacyDialogue ||
                channel == RimTalkPromptEntryChannelCatalog.ProactiveDiplomacyDialogue ||
                channel == RimTalkPromptEntryChannelCatalog.ProactiveRpgDialogue ||
                channel == RimTalkPromptEntryChannelCatalog.RpgDialogue;
            if (!dialogueChannel)
            {
                return prompt;
            }

            string marker = "\n</prompt_context>";
            int markerIndex = prompt.LastIndexOf(marker, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                return prompt.TrimEnd() + "\n" + styleLine;
            }

            return prompt.Insert(markerIndex, "\n" + styleLine);
        }

        internal string DeduplicatePromptAuthorityLines(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return string.Empty;
            }

            string[] lines = prompt.Replace("\r\n", "\n").Split('\n');
            var output = new List<string>(lines.Length);
            var seenAuthority = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in lines)
            {
                string line = raw ?? string.Empty;
                string trimmed = line.Trim();
                if (trimmed.Length == 0)
                {
                    output.Add(line);
                    continue;
                }

                if (IsDuplicateAuthorityLine(trimmed))
                {
                    if (!seenAuthority.Add(trimmed))
                    {
                        continue;
                    }
                }

                output.Add(line);
            }

            return string.Join("\n", output).TrimEnd();
        }

        internal bool IsDuplicateAuthorityLine(string trimmedLine)
        {
            return trimmedLine.IndexOf("输出规范唯一权威", StringComparison.OrdinalIgnoreCase) >= 0 ||
                trimmedLine.IndexOf("response_contract", StringComparison.OrdinalIgnoreCase) >= 0 && trimmedLine.IndexOf("唯一", StringComparison.OrdinalIgnoreCase) >= 0 ||
                trimmedLine.IndexOf("动作使用最小化", StringComparison.OrdinalIgnoreCase) >= 0 ||
                trimmedLine.IndexOf("输出规范权威区", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal bool IsSocialCirclePostChannel(string promptChannel)
        {
            string normalized = RimTalkPromptEntryChannelCatalog.NormalizeLoose(promptChannel);
            return string.Equals(
                normalized,
                RimTalkPromptEntryChannelCatalog.SocialCirclePost,
                StringComparison.Ordinal);
        }

        internal PromptWorkspaceComposeResult ComposePromptWorkspace(
            RimTalkPromptChannel rootChannel,
            string promptChannel,
            bool includeNodes,
            bool deterministicPreview,
            DialogueScenarioContext scenarioContext,
            EnvironmentPromptConfig environmentConfig,
            IReadOnlyDictionary<string, object> additionalValues)
        {
            string normalizedChannel = PromptSectionSchemaCatalog.NormalizeWorkspaceChannel(promptChannel, rootChannel);
            bool effectiveIncludeNodes = includeNodes && !IsSectionOnlyChannel(normalizedChannel);

            // Build compose values once for reuse across all sections and nodes
            Dictionary<string, object> sharedComposeValues = null;
            if (!deterministicPreview)
            {
                using (PerfScope.Measure("RpgPush.QueueProcess.BuildComposeValuesBase"))
                    sharedComposeValues = BuildCachedComposeValues(normalizedChannel, rootChannel, scenarioContext, environmentConfig);
            }

            PromptSectionAggregate aggregate = BuildPromptSectionAggregateForCompose(
                rootChannel,
                normalizedChannel,
                deterministicPreview,
                scenarioContext,
                environmentConfig,
                additionalValues,
                sharedComposeValues);
            List<ResolvedPromptNodePlacement> placements = effectiveIncludeNodes
                ? BuildPromptNodePlacementsForCompose(
                    rootChannel,
                    normalizedChannel,
                    deterministicPreview,
                    scenarioContext,
                    environmentConfig,
                    additionalValues,
                    sharedComposeValues)
                : new List<ResolvedPromptNodePlacement>();

            string mode = ResolvePromptModeForCompose(scenarioContext, normalizedChannel);
            string contextEnvironment = ResolveWorkspaceContextEnvironmentText(rootChannel, normalizedChannel, scenarioContext);
            var preview = new PromptWorkspaceStructuredPreview();
            preview.Blocks.Add(new PromptWorkspacePreviewBlock
            {
                Kind = PromptWorkspacePreviewBlockKind.Context,
                PromptChannel = normalizedChannel,
                Content = BuildPromptWorkspaceContextBlock(normalizedChannel, mode, contextEnvironment)
            });
            if (effectiveIncludeNodes)
            {
                AddPromptWorkspaceNodeBlocks(preview.Blocks, placements, PromptUnifiedNodeSlot.MetadataAfter);
                AddPromptWorkspaceNodeBlocks(preview.Blocks, placements, PromptUnifiedNodeSlot.MainChainBefore);
                AddPromptWorkspaceNodeBlocks(preview.Blocks, placements, PromptUnifiedNodeSlot.MainChainAfter);
                AddPromptWorkspaceNodeBlocks(preview.Blocks, placements, PromptUnifiedNodeSlot.DynamicDataAfter);
                AddPromptWorkspaceNodeBlocks(preview.Blocks, placements, PromptUnifiedNodeSlot.ContractBeforeEnd);
            }

            if (!deterministicPreview)
            {
                AddRuntimeMandatoryRaceProfileBlock(
                    preview.Blocks,
                    normalizedChannel,
                    scenarioContext);
                AddRuntimeDiplomacySupplementBlocks(
                    preview.Blocks,
                    normalizedChannel,
                    scenarioContext,
                    additionalValues);
                AddRuntimeRpgMemorySupplementBlocks(
                    preview.Blocks,
                    normalizedChannel,
                    scenarioContext);
            }

            string sectionPreview = aggregate?.RenderedText?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(sectionPreview))
            {
                preview.Blocks.Add(BuildSectionAggregateBlock(normalizedChannel, sectionPreview, aggregate));
            }

            preview.Blocks.Add(new PromptWorkspacePreviewBlock
            {
                Kind = PromptWorkspacePreviewBlockKind.Footer,
                PromptChannel = normalizedChannel,
                Content = "</prompt_context>"
            });
            preview.Blocks = ReorderWorkspacePreviewBlocks(preview.Blocks);
            preview.Signature = BuildPreviewSignature(normalizedChannel, preview.Blocks);
            return new PromptWorkspaceComposeResult
            {
                PromptChannel = normalizedChannel,
                Aggregate = aggregate,
                Placements = placements,
                Preview = preview
            };
        }

        internal void AddRuntimeMandatoryRaceProfileBlock(
            ICollection<PromptWorkspacePreviewBlock> blocks,
            string promptChannel,
            DialogueScenarioContext scenarioContext)
        {
            if (blocks == null || scenarioContext == null)
            {
                return;
            }

            string normalized = RimTalkPromptEntryChannelCatalog.NormalizeLoose(promptChannel);
            if (!RequiresMandatoryRaceProfileBlock(normalized))
            {
                return;
            }

            SystemPromptConfig config = host.DomainStore.LoadConfigReadOnly() ?? host.DomainStore.CreateDefaultConfig();
            string raceProfile = host.NodeSupport.BuildMandatoryRaceProfileBlock(config, scenarioContext)?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(raceProfile))
            {
                throw new PromptRenderException(
                    "prompt_blocks.mandatory_race_profile",
                    normalized,
                    new PromptRenderDiagnostic
                    {
                        ErrorCode = PromptRenderErrorCode.TemplateMissing,
                        Message = "Mandatory race profile block is empty for runtime prompt composition."
                    });
            }

            blocks.Add(new PromptWorkspacePreviewBlock
            {
                Kind = PromptWorkspacePreviewBlockKind.Node,
                PromptChannel = normalized,
                NodeId = "mandatory_race_profile",
                Slot = PromptUnifiedNodeSlot.MetadataAfter,
                Order = -95,
                Content = raceProfile
            });
        }

        internal string ResolveWorkspaceContextEnvironmentText(
            RimTalkPromptChannel rootChannel,
            string normalizedChannel,
            DialogueScenarioContext scenarioContext)
        {
            string channel = RimTalkPromptEntryChannelCatalog.NormalizeLoose(normalizedChannel);
            if (rootChannel == RimTalkPromptChannel.Rpg)
            {
                if (channel == RimTalkPromptEntryChannelCatalog.RpgArchiveCompression ||
                    channel == RimTalkPromptEntryChannelCatalog.SummaryGeneration)
                {
                    return "No environment context.";
                }
            }
            else
            {
                if (channel == RimTalkPromptEntryChannelCatalog.DiplomacyDialogue ||
                    channel == RimTalkPromptEntryChannelCatalog.ProactiveDiplomacyDialogue)
                {
                    SystemPromptConfig cfg = host.DomainStore.LoadConfigReadOnly() ?? host.DomainStore.CreateDefaultConfig();
                    string envText = host.BuildEnvironmentPromptBlocks(cfg, scenarioContext)?.Trim() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(envText))
                    {
                        return envText;
                    }

                    return "No environment context.";
                }

                return string.Empty;
            }

            bool isRpgRuntimeChannel =
                channel == RimTalkPromptEntryChannelCatalog.RpgDialogue ||
                channel == RimTalkPromptEntryChannelCatalog.ProactiveRpgDialogue ||
                channel == RimTalkPromptEntryChannelCatalog.PersonaBootstrap;
            if (!isRpgRuntimeChannel)
            {
                return string.Empty;
            }

            SystemPromptConfig cfg2 = host.DomainStore.LoadConfigReadOnly() ?? host.DomainStore.CreateDefaultConfig();
            string envResult = host.BuildEnvironmentPromptBlocks(cfg2, scenarioContext)?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(envResult))
            {
                return envResult;
            }

            string fallback = host.TemplateVariables.ResolveTemplateVariableValue("world.environment_params", scenarioContext, cfg2.EnvironmentPrompt)?.ToString();
            return string.IsNullOrWhiteSpace(fallback)
                ? "No environment context."
                : fallback.Trim();
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
            Pawn playerNegotiator = TryResolvePlayerNegotiator(additionalValues);
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

            TryAddSingleTextNodeBlock(
                blocks,
                normalized,
                "dynamic_faction_memory",
                factionMemory,
                PromptUnifiedNodeSlot.DynamicDataAfter,
                -100);
            TryAddSingleTextNodeBlock(
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
                    cachedComposeValues = BuildCachedComposeValues(normalizedChannel, rootChannel, scenarioContext, environmentConfig);
            }

            foreach (PromptSectionSchemaItem section in GetOrderedSectionsForCompose(normalizedChannel))
            {
                string template = RelationsMod.Settings?.ResolvePromptSectionText(normalizedChannel, section.Id) ?? string.Empty;
                bool rawModVariablesSection = IsRpgModVariablesRawOutputSection(
                    rootChannel,
                    normalizedChannel,
                    section.Id);
                string rendered = rawModVariablesSection
                    ? RenderRawModVariablesSection(
                        template,
                        rootChannel,
                        normalizedChannel,
                        deterministicPreview,
                        scenarioContext,
                        environmentConfig,
                        additionalValues)
                    : RenderUnifiedTemplate(
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
                BuildMainPromptSectionNodeForAggregate(aggregate.Sections));
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
            return RenderUnifiedTemplateLenient(
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
            if (TryBuildRuntimeAlignedPreviewNodePlacements(
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
            EnsureLayoutsContainAllowedNodes(normalizedChannel, layouts);
            bool suppressFallbackRoleNode = !deterministicPreview &&
                ShouldSuppressDiplomacyFallbackRoleNode(normalizedChannel, scenarioContext);

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
                    rendered = RenderUnifiedTemplate(
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

        internal void EnsureLayoutsContainAllowedNodes(
            string promptChannel,
            ICollection<PromptUnifiedNodeLayoutConfig> layouts)
        {
            if (layouts == null)
            {
                return;
            }

            var existing = new HashSet<string>(
                layouts
                    .Where(item => item != null && !string.IsNullOrWhiteSpace(item.NodeId))
                    .Select(item => PromptUnifiedNodeSchemaCatalog.NormalizeId(item.NodeId)),
                StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<PromptUnifiedNodeSchemaItem> allowedNodes = PromptUnifiedNodeSchemaCatalog.GetAllowedNodes(promptChannel);
            for (int i = 0; i < allowedNodes.Count; i++)
            {
                string nodeId = allowedNodes[i].Id;
                if (existing.Contains(nodeId))
                {
                    continue;
                }

                layouts.Add(PromptUnifiedNodeLayoutDefaults.BuildDefaultLayout(promptChannel, nodeId));
                existing.Add(nodeId);
            }
        }

        internal bool TryBuildRuntimeAlignedPreviewNodePlacements(
            RimTalkPromptChannel rootChannel,
            string promptChannel,
            bool deterministicPreview,
            DialogueScenarioContext scenarioContext,
            out List<ResolvedPromptNodePlacement> placements)
        {
            placements = null;
            if (!deterministicPreview)
            {
                return false;
            }

            string normalized = RimTalkPromptEntryChannelCatalog.NormalizeLoose(promptChannel);
            if (!IsRuntimeMainChainChannel(normalized))
            {
                return false;
            }

            DialogueScenarioContext previewContext = scenarioContext
                ?? CreateDeterministicPreviewScenarioContext(rootChannel, normalized);
            SystemPromptConfig config = host.DomainStore.LoadConfigReadOnly() ?? host.DomainStore.CreateDefaultConfig();
            if (normalized == RimTalkPromptEntryChannelCatalog.DiplomacyStrategy)
            {
                placements = host.NodeSupport.ResolveStrategyNodePlacements(
                    normalized,
                    config,
                    previewContext,
                    new DiplomacyStrategyPromptContext
                    {
                        NegotiatorContextText = "preview_negotiator_context",
                        StrategyFactPackText = "preview_fact_pack",
                        ScenarioDossierText = "preview_scenario_dossier"
                    });
                return true;
            }

            if (normalized == RimTalkPromptEntryChannelCatalog.DiplomacyDialogue ||
                normalized == RimTalkPromptEntryChannelCatalog.ProactiveDiplomacyDialogue)
            {
                placements = host.NodeSupport.ResolveDiplomacyNodePlacements(
                    normalized,
                    config,
                    previewContext,
                    previewContext?.Faction,
                    null);
                return true;
            }

            if (normalized == RimTalkPromptEntryChannelCatalog.RpgDialogue ||
                normalized == RimTalkPromptEntryChannelCatalog.ProactiveRpgDialogue)
            {
                placements = host.NodeSupport.ResolveRpgNodePlacements(
                    normalized,
                    RelationsMod.Settings,
                    config,
                    previewContext,
                    null,
                    null,
                    string.Empty,
                    host.NodeSupport.IsOpeningTurnContext(previewContext));
                return true;
            }

            return false;
        }

        internal bool IsRuntimeMainChainChannel(string promptChannel)
        {
            string normalized = RimTalkPromptEntryChannelCatalog.NormalizeLoose(promptChannel);
            return normalized == RimTalkPromptEntryChannelCatalog.DiplomacyDialogue
                || normalized == RimTalkPromptEntryChannelCatalog.ProactiveDiplomacyDialogue
                || normalized == RimTalkPromptEntryChannelCatalog.DiplomacyStrategy
                || normalized == RimTalkPromptEntryChannelCatalog.RpgDialogue
                || normalized == RimTalkPromptEntryChannelCatalog.ProactiveRpgDialogue;
        }

        internal DialogueScenarioContext CreateDeterministicPreviewScenarioContext(
            RimTalkPromptChannel rootChannel,
            string promptChannel)
        {
            bool proactive = IsProactivePromptChannel(promptChannel);
            if (rootChannel == RimTalkPromptChannel.Rpg ||
                promptChannel == RimTalkPromptEntryChannelCatalog.RpgDialogue ||
                promptChannel == RimTalkPromptEntryChannelCatalog.ProactiveRpgDialogue)
            {
                return DialogueScenarioContext.CreateRpg(
                    null,
                    null,
                    proactive,
                    new[] { "channel:" + promptChannel, "mode:preview" });
            }

            return DialogueScenarioContext.CreateDiplomacy(
                null,
                proactive,
                new[] { "channel:" + promptChannel, "mode:preview" });
        }

        internal bool IsProactivePromptChannel(string promptChannel)
        {
            string normalized = RimTalkPromptEntryChannelCatalog.NormalizeLoose(promptChannel);
            return normalized.IndexOf("proactive", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal bool IsSectionOnlyChannel(string promptChannel)
        {
            string normalized = RimTalkPromptEntryChannelCatalog.NormalizeLoose(promptChannel);
            return normalized == RimTalkPromptEntryChannelCatalog.PersonaBootstrap
                || normalized == RimTalkPromptEntryChannelCatalog.SummaryGeneration
                || normalized == RimTalkPromptEntryChannelCatalog.RpgArchiveCompression
                || normalized == RimTalkPromptEntryChannelCatalog.ImageGeneration;
        }

        internal string RenderUnifiedTemplate(
            string templateId,
            string promptChannel,
            string templateText,
            RimTalkPromptChannel rootChannel,
            bool deterministicPreview,
            DialogueScenarioContext scenarioContext,
            EnvironmentPromptConfig environmentConfig,
            IReadOnlyDictionary<string, object> additionalValues,
            Dictionary<string, object> cachedComposeValues = null)
        {
            string template = templateText?.Trim() ?? string.Empty;
            if (template.Length == 0)
            {
                return string.Empty;
            }

            string renderChannel = ResolveTemplateRenderChannel(promptChannel, rootChannel, scenarioContext);
            Dictionary<string, object> values;
            if (cachedComposeValues != null)
            {
                values = new Dictionary<string, object>(cachedComposeValues);
                InjectRuntimeNodeBodies(values, templateId, promptChannel, scenarioContext);
                values["ctx.channel"] = promptChannel ?? string.Empty;
                values["ctx.mode"] = ResolvePromptModeForCompose(scenarioContext, promptChannel);
                MergeAdditionalValues(values, additionalValues);
                PromptRequestSnapshotCache.RecordSnapshot(promptChannel, values, BuildScenarioSignature(scenarioContext));
            }
            else
            {
                using (PerfScope.Measure("RpgPush.QueueProcess.BuildComposeValues"))
                    values = deterministicPreview
                        ? BuildDeterministicComposeValues(promptChannel, scenarioContext, additionalValues)
                        : BuildRuntimeComposeValues(templateId, renderChannel, promptChannel, scenarioContext, environmentConfig, additionalValues);
            }

            PromptRenderContext renderContext = PromptRenderContext.Create(templateId, renderChannel);
            renderContext.SetValues(values);
            string rendered;
            using (PerfScope.Measure("RpgPush.QueueProcess.RenderTemplate"))
                rendered = PromptTemplateRenderer.RenderOrThrow(templateId, renderChannel, template, renderContext).Trim();
            return rendered;
        }

        // Build the base compose values (without template-specific injection) for reuse across sections.
        // Excludes: InjectRuntimeNodeBodies, ctx.channel, ctx.mode, MergeAdditionalValues, RecordSnapshot
        internal Dictionary<string, object> BuildCachedComposeValues(
            string promptChannel,
            RimTalkPromptChannel rootChannel,
            DialogueScenarioContext scenarioContext,
            EnvironmentPromptConfig environmentConfig)
        {
            // Use a dummy templateId; BuildTemplateVariableValues uses it only for provider availability checks.
            string dummyId = $"prompt_sections.{promptChannel}.__cached_base__";
            string renderChannel = ResolveTemplateRenderChannel(promptChannel, rootChannel, scenarioContext);
            return host.TemplateVariables.BuildTemplateVariableValues(dummyId, renderChannel, scenarioContext, environmentConfig);
        }

        internal string RenderUnifiedTemplateLenient(
            string templateId,
            string promptChannel,
            string templateText,
            RimTalkPromptChannel rootChannel,
            bool deterministicPreview,
            DialogueScenarioContext scenarioContext,
            EnvironmentPromptConfig environmentConfig,
            IReadOnlyDictionary<string, object> additionalValues,
            Dictionary<string, object> cachedComposeValues = null)
        {
            string template = templateText?.Trim() ?? string.Empty;
            if (template.Length == 0)
            {
                return string.Empty;
            }

            string renderChannel = ResolveTemplateRenderChannel(promptChannel, rootChannel, scenarioContext);
            Dictionary<string, object> values;
            if (cachedComposeValues != null)
            {
                values = new Dictionary<string, object>(cachedComposeValues);
                InjectRuntimeNodeBodies(values, templateId, promptChannel, scenarioContext);
                values["ctx.channel"] = promptChannel ?? string.Empty;
                values["ctx.mode"] = ResolvePromptModeForCompose(scenarioContext, promptChannel);
                MergeAdditionalValues(values, additionalValues);
                PromptRequestSnapshotCache.RecordSnapshot(promptChannel, values, BuildScenarioSignature(scenarioContext));
            }
            else
            {
                values = deterministicPreview
                    ? BuildDeterministicComposeValues(promptChannel, scenarioContext, additionalValues)
                    : BuildRuntimeComposeValues(templateId, renderChannel, promptChannel, scenarioContext, environmentConfig, additionalValues);
            }

            PromptRenderContext renderContext = PromptRenderContext.Create(templateId, renderChannel);
            renderContext.SetValues(values);
            return PromptTemplateRenderer.RenderLenient(templateId, renderChannel, template, renderContext).Trim();
        }

        internal Dictionary<string, object> BuildRuntimeComposeValues(
            string templateId,
            string renderChannel,
            string promptChannel,
            DialogueScenarioContext scenarioContext,
            EnvironmentPromptConfig environmentConfig,
            IReadOnlyDictionary<string, object> additionalValues)
        {
            Dictionary<string, object> values = host.TemplateVariables.BuildTemplateVariableValues(
                templateId,
                renderChannel,
                scenarioContext,
                environmentConfig);
            InjectRuntimeNodeBodies(values, templateId, promptChannel, scenarioContext);
            values["ctx.channel"] = promptChannel ?? string.Empty;
            values["ctx.mode"] = ResolvePromptModeForCompose(scenarioContext, promptChannel);
            MergeAdditionalValues(values, additionalValues);
            PromptRequestSnapshotCache.RecordSnapshot(promptChannel, values, BuildScenarioSignature(scenarioContext));
            return values;
        }

        internal string BuildScenarioSignature(DialogueScenarioContext context)
        {
            if (context == null)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            if (context.Initiator != null)
            {
                parts.Add("initiator:" + context.Initiator.LabelShortCap);
            }

            if (context.Target != null)
            {
                parts.Add("target:" + context.Target.LabelShortCap);
            }

            if (context.Faction != null)
            {
                parts.Add("faction:" + context.Faction.Name);
            }

            if (context.IsProactive)
            {
                parts.Add("mode:proactive");
            }

            if (context.IsRpg)
            {
                parts.Add("type:rpg");
            }

            return string.Join("|", parts);
        }

        internal void InjectRuntimeNodeBodies(
            IDictionary<string, object> values,
            string templateId,
            string promptChannel,
            DialogueScenarioContext scenarioContext)
        {
            if (values == null)
            {
                return;
            }

            string normalized = RimTalkPromptEntryChannelCatalog.NormalizeLoose(promptChannel);
            bool isDiplomacyChannel =
                normalized == RimTalkPromptEntryChannelCatalog.DiplomacyDialogue ||
                normalized == RimTalkPromptEntryChannelCatalog.ProactiveDiplomacyDialogue;
            bool isRpgChannel =
                normalized == RimTalkPromptEntryChannelCatalog.RpgDialogue ||
                normalized == RimTalkPromptEntryChannelCatalog.ProactiveRpgDialogue;
            if (!isDiplomacyChannel && !isRpgChannel)
            {
                return;
            }

            var faction = scenarioContext?.Faction;
            string normalizedTemplateId = (templateId ?? string.Empty).Trim();
            if (isDiplomacyChannel && normalizedTemplateId.EndsWith(".api_limits_node_template", StringComparison.OrdinalIgnoreCase))
            {
                values["dialogue.api_limits_body"] = host.NodeSupport.BuildTextBlock(sb => host.DiplomacyBuilder.AppendApiLimits(sb, faction));
                return;
            }

            if (isDiplomacyChannel && normalizedTemplateId.EndsWith(".quest_guidance_node_template", StringComparison.OrdinalIgnoreCase))
            {
                Dictionary<string, object> questContext = host.DiplomacyBuilder.BuildQuestPromptContext(scenarioContext);
                values["dialogue.quest_guidance_body"] = host.NodeSupport.BuildTextBlock(sb =>
                {
                    host.DiplomacyBuilder.AppendDynamicQuestGuidance(sb, faction, questContext);
                    host.DiplomacyBuilder.AppendQuestSelectionHardRules(sb);
                });
                return;
            }

            if (!normalizedTemplateId.EndsWith(".response_contract_node_template", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (isRpgChannel)
            {
                SystemPromptConfig rpgConfig = host.DomainStore.CachedConfig ?? host.DomainStore.LoadConfigReadOnly() ?? host.DomainStore.CreateDefaultConfig();
                Pawn initiator = scenarioContext?.Initiator;
                Pawn target = scenarioContext?.Target;
                bool samePlayerFaction =
                    initiator?.Faction != null &&
                    initiator.Faction == target?.Faction &&
                    initiator.Faction.IsPlayer;
                bool preferCompactApiContract = scenarioContext?.IsProactive != true && samePlayerFaction;
                values["dialogue.response_contract_body"] = host.NodeSupport.BuildRpgApiContractText(
                    RelationsMod.Settings,
                    rpgConfig,
                    scenarioContext,
                    preferCompactApiContract);
                return;
            }

            SystemPromptConfig config = host.DomainStore.CachedConfig ?? host.DomainStore.LoadConfigReadOnly() ?? host.DomainStore.CreateDefaultConfig();
            values["dialogue.response_contract_body"] = host.NodeSupport.BuildTextBlock(sb =>
            {
                if (config.UseAdvancedMode)
                {
                    host.DiplomacyBuilder.AppendAdvancedConfig(sb, config, faction);
                }
                else
                {
                    host.DiplomacyBuilder.AppendSimpleConfig(sb, config, faction);
                }
            });
        }

        internal void ValidateRuntimePromptComposition(PromptWorkspaceComposeResult composed)
        {
            if (composed == null)
            {
                throw new PromptRenderException(
                    "prompt_runtime.compose",
                    "unknown",
                    new PromptRenderDiagnostic
                    {
                        ErrorCode = PromptRenderErrorCode.TemplateMissing,
                        Message = "Runtime prompt composition result is null."
                    });
            }

            string channel = RimTalkPromptEntryChannelCatalog.NormalizeLoose(composed.PromptChannel);
            foreach (string nodeId in GetRequiredRuntimeNodeIds(channel))
            {
                string content = FindEnabledNodeContent(composed.Placements, nodeId);
                if (string.IsNullOrWhiteSpace(content))
                {
                    throw new PromptRenderException(
                        "prompt_nodes." + nodeId,
                        channel,
                        new PromptRenderDiagnostic
                        {
                            ErrorCode = PromptRenderErrorCode.TemplateMissing,
                            Message = "Runtime required node is empty or disabled: " + nodeId
                        });
                }
            }

            if (RequiresMandatoryRaceProfileBlock(channel))
            {
                string raceProfile = FindPreviewBlockContent(composed.Preview?.Blocks, "mandatory_race_profile");
                if (string.IsNullOrWhiteSpace(raceProfile))
                {
                    throw new PromptRenderException(
                        "prompt_blocks.mandatory_race_profile",
                        channel,
                        new PromptRenderDiagnostic
                        {
                            ErrorCode = PromptRenderErrorCode.TemplateMissing,
                            Message = "Mandatory race profile block is missing in runtime prompt composition."
                        });
                }
            }
        }

        internal IReadOnlyList<string> GetRequiredRuntimeNodeIds(string promptChannel)
        {
            if (promptChannel == RimTalkPromptEntryChannelCatalog.DiplomacyDialogue ||
                promptChannel == RimTalkPromptEntryChannelCatalog.ProactiveDiplomacyDialogue)
            {
                return new[]
                {
                    "api_limits_node_template",
                    "quest_guidance_node_template",
                    "response_contract_node_template"
                };
            }

            if (promptChannel == RimTalkPromptEntryChannelCatalog.DiplomacyStrategy)
            {
                return new[]
                {
                    "strategy_output_contract",
                    "strategy_player_negotiator_context_template",
                    "strategy_fact_pack_template",
                    "strategy_scenario_dossier_template"
                };
            }

            if (promptChannel == RimTalkPromptEntryChannelCatalog.RpgDialogue ||
                promptChannel == RimTalkPromptEntryChannelCatalog.ProactiveRpgDialogue)
            {
                return new[]
                {
                    "fact_grounding",
                    "output_language",
                    "decision_policy",
                    "turn_objective",
                    "rpg_role_setting_fallback",
                    "response_contract_node_template"
                };
            }

            return Array.Empty<string>();
        }

        internal string FindEnabledNodeContent(
            IEnumerable<ResolvedPromptNodePlacement> placements,
            string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return string.Empty;
            }

            string targetId = PromptUnifiedNodeSchemaCatalog.NormalizeId(nodeId);
            foreach (ResolvedPromptNodePlacement placement in placements ?? Enumerable.Empty<ResolvedPromptNodePlacement>())
            {
                if (placement == null || !placement.Enabled)
                {
                    continue;
                }

                string candidate = PromptUnifiedNodeSchemaCatalog.NormalizeId(placement.NodeId);
                if (!string.Equals(candidate, targetId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return placement.Content?.Trim() ?? string.Empty;
            }

            return string.Empty;
        }

        internal string FindPreviewBlockContent(
            IEnumerable<PromptWorkspacePreviewBlock> blocks,
            string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return string.Empty;
            }

            string targetId = PromptUnifiedNodeSchemaCatalog.NormalizeId(nodeId);
            foreach (PromptWorkspacePreviewBlock block in blocks ?? Enumerable.Empty<PromptWorkspacePreviewBlock>())
            {
                if (block == null || block.Kind != PromptWorkspacePreviewBlockKind.Node)
                {
                    continue;
                }

                string candidate = PromptUnifiedNodeSchemaCatalog.NormalizeId(block.NodeId);
                if (!string.Equals(candidate, targetId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return block.Content?.Trim() ?? string.Empty;
            }

            return string.Empty;
        }

        internal bool RequiresMandatoryRaceProfileBlock(string promptChannel)
        {
            return promptChannel == RimTalkPromptEntryChannelCatalog.DiplomacyDialogue ||
                promptChannel == RimTalkPromptEntryChannelCatalog.ProactiveDiplomacyDialogue ||
                promptChannel == RimTalkPromptEntryChannelCatalog.RpgDialogue ||
                promptChannel == RimTalkPromptEntryChannelCatalog.ProactiveRpgDialogue;
        }

        internal Dictionary<string, object> BuildDeterministicComposeValues(
            string promptChannel,
            DialogueScenarioContext scenarioContext,
            IReadOnlyDictionary<string, object> additionalValues)
        {
            Dictionary<string, object> values = TryBuildFromSnapshot(promptChannel);
            if (values != null)
            {
                values["ctx.channel"] = promptChannel ?? string.Empty;
                values["ctx.mode"] = ResolvePromptModeForCompose(scenarioContext, promptChannel);
                MergeAdditionalValues(values, additionalValues);
                return values;
            }

            // Fall back to last-known snapshot values (expired but preserved) before using
            // hard-coded placeholders — this resolves many variables that would otherwise
            // render as raw {{ ... }} tokens in the preview.
            values = TryBuildFromLastKnown(promptChannel);
            if (values != null)
            {
                values["ctx.channel"] = promptChannel ?? string.Empty;
                values["ctx.mode"] = ResolvePromptModeForCompose(scenarioContext, promptChannel);
                MergeAdditionalValues(values, additionalValues);
                return values;
            }

            values = host.NodeSupport.CreatePromptVariableSeed();
            values["ctx.channel"] = promptChannel ?? string.Empty;
            values["ctx.mode"] = ResolvePromptModeForCompose(scenarioContext, promptChannel);

            // Use live game state for preview when available, fall back to placeholders
            Map currentMap = Find.CurrentMap;
            values["system.target_language"] = RelationsMod.Settings?.GetEffectivePromptLanguage() ?? "English";
            values["system.game_language"] = LanguageDatabase.activeLanguage?.FriendlyNameNative ?? "English";

            if (currentMap != null)
            {
                // Use live game state for environment variables
                values["world.weather"] = currentMap.weatherManager?.curWeather?.label ?? "Clear";
                values["world.temperature"] = Mathf.RoundToInt(currentMap.mapTemperature?.OutdoorTemp ?? 21f).ToString() + "°C";
                values["world.time.season"] = GenLocalDate.Season(currentMap).Label();
            }
            else
            {
                values["world.time.hour"] = "12";
                values["world.time.day"] = "1";
                values["world.time.quadrum"] = "Aprimay";
                values["world.time.year"] = "5500";
                values["world.time.season"] = "Spring";
                values["world.time.date"] = "5500-04-01";
                values["world.weather"] = "Clear";
                values["world.temperature"] = "21°C";
            }

            // Use scenario context for faction/pawn data when available
            values["world.faction.name"] = scenarioContext?.Faction?.Name ?? "PreviewFaction";
            values["world.faction.description"] = scenarioContext?.Faction != null
                ? FactionPromptManager.Instance.GetPrompt(scenarioContext.Faction)
                : "preview_faction_description";
            values["pawn.initiator.name"] = scenarioContext?.Initiator?.LabelShort ?? "PreviewInitiator";
            values["pawn.target.name"] = scenarioContext?.Target?.LabelShort ?? "PreviewTarget";
            values["world.faction"] = scenarioContext?.Faction != null
                ? PromptRenderProjection.Project(scenarioContext.Faction)
                : host.NodeSupport.CreatePreviewFactionPlaceholder("PreviewFaction");
            values["pawn.initiator"] = scenarioContext?.Initiator != null
                ? PromptRenderProjection.Project(scenarioContext.Initiator)
                : host.NodeSupport.CreatePreviewPawnPlaceholder("PreviewInitiator");
            values["pawn.target"] = scenarioContext?.Target != null
                ? PromptRenderProjection.Project(scenarioContext.Target)
                : host.NodeSupport.CreatePreviewPawnPlaceholder("PreviewTarget");
            values["world.scene_tags"] = "scene:preview";
            values["world.environment_params"] = "preview_environment";
            values["world.recent_world_events"] = "preview_events";
            values["dialogue.primary_objective"] = "preview_objective";
            values["dialogue.optional_followup"] = "preview_followup";
            values["dialogue.latest_unresolved_intent"] = string.Empty;
            values["dialogue.api_limits_body"] = "preview_api_limits";
            values["dialogue.quest_guidance_body"] = "preview_quest_guidance";
            values["dialogue.response_contract_body"] = "preview_response_contract";
            MergeAdditionalValues(values, additionalValues);
            return values;
        }

        internal Dictionary<string, object> TryBuildFromSnapshot(string promptChannel)
        {
            Dictionary<string, object> snapshot = PromptRequestSnapshotCache.CloneSnapshotValues(promptChannel);
            if (snapshot == null || snapshot.Count == 0)
            {
                return null;
            }

            return snapshot;
        }

        internal Dictionary<string, object> TryBuildFromLastKnown(string promptChannel)
        {
            Dictionary<string, object> lastKnown = PromptRequestSnapshotCache.CloneLastKnownValues(promptChannel);
            if (lastKnown == null || lastKnown.Count == 0)
            {
                return null;
            }

            return lastKnown;
        }

        internal void MergeAdditionalValues(
            IDictionary<string, object> target,
            IReadOnlyDictionary<string, object> additionalValues)
        {
            if (target == null || additionalValues == null)
            {
                return;
            }

            foreach (KeyValuePair<string, object> entry in additionalValues)
            {
                if (string.IsNullOrWhiteSpace(entry.Key))
                {
                    continue;
                }

                target[entry.Key] = entry.Value ?? string.Empty;
            }
        }

        internal PromptHierarchyNode BuildMainPromptSectionNodeForAggregate(
            IEnumerable<PromptSectionAggregateSection> sections)
        {
            var node = new PromptHierarchyNode("main_prompt_sections");
            foreach (PromptSectionAggregateSection section in sections ?? Enumerable.Empty<PromptSectionAggregateSection>())
            {
                if (section == null || string.IsNullOrWhiteSpace(section.Content))
                {
                    continue;
                }

                node.AddChild(section.SectionId, section.Content.Trim());
            }

            return node;
        }

        internal string ResolveTemplateRenderChannel(
            string promptChannel,
            RimTalkPromptChannel rootChannel,
            DialogueScenarioContext scenarioContext)
        {
            if (scenarioContext?.IsRpg == true)
            {
                return "rpg";
            }

            string normalized = RimTalkPromptEntryChannelCatalog.NormalizeLoose(promptChannel);
            if (normalized.IndexOf("rpg", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized == RimTalkPromptEntryChannelCatalog.PersonaBootstrap ||
                normalized == RimTalkPromptEntryChannelCatalog.RpgArchiveCompression)
            {
                return "rpg";
            }

            if (normalized == RimTalkPromptEntryChannelCatalog.ImageGeneration)
            {
                return "image";
            }

            if (normalized == RimTalkPromptEntryChannelCatalog.SocialCirclePost)
            {
                return "social";
            }

            if (normalized == RimTalkPromptEntryChannelCatalog.SummaryGeneration)
            {
                return rootChannel == RimTalkPromptChannel.Rpg ? "rpg" : "diplomacy";
            }

            return rootChannel == RimTalkPromptChannel.Rpg ? "rpg" : "diplomacy";
        }

        internal string ResolvePromptModeForCompose(DialogueScenarioContext scenarioContext, string promptChannel)
        {
            if (scenarioContext?.IsProactive == true)
            {
                return "proactive";
            }

            string normalized = RimTalkPromptEntryChannelCatalog.NormalizeLoose(promptChannel);
            return normalized.IndexOf("proactive", StringComparison.OrdinalIgnoreCase) >= 0
                ? "proactive"
                : "manual";
        }

        internal string InjectPromptPayloadBlock(string promptText, string payloadTag, string payloadText)
        {
            string tag = SanitizePayloadTag(payloadTag);
            string text = EscapePromptXml(payloadText);
            if (tag.Length == 0 || text.Length == 0)
            {
                return promptText ?? string.Empty;
            }

            string block = "  <" + tag + ">\n    "
                + text.Replace("\r", string.Empty).Replace("\n", "\n    ")
                + "\n  </" + tag + ">";
            string normalized = promptText ?? string.Empty;
            const string footer = "</prompt_context>";
            int index = normalized.LastIndexOf(footer, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return normalized + "\n" + block;
            }

            return normalized.Insert(index, block + "\n\n");
        }

        internal string SanitizePayloadTag(string payloadTag)
        {
            string raw = (payloadTag ?? string.Empty).Trim().ToLowerInvariant();
            if (raw.Length == 0)
            {
                return string.Empty;
            }

            var chars = raw.Where(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-');
            string result = new string(chars.ToArray());
            return result.Length == 0 ? string.Empty : result;
        }

        internal string EscapePromptXml(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }
    }

    internal sealed class PromptWorkspaceComposeResult
    {
        public string PromptChannel = string.Empty;
        public PromptSectionAggregate Aggregate;
        public List<ResolvedPromptNodePlacement> Placements = new List<ResolvedPromptNodePlacement>();
        public PromptWorkspaceStructuredPreview Preview = new PromptWorkspaceStructuredPreview();
    }
}
