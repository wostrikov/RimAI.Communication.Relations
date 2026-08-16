using System.Collections.Generic;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Persistence;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Prompting.Builders
{
    /// <summary>/// Dependencies: PromptPersistenceService hierarchical diplomacy builder core.
 /// Responsibility: orchestrate diplomacy prompt build entry without changing output behavior.
 ///</summary>
    internal sealed class DiplomacyPromptBuilder
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
            Pawn playerNegotiator = null)
        {
            return promptService.BuildFullSystemPromptHierarchicalCore(
                faction,
                config,
                isProactive,
                additionalSceneTags,
                playerNegotiator);
        }
    }
}
