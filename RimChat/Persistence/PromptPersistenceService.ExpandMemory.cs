using RimChat.Compat;
using Verse;

namespace RimChat.Persistence
{
    /// <summary>
    /// Dependencies: ExpandMemoryBridge.
    /// Responsibility: build common knowledge prompt block via ExpandMemory keyword matching.
    /// </summary>
    public partial class PromptPersistenceService
    {
        private const int CommonKnowledgeMaxEntries = 10;

        private string BuildCommonKnowledgeBlock(string playerMessage)
        {
            if (!ExpandMemoryBridge.IsAvailable())
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(playerMessage))
            {
                return string.Empty;
            }

            string result = ExpandMemoryBridge.GetMatchedKnowledge(playerMessage, CommonKnowledgeMaxEntries);
            if (string.IsNullOrWhiteSpace(result) ||
                result.Contains("No matching knowledge") ||
                result.Contains("No context available"))
            {
                return string.Empty;
            }

            return result.Trim();
        }
    }
}
