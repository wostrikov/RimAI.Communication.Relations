using Ustas.RimAI.Communication.Relations.Config;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Communication.Relations.Diagnostics;
using Ustas.RimAI.Communication.Relations.WorldState;
using RimWorld;
using Verse;
using Ustas.RimAI.Communication.Relations.Context;
using Ustas.RimAI.Communication.Relations.Persistence;

namespace Ustas.RimAI.Communication.Relations.Prompting.Diplomacy
{
    /// <summary>
    /// Dependencies: PromptPersistenceService, LeaderMemoryManager, WorldEventLedgerComponent.
    /// Responsibility: build and maintain diplomacy prompt runtime snapshots with frame-budgeted warmup and explicit invalidation.
    /// </summary>
    public sealed class DiplomacyPromptSnapshotCache : IDiplomacyPromptSnapshotCache
    {
        private sealed class CacheEntry
        {
            public DiplomacyPromptRuntimeSnapshot Snapshot;
            public int NextRetryTick;
            public bool NeedsRefresh;
            public int NeedsRefreshSinceTick;
            public int LastValidatedTick;
            public int ConsecutiveFailureCount;
            public bool IsBuilding;
        }

        private const int RetryDelayTicks = 250;
        private const int MaxRetryDelayTicks = 60000;
        private const int ValidationThrottleTicks = 150;
        private const int RefreshGracePeriodTicks = 1500;

        private readonly Dictionary<string, CacheEntry> cacheEntries =
            new Dictionary<string, CacheEntry>(StringComparer.Ordinal);
        private readonly Queue<Faction> warmupQueue = new Queue<Faction>();
        private readonly HashSet<string> queuedFactionIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly PromptFileStampCache _fileStampCache = new PromptFileStampCache();

        private long lastObservedPromptFilesStamp = -1;
        private int lastObservedSettingsSignature = int.MinValue;

        private static readonly DiplomacyPromptSnapshotCache Singleton = new DiplomacyPromptSnapshotCache();

        public static DiplomacyPromptSnapshotCache Instance => Singleton;

        private DiplomacyPromptSnapshotCache()
        {
        }

        public void WarmupOnLoad()
        {
            warmupQueue.Clear();
            queuedFactionIds.Clear();
            cacheEntries.Clear();
            lastObservedPromptFilesStamp = _fileStampCache.GetStamp(Find.TickManager?.TicksGame ?? 0);
            lastObservedSettingsSignature = ComputeSettingsSignature();
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            _fileStampCache.Prime(currentTick);
            QueueAllCandidateFactions();
        }

        public bool TryGetSnapshot(Faction faction, out DiplomacyPromptRuntimeSnapshot snapshot)
        {
            snapshot = null;
            if (!IsValidFaction(faction))
            {
                return false;
            }

            RefreshGlobalInvalidationSignals();
            string factionId = faction.GetUniqueLoadID() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(factionId))
            {
                return false;
            }

            int currentTick = Find.TickManager?.TicksGame ?? 0;

            if (!cacheEntries.TryGetValue(factionId, out CacheEntry entry) || entry.Snapshot == null)
            {
                TryBuildSnapshot(faction, currentTick);
                if (!cacheEntries.TryGetValue(factionId, out entry) || entry.Snapshot == null)
                    return false;
            }

            if (!entry.NeedsRefresh)
            {
                ValidateSnapshot(faction, entry, currentTick);
            }

            snapshot = entry.Snapshot;
            return snapshot != null;
        }

        public void Invalidate(Faction faction = null, string reason = "manual")
        {
            if (faction == null)
            {
                cacheEntries.Clear();
                warmupQueue.Clear();
                queuedFactionIds.Clear();
                QueueAllCandidateFactions();
                return;
            }

            string factionId = faction.GetUniqueLoadID() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(factionId))
            {
                return;
            }

            cacheEntries.Remove(factionId);
            RequestWarmup(faction, reason);
        }

        public void RequestWarmup(Faction faction, string reason = "request")
        {
            if (!IsValidFaction(faction))
            {
                return;
            }

            string factionId = faction.GetUniqueLoadID() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(factionId) || queuedFactionIds.Contains(factionId))
            {
                return;
            }

            warmupQueue.Enqueue(faction);
            queuedFactionIds.Add(factionId);
        }

        public void Tick(int currentTick, int maxBuildsPerTick = 1)
        {
            // No proactive building — snapshots are built lazily on first access.
            // This avoids blocking the main thread with 100-988ms BuildRuntimeSnapshotForFaction calls.
        }

        private static Faction FindFactionByLoadId(string factionId)
        {
            if (string.IsNullOrWhiteSpace(factionId))
            {
                return null;
            }

            List<Faction> factions = Find.FactionManager?.AllFactionsListForReading;
            if (factions == null)
            {
                return null;
            }

            for (int i = 0; i < factions.Count; i++)
            {
                Faction f = factions[i];
                if (f != null && string.Equals(f.GetUniqueLoadID(), factionId, StringComparison.Ordinal))
                {
                    return f;
                }
            }

            return null;
        }

        private static bool IsValidFaction(Faction faction)
        {
            return faction != null && !faction.IsPlayer && !faction.defeated && !(faction.def?.hidden ?? true);
        }

        private void QueueAllCandidateFactions()
        {
            IEnumerable<Faction> factions = Find.FactionManager?.AllFactions
                ?.Where(IsValidFaction)
                ?? Enumerable.Empty<Faction>();
            foreach (Faction faction in factions)
            {
                RequestWarmup(faction, "load_warmup");
            }
        }

        private void RefreshGlobalInvalidationSignals()
        {
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            long promptStamp = _fileStampCache.GetStamp(currentTick);
            int settingsSignature = ComputeSettingsSignature();

            bool changed = false;
            changed |= lastObservedPromptFilesStamp >= 0 && promptStamp != lastObservedPromptFilesStamp;
            changed |= lastObservedSettingsSignature != int.MinValue && settingsSignature != lastObservedSettingsSignature;

            lastObservedPromptFilesStamp = promptStamp;
            lastObservedSettingsSignature = settingsSignature;

            if (!changed)
            {
                return;
            }

            MarkAllEntriesForRefresh(currentTick);
            QueueAllCandidateFactions();
        }

        private void MarkAllEntriesForRefresh(int currentTick)
        {
            foreach (var kvp in cacheEntries)
            {
                CacheEntry entry = kvp.Value;
                if (entry.Snapshot != null && !entry.NeedsRefresh)
                {
                    entry.NeedsRefresh = true;
                    entry.NeedsRefreshSinceTick = currentTick;
                }
            }
        }

        private bool TryDequeueNextBuildTarget(int currentTick, out Faction faction)
        {
            faction = null;
            while (warmupQueue.Count > 0)
            {
                Faction candidate = warmupQueue.Dequeue();
                string candidateId = candidate?.GetUniqueLoadID() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(candidateId))
                {
                    queuedFactionIds.Remove(candidateId);
                }

                if (!IsValidFaction(candidate))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(candidateId) &&
                    cacheEntries.TryGetValue(candidateId, out CacheEntry entry) &&
                    entry.NextRetryTick > currentTick)
                {
                    RequestWarmup(candidate, "retry_deferred");
                    continue;
                }

                faction = candidate;
                return true;
            }

            return false;
        }

        private static int ComputeBackoffDelay(int consecutiveFailures)
        {
            int delay = RetryDelayTicks;
            for (int i = 1; i < consecutiveFailures && delay < MaxRetryDelayTicks; i++)
            {
                delay *= 2;
            }
            return Math.Min(delay, MaxRetryDelayTicks);
        }

        private void TryBuildSnapshot(Faction faction, int currentTick)
        {
            string factionId = faction.GetUniqueLoadID() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(factionId))
            {
                return;
            }

            try
            {
                int memoryRevision = LeaderMemoryManager.Instance.GetFactionMemoryRevision(faction);
                int worldEventRevision = ResolveWorldEventRevision();
                long promptStamp = _fileStampCache.GetStamp(currentTick);
                int settingsSignature = ComputeSettingsSignature();
                DiplomacyPromptRuntimeSnapshot snapshot;
                using (PerfScope.Measure($"SnapshotCache.BuildSnapshot:{faction.Name}"))
                    snapshot = PromptPersistenceService.Instance.BuildRuntimeSnapshotForFaction(
                    faction,
                    null,
                    currentTick,
                    memoryRevision,
                    worldEventRevision,
                    promptStamp,
                    settingsSignature);
                if (snapshot == null)
                {
                    int prevFailures = cacheEntries.TryGetValue(factionId, out CacheEntry prev)
                        ? prev.ConsecutiveFailureCount
                        : 0;
                    cacheEntries[factionId] = new CacheEntry
                    {
                        Snapshot = null,
                        NextRetryTick = currentTick + ComputeBackoffDelay(prevFailures + 1),
                        ConsecutiveFailureCount = prevFailures + 1
                    };
                    return;
                }

                cacheEntries[factionId] = new CacheEntry
                {
                    Snapshot = snapshot,
                    NextRetryTick = 0,
                    NeedsRefresh = false,
                    NeedsRefreshSinceTick = 0,
                    LastValidatedTick = currentTick
                };
            }
            catch (Exception ex)
            {
                int prevFailures = cacheEntries.TryGetValue(factionId, out CacheEntry prev)
                    ? prev.ConsecutiveFailureCount
                    : 0;
                int newCount = prevFailures + 1;
                cacheEntries[factionId] = new CacheEntry
                {
                    Snapshot = null,
                    NextRetryTick = currentTick + ComputeBackoffDelay(newCount),
                    ConsecutiveFailureCount = newCount
                };
                Log.Warning($"[RimAI.Relations] Prompt snapshot warmup failed for {faction?.Name ?? "Unknown"} (attempt {newCount}): {ex.Message}");
            }
        }

        private bool ValidateSnapshot(Faction faction, CacheEntry entry, int currentTick)
        {
            DiplomacyPromptRuntimeSnapshot snapshot = entry.Snapshot;
            if (snapshot == null || faction == null)
            {
                return false;
            }

            string currentFactionId = faction.GetUniqueLoadID() ?? string.Empty;
            if (!string.Equals(snapshot.FactionLoadId, currentFactionId, StringComparison.Ordinal))
            {
                return false;
            }

            if (snapshot.PlayerRelationKind != faction.RelationKindWith(Faction.OfPlayer))
            {
                return false;
            }

            if (currentTick - entry.LastValidatedTick < ValidationThrottleTicks)
            {
                return true;
            }

            entry.LastValidatedTick = currentTick;

            bool l2Changed = snapshot.PlayerGoodwill != faction.PlayerGoodwill
                          || snapshot.MemoryRevision != LeaderMemoryManager.Instance.GetFactionMemoryRevision(faction)
                          || snapshot.QuestTrackingRevision != GameAIInterface.Instance.QuestTrackingRevision;

            bool l3Changed = snapshot.PromptFilesStampUtcTicks != _fileStampCache.GetStamp(currentTick)
                          || snapshot.SettingsSignature != ComputeSettingsSignature();

            if (l2Changed || l3Changed)
            {
                entry.NeedsRefresh = true;
                if (entry.NeedsRefreshSinceTick <= 0)
                {
                    entry.NeedsRefreshSinceTick = currentTick;
                }

                Log.Warning($"[RimChatPerf] Snapshot.ValidateStale:{faction.Name} l2={l2Changed} l3={l3Changed} goodwill={snapshot.PlayerGoodwill}!={faction.PlayerGoodwill} memRev={snapshot.MemoryRevision}!={LeaderMemoryManager.Instance.GetFactionMemoryRevision(faction)} questRev={snapshot.QuestTrackingRevision}!={GameAIInterface.Instance.QuestTrackingRevision} stamp={snapshot.PromptFilesStampUtcTicks}!={_fileStampCache.GetStamp(currentTick)} sig={snapshot.SettingsSignature}!={ComputeSettingsSignature()}");

                if (currentTick - entry.NeedsRefreshSinceTick > RefreshGracePeriodTicks)
                {
                    Log.Warning($"[RimAI.Relations] Snapshot for {faction.Name} expired after {RefreshGracePeriodTicks} ticks grace period, forcing rebuild.");
                    return false;
                }

                RequestWarmup(faction, l2Changed ? "l2_data_changed" : "l3_config_changed");
            }

            return true;
        }

        private static int ResolveWorldEventRevision()
        {
            return WorldEventLedgerComponent.GlobalEventRevision;
        }

        private static int ComputeSettingsSignature()
        {
            RelationsSettings settings = RelationsMod.Instance?.InstanceSettings;
            if (settings == null)
            {
                return 0;
            }

            unchecked
            {
                int hash = 17;
                hash = hash * 31 + settings.DialogueStyleMode.GetHashCode();
                hash = hash * 31 + settings.EnableSocialCircle.GetHashCode();
                hash = hash * 31 + settings.EnableAISimulationNews.GetHashCode();
                hash = hash * 31 + settings.EnablePlayerInfluenceNews.GetHashCode();
                hash = hash * 31 + settings.EnableNpcInitiatedDialogue.GetHashCode();
                return hash;
            }
        }

        private static long ComputePromptFilesStampUtcTicks()
        {
            long maxTicks = 0L;
            foreach (string path in EnumeratePromptFilePaths())
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    continue;
                }

                long ticks = File.GetLastWriteTimeUtc(path).Ticks;
                if (ticks > maxTicks)
                {
                    maxTicks = ticks;
                }
            }

            return maxTicks;
        }

        private static IEnumerable<string> EnumeratePromptFilePaths()
        {
            yield return PromptDomainFileCatalog.GetDefaultPath(PromptDomainFileCatalog.SystemPromptDefaultFileName);
            yield return PromptDomainFileCatalog.GetDefaultPath(PromptDomainFileCatalog.DiplomacyPromptDefaultFileName);
            yield return PromptDomainFileCatalog.GetDefaultPath(PromptDomainFileCatalog.SocialCirclePromptDefaultFileName);
            yield return PromptDomainFileCatalog.GetDefaultPath(PromptDomainFileCatalog.FactionPromptDefaultFileName);
            yield return PromptDomainFileCatalog.GetDefaultPath(PromptDomainFileCatalog.PromptUnifiedDefaultFileName);
            yield return PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.SystemPromptCustomFileName);
            yield return PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.DiplomacyPromptCustomFileName);
            yield return PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.SocialCirclePromptCustomFileName);
            yield return PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.FactionPromptCustomFileName);
            yield return PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.PromptUnifiedCustomFileName);
        }
    }
}
