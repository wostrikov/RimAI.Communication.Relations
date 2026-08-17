using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Ustas.RimAI.Communication.Relations.AI
{
    /// <summary>Hand-rolled JSON object/array helpers used by Diplomacy parsers.</summary>
    public static class JsonLooseObjectParser
    {
        public static List<string> ParseStringArray(string arrayJson)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(arrayJson))
            {
                return result;
            }

            string content = arrayJson.Trim();
            if (content.StartsWith("[")) content = content.Substring(1);
            if (content.EndsWith("]")) content = content.Substring(0, content.Length - 1);

            bool inString = false;
            var sb = new StringBuilder();
            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];
                if (c == '"' && (i == 0 || content[i - 1] != '\\'))
                {
                    if (inString)
                    {
                        string item = UnescapeJsonString(sb.ToString()).Trim();
                        if (!string.IsNullOrWhiteSpace(item))
                        {
                            result.Add(item);
                        }
                        sb.Clear();
                    }
                    inString = !inString;
                    continue;
                }

                if (inString)
                {
                    sb.Append(c);
                }
            }

            return result;
        }

        public static string ExtractJsonString(string json, string key)
        {
            string pattern = $"\"{key}\":";
            int index = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return null;

            index += pattern.Length;
            // 跳过空白字符
            while (index < json.Length && char.IsWhiteSpace(json[index])) index++;

            if (index >= json.Length) return null;

            // 检查whether是字符串
            if (json[index] == '"')
            {
                index++;
                var sb = new StringBuilder();
                while (index < json.Length)
                {
                    char c = json[index];
                    if (c == '"' && (index == 0 || json[index - 1] != '\\'))
                    {
                        break;
                    }
                    sb.Append(c);
                    index++;
                }
                return UnescapeJsonString(sb.ToString());
            }

            // 不是字符串, 提取到下一个逗号或括号
            var valueSb = new StringBuilder();
            while (index < json.Length && json[index] != ',' && json[index] != '}' && json[index] != ']')
            {
                valueSb.Append(json[index]);
                index++;
            }
            return valueSb.ToString().Trim();
        }

        /// <summary>/// 从JSON中提取对象
 ///</summary>
        public static string ExtractJsonObject(string json, string key)
        {
            string pattern = $"\"{key}\":";
            int index = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return null;

            index += pattern.Length;
            // 跳过空白字符
            while (index < json.Length && char.IsWhiteSpace(json[index])) index++;

            if (index >= json.Length || json[index] != '{') return null;

            // 找到匹配的结束括号
            int braceCount = 1;
            int startIndex = index;
            index++;

            while (index < json.Length && braceCount > 0)
            {
                if (json[index] == '{') braceCount++;
                else if (json[index] == '}') braceCount--;
                index++;
            }

            return json.Substring(startIndex, index - startIndex);
        }

        public static string ExtractJsonArray(string json, string key)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            string pattern = $"\"{key}\":";
            int index = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return null;

            index += pattern.Length;
            while (index < json.Length && char.IsWhiteSpace(json[index])) index++;
            if (index >= json.Length || json[index] != '[') return null;

            int depth = 1;
            int start = index;
            index++;
            while (index < json.Length && depth > 0)
            {
                if (json[index] == '[') depth++;
                else if (json[index] == ']') depth--;
                index++;
            }

            return depth == 0 ? json.Substring(start, index - start) : null;
        }

        public static List<string> SplitJsonObjects(string arrayJson)
        {
            var objects = new List<string>();
            if (string.IsNullOrWhiteSpace(arrayJson))
            {
                return objects;
            }

            string content = arrayJson.Trim();
            if (content.StartsWith("[")) content = content.Substring(1);
            if (content.EndsWith("]")) content = content.Substring(0, content.Length - 1);

            int depth = 0;
            int start = -1;
            bool inString = false;
            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];
                if (c == '"' && (i == 0 || content[i - 1] != '\\'))
                {
                    inString = !inString;
                    continue;
                }
                if (inString) continue;

                if (c == '{')
                {
                    if (depth == 0) start = i;
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && start >= 0)
                    {
                        objects.Add(content.Substring(start, i - start + 1));
                        start = -1;
                    }
                }
            }

            return objects;
        }

        /// <summary>/// 解析参数对象
 ///</summary>
        public static Dictionary<string, object> ParseParameters(string parametersJson)
        {
            var result = new Dictionary<string, object>();

            // 移除外层花括号
            string content = parametersJson.Trim();
            if (content.StartsWith("{")) content = content.Substring(1);
            if (content.EndsWith("}")) content = content.Substring(0, content.Length - 1);

            // 简单解析键values对
            var pairs = SplitJsonPairs(content);
            foreach (var pair in pairs)
            {
                var kv = pair.Split(new[] { ':' }, 2);
                if (kv.Length == 2)
                {
                    string key = kv[0].Trim().Trim('"');
                    result[key] = ParseJsonValue(kv[1]);
                }
            }

            return result;
        }

        public static object ParseJsonValue(string rawValue)
        {
            string value = (rawValue ?? string.Empty).Trim();
            if (value.Length == 0)
            {
                return string.Empty;
            }

            if (value.StartsWith("{") && value.EndsWith("}"))
            {
                return ParseJsonObject(value);
            }

            if (value.StartsWith("[") && value.EndsWith("]"))
            {
                return ParseJsonArray(value);
            }

            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
            {
                return UnescapeJsonString(value.Substring(1, value.Length - 2));
            }

            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(value, "null", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
            {
                return intValue;
            }

            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long longValue))
            {
                return longValue;
            }

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double doubleValue))
            {
                return doubleValue;
            }

            return UnescapeJsonString(value.Trim('"'));
        }

        public static Dictionary<string, object> ParseJsonObject(string objectJson)
        {
            var result = new Dictionary<string, object>();
            if (string.IsNullOrWhiteSpace(objectJson))
            {
                return result;
            }

            string content = objectJson.Trim();
            if (content.StartsWith("{"))
            {
                content = content.Substring(1);
            }
            if (content.EndsWith("}"))
            {
                content = content.Substring(0, content.Length - 1);
            }

            foreach (string pair in SplitJsonPairs(content))
            {
                string[] kv = pair.Split(new[] { ':' }, 2);
                if (kv.Length != 2)
                {
                    continue;
                }

                string key = kv[0].Trim().Trim('"');
                result[key] = ParseJsonValue(kv[1]);
            }

            return result;
        }

        public static List<object> ParseJsonArray(string arrayJson)
        {
            var result = new List<object>();
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

            foreach (string item in SplitJsonArrayItems(content))
            {
                result.Add(ParseJsonValue(item));
            }

            return result;
        }

        public static List<string> SplitJsonArrayItems(string content)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(content))
            {
                return result;
            }

            var builder = new StringBuilder();
            int depth = 0;
            bool inString = false;
            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];
                if (c == '"' && (i == 0 || content[i - 1] != '\\'))
                {
                    inString = !inString;
                }
                else if (!inString)
                {
                    if (c == '{' || c == '[')
                    {
                        depth++;
                    }
                    else if (c == '}' || c == ']')
                    {
                        depth--;
                    }
                    else if (c == ',' && depth == 0)
                    {
                        string item = builder.ToString().Trim();
                        if (!string.IsNullOrWhiteSpace(item))
                        {
                            result.Add(item);
                        }

                        builder.Clear();
                        continue;
                    }
                }

                builder.Append(c);
            }

            string tail = builder.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(tail))
            {
                result.Add(tail);
            }

            return result;
        }

        /// <summary>/// 分割JSON键values对
 ///</summary>
        public static List<string> SplitJsonPairs(string content)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            int braceDepth = 0;
            bool inString = false;

            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];

                if (c == '"' && (i == 0 || content[i - 1] != '\\'))
                {
                    inString = !inString;
                }
                else if (!inString)
                {
                    if (c == '{' || c == '[') braceDepth++;
                    else if (c == '}' || c == ']') braceDepth--;
                    else if (c == ',' && braceDepth == 0)
                    {
                        result.Add(sb.ToString());
                        sb.Clear();
                        continue;
                    }
                }

                sb.Append(c);
            }

            if (sb.Length > 0)
            {
                result.Add(sb.ToString());
            }

            return result;
        }

        /// <summary>/// 反转义JSON字符串
 ///</summary>
        public static string UnescapeJsonString(string str)
        {
            return str.Replace("\\\"", "\"")
                      .Replace("\\\\", "\\")
                      .Replace("\\n", "\n")
                      .Replace("\\r", "\r")
                      .Replace("\\t", "\t");
        }
    }
}
