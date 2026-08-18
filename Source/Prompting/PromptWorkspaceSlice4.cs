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
    internal sealed class PromptWorkspaceSlice4 : PromptWorkspaceComposerCollaborator
    {
        internal PromptWorkspaceSlice4(PromptWorkspaceComposer owner) : base(owner)
        {
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
            foreach (string nodeId in Owner.GetRequiredRuntimeNodeIds(channel))
            {
                string content = Owner.FindEnabledNodeContent(composed.Placements, nodeId);
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

            if (Owner.RequiresMandatoryRaceProfileBlock(channel))
            {
                string raceProfile = Owner.FindPreviewBlockContent(composed.Preview?.Blocks, "mandatory_race_profile");
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
                values["ctx.mode"] = Owner.ResolvePromptModeForCompose(scenarioContext, promptChannel);
                Owner.MergeAdditionalValues(values, additionalValues);
                return values;
            }

            // Fall back to last-known snapshot values (expired but preserved) before using
            // hard-coded placeholders — this resolves many variables that would otherwise
            // render as raw {{ ... }} tokens in the preview.
            values = TryBuildFromLastKnown(promptChannel);
            if (values != null)
            {
                values["ctx.channel"] = promptChannel ?? string.Empty;
                values["ctx.mode"] = Owner.ResolvePromptModeForCompose(scenarioContext, promptChannel);
                Owner.MergeAdditionalValues(values, additionalValues);
                return values;
            }

            values = host.NodeSupport.CreatePromptVariableSeed();
            values["ctx.channel"] = promptChannel ?? string.Empty;
            values["ctx.mode"] = Owner.ResolvePromptModeForCompose(scenarioContext, promptChannel);

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
            Owner.MergeAdditionalValues(values, additionalValues);
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
            string tag = Owner.SanitizePayloadTag(payloadTag);
            string text = Owner.EscapePromptXml(payloadText);
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
}
