using System;

namespace Ustas.RimAI.Communication.Relations.AI
{
    /// <summary>
    /// Local-provider transport retry policy. Distinct from Core TextAiRetryPolicy
    /// (generic provider retry) and from RelationsSemanticRetry (domain/contract retry).
    /// </summary>
    internal static class RelationsLocalProviderRetry
    {
        public const int LocalServerMaxAttempts = 3;
        public const int LocalConnectionMaxAttempts = 2;

        public static bool ShouldRetryLocalServerError(bool isLocalModel, long responseCode, int local5xxRetryCount)
        {
            if (!isLocalModel || !IsRetryableLocalServerStatus(responseCode))
            {
                return false;
            }

            return local5xxRetryCount < LocalServerMaxAttempts - 1;
        }

        public static bool IsRetryableLocalServerStatus(long responseCode)
        {
            return responseCode == 500 ||
                   responseCode == 502 ||
                   responseCode == 503 ||
                   responseCode == 504;
        }

        public static float GetLocalServerRetryDelaySeconds(int retryIndex, float jitter)
        {
            float baseDelay = retryIndex <= 1 ? 0.35f : 1.10f;
            return baseDelay + jitter;
        }

        public static bool ShouldRetryLocalConnectionError(
            bool isLocalModel,
            AIRequestDebugSource debugSource,
            string requestError,
            int localConnectionRetryCount)
        {
            if (!isLocalModel || localConnectionRetryCount >= LocalConnectionMaxAttempts - 1)
            {
                return false;
            }

            if (debugSource == AIRequestDebugSource.AirdropSelection)
            {
                return false;
            }

            return LooksLikeTimeoutError(requestError) ||
                   ContainsErrorToken(requestError, "connection reset") ||
                   ContainsErrorToken(requestError, "connection aborted") ||
                   ContainsErrorToken(requestError, "unexpected eof");
        }

        public static float GetLocalConnectionRetryDelaySeconds(int retryIndex, float jitter)
        {
            float baseDelay = retryIndex <= 1 ? 0.5f : 1.2f;
            return baseDelay + jitter;
        }

        public static bool LooksLikeTimeoutError(string requestError)
        {
            return ContainsErrorToken(requestError, "timeout") ||
                   ContainsErrorToken(requestError, "timed out");
        }

        public static bool ContainsErrorToken(string value, string token)
        {
            string source = value ?? string.Empty;
            if (source.Length == 0 || string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            return source.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static string GetUrlHostPort(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            try
            {
                var uri = new Uri(url);
                return uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
            }
            catch
            {
                return "invalid-url";
            }
        }

        public static string BuildResponsePreviewForLog(string responseText, int maxChars)
        {
            string raw = responseText ?? string.Empty;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return "<empty>";
            }

            string singleLine = raw
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Trim();
            if (maxChars <= 0 || singleLine.Length <= maxChars)
            {
                return singleLine;
            }

            return singleLine.Substring(0, maxChars) + "...";
        }
    }
}
