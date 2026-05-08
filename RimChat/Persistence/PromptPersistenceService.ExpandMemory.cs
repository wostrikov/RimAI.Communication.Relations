using RimChat.Compat;
using Verse;

namespace RimChat.Persistence
{
    /// <summary>
    /// Dependencies: ExpandMemoryBridge.
    /// Responsibility: build prompt blocks via ExpandMemory (common knowledge and per-pawn memory).
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

        internal string BuildExpandMemoryPawnBlock(Pawn pawn)
        {
            if (!ExpandMemoryBridge.IsPawnMemoryAvailable() || pawn == null)
            {
                return string.Empty;
            }

            string result = ExpandMemoryBridge.GetPawnMemory(pawn);
            return string.IsNullOrWhiteSpace(result) ? string.Empty : result;
        }

        private string InjectExpandMemoryIntoPrompt(string prompt, Pawn target)
        {
            string memory = BuildExpandMemoryPawnBlock(target);
            if (string.IsNullOrWhiteSpace(memory))
            {
                return prompt;
            }

            string tag = "</dynamic_npc_personal_memory>";
            int idx = prompt.IndexOf(tag, System.StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                return prompt.Insert(idx, "\n  [ExpandMemory]\n  " + memory.Replace("\n", "\n  ") + "\n");
            }

            // Fallback: append before </prompt_context>
            string closingTag = "</prompt_context>";
            int closingIdx = prompt.IndexOf(closingTag, System.StringComparison.OrdinalIgnoreCase);
            if (closingIdx >= 0)
            {
                return prompt.Insert(closingIdx, "<expandmemory_npc_memory>\n" + memory + "\n</expandmemory_npc_memory>\n");
            }

            return prompt;
        }
    }
}
