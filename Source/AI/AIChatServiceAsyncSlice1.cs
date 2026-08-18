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
    internal sealed class AIChatServiceAsyncSlice1 : AIChatServiceAsyncCollaborator
    {
        internal AIChatServiceAsyncSlice1(AIChatServiceAsync owner) : base(owner)
        {
        }

public string SendChatRequestAsync(
            List<ChatMessageData> messages,
            Action<string> onSuccess,
            Action<string> onError,
            Action<float> onProgress = null,
            DialogueUsageChannel usageChannel = DialogueUsageChannel.Unknown,
            AIRequestDebugSource debugSource = AIRequestDebugSource.Other,
            int? requestTimeoutSecondsOverride = null,
            float? queueTimeoutSecondsOverride = null)
        {
            Owner.EnsureCollaborators();
            List<ChatMessageData> normalizedMessages = RelationsTextAiRequestBuilder.Normalize(messages, usageChannel);
            string requestId = Guid.NewGuid().ToString("N");
            int requestContextVersion;
            int defaultTimeoutSeconds = RelationsMod.Instance == null ||
                                        !(RelationsMod.Instance.InstanceSettings?.UseCloudProviders ?? false)
                ? LocalRequestTimeoutSeconds
                : CloudRequestTimeoutSeconds;
            int requestTimeoutSeconds = Mathf.Clamp(
                requestTimeoutSecondsOverride ?? defaultTimeoutSeconds,
                5,
                120);
            float queueTimeoutSeconds = Mathf.Clamp(
                queueTimeoutSecondsOverride ?? 60f,
                5f,
                120f);

            session.CleanupCompletedRequests();

            var result = new AIRequestResult
            {
                State = AIRequestState.Pending,
                StartTime = DateTime.Now,
                Progress = 0f,
                Source = debugSource,
                Priority = RelationsAiRequestPriority.Resolve(debugSource),
                AllowCallbacks = true,
                CancelReason = string.Empty,
                FailureReason = string.Empty,
                EnqueuedAtUtc = DateTime.MinValue,
                QueueDeadlineUtc = DateTime.MinValue,
                StartedProcessingAtUtc = DateTime.MinValue,
                QueuePosition = 0,
                RequestTimeoutSeconds = requestTimeoutSeconds,
                QueueTimeoutSeconds = queueTimeoutSeconds,
                LastRequestPayloadBytes = 0,
                LastHttpStatusCode = 0,
                AttemptCount = 0,
                EndpointHostPort = string.Empty,
                FirstResponseByteAtUtc = DateTime.MinValue
            };

            lock (Gate)
            {
                requestContextVersion = contextVersion;
                result.ContextVersion = requestContextVersion;
                session.Add(requestId, result);
            }

            telemetry.BeginRequestDebugRecord(requestId, usageChannel, debugSource);

            StartCoroutine(Owner.ProcessRequestCoroutine(
                requestId,
                normalizedMessages,
                onSuccess,
                onError,
                onProgress,
                usageChannel,
                debugSource,
                requestContextVersion,
                requestTimeoutSeconds));

            return requestId;
        }

public DialogueTokenUsageSnapshot GetLatestDialogueTokenUsage()
        {
            Owner.EnsureCollaborators();
            return usageTracker.LatestClone();
        }

public static bool TryGetLatestDialogueTokenUsage(out DialogueTokenUsageSnapshot snapshot)
        {
            snapshot = null;
            if (_instance == null)
            {
                return false;
            }

            snapshot = _instance.GetLatestDialogueTokenUsage();
            return snapshot != null;
        }

public int GetCurrentContextVersionSnapshot()
        {
            lock (Gate)
            {
                return contextVersion;
            }
        }

public bool CancelRequest(
            string requestId,
            string cancelReason = "cancelled_by_user",
            string error = "Request cancelled by user")
        {
            return session.TryCancelRequest(requestId, cancelReason, error);
        }

public AIRequestResult GetRequestStatus(string requestId)
        {
            lock (Gate)
            {
                return session.Get(requestId);
            }
        }

public AIRequestDebugSnapshot GetRequestDebugSnapshot()
        {
            Owner.EnsureCollaborators();
            return telemetry.GetRequestDebugSnapshot();
        }

public static bool TryGetRequestDebugSnapshot(out AIRequestDebugSnapshot snapshot)
        {
            snapshot = null;
            if (_instance == null)
            {
                return false;
            }

            snapshot = _instance.GetRequestDebugSnapshot();
            return snapshot != null;
        }

public static void RecordExternalDebugRecord(
            AIRequestDebugSource source,
            DialogueUsageChannel channel,
            string model,
            AIRequestDebugStatus status,
            long durationMs,
            long httpStatusCode,
            string requestText,
            string responseText,
            string errorText,
            DateTime? startedAtUtc = null)
        {
            if (_instance == null)
            {
                return;
            }

            _instance.EnsureCollaborators();
            _instance.telemetry.RecordExternal(
                source, channel, model, status, durationMs, httpStatusCode,
                requestText, responseText, errorText, startedAtUtc);
        }

public static void RecordExternalDebugRecord(
            AIRequestDebugSource source,
            DialogueUsageChannel channel,
            string model,
            AIRequestDebugStatus status,
            long durationMs,
            long httpStatusCode,
            int promptTokens,
            int completionTokens,
            int totalTokens,
            bool isEstimatedTokens,
            string requestText,
            string responseText,
            string errorText,
            DateTime? startedAtUtc = null)
        {
            if (_instance == null)
            {
                return;
            }

            _instance.EnsureCollaborators();
            _instance.telemetry.RecordExternal(
                source, channel, model, status, durationMs, httpStatusCode,
                promptTokens, completionTokens, totalTokens, isEstimatedTokens,
                requestText, responseText, errorText, startedAtUtc);
        }

public void ExecuteOnMainThread(Action action)
        {
            if (action == null)
            {
                return;
            }

            if (MainThreadSchedulerAccess.IsAvailable)
            {
                MainThreadSchedulerAccess.Enqueue(action);
                return;
            }

            lock (Gate)
            {
                mainThreadActions.Enqueue(action);
            }
        }
    }
}
