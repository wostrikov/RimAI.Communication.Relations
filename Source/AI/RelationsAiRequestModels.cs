using System;

namespace Ustas.RimAI.Communication.Relations.AI
{
    public enum AIRequestState
    {
        Idle,
        Pending,
        Queued,
        Processing,
        Completed,
        Error,
        Cancelled
    }

    public enum AIRequestPriority
    {
        Background = 0,
        Interactive = 1
    }

    public enum DialogueUsageChannel
    {
        Unknown = 0,
        Diplomacy = 1,
        Rpg = 2
    }

    public class AIRequestResult
    {
        public bool Success { get; set; }
        public string Response { get; set; }
        public string Error { get; set; }
        public float Progress { get; set; }
        public AIRequestState State { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan Duration { get; set; }
        public int ContextVersion { get; set; }
        public AIRequestDebugSource Source { get; set; }
        public AIRequestPriority Priority { get; set; }
        public DateTime EnqueuedAtUtc { get; set; }
        public DateTime QueueDeadlineUtc { get; set; }
        public DateTime StartedProcessingAtUtc { get; set; }
        public int QueuePosition { get; set; }
        public bool AllowCallbacks { get; set; }
        public string CancelReason { get; set; }
        public string FailureReason { get; set; }
        public int RequestTimeoutSeconds { get; set; }
        public float QueueTimeoutSeconds { get; set; }
        public int LastRequestPayloadBytes { get; set; }
        public long LastHttpStatusCode { get; set; }
        public int AttemptCount { get; set; }
        public string EndpointHostPort { get; set; }
        public DateTime FirstResponseByteAtUtc { get; set; }
    }

    public class DialogueTokenUsageSnapshot
    {
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
        public bool IsEstimated { get; set; }
        public DialogueUsageChannel Channel { get; set; }
        public DateTime RecordedAtUtc { get; set; }

        public DialogueTokenUsageSnapshot Clone()
        {
            return new DialogueTokenUsageSnapshot
            {
                PromptTokens = PromptTokens,
                CompletionTokens = CompletionTokens,
                TotalTokens = TotalTokens,
                IsEstimated = IsEstimated,
                Channel = Channel,
                RecordedAtUtc = RecordedAtUtc
            };
        }
    }

    internal static class RelationsAiRequestPriority
    {
        public static AIRequestPriority Resolve(AIRequestDebugSource source)
        {
            switch (source)
            {
                case AIRequestDebugSource.DiplomacyDialogue:
                case AIRequestDebugSource.RpgDialogue:
                case AIRequestDebugSource.StrategySuggestion:
                case AIRequestDebugSource.SendImage:
                case AIRequestDebugSource.ApiUsabilityTest:
                case AIRequestDebugSource.AirdropSelection:
                    return AIRequestPriority.Interactive;
                default:
                    return AIRequestPriority.Background;
            }
        }

        public static bool IsInFlight(AIRequestState state)
        {
            return state == AIRequestState.Pending ||
                   state == AIRequestState.Queued ||
                   state == AIRequestState.Processing;
        }

        public static bool IsTerminal(AIRequestState state)
        {
            return state == AIRequestState.Completed ||
                   state == AIRequestState.Error ||
                   state == AIRequestState.Cancelled;
        }

        public static bool IsExternallyTerminated(AIRequestState state)
        {
            return state == AIRequestState.Error ||
                   state == AIRequestState.Cancelled;
        }
    }
}
