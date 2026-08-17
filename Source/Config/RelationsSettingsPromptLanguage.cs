using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.UI;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Config;

internal static class RelationsSettingsPromptLanguage
{
internal static void SyncLegacyPromptFieldsFromEntryChannels(this RelationsSettings settings)
        {
            settings.EnsurePromptEntrySeedForChannel(RimTalkPromptChannel.Diplomacy);
            settings.EnsurePromptEntrySeedForChannel(RimTalkPromptChannel.Rpg);

            RimTalkChannelCompatConfig diplomacy = settings.GetRimTalkChannelConfigClone(RimTalkPromptChannel.Diplomacy);
            string diplomacySystem = ComposePromptEntryTextByRole(diplomacy?.PromptEntries, includeSystemRole: true, includeNonSystemRole: false);
            string diplomacyDialogue = ComposePromptEntryTextByRole(diplomacy?.PromptEntries, includeSystemRole: false, includeNonSystemRole: true);

            if (!string.IsNullOrWhiteSpace(diplomacySystem))
            {
                RelationsSettingsPages.For(settings).PromptLegacy.SystemPromptConfigData.GlobalSystemPrompt = diplomacySystem;
                settings.GlobalSystemPrompt = diplomacySystem;
            }

            if (!string.IsNullOrWhiteSpace(diplomacyDialogue))
            {
                RelationsSettingsPages.For(settings).PromptLegacy.SystemPromptConfigData.GlobalDialoguePrompt = diplomacyDialogue;
                settings.GlobalDialoguePrompt = diplomacyDialogue;
            }

            RimTalkChannelCompatConfig rpg = settings.GetRimTalkChannelConfigClone(RimTalkPromptChannel.Rpg);
            string rpgRole = ComposePromptEntryTextByRole(rpg?.PromptEntries, includeSystemRole: true, includeNonSystemRole: false);
            string rpgDialogue = ComposePromptEntryTextByRole(rpg?.PromptEntries, includeSystemRole: false, includeNonSystemRole: true);
            if (!string.IsNullOrWhiteSpace(rpgRole))
            {
                settings.RPGRoleSetting = rpgRole;
            }

            if (!string.IsNullOrWhiteSpace(rpgDialogue))
            {
                settings.RPGDialogueStyle = rpgDialogue;
            }

            if (string.IsNullOrWhiteSpace(settings.RPGFormatConstraint))
            {
                string combined = ComposePromptEntryTextByRole(rpg?.PromptEntries, includeSystemRole: true, includeNonSystemRole: true);
                if (!string.IsNullOrWhiteSpace(combined))
                {
                    settings.RPGFormatConstraint = combined;
                }
            }
        }

        internal static string ComposePromptEntryTextByRole(
            IEnumerable<RimTalkPromptEntryConfig> entries,
            bool includeSystemRole,
            bool includeNonSystemRole)
        {
            List<string> filtered = CollectPromptEntryContents(entries, enabledOnly: true, includeSystemRole, includeNonSystemRole);
            if (filtered.Count == 0)
            {
                filtered = CollectPromptEntryContents(entries, enabledOnly: true, includeSystemRole: true, includeNonSystemRole: true);
            }

            if (filtered.Count == 0)
            {
                filtered = CollectPromptEntryContents(entries, enabledOnly: false, includeSystemRole: true, includeNonSystemRole: true);
            }

            return string.Join("\n\n", filtered.Where(item => !string.IsNullOrWhiteSpace(item))).Trim();
        }

        internal static List<string> CollectPromptEntryContents(
            IEnumerable<RimTalkPromptEntryConfig> entries,
            bool enabledOnly,
            bool includeSystemRole,
            bool includeNonSystemRole)
        {
            var result = new List<string>();
            if (entries == null)
            {
                return result;
            }

            foreach (RimTalkPromptEntryConfig entry in entries)
            {
                if (entry == null || (enabledOnly && !entry.Enabled))
                {
                    continue;
                }

                string text = entry.Content?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                bool isSystemRole = string.Equals(entry.Role, "System", StringComparison.OrdinalIgnoreCase);
                if ((isSystemRole && !includeSystemRole) || (!isSystemRole && !includeNonSystemRole))
                {
                    continue;
                }

                result.Add(text);
            }

            return result;
        }





        internal static bool TryGetSharedTextConfig(out ApiConfig config)
        {
            config = null;
            var shared = Ustas.RimAI.Core.Configuration.SharedTextAiAccess.Current;
            if (shared == null || !shared.HasActive)
                return false;

            if (!Enum.TryParse(shared.Provider, true, out AIProvider provider))
                provider = string.Equals(shared.Provider, "Local", StringComparison.OrdinalIgnoreCase)
                    ? AIProvider.Custom
                    : AIProvider.Custom;

            config = new ApiConfig
            {
                IsEnabled = true,
                Provider = provider,
                SelectedModel = string.IsNullOrWhiteSpace(shared.Model) ? "Custom" : shared.Model,
                CustomModelName = shared.CustomModel ?? string.Empty,
                BaseUrl = shared.BaseUrl ?? string.Empty,
                ApiKey = shared.ApiKey ?? string.Empty
            };
            return true;
        }


public static string GetEffectivePromptLanguage(this RelationsSettings settings)
        {
            if (!settings.PromptLanguageFollowSystem && !string.IsNullOrWhiteSpace(settings.PromptLanguageOverride))
            {
                return settings.PromptLanguageOverride.Trim();
            }

            return ResolveSystemPromptLanguage();
        }










        internal static string ResolveSystemPromptLanguage()
        {
            string folder = LanguageDatabase.activeLanguage?.folderName;
            if (string.IsNullOrWhiteSpace(folder))
            {
                return "English";
            }

            return folder switch
            {
                "ChineseSimplified" => "Chinese (Simplified)",
                "ChineseTraditional" => "Chinese (Traditional)",
                _ => folder.Replace('_', ' ')
            };
        }



        internal static string GetReasoningEffortLabel(string value)
        {
            switch (value)
            {
                case "low": return "RimChat_ReasoningEffortLow".Translate();
                case "medium": return "RimChat_ReasoningEffortMedium".Translate();
                case "high": return "RimChat_ReasoningEffortHigh".Translate();
                case "xhigh": return "RimChat_ReasoningEffortXHigh".Translate();
                default: return "RimChat_ReasoningEffortMedium".Translate();
            }
        }

        internal static string GetMaxTokensLabel(int value)
        {
            switch (value)
            {
                case 1024: return "RimChat_MaxTokensShort".Translate();
                case 2048: return "RimChat_MaxTokensNormal".Translate();
                case 4096: return "RimChat_MaxTokensDetailed".Translate();
                default: return value.ToString();
            }
        }



























}
