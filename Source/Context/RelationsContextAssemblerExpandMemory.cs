using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Core.Memory;
using Verse;

using Ustas.RimAI.Communication.Relations.Persistence;

namespace Ustas.RimAI.Communication.Relations.Context
{
    /// <summary>
    /// Typed Memory/Knowledge prompt blocks. Optional when Memory module is absent.
    /// </summary>
    internal sealed class RelationsContextAssemblerExpandMemory : RelationsContextAssemblerCollaborator
    {
        internal RelationsContextAssemblerExpandMemory(RelationsContextAssembler owner) : base(owner)
        {
        }


        internal const int CommonKnowledgeMaxEntries = 10;
        internal const int ExpandMemoryPawnMemoryMaxCharsDefault = 1200;
        internal const int ExpandMemoryPawnMemoryMaxCharsMin = 200;
        internal const int ExpandMemoryPawnMemoryMaxCharsMax = 4000;
        internal const int ExpandMemoryPawnMemoryMaxEntriesDefault = 50;
        internal const int ExpandMemoryPawnMemoryMaxEntriesMin = 10;
        internal const int ExpandMemoryPawnMemoryMaxEntriesMax = 500;
        internal const int ExpandMemoryPawnMemoryMaxEntriesPerLayer = 20;

        internal string BuildCommonKnowledgeBlock(string playerMessage)
        {
            var provider = MemoryContextAccess.Knowledge;
            if (provider == null || string.IsNullOrWhiteSpace(playerMessage))
                return string.Empty;

            string result = provider.GetKnowledge(new MemoryContextRequest
            {
                Query = playerMessage,
                TokenBudget = CommonKnowledgeMaxEntries * 80
            })?.Projection;
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
            int maxChars = RelationsMod.Settings?.ExpandMemoryPawnMemoryMaxChars ?? ExpandMemoryPawnMemoryMaxCharsDefault;
            int maxEntries = RelationsMod.Settings?.ExpandMemoryPawnMemoryMaxEntries ?? ExpandMemoryPawnMemoryMaxEntriesDefault;
            return Owner.BuildExpandMemoryPawnBlock(pawn, maxChars, maxEntries);
        }

        internal string BuildExpandMemoryPawnBlock(Pawn pawn, int maxChars, int maxTotalEntries)
        {
            var provider = MemoryContextAccess.Current;
            if (provider == null || pawn == null || RelationsMod.Settings?.IsExpandMemoryPawnMemoryEnabled() != true)
                return string.Empty;

            string result = provider.GetContext(new MemoryContextRequest
            {
                PawnId = pawn.ThingID,
                TokenBudget = maxTotalEntries * 80
            })?.Projection;
            if (string.IsNullOrWhiteSpace(result))
                return string.Empty;

            if (result.Length > maxChars)
                result = Owner.TruncateAtNaturalBoundary(result, maxChars);

            return result;
        }

        internal string TruncateAtNaturalBoundary(string text, int maxChars)
        {
            return PromptTextTruncate.AtNaturalBoundary(text, maxChars);
        }

        internal string InjectExpandMemoryIntoPrompt(string prompt, Pawn target)
        {
            string memory = Owner.BuildExpandMemoryPawnBlock(target);
            if (string.IsNullOrWhiteSpace(memory))
                return prompt;

            string tag = "</dynamic_npc_personal_memory>";
            int idx = prompt.IndexOf(tag, System.StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
                return prompt.Insert(idx, "\n  [Memory]\n  " + memory.Replace("\n", "\n  ") + "\n");

            string closingTag = "</prompt_context>";
            int closingIdx = prompt.IndexOf(closingTag, System.StringComparison.OrdinalIgnoreCase);
            if (closingIdx >= 0)
                return prompt.Insert(closingIdx, "[Memory]\n" + memory.Replace("\n", "\n  ") + "\n");

            return prompt;
        }
        }

}
