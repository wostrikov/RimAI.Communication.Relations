using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.UI;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Config;

internal static class RelationsSettingsPromptOps
{
internal static void ResolveRaidPointTuning(this RelationsSettings settings, Faction faction, out float multiplier, out float minRaidPoints)
        {
            multiplier = RaidPointsFactionOverride.ClampMultiplier(settings.RaidPointsMultiplier);
            minRaidPoints = RaidPointsFactionOverride.ClampMinPoints(settings.MinRaidPoints);

            if (faction?.def == null || settings.RaidPointsFactionOverrides == null || settings.RaidPointsFactionOverrides.Count == 0)
            {
                return;
            }

            string factionDefName = faction.def.defName;
            RaidPointsFactionOverride entry = settings.RaidPointsFactionOverrides.FirstOrDefault(o => o?.MatchesFactionDef(factionDefName) == true);
            if (entry == null)
            {
                return;
            }

            multiplier = RaidPointsFactionOverride.ClampMultiplier(entry.RaidPointsMultiplier);
            minRaidPoints = RaidPointsFactionOverride.ClampMinPoints(entry.MinRaidPoints);
        }

        private const string ModVariablesSectionId = "mod_variables";
        internal static readonly PromptWorkbenchSectionDefinition[] PromptWorkbenchSections =
        {
            new PromptWorkbenchSectionDefinition("system_rules", "System Rules", "系统规则"),
            new PromptWorkbenchSectionDefinition("character_persona", "Persona", "角色人设", "Character Persona", "人物设定", "人格"),
            new PromptWorkbenchSectionDefinition("memory_system", "Memory", "记忆", "Memory System", "记忆系统"),
            new PromptWorkbenchSectionDefinition("environment_perception", "Environment", "环境感知", "Environment Perception", "环境"),
            new PromptWorkbenchSectionDefinition("context", "Context", "上下文"),
            new PromptWorkbenchSectionDefinition("mod_variables", "Mod Variables", "模组变量", "Mod Vars"),
            new PromptWorkbenchSectionDefinition("action_rules", "Action Rules", "行为规则", "行动规则"),
            new PromptWorkbenchSectionDefinition("repetition_reinforcement", "Reinforcement", "强化规则", "Repetition Reinforcement", "重复强化", "强化"),
            new PromptWorkbenchSectionDefinition("output_specification", "Output Format", "输出格式", "Output Specification", "输出规范")
        };



internal static void NormalizeCloudConfigUrls(this RelationsSettings settings)
        {
            if (settings.CloudConfigs == null)
            {
                return;
            }

            foreach (var config in settings.CloudConfigs)
            {
                settings.NormalizeCloudConfigUrl(config);
            }
        }

internal static void NormalizeCloudConfigUrl(this RelationsSettings settings, ApiConfig config)
        {
            if (config == null || config.Provider != AIProvider.DeepSeek)
            {
                return;
            }

            config.BaseUrl = ApiConfig.DeepSeekOfficialBaseUrl;
        }

internal static void EnsureRpgPromptTextsLoaded(this RelationsSettings settings)
        {
            settings.LoadRpgPromptTextsFromCustom();
        }

internal static void LoadRpgPromptTextsFromCustom(this RelationsSettings settings)
        {
            RpgPromptCustomConfig config = RpgPromptCustomStore.LoadOrDefault();
            settings.RPGRoleSetting = config?.RoleSetting ?? PromptTextConstants.RpgRoleSettingDefault;
            settings.RPGDialogueStyle = config?.DialogueStyle ?? PromptTextConstants.RpgDialogueStyleDefault;
            settings.RPGFormatConstraint = config?.FormatConstraint ?? PromptTextConstants.RpgFormatConstraintDefault;
            settings.RPGRoleSettingFallbackTemplate = config?.RoleSettingFallbackTemplate ?? RpgPromptDefaultsProvider.GetDefaults().RoleSettingFallbackTemplate;
            settings.RPGFormatConstraintHeader = config?.FormatConstraintHeader ?? RpgPromptDefaultsProvider.GetDefaults().FormatConstraintHeader;
            settings.RPGCompactFormatFallback = config?.CompactFormatFallback ?? RpgPromptDefaultsProvider.GetDefaults().CompactFormatFallback;
            settings.RPGActionReliabilityFallback = config?.ActionReliabilityFallback ?? RpgPromptDefaultsProvider.GetDefaults().ActionReliabilityFallback;
            settings.RPGActionReliabilityMarker = config?.ActionReliabilityMarker ?? RpgPromptDefaultsProvider.GetDefaults().ActionReliabilityMarker;
            settings.RPGApiActionPromptConfig = config?.ApiActionPrompt?.Clone() ?? RpgPromptDefaultsProvider.GetDefaults().ApiActionPrompt?.Clone() ?? RpgApiActionPromptConfig.CreateFallback();
            settings.RimTalkPersonaCopyTemplate = config?.RimTalkPersonaCopyTemplate ?? RelationsSettings.DefaultRimTalkPersonaCopyTemplate;
            settings.RimTalkAutoPushSessionSummary = config?.RimTalkAutoPushSessionSummary ?? false;
            settings.RimTalkAutoInjectCompatPreset = config?.RimTalkAutoInjectCompatPreset ?? false;
            settings.RimTalkSummaryHistoryLimit = config?.RimTalkSummaryHistoryLimit ?? 10;
            if (!string.IsNullOrEmpty(settings.RPGFormatConstraint) && settings.RPGFormatConstraint.Contains("JoyFilled"))
            {
                settings.RPGFormatConstraint = settings.RPGFormatConstraint.Replace("JoyFilled", "RimChat_BriefJoy");
            }

            settings.ClampRimTalkCompatSettings();
        }

internal static void SaveRpgPromptTextsToCustom(this RelationsSettings settings)
        {
            RpgPromptCustomConfig existing = RpgPromptCustomStore.LoadOrDefault();
            var config = new RpgPromptCustomConfig
            {
                RoleSetting = settings.RPGRoleSetting ?? string.Empty,
                DialogueStyle = settings.RPGDialogueStyle ?? string.Empty,
                FormatConstraint = settings.RPGFormatConstraint ?? string.Empty,
                RoleSettingFallbackTemplate = settings.RPGRoleSettingFallbackTemplate ?? string.Empty,
                FormatConstraintHeader = settings.RPGFormatConstraintHeader ?? string.Empty,
                CompactFormatFallback = settings.RPGCompactFormatFallback ?? string.Empty,
                ActionReliabilityFallback = settings.RPGActionReliabilityFallback ?? string.Empty,
                ActionReliabilityMarker = settings.RPGActionReliabilityMarker ?? string.Empty,
                RpgRoleSettingTemplate = existing?.RpgRoleSettingTemplate ?? string.Empty,
                RpgCompactFormatConstraintTemplate = existing?.RpgCompactFormatConstraintTemplate ?? string.Empty,
                RpgActionReliabilityRuleTemplate = existing?.RpgActionReliabilityRuleTemplate ?? string.Empty,
                DecisionPolicyTemplate = existing?.DecisionPolicyTemplate ?? string.Empty,
                TurnObjectiveTemplate = existing?.TurnObjectiveTemplate ?? string.Empty,
                OpeningObjectiveTemplate = existing?.OpeningObjectiveTemplate ?? string.Empty,
                TopicShiftRuleTemplate = existing?.TopicShiftRuleTemplate ?? string.Empty,
                RelationshipProfileTemplate = existing?.RelationshipProfileTemplate ?? string.Empty,
                KinshipBoundaryRuleTemplate = existing?.KinshipBoundaryRuleTemplate ?? string.Empty,
                ProactiveRomanceRuleTemplate = existing?.ProactiveRomanceRuleTemplate ?? string.Empty,
                PersonaBootstrapSystemPrompt = existing?.PersonaBootstrapSystemPrompt ?? string.Empty,
                PersonaBootstrapUserPromptTemplate = existing?.PersonaBootstrapUserPromptTemplate ?? string.Empty,
                PersonaBootstrapOutputTemplate = existing?.PersonaBootstrapOutputTemplate ?? string.Empty,
                PersonaBootstrapExample = existing?.PersonaBootstrapExample ?? string.Empty,
                ApiActionPrompt = settings.RPGApiActionPromptConfig?.Clone() ?? RpgApiActionPromptConfig.CreateFallback(),
                RimTalkSummaryHistoryLimit = settings.RimTalkSummaryHistoryLimit,
                RimTalkPersonaCopyTemplate = settings.RimTalkPersonaCopyTemplate ?? RelationsSettings.DefaultRimTalkPersonaCopyTemplate,
                RimTalkAutoPushSessionSummary = settings.RimTalkAutoPushSessionSummary,
                RimTalkAutoInjectCompatPreset = settings.RimTalkAutoInjectCompatPreset
            };
            RpgPromptCustomStore.Save(config);
            settings.ApplyRpgPromptEditorStateToUnifiedCatalog(persistToFiles: true);
        }

internal static void ApplyRpgPromptEditorStateToUnifiedCatalog(this RelationsSettings settings, bool persistToFiles)
        {
            PromptUnifiedCatalog catalog = settings.GetPromptUnifiedCatalogClone();
            settings.ApplyRpgPromptEditorSectionToUnifiedCatalog(catalog, RimTalkPromptEntryChannelCatalog.RpgDialogue);
            settings.ApplyRpgPromptEditorSectionToUnifiedCatalog(catalog, RimTalkPromptEntryChannelCatalog.ProactiveRpgDialogue);
            catalog.SetNode(RimTalkPromptEntryChannelCatalog.RpgDialogue, "rpg_role_setting_fallback", settings.RPGRoleSettingFallbackTemplate ?? string.Empty);
            catalog.SetNode(RimTalkPromptEntryChannelCatalog.ProactiveRpgDialogue, "rpg_role_setting_fallback", settings.RPGRoleSettingFallbackTemplate ?? string.Empty);
            settings.SetPromptUnifiedCatalog(catalog, persistToFiles: persistToFiles);
        }

internal static void ApplyRpgPromptEditorSectionToUnifiedCatalog(this RelationsSettings settings, PromptUnifiedCatalog catalog, string channel)
        {
            if (catalog == null || string.IsNullOrWhiteSpace(channel))
            {
                return;
            }

            catalog.SetSection(channel, "character_persona", settings.RPGRoleSetting ?? string.Empty);
            catalog.SetSection(channel, "style_guidance", settings.RPGDialogueStyle ?? string.Empty);
            catalog.SetSection(channel, "output_specification", RelationsSettings.RpgOutputSpecificationReferenceText);
            catalog.SetSection(channel, "action_rules", settings.RPGFormatConstraint ?? string.Empty);
        }

internal static void EnsurePromptEntrySeedFromLegacyData(this RelationsSettings settings, RpgPromptCustomConfig rpgConfig)
        {
            settings.EnsurePromptEntrySeedForChannel(RimTalkPromptChannel.Diplomacy);
            settings.EnsurePromptEntrySeedForChannel(RimTalkPromptChannel.Rpg);
        }

internal static void EnsurePromptEntrySeedForChannel(this RelationsSettings settings, RimTalkPromptChannel channel)
        {
            RimTalkChannelCompatConfig current = settings.GetRimTalkChannelConfigClone(channel);
            bool dirty = false;
            if (!HasMeaningfulPromptEntries(current))
            {
                SystemPromptConfig systemConfig = RelationsSettingsPages.For(settings).PromptLegacy._systemPromptConfig ?? PromptPersistenceService.Instance?.LoadConfig();
                RpgPromptCustomConfig rpgConfig = RpgPromptCustomStore.LoadOrDefault();
                dirty |= EnsurePromptEntrySeedForChannel(channel, systemConfig, rpgConfig, current);
            }

            dirty |= EnsurePromptEntryChannelCoverage(channel, current);
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
            if (current == null || HasMeaningfulPromptEntries(current))
            {
                return false;
            }

            List<RimTalkPromptEntryConfig> legacyEntries = BuildLegacyPromptEntries(channel, systemConfig, rpgConfig);
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
            if (ArePromptEntryListsEquivalent(current, rebuilt))
            {
                return false;
            }

            ReplacePromptChannelEntries(allEntries, normalizedChannel, rebuilt);
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

            bool hasKnownSection = sourceEntries.Any(entry => TryResolvePromptSectionIndex(entry, out _));
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
            var result = new List<RimTalkPromptEntryConfig>(PromptWorkbenchSections.Length);
            for (int i = 0; i < PromptWorkbenchSections.Length; i++)
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
                if (!TryResolvePromptSectionIndex(entry, out int index) || used.ContainsKey(index))
                {
                    continue;
                }

                used[index] = entry;
                orderedIndexes.Add(index);
            }

            for (int i = 0; i < PromptWorkbenchSections.Length; i++)
            {
                if (!used.ContainsKey(i))
                {
                    orderedIndexes.Add(i);
                }
            }

            var result = new List<RimTalkPromptEntryConfig>(PromptWorkbenchSections.Length);
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
            PromptWorkbenchSectionDefinition section = PromptWorkbenchSections[sectionIndex];
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
            if (ShouldResetPromptEntryContent(target.Content))
            {
                target.Content = string.Empty;
            }

            if (string.IsNullOrWhiteSpace(target.Content))
            {
                target.Content = ResolveDefaultPromptEntryContent(promptChannel, section.Id);
            }

            return target;
        }

        internal static bool ShouldResetPromptEntryContent(string content)
        {
            string value = content?.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return LooksLikeRenderedStructuredPrompt(value) || LooksLikeCompiledPromptPreview(value);
        }

        internal static bool LooksLikeRenderedStructuredPrompt(string content)
        {
            string value = content?.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (value.IndexOf("<prompt_context>", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("</prompt_context>", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("=== PREVIEW DIAGNOSTICS ===", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            string[] xmlMarkers =
            {
                "<channel>",
                "<mode>",
                "<environment>",
                "<fact_grounding>",
                "<instruction_stack>",
                "<response_contract>",
                "<dynamic_npc_personal_memory>",
                "<actor_state>"
            };
            int xmlHits = CountMarkerHits(value, xmlMarkers);
            if (xmlHits >= 3 && value.Length >= 300)
            {
                return true;
            }

            string[] blockMarkers =
            {
                "=== ENVIRONMENT PARAMETERS ===",
                "=== RECENT WORLD EVENTS & BATTLE INTEL ===",
                "=== SCENE PROMPT LAYERS ===",
                "=== FACT GROUNDING RULES ===",
                "=== CHARACTER STATUS (YOU) ==="
            };
            return CountMarkerHits(value, blockMarkers) >= 3 && value.Length >= 500;
        }

        internal static bool LooksLikeCompiledPromptPreview(string content)
        {
            string value = content?.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (value.IndexOf("========== FULL MESSAGE LOG ==========", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return value.IndexOf("[FILE]", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   value.IndexOf("[CODE]", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   value.IndexOf("{{", StringComparison.OrdinalIgnoreCase) < 0 &&
                   value.Length >= 500;
        }

        internal static int CountMarkerHits(string value, IEnumerable<string> markers)
        {
            if (string.IsNullOrWhiteSpace(value) || markers == null)
            {
                return 0;
            }

            int hits = 0;
            foreach (string marker in markers)
            {
                if (!string.IsNullOrWhiteSpace(marker) &&
                    value.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hits++;
                }
            }

            return hits;
        }

        internal static string ResolveDefaultPromptEntryContent(string promptChannel, string sectionId)
        {
            return RimTalkPromptEntryDefaultsProvider.ResolveContent(promptChannel, sectionId);
        }

        internal static bool TryResolvePromptSectionIndex(RimTalkPromptEntryConfig entry, out int index)
        {
            string sectionId = entry?.SectionId?.Trim();
            for (int i = 0; i < PromptWorkbenchSections.Length; i++)
            {
                PromptWorkbenchSectionDefinition section = PromptWorkbenchSections[i];
                if (!string.IsNullOrWhiteSpace(sectionId) &&
                    string.Equals(section.Id, sectionId, StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    return true;
                }

                if (TokenEqualsSection(entry?.Name, section))
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        internal static bool TokenEqualsSection(string name, PromptWorkbenchSectionDefinition section)
        {
            string normalized = NormalizeSectionToken(name);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            if (string.Equals(normalized, NormalizeSectionToken(section.EnglishName), StringComparison.Ordinal))
            {
                return true;
            }

            for (int i = 0; i < section.Aliases.Length; i++)
            {
                if (string.Equals(normalized, NormalizeSectionToken(section.Aliases[i]), StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        internal static string NormalizeSectionToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(char.ToLowerInvariant(c));
                }
            }

            return sb.ToString();
        }

        internal static bool ArePromptEntryListsEquivalent(
            IReadOnlyList<RimTalkPromptEntryConfig> left,
            IReadOnlyList<RimTalkPromptEntryConfig> right)
        {
            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            for (int i = 0; i < left.Count; i++)
            {
                if (!ArePromptEntriesEquivalent(left[i], right[i]))
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool ArePromptEntriesEquivalent(RimTalkPromptEntryConfig left, RimTalkPromptEntryConfig right)
        {
            if (left == null || right == null)
            {
                return left == right;
            }

            return string.Equals(left.Id, right.Id, StringComparison.Ordinal) &&
                   string.Equals(left.SectionId, right.SectionId, StringComparison.Ordinal) &&
                   string.Equals(left.Name, right.Name, StringComparison.Ordinal) &&
                   string.Equals(left.Role, right.Role, StringComparison.Ordinal) &&
                   string.Equals(left.CustomRole, right.CustomRole, StringComparison.Ordinal) &&
                   string.Equals(left.Position, right.Position, StringComparison.Ordinal) &&
                   left.InChatDepth == right.InChatDepth &&
                   left.Enabled == right.Enabled &&
                   string.Equals(left.PromptChannel, right.PromptChannel, StringComparison.Ordinal) &&
                   string.Equals(left.Content, right.Content, StringComparison.Ordinal);
        }

        internal static void ReplacePromptChannelEntries(
            List<RimTalkPromptEntryConfig> allEntries,
            string promptChannel,
            List<RimTalkPromptEntryConfig> rebuilt)
        {
            int insertIndex = allEntries.Count;
            for (int i = 0; i < allEntries.Count; i++)
            {
                RimTalkPromptEntryConfig entry = allEntries[i];
                if (entry == null)
                {
                    continue;
                }

                if (!string.Equals(
                        RimTalkPromptEntryChannelCatalog.NormalizeLoose(entry.PromptChannel),
                        promptChannel,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                insertIndex = i;
                break;
            }

            allEntries.RemoveAll(entry =>
                entry != null &&
                string.Equals(
                    RimTalkPromptEntryChannelCatalog.NormalizeLoose(entry.PromptChannel),
                    promptChannel,
                    StringComparison.OrdinalIgnoreCase));

            allEntries.InsertRange(insertIndex, rebuilt);
        }

        internal static bool HasMeaningfulPromptEntries(RimTalkChannelCompatConfig config)
        {
            if (config?.PromptEntries == null || config.PromptEntries.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < config.PromptEntries.Count; i++)
            {
                RimTalkPromptEntryConfig entry = config.PromptEntries[i];
                string text = entry?.Content?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                if (!RelationsSettings.IsShippedCompatTemplateDefault(text))
                {
                    return true;
                }
            }

            return false;
        }

        internal static List<RimTalkPromptEntryConfig> BuildLegacyPromptEntries(
            RimTalkPromptChannel channel,
            SystemPromptConfig systemConfig,
            RpgPromptCustomConfig rpgConfig)
        {
            var entries = new List<RimTalkPromptEntryConfig>();
            if (channel == RimTalkPromptChannel.Diplomacy)
            {
                AddLegacyPromptEntry(
                    entries,
                    "Global System Prompt",
                    "System",
                    systemConfig?.GlobalSystemPrompt,
                    RimTalkPromptEntryChannelCatalog.DiplomacyDialogue);
                AddLegacyPromptEntry(
                    entries,
                    "Global Dialogue Prompt",
                    "System",
                    systemConfig?.GlobalDialoguePrompt,
                    RimTalkPromptEntryChannelCatalog.DiplomacyDialogue);
                return entries;
            }

            AddLegacyPromptEntry(
                entries,
                "Role Setting",
                "System",
                rpgConfig?.RoleSetting,
                RimTalkPromptEntryChannelCatalog.RpgDialogue);
            AddLegacyPromptEntry(
                entries,
                "Dialogue Style",
                "Assistant",
                rpgConfig?.DialogueStyle,
                RimTalkPromptEntryChannelCatalog.RpgDialogue);
            AddLegacyPromptEntry(
                entries,
                "Format Constraint",
                "System",
                rpgConfig?.FormatConstraint,
                RimTalkPromptEntryChannelCatalog.RpgDialogue);
            return entries;
        }

        internal static void AddLegacyPromptEntry(
            ICollection<RimTalkPromptEntryConfig> entries,
            string name,
            string role,
            string content,
            string promptChannel)
        {
            string normalized = content?.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            List<RelationsPromptEntrySeedImport.LegacyPromptEntrySeed> seeds = RelationsPromptEntrySeedImport.SplitLegacyPromptEntrySeeds(name ?? "Entry", normalized);
            if (seeds.Count == 0)
            {
                return;
            }

            for (int i = 0; i < seeds.Count; i++)
            {
                RelationsPromptEntrySeedImport.LegacyPromptEntrySeed seed = seeds[i];
                entries.Add(new RimTalkPromptEntryConfig
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = seed.Name,
                    Role = role ?? "System",
                    Position = "Relative",
                    InChatDepth = 0,
                    Enabled = true,
                    PromptChannel = RimTalkPromptEntryChannelCatalog.NormalizeLoose(promptChannel),
                    Content = seed.Content
                });
            }
        }
}
