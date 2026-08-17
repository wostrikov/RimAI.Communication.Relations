namespace Ustas.RimAI.Communication.Relations.Context
{
    /// <summary>
    /// Dependencies: none.
    /// Responsibility: truncate prompt context blocks at a nearby sentence/line boundary.
    /// </summary>
    internal static class PromptTextTruncate
    {
        public static string AtNaturalBoundary(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxChars)
            {
                return text;
            }

            if (maxChars <= 0)
            {
                return string.Empty;
            }

            int cutoff = maxChars - 3;
            if (cutoff <= 0)
            {
                return "...";
            }

            int newline = text.LastIndexOf('\n', cutoff);
            int dot = text.LastIndexOf('.', cutoff);
            int space = text.LastIndexOf(' ', cutoff);

            int boundary = newline > dot ? newline : dot;
            boundary = boundary > space ? boundary : space;
            if (boundary < cutoff / 2)
            {
                boundary = space > cutoff / 2 ? space : cutoff;
            }

            return text.Substring(0, boundary + 1) + "\n...";
        }
    }
}
