using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Diagnostics;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Core.AI;
using Ustas.RimAI.Core.Net;
using Ustas.RimAI.Core.Threading;

namespace Ustas.RimAI.Communication.Relations.AI
{
    /// <summary>
    /// Relations text-AI facade. Owns Unity coroutine hosting and public API;
    /// request building, extraction, domain validation, semantic retry, and
    /// in-flight state live in dedicated collaborators.
    /// Remains a MonoBehaviour because StartCoroutine/WaitForSeconds and
    /// DontDestroyOnLoad still require a Unity host. Domain semantics are not
    /// in the Stable Host.
    /// </summary>
    public class AIChatServiceAsync : MonoBehaviour
    {
        internal AIChatServiceAsyncParts Parts;

        internal static AIChatServiceAsync _instance;
        internal static readonly object _instanceLock = new object();
        public static AIChatServiceAsync Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        if (_instance == null)
                        {
                            var go = new GameObject("AIChatServiceAsync");
                            _instance = go.AddComponent<AIChatServiceAsync>();
                            DontDestroyOnLoad(go);
                        }
                    }
                }
                return _instance;
            }
        }

        internal const int LocalRequestTimeoutSeconds = 60;
        internal const int CloudRequestTimeoutSeconds = 60;
        internal const float RequestCleanupIntervalSeconds = 10f;

        internal readonly RelationsAiRequestSession session = new RelationsAiRequestSession();
        internal readonly Queue<Action> mainThreadActions = new Queue<Action>();
        internal DialogueTokenUsageTracker usageTracker;
        internal RelationsAiDebugTelemetry telemetry;
        internal float nextCleanupAtRealtime;
        internal int contextVersion = 1;
        internal int lastObservedGameContextId = -1;

        internal object Gate => session.Gate;

        internal void Awake()
        {
            Parts = new AIChatServiceAsyncParts(this);
            usageTracker = new DialogueTokenUsageTracker(Gate);
            telemetry = new RelationsAiDebugTelemetry(Gate);
            lastObservedGameContextId = GetCurrentGameContextId();
            nextCleanupAtRealtime = Time.realtimeSinceStartup + RequestCleanupIntervalSeconds;
        }

        internal void Update()
        {
            DetectGameContextChange();
            ProcessMainThreadActions();

            if (Time.realtimeSinceStartup >= nextCleanupAtRealtime)
            {
                session.CleanupCompletedRequests();
                telemetry?.CleanupPending(DateTime.UtcNow);
                nextCleanupAtRealtime = Time.realtimeSinceStartup + RequestCleanupIntervalSeconds;
            }
        }

        

        

        

        

        public static void NotifyGameContextChanged(string reason)
        {
            _instance?.HandleGameContextChanged(reason);
        }

        

        public int CancelAllPendingRequests(string reason = "Request cancelled by context change")
        {
            return session.CancelAllPending(reason);
        }

        

        public void CleanupCompletedRequests()
        {
            session.CleanupCompletedRequests();
        }

        

        

        

        

        

        public bool IsConfigured()
        {
            return GetFirstValidConfig() != null;
        }

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        internal void EnsureCollaborators()
        {
            if (usageTracker == null)
            {
                usageTracker = new DialogueTokenUsageTracker(Gate);
            }

            if (telemetry == null)
            {
                telemetry = new RelationsAiDebugTelemetry(Gate);
            }
        }

        internal void OnDestroy()
        {
            session.Destroy();
            lock (Gate)
            {
                mainThreadActions.Clear();
            }
        }
    
        #region Cluster forwards
        internal System.Collections.IEnumerator ProcessRequestCoroutine(string requestId, List<ChatMessageData> messages, Action<string> onSuccess, Action<string> onError, Action<float> onProgress, DialogueUsageChannel usageChannel, AIRequestDebugSource debugSource, int requestContextVersion, int requestTimeoutSeconds) => Parts.RequestFlow.ProcessRequestCoroutine(requestId, messages, onSuccess, onError, onProgress, usageChannel, debugSource, requestContextVersion, requestTimeoutSeconds);
        internal bool TryCompleteFromAssistantText(string requestId, int requestContextVersion, DialogueUsageChannel usageChannel, AIRequestDebugSource debugSource, AIProvider provider, string responseText, ref List<ChatMessageData> attemptMessages, ref int parseRetryCount, ref int immersionRetryCount, ref int textIntegrityRetryCount, ref int contractRetryCount, ref string contractValidationStatus, ref string contractFailureReason, Action<string> onSuccess, Action<string> onError, out bool shouldRetry, out AIRequestDebugStatus debugStatus, out string debugResponseText, out string debugParsedResponse, out string debugErrorText, out List<ChatMessageData> debugTokenMessages) => Parts.RequestFlow.TryCompleteFromAssistantText(requestId, requestContextVersion, usageChannel, debugSource, provider, responseText, ref attemptMessages, ref parseRetryCount, ref immersionRetryCount, ref textIntegrityRetryCount, ref contractRetryCount, ref contractValidationStatus, ref contractFailureReason, onSuccess, onError, out shouldRetry, out debugStatus, out debugResponseText, out debugParsedResponse, out debugErrorText, out debugTokenMessages);
        internal void FailEarly(string requestId, int requestContextVersion, string error, Action<string> onError, ref AIRequestDebugStatus debugStatus, ref string debugErrorText, ref bool debugRecordFinalized, List<ChatMessageData> debugTokenMessages) => Parts.RequestFlow.FailEarly(requestId, requestContextVersion, error, onError, ref debugStatus, ref debugErrorText, ref debugRecordFinalized, debugTokenMessages);
        internal void FinalizeDropped(string requestId, ref AIRequestDebugStatus debugStatus, ref string debugErrorText, ref bool debugRecordFinalized, List<ChatMessageData> debugTokenMessages, string debugResponseText, string debugParsedResponse, long debugHttpCode) => Parts.RequestFlow.FinalizeDropped(requestId, ref debugStatus, ref debugErrorText, ref debugRecordFinalized, debugTokenMessages, debugResponseText, debugParsedResponse, debugHttpCode);
        internal static string FormatProtocolError(long responseCode, bool isLocalModel) => AIChatServiceAsyncRequestFlow.FormatProtocolError(responseCode, isLocalModel);
        internal static void LogFingerprint(string requestId, int attempt, DialogueUsageChannel usageChannel, string model, string url, int messageCount, int jsonBytes, long elapsedMs, long httpCode, string result) => AIChatServiceAsyncRequestFlow.LogFingerprint(requestId, attempt, usageChannel, model, url, messageCount, jsonBytes, elapsedMs, httpCode, result);
        internal static void LogLocalServerRetry(string requestId, int attempt, long responseCode, float retryDelaySeconds, string responseBody) => AIChatServiceAsyncRequestFlow.LogLocalServerRetry(requestId, attempt, responseCode, retryDelaySeconds, responseBody);
        internal static void LogLocalConnRetry(string requestId, int attempt, string requestError, float retryDelaySeconds) => AIChatServiceAsyncRequestFlow.LogLocalConnRetry(requestId, attempt, requestError, retryDelaySeconds);
        internal ApiConfig GetFirstValidConfig() => Parts.RequestFlow.GetFirstValidConfig();
        internal bool IsContextVersionCurrent(int expectedContextVersion) => Parts.ContextOps.IsContextVersionCurrent(expectedContextVersion);
        internal void ExecuteRequestActionOnMainThread(string requestId, int expectedContextVersion, Action action) => Parts.ContextOps.ExecuteRequestActionOnMainThread(requestId, expectedContextVersion, action);
        internal void ProcessMainThreadActions() => Parts.ContextOps.ProcessMainThreadActions();
        internal void DetectGameContextChange() => Parts.ContextOps.DetectGameContextChange();
        internal void HandleGameContextChanged(string reason) => Parts.ContextOps.HandleGameContextChanged(reason);
        internal static int GetCurrentGameContextId() => AIChatServiceAsyncContextOps.GetCurrentGameContextId();
        public string SendChatRequestAsync(List<ChatMessageData> messages, Action<string> onSuccess, Action<string> onError, Action<float> onProgress = null, DialogueUsageChannel usageChannel = DialogueUsageChannel.Unknown, AIRequestDebugSource debugSource = AIRequestDebugSource.Other, int? requestTimeoutSecondsOverride = null, float? queueTimeoutSecondsOverride = null) => Parts.Slice1.SendChatRequestAsync(messages, onSuccess, onError, onProgress, usageChannel, debugSource, requestTimeoutSecondsOverride, queueTimeoutSecondsOverride);
        public DialogueTokenUsageSnapshot GetLatestDialogueTokenUsage() => Parts.Slice1.GetLatestDialogueTokenUsage();
        public static bool TryGetLatestDialogueTokenUsage(out DialogueTokenUsageSnapshot snapshot) => AIChatServiceAsyncSlice1.TryGetLatestDialogueTokenUsage(out snapshot);
        public int GetCurrentContextVersionSnapshot() => Parts.Slice1.GetCurrentContextVersionSnapshot();
        public bool CancelRequest(string requestId, string cancelReason = "cancelled_by_user", string error = "Request cancelled by user") => Parts.Slice1.CancelRequest(requestId, cancelReason, error);
        public AIRequestResult GetRequestStatus(string requestId) => Parts.Slice1.GetRequestStatus(requestId);
        public AIRequestDebugSnapshot GetRequestDebugSnapshot() => Parts.Slice1.GetRequestDebugSnapshot();
        public static bool TryGetRequestDebugSnapshot(out AIRequestDebugSnapshot snapshot) => AIChatServiceAsyncSlice1.TryGetRequestDebugSnapshot(out snapshot);
        public static void RecordExternalDebugRecord(AIRequestDebugSource source, DialogueUsageChannel channel, string model, AIRequestDebugStatus status, long durationMs, long httpStatusCode, string requestText, string responseText, string errorText, DateTime? startedAtUtc = null) => AIChatServiceAsyncSlice1.RecordExternalDebugRecord(source, channel, model, status, durationMs, httpStatusCode, requestText, responseText, errorText, startedAtUtc);
        public static void RecordExternalDebugRecord(AIRequestDebugSource source, DialogueUsageChannel channel, string model, AIRequestDebugStatus status, long durationMs, long httpStatusCode, int promptTokens, int completionTokens, int totalTokens, bool isEstimatedTokens, string requestText, string responseText, string errorText, DateTime? startedAtUtc = null) => AIChatServiceAsyncSlice1.RecordExternalDebugRecord(source, channel, model, status, durationMs, httpStatusCode, promptTokens, completionTokens, totalTokens, isEstimatedTokens, requestText, responseText, errorText, startedAtUtc);
        public void ExecuteOnMainThread(Action action) => Parts.Slice1.ExecuteOnMainThread(action);
        #endregion
}
    internal sealed class AIChatServiceAsyncRequestFlow : AIChatServiceAsyncCollaborator
    {
        internal AIChatServiceAsyncRequestFlow(AIChatServiceAsync owner) : base(owner)
        {
        }

internal System.Collections.IEnumerator ProcessRequestCoroutine(
            string requestId,
            List<ChatMessageData> messages,
            Action<string> onSuccess,
            Action<string> onError,
            Action<float> onProgress,
            DialogueUsageChannel usageChannel,
            AIRequestDebugSource debugSource,
            int requestContextVersion,
            int requestTimeoutSeconds)
        {
            AIRequestDebugStatus debugStatus = AIRequestDebugStatus.Error;
            string debugResponseText = string.Empty;
            string debugParsedResponse = string.Empty;
            string debugErrorText = string.Empty;
            long debugHttpCode = 0;
            List<ChatMessageData> debugTokenMessages = messages;
            bool debugRecordFinalized = false;

            if (!Owner.IsContextVersionCurrent(requestContextVersion))
            {
                Owner.FinalizeDropped(requestId, ref debugStatus, ref debugErrorText, ref debugRecordFinalized, debugTokenMessages, debugResponseText, debugParsedResponse, debugHttpCode);
                yield break;
            }

            var config = Owner.GetFirstValidConfig();
            if (config == null)
            {
                Owner.FailEarly(requestId, requestContextVersion, "RimChat_ErrorNoConfig".Translate(), onError, ref debugStatus, ref debugErrorText, ref debugRecordFinalized, debugTokenMessages);
                yield break;
            }

            string url = config.GetEffectiveEndpoint();
            string apiKey = config.GetRuntimeApiKey();
            string model = config.GetEffectiveModelName();
            telemetry.SetRequestDebugModel(requestId, model);
            bool isLocalModel = RelationsMod.Instance == null ||
                !(RelationsMod.Instance.InstanceSettings?.UseCloudProviders ?? false);
            session.RecordTransportEnvelope(requestId, RelationsLocalProviderRetry.GetUrlHostPort(url));

            if (!RelationsTextAiRequestBuilder.ValidateUrl(url, out string urlError))
            {
                Owner.FailEarly(requestId, requestContextVersion, urlError, onError, ref debugStatus, ref debugErrorText, ref debugRecordFinalized, debugTokenMessages);
                yield break;
            }

            if (messages == null || messages.Count == 0)
            {
                Owner.FailEarly(requestId, requestContextVersion, "RimChat_ErrorEmptyMessage".Translate(), onError, ref debugStatus, ref debugErrorText, ref debugRecordFinalized, debugTokenMessages);
                yield break;
            }

            bool localSlotAcquired = false;
            if (isLocalModel)
            {
                session.EnqueueLocalRequest(requestId);
                while (!localSlotAcquired)
                {
                    if (!Owner.IsContextVersionCurrent(requestContextVersion))
                    {
                        Owner.FinalizeDropped(requestId, ref debugStatus, ref debugErrorText, ref debugRecordFinalized, debugTokenMessages, debugResponseText, debugParsedResponse, debugHttpCode);
                        yield break;
                    }

                    session.TryTimeoutQueuedRequest(requestId);
                    if (session.TryGetTerminalRequestDisposition(
                            requestId,
                            out AIRequestState waitingState,
                            out string waitingError,
                            out bool allowWaitingCallback))
                    {
                        if (allowWaitingCallback)
                        {
                            Owner.ExecuteRequestActionOnMainThread(requestId, requestContextVersion, () => onError?.Invoke(waitingError));
                        }

                        debugStatus = waitingState == AIRequestState.Cancelled
                            ? AIRequestDebugStatus.Cancelled
                            : RelationsAiDebugTelemetry.ClassifyDebugStatusFromError(waitingError);
                        debugErrorText = waitingError ?? string.Empty;
                        telemetry.FinalizeRequestDebugRecord(requestId, debugTokenMessages, debugResponseText, debugParsedResponse, debugStatus, debugHttpCode, debugErrorText);
                        debugRecordFinalized = true;
                        yield break;
                    }

                    localSlotAcquired = session.TryAcquireLocalRequestSlot(requestId);
                    if (!localSlotAcquired)
                    {
                        yield return new WaitForSeconds(0.05f);
                    }
                }
            }
            else
            {
                session.MarkProcessingStarted(requestId);
            }

            List<ChatMessageData> attemptMessages = RelationsTextAiRequestBuilder.Clone(messages);
            int attempt = 1;
            int local5xxRetryCount = 0;
            int localConnectionRetryCount = 0;
            int immersionRetryCount = 0;
            int textIntegrityRetryCount = 0;
            int contractRetryCount = 0;
            int parseRetryCount = 0;
            string contractValidationStatus = "not_applicable";
            string contractFailureReason = string.Empty;
            try
            {
                while (true)
                {
                    string jsonBody;
                    try
                    {
                        jsonBody = RelationsTextAiRequestBuilder.BuildChatCompletionJson(model, attemptMessages, config);
                    }
                    catch (Exception)
                    {
                        Owner.FailEarly(requestId, requestContextVersion, "RimChat_ErrorBuildRequest".Translate(), onError, ref debugStatus, ref debugErrorText, ref debugRecordFinalized, debugTokenMessages);
                        yield break;
                    }

                    telemetry.SetRequestDebugPayload(requestId, jsonBody);
                    session.RecordAttemptTelemetry(requestId, attempt, Encoding.UTF8.GetByteCount(jsonBody));

                    if (!isLocalModel && config.Provider == AIProvider.OpenAI)
                    {
                        TextAiResponse shared = null;
                        bool sharedDone = false;
                        var sharedRequest = new TextAiRequest
                        {
                            Messages = attemptMessages.Select(m => new TextAiMessage(m.role, m.content)).ToList(),
                            Model = model,
                            BaseUrl = url,
                            ApiShape = TextAiApiShape.Responses,
                            UseSharedGameplayCredential = true,
                            PrebuiltJson = jsonBody,
                            TimeoutMs = requestTimeoutSeconds * 1000,
                            Caller = "relations",
                            Arbitration = new AiRequestMetadata(
                                moduleId: "relations",
                                requestKind: debugSource.ToString(),
                                priority: RelationsAiRequestPriority.Resolve(debugSource) == AIRequestPriority.Interactive
                                    ? AiRequestPriority.PlayerBlocking
                                    : AiRequestPriority.Background,
                                caller: "relations")
                        };
                        ThreadPool.QueueUserWorkItem(_ =>
                        {
                            try
                            {
                                shared = SharedTextAiOrchestrator.Complete(sharedRequest);
                            }
                            finally
                            {
                                sharedDone = true;
                            }
                        });
                        while (!sharedDone)
                        {
                            yield return null;
                        }

                        if (shared != null && shared.Transient && RelationsLocalProviderRetry.ShouldRetryLocalServerError(false, shared.StatusCode, local5xxRetryCount))
                        {
                            local5xxRetryCount++;
                            yield return new WaitForSeconds(RelationsLocalProviderRetry.GetLocalServerRetryDelaySeconds(local5xxRetryCount, UnityEngine.Random.Range(0f, 0.2f)));
                            attempt++;
                            continue;
                        }

                        if (shared == null || !shared.Succeeded)
                        {
                            string errorMsg = shared?.Error ?? "RimChat_ErrorConnectionCloud".Translate();
                            if (shared != null && shared.StatusCode > 0 && config.Provider == AIProvider.OpenAI)
                            {
                                errorMsg = OpenAIProviderAdapter.ParseError(shared.StatusCode, shared.RawPayload).ToString();
                            }
                            session.SetFailure(requestId, errorMsg, shared?.ErrorKind ?? "shared_transport");
                            Owner.ExecuteRequestActionOnMainThread(requestId, requestContextVersion, () => onError?.Invoke(errorMsg));
                            debugStatus = AIRequestDebugStatus.Error;
                            debugHttpCode = shared?.StatusCode ?? 0;
                            debugResponseText = shared?.RawPayload ?? string.Empty;
                            debugErrorText = errorMsg ?? string.Empty;
                            yield break;
                        }

                        string sharedResponseText = string.IsNullOrEmpty(shared.RawPayload) ? shared.Text : shared.RawPayload;
                        if (Owner.TryCompleteFromAssistantText(
                            requestId,
                            requestContextVersion,
                            usageChannel,
                            debugSource,
                            config.Provider,
                            sharedResponseText,
                            ref attemptMessages,
                            ref parseRetryCount,
                            ref immersionRetryCount,
                            ref textIntegrityRetryCount,
                            ref contractRetryCount,
                            ref contractValidationStatus,
                            ref contractFailureReason,
                            onSuccess,
                            onError,
                            out bool shouldRetry,
                            out debugStatus,
                            out debugResponseText,
                            out debugParsedResponse,
                            out debugErrorText,
                            out debugTokenMessages))
                        {
                            if (shouldRetry)
                            {
                                attempt++;
                                continue;
                            }

                            debugHttpCode = shared.StatusCode;
                            yield break;
                        }

                        yield break;
                    }

                    var stopwatch = Stopwatch.StartNew();
                    CancellationTokenSource transportCts = new CancellationTokenSource();
                    session.RegisterActiveTransportCancellation(requestId, transportCts);
                    try
                    {
                        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        string trimmedApiKey = apiKey?.Trim() ?? string.Empty;
                        if (!isLocalModel || !string.IsNullOrEmpty(trimmedApiKey))
                        {
                            headers["Authorization"] = $"Bearer {trimmedApiKey}";
                        }

                        var extraHeaders = config.Provider.GetExtraHeaders();
                        if (extraHeaders != null)
                        {
                            foreach (var header in extraHeaders)
                            {
                                headers[header.Key] = header.Value;
                            }
                        }

                        Task<HttpTransportResponse> sendTask = SharedHttpTransport.Current.SendAsync(
                            new HttpTransportRequest
                            {
                                Method = "POST",
                                Url = url,
                                Headers = headers,
                                BodyBytes = bodyRaw,
                                ContentType = "application/json",
                                TimeoutMilliseconds = Math.Max(1, requestTimeoutSeconds) * 1000,
                                CorrelationId = requestId,
                                OnBytesReceived = bytes =>
                                {
                                    if (bytes > 0)
                                    {
                                        session.RecordFirstResponseByte(requestId);
                                    }
                                }
                            },
                            cancellationToken: transportCts.Token);
                        float progress = 0f;

                        while (!sendTask.IsCompleted)
                        {
                            progress = Mathf.Min(progress + 0.02f, 0.9f);
                            session.UpdateProgress(requestId, progress);
                            Owner.ExecuteRequestActionOnMainThread(requestId, requestContextVersion, () => onProgress?.Invoke(progress));
                            yield return new WaitForSeconds(0.1f);

                            if (!Owner.IsContextVersionCurrent(requestContextVersion))
                            {
                                transportCts.Cancel();
                                session.TryCancelRequest(requestId, "context_changed", "Request dropped due to game context change");
                                debugStatus = AIRequestDebugStatus.Cancelled;
                                debugErrorText = "Request dropped due to game context change";
                                yield break;
                            }

                            if (session.TryGetTerminalRequestDisposition(
                                    requestId,
                                    out AIRequestState activeState,
                                    out string activeMessage,
                                    out bool allowActiveCallback))
                            {
                                transportCts.Cancel();
                                if (allowActiveCallback)
                                {
                                    Owner.ExecuteRequestActionOnMainThread(requestId, requestContextVersion, () => onError?.Invoke(activeMessage));
                                }

                                debugStatus = activeState == AIRequestState.Cancelled
                                    ? AIRequestDebugStatus.Cancelled
                                    : RelationsAiDebugTelemetry.ClassifyDebugStatusFromError(activeMessage);
                                debugErrorText = activeMessage ?? string.Empty;
                                yield break;
                            }
                        }

                        HttpTransportResponse http;
                        try
                        {
                            http = sendTask.GetAwaiter().GetResult();
                        }
                        catch (Exception ex)
                        {
                            http = HttpTransportResponse.Fail(
                                HttpTransportErrorKind.NetworkFailure,
                                ex.Message,
                                SharedHttpTransport.Current.Kind,
                                correlationId: requestId);
                        }

                        stopwatch.Stop();
                        session.UpdateProgress(requestId, 1f);
                        Owner.ExecuteRequestActionOnMainThread(requestId, requestContextVersion, () => onProgress?.Invoke(1f));
                        AIChatServiceAsync.LogFingerprint(requestId, attempt, usageChannel, model, url, attemptMessages.Count, bodyRaw.Length, stopwatch.ElapsedMilliseconds, http.StatusCode, http.ErrorKind.ToString());
                        debugHttpCode = http.StatusCode;
                        session.RecordHttpStatus(requestId, http.StatusCode);
                        if (http.BytesReceived > 0)
                        {
                            session.RecordFirstResponseByte(requestId);
                        }

                        if (session.TryGetTerminalRequestDisposition(
                                requestId,
                                out AIRequestState completedState,
                                out string completedMessage,
                                out bool allowCompletedCallback))
                        {
                            if (allowCompletedCallback)
                            {
                                Owner.ExecuteRequestActionOnMainThread(requestId, requestContextVersion, () => onError?.Invoke(completedMessage));
                            }

                            debugStatus = completedState == AIRequestState.Cancelled
                                ? AIRequestDebugStatus.Cancelled
                                : RelationsAiDebugTelemetry.ClassifyDebugStatusFromError(completedMessage);
                            debugErrorText = completedMessage ?? string.Empty;
                            yield break;
                        }

                        if (http.ErrorKind == HttpTransportErrorKind.NetworkFailure || http.TimedOut)
                        {
                            if (RelationsLocalProviderRetry.ShouldRetryLocalConnectionError(isLocalModel, debugSource, http.ErrorMessage, localConnectionRetryCount))
                            {
                                localConnectionRetryCount++;
                                float retryDelaySeconds = RelationsLocalProviderRetry.GetLocalConnectionRetryDelaySeconds(
                                    localConnectionRetryCount,
                                    UnityEngine.Random.Range(0f, 0.25f));
                                AIChatServiceAsync.LogLocalConnRetry(requestId, attempt, http.ErrorMessage, retryDelaySeconds);
                                yield return new WaitForSeconds(retryDelaySeconds);
                                attempt++;
                                continue;
                            }

                            string errorMsg = isLocalModel
                                ? "RimChat_ErrorConnectionLocal".Translate()
                                : "RimChat_ErrorConnectionCloud".Translate();
                            if (RelationsLocalProviderRetry.LooksLikeTimeoutError(http.ErrorMessage))
                            {
                                errorMsg = "RimChat_ErrorTimeout".Translate();
                            }
                            session.SetFailure(requestId, errorMsg, RelationsLocalProviderRetry.LooksLikeTimeoutError(http.ErrorMessage) ? "timeout" : "connection_error");
                            Owner.ExecuteRequestActionOnMainThread(requestId, requestContextVersion, () => onError?.Invoke(errorMsg));
                            debugStatus = AIRequestDebugStatus.Error;
                            debugHttpCode = http.StatusCode;
                            debugResponseText = http.ErrorMessage ?? string.Empty;
                            debugErrorText = errorMsg ?? string.Empty;
                            yield break;
                        }

                        if (http.ErrorKind == HttpTransportErrorKind.HttpFailure)
                        {
                            string responseBody = http.BodyText ?? string.Empty;
                            if (RelationsLocalProviderRetry.ShouldRetryLocalServerError(isLocalModel, http.StatusCode, local5xxRetryCount))
                            {
                                local5xxRetryCount++;
                                float retryDelaySeconds = RelationsLocalProviderRetry.GetLocalServerRetryDelaySeconds(
                                    local5xxRetryCount,
                                    UnityEngine.Random.Range(0f, 0.2f));
                                AIChatServiceAsync.LogLocalServerRetry(requestId, attempt, http.StatusCode, retryDelaySeconds, responseBody);
                                yield return new WaitForSeconds(retryDelaySeconds);
                                attempt++;
                                continue;
                            }

                            DebugLogger.LogFullMessages(attemptMessages, $"HTTP {http.StatusCode} ERROR\n{responseBody}");
                            string errorMsg;
                            string failureTag = $"http_{http.StatusCode}";
                            if (config.Provider == AIProvider.OpenAI)
                            {
                                OpenAIError openAiError = OpenAIProviderAdapter.ParseError(http.StatusCode, responseBody);
                                errorMsg = openAiError.ToString();
                                failureTag = openAiError.Category.ToString().ToLowerInvariant();
                                DebugLogger.Error($"OpenAI request failed: {errorMsg}");
                            }
                            else
                            {
                                DebugLogger.Error($"AI API Error (HTTP {http.StatusCode}): {http.ErrorMessage}");
                                errorMsg = AIChatServiceAsync.FormatProtocolError(http.StatusCode, isLocalModel);
                            }

                            session.SetFailure(requestId, errorMsg, failureTag);
                            Owner.ExecuteRequestActionOnMainThread(requestId, requestContextVersion, () => onError?.Invoke(errorMsg));
                            debugStatus = AIRequestDebugStatus.Error;
                            debugHttpCode = http.StatusCode;
                            debugResponseText = responseBody;
                            debugErrorText = errorMsg ?? string.Empty;
                            yield break;
                        }

                        if (http.ErrorKind == HttpTransportErrorKind.InvalidTransportResponse)
                        {
                            string errorMsg = "RimChat_ErrorDataProcessing".Translate(http.ErrorMessage);
                            session.SetFailure(requestId, errorMsg, "data_processing_error");
                            Owner.ExecuteRequestActionOnMainThread(requestId, requestContextVersion, () => onError?.Invoke(errorMsg));
                            debugStatus = AIRequestDebugStatus.Error;
                            debugHttpCode = http.StatusCode;
                            debugResponseText = http.ErrorMessage ?? string.Empty;
                            debugErrorText = errorMsg ?? string.Empty;
                            yield break;
                        }

                        if (http.StatusCode == 200)
                        {
                            string responseText = http.BodyText;
                            if (string.IsNullOrEmpty(responseText))
                            {
                                string errorMsg = "RimChat_ErrorEmptyResponse".Translate();
                                session.SetFailure(requestId, errorMsg, "empty_response");
                                Owner.ExecuteRequestActionOnMainThread(requestId, requestContextVersion, () => onError?.Invoke(errorMsg));
                                debugStatus = AIRequestDebugStatus.Error;
                                debugResponseText = responseText ?? string.Empty;
                                debugErrorText = errorMsg ?? string.Empty;
                                yield break;
                            }

                            if (Owner.TryCompleteFromAssistantText(
                                requestId,
                                requestContextVersion,
                                usageChannel,
                                debugSource,
                                config.Provider,
                                responseText,
                                ref attemptMessages,
                                ref parseRetryCount,
                                ref immersionRetryCount,
                                ref textIntegrityRetryCount,
                                ref contractRetryCount,
                                ref contractValidationStatus,
                                ref contractFailureReason,
                                onSuccess,
                                onError,
                                out bool shouldRetry,
                                out debugStatus,
                                out debugResponseText,
                                out debugParsedResponse,
                                out debugErrorText,
                                out debugTokenMessages))
                            {
                                if (shouldRetry)
                                {
                                    attempt++;
                                    continue;
                                }

                                debugHttpCode = http.StatusCode;
                                yield break;
                            }

                            yield break;
                        }

                        string fallbackError = $"HTTP {http.StatusCode}: {http.ErrorMessage}";
                        session.SetFailure(requestId, fallbackError, "unexpected_http_error");
                        Owner.ExecuteRequestActionOnMainThread(requestId, requestContextVersion, () => onError?.Invoke(fallbackError));
                        debugStatus = AIRequestDebugStatus.Error;
                        debugHttpCode = http.StatusCode;
                        debugResponseText = http.BodyText ?? http.ErrorMessage ?? string.Empty;
                        debugErrorText = fallbackError;
                        yield break;
                    }
                    finally
                    {
                        session.UnregisterActiveTransportCancellation(requestId, transportCts);
                        transportCts.Dispose();
                    }
                }
            }
            finally
            {
                session.UnregisterActiveTransportCancellation(requestId);

                if (isLocalModel)
                {
                    if (localSlotAcquired)
                    {
                        session.ReleaseLocalRequestSlot(requestId);
                    }
                    else
                    {
                        session.RemoveLocalRequest(requestId);
                    }
                }

                if (!debugRecordFinalized)
                {
                    telemetry.FinalizeRequestDebugRecord(
                        requestId,
                        debugTokenMessages,
                        debugResponseText,
                        debugParsedResponse,
                        debugStatus,
                        debugHttpCode,
                        debugErrorText,
                        contractValidationStatus,
                        contractRetryCount,
                        contractFailureReason);
                }
            }
        }

internal bool TryCompleteFromAssistantText(
            string requestId,
            int requestContextVersion,
            DialogueUsageChannel usageChannel,
            AIRequestDebugSource debugSource,
            AIProvider provider,
            string responseText,
            ref List<ChatMessageData> attemptMessages,
            ref int parseRetryCount,
            ref int immersionRetryCount,
            ref int textIntegrityRetryCount,
            ref int contractRetryCount,
            ref string contractValidationStatus,
            ref string contractFailureReason,
            Action<string> onSuccess,
            Action<string> onError,
            out bool shouldRetry,
            out AIRequestDebugStatus debugStatus,
            out string debugResponseText,
            out string debugParsedResponse,
            out string debugErrorText,
            out List<ChatMessageData> debugTokenMessages)
        {
            shouldRetry = false;
            debugStatus = AIRequestDebugStatus.Error;
            debugResponseText = responseText ?? string.Empty;
            debugParsedResponse = string.Empty;
            debugErrorText = string.Empty;
            debugTokenMessages = attemptMessages;

            DebugLogger.LogFullMessages(attemptMessages, responseText);
            PrimaryTextExtractionResult parseResult =
                Runtime.RelationsRuntimeGateway.Policy.ExtractProviderText(responseText, provider);
            DebugLogger.LogParseExtraction("AIChatServiceAsync", parseResult);
            if (!parseResult.IsSuccess)
            {
                string retryReason = RelationsSemanticRetry.BuildParseRetryReason(responseText, parseResult.ReasonTag);
                if (RelationsSemanticRetry.ShouldRetryParseFailure(retryReason, parseRetryCount))
                {
                    parseRetryCount++;
                    attemptMessages = RelationsSemanticRetry.AppendParseRetryMessage(
                        attemptMessages,
                        usageChannel,
                        responseText,
                        retryReason,
                        parseResult.MatchedPath);
                    shouldRetry = true;
                    return true;
                }

                string errorMsg = "RimChat_ErrorParseResponse".Translate();
                string failureTag = string.IsNullOrWhiteSpace(parseResult.ReasonTag)
                    ? "parse_error"
                    : $"parse_error_{parseResult.ReasonTag}";

                // The localized sentence is all any dialogue surface ever logs.
                // Without the reason tag one parse failure looks like every other,
                // which is exactly what hid the error_payload misclassification.
                DebugLogger.Error(
                    $"[RIMAI_RELATIONS_PARSE] outcome=Failed reason={failureTag} " +
                    $"provider={provider} path={parseResult.MatchedPath} " +
                    $"body_chars={responseText?.Length ?? 0}");
                session.SetFailure(requestId, errorMsg, failureTag);
                Owner.ExecuteRequestActionOnMainThread(requestId, requestContextVersion, () => onError?.Invoke(errorMsg));
                debugErrorText = errorMsg;
                return true;
            }

            if (RelationsDomainSuccessPipeline.Process(
                    parseResult.Content,
                    debugSource,
                    usageChannel,
                    ref attemptMessages,
                    ref parseRetryCount,
                    ref immersionRetryCount,
                    ref textIntegrityRetryCount,
                    ref contractRetryCount,
                    ref contractValidationStatus,
                    ref contractFailureReason,
                    out string parsedResponse) == DomainSuccessAction.Retry)
            {
                shouldRetry = true;
                return true;
            }

            if (usageTracker.TryRecord(attemptMessages, responseText, parsedResponse, usageChannel, out bool usedEstimatedAfterAnomaly, out int anomalyStreak, out _))
            {
                if (usedEstimatedAfterAnomaly)
                {
                    DebugLogger.WarningGated($"Token usage from provider looks abnormal for {anomalyStreak} consecutive calls, fallback to estimate.");
                }
            }

            session.UpdateState(requestId, AIRequestState.Completed, response: parsedResponse);
            Owner.ExecuteRequestActionOnMainThread(requestId, requestContextVersion, () => onSuccess?.Invoke(parsedResponse));
            debugStatus = AIRequestDebugStatus.Success;
            debugParsedResponse = parsedResponse;
            debugErrorText = string.Empty;
            debugTokenMessages = attemptMessages;
            return true;
        }

internal void FailEarly(
            string requestId,
            int requestContextVersion,
            string error,
            Action<string> onError,
            ref AIRequestDebugStatus debugStatus,
            ref string debugErrorText,
            ref bool debugRecordFinalized,
            List<ChatMessageData> debugTokenMessages)
        {
            session.UpdateState(requestId, AIRequestState.Error, error: error);
            Owner.ExecuteRequestActionOnMainThread(requestId, requestContextVersion, () => onError?.Invoke(error));
            debugStatus = AIRequestDebugStatus.Error;
            debugErrorText = error ?? string.Empty;
            telemetry.FinalizeRequestDebugRecord(requestId, debugTokenMessages, string.Empty, string.Empty, debugStatus, 0, debugErrorText);
            debugRecordFinalized = true;
        }

internal void FinalizeDropped(
            string requestId,
            ref AIRequestDebugStatus debugStatus,
            ref string debugErrorText,
            ref bool debugRecordFinalized,
            List<ChatMessageData> debugTokenMessages,
            string debugResponseText,
            string debugParsedResponse,
            long debugHttpCode)
        {
            session.TryCancelRequest(requestId, "context_changed", "Request dropped due to game context change");
            debugStatus = AIRequestDebugStatus.Cancelled;
            debugErrorText = "Request dropped due to game context change";
            telemetry.FinalizeRequestDebugRecord(requestId, debugTokenMessages, debugResponseText, debugParsedResponse, debugStatus, debugHttpCode, debugErrorText);
            debugRecordFinalized = true;
        }

internal static string FormatProtocolError(long responseCode, bool isLocalModel)
        {
            return responseCode switch
            {
                401 => isLocalModel
                    ? "RimChat_Error401Local".Translate()
                    : "RimChat_Error401Cloud".Translate(),
                404 => "RimChat_Error404".Translate(),
                429 => "RimChat_ErrorRateLimit".Translate(),
                500 => "RimChat_ErrorServer500".Translate(),
                502 => "RimChat_ErrorServer502".Translate(),
                503 => "RimChat_ErrorServer503".Translate(),
                _ => "RimChat_ErrorHTTP".Translate(responseCode)
            };
        }

internal static void LogFingerprint(
            string requestId,
            int attempt,
            DialogueUsageChannel usageChannel,
            string model,
            string url,
            int messageCount,
            int jsonBytes,
            long elapsedMs,
            long httpCode,
            string result)
        {
            if (!DebugLogger.LogInternals)
            {
                return;
            }

            DebugLogger.LogInternal(
                "AIChatServiceAsync",
                $"fingerprint stage=completed requestId={requestId} attempt={attempt} channel={usageChannel} model={model} host={RelationsLocalProviderRetry.GetUrlHostPort(url)} messageCount={messageCount} jsonBytes={jsonBytes} elapsedMs={elapsedMs} httpCode={httpCode} result={result}");
        }

internal static void LogLocalServerRetry(string requestId, int attempt, long responseCode, float retryDelaySeconds, string responseBody)
        {
            if (!DebugLogger.LogInternals)
            {
                return;
            }

            DebugLogger.LogInternal(
                "AIChatServiceAsync",
                $"local_retry requestId={requestId} attempt={attempt} nextAttempt={attempt + 1} httpCode={responseCode} backoffMs={(int)(retryDelaySeconds * 1000f)} responseSummary=\"{RelationsLocalProviderRetry.BuildResponsePreviewForLog(responseBody, 160)}\"");
        }

internal static void LogLocalConnRetry(string requestId, int attempt, string requestError, float retryDelaySeconds)
        {
            if (!DebugLogger.LogInternals)
            {
                return;
            }

            DebugLogger.LogInternal(
                "AIChatServiceAsync",
                $"local_conn_retry requestId={requestId} attempt={attempt} nextAttempt={attempt + 1} backoffMs={(int)(retryDelaySeconds * 1000f)} error=\"{RelationsLocalProviderRetry.BuildResponsePreviewForLog(requestError, 120)}\"");
        }

internal ApiConfig GetFirstValidConfig()
        {
            if (RelationsSettings.TryGetSharedTextConfig(out ApiConfig shared))
                return shared;

            if (RelationsMod.Instance == null || RelationsMod.Instance.InstanceSettings == null)
                return null;

            var localConfig = RelationsMod.Instance.InstanceSettings.LocalConfig;
            if (localConfig != null && localConfig.IsPlayer2Local())
            {
                string localBaseUrl = localConfig.GetNormalizedBaseUrl();
                return new ApiConfig
                {
                    IsEnabled = true,
                    Provider = AIProvider.Player2,
                    BaseUrl = localBaseUrl.TrimEnd('/') + "/v1/chat/completions",
                    ApiKey = "",
                    SelectedModel = "Default"
                };
            }

            return null;
        }
    }

    internal sealed class AIChatServiceAsyncContextOps : AIChatServiceAsyncCollaborator
    {
        internal AIChatServiceAsyncContextOps(AIChatServiceAsync owner) : base(owner)
        {
        }

internal bool IsContextVersionCurrent(int expectedContextVersion)
        {
            lock (Gate)
            {
                return expectedContextVersion == contextVersion;
            }
        }

internal void ExecuteRequestActionOnMainThread(string requestId, int expectedContextVersion, Action action)
        {
            Owner.ExecuteOnMainThread(() =>
            {
                if (!session.IsCallbackAllowed(requestId, expectedContextVersion, Owner.GetCurrentContextVersionSnapshot()))
                {
                    return;
                }

                action?.Invoke();
            });
        }

internal void ProcessMainThreadActions()
        {
            while (true)
            {
                Action action;
                lock (Gate)
                {
                    if (mainThreadActions.Count == 0)
                    {
                        break;
                    }

                    action = mainThreadActions.Dequeue();
                }

                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    DebugLogger.Error($"Error executing main thread action: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }

internal void DetectGameContextChange()
        {
            int currentContextId = AIChatServiceAsync.GetCurrentGameContextId();
            if (currentContextId == lastObservedGameContextId)
            {
                return;
            }

            if (lastObservedGameContextId == -1)
            {
                lastObservedGameContextId = currentContextId;
                return;
            }

            Owner.HandleGameContextChanged("Detected game context transition");
        }

internal void HandleGameContextChanged(string reason)
        {
            int cancelledCount;
            lock (Gate)
            {
                contextVersion++;
                lastObservedGameContextId = AIChatServiceAsync.GetCurrentGameContextId();
                cancelledCount = session.CancelAllPendingLockless(
                    "Request cancelled due to save/game context change",
                    "save_context_changed");
                mainThreadActions.Clear();
                session.ClearLocalRuntimeLockless();
            }

            session.CleanupCompletedRequests();

            if (cancelledCount > 0)
            {
                DebugLogger.Debug($"Cancelled {cancelledCount} pending AI requests due to context change: {reason}");
            }
        }

internal static int GetCurrentGameContextId()
        {
            return Current.Game == null ? 0 : Current.Game.GetHashCode();
        }
    }

    internal sealed class AIChatServiceAsyncParts
    {
        internal readonly AIChatServiceAsync Owner;
        internal readonly AIChatServiceAsyncSlice1 Slice1;
        internal readonly AIChatServiceAsyncRequestFlow RequestFlow;
        internal readonly AIChatServiceAsyncContextOps ContextOps;
        internal AIChatServiceAsyncParts(AIChatServiceAsync owner)
        {
            Owner = owner;
            Slice1 = new AIChatServiceAsyncSlice1(owner);
            RequestFlow = new AIChatServiceAsyncRequestFlow(owner);
            ContextOps = new AIChatServiceAsyncContextOps(owner);
        }
    }


}
