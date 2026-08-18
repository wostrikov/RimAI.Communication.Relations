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
    internal sealed class PromptConfigNormalizationSlice1 : PromptConfigNormalizationCollaborator
    {
        internal PromptConfigNormalizationSlice1(PromptConfigNormalization owner) : base(owner)
        {
        }

internal bool MigratePresenceBehaviorGuidance(SystemPromptConfig config)
        {
            if (config == null || string.IsNullOrWhiteSpace(config.GlobalSystemPrompt))
            {
                return false;
            }

            if (Owner.ContainsPresenceBehaviorGuidance(config.GlobalSystemPrompt))
            {
                return false;
            }

            string sectionContent = Owner.LoadPresenceBehaviorGuidanceSection();
            if (string.IsNullOrWhiteSpace(sectionContent))
            {
                return false;
            }

            int insertIndex = Owner.FindPresenceBehaviorInsertIndex(config.GlobalSystemPrompt);
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
            string extracted = Owner.ExtractPresenceBehaviorSection(defaultPrompt);
            return !string.IsNullOrWhiteSpace(extracted)
                ? extracted
                : Owner.BuildPresenceBehaviorFallbackSection();
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
            int start = Owner.FindPresenceBehaviorSectionStart(lines);
            if (start < 0)
            {
                return string.Empty;
            }

            var sectionLines = new List<string> { Owner.NormalizePresenceBehaviorSectionTitle(lines[start]) };
            for (int i = start + 1; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    continue;
                }

                if (Owner.IsPresenceBehaviorBoundary(trimmed))
                {
                    break;
                }

                if (trimmed.StartsWith("[", StringComparison.Ordinal) ||
                    trimmed.StartsWith("-", StringComparison.Ordinal))
                {
                    sectionLines.Add(trimmed);
                }
            }

            if (!Owner.ContainsPresenceBehaviorGuidance(string.Join("\n", sectionLines)))
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
            changed |= Owner.EnsureApiActionDefaults(config, defaults);
            changed |= Owner.EnsureResponseFormatDefaults(config, defaults);
            changed |= Owner.EnsureDecisionRuleDefaults(config, defaults);
            changed |= Owner.EnsureEnvironmentPromptDefaults(config, defaults);
            changed |= Owner.EnsureDynamicInjectionDefaults(config, defaults);
            changed |= Owner.EnsurePromptTemplateDefaults(config, defaults);
            changed |= Owner.EnsurePromptPolicyDefaults(config, defaults);
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
            changed |= Owner.RemoveDeprecatedPromptAction(config, "send_gift");
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

                changed |= Owner.AssignIfMissing(ref target.Description, defAction.Description);
                changed |= Owner.AssignIfMissing(ref target.Parameters, defAction.Parameters);
                changed |= Owner.AssignIfMissing(ref target.Requirement, defAction.Requirement);
                changed |= Owner.TryUpgradeLegacyMakePeaceAction(target, defAction);
                changed |= Owner.TryUpgradeRansomActionContract(target, defAction);
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
            if (Owner.IsLegacyMakePeaceDescription(target.Description) && !string.IsNullOrWhiteSpace(defAction?.Description))
            {
                target.Description = defAction.Description;
                changed = true;
            }

            if (Owner.IsLegacyMakePeaceRequirement(target.Requirement) && !string.IsNullOrWhiteSpace(defAction?.Requirement))
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
    }
}
