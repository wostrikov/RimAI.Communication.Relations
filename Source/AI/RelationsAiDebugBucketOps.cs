using System;
using System.Collections.Generic;
using System.Linq;

namespace Ustas.RimAI.Communication.Relations.AI
{
    /// <summary>
    /// Debug telemetry bucket/summary/token classification helpers.
    /// </summary>
    internal static class RelationsAiDebugBucketOps
    {
        internal static List<AIRequestDebugBucket> BuildEmptyDebugBuckets(DateTime windowStartUtc)
        {
            int bucketCount = RelationsAiDebugTelemetry.DebugWindowMinutes / RelationsAiDebugTelemetry.DebugBucketMinutes;
            var buckets = new List<AIRequestDebugBucket>(bucketCount);
            for (int i = 0; i < bucketCount; i++)
            {
                buckets.Add(new AIRequestDebugBucket
                {
                    BucketStartUtc = windowStartUtc.AddMinutes(i * RelationsAiDebugTelemetry.DebugBucketMinutes),
                    RequestCount = 0,
                    TotalTokens = 0,
                    HighPriorityTokens = 0
                });
            }

            return buckets;
        }

        internal static List<AIRequestDebugBucket> BuildDebugBuckets(List<AIRequestDebugRecord> records, DateTime windowStartUtc)
        {
            int bucketCount = RelationsAiDebugTelemetry.DebugWindowMinutes / RelationsAiDebugTelemetry.DebugBucketMinutes;
            var buckets = new List<AIRequestDebugBucket>(bucketCount);
            for (int i = 0; i < bucketCount; i++)
            {
                buckets.Add(new AIRequestDebugBucket
                {
                    BucketStartUtc = windowStartUtc.AddMinutes(i * RelationsAiDebugTelemetry.DebugBucketMinutes),
                    RequestCount = 0,
                    TotalTokens = 0,
                    HighPriorityTokens = 0
                });
            }

            if (records == null || records.Count == 0)
            {
                return buckets;
            }

            for (int i = 0; i < records.Count; i++)
            {
                AIRequestDebugRecord record = records[i];
                double deltaMinutes = (record.RecordedAtUtc - windowStartUtc).TotalMinutes;
                int bucketIndex = (int)Math.Floor(deltaMinutes / RelationsAiDebugTelemetry.DebugBucketMinutes);
                if (bucketIndex < 0 || bucketIndex >= buckets.Count)
                {
                    continue;
                }

                AIRequestDebugBucket bucket = buckets[bucketIndex];
                bucket.RequestCount++;
                bucket.TotalTokens += Math.Max(0, record.TotalTokens);
                if (record.IsHighPrioritySource)
                {
                    bucket.HighPriorityTokens += Math.Max(0, record.TotalTokens);
                }
            }

            return buckets;
        }

        internal static AIRequestDebugSummary BuildDebugSummary(List<AIRequestDebugRecord> records)
        {
            var summary = new AIRequestDebugSummary();
            if (records == null || records.Count == 0)
            {
                return summary;
            }

            int requestCount = records.Count;
            int successCount = records.Count(record => record.Status == AIRequestDebugStatus.Success);
            int errorCount = records.Count(record => record.Status == AIRequestDebugStatus.Error);
            int cancelledCount = records.Count(record => record.Status == AIRequestDebugStatus.Cancelled);
            int totalTokens = records.Sum(record => Math.Max(0, record.TotalTokens));
            int highPriorityTokens = records
                .Where(record => record.IsHighPrioritySource)
                .Sum(record => Math.Max(0, record.TotalTokens));

            summary.RequestCount = requestCount;
            summary.SuccessCount = successCount;
            summary.ErrorCount = errorCount;
            summary.CancelledCount = cancelledCount;
            summary.TotalTokens = totalTokens;
            summary.SuccessRatePercent = requestCount > 0 ? (float)successCount / requestCount * 100f : 0f;
            summary.AverageDurationMs = requestCount > 0 ? (float)records.Average(record => Math.Max(0L, record.DurationMs)) : 0f;
            summary.HighPriorityTokenSharePercent = totalTokens > 0 ? (float)highPriorityTokens / totalTokens * 100f : 0f;
            return summary;
        }

        internal static RelationsAiDebugTelemetry.DebugTokenUsage ResolveDebugTokenUsage(
            List<ChatMessageData> messages,
            string rawResponseText,
            string parsedResponse)
        {
            DialogueTokenUsageTracker.Estimate(messages, parsedResponse, out int estimatedPromptTokens, out int estimatedCompletionTokens, out int estimatedTotalTokens);
            bool hasProviderUsage = DialogueTokenUsageTracker.TryExtract(rawResponseText, out int providerPromptTokens, out int providerCompletionTokens, out int providerTotalTokens);
            bool providerLooksAbnormal = hasProviderUsage && DialogueTokenUsageTracker.ShouldUseEstimatedUsage(
                providerPromptTokens,
                providerCompletionTokens,
                providerTotalTokens,
                estimatedPromptTokens,
                estimatedCompletionTokens,
                estimatedTotalTokens);

            bool useEstimated = !hasProviderUsage || providerLooksAbnormal;
            int promptTokens = useEstimated ? estimatedPromptTokens : providerPromptTokens;
            int completionTokens = useEstimated ? estimatedCompletionTokens : providerCompletionTokens;
            int totalTokens = useEstimated ? estimatedTotalTokens : providerTotalTokens;
            if (totalTokens <= 0)
            {
                totalTokens = Math.Max(0, promptTokens) + Math.Max(0, completionTokens);
            }

            return new RelationsAiDebugTelemetry.DebugTokenUsage
            {
                PromptTokens = Math.Max(0, promptTokens),
                CompletionTokens = Math.Max(0, completionTokens),
                TotalTokens = Math.Max(0, totalTokens),
                IsEstimated = useEstimated
            };
        }

        public static AIRequestDebugStatus ClassifyDebugStatusFromError(string errorText)
        {
            if (string.IsNullOrWhiteSpace(errorText))
            {
                return AIRequestDebugStatus.Error;
            }

            string lower = errorText.ToLowerInvariant();
            if (lower.Contains("cancel") ||
                lower.Contains("context change") ||
                lower.Contains("dropped"))
            {
                return AIRequestDebugStatus.Cancelled;
            }

            return AIRequestDebugStatus.Error;
        }
    }
}
