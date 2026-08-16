namespace Ustas.RimAI.Communication.Relations.AI
{
    public sealed class AIChatClientResponse
    {
        public bool Success { get; set; }
        public string ParsedContent { get; set; }
        public string RawResponse { get; set; }
        public string ErrorText { get; set; }
        public string FailureReason { get; set; }
        public long HttpStatusCode { get; set; }
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
        public bool IsEstimatedTokens { get; set; } = true;
    }
}
