using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Ustas.RimAI.Communication.Relations.AI
{
    /// <summary>
    /// Dialogue token accounting. Provider usage envelopes stay here; the JSON/SSE
    /// checkpoint may later replace the regex extraction without changing callers.
    /// </summary>
    internal sealed class DialogueTokenUsageTracker
    {
        public const int ProviderUsageAnomalyFallbackThreshold = 2;

        static readonly Regex[] PromptTokensRegexes =
        {
            new Regex("\"prompt_tokens\"\\s*:\\s*(\\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("\"input_tokens\"\\s*:\\s*(\\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("\"promptTokenCount\"\\s*:\\s*(\\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("\"inputTokenCount\"\\s*:\\s*(\\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        };

        static readonly Regex[] CompletionTokensRegexes =
        {
            new Regex("\"completion_tokens\"\\s*:\\s*(\\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("\"output_tokens\"\\s*:\\s*(\\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("\"candidatesTokenCount\"\\s*:\\s*(\\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("\"outputTokenCount\"\\s*:\\s*(\\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        };

        static readonly Regex[] TotalTokensRegexes =
        {
            new Regex("\"total_tokens\"\\s*:\\s*(\\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("\"totalTokenCount\"\\s*:\\s*(\\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex("\"total_token_count\"\\s*:\\s*(\\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        };

        readonly object gate;
        DialogueTokenUsageSnapshot latest;
        int providerUsageAnomalyStreak;

        public DialogueTokenUsageTracker(object gate)
        {
            this.gate = gate ?? new object();
        }

        public DialogueTokenUsageSnapshot LatestClone()
        {
            lock (gate)
            {
                return latest?.Clone();
            }
        }

        public bool TryRecord(
            List<ChatMessageData> messages,
            string rawJsonResponse,
            string parsedResponse,
            DialogueUsageChannel usageChannel,
            out bool usedEstimatedAfterAnomaly,
            out int anomalyStreak,
            out DialogueTokenUsageSnapshot snapshot)
        {
            usedEstimatedAfterAnomaly = false;
            anomalyStreak = 0;
            snapshot = null;
            if (!ShouldTrack(usageChannel))
            {
                return false;
            }

            Estimate(messages, parsedResponse, out int estimatedPromptTokens, out int estimatedCompletionTokens, out int estimatedTotalTokens);
            bool hasUsage = TryExtract(rawJsonResponse, out int providerPromptTokens, out int providerCompletionTokens, out int providerTotalTokens);
            bool providerLooksAbnormal = hasUsage && ShouldUseEstimatedUsage(
                providerPromptTokens,
                providerCompletionTokens,
                providerTotalTokens,
                estimatedPromptTokens,
                estimatedCompletionTokens,
                estimatedTotalTokens);
            anomalyStreak = UpdateAnomalyStreak(hasUsage, providerLooksAbnormal);
            bool useEstimated = !hasUsage || (providerLooksAbnormal && anomalyStreak >= ProviderUsageAnomalyFallbackThreshold);
            usedEstimatedAfterAnomaly = useEstimated && providerLooksAbnormal;

            int promptTokens = useEstimated ? estimatedPromptTokens : providerPromptTokens;
            int completionTokens = useEstimated ? estimatedCompletionTokens : providerCompletionTokens;
            int totalTokens = useEstimated ? estimatedTotalTokens : providerTotalTokens;
            if (totalTokens <= 0)
            {
                return false;
            }

            snapshot = new DialogueTokenUsageSnapshot
            {
                PromptTokens = Math.Max(0, promptTokens),
                CompletionTokens = Math.Max(0, completionTokens),
                TotalTokens = Math.Max(0, totalTokens),
                IsEstimated = useEstimated,
                Channel = usageChannel,
                RecordedAtUtc = DateTime.UtcNow
            };

            lock (gate)
            {
                latest = snapshot;
            }

            return true;
        }

        public static bool ShouldTrack(DialogueUsageChannel usageChannel)
        {
            return usageChannel == DialogueUsageChannel.Diplomacy || usageChannel == DialogueUsageChannel.Rpg;
        }

        public static bool TryExtract(string json, out int promptTokens, out int completionTokens, out int totalTokens)
        {
            promptTokens = 0;
            completionTokens = 0;
            totalTokens = 0;

            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            string usageScope = json;
            if (TryExtractUsageObject(json, out string usageObject))
            {
                usageScope = usageObject;
            }

            bool usageFound = TryExtractUsageCore(usageScope, out promptTokens, out completionTokens, out totalTokens);
            if (usageFound)
            {
                return true;
            }

            if (!ReferenceEquals(usageScope, json))
            {
                return TryExtractUsageCore(json, out promptTokens, out completionTokens, out totalTokens);
            }

            return false;
        }

        public static void Estimate(
            List<ChatMessageData> messages,
            string parsedResponse,
            out int promptTokens,
            out int completionTokens,
            out int totalTokens)
        {
            int promptChars = 0;
            if (messages != null)
            {
                for (int i = 0; i < messages.Count; i++)
                {
                    promptChars += messages[i]?.content?.Length ?? 0;
                    promptChars += messages[i]?.role?.Length ?? 0;
                }
            }

            int completionChars = parsedResponse?.Length ?? 0;
            promptTokens = promptChars <= 0 ? 0 : (int)Math.Ceiling(promptChars / 4d);
            completionTokens = completionChars <= 0 ? 0 : (int)Math.Ceiling(completionChars / 4d);
            totalTokens = promptTokens + completionTokens;
        }

        public static bool ShouldUseEstimatedUsage(
            int providerPromptTokens,
            int providerCompletionTokens,
            int providerTotalTokens,
            int estimatedPromptTokens,
            int estimatedCompletionTokens,
            int estimatedTotalTokens)
        {
            if (providerTotalTokens <= 0)
            {
                return true;
            }

            if (providerCompletionTokens > providerTotalTokens)
            {
                return true;
            }

            if (providerPromptTokens > 0 && providerCompletionTokens > 0)
            {
                int providerCombined = providerPromptTokens + providerCompletionTokens;
                int mismatchTolerance = Math.Max(64, (int)(providerTotalTokens * 0.4f));
                if (Math.Abs(providerCombined - providerTotalTokens) > mismatchTolerance)
                {
                    return true;
                }
            }

            if (estimatedTotalTokens >= 200)
            {
                float minReliable = estimatedTotalTokens * 0.08f;
                float maxReliable = estimatedTotalTokens * 8.0f;
                if (providerTotalTokens < minReliable || providerTotalTokens > maxReliable)
                {
                    return true;
                }
            }

            if (estimatedPromptTokens >= 120 && providerPromptTokens > 0 && providerPromptTokens < estimatedPromptTokens * 0.3f)
            {
                return true;
            }

            if (estimatedCompletionTokens >= 120 && providerCompletionTokens > 0 && providerCompletionTokens < estimatedCompletionTokens * 0.3f)
            {
                return true;
            }

            return false;
        }

        int UpdateAnomalyStreak(bool hasUsage, bool providerLooksAbnormal)
        {
            lock (gate)
            {
                if (!hasUsage)
                {
                    providerUsageAnomalyStreak = 0;
                    return providerUsageAnomalyStreak;
                }

                providerUsageAnomalyStreak = providerLooksAbnormal
                    ? providerUsageAnomalyStreak + 1
                    : 0;
                return providerUsageAnomalyStreak;
            }
        }

        static bool TryExtractUsageCore(string source, out int promptTokens, out int completionTokens, out int totalTokens)
        {
            promptTokens = 0;
            completionTokens = 0;
            totalTokens = 0;

            bool promptOk = TryExtractIntByRegexes(PromptTokensRegexes, source, out promptTokens);
            bool completionOk = TryExtractIntByRegexes(CompletionTokensRegexes, source, out completionTokens);
            bool totalOk = TryExtractIntByRegexes(TotalTokensRegexes, source, out totalTokens);
            if (totalOk && totalTokens > 0)
            {
                return true;
            }

            if (promptOk && completionOk)
            {
                totalTokens = promptTokens + completionTokens;
                return totalTokens > 0;
            }

            return false;
        }

        static bool TryExtractUsageObject(string json, out string usageObject)
        {
            usageObject = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            int usageKeyIndex = json.IndexOf("\"usage\"", StringComparison.OrdinalIgnoreCase);
            if (usageKeyIndex < 0)
            {
                return false;
            }

            int colonIndex = json.IndexOf(':', usageKeyIndex);
            if (colonIndex < 0 || colonIndex + 1 >= json.Length)
            {
                return false;
            }

            int objectStart = colonIndex + 1;
            while (objectStart < json.Length && char.IsWhiteSpace(json[objectStart]))
            {
                objectStart++;
            }

            if (objectStart >= json.Length || json[objectStart] != '{')
            {
                return false;
            }

            int objectEnd = FindMatchingClosingBrace(json, objectStart);
            if (objectEnd <= objectStart)
            {
                return false;
            }

            usageObject = json.Substring(objectStart, objectEnd - objectStart + 1);
            return usageObject.Length > 2;
        }

        static int FindMatchingClosingBrace(string source, int startBraceIndex)
        {
            int depth = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = startBraceIndex; i < source.Length; i++)
            {
                char c = source[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (c == '\\')
                    {
                        escaped = true;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return i;
                    }

                    if (depth < 0)
                    {
                        return -1;
                    }
                }
            }

            return -1;
        }

        static bool TryExtractIntByRegexes(Regex[] regexes, string source, out int value)
        {
            value = 0;
            if (regexes == null || regexes.Length == 0 || string.IsNullOrEmpty(source))
            {
                return false;
            }

            for (int i = 0; i < regexes.Length; i++)
            {
                if (TryExtractIntByRegex(regexes[i], source, out value))
                {
                    return true;
                }
            }

            return false;
        }

        static bool TryExtractIntByRegex(Regex regex, string source, out int value)
        {
            value = 0;
            if (regex == null || string.IsNullOrEmpty(source))
            {
                return false;
            }

            Match match = regex.Match(source);
            if (!match.Success || match.Groups.Count < 2)
            {
                return false;
            }

            return int.TryParse(match.Groups[1].Value, out value);
        }
    }
}
