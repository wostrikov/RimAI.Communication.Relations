using System;
using System.Collections.Generic;

namespace Ustas.RimAI.Communication.Relations.AI
{
    /// <summary>Diplomacy strategy_suggestions array (exactly 3 or empty).</summary>
    public static class DiplomacyStrategySuggestionParser
    {
        public static List<StrategySuggestion> ParseStrategySuggestions(string arrayJson)
        {
            var suggestions = new List<StrategySuggestion>();
            foreach (string suggestionObj in JsonLooseObjectParser.SplitJsonObjects(arrayJson))
            {
                var parsed = ParseStrategySuggestionItem(suggestionObj);
                if (parsed != null)
                {
                    suggestions.Add(parsed);
                }
            }

            if (suggestions.Count != 3)
            {
                return new List<StrategySuggestion>();
            }

            return suggestions;
        }

        public static StrategySuggestion ParseStrategySuggestionItem(string suggestionObj)
        {
            if (string.IsNullOrWhiteSpace(suggestionObj))
            {
                return null;
            }

            string content = JsonLooseObjectParser.ExtractJsonString(suggestionObj, "content");
            if (string.IsNullOrWhiteSpace(content))
            {
                content = JsonLooseObjectParser.ExtractJsonString(suggestionObj, "hidden_reply");
            }
            if (string.IsNullOrWhiteSpace(content))
            {
                content = JsonLooseObjectParser.ExtractJsonString(suggestionObj, "reply");
            }
            if (string.IsNullOrWhiteSpace(content))
            {
                content = JsonLooseObjectParser.ExtractJsonString(suggestionObj, "full_reply");
            }
            if (string.IsNullOrWhiteSpace(content))
            {
                content = JsonLooseObjectParser.ExtractJsonString(suggestionObj, "expected_outcome");
            }
            if (string.IsNullOrWhiteSpace(content))
            {
                content = JsonLooseObjectParser.ExtractJsonString(suggestionObj, "description");
            }
            if (string.IsNullOrWhiteSpace(content))
            {
                content = JsonLooseObjectParser.ExtractJsonString(suggestionObj, "suggestion");
            }
            if (string.IsNullOrWhiteSpace(content))
            {
                content = JsonLooseObjectParser.ExtractJsonString(suggestionObj, "recommendation");
            }
            if (string.IsNullOrWhiteSpace(content))
            {
                content = JsonLooseObjectParser.ExtractJsonString(suggestionObj, "proposal");
            }
            if (string.IsNullOrWhiteSpace(content))
            {
                content = JsonLooseObjectParser.ExtractJsonString(suggestionObj, "reasoning");
            }
            if (string.IsNullOrWhiteSpace(content))
            {
                content = JsonLooseObjectParser.ExtractJsonString(suggestionObj, "macro_advice");
            }
            if (string.IsNullOrWhiteSpace(content))
            {
                content = JsonLooseObjectParser.ExtractJsonString(suggestionObj, "reason");
            }
            content = (content ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            string strategyName = JsonLooseObjectParser.ExtractJsonString(suggestionObj, "strategy_name");
            if (string.IsNullOrWhiteSpace(strategyName))
            {
                strategyName = JsonLooseObjectParser.ExtractJsonString(suggestionObj, "name");
            }
            if (string.IsNullOrWhiteSpace(strategyName))
            {
                strategyName = JsonLooseObjectParser.ExtractJsonString(suggestionObj, "title");
            }
            if (string.IsNullOrWhiteSpace(strategyName))
            {
                strategyName = JsonLooseObjectParser.ExtractJsonString(suggestionObj, "short_label");
            }
            if (string.IsNullOrWhiteSpace(strategyName))
            {
                strategyName = JsonLooseObjectParser.ExtractJsonString(suggestionObj, "label");
            }
            if (string.IsNullOrWhiteSpace(strategyName))
            {
                strategyName = JsonLooseObjectParser.ExtractJsonString(suggestionObj, "task");
            }
            if (string.IsNullOrWhiteSpace(strategyName))
            {
                strategyName = JsonLooseObjectParser.ExtractJsonString(suggestionObj, "plan");
            }
            if (string.IsNullOrWhiteSpace(strategyName))
            {
                strategyName = JsonLooseObjectParser.ExtractJsonString(suggestionObj, "macro_advice");
            }
            if (string.IsNullOrWhiteSpace(strategyName))
            {
                strategyName = JsonLooseObjectParser.ExtractJsonString(suggestionObj, "action");
            }

            string factReason = JsonLooseObjectParser.ExtractJsonString(suggestionObj, "reason");
            if (string.IsNullOrWhiteSpace(factReason))
            {
                factReason = JsonLooseObjectParser.ExtractJsonString(suggestionObj, "fact_reason");
            }
            if (string.IsNullOrWhiteSpace(factReason))
            {
                factReason = JsonLooseObjectParser.ExtractJsonString(suggestionObj, "trigger_basis");
            }
            if (string.IsNullOrWhiteSpace(factReason))
            {
                factReason = JsonLooseObjectParser.ExtractJsonString(suggestionObj, "basis");
            }
            if (string.IsNullOrWhiteSpace(factReason))
            {
                factReason = JsonLooseObjectParser.ExtractJsonString(suggestionObj, "trigger");
            }
            if (string.IsNullOrWhiteSpace(factReason))
            {
                factReason = JsonLooseObjectParser.ExtractJsonString(suggestionObj, "risk_level");
            }
            if (string.IsNullOrWhiteSpace(factReason))
            {
                factReason = JsonLooseObjectParser.ExtractJsonString(suggestionObj, "reasoning");
            }
            if (string.IsNullOrWhiteSpace(factReason))
            {
                factReason = JsonLooseObjectParser.ExtractJsonString(suggestionObj, "rationale");
            }
            if (string.IsNullOrWhiteSpace(factReason))
            {
                factReason = JsonLooseObjectParser.ExtractJsonString(suggestionObj, "risk_assessment");
            }
            if (string.IsNullOrWhiteSpace(factReason))
            {
                factReason = JsonLooseObjectParser.ExtractJsonString(suggestionObj, "analysis");
            }

            string keywordsJson = JsonLooseObjectParser.ExtractJsonArray(suggestionObj, "strategy_keywords");
            if (string.IsNullOrWhiteSpace(keywordsJson))
            {
                keywordsJson = JsonLooseObjectParser.ExtractJsonArray(suggestionObj, "keywords");
            }

            var keywords = JsonLooseObjectParser.ParseStringArray(keywordsJson);
            strategyName = NormalizeStrategyName(strategyName, keywords, content);
            factReason = NormalizeStrategyReason(factReason);

            return new StrategySuggestion
            {
                StrategyName = strategyName,
                Reason = factReason,
                StrategyKeywords = keywords,
                Content = content
            };
        }

        public static string NormalizeStrategyName(string label, List<string> keywords, string content)
        {
            string result = label ?? string.Empty;
            if (string.IsNullOrWhiteSpace(result) && keywords != null && keywords.Count > 0)
            {
                result = keywords[0];
            }
            if (string.IsNullOrWhiteSpace(result))
            {
                result = content;
            }
            result = (result ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            if (result.Length == 0)
            {
                result = "策略建议";
            }
            if (result.Length > 14)
            {
                result = result.Substring(0, 14);
            }
            return result;
        }

        public static string NormalizeStrategyReason(string reason)
        {
            string result = (reason ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            if (string.IsNullOrWhiteSpace(result))
            {
                return "综合判断";
            }
            if (result.Length > 80)
            {
                return result.Substring(0, 80);
            }
            return result;
        }
    }
}
