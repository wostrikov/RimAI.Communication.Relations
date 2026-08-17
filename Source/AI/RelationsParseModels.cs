using System.Collections.Generic;

namespace Ustas.RimAI.Communication.Relations.AI
{
    public class JsonResponse
    {
        public string RawJson { get; set; }
        public List<StrategySuggestion> StrategySuggestions { get; set; }
    }

    internal sealed class JsonPayloadSegment
    {
        public int Start { get; set; }
        public int End { get; set; }
        public string Json { get; set; }
        public bool HasActions { get; set; }
        public bool HasStrategySuggestions { get; set; }
    }

    public class ParsedResponse
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public string DialogueText { get; set; }
        public List<AIAction> Actions { get; set; }
        public List<StrategySuggestion> StrategySuggestions { get; set; }
    }

    public class StrategySuggestion
    {
        public string StrategyName { get; set; }
        public string Reason { get; set; }
        public List<string> StrategyKeywords { get; set; }
        public string Content { get; set; }
    }

    public class AIAction
    {
        public string ActionType { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public string Reason { get; set; }
    }
}
