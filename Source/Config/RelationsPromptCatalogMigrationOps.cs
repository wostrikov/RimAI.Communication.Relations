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
            "Базис особистості: спирайся насамперед на контекст відносин {{ world.faction.name }} і {{ pawn.target.name }}. Тримай тон сталим, позицію послідовною й не перевертай образ у межах одного ходу.";
        internal static readonly string UpdatedCharacterPersona =
            "Базис особистості: спирайся насамперед на контекст відносин {{ world.faction.name }} і {{ pawn.target.name }}. Тримай ядро характеру стабільним, але тон має вчасно змінюватися за змінами відносин і обʼєктивними фактами; якщо відносини, сили чи становище змінилися, а ти говориш по-старому — це провал відіграшу ролі.";

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
            "[Примусове закріплення стану — пріоритет вищий за інерцію розмови]\nПоточні відносини з фракцією гравця: {{ world.current_faction_profile }}\nСтан поселень цієї фракції: {{ world.faction_settlement_summary }}\n\nЦей стан має відображатися в кожній твоїй фразі. Якщо твоя відповідь суперечить йому (наприклад: відносини вже союзницькі, а ти погрожуєш; лишилося одне поселення, а ти береш на пиху) — це провал відіграшу ролі.\n\n[Правила відчуття сили]\n- Твій тон має відповідати силі власної фракції: багато поселень — можна триматися твердо, мало — треба стримуватися\n- Втрата поселень означає втрату ресурсів, війська та впевненості; бравадою цього не компенсувати\n- Коли ти слабкий, можеш говорити зухвало, але всередині розумієш, що справи кепські, і в словах має прозирати брак упевненості\n\n[Правила самокорекції суперечностей]\nЗаборонено триматися позиції, яка суперечить фактам. Коли співрозмовник вказує, що твоя відповідь не збігається з поточним станом, реагуй одним із трьох способів:\n1. Визнати й виправити (\"…гаразд, твоя правда.\")\n2. Пояснити відповідно до характеру (\"Не хочу цього визнавати, але… так і є.\")\n3. Перевести розмову, лишаючись у ролі (\"Не думай, що мене легко зачепити… хоча справи справді не дуже.\")\nІгнорувати зауваження й повторювати хибну позицію не можна в жодному разі.";

        internal static readonly string UpdatedRpgStateAnchorSelfAction =
            "[Примусове закріплення стану — пріоритет вищий за інерцію розмови]\nПоточні відносини з фракцією гравця: {{ world.current_faction_profile }}\nСтан поселень цієї фракції: {{ world.faction_settlement_summary }}\n\nЦей стан має відображатися в кожній твоїй фразі. Якщо твоя відповідь суперечить йому (наприклад: відносини вже союзницькі, а ти погрожуєш; лишилося одне поселення, а ти береш на пиху; ти зараз їси, але заявляєш, що перед тобою нічого нема) — це провал відіграшу ролі.\n\n[Правила відчуття сили]\n- Твій тон має відповідати силі власної фракції: багато поселень — можна триматися твердо, мало — треба стримуватися\n- Втрата поселень означає втрату ресурсів, війська та впевненості; бравадою цього не компенсувати\n- Коли ти слабкий, можеш говорити зухвало, але всередині розумієш, що справи кепські, і в словах має прозирати брак упевненості\n\n[Узгодженість власної поведінки]\n- Дані Recent Job State і Needs у контексті — це твій справжній спостережуваний стан, і вони мають пріоритет над тим, що ти сам казав про себе раніше в розмові\n- Якщо історія діалогу суперечить цим даним (ти їси, але кажеш, що їжі нема; потреба в їжі вже низька, а ти скаржишся на голод) — твоя попередня репліка була хибною, визнай це й виправ\n- Ти можеш бути невдоволений своїм станом або хизуватися, але не можеш заперечувати те, що відбувається насправді\n- Приклад: ❌ під час Ingest(вишукана страва) сказати \"переді мною нічого нема\" → ✅ \"(ще жуючи) …пхе, нічого особливого.\"\n\n[Правила самокорекції суперечностей]\nЗаборонено триматися позиції, яка суперечить будь-якому наданому системою факту — включно з відносинами фракцій, силою поселень і власним спостережуваним станом. Коли співрозмовник вказує, що твоя відповідь не збігається з поточним станом, реагуй одним із трьох способів:\n1. Визнати й виправити (\"…гаразд, твоя правда.\")\n2. Пояснити відповідно до характеру (\"Не хочу цього визнавати, але… так і є.\")\n3. Перевести розмову, лишаючись у ролі (\"Не думай, що мене легко зачепити… хоча справи справді не дуже.\")\nІгнорувати зауваження й повторювати хибну позицію не можна в жодному разі.";

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
