using System;
using System.Collections.Generic;

namespace Ustas.RimAI.Communication.Relations.AI
{
    /// <summary>
    /// Boundary between provider envelope normalization and Relations domain parsing.
    /// </summary>
    internal static class RelationsProviderTextExtractor
    {
        public static PrimaryTextExtractionResult Extract(string json, AIProvider provider)
        {
            if (string.IsNullOrEmpty(json))
            {
                return ProviderTextResult.Fail(ProviderTextErrorKind.Empty, "invalid_payload").ToExtractionResult();
            }

            try
            {
                ProviderTextResult parsed = provider == AIProvider.OpenAI
                    ? ParseOpenAi(json)
                    : ParseCompatible(json);
                return parsed.ToExtractionResult();
            }
            catch (Exception)
            {
                return ProviderTextResult.Fail(ProviderTextErrorKind.Malformed, "extractor_exception").ToExtractionResult();
            }
        }

        public static bool IsRetryableEmptyPrimaryText(string reasonTag)
        {
            return string.Equals(reasonTag, "empty_primary_text", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(reasonTag, "assistant_role_without_content", StringComparison.OrdinalIgnoreCase);
        }

        static ProviderTextResult ParseOpenAi(string json)
        {
            if (SseFrameReader.LooksLikeSse(json))
            {
                return ParseOpenAiSse(json);
            }

            return OpenAiResponsesEnvelopeParser.Parse(json);
        }

        static ProviderTextResult ParseOpenAiSse(string json)
        {
            List<string> dataPayloads = SseFrameReader.EnumerateDataPayloads(json);
            var segments = new List<string>();
            for (int i = 0; i < dataPayloads.Count; i++)
            {
                ProviderTextResult chunk = OpenAiResponsesEnvelopeParser.Parse(dataPayloads[i]);
                if (chunk.Success && !string.IsNullOrWhiteSpace(chunk.Text))
                {
                    segments.Add(chunk.Text.Trim());
                }
            }

            if (segments.Count == 0)
            {
                return ProviderTextResult.Fail(ProviderTextErrorKind.Empty, "sse_no_extractable_text", "sse.data");
            }

            return ProviderTextResult.Ok(string.Join(" ", segments).Trim(), "sse.data", isStreamingFinal: true);
        }

        static ProviderTextResult ParseCompatible(string json)
        {
            if (CompatibleChatEnvelopeParser.IsErrorPayload(json))
            {
                return ProviderTextResult.Fail(ProviderTextErrorKind.ErrorEnvelope, "error_payload");
            }

            return CompatibleChatEnvelopeParser.Parse(json);
        }
    }
}
