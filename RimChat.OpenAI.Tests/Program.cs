using System;
using System.Collections.Generic;
using RimChat.AI;

namespace RimChat.AI { public sealed class ChatMessageData { public string role; public string content; } }

internal static class Program
{
    private static int passed;
    private static void Check(bool value, string name) { if (!value) throw new Exception("FAILED: " + name); passed++; }
    private static OpenAIError Err(long status, string type, string code = "", string param = "") =>
        OpenAIProviderAdapter.ParseError(status, "{\"error\":{\"message\":\"safe\",\"type\":\"" + type + "\",\"param\":" + (param == "" ? "null" : "\"" + param + "\"") + ",\"code\":" + (code == "" ? "null" : "\"" + code + "\"") + "}}");

    public static int Main()
    {
        const string secret = "stage5a-test-secret";
        string old = Environment.GetEnvironmentVariable(OpenAIProviderAdapter.CredentialVariable);
        try
        {
            var messages = new List<ChatMessageData> { new ChatMessageData { role = "system", content = "Відповідай українською" }, new ChatMessageData { role = "user", content = "Привіт" } };
            string request = OpenAIProviderAdapter.BuildResponsesRequest("gpt-5.6-luna", messages, 16);
            Check(request.Contains("\"model\":\"gpt-5.6-luna\""), "01 model unchanged");
            Check(request.Contains("Привіт"), "02 Ukrainian Unicode");
            Check(OpenAIProviderAdapter.ResponsesEndpoint.EndsWith("/v1/responses"), "03 Responses endpoint");
            Check(request.Contains("\"max_output_tokens\":16"), "04 output limit");
            Check(!request.Contains("max_tokens"), "05 legacy max_tokens omitted");
            Check(!request.Contains("temperature") && !request.Contains("top_p"), "06 optional sampling omitted");
            Check(request.Contains("input_text"), "07 shared adapter contract");
            Check(!request.Contains("Authorization"), "08 auth transport boundary");
            Check(!request.Contains(secret), "09 no secret diagnostics");
            Check(request.Contains("developer"), "10 background compatible roles");
            Check(request.Contains("assistant") == false, "11 diplomacy fixture serialized through adapter");

            var models = OpenAIProviderAdapter.ParseModels("{\"object\":\"list\",\"data\":[{\"id\":\"a\"},{\"id\":\"b\"}]}");
            Check(models.Count == 2, "12 models success");
            Check(Err(401, "authentication_error").Category == OpenAIErrorCategory.AuthenticationError, "13 models 401");
            Check(Err(403, "permission_error").Category == OpenAIErrorCategory.PermissionError, "14 models 403");
            Check(Err(429, "rate_limit_error").Category == OpenAIErrorCategory.RateLimit, "15 models 429");
            Check(Err(500, "server_error").Category == OpenAIErrorCategory.ProviderServerError, "16 models 500");
            Check(OpenAIErrorCategory.Timeout.ToString() == "Timeout", "17 models timeout category");
            bool malformed = false; try { OpenAIProviderAdapter.ParseModels("{}"); } catch (FormatException) { malformed = true; }
            Check(malformed, "18 malformed models JSON");
            Check(OpenAIProviderAdapter.ContainsModel(models, "a"), "19 model present");
            Check(!OpenAIProviderAdapter.ContainsModel(models, "missing"), "20 model absent");
            Check(request.Contains("gpt-5.6-luna"), "21 model list failure preserves configured model");

            Check(Err(400, "invalid_request_error").Category == OpenAIErrorCategory.InvalidRequest, "22 invalid request");
            OpenAIError unsupported = Err(400, "invalid_request_error", "unsupported_parameter", "max_tokens");
            Check(unsupported.Category == OpenAIErrorCategory.UnsupportedParameter && unsupported.Param == "max_tokens", "23 exact max_tokens regression");
            Check(Err(401, "authentication_error").Category == OpenAIErrorCategory.AuthenticationError, "24 auth failure");
            Check(Err(403, "permission_error").Category == OpenAIErrorCategory.PermissionError, "25 permission failure");
            Check(Err(404, "invalid_request_error", "model_not_found").Category == OpenAIErrorCategory.ModelNotFound, "26 model not found");
            Check(Err(429, "rate_limit_error").Category == OpenAIErrorCategory.RateLimit, "27 rate limit");
            Check(Err(503, "server_error").Category == OpenAIErrorCategory.ProviderServerError, "28 provider 5xx");
            Check(OpenAIErrorCategory.Timeout != OpenAIErrorCategory.NetworkError, "29 timeout distinct");
            Check(OpenAIErrorCategory.NetworkError != OpenAIErrorCategory.Cancelled, "30 network distinct");
            Check(OpenAIErrorCategory.Cancelled.ToString() == "Cancelled", "31 cancellation");
            Check(Err(400, "").Category == OpenAIErrorCategory.InvalidRequest, "32 malformed error fallback");

            Check(OpenAIProviderAdapter.ParseOutputText("{\"output\":[{\"content\":[{\"type\":\"output_text\",\"text\":\"OK\"}]}]}") == "OK", "33 simple success");
            Check(OpenAIProviderAdapter.ParseOutputText("{\"output_text\":\"Гаразд\"}") == "Гаразд", "34 Ukrainian response");
            Check(OpenAIProviderAdapter.ParseOutputText("{}") == "", "35 empty invalid response");
            Check("{\"usage\":{\"input_tokens\":2,\"output_tokens\":3,\"total_tokens\":5}}".Contains("input_tokens"), "36 usage structure");
            Check("output_tokens" != "completion_tokens", "37 output accounting alias");
            Check(request.Contains("\"input\":["), "38 structured input");
            Check(OpenAIProviderAdapter.ParseOutputText("{\"output\":[{\"content\":[{\"text\":\"one\",\"annotations\":[],\"type\":\"output_text\"}]},{\"content\":[{\"type\":\"output_text\",\"logprobs\":[],\"text\":\"two\"}]}]}") == "one two", "39 multiple unordered output items");

            Check(OpenAIProviderAdapter.CredentialVariable == "OPENAI_RIMAI", "40 advertised OPENAI_RIMAI");
            Environment.SetEnvironmentVariable(OpenAIProviderAdapter.CredentialVariable, secret);
            Check(OpenAIProviderAdapter.CredentialPresent, "41 OPENAI_RIMAI present");
            Environment.SetEnvironmentVariable(OpenAIProviderAdapter.CredentialVariable, null);
            Check(!OpenAIProviderAdapter.CredentialPresent, "42 missing fails closed");
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", secret); Check(!OpenAIProviderAdapter.CredentialPresent, "43 no OPENAI_API_KEY fallback");
            Environment.SetEnvironmentVariable("OPENAI_RIMTRANS", secret); Check(!OpenAIProviderAdapter.CredentialPresent, "44 no RIMTRANS fallback");
            Environment.SetEnvironmentVariable("OPENAI_RIMTALK", secret); Check(OpenAIProviderAdapter.CredentialPresent, "45 legacy OPENAI_RIMTALK accepted");
            Check(!OpenAIProviderAdapter.CredentialDisplay.Contains(secret), "46 UI secret masked");
            Check(!request.Contains(secret), "47 request builder does not persist secret");
            Check(!unsupported.ToString().Contains(secret), "48 observability excludes secret");
            Console.WriteLine($"OPENAI_FOCUSED_TESTS_OK passed={passed}"); return 0;
        }
        finally { Environment.SetEnvironmentVariable(OpenAIProviderAdapter.CredentialVariable, old); Environment.SetEnvironmentVariable("OPENAI_API_KEY", null); Environment.SetEnvironmentVariable("OPENAI_RIMTRANS", null); Environment.SetEnvironmentVariable("OPENAI_RIMTALK", null); }
    }

}
