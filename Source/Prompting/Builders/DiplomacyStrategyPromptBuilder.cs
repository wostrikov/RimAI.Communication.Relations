using System.Collections.Generic;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Persistence;
using RimWorld;
using Ustas.RimAI.Communication.Relations.Context;

namespace Ustas.RimAI.Communication.Relations.Prompting.Builders
{
    /// <summary>
    /// Dependencies: PromptPersistenceService diplomacy-strategy builder core.
    /// Responsibility: orchestrate the dedicated diplomacy-strategy system prompt entry.
    /// </summary>
    internal sealed class DiplomacyStrategyPromptBuilder
    {
        private readonly PromptPersistenceService promptService;

        public DiplomacyStrategyPromptBuilder(PromptPersistenceService promptService)
        {
            this.promptService = promptService;
        }

        public string Build(
            Faction faction,
            SystemPromptConfig config,
            IEnumerable<string> additionalSceneTags,
            DiplomacyStrategyPromptContext strategyContext)
        {
            DialogueScenarioContext scenarioContext = DialogueScenarioContext.CreateDiplomacy(
                faction,
                false,
                additionalSceneTags);
            Dictionary<string, object> runtimeValues = promptService.BuildStrategyRuntimeValuesOrThrow(strategyContext);
            return promptService.WorkspaceComposer.BuildUnifiedChannelSystemPrompt(
                RimTalkPromptChannel.Diplomacy,
                RimTalkPromptEntryChannelCatalog.DiplomacyStrategy,
                scenarioContext,
                config?.EnvironmentPrompt,
                runtimeValues,
                deterministicPreview: false);
        }
    }
}
