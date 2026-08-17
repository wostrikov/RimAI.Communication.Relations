using System;

namespace Ustas.RimAI.Communication.Relations.AI
{
    /// <summary>
    /// One owner for markdown code-fence normalization. No provider or Relations semantics.
    /// </summary>
    public static class JsonMarkdownFence
    {
        public static string StripWrappingFence(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string trimmed = text.Trim();
            if (!trimmed.StartsWith("```", StringComparison.Ordinal) ||
                !trimmed.EndsWith("```", StringComparison.Ordinal))
            {
                return trimmed;
            }

            int firstNewLine = trimmed.IndexOf('\n');
            if (firstNewLine < 0 || firstNewLine >= trimmed.Length - 3)
            {
                return trimmed;
            }

            string body = trimmed.Substring(firstNewLine + 1);
            if (body.EndsWith("```", StringComparison.Ordinal))
            {
                body = body.Substring(0, body.Length - 3);
            }

            return body.Trim();
        }

        public static string StripFenceMarkers(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text.Replace("```json", string.Empty)
                       .Replace("```JSON", string.Empty)
                       .Replace("```", string.Empty)
                       .Trim();
        }

        public static bool TryExtractFencedBlock(string text, out string block)
        {
            block = string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            int fenceStart = text.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
            if (fenceStart < 0)
            {
                fenceStart = text.IndexOf("```", StringComparison.Ordinal);
            }

            if (fenceStart < 0)
            {
                return false;
            }

            int contentStart = text.IndexOf('\n', fenceStart);
            if (contentStart < 0 || contentStart >= text.Length - 1)
            {
                return false;
            }

            contentStart++;
            int fenceEnd = text.IndexOf("```", contentStart);
            block = fenceEnd > contentStart
                ? text.Substring(contentStart, fenceEnd - contentStart).Trim()
                : text.Substring(contentStart).Trim();
            return !string.IsNullOrWhiteSpace(block);
        }
    }
}
