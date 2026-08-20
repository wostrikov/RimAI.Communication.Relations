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
using Ustas.RimAI.Core.Storage;

namespace Ustas.RimAI.Communication.Relations.Persistence
{
        internal sealed class PromptDomainStoreSerialization : PromptDomainStoreCollaborator
    {
        internal PromptDomainStoreSerialization(PromptDomainStore owner) : base(owner)
        {
        }

        internal void EnsureDirectoryExists()
        {
            try
            {
                if (!LocalStorage.Current.DirectoryExists(BasePath))
                {
                    LocalStorage.Current.CreateDirectory(BasePath);
                    Log.Message($"[RimAI.Relations] Created prompt directory: {BasePath}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAI.Relations] Failed to create directory: {ex}");
            }
        }

        internal bool HasAnyPromptCustomOverrideFile()
        {
            try
            {
                return Owner.EnumeratePromptDomainCustomOverridePaths().Any(LocalStorage.Current.FileExists);
            }
            catch
            {
                return Owner.HasAnyCustomDomainFile();
            }
        }

        internal IEnumerable<string> EnumeratePromptDomainCustomOverridePaths()
        {
            yield return PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.SystemPromptCustomFileName);
            yield return PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.DiplomacyPromptCustomFileName);
            yield return PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.PawnPromptCustomFileName);
            yield return PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.SocialCirclePromptCustomFileName);
        }

        internal SystemPromptConfig CreateDefaultConfig()
        {
            if (Owner.TryLoadPromptDomains(
                includeCustom: false,
                out SystemPromptConfig domainConfig,
                out _,
                out List<string> validationErrors))
            {
                return domainConfig;
            }

            string validationSummary = validationErrors != null && validationErrors.Count > 0
                ? string.Join(", ", validationErrors)
                : "unknown";
            Log.Error("[RimAI.Relations] Default-only domain load failed semantic validation: " + validationSummary);
            Log.Error("[RimAI.Relations] Default-only domain diagnostics: "
                + Owner.BuildDefaultDomainDiagnosticSnapshot());

            if (_cachedConfig != null && !Owner.IsPlaceholderGlobalSystemPrompt(_cachedConfig))
            {
                _hasPendingPromptDomainRepairs = false;
                Log.Warning("[RimAI.Relations] Default-only recovery failed. Keeping cached config and blocking auto-heal writeback.");
                return _cachedConfig;
            }

            throw Owner.CreateDefaultConfigLoadFailureException(validationSummary, null);
        }

        internal PromptRenderException CreateDefaultConfigLoadFailureException(string reason, Exception innerException)
        {
            var diagnostic = new PromptRenderDiagnostic
            {
                ErrorCode = PromptRenderErrorCode.TemplateMissing,
                Message = "Default prompt-domain configuration is invalid: " + (reason ?? "unknown"),
                Line = 0,
                Column = 0
            };
            return new PromptRenderException("prompt_domain_default_only", "system", diagnostic, innerException);
        }

        internal string BuildDefaultDomainDiagnosticSnapshot()
        {
            string systemPath = PromptDomainFileCatalog.GetDefaultPath(PromptDomainFileCatalog.SystemPromptDefaultFileName);
            string diplomacyPath = PromptDomainFileCatalog.GetDefaultPath(PromptDomainFileCatalog.DiplomacyPromptDefaultFileName);
            string pawnPath = PromptDomainFileCatalog.GetDefaultPath(PromptDomainFileCatalog.PawnPromptDefaultFileName);
            string socialPath = PromptDomainFileCatalog.GetDefaultPath(PromptDomainFileCatalog.SocialCirclePromptDefaultFileName);
            return $"System={Owner.BuildPathSummary(systemPath)}; "
                + $"Diplomacy={Owner.BuildPathSummary(diplomacyPath)}; "
                + $"Pawn={Owner.BuildPathSummary(pawnPath)}; "
                + $"Social={Owner.BuildPathSummary(socialPath)}";
        }

        internal string BuildPathSummary(string path)
        {
            bool exists = !string.IsNullOrWhiteSpace(path) && LocalStorage.Current.FileExists(path);
            return $"{(exists ? "exists" : "missing")}:{path}";
        }


        internal string SerializeConfigToJson(SystemPromptConfig config, bool prettyPrint = false)
        {
            if (_configJsonCodec.TrySerialize(config, prettyPrint, out string typedJson)
                && Owner.IsTypedJsonComplete(typedJson, config))
            {
                return typedJson;
            }

            var sb = new StringBuilder();

            if (prettyPrint)
            {
                sb.AppendLine("{");
                sb.AppendLine($"  \"ConfigName\": \"{Owner.EscapeJson(config.ConfigName)}\",");
                sb.AppendLine($"  \"GlobalSystemPrompt\": \"{Owner.EscapeJson(config.GlobalSystemPrompt)}\",");
                sb.AppendLine($"  \"GlobalDialoguePrompt\": \"{Owner.EscapeJson(config.GlobalDialoguePrompt)}\",");
                sb.AppendLine($"  \"UseAdvancedMode\": {config.UseAdvancedMode.ToString().ToLower()},");
                sb.AppendLine($"  \"UseHierarchicalPromptFormat\": {config.UseHierarchicalPromptFormat.ToString().ToLower()},");
                sb.AppendLine($"  \"PromptSchemaVersion\": {config.PromptSchemaVersion},");
                sb.AppendLine($"  \"PromptPolicySchemaVersion\": {config.PromptPolicySchemaVersion},");
                sb.AppendLine($"  \"Enabled\": {config.Enabled.ToString().ToLower()},");
            }
            else
            {
                sb.Append("{");
                sb.Append($"\"ConfigName\":\"{Owner.EscapeJson(config.ConfigName)}\",");
                sb.Append($"\"GlobalSystemPrompt\":\"{Owner.EscapeJson(config.GlobalSystemPrompt)}\",");
                sb.Append($"\"GlobalDialoguePrompt\":\"{Owner.EscapeJson(config.GlobalDialoguePrompt)}\",");
                sb.Append($"\"UseAdvancedMode\":{config.UseAdvancedMode.ToString().ToLower()},");
                sb.Append($"\"UseHierarchicalPromptFormat\":{config.UseHierarchicalPromptFormat.ToString().ToLower()},");
                sb.Append($"\"PromptSchemaVersion\":{config.PromptSchemaVersion},");
                sb.Append($"\"PromptPolicySchemaVersion\":{config.PromptPolicySchemaVersion},");
                sb.Append($"\"Enabled\":{config.Enabled.ToString().ToLower()},");
            }

            Owner.SerializeApiActions(sb, config.ApiActions, prettyPrint);
            Owner.SerializeResponseFormat(sb, config.ResponseFormat, prettyPrint);
            Owner.SerializeDecisionRules(sb, config.DecisionRules, prettyPrint);
            Owner.SerializeEnvironmentPrompt(sb, config.EnvironmentPrompt, prettyPrint);
            Owner.SerializePromptTemplates(sb, config.PromptTemplates, prettyPrint);
            Owner.SerializePromptPolicy(sb, config.PromptPolicy, prettyPrint);
            Owner.SerializeDynamicDataInjection(sb, config.DynamicDataInjection, prettyPrint);

            if (prettyPrint)
            {
                sb.AppendLine();
                sb.Append("}");
            }
            else
            {
                sb.Append("}");
            }

            return sb.ToString();
        }

        internal bool IsTypedJsonComplete(string json, SystemPromptConfig config)
        {
            if (string.IsNullOrWhiteSpace(json) || config == null)
            {
                return false;
            }

            if (config.ApiActions != null && config.ApiActions.Count > 0
                && json.IndexOf("\"ApiActions\"", StringComparison.Ordinal) < 0)
            {
                return false;
            }

            if (config.ResponseFormat != null
                && json.IndexOf("\"ResponseFormat\"", StringComparison.Ordinal) < 0)
            {
                return false;
            }

            if (config.PromptTemplates != null
                && json.IndexOf("\"PromptTemplates\"", StringComparison.Ordinal) < 0)
            {
                return false;
            }

            if (config.PromptPolicy != null
                && json.IndexOf("\"PromptPolicy\"", StringComparison.Ordinal) < 0)
            {
                return false;
            }

            return true;
        }

        internal void SerializeApiActions(StringBuilder sb, List<ApiActionConfig> actions, bool prettyPrint)
        {
            if (prettyPrint)
            {
                sb.AppendLine();
                sb.AppendLine("  \"ApiActions\": [");
                for (int i = 0; i < actions.Count; i++)
                {
                    var action = actions[i];
                    sb.AppendLine("    {");
                    sb.AppendLine($"      \"ActionName\": \"{Owner.EscapeJson(action.ActionName)}\",");
                    sb.AppendLine($"      \"Description\": \"{Owner.EscapeJson(action.Description)}\",");
                    sb.AppendLine($"      \"Parameters\": \"{Owner.EscapeJson(action.Parameters)}\",");
                    sb.AppendLine($"      \"Requirement\": \"{Owner.EscapeJson(action.Requirement)}\",");
                    sb.AppendLine($"      \"IsEnabled\": {action.IsEnabled.ToString().ToLower()}");
                    sb.Append(i < actions.Count - 1 ? "    }," : "    }");
                    sb.AppendLine();
                }
                sb.Append("  ],");
            }
            else
            {
                sb.Append(",\"ApiActions\":[");
                for (int i = 0; i < actions.Count; i++)
                {
                    var action = actions[i];
                    sb.Append("{");
                    sb.Append($"\"ActionName\":\"{Owner.EscapeJson(action.ActionName)}\",");
                    sb.Append($"\"Description\":\"{Owner.EscapeJson(action.Description)}\",");
                    sb.Append($"\"Parameters\":\"{Owner.EscapeJson(action.Parameters)}\",");
                    sb.Append($"\"Requirement\":\"{Owner.EscapeJson(action.Requirement)}\",");
                    sb.Append($"\"IsEnabled\":{action.IsEnabled.ToString().ToLower()}");
                    sb.Append(i < actions.Count - 1 ? "}," : "}");
                }
                sb.Append("],");
            }
        }

        internal void SerializeResponseFormat(StringBuilder sb, ResponseFormatConfig format, bool prettyPrint)
        {
            if (format == null) return;

            if (prettyPrint)
            {
                sb.AppendLine();
                sb.AppendLine("  \"ResponseFormat\": {");
                sb.AppendLine($"    \"JsonTemplate\": \"{Owner.EscapeJson(format.JsonTemplate)}\",");
                sb.AppendLine($"    \"ImportantRules\": \"{Owner.EscapeJson(format.ImportantRules)}\"");
                sb.Append("  },");
            }
            else
            {
                sb.Append(",\"ResponseFormat\":{");
                sb.Append($"\"JsonTemplate\":\"{Owner.EscapeJson(format.JsonTemplate)}\",");
                sb.Append($"\"ImportantRules\":\"{Owner.EscapeJson(format.ImportantRules)}\"");
                sb.Append("},");
            }
        }

        internal void SerializeDecisionRules(StringBuilder sb, List<DecisionRuleConfig> rules, bool prettyPrint)
        {
            if (prettyPrint)
            {
                sb.AppendLine();
                sb.AppendLine("  \"DecisionRules\": [");
                for (int i = 0; i < rules.Count; i++)
                {
                    var rule = rules[i];
                    sb.AppendLine("    {");
                    sb.AppendLine($"      \"RuleName\": \"{Owner.EscapeJson(rule.RuleName)}\",");
                    sb.AppendLine($"      \"RuleContent\": \"{Owner.EscapeJson(rule.RuleContent)}\",");
                    sb.AppendLine($"      \"IsEnabled\": {rule.IsEnabled.ToString().ToLower()}");
                    sb.Append(i < rules.Count - 1 ? "    }," : "    }");
                    sb.AppendLine();
                }
                sb.Append("  ],");
            }
            else
            {
                sb.Append(",\"DecisionRules\":[");
                for (int i = 0; i < rules.Count; i++)
                {
                    var rule = rules[i];
                    sb.Append("{");
                    sb.Append($"\"RuleName\":\"{Owner.EscapeJson(rule.RuleName)}\",");
                    sb.Append($"\"RuleContent\":\"{Owner.EscapeJson(rule.RuleContent)}\",");
                    sb.Append($"\"IsEnabled\":{rule.IsEnabled.ToString().ToLower()}");
                    sb.Append(i < rules.Count - 1 ? "}," : "}");
                }
                sb.Append("],");
            }
        }

        internal void SerializeDynamicDataInjection(StringBuilder sb, DynamicDataInjectionConfig config, bool prettyPrint)
        {
            if (config == null) return;

            if (prettyPrint)
            {
                sb.AppendLine();
                sb.AppendLine("  \"DynamicDataInjection\": {");
                sb.AppendLine($"    \"InjectMemoryData\": {config.InjectMemoryData.ToString().ToLower()},");
                sb.AppendLine($"    \"InjectFactionInfo\": {config.InjectFactionInfo.ToString().ToLower()},");
                sb.AppendLine($"    \"CustomInjectionHeader\": \"{Owner.EscapeJson(config.CustomInjectionHeader)}\"");
                sb.Append("  }");
            }
            else
            {
                sb.Append(",\"DynamicDataInjection\":{");
                sb.Append($"\"InjectMemoryData\":{config.InjectMemoryData.ToString().ToLower()},");
                sb.Append($"\"InjectFactionInfo\":{config.InjectFactionInfo.ToString().ToLower()},");
                sb.Append($"\"CustomInjectionHeader\":\"{Owner.EscapeJson(config.CustomInjectionHeader)}\"");
                sb.Append("}");
            }
        }

        internal void SerializePromptTemplates(StringBuilder sb, PromptTemplateTextConfig templates, bool prettyPrint)
        {
            if (templates == null)
            {
                return;
            }

            if (prettyPrint)
            {
                sb.AppendLine();
                sb.AppendLine("  \"PromptTemplates\": {");
                sb.AppendLine($"    \"Enabled\": {templates.Enabled.ToString().ToLower()},");
                sb.AppendLine($"    \"FactGroundingTemplate\": \"{Owner.EscapeJson(templates.FactGroundingTemplate)}\",");
                sb.AppendLine($"    \"OutputLanguageTemplate\": \"{Owner.EscapeJson(templates.OutputLanguageTemplate)}\",");
                sb.AppendLine($"    \"DiplomacyFallbackRoleTemplate\": \"{Owner.EscapeJson(templates.DiplomacyFallbackRoleTemplate)}\",");
                sb.AppendLine($"    \"SocialCircleActionRuleTemplate\": \"{Owner.EscapeJson(templates.SocialCircleActionRuleTemplate)}\",");
                sb.AppendLine($"    \"SocialCircleNewsStyleTemplate\": \"{Owner.EscapeJson(templates.SocialCircleNewsStyleTemplate)}\",");
                sb.AppendLine($"    \"SocialCircleNewsJsonContractTemplate\": \"{Owner.EscapeJson(templates.SocialCircleNewsJsonContractTemplate)}\",");
                sb.AppendLine($"    \"SocialCircleNewsFactTemplate\": \"{Owner.EscapeJson(templates.SocialCircleNewsFactTemplate)}\",");
                sb.AppendLine($"    \"DecisionPolicyTemplate\": \"{Owner.EscapeJson(templates.DecisionPolicyTemplate)}\",");
                sb.AppendLine($"    \"TurnObjectiveTemplate\": \"{Owner.EscapeJson(templates.TurnObjectiveTemplate)}\",");
                sb.AppendLine($"    \"TopicShiftRuleTemplate\": \"{Owner.EscapeJson(templates.TopicShiftRuleTemplate)}\",");
                sb.AppendLine($"    \"ApiLimitsNodeTemplate\": \"{Owner.EscapeJson(templates.ApiLimitsNodeTemplate)}\",");
                sb.AppendLine($"    \"QuestGuidanceNodeTemplate\": \"{Owner.EscapeJson(templates.QuestGuidanceNodeTemplate)}\",");
                sb.AppendLine($"    \"ResponseContractNodeTemplate\": \"{Owner.EscapeJson(templates.ResponseContractNodeTemplate)}\"");
                sb.Append("  },");
            }
            else
            {
                sb.Append(",\"PromptTemplates\":{");
                sb.Append($"\"Enabled\":{templates.Enabled.ToString().ToLower()},");
                sb.Append($"\"FactGroundingTemplate\":\"{Owner.EscapeJson(templates.FactGroundingTemplate)}\",");
                sb.Append($"\"OutputLanguageTemplate\":\"{Owner.EscapeJson(templates.OutputLanguageTemplate)}\",");
                sb.Append($"\"DiplomacyFallbackRoleTemplate\":\"{Owner.EscapeJson(templates.DiplomacyFallbackRoleTemplate)}\",");
                sb.Append($"\"SocialCircleActionRuleTemplate\":\"{Owner.EscapeJson(templates.SocialCircleActionRuleTemplate)}\",");
                sb.Append($"\"SocialCircleNewsStyleTemplate\":\"{Owner.EscapeJson(templates.SocialCircleNewsStyleTemplate)}\",");
                sb.Append($"\"SocialCircleNewsJsonContractTemplate\":\"{Owner.EscapeJson(templates.SocialCircleNewsJsonContractTemplate)}\",");
                sb.Append($"\"SocialCircleNewsFactTemplate\":\"{Owner.EscapeJson(templates.SocialCircleNewsFactTemplate)}\",");
                sb.Append($"\"DecisionPolicyTemplate\":\"{Owner.EscapeJson(templates.DecisionPolicyTemplate)}\",");
                sb.Append($"\"TurnObjectiveTemplate\":\"{Owner.EscapeJson(templates.TurnObjectiveTemplate)}\",");
                sb.Append($"\"TopicShiftRuleTemplate\":\"{Owner.EscapeJson(templates.TopicShiftRuleTemplate)}\",");
                sb.Append($"\"ApiLimitsNodeTemplate\":\"{Owner.EscapeJson(templates.ApiLimitsNodeTemplate)}\",");
                sb.Append($"\"QuestGuidanceNodeTemplate\":\"{Owner.EscapeJson(templates.QuestGuidanceNodeTemplate)}\",");
                sb.Append($"\"ResponseContractNodeTemplate\":\"{Owner.EscapeJson(templates.ResponseContractNodeTemplate)}\"");
                sb.Append("},");
            }
        }

        internal void SerializePromptPolicy(StringBuilder sb, PromptPolicyConfig policy, bool prettyPrint)
        {
            policy ??= PromptPolicyConfig.CreateDefault();

            if (prettyPrint)
            {
                sb.AppendLine();
                sb.AppendLine("  \"PromptPolicy\": {");
                sb.AppendLine($"    \"Enabled\": {policy.Enabled.ToString().ToLower()},");
                sb.AppendLine($"    \"EnableIntentDrivenActionMapping\": {policy.EnableIntentDrivenActionMapping.ToString().ToLower()},");
                sb.AppendLine($"    \"IntentActionCooldownTurns\": {policy.IntentActionCooldownTurns},");
                sb.AppendLine($"    \"IntentMinAssistantRoundsForMemory\": {policy.IntentMinAssistantRoundsForMemory},");
                sb.AppendLine($"    \"IntentNoActionStreakThreshold\": {policy.IntentNoActionStreakThreshold},");
                sb.AppendLine($"    \"ResetPromptCustomOnSchemaUpgrade\": {policy.ResetPromptCustomOnSchemaUpgrade.ToString().ToLower()},");
                sb.AppendLine($"    \"SummaryTimelineTurnLimit\": {policy.SummaryTimelineTurnLimit},");
                sb.AppendLine($"    \"SummaryCharBudget\": {policy.SummaryCharBudget}");
                sb.Append("  },");
            }
            else
            {
                sb.Append(",\"PromptPolicy\":{");
                sb.Append($"\"Enabled\":{policy.Enabled.ToString().ToLower()},");
                sb.Append($"\"EnableIntentDrivenActionMapping\":{policy.EnableIntentDrivenActionMapping.ToString().ToLower()},");
                sb.Append($"\"IntentActionCooldownTurns\":{policy.IntentActionCooldownTurns},");
                sb.Append($"\"IntentMinAssistantRoundsForMemory\":{policy.IntentMinAssistantRoundsForMemory},");
                sb.Append($"\"IntentNoActionStreakThreshold\":{policy.IntentNoActionStreakThreshold},");
                sb.Append($"\"ResetPromptCustomOnSchemaUpgrade\":{policy.ResetPromptCustomOnSchemaUpgrade.ToString().ToLower()},");
                sb.Append($"\"SummaryTimelineTurnLimit\":{policy.SummaryTimelineTurnLimit},");
                sb.Append($"\"SummaryCharBudget\":{policy.SummaryCharBudget}");
                sb.Append("},");
            }
        }

        internal void SerializeEnvironmentPrompt(StringBuilder sb, EnvironmentPromptConfig environment, bool prettyPrint)
        {
            if (environment == null)
            {
                environment = new EnvironmentPromptConfig();
            }

            if (prettyPrint)
            {
                sb.AppendLine();
                sb.AppendLine("  \"EnvironmentPrompt\": {");
                sb.AppendLine("    \"Worldview\": {");
                sb.AppendLine($"      \"Enabled\": {(environment.Worldview?.Enabled ?? true).ToString().ToLower()},");
                sb.AppendLine($"      \"Content\": \"{Owner.EscapeJson(environment.Worldview?.Content ?? string.Empty)}\"");
                sb.AppendLine("    },");
                sb.AppendLine("    \"SceneSystem\": {");
                sb.AppendLine($"      \"Enabled\": {(environment.SceneSystem?.Enabled ?? true).ToString().ToLower()},");
                sb.AppendLine($"      \"MaxSceneChars\": {environment.SceneSystem?.MaxSceneChars ?? 1200},");
                sb.AppendLine($"      \"MaxTotalChars\": {environment.SceneSystem?.MaxTotalChars ?? 4000},");
                sb.AppendLine($"      \"PresetTagsEnabled\": {(environment.SceneSystem?.PresetTagsEnabled ?? true).ToString().ToLower()}");
                sb.AppendLine("    },");
                sb.AppendLine("    \"SceneEntries\": [");

                List<ScenePromptEntryConfig> entries = environment.SceneEntries ?? new List<ScenePromptEntryConfig>();
                for (int i = 0; i < entries.Count; i++)
                {
                    ScenePromptEntryConfig entry = entries[i] ?? new ScenePromptEntryConfig();
                    sb.AppendLine("      {");
                    sb.AppendLine($"        \"Id\": \"{Owner.EscapeJson(entry.Id ?? string.Empty)}\",");
                    sb.AppendLine($"        \"Name\": \"{Owner.EscapeJson(entry.Name ?? string.Empty)}\",");
                    sb.AppendLine($"        \"Enabled\": {entry.Enabled.ToString().ToLower()},");
                    sb.AppendLine($"        \"ApplyToDiplomacy\": {entry.ApplyToDiplomacy.ToString().ToLower()},");
                    sb.AppendLine($"        \"ApplyToRPG\": {entry.ApplyToRPG.ToString().ToLower()},");
                    sb.AppendLine($"        \"Priority\": {entry.Priority},");
                    sb.AppendLine($"        \"MatchTags\": {Owner.SerializeStringList(entry.MatchTags)},");
                    sb.AppendLine($"        \"Content\": \"{Owner.EscapeJson(entry.Content ?? string.Empty)}\"");
                    sb.Append(i < entries.Count - 1 ? "      }," : "      }");
                    sb.AppendLine();
                }

                sb.AppendLine("    ],");
                sb.AppendLine("    \"EnvironmentContextSwitches\": {");
                sb.AppendLine($"      \"Enabled\": {(environment.EnvironmentContextSwitches?.Enabled ?? true).ToString().ToLower()},");
                sb.AppendLine($"      \"IncludeTime\": {(environment.EnvironmentContextSwitches?.IncludeTime ?? true).ToString().ToLower()},");
                sb.AppendLine($"      \"IncludeDate\": {(environment.EnvironmentContextSwitches?.IncludeDate ?? false).ToString().ToLower()},");
                sb.AppendLine($"      \"IncludeSeason\": {(environment.EnvironmentContextSwitches?.IncludeSeason ?? true).ToString().ToLower()},");
                sb.AppendLine($"      \"IncludeWeather\": {(environment.EnvironmentContextSwitches?.IncludeWeather ?? true).ToString().ToLower()},");
                sb.AppendLine($"      \"IncludeLocationAndTemperature\": {(environment.EnvironmentContextSwitches?.IncludeLocationAndTemperature ?? true).ToString().ToLower()},");
                sb.AppendLine($"      \"IncludeTerrain\": {(environment.EnvironmentContextSwitches?.IncludeTerrain ?? false).ToString().ToLower()},");
                sb.AppendLine($"      \"IncludeBeauty\": {(environment.EnvironmentContextSwitches?.IncludeBeauty ?? false).ToString().ToLower()},");
                sb.AppendLine($"      \"IncludeCleanliness\": {(environment.EnvironmentContextSwitches?.IncludeCleanliness ?? false).ToString().ToLower()},");
                sb.AppendLine($"      \"IncludeSurroundings\": {(environment.EnvironmentContextSwitches?.IncludeSurroundings ?? false).ToString().ToLower()},");
                sb.AppendLine($"      \"IncludeWealth\": {(environment.EnvironmentContextSwitches?.IncludeWealth ?? false).ToString().ToLower()}");
                sb.AppendLine("    },");
                sb.AppendLine("    \"RpgSceneParamSwitches\": {");
                sb.AppendLine($"      \"IncludeSkills\": {(environment.RpgSceneParamSwitches?.IncludeSkills ?? true).ToString().ToLower()},");
                sb.AppendLine($"      \"IncludeEquipment\": {(environment.RpgSceneParamSwitches?.IncludeEquipment ?? true).ToString().ToLower()},");
                sb.AppendLine($"      \"IncludeGenes\": {(environment.RpgSceneParamSwitches?.IncludeGenes ?? true).ToString().ToLower()},");
                sb.AppendLine($"      \"IncludeNeeds\": {(environment.RpgSceneParamSwitches?.IncludeNeeds ?? true).ToString().ToLower()},");
                sb.AppendLine($"      \"IncludeHediffs\": {(environment.RpgSceneParamSwitches?.IncludeHediffs ?? true).ToString().ToLower()},");
                sb.AppendLine($"      \"IncludeRecentEvents\": {(environment.RpgSceneParamSwitches?.IncludeRecentEvents ?? true).ToString().ToLower()},");
                sb.AppendLine($"      \"IncludeColonyInventorySummary\": {(environment.RpgSceneParamSwitches?.IncludeColonyInventorySummary ?? true).ToString().ToLower()},");
                sb.AppendLine($"      \"IncludeHomeAlerts\": {(environment.RpgSceneParamSwitches?.IncludeHomeAlerts ?? true).ToString().ToLower()},");
                sb.AppendLine($"      \"IncludeRecentJobState\": {(environment.RpgSceneParamSwitches?.IncludeRecentJobState ?? true).ToString().ToLower()},");
                sb.AppendLine($"      \"IncludeAttributeLevels\": {(environment.RpgSceneParamSwitches?.IncludeAttributeLevels ?? true).ToString().ToLower()}");
                sb.AppendLine("    },");
                sb.AppendLine("    \"EventIntelPrompt\": {");
                sb.AppendLine($"      \"Enabled\": {(environment.EventIntelPrompt?.Enabled ?? true).ToString().ToLower()},");
                sb.AppendLine($"      \"ApplyToDiplomacy\": {(environment.EventIntelPrompt?.ApplyToDiplomacy ?? true).ToString().ToLower()},");
                sb.AppendLine($"      \"ApplyToRpg\": {(environment.EventIntelPrompt?.ApplyToRpg ?? true).ToString().ToLower()},");
                sb.AppendLine($"      \"IncludeMapEvents\": {(environment.EventIntelPrompt?.IncludeMapEvents ?? true).ToString().ToLower()},");
                sb.AppendLine($"      \"IncludeRaidBattleReports\": {(environment.EventIntelPrompt?.IncludeRaidBattleReports ?? true).ToString().ToLower()},");
                sb.AppendLine($"      \"DaysWindow\": {environment.EventIntelPrompt?.DaysWindow ?? 15},");
                sb.AppendLine($"      \"MaxStoredRecords\": {environment.EventIntelPrompt?.MaxStoredRecords ?? 50},");
                sb.AppendLine($"      \"MaxInjectedItems\": {environment.EventIntelPrompt?.MaxInjectedItems ?? 8},");
                sb.AppendLine($"      \"MaxInjectedChars\": {environment.EventIntelPrompt?.MaxInjectedChars ?? 1200}");
                sb.Append("    }");
                sb.AppendLine();
                sb.Append("  },");
            }
            else
            {
                sb.Append(",\"EnvironmentPrompt\":{");
                sb.Append("\"Worldview\":{");
                sb.Append($"\"Enabled\":{(environment.Worldview?.Enabled ?? true).ToString().ToLower()},");
                sb.Append($"\"Content\":\"{Owner.EscapeJson(environment.Worldview?.Content ?? string.Empty)}\"");
                sb.Append("},");
                sb.Append("\"SceneSystem\":{");
                sb.Append($"\"Enabled\":{(environment.SceneSystem?.Enabled ?? true).ToString().ToLower()},");
                sb.Append($"\"MaxSceneChars\":{environment.SceneSystem?.MaxSceneChars ?? 1200},");
                sb.Append($"\"MaxTotalChars\":{environment.SceneSystem?.MaxTotalChars ?? 4000},");
                sb.Append($"\"PresetTagsEnabled\":{(environment.SceneSystem?.PresetTagsEnabled ?? true).ToString().ToLower()}");
                sb.Append("},");
                sb.Append("\"SceneEntries\":[");

                List<ScenePromptEntryConfig> entries = environment.SceneEntries ?? new List<ScenePromptEntryConfig>();
                for (int i = 0; i < entries.Count; i++)
                {
                    ScenePromptEntryConfig entry = entries[i] ?? new ScenePromptEntryConfig();
                    sb.Append("{");
                    sb.Append($"\"Id\":\"{Owner.EscapeJson(entry.Id ?? string.Empty)}\",");
                    sb.Append($"\"Name\":\"{Owner.EscapeJson(entry.Name ?? string.Empty)}\",");
                    sb.Append($"\"Enabled\":{entry.Enabled.ToString().ToLower()},");
                    sb.Append($"\"ApplyToDiplomacy\":{entry.ApplyToDiplomacy.ToString().ToLower()},");
                    sb.Append($"\"ApplyToRPG\":{entry.ApplyToRPG.ToString().ToLower()},");
                    sb.Append($"\"Priority\":{entry.Priority},");
                    sb.Append($"\"MatchTags\":{Owner.SerializeStringList(entry.MatchTags)},");
                    sb.Append($"\"Content\":\"{Owner.EscapeJson(entry.Content ?? string.Empty)}\"");
                    sb.Append(i < entries.Count - 1 ? "}," : "}");
                }

                sb.Append("],");
                sb.Append("\"EnvironmentContextSwitches\":{");
                sb.Append($"\"Enabled\":{(environment.EnvironmentContextSwitches?.Enabled ?? true).ToString().ToLower()},");
                sb.Append($"\"IncludeTime\":{(environment.EnvironmentContextSwitches?.IncludeTime ?? true).ToString().ToLower()},");
                sb.Append($"\"IncludeDate\":{(environment.EnvironmentContextSwitches?.IncludeDate ?? false).ToString().ToLower()},");
                sb.Append($"\"IncludeSeason\":{(environment.EnvironmentContextSwitches?.IncludeSeason ?? true).ToString().ToLower()},");
                sb.Append($"\"IncludeWeather\":{(environment.EnvironmentContextSwitches?.IncludeWeather ?? true).ToString().ToLower()},");
                sb.Append($"\"IncludeLocationAndTemperature\":{(environment.EnvironmentContextSwitches?.IncludeLocationAndTemperature ?? true).ToString().ToLower()},");
                sb.Append($"\"IncludeTerrain\":{(environment.EnvironmentContextSwitches?.IncludeTerrain ?? false).ToString().ToLower()},");
                sb.Append($"\"IncludeBeauty\":{(environment.EnvironmentContextSwitches?.IncludeBeauty ?? false).ToString().ToLower()},");
                sb.Append($"\"IncludeCleanliness\":{(environment.EnvironmentContextSwitches?.IncludeCleanliness ?? false).ToString().ToLower()},");
                sb.Append($"\"IncludeSurroundings\":{(environment.EnvironmentContextSwitches?.IncludeSurroundings ?? false).ToString().ToLower()},");
                sb.Append($"\"IncludeWealth\":{(environment.EnvironmentContextSwitches?.IncludeWealth ?? false).ToString().ToLower()}");
                sb.Append("},");
                sb.Append("\"RpgSceneParamSwitches\":{");
                sb.Append($"\"IncludeSkills\":{(environment.RpgSceneParamSwitches?.IncludeSkills ?? true).ToString().ToLower()},");
                sb.Append($"\"IncludeEquipment\":{(environment.RpgSceneParamSwitches?.IncludeEquipment ?? true).ToString().ToLower()},");
                sb.Append($"\"IncludeGenes\":{(environment.RpgSceneParamSwitches?.IncludeGenes ?? true).ToString().ToLower()},");
                sb.Append($"\"IncludeNeeds\":{(environment.RpgSceneParamSwitches?.IncludeNeeds ?? true).ToString().ToLower()},");
                sb.Append($"\"IncludeHediffs\":{(environment.RpgSceneParamSwitches?.IncludeHediffs ?? true).ToString().ToLower()},");
                sb.Append($"\"IncludeRecentEvents\":{(environment.RpgSceneParamSwitches?.IncludeRecentEvents ?? true).ToString().ToLower()},");
                sb.Append($"\"IncludeColonyInventorySummary\":{(environment.RpgSceneParamSwitches?.IncludeColonyInventorySummary ?? true).ToString().ToLower()},");
                sb.Append($"\"IncludeHomeAlerts\":{(environment.RpgSceneParamSwitches?.IncludeHomeAlerts ?? true).ToString().ToLower()},");
                sb.Append($"\"IncludeRecentJobState\":{(environment.RpgSceneParamSwitches?.IncludeRecentJobState ?? true).ToString().ToLower()},");
                sb.Append($"\"IncludeAttributeLevels\":{(environment.RpgSceneParamSwitches?.IncludeAttributeLevels ?? true).ToString().ToLower()}");
                sb.Append("},");
                sb.Append("\"EventIntelPrompt\":{");
                sb.Append($"\"Enabled\":{(environment.EventIntelPrompt?.Enabled ?? true).ToString().ToLower()},");
                sb.Append($"\"ApplyToDiplomacy\":{(environment.EventIntelPrompt?.ApplyToDiplomacy ?? true).ToString().ToLower()},");
                sb.Append($"\"ApplyToRpg\":{(environment.EventIntelPrompt?.ApplyToRpg ?? true).ToString().ToLower()},");
                sb.Append($"\"IncludeMapEvents\":{(environment.EventIntelPrompt?.IncludeMapEvents ?? true).ToString().ToLower()},");
                sb.Append($"\"IncludeRaidBattleReports\":{(environment.EventIntelPrompt?.IncludeRaidBattleReports ?? true).ToString().ToLower()},");
                sb.Append($"\"DaysWindow\":{environment.EventIntelPrompt?.DaysWindow ?? 15},");
                sb.Append($"\"MaxStoredRecords\":{environment.EventIntelPrompt?.MaxStoredRecords ?? 50},");
                sb.Append($"\"MaxInjectedItems\":{environment.EventIntelPrompt?.MaxInjectedItems ?? 8},");
                sb.Append($"\"MaxInjectedChars\":{environment.EventIntelPrompt?.MaxInjectedChars ?? 1200}");
                sb.Append("}");
                sb.Append("},");
            }
        }

        internal string SerializeStringList(List<string> values)
        {
            if (values == null || values.Count == 0)
            {
                return "[]";
            }

            var sb = new StringBuilder();
            sb.Append("[");
            for (int i = 0; i < values.Count; i++)
            {
                sb.Append($"\"{Owner.EscapeJson(values[i] ?? string.Empty)}\"");
                if (i < values.Count - 1)
                {
                    sb.Append(",");
                }
            }
            sb.Append("]");
            return sb.ToString();
        }

        internal SystemPromptConfig ParseJsonToConfigInternal(string json, string sourceContext = "unknown")
        {
            string source = string.IsNullOrWhiteSpace(sourceContext) ? "unknown" : sourceContext.Trim();
            bool hasApiActionsKey = PromptJsonText.ContainsJsonKey(json, "ApiActions");
            bool hasResponseFormatKey = PromptJsonText.ContainsJsonKey(json, "ResponseFormat");
            bool hasDecisionRulesKey = PromptJsonText.ContainsJsonKey(json, "DecisionRules");
            bool hasPromptTemplatesKey = PromptJsonText.ContainsJsonKey(json, "PromptTemplates");
            bool hasSchemaAnchors =
                hasApiActionsKey ||
                hasResponseFormatKey ||
                hasDecisionRulesKey ||
                hasPromptTemplatesKey ||
                PromptJsonText.ContainsJsonKey(json, "ConfigName") ||
                PromptJsonText.ContainsJsonKey(json, "GlobalSystemPrompt") ||
                PromptJsonText.ContainsJsonKey(json, "GlobalDialoguePrompt");

            if (_configJsonCodec.TryDeserialize(json, out SystemPromptConfig typedConfig, out string typedError))
            {
                if (Owner.IsConfigStructurallyComplete(
                    typedConfig,
                    hasSchemaAnchors,
                    hasApiActionsKey,
                    hasResponseFormatKey,
                    hasDecisionRulesKey,
                    hasPromptTemplatesKey))
                {
                    return typedConfig;
                }

                Owner.LogTypedParseIncompleteWarningOnce(
                    hasSchemaAnchors,
                    hasApiActionsKey,
                    hasResponseFormatKey,
                    hasDecisionRulesKey,
                    hasPromptTemplatesKey,
                    typedConfig,
                    source);
            }

            bool hasTypedError = !string.IsNullOrWhiteSpace(typedError);

            SystemPromptConfig fallbackConfig = Owner.TryParseCurrentSchemaTextFallback(
                json,
                hasSchemaAnchors,
                hasApiActionsKey,
                hasResponseFormatKey,
                hasDecisionRulesKey,
                hasPromptTemplatesKey);
            if (fallbackConfig != null)
            {
                if (hasTypedError)
                {
                    Owner.LogTypedParseRecoveredInfoOnce(source, typedError);
                }
                else
                {
                    Log.Message($"[RimAI.Relations] Typed JSON parse was incomplete at source={source}; recovered config using current-schema text fallback.");
                }
            }
            else if (hasTypedError)
            {
                Owner.LogTypedParseFailureWarningOnce(source, typedError);
            }

            return fallbackConfig;
        }

        internal SystemPromptConfig TryParseCurrentSchemaTextFallback(
            string json,
            bool hasSchemaAnchors,
            bool hasApiActionsKey,
            bool hasResponseFormatKey,
            bool hasDecisionRulesKey,
            bool hasPromptTemplatesKey)
        {
            if (string.IsNullOrWhiteSpace(json) || !hasSchemaAnchors)
            {
                return null;
            }

            try
            {
                var config = new SystemPromptConfig
                {
                    ConfigName = Owner.ExtractString(json, "ConfigName"),
                    GlobalSystemPrompt = Owner.ExtractString(json, "GlobalSystemPrompt"),
                    GlobalDialoguePrompt = Owner.ExtractString(json, "GlobalDialoguePrompt")
                };

                string useAdvancedStr = Owner.ExtractValue(json, "UseAdvancedMode");
                if (bool.TryParse(useAdvancedStr, out bool useAdvanced))
                {
                    config.UseAdvancedMode = useAdvanced;
                }

                string useHierarchicalFormatStr = Owner.ExtractValue(json, "UseHierarchicalPromptFormat");
                if (bool.TryParse(useHierarchicalFormatStr, out bool useHierarchicalFormat))
                {
                    config.UseHierarchicalPromptFormat = useHierarchicalFormat;
                }

                string enabledStr = Owner.ExtractValue(json, "Enabled");
                if (bool.TryParse(enabledStr, out bool enabled))
                {
                    config.Enabled = enabled;
                }

                string promptSchemaVersionStr = Owner.ExtractValue(json, "PromptSchemaVersion");
                if (int.TryParse(promptSchemaVersionStr, out int promptSchemaVersion))
                {
                    config.PromptSchemaVersion = promptSchemaVersion;
                }

                string schemaVersionStr = Owner.ExtractValue(json, "PromptPolicySchemaVersion");
                if (int.TryParse(schemaVersionStr, out int schemaVersion))
                {
                    config.PromptPolicySchemaVersion = schemaVersion;
                }

                Owner.ParseApiActions(json, config);
                Owner.ParseResponseFormat(json, config);
                Owner.ParseDecisionRules(json, config);
                Owner.ParseEnvironmentPrompt(json, config);
                Owner.ParsePromptTemplates(json, config);
                Owner.ParsePromptPolicy(json, config);
                Owner.ParseDynamicDataInjection(json, config);

                return Owner.IsConfigStructurallyComplete(
                    config,
                    hasSchemaAnchors,
                    hasApiActionsKey,
                    hasResponseFormatKey,
                    hasDecisionRulesKey,
                    hasPromptTemplatesKey)
                    ? config
                    : null;
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimAI.Relations] Current-schema text fallback parse failed: {ex.Message}");
                return null;
            }
        }

        internal bool IsConfigStructurallyComplete(
            SystemPromptConfig config,
            bool hasSchemaAnchors,
            bool hasApiActionsKey,
            bool hasResponseFormatKey,
            bool hasDecisionRulesKey,
            bool hasPromptTemplatesKey)
        {
            if (config == null || !hasSchemaAnchors)
            {
                return false;
            }

            if (hasApiActionsKey && (config.ApiActions == null || config.ApiActions.Count == 0))
            {
                return false;
            }

            if (hasResponseFormatKey && config.ResponseFormat == null)
            {
                return false;
            }

            if (hasResponseFormatKey &&
                (string.IsNullOrWhiteSpace(config.ResponseFormat?.JsonTemplate) ||
                 string.IsNullOrWhiteSpace(config.ResponseFormat?.ImportantRules)))
            {
                return false;
            }

            if (hasDecisionRulesKey && (config.DecisionRules == null || config.DecisionRules.Count == 0))
            {
                return false;
            }

            if (hasPromptTemplatesKey && config.PromptTemplates == null)
            {
                return false;
            }

            return true;
        }

        internal bool ContainsJsonKey(string json, string key)
        {
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            return json.IndexOf($"\"{key}\"", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal void LogTypedParseIncompleteWarningOnce(
            bool hasSchemaAnchors,
            bool hasApiActionsKey,
            bool hasResponseFormatKey,
            bool hasDecisionRulesKey,
            bool hasPromptTemplatesKey,
            SystemPromptConfig config,
            string sourceContext)
        {
            var missing = new List<string>();
            if (!hasSchemaAnchors)
            {
                missing.Add("schema_anchor");
            }
            if (hasApiActionsKey && (config?.ApiActions == null || config.ApiActions.Count == 0))
            {
                missing.Add("ApiActions");
            }
            if (hasResponseFormatKey && config?.ResponseFormat == null)
            {
                missing.Add("ResponseFormat");
            }
            if (hasResponseFormatKey && string.IsNullOrWhiteSpace(config?.ResponseFormat?.JsonTemplate))
            {
                missing.Add("ResponseFormat.JsonTemplate");
            }
            if (hasResponseFormatKey && string.IsNullOrWhiteSpace(config?.ResponseFormat?.ImportantRules))
            {
                missing.Add("ResponseFormat.ImportantRules");
            }
            if (hasDecisionRulesKey && (config?.DecisionRules == null || config.DecisionRules.Count == 0))
            {
                missing.Add("DecisionRules");
            }
            if (hasPromptTemplatesKey && config?.PromptTemplates == null)
            {
                missing.Add("PromptTemplates");
            }

            string detail = missing.Count > 0 ? string.Join(",", missing) : "unknown";
            string source = string.IsNullOrWhiteSpace(sourceContext) ? "unknown" : sourceContext.Trim();
            string signature = $"typed_incomplete::{source}::{detail}";
            Owner.LogTypedParseWarningOnce(
                _typedParseIncompleteWarningHashes,
                signature,
                $"[RimAI.Relations] Typed JSON parse produced incomplete config at source={source} (missing: {detail}); config load rejected.");
        }

        internal void LogTypedParseFailureWarningOnce(string sourceContext, string typedError)
        {
            string source = string.IsNullOrWhiteSpace(sourceContext) ? "unknown" : sourceContext.Trim();
            string normalizedError = typedError ?? string.Empty;
            string signature = $"typed_fail::{source}::{normalizedError}";
            Owner.LogTypedParseWarningOnce(
                _typedParseFailureWarningHashes,
                signature,
                $"[RimAI.Relations] Typed JSON parse failed at source={source}; config load rejected: {normalizedError}");
        }

        internal void LogTypedParseRecoveredInfoOnce(string sourceContext, string typedError)
        {
            string source = string.IsNullOrWhiteSpace(sourceContext) ? "unknown" : sourceContext.Trim();
            string normalizedError = typedError ?? string.Empty;
            string signature = $"typed_recovered::{source}::{normalizedError}";
            Owner.LogTypedParseWarningOnce(
                _typedParseRecoveredInfoHashes,
                signature,
                $"[RimAI.Relations] Typed JSON parse failed at source={source}, but fallback recovery succeeded: {normalizedError}",
                logAsWarning: false);
        }

        internal void LogTypedParseWarningOnce(
            HashSet<int> warningHashes,
            string signature,
            string message,
            bool logAsWarning = true)
        {
            if (warningHashes == null || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            int hash = (signature ?? string.Empty).GetHashCode();
            bool shouldLog;
            lock (_typedParseWarningLock)
            {
                if (warningHashes.Count > 128)
                {
                    warningHashes.Clear();
                }

                shouldLog = warningHashes.Add(hash);
            }

            if (shouldLog)
            {
                if (logAsWarning)
                {
                    Log.Warning(message);
                }
                else
                {
                    Log.Message(message);
                }
            }
        }

        internal void ParseApiActions(string json, SystemPromptConfig config)
        {
            int actionsStart = json.IndexOf("\"ApiActions\":");
            if (actionsStart < 0) return;

            int arrayStart = json.IndexOf("[", actionsStart);
            if (arrayStart < 0) return;

            int depth = 1;
            int arrayEnd = arrayStart + 1;
            while (arrayEnd < json.Length && depth > 0)
            {
                if (json[arrayEnd] == '[') depth++;
                else if (json[arrayEnd] == ']') depth--;
                arrayEnd++;
            }

            string arrayContent = json.Substring(arrayStart + 1, arrayEnd - arrayStart - 2);
            var objects = Owner.SplitJsonObjects(arrayContent);

            foreach (var objStr in objects)
            {
                var action = new ApiActionConfig
                {
                    ActionName = Owner.ExtractString(objStr, "ActionName"),
                    Description = Owner.ExtractString(objStr, "Description"),
                    Parameters = Owner.ExtractString(objStr, "Parameters"),
                    Requirement = Owner.ExtractString(objStr, "Requirement")
                };

                string enabledStr = Owner.ExtractValue(objStr, "IsEnabled");
                if (bool.TryParse(enabledStr, out bool isEnabled))
                {
                    action.IsEnabled = isEnabled;
                }

                config.ApiActions.Add(action);
            }
        }

        internal void ParseResponseFormat(string json, SystemPromptConfig config)
        {
            int formatStart = json.IndexOf("\"ResponseFormat\":");
            if (formatStart < 0) return;

            int objStart = json.IndexOf("{", formatStart);
            if (objStart < 0) return;

            int depth = 1;
            int objEnd = objStart + 1;
            while (objEnd < json.Length && depth > 0)
            {
                if (json[objEnd] == '{') depth++;
                else if (json[objEnd] == '}') depth--;
                objEnd++;
            }

            string objContent = json.Substring(objStart, objEnd - objStart);

            config.ResponseFormat = new ResponseFormatConfig
            {
                JsonTemplate = Owner.ExtractString(objContent, "JsonTemplate"),
                ImportantRules = Owner.ExtractString(objContent, "ImportantRules")
            };
        }

        internal void ParseDecisionRules(string json, SystemPromptConfig config)
        {
            int rulesStart = json.IndexOf("\"DecisionRules\":");
            if (rulesStart < 0) return;

            int arrayStart = json.IndexOf("[", rulesStart);
            if (arrayStart < 0) return;

            int depth = 1;
            int arrayEnd = arrayStart + 1;
            while (arrayEnd < json.Length && depth > 0)
            {
                if (json[arrayEnd] == '[') depth++;
                else if (json[arrayEnd] == ']') depth--;
                arrayEnd++;
            }

            string arrayContent = json.Substring(arrayStart + 1, arrayEnd - arrayStart - 2);
            var objects = Owner.SplitJsonObjects(arrayContent);

            foreach (var objStr in objects)
            {
                var rule = new DecisionRuleConfig
                {
                    RuleName = Owner.ExtractString(objStr, "RuleName"),
                    RuleContent = Owner.ExtractString(objStr, "RuleContent")
                };

                string enabledStr = Owner.ExtractValue(objStr, "IsEnabled");
                if (bool.TryParse(enabledStr, out bool isEnabled))
                {
                    rule.IsEnabled = isEnabled;
                }

                config.DecisionRules.Add(rule);
            }
        }

        internal void ParseDynamicDataInjection(string json, SystemPromptConfig config)
        {
            int injectionStart = json.IndexOf("\"DynamicDataInjection\":");
            if (injectionStart < 0) return;

            int objStart = json.IndexOf("{", injectionStart);
            if (objStart < 0) return;

            int depth = 1;
            int objEnd = objStart + 1;
            while (objEnd < json.Length && depth > 0)
            {
                if (json[objEnd] == '{') depth++;
                else if (json[objEnd] == '}') depth--;
                objEnd++;
            }

            string objContent = json.Substring(objStart, objEnd - objStart);

            config.DynamicDataInjection = new DynamicDataInjectionConfig
            {
                CustomInjectionHeader = Owner.ExtractString(objContent, "CustomInjectionHeader")
            };

            string injectMemoryStr = Owner.ExtractValue(objContent, "InjectMemoryData");
            if (bool.TryParse(injectMemoryStr, out bool injectMemory))
            {
                config.DynamicDataInjection.InjectMemoryData = injectMemory;
            }

            string injectFactionStr = Owner.ExtractValue(objContent, "InjectFactionInfo");
            if (bool.TryParse(injectFactionStr, out bool injectFaction))
            {
                config.DynamicDataInjection.InjectFactionInfo = injectFaction;
            }
        }

        internal void ParsePromptTemplates(string json, SystemPromptConfig config)
        {
            if (!Owner.TryExtractJsonObject(json, "PromptTemplates", out string templatesContent))
            {
                if (config.PromptTemplates == null)
                {
                    config.PromptTemplates = new PromptTemplateTextConfig();
                }

                return;
            }

            config.PromptTemplates = new PromptTemplateTextConfig
            {
                FactGroundingTemplate = Owner.ExtractString(templatesContent, "FactGroundingTemplate"),
                OutputLanguageTemplate = Owner.ExtractString(templatesContent, "OutputLanguageTemplate"),
                DiplomacyFallbackRoleTemplate = Owner.ExtractString(templatesContent, "DiplomacyFallbackRoleTemplate"),
                SocialCircleActionRuleTemplate = Owner.ExtractString(templatesContent, "SocialCircleActionRuleTemplate"),
                SocialCircleNewsStyleTemplate = Owner.ExtractString(templatesContent, "SocialCircleNewsStyleTemplate"),
                SocialCircleNewsJsonContractTemplate = Owner.ExtractString(templatesContent, "SocialCircleNewsJsonContractTemplate"),
                SocialCircleNewsFactTemplate = Owner.ExtractString(templatesContent, "SocialCircleNewsFactTemplate"),
                RpgRoleSettingTemplate = Owner.ExtractString(templatesContent, "RpgRoleSettingTemplate"),
                RpgCompactFormatConstraintTemplate = Owner.ExtractString(templatesContent, "RpgCompactFormatConstraintTemplate"),
                RpgActionReliabilityRuleTemplate = Owner.ExtractString(templatesContent, "RpgActionReliabilityRuleTemplate"),
                DecisionPolicyTemplate = Owner.ExtractString(templatesContent, "DecisionPolicyTemplate"),
                TurnObjectiveTemplate = Owner.ExtractString(templatesContent, "TurnObjectiveTemplate"),
                OpeningObjectiveTemplate = Owner.ExtractString(templatesContent, "OpeningObjectiveTemplate"),
                TopicShiftRuleTemplate = Owner.ExtractString(templatesContent, "TopicShiftRuleTemplate"),
                ApiLimitsNodeTemplate = Owner.ExtractString(templatesContent, "ApiLimitsNodeTemplate"),
                QuestGuidanceNodeTemplate = Owner.ExtractString(templatesContent, "QuestGuidanceNodeTemplate"),
                ResponseContractNodeTemplate = Owner.ExtractString(templatesContent, "ResponseContractNodeTemplate"),
                MandatoryRaceInjectionTemplate = Owner.ExtractString(templatesContent, "MandatoryRaceInjectionTemplate")
            };

            string enabledStr = Owner.ExtractValue(templatesContent, "Enabled");
            if (bool.TryParse(enabledStr, out bool enabled))
            {
                config.PromptTemplates.Enabled = enabled;
            }
        }

        internal void ParsePromptPolicy(string json, SystemPromptConfig config)
        {
            if (!Owner.TryExtractJsonObject(json, "PromptPolicy", out string policyContent))
            {
                config.PromptPolicy ??= PromptPolicyConfig.CreateDefault();
                return;
            }

            var policy = PromptPolicyConfig.CreateDefault();
            string enabledStr = Owner.ExtractValue(policyContent, "Enabled");
            if (bool.TryParse(enabledStr, out bool enabled))
            {
                policy.Enabled = enabled;
            }

            string intentMappingStr = Owner.ExtractValue(policyContent, "EnableIntentDrivenActionMapping");
            if (bool.TryParse(intentMappingStr, out bool intentMapping))
            {
                policy.EnableIntentDrivenActionMapping = intentMapping;
            }

            string cooldownStr = Owner.ExtractValue(policyContent, "IntentActionCooldownTurns");
            if (int.TryParse(cooldownStr, out int cooldown))
            {
                policy.IntentActionCooldownTurns = cooldown;
            }

            string minRoundsStr = Owner.ExtractValue(policyContent, "IntentMinAssistantRoundsForMemory");
            if (int.TryParse(minRoundsStr, out int minRounds))
            {
                policy.IntentMinAssistantRoundsForMemory = minRounds;
            }

            string streakStr = Owner.ExtractValue(policyContent, "IntentNoActionStreakThreshold");
            if (int.TryParse(streakStr, out int streakThreshold))
            {
                policy.IntentNoActionStreakThreshold = streakThreshold;
            }

            string resetStr = Owner.ExtractValue(policyContent, "ResetPromptCustomOnSchemaUpgrade");
            if (bool.TryParse(resetStr, out bool reset))
            {
                policy.ResetPromptCustomOnSchemaUpgrade = reset;
            }

            string summaryLimitStr = Owner.ExtractValue(policyContent, "SummaryTimelineTurnLimit");
            if (int.TryParse(summaryLimitStr, out int summaryLimit))
            {
                policy.SummaryTimelineTurnLimit = summaryLimit;
            }

            string summaryBudgetStr = Owner.ExtractValue(policyContent, "SummaryCharBudget");
            if (int.TryParse(summaryBudgetStr, out int summaryBudget))
            {
                policy.SummaryCharBudget = summaryBudget;
            }

            config.PromptPolicy = policy;
        }

        internal void ParseEnvironmentPrompt(string json, SystemPromptConfig config)
        {
            if (!Owner.TryExtractJsonObject(json, "EnvironmentPrompt", out string envContent))
            {
                if (config.EnvironmentPrompt == null)
                {
                    config.EnvironmentPrompt = new EnvironmentPromptConfig();
                }
                return;
            }

            var environment = new EnvironmentPromptConfig();

            if (Owner.TryExtractJsonObject(envContent, "Worldview", out string worldviewContent))
            {
                environment.Worldview = new WorldviewPromptConfig
                {
                    Content = Owner.ExtractString(worldviewContent, "Content")
                };

                string enabledStr = Owner.ExtractValue(worldviewContent, "Enabled");
                if (bool.TryParse(enabledStr, out bool enabled))
                {
                    environment.Worldview.Enabled = enabled;
                }
            }

            if (Owner.TryExtractJsonObject(envContent, "SceneSystem", out string sceneSystemContent))
            {
                environment.SceneSystem = new SceneSystemPromptConfig();

                string enabledStr = Owner.ExtractValue(sceneSystemContent, "Enabled");
                if (bool.TryParse(enabledStr, out bool enabled))
                {
                    environment.SceneSystem.Enabled = enabled;
                }

                string maxSceneCharsStr = Owner.ExtractValue(sceneSystemContent, "MaxSceneChars");
                if (int.TryParse(maxSceneCharsStr, out int maxSceneChars))
                {
                    environment.SceneSystem.MaxSceneChars = maxSceneChars;
                }

                string maxTotalCharsStr = Owner.ExtractValue(sceneSystemContent, "MaxTotalChars");
                if (int.TryParse(maxTotalCharsStr, out int maxTotalChars))
                {
                    environment.SceneSystem.MaxTotalChars = maxTotalChars;
                }

                string presetTagsEnabledStr = Owner.ExtractValue(sceneSystemContent, "PresetTagsEnabled");
                if (bool.TryParse(presetTagsEnabledStr, out bool presetTagsEnabled))
                {
                    environment.SceneSystem.PresetTagsEnabled = presetTagsEnabled;
                }
            }

            if (Owner.TryExtractJsonArray(envContent, "SceneEntries", out string sceneEntriesContent))
            {
                environment.SceneEntries = new List<ScenePromptEntryConfig>();
                var objects = Owner.SplitJsonObjects(sceneEntriesContent);
                foreach (string objStr in objects)
                {
                    var entry = new ScenePromptEntryConfig
                    {
                        Id = Owner.ExtractString(objStr, "Id"),
                        Name = Owner.ExtractString(objStr, "Name"),
                        Content = Owner.ExtractString(objStr, "Content"),
                        MatchTags = Owner.ExtractStringArray(objStr, "MatchTags")
                    };

                    string enabledStr = Owner.ExtractValue(objStr, "Enabled");
                    if (bool.TryParse(enabledStr, out bool enabled))
                    {
                        entry.Enabled = enabled;
                    }

                    string applyToDiplomacyStr = Owner.ExtractValue(objStr, "ApplyToDiplomacy");
                    if (bool.TryParse(applyToDiplomacyStr, out bool applyToDiplomacy))
                    {
                        entry.ApplyToDiplomacy = applyToDiplomacy;
                    }

                    string applyToRpgStr = Owner.ExtractValue(objStr, "ApplyToRPG");
                    if (bool.TryParse(applyToRpgStr, out bool applyToRpg))
                    {
                        entry.ApplyToRPG = applyToRpg;
                    }

                    string priorityStr = Owner.ExtractValue(objStr, "Priority");
                    if (int.TryParse(priorityStr, out int priority))
                    {
                        entry.Priority = priority;
                    }

                    if (string.IsNullOrWhiteSpace(entry.Id))
                    {
                        entry.Id = Guid.NewGuid().ToString("N");
                    }

                    environment.SceneEntries.Add(entry);
                }
            }

            if (Owner.TryExtractJsonObject(envContent, "EnvironmentContextSwitches", out string environmentContextContent))
            {
                environment.EnvironmentContextSwitches = new EnvironmentContextSwitchesConfig();

                string enabledStr = Owner.ExtractValue(environmentContextContent, "Enabled");
                if (bool.TryParse(enabledStr, out bool enabled))
                {
                    environment.EnvironmentContextSwitches.Enabled = enabled;
                }

                string includeTimeStr = Owner.ExtractValue(environmentContextContent, "IncludeTime");
                if (bool.TryParse(includeTimeStr, out bool includeTime))
                {
                    environment.EnvironmentContextSwitches.IncludeTime = includeTime;
                }

                string includeDateStr = Owner.ExtractValue(environmentContextContent, "IncludeDate");
                if (bool.TryParse(includeDateStr, out bool includeDate))
                {
                    environment.EnvironmentContextSwitches.IncludeDate = includeDate;
                }

                string includeSeasonStr = Owner.ExtractValue(environmentContextContent, "IncludeSeason");
                if (bool.TryParse(includeSeasonStr, out bool includeSeason))
                {
                    environment.EnvironmentContextSwitches.IncludeSeason = includeSeason;
                }

                string includeWeatherStr = Owner.ExtractValue(environmentContextContent, "IncludeWeather");
                if (bool.TryParse(includeWeatherStr, out bool includeWeather))
                {
                    environment.EnvironmentContextSwitches.IncludeWeather = includeWeather;
                }

                string includeLocationAndTemperatureStr = Owner.ExtractValue(environmentContextContent, "IncludeLocationAndTemperature");
                if (bool.TryParse(includeLocationAndTemperatureStr, out bool includeLocationAndTemperature))
                {
                    environment.EnvironmentContextSwitches.IncludeLocationAndTemperature = includeLocationAndTemperature;
                }

                string includeTerrainStr = Owner.ExtractValue(environmentContextContent, "IncludeTerrain");
                if (bool.TryParse(includeTerrainStr, out bool includeTerrain))
                {
                    environment.EnvironmentContextSwitches.IncludeTerrain = includeTerrain;
                }

                string includeBeautyStr = Owner.ExtractValue(environmentContextContent, "IncludeBeauty");
                if (bool.TryParse(includeBeautyStr, out bool includeBeauty))
                {
                    environment.EnvironmentContextSwitches.IncludeBeauty = includeBeauty;
                }

                string includeCleanlinessStr = Owner.ExtractValue(environmentContextContent, "IncludeCleanliness");
                if (bool.TryParse(includeCleanlinessStr, out bool includeCleanliness))
                {
                    environment.EnvironmentContextSwitches.IncludeCleanliness = includeCleanliness;
                }

                string includeSurroundingsStr = Owner.ExtractValue(environmentContextContent, "IncludeSurroundings");
                if (bool.TryParse(includeSurroundingsStr, out bool includeSurroundings))
                {
                    environment.EnvironmentContextSwitches.IncludeSurroundings = includeSurroundings;
                }

                string includeWealthStr = Owner.ExtractValue(environmentContextContent, "IncludeWealth");
                if (bool.TryParse(includeWealthStr, out bool includeWealth))
                {
                    environment.EnvironmentContextSwitches.IncludeWealth = includeWealth;
                }
            }

            if (Owner.TryExtractJsonObject(envContent, "RpgSceneParamSwitches", out string rpgSwitchContent))
            {
                environment.RpgSceneParamSwitches = new RpgSceneParamSwitchesConfig();

                string includeSkillsStr = Owner.ExtractValue(rpgSwitchContent, "IncludeSkills");
                if (bool.TryParse(includeSkillsStr, out bool includeSkills))
                {
                    environment.RpgSceneParamSwitches.IncludeSkills = includeSkills;
                }

                string includeEquipmentStr = Owner.ExtractValue(rpgSwitchContent, "IncludeEquipment");
                if (bool.TryParse(includeEquipmentStr, out bool includeEquipment))
                {
                    environment.RpgSceneParamSwitches.IncludeEquipment = includeEquipment;
                }

                string includeGenesStr = Owner.ExtractValue(rpgSwitchContent, "IncludeGenes");
                if (bool.TryParse(includeGenesStr, out bool includeGenes))
                {
                    environment.RpgSceneParamSwitches.IncludeGenes = includeGenes;
                }

                string includeNeedsStr = Owner.ExtractValue(rpgSwitchContent, "IncludeNeeds");
                if (bool.TryParse(includeNeedsStr, out bool includeNeeds))
                {
                    environment.RpgSceneParamSwitches.IncludeNeeds = includeNeeds;
                }

                string includeHediffsStr = Owner.ExtractValue(rpgSwitchContent, "IncludeHediffs");
                if (bool.TryParse(includeHediffsStr, out bool includeHediffs))
                {
                    environment.RpgSceneParamSwitches.IncludeHediffs = includeHediffs;
                }

                string includeRecentEventsStr = Owner.ExtractValue(rpgSwitchContent, "IncludeRecentEvents");
                if (bool.TryParse(includeRecentEventsStr, out bool includeRecentEvents))
                {
                    environment.RpgSceneParamSwitches.IncludeRecentEvents = includeRecentEvents;
                }

                string includeInventorySummaryStr = Owner.ExtractValue(rpgSwitchContent, "IncludeColonyInventorySummary");
                if (bool.TryParse(includeInventorySummaryStr, out bool includeInventorySummary))
                {
                    environment.RpgSceneParamSwitches.IncludeColonyInventorySummary = includeInventorySummary;
                }

                string includeHomeAlertsStr = Owner.ExtractValue(rpgSwitchContent, "IncludeHomeAlerts");
                if (bool.TryParse(includeHomeAlertsStr, out bool includeHomeAlerts))
                {
                    environment.RpgSceneParamSwitches.IncludeHomeAlerts = includeHomeAlerts;
                }

                string includeRecentJobStateStr = Owner.ExtractValue(rpgSwitchContent, "IncludeRecentJobState");
                if (bool.TryParse(includeRecentJobStateStr, out bool includeRecentJobState))
                {
                    environment.RpgSceneParamSwitches.IncludeRecentJobState = includeRecentJobState;
                }

                string includeAttributeLevelsStr = Owner.ExtractValue(rpgSwitchContent, "IncludeAttributeLevels");
                if (bool.TryParse(includeAttributeLevelsStr, out bool includeAttributeLevels))
                {
                    environment.RpgSceneParamSwitches.IncludeAttributeLevels = includeAttributeLevels;
                }
            }

            if (Owner.TryExtractJsonObject(envContent, "EventIntelPrompt", out string eventIntelContent))
            {
                environment.EventIntelPrompt = new EventIntelPromptConfig();

                string enabledStr = Owner.ExtractValue(eventIntelContent, "Enabled");
                if (bool.TryParse(enabledStr, out bool enabled))
                {
                    environment.EventIntelPrompt.Enabled = enabled;
                }

                string applyToDiplomacyStr = Owner.ExtractValue(eventIntelContent, "ApplyToDiplomacy");
                if (bool.TryParse(applyToDiplomacyStr, out bool applyToDiplomacy))
                {
                    environment.EventIntelPrompt.ApplyToDiplomacy = applyToDiplomacy;
                }

                string applyToRpgStr = Owner.ExtractValue(eventIntelContent, "ApplyToRpg");
                if (bool.TryParse(applyToRpgStr, out bool applyToRpg))
                {
                    environment.EventIntelPrompt.ApplyToRpg = applyToRpg;
                }

                string includeMapEventsStr = Owner.ExtractValue(eventIntelContent, "IncludeMapEvents");
                if (bool.TryParse(includeMapEventsStr, out bool includeMapEvents))
                {
                    environment.EventIntelPrompt.IncludeMapEvents = includeMapEvents;
                }

                string includeRaidReportsStr = Owner.ExtractValue(eventIntelContent, "IncludeRaidBattleReports");
                if (bool.TryParse(includeRaidReportsStr, out bool includeRaidReports))
                {
                    environment.EventIntelPrompt.IncludeRaidBattleReports = includeRaidReports;
                }

                string daysWindowStr = Owner.ExtractValue(eventIntelContent, "DaysWindow");
                if (int.TryParse(daysWindowStr, out int daysWindow))
                {
                    environment.EventIntelPrompt.DaysWindow = daysWindow;
                }

                string maxStoredStr = Owner.ExtractValue(eventIntelContent, "MaxStoredRecords");
                if (int.TryParse(maxStoredStr, out int maxStored))
                {
                    environment.EventIntelPrompt.MaxStoredRecords = maxStored;
                }

                string maxItemsStr = Owner.ExtractValue(eventIntelContent, "MaxInjectedItems");
                if (int.TryParse(maxItemsStr, out int maxItems))
                {
                    environment.EventIntelPrompt.MaxInjectedItems = maxItems;
                }

                string maxCharsStr = Owner.ExtractValue(eventIntelContent, "MaxInjectedChars");
                if (int.TryParse(maxCharsStr, out int maxChars))
                {
                    environment.EventIntelPrompt.MaxInjectedChars = maxChars;
                }
            }

            config.EnvironmentPrompt = environment;
        }

        internal bool TryExtractJsonObject(string json, string key, out string objectContent)
        {
            objectContent = string.Empty;
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
            {
                return false;
            }

            string pattern = $"\"{key}\":";
            int start = json.IndexOf(pattern, StringComparison.Ordinal);
            if (start < 0)
            {
                return false;
            }

            int objectStart = json.IndexOf('{', start + pattern.Length);
            if (objectStart < 0)
            {
                return false;
            }

            if (!Owner.TryFindJsonBlockEnd(json, objectStart, '{', '}', out int objectEnd))
            {
                return false;
            }

            objectContent = json.Substring(objectStart, objectEnd - objectStart + 1);
            return true;
        }

        internal bool TryExtractJsonArray(string json, string key, out string arrayContent)
        {
            arrayContent = string.Empty;
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
            {
                return false;
            }

            string pattern = $"\"{key}\":";
            int start = json.IndexOf(pattern, StringComparison.Ordinal);
            if (start < 0)
            {
                return false;
            }

            int arrayStart = json.IndexOf('[', start + pattern.Length);
            if (arrayStart < 0)
            {
                return false;
            }

            if (!Owner.TryFindJsonBlockEnd(json, arrayStart, '[', ']', out int arrayEnd))
            {
                return false;
            }

            arrayContent = json.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);
            return true;
        }

        internal bool TryFindJsonBlockEnd(string json, int blockStart, char openChar, char closeChar, out int endIndex)
        {
            endIndex = -1;
            if (string.IsNullOrEmpty(json) || blockStart < 0 || blockStart >= json.Length || json[blockStart] != openChar)
            {
                return false;
            }

            bool inString = false;
            bool escape = false;
            int depth = 0;

            for (int i = blockStart; i < json.Length; i++)
            {
                char c = json[i];
                if (inString)
                {
                    if (escape)
                    {
                        escape = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escape = true;
                        continue;
                    }

                    if (c == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == openChar)
                {
                    depth++;
                    continue;
                }

                if (c == closeChar)
                {
                    depth--;
                    if (depth == 0)
                    {
                        endIndex = i;
                        return true;
                    }
                }
            }

            return false;
        }

        internal List<string> SplitJsonObjects(string arrayContent)
        {
            var objects = new List<string>();
            int depth = 0;
            int start = 0;

            for (int i = 0; i < arrayContent.Length; i++)
            {
                char c = arrayContent[i];

                if (c == '{')
                {
                    if (depth == 0) start = i;
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        objects.Add(arrayContent.Substring(start, i - start + 1));
                    }
                }
                else if (c == '"')
                {
                    i++;
                    while (i < arrayContent.Length && arrayContent[i] != '"')
                    {
                        if (arrayContent[i] == '\\' && i + 1 < arrayContent.Length)
                        {
                            i += 2;
                        }
                        else
                        {
                            i++;
                        }
                    }
                }
            }

            return objects;
        }

        internal string ExtractString(string json, string key)
        {
            string pattern = $"\"{key}\":";
            int index = json.IndexOf(pattern);
            if (index < 0) return "";

            int start = json.IndexOf("\"", index + pattern.Length);
            if (start < 0) return "";

            start++;
            var sb = new StringBuilder();

            for (int i = start; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"')
                {
                    break;
                }
                else if (c == '\\' && i + 1 < json.Length)
                {
                    char next = json[i + 1];
                    switch (next)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        default: sb.Append(next); break;
                    }
                    i++;
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        internal string ExtractValue(string json, string key)
        {
            string pattern = $"\"{key}\":";
            int index = json.IndexOf(pattern);
            if (index < 0) return "";

            int start = index + pattern.Length;
            int end = json.IndexOfAny(new[] { ',', '}', ']' }, start);
            if (end < 0) end = json.Length;

            return json.Substring(start, end - start).Trim();
        }

        internal List<string> ExtractStringArray(string json, string key)
        {
            var result = new List<string>();
            if (!Owner.TryExtractJsonArray(json, key, out string arrayContent))
            {
                return result;
            }

            bool inString = false;
            bool escape = false;
            var current = new StringBuilder();

            for (int i = 0; i < arrayContent.Length; i++)
            {
                char c = arrayContent[i];
                if (!inString)
                {
                    if (c == '"')
                    {
                        inString = true;
                        current.Clear();
                    }
                    continue;
                }

                if (escape)
                {
                    current.Append(c switch
                    {
                        'n' => '\n',
                        'r' => '\r',
                        't' => '\t',
                        '"' => '"',
                        '\\' => '\\',
                        _ => c
                    });
                    escape = false;
                    continue;
                }

                if (c == '\\')
                {
                    escape = true;
                    continue;
                }

                if (c == '"')
                {
                    inString = false;
                    result.Add(current.ToString());
                    continue;
                }

                current.Append(c);
            }

            return result;
        }

        internal string EscapeJson(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            return str
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        }

}
