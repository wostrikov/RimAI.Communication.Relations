using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Ustas.RimAI.Communication.Relations.Memory
{
    /// <summary>
    /// Low-level JSON extract/split helpers for leader memory codec.
    /// </summary>
    internal static class LeaderMemoryJsonUtil
    {
        internal static bool TryExtractJsonArray(string json, string key, out string arrayContent)
        {
            arrayContent = string.Empty;
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
            {
                return false;
            }

            string pattern = $"\"{key}\"\\s*:\\s*\\[";
            Match match = Regex.Match(json, pattern);
            if (!match.Success)
            {
                return false;
            }

            int start = json.IndexOf('[', match.Index);
            if (start < 0 || !TryFindJsonBlockEnd(json, start, '[', ']', out int end))
            {
                return false;
            }

            arrayContent = json.Substring(start, end - start + 1);
            return true;
        }

        internal static bool TryExtractJsonObject(string json, string key, out string objectContent)
        {
            objectContent = string.Empty;
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
            {
                return false;
            }

            string pattern = $"\"{key}\"\\s*:\\s*\\{{";
            Match match = Regex.Match(json, pattern);
            if (!match.Success)
            {
                return false;
            }

            int start = json.IndexOf('{', match.Index);
            if (start < 0 || !TryFindJsonBlockEnd(json, start, '{', '}', out int end))
            {
                return false;
            }

            objectContent = json.Substring(start, end - start + 1);
            return true;
        }

        internal static bool TryFindJsonBlockEnd(string json, int blockStart, char openChar, char closeChar, out int endIndex)
        {
            endIndex = -1;
            if (string.IsNullOrEmpty(json) || blockStart < 0 || blockStart >= json.Length || json[blockStart] != openChar)
            {
                return false;
            }

            int depth = 0;
            bool inString = false;
            bool escape = false;
            for (int i = blockStart; i < json.Length; i++)
            {
                char c = json[i];
                if (inString)
                {
                    if (escape)
                    {
                        escape = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escape = true;
                        continue;
                    }

                    if (c == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == openChar) depth++;
                if (c == closeChar) depth--;
                if (depth == 0)
                {
                    endIndex = i;
                    return true;
                }
            }

            return false;
        }

        internal static List<string> SplitJsonObjects(string arrayJson)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(arrayJson))
            {
                return result;
            }

            string content = arrayJson.Trim();
            if (content.StartsWith("["))
            {
                content = content.Substring(1);
            }
            if (content.EndsWith("]"))
            {
                content = content.Substring(0, content.Length - 1);
            }

            int depth = 0;
            int objectStart = -1;
            bool inString = false;
            bool escape = false;
            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];
                if (inString)
                {
                    if (escape)
                    {
                        escape = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escape = true;
                        continue;
                    }

                    if (c == '"')
                    {
                        inString = false;
                    }
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == '{')
                {
                    if (depth == 0)
                    {
                        objectStart = i;
                    }
                    depth++;
                    continue;
                }

                if (c == '}')
                {
                    depth--;
                    if (depth == 0 && objectStart >= 0)
                    {
                        result.Add(content.Substring(objectStart, i - objectStart + 1));
                        objectStart = -1;
                    }
                }
            }

            return result;
        }

        internal static string EscapeJson(string value)
        {
            return Ustas.RimAI.Core.AI.JsonStringCodec.Escape(value);
        }

        internal static string ExtractJsonString(string json, string key)
        {
            string pattern = $"\"{key}\"\\s*:\\s*\"((?:\\\\.|[^\"])*)\"";
            Match match = Regex.Match(json ?? string.Empty, pattern);
            if (!match.Success || match.Groups.Count < 2)
            {
                return string.Empty;
            }

            string value = match.Groups[1].Value
                .Replace("\\\\", "\\")
                .Replace("\\\"", "\"")
                .Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\\t", "\t")
                .Replace("\\b", "\b")
                .Replace("\\f", "\f");

            value = Regex.Replace(value, @"\\u([0-9a-fA-F]{4})", m =>
            {
                int code = int.Parse(m.Groups[1].Value, NumberStyles.HexNumber);
                return ((char)code).ToString();
            });

            return value;
        }

        internal static int ExtractJsonInt(string json, string key)
        {
            string pattern = $"\"{key}\"\\s*:\\s*(-?\\d+)";
            Match match = Regex.Match(json ?? string.Empty, pattern);
            return match.Success && int.TryParse(match.Groups[1].Value, out int result) ? result : 0;
        }

        internal static long ExtractJsonLong(string json, string key)
        {
            string pattern = $"\"{key}\"\\s*:\\s*(-?\\d+)";
            Match match = Regex.Match(json ?? string.Empty, pattern);
            return match.Success && long.TryParse(match.Groups[1].Value, out long result) ? result : 0L;
        }

        internal static float ExtractJsonFloat(string json, string key)
        {
            string pattern = $"\"{key}\"\\s*:\\s*(-?\\d+(?:\\.\\d+)?)";
            Match match = Regex.Match(json ?? string.Empty, pattern);
            if (!match.Success)
            {
                return 0f;
            }

            return float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result)
                ? result
                : 0f;
        }

        internal static bool ExtractJsonBool(string json, string key)
        {
            string pattern = $"\"{key}\"\\s*:\\s*(true|false)";
            Match match = Regex.Match(json ?? string.Empty, pattern, RegexOptions.IgnoreCase);
            return match.Success && string.Equals(match.Groups[1].Value, "true", StringComparison.OrdinalIgnoreCase);
        }

        internal static string FirstNonEmpty(params string[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                {
                    return values[i].Trim();
                }
            }

            return string.Empty;
        }

        internal static int FirstNonZero(params int[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] != 0)
                {
                    return values[i];
                }
            }

            return 0;
        }

        internal static long FirstNonZeroLong(params long[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] != 0L)
                {
                    return values[i];
                }
            }

            return 0L;
        }

        internal static float FirstNonZeroFloat(params float[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (Math.Abs(values[i]) > 0.0001f)
                {
                    return values[i];
                }
            }

            return 0f;
        }
    }
}
