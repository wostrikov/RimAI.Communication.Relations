using System;
using System.Collections.Generic;
using Ustas.RimAI.Communication.Relations.Context;

namespace Ustas.RimAI.Communication.Relations.Prompting.Builders
{
    /// <summary>
    /// Dependencies: DiplomacyStrategyPromptContext and PromptRenderException.
    /// Responsibility: validate strategy runtime blocks and project them into template values.
    /// </summary>
    internal static class DiplomacyStrategyRuntimeValues
    {
        public const string TemplateId = "prompt_nodes.diplomacy_strategy.runtime_context";
        public const string NegotiatorKey = "dialogue.strategy_player_negotiator_context_body";
        public const string FactPackKey = "dialogue.strategy_fact_pack_body";
        public const string DossierKey = "dialogue.strategy_scenario_dossier_body";

        public static Dictionary<string, object> BuildOrThrow(DiplomacyStrategyPromptContext strategyContext)
        {
            string negotiator = strategyContext?.NegotiatorContextText?.Trim() ?? string.Empty;
            string factPack = strategyContext?.StrategyFactPackText?.Trim() ?? string.Empty;
            string dossier = strategyContext?.ScenarioDossierText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(negotiator) ||
                string.IsNullOrWhiteSpace(factPack) ||
                string.IsNullOrWhiteSpace(dossier))
            {
                throw new PromptRenderException(
                    TemplateId,
                    PromptRuntimeChannels.DiplomacyStrategy,
                    new PromptRenderDiagnostic
                    {
                        ErrorCode = PromptRenderErrorCode.TemplateMissing,
                        Message = "Strategy runtime context is incomplete. Required: negotiator_context, fact_pack, scenario_dossier."
                    });
            }

            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                [NegotiatorKey] = negotiator,
                [FactPackKey] = factPack,
                [DossierKey] = dossier
            };
        }
    }
}
