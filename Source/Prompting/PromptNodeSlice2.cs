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
    internal sealed class PromptNodeSlice2 : PromptNodeSupportCollaborator
    {
        internal PromptNodeSlice2(PromptNodeSupport owner) : base(owner)
        {
        }

internal List<ResolvedPromptNodePlacement> ResolveDiplomacyNodePlacements(
            string promptChannel,
            SystemPromptConfig config,
            DialogueScenarioContext context,
            Faction faction,
            Pawn playerNegotiator)
        {
            string apiLimitsBody = Owner.BuildTextBlock(sb => host.DiplomacyBuilder.AppendApiLimits(sb, faction));
            Dictionary<string, object> questContext = host.DiplomacyBuilder.BuildQuestPromptContext(context);
            string questGuidanceBody = Owner.BuildTextBlock(sb =>
            {
                host.DiplomacyBuilder.AppendDynamicQuestGuidance(sb, faction, questContext);
                host.DiplomacyBuilder.AppendQuestSelectionHardRules(sb);
            });
            string responseContractBody = Owner.BuildTextBlock(sb =>
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

            var placements = new List<ResolvedPromptNodePlacement>();
            List<PromptUnifiedNodeLayoutConfig> diploLayouts = Owner.GetOrderedNodeLayouts(promptChannel);
            Log.Message($"[RimAI.Relations] ResolveDiplomacyNodePlacements: channel={promptChannel}, layout_count={diploLayouts.Count}, node_ids=[{string.Join(", ", diploLayouts.Select(l => l.NodeId))}]");
            foreach (PromptUnifiedNodeLayoutConfig layout in diploLayouts)
            {
                if (layout == null)
                {
                    continue;
                }

                string nodeId = layout.NodeId;
                var placement = new ResolvedPromptNodePlacement
                {
                    PromptChannel = promptChannel,
                    NodeId = nodeId,
                    Slot = layout.GetSlot(),
                    Order = layout.Order,
                    Enabled = layout.Enabled,
                    OutputTag = nodeId
                };

                switch (nodeId)
                {
                    case "fact_grounding":
                        placement.OutputTag = "fact_grounding";
                        placement.Content = Owner.BuildFactGroundingGuidanceText(config, context);
                        break;
                    case "output_language":
                        placement.OutputTag = "output_language";
                        placement.Content = Owner.BuildOutputLanguageGuidance(RelationsMod.Settings, config, context);
                        break;
                    case "decision_policy":
                        placement.OutputTag = "decision_policy";
                        placement.Content = Owner.BuildDecisionPolicyText(config, context);
                        break;
                    case "turn_objective":
                        placement.OutputTag = "turn_objective";
                        placement.Content = Owner.BuildTurnObjectiveText(
                            config,
                            context,
                            "Address the player's latest explicit intent from the current turn first.",
                            "After finishing the primary objective, you may add one natural follow-up extension.");
                        break;
                    case "topic_shift_rule":
                        placement.OutputTag = "topic_shift_rule";
                        placement.Content = Owner.BuildTopicShiftRuleText(config, context);
                        break;
                    case "diplomacy_fallback_role":
                        placement.OutputTag = "diplomacy_fallback_role";
                        placement.Content = Owner.ResolveFactionPromptText(faction, config, context);
                        break;
                    case "social_circle_action_rule":
                        placement.OutputTag = "social_circle_action_rule";
                        placement.Content = Owner.BuildSocialCircleActionRuleText(config, context);
                        break;
                    case "api_limits_node_template":
                        placement.OutputTag = "api_limits";
                        placement.Content = Owner.RenderPromptNodeTemplate(
                            config,
                            context,
                            Owner.ResolveUnifiedNodeTemplate(promptChannel, "api_limits_node_template", PromptTextConstants.ApiLimitsNodeLiteralDefault),
                            "api_limits_body",
                            apiLimitsBody);
                        break;
                    case "quest_guidance_node_template":
                        placement.OutputTag = "quest_guidance";
                        placement.Content = Owner.ResolveQuestGuidanceNodeText(
                            context,
                            promptChannel,
                            questGuidanceBody);
                        break;

                    case "response_contract_node_template":
                        placement.OutputTag = "response_contract";
                        placement.Content = Owner.RenderPromptNodeTemplate(
                            config,
                            context,
                            Owner.ResolveUnifiedNodeTemplate(promptChannel, "response_contract_node_template", PromptTextConstants.ResponseContractNodeLiteralDefault),
                            "response_contract_body",
                            responseContractBody);
                        break;
                    case "common_knowledge":
                        placement.OutputTag = "common_knowledge";
                        placement.Content = host.ContextAssembler.BuildCommonKnowledgeBlock(ExpandMemoryMatchContext.PlayerMessage);
                        break;
                    default:
                        placement.Content = string.Empty;
                        break;
                }

                placements.Add(placement);
            }

            return placements
                .OrderBy(item => item.Slot)
                .ThenBy(item => item.Order)
                .ThenBy(item => item.NodeId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

internal List<ResolvedPromptNodePlacement> ResolveRpgNodePlacements(
            string promptChannel,
            RelationsSettings settings,
            SystemPromptConfig config,
            DialogueScenarioContext context,
            Pawn initiator,
            Pawn target,
            string unresolvedIntent,
            bool includeOpeningObjective)
        {
            var placements = new List<ResolvedPromptNodePlacement>();
            List<PromptUnifiedNodeLayoutConfig> layouts = Owner.GetOrderedNodeLayouts(promptChannel);
            Log.Message($"[RimAI.Relations] ResolveRpgNodePlacements: channel={promptChannel}, layout_count={layouts.Count}, node_ids=[{string.Join(", ", layouts.Select(l => l.NodeId))}]");
            foreach (PromptUnifiedNodeLayoutConfig layout in layouts)
            {
                if (layout == null)
                {
                    continue;
                }

                string nodeId = layout.NodeId;
                var placement = new ResolvedPromptNodePlacement
                {
                    PromptChannel = promptChannel,
                    NodeId = nodeId,
                    Slot = layout.GetSlot(),
                    Order = layout.Order,
                    Enabled = layout.Enabled,
                    OutputTag = nodeId
                };

                switch (nodeId)
                {
                    case "fact_grounding":
                        placement.OutputTag = "fact_grounding";
                        placement.Content = Owner.BuildFactGroundingGuidanceText(config, context);
                        break;
                    case "output_language":
                        placement.OutputTag = "output_language";
                        placement.Content = Owner.BuildOutputLanguageGuidance(settings, config, context);
                        break;
                    case "decision_policy":
                        placement.OutputTag = "decision_policy";
                        placement.Content = Owner.BuildDecisionPolicyText(config, context);
                        break;
                    case "turn_objective":
                        placement.OutputTag = "turn_objective";
                        placement.Content = Owner.BuildTurnObjectiveText(
                            config,
                            context,
                            Owner.BuildPrimaryObjectiveFromIntent(unresolvedIntent),
                            "After completing the primary objective, optionally add one relevant follow-up.");
                        break;
                    case "topic_shift_rule":
                        placement.OutputTag = "topic_shift_rule";
                        placement.Content = Owner.BuildTopicShiftRuleText(config, context);
                        break;
                    case "opening_objective":
                        placement.OutputTag = "opening_objective";
                        placement.Content = includeOpeningObjective
                            ? Owner.BuildOpeningObjectiveText(config, context, unresolvedIntent)
                            : string.Empty;
                        break;
                    case "rpg_role_setting_fallback":
                        placement.OutputTag = "role_setting";
                        placement.Content = Owner.BuildRpgRoleSettingText(settings, config, context, target);
                        break;
                    case "rpg_relationship_profile":
                        placement.OutputTag = "relationship_profile";
                        placement.Content = Owner.BuildRpgRelationshipProfileText(settings, initiator, target, context);
                        break;
                    case "rpg_kinship_boundary":
                        placement.OutputTag = "kinship_boundary_rule";
                        // Keep node/layout compatibility but avoid duplicate guidance output.
                        placement.Content = string.Empty;
                        break;
                    case "rpg_proactive_romance":
                        placement.OutputTag = "proactive_romance_rule";
                        placement.Content = Owner.RenderPromptNodeTemplate(
                            config,
                            context,
                            Owner.ResolveUnifiedNodeTemplate(promptChannel, "rpg_proactive_romance", Owner.ResolveRpgProactiveRomanceRuleTemplate(settings)),
                            "proactive_romance_rule",
                            string.Empty);
                        break;
                    case "rpg_proactive_social":
                        placement.OutputTag = "proactive_social_rule";
                        placement.Content = Owner.RenderPromptNodeTemplate(
                            config,
                            context,
                            Owner.ResolveUnifiedNodeTemplate(promptChannel, "rpg_proactive_social", Owner.ResolveRpgProactiveSocialActionRuleTemplate(settings)),
                            "proactive_social_rule",
                            string.Empty);
                        break;
                    case "response_contract_node_template":
                        placement.OutputTag = "response_contract";
                        bool samePlayerFaction =
                            initiator?.Faction != null &&
                            initiator.Faction == target?.Faction &&
                            initiator.Faction.IsPlayer;
                        bool preferCompactApiContract = !context.IsProactive && samePlayerFaction;
                        placement.Content = Owner.RenderPromptNodeTemplate(
                            config,
                            context,
                            Owner.ResolveUnifiedNodeTemplate(promptChannel, "response_contract_node_template", PromptTextConstants.ResponseContractNodeLiteralDefault),
                            "response_contract_body",
                            Owner.BuildRpgApiContractText(settings, config, context, preferCompactApiContract));
                        break;
                    case "common_knowledge":
                        placement.OutputTag = "common_knowledge";
                        placement.Content = host.ContextAssembler.BuildCommonKnowledgeBlock(ExpandMemoryMatchContext.PlayerMessage);
                        break;
                    case "expandmemory_npc_memory":
                        placement.OutputTag = "expandmemory_npc_memory";
                        placement.Content = host.ContextAssembler.BuildExpandMemoryPawnBlock(target);
                        break;

                    default:
                        placement.Content = string.Empty;
                        break;
                }

                placements.Add(placement);
            }

            return placements
                .OrderBy(item => item.Slot)
                .ThenBy(item => item.Order)
                .ThenBy(item => item.NodeId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

internal List<ResolvedPromptNodePlacement> ResolveStrategyNodePlacements(
            string promptChannel,
            SystemPromptConfig config,
            DialogueScenarioContext context,
            DiplomacyStrategyPromptContext strategyContext)
        {
            var placements = new List<ResolvedPromptNodePlacement>();
            foreach (PromptUnifiedNodeLayoutConfig layout in Owner.GetOrderedNodeLayouts(promptChannel))
            {
                if (layout == null)
                {
                    continue;
                }

                string nodeId = layout.NodeId;
                var placement = new ResolvedPromptNodePlacement
                {
                    PromptChannel = promptChannel,
                    NodeId = nodeId,
                    Slot = layout.GetSlot(),
                    Order = layout.Order,
                    Enabled = layout.Enabled,
                    OutputTag = nodeId
                };

                switch (nodeId)
                {
                    case "fact_grounding":
                        placement.OutputTag = "fact_grounding";
                        placement.Content = Owner.BuildFactGroundingGuidanceText(config, context);
                        break;
                    case "output_language":
                        placement.OutputTag = "output_language";
                        placement.Content = Owner.BuildOutputLanguageGuidance(RelationsMod.Settings, config, context);
                        break;
                    case "decision_policy":
                        placement.OutputTag = "decision_policy";
                        placement.Content = Owner.BuildDiplomacyStrategyDecisionPolicyText();
                        break;
                    case "turn_objective":
                        placement.OutputTag = "turn_objective";
                        placement.Content = Owner.BuildDiplomacyStrategyTurnObjectiveText();
                        break;
                    case "strategy_output_contract":
                        placement.OutputTag = "strategy_output_contract";
                        placement.Content = Owner.BuildDiplomacyStrategyOutputContractText();
                        break;
                    case "strategy_player_negotiator_context_template":
                        placement.OutputTag = "player_negotiator_context";
                        placement.Content = Owner.RenderStrategyNodeTemplate(
                            promptChannel,
                            "strategy_player_negotiator_context_template",
                            "dialogue.strategy_player_negotiator_context_body",
                            strategyContext?.NegotiatorContextText,
                            context);
                        break;
                    case "strategy_fact_pack_template":
                        placement.OutputTag = "strategy_fact_pack";
                        placement.Content = Owner.RenderStrategyNodeTemplate(
                            promptChannel,
                            "strategy_fact_pack_template",
                            "dialogue.strategy_fact_pack_body",
                            strategyContext?.StrategyFactPackText,
                            context);
                        break;
                    case "strategy_scenario_dossier_template":
                        placement.OutputTag = "strategy_scenario_dossier";
                        placement.Content = Owner.RenderStrategyNodeTemplate(
                            promptChannel,
                            "strategy_scenario_dossier_template",
                            "dialogue.strategy_scenario_dossier_body",
                            strategyContext?.ScenarioDossierText,
                            context);
                        break;

                    default:
                        placement.Content = string.Empty;
                        break;
                }

                placements.Add(placement);
            }

            return placements
                .OrderBy(item => item.Slot)
                .ThenBy(item => item.Order)
                .ThenBy(item => item.NodeId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

internal string BuildRpgKinshipBoundaryGuidanceText(
            RelationsSettings settings,
            Pawn initiator,
            Pawn target,
            DialogueScenarioContext context)
        {
            if (initiator == null || target == null)
            {
                return string.Empty;
            }

            bool kinship = Owner.HasAnyBloodRelationBetweenPair(initiator, target);
            if (!kinship)
            {
                return string.Empty;
            }

            string kinshipValue = kinship ? "yes" : "no";
            string romanceState = Owner.ResolvePairRomanceState(initiator, target);
            var variables = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["pawn.initiator.name"] = initiator.LabelShort ?? "Unknown",
                ["pawn.target.name"] = target.LabelShort ?? "Unknown",
                ["pawn.relation.kinship"] = kinshipValue,
                ["pawn.relation.romance_state"] = romanceState,
                ["pawn.initiator"] = initiator,
                ["pawn.target"] = target
            };

            string promptChannel = Owner.ResolvePromptChannelForContext(context) ?? RimTalkPromptEntryChannelCatalog.RpgDialogue;
            string template = Owner.ResolveUnifiedNodeTemplate(
                promptChannel,
                "rpg_kinship_boundary",
                Owner.ResolveRpgKinshipBoundaryRuleTemplate(settings));
            return Owner.ApplyPromptSourceTag(
                Owner.RenderTemplateOrThrow("prompt_templates.rpg_kinship_boundary", "rpg", template, variables).Trim(),
                true);
        }

internal void AddTextNodeIfNotEmpty(PromptHierarchyNode parent, string id, string text, bool fromFile = false)
        {
            if (parent == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            parent.AddChild(id, Owner.ApplyPromptSourceTag(text.Trim(), fromFile));
        }

internal void AddNodeIfAnyChildren(PromptHierarchyNode parent, PromptHierarchyNode child)
        {
            if (parent == null || child == null || child.Children.Count == 0)
            {
                return;
            }

            parent.Children.Add(child);
        }
    }
}
