using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Dialogue;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Relation;
using Ustas.RimAI.Communication.Relations.Guards;
using Ustas.RimAI.Communication.Relations.WorldState;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.Prompting.Builders;
using Ustas.RimAI.Communication.Relations.Context;
using Ustas.RimAI.Communication.Relations.Prompting.Transfer;
using Ustas.RimAI.Communication.Relations.Serialization;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Prompting.Diplomacy;

namespace Ustas.RimAI.Communication.Relations.Persistence
{
    internal sealed class PromptConfigNormalizationSlice3 : PromptConfigNormalizationCollaborator
    {
        internal PromptConfigNormalizationSlice3(PromptConfigNormalization owner) : base(owner)
        {
        }

internal bool EnsurePromptTemplateDefaults(SystemPromptConfig config, SystemPromptConfig defaults)
        {
            if (config == null)
            {
                return false;
            }

            PromptTemplateTextConfig templateDefaults = defaults?.PromptTemplates;
            if (templateDefaults == null)
            {
                return false;
            }

            config.PromptTemplates ??= new PromptTemplateTextConfig();
            PromptTemplateTextConfig target = config.PromptTemplates;
            bool changed = false;

            changed |= Owner.AssignIfMissing(ref target.FactGroundingTemplate, templateDefaults.FactGroundingTemplate);
            changed |= Owner.AssignIfMissing(ref target.OutputLanguageTemplate, templateDefaults.OutputLanguageTemplate);
            changed |= Owner.AssignIfMissing(ref target.DiplomacyFallbackRoleTemplate, templateDefaults.DiplomacyFallbackRoleTemplate);
            changed |= Owner.AssignIfMissing(ref target.SocialCircleActionRuleTemplate, templateDefaults.SocialCircleActionRuleTemplate);
            changed |= Owner.AssignIfMissing(ref target.SocialCircleNewsStyleTemplate, templateDefaults.SocialCircleNewsStyleTemplate);
            changed |= Owner.AssignIfMissing(ref target.SocialCircleNewsJsonContractTemplate, templateDefaults.SocialCircleNewsJsonContractTemplate);
            changed |= Owner.AssignIfMissing(ref target.SocialCircleNewsFactTemplate, templateDefaults.SocialCircleNewsFactTemplate);
            changed |= Owner.AssignIfMissing(ref target.DecisionPolicyTemplate, templateDefaults.DecisionPolicyTemplate);
            changed |= Owner.AssignIfMissing(ref target.TurnObjectiveTemplate, templateDefaults.TurnObjectiveTemplate);
            changed |= Owner.AssignIfMissing(ref target.TopicShiftRuleTemplate, templateDefaults.TopicShiftRuleTemplate);
            changed |= Owner.AssignIfMissing(ref target.RpgRoleSettingTemplate, templateDefaults.RpgRoleSettingTemplate);
            changed |= Owner.AssignIfMissing(ref target.RpgCompactFormatConstraintTemplate, templateDefaults.RpgCompactFormatConstraintTemplate);
            changed |= Owner.AssignIfMissing(ref target.RpgActionReliabilityRuleTemplate, templateDefaults.RpgActionReliabilityRuleTemplate);
            changed |= Owner.AssignIfMissing(ref target.OpeningObjectiveTemplate, templateDefaults.OpeningObjectiveTemplate);
            changed |= Owner.AssignIfMissing(ref target.ProactiveRomanceRuleTemplate, templateDefaults.ProactiveRomanceRuleTemplate);
            changed |= Owner.AssignIfMissing(ref target.ProactiveSocialActionRuleTemplate, templateDefaults.ProactiveSocialActionRuleTemplate);
            changed |= Owner.ForceRefreshRpgPromptTemplates(target);
            changed |= Owner.AssignIfMissing(ref target.ApiLimitsNodeTemplate, templateDefaults.ApiLimitsNodeTemplate);
            changed |= Owner.AssignIfMissing(ref target.QuestGuidanceNodeTemplate, templateDefaults.QuestGuidanceNodeTemplate);
            changed |= Owner.AssignIfMissing(ref target.ResponseContractNodeTemplate, templateDefaults.ResponseContractNodeTemplate);
            changed |= Owner.AssignIfMissing(ref target.MandatoryRaceInjectionTemplate, templateDefaults.MandatoryRaceInjectionTemplate);
            changed |= Owner.TryMigrateLegacyNodeBodyLiteralTemplates(target);

            if (changed)
            {
                Log.Message("[RimAI.Relations] Migrating config: Filled missing PromptTemplates fields from default template file.");
            }

            return changed;
        }

internal bool ForceRefreshRpgPromptTemplates(PromptTemplateTextConfig target)
        {
            if (target == null)
            {
                return false;
            }

            RpgPromptDefaultsConfig rpgDefaults = RpgPromptDefaultsProvider.GetDefaults()
                ?? RpgPromptDefaultsConfig.CreateFallback();
            if (rpgDefaults == null)
            {
                return false;
            }

            bool changed = false;
            string newProactiveRomance = rpgDefaults.ProactiveRomanceRuleTemplate;
            if (!string.IsNullOrWhiteSpace(newProactiveRomance) && newProactiveRomance != target.ProactiveRomanceRuleTemplate)
            {
                target.ProactiveRomanceRuleTemplate = newProactiveRomance;
                changed = true;
            }

            string newProactiveSocial = rpgDefaults.ProactiveSocialActionRuleTemplate;
            if (!string.IsNullOrWhiteSpace(newProactiveSocial) && newProactiveSocial != target.ProactiveSocialActionRuleTemplate)
            {
                target.ProactiveSocialActionRuleTemplate = newProactiveSocial;
                changed = true;
            }

            return changed;
        }

internal bool EnsurePromptPolicyDefaults(SystemPromptConfig config, SystemPromptConfig defaults)
        {
            if (config == null)
            {
                return false;
            }

            PromptPolicyConfig defaultPolicy = defaults?.PromptPolicy ?? PromptPolicyConfig.CreateDefault();
            if (defaultPolicy == null)
            {
                return false;
            }

            bool changed = false;
            if (config.PromptPolicy == null)
            {
                config.PromptPolicy = defaultPolicy.Clone();
                changed = true;
            }

            PromptPolicyConfig target = config.PromptPolicy;
            changed |= Owner.AssignIfLessOrEqualZero(ref target.IntentActionCooldownTurns, defaultPolicy.IntentActionCooldownTurns);
            changed |= Owner.AssignIfLessOrEqualZero(ref target.IntentMinAssistantRoundsForMemory, defaultPolicy.IntentMinAssistantRoundsForMemory);
            changed |= Owner.AssignIfLessOrEqualZero(ref target.IntentNoActionStreakThreshold, defaultPolicy.IntentNoActionStreakThreshold);
            changed |= Owner.AssignIfLessOrEqualZero(ref target.SummaryTimelineTurnLimit, defaultPolicy.SummaryTimelineTurnLimit);
            changed |= Owner.AssignIfLessOrEqualZero(ref target.SummaryCharBudget, defaultPolicy.SummaryCharBudget);

            int schemaVersion = config.PromptPolicySchemaVersion;
            if (schemaVersion <= 0)
            {
                config.PromptPolicySchemaVersion = SystemPromptConfig.CurrentPromptPolicySchemaVersion;
                changed = true;
            }

            if (config.PromptSchemaVersion <= 0)
            {
                config.PromptSchemaVersion = SystemPromptConfig.CurrentPromptSchemaVersion;
                changed = true;
            }

            return changed;
        }

internal bool TryMigrateLegacyNodeBodyLiteralTemplates(PromptTemplateTextConfig templates)
        {
            if (templates == null)
            {
                return false;
            }

            bool changed = false;
            changed |= Owner.TryRewriteLegacyNodeTemplate(
                ref templates.ApiLimitsNodeTemplate,
                PromptTextConstants.ApiLimitsNodeLiteralDefault,
                "=== CURRENT API LIMITS (MUST FOLLOW) ===",
                "Max goodwill adjustment per call:");
            changed |= Owner.TryRewriteLegacyNodeTemplate(
                ref templates.QuestGuidanceNodeTemplate,
                PromptTextConstants.QuestGuidanceNodeLiteralDefault,
                "=== DYNAMIC QUEST AVAILABILITY (Auto-generated for current faction) ===",
                "=== QUEST TEMPLATE STRICT OVERRIDE ===");
            changed |= Owner.TryRewriteLegacyNodeTemplate(
                ref templates.QuestGuidanceNodeTemplate,
                PromptTextConstants.QuestGuidanceNodeLiteralDefault,
                "=== 动态任务可用性（按当前派系自动生成） ===",
                "=== 任务模板严格覆盖规则 ===");
            changed |= Owner.TryRewriteLegacyNodeTemplate(
                ref templates.ResponseContractNodeTemplate,
                PromptTextConstants.ResponseContractNodeLiteralDefault,
                "=== RESPONSE CONTRACT ===",
                "If no action is needed, reply normally with no JSON block.");
            if (changed)
            {
                Log.Warning("[RimAI.Relations] Migrating config: Rewrote legacy hard-text node templates to Scriban runtime-body templates.");
            }

            return changed;
        }

internal bool TryRewriteLegacyNodeTemplate(
            ref string template,
            string rewrittenTemplate,
            string requiredMarkerA,
            string requiredMarkerB)
        {
            string source = template?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            if (string.Equals(source, rewrittenTemplate, StringComparison.Ordinal))
            {
                return false;
            }

            if (!source.Contains(requiredMarkerA) || !source.Contains(requiredMarkerB))
            {
                return false;
            }

            template = rewrittenTemplate;
            return true;
        }

internal bool AssignIfMissing(ref string target, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(fallback))
            {
                return false;
            }

            target = fallback;
            return true;
        }

internal bool AssignIfLessOrEqualZero(ref int target, int fallback)
        {
            if (target > 0 || fallback <= 0)
            {
                return false;
            }

            target = fallback;
            return true;
        }
    }
}
