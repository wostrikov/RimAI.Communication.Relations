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
    internal sealed class PromptNodeSlice6 : PromptNodeSupportCollaborator
    {
        internal PromptNodeSlice6(PromptNodeSupport owner) : base(owner)
        {
        }

internal Dictionary<string, object> CreatePromptVariableSeed()
        {
            var variables = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in PromptVariableCatalog.GetAll())
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                variables[path] = string.Empty;
            }

            return variables;
        }

internal bool IsPreviewScenario(DialogueScenarioContext context)
        {
            return context?.Tags != null &&
                   (context.Tags.Contains("mode:preview") || context.Tags.Contains("scene:preview"));
        }

internal Dictionary<string, object> CreatePreviewPawnPlaceholder(string name)
        {
            string safeName = string.IsNullOrWhiteSpace(name) ? "PreviewPawn" : name.Trim();
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["name"] = safeName,
                ["profile"] = "preview_profile",
                ["labelshort"] = safeName
            };
        }

internal Dictionary<string, object> CreatePreviewFactionPlaceholder(string name)
        {
            string safeName = string.IsNullOrWhiteSpace(name) ? "PreviewFaction" : name.Trim();
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["name"] = safeName,
                ["profile"] = "preview_faction_profile"
            };
        }

internal string RenderPromptNodeTemplate(
            SystemPromptConfig config,
            DialogueScenarioContext context,
            string template,
            string bodyVariableName,
            string bodyText)
        {
            string normalizedBody = bodyText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedBody))
            {
                throw new PromptRenderException(
                    "prompt_templates.node." + bodyVariableName,
                    Owner.ResolveRenderChannel(context),
                    new PromptRenderDiagnostic
                    {
                        ErrorCode = PromptRenderErrorCode.TemplateMissing,
                        Message = "Runtime node body is empty for required variable: " + Owner.ResolveNodeBodyVariablePath(bodyVariableName)
                    });
            }

            Dictionary<string, object> variables = Owner.BuildSharedPromptTemplateVariables(context, string.Empty);
            string namespacedVariable = Owner.ResolveNodeBodyVariablePath(bodyVariableName);
            variables[namespacedVariable] = normalizedBody;
            string channel = Owner.ResolveRenderChannel(context);
            string templateId = $"prompt_templates.node.{bodyVariableName}";
            string requiredTemplate = Owner.RequireTemplateText(templateId, channel, template);
            return Owner.ApplyPromptSourceTag(
                Owner.RenderTemplateOrThrow(
                    templateId,
                    channel,
                    requiredTemplate,
                    variables),
                true);
        }

internal string ResolveQuestGuidanceNodeText(
            DialogueScenarioContext context,
            string promptChannel,
            string questGuidanceBody)
        {
            string body = (questGuidanceBody ?? string.Empty).Trim();
            if (body.Length == 0)
            {
                throw new PromptRenderException(
                    "prompt_nodes.quest_guidance_node_template",
                    Owner.ResolveRenderChannel(context),
                    new PromptRenderDiagnostic
                    {
                        ErrorCode = PromptRenderErrorCode.TemplateMissing,
                        Message = "Quest guidance body is empty."
                    });
            }

            string template = Owner.ResolveUnifiedNodeTemplate(promptChannel, "quest_guidance_node_template", PromptTextConstants.QuestGuidanceNodeLiteralDefault);
            string resolved = Owner.ReplaceLegacyQuestGuidanceVariableToken(template, body).Trim();
            if (resolved.Length == 0)
            {
                return Owner.ApplyPromptSourceTag(body, true);
            }

            return Owner.ApplyPromptSourceTag(resolved, true);
        }

internal string ReplaceLegacyQuestGuidanceVariableToken(string template, string body)
        {
            string source = template ?? string.Empty;
            string replacement = body ?? string.Empty;
            return source
                .Replace("{{ dialogue.quest_guidance_body }}", replacement)
                .Replace("{{dialogue.quest_guidance_body}}", replacement)
                .Replace("{{  dialogue.quest_guidance_body  }}", replacement);
        }

internal string BuildDiplomacyStrategyDecisionPolicyText()
        {
            const string fallback = "Порядок пріоритетів рішення: 1) правильність формату й мови; 2) правильність полів-посилань; 3) фактичні обмеження; 4) безпечність дій і межі відносин; 5) звʼязність та стиль персонажа.";
            return Owner.ResolveUnifiedNodeTemplate(RimTalkPromptEntryChannelCatalog.DiplomacyStrategy, "decision_policy", fallback);
        }

internal string BuildDiplomacyStrategyTurnObjectiveText()
        {
            const string fallback = "Головна мета: {{dialogue.primary_objective}} Необовʼязкове доповнення: {{ dialogue.optional_followup }} Умови: спершу заверши головну мету; тему можна змінити щонайбільше раз.";
            return Owner.ResolveUnifiedNodeTemplate(RimTalkPromptEntryChannelCatalog.DiplomacyStrategy, "turn_objective", fallback);
        }

internal string BuildDiplomacyStrategyOutputContractText()
        {
            string fallback =
                "Return exactly one JSON object only.\n" +
                "The first character must be '{' and the last character must be '}'.\n" +
                "Do not output markdown fences, prose, notes, or any extra text.\n" +
                "Required format:\n" +
                "{\"strategy_suggestions\":[{\"strategy_name\":\"\",\"reason\":\"\",\"content\":\"\"},{\"strategy_name\":\"\",\"reason\":\"\",\"content\":\"\"},{\"strategy_name\":\"\",\"reason\":\"\",\"content\":\"\"}]}\n" +
                "Rules:\n" +
                "- Exactly 3 items.\n" +
                "- Output keys must be exactly: strategy_suggestions, strategy_name, reason, content.\n" +
                "- strategy_name <= 6 Chinese characters and must be actionable intent.\n" +
                "- reason must cite at least one fact tag like [F1] and explain causality.\n" +
                "- reason should stay compact for button display.\n" +
                "- content must be a complete sendable line the player can auto-send directly.\n" +
                "- Keep style aligned with the current faction voice and the player's language.\n" +
                "- At least 2 items must explicitly leverage player attributes or current context.\n" +
                "- Never output extra fields such as action, priority, risk_assessment, task, plan, or macro_advice.";
            return Owner.ResolveUnifiedNodeTemplate(
                RimTalkPromptEntryChannelCatalog.DiplomacyStrategy,
                "strategy_output_contract",
                fallback);
        }

internal string RenderStrategyNodeTemplate(
            string promptChannel,
            string nodeId,
            string bodyVariableName,
            string bodyText,
            DialogueScenarioContext context)
        {
            string normalizedBody = bodyText?.Trim() ?? string.Empty;
            if (normalizedBody.Length == 0)
            {
                return string.Empty;
            }

            string channel = Owner.ResolveRenderChannel(context);
            string template = Owner.ResolveUnifiedNodeTemplate(promptChannel, nodeId, "{{ " + bodyVariableName + " }}");
            Dictionary<string, object> variables = Owner.BuildSharedPromptTemplateVariables(context, string.Empty);
            variables[bodyVariableName] = normalizedBody;
            return Owner.ApplyPromptSourceTag(
                Owner.RenderTemplateOrThrow(
                    "prompt_nodes." + nodeId,
                    channel,
                    Owner.RequireTemplateText("prompt_nodes." + nodeId, channel, template),
                    variables),
                true);
        }

internal string ResolveUnifiedNodeTemplate(string promptChannel, string nodeId, string fallback)
        {
            string fromCatalog = RelationsMod.Settings?.ResolvePromptNodeText(promptChannel, nodeId);
            if (!string.IsNullOrWhiteSpace(fromCatalog))
            {
                return fromCatalog.Trim();
            }

            return fallback?.Trim() ?? string.Empty;
        }

internal string ResolvePromptChannelForContext(DialogueScenarioContext context)
        {
            if (context?.IsRpg == true)
            {
                return context.IsProactive
                    ? RimTalkPromptEntryChannelCatalog.ProactiveRpgDialogue
                    : RimTalkPromptEntryChannelCatalog.RpgDialogue;
            }

            return context?.IsProactive == true
                ? RimTalkPromptEntryChannelCatalog.ProactiveDiplomacyDialogue
                : RimTalkPromptEntryChannelCatalog.DiplomacyDialogue;
        }

internal string ResolveNodeBodyVariablePath(string bodyVariableName)
        {
            if (string.IsNullOrWhiteSpace(bodyVariableName))
            {
                return "dialogue.body";
            }

            switch (bodyVariableName.Trim().ToLowerInvariant())
            {
                case "api_limits_body":
                    return "dialogue.api_limits_body";
                case "quest_guidance_body":
                    return "dialogue.quest_guidance_body";
                case "response_contract_body":
                    return "dialogue.response_contract_body";
                default:
                    return "dialogue." + bodyVariableName.Trim().ToLowerInvariant();
            }
        }
    }
}
