using System;
using System.Globalization;

namespace Ustas.RimAI.Communication.Relations.DiplomacySystem
{
    /// <summary>
    /// Dependencies: none.
    /// Responsibility: strict parser for second-pass airdrop selection JSON.
    /// </summary>
    internal static class ItemAirdropSelectionParser
    {
        public static bool TryParse(string raw, out ItemAirdropSelection selection, out string failureCode, out string failureMessage)
        {
            selection = null;
            failureCode = string.Empty;
            failureMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(raw))
            {
                failureCode = "selection_empty";
                failureMessage = "Selection response is empty.";
                return false;
            }

            string json = ExtractTopLevelJson(raw);
            if (string.IsNullOrWhiteSpace(json))
            {
                failureCode = "selection_json_missing";
                failureMessage = "Selection JSON object not found.";
                return false;
            }

            string selectedDef = ExtractJsonString(json, "selected_def");
            if (string.IsNullOrWhiteSpace(selectedDef))
            {
                failureCode = "selection_selected_def_missing";
                failureMessage = "Missing field: selected_def.";
                return false;
            }

            if (!TryExtractJsonInt(json, "count", out int count))
            {
                failureCode = "selection_count_missing";
                failureMessage = "Missing or invalid field: count.";
                return false;
            }

            string reason = ExtractJsonString(json, "reason");
            if (string.IsNullOrWhiteSpace(reason))
            {
                failureCode = "selection_reason_missing";
                failureMessage = "Missing field: reason.";
                return false;
            }

            selection = new ItemAirdropSelection
            {
                SelectedDefName = selectedDef.Trim(),
                Count = count,
                Reason = reason.Trim()
            };
            return true;
        }

        private static string ExtractTopLevelJson(string text)
        {
            string json = Ustas.RimAI.Communication.Relations.AI.JsonBoundedExtractor.TryExtractFirstObject(text, out string extracted)
                ? extracted
                : string.Empty;
            if (string.IsNullOrWhiteSpace(json))
            {
                return string.Empty;
            }

            return json;
        }

        private static string ExtractJsonString(string json, string key)
        {
            string pattern = $"\"{key}\"";
            int keyIndex = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (keyIndex < 0)
            {
                return string.Empty;
            }

            int colonIndex = json.IndexOf(':', keyIndex + pattern.Length);
            if (colonIndex < 0)
            {
                return string.Empty;
            }

            int firstQuote = json.IndexOf('"', colonIndex + 1);
            if (firstQuote < 0)
            {
                return string.Empty;
            }

            int secondQuote = firstQuote + 1;
            bool escaped = false;
            while (secondQuote < json.Length)
            {
                char current = json[secondQuote];
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
                    break;
                }

                secondQuote++;
            }

            if (secondQuote >= json.Length)
            {
                return string.Empty;
            }

            return json.Substring(firstQuote + 1, secondQuote - firstQuote - 1)
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\")
                .Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\\t", "\t");
        }

        private static bool TryExtractJsonInt(string json, string key, out int value)
        {
            value = 0;
            string pattern = $"\"{key}\"";
            int keyIndex = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (keyIndex < 0)
            {
                return false;
            }

            int colonIndex = json.IndexOf(':', keyIndex + pattern.Length);
            if (colonIndex < 0)
            {
                return false;
            }

            int cursor = colonIndex + 1;
            while (cursor < json.Length && char.IsWhiteSpace(json[cursor]))
            {
                cursor++;
            }

            int start = cursor;
            while (cursor < json.Length && (char.IsDigit(json[cursor]) || json[cursor] == '-'))
            {
                cursor++;
            }

            if (cursor <= start)
            {
                return false;
            }

            string token = json.Substring(start, cursor - start);
            return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }
    }
}
