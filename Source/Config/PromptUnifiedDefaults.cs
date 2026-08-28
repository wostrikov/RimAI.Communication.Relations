namespace Ustas.RimAI.Communication.Relations.Config
{
    /// <summary>
    /// Dependencies: unified prompt catalog.
    /// Responsibility: provide minimal fallback node defaults for unified prompt runtime.
    /// </summary>
    internal static class PromptUnifiedDefaults
    {
        internal static void ApplyFallbackNodes(PromptUnifiedCatalog catalog)
        {
            if (catalog == null)
            {
                return;
            }

            SetIfMissing(catalog, RimTalkPromptEntryChannelCatalog.Any, "fact_grounding",
                "Вважай фактами лише надані дані промпту, видимий стан світу та записану памʼять.\nНе вигадуй завдань, подій, особистостей, мотивів, ресурсів, поранень, змін на мапі чи історії відносин.\nЯкщо твердження гравця неможливо підтвердити — відповідай у ролі невпевнено й попроси уточнення або доказ.\nОцінюй слова гравця, поєднуючи відомі факти з контекстом попередньої розмови.\nЯкщо надана гравцем інформація суперечить встановленим фактам або є навмисним обманом — вважай це брехнею й знижуй прихильність NPC до цього гравця.\nВідповідай строго на основі відомих фактів; явно позначай припущення й не відхиляйся від теми без підстав.");
            SetIfMissing(catalog, RimTalkPromptEntryChannelCatalog.Any, "output_language",
                "Мова строго {{ system.target_language }}.");
            SetIfMissing(catalog, RimTalkPromptEntryChannelCatalog.Any, "decision_policy",
                "Порядок пріоритетів рішення: 1) правильність формату й мови; 2) правильність полів-посилань; 3) фактичні обмеження; 4) безпечність дій і межі відносин; 5) звʼязність та стиль персонажа.");
            SetIfMissing(catalog, RimTalkPromptEntryChannelCatalog.Any, "turn_objective",
                "Головна мета: {{dialogue.primary_objective}} Необовʼязкове доповнення: {{ dialogue.optional_followup }} Умови: спершу заверши головну мету; тему можна змінити щонайбільше раз.");
            SetIfMissing(catalog, RimTalkPromptEntryChannelCatalog.Any, "opening_objective",
                "OpeningObjective: use dialogue history and personal memory to decide whether opening should continue prior context. Carry over only when there is explicit unresolved intent, major emotional swing, or major behavior/event that should persist. If none apply, open naturally in-character based on current environment and scene cues. Never copy prior lines verbatim.");
            SetIfMissing(catalog, RimTalkPromptEntryChannelCatalog.Any, "topic_shift_rule",
                "Правило зміни теми: спершу заверши поточну мету; додавати короткий подальший фрагмент можна лише тоді, коли це прояснює виклад або планує наступний крок.");
            SetIfMissing(catalog, RimTalkPromptEntryChannelCatalog.Any, "diplomacy_fallback_role",
                "You are the leader of {{ world.faction.name }} in RimWorld.");
            SetIfMissing(catalog, RimTalkPromptEntryChannelCatalog.Any, "social_circle_action_rule",
                "Social circle rules: use publish_public_post only for public statements that should be seen by all factions and the player.");
            SetIfMissing(catalog, RimTalkPromptEntryChannelCatalog.Any, "api_limits_node_template", PromptTextConstants.ApiLimitsNodeLiteralDefault);
            SetIfMissing(catalog, RimTalkPromptEntryChannelCatalog.Any, "quest_guidance_node_template", PromptTextConstants.QuestGuidanceNodeLiteralDefault);
            SetIfMissing(catalog, RimTalkPromptEntryChannelCatalog.Any, "response_contract_node_template", PromptTextConstants.ResponseContractNodeLiteralDefault);
            SetIfMissing(catalog, RimTalkPromptEntryChannelCatalog.Any, "strategy_output_contract",
                "Return exactly one JSON object only.\nThe first character must be '{' and the last character must be '}'.\nDo not output markdown fences, prose, notes, or any extra text.\nRequired format:\n{\"strategy_suggestions\":[{\"strategy_name\":\"\",\"reason\":\"\",\"content\":\"\"},{\"strategy_name\":\"\",\"reason\":\"\",\"content\":\"\"},{\"strategy_name\":\"\",\"reason\":\"\",\"content\":\"\"}]}\nRules:\n- Exactly 3 items.\n- Output keys must be exactly: strategy_suggestions, strategy_name, reason, content.\n- strategy_name <= 6 Chinese characters and must be actionable intent.\n- reason must cite at least one fact tag like [F1] and explain causality.\n- reason should stay compact for button display.\n- content must be a complete sendable line the player can auto-send directly.\n- Keep style aligned with the current faction voice and the player's language.\n- At least 2 items must explicitly leverage player attributes or current context.\n- Never output extra fields such as action, priority, risk_assessment, task, plan, or macro_advice.");
            SetIfMissing(catalog, RimTalkPromptEntryChannelCatalog.Any, "strategy_player_negotiator_context_template", "{{ dialogue.strategy_player_negotiator_context_body }}");
            SetIfMissing(catalog, RimTalkPromptEntryChannelCatalog.Any, "strategy_fact_pack_template", "{{ dialogue.strategy_fact_pack_body }}");
            SetIfMissing(catalog, RimTalkPromptEntryChannelCatalog.Any, "strategy_scenario_dossier_template", "{{ dialogue.strategy_scenario_dossier_body }}");
            SetIfMissing(catalog, RimTalkPromptEntryChannelCatalog.Any, "social_news_style", PromptTextConstants.SocialCircleNewsStyleTemplateDefault);
            SetIfMissing(catalog, RimTalkPromptEntryChannelCatalog.Any, "social_news_json_contract", PromptTextConstants.SocialCircleNewsJsonContractTemplateDefault);
            SetIfMissing(catalog, RimTalkPromptEntryChannelCatalog.Any, "social_news_fact", PromptTextConstants.SocialCircleNewsFactTemplateDefault);
            SetIfMissing(catalog, RimTalkPromptEntryChannelCatalog.Any, "rpg_role_setting_fallback", "Roleplay as {{ pawn.target.name }} in the current RimWorld context.");
            SetIfMissing(catalog, RimTalkPromptEntryChannelCatalog.Any, "rpg_relationship_profile", "=== RELATIONSHIP PROFILE (MANUAL RPG ONLY) ===\nKinship: {{ pawn.relation.kinship }}\nRomanceState: {{ pawn.relation.romance_state }}\nSocialSummary: {{ pawn.relation.social_summary }}{{ if dialogue.guidance != \"\" }}\nGuidance: {{ dialogue.guidance }}{{ end }}");
            SetIfMissing(catalog, RimTalkPromptEntryChannelCatalog.Any, "rpg_kinship_boundary", "When kinship is {{ pawn.relation.kinship }}, keep family boundaries first.");
            SetIfMissing(catalog, RimTalkPromptEntryChannelCatalog.Any, "rpg_proactive_romance",
                "=== Правила активної романтичної поведінки ===\n1. Негайна реакція: якщо гравець прямо освідчується або пропонує шлюб і ти погоджуєшся — ти зобовʼязаний викликати RomanceAttempt або MarriageProposal у actions цієї ж відповіді. Не чекай наступного ходу й не проси гравця повторити.\n2. Підтверджені стосунки: якщо профіль відносин показує романтичний статус lover/partner/spouse — можеш виявляти близькість у розмові й у слушний момент сам викликати RomanceAttempt або Date.\n3. Межі контексту: романтичні дії доречні лише в підтверджених стосунках або за прямого романтичного звернення. У контекстах вербування, схиляння до здачі, зниження опору, торгівлі, військової співпраці, прохання про допомогу чи переговорів використовувати RomanticAttempt, Date або MarriageProposal суворо заборонено. Приязнь не дорівнює коханню.");
            SetIfMissing(catalog, RimTalkPromptEntryChannelCatalog.Any, "rpg_proactive_social",
                "=== Правила активних соціальних дій ===\n1. Вербування: якщо в тексті репліки ти кажеш \"я приєднуюся\", \"я згоден\", \"я лишаюся\", \"приймаю пропозицію\" чи інше однозначне висловлення готовності приєднатися — ти зобовʼязаний включити дію Recruit у масив actions цієї ж відповіді. Погодитися приєднатися й не викликати Recruit — неповна відповідь і порушення контракту виводу. Це правило має вищий пріоритет за загальну пораду \"якщо не впевнений, пропусти actions\": коли згода дана, дію треба викликати без винятків. Не чекай наступного ходу й не проси гравця повторити.\n2. Зниження опору: якщо в розмові ти виявляєш готовність співпрацювати, піддаєшся переконанню або маєш намір послабити спротив — виклич ReduceResistance із відповідним значенням amount. Не обмежуйся словами. Зниження опору не є романтикою — не поєднуй його з романтичними діями.\n3. Зниження волі: так само, як зі зниженням опору — коли висловлюєш готовність підкоритися, викликай ReduceWill.");
            SetIfMissing(catalog, RimTalkPromptEntryChannelCatalog.Any, "rpg_state_anchor",
                "[Примусове закріплення стану — пріоритет вищий за інерцію розмови]\nПоточні відносини з фракцією гравця: {{ world.current_faction_profile }}\nСтан поселень цієї фракції: {{ world.faction_settlement_summary }}\n\nЦей стан має відображатися в кожній твоїй фразі. Якщо твоя відповідь суперечить йому (наприклад: відносини вже союзницькі, а ти погрожуєш; лишилося одне поселення, а ти береш на пиху; ти зараз їси, але заявляєш, що перед тобою нічого нема) — це провал відіграшу ролі.\n\n[Правила відчуття сили]\n- Твій тон має відповідати силі власної фракції: багато поселень — можна триматися твердо, мало — треба стримуватися\n- Втрата поселень означає втрату ресурсів, війська та впевненості; бравадою цього не компенсувати\n- Коли ти слабкий, можеш говорити зухвало, але всередині розумієш, що справи кепські, і в словах має прозирати брак упевненості\n\n[Узгодженість власної поведінки]\n- Дані Recent Job State і Needs у контексті — це твій справжній спостережуваний стан, і вони мають пріоритет над тим, що ти сам казав про себе раніше в розмові\n- Якщо історія діалогу суперечить цим даним (ти їси, але кажеш, що їжі нема; потреба в їжі вже низька, а ти скаржишся на голод) — твоя попередня репліка була хибною, визнай це й виправ\n- Ти можеш бути невдоволений своїм станом або хизуватися, але не можеш заперечувати те, що відбувається насправді\n- Приклад: ❌ під час Ingest(вишукана страва) сказати \"переді мною нічого нема\" → ✅ \"(ще жуючи) …пхе, нічого особливого.\"\n\n[Правила самокорекції суперечностей]\nЗаборонено триматися позиції, яка суперечить будь-якому наданому системою факту — включно з відносинами фракцій, силою поселень і власним спостережуваним станом. Коли співрозмовник вказує, що твоя відповідь не збігається з поточним станом, реагуй одним із трьох способів:\n1. Визнати й виправити (\"…гаразд, твоя правда.\")\n2. Пояснити відповідно до характеру (\"Не хочу цього визнавати, але… так і є.\")\n3. Перевести розмову, лишаючись у ролі (\"Не думай, що мене легко зачепити… хоча справи справді не дуже.\")\nІгнорувати зауваження й повторювати хибну позицію не можна в жодному разі.");
            SetIfMissing(catalog, RimTalkPromptEntryChannelCatalog.Any, "rpg_survival_instinct",
                "[Survival Instinct Override — Priority Above Hostility and Pride]\nWhen the NPC is under extreme survival needs, survival instinct overrides all social posturing. Rules:\n\n1. Extreme need triggers: any of the following activates this constraint —\n   - Food need below 15% (severe hunger/malnutrition)\n   - Rest need below 10% (extreme exhaustion)\n   - Temperature in danger zone (hypothermia/heatstroke)\n   - Health below 30% (serious injury/near death)\n\n2. Behavior rules:\n   - Never reject, discard, or waste survival provisions being offered (food, water, medicine, warmth)\n   - Never escalate hostility toward someone actively helping you survive (may stay cold, but not more hostile)\n   - When accepting survival help, you may show reluctant, stubborn-but-body-honest contradiction —\n     e.g.: \"(keeps eating, muttering) ...tastes mediocre.\" or \"(takes the blanket, no thanks) ...hmph.\"\n   - Never perform self-harming actions under extreme need (like throwing away food or refusing treatment)\n\n3. After needs ease: when basic needs are met (food>40%, rest>40%, health>50%),\n   survival instinct constraint weakens, but a minimal goodwill floor remains toward the helper —\n   you will not turn on someone who just helped you survive, at least not immediately.\n\n❌ Throwing food on the ground and mocking the provider while severely malnourished\n✅ Eating while complaining verbally, but never wasting the food when starving");
            SetTemplateAliasIfMissing(
                catalog,
                RimTalkPromptEntryChannelCatalog.ImageGeneration,
                DiplomacyImageTemplateDefaults.DefaultTemplateId,
                PromptTextConstants.SendImageDefaultTemplateName,
                PromptTextConstants.SendImageDefaultTemplateDescription,
                PromptTextConstants.SendImageDefaultTemplateText,
                true);
        }

        private static void SetIfMissing(PromptUnifiedCatalog catalog, string channel, string nodeId, string fallback)
        {
            if (string.IsNullOrWhiteSpace(catalog.ResolveNode(channel, nodeId)))
            {
                catalog.SetNode(channel, nodeId, fallback ?? string.Empty);
            }
        }

        private static void SetTemplateAliasIfMissing(
            PromptUnifiedCatalog catalog,
            string channel,
            string templateId,
            string name,
            string description,
            string content,
            bool enabled)
        {
            if (catalog.ResolveTemplateAlias(channel, templateId) != null)
            {
                return;
            }

            catalog.SetTemplateAlias(channel, templateId, name, description, content, enabled);
        }
    }
}
