using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ustas.RimAI.Communication.Relations.Prompting;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Config
{
    using LegacyPromptCompatPayload = PromptLegacyCompatMigration.LegacyPromptCompatPayload;
    internal static class PromptLegacyCompatMigration
    {
        [Serializable]
        internal sealed class LegacyPromptCompatPayload
        {
            public bool EnableRimTalkPromptCompat = true;
            public int RimTalkPresetInjectionMaxEntries = RelationsSettings.RimTalkPresetInjectionLimitUnlimited;
            public int RimTalkPresetInjectionMaxChars = RelationsSettings.RimTalkPresetInjectionLimitUnlimited;
            public string RimTalkCompatTemplate = string.Empty;
            public RimTalkChannelCompatConfig RimTalkDiplomacy = null;
            public RimTalkChannelCompatConfig RimTalkRpg = null;
        }

        internal static readonly PromptSectionDefinition[] SectionDefinitions =
        {
            new PromptSectionDefinition("system_rules", "System Rules", "系统规则"),
            new PromptSectionDefinition("character_persona", "Persona", "角色人设", "Character Persona", "人物设定", "人格"),
            new PromptSectionDefinition("memory_system", "Memory", "记忆", "Memory System", "记忆系统"),
            new PromptSectionDefinition("environment_perception", "Environment", "环境感知", "Environment Perception", "环境"),
            new PromptSectionDefinition("context", "Context", "上下文"),
            new PromptSectionDefinition("action_rules", "Action Rules", "行为规则", "行动规则"),
            new PromptSectionDefinition("repetition_reinforcement", "Reinforcement", "强化规则", "Repetition Reinforcement", "重复强化", "强化"),
            new PromptSectionDefinition("output_specification", "Output Format", "输出格式", "Output Specification", "输出规范")
        };

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        internal static bool ShouldRejectMigratedContent(string content)
        {
            return LooksLikeRenderedStructuredPrompt(content) || LooksLikeCompiledPromptPreview(content);
        }

        

        

        

        

        

        

        internal sealed class LegacyTemplateSeed
        {
            public string SectionId = string.Empty;
            public string Name = string.Empty;
            public string PromptChannel = string.Empty;
            public string Content = string.Empty;
        }

        internal readonly struct PromptSectionDefinition
        {
            public readonly string Id;
            public readonly string EnglishName;
            public readonly string[] Aliases;

            public PromptSectionDefinition(string id, string englishName, params string[] aliases)
            {
                Id = id ?? string.Empty;
                EnglishName = englishName ?? string.Empty;
                Aliases = aliases ?? Array.Empty<string>();
            }

            public bool Matches(string normalizedToken)
            {
                if (string.Equals(NormalizeToken(Id), normalizedToken, StringComparison.Ordinal) ||
                    string.Equals(NormalizeToken(EnglishName), normalizedToken, StringComparison.Ordinal))
                {
                    return true;
                }

                for (int i = 0; i < Aliases.Length; i++)
                {
                    if (string.Equals(NormalizeToken(Aliases[i]), normalizedToken, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        #region Facade forwards
        public static LegacyPromptMigrationReport GetLatestReport() => PromptLegacyCompatMigrationReporting.GetLatestReport();
        internal static LegacyPromptMigrationReport CreateReport(string sourceId) => PromptLegacyCompatMigrationReporting.CreateReport(sourceId);
        internal static void RecordImported(LegacyPromptMigrationReport report, string sourceId, string promptChannel, string sectionId, bool rewritten) => PromptLegacyCompatMigrationReporting.RecordImported(report, sourceId, promptChannel, sectionId, rewritten);
        internal static void RecordRejected(LegacyPromptMigrationReport report, string sourceId, string promptChannel, string sectionId, string detail, bool fallbackApplied) => PromptLegacyCompatMigrationReporting.RecordRejected(report, sourceId, promptChannel, sectionId, detail, fallbackApplied);
        internal static void PublishReport(LegacyPromptMigrationReport report) => PromptLegacyCompatMigrationReporting.PublishReport(report);
        internal static string GetReportPath() => PromptLegacyCompatMigrationReporting.GetReportPath();
        #endregion
    
        #region Cluster forwards
        public static RimTalkPromptEntryDefaultsConfig ApplyLegacyPayloadToPromptSections(RimTalkPromptEntryDefaultsConfig currentSections, bool enablePromptCompat, int presetInjectionMaxEntries, int presetInjectionMaxChars, string compatTemplate, RimTalkChannelCompatConfig diplomacy, RimTalkChannelCompatConfig rpg, string sourceIdPrefix) => PromptLegacyCompatSlice1.ApplyLegacyPayloadToPromptSections(currentSections, enablePromptCompat, presetInjectionMaxEntries, presetInjectionMaxChars, compatTemplate, diplomacy, rpg, sourceIdPrefix);
        public static RimTalkPromptEntryDefaultsConfig ApplyLegacyPayloadToPromptSections(RimTalkPromptEntryDefaultsConfig currentSections, string rawJson, string sourceIdPrefix) => PromptLegacyCompatSlice1.ApplyLegacyPayloadToPromptSections(currentSections, rawJson, sourceIdPrefix);
        public static RimTalkChannelCompatConfig NormalizeChannelConfig(RimTalkChannelCompatConfig config, string channel, string idPrefix) => PromptLegacyCompatSlice1.NormalizeChannelConfig(config, channel, idPrefix);
        public static RimTalkChannelCompatConfig BuildFromLegacyFields(bool enablePromptCompat, int presetInjectionMaxEntries, int presetInjectionMaxChars, string compatTemplate, RimTalkChannelCompatConfig fallback, string channel, string idPrefix) => PromptLegacyCompatSlice1.BuildFromLegacyFields(enablePromptCompat, presetInjectionMaxEntries, presetInjectionMaxChars, compatTemplate, fallback, channel, idPrefix);
        public static RimTalkPromptEntryDefaultsConfig NormalizePromptSections(RimTalkPromptEntryDefaultsConfig sections) => PromptLegacyCompatSlice1.NormalizePromptSections(sections);
        public static bool HasMeaningfulLegacyChannelConfig(RimTalkChannelCompatConfig config) => PromptLegacyCompatSlice1.HasMeaningfulLegacyChannelConfig(config);
        public static RimTalkChannelCompatConfig CreateLegacyAdapterFromPromptSections(RimTalkPromptEntryDefaultsConfig sections, RimTalkPromptChannel rootChannel) => PromptLegacyCompatSlice1.CreateLegacyAdapterFromPromptSections(sections, rootChannel);
        public static RimTalkPromptEntryDefaultsConfig ApplyLegacyAdapterToPromptSections(RimTalkPromptEntryDefaultsConfig currentSections, RimTalkChannelCompatConfig config, RimTalkPromptChannel rootChannel, string sourceId, LegacyPromptMigrationReport report = null) => PromptLegacyCompatSlice1.ApplyLegacyAdapterToPromptSections(currentSections, config, rootChannel, sourceId, report);
        public static void ResetLegacyFields(RpgPromptCustomConfig config) => PromptLegacyCompatSlice1.ResetLegacyFields(config);
        public static void ResetLegacyFields(RelationsSettings settings) => PromptLegacyCompatSlice1.ResetLegacyFields(settings);
        internal static void ImportLegacyChannelConfig(RimTalkPromptEntryDefaultsConfig target, RimTalkChannelCompatConfig config, RimTalkPromptChannel rootChannel, string sourceId, LegacyPromptMigrationReport report) => PromptLegacyCompatSlice1.ImportLegacyChannelConfig(target, config, rootChannel, sourceId, report);
        internal static void ApplyDefaultSectionContent(RimTalkPromptEntryDefaultsConfig target, string promptChannel, string sectionId) => PromptLegacyCompatSlice1.ApplyDefaultSectionContent(target, promptChannel, sectionId);
        internal static string ComposeTemplateFromEntries(IEnumerable<RimTalkPromptEntryConfig> entries) => PromptLegacyCompatSlice1.ComposeTemplateFromEntries(entries);
        internal static void AppendChannelSections(ICollection<RimTalkPromptEntryConfig> entries, RimTalkPromptEntryDefaultsConfig sections, string promptChannel) => PromptLegacyCompatSlice1.AppendChannelSections(entries, sections, promptChannel);
        internal static List<RimTalkPromptEntryConfig> ExtractLegacyEntries(RimTalkChannelCompatConfig config, RimTalkPromptChannel rootChannel) => PromptLegacyCompatSlice2.ExtractLegacyEntries(config, rootChannel);
        internal static List<LegacyTemplateSeed> SplitCompatTemplate(string compatTemplate) => PromptLegacyCompatSlice2.SplitCompatTemplate(compatTemplate);
        internal static void FlushTemplateSeed(ICollection<LegacyTemplateSeed> target, string header, StringBuilder buffer) => PromptLegacyCompatSlice2.FlushTemplateSeed(target, header, buffer);
        internal static string ResolveSectionId(RimTalkPromptEntryConfig entry, int index) => PromptLegacyCompatSlice2.ResolveSectionId(entry, index);
        internal static string ResolveSectionId(string candidate) => PromptLegacyCompatSlice2.ResolveSectionId(candidate);
        internal static bool LooksLikeRenderedStructuredPrompt(string content) => PromptLegacyCompatSlice2.LooksLikeRenderedStructuredPrompt(content);
        internal static bool LooksLikeCompiledPromptPreview(string content) => PromptLegacyCompatSlice2.LooksLikeCompiledPromptPreview(content);
        internal static int CountMarkerHits(string content, IEnumerable<string> markers) => PromptLegacyCompatSlice2.CountMarkerHits(content, markers);
        internal static bool IsSectionHeader(string line) => PromptLegacyCompatSlice2.IsSectionHeader(line);
        internal static string CleanupHeader(string header) => PromptLegacyCompatSlice2.CleanupHeader(header);
        internal static string NormalizeToken(string value) => PromptLegacyCompatSlice2.NormalizeToken(value);
        #endregion
}
    internal static class PromptLegacyCompatSlice1
    {
public static RimTalkPromptEntryDefaultsConfig ApplyLegacyPayloadToPromptSections(
            RimTalkPromptEntryDefaultsConfig currentSections,
            bool enablePromptCompat,
            int presetInjectionMaxEntries,
            int presetInjectionMaxChars,
            string compatTemplate,
            RimTalkChannelCompatConfig diplomacy,
            RimTalkChannelCompatConfig rpg,
            string sourceIdPrefix)
        {
            RimTalkPromptEntryDefaultsConfig normalized = PromptLegacyCompatMigration.NormalizePromptSections(currentSections);
            LegacyPromptMigrationReport report = PromptLegacyCompatMigration.CreateReport(sourceIdPrefix);
            bool hasExplicitChannels =
                PromptLegacyCompatMigration.HasMeaningfulLegacyChannelConfig(diplomacy) ||
                PromptLegacyCompatMigration.HasMeaningfulLegacyChannelConfig(rpg);

            if (!hasExplicitChannels && string.IsNullOrWhiteSpace(compatTemplate))
            {
                PromptLegacyCompatMigration.PublishReport(report);
                return normalized;
            }

            RimTalkChannelCompatConfig diplomacyConfig = hasExplicitChannels
                ? PromptLegacyCompatMigration.NormalizeChannelConfig(diplomacy, "diplomacy", $"{sourceIdPrefix}.diplomacy")
                : PromptLegacyCompatMigration.BuildFromLegacyFields(
                    enablePromptCompat,
                    presetInjectionMaxEntries,
                    presetInjectionMaxChars,
                    compatTemplate,
                    diplomacy,
                    "diplomacy",
                    $"{sourceIdPrefix}.diplomacy");
            RimTalkChannelCompatConfig rpgConfig = hasExplicitChannels
                ? PromptLegacyCompatMigration.NormalizeChannelConfig(rpg, "rpg", $"{sourceIdPrefix}.rpg")
                : PromptLegacyCompatMigration.BuildFromLegacyFields(
                    enablePromptCompat,
                    presetInjectionMaxEntries,
                    presetInjectionMaxChars,
                    compatTemplate,
                    rpg,
                    "rpg",
                    $"{sourceIdPrefix}.rpg");

            if (PromptLegacyCompatMigration.HasMeaningfulLegacyChannelConfig(diplomacyConfig))
            {
                normalized = PromptLegacyCompatMigration.ApplyLegacyAdapterToPromptSections(
                    normalized,
                    diplomacyConfig,
                    RimTalkPromptChannel.Diplomacy,
                    $"{sourceIdPrefix}.diplomacy",
                    report);
            }

            if (PromptLegacyCompatMigration.HasMeaningfulLegacyChannelConfig(rpgConfig))
            {
                normalized = PromptLegacyCompatMigration.ApplyLegacyAdapterToPromptSections(
                    normalized,
                    rpgConfig,
                    RimTalkPromptChannel.Rpg,
                    $"{sourceIdPrefix}.rpg",
                    report);
            }

            PromptLegacyCompatMigration.PublishReport(report);
            return normalized;
        }

public static RimTalkPromptEntryDefaultsConfig ApplyLegacyPayloadToPromptSections(
            RimTalkPromptEntryDefaultsConfig currentSections,
            string rawJson,
            string sourceIdPrefix)
        {
            LegacyPromptMigrationReport report = PromptLegacyCompatMigration.CreateReport(sourceIdPrefix);
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                PromptLegacyCompatMigration.PublishReport(report);
                return PromptLegacyCompatMigration.NormalizePromptSections(currentSections);
            }

            try
            {
                LegacyPromptCompatPayload payload = JsonUtility.FromJson<LegacyPromptCompatPayload>(rawJson);
                if (payload == null)
                {
                    PromptLegacyCompatMigration.PublishReport(report);
                    return PromptLegacyCompatMigration.NormalizePromptSections(currentSections);
                }

                RimTalkPromptEntryDefaultsConfig migrated = PromptLegacyCompatMigration.ApplyLegacyPayloadToPromptSections(
                    currentSections,
                    payload.EnableRimTalkPromptCompat,
                    payload.RimTalkPresetInjectionMaxEntries,
                    payload.RimTalkPresetInjectionMaxChars,
                    payload.RimTalkCompatTemplate,
                    payload.RimTalkDiplomacy,
                    payload.RimTalkRpg,
                    sourceIdPrefix);
                return migrated;
            }
            catch (Exception ex)
            {
                PromptLegacyCompatMigration.RecordRejected(
                    report,
                    sourceIdPrefix,
                    string.Empty,
                    string.Empty,
                    $"Failed to parse legacy payload: {ex.Message}",
                    fallbackApplied: false);
                PromptLegacyCompatMigration.PublishReport(report);
                Log.Warning($"[RimAI.Relations] Failed to parse legacy compat payload for {sourceIdPrefix}: {ex.Message}");
                return PromptLegacyCompatMigration.NormalizePromptSections(currentSections);
            }
        }

public static RimTalkChannelCompatConfig NormalizeChannelConfig(
            RimTalkChannelCompatConfig config,
            string channel,
            string idPrefix)
        {
            RimTalkChannelCompatConfig normalized = (config ?? RimTalkChannelCompatConfig.CreateDefault()).Clone();
            normalized.NormalizeWith(RimTalkChannelCompatConfig.CreateDefault());
            if (string.IsNullOrWhiteSpace(normalized.CompatTemplate))
            {
                normalized.CompatTemplate = PromptLegacyCompatMigration.ComposeTemplateFromEntries(normalized.PromptEntries);
            }

            normalized.CompatTemplate = string.IsNullOrWhiteSpace(normalized.CompatTemplate)
                ? RelationsSettings.DefaultRimTalkCompatTemplate
                : normalized.CompatTemplate.Trim();
            PromptTemplateAutoRewriter.RewriteRimTalkChannelConfig(
                normalized,
                channel,
                ScribanPromptEngine.Instance,
                string.IsNullOrWhiteSpace(idPrefix) ? "legacy" : idPrefix);
            return normalized;
        }

public static RimTalkChannelCompatConfig BuildFromLegacyFields(
            bool enablePromptCompat,
            int presetInjectionMaxEntries,
            int presetInjectionMaxChars,
            string compatTemplate,
            RimTalkChannelCompatConfig fallback,
            string channel,
            string idPrefix)
        {
            RimTalkChannelCompatConfig config = fallback?.Clone() ?? RimTalkChannelCompatConfig.CreateDefault();
            config.EnablePromptCompat = enablePromptCompat;
            config.PresetInjectionMaxEntries = presetInjectionMaxEntries;
            config.PresetInjectionMaxChars = presetInjectionMaxChars;
            if (!string.IsNullOrWhiteSpace(compatTemplate))
            {
                config.CompatTemplate = compatTemplate.Trim();
            }

            return PromptLegacyCompatMigration.NormalizeChannelConfig(config, channel, idPrefix);
        }

public static RimTalkPromptEntryDefaultsConfig NormalizePromptSections(RimTalkPromptEntryDefaultsConfig sections)
        {
            RimTalkPromptEntryDefaultsConfig normalized = sections?.Clone() ?? RimTalkPromptEntryDefaultsProvider.GetDefaultsSnapshot();
            normalized.NormalizeWith(RimTalkPromptEntryDefaultsProvider.GetDefaultsSnapshot());
            return normalized;
        }

public static bool HasMeaningfulLegacyChannelConfig(RimTalkChannelCompatConfig config)
        {
            if (config == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(config.CompatTemplate) &&
                !RelationsSettings.IsShippedCompatTemplateDefault(config.CompatTemplate))
            {
                return true;
            }

            return config.PromptEntries != null &&
                   config.PromptEntries.Any(entry => entry != null && !string.IsNullOrWhiteSpace(entry.Content));
        }

public static RimTalkChannelCompatConfig CreateLegacyAdapterFromPromptSections(
            RimTalkPromptEntryDefaultsConfig sections,
            RimTalkPromptChannel rootChannel)
        {
            RimTalkPromptEntryDefaultsConfig normalizedSections = PromptLegacyCompatMigration.NormalizePromptSections(sections);
            var config = new RimTalkChannelCompatConfig
            {
                EnablePromptCompat = false,
                PresetInjectionMaxEntries = RelationsSettings.RimTalkPresetInjectionLimitUnlimited,
                PresetInjectionMaxChars = RelationsSettings.RimTalkPresetInjectionLimitUnlimited,
                CompatTemplate = string.Empty,
                PromptEntries = new List<RimTalkPromptEntryConfig>()
            };

            IReadOnlyList<string> channels = RimTalkPromptEntryChannelCatalog.GetSelectableChannels(rootChannel);
            for (int i = 0; i < channels.Count; i++)
            {
                PromptLegacyCompatMigration.AppendChannelSections(config.PromptEntries, normalizedSections, channels[i]);
            }

            string merged = PromptLegacyCompatMigration.ComposeTemplateFromEntries(config.PromptEntries);
            config.CompatTemplate = string.IsNullOrWhiteSpace(merged)
                ? RelationsSettings.DefaultRimTalkCompatTemplate
                : merged;
            return config;
        }

public static RimTalkPromptEntryDefaultsConfig ApplyLegacyAdapterToPromptSections(
            RimTalkPromptEntryDefaultsConfig currentSections,
            RimTalkChannelCompatConfig config,
            RimTalkPromptChannel rootChannel,
            string sourceId,
            LegacyPromptMigrationReport report = null)
        {
            RimTalkPromptEntryDefaultsConfig normalizedSections = PromptLegacyCompatMigration.NormalizePromptSections(currentSections);
            PromptLegacyCompatMigration.ImportLegacyChannelConfig(normalizedSections, config, rootChannel, sourceId, report);
            normalizedSections.NormalizeWith(RimTalkPromptEntryDefaultsProvider.GetDefaultsSnapshot());
            return normalizedSections;
        }

public static void ResetLegacyFields(RpgPromptCustomConfig config)
        {
            if (config == null)
            {
                return;
            }
            config.RimTalkPersonaCopyTemplate = string.IsNullOrWhiteSpace(config.RimTalkPersonaCopyTemplate)
                ? RelationsSettings.DefaultRimTalkPersonaCopyTemplate
                : config.RimTalkPersonaCopyTemplate;
        }

public static void ResetLegacyFields(RelationsSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            settings.ResetLegacyCompatLoadPayload();
        }

internal static void ImportLegacyChannelConfig(
            RimTalkPromptEntryDefaultsConfig target,
            RimTalkChannelCompatConfig config,
            RimTalkPromptChannel rootChannel,
            string sourceId,
            LegacyPromptMigrationReport report)
        {
            if (target == null || !PromptLegacyCompatMigration.HasMeaningfulLegacyChannelConfig(config))
            {
                return;
            }

            List<RimTalkPromptEntryConfig> entries = PromptLegacyCompatMigration.ExtractLegacyEntries(config, rootChannel);
            if (entries.Count == 0)
            {
                Log.Warning($"[RimAI.Relations] Legacy prompt migration skipped for {sourceId}: no usable section entries were found.");
                return;
            }

            int migrated = 0;
            int rejected = 0;
            foreach (IGrouping<string, RimTalkPromptEntryConfig> group in entries.GroupBy(entry =>
                         RimTalkPromptEntryChannelCatalog.NormalizeLoose(entry?.PromptChannel)))
            {
                List<RimTalkPromptEntryConfig> scoped = group.Where(entry => entry != null).ToList();
                for (int i = 0; i < scoped.Count; i++)
                {
                    RimTalkPromptEntryConfig entry = scoped[i];
                    string sectionId = PromptLegacyCompatMigration.ResolveSectionId(entry, i);
                    if (string.IsNullOrWhiteSpace(sectionId))
                    {
                        rejected++;
                        PromptLegacyCompatMigration.RecordRejected(
                            report,
                            sourceId,
                            group.Key,
                            string.Empty,
                            $"Legacy entry '{entry?.Name ?? "<unnamed>"}' could not be mapped to a canonical section.",
                            fallbackApplied: false);
                        Log.Warning($"[RimAI.Relations] Legacy prompt migration rejected entry without section mapping: source={sourceId}, channel={group.Key}, entry={entry?.Name ?? "<unnamed>"}");
                        continue;
                    }

                    string normalized = entry.Content?.Trim() ?? string.Empty;
                    if (PromptLegacyCompatMigration.ShouldRejectMigratedContent(normalized))
                    {
                        rejected++;
                        PromptLegacyCompatMigration.ApplyDefaultSectionContent(target, group.Key, sectionId);
                        PromptLegacyCompatMigration.RecordRejected(
                            report,
                            sourceId,
                            group.Key,
                            sectionId,
                            "Content looked like a rendered or polluted prompt preview and was reset to the default section.",
                            fallbackApplied: true);
                        Log.Warning($"[RimAI.Relations] Legacy prompt migration rejected polluted content: source={sourceId}, channel={group.Key}, section={sectionId}");
                        continue;
                    }

                    if (!PromptTemplateAutoRewriter.TryRewriteLegacyTemplate(
                            $"{sourceId}.{group.Key}.{sectionId}",
                            group.Key,
                            normalized,
                            ScribanPromptEngine.Instance,
                            out string rewritten,
                            out string failureReason))
                    {
                        rejected++;
                        PromptLegacyCompatMigration.ApplyDefaultSectionContent(target, group.Key, sectionId);
                        PromptLegacyCompatMigration.RecordRejected(
                            report,
                            sourceId,
                            group.Key,
                            sectionId,
                            $"Template rewrite failed: {failureReason}",
                            fallbackApplied: true);
                        Log.Warning($"[RimAI.Relations] Legacy prompt migration rejected invalid template: source={sourceId}, channel={group.Key}, section={sectionId}, reason={failureReason}");
                        continue;
                    }

                    target.SetContent(group.Key, sectionId, rewritten);
                    migrated++;
                    PromptLegacyCompatMigration.RecordImported(
                        report,
                        sourceId,
                        group.Key,
                        sectionId,
                        !string.Equals(normalized, rewritten, StringComparison.Ordinal));
                }
            }

            if (migrated > 0 || rejected > 0)
            {
                Log.Message($"[RimAI.Relations] Legacy prompt migration finished: source={sourceId}, migrated={migrated}, rejected={rejected}.");
            }
        }

internal static void ApplyDefaultSectionContent(
            RimTalkPromptEntryDefaultsConfig target,
            string promptChannel,
            string sectionId)
        {
            if (target == null || string.IsNullOrWhiteSpace(sectionId))
            {
                return;
            }

            string fallback = RimTalkPromptEntryDefaultsProvider.ResolveContent(promptChannel, sectionId);
            if (string.IsNullOrWhiteSpace(fallback))
            {
                fallback = RimTalkPromptEntryDefaultsProvider.ResolveContent(RimTalkPromptEntryChannelCatalog.Any, sectionId);
            }

            if (!string.IsNullOrWhiteSpace(fallback))
            {
                target.SetContent(promptChannel, sectionId, fallback);
            }
        }

internal static string ComposeTemplateFromEntries(IEnumerable<RimTalkPromptEntryConfig> entries)
        {
            if (entries == null)
            {
                return string.Empty;
            }

            IEnumerable<string> enabled = entries
                .Where(entry => entry != null && entry.Enabled && !string.IsNullOrWhiteSpace(entry.Content))
                .Select(entry => entry.Content.Trim());
            string combined = string.Join("\n\n", enabled);
            if (!string.IsNullOrWhiteSpace(combined))
            {
                return combined;
            }

            IEnumerable<string> all = entries
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.Content))
                .Select(entry => entry.Content.Trim());
            return string.Join("\n\n", all).Trim();
        }

internal static void AppendChannelSections(
            ICollection<RimTalkPromptEntryConfig> entries,
            RimTalkPromptEntryDefaultsConfig sections,
            string promptChannel)
        {
            if (entries == null || sections == null)
            {
                return;
            }

            for (int i = 0; i < PromptLegacyCompatMigration.SectionDefinitions.Length; i++)
            {
                PromptLegacyCompatMigration.PromptSectionDefinition section = PromptLegacyCompatMigration.SectionDefinitions[i];
                entries.Add(new RimTalkPromptEntryConfig
                {
                    Id = Guid.NewGuid().ToString("N"),
                    SectionId = section.Id,
                    Name = section.EnglishName,
                    Role = "System",
                    CustomRole = string.Empty,
                    Position = "Relative",
                    InChatDepth = 0,
                    Enabled = true,
                    PromptChannel = promptChannel,
                    Content = sections.ResolveContent(promptChannel, section.Id)
                });
            }
        }
    }


}
