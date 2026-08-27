using System;
using System.Collections.Generic;
using Ustas.RimAI.Communication.Relations.AI;

internal static class ParserLayerTests
{
    public static void Run(Action<bool, string> check)
    {
        Sse(check);
        OpenAiEnvelopes(check);
        CompatibleAndErrors(check);
        GenericJson(check);
        DomainActions(check);
        ValidationBoundary(check);
    }

    static void Sse(Action<bool, string> check)
    {
        List<string> single = SseFrameReader.EnumerateDataPayloads("data: hello\n\n");
        check(single.Count == 1 && single[0] == "hello", "sse single data payload");

        List<string> multi = SseFrameReader.EnumerateDataPayloads("data: one\ndata: two\n\n");
        check(multi.Count == 2 && multi[0] == "one" && multi[1] == "two", "sse multi-event data lines");

        List<SseFrame> blank = SseFrameReader.ReadEvents("data: a\n\ndata: b\n\n");
        check(blank.Count == 2 && blank[0].Data == "a" && blank[1].Data == "b", "sse blank-line event boundaries");

        List<SseFrame> joined = SseFrameReader.ReadEvents("data: line1\ndata: line2\n\n");
        check(joined.Count == 1 && joined[0].Data == "line1\nline2", "sse multiple data lines join");

        List<SseFrame> comments = SseFrameReader.ReadEvents(": keep-alive\ndata: ok\n\n");
        check(comments.Count == 1 && comments[0].Data == "ok", "sse comments ignored");

        List<SseFrame> done = SseFrameReader.ReadEvents("data: [DONE]\n\n");
        check(done.Count == 1 && done[0].IsDone, "sse [DONE] marked");
        check(SseFrameReader.EnumerateDataPayloads("data: [DONE]\n").Count == 0, "sse [DONE] skipped in production payloads");

        List<SseFrame> incomplete = SseFrameReader.ReadEvents("event: delta\ndata: last");
        check(incomplete.Count == 1 && incomplete[0].EventName == "delta" && incomplete[0].Data == "last", "sse incomplete final frame");

        List<string> crlf = SseFrameReader.EnumerateDataPayloads("data: a\r\ndata: b\r\n");
        check(crlf.Count == 2 && crlf[0] == "a" && crlf[1] == "b", "sse CRLF");

        List<string> lf = SseFrameReader.EnumerateDataPayloads("data: a\ndata: b\n");
        check(lf.Count == 2, "sse LF");

        check(SseFrameReader.EnumerateDataPayloads("not-a-field\ndata:\n").Count == 0, "sse malformed/empty data skipped");
        check(SseFrameReader.LooksLikeSse("data: x"), "sse looks-like");
        check(!SseFrameReader.LooksLikeSse("{\"content\":\"hi\"}"), "json is not sse");
    }

    static void OpenAiEnvelopes(Action<bool, string> check)
    {
        ProviderTextResult ok = OpenAiResponsesEnvelopeParser.Parse(
            "{\"output\":[{\"content\":[{\"type\":\"output_text\",\"text\":\"Visible line\"}]}]}");
        check(ok.Success && ok.Text == "Visible line", "openai responses output_text");

        check(OpenAiResponsesEnvelopeParser.ExtractOutputText("{\"output_text\":\"Гаразд\"}") == "Гаразд", "openai convenience output_text");

        ProviderTextResult empty = OpenAiResponsesEnvelopeParser.Parse("{}");
        check(!empty.Success && empty.ErrorKind == ProviderTextErrorKind.Empty, "openai empty output");
        check(empty.ReasonTag == "no_output_text", "openai empty reason");

        ProviderTextResult incomplete = OpenAiResponsesEnvelopeParser.Parse(
            "{\"status\":\"incomplete\",\"incomplete_details\":{\"reason\":\"max_output_tokens\"},\"output\":[{\"type\":\"reasoning\"}]}");
        check(!incomplete.Success && incomplete.ReasonTag == "incomplete_max_output_tokens", "openai incomplete token limit reason");
        check(incomplete.MatchedPath == "incomplete_details.reason", "openai incomplete reason path");

        ProviderTextResult partial = OpenAiResponsesEnvelopeParser.Parse(
            "{\"status\":\"incomplete\",\"incomplete_details\":{\"reason\":\"max_output_tokens\"},\"output\":[{\"content\":[{\"type\":\"output_text\",\"text\":\"Usable partial line\"}]}]}");
        check(partial.Success && partial.Text == "Usable partial line", "openai incomplete payload keeps usable output text");

        ProviderTextResult malformed = OpenAiResponsesEnvelopeParser.Parse("   ");
        check(!malformed.Success && malformed.ErrorKind == ProviderTextErrorKind.Empty, "openai blank payload");

        string multi = OpenAIProviderAdapter.ParseOutputText(
            "{\"output\":[{\"content\":[{\"text\":\"one\",\"annotations\":[],\"type\":\"output_text\"}]},{\"content\":[{\"type\":\"output_text\",\"logprobs\":[],\"text\":\"two\"}]}]}");
        check(multi == "one two", "openai unordered output items");

        ProviderTextResult error = OpenAiResponsesEnvelopeParser.Parse("{\"error\":{\"message\":\"nope\"}}");
        check(!error.Success && error.ErrorKind == ProviderTextErrorKind.ErrorEnvelope, "openai error envelope is not text");
        check(error.ReasonTag == "error_payload", "openai error reason");
        check(OpenAiResponsesEnvelopeParser.ExtractOutputText("{\"error\":{\"message\":\"nope\"}}") == string.Empty, "openai error extract empty");

        PrimaryTextExtractionResult viaExtractor = RelationsProviderTextExtractor.Extract(
            "{\"error\":{\"message\":\"nope\"}}",
            AIProvider.OpenAI);
        check(!viaExtractor.IsSuccess && viaExtractor.ReasonTag == "error_payload", "openai extractor rejects error envelope");

        // A live gpt-5.6 Responses reply, trimmed but shape-faithful. Every success
        // envelope carries "error": null, which an earlier presence-only check read as
        // an error payload - so every dialogue call failed while the provider was fine.
        ProviderTextResult liveEnvelope = OpenAiResponsesEnvelopeParser.Parse(
            "{\"id\":\"resp_0402b67b31fd60d6\",\"object\":\"response\",\"status\":\"completed\",\"error\":null,\"incomplete_details\":null,\"max_output_tokens\":2048,\"model\":\"gpt-5.6-luna\",\"output\":[{\"id\":\"msg_0402b67b31fd60d6\",\"type\":\"message\",\"status\":\"completed\",\"content\":[{\"type\":\"output_text\",\"annotations\":[],\"logprobs\":[],\"text\":\"I am doing well, though the nutrient paste tastes like despair today.\"}],\"role\":\"assistant\"}],\"store\":false,\"usage\":{\"input_tokens\":31,\"output_tokens\":18}}");
        check(liveEnvelope.Success, "openai null error field is not an error payload");
        check(liveEnvelope.Text.StartsWith("I am doing well"), "openai live envelope text");

        ProviderTextResult emptyErrorField = OpenAiResponsesEnvelopeParser.Parse(
            "{\"error\":\"\",\"output\":[{\"content\":[{\"type\":\"output_text\",\"text\":\"Still fine\"}]}]}");
        check(emptyErrorField.Success && emptyErrorField.Text == "Still fine", "openai empty error string is not an error payload");

        // Text the model produced outranks a populated error marker: a partial reply is
        // worth more to the player than a generic failure line.
        ProviderTextResult textWins = OpenAiResponsesEnvelopeParser.Parse(
            "{\"error\":{\"message\":\"late failure\"},\"output\":[{\"content\":[{\"type\":\"output_text\",\"text\":\"Partial reply\"}]}]}");
        check(textWins.Success && textWins.Text == "Partial reply", "openai usable text outranks error marker");

        PrimaryTextExtractionResult liveViaExtractor = RelationsProviderTextExtractor.Extract(
            "{\"id\":\"resp_0402b67b31fd60d6\",\"object\":\"response\",\"status\":\"completed\",\"error\":null,\"incomplete_details\":null,\"max_output_tokens\":2048,\"model\":\"gpt-5.6-luna\",\"output\":[{\"id\":\"msg_0402b67b31fd60d6\",\"type\":\"message\",\"status\":\"completed\",\"content\":[{\"type\":\"output_text\",\"annotations\":[],\"logprobs\":[],\"text\":\"I am doing well, though the nutrient paste tastes like despair today.\"}],\"role\":\"assistant\"}],\"store\":false,\"usage\":{\"input_tokens\":31,\"output_tokens\":18}}",
            AIProvider.OpenAI);
        check(liveViaExtractor.IsSuccess, "openai extractor accepts a live success envelope");

        // OpenAI escapes every non-ASCII character as \uXXXX. A decoder that only
        // handled \n and quotes delivered the escape sequences themselves to the player.
        ProviderTextResult cyrillic = OpenAiResponsesEnvelopeParser.Parse(
            "{\"output\":[{\"content\":[{\"type\":\"output_text\",\"text\":\"\\u042f \\u0441\\u043b\\u0443\\u0445\\u0430\\u044e. \\u2019 \\ud83d\\ude42\"}]}]}");
        check(cyrillic.Success, "openai unicode escapes parse");
        check(cyrillic.Text.StartsWith("Я слухаю."), "openai decodes hex escapes to Cyrillic");
        check(cyrillic.Text.Contains("’"), "openai decodes a punctuation escape");
        check(cyrillic.Text.Contains(char.ConvertFromUtf32(0x1F642)), "openai decodes a surrogate pair");
        check(cyrillic.Text.IndexOf("\\u", StringComparison.Ordinal) < 0, "openai leaves no raw escape sequence in player text");
        check(RelationsProviderTextExtractor.IsRetryableEmptyPrimaryText("no_output_text"), "openai missing output text is retryable");
        check(RelationsProviderTextExtractor.IsRetryableEmptyPrimaryText("incomplete_max_output_tokens"), "openai incomplete token limit is retryable");
    }

    static void CompatibleAndErrors(Action<bool, string> check)
    {
        PrimaryTextExtractionResult compatible = RelationsProviderTextExtractor.Extract(
            "{\"choices\":[{\"message\":{\"content\":\"Compat\"}}]}",
            AIProvider.DeepSeek);
        check(compatible.IsSuccess && compatible.Content.Contains("Compat"), "compatible chat envelope");

        PrimaryTextExtractionResult error = RelationsProviderTextExtractor.Extract(
            "{\"error\":{\"message\":\"nope\"}}",
            AIProvider.DeepSeek);
        check(!error.IsSuccess && error.ReasonTag == "error_payload", "compatible error envelope");

        string sse = "data: {\"choices\":[{\"message\":{\"content\":\"A\"}}]}\ndata: {\"choices\":[{\"message\":{\"content\":\"B\"}}]}\ndata: [DONE]\n";
        PrimaryTextExtractionResult streamed = CompatibleChatEnvelopeParser.Extract(sse);
        check(streamed.IsSuccess && streamed.MatchedPath == "sse.data", "compatible sse path");
        check(streamed.Content.Contains("A") && streamed.Content.Contains("B"), "compatible sse concatenates chunks");

        PrimaryTextExtractionResult doneOnly = CompatibleChatEnvelopeParser.Extract("data: [DONE]\n");
        check(!doneOnly.IsSuccess && doneOnly.ReasonTag == "sse_no_extractable_text", "sse done-only is empty");
    }

    static void GenericJson(Action<bool, string> check)
    {
        check(JsonBoundedExtractor.LooksLikeSingleJsonObject("{\"a\":1}"), "plain json object");
        check(JsonBoundedExtractor.LooksLikeJsonPayload("[1,2]"), "json array looks like json");
        check(JsonMarkdownFence.StripWrappingFence("```json\n{\"a\":1}\n```") == "{\"a\":1}", "wrapping json fence");
        check(JsonMarkdownFence.StripWrappingFence("  {\"a\":1}  ") == "{\"a\":1}", "whitespace object");

        check(JsonBoundedExtractor.TryExtractFirstObject("say {\"x\":\"a}b\"}", out string extracted) &&
              extracted == "{\"x\":\"a}b\"}", "braces inside quoted strings");

        check(JsonBoundedExtractor.TryExtractFirstObject("{\"q\":\"say \\\"hi\\\"\"}", out string escaped) &&
              escaped.Contains("\\\"hi\\\""), "escaped quotes stay inside object");

        check(JsonMarkdownFence.TryExtractFencedBlock("prose\n```json\n{\"k\":1}\n```\nmore", out string fenced) &&
              fenced == "{\"k\":1}", "leading commentary fence recovery");

        check(JsonMarkdownFence.TryExtractFencedBlock("```\n{\"k\":1}\n``` trailing", out string trailing) &&
              trailing.Contains("{\"k\":1}"), "trailing commentary after fence");

        check(!JsonBoundedExtractor.TryExtractFirstObject("not json", out _), "malformed has no object");
        check(!JsonBoundedExtractor.LooksLikeSingleJsonObject("{unterminated"), "unterminated object");
    }

    static void DomainActions(Action<bool, string> check)
    {
        check(DiplomacyActionCatalog.NormalizeActionName("exit") == "exit_dialogue", "diplomacy alias exit");
        check(DiplomacyActionCatalog.IsValidAction("adjust_goodwill"), "valid diplomacy action");
        check(!DiplomacyActionCatalog.IsValidAction("send_image"), "unknown action not coerced");
        check(!DiplomacyActionCatalog.IsValidAction("totally_unknown"), "unknown discriminator rejected");
        check(DiplomacyActionCatalog.NormalizeActionName("none") == "none", "none stays none");
        check(!DiplomacyActionCatalog.IsValidAction("none"), "none is not a valid action");
        check(string.IsNullOrEmpty(DiplomacyActionCatalog.NormalizeActionName("")), "missing discriminator");

        string strategyJson = "[" +
            "{\"strategy_name\":\"A\",\"reason\":\"r1\",\"content\":\"do one\"}," +
            "{\"strategy_name\":\"B\",\"reason\":\"r2\",\"content\":\"do two\"}," +
            "{\"strategy_name\":\"C\",\"reason\":\"r3\",\"content\":\"do three\"}]";
        List<StrategySuggestion> three = DiplomacyStrategySuggestionParser.ParseStrategySuggestions(strategyJson);
        check(three.Count == 3 && three[0].Content == "do one", "strategy exactly three kept");

        string two = "[" +
            "{\"strategy_name\":\"A\",\"reason\":\"r1\",\"content\":\"do one\"}," +
            "{\"strategy_name\":\"B\",\"reason\":\"r2\",\"content\":\"do two\"}]";
        check(DiplomacyStrategySuggestionParser.ParseStrategySuggestions(two).Count == 0, "strategy not three discarded");

        string actions = "{\"actions\":[{\"action\":\"exit\",\"reason\":\"bye\",\"parameters\":{}}]}";
        check(JsonLooseObjectParser.ExtractJsonArray(actions, "actions").Contains("exit"), "action array extract");
        check(JsonLooseObjectParser.ExtractJsonString("{\"action\":\"adjust_goodwill\"}", "action") == "adjust_goodwill", "action discriminator extract");
    }

    static void ValidationBoundary(Action<bool, string> check)
    {
        const string json = "{\"visible_dialogue\":\"I will arrange the prisoner transfer immediately.\"}";
        check(JsonBoundedExtractor.LooksLikeSingleJsonObject(json), "structural parse succeeds");
        string visible = JsonLooseObjectParser.ExtractJsonString(json, "visible_dialogue");
        DiplomacyResponseContractCheckResult contract = DiplomacyResponseContractGuard.Validate(visible);
        check(!contract.IsValid, "valid json can still fail diplomacy contract");

        const string rpgJson = "{\"visible_dialogue\":\"Line one.\\nLine two.\"}";
        check(JsonBoundedExtractor.LooksLikeSingleJsonObject(rpgJson), "rpg json structurally valid");
        string rpgVisible = JsonLooseObjectParser.ExtractJsonString(rpgJson, "visible_dialogue");
        RpgResponseContractCheckResult rpg = RpgResponseContractGuard.Validate(rpgVisible);
        check(!rpg.IsValid, "valid json can still fail rpg contract");

        DiplomacyResponseContractCheckResult ok = DiplomacyResponseContractGuard.Validate("The caravan will wait at the gates.");
        check(ok.IsValid, "contract accepts valid diplomacy speech");
    }
}
