using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using Verse;

namespace Ustas.RimAI.Communication.Relations.AI
{
    /// <summary>
    /// Owns Relations request-body construction and outgoing message normalization.
    /// Does not send HTTP, choose the global provider catalog, or validate domain contracts.
    /// </summary>
    internal static class RelationsTextAiRequestBuilder
    {
        public const string MinimalUserFollowSystemPrompt =
            "Please follow the system instructions and provide the requested output in plain text.";

        public static List<ChatMessageData> Normalize(List<ChatMessageData> source, DialogueUsageChannel usageChannel)
        {
            List<ChatMessageData> normalized = CollectNormalizedMessages(source);
            if (normalized.Count > 0 && !HasUserMessage(normalized))
            {
                normalized.Add(new ChatMessageData
                {
                    role = "user",
                    content = MinimalUserFollowSystemPrompt
                });
            }

            return normalized;
        }

        public static List<ChatMessageData> Clone(List<ChatMessageData> source)
        {
            var result = new List<ChatMessageData>();
            if (source == null)
            {
                return result;
            }

            for (int i = 0; i < source.Count; i++)
            {
                ChatMessageData msg = source[i];
                if (msg == null)
                {
                    continue;
                }

                result.Add(new ChatMessageData
                {
                    role = msg.role ?? string.Empty,
                    content = msg.content ?? string.Empty
                });
            }

            return result;
        }

        public static string BuildChatCompletionJson(string model, List<ChatMessageData> messages, ApiConfig config)
        {
            RelationsSettings globalSettings = RelationsMod.Settings;
            int configuredMaxTokens = globalSettings?.MaxTokens ?? 2048;
            if (configuredMaxTokens < 64) configuredMaxTokens = 2048;
            if (config.Provider == AIProvider.OpenAI)
            {
                return OpenAIProviderAdapter.BuildResponsesRequest(model, messages, configuredMaxTokens);
            }

            var sb = new StringBuilder();
            sb.Append("{");

            if (config.Provider != AIProvider.Player2)
            {
                sb.Append($"\"model\":\"{EscapeJson(model)}\",");
            }

            sb.Append("\"messages\":[");

            for (int i = 0; i < messages.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append("{");
                sb.Append($"\"role\":\"{EscapeJson(messages[i].role)}\",");
                sb.Append($"\"content\":\"{EscapeJson(messages[i].content)}\"");
                sb.Append("}");
            }

            sb.Append("],");

            bool thinkingEnabled = globalSettings?.ThinkingEnabled ?? false;
            bool isDeepSeek = config.Provider == AIProvider.DeepSeek;
            float temperature = globalSettings?.Temperature ?? 0.5f;
            int maxTokens = configuredMaxTokens;
            if (maxTokens < 64) maxTokens = 2048;

            if (!thinkingEnabled || !isDeepSeek)
            {
                sb.Append($"\"temperature\":{temperature.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)},");
            }
            sb.Append($"\"max_tokens\":{maxTokens}");

            if (thinkingEnabled)
            {
                string reasoningEffort = globalSettings?.ReasoningEffort ?? "medium";
                sb.Append(",\"thinking\":{\"type\":\"enabled\"}");
                if (!string.IsNullOrEmpty(reasoningEffort))
                {
                    sb.Append($",\"reasoning_effort\":\"{EscapeJson(reasoningEffort)}\"");
                }
            }
            else if (isDeepSeek)
            {
                sb.Append(",\"thinking\":{\"type\":\"disabled\"}");
            }
            sb.Append("}");

            return sb.ToString();
        }

        public static string BuildChatCompletionJsonForProvider(
            string model,
            List<ChatMessageData> messages,
            AIProvider provider,
            int maxTokens = 2048,
            float temperature = 0.5f,
            bool thinkingEnabled = false,
            string reasoningEffort = "medium")
        {
            if (maxTokens < 64) maxTokens = 2048;
            if (provider == AIProvider.OpenAI)
            {
                return OpenAIProviderAdapter.BuildResponsesRequest(model, messages, maxTokens);
            }

            var sb = new StringBuilder();
            sb.Append("{");
            if (provider != AIProvider.Player2)
            {
                sb.Append($"\"model\":\"{EscapeJson(model)}\",");
            }

            sb.Append("\"messages\":[");
            for (int i = 0; i < messages.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append("{");
                sb.Append($"\"role\":\"{EscapeJson(messages[i].role)}\",");
                sb.Append($"\"content\":\"{EscapeJson(messages[i].content)}\"");
                sb.Append("}");
            }

            sb.Append("],");
            bool isDeepSeek = provider == AIProvider.DeepSeek;
            if (!thinkingEnabled || !isDeepSeek)
            {
                sb.Append($"\"temperature\":{temperature.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)},");
            }
            sb.Append($"\"max_tokens\":{maxTokens}");
            if (thinkingEnabled)
            {
                sb.Append(",\"thinking\":{\"type\":\"enabled\"}");
                if (!string.IsNullOrEmpty(reasoningEffort))
                {
                    sb.Append($",\"reasoning_effort\":\"{EscapeJson(reasoningEffort)}\"");
                }
            }
            else if (isDeepSeek)
            {
                sb.Append(",\"thinking\":{\"type\":\"disabled\"}");
            }
            sb.Append("}");
            return sb.ToString();
        }

        public static bool ValidateUrl(string url, out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(url))
            {
                error = "RimChat_ErrorEmptyUrl".Translate();
                return false;
            }

            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                error = "RimChat_ErrorInvalidUrl".Translate();
                return false;
            }

            try
            {
                var uri = new Uri(url);
                if (!uri.IsWellFormedOriginalString())
                {
                    error = "RimChat_ErrorMalformedUrl".Translate();
                    return false;
                }
            }
            catch (UriFormatException)
            {
                error = "RimChat_ErrorMalformedUrl".Translate();
                return false;
            }

            return true;
        }

        public static bool ValidateUrlShape(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                return false;
            }

            try
            {
                var uri = new Uri(url);
                return uri.IsWellFormedOriginalString();
            }
            catch (UriFormatException)
            {
                return false;
            }
        }

        public static string EscapeJson(string str)
        {
            return Ustas.RimAI.Core.AI.JsonStringCodec.Escape(str);
        }

        static List<ChatMessageData> CollectNormalizedMessages(List<ChatMessageData> source)
        {
            var normalized = new List<ChatMessageData>();
            if (source == null)
            {
                return normalized;
            }

            for (int i = 0; i < source.Count; i++)
            {
                ChatMessageData msg = source[i];
                if (msg == null)
                {
                    continue;
                }

                normalized.Add(new ChatMessageData
                {
                    role = NormalizeOutgoingRole(msg.role),
                    content = msg.content ?? string.Empty
                });
            }

            return normalized;
        }

        static bool HasUserMessage(List<ChatMessageData> messages)
        {
            return messages != null && messages.Any(msg =>
                msg != null &&
                string.Equals(msg.role, "user", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(msg.content));
        }

        static string NormalizeOutgoingRole(string role)
        {
            if (string.IsNullOrWhiteSpace(role))
            {
                return "user";
            }

            string trimmed = role.Trim();
            if (string.Equals(trimmed, "system", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "user", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "assistant", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed.ToLowerInvariant();
            }

            return "user";
        }
    }
}
