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
    internal sealed class PromptWorkspaceSlice3 : PromptWorkspaceComposerCollaborator
    {
        internal PromptWorkspaceSlice3(PromptWorkspaceComposer owner) : base(owner)
        {
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
            if (!Owner.IsRuntimeMainChainChannel(normalized))
            {
                return false;
            }

            DialogueScenarioContext previewContext = scenarioContext
                ?? Owner.CreateDeterministicPreviewScenarioContext(rootChannel, normalized);
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
            bool proactive = Owner.IsProactivePromptChannel(promptChannel);
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

            string renderChannel = Owner.ResolveTemplateRenderChannel(promptChannel, rootChannel, scenarioContext);
            Dictionary<string, object> values;
            if (cachedComposeValues != null)
            {
                values = new Dictionary<string, object>(cachedComposeValues);
                Owner.InjectRuntimeNodeBodies(values, templateId, promptChannel, scenarioContext);
                values["ctx.channel"] = promptChannel ?? string.Empty;
                values["ctx.mode"] = Owner.ResolvePromptModeForCompose(scenarioContext, promptChannel);
                Owner.MergeAdditionalValues(values, additionalValues);
                PromptRequestSnapshotCache.RecordSnapshot(promptChannel, values, Owner.BuildScenarioSignature(scenarioContext));
            }
            else
            {
                using (PerfScope.Measure("RpgPush.QueueProcess.BuildComposeValues"))
                    values = deterministicPreview
                        ? Owner.BuildDeterministicComposeValues(promptChannel, scenarioContext, additionalValues)
                        : BuildRuntimeComposeValues(templateId, renderChannel, promptChannel, scenarioContext, environmentConfig, additionalValues);
            }

            PromptRenderContext renderContext = PromptRenderContext.Create(templateId, renderChannel);
            renderContext.SetValues(values);
            string rendered;
            using (PerfScope.Measure("RpgPush.QueueProcess.RenderTemplate"))
                rendered = PromptTemplateRenderer.RenderOrThrow(templateId, renderChannel, template, renderContext).Trim();
            return rendered;
        }

internal Dictionary<string, object> BuildCachedComposeValues(
            string promptChannel,
            RimTalkPromptChannel rootChannel,
            DialogueScenarioContext scenarioContext,
            EnvironmentPromptConfig environmentConfig)
        {
            // Use a dummy templateId; BuildTemplateVariableValues uses it only for provider availability checks.
            string dummyId = $"prompt_sections.{promptChannel}.__cached_base__";
            string renderChannel = Owner.ResolveTemplateRenderChannel(promptChannel, rootChannel, scenarioContext);
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

            string renderChannel = Owner.ResolveTemplateRenderChannel(promptChannel, rootChannel, scenarioContext);
            Dictionary<string, object> values;
            if (cachedComposeValues != null)
            {
                values = new Dictionary<string, object>(cachedComposeValues);
                Owner.InjectRuntimeNodeBodies(values, templateId, promptChannel, scenarioContext);
                values["ctx.channel"] = promptChannel ?? string.Empty;
                values["ctx.mode"] = Owner.ResolvePromptModeForCompose(scenarioContext, promptChannel);
                Owner.MergeAdditionalValues(values, additionalValues);
                PromptRequestSnapshotCache.RecordSnapshot(promptChannel, values, Owner.BuildScenarioSignature(scenarioContext));
            }
            else
            {
                values = deterministicPreview
                    ? Owner.BuildDeterministicComposeValues(promptChannel, scenarioContext, additionalValues)
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
            Owner.InjectRuntimeNodeBodies(values, templateId, promptChannel, scenarioContext);
            values["ctx.channel"] = promptChannel ?? string.Empty;
            values["ctx.mode"] = Owner.ResolvePromptModeForCompose(scenarioContext, promptChannel);
            Owner.MergeAdditionalValues(values, additionalValues);
            PromptRequestSnapshotCache.RecordSnapshot(promptChannel, values, Owner.BuildScenarioSignature(scenarioContext));
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
    }
}
