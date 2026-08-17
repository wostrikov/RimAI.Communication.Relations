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
    internal sealed class PromptConfigNormalization
    {
        private readonly PromptPersistenceService host;

        private PromptTemplateAutoRewriteResult _lastSchemaRewriteResult;
        internal PromptTemplateAutoRewriteResult LastSchemaRewriteResult => _lastSchemaRewriteResult;

        private static readonly string[] PresenceBehaviorSectionTitles =
        {
            "【在线状态策略】",
            "Online Status Strategy:",
            "Online Status Strategy"
        };

        private static readonly string[] PresenceBehaviorActionAnchors =
        {
            "[exit_dialogue]",
            "[go_offline",
            "[set_dnd]"
        };

        internal PromptConfigNormalization(PromptPersistenceService host)
        {
            this.host = host ?? throw new System.ArgumentNullException(nameof(host));
        }
        internal bool MigratePresenceBehaviorGuidance(SystemPromptConfig config)
        {
            if (config == null || string.IsNullOrWhiteSpace(config.GlobalSystemPrompt))
            {
                return false;
            }

            if (ContainsPresenceBehaviorGuidance(config.GlobalSystemPrompt))
            {
                return false;
            }

            string sectionContent = LoadPresenceBehaviorGuidanceSection();
            if (string.IsNullOrWhiteSpace(sectionContent))
            {
                return false;
            }

            int insertIndex = FindPresenceBehaviorInsertIndex(config.GlobalSystemPrompt);
            if (insertIndex >= 0)
            {
                config.GlobalSystemPrompt = config.GlobalSystemPrompt.Insert(insertIndex, sectionContent);
            }
            else
            {
                config.GlobalSystemPrompt += "\n\n" + sectionContent.TrimEnd();
            }

            Log.Message("[RimAI.Relations] Migrating config: Added presence behavior guidance.");
            return true;
        }

        internal string LoadPresenceBehaviorGuidanceSection()
        {
            string defaultPrompt = host.DomainStore.CreateDefaultConfig()?.GlobalSystemPrompt;
            string extracted = ExtractPresenceBehaviorSection(defaultPrompt);
            return !string.IsNullOrWhiteSpace(extracted)
                ? extracted
                : BuildPresenceBehaviorFallbackSection();
        }

        internal bool ContainsPresenceBehaviorGuidance(string promptText)
        {
            if (string.IsNullOrWhiteSpace(promptText))
            {
                return false;
            }

            string normalized = promptText.Replace("\r\n", "\n");
            for (int i = 0; i < PresenceBehaviorActionAnchors.Length; i++)
            {
                if (normalized.IndexOf(PresenceBehaviorActionAnchors[i], StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return false;
                }
            }

            return true;
        }

        internal int FindPresenceBehaviorInsertIndex(string promptText)
        {
            if (string.IsNullOrWhiteSpace(promptText))
            {
                return -1;
            }

            int actionOutputIndex = promptText.IndexOf("Action Output Rule:", StringComparison.OrdinalIgnoreCase);
            if (actionOutputIndex >= 0)
            {
                return actionOutputIndex;
            }

            int importantBanIndex = promptText.IndexOf("【重要禁令】", StringComparison.Ordinal);
            if (importantBanIndex >= 0)
            {
                return importantBanIndex;
            }

            return promptText.IndexOf("Format Ban:", StringComparison.OrdinalIgnoreCase);
        }

        internal string ExtractPresenceBehaviorSection(string promptText)
        {
            if (string.IsNullOrWhiteSpace(promptText))
            {
                return string.Empty;
            }

            string[] lines = promptText.Replace("\r\n", "\n").Split('\n');
            int start = FindPresenceBehaviorSectionStart(lines);
            if (start < 0)
            {
                return string.Empty;
            }

            var sectionLines = new List<string> { NormalizePresenceBehaviorSectionTitle(lines[start]) };
            for (int i = start + 1; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    continue;
                }

                if (IsPresenceBehaviorBoundary(trimmed))
                {
                    break;
                }

                if (trimmed.StartsWith("[", StringComparison.Ordinal) ||
                    trimmed.StartsWith("-", StringComparison.Ordinal))
                {
                    sectionLines.Add(trimmed);
                }
            }

            if (!ContainsPresenceBehaviorGuidance(string.Join("\n", sectionLines)))
            {
                return string.Empty;
            }

            return string.Join("\n", sectionLines) + "\n\n";
        }

        internal int FindPresenceBehaviorSectionStart(IReadOnlyList<string> lines)
        {
            if (lines == null || lines.Count == 0)
            {
                return -1;
            }

            for (int i = 0; i < lines.Count; i++)
            {
                string trimmed = lines[i]?.Trim() ?? string.Empty;
                for (int j = 0; j < PresenceBehaviorSectionTitles.Length; j++)
                {
                    if (string.Equals(trimmed, PresenceBehaviorSectionTitles[j], StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        internal string NormalizePresenceBehaviorSectionTitle(string titleLine)
        {
            string trimmed = titleLine?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(trimmed) ? "Online Status Strategy:" : trimmed;
        }

        internal bool IsPresenceBehaviorBoundary(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            return line.StartsWith("Action Output Rule:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Setting Integrity:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Identity Lock:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Format Ban:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Worldview Compliance:", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(line, "【重要禁令】", StringComparison.Ordinal);
        }

        internal string BuildPresenceBehaviorFallbackSection()
        {
            return
                "Online Status Strategy:\n" +
                "[exit_dialogue]: Leave normally when the topic is complete, the discussion stalls repeatedly, or a polite refusal has already been made.\n" +
                "[go_offline | reason]: Go fully offline due to duties, survival pressure, or serious player offense.\n" +
                "[set_dnd]: Refuse further interruption while staying in character after strong offense, emotional overload, or repeated harassment.\n\n";
        }

        internal bool TryApplyPromptSchemaUpgrade(SystemPromptConfig config)
        {
            if (config == null)
            {
                return false;
            }

            int current = SystemPromptConfig.CurrentPromptSchemaVersion;
            int loaded = config.PromptSchemaVersion;
            if (loaded >= current)
            {
                return false;
            }

            PromptTemplateAutoRewriteResult rewrite = PromptTemplateAutoRewriter.RewriteSystemPromptConfig(
                config,
                ScribanPromptEngine.Instance);
            _lastSchemaRewriteResult = rewrite;
            config.PromptSchemaVersion = current;
            if (rewrite.HasBlockedTemplates)
            {
                PromptTemplateRewriteDiagnostic blocked = rewrite.TemplateDiagnostics.FirstOrDefault(item => item != null && item.Blocked);
                string blockedId = blocked?.TemplateId;
                if (string.IsNullOrWhiteSpace(blockedId))
                {
                    blockedId = rewrite.BlockedTemplateIds[0];
                }

                string blockedChannel = string.IsNullOrWhiteSpace(blocked?.Channel) ? "system" : blocked.Channel;
                string blockedReason = string.IsNullOrWhiteSpace(blocked?.Reason)
                    ? "Template migration failed and the template was marked as Blocked."
                    : blocked.Reason;
                throw new PromptRenderException(
                    blockedId,
                    blockedChannel,
                    new PromptRenderDiagnostic
                    {
                        ErrorCode = PromptRenderErrorCode.TemplateBlocked,
                        Message = blockedReason
                    });
            }

            if (rewrite.Changed)
            {
                Log.Warning($"[RimAI.Relations] Prompt schema migration ({loaded} -> {current}) rewrote templates to namespaced Scriban variables.");
            }

            return rewrite.Changed || loaded != current;
        }

        internal bool TryApplyPromptPolicySchemaUpgrade(ref SystemPromptConfig config)
        {
            if (config == null)
            {
                return false;
            }

            int current = SystemPromptConfig.CurrentPromptPolicySchemaVersion;
            int loaded = config.PromptPolicySchemaVersion;
            config.PromptPolicy ??= PromptPolicyConfig.CreateDefault();
            if (loaded >= current)
            {
                return false;
            }

            if (config.PromptPolicy.ResetPromptCustomOnSchemaUpgrade)
            {
                Log.Warning(
                    $"[RimAI.Relations] Prompt policy schema upgrade detected ({loaded} -> {current}). " +
                    "Resetting prompt custom overrides to new defaults.");
                config = host.DomainStore.CreateDefaultConfig();
                config.PromptPolicySchemaVersion = current;
                config.PromptPolicy ??= PromptPolicyConfig.CreateDefault();
                return true;
            }

            config.PromptPolicySchemaVersion = current;
            if (config.PromptPolicy == null)
            {
                config.PromptPolicy = PromptPolicyConfig.CreateDefault();
            }

            return true;
        }

        internal bool EnsurePresenceActionExists(SystemPromptConfig config, string actionName, string description, string parameters, string requirement)
        {
            if (config?.ApiActions == null || string.IsNullOrEmpty(actionName))
            {
                return false;
            }

            if (config.ApiActions.Any(a => string.Equals(a.ActionName, actionName, StringComparison.Ordinal)))
            {
                return false;
            }

            int insertIndex = config.ApiActions.FindIndex(a => a.ActionName == "reject_request");
            if (insertIndex == -1)
            {
                insertIndex = config.ApiActions.Count;
            }

            config.ApiActions.Insert(insertIndex, new ApiActionConfig(actionName, description, parameters, requirement));
            Log.Message($"[RimAI.Relations] Migrating config: Adding {actionName} action...");
            return true;
        }

        internal bool EnsureConfigDefaults(SystemPromptConfig config)
        {
            if (config == null)
            {
                return false;
            }

            SystemPromptConfig defaults = host.DomainStore.CreateDefaultConfig();
            if (defaults == null)
            {
                return false;
            }

            bool changed = false;
            changed |= EnsureApiActionDefaults(config, defaults);
            changed |= EnsureResponseFormatDefaults(config, defaults);
            changed |= EnsureDecisionRuleDefaults(config, defaults);
            changed |= EnsureEnvironmentPromptDefaults(config, defaults);
            changed |= EnsureDynamicInjectionDefaults(config, defaults);
            changed |= EnsurePromptTemplateDefaults(config, defaults);
            changed |= EnsurePromptPolicyDefaults(config, defaults);
            return changed;
        }

        internal bool EnsureApiActionDefaults(SystemPromptConfig config, SystemPromptConfig defaults)
        {
            if (defaults?.ApiActions == null || defaults.ApiActions.Count == 0)
            {
                return false;
            }

            config.ApiActions ??= new List<ApiActionConfig>();
            bool changed = false;
            changed |= RemoveDeprecatedPromptAction(config, "send_gift");
            foreach (ApiActionConfig defAction in defaults.ApiActions)
            {
                ApiActionConfig target = config.ApiActions.FirstOrDefault(
                    a => string.Equals(a.ActionName, defAction.ActionName, StringComparison.Ordinal));
                if (target == null)
                {
                    config.ApiActions.Add(defAction.Clone());
                    changed = true;
                    continue;
                }

                changed |= AssignIfMissing(ref target.Description, defAction.Description);
                changed |= AssignIfMissing(ref target.Parameters, defAction.Parameters);
                changed |= AssignIfMissing(ref target.Requirement, defAction.Requirement);
                changed |= TryUpgradeLegacyMakePeaceAction(target, defAction);
                changed |= TryUpgradeRansomActionContract(target, defAction);
            }

            return changed;
        }

        internal bool TryUpgradeLegacyMakePeaceAction(ApiActionConfig target, ApiActionConfig defAction)
        {
            if (target == null || !string.Equals(target.ActionName, "make_peace", StringComparison.Ordinal))
            {
                return false;
            }

            bool changed = false;
            if (IsLegacyMakePeaceDescription(target.Description) && !string.IsNullOrWhiteSpace(defAction?.Description))
            {
                target.Description = defAction.Description;
                changed = true;
            }

            if (IsLegacyMakePeaceRequirement(target.Requirement) && !string.IsNullOrWhiteSpace(defAction?.Requirement))
            {
                target.Requirement = defAction.Requirement;
                changed = true;
            }

            return changed;
        }

        internal bool TryUpgradeRansomActionContract(ApiActionConfig target, ApiActionConfig defAction)
        {
            if (target == null || string.IsNullOrWhiteSpace(target.ActionName))
            {
                return false;
            }

            bool changed = false;
            if (string.Equals(target.ActionName, "request_info", StringComparison.Ordinal))
            {
                string requestInfoRequirement = target.Requirement ?? string.Empty;
                bool hasLegacyPreconditionWording =
                    requestInfoRequirement.IndexOf("only valid precondition", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    requestInfoRequirement.IndexOf("唯一合法前置", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    requestInfoRequirement.IndexOf("必须且只能先调用", StringComparison.OrdinalIgnoreCase) >= 0;
                if (string.IsNullOrWhiteSpace(target.Requirement) ||
                    target.Requirement.IndexOf("info_type=prisoner", StringComparison.OrdinalIgnoreCase) < 0 ||
                    hasLegacyPreconditionWording)
                {
                    target.Requirement = defAction?.Requirement ?? target.Requirement;
                    changed = true;
                }
            }
            else if (string.Equals(target.ActionName, "pay_prisoner_ransom", StringComparison.Ordinal))
            {
                string payRequirement = target.Requirement ?? string.Empty;
                bool hasLegacyHardGate =
                    payRequirement.IndexOf("forbidden before request_info", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    payRequirement.IndexOf("唯一合法前置", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    payRequirement.IndexOf("严禁调用", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    payRequirement.IndexOf("MUST call request_info", StringComparison.OrdinalIgnoreCase) >= 0;
                if (string.IsNullOrWhiteSpace(target.Requirement) ||
                    hasLegacyHardGate ||
                    target.Requirement.IndexOf("payment_mode may be omitted", StringComparison.OrdinalIgnoreCase) < 0 ||
                    (target.Requirement.IndexOf("offer_silver must stay inside the current offer window", StringComparison.OrdinalIgnoreCase) < 0 &&
                     target.Requirement.IndexOf("offer_silver must reference the current offer window", StringComparison.OrdinalIgnoreCase) < 0) ||
                    target.Requirement.IndexOf("single payment submit", StringComparison.OrdinalIgnoreCase) < 0 ||
                    target.Requirement.IndexOf("must include pay_prisoner_ransom action", StringComparison.OrdinalIgnoreCase) < 0 ||
                    target.Requirement.IndexOf("submitted/paid/settled/released", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    target.Requirement = defAction?.Requirement ?? target.Requirement;
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(target.Parameters) ||
                    target.Parameters.IndexOf("omit or set exactly silver", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    target.Parameters = defAction?.Parameters ?? target.Parameters;
                    changed = true;
                }
            }

            return changed;
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

            if (rules.IndexOf("赎金意图的唯一合法前置动作是 request_info(info_type=prisoner)。", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                rules = rules.Replace(
                    "赎金意图的唯一合法前置动作是 request_info(info_type=prisoner)。",
                    "仅在赎金目标信息不足时使用 request_info(info_type=prisoner)。");
                changed = true;
            }

            if (rules.IndexOf("在 request_info(info_type=prisoner) 成功前，严禁调用 pay_prisoner_ransom。", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                rules = rules.Replace(
                    "在 request_info(info_type=prisoner) 成功前，严禁调用 pay_prisoner_ransom。",
                    "若 target_pawn_load_id 已明确有效，可直接调用 pay_prisoner_ransom。");
                changed = true;
            }

            if (rules.IndexOf("offer_silver must be a positive integer with no upper/lower limits.", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                rules = rules.Replace(
                    "offer_silver must be a positive integer with no upper/lower limits.",
                    "For pay_prisoner_ransom, offer_silver must reference the current offer window from system messages; execution clamps out-of-range values to the nearest boundary before submit.");
                changed = true;
            }

            if (rules.IndexOf("offer_silver 必须为正整数，无上下限限制。", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                rules = rules.Replace(
                    "offer_silver 必须为正整数，无上下限限制。",
                    "offer_silver 必须参考系统消息给出的当前可报价区间；若越界，执行前会自动夹逼到最近边界。");
                changed = true;
            }

            if (rules.IndexOf("Use request_info(info_type=prisoner) only when ransom target information is missing.", StringComparison.OrdinalIgnoreCase) < 0)
            {
                rules = AppendRuleLine(rules, "Use request_info(info_type=prisoner) only when ransom target information is missing.");
                changed = true;
            }

            if (rules.IndexOf("If target_pawn_load_id is already known and valid, pay_prisoner_ransom may be called directly.", StringComparison.OrdinalIgnoreCase) < 0)
            {
                rules = AppendRuleLine(rules, "If target_pawn_load_id is already known and valid, pay_prisoner_ransom may be called directly.");
                changed = true;
            }

            if (rules.IndexOf("payment_mode may be omitted", StringComparison.OrdinalIgnoreCase) < 0)
            {
                rules = AppendRuleLine(rules, "For pay_prisoner_ransom, payment_mode may be omitted; if provided, use exactly silver.");
                changed = true;
            }

            if (rules.IndexOf("keep offer_silver within the current offer window", StringComparison.OrdinalIgnoreCase) < 0)
            {
                rules = AppendRuleLine(rules, "For pay_prisoner_ransom, keep offer_silver within the current offer window provided by system messages.");
                changed = true;
            }

            if (rules.IndexOf("execution will clamp out-of-range offer_silver to the nearest window boundary", StringComparison.OrdinalIgnoreCase) < 0)
            {
                rules = AppendRuleLine(rules, "For pay_prisoner_ransom, execution will clamp out-of-range offer_silver to the nearest window boundary before submit.");
                changed = true;
            }

            if (rules.IndexOf("single payment submit", StringComparison.OrdinalIgnoreCase) < 0)
            {
                rules = AppendRuleLine(rules, "For pay_prisoner_ransom, normal flow executes a single payment submit only; in [RansomBatchSelection] flow, if pay_prisoner_ransom is emitted this turn, output one action per listed target in the same response.");
                changed = true;
            }

            if (rules.IndexOf("RansomBatchSelection", StringComparison.OrdinalIgnoreCase) < 0)
            {
                rules = AppendRuleLine(rules, "When [RansomBatchSelection] exists and pay_prisoner_ransom is emitted, keep total offer_silver within the batch offer window.");
                changed = true;
            }

            if (rules.IndexOf("rewrites each target offer proportionally", StringComparison.OrdinalIgnoreCase) < 0)
            {
                rules = AppendRuleLine(rules, "If [RansomBatchSelection] total offer_silver is out of batch window, execution clamps total to nearest boundary and rewrites each target offer proportionally with integer residual distribution.");
                changed = true;
            }

            if (rules.IndexOf("comms terminal", StringComparison.OrdinalIgnoreCase) < 0 &&
                rules.IndexOf("communication terminal", StringComparison.OrdinalIgnoreCase) < 0)
            {
                rules = AppendRuleLine(rules, "Dialogue is happening over the communication terminal, not offline or in-person.");
                changed = true;
            }

            if (rules.IndexOf("ransom paid/submitted", StringComparison.OrdinalIgnoreCase) < 0)
            {
                rules = AppendRuleLine(rules, "If natural language claims ransom paid/submitted, the same response must include pay_prisoner_ransom action.");
                changed = true;
            }

            if (rules.IndexOf("ransom settled/released", StringComparison.OrdinalIgnoreCase) < 0)
            {
                rules = AppendRuleLine(rules, "If natural language claims ransom settled/released, the same response must include pay_prisoner_ransom action.");
                changed = true;
            }

            const string hardRuleCurrentAskEn = "HARD RULE for pay_prisoner_ransom: when system messages provide current ask, offer_silver must equal current ask and must not reuse stale offers from memory.";
            const string hardRuleCurrentAskZh = "对 pay_prisoner_ransom（硬规则）：当系统消息给出“当前叫价”时，offer_silver 必须等于当前叫价；禁止复用历史记忆中的旧报价。";
            string rulesWithoutHardAskRule = RemoveRuleLine(RemoveRuleLine(rules, hardRuleCurrentAskEn), hardRuleCurrentAskZh);
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
            changed |= AssignIfMissing(ref config.ResponseFormat.JsonTemplate, defaults.ResponseFormat.JsonTemplate);
            changed |= AssignIfMissing(ref config.ResponseFormat.ImportantRules, defaults.ResponseFormat.ImportantRules);
            changed |= EnsureRansomImportantRules(config.ResponseFormat);
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

                changed |= AssignIfMissing(ref target.RuleContent, defRule.RuleContent);
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
            changed |= EnsureWorldviewDefaults(config.EnvironmentPrompt, defaults.EnvironmentPrompt);
            changed |= EnsureSceneDefaults(config.EnvironmentPrompt, defaults.EnvironmentPrompt);
            changed |= EnsureEnvSwitchDefaults(config.EnvironmentPrompt, defaults.EnvironmentPrompt);
            changed |= EnsureRpgSwitchDefaults(config.EnvironmentPrompt, defaults.EnvironmentPrompt);
            changed |= TryUpgradeLegacyRpgSwitchDefaults(config.EnvironmentPrompt);
            changed |= EnsureEventIntelDefaults(config.EnvironmentPrompt, defaults.EnvironmentPrompt);
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

            return AssignIfMissing(ref target.Worldview.Content, defaults.Worldview.Content);
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
            if (switches == null || !IsLegacyRpgSwitchSignature(switches))
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

            return AssignIfMissing(
                ref config.DynamicDataInjection.CustomInjectionHeader,
                defaults.DynamicDataInjection.CustomInjectionHeader);
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

            changed |= AssignIfMissing(ref target.FactGroundingTemplate, templateDefaults.FactGroundingTemplate);
            changed |= AssignIfMissing(ref target.OutputLanguageTemplate, templateDefaults.OutputLanguageTemplate);
            changed |= AssignIfMissing(ref target.DiplomacyFallbackRoleTemplate, templateDefaults.DiplomacyFallbackRoleTemplate);
            changed |= AssignIfMissing(ref target.SocialCircleActionRuleTemplate, templateDefaults.SocialCircleActionRuleTemplate);
            changed |= AssignIfMissing(ref target.SocialCircleNewsStyleTemplate, templateDefaults.SocialCircleNewsStyleTemplate);
            changed |= AssignIfMissing(ref target.SocialCircleNewsJsonContractTemplate, templateDefaults.SocialCircleNewsJsonContractTemplate);
            changed |= AssignIfMissing(ref target.SocialCircleNewsFactTemplate, templateDefaults.SocialCircleNewsFactTemplate);
            changed |= AssignIfMissing(ref target.DecisionPolicyTemplate, templateDefaults.DecisionPolicyTemplate);
            changed |= AssignIfMissing(ref target.TurnObjectiveTemplate, templateDefaults.TurnObjectiveTemplate);
            changed |= AssignIfMissing(ref target.TopicShiftRuleTemplate, templateDefaults.TopicShiftRuleTemplate);
            changed |= AssignIfMissing(ref target.RpgRoleSettingTemplate, templateDefaults.RpgRoleSettingTemplate);
            changed |= AssignIfMissing(ref target.RpgCompactFormatConstraintTemplate, templateDefaults.RpgCompactFormatConstraintTemplate);
            changed |= AssignIfMissing(ref target.RpgActionReliabilityRuleTemplate, templateDefaults.RpgActionReliabilityRuleTemplate);
            changed |= AssignIfMissing(ref target.OpeningObjectiveTemplate, templateDefaults.OpeningObjectiveTemplate);
            changed |= AssignIfMissing(ref target.ProactiveRomanceRuleTemplate, templateDefaults.ProactiveRomanceRuleTemplate);
            changed |= AssignIfMissing(ref target.ProactiveSocialActionRuleTemplate, templateDefaults.ProactiveSocialActionRuleTemplate);
            changed |= ForceRefreshRpgPromptTemplates(target);
            changed |= AssignIfMissing(ref target.ApiLimitsNodeTemplate, templateDefaults.ApiLimitsNodeTemplate);
            changed |= AssignIfMissing(ref target.QuestGuidanceNodeTemplate, templateDefaults.QuestGuidanceNodeTemplate);
            changed |= AssignIfMissing(ref target.ResponseContractNodeTemplate, templateDefaults.ResponseContractNodeTemplate);
            changed |= AssignIfMissing(ref target.MandatoryRaceInjectionTemplate, templateDefaults.MandatoryRaceInjectionTemplate);
            changed |= TryMigrateLegacyNodeBodyLiteralTemplates(target);

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
            changed |= AssignIfLessOrEqualZero(ref target.IntentActionCooldownTurns, defaultPolicy.IntentActionCooldownTurns);
            changed |= AssignIfLessOrEqualZero(ref target.IntentMinAssistantRoundsForMemory, defaultPolicy.IntentMinAssistantRoundsForMemory);
            changed |= AssignIfLessOrEqualZero(ref target.IntentNoActionStreakThreshold, defaultPolicy.IntentNoActionStreakThreshold);
            changed |= AssignIfLessOrEqualZero(ref target.SummaryTimelineTurnLimit, defaultPolicy.SummaryTimelineTurnLimit);
            changed |= AssignIfLessOrEqualZero(ref target.SummaryCharBudget, defaultPolicy.SummaryCharBudget);

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
            changed |= TryRewriteLegacyNodeTemplate(
                ref templates.ApiLimitsNodeTemplate,
                PromptTextConstants.ApiLimitsNodeLiteralDefault,
                "=== CURRENT API LIMITS (MUST FOLLOW) ===",
                "Max goodwill adjustment per call:");
            changed |= TryRewriteLegacyNodeTemplate(
                ref templates.QuestGuidanceNodeTemplate,
                PromptTextConstants.QuestGuidanceNodeLiteralDefault,
                "=== DYNAMIC QUEST AVAILABILITY (Auto-generated for current faction) ===",
                "=== QUEST TEMPLATE STRICT OVERRIDE ===");
            changed |= TryRewriteLegacyNodeTemplate(
                ref templates.QuestGuidanceNodeTemplate,
                PromptTextConstants.QuestGuidanceNodeLiteralDefault,
                "=== 动态任务可用性（按当前派系自动生成） ===",
                "=== 任务模板严格覆盖规则 ===");
            changed |= TryRewriteLegacyNodeTemplate(
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
