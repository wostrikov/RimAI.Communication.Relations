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
    {                public bool ConfigExists()
        {
            return HasAnyCustomDomainFile();
        }


                internal bool TryGetConfigLastWriteTimeUtc(out DateTime writeTimeUtc)
        {
            try
            {
                return TryGetDomainConfigLastWriteTimeUtc(out writeTimeUtc);
            }
            catch
            {
                writeTimeUtc = DateTime.MinValue;
                return false;
            }
        }


        internal bool IsConfigCacheFresh(out DateTime writeTimeUtc)
        {
            writeTimeUtc = DateTime.MinValue;
            if (_cachedConfig == null)
            {
                return false;
            }

            if (!TryGetConfigLastWriteTimeUtc(out writeTimeUtc))
            {
                return false;
            }

            return writeTimeUtc == _cachedConfigWriteTimeUtc;
        }

        internal bool IsPlaceholderGlobalSystemPrompt(SystemPromptConfig config)
        {
            return config != null &&
                string.Equals(
                    config.GlobalSystemPrompt?.Trim() ?? string.Empty,
                    SystemPromptConfig.PlaceholderGlobalSystemPrompt,
                    StringComparison.Ordinal);
        }

        internal bool TryRepairPlaceholderGlobalSystemPrompt(ref SystemPromptConfig config, string sourceLabel)
        {
            if (!IsPlaceholderGlobalSystemPrompt(config))
            {
                return false;
            }

            Log.Error($"[RimAI.Relations] Detected placeholder GlobalSystemPrompt in {sourceLabel}; rebuilding from default file.");
            SystemPromptConfig rebuilt = CreateDefaultConfig();
            if (rebuilt == null ||
                IsPlaceholderGlobalSystemPrompt(rebuilt) ||
                string.IsNullOrWhiteSpace(rebuilt.GlobalSystemPrompt))
            {
                Log.Error("[RimAI.Relations] Failed to rebuild placeholder GlobalSystemPrompt from Prompt/Default/SystemPrompt_Default.json.");
                return false;
            }

            config = rebuilt;
            return true;
        }

        public SystemPromptConfig LoadConfig()
        {
            return LoadConfigInternal(saveRepairsWhenNeeded: true, out _);
        }

        public SystemPromptConfig LoadConfigReadOnly()
        {
            return LoadConfigInternal(saveRepairsWhenNeeded: false, out _);
        }

        public bool RepairAndRewritePromptDomains()
        {
            LoadConfigInternal(saveRepairsWhenNeeded: true, out bool repaired);
            return repaired;
        }

        internal SystemPromptConfig LoadConfigInternal(bool saveRepairsWhenNeeded, out bool repaired)
        {
            repaired = false;
            try
            {
                if (saveRepairsWhenNeeded)
                {
                    EnsureDirectoryExists();
                }
                if (IsConfigCacheFresh(out DateTime domainWriteTimeUtc) && !IsPlaceholderGlobalSystemPrompt(_cachedConfig))
                {
                    if (saveRepairsWhenNeeded &&
                        _hasPendingPromptDomainRepairs &&
                        HasAnyPromptCustomOverrideFile())
                    {
                        SaveConfig(_cachedConfig);
                        _hasPendingPromptDomainRepairs = false;
                        repaired = true;
                    }

                    return _cachedConfig;
                }
                bool hasPromptCustomOverrides = HasAnyPromptCustomOverrideFile();
                bool loadedFromDomains = TryLoadPromptDomains(
                    includeCustom: true,
                    out SystemPromptConfig loadedConfig,
                    out int loadedDomainSchemaVersion,
                    out List<string> domainValidationErrors);

                SystemPromptConfig resolvedConfig = loadedFromDomains
                    ? loadedConfig
                    : CreateDefaultConfig();
                bool recoveredWithCachedConfig = !loadedFromDomains &&
                                                 _cachedConfig != null &&
                                                 ReferenceEquals(resolvedConfig, _cachedConfig);
                if (recoveredWithCachedConfig)
                {
                    _cachedConfigWriteTimeUtc = domainWriteTimeUtc;
                    _hasPendingPromptDomainRepairs = false;
                    Log.Warning("[RimAI.Relations] Invalid prompt-domain config detected, and default-only recovery also failed. Keeping cached config and skipping auto-heal writeback.");
                    return _cachedConfig;
                }

                bool needsDomainSave = false;
                var migrationFixes = new List<string>();
                if (!loadedFromDomains && hasPromptCustomOverrides)
                {
                    migrationFixes.Add("fallback_source=default_only");
                    if (domainValidationErrors != null && domainValidationErrors.Count > 0)
                    {
                        migrationFixes.Add("domain_validation=" + string.Join("|", domainValidationErrors));
                    }

                    needsDomainSave = true;
                    Log.Warning(saveRepairsWhenNeeded
                        ? "[RimAI.Relations] Invalid prompt-domain custom config detected. Recovered with default-only load and scheduled auto-heal writeback."
                        : "[RimAI.Relations] Invalid prompt-domain custom config detected. Recovered with default-only load in read-only mode.");
                }

                if (hasPromptCustomOverrides && loadedDomainSchemaVersion < CurrentPromptDomainSchemaVersion)
                {
                    migrationFixes.Add($"prompt_domain_schema:{loadedDomainSchemaVersion}->{CurrentPromptDomainSchemaVersion}");
                    needsDomainSave = true;
                }

                if (TryRepairPlaceholderGlobalSystemPrompt(ref resolvedConfig, "prompt domain config"))
                {
                    migrationFixes.Add("repair_placeholder_global_system_prompt");
                    needsDomainSave = true;
                }

                if (host.Normalization.TryApplyPromptPolicySchemaUpgrade(ref resolvedConfig))
                {
                    migrationFixes.Add("prompt_policy_schema_upgrade");
                    needsDomainSave = true;
                }

                if (host.Normalization.MigratePresenceBehaviorGuidance(resolvedConfig))
                {
                    migrationFixes.Add("presence_behavior_migration");
                    needsDomainSave = true;
                }

                if (host.Normalization.EnsureConfigDefaults(resolvedConfig))
                {
                    migrationFixes.Add("defaults_backfill");
                    needsDomainSave = true;
                }

                if (host.WorkspaceComposer.SyncLegacyPromptMirrorsFromSections(resolvedConfig))
                {
                    migrationFixes.Add("legacy_mirror_sync");
                    needsDomainSave = true;
                }

                if (host.Normalization.TryApplyPromptSchemaUpgrade(resolvedConfig))
                {
                    migrationFixes.Add("prompt_schema_upgrade");
                    needsDomainSave = true;
                }

                _cachedConfig = resolvedConfig;
                _cachedConfigWriteTimeUtc = domainWriteTimeUtc;
                _hasPendingPromptDomainRepairs = needsDomainSave && hasPromptCustomOverrides;
                if (_hasPendingPromptDomainRepairs && saveRepairsWhenNeeded)
                {
                    SaveConfig(resolvedConfig);
                    string summary = migrationFixes.Count == 0
                        ? "none"
                        : string.Join(", ", migrationFixes);
                    Log.Message("[RimAI.Relations] Prompt domain migration completed and saved. Fixes: " + summary);
                    _hasPendingPromptDomainRepairs = false;
                    repaired = true;
                }

                Log.Message("[RimAI.Relations] Loaded SystemPromptConfig from prompt domain files.");
                return resolvedConfig;
            }
            catch (PromptRenderException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAI.Relations] Failed to load config: {ex}");
                if (_cachedConfig != null && !IsPlaceholderGlobalSystemPrompt(_cachedConfig))
                {
                    _hasPendingPromptDomainRepairs = false;
                    Log.Warning("[RimAI.Relations] Load config failed. Returning cached config and blocking repair writeback.");
                    return _cachedConfig;
                }

                throw CreateDefaultConfigLoadFailureException("load_config_exception", ex);
            }
        }


                public void SaveConfig(SystemPromptConfig config)
        {
            try
            {
                EnsureDirectoryExists();

                if (config == null)
                {
                    Log.Warning("[RimAI.Relations] Attempted to save null config");
                    return;
                }

                if (TryRepairPlaceholderGlobalSystemPrompt(ref config, "config save request"))
                {
                    Log.Message("[RimAI.Relations] Rebuilt placeholder GlobalSystemPrompt before saving custom config.");
                }

                if (IsPlaceholderGlobalSystemPrompt(config))
                {
                    Log.Error("[RimAI.Relations] Refusing to save placeholder GlobalSystemPrompt into prompt domain files.");
                    return;
                }

                host.Normalization.EnsureConfigDefaults(config);
                host.WorkspaceComposer.SyncLegacyPromptMirrorsFromSections(config);
                SavePromptDomainFiles(config);
                _cachedConfig = config;
                if (!TryGetConfigLastWriteTimeUtc(out _cachedConfigWriteTimeUtc))
                {
                    _cachedConfigWriteTimeUtc = DateTime.MinValue;
                }

                Log.Message($"[RimAI.Relations] Saved SystemPromptConfig to: {ConfigFilePath}");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAI.Relations] Failed to save config: {ex}");
            }
        }


                public void ResetToDefault()
        {
            try
            {
                DeletePromptDomainCustomFiles();
                RpgPromptCustomStore.DeleteCustomConfig();
                FactionPromptManager.Instance.ResetAllConfigs();
                _cachedConfig = CreateDefaultConfig();
                _cachedConfigWriteTimeUtc = DateTime.MinValue;
                Log.Message("[RimAI.Relations] Reset SystemPromptConfig to default");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAI.Relations] Failed to reset config: {ex}");
            }
        }
    }
}
