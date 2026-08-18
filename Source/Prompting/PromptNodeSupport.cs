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
    /// <summary>/// Dependencies: SystemPromptConfig, DialogueScenarioContext, PromptHierarchyRenderer.
 /// Responsibility: build diplomacy/RPG prompts with strict Scriban rendering and hierarchical policy pipeline.
 ///</summary>
internal sealed class PromptNodeSupport
    {
        internal PromptNodeSupportParts Parts;

        internal readonly PromptPersistenceService host;

        internal PromptNodeSupport(PromptPersistenceService host)
        {
            Parts = new PromptNodeSupportParts(this);
            this.host = host ?? throw new System.ArgumentNullException(nameof(host));
        }

        internal const string DefaultDiplomacyFallbackRoleTemplate =
            "You are the leader of {{ world.faction.name }} in RimWorld.";

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        internal string ApplyPromptSourceTag(string text, bool fromFile)
        {
            return text?.Trim() ?? string.Empty;
        }

        

        

        

        

        internal string ResolveMandatoryRaceName(Pawn pawn)
        {
            return pawn?.LabelShortCap ?? "N/A";
        }

        

        internal string ResolveMandatoryRaceDef(Pawn pawn)
        {
            return pawn?.def?.defName ?? "N/A";
        }

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        internal string BuildPrimaryObjectiveFromIntent(string unresolvedIntent)
        {
            return string.Empty;
        }

        

        internal PromptPolicyConfig ResolvePromptPolicyConfig(SystemPromptConfig config)
        {
            return config?.PromptPolicy?.Clone() ?? PromptPolicyConfig.CreateDefault();
        }

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        internal bool HasAnyBloodRelationBetweenPair(Pawn first, Pawn second)
        {
            return HasAnyBloodRelationOneWay(first, second) || HasAnyBloodRelationOneWay(second, first);
        }

        

        

        

        

        

        

        

        internal string ResolveRpgFormatConstraintHeader(RelationsSettings settings)
        {
            return "=== FORMAT CONSTRAINT (REQUIRED) ===";
        }

        

        

        

        internal string ResolveRpgActionReliabilityMarker(RelationsSettings settings)
        {
            return "Reliability rules:";
        }

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        internal string ResolveRenderChannel(DialogueScenarioContext context)
        {
            return context?.IsRpg == true ? "rpg" : "diplomacy";
        }

        

        

        

        

        

        

        
    
        #region Cluster forwards
        internal string BuildFullSystemPromptHierarchicalCore(Faction faction, SystemPromptConfig config, bool isProactive, IEnumerable<string> additionalSceneTags) => Parts.Slice1.BuildFullSystemPromptHierarchicalCore(faction, config, isProactive, additionalSceneTags);
        internal string BuildFullSystemPromptHierarchicalCore(Faction faction, SystemPromptConfig config, bool isProactive, IEnumerable<string> additionalSceneTags, Pawn playerNegotiator) => Parts.Slice1.BuildFullSystemPromptHierarchicalCore(faction, config, isProactive, additionalSceneTags, playerNegotiator);
        internal string BuildRpgSystemPromptHierarchicalCore(Pawn initiator, Pawn target, bool isProactive, IEnumerable<string> additionalSceneTags) => Parts.Slice1.BuildRpgSystemPromptHierarchicalCore(initiator, target, isProactive, additionalSceneTags);
        internal string BuildDiplomacyStrategySystemPromptCore(Faction faction, SystemPromptConfig config, IEnumerable<string> additionalSceneTags, DiplomacyStrategyPromptContext strategyContext) => Parts.Slice1.BuildDiplomacyStrategySystemPromptCore(faction, config, additionalSceneTags, strategyContext);
        internal string BuildFullSystemPromptHierarchical(Faction faction, SystemPromptConfig config, bool isProactive, IEnumerable<string> additionalSceneTags, Pawn playerNegotiator) => Parts.Slice1.BuildFullSystemPromptHierarchical(faction, config, isProactive, additionalSceneTags, playerNegotiator);
        internal string BuildDiplomacyStrategySystemPromptHierarchical(Faction faction, SystemPromptConfig config, IEnumerable<string> additionalSceneTags, DiplomacyStrategyPromptContext strategyContext) => Parts.Slice1.BuildDiplomacyStrategySystemPromptHierarchical(faction, config, additionalSceneTags, strategyContext);
        internal string BuildRpgSystemPromptHierarchical(Pawn initiator, Pawn target, bool isProactive, IEnumerable<string> additionalSceneTags) => Parts.Slice1.BuildRpgSystemPromptHierarchical(initiator, target, isProactive, additionalSceneTags);
        internal PromptHierarchyNode BuildDiplomacyDynamicDataNode(SystemPromptConfig config, Faction faction, Pawn playerNegotiator) => Parts.Slice1.BuildDiplomacyDynamicDataNode(config, faction, playerNegotiator);
        internal PromptHierarchyNode BuildRpgActorStateNode(RelationsSettings settings, SystemPromptConfig config, Pawn initiator, Pawn target, bool preferCompactContext) => Parts.Slice1.BuildRpgActorStateNode(settings, config, initiator, target, preferCompactContext);
        internal void ApplyResolvedNodePlacements(PromptHierarchyNode root, IEnumerable<ResolvedPromptNodePlacement> placements, PromptUnifiedNodeSlot slot) => Parts.Slice1.ApplyResolvedNodePlacements(root, placements, slot);
        internal List<PromptUnifiedNodeLayoutConfig> GetOrderedNodeLayouts(string promptChannel) => Parts.Slice1.GetOrderedNodeLayouts(promptChannel);
        internal List<ResolvedPromptNodePlacement> ResolveDiplomacyNodePlacements(string promptChannel, SystemPromptConfig config, DialogueScenarioContext context, Faction faction, Pawn playerNegotiator) => Parts.Slice2.ResolveDiplomacyNodePlacements(promptChannel, config, context, faction, playerNegotiator);
        internal List<ResolvedPromptNodePlacement> ResolveRpgNodePlacements(string promptChannel, RelationsSettings settings, SystemPromptConfig config, DialogueScenarioContext context, Pawn initiator, Pawn target, string unresolvedIntent, bool includeOpeningObjective) => Parts.Slice2.ResolveRpgNodePlacements(promptChannel, settings, config, context, initiator, target, unresolvedIntent, includeOpeningObjective);
        internal List<ResolvedPromptNodePlacement> ResolveStrategyNodePlacements(string promptChannel, SystemPromptConfig config, DialogueScenarioContext context, DiplomacyStrategyPromptContext strategyContext) => Parts.Slice2.ResolveStrategyNodePlacements(promptChannel, config, context, strategyContext);
        internal string BuildRpgKinshipBoundaryGuidanceText(RelationsSettings settings, Pawn initiator, Pawn target, DialogueScenarioContext context) => Parts.Slice2.BuildRpgKinshipBoundaryGuidanceText(settings, initiator, target, context);
        internal void AddTextNodeIfNotEmpty(PromptHierarchyNode parent, string id, string text, bool fromFile = false) => Parts.Slice2.AddTextNodeIfNotEmpty(parent, id, text, fromFile);
        internal void AddNodeIfAnyChildren(PromptHierarchyNode parent, PromptHierarchyNode child) => Parts.Slice2.AddNodeIfAnyChildren(parent, child);
        internal string BuildTextBlock(Action<StringBuilder> appendAction) => Parts.Slice3.BuildTextBlock(appendAction);
        internal string BuildMandatoryRaceProfileBlock(SystemPromptConfig config, DialogueScenarioContext context) => Parts.Slice3.BuildMandatoryRaceProfileBlock(config, context);
        internal string BuildMandatoryRaceProfileBody(DialogueScenarioContext context) => Parts.Slice3.BuildMandatoryRaceProfileBody(context);
        internal void AppendMandatoryRaceEntry(StringBuilder sb, string roleKey, Pawn pawn) => Parts.Slice3.AppendMandatoryRaceEntry(sb, roleKey, pawn);
        internal string ResolveMandatoryRaceKind(Pawn pawn) => Parts.Slice3.ResolveMandatoryRaceKind(pawn);
        internal string ResolveMandatoryRaceLabel(Pawn pawn) => Parts.Slice3.ResolveMandatoryRaceLabel(pawn);
        internal string ResolveMandatoryRaceXenotype(Pawn pawn) => Parts.Slice3.ResolveMandatoryRaceXenotype(pawn);
        internal string ResolveMandatoryRaceDescription(Pawn pawn) => Parts.Slice3.ResolveMandatoryRaceDescription(pawn);
        internal string NormalizeMandatoryRaceText(string text, string fallback, int maxChars) => Parts.Slice3.NormalizeMandatoryRaceText(text, fallback, maxChars);
        internal string ReadMemberAsString(object target, string memberName) => Parts.Slice3.ReadMemberAsString(target, memberName);
        internal string TryReadMemberAsStringNoThrow(object target, string memberName, ref bool reflectionFaulted) => Parts.Slice3.TryReadMemberAsStringNoThrow(target, memberName, ref reflectionFaulted);
        internal object ReadMemberValue(object target, string memberName) => Parts.Slice3.ReadMemberValue(target, memberName);
        internal object TryReadMemberValueNoThrow(object target, string memberName, ref bool reflectionFaulted) => Parts.Slice3.TryReadMemberValueNoThrow(target, memberName, ref reflectionFaulted);
        internal string RenderTemplateOrThrow(string templateId, string channel, string templateText, IReadOnlyDictionary<string, object> variables) => Parts.Slice3.RenderTemplateOrThrow(templateId, channel, templateText, variables);
        internal string RequireTemplateText(string templateId, string channel, string templateText) => Parts.Slice3.RequireTemplateText(templateId, channel, templateText);
        internal string BuildDecisionPolicyText(SystemPromptConfig config, DialogueScenarioContext context) => Parts.Slice3.BuildDecisionPolicyText(config, context);
        internal string BuildTurnObjectiveText(SystemPromptConfig config, DialogueScenarioContext context, string primaryObjective, string optionalFollowup) => Parts.Slice3.BuildTurnObjectiveText(config, context, primaryObjective, optionalFollowup);
        internal string BuildOpeningObjectiveText(SystemPromptConfig config, DialogueScenarioContext context, string unresolvedIntent) => Parts.Slice3.BuildOpeningObjectiveText(config, context, unresolvedIntent);
        internal string BuildTopicShiftRuleText(SystemPromptConfig config, DialogueScenarioContext context) => Parts.Slice3.BuildTopicShiftRuleText(config, context);
        internal bool IsOpeningTurnContext(DialogueScenarioContext context) => Parts.Slice3.IsOpeningTurnContext(context);
        internal Dictionary<string, object> BuildPolicyTemplateVariables(DialogueScenarioContext context, string primaryObjective, string optionalFollowup, string unresolvedIntent) => Parts.Slice3.BuildPolicyTemplateVariables(context, primaryObjective, optionalFollowup, unresolvedIntent);
        internal string BuildFactGroundingGuidanceText(SystemPromptConfig config, DialogueScenarioContext context) => Parts.Slice3.BuildFactGroundingGuidanceText(config, context);
        internal string ResolveFactionPromptText(Faction faction, SystemPromptConfig config, DialogueScenarioContext context) => Parts.Slice4.ResolveFactionPromptText(faction, config, context);
        internal string AppendFixedFactionIntelBlock(string baseText, Faction faction, string promptChannel) => Parts.Slice4.AppendFixedFactionIntelBlock(baseText, faction, promptChannel);
        internal string TryAppendFactionToneVariables(string baseText) => Parts.Slice4.TryAppendFactionToneVariables(baseText);
        internal string NormalizeFactionPromptTemplateAliases(string template) => Parts.Slice4.NormalizeFactionPromptTemplateAliases(template);
        internal void PopulateFactionSettlementTemplateVariables(Dictionary<string, object> variables, Faction faction) => Parts.Slice4.PopulateFactionSettlementTemplateVariables(variables, faction);
        internal string ExtractSummaryLineValue(string summary, string key) => Parts.Slice4.ExtractSummaryLineValue(summary, key);
        internal string BuildSocialCircleActionRuleText(SystemPromptConfig config, DialogueScenarioContext context) => Parts.Slice4.BuildSocialCircleActionRuleText(config, context);
        internal string BuildRpgRoleSettingText(RelationsSettings settings, SystemPromptConfig config, DialogueScenarioContext context, Pawn target) => Parts.Slice4.BuildRpgRoleSettingText(settings, config, context, target);
        internal string AppendRpgIdentityGuidance(string baseText, DialogueScenarioContext context, Pawn target) => Parts.Slice4.AppendRpgIdentityGuidance(baseText, context, target);
        internal string BuildRpgIdentityGuidance(DialogueScenarioContext context, Pawn target) => Parts.Slice4.BuildRpgIdentityGuidance(context, target);
        internal string ResolveRpgPawnIdentityRole(Pawn pawn) => Parts.Slice4.ResolveRpgPawnIdentityRole(pawn);
        internal string ResolveRpgPawnSocialStatus(Pawn pawn) => Parts.Slice4.ResolveRpgPawnSocialStatus(pawn);
        internal string ResolveRpgPawnFactionStatus(Pawn pawn) => Parts.Slice4.ResolveRpgPawnFactionStatus(pawn);
        internal string ResolveRpgAttitudeGuidance(DialogueScenarioContext context, Pawn target) => Parts.Slice4.ResolveRpgAttitudeGuidance(context, target);
        internal string BuildRpgRelationshipProfileText(RelationsSettings settings, Pawn initiator, Pawn target, DialogueScenarioContext context) => Parts.Slice5.BuildRpgRelationshipProfileText(settings, initiator, target, context);
        internal bool HasAnyBloodRelationOneWay(Pawn fromPawn, Pawn toPawn) => Parts.Slice5.HasAnyBloodRelationOneWay(fromPawn, toPawn);
        internal string ResolvePairRomanceState(Pawn first, Pawn second) => Parts.Slice5.ResolvePairRomanceState(first, second);
        internal bool HasPairRelationEitherDirection(Pawn first, Pawn second, PawnRelationDef relationDef) => Parts.Slice5.HasPairRelationEitherDirection(first, second, relationDef);
        internal string BuildRpgApiContractText(RelationsSettings settings, SystemPromptConfig config, DialogueScenarioContext context, bool preferCompact) => Parts.Slice5.BuildRpgApiContractText(settings, config, context, preferCompact);
        internal string BuildRpgFormatConstraintText(RelationsSettings settings, SystemPromptConfig config, DialogueScenarioContext context, bool preferCompact) => Parts.Slice5.BuildRpgFormatConstraintText(settings, config, context, preferCompact);
        internal string AppendRpgActionReliabilityConstraint(string baseConstraint, RelationsSettings settings, SystemPromptConfig config, DialogueScenarioContext context) => Parts.Slice5.AppendRpgActionReliabilityConstraint(baseConstraint, settings, config, context);
        internal string ResolveRpgRoleFallbackTemplate(RelationsSettings settings) => Parts.Slice5.ResolveRpgRoleFallbackTemplate(settings);
        internal string ResolveRpgCompactFormatFallback() => Parts.Slice5.ResolveRpgCompactFormatFallback();
        internal string ResolveRpgFullFormatFallback() => Parts.Slice5.ResolveRpgFullFormatFallback();
        internal string ResolveRpgActionReliabilityFallback(RelationsSettings settings) => Parts.Slice5.ResolveRpgActionReliabilityFallback(settings);
        internal string ResolveRpgOutputSpecificationReference(DialogueScenarioContext context) => Parts.Slice5.ResolveRpgOutputSpecificationReference(context);
        internal string ResolveRpgRelationshipProfileTemplate(RelationsSettings settings) => Parts.Slice5.ResolveRpgRelationshipProfileTemplate(settings);
        internal string ResolveRpgKinshipBoundaryRuleTemplate(RelationsSettings settings) => Parts.Slice5.ResolveRpgKinshipBoundaryRuleTemplate(settings);
        internal string ResolveRpgProactiveRomanceRuleTemplate(RelationsSettings settings) => Parts.Slice5.ResolveRpgProactiveRomanceRuleTemplate(settings);
        internal string ResolveRpgProactiveSocialActionRuleTemplate(RelationsSettings settings) => Parts.Slice5.ResolveRpgProactiveSocialActionRuleTemplate(settings);
        internal string CompactRpgEnvironmentBlock(string environmentBlock) => Parts.Slice5.CompactRpgEnvironmentBlock(environmentBlock);
        internal string BuildOutputLanguageGuidance(RelationsSettings settings, SystemPromptConfig config, DialogueScenarioContext context) => Parts.Slice5.BuildOutputLanguageGuidance(settings, config, context);
        internal Dictionary<string, object> BuildSharedPromptTemplateVariables(DialogueScenarioContext context, string targetLanguage) => Parts.Slice5.BuildSharedPromptTemplateVariables(context, targetLanguage);
        internal Dictionary<string, object> CreatePromptVariableSeed() => Parts.Slice6.CreatePromptVariableSeed();
        internal bool IsPreviewScenario(DialogueScenarioContext context) => Parts.Slice6.IsPreviewScenario(context);
        internal Dictionary<string, object> CreatePreviewPawnPlaceholder(string name) => Parts.Slice6.CreatePreviewPawnPlaceholder(name);
        internal Dictionary<string, object> CreatePreviewFactionPlaceholder(string name) => Parts.Slice6.CreatePreviewFactionPlaceholder(name);
        internal string RenderPromptNodeTemplate(SystemPromptConfig config, DialogueScenarioContext context, string template, string bodyVariableName, string bodyText) => Parts.Slice6.RenderPromptNodeTemplate(config, context, template, bodyVariableName, bodyText);
        internal string ResolveQuestGuidanceNodeText(DialogueScenarioContext context, string promptChannel, string questGuidanceBody) => Parts.Slice6.ResolveQuestGuidanceNodeText(context, promptChannel, questGuidanceBody);
        internal string ReplaceLegacyQuestGuidanceVariableToken(string template, string body) => Parts.Slice6.ReplaceLegacyQuestGuidanceVariableToken(template, body);
        internal string BuildDiplomacyStrategyDecisionPolicyText() => Parts.Slice6.BuildDiplomacyStrategyDecisionPolicyText();
        internal string BuildDiplomacyStrategyTurnObjectiveText() => Parts.Slice6.BuildDiplomacyStrategyTurnObjectiveText();
        internal string BuildDiplomacyStrategyOutputContractText() => Parts.Slice6.BuildDiplomacyStrategyOutputContractText();
        internal string RenderStrategyNodeTemplate(string promptChannel, string nodeId, string bodyVariableName, string bodyText, DialogueScenarioContext context) => Parts.Slice6.RenderStrategyNodeTemplate(promptChannel, nodeId, bodyVariableName, bodyText, context);
        internal string ResolveUnifiedNodeTemplate(string promptChannel, string nodeId, string fallback) => Parts.Slice6.ResolveUnifiedNodeTemplate(promptChannel, nodeId, fallback);
        internal string ResolvePromptChannelForContext(DialogueScenarioContext context) => Parts.Slice6.ResolvePromptChannelForContext(context);
        internal string ResolveNodeBodyVariablePath(string bodyVariableName) => Parts.Slice6.ResolveNodeBodyVariablePath(bodyVariableName);
        #endregion
}
    internal sealed class PromptNodeSlice3 : PromptNodeSupportCollaborator
    {
        internal PromptNodeSlice3(PromptNodeSupport owner) : base(owner)
        {
        }

internal string BuildTextBlock(Action<StringBuilder> appendAction)
        {
            if (appendAction == null)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            appendAction(sb);
            return sb.ToString().Trim();
        }

internal string BuildMandatoryRaceProfileBlock(SystemPromptConfig config, DialogueScenarioContext context)
        {
            string channel = Owner.ResolveRenderChannel(context);
            string template = config?.PromptTemplates?.MandatoryRaceInjectionTemplate ?? string.Empty;
            string requiredTemplate = Owner.RequireTemplateText("prompt_templates.mandatory_race_injection", channel, template);
            Dictionary<string, object> variables = Owner.BuildSharedPromptTemplateVariables(context, string.Empty);
            variables["dialogue.mandatory_race_profile_body"] = Owner.BuildMandatoryRaceProfileBody(context);
            return Owner.ApplyPromptSourceTag(
                Owner.RenderTemplateOrThrow(
                    "prompt_templates.mandatory_race_injection",
                    channel,
                    requiredTemplate,
                    variables),
                true);
        }

internal string BuildMandatoryRaceProfileBody(DialogueScenarioContext context)
        {
            var sb = new StringBuilder();
            if (context?.IsRpg == true)
            {
                Owner.AppendMandatoryRaceEntry(sb, "RimChat_MandatoryRaceRole_Target", context.Target);
                Owner.AppendMandatoryRaceEntry(sb, "RimChat_MandatoryRaceRole_Initiator", context.Initiator);
            }
            else
            {
                Faction faction = context?.Faction;
                Pawn leader = faction?.leader;
                Pawn negotiator = host.ContextAssembler.ResolveBestPlayerNegotiator(context?.Initiator);
                Owner.AppendMandatoryRaceEntry(sb, "RimChat_MandatoryRaceRole_Leader", leader);
                Owner.AppendMandatoryRaceEntry(sb, "RimChat_MandatoryRaceRole_Negotiator", negotiator);
            }

            return sb.ToString().Trim();
        }

internal void AppendMandatoryRaceEntry(StringBuilder sb, string roleKey, Pawn pawn)
        {
            if (sb == null)
            {
                return;
            }

            if (sb.Length > 0)
            {
                sb.AppendLine();
            }

            sb.AppendLine($"Role: {roleKey.Translate()}");
            sb.AppendLine($"Name: {Owner.ResolveMandatoryRaceName(pawn)}");
            sb.AppendLine($"RaceKind: {Owner.ResolveMandatoryRaceKind(pawn)}");
            sb.AppendLine($"RaceDef: {Owner.ResolveMandatoryRaceDef(pawn)}");
            sb.AppendLine($"RaceLabel: {Owner.ResolveMandatoryRaceLabel(pawn)}");
            sb.AppendLine($"Xenotype: {Owner.ResolveMandatoryRaceXenotype(pawn)}");
            sb.AppendLine($"RaceDescription: {Owner.ResolveMandatoryRaceDescription(pawn)}");
        }

internal string ResolveMandatoryRaceKind(Pawn pawn)
        {
            RaceProperties raceProps = pawn?.RaceProps;
            if (raceProps == null)
            {
                return "N/A";
            }

            if (raceProps.Humanlike)
            {
                return "Humanlike";
            }

            if (raceProps.Animal)
            {
                return "Animal";
            }

            if (raceProps.IsMechanoid)
            {
                return "Mechanoid";
            }

            return "Other";
        }

internal string ResolveMandatoryRaceLabel(Pawn pawn)
        {
            string label = pawn?.def?.label;
            if (string.IsNullOrWhiteSpace(label))
            {
                label = pawn?.def != null ? pawn.def.LabelCap.ToString() : null;
            }

            return Owner.NormalizeMandatoryRaceText(label, "N/A", 120);
        }

internal string ResolveMandatoryRaceXenotype(Pawn pawn)
        {
            object genesObj = pawn?.genes;
            if (genesObj == null)
            {
                return "N/A";
            }

            bool reflectionFaulted = false;
            object xenotypeObj = Owner.TryReadMemberValueNoThrow(genesObj, "Xenotype", ref reflectionFaulted)
                ?? Owner.TryReadMemberValueNoThrow(genesObj, "xenotype", ref reflectionFaulted);
            string xenotype = Owner.TryReadMemberAsStringNoThrow(xenotypeObj, "LabelCap", ref reflectionFaulted)
                ?? Owner.TryReadMemberAsStringNoThrow(xenotypeObj, "label", ref reflectionFaulted)
                ?? Owner.TryReadMemberAsStringNoThrow(xenotypeObj, "defName", ref reflectionFaulted);
            if (!string.IsNullOrWhiteSpace(xenotype))
            {
                return xenotype.Trim();
            }

            object xenotypeDefObj = Owner.TryReadMemberValueNoThrow(genesObj, "XenotypeDef", ref reflectionFaulted)
                ?? Owner.TryReadMemberValueNoThrow(genesObj, "xenotypeDef", ref reflectionFaulted);
            xenotype = Owner.TryReadMemberAsStringNoThrow(xenotypeDefObj, "LabelCap", ref reflectionFaulted)
                ?? Owner.TryReadMemberAsStringNoThrow(xenotypeDefObj, "label", ref reflectionFaulted)
                ?? Owner.TryReadMemberAsStringNoThrow(xenotypeDefObj, "defName", ref reflectionFaulted);
            if (!string.IsNullOrWhiteSpace(xenotype))
            {
                return xenotype.Trim();
            }

            if (reflectionFaulted)
            {
                Log.Warning(
                    $"[RimAI.Relations] Mandatory race xenotype fallback to N/A after reflection fault. " +
                    $"pawn={pawn?.ThingID ?? "null"}, name={pawn?.LabelShortCap ?? "null"}, faction={pawn?.Faction?.Name ?? "null"}");
            }

            return "N/A";
        }

internal string ResolveMandatoryRaceDescription(Pawn pawn)
        {
            string description = pawn?.def?.description;
            if (string.IsNullOrWhiteSpace(description))
            {
                description = pawn?.kindDef?.race?.description;
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                string labelFallback = Owner.ResolveMandatoryRaceLabel(pawn);
                if (!string.Equals(labelFallback, "N/A", StringComparison.OrdinalIgnoreCase))
                {
                    description = labelFallback;
                }
            }

            return Owner.NormalizeMandatoryRaceText(description, "N/A", 220);
        }

internal string NormalizeMandatoryRaceText(string text, string fallback, int maxChars)
        {
            string normalized = (text ?? string.Empty)
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();
            if (normalized.Length == 0)
            {
                return fallback;
            }

            if (maxChars > 0 && normalized.Length > maxChars)
            {
                return normalized.Substring(0, maxChars).TrimEnd() + "...";
            }

            return normalized;
        }

internal string ReadMemberAsString(object target, string memberName)
        {
            object value = Owner.ReadMemberValue(target, memberName);
            string text = value?.ToString();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

internal string TryReadMemberAsStringNoThrow(object target, string memberName, ref bool reflectionFaulted)
        {
            object value = Owner.TryReadMemberValueNoThrow(target, memberName, ref reflectionFaulted);
            string text = value?.ToString();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

internal object ReadMemberValue(object target, string memberName)
        {
            if (target == null || string.IsNullOrWhiteSpace(memberName))
            {
                return null;
            }

            Type type = target.GetType();
            var property = type.GetProperty(memberName);
            if (property != null)
            {
                return property.GetValue(target, null);
            }

            var field = type.GetField(memberName);
            return field?.GetValue(target);
        }

internal object TryReadMemberValueNoThrow(object target, string memberName, ref bool reflectionFaulted)
        {
            try
            {
                return Owner.ReadMemberValue(target, memberName);
            }
            catch (Exception ex)
            {
                reflectionFaulted = true;
                Log.Warning(
                    $"[RimAI.Relations] Reflection read failed for member '{memberName}' on '{target?.GetType().FullName ?? "null"}': {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

internal string RenderTemplateOrThrow(
            string templateId,
            string channel,
            string templateText,
            IReadOnlyDictionary<string, object> variables)
        {
            string requiredTemplate = Owner.RequireTemplateText(templateId, channel, templateText);
            PromptRenderContext renderContext = PromptRenderContext.Create(templateId, channel);
            renderContext.SetValues(variables);
            return PromptTemplateRenderer.RenderOrThrow(templateId, channel, requiredTemplate, renderContext);
        }

internal string RequireTemplateText(
            string templateId,
            string channel,
            string templateText)
        {
            if (!string.IsNullOrWhiteSpace(templateText))
            {
                return templateText;
            }

            throw new PromptRenderException(
                templateId,
                channel,
                new PromptRenderDiagnostic
                {
                    ErrorCode = PromptRenderErrorCode.TemplateMissing,
                    Message = "Template text is required in strict Scriban mode."
                });
        }

internal string BuildDecisionPolicyText(SystemPromptConfig config, DialogueScenarioContext context)
        {
            bool isRpg = context?.IsRpg == true;
            string legacyTemplate = isRpg
                ? PromptUnifiedCatalog.CreateFallback().ResolveNode(Owner.ResolvePromptChannelForContext(context), "decision_policy")
                : config?.PromptTemplates?.DecisionPolicyTemplate;
            string channel = Owner.ResolveRenderChannel(context);
            string promptChannel = Owner.ResolvePromptChannelForContext(context);
            string template = Owner.ResolveUnifiedNodeTemplate(promptChannel, "decision_policy", legacyTemplate);
            string requiredTemplate = Owner.RequireTemplateText("prompt_templates.decision_policy", channel, template);

            return Owner.ApplyPromptSourceTag(
                Owner.RenderTemplateOrThrow(
                    "prompt_templates.decision_policy",
                    channel,
                    requiredTemplate,
                    BuildPolicyTemplateVariables(context, string.Empty, string.Empty, string.Empty)),
                true);
        }

internal string BuildTurnObjectiveText(
            SystemPromptConfig config,
            DialogueScenarioContext context,
            string primaryObjective,
            string optionalFollowup)
        {
            string primary = primaryObjective?.Trim() ?? string.Empty;
            string followup = optionalFollowup?.Trim() ?? string.Empty;
            bool isRpg = context?.IsRpg == true;
            string legacyTemplate = isRpg
                ? PromptUnifiedCatalog.CreateFallback().ResolveNode(Owner.ResolvePromptChannelForContext(context), "turn_objective")
                : config?.PromptTemplates?.TurnObjectiveTemplate;
            string channel = Owner.ResolveRenderChannel(context);
            string promptChannel = Owner.ResolvePromptChannelForContext(context);
            string template = Owner.ResolveUnifiedNodeTemplate(promptChannel, "turn_objective", legacyTemplate);
            string requiredTemplate = Owner.RequireTemplateText("prompt_templates.turn_objective", channel, template);

            return Owner.ApplyPromptSourceTag(
                Owner.RenderTemplateOrThrow(
                    "prompt_templates.turn_objective",
                    channel,
                    requiredTemplate,
                    BuildPolicyTemplateVariables(context, primary, followup, string.Empty)),
                true);
        }

internal string BuildOpeningObjectiveText(
            SystemPromptConfig config,
            DialogueScenarioContext context,
            string unresolvedIntent)
        {
            string legacyTemplate = PromptUnifiedCatalog.CreateFallback().ResolveNode(
                Owner.ResolvePromptChannelForContext(context),
                "opening_objective");
            string channel = Owner.ResolveRenderChannel(context);
            string promptChannel = Owner.ResolvePromptChannelForContext(context);
            string template = Owner.ResolveUnifiedNodeTemplate(promptChannel, "opening_objective", legacyTemplate);
            string requiredTemplate = Owner.RequireTemplateText("prompt_templates.opening_objective", channel, template);

            return Owner.ApplyPromptSourceTag(
                Owner.RenderTemplateOrThrow(
                    "prompt_templates.opening_objective",
                    channel,
                    requiredTemplate,
                    BuildPolicyTemplateVariables(context, string.Empty, string.Empty, string.Empty)),
                true);
        }

internal string BuildTopicShiftRuleText(SystemPromptConfig config, DialogueScenarioContext context)
        {
            bool isRpg = context?.IsRpg == true;
            string legacyTemplate = isRpg
                ? PromptUnifiedCatalog.CreateFallback().ResolveNode(Owner.ResolvePromptChannelForContext(context), "topic_shift_rule")
                : config?.PromptTemplates?.TopicShiftRuleTemplate;
            string channel = Owner.ResolveRenderChannel(context);
            string promptChannel = Owner.ResolvePromptChannelForContext(context);
            string template = Owner.ResolveUnifiedNodeTemplate(promptChannel, "topic_shift_rule", legacyTemplate);
            string requiredTemplate = Owner.RequireTemplateText("prompt_templates.topic_shift_rule", channel, template);

            return Owner.ApplyPromptSourceTag(
                Owner.RenderTemplateOrThrow(
                    "prompt_templates.topic_shift_rule",
                    channel,
                    requiredTemplate,
                    BuildPolicyTemplateVariables(context, string.Empty, string.Empty, string.Empty)),
                true);
        }

internal bool IsOpeningTurnContext(DialogueScenarioContext context)
        {
            if (context?.IsProactive == true)
            {
                return true;
            }

            if (context?.Tags == null || context.Tags.Count == 0)
            {
                return false;
            }

            return context.Tags.Contains("phase:opening")
                || context.Tags.Contains("turn:opening")
                || context.Tags.Contains("opening");
        }

internal Dictionary<string, object> BuildPolicyTemplateVariables(
            DialogueScenarioContext context,
            string primaryObjective,
            string optionalFollowup,
            string unresolvedIntent)
        {
            Dictionary<string, object> variables = Owner.BuildSharedPromptTemplateVariables(context, string.Empty);
            variables["dialogue.primary_objective"] = primaryObjective ?? string.Empty;
            variables["dialogue.optional_followup"] = optionalFollowup ?? string.Empty;
            variables["dialogue.latest_unresolved_intent"] = unresolvedIntent ?? string.Empty;
            variables["dialogue.topic_shift_rule"] = "Complete the primary objective first, then allow at most one natural topic extension.";
            return variables;
        }

internal string BuildFactGroundingGuidanceText(SystemPromptConfig config, DialogueScenarioContext context)
        {
            string legacyTemplate = config?.PromptTemplates?.FactGroundingTemplate;
            string channel = Owner.ResolveRenderChannel(context);
            string promptChannel = Owner.ResolvePromptChannelForContext(context);
            string template = Owner.ResolveUnifiedNodeTemplate(promptChannel, "fact_grounding", legacyTemplate);
            string requiredTemplate = Owner.RequireTemplateText("prompt_templates.fact_grounding", channel, template);

            return Owner.ApplyPromptSourceTag(
                Owner.RenderTemplateOrThrow(
                    "prompt_templates.fact_grounding",
                    channel,
                    requiredTemplate,
                    Owner.BuildSharedPromptTemplateVariables(context, string.Empty)),
                true);
        }
    }

    internal sealed class PromptNodeSupportParts
    {
        internal readonly PromptNodeSupport Owner;
        internal readonly PromptNodeSlice1 Slice1;
        internal readonly PromptNodeSlice2 Slice2;
        internal readonly PromptNodeSlice3 Slice3;
        internal readonly PromptNodeSlice4 Slice4;
        internal readonly PromptNodeSlice5 Slice5;
        internal readonly PromptNodeSlice6 Slice6;
        internal PromptNodeSupportParts(PromptNodeSupport owner)
        {
            Owner = owner;
            Slice1 = new PromptNodeSlice1(owner);
            Slice2 = new PromptNodeSlice2(owner);
            Slice3 = new PromptNodeSlice3(owner);
            Slice4 = new PromptNodeSlice4(owner);
            Slice5 = new PromptNodeSlice5(owner);
            Slice6 = new PromptNodeSlice6(owner);
        }
    }

}
