using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using Ustas.RimAI.Communication.Relations.Diagnostics;

namespace Ustas.RimAI.Communication.Relations.DiplomacySystem
{
    /// <summary>
    /// Dependencies: none.
    /// Responsibility: tokenize need text and infer airdrop need family.
    /// </summary>
    internal static class ItemAirdropIntentParser
    {
        private static readonly string[] MedicineKeywords =
        {
            "medicine", "medical", "med", "drug", "медицина", "медикаменти", "ліки", "ліки", "ліки", "трави", "бинт", "аптечка"
        };

        private static readonly string[] WeaponKeywords =
        {
            "weapon", "gun", "ammo", "rifle", "melee", "зброя", "рушниця", "боєприпаси", "гвинтівка", "пістолет", "дробовик", "куля"
        };

        private static readonly string[] ApparelKeywords =
        {
            "apparel", "armor", "cloth", "wear", "jacket", "hat", "броня", "одяг", "вбрання", "куртка", "шолом", "захист"
        };

        private static readonly string[] FoodKeywords =
        {
            "food", "meal", "nutrition", "eat", "ration", "їжа", "інгредієнти", "харчі", "пайок", "сухпай", "котлета", "пайок", "поживна паста"
        };

        private static readonly string[] ResourceKeywords =
        {
            "resource", "resources", "material", "materials", "chemfuel", "fuel", "steel", "component", "components", "plasteel",
            "uranium", "neutroamine", "cloth", "textile", "leather", "wood", "lumber", "stone", "blocks",
            "bionic", "prosthetic", "implant", "artificial", "bodypart", "cybernetic",
            "ресурси", "матеріали", "Хімпаливо", "паливо", "сталь", "прокат", "компоненти", "компоненти", "пластасталь", "уран", "нейтроамін", "тканина", "текстиль", "шкіра", "деревина", "дерево", "камінь",
            "біонік", "протез", "імплант кінцівки", "імплантат", "штучний"
        };

        private static readonly HashSet<string> StopTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "a", "an", "please", "need", "want", "some", "for", "to", "of", "and",
            "give", "send", "drop", "supply",
            "me", "my", "our", "us", "it", "is", "are", "be", "we", "he", "she", "they",
            "дай", "потрібно", "хочу", "трохи", "для", "це", "та", "я", "ти", "скидання", "запит"
        };

        private static readonly HashSet<string> NoiseUnitTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "штука", "порція", "набір", "пакунок", "ящик", "одиниця", "держак", "пляшка", "ствол", "голова", "брусок", "фунт", "кілограм", "грам",
            "kg", "g", "x"
        };

        private static readonly HashSet<string> ShortTokenWhitelist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ag", "au", "cu", "fe", "pb", "sn"
        };

        public static List<string> Tokenize(string text, bool includeNoise = false)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new List<string>();
            }

            string normalized = NormalizeDelimiters(text);
            string[] raw = normalized.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            var tokens = new List<string>();
            for (int i = 0; i < raw.Length; i++)
            {
                List<string> parts = SplitMixedToken(raw[i]);
                for (int j = 0; j < parts.Count; j++)
                {
                    string token = parts[j].Trim().ToLowerInvariant();
                    if (!includeNoise && IsNoiseToken(token))
                    {
                        continue;
                    }

                    tokens.Add(token);
                }
            }

            var result = tokens.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            ModuleLog.Message($"[RimAI.Relations][Tokenize] input=\"{text}\" -> tokens=[{string.Join(",", result)}]");
            return result;
        }

        private static readonly HashSet<string> NegationPrefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "not", "except", "without", "no",
            "крім", "не хочу", "виключити", "без", "не потрібно", "не смій"
        };

        public static void TokenizeWithExclusions(string text, out List<string> tokens, out List<string> exclusionTokens)
        {
            tokens = Tokenize(text);
            exclusionTokens = new List<string>();

            if (string.IsNullOrWhiteSpace(text) || tokens.Count == 0)
            {
                return;
            }

            string lower = text.ToLowerInvariant();
            string[] raw = lower.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < raw.Length; i++)
            {
                string word = raw[i].Trim();
                if (NegationPrefixes.Contains(word) && i + 1 < raw.Length)
                {
                    // Collect up to 2 tokens after the negation prefix as exclusions.
                    for (int k = 1; k <= 2 && i + k < raw.Length; k++)
                    {
                        string excluded = raw[i + k].Trim().ToLowerInvariant();
                        if (!string.IsNullOrWhiteSpace(excluded) && !IsNoiseToken(excluded))
                        {
                            exclusionTokens.Add(excluded);
                        }
                    }
                }
            }

            if (exclusionTokens.Count > 0)
            {
                exclusionTokens = exclusionTokens.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                // Remove exclusion tokens from the positive token list.
                var exclusionsCapture = exclusionTokens;
                tokens = tokens
                    .Where(t => !exclusionsCapture.Contains(t))
                    .ToList();
                ModuleLog.Message($"[RimAI.Relations][TokenizeWithExclusions] exclusions=[{string.Join(",", exclusionTokens)}], tokens=[{string.Join(",", tokens)}]");
            }
        }

        public static ItemAirdropNeedFamily ResolveFamily(List<string> tokens)
        {
            if (tokens == null || tokens.Count == 0)
            {
                return ItemAirdropNeedFamily.Unknown;
            }

            // Score-based family resolution: each family gets points per matched keyword,
            // highest score wins. Ties return Unknown to keep the candidate pool open.
            int foodScore = CountKeywordMatches(tokens, FoodKeywords);
            int medicineScore = CountKeywordMatches(tokens, MedicineKeywords);
            int weaponScore = CountKeywordMatches(tokens, WeaponKeywords);
            int apparelScore = CountKeywordMatches(tokens, ApparelKeywords);
            int resourceScore = CountKeywordMatches(tokens, ResourceKeywords);
            int implantScore = CountImplantKeywordMatches(tokens);
            resourceScore += implantScore;

            int bestScore = 0;
            ItemAirdropNeedFamily bestFamily = ItemAirdropNeedFamily.Unknown;
            bool tie = false;

            void Consider(ItemAirdropNeedFamily family, int score)
            {
                if (score > bestScore)
                {
                    bestScore = score;
                    bestFamily = family;
                    tie = false;
                }
                else if (score == bestScore && score > 0)
                {
                    tie = true;
                }
            }

            Consider(ItemAirdropNeedFamily.Food, foodScore);
            Consider(ItemAirdropNeedFamily.Medicine, medicineScore);
            Consider(ItemAirdropNeedFamily.Weapon, weaponScore);
            Consider(ItemAirdropNeedFamily.Apparel, apparelScore);
            Consider(ItemAirdropNeedFamily.Resource, resourceScore);

            if (tie || bestScore == 0)
            {
                return ItemAirdropNeedFamily.Unknown;
            }

            return bestFamily;
        }

        private static int CountKeywordMatches(List<string> tokens, string[] keywords)
        {
            int count = 0;
            for (int i = 0; i < keywords.Length; i++)
            {
                string keyword = keywords[i];
                if (string.IsNullOrWhiteSpace(keyword))
                {
                    continue;
                }

                for (int j = 0; j < tokens.Count; j++)
                {
                    string token = tokens[j];
                    if (string.IsNullOrWhiteSpace(token))
                    {
                        continue;
                    }

                    if (token.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        count++;
                        break;
                    }

                    if (keyword.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (token.Length >= 3 || token.Length * 2 >= keyword.Length)
                        {
                            count++;
                            break;
                        }
                    }
                }
            }

            return count;
        }

        private static int CountImplantKeywordMatches(List<string> tokens)
        {
            return CountKeywordMatches(tokens, new string[]
            {
                "bionic", "prosthetic", "implant", "artificial", "bodypart", "cybernetic",
                "біонік", "протез", "імплант кінцівки", "імплантат", "штучний"
            });
        }

        private static bool ContainsAny(List<string> tokens, params string[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                string value = values[i];
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                for (int j = 0; j < tokens.Count; j++)
                {
                    string token = tokens[j];
                    if (string.IsNullOrWhiteSpace(token))
                    {
                        continue;
                    }

                    if (token.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }

                    // Keyword contains token: require minimum token length to prevent
                    // short tokens like "me", "arm", "art" from triggering false matches.
                    if (value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (token.Length >= 3 || token.Length * 2 >= value.Length)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static string NormalizeDelimiters(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (IsBoundaryDelimiter(ch))
                {
                    sb.Append(' ');
                    continue;
                }

                sb.Append(ch);
            }

            return sb.ToString();
        }

        private static List<string> SplitMixedToken(string token)
        {
            var parts = new List<string>();
            if (string.IsNullOrWhiteSpace(token))
            {
                return parts;
            }

            var sb = new StringBuilder(token.Length);
            CharBucket prev = CharBucket.Other;
            for (int i = 0; i < token.Length; i++)
            {
                char ch = token[i];
                CharBucket current = GetCharBucket(ch);
                if (sb.Length > 0 && (ShouldSplit(prev, current) || IsUpperCharBucketTransition(token, i)))
                {
                    parts.Add(sb.ToString());
                    sb.Clear();
                }

                sb.Append(ch);
                prev = current;
            }

            if (sb.Length > 0)
            {
                parts.Add(sb.ToString());
            }

            return parts;
        }

        private static bool IsNoiseToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token) || StopTokens.Contains(token))
            {
                return true;
            }

            if (NoiseUnitTokens.Contains(token))
            {
                return true;
            }

            bool allDigits = token.All(char.IsDigit);
            if (allDigits)
            {
                return true;
            }

            bool isCjk = IsCjkToken(token);
            if (!isCjk && token.Length < 2 && !ShortTokenWhitelist.Contains(token))
            {
                return true;
            }

            return false;
        }

        private static bool IsCjkToken(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }

            foreach (char ch in token)
            {
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

        private static bool IsBoundaryDelimiter(char ch)
        {
            return ch == ',' || ch == ';' || ch == ':' || ch == '.' ||
                   ch == '|' || ch == '/' || ch == '\\' ||
                   ch == '、' || ch == '，' || ch == '。' || ch == '；' || ch == '：';
        }

        private static bool ShouldSplit(CharBucket prev, CharBucket current)
        {
            if (prev == CharBucket.Other || current == CharBucket.Other)
            {
                return false;
            }

            if (prev != current)
            {
                return true;
            }

            return false;
        }

        private static bool IsUpperCharBucketTransition(string token, int i)
        {
            if (i <= 0 || i >= token.Length)
            {
                return false;
            }

            char prev = token[i - 1];
            char curr = token[i];
            return char.IsLower(prev) && char.IsUpper(curr);
        }

        private static CharBucket GetCharBucket(char ch)
        {
            if (char.IsDigit(ch))
            {
                return CharBucket.Digit;
            }

            if (IsCjk(ch))
            {
                return CharBucket.Cjk;
            }

            if (char.IsLetter(ch))
            {
                return CharBucket.Letter;
            }

            return CharBucket.Other;
        }

        private static bool IsCjk(char ch)
        {
            return ch >= 0x4E00 && ch <= 0x9FFF;
        }

        private enum CharBucket
        {
            Other = 0,
            Letter = 1,
            Digit = 2,
            Cjk = 3
        }
    }
}
