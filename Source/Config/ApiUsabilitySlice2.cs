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
    internal static class ApiUsabilitySlice2
    {
internal static IEnumerator RunLocalDiagnosticCoroutine(
            LocalModelConfig config,
            Action<ApiUsabilityProgress> onProgress,
            Action<ApiUsabilityDiagnosticResult> onCompleted)
        {
            DateTime startedAtUtc = DateTime.UtcNow;
            var steps = new List<ApiUsabilityStepResult>();

            // Player2 local: simplified diagnostic (no model listing, no Ollama probe)
            if (config != null && config.IsPlayer2Local())
            {
                yield return ApiUsabilityDiagnosticService.RunPlayer2LocalDiagnosticCoroutine(config, onProgress, onCompleted, startedAtUtc, steps);
                yield break;
            }

            const int totalSteps = 4;
            string modelName = config?.ModelName ?? string.Empty;
            string normalizedBaseUrl = ApiConfig.NormalizeUrl(config?.GetNormalizedBaseUrl() ?? string.Empty);

            ApiUsabilityDiagnosticResult validationFailure = ApiUsabilityDiagnosticService.ValidateLocalConfig(config, startedAtUtc, steps);
            if (validationFailure != null)
            {
                onCompleted?.Invoke(validationFailure);
                yield break;
            }

            ApiUsabilityDiagnosticService.NotifyProgress(onProgress, ApiUsabilityStep.ConfigValidation, 1, totalSteps);
            steps.Add(ApiUsabilityDiagnosticService.BuildStepSuccess(ApiUsabilityStep.ConfigValidation, normalizedBaseUrl, startedAtUtc));
            ApiUsabilityDiagnosticService.NotifyProgress(onProgress, ApiUsabilityStep.LocalServiceProbe, 2, totalSteps);

            ApiUsabilityLocalServiceProbe localProbe = default;
            yield return ApiUsabilityDiagnosticService.ProbeLocalServiceCoroutine(normalizedBaseUrl, probe => localProbe = probe);
            if (!localProbe.IsSuccess)
            {
                onCompleted?.Invoke(ApiUsabilityDiagnosticService.BuildFailureFromProbe(
                    ApiUsabilityStep.LocalServiceProbe,
                    localProbe.Response,
                    localProbe.EndpointUsed,
                    startedAtUtc,
                    steps,
                    modelName,
                    false,
                    null,
                    ApiUsabilityErrorCode.LOCAL_SERVICE_DOWN));
                yield break;
            }

            steps.Add(ApiUsabilityDiagnosticService.BuildStepSuccess(ApiUsabilityStep.LocalServiceProbe, localProbe.EndpointUsed, startedAtUtc));
            ApiUsabilityDiagnosticService.NotifyProgress(onProgress, ApiUsabilityStep.ChatProbe, 3, totalSteps);
            string chatEndpoint = ApiUsabilityDiagnosticService.BuildLocalChatEndpoint(normalizedBaseUrl, localProbe.ServiceType);
            string chatPayload = ApiUsabilityDiagnosticService.BuildLocalChatPayload(modelName, localProbe.ServiceType);
            ApiUsabilityProbeResponse chatProbe = default;
            yield return ApiUsabilityDiagnosticService.SendProbeCoroutine(
                ApiUsabilityDiagnosticService.BuildProbeRequest(chatEndpoint, "POST", chatPayload, AIProvider.None, string.Empty, LocalTimeoutSeconds),
                probe => chatProbe = probe);

            if (!chatProbe.IsHttpSuccess)
            {
                onCompleted?.Invoke(ApiUsabilityDiagnosticService.BuildFailureFromProbe(
                    ApiUsabilityStep.ChatProbe,
                    chatProbe,
                    chatEndpoint,
                    startedAtUtc,
                    steps,
                    modelName,
                    false,
                    chatPayload));
                yield break;
            }

            steps.Add(ApiUsabilityDiagnosticService.BuildStepSuccess(ApiUsabilityStep.ChatProbe, chatEndpoint, startedAtUtc));
            ApiUsabilityDiagnosticService.NotifyProgress(onProgress, ApiUsabilityStep.ResponseContractValidation, 4, totalSteps);
            ContractValidationOutcome localContract = ApiUsabilityDiagnosticService.ValidateLocalChatContract(localProbe.ServiceType, chatProbe.ResponseBody);
            ApiUsabilityProbeResponse localFinalProbe = chatProbe;
            bool localRetried = false;
            if (localContract.ShouldRetry)
            {
                localRetried = true;
                ApiUsabilityProbeResponse retryProbe = default;
                yield return ApiUsabilityDiagnosticService.SendProbeCoroutine(
                    ApiUsabilityDiagnosticService.BuildProbeRequest(chatEndpoint, "POST", chatPayload, AIProvider.None, string.Empty, LocalTimeoutSeconds),
                    probe => retryProbe = probe);

                if (!retryProbe.IsHttpSuccess)
                {
                    onCompleted?.Invoke(ApiUsabilityDiagnosticService.BuildFailureFromProbe(
                        ApiUsabilityStep.ResponseContractValidation,
                        retryProbe,
                        chatEndpoint,
                        startedAtUtc,
                        steps,
                        modelName,
                        false,
                        chatPayload));
                    yield break;
                }

                localFinalProbe = retryProbe;
                localContract = ApiUsabilityDiagnosticService.ValidateLocalChatContract(localProbe.ServiceType, retryProbe.ResponseBody);
            }

            if (!localContract.IsAccepted)
            {
                onCompleted?.Invoke(ApiUsabilityDiagnosticService.BuildFailure(
                    ApiUsabilityStep.ResponseContractValidation,
                    ApiUsabilityErrorCode.RESPONSE_SCHEMA_INVALID,
                    localContract.Detail,
                    localFinalProbe.HttpCode,
                    chatEndpoint,
                    startedAtUtc,
                    steps,
                    modelName,
                    false,
                    chatPayload,
                    localFinalProbe.ResponseBody));
                yield break;
            }

            steps.Add(ApiUsabilityDiagnosticService.BuildStepSuccess(ApiUsabilityStep.ResponseContractValidation, chatEndpoint, startedAtUtc));
            string localSuccessDetail = ApiUsabilityDiagnosticService.BuildContractValidationSuccessDetail(localContract, localRetried);
            onCompleted?.Invoke(ApiUsabilityDiagnosticService.BuildSuccess(
                ApiUsabilityStep.ResponseContractValidation,
                localFinalProbe.HttpCode,
                chatEndpoint,
                startedAtUtc,
                steps,
                modelName,
                false,
                chatPayload,
                localFinalProbe.ResponseBody,
                localSuccessDetail));
        }

internal static string GetStepLabelKey(ApiUsabilityStep step)
        {
            return step switch
            {
                ApiUsabilityStep.ConfigValidation => "RimChat_UsabilityStep_ConfigValidation",
                ApiUsabilityStep.RuntimeEndpointResolution => "RimChat_UsabilityStep_RuntimeEndpointResolution",
                ApiUsabilityStep.ModelsProbe => "RimChat_UsabilityStep_ModelsProbe",
                ApiUsabilityStep.ModelAvailability => "RimChat_UsabilityStep_ModelAvailability",
                ApiUsabilityStep.LocalServiceProbe => "RimChat_UsabilityStep_LocalServiceProbe",
                ApiUsabilityStep.ChatProbe => "RimChat_UsabilityStep_ChatProbe",
                _ => "RimChat_UsabilityStep_ResponseContractValidation"
            };
        }

internal static string GetErrorTitleKey(ApiUsabilityErrorCode code)
        {
            return code switch
            {
                ApiUsabilityErrorCode.AUTH_INVALID => "RimChat_UsabilityError_AUTH_INVALID",
                ApiUsabilityErrorCode.ENDPOINT_NOT_FOUND => "RimChat_UsabilityError_ENDPOINT_NOT_FOUND",
                ApiUsabilityErrorCode.MODEL_NOT_FOUND => "RimChat_UsabilityError_MODEL_NOT_FOUND",
                ApiUsabilityErrorCode.TIMEOUT => "RimChat_UsabilityError_TIMEOUT",
                ApiUsabilityErrorCode.RATE_LIMIT => "RimChat_UsabilityError_RATE_LIMIT",
                ApiUsabilityErrorCode.TLS_OR_CERT => "RimChat_UsabilityError_TLS_OR_CERT",
                ApiUsabilityErrorCode.DNS_OR_NETWORK => "RimChat_UsabilityError_DNS_OR_NETWORK",
                ApiUsabilityErrorCode.RESPONSE_SCHEMA_INVALID => "RimChat_UsabilityError_RESPONSE_SCHEMA_INVALID",
                ApiUsabilityErrorCode.LOCAL_SERVICE_DOWN => "RimChat_UsabilityError_LOCAL_SERVICE_DOWN",
                _ => "RimChat_UsabilityError_UNKNOWN"
            };
        }

internal static ApiUsabilityDiagnosticResult ValidateCloudConfig(
            ApiConfig config,
            DateTime startedAtUtc,
            List<ApiUsabilityStepResult> steps,
            string modelName)
        {
            if (config == null)
            {
                return ApiUsabilityDiagnosticService.BuildFailure(
                    ApiUsabilityStep.ConfigValidation,
                    ApiUsabilityErrorCode.UNKNOWN,
                    "Cloud config is null.",
                    0,
                    string.Empty,
                    startedAtUtc,
                    steps,
                    modelName,
                    true,
                    null,
                    string.Empty);
            }

            if (string.IsNullOrWhiteSpace(config.ApiKey))
            {
                return ApiUsabilityDiagnosticService.BuildFailure(
                    ApiUsabilityStep.ConfigValidation,
                    ApiUsabilityErrorCode.UNKNOWN,
                    "RimChat_EnterApiKey".Translate(),
                    0,
                    string.Empty,
                    startedAtUtc,
                    steps,
                    modelName,
                    true,
                    null,
                    string.Empty);
            }

            if (string.IsNullOrWhiteSpace(modelName))
            {
                return ApiUsabilityDiagnosticService.BuildFailure(
                    ApiUsabilityStep.ConfigValidation,
                    ApiUsabilityErrorCode.UNKNOWN,
                    "RimChat_ErrorEmptyModel".Translate(),
                    0,
                    string.Empty,
                    startedAtUtc,
                    steps,
                    modelName,
                    true,
                    null,
                    string.Empty);
            }

            return null;
        }

internal static ApiUsabilityDiagnosticResult ValidateLocalConfig(
            LocalModelConfig config,
            DateTime startedAtUtc,
            List<ApiUsabilityStepResult> steps)
        {
            string baseUrl = config?.GetNormalizedBaseUrl() ?? string.Empty;
            // Player2 local does not require a model name
            bool missingRequired = string.IsNullOrWhiteSpace(baseUrl) ||
                (!config.IsPlayer2Local() && string.IsNullOrWhiteSpace(config?.ModelName));
            if (missingRequired)
            {
                return ApiUsabilityDiagnosticService.BuildFailure(
                    ApiUsabilityStep.ConfigValidation,
                    ApiUsabilityErrorCode.UNKNOWN,
                    "Local config requires base URL and model name.",
                    0,
                    baseUrl,
                    startedAtUtc,
                    steps,
                    config?.ModelName ?? string.Empty,
                    false,
                    null,
                    string.Empty);
            }

            return null;
        }

internal static ApiUsabilityCloudRuntime ResolveCloudRuntime(ApiConfig config)
        {
            string modelsEndpoint;
            string chatEndpoint;
            string details = string.Empty;
            if (config.Provider == AIProvider.Custom && config.TryResolveCustomRuntimeEndpoints(out CustomUrlRuntimeResolution resolved))
            {
                modelsEndpoint = ApiConfig.NormalizeUrl(resolved.ModelsEndpoint);
                chatEndpoint = ApiConfig.NormalizeUrl(resolved.ChatEndpoint);
                if (resolved.HasSuspiciousBasePath)
                {
                    details = "Custom URL keeps a suspicious base path.";
                }
            }
            else
            {
                modelsEndpoint = ApiUsabilityDiagnosticService.ResolveCloudModelsEndpoint(config);
                chatEndpoint = ApiUsabilityDiagnosticService.ResolveCloudChatEndpoint(config);
            }

            return new ApiUsabilityCloudRuntime
            {
                ModelsEndpoint = modelsEndpoint,
                ChatEndpoint = chatEndpoint,
                Details = details
            };
        }

internal static string ResolveCloudModelsEndpoint(ApiConfig config)
        {
            if (config == null)
            {
                return string.Empty;
            }

            if (config.Provider == AIProvider.DeepSeek)
            {
                return config.Provider.GetListModelsUrl();
            }

            string baseUrl = ApiConfig.NormalizeUrl(config.BaseUrl);
            return string.IsNullOrWhiteSpace(baseUrl)
                ? config.Provider.GetListModelsUrl()
                : ApiConfig.ToModelsEndpoint(baseUrl);
        }

internal static string ResolveCloudChatEndpoint(ApiConfig config)
        {
            if (config == null)
            {
                return string.Empty;
            }

            if (config.Provider == AIProvider.DeepSeek)
            {
                return config.Provider.GetEndpointUrl();
            }

            string baseUrl = ApiConfig.NormalizeUrl(config.BaseUrl);
            return string.IsNullOrWhiteSpace(baseUrl)
                ? config.GetEffectiveEndpoint()
                : ApiConfig.EnsureChatCompletionsEndpoint(baseUrl);
        }

internal static IEnumerator ProbeLocalServiceCoroutine(
            string normalizedBaseUrl,
            Action<ApiUsabilityLocalServiceProbe> onCompleted)
        {
            ApiUsabilityProbeResponse ollamaProbe = default;
            string ollamaEndpoint = ApiUsabilityDiagnosticService.JoinUrl(normalizedBaseUrl, LocalOllamaProbePath);
            yield return ApiUsabilityDiagnosticService.SendProbeCoroutine(
                ApiUsabilityDiagnosticService.BuildProbeRequest(ollamaEndpoint, "GET", null, AIProvider.None, string.Empty, LocalTimeoutSeconds),
                probe => ollamaProbe = probe);

            if (ollamaProbe.IsHttpSuccess)
            {
                onCompleted?.Invoke(new ApiUsabilityLocalServiceProbe
                {
                    IsSuccess = true,
                    EndpointUsed = ollamaEndpoint,
                    ServiceType = ApiUsabilityLocalServiceType.Ollama,
                    Response = ollamaProbe
                });
                yield break;
            }

            ApiUsabilityProbeResponse openAiProbe = default;
            string openAiEndpoint = ApiUsabilityDiagnosticService.JoinUrl(normalizedBaseUrl, LocalOpenAiModelsPath);
            yield return ApiUsabilityDiagnosticService.SendProbeCoroutine(
                ApiUsabilityDiagnosticService.BuildProbeRequest(openAiEndpoint, "GET", null, AIProvider.None, string.Empty, LocalTimeoutSeconds),
                probe => openAiProbe = probe);

            if (openAiProbe.IsHttpSuccess)
            {
                onCompleted?.Invoke(new ApiUsabilityLocalServiceProbe
                {
                    IsSuccess = true,
                    EndpointUsed = openAiEndpoint,
                    ServiceType = ApiUsabilityLocalServiceType.OpenAiCompatible,
                    Response = openAiProbe
                });
                yield break;
            }

            if (ApiUsabilityDiagnosticService.IsModelsEndpointMissingStatusCode(openAiProbe.HttpCode))
            {
                Log.Warning($"[RimAI.Relations] Local OpenAI-compatible models endpoint missing (HTTP {openAiProbe.HttpCode}), fallback to chat probe. endpoint={openAiEndpoint}");
                onCompleted?.Invoke(new ApiUsabilityLocalServiceProbe
                {
                    IsSuccess = true,
                    EndpointUsed = openAiEndpoint,
                    ServiceType = ApiUsabilityLocalServiceType.OpenAiCompatible,
                    Response = openAiProbe
                });
                yield break;
            }

            ApiUsabilityProbeResponse failed = openAiProbe.HttpCode > 0 ? openAiProbe : ollamaProbe;
            onCompleted?.Invoke(new ApiUsabilityLocalServiceProbe
            {
                IsSuccess = false,
                EndpointUsed = failed.HttpCode > 0 ? openAiEndpoint : ollamaEndpoint,
                ServiceType = ApiUsabilityLocalServiceType.Unknown,
                Response = failed
            });
        }

internal static ApiUsabilityProbeRequest BuildProbeRequest(
            string url,
            string method,
            string body,
            AIProvider provider,
            string apiKey,
            int timeoutSeconds)
        {
            return new ApiUsabilityProbeRequest
            {
                Url = ApiConfig.NormalizeUrl(url),
                Method = string.IsNullOrWhiteSpace(method) ? "GET" : method,
                Body = body ?? string.Empty,
                Provider = provider,
                ApiKey = (apiKey ?? string.Empty).Trim(),
                TimeoutSeconds = Mathf.Clamp(timeoutSeconds, 3, 30)
            };
        }
    }
}
