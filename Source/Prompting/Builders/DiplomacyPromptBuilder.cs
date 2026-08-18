using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Context;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Prompting;
using System.Text;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Communication.Relations.Memory;

namespace Ustas.RimAI.Communication.Relations.Prompting.Builders
{
    using ImageTemplatePromptHint = DiplomacyPromptBuilderContract.ImageTemplatePromptHint;
    /// <summary>
    /// Dependencies: PromptWorkspaceComposer and Diplomacy runtime context.
    /// Responsibility: orchestrate Diplomacy prompt composition without persistence or HTTP.
    /// </summary>
    internal sealed class DiplomacyPromptBuilder
    {
        internal DiplomacyPromptBuilderParts Parts;

        internal readonly PromptPersistenceService promptService;

        public DiplomacyPromptBuilder(PromptPersistenceService promptService)
        {
            Parts = new DiplomacyPromptBuilderParts(this);
            this.promptService = promptService;
        }

        public string Build(
            Faction faction,
            SystemPromptConfig config,
            bool isProactive,
            IEnumerable<string> additionalSceneTags,
            Pawn playerNegotiator = null,
            DiplomacyPromptRuntimeSnapshot runtimeSnapshot = null)
        {
            DialogueScenarioContext scenarioContext = DialogueScenarioContext.CreateDiplomacy(
                faction,
                isProactive,
                additionalSceneTags);
            string promptChannel = PromptRuntimeChannels.ResolveDiplomacy(isProactive);
            Dictionary<string, object> additionalValues = playerNegotiator == null
                ? null
                : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    [PromptRuntimeChannels.PlayerNegotiatorValueKey] = playerNegotiator
                };
            return promptService.WorkspaceComposer.BuildUnifiedChannelSystemPrompt(
                RimTalkPromptChannel.Diplomacy,
                promptChannel,
                scenarioContext,
                config?.EnvironmentPrompt,
                additionalValues,
                deterministicPreview: false,
                runtimeSnapshot: runtimeSnapshot);
        }

        #region Facade forwards
        internal void AppendSimpleConfig(StringBuilder sb, SystemPromptConfig config, Faction faction) => Parts.Contract.AppendSimpleConfig(sb, config, faction);
        internal void AppendAdvancedConfig(StringBuilder sb, SystemPromptConfig config, Faction faction) => Parts.Contract.AppendAdvancedConfig(sb, config, faction);
        internal void AppendCompactDiplomacyResponseContract(StringBuilder sb, SystemPromptConfig config, Faction faction) => Parts.Contract.AppendCompactDiplomacyResponseContract(sb, config, faction);
        internal void AppendDiplomacyResponseFormatSection(StringBuilder sb, SystemPromptConfig config) => Parts.Contract.AppendDiplomacyResponseFormatSection(sb, config);
        internal void AppendDiplomacyCriticalActionRules(StringBuilder sb) => Parts.Contract.AppendDiplomacyCriticalActionRules(sb);
        internal void AppendOutputSpecificationAuthoritySection(StringBuilder sb, SystemPromptConfig config) => Parts.Contract.AppendOutputSpecificationAuthoritySection(sb, config);
        internal void AppendStrictJsonFormatPreamble(StringBuilder sb) => Parts.Contract.AppendStrictJsonFormatPreamble(sb);
        internal void AppendOutputSpecificationAuthorityRules(StringBuilder sb) => Parts.Contract.AppendOutputSpecificationAuthorityRules(sb);
        internal void AppendOutputSpecificationAuthorityTemplate(StringBuilder sb, string jsonTemplate) => Parts.Contract.AppendOutputSpecificationAuthorityTemplate(sb, jsonTemplate);
        internal void AppendCompactActionCatalog(StringBuilder sb, List<ApiActionConfig> availableActions) => Parts.Contract.AppendCompactActionCatalog(sb, availableActions);
        internal string BuildCompactActionLine(ApiActionConfig action) => Parts.Contract.BuildCompactActionLine(action);
        internal string BuildCompactActionParameterHint(string actionName) => Parts.Contract.BuildCompactActionParameterHint(actionName);
        internal string BuildCompactActionRequirementHint(ApiActionConfig action) => Parts.Contract.BuildCompactActionRequirementHint(action);
        internal string BuildCompactActionDescriptionHint(ApiActionConfig action) => Parts.Contract.BuildCompactActionDescriptionHint(action);
        internal string NormalizeCompactActionText(string text, int maxChars) => Parts.Contract.NormalizeCompactActionText(text, maxChars);
        internal string MergeMakePeaceRequirement(string configured) => Parts.Contract.MergeMakePeaceRequirement(configured);
        internal bool ContainsSincerityConstraint(string text) => Parts.Contract.ContainsSincerityConstraint(text);
        internal void AppendStrategySuggestionGuidance(StringBuilder sb) => Parts.Contract.AppendStrategySuggestionGuidance(sb);
        internal void AppendSendImageTemplateGuidance(StringBuilder sb, List<ApiActionConfig> availableActions) => Parts.Contract.AppendSendImageTemplateGuidance(sb, availableActions);
        internal string ResolveSendImageCaptionStylePrompt() => Parts.Contract.ResolveSendImageCaptionStylePrompt();
        internal string ResolveCurrentGameLanguageLabel() => Parts.Contract.ResolveCurrentGameLanguageLabel();
        internal List<ImageTemplatePromptHint> GetEnabledImageTemplateHintsForPrompt() => Parts.Contract.GetEnabledImageTemplateHintsForPrompt();
        internal void AppendPresenceActionGuidance(StringBuilder sb, List<ApiActionConfig> availableActions) => Parts.Contract.AppendPresenceActionGuidance(sb, availableActions);
        internal List<ApiActionConfig> GetAvailableActionsForFaction(SystemPromptConfig config, Faction faction) => Parts.Contract.GetAvailableActionsForFaction(config, faction);
        internal bool ShouldKeepActionVisibleInPrompt(string actionName, ActionValidationResult eligibility) => Parts.Contract.ShouldKeepActionVisibleInPrompt(actionName, eligibility);
        internal bool IsPromptActionAllowedInCurrentBuild(string actionName) => Parts.Contract.IsPromptActionAllowedInCurrentBuild(actionName);
        internal void AppendBlockedActionHints(StringBuilder sb, SystemPromptConfig config, Faction faction) => Parts.Contract.AppendBlockedActionHints(sb, config, faction);
        internal bool ShouldHideBlockedActionHint(string actionName, ActionValidationResult eligibility) => Parts.Contract.ShouldHideBlockedActionHint(actionName, eligibility);
        internal void AppendGoodwillPeacePolicyHints(StringBuilder sb, Faction faction) => Parts.Contract.AppendGoodwillPeacePolicyHints(sb, faction);
        internal void AppendVeryLowGoodwillPeacePolicy(StringBuilder sb, int goodwill, int peaceTalkOnlyMin) => Parts.Contract.AppendVeryLowGoodwillPeacePolicy(sb, goodwill, peaceTalkOnlyMin);
        internal void AppendPeaceTalkOnlyPolicy(StringBuilder sb, int goodwill, int peaceTalkOnlyMin, int makePeaceReenabledMin, string peaceTalkQuest) => Parts.Contract.AppendPeaceTalkOnlyPolicy(sb, goodwill, peaceTalkOnlyMin, makePeaceReenabledMin, peaceTalkQuest);
        internal void AppendMakePeaceReenabledPolicy(StringBuilder sb, int goodwill, string peaceTalkQuest) => Parts.Contract.AppendMakePeaceReenabledPolicy(sb, goodwill, peaceTalkQuest);
        internal bool ShouldHideActionFromPromptByProjectedGoodwill(Faction faction, string actionName) => Parts.Contract.ShouldHideActionFromPromptByProjectedGoodwill(faction, actionName);
        internal string GetProjectedGoodwillBlockReason(Faction faction, string actionName) => Parts.Contract.GetProjectedGoodwillBlockReason(faction, actionName);
        internal string GetRelationLabel(int goodwill) => Parts.Contract.GetRelationLabel(goodwill);
        internal string GetEventIcon(SignificantEventType eventType) => Parts.Contract.GetEventIcon(eventType);
        internal string GetEventTypeName(SignificantEventType eventType) => Parts.Contract.GetEventTypeName(eventType);
        internal string GetRelationImpression(FactionMemoryEntry memory) => Parts.Contract.GetRelationImpression(memory);
        internal string GetRelationTrend(List<RelationSnapshot> history) => Parts.Contract.GetRelationTrend(history);
        internal void AppendApiLimits(StringBuilder sb, Faction faction = null) => Parts.Guidance.AppendApiLimits(sb, faction);
        internal void AppendAirdropTradeRules(StringBuilder sb, Faction faction) => Parts.Guidance.AppendAirdropTradeRules(sb, faction);
        internal void AppendFactionSpecialItemInventory(StringBuilder sb, Faction faction) => Parts.Guidance.AppendFactionSpecialItemInventory(sb, faction);
        internal Dictionary<string, object> BuildQuestPromptContext(DialogueScenarioContext context) => Parts.Guidance.BuildQuestPromptContext(context);
        internal void AppendDynamicQuestGuidance(StringBuilder sb, Faction faction, Dictionary<string, object> parameters = null) => Parts.Guidance.AppendDynamicQuestGuidance(sb, faction, parameters);
        internal void AppendQuestSelectionHardRules(StringBuilder sb) => Parts.Guidance.AppendQuestSelectionHardRules(sb);
        internal string GetQuestTemplateDescription(string questDefName) => Parts.Guidance.GetQuestTemplateDescription(questDefName);
        #endregion
    }

    internal sealed class DiplomacyPromptBuilderParts
    {
        internal readonly DiplomacyPromptBuilder Owner;
        internal readonly DiplomacyPromptBuilderContract Contract;
        internal readonly DiplomacyPromptBuilderGuidance Guidance;
        internal DiplomacyPromptBuilderParts(DiplomacyPromptBuilder owner)
        {
            Owner = owner;
            Contract = new DiplomacyPromptBuilderContract(owner);
            Guidance = new DiplomacyPromptBuilderGuidance(owner);
        }
    }
}
