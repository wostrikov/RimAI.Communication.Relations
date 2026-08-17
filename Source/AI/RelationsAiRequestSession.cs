using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Ustas.RimAI.Communication.Relations.Diagnostics;
using Verse;

namespace Ustas.RimAI.Communication.Relations.AI
{
    /// <summary>
    /// In-flight Relations request table, local single-flight gate, and logical
    /// CancellationTokenSource tracking. Does not own UnityWebRequest handles.
    /// </summary>
    internal sealed class RelationsAiRequestSession
    {
        public const float LocalRequestQueueTimeoutSeconds = 60f;
        public const double RequestResultRetentionMinutes = 5d;
        public const int MaxRetainedTerminalRequests = 256;

        public readonly object Gate = new object();

        readonly Dictionary<string, AIRequestResult> activeRequests = new Dictionary<string, AIRequestResult>();
        readonly Queue<string> localRequestQueue = new Queue<string>();
        readonly Queue<string> interactiveLocalRequestQueue = new Queue<string>();
        readonly HashSet<string> queuedLocalRequestIds = new HashSet<string>(StringComparer.Ordinal);
        readonly Dictionary<string, CancellationTokenSource> activeTransportCancellations =
            new Dictionary<string, CancellationTokenSource>(StringComparer.Ordinal);

        string activeLocalRequestId;

        public void Add(string requestId, AIRequestResult result)
        {
            activeRequests[requestId] = result;
        }

        public bool TryGet(string requestId, out AIRequestResult result)
        {
            return activeRequests.TryGetValue(requestId, out result);
        }

        public AIRequestResult Get(string requestId)
        {
            return activeRequests.TryGetValue(requestId, out AIRequestResult result) ? result : null;
        }

        public void EnqueueLocalRequest(string requestId)
        {
            lock (Gate)
            {
                if (string.IsNullOrWhiteSpace(requestId))
                {
                    return;
                }

                if (queuedLocalRequestIds.Add(requestId))
                {
                    ResolveLocalRequestQueueLockless(requestId).Enqueue(requestId);
                    MarkRequestQueuedLockless(requestId);
                    RefreshQueuedRequestPositionsLockless();
                }
            }
        }

        public bool TryAcquireLocalRequestSlot(string requestId)
        {
            lock (Gate)
            {
                if (activeLocalRequestId == requestId)
                {
                    return true;
                }

                if (!string.IsNullOrEmpty(activeLocalRequestId))
                {
                    return false;
                }

                if (!TryPeekNextLocalRequestIdLockless(out string nextRequestId) ||
                    !string.Equals(nextRequestId, requestId, StringComparison.Ordinal))
                {
                    return false;
                }

                DequeueNextLocalRequestLockless();
                queuedLocalRequestIds.Remove(requestId);
                activeLocalRequestId = requestId;
                MarkRequestProcessingStartedLockless(requestId);
                RefreshQueuedRequestPositionsLockless();
                return true;
            }
        }

        public void ReleaseLocalRequestSlot(string requestId)
        {
            lock (Gate)
            {
                if (string.Equals(activeLocalRequestId, requestId, StringComparison.Ordinal))
                {
                    activeLocalRequestId = null;
                }

                RemoveLocalRequestLockless(requestId);
            }
        }

        public void RemoveLocalRequest(string requestId)
        {
            lock (Gate)
            {
                RemoveLocalRequestLockless(requestId);
            }
        }

        public void MarkProcessingStarted(string requestId)
        {
            lock (Gate)
            {
                MarkRequestProcessingStartedLockless(requestId);
            }
        }

        public void RegisterActiveTransportCancellation(string requestId, CancellationTokenSource cancellation)
        {
            lock (Gate)
            {
                if (string.IsNullOrWhiteSpace(requestId) || cancellation == null)
                {
                    return;
                }

                activeTransportCancellations[requestId] = cancellation;
            }
        }

        public void UnregisterActiveTransportCancellation(string requestId, CancellationTokenSource cancellation = null)
        {
            lock (Gate)
            {
                if (string.IsNullOrWhiteSpace(requestId))
                {
                    return;
                }

                if (!activeTransportCancellations.TryGetValue(requestId, out CancellationTokenSource current))
                {
                    return;
                }

                if (cancellation != null && !ReferenceEquals(current, cancellation))
                {
                    return;
                }

                activeTransportCancellations.Remove(requestId);
            }
        }

        public bool TryCancelRequest(string requestId, string cancelReason, string error)
        {
            lock (Gate)
            {
                return TryCancelRequestLockless(requestId, cancelReason, error);
            }
        }

        public int CancelAllPendingLockless(string reason, string cancelReason)
        {
            int cancelled = 0;
            foreach (var kvp in activeRequests)
            {
                if (RelationsAiRequestPriority.IsInFlight(kvp.Value.State) &&
                    TryCancelRequestLockless(kvp.Key, cancelReason, reason))
                {
                    cancelled++;
                }
            }

            return cancelled;
        }

        public void ClearLocalRuntimeLockless()
        {
            interactiveLocalRequestQueue.Clear();
            localRequestQueue.Clear();
            queuedLocalRequestIds.Clear();
            foreach (CancellationTokenSource cancellation in activeTransportCancellations.Values)
            {
                try
                {
                    cancellation.Cancel();
                }
                catch
                {
                }
            }

            activeTransportCancellations.Clear();
            activeLocalRequestId = null;
        }

        public int CancelAllPending(string reason, string cancelReason = "context_change")
        {
            lock (Gate)
            {
                return CancelAllPendingLockless(reason, cancelReason);
            }
        }

        public void ClearLocalRuntime()
        {
            lock (Gate)
            {
                ClearLocalRuntimeLockless();
            }
        }

        public bool TryTimeoutQueuedRequest(string requestId)
        {
            lock (Gate)
            {
                if (!activeRequests.TryGetValue(requestId, out AIRequestResult result))
                {
                    return false;
                }

                if (result.State != AIRequestState.Queued || result.QueueDeadlineUtc == DateTime.MinValue)
                {
                    return false;
                }

                if (DateTime.UtcNow < result.QueueDeadlineUtc)
                {
                    return false;
                }

                SetRequestFailureLockless(requestId, "RimChat_ErrorQueueTimeout".Translate().ToString(), "queue_timeout");
                RemoveLocalRequestLockless(requestId);
                return true;
            }
        }

        public bool TryGetTerminalRequestDisposition(
            string requestId,
            out AIRequestState terminalState,
            out string message,
            out bool allowCallback)
        {
            lock (Gate)
            {
                terminalState = AIRequestState.Idle;
                message = null;
                allowCallback = false;

                if (!activeRequests.TryGetValue(requestId, out AIRequestResult result) ||
                    !RelationsAiRequestPriority.IsExternallyTerminated(result.State))
                {
                    return false;
                }

                terminalState = result.State;
                message = string.IsNullOrWhiteSpace(result.Error)
                    ? result.CancelReason
                    : result.Error;
                allowCallback = result.AllowCallbacks && result.State == AIRequestState.Error;
                return true;
            }
        }

        public bool TryGetRequestError(string requestId, out string error)
        {
            lock (Gate)
            {
                error = null;
                if (!activeRequests.TryGetValue(requestId, out AIRequestResult result) ||
                    result.State != AIRequestState.Error)
                {
                    return false;
                }

                error = string.IsNullOrWhiteSpace(result.Error)
                    ? "Request cancelled"
                    : result.Error;
                return true;
            }
        }

        public void SetFailure(string requestId, string error, string failureReason)
        {
            lock (Gate)
            {
                SetRequestFailureLockless(requestId, error, failureReason);
            }
        }

        public void UpdateState(string requestId, AIRequestState state, string response = null, string error = null)
        {
            lock (Gate)
            {
                if (!activeRequests.TryGetValue(requestId, out AIRequestResult result))
                {
                    return;
                }

                result.State = state;
                result.Response = response;
                result.Error = error;
                if (state == AIRequestState.Completed)
                {
                    result.AllowCallbacks = true;
                    result.CancelReason = string.Empty;
                    result.FailureReason = string.Empty;
                    result.QueueDeadlineUtc = DateTime.MinValue;
                    result.QueuePosition = 0;
                    result.StartedProcessingAtUtc = result.StartedProcessingAtUtc == DateTime.MinValue
                        ? DateTime.UtcNow
                        : result.StartedProcessingAtUtc;
                }
                else if (state == AIRequestState.Error)
                {
                    result.AllowCallbacks = true;
                    if (string.IsNullOrWhiteSpace(result.FailureReason))
                    {
                        result.FailureReason = "request_error";
                    }

                    result.CancelReason = string.Empty;
                    result.QueueDeadlineUtc = DateTime.MinValue;
                    result.QueuePosition = 0;
                }

                if (state == AIRequestState.Completed ||
                    state == AIRequestState.Error ||
                    state == AIRequestState.Cancelled)
                {
                    result.Duration = DateTime.Now - result.StartTime;
                }
            }
        }

        public void UpdateProgress(string requestId, float progress)
        {
            lock (Gate)
            {
                if (activeRequests.TryGetValue(requestId, out AIRequestResult result))
                {
                    result.Progress = progress;
                }
            }
        }

        public void RecordTransportEnvelope(string requestId, string endpointHostPort)
        {
            lock (Gate)
            {
                if (activeRequests.TryGetValue(requestId, out AIRequestResult result))
                {
                    result.EndpointHostPort = endpointHostPort ?? string.Empty;
                }
            }
        }

        public void RecordAttemptTelemetry(string requestId, int attempt, int payloadBytes)
        {
            lock (Gate)
            {
                if (!activeRequests.TryGetValue(requestId, out AIRequestResult result))
                {
                    return;
                }

                result.AttemptCount = attempt;
                result.LastRequestPayloadBytes = payloadBytes;
            }
        }

        public void RecordHttpStatus(string requestId, long httpStatusCode)
        {
            lock (Gate)
            {
                if (activeRequests.TryGetValue(requestId, out AIRequestResult result))
                {
                    result.LastHttpStatusCode = httpStatusCode;
                }
            }
        }

        public void RecordFirstResponseByte(string requestId)
        {
            lock (Gate)
            {
                if (!activeRequests.TryGetValue(requestId, out AIRequestResult result) ||
                    result.FirstResponseByteAtUtc != DateTime.MinValue)
                {
                    return;
                }

                result.FirstResponseByteAtUtc = DateTime.UtcNow;
            }
        }

        public bool IsCallbackAllowed(string requestId, int expectedContextVersion, int currentContextVersion)
        {
            lock (Gate)
            {
                if (expectedContextVersion != currentContextVersion)
                {
                    return false;
                }

                if (!activeRequests.TryGetValue(requestId, out AIRequestResult result))
                {
                    return false;
                }

                return result.ContextVersion == expectedContextVersion &&
                       result.AllowCallbacks &&
                       result.State != AIRequestState.Cancelled;
            }
        }

        public void CleanupCompletedRequests()
        {
            lock (Gate)
            {
                var completedIds = new List<string>();
                var terminalRequests = new List<KeyValuePair<string, AIRequestResult>>();
                foreach (var kvp in activeRequests)
                {
                    if (RelationsAiRequestPriority.IsTerminal(kvp.Value.State))
                    {
                        terminalRequests.Add(kvp);
                        if ((DateTime.Now - kvp.Value.StartTime).TotalMinutes > RequestResultRetentionMinutes)
                        {
                            completedIds.Add(kvp.Key);
                        }
                    }
                }

                int retainedCount = terminalRequests.Count - completedIds.Count;
                if (retainedCount > MaxRetainedTerminalRequests)
                {
                    int extraCount = retainedCount - MaxRetainedTerminalRequests;
                    foreach (var kvp in terminalRequests
                        .Where(item => !completedIds.Contains(item.Key))
                        .OrderBy(item => item.Value.StartTime)
                        .Take(extraCount))
                    {
                        completedIds.Add(kvp.Key);
                    }
                }

                foreach (var id in completedIds)
                {
                    activeRequests.Remove(id);
                }
            }
        }

        public void Destroy()
        {
            lock (Gate)
            {
                foreach (var kvp in activeRequests)
                {
                    if (RelationsAiRequestPriority.IsInFlight(kvp.Value.State))
                    {
                        TryCancelRequestLockless(kvp.Key, "service_destroyed", "Service destroyed");
                    }
                }

                interactiveLocalRequestQueue.Clear();
                localRequestQueue.Clear();
                queuedLocalRequestIds.Clear();
                foreach (CancellationTokenSource cancellation in activeTransportCancellations.Values)
                {
                    try
                    {
                        cancellation.Cancel();
                    }
                    catch
                    {
                    }
                }

                activeTransportCancellations.Clear();
                activeLocalRequestId = null;
            }
        }

        void MarkRequestQueuedLockless(string requestId)
        {
            if (!activeRequests.TryGetValue(requestId, out AIRequestResult result))
            {
                return;
            }

            result.State = AIRequestState.Queued;
            if (result.EnqueuedAtUtc == DateTime.MinValue)
            {
                result.EnqueuedAtUtc = DateTime.UtcNow;
            }

            float timeoutSeconds = result.QueueTimeoutSeconds > 0f
                ? result.QueueTimeoutSeconds
                : LocalRequestQueueTimeoutSeconds;
            result.QueueDeadlineUtc = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            result.StartedProcessingAtUtc = DateTime.MinValue;
            result.Progress = 0f;
            result.CancelReason = string.Empty;
            result.FailureReason = string.Empty;
            result.AllowCallbacks = true;
        }

        void MarkRequestProcessingStartedLockless(string requestId)
        {
            if (!activeRequests.TryGetValue(requestId, out AIRequestResult result))
            {
                return;
            }

            result.State = AIRequestState.Processing;
            result.StartedProcessingAtUtc = DateTime.UtcNow;
            result.QueueDeadlineUtc = DateTime.MinValue;
            result.QueuePosition = 0;
            result.CancelReason = string.Empty;
        }

        void CancelActiveTransportLockless(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId))
            {
                return;
            }

            if (!activeTransportCancellations.TryGetValue(requestId, out CancellationTokenSource cancellation) || cancellation == null)
            {
                return;
            }

            try
            {
                cancellation.Cancel();
            }
            catch (Exception ex)
            {
                DebugLogger.LogInternal("AIChatServiceAsync", $"Abort request failed: requestId={requestId}, error={ex.Message}");
            }

            activeTransportCancellations.Remove(requestId);
        }

        void SetRequestFailureLockless(string requestId, string error, string failureReason)
        {
            if (!activeRequests.TryGetValue(requestId, out AIRequestResult result))
            {
                return;
            }

            result.State = AIRequestState.Error;
            result.Error = error;
            result.FailureReason = failureReason ?? string.Empty;
            result.CancelReason = string.Empty;
            result.AllowCallbacks = true;
            result.QueueDeadlineUtc = DateTime.MinValue;
            result.QueuePosition = 0;
            result.Duration = DateTime.Now - result.StartTime;
        }

        bool TryCancelRequestLockless(string requestId, string cancelReason, string error = null)
        {
            if (string.IsNullOrWhiteSpace(requestId))
            {
                return false;
            }

            if (!activeRequests.TryGetValue(requestId, out AIRequestResult result) ||
                !RelationsAiRequestPriority.IsInFlight(result.State))
            {
                return false;
            }

            result.State = AIRequestState.Cancelled;
            result.Error = error ?? string.Empty;
            result.CancelReason = cancelReason ?? "cancelled";
            result.FailureReason = result.CancelReason;
            result.AllowCallbacks = false;
            result.QueueDeadlineUtc = DateTime.MinValue;
            result.QueuePosition = 0;
            result.Duration = DateTime.Now - result.StartTime;
            RemoveLocalRequestLockless(requestId);
            CancelActiveTransportLockless(requestId);
            return true;
        }

        void RefreshQueuedRequestPositionsLockless()
        {
            foreach (AIRequestResult result in activeRequests.Values)
            {
                if (result.State == AIRequestState.Queued)
                {
                    result.QueuePosition = 0;
                }
            }

            int queuePosition = 1;
            RefreshQueuedRequestPositionsForQueueLockless(interactiveLocalRequestQueue, ref queuePosition);
            RefreshQueuedRequestPositionsForQueueLockless(localRequestQueue, ref queuePosition);
        }

        void RefreshQueuedRequestPositionsForQueueLockless(Queue<string> queue, ref int queuePosition)
        {
            if (queue == null || queue.Count == 0)
            {
                return;
            }

            foreach (string requestId in queue)
            {
                if (!queuedLocalRequestIds.Contains(requestId))
                {
                    continue;
                }

                if (!activeRequests.TryGetValue(requestId, out AIRequestResult result))
                {
                    continue;
                }

                if (!RelationsAiRequestPriority.IsInFlight(result.State))
                {
                    continue;
                }

                result.State = AIRequestState.Queued;
                result.QueuePosition = queuePosition++;
            }
        }

        void RemoveLocalRequestLockless(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId))
            {
                return;
            }

            if (string.Equals(activeLocalRequestId, requestId, StringComparison.Ordinal))
            {
                activeLocalRequestId = null;
            }

            if (!queuedLocalRequestIds.Remove(requestId))
            {
                return;
            }

            RebuildLocalRequestQueueWithoutLockless(interactiveLocalRequestQueue, requestId);
            RebuildLocalRequestQueueWithoutLockless(localRequestQueue, requestId);
            RefreshQueuedRequestPositionsLockless();
        }

        Queue<string> ResolveLocalRequestQueueLockless(string requestId)
        {
            if (activeRequests.TryGetValue(requestId, out AIRequestResult result) &&
                result.Priority == AIRequestPriority.Interactive)
            {
                return interactiveLocalRequestQueue;
            }

            return localRequestQueue;
        }

        bool TryPeekNextLocalRequestIdLockless(out string requestId)
        {
            if (TryPeekNextLocalRequestIdFromQueueLockless(interactiveLocalRequestQueue, out requestId))
            {
                return true;
            }

            return TryPeekNextLocalRequestIdFromQueueLockless(localRequestQueue, out requestId);
        }

        bool TryPeekNextLocalRequestIdFromQueueLockless(Queue<string> queue, out string requestId)
        {
            requestId = null;
            if (queue == null)
            {
                return false;
            }

            while (queue.Count > 0)
            {
                string candidate = queue.Peek();
                if (IsQueuedLocalRequestEligibleLockless(candidate))
                {
                    requestId = candidate;
                    return true;
                }

                queue.Dequeue();
                queuedLocalRequestIds.Remove(candidate);
            }

            return false;
        }

        void DequeueNextLocalRequestLockless()
        {
            if (TryPeekNextLocalRequestIdFromQueueLockless(interactiveLocalRequestQueue, out _))
            {
                interactiveLocalRequestQueue.Dequeue();
                return;
            }

            if (TryPeekNextLocalRequestIdFromQueueLockless(localRequestQueue, out _))
            {
                localRequestQueue.Dequeue();
            }
        }

        bool IsQueuedLocalRequestEligibleLockless(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId) || !queuedLocalRequestIds.Contains(requestId))
            {
                return false;
            }

            return activeRequests.TryGetValue(requestId, out AIRequestResult result) &&
                   RelationsAiRequestPriority.IsInFlight(result.State);
        }

        static void RebuildLocalRequestQueueWithoutLockless(Queue<string> queue, string requestId)
        {
            if (queue == null || queue.Count == 0)
            {
                return;
            }

            var remaining = new Queue<string>(queue.Count);
            while (queue.Count > 0)
            {
                string queuedId = queue.Dequeue();
                if (!string.Equals(queuedId, requestId, StringComparison.Ordinal))
                {
                    remaining.Enqueue(queuedId);
                }
            }

            while (remaining.Count > 0)
            {
                queue.Enqueue(remaining.Dequeue());
            }
        }
    }
}
