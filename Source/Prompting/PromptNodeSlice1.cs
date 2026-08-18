using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Dialogue;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.Prompting;
using RimWorld;
using Verse;
using Ustas.RimAI.Communication.Relations.Context;
using Ustas.RimAI.Communication.Relations.Prompting.Diplomacy;

using Ustas.RimAI.Communication.Relations.Persistence;

namespace Ustas.RimAI.Communication.Relations.Prompting
{
    internal sealed class PromptNodeSlice1 : PromptNodeSupportCollaborator
    {
        internal PromptNodeSlice1(PromptNodeSupport owner) : base(owner)
        {
        }

internal string BuildFullSystemPromptHierarchicalCore(
            Faction faction,
            SystemPromptConfig config,
            bool isProactive,
            IEnumerable<string> additionalSceneTags)
        {
            return Owner.BuildFullSystemPromptHierarchicalCore(
                faction,
                config,
                isProactive,
                additionalSceneTags,
                null);
        }

internal string BuildFullSystemPromptHierarchicalCore(
            Faction faction,
            SystemPromptConfig config,
            bool isProactive,
            IEnumerable<string> additionalSceneTags,
            Pawn playerNegotiator)
        {
            return Owner.BuildFullSystemPromptHierarchical(faction, config, isProactive, additionalSceneTags, playerNegotiator);
        }

internal string BuildRpgSystemPromptHierarchicalCore(
            Pawn initiator,
            Pawn target,
            bool isProactive,
            IEnumerable<string> additionalSceneTags)
        {
            return Owner.BuildRpgSystemPromptHierarchical(initiator, target, isProactive, additionalSceneTags);
        }

internal string BuildDiplomacyStrategySystemPromptCore(
            Faction faction,
            SystemPromptConfig config,
            IEnumerable<string> additionalSceneTags,
            DiplomacyStrategyPromptContext strategyContext)
        {
            return Owner.BuildDiplomacyStrategySystemPromptHierarchical(
                faction,
                config,
                additionalSceneTags,
                strategyContext);
        }

internal string BuildFullSystemPromptHierarchical(
            Faction faction,
            SystemPromptConfig config,
            bool isProactive,
            IEnumerable<string> additionalSceneTags,
            Pawn playerNegotiator)
        {
            var scenarioContext = DialogueScenarioContext.CreateDiplomacy(faction, isProactive, additionalSceneTags);
            string promptChannel = Owner.ResolvePromptChannelForContext(scenarioContext);
            List<ResolvedPromptNodePlacement> placements = Owner.ResolveDiplomacyNodePlacements(
                promptChannel,
                config,
                scenarioContext,
                faction,
                playerNegotiator);
            var root = new PromptHierarchyNode("prompt_context");
            Owner.AddTextNodeIfNotEmpty(root, "channel", "diplomacy");
            Owner.AddTextNodeIfNotEmpty(root, "mode", isProactive ? "proactive" : "manual");
            Owner.AddTextNodeIfNotEmpty(root, "environment", host.BuildEnvironmentPromptBlocks(config, scenarioContext));
            Owner.AddTextNodeIfNotEmpty(root, "mandatory_race_profile", Owner.BuildMandatoryRaceProfileBlock(config, scenarioContext));
            Owner.ApplyResolvedNodePlacements(root, placements, PromptUnifiedNodeSlot.MetadataAfter);
            Owner.ApplyResolvedNodePlacements(root, placements, PromptUnifiedNodeSlot.MainChainBefore);
            Owner.AddNodeIfAnyChildren(root, host.WorkspaceComposer.BuildMainChainPromptSectionNode(
                RimTalkPromptChannel.Diplomacy,
                config,
                scenarioContext,
                config?.EnvironmentPrompt));
            Owner.ApplyResolvedNodePlacements(root, placements, PromptUnifiedNodeSlot.MainChainBefore);
            Owner.ApplyResolvedNodePlacements(root, placements, PromptUnifiedNodeSlot.MainChainAfter);

            var instruction = root.AddChild("instruction_stack");
            Owner.AddTextNodeIfNotEmpty(instruction, "faction_characteristics", Owner.ResolveFactionPromptText(faction, config, scenarioContext));

            PromptHierarchyNode dynamicData = Owner.BuildDiplomacyDynamicDataNode(config, faction, playerNegotiator);
            if (dynamicData != null)
            {
                root.Children.Add(dynamicData);
            }
            Owner.ApplyResolvedNodePlacements(root, placements, PromptUnifiedNodeSlot.DynamicDataAfter);
            Owner.ApplyResolvedNodePlacements(root, placements, PromptUnifiedNodeSlot.ContractBeforeEnd);
            if (instruction.Children.Count == 0)
            {
                root.Children.Remove(instruction);
            }

            return PromptHierarchyRenderer.Render(root);
        }

internal string BuildDiplomacyStrategySystemPromptHierarchical(
            Faction faction,
            SystemPromptConfig config,
            IEnumerable<string> additionalSceneTags,
            DiplomacyStrategyPromptContext strategyContext)
        {
            config ??= host.DomainStore.LoadConfig() ?? host.DomainStore.CreateDefaultConfig();
            var scenarioContext = DialogueScenarioContext.CreateDiplomacy(faction, false, additionalSceneTags);
            strategyContext ??= new DiplomacyStrategyPromptContext();
            string promptChannel = RimTalkPromptEntryChannelCatalog.DiplomacyStrategy;
            List<ResolvedPromptNodePlacement> placements = Owner.ResolveStrategyNodePlacements(
                promptChannel,
                config,
                scenarioContext,
                strategyContext);

            var root = new PromptHierarchyNode("prompt_context");
            Owner.AddTextNodeIfNotEmpty(root, "channel", RimTalkPromptEntryChannelCatalog.DiplomacyStrategy);
            Owner.AddTextNodeIfNotEmpty(root, "mode", "manual");
            Owner.AddTextNodeIfNotEmpty(root, "environment", host.BuildEnvironmentPromptBlocks(config, scenarioContext));
            Owner.ApplyResolvedNodePlacements(root, placements, PromptUnifiedNodeSlot.MetadataAfter);
            Owner.ApplyResolvedNodePlacements(root, placements, PromptUnifiedNodeSlot.MainChainBefore);
            Owner.AddNodeIfAnyChildren(root, host.WorkspaceComposer.BuildPromptSectionAggregateNode(
                config,
                RimTalkPromptEntryChannelCatalog.DiplomacyStrategy,
                scenarioContext,
                config?.EnvironmentPrompt));
            Owner.ApplyResolvedNodePlacements(root, placements, PromptUnifiedNodeSlot.MainChainBefore);
            Owner.ApplyResolvedNodePlacements(root, placements, PromptUnifiedNodeSlot.MainChainAfter);

            var instruction = root.AddChild("instruction_stack");
            Owner.AddTextNodeIfNotEmpty(instruction, "faction_characteristics", Owner.ResolveFactionPromptText(faction, config, scenarioContext));

            Owner.ApplyResolvedNodePlacements(root, placements, PromptUnifiedNodeSlot.DynamicDataAfter);
            Owner.ApplyResolvedNodePlacements(root, placements, PromptUnifiedNodeSlot.ContractBeforeEnd);
            if (instruction.Children.Count == 0)
            {
                root.Children.Remove(instruction);
            }

            return PromptHierarchyRenderer.Render(root);
        }

internal string BuildRpgSystemPromptHierarchical(
            Pawn initiator,
            Pawn target,
            bool isProactive,
            IEnumerable<string> additionalSceneTags)
        {
            var settings = RelationsMod.Settings;
            settings?.EnsureRpgPromptTextsLoaded();
            SystemPromptConfig config = host.DomainStore.LoadConfig() ?? host.DomainStore.CreateDefaultConfig();
            var scenarioContext = DialogueScenarioContext.CreateRpg(initiator, target, isProactive, additionalSceneTags);
            bool samePlayerFaction =
                initiator?.Faction != null &&
                initiator.Faction == target?.Faction &&
                initiator.Faction.IsPlayer;
            bool preferCompactContext = !isProactive && samePlayerFaction;
            PromptPolicyConfig promptPolicy = Owner.ResolvePromptPolicyConfig(config);
            bool includeOpeningObjective = Owner.IsOpeningTurnContext(scenarioContext);
            bool allowMemoryCompressionScheduling = RpgPromptTurnContextScope.Current?.AllowMemoryCompressionScheduling ?? true;
            bool allowMemoryColdLoad = RpgPromptTurnContextScope.Current?.AllowMemoryColdLoad ?? true;
            string unresolvedIntent = includeOpeningObjective
                ? string.Empty
                : RpgNpcDialogueArchiveManager.Instance.BuildUnresolvedIntentSummary(target, initiator);
            string promptChannel = Owner.ResolvePromptChannelForContext(scenarioContext);
            List<ResolvedPromptNodePlacement> placements = Owner.ResolveRpgNodePlacements(
                promptChannel,
                settings,
                config,
                scenarioContext,
                initiator,
                target,
                unresolvedIntent,
                includeOpeningObjective);

            var root = new PromptHierarchyNode("prompt_context");
            Owner.AddTextNodeIfNotEmpty(root, "channel", "rpg");
            Owner.AddTextNodeIfNotEmpty(root, "mode", isProactive ? "proactive" : "manual");
            string environmentBlock = host.BuildEnvironmentPromptBlocks(config, scenarioContext);
            if (preferCompactContext)
            {
                environmentBlock = Owner.CompactRpgEnvironmentBlock(environmentBlock);
            }
            Owner.AddTextNodeIfNotEmpty(root, "environment", environmentBlock);
            Owner.AddTextNodeIfNotEmpty(root, "mandatory_race_profile", Owner.BuildMandatoryRaceProfileBlock(config, scenarioContext));
            Owner.ApplyResolvedNodePlacements(root, placements, PromptUnifiedNodeSlot.MetadataAfter);
            Owner.ApplyResolvedNodePlacements(root, placements, PromptUnifiedNodeSlot.MainChainBefore);
            Owner.AddNodeIfAnyChildren(root, host.WorkspaceComposer.BuildMainChainPromptSectionNode(
                RimTalkPromptChannel.Rpg,
                config,
                scenarioContext,
                config?.EnvironmentPrompt));
            Owner.ApplyResolvedNodePlacements(root, placements, PromptUnifiedNodeSlot.MainChainBefore);
            Owner.ApplyResolvedNodePlacements(root, placements, PromptUnifiedNodeSlot.MainChainAfter);

            var roleStack = root.AddChild("role_stack");
            Owner.AddTextNodeIfNotEmpty(roleStack, "personality_override", host.ContextAssembler.ResolveRpgPawnPersonaPrompt(target));

            Owner.AddTextNodeIfNotEmpty(root, "dynamic_faction_memory",
                DialogueSummaryService.BuildRpgDynamicFactionMemoryBlock(target?.Faction, target));
            Owner.AddTextNodeIfNotEmpty(root, "dynamic_npc_personal_memory",
                RpgNpcDialogueArchiveManager.Instance.BuildPromptMemoryBlock(
                    target,
                    initiator,
                    promptPolicy?.SummaryTimelineTurnLimit ?? 8,
                    promptPolicy?.SummaryCharBudget ?? 1200,
                    allowCompressionScheduling: allowMemoryCompressionScheduling,
                    allowCacheLoad: allowMemoryColdLoad));

            PromptHierarchyNode actorState = Owner.BuildRpgActorStateNode(
                settings,
                config,
                initiator,
                target,
                preferCompactContext);
            if (actorState != null)
            {
                root.Children.Add(actorState);
            }
            Owner.ApplyResolvedNodePlacements(root, placements, PromptUnifiedNodeSlot.DynamicDataAfter);
            Owner.ApplyResolvedNodePlacements(root, placements, PromptUnifiedNodeSlot.ContractBeforeEnd);

            bool preferCompactApiContract = preferCompactContext;
            Owner.AddTextNodeIfNotEmpty(root, "api_contract", Owner.BuildRpgApiContractText(settings, config, scenarioContext, preferCompactApiContract));
            return PromptHierarchyRenderer.Render(root);
        }

internal PromptHierarchyNode BuildDiplomacyDynamicDataNode(SystemPromptConfig config, Faction faction, Pawn playerNegotiator)
        {
            if (config?.DynamicDataInjection == null)
            {
                return null;
            }

            var node = new PromptHierarchyNode("dynamic_data");
            DynamicDataInjectionConfig dyn = config.DynamicDataInjection;
            if (dyn.InjectMemoryData)
            {
                Owner.AddTextNodeIfNotEmpty(node, "memory_data", Owner.BuildTextBlock(sb => host.ContextAssembler.AppendMemoryData(sb, faction)));
            }

            if (dyn.InjectFactionInfo)
            {
                Owner.AddTextNodeIfNotEmpty(node, "faction_info", Owner.BuildTextBlock(sb => host.ContextAssembler.AppendFactionInfo(sb, faction)));
                Owner.AddTextNodeIfNotEmpty(node, "player_pawn_profile", host.ContextAssembler.BuildPlayerPawnContextForPrompt(faction, playerNegotiator));
                Owner.AddTextNodeIfNotEmpty(node, "player_royalty_summary", host.ContextAssembler.BuildPlayerRoyaltySummaryForPrompt(faction, playerNegotiator));
                Owner.AddTextNodeIfNotEmpty(node, "faction_settlement_summary", host.ContextAssembler.BuildFactionSettlementSummaryForPrompt(faction));
                Owner.AddTextNodeIfNotEmpty(node, "faction_quest_status", host.ContextAssembler.BuildFactionQuestStatusBlockForPrompt(faction));
            }

            return node.Children.Count > 0 ? node : null;
        }

internal PromptHierarchyNode BuildRpgActorStateNode(
            RelationsSettings settings,
            SystemPromptConfig config,
            Pawn initiator,
            Pawn target,
            bool preferCompactContext)
        {
            var node = new PromptHierarchyNode("actor_state");
            bool samePlayerFaction =
                initiator?.Faction != null &&
                initiator.Faction == target?.Faction &&
                initiator.Faction.IsPlayer;

            if (settings?.RPGInjectSelfStatus == true)
            {
                Owner.AddTextNodeIfNotEmpty(node, "self_status",
                    Owner.BuildTextBlock(sb => host.RpgBuilder.AppendRPGPawnInfo(
                        sb,
                        target,
                        true,
                        config?.EnvironmentPrompt?.RpgSceneParamSwitches,
                        includePlayerSharedColonyContext: true,
                        includeStaticProfileDetails: !preferCompactContext)));
            }

            if (settings?.RPGInjectInterlocutorStatus == true)
            {
                Owner.AddTextNodeIfNotEmpty(node, "interlocutor_status",
                    Owner.BuildTextBlock(sb => host.RpgBuilder.AppendRPGPawnInfo(
                        sb,
                        initiator,
                        false,
                        config?.EnvironmentPrompt?.RpgSceneParamSwitches,
                        includePlayerSharedColonyContext: !samePlayerFaction,
                        includeStaticProfileDetails: !samePlayerFaction && !preferCompactContext)));
            }

            if (settings?.RPGInjectFactionBackground == true)
            {
                Owner.AddTextNodeIfNotEmpty(node, "target_faction_context", Owner.BuildTextBlock(sb => host.RpgBuilder.AppendRPGFactionContext(sb, target)));
                if (initiator?.Faction != target?.Faction)
                {
                    Owner.AddTextNodeIfNotEmpty(node, "interlocutor_faction_context",
                        Owner.BuildTextBlock(sb => host.RpgBuilder.AppendRPGFactionContext(sb, initiator)));
                }
            }

            return node.Children.Count > 0 ? node : null;
        }

internal void ApplyResolvedNodePlacements(
            PromptHierarchyNode root,
            IEnumerable<ResolvedPromptNodePlacement> placements,
            PromptUnifiedNodeSlot slot)
        {
            if (root == null || placements == null)
            {
                return;
            }

            foreach (ResolvedPromptNodePlacement placement in placements)
            {
                if (placement == null || placement.Slot != slot || !placement.Enabled)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(placement.Content))
                {
                    placement.Applied = false;
                    continue;
                }

                Owner.AddTextNodeIfNotEmpty(root, placement.OutputTag, placement.Content);
                placement.Applied = true;
            }
        }

internal List<PromptUnifiedNodeLayoutConfig> GetOrderedNodeLayouts(string promptChannel)
        {
            var allowedNodeIds = new HashSet<string>(
                PromptUnifiedNodeSchemaCatalog.GetAllowedNodes(promptChannel).Select(item => item.Id),
                StringComparer.OrdinalIgnoreCase);
            if (allowedNodeIds.Count == 0)
            {
                return new List<PromptUnifiedNodeLayoutConfig>();
            }

            List<PromptUnifiedNodeLayoutConfig> fromSettings = RelationsMod.Settings?.GetPromptNodeLayouts(promptChannel);
            if (fromSettings != null && fromSettings.Count > 0)
            {
                var filtered = new Dictionary<string, PromptUnifiedNodeLayoutConfig>(StringComparer.OrdinalIgnoreCase);
                foreach (PromptUnifiedNodeLayoutConfig layout in fromSettings)
                {
                    if (layout == null || string.IsNullOrWhiteSpace(layout.NodeId))
                    {
                        continue;
                    }

                    if (!allowedNodeIds.Contains(layout.NodeId))
                    {
                        Log.Error($"[RimAI.Relations] Runtime node layout '{layout.NodeId}' is not allowed for channel '{promptChannel}'. Layout ignored.");
                        continue;
                    }

                    filtered[layout.NodeId] = layout.Clone();
                }

                foreach (string nodeId in allowedNodeIds)
                {
                    if (!filtered.ContainsKey(nodeId))
                    {
                        filtered[nodeId] = PromptUnifiedNodeLayoutDefaults.BuildDefaultLayout(promptChannel, nodeId);
                    }
                }

                return filtered.Values
                    .OrderBy(item => item.GetSlot())
                    .ThenBy(item => item.Order)
                    .ThenBy(item => item.NodeId, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            return PromptUnifiedNodeSchemaCatalog.GetAllowedNodes(promptChannel)
                .Select(node => PromptUnifiedNodeLayoutDefaults.BuildDefaultLayout(promptChannel, node.Id))
                .OrderBy(item => item.GetSlot())
                .ThenBy(item => item.Order)
                .ThenBy(item => item.NodeId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
