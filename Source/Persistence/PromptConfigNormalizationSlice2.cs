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
    internal sealed class PromptConfigNormalizationSlice2 : PromptConfigNormalizationCollaborator
    {
        internal PromptConfigNormalizationSlice2(PromptConfigNormalization owner) : base(owner)
        {
        }

internal bool EnsureRansomImportantRules(ResponseFormatConfig format)
        {
            if (format == null)
            {
                return false;
            }

            string rules = format.ImportantRules ?? string.Empty;
            bool changed = false;
            if (rules.IndexOf("For ransom intent, you MUST call request_info(info_type=prisoner) first.", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                rules = rules.Replace(
                    "For ransom intent, you MUST call request_info(info_type=prisoner) first.",
                    "Use request_info(info_type=prisoner) only when ransom target information is missing.");
                changed = true;
            }

            if (rules.IndexOf("pay_prisoner_ransom is forbidden before request_info(info_type=prisoner) succeeds.", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                rules = rules.Replace(
                    "pay_prisoner_ransom is forbidden before request_info(info_type=prisoner) succeeds.",
                    "If target_pawn_load_id is already known and valid, pay_prisoner_ransom may be called directly.");
                changed = true;
            }

            if (rules.IndexOf("Єдина законна дія перед наміром викупу — request_info(info_type=prisoner).", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                rules = rules.Replace(
                    "Єдина законна дія перед наміром викупу — request_info(info_type=prisoner).",
                    "request_info(info_type=prisoner) лише тоді, коли даних про ціль викупу бракує.");
                changed = true;
            }

            if (rules.IndexOf("Поки request_info(info_type=prisoner) не пройшов успішно, викликати pay_prisoner_ransom суворо заборонено.", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                rules = rules.Replace(
                    "Поки request_info(info_type=prisoner) не пройшов успішно, викликати pay_prisoner_ransom суворо заборонено.",
                    "Якщо target_pawn_load_id уже відомий і чинний, можна одразу викликати pay_prisoner_ransom.");
                changed = true;
            }

            if (rules.IndexOf("offer_silver must be a positive integer with no upper/lower limits.", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                rules = rules.Replace(
                    "offer_silver must be a positive integer with no upper/lower limits.",
                    "For pay_prisoner_ransom, offer_silver must reference the current offer window from system messages; execution clamps out-of-range values to the nearest boundary before submit.");
                changed = true;
            }

            if (rules.IndexOf("offer_silver має бути додатним цілим, без верхньої чи нижньої межі.", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                rules = rules.Replace(
                    "offer_silver має бути додатним цілим, без верхньої чи нижньої межі.",
                    "offer_silver має спиратися на поточний діапазон пропозицій із системного повідомлення; якщо він поза межами, перед виконанням його автоматично притиснуть до найближчої межі.");
                changed = true;
            }

            if (rules.IndexOf("Use request_info(info_type=prisoner) only when ransom target information is missing.", StringComparison.OrdinalIgnoreCase) < 0)
            {
                rules = Owner.AppendRuleLine(rules, "Use request_info(info_type=prisoner) only when ransom target information is missing.");
                changed = true;
            }

            if (rules.IndexOf("If target_pawn_load_id is already known and valid, pay_prisoner_ransom may be called directly.", StringComparison.OrdinalIgnoreCase) < 0)
            {
                rules = Owner.AppendRuleLine(rules, "If target_pawn_load_id is already known and valid, pay_prisoner_ransom may be called directly.");
                changed = true;
            }

            if (rules.IndexOf("payment_mode may be omitted", StringComparison.OrdinalIgnoreCase) < 0)
            {
                rules = Owner.AppendRuleLine(rules, "For pay_prisoner_ransom, payment_mode may be omitted; if provided, use exactly silver.");
                changed = true;
            }

            if (rules.IndexOf("keep offer_silver within the current offer window", StringComparison.OrdinalIgnoreCase) < 0)
            {
                rules = Owner.AppendRuleLine(rules, "For pay_prisoner_ransom, keep offer_silver within the current offer window provided by system messages.");
                changed = true;
            }

            if (rules.IndexOf("execution will clamp out-of-range offer_silver to the nearest window boundary", StringComparison.OrdinalIgnoreCase) < 0)
            {
                rules = Owner.AppendRuleLine(rules, "For pay_prisoner_ransom, execution will clamp out-of-range offer_silver to the nearest window boundary before submit.");
                changed = true;
            }

            if (rules.IndexOf("single payment submit", StringComparison.OrdinalIgnoreCase) < 0)
            {
                rules = Owner.AppendRuleLine(rules, "For pay_prisoner_ransom, normal flow executes a single payment submit only; in [RansomBatchSelection] flow, if pay_prisoner_ransom is emitted this turn, output one action per listed target in the same response.");
                changed = true;
            }

            if (rules.IndexOf("RansomBatchSelection", StringComparison.OrdinalIgnoreCase) < 0)
            {
                rules = Owner.AppendRuleLine(rules, "When [RansomBatchSelection] exists and pay_prisoner_ransom is emitted, keep total offer_silver within the batch offer window.");
                changed = true;
            }

            if (rules.IndexOf("rewrites each target offer proportionally", StringComparison.OrdinalIgnoreCase) < 0)
            {
                rules = Owner.AppendRuleLine(rules, "If [RansomBatchSelection] total offer_silver is out of batch window, execution clamps total to nearest boundary and rewrites each target offer proportionally with integer residual distribution.");
                changed = true;
            }

            if (rules.IndexOf("comms terminal", StringComparison.OrdinalIgnoreCase) < 0 &&
                rules.IndexOf("communication terminal", StringComparison.OrdinalIgnoreCase) < 0)
            {
                rules = Owner.AppendRuleLine(rules, "Dialogue is happening over the communication terminal, not offline or in-person.");
                changed = true;
            }

            if (rules.IndexOf("ransom paid/submitted", StringComparison.OrdinalIgnoreCase) < 0)
            {
                rules = Owner.AppendRuleLine(rules, "If natural language claims ransom paid/submitted, the same response must include pay_prisoner_ransom action.");
                changed = true;
            }

            if (rules.IndexOf("ransom settled/released", StringComparison.OrdinalIgnoreCase) < 0)
            {
                rules = Owner.AppendRuleLine(rules, "If natural language claims ransom settled/released, the same response must include pay_prisoner_ransom action.");
                changed = true;
            }

            const string hardRuleCurrentAskEn = "HARD RULE for pay_prisoner_ransom: when system messages provide current ask, offer_silver must equal current ask and must not reuse stale offers from memory.";
            const string hardRuleCurrentAskZh = "Для pay_prisoner_ransom (жорстке правило): коли системне повідомлення дає «поточну ціну», offer_silver має дорівнювати саме їй; повторно брати стару ціну з памʼяті заборонено.";
            string rulesWithoutHardAskRule = Owner.RemoveRuleLine(Owner.RemoveRuleLine(rules, hardRuleCurrentAskEn), hardRuleCurrentAskZh);
            if (!string.Equals(rulesWithoutHardAskRule, rules, StringComparison.Ordinal))
            {
                rules = rulesWithoutHardAskRule;
                changed = true;
            }

            if (changed)
            {
                format.ImportantRules = rules;
            }

            return changed;
        }

internal string AppendRuleLine(string rules, string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return rules ?? string.Empty;
            }

            string baseText = rules ?? string.Empty;
            int maxRuleNumber = 0;
            foreach (string existingLine in baseText.Split('\n'))
            {
                string trimmed = existingLine?.Trim() ?? string.Empty;
                int dotIndex = trimmed.IndexOf('.');
                if (dotIndex <= 0)
                {
                    continue;
                }

                string prefix = trimmed.Substring(0, dotIndex);
                if (int.TryParse(prefix, out int parsed) && parsed > maxRuleNumber)
                {
                    maxRuleNumber = parsed;
                }
            }

            int nextRuleNumber = Math.Max(1, maxRuleNumber + 1);
            string nextLine = $"{nextRuleNumber}. {line}";
            if (string.IsNullOrWhiteSpace(baseText))
            {
                return nextLine;
            }

            return baseText.TrimEnd() + "\n" + nextLine;
        }

internal string RemoveRuleLine(string rules, string line)
        {
            if (string.IsNullOrWhiteSpace(rules) || string.IsNullOrWhiteSpace(line))
            {
                return rules ?? string.Empty;
            }

            string target = line.Trim();
            string[] sourceLines = rules.Replace("\r\n", "\n").Split('\n');
            var kept = new List<string>(sourceLines.Length);
            foreach (string sourceLine in sourceLines)
            {
                if (string.Equals(sourceLine?.Trim(), target, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                kept.Add(sourceLine ?? string.Empty);
            }

            return string.Join("\n", kept).Trim();
        }

internal bool IsLegacyMakePeaceDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return true;
            }

            string value = description.Trim();
            return string.Equals(value, "Offer peace treaty", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Offer peace treaty (requires war)", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "offer peace", StringComparison.OrdinalIgnoreCase);
        }

internal bool IsLegacyMakePeaceRequirement(string requirement)
        {
            if (string.IsNullOrWhiteSpace(requirement))
            {
                return true;
            }

            string value = requirement.Trim().ToLowerInvariant();
            return value == "already at war"
                || value == "requires war"
                || value.Contains("already at war") && !host.DiplomacyBuilder.ContainsSincerityConstraint(requirement);
        }

internal bool RemoveDeprecatedPromptAction(SystemPromptConfig config, string actionName)
        {
            if (config?.ApiActions == null || string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            int removedCount = config.ApiActions.RemoveAll(action =>
                string.Equals(action?.ActionName, actionName, StringComparison.Ordinal));
            if (removedCount <= 0)
            {
                return false;
            }

            Log.Message($"[RimAI.Relations] Migrating config: Removing deprecated prompt action '{actionName}'.");
            return true;
        }

internal bool EnsureResponseFormatDefaults(SystemPromptConfig config, SystemPromptConfig defaults)
        {
            if (defaults?.ResponseFormat == null)
            {
                return false;
            }

            if (config.ResponseFormat == null)
            {
                config.ResponseFormat = defaults.ResponseFormat.Clone();
                return true;
            }

            bool changed = false;
            changed |= Owner.AssignIfMissing(ref config.ResponseFormat.JsonTemplate, defaults.ResponseFormat.JsonTemplate);
            changed |= Owner.AssignIfMissing(ref config.ResponseFormat.ImportantRules, defaults.ResponseFormat.ImportantRules);
            changed |= Owner.EnsureRansomImportantRules(config.ResponseFormat);
            return changed;
        }

internal bool EnsureDecisionRuleDefaults(SystemPromptConfig config, SystemPromptConfig defaults)
        {
            if (defaults?.DecisionRules == null || defaults.DecisionRules.Count == 0)
            {
                return false;
            }

            config.DecisionRules ??= new List<DecisionRuleConfig>();
            bool changed = false;
            foreach (DecisionRuleConfig defRule in defaults.DecisionRules)
            {
                DecisionRuleConfig target = config.DecisionRules.FirstOrDefault(
                    r => string.Equals(r.RuleName, defRule.RuleName, StringComparison.Ordinal));
                if (target == null)
                {
                    config.DecisionRules.Add(defRule.Clone());
                    changed = true;
                    continue;
                }

                changed |= Owner.AssignIfMissing(ref target.RuleContent, defRule.RuleContent);
            }

            return changed;
        }

internal bool EnsureEnvironmentPromptDefaults(SystemPromptConfig config, SystemPromptConfig defaults)
        {
            if (defaults?.EnvironmentPrompt == null)
            {
                return false;
            }

            if (config.EnvironmentPrompt == null)
            {
                config.EnvironmentPrompt = defaults.EnvironmentPrompt.Clone();
                return true;
            }

            bool changed = false;
            changed |= Owner.EnsureWorldviewDefaults(config.EnvironmentPrompt, defaults.EnvironmentPrompt);
            changed |= Owner.EnsureSceneDefaults(config.EnvironmentPrompt, defaults.EnvironmentPrompt);
            changed |= Owner.EnsureEnvSwitchDefaults(config.EnvironmentPrompt, defaults.EnvironmentPrompt);
            changed |= Owner.EnsureRpgSwitchDefaults(config.EnvironmentPrompt, defaults.EnvironmentPrompt);
            changed |= Owner.TryUpgradeLegacyRpgSwitchDefaults(config.EnvironmentPrompt);
            changed |= Owner.EnsureEventIntelDefaults(config.EnvironmentPrompt, defaults.EnvironmentPrompt);
            return changed;
        }

internal bool EnsureWorldviewDefaults(EnvironmentPromptConfig target, EnvironmentPromptConfig defaults)
        {
            if (defaults?.Worldview == null)
            {
                return false;
            }

            if (target.Worldview == null)
            {
                target.Worldview = defaults.Worldview.Clone();
                return true;
            }

            return Owner.AssignIfMissing(ref target.Worldview.Content, defaults.Worldview.Content);
        }

internal bool EnsureSceneDefaults(EnvironmentPromptConfig target, EnvironmentPromptConfig defaults)
        {
            if (defaults?.SceneEntries == null || defaults.SceneEntries.Count == 0)
            {
                return false;
            }

            target.SceneEntries ??= new List<ScenePromptEntryConfig>();
            if (target.SceneEntries.Count > 0)
            {
                return false;
            }

            target.SceneEntries = defaults.SceneEntries.Select(entry => entry.Clone()).ToList();
            return true;
        }

internal bool EnsureEnvSwitchDefaults(EnvironmentPromptConfig target, EnvironmentPromptConfig defaults)
        {
            if (defaults?.EnvironmentContextSwitches == null || target.EnvironmentContextSwitches != null)
            {
                return false;
            }

            target.EnvironmentContextSwitches = defaults.EnvironmentContextSwitches.Clone();
            return true;
        }

internal bool EnsureRpgSwitchDefaults(EnvironmentPromptConfig target, EnvironmentPromptConfig defaults)
        {
            if (defaults?.RpgSceneParamSwitches == null || target.RpgSceneParamSwitches != null)
            {
                return false;
            }

            target.RpgSceneParamSwitches = defaults.RpgSceneParamSwitches.Clone();
            return true;
        }

internal bool TryUpgradeLegacyRpgSwitchDefaults(EnvironmentPromptConfig target)
        {
            RpgSceneParamSwitchesConfig switches = target?.RpgSceneParamSwitches;
            if (switches == null || !Owner.IsLegacyRpgSwitchSignature(switches))
            {
                return false;
            }

            switches.IncludeNeeds = true;
            switches.IncludeRecentJobState = true;
            return true;
        }

internal bool IsLegacyRpgSwitchSignature(RpgSceneParamSwitchesConfig switches)
        {
            return switches.IncludeSkills &&
                switches.IncludeEquipment &&
                !switches.IncludeGenes &&
                !switches.IncludeNeeds &&
                switches.IncludeHediffs &&
                switches.IncludeRecentEvents &&
                !switches.IncludeColonyInventorySummary &&
                !switches.IncludeHomeAlerts &&
                !switches.IncludeRecentJobState &&
                !switches.IncludeAttributeLevels;
        }

internal bool EnsureEventIntelDefaults(EnvironmentPromptConfig target, EnvironmentPromptConfig defaults)
        {
            if (defaults?.EventIntelPrompt == null || target.EventIntelPrompt != null)
            {
                return false;
            }

            target.EventIntelPrompt = defaults.EventIntelPrompt.Clone();
            return true;
        }

internal bool EnsureDynamicInjectionDefaults(SystemPromptConfig config, SystemPromptConfig defaults)
        {
            if (defaults?.DynamicDataInjection == null)
            {
                return false;
            }

            if (config.DynamicDataInjection == null)
            {
                config.DynamicDataInjection = defaults.DynamicDataInjection.Clone();
                return true;
            }

            return Owner.AssignIfMissing(
                ref config.DynamicDataInjection.CustomInjectionHeader,
                defaults.DynamicDataInjection.CustomInjectionHeader);
        }
    }
}
