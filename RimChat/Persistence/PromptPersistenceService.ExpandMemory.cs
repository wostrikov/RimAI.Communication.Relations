using RimChat.Compat;
using RimChat.Core;
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
        internal const int ExpandMemoryPawnMemoryMaxCharsDefault = 1200;
        internal const int ExpandMemoryPawnMemoryMaxCharsMin = 200;
        internal const int ExpandMemoryPawnMemoryMaxCharsMax = 4000;
        internal const int ExpandMemoryPawnMemoryMaxEntriesDefault = 50;
        internal const int ExpandMemoryPawnMemoryMaxEntriesMin = 10;
        internal const int ExpandMemoryPawnMemoryMaxEntriesMax = 500;
        internal const int ExpandMemoryPawnMemoryMaxEntriesPerLayer = 20;

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
            int maxChars = RimChatMod.Settings?.ExpandMemoryPawnMemoryMaxChars ?? ExpandMemoryPawnMemoryMaxCharsDefault;
            int maxEntries = RimChatMod.Settings?.ExpandMemoryPawnMemoryMaxEntries ?? ExpandMemoryPawnMemoryMaxEntriesDefault;
            return BuildExpandMemoryPawnBlock(pawn, maxChars, maxEntries);
        }

        internal string BuildExpandMemoryPawnBlock(Pawn pawn, int maxChars, int maxTotalEntries)
        {
            if (!ExpandMemoryBridge.IsPawnMemoryAvailable() || pawn == null)
            {
                return string.Empty;
            }

            string result = ExpandMemoryBridge.GetPawnMemory(pawn, ExpandMemoryPawnMemoryMaxEntriesPerLayer, maxTotalEntries);
            if (string.IsNullOrWhiteSpace(result))
            {
                return string.Empty;
            }

            if (result.Length > maxChars)
            {
                result = TruncateAtNaturalBoundary(result, maxChars);
            }

            return result;
        }

        private static string TruncateAtNaturalBoundary(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxChars)
            {
                return text;
            }

            if (maxChars <= 0) return string.Empty;

            int cutoff = maxChars - 3;
            if (cutoff <= 0) return "...";

            int newline = text.LastIndexOf('\n', cutoff);
            int dot = text.LastIndexOf('.', cutoff);
            int space = text.LastIndexOf(' ', cutoff);

            int boundary = newline > dot ? newline : dot;
            boundary = boundary > space ? boundary : space;
            if (boundary < cutoff / 2) boundary = space > cutoff / 2 ? space : cutoff;

            return text.Substring(0, boundary + 1) + "\n...";
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
                return prompt.Insert(closingIdx, "[ExpandMemory]\n" + memory.Replace("\n", "\n  ") + "\n");
            }

            return prompt;
        }
    }
}
