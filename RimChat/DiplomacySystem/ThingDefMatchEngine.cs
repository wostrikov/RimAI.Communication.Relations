using System;
using System.Collections.Generic;
using System.Linq;

namespace RimChat.DiplomacySystem
{
    internal sealed class ThingDefMatchRequest
    {
        public string Query { get; set; } = string.Empty;
        public IReadOnlyCollection<string> Tokens { get; set; } = Array.Empty<string>();
        public IReadOnlyCollection<string> Aliases { get; set; } = Array.Empty<string>();
        public IReadOnlyCollection<string> SemanticTokens { get; set; } = Array.Empty<string>();
        public IReadOnlyCollection<string> NormalizedTokens { get; set; } = Array.Empty<string>();
        public IReadOnlyCollection<string> NormalizedAliases { get; set; } = Array.Empty<string>();
        public int MinScore { get; set; } = 1;
        public int MaxResults { get; set; } = 6;
    }

    internal sealed class ThingDefMatchCandidate
    {
        public ThingDefRecord Record { get; set; }
        public int Score { get; set; }
        public List<string> Breakdown { get; set; } = new List<string>();
    }

    internal sealed class ThingDefMatchResult
    {
        public bool Success { get; set; }
        public bool IsAmbiguous { get; set; }
        public ThingDefMatchCandidate BestCandidate { get; set; }
        public List<ThingDefMatchCandidate> Candidates { get; set; } = new List<ThingDefMatchCandidate>();
    }

    internal static class ThingDefMatchEngine
    {
        public static IReadOnlyList<ThingDefMatchCandidate> RankCandidates(
            IReadOnlyList<ThingDefRecord> records,
            ThingDefMatchRequest request)
        {
            if (records == null || records.Count == 0 || request == null)
            {
                return Array.Empty<ThingDefMatchCandidate>();
            }

            int minScore = Math.Max(1, request.MinScore);
            var candidates = new List<ThingDefMatchCandidate>(records.Count);
            for (int i = 0; i < records.Count; i++)
            {
                ThingDefMatchCandidate candidate = ScoreRecord(records[i], request);
                if (candidate != null && candidate.Score >= minScore)
                {
                    candidates.Add(candidate);
                }
            }

            candidates.Sort((a, b) =>
            {
                int scoreCompare = b.Score.CompareTo(a.Score);
                if (scoreCompare != 0) return scoreCompare;
                float marketA = a.Record?.MarketValue ?? 0f;
                float marketB = b.Record?.MarketValue ?? 0f;
                int marketCompare = marketB.CompareTo(marketA);
                if (marketCompare != 0) return marketCompare;
                return string.Compare(
                    a.Record?.DefName ?? string.Empty,
                    b.Record?.DefName ?? string.Empty,
                    StringComparison.Ordinal);
            });

            int limit = Math.Max(1, request.MaxResults);
            if (candidates.Count > limit)
            {
                candidates.RemoveRange(limit, candidates.Count - limit);
            }

            return candidates;
        }

        public static ThingDefMatchResult ResolveSingle(
            IReadOnlyList<ThingDefRecord> records,
            ThingDefMatchRequest request,
            int ambiguityWindow = 0)
        {
            IReadOnlyList<ThingDefMatchCandidate> ranked = RankCandidates(records, request);
            if (ranked.Count == 0)
            {
                return new ThingDefMatchResult();
            }

            ThingDefMatchCandidate best = ranked[0];
            bool ambiguous = ranked.Count > 1 && ranked[1].Score >= best.Score - Math.Max(0, ambiguityWindow);
            return new ThingDefMatchResult
            {
                Success = !ambiguous,
                IsAmbiguous = ambiguous,
                BestCandidate = ambiguous ? null : best,
                Candidates = ranked.Take(3).ToList()
            };
        }

        public static ThingDefMatchCandidate ScoreRecord(ThingDefRecord record, ThingDefMatchRequest request)
        {
            if (record?.Def == null || request == null)
            {
                return null;
            }

            string query = (request.Query ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                return null;
            }

            string rawQuery = query.ToLowerInvariant();
            string normalizedQuery = NormalizeToken(query);
            string normalizedDef = record.NormalizedDefName ?? NormalizeToken(record.DefName);
            string normalizedLabel = record.NormalizedLabel ?? NormalizeToken(record.Label);
            string search = (record.SearchText ?? string.Empty).ToLowerInvariant();
            HashSet<string> semanticTargetTokens = record.SemanticTokens ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (record.SemanticTokens == null)
            {
                semanticTargetTokens.UnionWith(ExtractSemanticTokens(record.DefName));
                semanticTargetTokens.UnionWith(ExtractSemanticTokens(record.Label));
                semanticTargetTokens.UnionWith(ExtractSemanticTokens(record.SearchText));
            }

            int score = 0;
            var breakdown = new List<string>();
            var aliasSet = new HashSet<string>(request.Aliases ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            if (!aliasSet.Contains(query))
            {
                aliasSet.Add(query);
            }

            if (string.Equals(record.DefName, query, StringComparison.OrdinalIgnoreCase))
            {
                score += 1000;
                breakdown.Add("exact_def");
            }

            if (string.Equals(record.Label, query, StringComparison.OrdinalIgnoreCase))
            {
                score += 920;
                breakdown.Add("exact_label");
            }

            if (!string.IsNullOrWhiteSpace(normalizedQuery) && string.Equals(normalizedDef, normalizedQuery, StringComparison.OrdinalIgnoreCase))
            {
                score += 820;
                breakdown.Add("normalized_def");
            }

            if (!string.IsNullOrWhiteSpace(normalizedQuery) && string.Equals(normalizedLabel, normalizedQuery, StringComparison.OrdinalIgnoreCase))
            {
                score += 760;
                breakdown.Add("normalized_label");
            }

            IReadOnlyCollection<string> normalizedAliases = request.NormalizedAliases ?? request.Aliases;
            int aliasIdx = 0;
            foreach (string alias in aliasSet)
            {
                if (string.IsNullOrWhiteSpace(alias))
                {
                    aliasIdx++;
                    continue;
                }

                string normalizedAlias = aliasIdx < normalizedAliases.Count
                    ? ((List<string>)normalizedAliases)[aliasIdx]
                    : NormalizeToken(alias);
                aliasIdx++;

                if (string.Equals(record.DefName, alias, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(normalizedAlias) && string.Equals(normalizedDef, normalizedAlias, StringComparison.OrdinalIgnoreCase)))
                {
                    score += 520;
                    breakdown.Add("alias_def");
                    break;
                }

                if (string.Equals(record.Label, alias, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(normalizedAlias) && string.Equals(normalizedLabel, normalizedAlias, StringComparison.OrdinalIgnoreCase)))
                {
                    score += 480;
                    breakdown.Add("alias_label");
                    break;
                }
            }

            HashSet<string> requestSemanticTokens = new HashSet<string>(request.SemanticTokens ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            if (requestSemanticTokens.Count == 0)
            {
                requestSemanticTokens = ExtractSemanticTokens(query);
            }

            if (requestSemanticTokens.Count >= 2 && requestSemanticTokens.All(token => semanticTargetTokens.Contains(token)))
            {
                score += 340;
                breakdown.Add("semantic_all");
            }

            // Word-boundary-based contains check: prevents false positives like "steel" in "Plasteel".
            if (!string.IsNullOrWhiteSpace(normalizedQuery))
            {
                bool boundaryMatch = ContainsWithWordBoundary(semanticTargetTokens, normalizedQuery);
                if (!boundaryMatch && normalizedQuery.Length < 4)
                {
                    // Short query fallback: keep original Contains behavior at reduced score.
                    boundaryMatch = normalizedDef.Contains(normalizedQuery) || normalizedQuery.Contains(normalizedDef) ||
                                    normalizedLabel.Contains(normalizedQuery) || normalizedQuery.Contains(normalizedLabel);
                    if (boundaryMatch)
                    {
                        score += 80;
                        breakdown.Add("normalized_contains_fb");
                    }
                }
                else if (boundaryMatch)
                {
                    score += 260;
                    breakdown.Add("normalized_contains");
                }
            }

            if (search.Contains(rawQuery))
            {
                score += 220;
                breakdown.Add("search_query");
            }

            IReadOnlyCollection<string> normalizedTokens = request.NormalizedTokens ?? request.Tokens;
            int tokenCoverage = 0;
            int tokenIdx = 0;
            foreach (string token in request.Tokens ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(token) || (token.Length < 2 && !IsCjkToken(token)))
                {
                    tokenIdx++;
                    continue;
                }

                string normalizedToken = tokenIdx < normalizedTokens.Count
                    ? ((List<string>)normalizedTokens)[tokenIdx]
                    : NormalizeToken(token);
                tokenIdx++;

                bool tokenMatched = false;
                // Word-boundary match: token appears as a complete semantic token in the target.
                if (!string.IsNullOrWhiteSpace(normalizedToken) &&
                    ContainsWithWordBoundary(semanticTargetTokens, normalizedToken))
                {
                    score += 120;
                    tokenCoverage++;
                    tokenMatched = true;
                }

                // Fallback: substring match on normalized def/label (reduced score to limit false positives).
                if (!tokenMatched && !string.IsNullOrWhiteSpace(normalizedToken) &&
                    (normalizedDef.Contains(normalizedToken) || normalizedLabel.Contains(normalizedToken)))
                {
                    score += 40;
                    tokenMatched = true;
                }

                if (!tokenMatched && search.Contains(token.ToLowerInvariant()))
                {
                    score += 72;
                    tokenMatched = true;
                }

                if (!tokenMatched)
                {
                    int overlap = semanticTargetTokens.Contains(token) || (!string.IsNullOrWhiteSpace(normalizedToken) && semanticTargetTokens.Contains(normalizedToken))
                        ? 1
                        : 0;
                    if (overlap > 0)
                    {
                        score += 52;
                    }
                }
            }

            if ((request.Tokens?.Count ?? 0) > 0 && tokenCoverage == request.Tokens.Count)
            {
                score += 110;
                breakdown.Add("token_full_cover");
            }

            score += ScoreNearMatch(normalizedQuery, normalizedDef, 90, breakdown, "near_def");
            score += ScoreNearMatch(normalizedQuery, normalizedLabel, 76, breakdown, "near_label");
            if (score <= 0)
            {
                return null;
            }

            return new ThingDefMatchCandidate
            {
                Record = record,
                Score = score,
                Breakdown = breakdown.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            };
        }

        public static string NormalizeToken(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var sb = new System.Text.StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (!char.IsWhiteSpace(c) && c != '_' && c != '-' &&
                    c != '/' && c != '\\' && c != '(' && c != ')' &&
                    c != '[' && c != ']' && c != '{' && c != '}' &&
                    c != ',' && c != '.' && c != ':' && c != ';' && c != '|')
                {
                    sb.Append(c);
                }
            }

            return sb.ToString().ToLowerInvariant();
        }

        private static readonly char[] SemanticTokenSeparators =
        {
            ' ', '\t', '\r', '\n', '_', '-', '/', '\\', ',', '.', ':', ';', '|', '(', ')', '[', ']', '{', '}'
        };

        public static HashSet<string> ExtractSemanticTokens(string text)
        {
            var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(text))
            {
                return tokens;
            }

            string expanded = ExpandCamelCase(text).ToLowerInvariant();

            foreach (string part in expanded.Split(SemanticTokenSeparators, StringSplitOptions.RemoveEmptyEntries))
            {
                string token = part.Trim();
                if (token.Length >= 2)
                {
                    tokens.Add(token);
                }
            }

            return tokens;
        }

        public static string ExpandCamelCase(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var chars = new List<char>(text.Length * 2);
            for (int i = 0; i < text.Length; i++)
            {
                char current = text[i];
                if (i > 0 &&
                    char.IsUpper(current) &&
                    (char.IsLower(text[i - 1]) || char.IsDigit(text[i - 1])))
                {
                    chars.Add(' ');
                }

                chars.Add(current);
            }

            return new string(chars.ToArray());
        }

        private static bool IsCjkToken(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }

            for (int i = 0; i < token.Length; i++)
            {
                char ch = token[i];
                if ((ch >= 0x4E00 && ch <= 0x9FFF) ||
                    (ch >= 0x3400 && ch <= 0x4DBF) ||
                    (ch >= 0xF900 && ch <= 0xFAFF) ||
                    (ch >= 0x2E80 && ch <= 0x2EFF))
                {
                    return true;
                }
            }

            return false;
        }

        // Checks whether needle is a word-boundary token in haystack's semantic tokens.
        private static bool ContainsWithWordBoundary(HashSet<string> semanticTokens, string normalizedNeedle)
        {
            if (string.IsNullOrWhiteSpace(normalizedNeedle) || semanticTokens == null || semanticTokens.Count == 0)
            {
                return false;
            }

            return semanticTokens.Contains(normalizedNeedle);
        }

        private static int ScoreNearMatch(string normalizedQuery, string normalizedTarget, int maxScore, List<string> breakdown, string tag)
        {
            if (string.IsNullOrWhiteSpace(normalizedQuery) || string.IsNullOrWhiteSpace(normalizedTarget))
            {
                return 0;
            }

            int maxLength = Math.Max(normalizedQuery.Length, normalizedTarget.Length);
            if (maxLength < 4)
            {
                return 0;
            }

            int distance = ComputeLevenshteinDistance(normalizedQuery, normalizedTarget);
            if (distance == 1)
            {
                breakdown.Add(tag);
                return maxScore;
            }

            if (distance == 2 && maxLength >= 5)
            {
                breakdown.Add(tag);
                return Math.Max(0, maxScore - 24);
            }

            if (distance == 3 && maxLength >= 8)
            {
                breakdown.Add(tag);
                return Math.Max(0, maxScore - 48);
            }

            return 0;
        }

        // Damerau-Levenshtein with transposition support.
        private static int ComputeLevenshteinDistance(string left, string right)
        {
            int leftLength = left.Length;
            int rightLength = right.Length;
            if (leftLength == 0)
            {
                return rightLength;
            }

            if (rightLength == 0)
            {
                return leftLength;
            }

            var prevPrev = new int[rightLength + 1];
            var prev = new int[rightLength + 1];
            var curr = new int[rightLength + 1];
            for (int j = 0; j <= rightLength; j++)
            {
                prev[j] = j;
            }

            for (int i = 1; i <= leftLength; i++)
            {
                curr[0] = i;
                for (int j = 1; j <= rightLength; j++)
                {
                    int cost = left[i - 1] == right[j - 1] ? 0 : 1;
                    int min = Math.Min(
                        Math.Min(prev[j] + 1, curr[j - 1] + 1),
                        prev[j - 1] + cost);

                    // Transposition: swap adjacent characters costs 1 instead of 2.
                    if (i > 1 && j > 1 &&
                        left[i - 1] == right[j - 2] &&
                        left[i - 2] == right[j - 1])
                    {
                        min = Math.Min(min, prevPrev[j - 2] + 1);
                    }

                    curr[j] = min;
                }

                var tmp = prevPrev;
                prevPrev = prev;
                prev = curr;
                curr = tmp;
            }

            return prev[rightLength];
        }
    }
}
