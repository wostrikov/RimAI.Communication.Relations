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
using Ustas.RimAI.Communication.Relations.Diagnostics;

namespace Ustas.RimAI.Communication.Relations.Persistence
{
        internal sealed class PromptDomainStoreLifecycle : PromptDomainStoreCollaborator
    {
        internal PromptDomainStoreLifecycle(PromptDomainStore owner) : base(owner)
        {
        }

                public bool ConfigExists()
        {
            return Owner.HasAnyCustomDomainFile();
        }


                internal bool TryGetConfigLastWriteTimeUtc(out DateTime writeTimeUtc)
        {
            try
            {
                return Owner.TryGetDomainConfigLastWriteTimeUtc(out writeTimeUtc);
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

            if (!Owner.TryGetConfigLastWriteTimeUtc(out writeTimeUtc))
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
            if (!Owner.IsPlaceholderGlobalSystemPrompt(config))
            {
                return false;
            }

            Log.Error($"[RimAI.Relations] Detected placeholder GlobalSystemPrompt in {sourceLabel}; rebuilding from default file.");
            SystemPromptConfig rebuilt = Owner.CreateDefaultConfig();
            if (rebuilt == null ||
                Owner.IsPlaceholderGlobalSystemPrompt(rebuilt) ||
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
            return Owner.LoadConfigInternal(saveRepairsWhenNeeded: true, out _);
        }

        public SystemPromptConfig LoadConfigReadOnly()
        {
            return Owner.LoadConfigInternal(saveRepairsWhenNeeded: false, out _);
        }

        public bool RepairAndRewritePromptDomains()
        {
            Owner.LoadConfigInternal(saveRepairsWhenNeeded: true, out bool repaired);
            return repaired;
        }

        internal SystemPromptConfig LoadConfigInternal(bool saveRepairsWhenNeeded, out bool repaired)
        {
            repaired = false;
            try
            {
                if (saveRepairsWhenNeeded)
                {
                    Owner.EnsureDirectoryExists();
                }
                if (Owner.IsConfigCacheFresh(out DateTime domainWriteTimeUtc) && !Owner.IsPlaceholderGlobalSystemPrompt(_cachedConfig))
                {
                    if (saveRepairsWhenNeeded &&
                        _hasPendingPromptDomainRepairs &&
                        Owner.HasAnyPromptCustomOverrideFile())
                    {
                        Owner.SaveConfig(_cachedConfig);
                        _hasPendingPromptDomainRepairs = false;
                        repaired = true;
                    }

                    return _cachedConfig;
                }
                bool hasPromptCustomOverrides = Owner.HasAnyPromptCustomOverrideFile();
                bool loadedFromDomains = Owner.TryLoadPromptDomains(
                    includeCustom: true,
                    out SystemPromptConfig loadedConfig,
                    out int loadedDomainSchemaVersion,
                    out List<string> domainValidationErrors);

                SystemPromptConfig resolvedConfig = loadedFromDomains
                    ? loadedConfig
                    : Owner.CreateDefaultConfig();
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

                if (Owner.TryRepairPlaceholderGlobalSystemPrompt(ref resolvedConfig, "prompt domain config"))
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
                    Owner.SaveConfig(resolvedConfig);
                    string summary = migrationFixes.Count == 0
                        ? "none"
                        : string.Join(", ", migrationFixes);
                    ModuleLog.Message("[RimAI.Relations] Prompt domain migration completed and saved. Fixes: " + summary);
                    _hasPendingPromptDomainRepairs = false;
                    repaired = true;
                }

                ModuleLog.Message("[RimAI.Relations] Loaded SystemPromptConfig from prompt domain files.");
                return resolvedConfig;
            }
            catch (PromptRenderException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAI.Relations] Failed to load config: {ex}");
                if (_cachedConfig != null && !Owner.IsPlaceholderGlobalSystemPrompt(_cachedConfig))
                {
                    _hasPendingPromptDomainRepairs = false;
                    Log.Warning("[RimAI.Relations] Load config failed. Returning cached config and blocking repair writeback.");
                    return _cachedConfig;
                }

                throw Owner.CreateDefaultConfigLoadFailureException("load_config_exception", ex);
            }
        }


                public void SaveConfig(SystemPromptConfig config)
        {
            try
            {
                Owner.EnsureDirectoryExists();

                if (config == null)
                {
                    Log.Warning("[RimAI.Relations] Attempted to save null config");
                    return;
                }

                if (Owner.TryRepairPlaceholderGlobalSystemPrompt(ref config, "config save request"))
                {
                    ModuleLog.Message("[RimAI.Relations] Rebuilt placeholder GlobalSystemPrompt before saving custom config.");
                }

                if (Owner.IsPlaceholderGlobalSystemPrompt(config))
                {
                    Log.Error("[RimAI.Relations] Refusing to save placeholder GlobalSystemPrompt into prompt domain files.");
                    return;
                }

                host.Normalization.EnsureConfigDefaults(config);
                host.WorkspaceComposer.SyncLegacyPromptMirrorsFromSections(config);
                Owner.SavePromptDomainFiles(config);
                _cachedConfig = config;
                if (!Owner.TryGetConfigLastWriteTimeUtc(out DateTime writeTimeUtc))
                {
                    _cachedConfigWriteTimeUtc = DateTime.MinValue;
                }
                else
                {
                    _cachedConfigWriteTimeUtc = writeTimeUtc;
                }

                ModuleLog.Message($"[RimAI.Relations] Saved SystemPromptConfig to: {ConfigFilePath}");
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
                Owner.DeletePromptDomainCustomFiles();
                RpgPromptCustomStore.DeleteCustomConfig();
                FactionPromptManager.Instance.ResetAllConfigs();
                _cachedConfig = Owner.CreateDefaultConfig();
                _cachedConfigWriteTimeUtc = DateTime.MinValue;
                ModuleLog.Message("[RimAI.Relations] Reset SystemPromptConfig to default");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAI.Relations] Failed to reset config: {ex}");
            }
        }
        }

}
