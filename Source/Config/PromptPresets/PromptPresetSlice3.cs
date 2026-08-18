using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Persistence;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Serialization;
using Ustas.RimAI.Core.Storage;

namespace Ustas.RimAI.Communication.Relations.Config
{
internal sealed class PromptPresetSlice3 : PromptPresetServiceCollaborator
    {
        internal PromptPresetSlice3(PromptPresetService owner) : base(owner)
        {
        }

internal static void AtomicWriteText(string path, string tempPath, string content)
        {
            // tempPath retained for call-site compatibility; AtomicFileWriter owns path+".tmp".
            _ = tempPath;
            AtomicFileWriter.WriteAllText(path, content);
        }

internal static void SaveStoreToPath(string path, PromptPresetStoreConfig store)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && !LocalStorage.Current.DirectoryExists(dir))
            {
                LocalStorage.Current.CreateDirectory(dir);
            }

            string json = ReflectionJsonFieldSerializer.Serialize(store, prettyPrint: true);
            string tempPath = path + ".tmp";
            try
            {
                PromptPresetService.AtomicWriteText(path, tempPath, json);
            }
            finally
            {
                if (LocalStorage.Current.FileExists(tempPath))
                {
                    LocalStorage.Current.DeleteFile(tempPath);
                }
            }
        }

internal static void MirrorStoreToLegacyPath(string primaryPath)
        {
            string legacyPath = PromptPresetService.GetLegacyStorePath();
            if (string.IsNullOrWhiteSpace(legacyPath) ||
                string.Equals(primaryPath, legacyPath, StringComparison.OrdinalIgnoreCase) ||
                !LocalStorage.Current.FileExists(primaryPath))
            {
                return;
            }

            try
            {
                string legacyDir = Path.GetDirectoryName(legacyPath);
                if (!string.IsNullOrWhiteSpace(legacyDir) && !LocalStorage.Current.DirectoryExists(legacyDir))
                {
                    LocalStorage.Current.CreateDirectory(legacyDir);
                }

                LocalStorage.Current.CopyFile(primaryPath, legacyPath, overwrite: true);
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimAI.Relations] Failed to mirror preset store to legacy path: {ex.Message}");
            }
        }

internal static void TryMigrateLegacyStoreToConfigPath()
        {
            string targetPath = PromptPresetService.GetStorePath();
            if (LocalStorage.Current.FileExists(targetPath))
            {
                return;
            }

            string legacyPath = PromptPresetService.GetLegacyStorePath();
            if (string.IsNullOrWhiteSpace(legacyPath) || !LocalStorage.Current.FileExists(legacyPath))
            {
                return;
            }

            try
            {
                PromptPresetService.EnsureStoreDirectory();
                LocalStorage.Current.CopyFile(legacyPath, targetPath, overwrite: false);
                Log.Message($"[RimAI.Relations] Migrated preset store to config path: {targetPath}");
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimAI.Relations] Failed to migrate legacy preset store: {ex.Message}");
            }
        }

internal static PromptPresetChannelPayloads CaptureCurrentPayload(RelationsSettings settings)
        {
            return new PromptPresetChannelPayloads
            {
                Diplomacy = new PromptChannelPayload
                {
                    SystemPromptCustomJson = PromptPresetService.ReadOrEmpty(PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.SystemPromptCustomFileName)),
                    DialoguePromptCustomJson = PromptPresetService.ReadOrEmpty(PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.DiplomacyPromptCustomFileName)),
                    SocialCirclePromptCustomJson = PromptPresetService.ReadOrEmpty(PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.SocialCirclePromptCustomFileName)),
                    FactionPromptsCustomJson = PromptPresetService.ReadOrEmpty(PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.FactionPromptCustomFileName))
                },
                Rpg = new PromptChannelPayload
                {
                    PawnPromptCustomJson = PromptPresetService.ReadOrEmpty(PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.PawnPromptCustomFileName))
                },
                UnifiedPromptCatalog = settings?.GetPromptUnifiedCatalogClone() ?? PromptUnifiedCatalog.CreateFallback(),
                RimTalkSummaryHistoryLimit = settings?.GetRimTalkSummaryHistoryLimitClamped() ?? 10,
                RimTalkAutoPushSessionSummary = settings?.RimTalkAutoPushSessionSummary ?? false,
                RimTalkAutoInjectCompatPreset = settings?.RimTalkAutoInjectCompatPreset ?? false,
                RimTalkPersonaCopyTemplate = settings?.GetRimTalkPersonaCopyTemplateOrDefault() ?? RelationsSettings.DefaultRimTalkPersonaCopyTemplate
            };
        }

internal static PromptPresetConfig CreateCanonicalDefaultPreset(string name)
        {
            PromptPresetConfig preset = PromptPresetService.BuildPresetShell(name);
            preset.Id = ImmutableDefaultPresetId;
            preset.Name = ImmutableDefaultPresetName;
            preset.ChannelPayloads = PromptPresetService.CreateCanonicalDefaultPayload();
            return preset;
        }

internal static void EnforceImmutableDefaultPreset(PromptPresetStoreConfig store)
        {
            if (store == null)
            {
                return;
            }

            store.Presets ??= new List<PromptPresetConfig>();
            PromptPresetConfig canonical = PromptPresetService.CreateCanonicalDefaultPreset(ImmutableDefaultPresetName);
            PromptPresetConfig existing = store.Presets.FirstOrDefault(p => PromptPresetService.IsImmutableDefaultId(p?.Id));
            if (existing == null)
            {
                store.Presets.Insert(0, canonical);
                existing = canonical;
            }
            else
            {
                existing.Id = ImmutableDefaultPresetId;
                existing.Name = ImmutableDefaultPresetName;
                existing.ChannelPayloads = canonical.ChannelPayloads.Clone();
                existing.CreatedAtUtc = string.IsNullOrWhiteSpace(existing.CreatedAtUtc)
                    ? canonical.CreatedAtUtc
                    : existing.CreatedAtUtc;
                existing.UpdatedAtUtc = existing.CreatedAtUtc;
            }

            for (int i = store.Presets.Count - 1; i >= 0; i--)
            {
                PromptPresetConfig current = store.Presets[i];
                if (current == null)
                {
                    continue;
                }

                if (PromptPresetService.IsImmutableDefaultId(current.Id) && !ReferenceEquals(current, existing))
                {
                    store.Presets.RemoveAt(i);
                }
            }

            store.DefaultPresetId = ImmutableDefaultPresetId;
        }

internal static PromptPresetChannelPayloads CreateCanonicalDefaultPayload()
        {
            var payload = new PromptPresetChannelPayloads
            {
                Diplomacy = new PromptChannelPayload
                {
                    SystemPromptCustomJson = PromptPresetService.ReadDefaultOrEmpty(PromptDomainFileCatalog.SystemPromptDefaultFileName),
                    DialoguePromptCustomJson = PromptPresetService.ReadDefaultOrEmpty(PromptDomainFileCatalog.DiplomacyPromptDefaultFileName),
                    SocialCirclePromptCustomJson = PromptPresetService.ReadDefaultOrEmpty(PromptDomainFileCatalog.SocialCirclePromptDefaultFileName),
                    FactionPromptsCustomJson = PromptPresetService.ReadDefaultOrEmpty(PromptDomainFileCatalog.FactionPromptDefaultFileName)
                },
                Rpg = new PromptChannelPayload
                {
                    PawnPromptCustomJson = PromptPresetService.ReadDefaultOrEmpty(PromptDomainFileCatalog.PawnPromptDefaultFileName)
                },
                UnifiedPromptCatalog = PromptPresetService.LoadCanonicalDefaultUnifiedCatalog(),
                RimTalkSummaryHistoryLimit = 10,
                RimTalkAutoPushSessionSummary = false,
                RimTalkAutoInjectCompatPreset = false,
                RimTalkPersonaCopyTemplate = RelationsSettings.DefaultRimTalkPersonaCopyTemplate
            };
            PromptPresetService.NormalizePayload(payload);
            return payload;
        }

internal static void NormalizePayload(PromptPresetChannelPayloads payload)
        {
            if (payload == null)
            {
                return;
            }

            payload.Diplomacy ??= new PromptChannelPayload();
            payload.Rpg ??= new PromptChannelPayload();
            payload.UnifiedPromptCatalog ??= PromptUnifiedCatalog.CreateFallback();
            payload.UnifiedPromptCatalog.NormalizeWith(PromptUnifiedCatalog.CreateFallback());
            payload.RimTalkSummaryHistoryLimit = Mathf.Clamp(
                payload.RimTalkSummaryHistoryLimit,
                RelationsSettings.RimTalkSummaryHistoryMin,
                RelationsSettings.RimTalkSummaryHistoryMax);
            payload.RimTalkPersonaCopyTemplate = string.IsNullOrWhiteSpace(payload.RimTalkPersonaCopyTemplate)
                ? RelationsSettings.DefaultRimTalkPersonaCopyTemplate
                : payload.RimTalkPersonaCopyTemplate.Trim();
            payload.Diplomacy.SystemPromptCustomJson ??= string.Empty;
            payload.Diplomacy.DialoguePromptCustomJson ??= string.Empty;
            payload.Diplomacy.SocialCirclePromptCustomJson ??= string.Empty;
            payload.Diplomacy.FactionPromptsCustomJson ??= string.Empty;
            payload.Rpg.PawnPromptCustomJson ??= string.Empty;
        }

internal static void ApplyLegacyRpgNodeMigrationIfNeeded(PromptPresetConfig preset)
        {
            if (preset?.ChannelPayloads?.UnifiedPromptCatalog == null)
            {
                return;
            }

            PromptUnifiedCatalog catalog = preset.ChannelPayloads.UnifiedPromptCatalog;
            if (catalog.MigrationVersion >= LegacyRpgNodeMigrationVersion)
            {
                return;
            }

            PromptUnifiedCatalog authoritative = PromptUnifiedCatalog.CreateFallback();
            int overriddenCount = 0;
            string[] channels =
            {
                RimTalkPromptEntryChannelCatalog.RpgDialogue,
                RimTalkPromptEntryChannelCatalog.ProactiveRpgDialogue
            };
            string[] nodeIds =
            {
                "rpg_relationship_profile",
                "rpg_kinship_boundary",
                "rpg_proactive_romance",
                "rpg_role_setting_fallback"
            };

            foreach (string channel in channels)
            {
                foreach (string nodeId in nodeIds)
                {
                    string authoritativeValue = authoritative.ResolveNode(channel, nodeId);
                    if (!string.IsNullOrWhiteSpace(authoritativeValue))
                    {
                        catalog.SetNode(channel, nodeId, authoritativeValue);
                        overriddenCount++;
                    }
                }
            }

            catalog.MigrationVersion = LegacyRpgNodeMigrationVersion;
            Log.Message($"[RimAI.Relations] Legacy RPG node migration applied to preset '{preset.Id}': {overriddenCount} nodes overridden, new migrationVersion={catalog.MigrationVersion}.");
        }

internal static void ApplyLegacySocialNewsNodeMigrationIfNeeded(PromptPresetConfig preset)
        {
            if (preset?.ChannelPayloads?.UnifiedPromptCatalog == null)
            {
                return;
            }

            PromptUnifiedCatalog catalog = preset.ChannelPayloads.UnifiedPromptCatalog;
            if (catalog.MigrationVersion >= LegacySocialNewsNodeMigrationVersion)
            {
                return;
            }

            PromptUnifiedCatalog authoritative = PromptUnifiedCatalog.CreateFallback();
            int overriddenCount = 0;
            overriddenCount += PromptPresetService.TryOverrideLegacySocialNewsNode(catalog, authoritative, "social_news_style", "文风：中性新闻播报");
            overriddenCount += PromptPresetService.TryOverrideLegacySocialNewsNode(catalog, authoritative, "social_news_json_contract", "如果 quote 为空，quote_attribution 也必须为空。", "narrative_mode");
            overriddenCount += PromptPresetService.TryOverrideLegacySocialNewsNode(catalog, authoritative, "social_news_fact", "基于以下事实种子生成一条社交圈世界新闻卡片。", "narrative_mode={{ world.social.narrative_mode }}");
            catalog.MigrationVersion = LegacySocialNewsNodeMigrationVersion;
            if (overriddenCount > 0)
            {
                Log.Message($"[RimAI.Relations] Legacy social news node migration applied to preset '{preset.Id}': {overriddenCount} nodes overridden, new migrationVersion={catalog.MigrationVersion}.");
            }
        }

internal static int TryOverrideLegacySocialNewsNode(
            PromptUnifiedCatalog catalog,
            PromptUnifiedCatalog authoritative,
            string nodeId,
            string legacyMarker,
            string missingMarker = null)
        {
            string current = catalog.ResolveNode(RimTalkPromptEntryChannelCatalog.Any, nodeId)?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(current) || !current.Contains(legacyMarker))
            {
                return 0;
            }

            if (!string.IsNullOrWhiteSpace(missingMarker) && current.Contains(missingMarker))
            {
                return 0;
            }

            string authoritativeValue = authoritative.ResolveNode(RimTalkPromptEntryChannelCatalog.Any, nodeId);
            if (string.IsNullOrWhiteSpace(authoritativeValue))
            {
                return 0;
            }

            catalog.SetNode(RimTalkPromptEntryChannelCatalog.Any, nodeId, authoritativeValue);
            return 1;
        }

internal static bool ShouldCreateMigratedPreset(
            PromptPresetChannelPayloads legacyPayload,
            PromptPresetChannelPayloads canonicalPayload)
        {
            PromptPresetService.NormalizePayload(legacyPayload);
            PromptPresetService.NormalizePayload(canonicalPayload);
            if (!PromptPresetService.HasMeaningfulLegacyPayload(legacyPayload))
            {
                return false;
            }

            return !PromptPresetService.ArePayloadsEquivalent(legacyPayload, canonicalPayload);
        }

internal static bool HasMeaningfulLegacyPayload(PromptPresetChannelPayloads payload)
        {
            if (payload == null)
            {
                return false;
            }

            bool hasCustomPromptFiles = !string.IsNullOrWhiteSpace(payload.Diplomacy?.SystemPromptCustomJson) ||
                                        !string.IsNullOrWhiteSpace(payload.Diplomacy?.DialoguePromptCustomJson) ||
                                        !string.IsNullOrWhiteSpace(payload.Diplomacy?.SocialCirclePromptCustomJson) ||
                                        !string.IsNullOrWhiteSpace(payload.Diplomacy?.FactionPromptsCustomJson) ||
                                        !string.IsNullOrWhiteSpace(payload.Rpg?.PawnPromptCustomJson);
            if (hasCustomPromptFiles)
            {
                return true;
            }

            if (payload.RimTalkSummaryHistoryLimit != 10 ||
                payload.RimTalkAutoPushSessionSummary ||
                payload.RimTalkAutoInjectCompatPreset ||
                !PromptPresetService.AreUnifiedCatalogsEquivalent(payload.UnifiedPromptCatalog, PromptPresetService.LoadCanonicalDefaultUnifiedCatalog()) ||
                !string.Equals(
                    PromptPresetService.NormalizeText(payload.RimTalkPersonaCopyTemplate),
                    PromptPresetService.NormalizeText(RelationsSettings.DefaultRimTalkPersonaCopyTemplate),
                    StringComparison.Ordinal))
            {
                return true;
            }
            return false;
        }

internal static PromptUnifiedCatalog LoadCanonicalDefaultUnifiedCatalog()
        {
            PromptUnifiedCatalog loaded = null;
            string defaultPath = PromptDomainFileCatalog.GetDefaultPath(PromptDomainFileCatalog.PromptUnifiedDefaultFileName);
            if (!string.IsNullOrWhiteSpace(defaultPath) && LocalStorage.Current.FileExists(defaultPath))
            {
                try
                {
                    string rawJson = LocalStorage.Current.ReadAllText(defaultPath);
                    loaded = JsonUtility.FromJson<PromptUnifiedCatalog>(rawJson);
                }
                catch (Exception ex)
                {
                    Log.Warning($"[RimAI.Relations] Failed to parse default unified prompt catalog: {ex.Message}");
                }
            }

            loaded ??= PromptUnifiedCatalog.CreateFallback();
            loaded.NormalizeWith(PromptUnifiedCatalog.CreateFallback());
            loaded.LegacyMigrated = true;

            return loaded;
        }

internal static bool ArePayloadsEquivalent(PromptPresetChannelPayloads left, PromptPresetChannelPayloads right)
        {
            if (left == null || right == null)
            {
                return left == right;
            }

            return string.Equals(PromptPresetService.NormalizeText(left.Diplomacy?.SystemPromptCustomJson), PromptPresetService.NormalizeText(right.Diplomacy?.SystemPromptCustomJson), StringComparison.Ordinal) &&
                   string.Equals(PromptPresetService.NormalizeText(left.Diplomacy?.DialoguePromptCustomJson), PromptPresetService.NormalizeText(right.Diplomacy?.DialoguePromptCustomJson), StringComparison.Ordinal) &&
                   string.Equals(PromptPresetService.NormalizeText(left.Diplomacy?.SocialCirclePromptCustomJson), PromptPresetService.NormalizeText(right.Diplomacy?.SocialCirclePromptCustomJson), StringComparison.Ordinal) &&
                   string.Equals(PromptPresetService.NormalizeText(left.Diplomacy?.FactionPromptsCustomJson), PromptPresetService.NormalizeText(right.Diplomacy?.FactionPromptsCustomJson), StringComparison.Ordinal) &&
                   string.Equals(PromptPresetService.NormalizeText(left.Rpg?.PawnPromptCustomJson), PromptPresetService.NormalizeText(right.Rpg?.PawnPromptCustomJson), StringComparison.Ordinal) &&
                   left.RimTalkSummaryHistoryLimit == right.RimTalkSummaryHistoryLimit &&
                   left.RimTalkAutoPushSessionSummary == right.RimTalkAutoPushSessionSummary &&
                   left.RimTalkAutoInjectCompatPreset == right.RimTalkAutoInjectCompatPreset &&
                   PromptPresetService.AreUnifiedCatalogsEquivalent(left.UnifiedPromptCatalog, right.UnifiedPromptCatalog) &&
                   string.Equals(PromptPresetService.NormalizeText(left.RimTalkPersonaCopyTemplate), PromptPresetService.NormalizeText(right.RimTalkPersonaCopyTemplate), StringComparison.Ordinal);
        }
    }
}
