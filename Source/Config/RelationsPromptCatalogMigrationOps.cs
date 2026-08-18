using System;
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Prompting;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Config
{
    /// <summary>
    /// One-time prompt catalog migrations for Relations settings.
    /// </summary>
    internal static class RelationsPromptCatalogMigrationOps
    {
internal static void ApplyUnifiedCatalogOneTimeMigration(RelationsSettings settings, PromptUnifiedCatalog catalog)
        {
            if (catalog == null)
            {
                return;
            }

            settings.ApplyLegacyRpgPromptMigration(catalog);
            settings.ApplyLegacyImageTemplateMigration(catalog);
            ApplyAnySystemRulesBackgroundMigration(catalog);
            ApplyRpgOutputProtocolMigration(catalog);
            ApplyCharacterPersonaStateAnchorMigration(catalog);
            ApplyRpgStateAnchorSelfActionMigration(catalog);
            settings.EnsureRpgArchiveCompressionSectionContract(catalog);
        }

internal static bool EnsureRpgArchiveCompressionContractReady(RelationsSettings settings)
        {
            settings.EnsurePromptSectionCatalogReady();
            if (settings.UnifiedPromptCatalog == null)
            {
                return false;
            }

            bool changed = settings.EnsureRpgArchiveCompressionSectionContract(settings.UnifiedPromptCatalog);
            if (changed)
            {
                settings.PromptSectionCatalog = settings.UnifiedPromptCatalog.ToSectionCatalog();
                settings.ApplyUnifiedCatalogPersistence(persistToFiles: true);
            }

            string outputSpec = settings.UnifiedPromptCatalog.ResolveSection(
                RimTalkPromptEntryChannelCatalog.RpgArchiveCompression,
                "output_specification");
            return !RelationsPromptCatalogMigration.IsRpgArchiveCompressionOutputSpecificationInvalid(outputSpec);
        }

        internal static void ApplyAnySystemRulesBackgroundMigration(PromptUnifiedCatalog catalog)
        {
            if (catalog == null)
            {
                return;
            }

            string current = catalog.ResolveSection(RimTalkPromptEntryChannelCatalog.Any, "system_rules") ?? string.Empty;
            if (current.IndexOf(RelationsSettings.RimWorldBackgroundNarrativeLead, StringComparison.Ordinal) >= 0)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(current))
            {
                catalog.SetSection(RimTalkPromptEntryChannelCatalog.Any, "system_rules", RelationsSettings.RimWorldBackgroundNarrativeText);
                return;
            }

            string separator = current.EndsWith("\n", StringComparison.Ordinal) ? string.Empty : "\n\n";
            catalog.SetSection(
                RimTalkPromptEntryChannelCatalog.Any,
                "system_rules",
                current + separator + RelationsSettings.RimWorldBackgroundNarrativeText);
        }

        internal static readonly string LegacyCharacterPersona =
            "人格基线：优先参考 {{ world.faction.name }} 与 {{ pawn.target.name }} 的关系语境。保持语气稳定、立场连续，不在单轮内突然人设反转。";
        internal static readonly string UpdatedCharacterPersona =
            "人格基线：优先参考 {{ world.faction.name }} 与 {{ pawn.target.name }} 的关系语境。保持角色核心性格稳定，但态度必须根据关系变化和客观事实及时调整；当关系/实力/处境已变，继续使用旧语气视为角色扮演失败。";

        internal static void ApplyCharacterPersonaStateAnchorMigration(PromptUnifiedCatalog catalog)
        {
            if (catalog == null)
            {
                return;
            }

            string[] channelsToMigrate =
            {
                RimTalkPromptEntryChannelCatalog.Any,
                RimTalkPromptEntryChannelCatalog.DiplomacyDialogue,
                RimTalkPromptEntryChannelCatalog.ProactiveDiplomacyDialogue,
                RimTalkPromptEntryChannelCatalog.RpgDialogue,
                RimTalkPromptEntryChannelCatalog.ProactiveRpgDialogue
            };

            foreach (string channel in channelsToMigrate)
            {
                string current = (catalog.ResolveSection(channel, "character_persona") ?? string.Empty).Trim();
                if (string.Equals(current, LegacyCharacterPersona.Trim(), StringComparison.Ordinal))
                {
                    catalog.SetSection(channel, "character_persona", UpdatedCharacterPersona);
                }
            }
        }

        internal static readonly string LegacyRpgStateAnchorOld =
            "[强制状态锚定 - 优先级高于对话惯性]\n当前与玩家派系关系：{{ world.current_faction_profile }}\n本派系据点状态：{{ world.faction_settlement_summary }}\n\n以上状态必须反映在你的每一句话中。若你的回应与上述状态矛盾（如关系已为盟友却使用威胁语气、据点只剩1个却虚张声势），视为角色扮演失败。\n\n[实力感知规则]\n- 你的态度必须与自身派系实力匹配：据点多时可以强硬，据点少时必须收敛\n- 失去据点意味着失去资源、兵力和底气——这不是嘴硬能弥补的\n- 当你处于弱势时，可以嘴硬但内心清楚自己处境不妙，言行中应透露出底气的缺失\n\n[矛盾自纠规则]\n禁止固守与事实矛盾的立场。当对方指出你的回应与当前状态不符时，你必须以下列方式之一回应：\n1. 承认并修正（\"……行吧，你说得对。\"）\n2. 给出符合人物性格的解释（\"我不想承认，但……确实是这样。\"）\n3. 以角色内方式转移（\"别以为我好欺负……虽然确实不太好过。\"）\n绝对不得无视对方的指正并重复错误立场。";

        internal static readonly string UpdatedRpgStateAnchorSelfAction =
            "[强制状态锚定 - 优先级高于对话惯性]\n当前与玩家派系关系：{{ world.current_faction_profile }}\n本派系据点状态：{{ world.faction_settlement_summary }}\n\n以上状态必须反映在你的每一句话中。若你的回应与上述状态矛盾（如关系已为盟友却使用威胁语气、据点只剩1个却虚张声势、自己正在吃东西却声称面前什么都没有），视为角色扮演失败。\n\n[实力感知规则]\n- 你的态度必须与自身派系实力匹配：据点多时可以强硬，据点少时必须收敛\n- 失去据点意味着失去资源、兵力和底气——这不是嘴硬能弥补的\n- 当你处于弱势时，可以嘴硬但内心清楚自己处境不妙，言行中应透露出底气的缺失\n\n[自身行为一致性]\n- 上下文中你的 Recent Job State 和 Needs 数据是你当前的真实可观测状态，优先级高于你之前对话中的自我描述\n- 如果你的对话历史与这些数据矛盾（如：你正在进食却声称面前没有食物、你的饮食需求已很低却说饥肠辘辘），你之前的对话输出是错误的，必须承认并纠正\n- 你可以对自身状态感到不满或嘴硬，但不能否认正在发生的客观事实\n- 示例：❌ 你正在Ingest(奢侈食物)时说\"我面前什么都没有\" → ✅ \"（嘴里还在嚼）……哼，这不算什么好东西。\"\n\n[矛盾自纠规则]\n禁止固守与任何系统注入事实矛盾的立场——包括派系关系、据点实力和自身可观测状态。当对方指出你的回应与当前状态不符时，你必须以下列方式之一回应：\n1. 承认并修正（\"……行吧，你说得对。\"）\n2. 给出符合人物性格的解释（\"我不想承认，但……确实是这样。\"）\n3. 以角色内方式转移（\"别以为我好欺负……虽然确实不太好过。\"）\n绝对不得无视对方的指正并重复错误立场。";

        internal static void ApplyRpgStateAnchorSelfActionMigration(PromptUnifiedCatalog catalog)
        {
            if (catalog == null)
            {
                return;
            }

            string[] channelsToMigrate =
            {
                RimTalkPromptEntryChannelCatalog.Any,
                RimTalkPromptEntryChannelCatalog.RpgDialogue,
                RimTalkPromptEntryChannelCatalog.ProactiveRpgDialogue
            };

            foreach (string channel in channelsToMigrate)
            {
                string currentNode = (catalog.ResolveNode(channel, "rpg_state_anchor") ?? string.Empty).Trim();
                if (string.Equals(currentNode, LegacyRpgStateAnchorOld.Trim(), StringComparison.Ordinal))
                {
                    catalog.SetNode(channel, "rpg_state_anchor", UpdatedRpgStateAnchorSelfAction);
                }
            }
        }

        internal static void ApplyLegacyRpgPromptMigration(RelationsSettings settings, PromptUnifiedCatalog catalog)
        {
            RpgPromptCustomConfig legacy = RpgPromptCustomStore.LoadOrDefault();
            if (legacy == null)
            {
                return;
            }

            RimTalkPromptEntryDefaultsConfig legacySections = RpgPromptCustomStore.LoadLegacyPromptSectionCatalogSnapshot();
            CopyLegacySectionsToUnifiedCatalog(catalog, legacySections);

            RelationsPromptCatalogMigration.CopySectionIfNotEmpty(catalog, RimTalkPromptEntryChannelCatalog.RpgDialogue, "character_persona", legacy.RoleSetting);
            RelationsPromptCatalogMigration.CopySectionIfNotEmpty(catalog, RimTalkPromptEntryChannelCatalog.ProactiveRpgDialogue, "character_persona", legacy.RoleSetting);
            catalog.SetSection(RimTalkPromptEntryChannelCatalog.RpgDialogue, "output_specification", RelationsSettings.RpgOutputSpecificationReferenceText);
            catalog.SetSection(RimTalkPromptEntryChannelCatalog.ProactiveRpgDialogue, "output_specification", RelationsSettings.RpgOutputSpecificationReferenceText);
            RelationsPromptCatalogMigration.CopySectionIfNotEmpty(
                catalog,
                RimTalkPromptEntryChannelCatalog.RpgDialogue,
                "action_rules",
                SanitizeLegacyRpgActionRulesText(legacy.FormatConstraint));
            RelationsPromptCatalogMigration.CopySectionIfNotEmpty(
                catalog,
                RimTalkPromptEntryChannelCatalog.ProactiveRpgDialogue,
                "action_rules",
                SanitizeLegacyRpgActionRulesText(legacy.FormatConstraint));
            RelationsPromptCatalogMigration.CopySectionIfNotEmpty(catalog, RimTalkPromptEntryChannelCatalog.PersonaBootstrap, "system_rules", legacy.PersonaBootstrapSystemPrompt);
            RelationsPromptCatalogMigration.CopySectionIfNotEmpty(catalog, RimTalkPromptEntryChannelCatalog.PersonaBootstrap, "context", legacy.PersonaBootstrapUserPromptTemplate);
            RelationsPromptCatalogMigration.CopySectionIfNotEmpty(
                catalog,
                RimTalkPromptEntryChannelCatalog.PersonaBootstrap,
                "output_specification",
                RelationsPromptCatalogMigration.BuildPersonaBootstrapOutputSection(legacy.PersonaBootstrapOutputTemplate, legacy.PersonaBootstrapExample));
            RelationsPromptCatalogMigration.CopyNodeIfNotEmpty(catalog, RimTalkPromptEntryChannelCatalog.RpgDialogue, "rpg_role_setting_fallback", legacy.RoleSettingFallbackTemplate);
            RelationsPromptCatalogMigration.CopyNodeIfNotEmpty(catalog, RimTalkPromptEntryChannelCatalog.ProactiveRpgDialogue, "rpg_role_setting_fallback", legacy.RoleSettingFallbackTemplate);
            RelationsPromptCatalogMigration.CopyNodeIfNotEmpty(catalog, RimTalkPromptEntryChannelCatalog.RpgDialogue, "rpg_relationship_profile", legacy.RelationshipProfileTemplate);
            RelationsPromptCatalogMigration.CopyNodeIfNotEmpty(catalog, RimTalkPromptEntryChannelCatalog.ProactiveRpgDialogue, "rpg_relationship_profile", legacy.RelationshipProfileTemplate);
            RelationsPromptCatalogMigration.CopyNodeIfNotEmpty(catalog, RimTalkPromptEntryChannelCatalog.RpgDialogue, "rpg_kinship_boundary", legacy.KinshipBoundaryRuleTemplate);
            RelationsPromptCatalogMigration.CopyNodeIfNotEmpty(catalog, RimTalkPromptEntryChannelCatalog.ProactiveRpgDialogue, "rpg_kinship_boundary", legacy.KinshipBoundaryRuleTemplate);
            RelationsPromptCatalogMigration.CopyNodeIfNotEmpty(catalog, RimTalkPromptEntryChannelCatalog.RpgDialogue, "rpg_proactive_romance", legacy.ProactiveRomanceRuleTemplate);
            RelationsPromptCatalogMigration.CopyNodeIfNotEmpty(catalog, RimTalkPromptEntryChannelCatalog.ProactiveRpgDialogue, "rpg_proactive_romance", legacy.ProactiveRomanceRuleTemplate);
        }

        internal static void CopyLegacySectionsToUnifiedCatalog(
            PromptUnifiedCatalog catalog,
            RimTalkPromptEntryDefaultsConfig legacySections)
        {
            if (catalog == null)
            {
                return;
            }

            RimTalkPromptEntryDefaultsConfig normalized = PromptLegacyCompatMigration.NormalizePromptSections(legacySections);
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

                    catalog.SetSection(channel.PromptChannel, section.SectionId, section.Content ?? string.Empty);
                }
            }
        }

        internal static string SanitizeLegacyRpgActionRulesText(string candidate)
        {
            string normalized = candidate?.Trim() ?? string.Empty;
            if (normalized.Length == 0)
            {
                return normalized;
            }

            if (LooksLikeLegacyRpgProtocolText(normalized) || ContainsPlaceholderActionPayload(normalized))
            {
                return PromptUnifiedCatalog.CreateFallback().ResolveSection(
                    RimTalkPromptEntryChannelCatalog.RpgDialogue,
                    "action_rules");
            }

            return normalized;
        }

        internal static void ApplyRpgOutputProtocolMigration(PromptUnifiedCatalog catalog)
        {
            ApplyRpgOutputProtocolMigrationForChannel(catalog, RimTalkPromptEntryChannelCatalog.RpgDialogue);
            ApplyRpgOutputProtocolMigrationForChannel(catalog, RimTalkPromptEntryChannelCatalog.ProactiveRpgDialogue);
        }

        internal static void ApplyRpgOutputProtocolMigrationForChannel(PromptUnifiedCatalog catalog, string promptChannel)
        {
            if (catalog == null || string.IsNullOrWhiteSpace(promptChannel))
            {
                return;
            }

            string outputSpec = catalog.ResolveSection(promptChannel, "output_specification") ?? string.Empty;
            if (LooksLikeLegacyRpgProtocolText(outputSpec))
            {
                catalog.SetSection(promptChannel, "output_specification", RelationsSettings.RpgOutputSpecificationReferenceText);
            }

            string actionRules = catalog.ResolveSection(promptChannel, "action_rules") ?? string.Empty;
            if (ContainsPlaceholderActionPayload(actionRules))
            {
                catalog.SetSection(
                    promptChannel,
                    "action_rules",
                    PromptUnifiedCatalog.CreateFallback().ResolveSection(promptChannel, "action_rules"));
            }
        }

        internal static bool LooksLikeLegacyRpgProtocolText(string text)
        {
            string normalized = text?.Trim() ?? string.Empty;
            if (normalized.Length == 0)
            {
                return false;
            }

            if (normalized.StartsWith("{\"dialogue\"", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return normalized.IndexOf("{\"dialogue\":\"\",\"actions\":", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("OptionalDef", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("\"amount\":0", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static bool ContainsPlaceholderActionPayload(string text)
        {
            string normalized = text?.Trim() ?? string.Empty;
            if (normalized.Length == 0)
            {
                return false;
            }

            return normalized.IndexOf("OptionalDef", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("OptionalReason", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("\"amount\":0", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
