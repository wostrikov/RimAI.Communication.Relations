using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Verse;

namespace Ustas.RimAI.Communication.Relations.AI
{
    /// <summary>RPG action payload parse. Dialogue envelope parse stays in DialogueResponseEnvelopeParser.</summary>
    public static class RpgActionParser
    {
        public static List<LLMRpgApiResponse.ApiAction> ParseActionsFromJson(string actionsJson)
        {
            var actions = new List<LLMRpgApiResponse.ApiAction>();
            if (string.IsNullOrWhiteSpace(actionsJson))
            {
                return actions;
            }

            ParseActions("{\"actions\":" + actionsJson.Trim() + "}", actions);
            return actions;
        }

        public static void ParseActions(string jsonContent, List<LLMRpgApiResponse.ApiAction> actions)
        {
            if (actions == null) return;

            string actionArrayJson = ExtractJsonArray(jsonContent, "actions");
            if (string.IsNullOrEmpty(actionArrayJson))
            {
                return;
            }

            foreach (string actionObject in SplitJsonObjects(actionArrayJson))
            {
                string normalizedAction = NormalizeActionName(
                    CoalesceActionName(
                        ExtractStringField(actionObject, "action"),
                        ExtractStringField(actionObject, "name")));
                if (string.IsNullOrEmpty(normalizedAction) || !RpgActionCatalog.IsValidAction(normalizedAction))
                {
                    continue;
                }

                // Accept both legacy "params" and mainstream "parameters" wrappers.
                string paramsObject = ExtractJsonObject(actionObject, "params");
                if (string.IsNullOrWhiteSpace(paramsObject))
                {
                    paramsObject = ExtractJsonObject(actionObject, "parameters");
                }
                string parameterSource = string.IsNullOrWhiteSpace(paramsObject) ? actionObject : paramsObject;

                var api = new LLMRpgApiResponse.ApiAction
                {
                    action = normalizedAction,
                    defName = CoalesceField(parameterSource, actionObject, "defName"),
                    reason = CoalesceField(parameterSource, actionObject, "reason"),
                    title = CoalesceField(parameterSource, actionObject, "title"),
                    description = CoalesceField(parameterSource, actionObject, "description"),
                    rewardDescription = CoalesceField(parameterSource, actionObject, "rewardDescription"),
                    callbackId = CoalesceField(parameterSource, actionObject, "callbackId")
                };

                int? amount = ExtractIntField(parameterSource, "amount") ?? ExtractIntField(actionObject, "amount");
                if (amount.HasValue)
                {
                    api.amount = amount.Value;
                }

                actions.Add(api);
            }
        }

        public static void TryExtractInlineActions(string rawResponse, List<LLMRpgApiResponse.ApiAction> actions)
        {
            if (actions == null || actions.Count > 0 || string.IsNullOrWhiteSpace(rawResponse))
            {
                return;
            }

            MatchCollection matches = Regex.Matches(
                rawResponse,
                @"(?:Use\s+Action|使用动作)\s*[:：]\s*([A-Za-z_][A-Za-z0-9_]*)[^\r\n\)]*",
                RegexOptions.IgnoreCase);

            foreach (Match match in matches)
            {
                string actionName = NormalizeActionName(match.Groups[1].Value);
                if (string.IsNullOrWhiteSpace(actionName) ||
                    !RpgActionCatalog.IsValidAction(actionName) ||
                    HasAction(actions, actionName))
                {
                    continue;
                }

                actions.Add(new LLMRpgApiResponse.ApiAction
                {
                    action = actionName,
                    defName = ExtractInlineDefName(match.Value)
                });
            }
        }

        public static bool HasAction(List<LLMRpgApiResponse.ApiAction> actions, string actionName)
        {
            for (int i = 0; i < actions.Count; i++)
            {
                if (string.Equals(actions[i].action, actionName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static string ExtractInlineDefName(string actionSegment)
        {
            if (string.IsNullOrWhiteSpace(actionSegment))
            {
                return null;
            }

            Match match = Regex.Match(actionSegment, @"questDefName\s*=\s*([A-Za-z0-9_\.]+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }

            match = Regex.Match(actionSegment, @"defName\s*=\s*([A-Za-z0-9_\.]+)", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }

        public static string ExtractFirstBalancedJsonObject(string raw)
        {
            if (!JsonBoundedExtractor.TryExtractFirstObject(raw, out string json))
            {
                return null;
            }

            return json;
        }

        public static string ExtractJsonObject(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
            {
                return null;
            }

            string pattern = $"\"{key}\"";
            int keyIndex = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (keyIndex < 0)
            {
                return null;
            }

            int colonIndex = json.IndexOf(':', keyIndex + pattern.Length);
            if (colonIndex < 0)
            {
                return null;
            }

            int objectStart = json.IndexOf('{', colonIndex + 1);
            if (objectStart < 0)
            {
                return null;
            }

            bool inString = false;
            int depth = 0;
            for (int i = objectStart; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"' && (i == 0 || json[i - 1] != '\\'))
                {
                    inString = !inString;
                }

                if (inString)
                {
                    continue;
                }

                if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return json.Substring(objectStart, i - objectStart + 1);
                    }
                }
            }

            return null;
        }

        public static string ExtractJsonArray(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
            {
                return null;
            }

            string pattern = $"\"{key}\"";
            int keyIndex = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (keyIndex < 0)
            {
                return null;
            }

            int colonIndex = json.IndexOf(':', keyIndex + pattern.Length);
            if (colonIndex < 0)
            {
                return null;
            }

            int arrayStart = json.IndexOf('[', colonIndex + 1);
            if (arrayStart < 0)
            {
                return null;
            }

            bool inString = false;
            int depth = 0;
            for (int i = arrayStart; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"' && (i == 0 || json[i - 1] != '\\'))
                {
                    inString = !inString;
                }

                if (inString)
                {
                    continue;
                }

                if (c == '[')
                {
                    depth++;
                }
                else if (c == ']')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return json.Substring(arrayStart, i - arrayStart + 1);
                    }
                }
            }

            return null;
        }

        public static List<string> SplitJsonObjects(string arrayJson)
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

            bool inString = false;
            int depth = 0;
            int start = -1;
            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];
                if (c == '"' && (i == 0 || content[i - 1] != '\\'))
                {
                    inString = !inString;
                }

                if (inString)
                {
                    continue;
                }

                if (c == '{')
                {
                    if (depth == 0)
                    {
                        start = i;
                    }
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && start >= 0)
                    {
                        result.Add(content.Substring(start, i - start + 1));
                        start = -1;
                    }
                }
            }

            return result;
        }

        public static string ExtractStringField(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
            {
                return null;
            }

            string pattern = $"\"{Regex.Escape(key)}\"\\s*:\\s*\"((?:\\\\.|[^\"])*)\"";
            Match match = Regex.Match(json, pattern, RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return null;
            }

            return UnescapeJson(match.Groups[1].Value);
        }

        public static int? ExtractIntField(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
            {
                return null;
            }

            string pattern = $"\"{Regex.Escape(key)}\"\\s*:\\s*(-?\\d+)";
            Match match = Regex.Match(json, pattern, RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return null;
            }

            if (int.TryParse(match.Groups[1].Value, out int value))
            {
                return value;
            }

            return null;
        }

        public static string CoalesceActionName(string primary, string secondary)
        {
            if (!string.IsNullOrWhiteSpace(primary))
            {
                return primary;
            }

            return secondary;
        }

        public static string CoalesceField(string preferredJson, string fallbackJson, string key)
        {
            string value = ExtractStringField(preferredJson, key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            return ExtractStringField(fallbackJson, key);
        }

        public static string NormalizeActionName(string actionName)
        {
            return RpgActionCatalog.NormalizeActionName(actionName);
        }

        public static string UnescapeJson(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return str;
            }

            return str.Replace("\\\"", "\"")
                      .Replace("\\\\", "\\")
                      .Replace("\\n", "\n")
                      .Replace("\\r", "\r")
                      .Replace("\\t", "\t");
        }

        public static string SanitizeDialogueContent(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            string sanitized = content;
            sanitized = Regex.Replace(sanitized, @"\(\s*(?:Use\s+Action|使用动作)\s*[:：][^)\r\n]*\)", string.Empty, RegexOptions.IgnoreCase);
            sanitized = Regex.Replace(sanitized, @"（\s*(?:Use\s+Action|使用动作)\s*[:：][^）\r\n]*）", string.Empty, RegexOptions.IgnoreCase);
            sanitized = Regex.Replace(sanitized, @"^[ \t]*(?:Use\s+Action|使用动作)\s*[:：][^\r\n]*$", string.Empty, RegexOptions.Multiline | RegexOptions.IgnoreCase);
            sanitized = Regex.Replace(sanitized, @"^\s*\*\*<[^>\r\n]+>\*\*\s*$", string.Empty, RegexOptions.Multiline);
            sanitized = Regex.Replace(sanitized, @"^\s*<[^>\r\n]+>\s*$", string.Empty, RegexOptions.Multiline);
            sanitized = Regex.Replace(sanitized, @"^\s*\{[\s\r\n]*""defName""\s*:\s*""[^""]+""[\s\r\n]*\}\s*$", string.Empty, RegexOptions.Multiline);
            sanitized = Regex.Replace(sanitized, @"\n{3,}", "\n\n");
            ImmersionGuardResult guardResult = ImmersionOutputGuard.ValidateVisibleDialogue(sanitized.Trim());
            if (!guardResult.IsValid)
            {
                Log.Warning($"[RimAI.Relations] Immersion guard blocked RPG text: reason={ImmersionOutputGuard.BuildViolationTag(guardResult.ViolationReason)}, snippet={guardResult.ViolationSnippet}");
                return ImmersionOutputGuard.BuildLocalFallbackDialogue(DialogueUsageChannel.Rpg);
            }

            return guardResult.VisibleDialogue;
        }

        public static string ExtractLegacyDialogueContent(string jsonContent)
        {
            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                return string.Empty;
            }

            string dialogue = ExtractStringField(jsonContent, "dialogue");
            if (!string.IsNullOrWhiteSpace(dialogue))
            {
                return dialogue;
            }

            string response = ExtractStringField(jsonContent, "response");
            if (!string.IsNullOrWhiteSpace(response))
            {
                return response;
            }

            string content = ExtractStringField(jsonContent, "content");
            if (!string.IsNullOrWhiteSpace(content) && content.IndexOf("\"actions\"", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return content;
            }

            return string.Empty;
        }
    }
}
