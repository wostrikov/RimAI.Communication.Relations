using System;
using System.Collections.Generic;

namespace Ustas.RimAI.Communication.Relations.Serialization
{
    /// <summary>
    /// Dependencies: none.
    /// Responsibility: cheap JSON-text probes used by prompt-domain load and bundle transfer.
    /// </summary>
    internal static class PromptJsonText
    {
        public static bool LooksLikeJsonObject(string json)
        {
            string trimmed = json?.Trim();
            return !string.IsNullOrWhiteSpace(trimmed) &&
                   trimmed.StartsWith("{", StringComparison.Ordinal) &&
                   trimmed.EndsWith("}", StringComparison.Ordinal);
        }

        public static bool ContainsJsonKey(string json, string key)
        {
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            return json.IndexOf("\"" + key + "\"", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool ContainsAnyJsonKey(string json, IEnumerable<string> keys)
        {
            if (keys == null)
            {
                return false;
            }

            foreach (string key in keys)
            {
                if (ContainsJsonKey(json, key))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
