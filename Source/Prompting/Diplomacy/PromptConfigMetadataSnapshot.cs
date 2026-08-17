using System;
using System.Collections.Generic;
using System.Text;

namespace Ustas.RimAI.Communication.Relations.Prompting.Diplomacy
{
    /// <summary>
    /// Dependencies: none.
    /// Responsibility: render a diagnostic snapshot of prompt-config metadata without secrets or filesystem writes.
    /// </summary>
    internal static class PromptConfigMetadataSnapshot
    {
        private static readonly string[] SecretTokens =
        {
            "api_key",
            "apikey",
            "secret",
            "password",
            "token",
            "credential",
            "openai_rimai",
            "authorization"
        };

        public static string Render(
            int schemaVersion,
            int policySchemaVersion,
            bool enabled,
            string configPath,
            IReadOnlyDictionary<string, string> extra = null)
        {
            var sb = new StringBuilder();
            sb.Append("schema=").Append(schemaVersion);
            sb.Append("; policy=").Append(policySchemaVersion);
            sb.Append("; enabled=").Append(enabled ? "true" : "false");
            sb.Append("; path=").Append(configPath ?? string.Empty);
            if (extra != null)
            {
                foreach (KeyValuePair<string, string> pair in extra)
                {
                    if (LooksLikeSecret(pair.Key) || LooksLikeSecret(pair.Value))
                    {
                        continue;
                    }

                    sb.Append("; ").Append(pair.Key ?? string.Empty).Append('=').Append(pair.Value ?? string.Empty);
                }
            }

            return sb.ToString();
        }

        public static bool LooksLikeSecret(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = value.Trim().ToLowerInvariant();
            for (int i = 0; i < SecretTokens.Length; i++)
            {
                if (normalized.IndexOf(SecretTokens[i], StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
