using System.Collections.Generic;
using Ustas.RimAI.Communication.Relations.Persistence;
using Verse;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Context;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Prompting;

namespace Ustas.RimAI.Communication.Relations.Prompting.Builders
{
    /// <summary>/// Dependencies: PromptPersistenceService hierarchical RPG builder core.
 /// Responsibility: orchestrate RPG prompt build entry without changing output behavior.
 ///</summary>
    internal sealed partial class RpgPromptBuilder
    {
        private readonly PromptPersistenceService promptService;

        public RpgPromptBuilder(PromptPersistenceService promptService)
        {
            this.promptService = promptService;
        }

        public string Build(
            Pawn initiator,
            Pawn target,
            bool isProactive,
            IEnumerable<string> additionalSceneTags,
            bool allowMemoryCompressionScheduling = true,
            bool allowMemoryColdLoad = true)
        {
            DialogueScenarioContext scenarioContext = DialogueScenarioContext.CreateRpg(
                initiator,
                target,
                isProactive,
                additionalSceneTags);
            string promptChannel = PromptRuntimeChannels.ResolveRpg(isProactive);
            SystemPromptConfig config = promptService.LoadConfig() ?? promptService.DomainStore.CreateDefaultConfig();
            return promptService.WorkspaceComposer.BuildUnifiedChannelSystemPrompt(
                RimTalkPromptChannel.Rpg,
                promptChannel,
                scenarioContext,
                config?.EnvironmentPrompt,
                null,
                deterministicPreview: false,
                allowMemoryCompressionScheduling: allowMemoryCompressionScheduling,
                allowMemoryColdLoad: allowMemoryColdLoad);
        }
    }
}
