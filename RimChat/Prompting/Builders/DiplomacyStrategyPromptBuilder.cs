using System.Collections.Generic;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Persistence;
using RimWorld;

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
            return promptService.BuildDiplomacyStrategySystemPromptCore(
                faction,
                config,
                additionalSceneTags,
                strategyContext);
        }
    }
}
