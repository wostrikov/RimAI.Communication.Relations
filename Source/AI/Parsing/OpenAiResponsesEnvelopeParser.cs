using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Ustas.RimAI.Communication.Relations.AI
{
    /// <summary>
    /// OpenAI Responses envelope → text. No Relations domain parsing.
    /// </summary>
    public static class OpenAiResponsesEnvelopeParser
    {
        private static readonly Regex ShallowObject =
            new Regex("\\{(?<object>(?:[^{}\\\"]|\\\"(?:[^\\\"\\\\]|\\\\.)*\\\")*)\\}", RegexOptions.Singleline);

        private static readonly Regex OutputTextType =
            new Regex("\\\"type\\\"\\s*:\\s*\\\"output_text\\\"", RegexOptions.IgnoreCase);

        private static readonly Regex TextField =
            new Regex("\\\"text\\\"\\s*:\\s*\\\"(?<v>(?:[^\\\"\\\\]|\\\\.)*)\\\"", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex ConvenienceOutputText =
            new Regex("\\\"output_text\\\"\\s*:\\s*\\\"(?<v>(?:[^\\\"\\\\]|\\\\.)*)\\\"", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex IncompleteStatus =
            new Regex("\\\"status\\\"\\s*:\\s*\\\"incomplete\\\"", RegexOptions.IgnoreCase);

        private static readonly Regex IncompleteReason =
            new Regex(
                "\\\"incomplete_details\\\"\\s*:\\s*\\{[^{}]*\\\"reason\\\"\\s*:\\s*\\\"(?<v>(?:[^\\\"\\\\]|\\\\.)*)\\\"",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

        public static string ExtractOutputText(string json)
        {
            ProviderTextResult result = Parse(json);
            return result.Success ? result.Text : string.Empty;
        }

        public static ProviderTextResult Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return ProviderTextResult.Fail(ProviderTextErrorKind.Empty, "invalid_payload");
            }

            var values = new List<string>();
            MatchCollection objects = ShallowObject.Matches(json);
            foreach (Match match in objects)
            {
                string item = match.Groups["object"].Value;
                if (!OutputTextType.IsMatch(item))
                {
                    continue;
                }

                Match text = TextField.Match(item);
                if (text.Success)
                {
                    values.Add(Unescape(text.Groups["v"].Value));
                }
            }

            if (values.Count == 0)
            {
                Match convenience = ConvenienceOutputText.Match(json);
                if (convenience.Success)
                {
                    values.Add(Unescape(convenience.Groups["v"].Value));
                }
            }

            string joined = string.Join(" ", values).Trim();
            if (string.IsNullOrWhiteSpace(joined))
            {
                // Only now. Every Responses reply carries an "error" field, null on
                // success, so an error marker must never outrank text the model
                // actually produced.
                if (CompatibleChatEnvelopeParser.IsErrorPayload(json))
                {
                    return ProviderTextResult.Fail(ProviderTextErrorKind.ErrorEnvelope, "error_payload");
                }

                if (IncompleteStatus.IsMatch(json))
                {
                    Match reason = IncompleteReason.Match(json);
                    string reasonValue = reason.Success ? Unescape(reason.Groups["v"].Value) : string.Empty;
                    string reasonTag = string.Equals(reasonValue, "max_output_tokens", StringComparison.OrdinalIgnoreCase)
                        ? "incomplete_max_output_tokens"
                        : "incomplete_response";
                    return ProviderTextResult.Fail(
                        ProviderTextErrorKind.Empty,
                        reasonTag,
                        reason.Success ? "incomplete_details.reason" : "status");
                }

                return ProviderTextResult.Fail(ProviderTextErrorKind.Empty, "no_output_text");
            }

            return ProviderTextResult.Ok(joined, "output[].content[].output_text");
        }

        static string Unescape(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\\t", "\t")
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");
        }
    }
}
