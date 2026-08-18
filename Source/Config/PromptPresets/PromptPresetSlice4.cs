using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Persistence;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Serialization;

using LegacyPromptPresetChannelPayloads = Ustas.RimAI.Communication.Relations.Config.PromptPresetService.LegacyPromptPresetChannelPayloads;
using LegacyPromptPresetStoreConfig = Ustas.RimAI.Communication.Relations.Config.PromptPresetService.LegacyPromptPresetStoreConfig;
using LegacyPromptPresetConfig = Ustas.RimAI.Communication.Relations.Config.PromptPresetService.LegacyPromptPresetConfig;
using Ustas.RimAI.Core.Storage;

namespace Ustas.RimAI.Communication.Relations.Config
{
internal sealed class PromptPresetSlice4 : PromptPresetServiceCollaborator
    {
        internal PromptPresetSlice4(PromptPresetService owner) : base(owner)
        {
        }

internal static bool AreUnifiedCatalogsEquivalent(
            PromptUnifiedCatalog left,
            PromptUnifiedCatalog right)
        {
            PromptUnifiedCatalog normalizedLeft = left?.Clone() ?? PromptUnifiedCatalog.CreateFallback();
            PromptUnifiedCatalog normalizedRight = right?.Clone() ?? PromptUnifiedCatalog.CreateFallback();
            normalizedLeft.NormalizeWith(PromptUnifiedCatalog.CreateFallback());
            normalizedRight.NormalizeWith(PromptUnifiedCatalog.CreateFallback());
            return string.Equals(
                PromptDomainJsonUtility.Serialize(normalizedLeft, prettyPrint: false) ?? string.Empty,
                PromptDomainJsonUtility.Serialize(normalizedRight, prettyPrint: false) ?? string.Empty,
                StringComparison.Ordinal);
        }

internal static bool AreChannelConfigsEquivalent(RimTalkChannelCompatConfig left, RimTalkChannelCompatConfig right)
        {
            if (left == null || right == null)
            {
                return left == right;
            }

            List<RimTalkPromptEntryConfig> leftEntries = left.PromptEntries ?? new List<RimTalkPromptEntryConfig>();
            List<RimTalkPromptEntryConfig> rightEntries = right.PromptEntries ?? new List<RimTalkPromptEntryConfig>();
            if (leftEntries.Count != rightEntries.Count)
            {
                return false;
            }

            for (int i = 0; i < leftEntries.Count; i++)
            {
                if (!PromptPresetService.ArePromptEntriesEquivalent(leftEntries[i], rightEntries[i]))
                {
                    return false;
                }
            }

            return left.EnablePromptCompat == right.EnablePromptCompat &&
                   left.PresetInjectionMaxEntries == right.PresetInjectionMaxEntries &&
                   left.PresetInjectionMaxChars == right.PresetInjectionMaxChars &&
                   string.Equals(PromptPresetService.NormalizeText(left.CompatTemplate), PromptPresetService.NormalizeText(right.CompatTemplate), StringComparison.Ordinal);
        }

internal static bool ArePromptEntriesEquivalent(RimTalkPromptEntryConfig left, RimTalkPromptEntryConfig right)
        {
            if (left == null || right == null)
            {
                return left == right;
            }

            return string.Equals(PromptPresetService.NormalizeText(left.SectionId), PromptPresetService.NormalizeText(right.SectionId), StringComparison.Ordinal) &&
                   string.Equals(PromptPresetService.NormalizeText(left.Name), PromptPresetService.NormalizeText(right.Name), StringComparison.Ordinal) &&
                   string.Equals(PromptPresetService.NormalizeText(left.Role), PromptPresetService.NormalizeText(right.Role), StringComparison.Ordinal) &&
                   string.Equals(PromptPresetService.NormalizeText(left.CustomRole), PromptPresetService.NormalizeText(right.CustomRole), StringComparison.Ordinal) &&
                   string.Equals(PromptPresetService.NormalizeText(left.Position), PromptPresetService.NormalizeText(right.Position), StringComparison.Ordinal) &&
                   left.InChatDepth == right.InChatDepth &&
                   left.Enabled == right.Enabled &&
                   string.Equals(PromptPresetService.NormalizeText(left.PromptChannel), PromptPresetService.NormalizeText(right.PromptChannel), StringComparison.Ordinal) &&
                   string.Equals(PromptPresetService.NormalizeText(left.Content), PromptPresetService.NormalizeText(right.Content), StringComparison.Ordinal);
        }

internal static void ApplyPayloadToCustomFiles(PromptPresetChannelPayloads payload)
        {
            PromptPresetChannelPayloads data = payload ?? new PromptPresetChannelPayloads();
            PromptPresetService.WriteIfNotNull(PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.SystemPromptCustomFileName), data.Diplomacy?.SystemPromptCustomJson);
            PromptPresetService.WriteIfNotNull(PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.DiplomacyPromptCustomFileName), data.Diplomacy?.DialoguePromptCustomJson);
            PromptPresetService.WriteIfNotNull(PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.SocialCirclePromptCustomFileName), data.Diplomacy?.SocialCirclePromptCustomJson);
            PromptPresetService.WriteIfNotNull(PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.FactionPromptCustomFileName), data.Diplomacy?.FactionPromptsCustomJson);
        }

internal static void PersistRpgPromptCustomStore(PromptPresetChannelPayloads payload)
        {
            PromptPresetChannelPayloads data = payload ?? new PromptPresetChannelPayloads();
            RpgPromptCustomConfig config = PromptPresetService.ParseRpgPromptCustomConfig(data.Rpg?.PawnPromptCustomJson)
                                           ?? new RpgPromptCustomConfig();
            config.RimTalkSummaryHistoryLimit = Mathf.Clamp(
                data.RimTalkSummaryHistoryLimit,
                RelationsSettings.RimTalkSummaryHistoryMin,
                RelationsSettings.RimTalkSummaryHistoryMax);
            config.RimTalkAutoPushSessionSummary = data.RimTalkAutoPushSessionSummary;
            config.RimTalkAutoInjectCompatPreset = data.RimTalkAutoInjectCompatPreset;
            config.RimTalkPersonaCopyTemplate = string.IsNullOrWhiteSpace(data.RimTalkPersonaCopyTemplate)
                ? RelationsSettings.DefaultRimTalkPersonaCopyTemplate
                : data.RimTalkPersonaCopyTemplate;
            RpgPromptCustomStore.Save(config);
        }

internal static void ApplyRimTalkCompatSettings(RelationsSettings settings, PromptPresetChannelPayloads payload, bool persistToFiles)
        {
            PromptPresetChannelPayloads data = payload ?? new PromptPresetChannelPayloads();
            settings.RimTalkSummaryHistoryLimit = Mathf.Clamp(data.RimTalkSummaryHistoryLimit, RelationsSettings.RimTalkSummaryHistoryMin, RelationsSettings.RimTalkSummaryHistoryMax);
            settings.RimTalkAutoPushSessionSummary = data.RimTalkAutoPushSessionSummary;
            settings.RimTalkAutoInjectCompatPreset = data.RimTalkAutoInjectCompatPreset;
            settings.RimTalkPersonaCopyTemplate = string.IsNullOrWhiteSpace(data.RimTalkPersonaCopyTemplate)
                ? RelationsSettings.DefaultRimTalkPersonaCopyTemplate
                : data.RimTalkPersonaCopyTemplate;
            settings.SetPromptUnifiedCatalog(data.UnifiedPromptCatalog, persistToFiles: persistToFiles);
        }

internal static string ReadOrEmpty(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !LocalStorage.Current.FileExists(path))
            {
                return string.Empty;
            }

            return LocalStorage.Current.ReadAllText(path);
        }

internal static void WriteIfNotNull(string path, string payload)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(payload))
            {
                if (LocalStorage.Current.FileExists(path))
                {
                    LocalStorage.Current.DeleteFile(path);
                }

                return;
            }

            string trimmed = payload.Trim();
            if (!trimmed.StartsWith("{", StringComparison.Ordinal) &&
                !trimmed.StartsWith("[", StringComparison.Ordinal))
            {
                Log.Warning($"[RimAI.Relations] Skip writing preset payload because content is not JSON. Path: {path}");
                return;
            }

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && !LocalStorage.Current.DirectoryExists(dir))
            {
                LocalStorage.Current.CreateDirectory(dir);
            }

            LocalStorage.Current.WriteAllText(path, payload);
        }

internal static RpgPromptCustomConfig ParseRpgPromptCustomConfig(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            string trimmed = json.Trim();
            if (!trimmed.StartsWith("{", StringComparison.Ordinal))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<RpgPromptCustomConfig>(trimmed);
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimAI.Relations] Failed to parse RPG prompt custom payload for preset activation: {ex.Message}");
                return null;
            }
        }

internal static string EnsureUniqueName(List<PromptPresetConfig> presets, string name)
        {
            string baseName = string.IsNullOrWhiteSpace(name) ? "Preset" : name.Trim();
            List<PromptPresetConfig> all = presets ?? new List<PromptPresetConfig>();
            string candidate = baseName;
            int n = 2;
            while (all.Any(p => string.Equals(p.Name, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                candidate = $"{baseName} {n}";
                n++;
            }

            return candidate;
        }

internal static void ApplyLegacyPayloadsFromStoreJson(PromptPresetStoreConfig store, string rawJson)
        {
            if (store?.Presets == null ||
                string.IsNullOrWhiteSpace(rawJson) ||
                !PromptPresetService.ShouldApplyLegacyPayloadOverlay(rawJson))
            {
                return;
            }

            LegacyPromptPresetStoreConfig legacy = JsonUtility.FromJson<LegacyPromptPresetStoreConfig>(rawJson);
            if (legacy?.Presets == null)
            {
                return;
            }

            int count = Math.Min(store.Presets.Count, legacy.Presets.Count);
            for (int i = 0; i < count; i++)
            {
                PromptPresetService.ApplyLegacyPayload(store.Presets[i], legacy.Presets[i]?.ChannelPayloads, $"preset.store.{i}");
            }
        }

internal static void ApplyLegacyPayloadFromJson(PromptPresetConfig preset, string rawJson, string sourceId)
        {
            if (preset == null ||
                string.IsNullOrWhiteSpace(rawJson) ||
                !PromptPresetService.ShouldApplyLegacyPayloadOverlay(rawJson))
            {
                return;
            }

            LegacyPromptPresetConfig legacy = JsonUtility.FromJson<LegacyPromptPresetConfig>(rawJson);
            PromptPresetService.ApplyLegacyPayload(preset, legacy?.ChannelPayloads, sourceId);
        }

internal static bool ShouldApplyLegacyPayloadOverlay(string rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return false;
            }

            return rawJson.IndexOf("\"PromptSectionCatalog\"", StringComparison.Ordinal) >= 0 ||
                   rawJson.IndexOf("\"EnableRimTalkPromptCompat\"", StringComparison.Ordinal) >= 0 ||
                   rawJson.IndexOf("\"RimTalkPresetInjectionMaxEntries\"", StringComparison.Ordinal) >= 0 ||
                   rawJson.IndexOf("\"RimTalkPresetInjectionMaxChars\"", StringComparison.Ordinal) >= 0 ||
                   rawJson.IndexOf("\"RimTalkCompatTemplate\"", StringComparison.Ordinal) >= 0;
        }

internal static void ApplyLegacyPayload(
            PromptPresetConfig preset,
            LegacyPromptPresetChannelPayloads legacyPayload,
            string sourceId)
        {
            if (preset?.ChannelPayloads == null || legacyPayload == null)
            {
                return;
            }

            RimTalkPromptEntryDefaultsConfig sections = PromptLegacyCompatMigration.ApplyLegacyPayloadToPromptSections(
                legacyPayload.PromptSectionCatalog,
                legacyPayload.EnableRimTalkPromptCompat,
                legacyPayload.RimTalkPresetInjectionMaxEntries,
                legacyPayload.RimTalkPresetInjectionMaxChars,
                legacyPayload.RimTalkCompatTemplate,
                legacyPayload.RimTalkDiplomacy,
                legacyPayload.RimTalkRpg,
                sourceId);
            PromptUnifiedCatalog unified = preset.ChannelPayloads.UnifiedPromptCatalog?.Clone() ?? PromptUnifiedCatalog.CreateFallback();
            PromptPresetService.ApplyLegacySectionsToUnifiedCatalog(unified, sections);
            preset.ChannelPayloads.UnifiedPromptCatalog = unified;
        }

internal static void ApplyLegacySectionsToUnifiedCatalog(
            PromptUnifiedCatalog unified,
            RimTalkPromptEntryDefaultsConfig sections)
        {
            if (unified == null)
            {
                return;
            }

            RimTalkPromptEntryDefaultsConfig normalized = PromptLegacyCompatMigration.NormalizePromptSections(sections);
            foreach (RimTalkPromptChannelDefaultsConfig channel in normalized.Channels ?? new List<RimTalkPromptChannelDefaultsConfig>())
            {
                if (channel == null || string.IsNullOrWhiteSpace(channel.PromptChannel))
                {
                    continue;
                }

                foreach (RimTalkPromptSectionDefaultConfig section in channel.Sections ?? new List<RimTalkPromptSectionDefaultConfig>())
                {
                    if (section == null || string.IsNullOrWhiteSpace(section.SectionId))
                    {
                        continue;
                    }

                    unified.SetSection(channel.PromptChannel, section.SectionId, section.Content ?? string.Empty);
                }
            }
        }

internal static bool HasMeaningfulPayload(PromptPresetChannelPayloads payload)
        {
            if (payload == null)
            {
                return false;
            }

            bool diplomacy = !string.IsNullOrWhiteSpace(payload.Diplomacy?.SystemPromptCustomJson) ||
                             !string.IsNullOrWhiteSpace(payload.Diplomacy?.DialoguePromptCustomJson) ||
                             !string.IsNullOrWhiteSpace(payload.Diplomacy?.SocialCirclePromptCustomJson) ||
                             !string.IsNullOrWhiteSpace(payload.Diplomacy?.FactionPromptsCustomJson);
            bool rpg = !string.IsNullOrWhiteSpace(payload.Rpg?.PawnPromptCustomJson);
            bool unified = !PromptPresetService.AreUnifiedCatalogsEquivalent(payload.UnifiedPromptCatalog, PromptUnifiedCatalog.CreateFallback());
            return diplomacy || rpg || unified;
        }
    }
}
