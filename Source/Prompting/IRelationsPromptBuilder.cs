using System.Collections.Generic;
using RimWorld;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Context;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Prompting
{
    /// <summary>
    /// Relations-domain prompt composition: diplomacy, diplomacy strategy and RPG pawn dialogue.
    /// Configuration storage lives behind <see cref="Persistence.IPromptConfigStore"/>.
    /// </summary>
    public interface IRelationsPromptBuilder
    {
        string BuildFullSystemPrompt(
            Faction faction,
            SystemPromptConfig config,
            bool isProactive,
            IEnumerable<string> additionalSceneTags);

        string BuildFullSystemPrompt(
            Faction faction,
            SystemPromptConfig config,
            bool isProactive,
            IEnumerable<string> additionalSceneTags,
            DiplomacyPromptRuntimeSnapshot runtimeSnapshot);

        string BuildDiplomacyStrategySystemPrompt(
            Faction faction,
            SystemPromptConfig config,
            IEnumerable<string> additionalSceneTags,
            DiplomacyStrategyPromptContext strategyContext);

        string BuildRPGFullSystemPrompt(
            Pawn initiator,
            Pawn target,
            bool isProactive,
            IEnumerable<string> additionalSceneTags,
            bool allowMemoryCompressionScheduling = true,
            bool allowMemoryColdLoad = true);
    }
}
