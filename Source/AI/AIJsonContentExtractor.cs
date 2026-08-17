namespace Ustas.RimAI.Communication.Relations.AI
{
    /// <summary>
    /// Thin compatible-provider extraction facade. SSE and envelope work live in dedicated parsers.
    /// </summary>
    public static class AIJsonContentExtractor
    {
        public static bool IsErrorPayload(string json)
        {
            return CompatibleChatEnvelopeParser.IsErrorPayload(json);
        }

        public static PrimaryTextExtractionResult TryExtractPrimaryText(string json)
        {
            return CompatibleChatEnvelopeParser.Extract(json);
        }
    }
}
