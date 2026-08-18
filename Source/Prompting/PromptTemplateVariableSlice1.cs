using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.Guards;
using RimWorld;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Context;

using Ustas.RimAI.Communication.Relations.Persistence;

namespace Ustas.RimAI.Communication.Relations.Prompting
{
    internal sealed class PromptTemplateVariableSlice1 : PromptTemplateVariableServiceCollaborator
    {
        internal PromptTemplateVariableSlice1(PromptTemplateVariableService owner) : base(owner)
        {
        }

public IReadOnlyList<PromptTemplateVariableDefinition> GetTemplateVariableDefinitions()
        {
            return PromptVariableCatalog.GetDefinitions()
                .Where(item => item != null)
                .Select(item => item.ToTemplateDefinition())
                .ToList();
        }

public TemplateVariableValidationResult ValidateTemplateVariables(
            string templateText,
            IEnumerable<string> additionalKnownVariables)
        {
            return Owner.ValidateTemplateVariables(
                templateText,
                TemplateVariableValidationContext.FromAdditionalKnownVariables(additionalKnownVariables));
        }

internal TemplateVariableValidationResult ValidateTemplateVariables(
            string templateText,
            TemplateVariableValidationContext validationContext)
        {
            var result = new TemplateVariableValidationResult();
            if (string.IsNullOrWhiteSpace(templateText))
            {
                return result;
            }

            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var unknown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var validationPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            TemplateVariableValidationContext context = validationContext ?? TemplateVariableValidationContext.CreateDefault();
            MatchCollection matches = TemplateVariableRegex.Matches(templateText);
            for (int i = 0; i < matches.Count; i++)
            {
                string name = Owner.NormalizeTemplateVariableName(matches[i].Groups[1].Value);
                if (name.Length == 0)
                {
                    continue;
                }

                if (context.Contains(name))
                {
                    used.Add(name);
                }
                else
                {
                    unknown.Add(name);
                }

                if (Owner.IsNamespacedVariablePath(name))
                {
                    validationPaths.Add(name);
                }
            }

            Owner.TryCollectScribanDiagnostic(templateText, validationPaths, result);
            result.UsedVariables.AddRange(used.OrderBy(item => item));
            result.UnknownVariables.AddRange(unknown.OrderBy(item => item));
            return result;
        }

internal string RenderTemplateVariables(
            string templateText,
            DialogueScenarioContext context,
            EnvironmentPromptConfig envConfig,
            out List<string> usedVariables,
            out List<string> unknownVariables)
        {
            if (string.IsNullOrWhiteSpace(templateText) || templateText.IndexOf("{{", StringComparison.Ordinal) < 0)
            {
                usedVariables = new List<string>();
                unknownVariables = new List<string>();
                return templateText ?? string.Empty;
            }

            TemplateVariableValidationResult validation = Owner.ValidateTemplateVariables(templateText);
            usedVariables = validation.UsedVariables.OrderBy(item => item).ToList();
            unknownVariables = validation.UnknownVariables.OrderBy(item => item).ToList();
            if (unknownVariables.Count > 0)
            {
                string channel = context?.IsRpg == true ? "rpg" : "diplomacy";
                throw new PromptRenderException(
                    "scene_entry.template",
                    channel,
                    new PromptRenderDiagnostic
                    {
                        ErrorCode = PromptRenderErrorCode.UnknownVariable,
                        Message = $"Unknown namespaced variable: {unknownVariables[0]}"
                    });
            }

            string resolvedChannel = context?.IsRpg == true ? "rpg" : "diplomacy";
            const string templateId = "scene_entry.template";
            PromptRenderContext renderContext = Owner.BuildTemplateRenderContext(templateId, resolvedChannel, context, envConfig);
            return PromptTemplateRenderer.RenderOrThrow(templateId, resolvedChannel, templateText, renderContext);
        }

internal PromptRenderContext BuildTemplateRenderContext(
            string templateId,
            string channel,
            DialogueScenarioContext context,
            EnvironmentPromptConfig envConfig)
        {
            PromptRenderContext renderContext = PromptRenderContext.Create(templateId, channel);
            renderContext.SetValues(Owner.BuildTemplateVariableValues(templateId, channel, context, envConfig));
            return renderContext;
        }

internal Dictionary<string, object> BuildTemplateVariableValues(
            string templateId,
            string channel,
            DialogueScenarioContext context,
            EnvironmentPromptConfig envConfig)
        {
            var values = host.NodeSupport.CreatePromptVariableSeed();
            var variableContext = new PromptRuntimeVariableContext(templateId, channel, context, envConfig);
            List<IPromptRuntimeVariableProvider> providers = PromptRuntimeVariableRegistry.CreateRuntimeProviders(
                (path, runtimeContext) => Owner.ResolveTemplateVariableValue(path, runtimeContext.ScenarioContext, runtimeContext.EnvironmentConfig));
            for (int i = 0; i < providers.Count; i++)
            {
                IPromptRuntimeVariableProvider provider = providers[i];
                if (provider == null || !provider.IsAvailable(variableContext))
                {
                    continue;
                }

                provider.PopulateValues(values, variableContext);
            }

            values["system.game_language"] = LanguageDatabase.activeLanguage?.FriendlyNameNative
                ?? (RelationsMod.Settings?.GetEffectivePromptLanguage() ?? string.Empty);
            values["dialogue.mandatory_race_profile_body"] = host.NodeSupport.BuildMandatoryRaceProfileBody(context);
            bool isPreview = host.NodeSupport.IsPreviewScenario(context);
            if (context?.Initiator != null)
            {
                values["pawn.initiator"] = context.Initiator;
            }
            else if (isPreview)
            {
                values["pawn.initiator"] = host.NodeSupport.CreatePreviewPawnPlaceholder("PreviewInitiator");
            }

            if (context?.Target != null)
            {
                values["pawn.target"] = context.Target;
            }
            else if (isPreview)
            {
                values["pawn.target"] = host.NodeSupport.CreatePreviewPawnPlaceholder("PreviewTarget");
            }

            if (context?.Faction != null)
            {
                values["world.faction"] = context.Faction;
            }
            else if (isPreview)
            {
                values["world.faction"] = host.NodeSupport.CreatePreviewFactionPlaceholder("PreviewFaction");
            }

            return values;
        }

internal string NormalizeTemplateVariableName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
            {
                return string.Empty;
            }

            return rawName.Trim().ToLowerInvariant();
        }

internal bool IsNamespacedVariablePath(string variableName)
        {
            if (string.IsNullOrWhiteSpace(variableName))
            {
                return false;
            }

            int separator = variableName.IndexOf('.');
            if (separator <= 0)
            {
                return false;
            }

            string rootNamespace = variableName.Substring(0, separator).Trim();
            return AllowedTemplateVariableNamespaces.Contains(rootNamespace);
        }

internal void TryCollectScribanDiagnostic(
            string templateText,
            IEnumerable<string> variablePaths,
            TemplateVariableValidationResult result)
        {
            const string templateId = "editor.template_validation";
            const string channel = "editor";
            try
            {
                PromptRenderContext context = PromptTemplateRenderer.BuildValidationContext(templateId, channel, variablePaths);
                PromptTemplateRenderer.ValidateOrThrow(templateId, channel, templateText, context);
            }
            catch (PromptRenderException ex)
            {
                result.ScribanErrorCode = (int)ex.ErrorCode;
                result.ScribanErrorLine = ex.ErrorLine;
                result.ScribanErrorColumn = ex.ErrorColumn;
                result.ScribanErrorMessage = ex.Message ?? string.Empty;
            }
            catch (ArgumentException ex)
            {
                result.ScribanErrorCode = (int)PromptRenderErrorCode.UnknownVariable;
                result.ScribanErrorLine = 0;
                result.ScribanErrorColumn = 0;
                result.ScribanErrorMessage = ex.Message ?? string.Empty;
            }
        }

internal object ResolveTemplateVariableValue(
            string variableName,
            DialogueScenarioContext context,
            EnvironmentPromptConfig envConfig)
        {
            switch (variableName)
            {
                case "ctx.channel":
                    return context?.IsRpg == true ? "rpg" : "diplomacy";
                case "ctx.mode":
                    return context?.IsProactive == true ? "proactive" : "manual";
                case "system.target_language":
                    return RelationsMod.Settings?.GetEffectivePromptLanguage() ?? string.Empty;
                case "world.time.hour":
                    return Owner.BuildWorldTimeHourVariableValue(context);
                case "world.time.day":
                    return Owner.BuildWorldTimeDayVariableValue(context);
                case "world.time.quadrum":
                    return Owner.BuildWorldTimeQuadrumVariableValue(context);
                case "world.time.year":
                    return Owner.BuildWorldTimeYearVariableValue(context);
                case "world.time.season":
                    return Owner.BuildWorldTimeSeasonVariableValue(context);
                case "world.time.date":
                    return Owner.BuildWorldTimeDateVariableValue(context);
                case "world.weather":
                    return Owner.BuildWorldWeatherVariableValue(context);
                case "world.temperature":
                    return Owner.BuildWorldTemperatureVariableValue(context);
                case "world.faction.name":
                    return context?.Faction?.Name ?? "Unknown Faction";
                case "world.faction.description":
                    return Owner.BuildFactionDescriptionVariableText(context);
                case "pawn.initiator.name":
                    return context?.Initiator?.LabelShort ?? "Unknown";
                case "pawn.target.name":
                    return context?.Target?.LabelShort ?? "Unknown";
                case "pawn.recipient":
                    return context?.Target;
                case "pawn.recipient.name":
                    return context?.Target?.LabelShort ?? "Unknown";
                case "world.scene_tags":
                    return Owner.BuildSceneTagsVariableText(context);
                case "world.environment_params":
                    return Owner.BuildEnvironmentParamsVariableText(context, envConfig);
                case "world.recent_world_events":
                    return Owner.BuildRecentWorldEventsVariableText(context, envConfig);
                case "world.colony_status":
                    return Owner.BuildColonyStatusVariableText();
                case "world.colony_factions":
                    return Owner.BuildColonyFactionsVariableText();
                case "world.current_faction_profile":
                    return Owner.BuildCurrentFactionProfileVariableText(context);
                case "pawn.target.profile":
                    return host.RpgBuilder.BuildPawnProfileVariableText(context?.Target, context, envConfig);
                case "pawn.initiator.profile":
                    return host.RpgBuilder.BuildPawnProfileVariableText(context?.Initiator, context, envConfig);
                case "pawn.player.profile":
                    return Owner.BuildPlayerPawnProfileVariableText(context);
                case "pawn.player.royalty_summary":
                    return Owner.BuildPlayerRoyaltySummaryVariableText(context);
                case "world.faction_settlement_summary":
                    return Owner.BuildFactionSettlementSummaryVariableText(context);
                case "pawn.personality":
                    return Owner.BuildPawnPersonalityVariableText(context);
                case "dialogue.primary_objective":
                    return Owner.ResolveDialoguePrimaryObjectiveVariableValue(context);
                case "dialogue.optional_followup":
                    return Owner.ResolveDialogueOptionalFollowupVariableValue(context);
                case "dialogue.latest_unresolved_intent":
                    return Owner.ResolveDialogueLatestUnresolvedIntentVariableValue(context);
                case "dialogue.topic_shift_rule":
                    return "Complete the primary objective first, then allow at most one natural topic extension.";
                case "pawn.relation.kinship":
                    return Owner.ResolveRpgRelationSnapshot(context).Kinship;
                case "pawn.relation.romance_state":
                    return Owner.ResolveRpgRelationSnapshot(context).RomanceState;
                case "pawn.relation.social_summary":
                    return Owner.ResolveRpgRelationSnapshot(context).SocialSummary;
                case "dialogue.guidance":
                    return Owner.ResolveRpgRelationSnapshot(context).Guidance;
                case "world.faction.relation_band":
                    return Owner.BuildFactionRelationBandVariableValue(context);
                case "pawn.target.traits_summary":
                    return Owner.BuildPawnTraitsSummaryVariableValue(context);
                case "world.faction.ideology_summary":
                    return Owner.BuildFactionIdeologySummaryVariableValue(context);
                case "world.faction.tech_level":
                    return Owner.BuildFactionTechLevelVariableValue(context);
                case "world.social.diplomacy_stance":
                    return Owner.BuildSocialDiplomacyStanceVariableValue(context);
                case "world.social.source_faction":
                    return context?.Faction?.Name ?? string.Empty;
                case "world.social.target_faction":
                    return Faction.OfPlayer?.Name ?? "Colony";
                case "dialogue.action_names":
                    return Owner.BuildAvailableActionNamesVariableValue(context);
                case "dialogue.response_contract_body":
                    return Owner.BuildResponseContractBodyVariableValue(context);
                case "dialogue.api_limits_body":
                    return "Follow rate limits and token budgets as specified in the system prompt.";
                case "dialogue.example_line":
                    return string.Empty;
                case "dialogue.examples":
                    return string.Empty;
                case "dialogue.intent_hint":
                    return string.Empty;
                case "dialogue.mandatory_race_profile_body":
                    return string.Empty;
                case "dialogue.quest_guidance_body":
                    return string.Empty;
                case "dialogue.strategy_fact_pack_body":
                    return string.Empty;
                case "dialogue.strategy_player_negotiator_context_body":
                    return string.Empty;
                case "dialogue.strategy_scenario_dossier_body":
                    return string.Empty;
                case "dialogue.summary":
                    return string.Empty;
                case "dialogue.template_line":
                    return string.Empty;
                case "pawn.initiator":
                    return context?.Initiator?.LabelShortCap ?? "Unknown";
                case "pawn.profile":
                    return host.RpgBuilder.BuildPawnProfileVariableText(context?.Initiator, context, envConfig);
                case "pawn.pronouns.be_verb":
                    return "is";
                case "pawn.pronouns.object":
                    return "them";
                case "pawn.pronouns.possessive":
                    return "their";
                case "pawn.pronouns.seek_verb":
                    return "seeks";
                case "pawn.pronouns.subject":
                    return "They";
                case "pawn.pronouns.subject_lower":
                    return "they";
                case "pawn.speaker.animal_sound":
                    return "*growl*";
                case "pawn.speaker.baby_sound":
                    return "*coo*";
                case "pawn.speaker.default_sound":
                    return string.Empty;
                case "pawn.speaker.kind":
                    return context?.Initiator?.kindDef?.label ?? "human";
                case "pawn.speaker.mechanoid_sound":
                    return "*beep*";
                case "pawn.target":
                    return context?.Target?.LabelShortCap ?? "Unknown";
                case "system.game_language":
                    return Prefs.LangFolderName ?? "English";
                case "system.punctuation.close_paren":
                    return ")";
                case "system.punctuation.open_paren":
                    return "(";
                case "world.social.category":
                    return string.Empty;
                case "world.social.credibility_label":
                    return "official statement";
                case "world.social.credibility_value":
                    return "0.8";
                case "world.social.fact_lines":
                    return string.Empty;
                case "world.social.origin_type":
                    return "dialogue";
                case "world.social.source_label":
                    return context?.Faction?.Name ?? "Unknown";
                default:
                    // Validation layer (ValidateTemplateVariables) reports unknown variables.
                    // Silent fallback here to avoid per-variable log spam during rendering.
                    return string.Empty;
            }
        }

internal string ResolveDialoguePrimaryObjectiveVariableValue(DialogueScenarioContext context)
        {
            string unresolvedIntent = Owner.ResolveDialogueLatestUnresolvedIntentVariableValue(context);
            return host.NodeSupport.BuildPrimaryObjectiveFromIntent(unresolvedIntent);
        }
    }
}
