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
    internal sealed class PromptWorkspaceSlice1 : PromptWorkspaceComposerCollaborator
    {
        internal PromptWorkspaceSlice1(PromptWorkspaceComposer owner) : base(owner)
        {
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
                    composed = Owner.ComposePromptWorkspace(
                    rootChannel,
                    promptChannel,
                    includeNodes: !Owner.IsSectionOnlyChannel(promptChannel),
                    deterministicPreview,
                    scenarioContext,
                    environmentConfig,
                    additionalValues);
                if (!deterministicPreview)
                {
                    Owner.ValidateRuntimePromptComposition(composed);
                }

                string prompt = Owner.RenderStructuredPreviewAsText(composed.Preview);
                if (!string.IsNullOrWhiteSpace(payloadTag) && !string.IsNullOrWhiteSpace(payloadText))
                {
                    prompt = Owner.InjectPromptPayloadBlock(prompt, payloadTag, payloadText);
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

                return Owner.ApplyRuntimePromptPostProcessing(
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

            string withStyle = Owner.InjectDialogueStyleDirective(prompt, rootChannel, promptChannel);
            return Owner.DeduplicatePromptAuthorityLines(withStyle);
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

                if (Owner.IsDuplicateAuthorityLine(trimmed))
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
            return trimmedLine.IndexOf("Єдиний авторитет правил виводу", StringComparison.OrdinalIgnoreCase) >= 0 ||
                trimmedLine.IndexOf("response_contract", StringComparison.OrdinalIgnoreCase) >= 0 && trimmedLine.IndexOf("єдиний", StringComparison.OrdinalIgnoreCase) >= 0 ||
                trimmedLine.IndexOf("Мінімум дій", StringComparison.OrdinalIgnoreCase) >= 0 ||
                trimmedLine.IndexOf("Авторитет правил виводу", StringComparison.OrdinalIgnoreCase) >= 0;
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
            bool effectiveIncludeNodes = includeNodes && !Owner.IsSectionOnlyChannel(normalizedChannel);

            // Build compose values once for reuse across all sections and nodes
            Dictionary<string, object> sharedComposeValues = null;
            if (!deterministicPreview)
            {
                using (PerfScope.Measure("RpgPush.QueueProcess.BuildComposeValuesBase"))
                    sharedComposeValues = Owner.BuildCachedComposeValues(normalizedChannel, rootChannel, scenarioContext, environmentConfig);
            }

            PromptSectionAggregate aggregate = Owner.BuildPromptSectionAggregateForCompose(
                rootChannel,
                normalizedChannel,
                deterministicPreview,
                scenarioContext,
                environmentConfig,
                additionalValues,
                sharedComposeValues);
            List<ResolvedPromptNodePlacement> placements = effectiveIncludeNodes
                ? Owner.BuildPromptNodePlacementsForCompose(
                    rootChannel,
                    normalizedChannel,
                    deterministicPreview,
                    scenarioContext,
                    environmentConfig,
                    additionalValues,
                    sharedComposeValues)
                : new List<ResolvedPromptNodePlacement>();

            string mode = Owner.ResolvePromptModeForCompose(scenarioContext, normalizedChannel);
            string contextEnvironment = Owner.ResolveWorkspaceContextEnvironmentText(rootChannel, normalizedChannel, scenarioContext);
            var preview = new PromptWorkspaceStructuredPreview();
            preview.Blocks.Add(new PromptWorkspacePreviewBlock
            {
                Kind = PromptWorkspacePreviewBlockKind.Context,
                PromptChannel = normalizedChannel,
                Content = Owner.BuildPromptWorkspaceContextBlock(normalizedChannel, mode, contextEnvironment)
            });
            if (effectiveIncludeNodes)
            {
                Owner.AddPromptWorkspaceNodeBlocks(preview.Blocks, placements, PromptUnifiedNodeSlot.MetadataAfter);
                Owner.AddPromptWorkspaceNodeBlocks(preview.Blocks, placements, PromptUnifiedNodeSlot.MainChainBefore);
                Owner.AddPromptWorkspaceNodeBlocks(preview.Blocks, placements, PromptUnifiedNodeSlot.MainChainAfter);
                Owner.AddPromptWorkspaceNodeBlocks(preview.Blocks, placements, PromptUnifiedNodeSlot.DynamicDataAfter);
                Owner.AddPromptWorkspaceNodeBlocks(preview.Blocks, placements, PromptUnifiedNodeSlot.ContractBeforeEnd);
            }

            if (!deterministicPreview)
            {
                Owner.AddRuntimeMandatoryRaceProfileBlock(
                    preview.Blocks,
                    normalizedChannel,
                    scenarioContext);
                Owner.AddRuntimeDiplomacySupplementBlocks(
                    preview.Blocks,
                    normalizedChannel,
                    scenarioContext,
                    additionalValues);
                Owner.AddRuntimeRpgMemorySupplementBlocks(
                    preview.Blocks,
                    normalizedChannel,
                    scenarioContext);
            }

            string sectionPreview = aggregate?.RenderedText?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(sectionPreview))
            {
                preview.Blocks.Add(Owner.BuildSectionAggregateBlock(normalizedChannel, sectionPreview, aggregate));
            }

            preview.Blocks.Add(new PromptWorkspacePreviewBlock
            {
                Kind = PromptWorkspacePreviewBlockKind.Footer,
                PromptChannel = normalizedChannel,
                Content = "</prompt_context>"
            });
            preview.Blocks = Owner.ReorderWorkspacePreviewBlocks(preview.Blocks);
            preview.Signature = Owner.BuildPreviewSignature(normalizedChannel, preview.Blocks);
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
            if (!Owner.RequiresMandatoryRaceProfileBlock(normalized))
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
    }
}
