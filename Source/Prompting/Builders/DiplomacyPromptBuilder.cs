using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Context;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Prompting;

namespace Ustas.RimAI.Communication.Relations.Prompting.Builders
{
    /// <summary>
    /// Dependencies: PromptWorkspaceComposer and Diplomacy runtime context.
    /// Responsibility: orchestrate Diplomacy prompt composition without persistence or HTTP.
    /// </summary>
    internal sealed partial class DiplomacyPromptBuilder
    {
        private readonly PromptPersistenceService promptService;

        public DiplomacyPromptBuilder(PromptPersistenceService promptService)
        {
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
    }
}
