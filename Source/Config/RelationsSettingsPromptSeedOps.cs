using System;
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.UI;
using Ustas.RimAI.Communication.Relations.Persistence;

namespace Ustas.RimAI.Communication.Relations.Config
{
    /// <summary>
    /// Prompt entry seeding and default section builders for Relations settings.
    /// </summary>
    internal static class RelationsSettingsPromptSeedOps
    {
internal static void EnsurePromptEntrySeedFromLegacyData(RelationsSettings settings, RpgPromptCustomConfig rpgConfig)
        {
            settings.EnsurePromptEntrySeedForChannel(RimTalkPromptChannel.Diplomacy);
            settings.EnsurePromptEntrySeedForChannel(RimTalkPromptChannel.Rpg);
        }

internal static void EnsurePromptEntrySeedForChannel(RelationsSettings settings, RimTalkPromptChannel channel)
        {
            RimTalkChannelCompatConfig current = settings.GetRimTalkChannelConfigClone(channel);
            bool dirty = false;
            if (!RelationsSettingsPromptOps.HasMeaningfulPromptEntries(current))
            {
                SystemPromptConfig systemConfig = RelationsSettingsPages.For(settings).PromptLegacy._systemPromptConfig ?? PromptPersistenceService.Instance?.LoadConfig();
                RpgPromptCustomConfig rpgConfig = RpgPromptCustomStore.LoadOrDefault();
                dirty |= EnsurePromptEntrySeedForChannel(channel, systemConfig, rpgConfig, current);
            }

            dirty |= RelationsSettingsPromptOps.EnsurePromptEntryChannelCoverage(channel, current);
            if (dirty)
            {
                current.CompatTemplate = RelationsSettingsPromptLanguage.ComposePromptEntryTextByRole(
                    current.PromptEntries,
                    includeSystemRole: true,
                    includeNonSystemRole: true);
                settings.SetRimTalkChannelConfig(channel, current);
            }
        }

        internal static bool EnsurePromptEntrySeedForChannel(
            RimTalkPromptChannel channel,
            SystemPromptConfig systemConfig,
            RpgPromptCustomConfig rpgConfig,
            RimTalkChannelCompatConfig current)
        {
            if (current == null || RelationsSettingsPromptOps.HasMeaningfulPromptEntries(current))
            {
                return false;
            }

            List<RimTalkPromptEntryConfig> legacyEntries = RelationsSettingsPromptOps.BuildLegacyPromptEntries(channel, systemConfig, rpgConfig);
            if (legacyEntries.Count == 0)
            {
                return false;
            }

            current.PromptEntries = legacyEntries;
            current.EnablePromptCompat = true;
            return true;
        }

        internal static bool EnsurePromptEntryChannelCoverage(
            RimTalkPromptChannel channel,
            RimTalkChannelCompatConfig config)
        {
            bool changed = RimTalkPromptEntrySeedSynchronizer.EnsureCoverage(channel, config);
            changed |= EnforcePromptWorkbenchSectionLayout(channel, config);
            return changed;
        }

        internal static bool EnforcePromptWorkbenchSectionLayout(
            RimTalkPromptChannel rootChannel,
            RimTalkChannelCompatConfig config)
        {
            if (config == null)
            {
                return false;
            }

            config.PromptEntries ??= new List<RimTalkPromptEntryConfig>();
            bool changed = false;
            IReadOnlyList<string> channels = RimTalkPromptEntryChannelCatalog.GetSelectableChannels(rootChannel);
            for (int i = 0; i < channels.Count; i++)
            {
                changed |= NormalizePromptChannelEntries(config.PromptEntries, channels[i]);
            }

            return changed;
        }

        internal static bool NormalizePromptChannelEntries(
            List<RimTalkPromptEntryConfig> allEntries,
            string promptChannel)
        {
            string normalizedChannel = RimTalkPromptEntryChannelCatalog.NormalizeLoose(promptChannel);
            List<RimTalkPromptEntryConfig> current = allEntries
                .Where(entry => entry != null &&
                                string.Equals(
                                    RimTalkPromptEntryChannelCatalog.NormalizeLoose(entry.PromptChannel),
                                    normalizedChannel,
                                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            List<RimTalkPromptEntryConfig> rebuilt = BuildCanonicalPromptEntriesForChannel(current, normalizedChannel);
            if (RelationsSettingsPromptOps.ArePromptEntryListsEquivalent(current, rebuilt))
            {
                return false;
            }

            RelationsSettingsPromptOps.ReplacePromptChannelEntries(allEntries, normalizedChannel, rebuilt);
            return true;
        }

        internal static List<RimTalkPromptEntryConfig> BuildCanonicalPromptEntriesForChannel(
            List<RimTalkPromptEntryConfig> sourceEntries,
            string promptChannel)
        {
            if (sourceEntries == null || sourceEntries.Count == 0)
            {
                return BuildLegacyOrderedSectionEntries(new List<RimTalkPromptEntryConfig>(), promptChannel);
            }

            bool hasSectionIdentity = sourceEntries.Any(entry => !string.IsNullOrWhiteSpace(entry?.SectionId));
            if (!hasSectionIdentity)
            {
                return BuildLegacyOrderedSectionEntries(sourceEntries, promptChannel);
            }

            bool hasKnownSection = sourceEntries.Any(entry => RelationsSettingsPromptOps.TryResolvePromptSectionIndex(entry, out _));
            return hasKnownSection
                ? BuildCoverageSectionEntries(sourceEntries, promptChannel)
                : BuildLegacyOrderedSectionEntries(sourceEntries, promptChannel);
        }

        internal static List<RimTalkPromptEntryConfig> BuildDefaultSectionEntriesForChannel(string promptChannel)
        {
            return BuildLegacyOrderedSectionEntries(new List<RimTalkPromptEntryConfig>(), promptChannel);
        }

        internal static RimTalkChannelCompatConfig CreateCanonicalDefaultRimTalkChannelConfig(RimTalkPromptChannel rootChannel)
        {
            return PromptLegacyCompatMigration.CreateLegacyAdapterFromPromptSections(
                RimTalkPromptEntryDefaultsProvider.GetDefaultsSnapshot(),
                rootChannel);
        }

        internal static List<RimTalkPromptEntryConfig> BuildLegacyOrderedSectionEntries(
            IReadOnlyList<RimTalkPromptEntryConfig> sourceEntries,
            string promptChannel)
        {
            var result = new List<RimTalkPromptEntryConfig>(RelationsSettingsPromptOps.PromptWorkbenchSections.Length);
            for (int i = 0; i < RelationsSettingsPromptOps.PromptWorkbenchSections.Length; i++)
            {
                RimTalkPromptEntryConfig source = sourceEntries != null && i < sourceEntries.Count ? sourceEntries[i] : null;
                result.Add(BuildCanonicalSectionEntry(source, promptChannel, i));
            }

            return result;
        }

        internal static List<RimTalkPromptEntryConfig> BuildCoverageSectionEntries(
            IReadOnlyList<RimTalkPromptEntryConfig> sourceEntries,
            string promptChannel)
        {
            var used = new Dictionary<int, RimTalkPromptEntryConfig>();
            var orderedIndexes = new List<int>();
            for (int i = 0; i < sourceEntries.Count; i++)
            {
                RimTalkPromptEntryConfig entry = sourceEntries[i];
                if (!RelationsSettingsPromptOps.TryResolvePromptSectionIndex(entry, out int index) || used.ContainsKey(index))
                {
                    continue;
                }

                used[index] = entry;
                orderedIndexes.Add(index);
            }

            for (int i = 0; i < RelationsSettingsPromptOps.PromptWorkbenchSections.Length; i++)
            {
                if (!used.ContainsKey(i))
                {
                    orderedIndexes.Add(i);
                }
            }

            var result = new List<RimTalkPromptEntryConfig>(RelationsSettingsPromptOps.PromptWorkbenchSections.Length);
            for (int i = 0; i < orderedIndexes.Count; i++)
            {
                int index = orderedIndexes[i];
                used.TryGetValue(index, out RimTalkPromptEntryConfig source);
                result.Add(BuildCanonicalSectionEntry(source, promptChannel, index));
            }

            return result;
        }

        internal static RimTalkPromptEntryConfig BuildCanonicalSectionEntry(
            RimTalkPromptEntryConfig source,
            string promptChannel,
            int sectionIndex)
        {
            PromptWorkbenchSectionDefinition section = RelationsSettingsPromptOps.PromptWorkbenchSections[sectionIndex];
            RimTalkPromptEntryConfig target = source?.Clone() ?? new RimTalkPromptEntryConfig
            {
                Id = Guid.NewGuid().ToString("N"),
                Role = "System",
                CustomRole = string.Empty,
                Position = "Relative",
                InChatDepth = 0,
                Enabled = true,
                Content = string.Empty
            };

            target.SectionId = section.Id;
            target.Name = section.EnglishName;
            target.PromptChannel = promptChannel;
            if (RelationsSettingsPromptOps.ShouldResetPromptEntryContent(target.Content))
            {
                target.Content = string.Empty;
            }

            if (string.IsNullOrWhiteSpace(target.Content))
            {
                target.Content = RelationsSettingsPromptOps.ResolveDefaultPromptEntryContent(promptChannel, section.Id);
            }

            return target;
        }
    }
}
