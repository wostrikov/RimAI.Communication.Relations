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
    internal static class ApiUsabilitySlice1
    {
internal static IEnumerator RunCloudDiagnosticCoroutine(
            ApiConfig config,
            Action<ApiUsabilityProgress> onProgress,
            Action<ApiUsabilityDiagnosticResult> onCompleted)
        {
            DateTime startedAtUtc = DateTime.UtcNow;
            var steps = new List<ApiUsabilityStepResult>();

            // Player2 has no models endpoint and selects model server-side;
            // run a simplified diagnostic that only validates chat connectivity.
            if (config?.Provider == AIProvider.Player2)
            {
                yield return ApiUsabilityDiagnosticService.RunPlayer2CloudDiagnosticCoroutine(config, onProgress, onCompleted, startedAtUtc, steps);
                yield break;
            }

            const int totalSteps = 6;
            string modelName = config?.GetEffectiveModelName() ?? string.Empty;

            ApiUsabilityDiagnosticResult validationFailure = ApiUsabilityDiagnosticService.ValidateCloudConfig(config, startedAtUtc, steps, modelName);
            if (validationFailure != null)
            {
                onCompleted?.Invoke(validationFailure);
                yield break;
            }

            ApiUsabilityDiagnosticService.NotifyProgress(onProgress, ApiUsabilityStep.ConfigValidation, 1, totalSteps);
            steps.Add(ApiUsabilityDiagnosticService.BuildStepSuccess(ApiUsabilityStep.ConfigValidation, string.Empty, startedAtUtc));
            ApiUsabilityCloudRuntime runtime = ApiUsabilityDiagnosticService.ResolveCloudRuntime(config);
            if (!runtime.IsValid)
            {
                onCompleted?.Invoke(ApiUsabilityDiagnosticService.BuildFailure(
                    ApiUsabilityStep.RuntimeEndpointResolution,
                    ApiUsabilityErrorCode.ENDPOINT_NOT_FOUND,
                    runtime.Details,
                    0,
                    runtime.ModelsEndpoint,
                    startedAtUtc,
                    steps,
                    modelName,
                    true,
                    null,
                    runtime.Details));
                yield break;
            }

            ApiUsabilityDiagnosticService.NotifyProgress(onProgress, ApiUsabilityStep.RuntimeEndpointResolution, 2, totalSteps);
            steps.Add(ApiUsabilityDiagnosticService.BuildStepSuccess(ApiUsabilityStep.RuntimeEndpointResolution, runtime.ModelsEndpoint, startedAtUtc));

            ApiUsabilityDiagnosticService.NotifyProgress(onProgress, ApiUsabilityStep.ModelsProbe, 3, totalSteps);
            ApiUsabilityProbeResponse modelsProbe = default;
            yield return ApiUsabilityDiagnosticService.SendProbeCoroutine(
                ApiUsabilityDiagnosticService.BuildProbeRequest(runtime.ModelsEndpoint, "GET", null, config.Provider, config.GetRuntimeApiKey(), CloudTimeoutSeconds),
                probe => modelsProbe = probe);
            bool modelsEndpointMissing = false;
            string modelsFallbackDetail = string.Empty;

            if (!modelsProbe.IsHttpSuccess)
            {
                if (config.Provider != AIProvider.OpenAI && !ApiUsabilityDiagnosticService.IsModelsEndpointMissingStatusCode(modelsProbe.HttpCode))
                {
                    onCompleted?.Invoke(ApiUsabilityDiagnosticService.BuildFailureFromProbe(
                        ApiUsabilityStep.ModelsProbe,
                        modelsProbe,
                        runtime.ModelsEndpoint,
                        startedAtUtc,
                        steps,
                        modelName,
                        true,
                        null));
                    yield break;
                }

                modelsEndpointMissing = true;
                modelsFallbackDetail = config.Provider == AIProvider.OpenAI
                    ? $"models_probe_optional_http={modelsProbe.HttpCode}; direct_model_probe=true"
                    : ApiUsabilityDiagnosticService.BuildMissingModelsFallbackDetail(modelsProbe.HttpCode);
                Log.Warning($"[RimAI.Relations] Models probe unavailable (HTTP {modelsProbe.HttpCode}); selected model will be tested directly. endpoint={runtime.ModelsEndpoint}");
            }

            steps.Add(ApiUsabilityDiagnosticService.BuildStepSuccess(ApiUsabilityStep.ModelsProbe, runtime.ModelsEndpoint, startedAtUtc));
            ApiUsabilityDiagnosticService.NotifyProgress(onProgress, ApiUsabilityStep.ModelAvailability, 4, totalSteps);
            if (!modelsEndpointMissing)
            {
                List<string> cloudModels = ApiUsabilityDiagnosticService.ParseCloudModels(modelsProbe.ResponseBody, config.Provider);
                if (!ApiUsabilityDiagnosticService.ContainsModel(cloudModels, modelName))
                {
                    string detail = ApiUsabilityDiagnosticService.BuildMissingModelDetail(modelName, cloudModels);
                    onCompleted?.Invoke(ApiUsabilityDiagnosticService.BuildFailure(
                        ApiUsabilityStep.ModelAvailability,
                        ApiUsabilityErrorCode.MODEL_NOT_FOUND,
                        detail,
                        modelsProbe.HttpCode,
                        runtime.ModelsEndpoint,
                        startedAtUtc,
                        steps,
                        modelName,
                        true,
                        null,
                        modelsProbe.ResponseBody));
                    yield break;
                }
            }

            steps.Add(ApiUsabilityDiagnosticService.BuildStepSuccess(ApiUsabilityStep.ModelAvailability, runtime.ModelsEndpoint, startedAtUtc));
            ApiUsabilityDiagnosticService.NotifyProgress(onProgress, ApiUsabilityStep.ChatProbe, 5, totalSteps);
            string chatPayload = ApiUsabilityDiagnosticService.BuildCloudProbePayload(config.Provider, modelName);
            ApiUsabilityProbeResponse chatProbe = default;
            yield return ApiUsabilityDiagnosticService.SendProbeCoroutine(
                ApiUsabilityDiagnosticService.BuildProbeRequest(runtime.ChatEndpoint, "POST", chatPayload, config.Provider, config.GetRuntimeApiKey(), CloudTimeoutSeconds),
                probe => chatProbe = probe);

            if (!chatProbe.IsHttpSuccess)
            {
                onCompleted?.Invoke(ApiUsabilityDiagnosticService.BuildFailureFromProbe(
                    ApiUsabilityStep.ChatProbe,
                    chatProbe,
                    runtime.ChatEndpoint,
                    startedAtUtc,
                    steps,
                    modelName,
                    true,
                    chatPayload));
                yield break;
            }

            steps.Add(ApiUsabilityDiagnosticService.BuildStepSuccess(ApiUsabilityStep.ChatProbe, runtime.ChatEndpoint, startedAtUtc));
            if (modelsEndpointMissing)
            {
                string responseModel = ApiUsabilityDiagnosticService.ExtractModelFromChatResponse(chatProbe.ResponseBody);
                if (!string.IsNullOrWhiteSpace(responseModel) && !ApiUsabilityDiagnosticService.ContainsModel(new List<string> { responseModel }, modelName))
                {
                    string detail = $"Model name mismatch: configured='{modelName}' but API returned='{responseModel}'. The /models endpoint is missing, and the chat response model name does not match.";
                    onCompleted?.Invoke(ApiUsabilityDiagnosticService.BuildFailure(
                        ApiUsabilityStep.ChatProbe,
                        ApiUsabilityErrorCode.MODEL_NOT_FOUND,
                        detail,
                        chatProbe.HttpCode,
                        runtime.ChatEndpoint,
                        startedAtUtc,
                        steps,
                        modelName,
                        true,
                        chatPayload,
                        chatProbe.ResponseBody));
                    yield break;
                }
            }

            ApiUsabilityDiagnosticService.NotifyProgress(onProgress, ApiUsabilityStep.ResponseContractValidation, 6, totalSteps);
            ContractValidationOutcome cloudContract = ApiUsabilityDiagnosticService.ValidateCloudContract(config.Provider, chatProbe.ResponseBody);
            ApiUsabilityProbeResponse cloudFinalProbe = chatProbe;
            bool cloudRetried = false;
            if (cloudContract.ShouldRetry)
            {
                cloudRetried = true;
                ApiUsabilityProbeResponse retryProbe = default;
                yield return ApiUsabilityDiagnosticService.SendProbeCoroutine(
                    ApiUsabilityDiagnosticService.BuildProbeRequest(runtime.ChatEndpoint, "POST", chatPayload, config.Provider, config.GetRuntimeApiKey(), CloudTimeoutSeconds),
                    probe => retryProbe = probe);

                if (!retryProbe.IsHttpSuccess)
                {
                    onCompleted?.Invoke(ApiUsabilityDiagnosticService.BuildFailureFromProbe(
                        ApiUsabilityStep.ResponseContractValidation,
                        retryProbe,
                        runtime.ChatEndpoint,
                        startedAtUtc,
                        steps,
                        modelName,
                        true,
                        chatPayload));
                    yield break;
                }

                cloudFinalProbe = retryProbe;
                cloudContract = ApiUsabilityDiagnosticService.ValidateCloudContract(config.Provider, retryProbe.ResponseBody);
            }

            if (!cloudContract.IsAccepted)
            {
                onCompleted?.Invoke(ApiUsabilityDiagnosticService.BuildFailure(
                    ApiUsabilityStep.ResponseContractValidation,
                    ApiUsabilityErrorCode.RESPONSE_SCHEMA_INVALID,
                    cloudContract.Detail,
                    cloudFinalProbe.HttpCode,
                    runtime.ChatEndpoint,
                    startedAtUtc,
                    steps,
                    modelName,
                    true,
                    chatPayload,
                    cloudFinalProbe.ResponseBody));
                yield break;
            }

            steps.Add(ApiUsabilityDiagnosticService.BuildStepSuccess(ApiUsabilityStep.ResponseContractValidation, runtime.ChatEndpoint, startedAtUtc));
            string cloudSuccessDetail = ApiUsabilityDiagnosticService.BuildContractValidationSuccessDetail(cloudContract, cloudRetried);
            if (modelsEndpointMissing)
            {
                cloudSuccessDetail = ApiUsabilityDiagnosticService.AppendDiagnosticDetail(cloudSuccessDetail, modelsFallbackDetail);
            }
            onCompleted?.Invoke(ApiUsabilityDiagnosticService.BuildSuccess(
                ApiUsabilityStep.ResponseContractValidation,
                cloudFinalProbe.HttpCode,
                runtime.ChatEndpoint,
                startedAtUtc,
                steps,
                modelName,
                true,
                chatPayload,
                cloudFinalProbe.ResponseBody,
                cloudSuccessDetail));
        }

internal static IEnumerator RunPlayer2CloudDiagnosticCoroutine(
            ApiConfig config,
            Action<ApiUsabilityProgress> onProgress,
            Action<ApiUsabilityDiagnosticResult> onCompleted,
            DateTime startedAtUtc,
            List<ApiUsabilityStepResult> steps)
        {
            const int totalSteps = 3;
            string modelName = "Default";

            // Step 1: Config validation
            ApiUsabilityDiagnosticService.NotifyProgress(onProgress, ApiUsabilityStep.ConfigValidation, 1, totalSteps);
            if (config == null || !config.IsEnabled)
            {
                onCompleted?.Invoke(ApiUsabilityDiagnosticService.BuildFailure(
                    ApiUsabilityStep.ConfigValidation,
                    ApiUsabilityErrorCode.UNKNOWN,
                    "Player2 config is null or disabled.",
                    0, string.Empty, startedAtUtc, steps, modelName, true, null, string.Empty));
                yield break;
            }
            if (string.IsNullOrWhiteSpace(config.GetRuntimeApiKey()))
            {
                onCompleted?.Invoke(ApiUsabilityDiagnosticService.BuildFailure(
                    ApiUsabilityStep.ConfigValidation,
                    ApiUsabilityErrorCode.AUTH_INVALID,
                    "RimChat_EnterApiKey".Translate(),
                    0, string.Empty, startedAtUtc, steps, modelName, true, null, string.Empty));
                yield break;
            }
            steps.Add(ApiUsabilityDiagnosticService.BuildStepSuccess(ApiUsabilityStep.ConfigValidation, string.Empty, startedAtUtc));

            // Step 2: Chat probe
            ApiUsabilityDiagnosticService.NotifyProgress(onProgress, ApiUsabilityStep.ChatProbe, 2, totalSteps);
            string chatEndpoint = config.GetEffectiveEndpoint();
            string chatPayload = ApiUsabilityDiagnosticService.BuildPlayer2ChatPayload();
            ApiUsabilityProbeResponse chatProbe = default;
            yield return ApiUsabilityDiagnosticService.SendProbeCoroutine(
                ApiUsabilityDiagnosticService.BuildProbeRequest(chatEndpoint, "POST", chatPayload, config.Provider, config.ApiKey ?? string.Empty, CloudTimeoutSeconds),
                probe => chatProbe = probe);

            if (!chatProbe.IsHttpSuccess)
            {
                onCompleted?.Invoke(ApiUsabilityDiagnosticService.BuildFailureFromProbe(
                    ApiUsabilityStep.ChatProbe, chatProbe, chatEndpoint,
                    startedAtUtc, steps, modelName, true, chatPayload));
                yield break;
            }
            steps.Add(ApiUsabilityDiagnosticService.BuildStepSuccess(ApiUsabilityStep.ChatProbe, chatEndpoint, startedAtUtc));

            // Step 3: Response contract validation
            ApiUsabilityDiagnosticService.NotifyProgress(onProgress, ApiUsabilityStep.ResponseContractValidation, 3, totalSteps);
            ContractValidationOutcome contract = ApiUsabilityDiagnosticService.ValidateOpenAiChatContract(chatProbe.ResponseBody);
            ApiUsabilityProbeResponse finalProbe = chatProbe;
            bool retried = false;
            if (contract.ShouldRetry)
            {
                retried = true;
                ApiUsabilityProbeResponse retryProbe = default;
                yield return ApiUsabilityDiagnosticService.SendProbeCoroutine(
                    ApiUsabilityDiagnosticService.BuildProbeRequest(chatEndpoint, "POST", chatPayload, config.Provider, config.ApiKey ?? string.Empty, CloudTimeoutSeconds),
                    probe => retryProbe = probe);

                if (!retryProbe.IsHttpSuccess)
                {
                    onCompleted?.Invoke(ApiUsabilityDiagnosticService.BuildFailureFromProbe(
                        ApiUsabilityStep.ResponseContractValidation, retryProbe, chatEndpoint,
                        startedAtUtc, steps, modelName, true, chatPayload));
                    yield break;
                }
                finalProbe = retryProbe;
                contract = ApiUsabilityDiagnosticService.ValidateOpenAiChatContract(retryProbe.ResponseBody);
            }

            if (!contract.IsAccepted)
            {
                onCompleted?.Invoke(ApiUsabilityDiagnosticService.BuildFailure(
                    ApiUsabilityStep.ResponseContractValidation,
                    ApiUsabilityErrorCode.RESPONSE_SCHEMA_INVALID,
                    contract.Detail,
                    finalProbe.HttpCode, chatEndpoint, startedAtUtc, steps, modelName, true, chatPayload, finalProbe.ResponseBody));
                yield break;
            }

            steps.Add(ApiUsabilityDiagnosticService.BuildStepSuccess(ApiUsabilityStep.ResponseContractValidation, chatEndpoint, startedAtUtc));
            string successDetail = ApiUsabilityDiagnosticService.BuildContractValidationSuccessDetail(contract, retried);
            onCompleted?.Invoke(ApiUsabilityDiagnosticService.BuildSuccess(
                ApiUsabilityStep.ResponseContractValidation,
                finalProbe.HttpCode, chatEndpoint, startedAtUtc, steps, modelName, true, chatPayload, finalProbe.ResponseBody, successDetail));
        }

internal static IEnumerator RunPlayer2LocalDiagnosticCoroutine(
            LocalModelConfig config,
            Action<ApiUsabilityProgress> onProgress,
            Action<ApiUsabilityDiagnosticResult> onCompleted,
            DateTime startedAtUtc,
            List<ApiUsabilityStepResult> steps)
        {
            const int totalSteps = 3;
            string modelName = "Default";
            string normalizedBaseUrl = ApiConfig.NormalizeUrl(config.GetNormalizedBaseUrl());

            // Step 1: Config validation
            ApiUsabilityDiagnosticService.NotifyProgress(onProgress, ApiUsabilityStep.ConfigValidation, 1, totalSteps);
            if (string.IsNullOrWhiteSpace(normalizedBaseUrl))
            {
                onCompleted?.Invoke(ApiUsabilityDiagnosticService.BuildFailure(
                    ApiUsabilityStep.ConfigValidation,
                    ApiUsabilityErrorCode.UNKNOWN,
                    "Player2 local config requires a base URL.",
                    0, string.Empty, startedAtUtc, steps, modelName, false, null, string.Empty));
                yield break;
            }
            steps.Add(ApiUsabilityDiagnosticService.BuildStepSuccess(ApiUsabilityStep.ConfigValidation, normalizedBaseUrl, startedAtUtc));

            // Step 2: Chat probe
            ApiUsabilityDiagnosticService.NotifyProgress(onProgress, ApiUsabilityStep.ChatProbe, 2, totalSteps);
            string chatEndpoint = normalizedBaseUrl.TrimEnd('/') + "/v1/chat/completions";
            string chatPayload = ApiUsabilityDiagnosticService.BuildPlayer2ChatPayload();
            ApiUsabilityProbeResponse chatProbe = default;
            yield return ApiUsabilityDiagnosticService.SendProbeCoroutine(
                ApiUsabilityDiagnosticService.BuildProbeRequest(chatEndpoint, "POST", chatPayload, AIProvider.Player2, string.Empty, CloudTimeoutSeconds),
                probe => chatProbe = probe);

            if (!chatProbe.IsHttpSuccess)
            {
                onCompleted?.Invoke(ApiUsabilityDiagnosticService.BuildFailureFromProbe(
                    ApiUsabilityStep.ChatProbe, chatProbe, chatEndpoint,
                    startedAtUtc, steps, modelName, false, chatPayload));
                yield break;
            }
            steps.Add(ApiUsabilityDiagnosticService.BuildStepSuccess(ApiUsabilityStep.ChatProbe, chatEndpoint, startedAtUtc));

            // Step 3: Response contract validation
            ApiUsabilityDiagnosticService.NotifyProgress(onProgress, ApiUsabilityStep.ResponseContractValidation, 3, totalSteps);
            ContractValidationOutcome contract = ApiUsabilityDiagnosticService.ValidateOpenAiChatContract(chatProbe.ResponseBody);
            ApiUsabilityProbeResponse finalProbe = chatProbe;
            bool retried = false;
            if (contract.ShouldRetry)
            {
                retried = true;
                ApiUsabilityProbeResponse retryProbe = default;
                yield return ApiUsabilityDiagnosticService.SendProbeCoroutine(
                    ApiUsabilityDiagnosticService.BuildProbeRequest(chatEndpoint, "POST", chatPayload, AIProvider.Player2, string.Empty, CloudTimeoutSeconds),
                    probe => retryProbe = probe);

                if (!retryProbe.IsHttpSuccess)
                {
                    onCompleted?.Invoke(ApiUsabilityDiagnosticService.BuildFailureFromProbe(
                        ApiUsabilityStep.ResponseContractValidation, retryProbe, chatEndpoint,
                        startedAtUtc, steps, modelName, false, chatPayload));
                    yield break;
                }
                finalProbe = retryProbe;
                contract = ApiUsabilityDiagnosticService.ValidateOpenAiChatContract(retryProbe.ResponseBody);
            }

            if (!contract.IsAccepted)
            {
                onCompleted?.Invoke(ApiUsabilityDiagnosticService.BuildFailure(
                    ApiUsabilityStep.ResponseContractValidation,
                    ApiUsabilityErrorCode.RESPONSE_SCHEMA_INVALID,
                    contract.Detail,
                    finalProbe.HttpCode, chatEndpoint, startedAtUtc, steps, modelName, false, chatPayload, finalProbe.ResponseBody));
                yield break;
            }

            steps.Add(ApiUsabilityDiagnosticService.BuildStepSuccess(ApiUsabilityStep.ResponseContractValidation, chatEndpoint, startedAtUtc));
            string successDetail = ApiUsabilityDiagnosticService.BuildContractValidationSuccessDetail(contract, retried);
            onCompleted?.Invoke(ApiUsabilityDiagnosticService.BuildSuccess(
                ApiUsabilityStep.ResponseContractValidation,
                finalProbe.HttpCode, chatEndpoint, startedAtUtc, steps, modelName, false, chatPayload, finalProbe.ResponseBody, successDetail));
        }
    }
}
