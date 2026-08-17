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

            if (CompatibleChatEnvelopeParser.IsErrorPayload(json))
            {
                return ProviderTextResult.Fail(ProviderTextErrorKind.ErrorEnvelope, "error_payload");
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
