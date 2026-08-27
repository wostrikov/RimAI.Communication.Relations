using System;
using System.Collections.Generic;

namespace Ustas.RimAI.Communication.Relations.AI
{
    /// <summary>
    /// Relations semantic/domain retry: attempt limits and correction prompts.
    /// Transport/provider retries stay outside this type.
    /// </summary>
    internal static class RelationsSemanticRetry
    {
        public const int MaxImmersionRetryCount = 1;
        public const int MaxTextIntegrityRetryCount = 1;
        public const int MaxDiplomacyContractRetryCount = 1;
        public const int MaxRpgContractRetryCount = 1;
        public const int MaxParseRetryCount = 1;

        public static bool ShouldRetryParseFailure(string retryReason, int parseRetryCount)
        {
            if (parseRetryCount >= MaxParseRetryCount)
            {
                return false;
            }

            return Runtime.RelationsRuntimeGateway.Policy.IsRetryableEmptyPrimaryText(retryReason);
        }

        public static string BuildParseRetryReason(string rawResponse, string reasonTag)
        {
            if (!string.IsNullOrWhiteSpace(reasonTag) &&
                !string.Equals(reasonTag, "no_extractable_text", StringComparison.OrdinalIgnoreCase))
            {
                return reasonTag;
            }

            string payload = rawResponse ?? string.Empty;
            if (payload.IndexOf("\"role\":\"assistant\"", StringComparison.OrdinalIgnoreCase) >= 0 &&
                payload.IndexOf("\"content\"", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return "assistant_role_without_content";
            }

            return "no_extractable_text";
        }

        public static List<ChatMessageData> AppendParseRetryMessage(
            List<ChatMessageData> messages,
            DialogueUsageChannel usageChannel,
            string rawResponse,
            string reasonTag,
            string matchedPath)
        {
            List<ChatMessageData> updated = RelationsTextAiRequestBuilder.Clone(messages);
            string reason = BuildParseRetryReason(rawResponse, reasonTag);
            string path = string.IsNullOrWhiteSpace(matchedPath) ? "n/a" : matchedPath;
            string hint = usageChannel switch
            {
                DialogueUsageChannel.Rpg =>
                    "Return exactly one JSON object only. visible_dialogue must be one single-line in-character dialogue sentence. Include actions only inside the same top-level actions array when gameplay effects are required.",
                DialogueUsageChannel.Diplomacy =>
                    "Return exactly one JSON object only. Put 1-2 concise in-character diplomacy sentences inside visible_dialogue. Include actions only inside the same top-level actions array when needed.",
                _ =>
                    "Return plain visible text content directly."
            };

            updated.Add(new ChatMessageData
            {
                role = "user",
                content = $"PARSE_RETRY_REASON={reason}; PARSE_MATCH_PATH={path}. Previous output could not be parsed into visible text. {hint} Do not output empty content."
            });
            return RelationsTextAiRequestBuilder.Normalize(updated, usageChannel);
        }

        public static List<ChatMessageData> AppendDialogueEnvelopeRetryMessage(
            List<ChatMessageData> messages,
            DialogueUsageChannel usageChannel,
            string reasonTag)
        {
            List<ChatMessageData> updated = RelationsTextAiRequestBuilder.Clone(messages);
            string example = usageChannel == DialogueUsageChannel.Rpg
                ? "{\"visible_dialogue\":\"角色的一句对白\"}"
                : "{\"visible_dialogue\":\"外交发言文本\"}";
            string hint = usageChannel == DialogueUsageChannel.Rpg
                ? "Put one in-character NPC line inside visible_dialogue."
                : "Put 1-2 in-character diplomacy sentences inside visible_dialogue.";
            updated.Add(new ChatMessageData
            {
                role = "user",
                content = $"DIALOGUE_PROTOCOL_VIOLATION={reasonTag ?? "invalid_dialogue_contract"}. "
                    + $"Формат твоєї попередньої відповіді не відповідає протоколу. Виведи строго один обʼєкт JSON: перший символ {{ останній символ }}, без жодного тексту природною мовою."
                    + $"Текст своєї репліки клади в поле visible_dialogue. Приклад: {example} Якщо потрібна дія, додай масив actions у тому самому JSON."
                    + $" "
                    + $"Your last response violated the dialogue protocol. Output exactly one JSON object — first char {{, last char }}. {hint} "
                    + $"Example: {example}. If actions are needed, add them inside the same JSON object. "
                    + $"No text, markdown, or explanations outside the JSON object."
            });
            return RelationsTextAiRequestBuilder.Normalize(updated, usageChannel);
        }

        public static List<ChatMessageData> AppendImmersionRetryMessage(
            List<ChatMessageData> messages,
            DialogueUsageChannel usageChannel,
            ImmersionGuardResult guardResult)
        {
            List<ChatMessageData> updated = RelationsTextAiRequestBuilder.Clone(messages);
            string reasonTag = ImmersionOutputGuard.BuildViolationTag(guardResult?.ViolationReason ?? ImmersionViolationReason.None);
            string snippet = guardResult?.ViolationSnippet ?? string.Empty;
            string problem = reasonTag switch
            {
                "reasoning_leakage" => "暴露了推理过程",
                "mechanic_keyword" => "згадувала ключові слова ігрових механік",
                "parenthetical_metadata" => "позначала стан системи в дужках",
                "status_panel_numeric" => "розкривала числовий стан системи",
                _ => "містила те, що руйнує занурення"
            };
            string hint = usageChannel == DialogueUsageChannel.Rpg
                ? "Напиши лише одну природну репліку персонажа."
                : "Напиши лише 1-2 речення природної дипломатичної репліки персонажа.";
            updated.Add(new ChatMessageData
            {
                role = "user",
                content = $"IMMERSION_VIOLATION={reasonTag}; snippet={snippet}. "
                    + $"Твоя попередня відповідь {problem} (фрагмент порушення: {snippet}). Виведи наново чистий обʼєкт JSON,"
                    + $"首字符 {{ 末字符 }}。{hint}"
                    + $"Увесь видимий текст клади у visible_dialogue. Заборонено вносити в нього стан системи, числові показники, хід міркувань чи примітки в дужках."
                    + $" "
                    + $"IMMERSION_VIOLATION={reasonTag}. Your last reply {problem}. "
                    + $"Rewrite as exactly one JSON object, first char {{ last char }}. {hint} "
                    + $"No system-state numbers, reasoning, or parenthetical notes in the visible text."
            });
            return RelationsTextAiRequestBuilder.Normalize(updated, usageChannel);
        }

        public static List<ChatMessageData> AppendTextIntegrityRetryMessage(
            List<ChatMessageData> messages,
            DialogueUsageChannel usageChannel,
            TextIntegrityCheckResult integrityResult)
        {
            List<ChatMessageData> updated = RelationsTextAiRequestBuilder.Clone(messages);
            string reasonTag = integrityResult?.ReasonTag ?? "unknown";
            string hint = usageChannel == DialogueUsageChannel.Rpg
                ? "Rewrite only visible NPC dialogue in clean natural language. Keep roleplay immersion."
                : "Rewrite only visible faction dialogue in clean natural language. Keep in-character immersion.";
            updated.Add(new ChatMessageData
            {
                role = "user",
                content = $"TEXT_INTEGRITY_VIOLATION={reasonTag}. {hint} Output exactly one JSON object only. Put visible dialogue inside visible_dialogue. Keep actions inside the same top-level JSON object when needed. Remove garbled fragments and mojibake. Do not add notes, headers, or extra text outside the JSON object."
            });
            return RelationsTextAiRequestBuilder.Normalize(updated, usageChannel);
        }

        public static List<ChatMessageData> AppendRpgContractRetryMessage(
            List<ChatMessageData> messages,
            RpgResponseContractCheckResult contractResult)
        {
            List<ChatMessageData> updated = RelationsTextAiRequestBuilder.Clone(messages);
            string reasonTag = RpgResponseContractGuard.BuildViolationTag(contractResult?.Violation ?? RpgResponseContractViolation.None);
            updated.Add(new ChatMessageData
            {
                role = "user",
                content = $"RPG_CONTRACT_VIOLATION={reasonTag}. Return exactly one JSON object only. visible_dialogue must be one single-line in-character dialogue sentence. If gameplay effects are needed, include them in the same top-level actions array; otherwise omit actions. Do not place dialogue outside JSON. Do not append a trailing JSON object. Do not use placeholder values (OptionalDef/OptionalReason/amount:0)."
            });
            return RelationsTextAiRequestBuilder.Normalize(updated, DialogueUsageChannel.Rpg);
        }

        public static List<ChatMessageData> AppendDiplomacyContractRetryMessage(
            List<ChatMessageData> messages,
            DiplomacyResponseContractCheckResult contractResult)
        {
            List<ChatMessageData> updated = RelationsTextAiRequestBuilder.Clone(messages);
            string reasonTag = DiplomacyResponseContractGuard.BuildViolationTag(
                contractResult?.Violation ?? DiplomacyResponseContractViolation.None);
            updated.Add(new ChatMessageData
            {
                role = "user",
                content =
                    $"DIPLOMACY_CONTRACT_VIOLATION={reasonTag}. Return exactly one JSON object only with visible_dialogue and optional actions. Put all visible dialogue inside visible_dialogue. If you make explicit execution commitments (arranged/submitted/dispatched), include the matching action inside the same top-level actions array. Do not place dialogue outside JSON. Do not append a trailing JSON object. " +
                    "Use request_info(info_type=prisoner) only when ransom target information is missing; if target_pawn_load_id is already valid, pay_prisoner_ransom may be called directly. " +
                    "For pay_prisoner_ransom, never claim payment/submission unless target_pawn_load_id and offer_silver are both valid positive integers. " +
                    "For pay_prisoner_ransom, keep offer_silver inside the current offer window from system messages; current ask is a preferred reference, not a strict exact-match requirement. If offer_silver is out of range, execution will clamp it to the nearest window boundary before submit. " +
                    "If a [RansomBatchSelection] block is present and you choose to output pay_prisoner_ransom this turn, output one action for every listed target_pawn_load_id exactly once in the same response, and keep total offer_silver inside the provided batch window. " +
                    "If target is unknown or offer is missing, rewrite as one in-character clarification question and do NOT claim the request was submitted."
            });
            return RelationsTextAiRequestBuilder.Normalize(updated, DialogueUsageChannel.Diplomacy);
        }
    }
}
