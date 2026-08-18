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

namespace Ustas.RimAI.Communication.Relations.Config
{
    /// <summary>
    /// Dependencies: ApiConfig/LocalModelConfig, UnityWebRequest, and AI debug telemetry.
    /// Responsibility: execute fail-fast API usability diagnostics for cloud/local chat providers.
    /// </summary>
    internal static class ApiUsabilityDiagnosticService
    {
        internal const int CloudTimeoutSeconds = 30;
        internal const int LocalTimeoutSeconds = 8;
        internal const string LocalOllamaProbePath = "/api/tags";
        internal const string LocalOpenAiModelsPath = "/v1/models";
        internal const string LocalOpenAiChatPath = "/v1/chat/completions";
        internal const string LocalOllamaGeneratePath = "/api/generate";

        

        /// <summary>
        /// Simplified diagnostic for Player2: no models endpoint, model selected server-side.
        /// Steps: ConfigValidation → ChatProbe → ResponseContractValidation
        /// </summary>
        

        /// <summary>
        /// Simplified diagnostic for Player2 local app: no model listing, no Ollama probe.
        /// Steps: ConfigValidation → ChatProbe → ResponseContractValidation
        /// </summary>
        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        internal static bool IsModelsEndpointMissingStatusCode(long httpCode)
        {
            return httpCode == 404 || httpCode == 405 || httpCode == 501;
        }

        

        

        

        

        /// <summary>
        /// Player2 does not accept a model field; model is selected server-side.
        /// </summary>
        

        

        

        

        

        

        

        

        

        internal struct ContractValidationOutcome
        {
            public bool IsAccepted;
            public bool ShouldRetry;
            public bool IsWarning;
            public string Detail;

            public static ContractValidationOutcome Pass()
            {
                return new ContractValidationOutcome
                {
                    IsAccepted = true,
                    ShouldRetry = false,
                    IsWarning = false,
                    Detail = string.Empty
                };
            }

            public static ContractValidationOutcome Warning(string detail)
            {
                return new ContractValidationOutcome
                {
                    IsAccepted = true,
                    ShouldRetry = false,
                    IsWarning = true,
                    Detail = detail ?? string.Empty
                };
            }

            public static ContractValidationOutcome RetryableFail(string detail)
            {
                return new ContractValidationOutcome
                {
                    IsAccepted = false,
                    ShouldRetry = true,
                    IsWarning = false,
                    Detail = detail ?? string.Empty
                };
            }

            public static ContractValidationOutcome Fail(string detail)
            {
                return new ContractValidationOutcome
                {
                    IsAccepted = false,
                    ShouldRetry = false,
                    IsWarning = false,
                    Detail = detail ?? string.Empty
                };
            }
        }

    
        #region Cluster forwards
        internal static IEnumerator RunCloudDiagnosticCoroutine(ApiConfig config, Action<ApiUsabilityProgress> onProgress, Action<ApiUsabilityDiagnosticResult> onCompleted) => ApiUsabilitySlice1.RunCloudDiagnosticCoroutine(config, onProgress, onCompleted);
        internal static IEnumerator RunPlayer2CloudDiagnosticCoroutine(ApiConfig config, Action<ApiUsabilityProgress> onProgress, Action<ApiUsabilityDiagnosticResult> onCompleted, DateTime startedAtUtc, List<ApiUsabilityStepResult> steps) => ApiUsabilitySlice1.RunPlayer2CloudDiagnosticCoroutine(config, onProgress, onCompleted, startedAtUtc, steps);
        internal static IEnumerator RunPlayer2LocalDiagnosticCoroutine(LocalModelConfig config, Action<ApiUsabilityProgress> onProgress, Action<ApiUsabilityDiagnosticResult> onCompleted, DateTime startedAtUtc, List<ApiUsabilityStepResult> steps) => ApiUsabilitySlice1.RunPlayer2LocalDiagnosticCoroutine(config, onProgress, onCompleted, startedAtUtc, steps);
        internal static IEnumerator RunLocalDiagnosticCoroutine(LocalModelConfig config, Action<ApiUsabilityProgress> onProgress, Action<ApiUsabilityDiagnosticResult> onCompleted) => ApiUsabilitySlice2.RunLocalDiagnosticCoroutine(config, onProgress, onCompleted);
        internal static string GetStepLabelKey(ApiUsabilityStep step) => ApiUsabilitySlice2.GetStepLabelKey(step);
        internal static string GetErrorTitleKey(ApiUsabilityErrorCode code) => ApiUsabilitySlice2.GetErrorTitleKey(code);
        internal static ApiUsabilityDiagnosticResult ValidateCloudConfig(ApiConfig config, DateTime startedAtUtc, List<ApiUsabilityStepResult> steps, string modelName) => ApiUsabilitySlice2.ValidateCloudConfig(config, startedAtUtc, steps, modelName);
        internal static ApiUsabilityDiagnosticResult ValidateLocalConfig(LocalModelConfig config, DateTime startedAtUtc, List<ApiUsabilityStepResult> steps) => ApiUsabilitySlice2.ValidateLocalConfig(config, startedAtUtc, steps);
        internal static ApiUsabilityCloudRuntime ResolveCloudRuntime(ApiConfig config) => ApiUsabilitySlice2.ResolveCloudRuntime(config);
        internal static string ResolveCloudModelsEndpoint(ApiConfig config) => ApiUsabilitySlice2.ResolveCloudModelsEndpoint(config);
        internal static string ResolveCloudChatEndpoint(ApiConfig config) => ApiUsabilitySlice2.ResolveCloudChatEndpoint(config);
        internal static IEnumerator ProbeLocalServiceCoroutine(string normalizedBaseUrl, Action<ApiUsabilityLocalServiceProbe> onCompleted) => ApiUsabilitySlice2.ProbeLocalServiceCoroutine(normalizedBaseUrl, onCompleted);
        internal static ApiUsabilityProbeRequest BuildProbeRequest(string url, string method, string body, AIProvider provider, string apiKey, int timeoutSeconds) => ApiUsabilitySlice2.BuildProbeRequest(url, method, body, provider, apiKey, timeoutSeconds);
        internal static IEnumerator SendProbeCoroutine(ApiUsabilityProbeRequest request, Action<ApiUsabilityProbeResponse> onCompleted) => ApiUsabilitySlice3.SendProbeCoroutine(request, onCompleted);
        internal static void ApplyProbeAuthHeader(UnityWebRequest request, AIProvider provider, string apiKey, string requestUrl) => ApiUsabilitySlice3.ApplyProbeAuthHeader(request, provider, apiKey, requestUrl);
        internal static bool IsGoogleOpenAiCompatibleUrl(string requestUrl) => ApiUsabilitySlice3.IsGoogleOpenAiCompatibleUrl(requestUrl);
        internal static List<string> ParseCloudModels(string responseBody, AIProvider provider) => ApiUsabilitySlice3.ParseCloudModels(responseBody, provider);
        internal static List<string> ParseLocalModels(ApiUsabilityLocalServiceType serviceType, string responseBody) => ApiUsabilitySlice3.ParseLocalModels(serviceType, responseBody);
        internal static bool ContainsModel(List<string> models, string targetModel) => ApiUsabilitySlice3.ContainsModel(models, targetModel);
        internal static string BuildMissingModelsFallbackDetail(long httpCode) => ApiUsabilitySlice3.BuildMissingModelsFallbackDetail(httpCode);
        internal static string BuildOpenAiChatPayload(string modelName) => ApiUsabilitySlice3.BuildOpenAiChatPayload(modelName);
        internal static string BuildCloudProbePayload(AIProvider provider, string modelName) => ApiUsabilitySlice3.BuildCloudProbePayload(provider, modelName);
        internal static ContractValidationOutcome ValidateCloudContract(AIProvider provider, string responseBody) => ApiUsabilitySlice3.ValidateCloudContract(provider, responseBody);
        internal static string BuildPlayer2ChatPayload() => ApiUsabilitySlice3.BuildPlayer2ChatPayload();
        internal static string BuildLocalChatEndpoint(string baseUrl, ApiUsabilityLocalServiceType serviceType) => ApiUsabilitySlice3.BuildLocalChatEndpoint(baseUrl, serviceType);
        internal static string BuildLocalChatPayload(string modelName, ApiUsabilityLocalServiceType serviceType) => ApiUsabilitySlice3.BuildLocalChatPayload(modelName, serviceType);
        internal static ContractValidationOutcome ValidateOpenAiChatContract(string responseBody) => ApiUsabilitySlice3.ValidateOpenAiChatContract(responseBody);
        internal static string ExtractModelFromChatResponse(string responseBody) => ApiUsabilitySlice3.ExtractModelFromChatResponse(responseBody);
        internal static ContractValidationOutcome ValidateLocalChatContract(ApiUsabilityLocalServiceType serviceType, string responseBody) => ApiUsabilitySlice3.ValidateLocalChatContract(serviceType, responseBody);
        internal static string BuildContractValidationSuccessDetail(ContractValidationOutcome outcome, bool retried) => ApiUsabilitySlice3.BuildContractValidationSuccessDetail(outcome, retried);
        internal static string AppendDiagnosticDetail(string primary, string extra) => ApiUsabilitySlice3.AppendDiagnosticDetail(primary, extra);
        internal static bool ContainsJsonField(string responseBody, string fieldToken) => ApiUsabilitySlice3.ContainsJsonField(responseBody, fieldToken);
        #endregion

        #region Facade forwards
        internal static ApiUsabilityDiagnosticResult BuildSuccess(ApiUsabilityStep step, long httpCode, string endpoint, DateTime startedAtUtc, List<ApiUsabilityStepResult> steps, string modelName, bool isCloud, string requestPayload, string responsePayload, string successDetail = "") => ApiUsabilityDiagnosticServiceHelpers.BuildSuccess(step, httpCode, endpoint, startedAtUtc, steps, modelName, isCloud, requestPayload, responsePayload, successDetail);
        internal static ApiUsabilityDiagnosticResult BuildFailureFromProbe(ApiUsabilityStep step, ApiUsabilityProbeResponse probe, string endpoint, DateTime startedAtUtc, List<ApiUsabilityStepResult> steps, string modelName, bool isCloud, string requestPayload, ApiUsabilityErrorCode? forceCode = null) => ApiUsabilityDiagnosticServiceHelpers.BuildFailureFromProbe(step, probe, endpoint, startedAtUtc, steps, modelName, isCloud, requestPayload, forceCode);
        internal static ApiUsabilityDiagnosticResult BuildFailure(ApiUsabilityStep step, ApiUsabilityErrorCode code, string details, long httpCode, string endpoint, DateTime startedAtUtc, List<ApiUsabilityStepResult> steps, string modelName, bool isCloud, string requestPayload, string responsePayload) => ApiUsabilityDiagnosticServiceHelpers.BuildFailure(step, code, details, httpCode, endpoint, startedAtUtc, steps, modelName, isCloud, requestPayload, responsePayload);
        internal static ApiUsabilityStepResult BuildStepSuccess(ApiUsabilityStep step, string endpoint, DateTime startedAtUtc) => ApiUsabilityDiagnosticServiceHelpers.BuildStepSuccess(step, endpoint, startedAtUtc);
        internal static void NotifyProgress(Action<ApiUsabilityProgress> onProgress, ApiUsabilityStep step, int current, int total) => ApiUsabilityDiagnosticServiceHelpers.NotifyProgress(onProgress, step, current, total);
        internal static ApiUsabilityErrorCode ClassifyErrorCode(long httpCode, string error, bool isCloud) => ApiUsabilityDiagnosticServiceHelpers.ClassifyErrorCode(httpCode, error, isCloud);
        internal static List<string> GetHintKeys(ApiUsabilityErrorCode code) => ApiUsabilityDiagnosticServiceHelpers.GetHintKeys(code);
        internal static string BuildMissingModelDetail(string targetModel, List<string> discoveredModels) => ApiUsabilityDiagnosticServiceHelpers.BuildMissingModelDetail(targetModel, discoveredModels);
        internal static string BuildTechDetail(ApiUsabilityStep step, string endpoint, long httpCode, string details) => ApiUsabilityDiagnosticServiceHelpers.BuildTechDetail(step, endpoint, httpCode, details);
        internal static long GetElapsedMilliseconds(DateTime startedAtUtc) => ApiUsabilityDiagnosticServiceHelpers.GetElapsedMilliseconds(startedAtUtc);
        internal static string BuildModelTagPrefix(string model) => ApiUsabilityDiagnosticServiceHelpers.BuildModelTagPrefix(model);
        internal static string NormalizeGoogleModelName(string modelName) => ApiUsabilityDiagnosticServiceHelpers.NormalizeGoogleModelName(modelName);
        internal static string JoinUrl(string baseUrl, string path) => ApiUsabilityDiagnosticServiceHelpers.JoinUrl(baseUrl, path);
        internal static List<string> ExtractQuotedValues(string json, string fieldToken) => ApiUsabilityDiagnosticServiceHelpers.ExtractQuotedValues(json, fieldToken);
        internal static int FindClosingQuote(string text, int startIndex) => ApiUsabilityDiagnosticServiceHelpers.FindClosingQuote(text, startIndex);
        internal static string EscapeJsonString(string value) => ApiUsabilityDiagnosticServiceHelpers.EscapeJsonString(value);
        internal static string TruncateDebugPayload(string payload) => ApiUsabilityDiagnosticServiceHelpers.TruncateDebugPayload(payload);
        #endregion
}


    internal enum ApiUsabilityStep
    {
        ConfigValidation = 0,
        RuntimeEndpointResolution = 1,
        ModelsProbe = 2,
        ModelAvailability = 3,
        LocalServiceProbe = 4,
        ChatProbe = 5,
        ResponseContractValidation = 6
    }

    internal enum ApiUsabilityErrorCode
    {
        NONE = 0,
        AUTH_INVALID = 1,
        ENDPOINT_NOT_FOUND = 2,
        MODEL_NOT_FOUND = 3,
        TIMEOUT = 4,
        RATE_LIMIT = 5,
        TLS_OR_CERT = 6,
        DNS_OR_NETWORK = 7,
        RESPONSE_SCHEMA_INVALID = 8,
        LOCAL_SERVICE_DOWN = 9,
        UNKNOWN = 10
    }

    internal sealed class ApiUsabilityProgress
    {
        public ApiUsabilityStep Step { get; set; }
        public int Current { get; set; }
        public int Total { get; set; }
    }

    internal sealed class ApiUsabilityStepResult
    {
        public ApiUsabilityStep Step { get; set; }
        public bool Success { get; set; }
        public ApiUsabilityErrorCode ErrorCode { get; set; }
        public string TechDetail { get; set; }
        public long HttpCode { get; set; }
        public string EndpointUsed { get; set; }
        public long ElapsedMs { get; set; }
    }

    internal sealed class ApiUsabilityDiagnosticResult
    {
        public bool IsSuccess { get; set; }
        public ApiUsabilityStep Step { get; set; }
        public ApiUsabilityErrorCode ErrorCode { get; set; }
        public string TechDetail { get; set; }
        public long HttpCode { get; set; }
        public string EndpointUsed { get; set; }
        public long ElapsedMs { get; set; }
        public List<ApiUsabilityStepResult> Steps { get; set; } = new List<ApiUsabilityStepResult>();
        public List<string> PlayerHintKeys { get; set; } = new List<string>();
        public List<ApiDiagnosticSuggestion> Suggestions { get; set; } = new List<ApiDiagnosticSuggestion>();
        public string DebugRequestText { get; set; }
        public string DebugResponseText { get; set; }
        public string ModelName { get; set; }
        public bool IsCloud { get; set; }
        public AIProvider Provider { get; set; } = AIProvider.None;
    }

}
