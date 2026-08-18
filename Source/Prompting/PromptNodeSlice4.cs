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
    internal sealed class PromptNodeSlice4 : PromptNodeSupportCollaborator
    {
        internal PromptNodeSlice4(PromptNodeSupport owner) : base(owner)
        {
        }

internal string ResolveFactionPromptText(
            Faction faction,
            SystemPromptConfig config,
            DialogueScenarioContext context)
        {
            string promptChannel = Owner.ResolvePromptChannelForContext(context);

            // Workbench priority: if user customized diplomacy_fallback_role, use it first.
            string workbenchNode = RelationsMod.Settings?.ResolvePromptNodeText(promptChannel, "diplomacy_fallback_role")?.Trim();
            if (!string.IsNullOrWhiteSpace(workbenchNode)
                && !string.Equals(workbenchNode, DefaultDiplomacyFallbackRoleTemplate, StringComparison.Ordinal))
            {
                string renderChannel = Owner.ResolveRenderChannel(context);
                Faction resolvedFaction = faction ?? context?.Faction;
                string factionName = resolvedFaction?.Name ?? "Unknown Faction";
                Dictionary<string, object> variables = Owner.BuildSharedPromptTemplateVariables(context, string.Empty);
                variables["world.faction.name"] = factionName;
                variables["world.faction"] = resolvedFaction != null
                    ? (object)resolvedFaction
                    : Owner.CreatePreviewFactionPlaceholder(factionName);
                string normalizedTemplate = Owner.NormalizeFactionPromptTemplateAliases(workbenchNode);
                string rendered = Owner.RenderTemplateOrThrow("prompt_templates.diplomacy_fallback_role", renderChannel, normalizedTemplate, variables);
                string enriched = Owner.TryAppendFactionToneVariables(rendered.Trim());
                return Owner.ApplyPromptSourceTag(Owner.AppendFixedFactionIntelBlock(enriched, resolvedFaction, promptChannel), true);
            }

            // Fallback: FactionPromptManager per-faction prompts.
            string factionPrompt = FactionPromptManager.Instance.GetPrompt(faction);
            if (!string.IsNullOrWhiteSpace(factionPrompt))
            {
                string trimmed = factionPrompt.Trim();
                if (trimmed.IndexOf("{{", StringComparison.Ordinal) < 0)
                {
                    string enrichedPrompt = Owner.TryAppendFactionToneVariables(trimmed);
                    return Owner.ApplyPromptSourceTag(Owner.AppendFixedFactionIntelBlock(enrichedPrompt, faction, promptChannel), true);
                }

                string renderChannel = Owner.ResolveRenderChannel(context);
                Dictionary<string, object> renderVariables = Owner.BuildSharedPromptTemplateVariables(context, string.Empty);
                Owner.PopulateFactionSettlementTemplateVariables(renderVariables, faction);
                string normalizedTemplate = Owner.NormalizeFactionPromptTemplateAliases(trimmed);
                string rendered = Owner.RenderTemplateOrThrow("faction_prompt.template", renderChannel, normalizedTemplate, renderVariables);
                string enrichedTemplatePrompt = Owner.TryAppendFactionToneVariables(rendered.Trim());
                return Owner.ApplyPromptSourceTag(Owner.AppendFixedFactionIntelBlock(enrichedTemplatePrompt, faction, promptChannel), true);
            }

            // Final fallback: default diplomacy_fallback_role template.
            string legacyTemplate = config?.PromptTemplates?.DiplomacyFallbackRoleTemplate;
            string channel = Owner.ResolveRenderChannel(context);
            string template = Owner.ResolveUnifiedNodeTemplate(promptChannel, "diplomacy_fallback_role", legacyTemplate);
            string requiredTemplate = Owner.RequireTemplateText("prompt_templates.diplomacy_fallback_role", channel, template);
            Faction finalFaction = faction ?? context?.Faction;
            string finalFactionName = finalFaction?.Name ?? "Unknown Faction";
            Dictionary<string, object> finalVariables = Owner.BuildSharedPromptTemplateVariables(context, string.Empty);
            finalVariables["world.faction.name"] = finalFactionName;
            finalVariables["world.faction"] = finalFaction != null
                ? (object)finalFaction
                : Owner.CreatePreviewFactionPlaceholder(finalFactionName);
            string fallbackText = Owner.RenderTemplateOrThrow(
                "prompt_templates.diplomacy_fallback_role",
                channel,
                requiredTemplate,
                finalVariables);
            string enrichedFallback = Owner.TryAppendFactionToneVariables(fallbackText.Trim());
            return Owner.ApplyPromptSourceTag(Owner.AppendFixedFactionIntelBlock(enrichedFallback, finalFaction, promptChannel), true);
        }

internal string AppendFixedFactionIntelBlock(string baseText, Faction faction, string promptChannel)
        {
            string fixedIntelBlock = DiplomacyFactionFixedIntelBuilder.Build(faction, promptChannel);
            if (string.IsNullOrWhiteSpace(fixedIntelBlock))
            {
                return baseText ?? string.Empty;
            }

            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(baseText))
            {
                sb.Append(baseText.TrimEnd());
                sb.AppendLine();
                sb.AppendLine();
            }

            sb.Append(fixedIntelBlock.Trim());
            return sb.ToString();
        }

internal string TryAppendFactionToneVariables(string baseText)
        {
            string current = baseText ?? string.Empty;
            string lower = current.ToLowerInvariant();
            bool hasTone = lower.Contains("system.custom.faction_tone") || lower.Contains("faction_tone");
            bool hasAttitude = lower.Contains("system.custom.faction_attitude_text") || lower.Contains("faction_attitude_text");

            if (hasTone && hasAttitude)
            {
                return current;
            }

            var sb = new StringBuilder(current.Length + 128);
            sb.Append(current.TrimEnd());
            if (!current.EndsWith("\n", StringComparison.Ordinal))
            {
                sb.AppendLine();
            }

            if (!hasTone)
            {
                sb.AppendLine("{{ system.custom.faction_tone }}");
            }

            if (!hasAttitude)
            {
                sb.AppendLine("{{ system.custom.faction_attitude_text }}");
            }

            return sb.ToString().TrimEnd();
        }

internal string NormalizeFactionPromptTemplateAliases(string template)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                return string.Empty;
            }

            string normalized = template;
            normalized = Regex.Replace(
                normalized,
                @"\{\{\s*SettlementCount\s*\}\}",
                "{{ world.faction_settlement.settlement_count }}",
                RegexOptions.IgnoreCase);
            normalized = Regex.Replace(
                normalized,
                @"\{\{\s*NearestToPlayerHome\s*\}\}",
                "{{ world.faction_settlement.nearest_to_player_home }}",
                RegexOptions.IgnoreCase);
            normalized = Regex.Replace(
                normalized,
                @"\{\{\s*AllSettlements\s*\}\}",
                "{{ world.faction_settlement.all_settlements }}",
                RegexOptions.IgnoreCase);
            return normalized;
        }

internal void PopulateFactionSettlementTemplateVariables(Dictionary<string, object> variables, Faction faction)
        {
            if (variables == null)
            {
                return;
            }

            string summary = host.ContextAssembler.BuildFactionSettlementSummaryForPrompt(faction);
            variables["world.faction_settlement_summary"] = summary ?? string.Empty;
            variables["world.faction_settlement.settlement_count"] = Owner.ExtractSummaryLineValue(summary, "SettlementCount");
            variables["world.faction_settlement.nearest_to_player_home"] = Owner.ExtractSummaryLineValue(summary, "NearestToPlayerHome");
            variables["world.faction_settlement.all_settlements"] = Owner.ExtractSummaryLineValue(summary, "AllSettlements");
        }

internal string ExtractSummaryLineValue(string summary, string key)
        {
            if (string.IsNullOrWhiteSpace(summary) || string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            string[] lines = summary.Replace("\r", string.Empty).Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i]?.Trim() ?? string.Empty;
                if (!line.StartsWith(key + ":", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return line.Substring(key.Length + 1).Trim();
            }

            return string.Empty;
        }

internal string BuildSocialCircleActionRuleText(SystemPromptConfig config, DialogueScenarioContext context)
        {
            if (RelationsMod.Settings?.EnableSocialCircle != true)
            {
                return string.Empty;
            }

            string legacyTemplate = config?.PromptTemplates?.SocialCircleActionRuleTemplate;
            string channel = Owner.ResolveRenderChannel(context);
            string promptChannel = Owner.ResolvePromptChannelForContext(context);
            string template = Owner.ResolveUnifiedNodeTemplate(promptChannel, "social_circle_action_rule", legacyTemplate);
            string requiredTemplate = Owner.RequireTemplateText("prompt_templates.social_circle_action_rule", channel, template);
            return Owner.ApplyPromptSourceTag(
                Owner.RenderTemplateOrThrow(
                    "prompt_templates.social_circle_action_rule",
                    channel,
                    requiredTemplate,
                    Owner.BuildSharedPromptTemplateVariables(context, string.Empty)),
                true);
        }

internal string BuildRpgRoleSettingText(
            RelationsSettings settings,
            SystemPromptConfig config,
            DialogueScenarioContext context,
            Pawn target)
        {
            string promptChannel = Owner.ResolvePromptChannelForContext(context);
            string personaSection = RelationsMod.Settings?.ResolvePromptSectionText(promptChannel, "character_persona");
            if (!string.IsNullOrWhiteSpace(personaSection))
            {
                return Owner.ApplyPromptSourceTag(Owner.AppendRpgIdentityGuidance(personaSection.Trim(), context, target), true);
            }

            Dictionary<string, object> variables = Owner.BuildSharedPromptTemplateVariables(context, string.Empty);
            variables["pawn.target.name"] = target?.LabelShort ?? "Unknown";
            variables["pawn.target"] = target;
            string channel = Owner.ResolveRenderChannel(context);
            string roleTemplate = Owner.ResolveUnifiedNodeTemplate(
                promptChannel,
                "rpg_role_setting_fallback",
                Owner.ResolveRpgRoleFallbackTemplate(settings));
            string requiredTemplate = Owner.RequireTemplateText("prompt_templates.rpg_role_setting_fallback", channel, roleTemplate);
            string roleText = Owner.RenderTemplateOrThrow(
                "prompt_templates.rpg_role_setting_fallback",
                channel,
                requiredTemplate,
                variables);
            return Owner.ApplyPromptSourceTag(Owner.AppendRpgIdentityGuidance(roleText, context, target), true);
        }

internal string AppendRpgIdentityGuidance(string baseText, DialogueScenarioContext context, Pawn target)
        {
            string identityGuidance = Owner.BuildRpgIdentityGuidance(context, target);
            if (string.IsNullOrWhiteSpace(identityGuidance))
            {
                return baseText;
            }

            if (string.IsNullOrWhiteSpace(baseText))
            {
                return identityGuidance;
            }

            return baseText.TrimEnd() + "\n" + identityGuidance;
        }

internal string BuildRpgIdentityGuidance(DialogueScenarioContext context, Pawn target)
        {
            if (context?.IsRpg != true || target == null)
            {
                return string.Empty;
            }

            var identityParts = new List<string>();
            string role = Owner.ResolveRpgPawnIdentityRole(target);
            if (!string.IsNullOrWhiteSpace(role))
            {
                identityParts.Add($"IdentityRole: {role}");
            }

            string socialStatus = Owner.ResolveRpgPawnSocialStatus(target);
            if (!string.IsNullOrWhiteSpace(socialStatus))
            {
                identityParts.Add($"SocialStatus: {socialStatus}");
            }

            string factionStatus = Owner.ResolveRpgPawnFactionStatus(target);
            if (!string.IsNullOrWhiteSpace(factionStatus))
            {
                identityParts.Add($"FactionStatus: {factionStatus}");
            }

            string attitude = Owner.ResolveRpgAttitudeGuidance(context, target);
            if (!string.IsNullOrWhiteSpace(attitude))
            {
                identityParts.Add($"AttitudeGuidance: {attitude}");
            }

            if (identityParts.Count == 0)
            {
                return string.Empty;
            }

            return "=== IDENTITY AND ATTITUDE (REQUIRED) ===\n" +
                string.Join("\n", identityParts) +
                "\nKeep the dialogue aligned with this identity and attitude, but still react to the current scene instead of repeating labels mechanically.";
        }

internal string ResolveRpgPawnIdentityRole(Pawn pawn)
        {
            if (pawn == null)
            {
                return string.Empty;
            }

            if (pawn.IsPrisonerOfColony)
            {
                return "prisoner";
            }

            if (pawn.IsSlaveOfColony)
            {
                return "slave";
            }

            if (pawn.IsColonistPlayerControlled)
            {
                if (pawn.royalty?.AllTitlesForReading?.Count > 0)
                {
                    return "colonist noble";
                }

                return pawn.ageTracker?.CurLifeStage?.developmentalStage == DevelopmentalStage.Child
                    ? "colony child"
                    : "colonist";
            }

            if (pawn.IsQuestLodger())
            {
                return "quest lodger";
            }

            if (pawn.Faction != null)
            {
                if (pawn.Faction.HostileTo(Faction.OfPlayer))
                {
                    return "hostile outsider";
                }

                if (pawn.Faction != Faction.OfPlayer)
                {
                    return pawn.Faction.IsPlayer ? "player ally" : "visitor or outsider";
                }
            }

            return PawnDialogueRoutingPolicy.IsRpgDialogueEligibleRace(pawn) ? "independent pawn" : "non-dialogue-eligible pawn";
        }

internal string ResolveRpgPawnSocialStatus(Pawn pawn)
        {
            if (pawn == null)
            {
                return string.Empty;
            }

            if (pawn.IsPrisonerOfColony)
            {
                return "under player custody";
            }

            if (pawn.IsSlaveOfColony)
            {
                return "owned by the colony and expected to obey";
            }

            if (pawn.IsColonistPlayerControlled)
            {
                return "member of the player's colony";
            }

            if (pawn.IsQuestLodger())
            {
                return "temporary guest under quest protection";
            }

            return pawn.HostFaction != null
                ? $"linked to host faction {pawn.HostFaction.Name}"
                : string.Empty;
        }

internal string ResolveRpgPawnFactionStatus(Pawn pawn)
        {
            if (pawn?.Faction == null)
            {
                return string.Empty;
            }

            if (pawn.Faction == Faction.OfPlayer || pawn.Faction.IsPlayer)
            {
                return "player faction";
            }

            return pawn.Faction.HostileTo(Faction.OfPlayer)
                ? "hostile to player faction"
                : "not hostile to player faction";
        }

internal string ResolveRpgAttitudeGuidance(DialogueScenarioContext context, Pawn target)
        {
            Pawn initiator = context?.Initiator;
            string romanceState = initiator != null ? Owner.ResolvePairRomanceState(initiator, target) : string.Empty;

            if (target.IsPrisonerOfColony)
            {
                return "Default to guarded, pressured, or pleading responses. If the player controls their life, food, or release, the tone should naturally lean toward begging, bargaining, fear, or cautious compliance.";
            }

            if (target.IsSlaveOfColony)
            {
                return "Default to obedient and restrained responses. The tone should show submission, deference, and learned caution unless the scene clearly justifies resistance or emotional leakage.";
            }

            if (romanceState == "spouse" || romanceState == "fiance" || romanceState == "lover")
            {
                return "Default to warm, intimate, and familiar responses. The tone should reflect trust, closeness, and emotional attachment unless the current conflict clearly overrides it.";
            }

            if (target.IsColonistPlayerControlled)
            {
                if (target.ageTracker?.CurLifeStage?.developmentalStage == DevelopmentalStage.Child)
                {
                    return "Default to age-appropriate child responses. Keep the tone more direct, dependent, and emotionally transparent instead of sounding like a mature strategist.";
                }

                return "Default to cooperative colony-member responses. Speak like someone sharing daily survival, work, and risk with the other person.";
            }

            if (target.IsQuestLodger())
            {
                return "Default to polite and cautious guest-like responses. Show restraint because the pawn is staying under temporary protection, not fully at home.";
            }

            if (target.Faction != null && target.Faction.HostileTo(Faction.OfPlayer))
            {
                return "Default to guarded, distrustful, or provocative responses. Do not sound like a friendly assistant; hostility or tension should remain visible unless the scene meaningfully softens it.";
            }

            if (target.HostFaction != null && target.HostFaction != Faction.OfPlayer)
            {
                return "Default to outsider-style responses: polite but reserved, with clear social distance and limited trust.";
            }

            return "Match the pawn's concrete social position first, then let mood, opinion, and scene details shape the exact tone.";
        }
    }
}
