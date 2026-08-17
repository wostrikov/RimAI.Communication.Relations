using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.UI;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.Persistence;

namespace Ustas.RimAI.Communication.Relations.Config;

internal static class RelationsPromptCatalogMigration
{
        internal static bool IsRpgArchiveCompressionOutputSpecificationInvalid(string text)
        {
            string normalized = (text ?? string.Empty).Trim();
            if (normalized.Length == 0)
            {
                return true;
            }

            return normalized.IndexOf("response_contract", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("JSON", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("compressed_summary", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static string GetRpgArchiveCompressionSectionDefault(string sectionId)
        {
            string normalized = PromptSectionSchemaCatalog.NormalizeSectionId(sectionId);
            switch (normalized)
            {
                case "system_rules":
                    return RelationsSettings.RpgArchiveCompressionSystemRulesText;
                case "output_specification":
                    return RelationsSettings.RpgArchiveCompressionOutputSpecificationText;
                default:
                    return string.Empty;
            }
        }

internal static void ApplyLegacyImageTemplateMigration(this RelationsSettings settings, PromptUnifiedCatalog catalog)
        {
            settings.DiplomacyImagePromptTemplates ??= new List<DiplomacyImagePromptTemplate>();
            DiplomacyImageTemplateDefaults.EnsureDefaults(settings.DiplomacyImagePromptTemplates);
            foreach (DiplomacyImagePromptTemplate template in settings.DiplomacyImagePromptTemplates.Where(item => item != null))
            {
                string id = PromptUnifiedTemplateAliasConfig.NormalizeTemplateId(template.Id);
                if (id.Length == 0)
                {
                    continue;
                }

                catalog.SetTemplateAlias(
                    RimTalkPromptEntryChannelCatalog.ImageGeneration,
                    id,
                    template.Name,
                    template.Description,
                    template.Text,
                    template.Enabled);
            }

            MirrorImageAlias(catalog, "diplomacy_scene", DiplomacyImageTemplateDefaults.DefaultTemplateId);
            MirrorImageAlias(catalog, "diplomacyscene", DiplomacyImageTemplateDefaults.DefaultTemplateId);
            MirrorImageAlias(catalog, "diplomacy_image", DiplomacyImageTemplateDefaults.DefaultTemplateId);
            MirrorImageAlias(catalog, "diplomacyimage", DiplomacyImageTemplateDefaults.DefaultTemplateId);
            MirrorImageAlias(catalog, "leader_portrait", DiplomacyImageTemplateDefaults.DefaultTemplateId);
        }

        internal static void MirrorImageAlias(PromptUnifiedCatalog catalog, string aliasId, string targetTemplateId)
        {
            if (catalog == null)
            {
                return;
            }

            PromptUnifiedTemplateAliasConfig target = catalog.ResolveTemplateAlias(
                RimTalkPromptEntryChannelCatalog.ImageGeneration,
                targetTemplateId);
            if (target == null || string.IsNullOrWhiteSpace(target.Content))
            {
                return;
            }

            catalog.SetTemplateAlias(
                RimTalkPromptEntryChannelCatalog.ImageGeneration,
                aliasId,
                target.Name,
                target.Description,
                target.Content,
                target.Enabled);
        }

        internal static bool ApplyStaticLiteralNodeDefaults(PromptUnifiedCatalog catalog)
        {
            if (catalog == null)
            {
                return false;
            }

            bool changed = false;
            string[] diplomacyChannels =
            {
                RimTalkPromptEntryChannelCatalog.Any,
                RimTalkPromptEntryChannelCatalog.DiplomacyDialogue,
                RimTalkPromptEntryChannelCatalog.ProactiveDiplomacyDialogue
            };

            foreach (string channel in diplomacyChannels)
            {
                changed |= SetNodeIfDifferent(
                    catalog,
                    channel,
                    "api_limits_node_template",
                    PromptTextConstants.ApiLimitsNodeLiteralDefault);
                changed |= SetNodeIfDifferent(
                    catalog,
                    channel,
                    "quest_guidance_node_template",
                    PromptTextConstants.QuestGuidanceNodeLiteralDefault);
                changed |= SetNodeIfDifferent(
                    catalog,
                    channel,
                    "response_contract_node_template",
                    PromptTextConstants.ResponseContractNodeLiteralDefault);
            }

            return changed;
        }

        internal static bool SetNodeIfDifferent(
            PromptUnifiedCatalog catalog,
            string channel,
            string nodeId,
            string targetText)
        {
            string current = (catalog.ResolveNode(channel, nodeId) ?? string.Empty).Trim();
            string target = (targetText ?? string.Empty).Trim();
            if (string.Equals(current, target, StringComparison.Ordinal))
            {
                return false;
            }

            catalog.SetNode(channel, nodeId, target);
            return true;
        }


        internal static string BuildPersonaBootstrapOutputSection(string templateLine, string exampleLine)
        {
            string template = (templateLine ?? string.Empty).Trim();
            string example = (exampleLine ?? string.Empty).Trim();
            if (template.Length == 0 && example.Length == 0)
            {
                return string.Empty;
            }

            // A strict JSON template must stay untouched so the workbench section remains parser-safe.
            if ((template.StartsWith("{", StringComparison.Ordinal) && template.EndsWith("}", StringComparison.Ordinal)) ||
                (template.StartsWith("[", StringComparison.Ordinal) && template.EndsWith("]", StringComparison.Ordinal)))
            {
                return template;
            }

            if (example.Length == 0)
            {
                return template;
            }

            if (template.Length == 0)
            {
                return "Example:\n" + example;
            }

            return template + "\n\nExample:\n" + example;
        }

        internal static void CopySectionIfNotEmpty(PromptUnifiedCatalog catalog, string channel, string sectionId, string content)
        {
            string text = content?.Trim() ?? string.Empty;
            if (text.Length == 0 || catalog == null)
            {
                return;
            }

            catalog.SetSection(channel, sectionId, text);
        }

        internal static void CopyNodeIfNotEmpty(PromptUnifiedCatalog catalog, string channel, string nodeId, string content)
        {
            string text = content?.Trim() ?? string.Empty;
            if (text.Length == 0 || catalog == null)
            {
                return;
            }

            catalog.SetNode(channel, nodeId, text);
        }

        internal static RimTalkPromptChannel ParseChannel(string channel)
        {
            if (string.Equals(channel, "diplomacy", StringComparison.OrdinalIgnoreCase))
            {
                return RimTalkPromptChannel.Diplomacy;
            }

            return RimTalkPromptChannel.Rpg;
        }

internal static void ClampRimTalkCompatSettings(this RelationsSettings settings)
        {
            settings.EnsurePromptSectionCatalogReady();
            settings.RimTalkSummaryHistoryLimit = Mathf.Clamp(
                settings.RimTalkSummaryHistoryLimit,
                RelationsSettings.RimTalkSummaryHistoryMin,
                RelationsSettings.RimTalkSummaryHistoryMax);
            settings.ExpandMemoryPawnMemoryMaxChars = Mathf.Clamp(
                settings.ExpandMemoryPawnMemoryMaxChars,
                Persistence.PromptPersistenceService.ExpandMemoryPawnMemoryMaxCharsMin,
                Persistence.PromptPersistenceService.ExpandMemoryPawnMemoryMaxCharsMax);
            settings.ExpandMemoryPawnMemoryMaxEntries = Mathf.Clamp(
                settings.ExpandMemoryPawnMemoryMaxEntries,
                Persistence.PromptPersistenceService.ExpandMemoryPawnMemoryMaxEntriesMin,
                Persistence.PromptPersistenceService.ExpandMemoryPawnMemoryMaxEntriesMax);
            settings.PromptSectionCatalog = PromptLegacyCompatMigration.NormalizePromptSections(settings.PromptSectionCatalog);
            settings.RimTalkPersonaCopyTemplate = NormalizePersonaCopyTemplateToStrictScriban(settings.RimTalkPersonaCopyTemplate);
            if (settings.RimTalkPersonaCopyTemplate.Length > RelationsSettings.RimTalkPersonaCopyTemplateMaxLength)
            {
                settings.RimTalkPersonaCopyTemplate = settings.RimTalkPersonaCopyTemplate.Substring(0, RelationsSettings.RimTalkPersonaCopyTemplateMaxLength);
            }
        }

        internal static string NormalizePersonaCopyTemplateToStrictScriban(string template)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                return RelationsSettings.DefaultRimTalkPersonaCopyTemplate;
            }

            string trimmed = template.Trim();
            if (string.Equals(trimmed, "pawn.personality", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "{{pawn.personality}}", StringComparison.OrdinalIgnoreCase))
            {
                return RelationsSettings.DefaultRimTalkPersonaCopyTemplate;
            }

            return trimmed;
        }

internal static RimTalkPromptEntryDefaultsConfig GetPromptSectionCatalogClone(this RelationsSettings settings)
        {
            settings.EnsurePromptSectionCatalogReady();
            return settings.UnifiedPromptCatalog?.ToSectionCatalog() ?? PromptLegacyCompatMigration.NormalizePromptSections(settings.PromptSectionCatalog);
        }

internal static void SetPromptSectionCatalog(this RelationsSettings settings, RimTalkPromptEntryDefaultsConfig sections)
        {
            throw new InvalidOperationException(
                "SetPromptSectionCatalog is migration-only and cannot be used in the editable workflow. " +
                "Use ImportLegacySectionCatalogToUnifiedCatalog instead.");
        }

internal static void ImportLegacySectionCatalogToUnifiedCatalog(this RelationsSettings settings, RimTalkPromptEntryDefaultsConfig sections, string sourceId, bool persistToFiles = true)
        {
            settings.PromptSectionCatalog = PromptLegacyCompatMigration.NormalizePromptSections(sections);
            settings.EnsureUnifiedCatalogReady();
            foreach (RimTalkPromptChannelDefaultsConfig channel in settings.PromptSectionCatalog.Channels ?? new System.Collections.Generic.List<RimTalkPromptChannelDefaultsConfig>())
            {
                if (channel == null)
                {
                    continue;
                }

                foreach (RimTalkPromptSectionDefaultConfig section in channel.Sections ?? new System.Collections.Generic.List<RimTalkPromptSectionDefaultConfig>())
                {
                    if (section == null)
                    {
                        continue;
                    }

                    settings.UnifiedPromptCatalog.SetSection(channel.PromptChannel, section.SectionId, section.Content);
                }
            }

            settings.ApplyUnifiedCatalogPersistence(persistToFiles);
            PromptLegacyCompatMigration.ResetLegacyFields(settings);
            RelationsSettingsPages.For(settings).PromptWorkspace._promptWorkspaceBufferedChannel = string.Empty;
            RelationsSettingsPages.For(settings).PromptWorkspace._promptWorkspaceBufferedSectionId = string.Empty;
            RelationsSettingsPages.For(settings).PromptWorkspace.InvalidatePromptWorkspacePreviewCache();
        }

internal static void SetPromptSectionText(this RelationsSettings settings, string promptChannel, string sectionId, string content, bool persistToFiles = true)
        {
            settings.EnsurePromptSectionCatalogReady();
            settings.UnifiedPromptCatalog.SetSection(promptChannel, sectionId, content ?? string.Empty);
            settings.PromptSectionCatalog = settings.UnifiedPromptCatalog.ToSectionCatalog();
            settings.ApplyUnifiedCatalogPersistence(persistToFiles);
            RelationsSettingsPages.For(settings).PromptWorkspace.InvalidatePromptWorkspacePreviewCache();
        }

internal static string ResolvePromptSectionText(this RelationsSettings settings, string promptChannel, string sectionId)
        {
            settings.EnsurePromptSectionCatalogReady();
            return settings.UnifiedPromptCatalog?.ResolveSection(promptChannel, sectionId) ?? string.Empty;
        }

internal static string ResolvePromptNodeText(this RelationsSettings settings, string promptChannel, string nodeId)
        {
            settings.EnsurePromptSectionCatalogReady();
            return settings.UnifiedPromptCatalog?.ResolveNode(promptChannel, nodeId) ?? string.Empty;
        }

internal static PromptUnifiedTemplateAliasConfig ResolvePromptTemplateAlias(this RelationsSettings settings, string promptChannel, string templateId)
        {
            settings.EnsurePromptSectionCatalogReady();
            return settings.UnifiedPromptCatalog?.ResolveTemplateAlias(promptChannel, templateId);
        }

internal static PromptUnifiedTemplateAliasConfig ResolvePreferredPromptTemplateAlias(this RelationsSettings settings, string promptChannel, string preferredTemplateId)
        {
            settings.EnsurePromptSectionCatalogReady();
            return settings.UnifiedPromptCatalog?.ResolvePreferredTemplateAlias(promptChannel, preferredTemplateId);
        }

internal static List<PromptUnifiedTemplateAliasConfig> GetPromptTemplateAliases(this RelationsSettings settings, string promptChannel)
        {
            settings.EnsurePromptSectionCatalogReady();
            return settings.UnifiedPromptCatalog?.GetTemplateAliases(promptChannel) ?? new List<PromptUnifiedTemplateAliasConfig>();
        }

internal static PromptUnifiedCatalog GetPromptUnifiedCatalogClone(this RelationsSettings settings)
        {
            settings.EnsurePromptSectionCatalogReady();
            return settings.UnifiedPromptCatalog?.Clone() ?? PromptUnifiedCatalog.CreateFallback();
        }

internal static void SetPromptUnifiedCatalog(this RelationsSettings settings, PromptUnifiedCatalog catalog, bool persistToFiles = true)
        {
            settings.EnsurePromptSectionCatalogReady();
            settings.UnifiedPromptCatalog = catalog?.Clone() ?? PromptUnifiedCatalog.CreateFallback();
            settings.UnifiedPromptCatalog.NormalizeWith(PromptUnifiedCatalog.CreateFallback());
            // Treat applied unified payload as modern source-of-truth and skip legacy backfill overwrite.
            settings.UnifiedPromptCatalog.LegacyMigrated = true;
            if (persistToFiles)
            {
                settings.ApplyUnifiedCatalogPersistence(persistToFiles: true);
            }
            else
            {
                settings._promptUnifiedCatalogLoaded = true;
                settings._promptUnifiedCatalogDirty = false;
            }

            settings.PromptSectionCatalog = settings.UnifiedPromptCatalog.ToSectionCatalog();
            PromptLegacyCompatMigration.ResetLegacyFields(settings);
            RelationsSettingsPages.For(settings).PromptWorkspace._promptWorkspaceBufferedChannel = string.Empty;
            RelationsSettingsPages.For(settings).PromptWorkspace._promptWorkspaceBufferedSectionId = string.Empty;
            RelationsSettingsPages.For(settings).PromptWorkspace.InvalidatePromptWorkspacePreviewCache();
        }

internal static void EnsurePawnPersonalityTokenForRpgChannelsSafe(this RelationsSettings settings)
        {
            try
            {
                settings.EnsurePromptSectionCatalogReady();
                string[] channels =
                {
                    RimTalkPromptEntryChannelCatalog.RpgDialogue,
                    RimTalkPromptEntryChannelCatalog.ProactiveRpgDialogue
                };

                bool changed = false;
                const string sectionId = "character_persona";
                foreach (string channel in channels)
                {
                    if (string.IsNullOrWhiteSpace(channel))
                    {
                        continue;
                    }

                    string current = settings.UnifiedPromptCatalog.ResolveSection(channel, sectionId) ?? string.Empty;
                    const string variableName = "pawn.personality";
                    if (RelationsRimTalkTemplateEditors.ContainsVariableToken(current, variableName))
                    {
                        continue;
                    }

                    const string token = "{{ pawn.personality }}";
                    string updated = string.IsNullOrWhiteSpace(current)
                        ? token
                        : current.TrimEnd() + "\n" + token;
                    settings.UnifiedPromptCatalog.SetSection(channel, sectionId, updated);
                    changed = true;
                }

                if (changed)
                {
                    settings.ApplyUnifiedCatalogPersistence(persistToFiles: true);
                    settings.PromptSectionCatalog = settings.UnifiedPromptCatalog.ToSectionCatalog();
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimAI.Relations] Failed to ensure RPG persona token coverage: {ex.Message}");
            }
        }

internal static void SetPromptNodeText(this RelationsSettings settings, string promptChannel, string nodeId, string content, bool persistToFiles = true)
        {
            settings.EnsurePromptSectionCatalogReady();
            settings.UnifiedPromptCatalog.SetNode(promptChannel, nodeId, content ?? string.Empty);
            settings.ApplyUnifiedCatalogPersistence(persistToFiles);
            RelationsSettingsPages.For(settings).PromptWorkspace.InvalidatePromptWorkspacePreviewCache();
        }

internal static List<PromptUnifiedNodeLayoutConfig> GetPromptNodeLayouts(this RelationsSettings settings, string promptChannel)
        {
            settings.EnsurePromptSectionCatalogReady();
            return settings.UnifiedPromptCatalog
                .GetOrderedNodeLayouts(promptChannel)
                .Select(item => item.Clone())
                .ToList();
        }

internal static List<PromptSectionLayoutConfig> GetPromptSectionLayouts(this RelationsSettings settings, string promptChannel)
        {
            settings.EnsurePromptSectionCatalogReady();
            return settings.UnifiedPromptCatalog
                .GetOrderedSectionLayouts(promptChannel)
                .Select(item => item.Clone())
                .ToList();
        }

internal static void SavePromptSectionLayouts(this RelationsSettings settings, string promptChannel, IEnumerable<PromptSectionLayoutConfig> layouts, bool persistToFiles = true)
        {
            settings.EnsurePromptSectionCatalogReady();
            string channel = RimTalkPromptEntryChannelCatalog.NormalizeLoose(promptChannel);
            List<PromptSectionLayoutConfig> ordered = (layouts ?? Enumerable.Empty<PromptSectionLayoutConfig>())
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.SectionId))
                .Select(item => item.Clone())
                .OrderBy(item => item.Order)
                .ThenBy(item => item.SectionId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            int nextOrder = 0;
            foreach (PromptSectionLayoutConfig item in ordered)
            {
                settings.UnifiedPromptCatalog.SetSectionLayout(channel, item.SectionId, nextOrder);
                nextOrder++;
            }

            settings.ApplyUnifiedCatalogPersistence(persistToFiles);
            RelationsSettingsPages.For(settings).PromptWorkspace.InvalidatePromptWorkspacePreviewCache();
        }

internal static PromptUnifiedNodeLayoutConfig ResolvePromptNodeLayout(this RelationsSettings settings, string promptChannel, string nodeId)
        {
            settings.EnsurePromptSectionCatalogReady();
            return settings.UnifiedPromptCatalog.ResolveNodeLayout(promptChannel, nodeId);
        }

internal static void SetPromptNodeLayout(this RelationsSettings settings, string promptChannel, string nodeId, PromptUnifiedNodeSlot slot, int order, bool enabled, bool persistToFiles = true)
        {
            settings.EnsurePromptSectionCatalogReady();
            settings.UnifiedPromptCatalog.SetNodeLayout(promptChannel, nodeId, slot, order, enabled);
            settings.ApplyUnifiedCatalogPersistence(persistToFiles);
            RelationsSettingsPages.For(settings).PromptWorkspace.InvalidatePromptWorkspacePreviewCache();
        }

internal static void SavePromptNodeLayouts(this RelationsSettings settings, string promptChannel, IEnumerable<PromptUnifiedNodeLayoutConfig> layouts, bool persistToFiles = true)
        {
            settings.EnsurePromptSectionCatalogReady();
            string channel = RimTalkPromptEntryChannelCatalog.NormalizeLoose(promptChannel);
            List<PromptUnifiedNodeLayoutConfig> ordered = (layouts ?? Enumerable.Empty<PromptUnifiedNodeLayoutConfig>())
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.NodeId))
                .Select(item => item.Clone())
                .OrderBy(item => item.GetSlot())
                .ThenBy(item => item.Order)
                .ThenBy(item => item.NodeId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var nextOrderBySlot = new Dictionary<PromptUnifiedNodeSlot, int>();
            foreach (PromptUnifiedNodeLayoutConfig item in ordered)
            {
                PromptUnifiedNodeSlot slot = item.GetSlot();
                if (!nextOrderBySlot.TryGetValue(slot, out int nextOrder))
                {
                    nextOrder = 0;
                }

                settings.UnifiedPromptCatalog.SetNodeLayout(channel, item.NodeId, slot, nextOrder, item.Enabled);
                nextOrderBySlot[slot] = nextOrder + 1;
            }

            settings.ApplyUnifiedCatalogPersistence(persistToFiles);
            RelationsSettingsPages.For(settings).PromptWorkspace.InvalidatePromptWorkspacePreviewCache();
        }

internal static bool HasPendingUnifiedPromptCatalogChanges(this RelationsSettings settings)
        {
            settings.EnsurePromptSectionCatalogReady();
            return settings._promptUnifiedCatalogDirty;
        }

internal static void PersistUnifiedPromptCatalogToCustom(this RelationsSettings settings)
        {
            settings.EnsurePromptSectionCatalogReady();
            PromptUnifiedCatalogProvider.SaveCustom(settings.UnifiedPromptCatalog);
            settings._promptUnifiedCatalogDirty = false;
        }

internal static void ReloadPromptUnifiedCatalogFromStorage(this RelationsSettings settings)
        {
            settings.UnifiedPromptCatalog = PromptUnifiedCatalogProvider.LoadMerged() ?? PromptUnifiedCatalog.CreateFallback();
            settings.UnifiedPromptCatalog.NormalizeWith(PromptUnifiedCatalog.CreateFallback());
            settings.PromptSectionCatalog = settings.UnifiedPromptCatalog.ToSectionCatalog();
            settings._promptUnifiedCatalogLoaded = true;
            settings._promptUnifiedCatalogDirty = false;
            RelationsSettingsPages.For(settings).PromptWorkspace.InvalidatePromptWorkspacePreviewCache();
        }

internal static void ApplyUnifiedCatalogPersistence(this RelationsSettings settings, bool persistToFiles)
        {
            settings._promptUnifiedCatalogLoaded = true;
            if (persistToFiles)
            {
                PromptUnifiedCatalogProvider.SaveCustom(settings.UnifiedPromptCatalog);
                settings._promptUnifiedCatalogDirty = false;
                return;
            }

            settings._promptUnifiedCatalogDirty = true;
        }
}
