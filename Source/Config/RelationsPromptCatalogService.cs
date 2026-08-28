using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.UI;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Diagnostics;

namespace Ustas.RimAI.Communication.Relations.Config;

internal static class RelationsPromptCatalogService
{
public static bool IsExpandMemoryCompatEnabled(this RelationsSettings settings)
        {
            string mode = (settings.ExpandMemoryCompatMode ?? "auto").ToLowerInvariant();
            if (mode == "on") return true;
            if (mode == "off") return false;
            return Prompting.PromptRuntimeVariableBridge.IsDependencyAvailable("expandmemory");
        }

public static bool IsExpandMemoryPawnMemoryEnabled(this RelationsSettings settings)
        {
            return settings.ExpandMemoryInjectPawnMemory;
        }

public static bool IsAnyRimTalkPromptCompatEnabled(this RelationsSettings settings)
        {
            return false;
        }

public static bool IsRimTalkPromptCompatEnabled(this RelationsSettings settings, string channel)
        {
            return false;
        }

internal static RimTalkChannelCompatConfig GetRimTalkChannelConfig(this RelationsSettings settings, RimTalkPromptChannel channel)
        {
            settings.EnsurePromptSectionCatalogReady();
            return PromptLegacyCompatMigration.CreateLegacyAdapterFromPromptSections(settings.PromptSectionCatalog, channel);
        }

internal static RimTalkChannelCompatConfig GetRimTalkChannelConfigClone(this RelationsSettings settings, RimTalkPromptChannel channel)
        {
            return settings.GetRimTalkChannelConfig(channel).Clone();
        }

internal static void SetRimTalkChannelConfig(this RelationsSettings settings, RimTalkPromptChannel channel, RimTalkChannelCompatConfig config)
        {
            settings.EnsurePromptSectionCatalogReady();
            string sourceId = channel == RimTalkPromptChannel.Diplomacy ? "settings.diplomacy" : "settings.rpg";
            settings.PromptSectionCatalog = PromptLegacyCompatMigration.ApplyLegacyAdapterToPromptSections(
                settings.PromptSectionCatalog,
                config,
                channel,
                sourceId);
            settings.ClampRimTalkCompatSettings();
            RelationsSettingsPages.For(settings).PromptWorkbench.SyncWorkbenchEditingChannelConfig(channel, settings.GetRimTalkChannelConfig(channel));
        }

public static int GetRimTalkSummaryHistoryLimitClamped(this RelationsSettings settings)
        {
            return Mathf.Clamp(settings.RimTalkSummaryHistoryLimit, RelationsSettings.RimTalkSummaryHistoryMin, RelationsSettings.RimTalkSummaryHistoryMax);
        }

public static int GetRimTalkPresetInjectionMaxEntriesClamped(this RelationsSettings settings, string channel)
        {
            RimTalkChannelCompatConfig config = settings.GetRimTalkChannelConfig(RelationsPromptCatalogMigration.ParseChannel(channel));
            return Mathf.Clamp(
                config.PresetInjectionMaxEntries,
                RelationsSettings.RimTalkPresetInjectionMaxEntriesMin,
                RelationsSettings.RimTalkPresetInjectionMaxEntriesMax);
        }

public static int GetRimTalkPresetInjectionMaxEntriesClamped(this RelationsSettings settings)
        {
            return settings.GetRimTalkPresetInjectionMaxEntriesClamped("rpg");
        }

public static int GetRimTalkPresetInjectionMaxCharsClamped(this RelationsSettings settings, string channel)
        {
            RimTalkChannelCompatConfig config = settings.GetRimTalkChannelConfig(RelationsPromptCatalogMigration.ParseChannel(channel));
            return Mathf.Clamp(
                config.PresetInjectionMaxChars,
                RelationsSettings.RimTalkPresetInjectionMaxCharsMin,
                RelationsSettings.RimTalkPresetInjectionMaxCharsMax);
        }

public static int GetRimTalkPresetInjectionMaxCharsClamped(this RelationsSettings settings)
        {
            return settings.GetRimTalkPresetInjectionMaxCharsClamped("rpg");
        }

public static string GetRimTalkCompatTemplateOrDefault(this RelationsSettings settings, string channel)
        {
            RimTalkChannelCompatConfig config = settings.GetRimTalkChannelConfig(RelationsPromptCatalogMigration.ParseChannel(channel));
            return config.CompatTemplate;
        }

public static string GetRimTalkCompatTemplateOrDefault(this RelationsSettings settings)
        {
            return settings.GetRimTalkCompatTemplateOrDefault("rpg");
        }

public static string GetRimTalkPersonaCopyTemplateOrDefault(this RelationsSettings settings)
        {
            settings.ClampRimTalkCompatSettings();
            return settings.RimTalkPersonaCopyTemplate;
        }

public static bool IsRimTalkSummaryPushEnabled(this RelationsSettings settings)
        {
            return settings.RimTalkAutoPushSessionSummary;
        }

public static bool IsRimTalkAutoPresetSyncEnabled(this RelationsSettings settings)
        {
            return settings.RimTalkAutoInjectCompatPreset;
        }

internal static void EnsureRimTalkChannelMigration(this RelationsSettings settings)
        {
            settings.EnsurePromptSectionCatalogReady();
        }

internal static void SyncLegacyRimTalkFieldsFromRpgChannel(this RelationsSettings settings)
        {
            PromptLegacyCompatMigration.ResetLegacyFields(settings);
        }

internal static void ResetLegacyCompatLoadPayload(this RelationsSettings settings)
        {
            settings._legacyEnableRimTalkPromptCompat = false;
            settings._legacyRimTalkPresetInjectionMaxEntries = RelationsSettings.RimTalkPresetInjectionLimitUnlimited;
            settings._legacyRimTalkPresetInjectionMaxChars = RelationsSettings.RimTalkPresetInjectionLimitUnlimited;
            settings._legacyRimTalkCompatTemplate = string.Empty;
            settings._legacyRimTalkChannelSplitMigrated = true;
            settings._legacyRimTalkDiplomacy = RimTalkChannelCompatConfig.CreateDefault();
            settings._legacyRimTalkRpg = RimTalkChannelCompatConfig.CreateDefault();
        }

internal static void EnsurePromptSectionCatalogReady(this RelationsSettings settings)
        {
            if (settings._isEnsuringPromptCatalog)
            {
                return;
            }

            settings._isEnsuringPromptCatalog = true;
            try
            {
                if (!settings._promptUnifiedCatalogLoaded || settings.UnifiedPromptCatalog == null)
                {
                    settings.UnifiedPromptCatalog = PromptUnifiedCatalogProvider.LoadMerged();
                    settings._promptUnifiedCatalogLoaded = true;
                }

                settings.PromptSectionCatalog = PromptLegacyCompatMigration.NormalizePromptSections(settings.PromptSectionCatalog);
                RimTalkPromptEntryDefaultsConfig.TryUpgradeLegacyAnyDefaults(settings.PromptSectionCatalog);
                if (settings._legacyPromptCompatImported)
                {
                    settings.EnsureUnifiedCatalogReady();
                    return;
                }

                settings.PromptSectionCatalog = PromptLegacyCompatMigration.ApplyLegacyPayloadToPromptSections(
                    settings.PromptSectionCatalog,
                    settings._legacyEnableRimTalkPromptCompat,
                    settings._legacyRimTalkPresetInjectionMaxEntries,
                    settings._legacyRimTalkPresetInjectionMaxChars,
                    settings._legacyRimTalkCompatTemplate,
                    settings._legacyRimTalkDiplomacy,
                    settings._legacyRimTalkRpg,
                    "settings");
                PromptLegacyCompatMigration.ResetLegacyFields(settings);
                settings._legacyPromptCompatImported = true;
                settings.EnsureUnifiedCatalogReady();
            }
            finally
            {
                settings._isEnsuringPromptCatalog = false;
            }
        }

internal static void EnsureUnifiedCatalogReady(this RelationsSettings settings)
        {
            if (settings._isEnsuringUnifiedPromptCatalog)
            {
                return;
            }

            settings._isEnsuringUnifiedPromptCatalog = true;
            try
            {
                bool legacyMigratedChanged = false;
                bool migrationVersionChanged = false;
                settings.UnifiedPromptCatalog = settings.UnifiedPromptCatalog?.Clone() ?? PromptUnifiedCatalogProvider.LoadMerged();
                if (settings.UnifiedPromptCatalog == null)
                {
                    settings.UnifiedPromptCatalog = PromptUnifiedCatalog.CreateFallback();
                }

                if (!settings.UnifiedPromptCatalog.LegacyMigrated)
                {
                    // Avoid recursive settings->loadConfig->settings loops during workbench initialization.
                    PromptTemplateTextConfig templates = RelationsSettingsPages.For(settings).PromptLegacy._systemPromptConfig?.PromptTemplates ?? new PromptTemplateTextConfig();
                    settings.UnifiedPromptCatalog = PromptUnifiedCatalog.FromLegacy(settings.PromptSectionCatalog, templates);
                    settings.UnifiedPromptCatalog.LegacyMigrated = true;
                    legacyMigratedChanged = true;
                }

                PromptUnifiedCatalogNormalizeReport normalizeReport =
                    settings.UnifiedPromptCatalog.NormalizeWithReport(PromptUnifiedCatalog.CreateFallback());
                if (settings.UnifiedPromptCatalog.MigrationVersion < RelationsSettings.UnifiedCatalogMigrationTargetVersion)
                {
                    settings.ApplyUnifiedCatalogOneTimeMigration(settings.UnifiedPromptCatalog);
                    settings.UnifiedPromptCatalog.MigrationVersion = RelationsSettings.UnifiedCatalogMigrationTargetVersion;
                    migrationVersionChanged = true;
                    normalizeReport.Merge(settings.UnifiedPromptCatalog.NormalizeWithReport(PromptUnifiedCatalog.CreateFallback()));
                }
                bool literalDefaultsChanged = RelationsPromptCatalogMigration.ApplyStaticLiteralNodeDefaults(settings.UnifiedPromptCatalog);
                bool archiveCompressionSectionChanged = settings.EnsureRpgArchiveCompressionSectionContract(settings.UnifiedPromptCatalog);

                try
                {
                    settings.UnifiedPromptCatalog.ValidateInvariantsOrThrow();
                }
                catch (InvalidOperationException ex)
                {
                    Log.Error($"[RimAI.Relations] Unified prompt catalog invariant violation: {ex.Message}");
                    throw;
                }

                bool requiresSave = legacyMigratedChanged ||
                    migrationVersionChanged ||
                    normalizeReport.HasStructuralChange ||
                    literalDefaultsChanged ||
                    archiveCompressionSectionChanged;
                bool hasCleanup = normalizeReport.UnknownChannelCount > 0 ||
                    normalizeReport.RemovedNodeCount > 0 ||
                    normalizeReport.RemovedLayoutCount > 0;
                if (hasCleanup)
                {
                    Log.Warning(
                        $"[RimAI.Relations] Unified prompt catalog cleanup applied: " +
                        $"unknownChannels={normalizeReport.UnknownChannelCount}, " +
                        $"removedNodes={normalizeReport.RemovedNodeCount}, " +
                        $"removedLayouts={normalizeReport.RemovedLayoutCount}.");
                }

                if (normalizeReport.FilledDefaultLayoutCount > 0)
                {
                    ModuleLog.Message(
                        $"[RimAI.Relations] Unified prompt catalog filled {normalizeReport.FilledDefaultLayoutCount} missing node layouts.");
                }

                if (legacyMigratedChanged || migrationVersionChanged)
                {
                    ModuleLog.Message(
                        $"[RimAI.Relations] Unified prompt catalog migration applied " +
                        $"(legacyMigrated={legacyMigratedChanged}, migrationVersionUpdated={migrationVersionChanged}).");
                }
                if (literalDefaultsChanged)
                {
                    ModuleLog.Message("[RimAI.Relations] Unified prompt catalog applied static literal node defaults.");
                }
                if (archiveCompressionSectionChanged)
                {
                    ModuleLog.Message("[RimAI.Relations] Unified prompt catalog repaired rpg_archive_compression section contract.");
                }

                if (requiresSave)
                {
                    PromptUnifiedCatalogProvider.SaveCustom(settings.UnifiedPromptCatalog);
                    settings._promptUnifiedCatalogDirty = false;
                }

                settings.PromptSectionCatalog = settings.UnifiedPromptCatalog.ToSectionCatalog();
            }
            finally
            {
                settings._isEnsuringUnifiedPromptCatalog = false;
            }
        }

        internal static void ApplyUnifiedCatalogOneTimeMigration(this RelationsSettings settings, PromptUnifiedCatalog catalog) => RelationsPromptCatalogMigrationOps.ApplyUnifiedCatalogOneTimeMigration(settings, catalog);
        internal static bool EnsureRpgArchiveCompressionContractReady(this RelationsSettings settings) => RelationsPromptCatalogMigrationOps.EnsureRpgArchiveCompressionContractReady(settings);
        internal static void ApplyAnySystemRulesBackgroundMigration(PromptUnifiedCatalog catalog) => RelationsPromptCatalogMigrationOps.ApplyAnySystemRulesBackgroundMigration(catalog);
        internal static void ApplyCharacterPersonaStateAnchorMigration(PromptUnifiedCatalog catalog) => RelationsPromptCatalogMigrationOps.ApplyCharacterPersonaStateAnchorMigration(catalog);
        internal static void ApplyRpgStateAnchorSelfActionMigration(PromptUnifiedCatalog catalog) => RelationsPromptCatalogMigrationOps.ApplyRpgStateAnchorSelfActionMigration(catalog);
        internal static void ApplyLegacyRpgPromptMigration(this RelationsSettings settings, PromptUnifiedCatalog catalog) => RelationsPromptCatalogMigrationOps.ApplyLegacyRpgPromptMigration(settings, catalog);
        internal static void CopyLegacySectionsToUnifiedCatalog(PromptUnifiedCatalog catalog,
            RimTalkPromptEntryDefaultsConfig legacySections) => RelationsPromptCatalogMigrationOps.CopyLegacySectionsToUnifiedCatalog(catalog, legacySections);
        internal static string SanitizeLegacyRpgActionRulesText(string candidate) => RelationsPromptCatalogMigrationOps.SanitizeLegacyRpgActionRulesText(candidate);
        internal static void ApplyRpgOutputProtocolMigration(PromptUnifiedCatalog catalog) => RelationsPromptCatalogMigrationOps.ApplyRpgOutputProtocolMigration(catalog);
        internal static void ApplyRpgOutputProtocolMigrationForChannel(PromptUnifiedCatalog catalog, string promptChannel) => RelationsPromptCatalogMigrationOps.ApplyRpgOutputProtocolMigrationForChannel(catalog, promptChannel);
        internal static bool LooksLikeLegacyRpgProtocolText(string text) => RelationsPromptCatalogMigrationOps.LooksLikeLegacyRpgProtocolText(text);
        internal static bool ContainsPlaceholderActionPayload(string text) => RelationsPromptCatalogMigrationOps.ContainsPlaceholderActionPayload(text);
        internal static bool EnsureRpgArchiveCompressionSectionContract(this RelationsSettings settings, PromptUnifiedCatalog catalog)
        {
            if (catalog == null)
            {
                return false;
            }

            bool changed = false;
            for (int i = 0; i < RelationsSettings.RpgArchiveCompressionRequiredSectionIds.Length; i++)
            {
                string sectionId = RelationsSettings.RpgArchiveCompressionRequiredSectionIds[i];
                string expected = RelationsPromptCatalogMigration.GetRpgArchiveCompressionSectionDefault(sectionId);
                string current = catalog.ResolveSection(RimTalkPromptEntryChannelCatalog.RpgArchiveCompression, sectionId);
                string any = catalog.ResolveSection(RimTalkPromptEntryChannelCatalog.Any, sectionId);
                if (!settings.ShouldRepairRpgArchiveCompressionSection(sectionId, current, any, expected))
                {
                    continue;
                }

                catalog.SetSection(RimTalkPromptEntryChannelCatalog.RpgArchiveCompression, sectionId, expected);
                changed = true;
            }

            return changed;
        }

        internal static bool ShouldRepairRpgArchiveCompressionSection(this RelationsSettings settings, 
            string sectionId,
            string current,
            string any,
            string expected)
        {
            string normalizedSectionId = PromptSectionSchemaCatalog.NormalizeSectionId(sectionId);
            string currentText = (current ?? string.Empty).Trim();
            string anyText = (any ?? string.Empty).Trim();
            string expectedText = (expected ?? string.Empty).Trim();

            if (currentText.Length == 0)
            {
                // No content — only fill if we have a specific expected value
                return expectedText.Length > 0;
            }

            // For sections without a specific default (expected is empty),
            // inheriting from the "any" channel is the correct and intended behavior.
            // Do NOT flag settings as needing repair to avoid infinite fix-save-reload loops.
            if (expectedText.Length == 0)
            {
                return false;
            }

            // Current already matches expected — no repair needed
            if (string.Equals(currentText, expectedText, StringComparison.Ordinal))
            {
                return false;
            }

            // Current equals "any" channel but expected differs — override needed
            if (string.Equals(currentText, anyText, StringComparison.Ordinal))
            {
                return true;
            }

            // output_specification: special validation for invalid legacy content
            if (string.Equals(normalizedSectionId, "output_specification", StringComparison.Ordinal))
            {
                return RelationsPromptCatalogMigration.IsRpgArchiveCompressionOutputSpecificationInvalid(currentText);
            }

            // system_rules: special validation for legacy placeholder patterns
            if (string.Equals(normalizedSectionId, "system_rules", StringComparison.Ordinal))
            {
                return currentText.IndexOf("лишайся у світі", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    currentText.IndexOf("{{ ctx.channel }}", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    currentText.IndexOf("вислів у ролі", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            return false;
        }

}
