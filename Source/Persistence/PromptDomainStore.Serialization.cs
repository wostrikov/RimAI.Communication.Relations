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
    internal sealed partial class PromptDomainStore
    {        internal void EnsureDirectoryExists()
        {
            try
            {
                if (!Directory.Exists(BasePath))
                {
                    Directory.CreateDirectory(BasePath);
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
                return EnumeratePromptDomainCustomOverridePaths().Any(File.Exists);
            }
            catch
            {
                return HasAnyCustomDomainFile();
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
            if (TryLoadPromptDomains(
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
                + BuildDefaultDomainDiagnosticSnapshot());

            if (_cachedConfig != null && !IsPlaceholderGlobalSystemPrompt(_cachedConfig))
            {
                _hasPendingPromptDomainRepairs = false;
                Log.Warning("[RimAI.Relations] Default-only recovery failed. Keeping cached config and blocking auto-heal writeback.");
                return _cachedConfig;
            }

            throw CreateDefaultConfigLoadFailureException(validationSummary, null);
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
            return $"System={BuildPathSummary(systemPath)}; "
                + $"Diplomacy={BuildPathSummary(diplomacyPath)}; "
                + $"Pawn={BuildPathSummary(pawnPath)}; "
                + $"Social={BuildPathSummary(socialPath)}";
        }

        internal string BuildPathSummary(string path)
        {
            bool exists = !string.IsNullOrWhiteSpace(path) && File.Exists(path);
            return $"{(exists ? "exists" : "missing")}:{path}";
        }


        internal string SerializeConfigToJson(SystemPromptConfig config, bool prettyPrint = false)
        {
            if (_configJsonCodec.TrySerialize(config, prettyPrint, out string typedJson)
                && IsTypedJsonComplete(typedJson, config))
            {
                return typedJson;
            }

            var sb = new StringBuilder();

            if (prettyPrint)
            {
                sb.AppendLine("{");
                sb.AppendLine($"  \"ConfigName\": \"{EscapeJson(config.ConfigName)}\",");
                sb.AppendLine($"  \"GlobalSystemPrompt\": \"{EscapeJson(config.GlobalSystemPrompt)}\",");
                sb.AppendLine($"  \"GlobalDialoguePrompt\": \"{EscapeJson(config.GlobalDialoguePrompt)}\",");
                sb.AppendLine($"  \"UseAdvancedMode\": {config.UseAdvancedMode.ToString().ToLower()},");
                sb.AppendLine($"  \"UseHierarchicalPromptFormat\": {config.UseHierarchicalPromptFormat.ToString().ToLower()},");
                sb.AppendLine($"  \"PromptSchemaVersion\": {config.PromptSchemaVersion},");
                sb.AppendLine($"  \"PromptPolicySchemaVersion\": {config.PromptPolicySchemaVersion},");
                sb.AppendLine($"  \"Enabled\": {config.Enabled.ToString().ToLower()},");
            }
            else
            {
                sb.Append("{");
                sb.Append($"\"ConfigName\":\"{EscapeJson(config.ConfigName)}\",");
                sb.Append($"\"GlobalSystemPrompt\":\"{EscapeJson(config.GlobalSystemPrompt)}\",");
                sb.Append($"\"GlobalDialoguePrompt\":\"{EscapeJson(config.GlobalDialoguePrompt)}\",");
                sb.Append($"\"UseAdvancedMode\":{config.UseAdvancedMode.ToString().ToLower()},");
                sb.Append($"\"UseHierarchicalPromptFormat\":{config.UseHierarchicalPromptFormat.ToString().ToLower()},");
                sb.Append($"\"PromptSchemaVersion\":{config.PromptSchemaVersion},");
                sb.Append($"\"PromptPolicySchemaVersion\":{config.PromptPolicySchemaVersion},");
                sb.Append($"\"Enabled\":{config.Enabled.ToString().ToLower()},");
            }

            SerializeApiActions(sb, config.ApiActions, prettyPrint);
            SerializeResponseFormat(sb, config.ResponseFormat, prettyPrint);
            SerializeDecisionRules(sb, config.DecisionRules, prettyPrint);
            SerializeEnvironmentPrompt(sb, config.EnvironmentPrompt, prettyPrint);
            SerializePromptTemplates(sb, config.PromptTemplates, prettyPrint);
            SerializePromptPolicy(sb, config.PromptPolicy, prettyPrint);
            SerializeDynamicDataInjection(sb, config.DynamicDataInjection, prettyPrint);

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
                    sb.AppendLine($"      \"ActionName\": \"{EscapeJson(action.ActionName)}\",");
                    sb.AppendLine($"      \"Description\": \"{EscapeJson(action.Description)}\",");
                    sb.AppendLine($"      \"Parameters\": \"{EscapeJson(action.Parameters)}\",");
                    sb.AppendLine($"      \"Requirement\": \"{EscapeJson(action.Requirement)}\",");
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
                    sb.Append($"\"ActionName\":\"{EscapeJson(action.ActionName)}\",");
                    sb.Append($"\"Description\":\"{EscapeJson(action.Description)}\",");
                    sb.Append($"\"Parameters\":\"{EscapeJson(action.Parameters)}\",");
                    sb.Append($"\"Requirement\":\"{EscapeJson(action.Requirement)}\",");
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
                sb.AppendLine($"    \"JsonTemplate\": \"{EscapeJson(format.JsonTemplate)}\",");
                sb.AppendLine($"    \"ImportantRules\": \"{EscapeJson(format.ImportantRules)}\"");
                sb.Append("  },");
            }
            else
            {
                sb.Append(",\"ResponseFormat\":{");
                sb.Append($"\"JsonTemplate\":\"{EscapeJson(format.JsonTemplate)}\",");
                sb.Append($"\"ImportantRules\":\"{EscapeJson(format.ImportantRules)}\"");
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
                    sb.AppendLine($"      \"RuleName\": \"{EscapeJson(rule.RuleName)}\",");
                    sb.AppendLine($"      \"RuleContent\": \"{EscapeJson(rule.RuleContent)}\",");
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
                    sb.Append($"\"RuleName\":\"{EscapeJson(rule.RuleName)}\",");
                    sb.Append($"\"RuleContent\":\"{EscapeJson(rule.RuleContent)}\",");
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
                sb.AppendLine($"    \"CustomInjectionHeader\": \"{EscapeJson(config.CustomInjectionHeader)}\"");
                sb.Append("  }");
            }
            else
            {
                sb.Append(",\"DynamicDataInjection\":{");
                sb.Append($"\"InjectMemoryData\":{config.InjectMemoryData.ToString().ToLower()},");
                sb.Append($"\"InjectFactionInfo\":{config.InjectFactionInfo.ToString().ToLower()},");
                sb.Append($"\"CustomInjectionHeader\":\"{EscapeJson(config.CustomInjectionHeader)}\"");
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
                sb.AppendLine($"    \"FactGroundingTemplate\": \"{EscapeJson(templates.FactGroundingTemplate)}\",");
                sb.AppendLine($"    \"OutputLanguageTemplate\": \"{EscapeJson(templates.OutputLanguageTemplate)}\",");
                sb.AppendLine($"    \"DiplomacyFallbackRoleTemplate\": \"{EscapeJson(templates.DiplomacyFallbackRoleTemplate)}\",");
                sb.AppendLine($"    \"SocialCircleActionRuleTemplate\": \"{EscapeJson(templates.SocialCircleActionRuleTemplate)}\",");
                sb.AppendLine($"    \"SocialCircleNewsStyleTemplate\": \"{EscapeJson(templates.SocialCircleNewsStyleTemplate)}\",");
                sb.AppendLine($"    \"SocialCircleNewsJsonContractTemplate\": \"{EscapeJson(templates.SocialCircleNewsJsonContractTemplate)}\",");
                sb.AppendLine($"    \"SocialCircleNewsFactTemplate\": \"{EscapeJson(templates.SocialCircleNewsFactTemplate)}\",");
                sb.AppendLine($"    \"DecisionPolicyTemplate\": \"{EscapeJson(templates.DecisionPolicyTemplate)}\",");
                sb.AppendLine($"    \"TurnObjectiveTemplate\": \"{EscapeJson(templates.TurnObjectiveTemplate)}\",");
                sb.AppendLine($"    \"TopicShiftRuleTemplate\": \"{EscapeJson(templates.TopicShiftRuleTemplate)}\",");
                sb.AppendLine($"    \"ApiLimitsNodeTemplate\": \"{EscapeJson(templates.ApiLimitsNodeTemplate)}\",");
                sb.AppendLine($"    \"QuestGuidanceNodeTemplate\": \"{EscapeJson(templates.QuestGuidanceNodeTemplate)}\",");
                sb.AppendLine($"    \"ResponseContractNodeTemplate\": \"{EscapeJson(templates.ResponseContractNodeTemplate)}\"");
                sb.Append("  },");
            }
            else
            {
                sb.Append(",\"PromptTemplates\":{");
                sb.Append($"\"Enabled\":{templates.Enabled.ToString().ToLower()},");
                sb.Append($"\"FactGroundingTemplate\":\"{EscapeJson(templates.FactGroundingTemplate)}\",");
                sb.Append($"\"OutputLanguageTemplate\":\"{EscapeJson(templates.OutputLanguageTemplate)}\",");
                sb.Append($"\"DiplomacyFallbackRoleTemplate\":\"{EscapeJson(templates.DiplomacyFallbackRoleTemplate)}\",");
                sb.Append($"\"SocialCircleActionRuleTemplate\":\"{EscapeJson(templates.SocialCircleActionRuleTemplate)}\",");
                sb.Append($"\"SocialCircleNewsStyleTemplate\":\"{EscapeJson(templates.SocialCircleNewsStyleTemplate)}\",");
                sb.Append($"\"SocialCircleNewsJsonContractTemplate\":\"{EscapeJson(templates.SocialCircleNewsJsonContractTemplate)}\",");
                sb.Append($"\"SocialCircleNewsFactTemplate\":\"{EscapeJson(templates.SocialCircleNewsFactTemplate)}\",");
                sb.Append($"\"DecisionPolicyTemplate\":\"{EscapeJson(templates.DecisionPolicyTemplate)}\",");
                sb.Append($"\"TurnObjectiveTemplate\":\"{EscapeJson(templates.TurnObjectiveTemplate)}\",");
                sb.Append($"\"TopicShiftRuleTemplate\":\"{EscapeJson(templates.TopicShiftRuleTemplate)}\",");
                sb.Append($"\"ApiLimitsNodeTemplate\":\"{EscapeJson(templates.ApiLimitsNodeTemplate)}\",");
                sb.Append($"\"QuestGuidanceNodeTemplate\":\"{EscapeJson(templates.QuestGuidanceNodeTemplate)}\",");
                sb.Append($"\"ResponseContractNodeTemplate\":\"{EscapeJson(templates.ResponseContractNodeTemplate)}\"");
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
                sb.AppendLine($"      \"Content\": \"{EscapeJson(environment.Worldview?.Content ?? string.Empty)}\"");
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
                    sb.AppendLine($"        \"Id\": \"{EscapeJson(entry.Id ?? string.Empty)}\",");
                    sb.AppendLine($"        \"Name\": \"{EscapeJson(entry.Name ?? string.Empty)}\",");
                    sb.AppendLine($"        \"Enabled\": {entry.Enabled.ToString().ToLower()},");
                    sb.AppendLine($"        \"ApplyToDiplomacy\": {entry.ApplyToDiplomacy.ToString().ToLower()},");
                    sb.AppendLine($"        \"ApplyToRPG\": {entry.ApplyToRPG.ToString().ToLower()},");
                    sb.AppendLine($"        \"Priority\": {entry.Priority},");
                    sb.AppendLine($"        \"MatchTags\": {SerializeStringList(entry.MatchTags)},");
                    sb.AppendLine($"        \"Content\": \"{EscapeJson(entry.Content ?? string.Empty)}\"");
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
                sb.Append($"\"Content\":\"{EscapeJson(environment.Worldview?.Content ?? string.Empty)}\"");
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
                    sb.Append($"\"Id\":\"{EscapeJson(entry.Id ?? string.Empty)}\",");
                    sb.Append($"\"Name\":\"{EscapeJson(entry.Name ?? string.Empty)}\",");
                    sb.Append($"\"Enabled\":{entry.Enabled.ToString().ToLower()},");
                    sb.Append($"\"ApplyToDiplomacy\":{entry.ApplyToDiplomacy.ToString().ToLower()},");
                    sb.Append($"\"ApplyToRPG\":{entry.ApplyToRPG.ToString().ToLower()},");
                    sb.Append($"\"Priority\":{entry.Priority},");
                    sb.Append($"\"MatchTags\":{SerializeStringList(entry.MatchTags)},");
                    sb.Append($"\"Content\":\"{EscapeJson(entry.Content ?? string.Empty)}\"");
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
                sb.Append($"\"{EscapeJson(values[i] ?? string.Empty)}\"");
                if (i < values.Count - 1)
                {
                    sb.Append(",");
                }
            }
            sb.Append("]");
            return sb.ToString();
        }

        /// <summary>/// 解析 JSON 字符串为 SystemPromptConfig (内部使用)
 ///</summary>
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
                if (IsConfigStructurallyComplete(
                    typedConfig,
                    hasSchemaAnchors,
                    hasApiActionsKey,
                    hasResponseFormatKey,
                    hasDecisionRulesKey,
                    hasPromptTemplatesKey))
                {
                    return typedConfig;
                }

                LogTypedParseIncompleteWarningOnce(
                    hasSchemaAnchors,
                    hasApiActionsKey,
                    hasResponseFormatKey,
                    hasDecisionRulesKey,
                    hasPromptTemplatesKey,
                    typedConfig,
                    source);
            }

            bool hasTypedError = !string.IsNullOrWhiteSpace(typedError);

            SystemPromptConfig fallbackConfig = TryParseCurrentSchemaTextFallback(
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
                    LogTypedParseRecoveredInfoOnce(source, typedError);
                }
                else
                {
                    Log.Message($"[RimAI.Relations] Typed JSON parse was incomplete at source={source}; recovered config using current-schema text fallback.");
                }
            }
            else if (hasTypedError)
            {
                LogTypedParseFailureWarningOnce(source, typedError);
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
                    ConfigName = ExtractString(json, "ConfigName"),
                    GlobalSystemPrompt = ExtractString(json, "GlobalSystemPrompt"),
                    GlobalDialoguePrompt = ExtractString(json, "GlobalDialoguePrompt")
                };

                string useAdvancedStr = ExtractValue(json, "UseAdvancedMode");
                if (bool.TryParse(useAdvancedStr, out bool useAdvanced))
                {
                    config.UseAdvancedMode = useAdvanced;
                }

                string useHierarchicalFormatStr = ExtractValue(json, "UseHierarchicalPromptFormat");
                if (bool.TryParse(useHierarchicalFormatStr, out bool useHierarchicalFormat))
                {
                    config.UseHierarchicalPromptFormat = useHierarchicalFormat;
                }

                string enabledStr = ExtractValue(json, "Enabled");
                if (bool.TryParse(enabledStr, out bool enabled))
                {
                    config.Enabled = enabled;
                }

                string promptSchemaVersionStr = ExtractValue(json, "PromptSchemaVersion");
                if (int.TryParse(promptSchemaVersionStr, out int promptSchemaVersion))
                {
                    config.PromptSchemaVersion = promptSchemaVersion;
                }

                string schemaVersionStr = ExtractValue(json, "PromptPolicySchemaVersion");
                if (int.TryParse(schemaVersionStr, out int schemaVersion))
                {
                    config.PromptPolicySchemaVersion = schemaVersion;
                }

                ParseApiActions(json, config);
                ParseResponseFormat(json, config);
                ParseDecisionRules(json, config);
                ParseEnvironmentPrompt(json, config);
                ParsePromptTemplates(json, config);
                ParsePromptPolicy(json, config);
                ParseDynamicDataInjection(json, config);

                return IsConfigStructurallyComplete(
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
            LogTypedParseWarningOnce(
                _typedParseIncompleteWarningHashes,
                signature,
                $"[RimAI.Relations] Typed JSON parse produced incomplete config at source={source} (missing: {detail}); config load rejected.");
        }

        internal void LogTypedParseFailureWarningOnce(string sourceContext, string typedError)
        {
            string source = string.IsNullOrWhiteSpace(sourceContext) ? "unknown" : sourceContext.Trim();
            string normalizedError = typedError ?? string.Empty;
            string signature = $"typed_fail::{source}::{normalizedError}";
            LogTypedParseWarningOnce(
                _typedParseFailureWarningHashes,
                signature,
                $"[RimAI.Relations] Typed JSON parse failed at source={source}; config load rejected: {normalizedError}");
        }

        internal void LogTypedParseRecoveredInfoOnce(string sourceContext, string typedError)
        {
            string source = string.IsNullOrWhiteSpace(sourceContext) ? "unknown" : sourceContext.Trim();
            string normalizedError = typedError ?? string.Empty;
            string signature = $"typed_recovered::{source}::{normalizedError}";
            LogTypedParseWarningOnce(
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
            var objects = SplitJsonObjects(arrayContent);

            foreach (var objStr in objects)
            {
                var action = new ApiActionConfig
                {
                    ActionName = ExtractString(objStr, "ActionName"),
                    Description = ExtractString(objStr, "Description"),
                    Parameters = ExtractString(objStr, "Parameters"),
                    Requirement = ExtractString(objStr, "Requirement")
                };

                string enabledStr = ExtractValue(objStr, "IsEnabled");
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
                JsonTemplate = ExtractString(objContent, "JsonTemplate"),
                ImportantRules = ExtractString(objContent, "ImportantRules")
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
            var objects = SplitJsonObjects(arrayContent);

            foreach (var objStr in objects)
            {
                var rule = new DecisionRuleConfig
                {
                    RuleName = ExtractString(objStr, "RuleName"),
                    RuleContent = ExtractString(objStr, "RuleContent")
                };

                string enabledStr = ExtractValue(objStr, "IsEnabled");
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
                CustomInjectionHeader = ExtractString(objContent, "CustomInjectionHeader")
            };

            string injectMemoryStr = ExtractValue(objContent, "InjectMemoryData");
            if (bool.TryParse(injectMemoryStr, out bool injectMemory))
            {
                config.DynamicDataInjection.InjectMemoryData = injectMemory;
            }

            string injectFactionStr = ExtractValue(objContent, "InjectFactionInfo");
            if (bool.TryParse(injectFactionStr, out bool injectFaction))
            {
                config.DynamicDataInjection.InjectFactionInfo = injectFaction;
            }
        }

        internal void ParsePromptTemplates(string json, SystemPromptConfig config)
        {
            if (!TryExtractJsonObject(json, "PromptTemplates", out string templatesContent))
            {
                if (config.PromptTemplates == null)
                {
                    config.PromptTemplates = new PromptTemplateTextConfig();
                }

                return;
            }

            config.PromptTemplates = new PromptTemplateTextConfig
            {
                FactGroundingTemplate = ExtractString(templatesContent, "FactGroundingTemplate"),
                OutputLanguageTemplate = ExtractString(templatesContent, "OutputLanguageTemplate"),
                DiplomacyFallbackRoleTemplate = ExtractString(templatesContent, "DiplomacyFallbackRoleTemplate"),
                SocialCircleActionRuleTemplate = ExtractString(templatesContent, "SocialCircleActionRuleTemplate"),
                SocialCircleNewsStyleTemplate = ExtractString(templatesContent, "SocialCircleNewsStyleTemplate"),
                SocialCircleNewsJsonContractTemplate = ExtractString(templatesContent, "SocialCircleNewsJsonContractTemplate"),
                SocialCircleNewsFactTemplate = ExtractString(templatesContent, "SocialCircleNewsFactTemplate"),
                RpgRoleSettingTemplate = ExtractString(templatesContent, "RpgRoleSettingTemplate"),
                RpgCompactFormatConstraintTemplate = ExtractString(templatesContent, "RpgCompactFormatConstraintTemplate"),
                RpgActionReliabilityRuleTemplate = ExtractString(templatesContent, "RpgActionReliabilityRuleTemplate"),
                DecisionPolicyTemplate = ExtractString(templatesContent, "DecisionPolicyTemplate"),
                TurnObjectiveTemplate = ExtractString(templatesContent, "TurnObjectiveTemplate"),
                OpeningObjectiveTemplate = ExtractString(templatesContent, "OpeningObjectiveTemplate"),
                TopicShiftRuleTemplate = ExtractString(templatesContent, "TopicShiftRuleTemplate"),
                ApiLimitsNodeTemplate = ExtractString(templatesContent, "ApiLimitsNodeTemplate"),
                QuestGuidanceNodeTemplate = ExtractString(templatesContent, "QuestGuidanceNodeTemplate"),
                ResponseContractNodeTemplate = ExtractString(templatesContent, "ResponseContractNodeTemplate"),
                MandatoryRaceInjectionTemplate = ExtractString(templatesContent, "MandatoryRaceInjectionTemplate")
            };

            string enabledStr = ExtractValue(templatesContent, "Enabled");
            if (bool.TryParse(enabledStr, out bool enabled))
            {
                config.PromptTemplates.Enabled = enabled;
            }
        }

        internal void ParsePromptPolicy(string json, SystemPromptConfig config)
        {
            if (!TryExtractJsonObject(json, "PromptPolicy", out string policyContent))
            {
                config.PromptPolicy ??= PromptPolicyConfig.CreateDefault();
                return;
            }

            var policy = PromptPolicyConfig.CreateDefault();
            string enabledStr = ExtractValue(policyContent, "Enabled");
            if (bool.TryParse(enabledStr, out bool enabled))
            {
                policy.Enabled = enabled;
            }

            string intentMappingStr = ExtractValue(policyContent, "EnableIntentDrivenActionMapping");
            if (bool.TryParse(intentMappingStr, out bool intentMapping))
            {
                policy.EnableIntentDrivenActionMapping = intentMapping;
            }

            string cooldownStr = ExtractValue(policyContent, "IntentActionCooldownTurns");
            if (int.TryParse(cooldownStr, out int cooldown))
            {
                policy.IntentActionCooldownTurns = cooldown;
            }

            string minRoundsStr = ExtractValue(policyContent, "IntentMinAssistantRoundsForMemory");
            if (int.TryParse(minRoundsStr, out int minRounds))
            {
                policy.IntentMinAssistantRoundsForMemory = minRounds;
            }

            string streakStr = ExtractValue(policyContent, "IntentNoActionStreakThreshold");
            if (int.TryParse(streakStr, out int streakThreshold))
            {
                policy.IntentNoActionStreakThreshold = streakThreshold;
            }

            string resetStr = ExtractValue(policyContent, "ResetPromptCustomOnSchemaUpgrade");
            if (bool.TryParse(resetStr, out bool reset))
            {
                policy.ResetPromptCustomOnSchemaUpgrade = reset;
            }

            string summaryLimitStr = ExtractValue(policyContent, "SummaryTimelineTurnLimit");
            if (int.TryParse(summaryLimitStr, out int summaryLimit))
            {
                policy.SummaryTimelineTurnLimit = summaryLimit;
            }

            string summaryBudgetStr = ExtractValue(policyContent, "SummaryCharBudget");
            if (int.TryParse(summaryBudgetStr, out int summaryBudget))
            {
                policy.SummaryCharBudget = summaryBudget;
            }

            config.PromptPolicy = policy;
        }

        internal void ParseEnvironmentPrompt(string json, SystemPromptConfig config)
        {
            if (!TryExtractJsonObject(json, "EnvironmentPrompt", out string envContent))
            {
                if (config.EnvironmentPrompt == null)
                {
                    config.EnvironmentPrompt = new EnvironmentPromptConfig();
                }
                return;
            }

            var environment = new EnvironmentPromptConfig();

            if (TryExtractJsonObject(envContent, "Worldview", out string worldviewContent))
            {
                environment.Worldview = new WorldviewPromptConfig
                {
                    Content = ExtractString(worldviewContent, "Content")
                };

                string enabledStr = ExtractValue(worldviewContent, "Enabled");
                if (bool.TryParse(enabledStr, out bool enabled))
                {
                    environment.Worldview.Enabled = enabled;
                }
            }

            if (TryExtractJsonObject(envContent, "SceneSystem", out string sceneSystemContent))
            {
                environment.SceneSystem = new SceneSystemPromptConfig();

                string enabledStr = ExtractValue(sceneSystemContent, "Enabled");
                if (bool.TryParse(enabledStr, out bool enabled))
                {
                    environment.SceneSystem.Enabled = enabled;
                }

                string maxSceneCharsStr = ExtractValue(sceneSystemContent, "MaxSceneChars");
                if (int.TryParse(maxSceneCharsStr, out int maxSceneChars))
                {
                    environment.SceneSystem.MaxSceneChars = maxSceneChars;
                }

                string maxTotalCharsStr = ExtractValue(sceneSystemContent, "MaxTotalChars");
                if (int.TryParse(maxTotalCharsStr, out int maxTotalChars))
                {
                    environment.SceneSystem.MaxTotalChars = maxTotalChars;
                }

                string presetTagsEnabledStr = ExtractValue(sceneSystemContent, "PresetTagsEnabled");
                if (bool.TryParse(presetTagsEnabledStr, out bool presetTagsEnabled))
                {
                    environment.SceneSystem.PresetTagsEnabled = presetTagsEnabled;
                }
            }

            if (TryExtractJsonArray(envContent, "SceneEntries", out string sceneEntriesContent))
            {
                environment.SceneEntries = new List<ScenePromptEntryConfig>();
                var objects = SplitJsonObjects(sceneEntriesContent);
                foreach (string objStr in objects)
                {
                    var entry = new ScenePromptEntryConfig
                    {
                        Id = ExtractString(objStr, "Id"),
                        Name = ExtractString(objStr, "Name"),
                        Content = ExtractString(objStr, "Content"),
                        MatchTags = ExtractStringArray(objStr, "MatchTags")
                    };

                    string enabledStr = ExtractValue(objStr, "Enabled");
                    if (bool.TryParse(enabledStr, out bool enabled))
                    {
                        entry.Enabled = enabled;
                    }

                    string applyToDiplomacyStr = ExtractValue(objStr, "ApplyToDiplomacy");
                    if (bool.TryParse(applyToDiplomacyStr, out bool applyToDiplomacy))
                    {
                        entry.ApplyToDiplomacy = applyToDiplomacy;
                    }

                    string applyToRpgStr = ExtractValue(objStr, "ApplyToRPG");
                    if (bool.TryParse(applyToRpgStr, out bool applyToRpg))
                    {
                        entry.ApplyToRPG = applyToRpg;
                    }

                    string priorityStr = ExtractValue(objStr, "Priority");
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

            if (TryExtractJsonObject(envContent, "EnvironmentContextSwitches", out string environmentContextContent))
            {
                environment.EnvironmentContextSwitches = new EnvironmentContextSwitchesConfig();

                string enabledStr = ExtractValue(environmentContextContent, "Enabled");
                if (bool.TryParse(enabledStr, out bool enabled))
                {
                    environment.EnvironmentContextSwitches.Enabled = enabled;
                }

                string includeTimeStr = ExtractValue(environmentContextContent, "IncludeTime");
                if (bool.TryParse(includeTimeStr, out bool includeTime))
                {
                    environment.EnvironmentContextSwitches.IncludeTime = includeTime;
                }

                string includeDateStr = ExtractValue(environmentContextContent, "IncludeDate");
                if (bool.TryParse(includeDateStr, out bool includeDate))
                {
                    environment.EnvironmentContextSwitches.IncludeDate = includeDate;
                }

                string includeSeasonStr = ExtractValue(environmentContextContent, "IncludeSeason");
                if (bool.TryParse(includeSeasonStr, out bool includeSeason))
                {
                    environment.EnvironmentContextSwitches.IncludeSeason = includeSeason;
                }

                string includeWeatherStr = ExtractValue(environmentContextContent, "IncludeWeather");
                if (bool.TryParse(includeWeatherStr, out bool includeWeather))
                {
                    environment.EnvironmentContextSwitches.IncludeWeather = includeWeather;
                }

                string includeLocationAndTemperatureStr = ExtractValue(environmentContextContent, "IncludeLocationAndTemperature");
                if (bool.TryParse(includeLocationAndTemperatureStr, out bool includeLocationAndTemperature))
                {
                    environment.EnvironmentContextSwitches.IncludeLocationAndTemperature = includeLocationAndTemperature;
                }

                string includeTerrainStr = ExtractValue(environmentContextContent, "IncludeTerrain");
                if (bool.TryParse(includeTerrainStr, out bool includeTerrain))
                {
                    environment.EnvironmentContextSwitches.IncludeTerrain = includeTerrain;
                }

                string includeBeautyStr = ExtractValue(environmentContextContent, "IncludeBeauty");
                if (bool.TryParse(includeBeautyStr, out bool includeBeauty))
                {
                    environment.EnvironmentContextSwitches.IncludeBeauty = includeBeauty;
                }

                string includeCleanlinessStr = ExtractValue(environmentContextContent, "IncludeCleanliness");
                if (bool.TryParse(includeCleanlinessStr, out bool includeCleanliness))
                {
                    environment.EnvironmentContextSwitches.IncludeCleanliness = includeCleanliness;
                }

                string includeSurroundingsStr = ExtractValue(environmentContextContent, "IncludeSurroundings");
                if (bool.TryParse(includeSurroundingsStr, out bool includeSurroundings))
                {
                    environment.EnvironmentContextSwitches.IncludeSurroundings = includeSurroundings;
                }

                string includeWealthStr = ExtractValue(environmentContextContent, "IncludeWealth");
                if (bool.TryParse(includeWealthStr, out bool includeWealth))
                {
                    environment.EnvironmentContextSwitches.IncludeWealth = includeWealth;
                }
            }

            if (TryExtractJsonObject(envContent, "RpgSceneParamSwitches", out string rpgSwitchContent))
            {
                environment.RpgSceneParamSwitches = new RpgSceneParamSwitchesConfig();

                string includeSkillsStr = ExtractValue(rpgSwitchContent, "IncludeSkills");
                if (bool.TryParse(includeSkillsStr, out bool includeSkills))
                {
                    environment.RpgSceneParamSwitches.IncludeSkills = includeSkills;
                }

                string includeEquipmentStr = ExtractValue(rpgSwitchContent, "IncludeEquipment");
                if (bool.TryParse(includeEquipmentStr, out bool includeEquipment))
                {
                    environment.RpgSceneParamSwitches.IncludeEquipment = includeEquipment;
                }

                string includeGenesStr = ExtractValue(rpgSwitchContent, "IncludeGenes");
                if (bool.TryParse(includeGenesStr, out bool includeGenes))
                {
                    environment.RpgSceneParamSwitches.IncludeGenes = includeGenes;
                }

                string includeNeedsStr = ExtractValue(rpgSwitchContent, "IncludeNeeds");
                if (bool.TryParse(includeNeedsStr, out bool includeNeeds))
                {
                    environment.RpgSceneParamSwitches.IncludeNeeds = includeNeeds;
                }

                string includeHediffsStr = ExtractValue(rpgSwitchContent, "IncludeHediffs");
                if (bool.TryParse(includeHediffsStr, out bool includeHediffs))
                {
                    environment.RpgSceneParamSwitches.IncludeHediffs = includeHediffs;
                }

                string includeRecentEventsStr = ExtractValue(rpgSwitchContent, "IncludeRecentEvents");
                if (bool.TryParse(includeRecentEventsStr, out bool includeRecentEvents))
                {
                    environment.RpgSceneParamSwitches.IncludeRecentEvents = includeRecentEvents;
                }

                string includeInventorySummaryStr = ExtractValue(rpgSwitchContent, "IncludeColonyInventorySummary");
                if (bool.TryParse(includeInventorySummaryStr, out bool includeInventorySummary))
                {
                    environment.RpgSceneParamSwitches.IncludeColonyInventorySummary = includeInventorySummary;
                }

                string includeHomeAlertsStr = ExtractValue(rpgSwitchContent, "IncludeHomeAlerts");
                if (bool.TryParse(includeHomeAlertsStr, out bool includeHomeAlerts))
                {
                    environment.RpgSceneParamSwitches.IncludeHomeAlerts = includeHomeAlerts;
                }

                string includeRecentJobStateStr = ExtractValue(rpgSwitchContent, "IncludeRecentJobState");
                if (bool.TryParse(includeRecentJobStateStr, out bool includeRecentJobState))
                {
                    environment.RpgSceneParamSwitches.IncludeRecentJobState = includeRecentJobState;
                }

                string includeAttributeLevelsStr = ExtractValue(rpgSwitchContent, "IncludeAttributeLevels");
                if (bool.TryParse(includeAttributeLevelsStr, out bool includeAttributeLevels))
                {
                    environment.RpgSceneParamSwitches.IncludeAttributeLevels = includeAttributeLevels;
                }
            }

            if (TryExtractJsonObject(envContent, "EventIntelPrompt", out string eventIntelContent))
            {
                environment.EventIntelPrompt = new EventIntelPromptConfig();

                string enabledStr = ExtractValue(eventIntelContent, "Enabled");
                if (bool.TryParse(enabledStr, out bool enabled))
                {
                    environment.EventIntelPrompt.Enabled = enabled;
                }

                string applyToDiplomacyStr = ExtractValue(eventIntelContent, "ApplyToDiplomacy");
                if (bool.TryParse(applyToDiplomacyStr, out bool applyToDiplomacy))
                {
                    environment.EventIntelPrompt.ApplyToDiplomacy = applyToDiplomacy;
                }

                string applyToRpgStr = ExtractValue(eventIntelContent, "ApplyToRpg");
                if (bool.TryParse(applyToRpgStr, out bool applyToRpg))
                {
                    environment.EventIntelPrompt.ApplyToRpg = applyToRpg;
                }

                string includeMapEventsStr = ExtractValue(eventIntelContent, "IncludeMapEvents");
                if (bool.TryParse(includeMapEventsStr, out bool includeMapEvents))
                {
                    environment.EventIntelPrompt.IncludeMapEvents = includeMapEvents;
                }

                string includeRaidReportsStr = ExtractValue(eventIntelContent, "IncludeRaidBattleReports");
                if (bool.TryParse(includeRaidReportsStr, out bool includeRaidReports))
                {
                    environment.EventIntelPrompt.IncludeRaidBattleReports = includeRaidReports;
                }

                string daysWindowStr = ExtractValue(eventIntelContent, "DaysWindow");
                if (int.TryParse(daysWindowStr, out int daysWindow))
                {
                    environment.EventIntelPrompt.DaysWindow = daysWindow;
                }

                string maxStoredStr = ExtractValue(eventIntelContent, "MaxStoredRecords");
                if (int.TryParse(maxStoredStr, out int maxStored))
                {
                    environment.EventIntelPrompt.MaxStoredRecords = maxStored;
                }

                string maxItemsStr = ExtractValue(eventIntelContent, "MaxInjectedItems");
                if (int.TryParse(maxItemsStr, out int maxItems))
                {
                    environment.EventIntelPrompt.MaxInjectedItems = maxItems;
                }

                string maxCharsStr = ExtractValue(eventIntelContent, "MaxInjectedChars");
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

            if (!TryFindJsonBlockEnd(json, objectStart, '{', '}', out int objectEnd))
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

            if (!TryFindJsonBlockEnd(json, arrayStart, '[', ']', out int arrayEnd))
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
            if (!TryExtractJsonArray(json, key, out string arrayContent))
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
