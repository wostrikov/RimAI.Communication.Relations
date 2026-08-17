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
        internal const string RimWorldBackgroundNarrativeLead = "背景：破碎的人类文明散落在已知宇宙边缘。";
        internal const string RimWorldBackgroundNarrativeText =
            "背景：破碎的人类文明散落在已知宇宙边缘。远离中央权威的边缘世界普遍无序，辽阔而危险的星球迫使幸存者自力更生。由于缺乏超光速航行与通信，各世界长期隔绝且发展失衡，原始部落、工业社会、高科技派系与近神级机器得以并存。整体基调是硬科幻与边境生存的结合，聚焦普通人在破碎世界中求生并书写自己的故事；";
        internal const string RpgOutputSpecificationReferenceText = "输出规范唯一权威：见独立 `response_contract` 节点（`dialogue.response_contract_body`）。本段只做引用，不重复定义规则。";
        internal const string RpgArchiveCompressionSystemRulesText =
            "RPG 归档压缩模式：你是离线归档压缩器。仅基于提供的会话文本提取事实，不增删事件，不重写因果，不加入角色扮演语气。";
        internal const string RpgArchiveCompressionOutputSpecificationText =
            "输出规范：仅输出单句纯文本摘要。禁止结构化数据、列表、换行、额外说明或引号包裹。";
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
