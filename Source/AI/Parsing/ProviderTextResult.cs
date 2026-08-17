namespace Ustas.RimAI.Communication.Relations.AI
{
    public sealed class PrimaryTextExtractionResult
    {
        public bool IsSuccess { get; set; }
        public string Content { get; set; } = string.Empty;
        public string ReasonTag { get; set; } = "unknown";
        public string MatchedPath { get; set; } = string.Empty;
    }

    public enum ProviderTextErrorKind
    {
        None = 0,
        Empty = 1,
        Malformed = 2,
        ErrorEnvelope = 3
    }

    /// <summary>
    /// Provider-neutral transport/content output. No Relations domain fields.
    /// </summary>
    public sealed class ProviderTextResult
    {
        public bool Success { get; set; }
        public string Text { get; set; } = string.Empty;
        public ProviderTextErrorKind ErrorKind { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string ReasonTag { get; set; } = "unknown";
        public string MatchedPath { get; set; } = string.Empty;
        public bool IsStreamingFinal { get; set; }

        public static ProviderTextResult Ok(string text, string matchedPath, bool isStreamingFinal = false)
        {
            return new ProviderTextResult
            {
                Success = true,
                Text = text ?? string.Empty,
                ErrorKind = ProviderTextErrorKind.None,
                ReasonTag = "ok",
                MatchedPath = matchedPath ?? string.Empty,
                IsStreamingFinal = isStreamingFinal
            };
        }

        public static ProviderTextResult Fail(
            ProviderTextErrorKind kind,
            string reasonTag,
            string matchedPath = "",
            string errorMessage = "")
        {
            return new ProviderTextResult
            {
                Success = false,
                Text = string.Empty,
                ErrorKind = kind,
                ReasonTag = string.IsNullOrWhiteSpace(reasonTag) ? "unknown" : reasonTag,
                MatchedPath = matchedPath ?? string.Empty,
                ErrorMessage = errorMessage ?? string.Empty
            };
        }

        public PrimaryTextExtractionResult ToExtractionResult()
        {
            return new PrimaryTextExtractionResult
            {
                IsSuccess = Success,
                Content = Text ?? string.Empty,
                ReasonTag = ReasonTag ?? "unknown",
                MatchedPath = MatchedPath ?? string.Empty
            };
        }
    }
}
