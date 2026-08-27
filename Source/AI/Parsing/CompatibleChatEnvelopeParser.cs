using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Ustas.RimAI.Core.AI;

namespace Ustas.RimAI.Communication.Relations.AI
{
    /// <summary>
    /// Chat-completions-compatible envelope + raw/SSE text extraction.
    /// No Relations dialogue or action semantics.
    /// </summary>
    public static class CompatibleChatEnvelopeParser
    {
        private static readonly string[] CandidateTextKeys =
        {
            "output_text",
            "response",
            "content",
            "text",
            "generated_text",
            "answer",
            "reasoning_content"
        };

        // An OpenAI Responses envelope always carries "error": null on success, so
        // the presence of the key says nothing at all. Only a populated value - an
        // object or array holding something, or a non-empty string - is a real error.
        private static readonly Regex ErrorRegex =
            new Regex(
                "\"error\"\\s*:\\s*(?:\\{\\s*[^\\s}]|\\[\\s*[^\\s\\]]|\"[^\"])",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ContentArrayStartRegex =
            new Regex("\"content\"\\s*:\\s*\\[", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex TextFieldRegex =
            new Regex("\"text\"\\s*:\\s*\"(?<value>(?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Dictionary<string, Regex> KeyRegexCache =
            new Dictionary<string, Regex>(StringComparer.OrdinalIgnoreCase);

        private static readonly object RegexCacheLock = new object();

        public static bool IsErrorPayload(string json)
        {
            return !string.IsNullOrWhiteSpace(json) && ErrorRegex.IsMatch(json);
        }

        public static PrimaryTextExtractionResult Extract(string json)
        {
            return Parse(json).ToExtractionResult();
        }

        public static ProviderTextResult Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return ProviderTextResult.Fail(ProviderTextErrorKind.Malformed, "invalid_payload");
            }

            string trimmed = json.Trim();
            if (SseFrameReader.LooksLikeSse(trimmed))
            {
                return ParseSse(trimmed);
            }

            ProviderTextResult jsonResult = ParseJsonPayload(trimmed);
            if (jsonResult.Success)
            {
                return jsonResult;
            }

            if (!JsonBoundedExtractor.LooksLikeJsonPayload(trimmed))
            {
                string raw = SanitizeText(trimmed);
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    return ProviderTextResult.Ok(raw, "raw_text");
                }
            }

            return jsonResult;
        }

        static ProviderTextResult ParseSse(string payload)
        {
            List<string> dataPayloads = SseFrameReader.EnumerateDataPayloads(payload);
            var segments = new List<string>();
            for (int i = 0; i < dataPayloads.Count; i++)
            {
                string data = dataPayloads[i];
                if (JsonBoundedExtractor.LooksLikeJsonPayload(data))
                {
                    ProviderTextResult chunk = ParseJsonPayload(data);
                    if (chunk.Success && !string.IsNullOrWhiteSpace(chunk.Text))
                    {
                        segments.Add(chunk.Text.Trim());
                    }
                    continue;
                }

                string plainChunk = SanitizeText(data);
                if (!string.IsNullOrWhiteSpace(plainChunk))
                {
                    segments.Add(plainChunk);
                }
            }

            if (segments.Count == 0)
            {
                return ProviderTextResult.Fail(
                    ProviderTextErrorKind.Empty,
                    "sse_no_extractable_text",
                    "sse.data");
            }

            ProviderTextResult ok = ProviderTextResult.Ok(string.Join(" ", segments).Trim(), "sse.data", isStreamingFinal: true);
            return ok;
        }

        static ProviderTextResult ParseJsonPayload(string json)
        {
            var candidates = new List<TextCandidate>();
            CollectStringKeyCandidates(json, candidates);
            CollectContentArrayCandidates(json, candidates);
            if (candidates.Count == 0)
            {
                return ProviderTextResult.Fail(ProviderTextErrorKind.Empty, "no_extractable_text");
            }

            List<TextCandidate> ordered = candidates
                .OrderByDescending(candidate => ScoreMatchCandidate(json, candidate))
                .ThenByDescending(candidate => candidate.Value.Length)
                .ToList();

            for (int i = 0; i < ordered.Count; i++)
            {
                string sanitized = SanitizeText(ordered[i].Value);
                if (string.IsNullOrWhiteSpace(sanitized))
                {
                    continue;
                }

                return ProviderTextResult.Ok(sanitized, ordered[i].Path);
            }

            return ProviderTextResult.Fail(ProviderTextErrorKind.Empty, "empty_primary_text");
        }

        static string SanitizeText(string value)
        {
            return ModelOutputSanitizer.StripReasoningTags(value ?? string.Empty).Trim();
        }

        static void CollectStringKeyCandidates(string json, List<TextCandidate> candidates)
        {
            for (int i = 0; i < CandidateTextKeys.Length; i++)
            {
                CollectStringKeyCandidates(json, CandidateTextKeys[i], candidates);
            }
        }

        static void CollectStringKeyCandidates(string json, string key, List<TextCandidate> candidates)
        {
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            Regex regex = GetKeyRegex(key);
            MatchCollection matches = regex.Matches(json);
            if (matches == null || matches.Count == 0)
            {
                return;
            }

            for (int i = 0; i < matches.Count; i++)
            {
                if (!TryDecodeMatchCandidate(matches[i], out string decoded))
                {
                    continue;
                }

                candidates.Add(new TextCandidate
                {
                    Key = key,
                    Value = decoded,
                    MatchIndex = matches[i].Index,
                    Path = key
                });
            }
        }

        static bool TryDecodeMatchCandidate(Match match, out string candidate)
        {
            candidate = string.Empty;
            if (match == null || !match.Success)
            {
                return false;
            }

            string captured = match.Groups["value"]?.Value ?? string.Empty;
            candidate = JsonStringCodec.Unescape(captured).Trim();
            return !string.IsNullOrWhiteSpace(candidate);
        }

        static void CollectContentArrayCandidates(string json, List<TextCandidate> candidates)
        {
            MatchCollection starts = ContentArrayStartRegex.Matches(json);
            if (starts == null || starts.Count == 0)
            {
                return;
            }

            for (int i = 0; i < starts.Count; i++)
            {
                Match match = starts[i];
                if (match == null || !match.Success)
                {
                    continue;
                }

                int bracketStart = json.IndexOf('[', match.Index);
                if (bracketStart < 0)
                {
                    continue;
                }

                if (!JsonBoundedExtractor.TryExtractArrayAt(json, bracketStart, out string arrayBlock))
                {
                    continue;
                }

                MatchCollection textMatches = TextFieldRegex.Matches(arrayBlock);
                if (textMatches == null || textMatches.Count == 0)
                {
                    continue;
                }

                for (int j = 0; j < textMatches.Count; j++)
                {
                    if (!TryDecodeMatchCandidate(textMatches[j], out string decoded))
                    {
                        continue;
                    }

                    candidates.Add(new TextCandidate
                    {
                        Key = "text",
                        Value = decoded,
                        MatchIndex = match.Index + textMatches[j].Index,
                        Path = "content[].text"
                    });
                }
            }
        }

        static int ScoreMatchCandidate(string json, TextCandidate candidate)
        {
            if (candidate == null)
            {
                return int.MinValue;
            }

            return GetKeyPriorityScore(candidate.Key)
                + GetContextScore(json, candidate.MatchIndex)
                + GetPositionScore(json?.Length ?? 0, candidate.MatchIndex)
                + GetLengthScore(candidate.Value?.Length ?? 0);
        }

        static int GetKeyPriorityScore(string key)
        {
            string keyLower = (key ?? string.Empty).ToLowerInvariant();
            if (keyLower == "output_text" || keyLower == "response")
            {
                return 60;
            }
            if (keyLower == "content")
            {
                return 30;
            }
            if (keyLower == "text")
            {
                return 25;
            }
            return 0;
        }

        static int GetContextScore(string json, int matchIndex)
        {
            int score = 0;
            int start = Math.Max(0, matchIndex - 240);
            int length = Math.Max(0, matchIndex - start);
            string context = length > 0 ? json.Substring(start, length).ToLowerInvariant() : string.Empty;
            if (context.Contains("\"role\"") && context.Contains("assistant"))
            {
                score += 120;
            }
            if (context.Contains("\"role\"") && context.Contains("user"))
            {
                score -= 90;
            }
            if (context.Contains("\"choices\""))
            {
                score += 25;
            }
            if (context.Contains("\"messages\""))
            {
                score -= 20;
            }
            return score;
        }

        static int GetPositionScore(int jsonLength, int matchIndex)
        {
            if (jsonLength <= 0)
            {
                return 0;
            }

            int score = 0;
            int half = jsonLength / 2;
            if (matchIndex >= half)
            {
                score += 20;
            }
            if (matchIndex >= (jsonLength * 3) / 4)
            {
                score += 10;
            }
            return score;
        }

        static int GetLengthScore(int valueLength)
        {
            if (valueLength >= 200)
            {
                return 30;
            }
            if (valueLength >= 80)
            {
                return 20;
            }
            if (valueLength >= 40)
            {
                return 10;
            }
            if (valueLength < 8)
            {
                return -20;
            }
            return 0;
        }

        static Regex GetKeyRegex(string key)
        {
            lock (RegexCacheLock)
            {
                if (KeyRegexCache.TryGetValue(key, out Regex cached))
                {
                    return cached;
                }

                string pattern = $"\"{Regex.Escape(key)}\"\\s*:\\s*\"(?<value>(?:[^\"\\\\]|\\\\.)*)\"";
                var created = new Regex(
                    pattern,
                    RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant);
                KeyRegexCache[key] = created;
                return created;
            }
        }

        sealed class TextCandidate
        {
            public string Key { get; set; } = string.Empty;
            public string Path { get; set; } = string.Empty;
            public string Value { get; set; } = string.Empty;
            public int MatchIndex { get; set; }
        }
    }
}
