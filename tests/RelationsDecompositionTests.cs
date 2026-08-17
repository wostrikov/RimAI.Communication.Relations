using System;
using System.Collections.Generic;
using Ustas.RimAI.Communication.Relations.AI;

internal static class RelationsDecompositionTests
{
    public static void Run(Action<bool, string> check)
    {
        RequestBuilderSamePayload(check);
        ResponseExtraction(check);
        SemanticValidation(check);
        LocalRetryPolicy(check);
        TokenAccounting(check);
    }

    static void RequestBuilderSamePayload(Action<bool, string> check)
    {
        var messages = new List<ChatMessageData>
        {
            new ChatMessageData { role = "system", content = "Stay in character." },
            new ChatMessageData { role = "user", content = "Hello" }
        };
        string a = OpenAIProviderAdapter.BuildResponsesRequest("gpt-5.6-luna", messages, 2048);
        string b = OpenAIProviderAdapter.BuildResponsesRequest("gpt-5.6-luna", messages, 2048);
        check(a == b, "builder same logical request same payload");
        check(a.Contains("\"model\":\"gpt-5.6-luna\""), "builder preserves model");
        check(!a.Contains("Authorization"), "builder does not embed credentials");
    }

    static void ResponseExtraction(Action<bool, string> check)
    {
        PrimaryTextExtractionResult ok = RelationsProviderTextExtractor.Extract(
            "{\"output\":[{\"content\":[{\"type\":\"output_text\",\"text\":\"Visible line\"}]}]}",
            AIProvider.OpenAI);
        check(ok.IsSuccess && ok.Content == "Visible line", "openai envelope extracts text");

        PrimaryTextExtractionResult empty = RelationsProviderTextExtractor.Extract("{}", AIProvider.OpenAI);
        check(!empty.IsSuccess, "malformed openai envelope fails");

        PrimaryTextExtractionResult compatible = RelationsProviderTextExtractor.Extract(
            "{\"choices\":[{\"message\":{\"content\":\"Compat\"}}]}",
            AIProvider.DeepSeek);
        check(compatible.IsSuccess && compatible.Content.Contains("Compat"), "compatible envelope extracts text");

        PrimaryTextExtractionResult error = RelationsProviderTextExtractor.Extract(
            "{\"error\":{\"message\":\"nope\"}}",
            AIProvider.DeepSeek);
        check(!error.IsSuccess && error.ReasonTag == "error_payload", "error envelope is not assistant text");

        check(RelationsProviderTextExtractor.IsRetryableEmptyPrimaryText("empty_primary_text"), "empty primary text is retryable");
        check(RelationsProviderTextExtractor.IsRetryableEmptyPrimaryText("assistant_role_without_content"), "assistant without content is retryable");
        check(!RelationsProviderTextExtractor.IsRetryableEmptyPrimaryText("error_payload"), "error payload is not parse-retryable");
    }

    static void SemanticValidation(Action<bool, string> check)
    {
        DiplomacyResponseContractCheckResult diplomacyOk = DiplomacyResponseContractGuard.Validate("The caravan will wait at the gates.");
        check(diplomacyOk.IsValid, "diplomacy contract accepts in-character speech");

        DiplomacyResponseContractCheckResult diplomacyFail = DiplomacyResponseContractGuard.Validate("I will arrange the prisoner transfer immediately.");
        check(!diplomacyFail.IsValid, "diplomacy contract fails commitment without actions");
        check(DiplomacyResponseContractGuard.BuildViolationTag(diplomacyFail.Violation) == "commitment_without_action_json", "diplomacy violation tag");

        RpgResponseContractCheckResult rpgOk = RpgResponseContractGuard.Validate("Keep your voice down.");
        check(rpgOk.IsValid, "rpg contract accepts a single dialogue line");

        RpgResponseContractCheckResult rpgFail = RpgResponseContractGuard.Validate("Line one.\nLine two.");
        check(!rpgFail.IsValid, "rpg contract rejects multiline visible dialogue");

        TextIntegrityCheckResult integrityOk = TextIntegrityGuard.ValidateVisibleDialogue("The outpost is quiet tonight.");
        check(integrityOk.IsValid, "text integrity accepts clean dialogue");

        TextIntegrityCheckResult integrityFail = TextIntegrityGuard.ValidateVisibleDialogue("???? \uFFFD \uFFFD broken");
        check(!integrityFail.IsValid, "text integrity rejects replacement-character garbage");
    }

    static void LocalRetryPolicy(Action<bool, string> check)
    {
        check(RelationsLocalProviderRetry.ShouldRetryLocalServerError(true, 503, 0), "local 5xx retries");
        check(RelationsLocalProviderRetry.ShouldRetryLocalServerError(true, 503, 1), "local 5xx second retry");
        check(!RelationsLocalProviderRetry.ShouldRetryLocalServerError(true, 503, 2), "local 5xx stops after attempt limit");
        check(!RelationsLocalProviderRetry.ShouldRetryLocalServerError(false, 503, 0), "cloud 5xx is not local retry");

        check(RelationsLocalProviderRetry.ShouldRetryLocalConnectionError(true, AIRequestDebugSource.DiplomacyDialogue, "connection reset", 0), "local connection retry");
        check(!RelationsLocalProviderRetry.ShouldRetryLocalConnectionError(true, AIRequestDebugSource.DiplomacyDialogue, "connection reset", 1), "local connection stops after attempt limit");
        check(!RelationsLocalProviderRetry.ShouldRetryLocalConnectionError(true, AIRequestDebugSource.AirdropSelection, "timeout", 0), "airdrop skips connection retry");
        check(!RelationsLocalProviderRetry.ShouldRetryLocalConnectionError(true, AIRequestDebugSource.DiplomacyDialogue, "Request cancelled by user", 0), "cancellation is not a connection retry");
        check(!RelationsLocalProviderRetry.LooksLikeTimeoutError("Request cancelled by context change"), "context cancel is not timeout");
    }

    static void TokenAccounting(Action<bool, string> check)
    {
        bool extracted = DialogueTokenUsageTracker.TryExtract(
            "{\"usage\":{\"input_tokens\":11,\"output_tokens\":7,\"total_tokens\":18}}",
            out int prompt,
            out int completion,
            out int total);
        check(extracted && prompt == 11 && completion == 7 && total == 18, "usage envelope extraction");

        var messages = new List<ChatMessageData> { new ChatMessageData { role = "user", content = new string('a', 40) } };
        DialogueTokenUsageTracker.Estimate(messages, "abcd", out int estPrompt, out int estCompletion, out int estTotal);
        check(estPrompt == 11 && estCompletion == 1 && estTotal == 12, "estimate is deterministic");
    }
}
