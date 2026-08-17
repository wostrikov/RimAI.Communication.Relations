using System;
using System.Collections.Generic;

namespace Ustas.RimAI.Communication.Relations.AI
{
    public sealed class JsonTextSpan
    {
        public int Start { get; set; }
        public int End { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>
    /// One string-aware JSON boundary scanner. No provider or Relations semantics.
    /// </summary>
    public static class JsonBoundedExtractor
    {
        public static bool LooksLikeJsonPayload(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string trimmed = value.TrimStart();
            return trimmed.StartsWith("{", StringComparison.Ordinal) ||
                   trimmed.StartsWith("[", StringComparison.Ordinal);
        }

        public static bool LooksLikeSingleJsonObject(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string trimmed = text.Trim();
            if (!trimmed.StartsWith("{", StringComparison.Ordinal) ||
                !trimmed.EndsWith("}", StringComparison.Ordinal))
            {
                return false;
            }

            return FindMatchingBracket(trimmed, 0, '{', '}') == trimmed.Length - 1;
        }

        public static bool TryExtractFirstObject(string text, out string json)
        {
            json = string.Empty;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            int start = text.IndexOf('{');
            if (start < 0)
            {
                return false;
            }

            int end = FindMatchingBracket(text, start, '{', '}');
            if (end < start)
            {
                return false;
            }

            json = text.Substring(start, end - start + 1);
            return true;
        }

        public static List<JsonTextSpan> EnumerateTopLevelObjects(string text)
        {
            var spans = new List<JsonTextSpan>();
            if (string.IsNullOrEmpty(text))
            {
                return spans;
            }

            int start = -1;
            int depth = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = 0; i < text.Length; i++)
            {
                char current = text[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (current == '\\')
                    {
                        escaped = true;
                    }
                    else if (current == '"')
                    {
                        inString = false;
                    }
                    continue;
                }

                if (current == '"')
                {
                    inString = true;
                    continue;
                }

                if (current == '{')
                {
                    if (depth == 0)
                    {
                        start = i;
                    }
                    depth++;
                    continue;
                }

                if (current != '}')
                {
                    continue;
                }

                if (depth <= 0)
                {
                    continue;
                }

                depth--;
                if (depth == 0 && start >= 0)
                {
                    spans.Add(new JsonTextSpan
                    {
                        Start = start,
                        End = i,
                        Text = text.Substring(start, i - start + 1)
                    });
                    start = -1;
                }
            }

            return spans;
        }

        public static int FindMatchingBracket(string text, int start, char open, char close)
        {
            if (string.IsNullOrEmpty(text) || start < 0 || start >= text.Length || text[start] != open)
            {
                return -1;
            }

            bool inString = false;
            bool escaped = false;
            int depth = 0;

            for (int i = start; i < text.Length; i++)
            {
                char current = text[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (current == '\\')
                    {
                        escaped = true;
                    }
                    else if (current == '"')
                    {
                        inString = false;
                    }
                    continue;
                }

                if (current == '"')
                {
                    inString = true;
                    continue;
                }

                if (current == open)
                {
                    depth++;
                    continue;
                }

                if (current != close)
                {
                    continue;
                }

                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }

            return -1;
        }

        public static bool TryExtractArrayAt(string json, int startIndex, out string block)
        {
            block = string.Empty;
            int end = FindMatchingBracket(json, startIndex, '[', ']');
            if (end < startIndex)
            {
                return false;
            }

            block = json.Substring(startIndex, end - startIndex + 1);
            return true;
        }

        public static string ExtractObjectContainingKey(string text, string requiredKey)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(requiredKey))
            {
                return string.Empty;
            }

            string needle = "\"" + requiredKey + "\"";
            int keyIndex = text.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
            if (keyIndex < 0)
            {
                return string.Empty;
            }

            int depth = 0;
            int objectStart = -1;
            for (int i = keyIndex; i >= 0; i--)
            {
                char c = text[i];
                if (c == '}')
                {
                    depth++;
                }
                else if (c == '{')
                {
                    if (depth == 0)
                    {
                        objectStart = i;
                        break;
                    }

                    depth--;
                }
            }

            if (objectStart < 0)
            {
                return string.Empty;
            }

            int objectEnd = FindMatchingBracket(text, objectStart, '{', '}');
            if (objectEnd <= objectStart)
            {
                return string.Empty;
            }

            return text.Substring(objectStart, objectEnd - objectStart + 1).Trim();
        }
    }
}
