using System;
using System.Collections.Generic;
using System.Globalization;

namespace Ustas.RimAI.Communication.Relations.Memory
{
    /// <summary>
    /// Dependencies: RPG archive prompt memory builder and runtime archive mutation hooks.
    /// Responsibility: cache prompt memory blocks with version-based invalidation to reduce repeated main-thread rebuilds.
    /// </summary>
        internal sealed class RpgNpcDialogueArchiveManagerPromptCache : RpgNpcDialogueArchiveManagerCollaborator
    {
        internal RpgNpcDialogueArchiveManagerPromptCache(RpgNpcDialogueArchiveManager owner) : base(owner)
        {
        }


        internal struct PromptMemoryCacheEntry
        {
            public long Version;
            public string MemoryBlock;
        }

        internal readonly Dictionary<string, PromptMemoryCacheEntry> _promptMemoryCache =
            new Dictionary<string, PromptMemoryCacheEntry>(StringComparer.Ordinal);
        internal long _promptMemoryCacheVersion;

        internal void ResetPromptMemoryCacheLockless()
        {
            _promptMemoryCacheVersion = 0L;
            _promptMemoryCache.Clear();
        }

        internal void InvalidatePromptMemoryCacheLockless()
        {
            _promptMemoryCacheVersion++;
            _promptMemoryCache.Clear();
        }

        internal bool TryGetPromptMemoryCacheLockless(string cacheKey, out string memoryBlock)
        {
            memoryBlock = string.Empty;
            if (string.IsNullOrWhiteSpace(cacheKey) ||
                !_promptMemoryCache.TryGetValue(cacheKey, out PromptMemoryCacheEntry cacheEntry))
            {
                return false;
            }

            if (cacheEntry.Version != _promptMemoryCacheVersion)
            {
                _promptMemoryCache.Remove(cacheKey);
                return false;
            }

            memoryBlock = cacheEntry.MemoryBlock ?? string.Empty;
            return true;
        }

        internal void SetPromptMemoryCacheLockless(string cacheKey, string memoryBlock)
        {
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                return;
            }

            _promptMemoryCache[cacheKey] = new PromptMemoryCacheEntry
            {
                Version = _promptMemoryCacheVersion,
                MemoryBlock = memoryBlock ?? string.Empty
            };
        }

        internal static string BuildPromptMemoryCacheKey(
            int targetPawnLoadId,
            int interlocutorPawnLoadId,
            int summaryTurnLimit,
            int summaryCharBudget,
            int dayStamp)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}|{1}|{2}|{3}|{4}",
                targetPawnLoadId,
                interlocutorPawnLoadId,
                summaryTurnLimit,
                summaryCharBudget,
                dayStamp);
        }
        }

}
