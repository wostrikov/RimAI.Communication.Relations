using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.UI;

namespace Ustas.RimAI.Communication.Relations.Config
{
    /// <summary>
    /// Dependencies: prompt legacy migration service and unified prompt catalog provider.
    /// Responsibility: keep PromptUnifiedCatalog as the single editable source and expose legacy section import as one-way migration only.
    /// </summary>
    public partial class RelationsSettings : ModSettings
    {
        public int RimTalkSummaryHistoryLimit = 10;
        public string RimTalkPersonaCopyTemplate = DefaultRimTalkPersonaCopyTemplate;
        public bool RimTalkAutoPushSessionSummary;
        public bool RimTalkAutoInjectCompatPreset;
        public string ExpandMemoryCompatMode = "auto";
        public bool ExpandMemoryInjectPawnMemory = true;
        public int ExpandMemoryPawnMemoryMaxChars = 1200;
        public int ExpandMemoryPawnMemoryMaxEntries = 50;
        internal RimTalkPromptEntryDefaultsConfig PromptSectionCatalog = RimTalkPromptEntryDefaultsProvider.GetDefaultsSnapshot();
        internal PromptUnifiedCatalog UnifiedPromptCatalog = PromptUnifiedCatalog.CreateFallback();

        internal bool _legacyEnableRimTalkPromptCompat = true;
        internal int _legacyRimTalkPresetInjectionMaxEntries;
        internal int _legacyRimTalkPresetInjectionMaxChars;
        internal string _legacyRimTalkCompatTemplate = string.Empty;
        internal bool _legacyRimTalkChannelSplitMigrated;
        internal RimTalkChannelCompatConfig _legacyRimTalkDiplomacy = RimTalkChannelCompatConfig.CreateDefault();
        internal RimTalkChannelCompatConfig _legacyRimTalkRpg = RimTalkChannelCompatConfig.CreateDefault();
        internal bool _legacyPromptCompatImported;
        internal bool _isEnsuringPromptCatalog;
        internal bool _isEnsuringUnifiedPromptCatalog;
        internal bool _promptUnifiedCatalogLoaded;
        internal bool _promptUnifiedCatalogDirty;
        internal const int UnifiedCatalogMigrationTargetVersion = 7;
        internal const string RimWorldBackgroundNarrativeLead = "Тло: розколота людська цивілізація розсіяна на околиці відомого всесвіту.";
        internal const string RimWorldBackgroundNarrativeText =
            "Тло: розколота людська цивілізація розсіяна на околиці відомого всесвіту. Далекі від центральної влади прикордонні світи здебільшого не мають ладу, а величезні й небезпечні планети змушують уцілілих покладатися на себе. Через брак надсвітлових перельотів і звʼязку світи давно ізольовані й розвинулися нерівномірно, тож первісні племена, індустріальні суспільства, високотехнологічні фракції та майже божественні машини існують поруч. Загальний тон — поєднання твердої наукової фантастики й прикордонного виживання, з фокусом на звичайних людях, які виживають у розколотому світі й пишуть власну історію;";
        internal const string RpgOutputSpecificationReferenceText = "Єдиний авторитет правил виводу: окремий вузол `response_contract` (`dialogue.response_contract_body`). Цей розділ лише посилається на нього й правил не повторює.";
        internal const string RpgArchiveCompressionSystemRulesText =
            "Режим стиснення архіву RPG: ти — офлайновий стискач архіву. Витягуй факти лише з наданого тексту сесії, не додавай і не прибирай подій, не переписуй причинності, не додавай рольового тону.";
        internal const string RpgArchiveCompressionOutputSpecificationText =
            "Правила виводу: виводь лише односкладовий переказ звичайним текстом. Заборонено структуровані дані, списки, переноси рядків, додаткові пояснення чи лапки навколо.";
        internal static readonly string[] RpgArchiveCompressionRequiredSectionIds =
        {
            "system_rules",
            "character_persona",
            "memory_system",
            "environment_perception",
            "context",
            "mod_variables",
            "action_rules",
            "repetition_reinforcement",
            "output_specification"
        };

        public const int RimTalkSummaryHistoryMin = 1;
        public const int RimTalkSummaryHistoryMax = 30;
        public const int RimTalkPresetInjectionLimitUnlimited = 0;
        public const int RimTalkPresetInjectionMaxEntriesMin = 0;
        public const int RimTalkPresetInjectionMaxEntriesMax = 200;
        public const int RimTalkPresetInjectionMaxCharsMin = 0;
        public const int RimTalkPresetInjectionMaxCharsMax = 200000;
        public const int RimTalkCompatTemplateMaxLength = 6000;
        public const int RimTalkPersonaCopyTemplateMaxLength = 2000;

        public const string DefaultRimTalkCompatTemplate =
@"=== RIMAI TEMPLATE SECTION ===
You may reference current RimAI namespaced variables in this section.";
        public const string DefaultRimTalkPersonaCopyTemplate = "{{ pawn.personality }}";
        internal const string PreviousEnglishCompatTemplate =
@"=== RIMTALK SCRIBAN COMPAT (RIMCHAT) ===
You may reference RimTalk variables/plugins directly in this section.";
        internal const string PreviousUkrainianCompatTemplate =
            "=== Сумісність із RIMTALK SCRIBAN (RIMCHAT) ===\nУ цьому блоці можна безпосередньо посилатися на змінні/плагіни RimTalk.";

        internal static bool IsShippedCompatTemplateDefault(string text)
        {
            string normalized = (text ?? string.Empty).Trim();
            if (normalized.Length == 0)
            {
                return true;
            }

            return string.Equals(normalized, DefaultRimTalkCompatTemplate.Trim(), StringComparison.Ordinal) ||
                   string.Equals(normalized, PreviousEnglishCompatTemplate.Trim(), StringComparison.Ordinal) ||
                   string.Equals(normalized, PreviousUkrainianCompatTemplate.Trim(), StringComparison.Ordinal);
        }

        internal void ExposeData_RimTalkCompat()
        {
            Scribe_Deep.Look(ref PromptSectionCatalog, "PromptSectionCatalog");
            Scribe_Deep.Look(ref UnifiedPromptCatalog, "PromptUnifiedCatalog");
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                Scribe_Values.Look(ref _legacyEnableRimTalkPromptCompat, "EnableRimTalkPromptCompat", true);
                Scribe_Values.Look(ref _legacyRimTalkPresetInjectionMaxEntries, "RimTalkPresetInjectionMaxEntries", RimTalkPresetInjectionLimitUnlimited);
                Scribe_Values.Look(ref _legacyRimTalkPresetInjectionMaxChars, "RimTalkPresetInjectionMaxChars", RimTalkPresetInjectionLimitUnlimited);
                Scribe_Values.Look(ref _legacyRimTalkCompatTemplate, "RimTalkCompatTemplate", string.Empty);
                Scribe_Values.Look(ref _legacyRimTalkChannelSplitMigrated, "RimTalkChannelSplitMigrated", false);
                Scribe_Deep.Look(ref _legacyRimTalkDiplomacy, "RimTalkDiplomacy");
                Scribe_Deep.Look(ref _legacyRimTalkRpg, "RimTalkRpg");
            }

            Scribe_Values.Look(ref ExpandMemoryCompatMode, "ExpandMemoryCompatMode", "auto");
            Scribe_Values.Look(ref ExpandMemoryInjectPawnMemory, "ExpandMemoryInjectPawnMemory", true);
            Scribe_Values.Look(ref ExpandMemoryPawnMemoryMaxChars, "ExpandMemoryPawnMemoryMaxChars", 1200);
            Scribe_Values.Look(ref ExpandMemoryPawnMemoryMaxEntries, "ExpandMemoryPawnMemoryMaxEntries", 50);
            this.EnsurePromptSectionCatalogReady();
            this.ClampRimTalkCompatSettings();
        }
    }
}
