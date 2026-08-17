using System;

namespace Ustas.RimAI.Communication.Relations.AI
{
    /// <summary>
    /// Generic provider-envelope text extraction. Internals (SSE framing, envelope
    /// normalization, stream chunks) are deferred to the JSON/SSE checkpoint; this
    /// class only preserves the current extraction behavior behind one owner.
    /// </summary>
    internal static class RelationsProviderTextExtractor
    {
        public static PrimaryTextExtractionResult Extract(string json, AIProvider provider)
        {
            if (string.IsNullOrEmpty(json))
            {
                return new PrimaryTextExtractionResult
                {
                    IsSuccess = false,
                    Content = string.Empty,
                    ReasonTag = "invalid_payload",
                    MatchedPath = string.Empty
                };
            }

            try
            {
                if (provider == AIProvider.OpenAI)
                {
                    string outputText = OpenAIProviderAdapter.ParseOutputText(json);
                    return string.IsNullOrWhiteSpace(outputText)
                        ? new PrimaryTextExtractionResult { IsSuccess = false, Content = string.Empty, ReasonTag = "no_output_text", MatchedPath = string.Empty }
                        : new PrimaryTextExtractionResult { IsSuccess = true, Content = outputText, ReasonTag = "ok", MatchedPath = "output[].content[].output_text" };
                }

                if (AIJsonContentExtractor.IsErrorPayload(json))
                {
                    return new PrimaryTextExtractionResult
                    {
                        IsSuccess = false,
                        Content = string.Empty,
                        ReasonTag = "error_payload",
                        MatchedPath = string.Empty
                    };
                }

                return AIJsonContentExtractor.TryExtractPrimaryText(json);
            }
            catch (Exception)
            {
                return new PrimaryTextExtractionResult
                {
                    IsSuccess = false,
                    Content = string.Empty,
                    ReasonTag = "extractor_exception",
                    MatchedPath = string.Empty
                };
            }
        }

        public static bool IsRetryableEmptyPrimaryText(string reasonTag)
        {
            return string.Equals(reasonTag, "empty_primary_text", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(reasonTag, "assistant_role_without_content", StringComparison.OrdinalIgnoreCase);
        }
    }
}
