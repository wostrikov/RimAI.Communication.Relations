using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ustas.RimAI.Communication.Relations.AI;
using UnityEngine;
using UnityEngine.Networking;
using Verse;

using ApiUsabilityCloudRuntime = Ustas.RimAI.Communication.Relations.Config.ApiUsabilityDiagnosticServiceHelpers.ApiUsabilityCloudRuntime;
using ApiUsabilityProbeRequest = Ustas.RimAI.Communication.Relations.Config.ApiUsabilityDiagnosticServiceHelpers.ApiUsabilityProbeRequest;
using ApiUsabilityProbeResponse = Ustas.RimAI.Communication.Relations.Config.ApiUsabilityDiagnosticServiceHelpers.ApiUsabilityProbeResponse;
using ApiUsabilityLocalServiceProbe = Ustas.RimAI.Communication.Relations.Config.ApiUsabilityDiagnosticServiceHelpers.ApiUsabilityLocalServiceProbe;
using ApiUsabilityLocalServiceType = Ustas.RimAI.Communication.Relations.Config.ApiUsabilityDiagnosticServiceHelpers.ApiUsabilityLocalServiceType;
using ContractValidationOutcome = Ustas.RimAI.Communication.Relations.Config.ApiUsabilityDiagnosticService.ContractValidationOutcome;

using static Ustas.RimAI.Communication.Relations.Config.ApiUsabilityDiagnosticService;

namespace Ustas.RimAI.Communication.Relations.Config
{
    internal static class ApiUsabilitySlice3
    {
internal static IEnumerator SendProbeCoroutine(
            ApiUsabilityProbeRequest request,
            Action<ApiUsabilityProbeResponse> onCompleted)
        {
            if (string.IsNullOrWhiteSpace(request.Url))
            {
                onCompleted?.Invoke(new ApiUsabilityProbeResponse
                {
                    HttpCode = 0,
                    Error = "Empty URL.",
                    ResponseBody = string.Empty
                });
                yield break;
            }

            using (var web = new UnityWebRequest(request.Url, request.Method))
            {
                web.downloadHandler = new DownloadHandlerBuffer();
                web.timeout = request.TimeoutSeconds;
                ApiUsabilityDiagnosticService.ApplyProbeAuthHeader(web, request.Provider, request.ApiKey, request.Url);
                if (!string.IsNullOrWhiteSpace(request.Body))
                {
                    byte[] payload = Encoding.UTF8.GetBytes(request.Body);
                    web.uploadHandler = new UploadHandlerRaw(payload);
                    web.SetRequestHeader("Content-Type", "application/json");
                }

                yield return web.SendWebRequest();
                onCompleted?.Invoke(new ApiUsabilityProbeResponse
                {
                    HttpCode = web.responseCode,
                    Error = web.error ?? string.Empty,
                    ResponseBody = web.downloadHandler?.text ?? string.Empty
                });
            }
        }

internal static void ApplyProbeAuthHeader(UnityWebRequest request, AIProvider provider, string apiKey, string requestUrl)
        {
            // Player2 requires both Bearer token and game-key header
            if (provider == AIProvider.Player2)
            {
                string trimmedKey = apiKey?.Trim();
                if (!string.IsNullOrWhiteSpace(trimmedKey))
                {
                    request.SetRequestHeader("Authorization", $"Bearer {trimmedKey}");
                }
                var extraHeaders = provider.GetExtraHeaders();
                if (extraHeaders != null)
                {
                    foreach (var header in extraHeaders)
                    {
                        request.SetRequestHeader(header.Key, header.Value);
                    }
                }
                return;
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return;
            }

            if (provider == AIProvider.Google)
            {
                if (ApiUsabilityDiagnosticService.IsGoogleOpenAiCompatibleUrl(requestUrl))
                {
                    request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                }
                else
                {
                    request.SetRequestHeader("x-goog-api-key", apiKey);
                }

                return;
            }

            request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
        }

internal static bool IsGoogleOpenAiCompatibleUrl(string requestUrl)
        {
            if (string.IsNullOrWhiteSpace(requestUrl))
            {
                return false;
            }

            return requestUrl.IndexOf("/openai/", StringComparison.OrdinalIgnoreCase) >= 0
                || requestUrl.IndexOf("/chat/completions", StringComparison.OrdinalIgnoreCase) >= 0;
        }

internal static List<string> ParseCloudModels(string responseBody, AIProvider provider)
        {
            if (provider == AIProvider.Google)
            {
                List<string> googleModels = ApiUsabilityDiagnosticService.ExtractQuotedValues(responseBody, "\"name\"");
                return googleModels
                    .Select(NormalizeGoogleModelName)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            List<string> openAiModels = ApiUsabilityDiagnosticService.ExtractQuotedValues(responseBody, "\"id\"");
            if (openAiModels.Count == 0)
            {
                openAiModels = ApiUsabilityDiagnosticService.ExtractQuotedValues(responseBody, "\"model\"");
            }

            return openAiModels
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

internal static List<string> ParseLocalModels(ApiUsabilityLocalServiceType serviceType, string responseBody)
        {
            List<string> source = serviceType == ApiUsabilityLocalServiceType.Ollama
                ? ApiUsabilityDiagnosticService.ExtractQuotedValues(responseBody, "\"name\"")
                : ApiUsabilityDiagnosticService.ExtractQuotedValues(responseBody, "\"id\"");

            return source
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

internal static bool ContainsModel(List<string> models, string targetModel)
        {
            if (models == null || models.Count == 0 || string.IsNullOrWhiteSpace(targetModel))
            {
                return false;
            }

            string normalized = targetModel.Trim();
            string prefix = ApiUsabilityDiagnosticService.BuildModelTagPrefix(normalized);
            return models.Any(model =>
                string.Equals(model, normalized, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(model, prefix, StringComparison.OrdinalIgnoreCase) ||
                model.StartsWith(normalized + ":", StringComparison.OrdinalIgnoreCase));
        }

internal static string BuildMissingModelsFallbackDetail(long httpCode)
        {
            string codeText = httpCode > 0 ? httpCode.ToString() : "unknown";
            return $"models_endpoint_missing_http={codeText}; fallback_to_chat_probe=true";
        }

internal static string BuildOpenAiChatPayload(string modelName)
        {
            string escapedModel = ApiUsabilityDiagnosticService.EscapeJsonString(modelName);
            return "{"
                + $"\"model\":\"{escapedModel}\","
                + "\"messages\":[{\"role\":\"user\",\"content\":\"ping\"}],"
                + "\"max_tokens\":8,"
                + "\"temperature\":0"
                + "}";
        }

internal static string BuildCloudProbePayload(AIProvider provider, string modelName)
        {
            if (provider == AIProvider.OpenAI)
            {
                return OpenAIProviderAdapter.BuildResponsesRequest(
                    modelName,
                    new List<ChatMessageData> { new ChatMessageData { role = "user", content = "Reply with OK." } },
                    16);
            }
            return ApiUsabilityDiagnosticService.BuildOpenAiChatPayload(modelName);
        }

internal static ContractValidationOutcome ValidateCloudContract(AIProvider provider, string responseBody)
        {
            if (provider != AIProvider.OpenAI)
            {
                return ApiUsabilityDiagnosticService.ValidateOpenAiChatContract(responseBody);
            }
            string extracted = OpenAIProviderAdapter.ParseOutputText(responseBody);
            return !string.IsNullOrWhiteSpace(extracted)
                ? ContractValidationOutcome.Pass()
                : ContractValidationOutcome.Fail("OpenAI Responses payload contains no output_text content.");
        }

internal static string BuildPlayer2ChatPayload()
        {
            return "{"
                + "\"messages\":[{\"role\":\"user\",\"content\":\"ping\"}],"
                + "\"max_tokens\":8,"
                + "\"temperature\":0"
                + "}";
        }

internal static string BuildLocalChatEndpoint(string baseUrl, ApiUsabilityLocalServiceType serviceType)
        {
            return serviceType == ApiUsabilityLocalServiceType.Ollama
                ? ApiUsabilityDiagnosticService.JoinUrl(baseUrl, LocalOllamaGeneratePath)
                : ApiUsabilityDiagnosticService.JoinUrl(baseUrl, LocalOpenAiChatPath);
        }

internal static string BuildLocalChatPayload(string modelName, ApiUsabilityLocalServiceType serviceType)
        {
            string escapedModel = ApiUsabilityDiagnosticService.EscapeJsonString(modelName);
            if (serviceType == ApiUsabilityLocalServiceType.Ollama)
            {
                return "{"
                    + $"\"model\":\"{escapedModel}\","
                    + "\"prompt\":\"ping\","
                    + "\"stream\":false"
                    + "}";
            }

            return ApiUsabilityDiagnosticService.BuildOpenAiChatPayload(modelName);
        }

internal static ContractValidationOutcome ValidateOpenAiChatContract(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return ContractValidationOutcome.Fail("Chat response body is empty.");
            }

            if (!ApiUsabilityDiagnosticService.ContainsJsonField(responseBody, "\"choices\""))
            {
                return ContractValidationOutcome.Fail("Missing choices field.");
            }

            List<string> contents = ApiUsabilityDiagnosticService.ExtractQuotedValues(responseBody, "\"content\"");
            if (contents.Count > 0 && contents.Any(value => !string.IsNullOrWhiteSpace(value)))
            {
                return ContractValidationOutcome.Pass();
            }

            bool hasFinishReason = ApiUsabilityDiagnosticService.ContainsJsonField(responseBody, "\"finish_reason\"");
            bool hasUsageSignal = ApiUsabilityDiagnosticService.ContainsJsonField(responseBody, "\"usage\"")
                || ApiUsabilityDiagnosticService.ContainsJsonField(responseBody, "\"prompt_tokens\"")
                || ApiUsabilityDiagnosticService.ContainsJsonField(responseBody, "\"completion_tokens\"")
                || ApiUsabilityDiagnosticService.ContainsJsonField(responseBody, "\"total_tokens\"");
            if (hasFinishReason || hasUsageSignal)
            {
                return ContractValidationOutcome.Warning("Missing assistant content; accepted by finish_reason/usage signal.");
            }

            return ContractValidationOutcome.RetryableFail("Missing assistant content without finish_reason/usage signal.");
        }

internal static string ExtractModelFromChatResponse(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return null;
            }

            List<string> models = ApiUsabilityDiagnosticService.ExtractQuotedValues(responseBody, "\"model\"");
            return models.FirstOrDefault(model => !string.IsNullOrWhiteSpace(model));
        }

internal static ContractValidationOutcome ValidateLocalChatContract(ApiUsabilityLocalServiceType serviceType, string responseBody)
        {
            if (serviceType != ApiUsabilityLocalServiceType.Ollama)
            {
                return ApiUsabilityDiagnosticService.ValidateOpenAiChatContract(responseBody);
            }

            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return ContractValidationOutcome.Fail("Ollama response body is empty.");
            }

            List<string> values = ApiUsabilityDiagnosticService.ExtractQuotedValues(responseBody, "\"response\"");
            if (values.Count > 0 && values.Any(value => !string.IsNullOrWhiteSpace(value)))
            {
                return ContractValidationOutcome.Pass();
            }

            bool hasDoneSignal = ApiUsabilityDiagnosticService.ContainsJsonField(responseBody, "\"done\"")
                || ApiUsabilityDiagnosticService.ContainsJsonField(responseBody, "\"eval_count\"")
                || ApiUsabilityDiagnosticService.ContainsJsonField(responseBody, "\"prompt_eval_count\"");
            if (hasDoneSignal)
            {
                return ContractValidationOutcome.Warning("Missing response text; accepted by ollama completion signal.");
            }

            return ContractValidationOutcome.RetryableFail("Missing response field without ollama completion signal.");
        }

internal static string BuildContractValidationSuccessDetail(ContractValidationOutcome outcome, bool retried)
        {
            if (outcome.IsWarning)
            {
                string retryText = retried ? "true" : "false";
                return $"contract_warning={outcome.Detail}; retry_probe={retryText}";
            }

            if (retried)
            {
                return "retry_probe=true";
            }

            return string.Empty;
        }

internal static string AppendDiagnosticDetail(string primary, string extra)
        {
            bool hasPrimary = !string.IsNullOrWhiteSpace(primary);
            bool hasExtra = !string.IsNullOrWhiteSpace(extra);
            if (!hasPrimary)
            {
                return hasExtra ? extra.Trim() : string.Empty;
            }

            if (!hasExtra)
            {
                return primary.Trim();
            }

            return $"{primary.Trim()}; {extra.Trim()}";
        }

internal static bool ContainsJsonField(string responseBody, string fieldToken)
        {
            return !string.IsNullOrWhiteSpace(responseBody)
                && !string.IsNullOrWhiteSpace(fieldToken)
                && responseBody.IndexOf(fieldToken, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
