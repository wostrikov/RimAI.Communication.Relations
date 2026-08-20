using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Diagnostics;
using Ustas.RimAI.Core.Storage;
using Verse;
using RimWorld;

using JsonCopyStats = Ustas.RimAI.Communication.Relations.Memory.LeaderMemoryManagerPersistenceHelpers.JsonCopyStats;

namespace Ustas.RimAI.Communication.Relations.Memory
{
    public class LeaderMemoryManager
    {
        internal LeaderMemoryManagerParts Parts;

        internal LeaderMemoryManager()
        {
            Parts = new LeaderMemoryManagerParts(this);
        }

        public sealed class DiplomacyHistoryRow
        {
            public string FactionId { get; set; } = string.Empty;
            public string FactionName { get; set; } = string.Empty;
            public bool IsPlayer { get; set; }
            public int GameTick { get; set; }
            public string Message { get; set; } = string.Empty;
            public bool IsCurrentSession { get; set; }
            public int SessionOrdinal { get; set; } = -1;
            public int SessionRowOrdinal { get; set; } = -1;
            public int LiveMessageIndex { get; set; } = -1;
            public int HistoryRecordIndex { get; set; } = -1;
            public string SenderLabel { get; set; } = string.Empty;
        }

        public sealed class DiplomacyHistorySessionGroup
        {
            public bool IsCurrentSession { get; set; }
            public int SessionOrdinal { get; set; }
            public int StartTick { get; set; }
            public int EndTick { get; set; }
            public List<DiplomacyHistoryRow> Rows { get; set; } = new List<DiplomacyHistoryRow>();
        }

        internal const string InitSnapshotPrefix = "[init-snapshot]";
        internal const string SessionBackfillPrefix = "[session-backfill]";
        internal const int MaxSignificantEvents = 80;
        internal const string SaveRootDir = "Ustas.RimAI.Communication.Relations";
        internal const string SaveSubDir = "save_data";
        internal const string PromptFolderName = "Prompt";
        internal const string NpcPromptSubDir = "NPC";
        internal const string LeaderMemorySubDir = "leader_memories";
        internal const string DefaultSaveName = "Default";
        internal const string LegacyMigrationBackupDirName = "_migration_backup";
        internal const string LegacyDefaultBucketClaimMarker = ".legacy_default_bucket_claimed";

        internal static LeaderMemoryManager _instance;
        public static LeaderMemoryManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new LeaderMemoryManager();
                }
                return _instance;
            }
        }

        internal string CurrentSaveDataPath
        {
            get
            {
                return Path.Combine(CurrentPromptNpcRootPath, CurrentSaveKey, LeaderMemorySubDir);
            }
        }

        internal string CurrentSaveKey
        {
            get
            {
                if (ShouldRefreshResolvedSaveKey())
                {
                    _resolvedSaveKey = ResolveCurrentSaveKey();
                }
                return _resolvedSaveKey;
            }
        }

        internal string CurrentPromptNpcRootPath
        {
            get
            {
                try
                {
                    ModContentPack mod = LoadedModManager.GetMod<RelationsMod>()?.Content;
                    if (mod != null)
                    {
                        string path = Path.Combine(mod.RootDir, PromptFolderName, NpcPromptSubDir);
                        if (!LocalStorage.Current.DirectoryExists(path))
                        {
                            LocalStorage.Current.CreateDirectory(path);
                        }
                        return path;
                    }
                }
                catch
                {
                }

                string fallback = Path.Combine(GenFilePaths.ConfigFolderPath, SaveRootDir, PromptFolderName, NpcPromptSubDir);
                if (!LocalStorage.Current.DirectoryExists(fallback))
                {
                    LocalStorage.Current.CreateDirectory(fallback);
                }
                return fallback;
            }
        }

        internal Dictionary<string, FactionLeaderMemory> _memoryCache = new Dictionary<string, FactionLeaderMemory>();
        internal readonly Dictionary<string, int> diplomacyMemoryRevisions = new Dictionary<string, int>(StringComparer.Ordinal);
        public event Action<DiplomacyMemoryChangedEventArgs> DiplomacyMemoryChanged;
        internal void RaiseDiplomacyMemoryChanged(DiplomacyMemoryChangedEventArgs args)
        {
            DiplomacyMemoryChanged?.Invoke(args);
        }

        internal bool _cacheLoaded = false;
        internal readonly object _summarySyncRoot = new object();
        internal readonly object _cacheSyncRoot = new object();
        internal string _resolvedSaveKey = string.Empty;

        

        

        

        

        

        

        

        

        

        

        public void AddRpgDepartSummary(Faction faction, CrossChannelSummaryRecord record, int maxEntries)
        {
            UpsertSummaryInternal(faction, record, maxEntries, useRpgPool: true);
        }

        public void AddDiplomacySessionSummary(Faction faction, CrossChannelSummaryRecord record, int maxEntries)
        {
            UpsertSummaryInternal(faction, record, maxEntries, useRpgPool: false);
        }

        public void UpsertRpgDepartSummary(Faction faction, CrossChannelSummaryRecord record, int maxEntries)
        {
            UpsertSummaryInternal(faction, record, maxEntries, useRpgPool: true);
        }

        public void UpsertDiplomacySessionSummary(Faction faction, CrossChannelSummaryRecord record, int maxEntries)
        {
            UpsertSummaryInternal(faction, record, maxEntries, useRpgPool: false);
        }

        

        

        

        

        

        

        

        

        internal string ResolveMemorySourceDirectory()
        {
            return CurrentSaveDataPath;
        }

        internal static bool DirectoryHasJsonFiles(string path)
        {
            return LocalStorage.Current.DirectoryExists(path) && LocalStorage.Current.GetFiles(path, "*.json").Length > 0;
        }

        

        internal string ResolveMemoryFilePath(string fileName)
        {
            return Path.Combine(CurrentSaveDataPath, fileName);
        }

        

        

        

        internal string ConvertMemoryToJson(FactionLeaderMemory memory)
        {
            return LeaderMemoryJsonCodec.ConvertMemoryToJson(memory);
        }

        internal FactionLeaderMemory ParseJsonToMemory(string json)
        {
            return LeaderMemoryJsonCodec.ParseJsonToMemory(json);
        }

        

        

        

        public void OnAfterGameLoad()
        {
            OnAfterGameLoad(null);
        }

        

        

        #region Facade forwards
        public List<DiplomacyHistorySessionGroup> GetDialogueHistorySessionGroups(Faction faction) => Parts.DialogueHistory.GetDialogueHistorySessionGroups(faction);
        public bool TryUpdateDialogueHistoryRow(Faction faction, DiplomacyHistoryRow row, string newMessage, out string error) => Parts.DialogueHistory.TryUpdateDialogueHistoryRow(faction, row, newMessage, out error);
        public bool TryDeleteDialogueHistoryRow(Faction faction, DiplomacyHistoryRow row, out string error) => Parts.DialogueHistory.TryDeleteDialogueHistoryRow(faction, row, out error);
        public bool TryClearAllDialogueHistory(Faction faction, out string error, out int clearedCount) => Parts.DialogueHistory.TryClearAllDialogueHistory(faction, out error, out clearedCount);
        internal static DiplomacyHistorySessionGroup BuildCurrentSessionGroup(List<DiplomacyHistoryRow> currentRows) => LeaderMemoryManagerDialogueHistory.BuildCurrentSessionGroup(currentRows);
        internal List<DiplomacyHistoryRow> BuildCurrentSessionRows(FactionDialogueSession session, FactionLeaderMemory memory, string factionId, string factionName) => Parts.DialogueHistory.BuildCurrentSessionRows(session, memory, factionId, factionName);
        internal List<DiplomacyHistoryRow> BuildPersistentHistoryRows(FactionLeaderMemory memory, string factionId, string factionName, List<DiplomacyHistoryRow> currentRows) => Parts.DialogueHistory.BuildPersistentHistoryRows(memory, factionId, factionName, currentRows);
        internal static Dictionary<string, int> BuildLiveSignatureCounts(List<DiplomacyHistoryRow> currentRows) => LeaderMemoryManagerDialogueHistory.BuildLiveSignatureCounts(currentRows);
        internal List<DiplomacyHistorySessionGroup> BuildHistoricalGroups(List<DiplomacyHistoryRow> rows) => Parts.DialogueHistory.BuildHistoricalGroups(rows);
        internal static bool ShouldSplitHistorySession(DiplomacyHistorySessionGroup current, DiplomacyHistoryRow incoming) => LeaderMemoryManagerDialogueHistory.ShouldSplitHistorySession(current, incoming);
        internal static int FindMatchingHistoryRecordIndex(List<DialogueRecord> history, DialogueMessageData message, HashSet<int> consumedIndexes) => LeaderMemoryManagerDialogueHistory.FindMatchingHistoryRecordIndex(history, message, consumedIndexes);
        internal static string BuildHistorySignature(int tick, bool isPlayer, string message) => LeaderMemoryManagerDialogueHistory.BuildHistorySignature(tick, isPlayer, message);
        internal static string NormalizeMessage(string message) => LeaderMemoryManagerDialogueHistory.NormalizeMessage(message);
        internal static bool TryResolveLiveMessage(FactionDialogueSession session, int liveMessageIndex, out DialogueMessageData message) => LeaderMemoryManagerDialogueHistory.TryResolveLiveMessage(session, liveMessageIndex, out message);
        internal static bool TryRemoveLiveMessage(FactionDialogueSession session, int liveMessageIndex) => LeaderMemoryManagerDialogueHistory.TryRemoveLiveMessage(session, liveMessageIndex);
        internal static bool TryRemoveDialogueRecord(FactionLeaderMemory memory, int recordIndex) => LeaderMemoryManagerDialogueHistory.TryRemoveDialogueRecord(memory, recordIndex);
        internal bool TryValidateHistoryRowMutation(Faction faction, DiplomacyHistoryRow row, string message, bool requireNonEmptyMessage, out string error) => Parts.DialogueHistory.TryValidateHistoryRowMutation(faction, row, message, requireNonEmptyMessage, out error);
        internal static bool TryResolveDialogueRecord(FactionLeaderMemory memory, int recordIndex, out DialogueRecord record, out string error) => LeaderMemoryManagerDialogueHistory.TryResolveDialogueRecord(memory, recordIndex, out record, out error);
        internal void NormalizeAndPersistDialogueHistory(Faction faction, FactionLeaderMemory memory) => Parts.DialogueHistory.NormalizeAndPersistDialogueHistory(faction, memory);
        internal bool ShouldRefreshResolvedSaveKey() => Parts.PersistenceHelpers.ShouldRefreshResolvedSaveKey();
        internal string ResolveCurrentSaveKey() => Parts.PersistenceHelpers.ResolveCurrentSaveKey();
        internal void TryMigrateLegacyMemories(string currentSaveKey) => Parts.PersistenceHelpers.TryMigrateLegacyMemories(currentSaveKey);
        internal List<string> CollectLegacyMemorySourceDirectories(string targetDir) => Parts.PersistenceHelpers.CollectLegacyMemorySourceDirectories(targetDir);
        internal static void TryAddLegacySourceDir(List<string> dirs, string sourceDir, string targetDir) => LeaderMemoryManagerPersistenceHelpers.TryAddLegacySourceDir(dirs, sourceDir, targetDir);
        internal static JsonCopyStats CopyJsonFiles(string sourceDir, string targetDir, bool overwrite) => LeaderMemoryManagerPersistenceHelpers.CopyJsonFiles(sourceDir, targetDir, overwrite);
        internal bool HasClaimedDefaultBucketForAnotherSave(string currentSaveKey, List<string> legacyDirs) => Parts.PersistenceHelpers.HasClaimedDefaultBucketForAnotherSave(currentSaveKey, legacyDirs);
        internal void TryClaimDefaultBucket(string currentSaveKey, List<string> legacyDirs) => Parts.PersistenceHelpers.TryClaimDefaultBucket(currentSaveKey, legacyDirs);
        internal static bool IsDefaultBucketPath(string path) => LeaderMemoryManagerPersistenceHelpers.IsDefaultBucketPath(path);
        internal static void NormalizeMemoryData(FactionLeaderMemory memory) => LeaderMemoryManagerPersistenceHelpers.NormalizeMemoryData(memory);
        internal bool TryBuildSanitizedSummaryRecord(CrossChannelSummaryRecord source, out CrossChannelSummaryRecord sanitized, out string reasonTag) => Parts.SummaryIntegrity.TryBuildSanitizedSummaryRecord(source, out sanitized, out reasonTag);
        internal void TryQueueSummaryRepair(Faction faction, CrossChannelSummaryRecord original, int maxEntries, bool useRpgPool, string reasonTag) => Parts.SummaryIntegrity.TryQueueSummaryRepair(faction, original, maxEntries, useRpgPool, reasonTag);
        internal static string BuildSummaryRepairKey(Faction faction, CrossChannelSummaryRecord record) => LeaderMemoryManagerSummaryIntegrity.BuildSummaryRepairKey(faction, record);
        internal static void ParseRepairResponse(string raw, out string summary, out List<string> facts) => LeaderMemoryManagerSummaryIntegrity.ParseRepairResponse(raw, out summary, out facts);
        #endregion
    
        #region Cluster forwards
        public int GetFactionMemoryRevision(Faction faction) => Parts.Slice1.GetFactionMemoryRevision(faction);
        internal int GetFactionMemoryRevision(string factionId) => Parts.Slice1.GetFactionMemoryRevision(factionId);
        internal DiplomacyMemoryChangedEventArgs PublishDiplomacyMemoryChanged(Faction faction, bool affectsCurrentSession, bool affectsPersistentHistory, bool affectsAiPrompt) => Parts.Slice1.PublishDiplomacyMemoryChanged(faction, affectsCurrentSession, affectsPersistentHistory, affectsAiPrompt);
        public void EnsureDataDirectoryExists() => Parts.Slice1.EnsureDataDirectoryExists();
        internal bool TryValidatePersistenceContext(string operationName) => Parts.Slice1.TryValidatePersistenceContext(operationName);
        public FactionLeaderMemory GetMemory(Faction faction) => Parts.Slice1.GetMemory(faction);
        public void SaveMemory(Faction faction) => Parts.Slice1.SaveMemory(faction);
        public void SaveAllMemories() => Parts.Slice1.SaveAllMemories();
        public void UpdateFromDialogue(Faction faction, List<DialogueMessageData> messages) => Parts.Slice1.UpdateFromDialogue(faction, messages);
        public void RecordSignificantEvent(Faction faction, SignificantEventType eventType, Faction involvedFaction, string description) => Parts.Slice1.RecordSignificantEvent(faction, eventType, involvedFaction, description);
        internal void UpsertSummaryInternal(Faction faction, CrossChannelSummaryRecord record, int maxEntries, bool useRpgPool) => Parts.Slice1.UpsertSummaryInternal(faction, record, maxEntries, useRpgPool);
        internal static List<Faction> GetActiveFactions() => LeaderMemorySlice1.GetActiveFactions();
        internal static bool HasMarkerEvent(FactionLeaderMemory memory, string prefix) => LeaderMemorySlice1.HasMarkerEvent(memory, prefix);
        internal static void TrimSignificantEvents(FactionLeaderMemory memory) => LeaderMemorySlice1.TrimSignificantEvents(memory);
        internal bool EnsureBaselineSnapshot(Faction faction, FactionLeaderMemory memory, string sourceTag) => Parts.Slice1.EnsureBaselineSnapshot(faction, memory, sourceTag);
        internal int RefreshBaselineSnapshotsAfterLoad() => Parts.Slice1.RefreshBaselineSnapshotsAfterLoad();
        internal void EnsureCacheLoaded() => Parts.Slice1.EnsureCacheLoaded();
        internal void LoadAllMemoriesFromFiles() => Parts.Slice1.LoadAllMemoriesFromFiles();
        internal FactionLeaderMemory LoadMemoryFromFile(Faction faction) => Parts.Slice1.LoadMemoryFromFile(faction);
        internal void SaveMemoryToFile(Faction faction, FactionLeaderMemory memory) => Parts.Slice1.SaveMemoryToFile(faction, memory);
        internal string GetMemoryFileName(Faction faction) => Parts.Slice1.GetMemoryFileName(faction);
        internal string GetUniqueFactionId(Faction faction) => Parts.Slice1.GetUniqueFactionId(faction);
        public void CleanupInvalidSaveData() => Parts.Slice1.CleanupInvalidSaveData();
        public void OnNewGame() => Parts.Slice1.OnNewGame();
        public void OnLoadedGame() => Parts.Slice1.OnLoadedGame();
        public void OnAfterGameLoad(IEnumerable<FactionDialogueSession> loadedSessions) => Parts.Slice1.OnAfterGameLoad(loadedSessions);
        public void OnBeforeGameSave() => Parts.Slice1.OnBeforeGameSave();
        #endregion
}
    internal sealed class LeaderMemorySlice1 : LeaderMemoryManagerCollaborator
    {
        internal LeaderMemorySlice1(LeaderMemoryManager owner) : base(owner)
        {
        }

public int GetFactionMemoryRevision(Faction faction)
        {
            if (faction == null)
            {
                return 0;
            }

            Owner.EnsureCacheLoaded();
            string factionId = Owner.GetUniqueFactionId(faction);
            return Owner.GetFactionMemoryRevision(factionId);
        }

internal int GetFactionMemoryRevision(string factionId)
        {
            if (string.IsNullOrWhiteSpace(factionId))
            {
                return 0;
            }

            if (!diplomacyMemoryRevisions.TryGetValue(factionId, out int revision))
            {
                revision = 0;
                diplomacyMemoryRevisions[factionId] = revision;
            }

            return revision;
        }

internal DiplomacyMemoryChangedEventArgs PublishDiplomacyMemoryChanged(
            Faction faction,
            bool affectsCurrentSession,
            bool affectsPersistentHistory,
            bool affectsAiPrompt)
        {
            if (faction == null)
            {
                return null;
            }

            Owner.EnsureCacheLoaded();
            string factionId = Owner.GetUniqueFactionId(faction);
            int revision = Owner.GetFactionMemoryRevision(factionId) + 1;
            diplomacyMemoryRevisions[factionId] = revision;

            var args = new DiplomacyMemoryChangedEventArgs
            {
                FactionId = factionId,
                Revision = revision,
                AffectsCurrentSession = affectsCurrentSession,
                AffectsPersistentHistory = affectsPersistentHistory,
                AffectsAiPrompt = affectsAiPrompt
            };

            Owner.RaiseDiplomacyMemoryChanged(args);
            return args;
        }

public void EnsureDataDirectoryExists()
        {
            try
            {
                if (!LocalStorage.Current.DirectoryExists(CurrentSaveDataPath))
                {
                    LocalStorage.Current.CreateDirectory(CurrentSaveDataPath);
                    DebugLogger.Debug($"Created memory data directory: {CurrentSaveDataPath}");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Failed to create data directory: {ex.Message}");
            }
        }

internal bool TryValidatePersistenceContext(string operationName)
        {
            try
            {
                _ = CurrentSaveKey;
                return true;
            }
            catch (InvalidOperationException ex)
            {
                DebugLogger.Error($"Leader memory persistence blocked in {operationName}: {ex.Message}");
                return false;
            }
        }

public FactionLeaderMemory GetMemory(Faction faction)
        {
            if (faction == null) return null;

            Owner.EnsureCacheLoaded();

            var factionId = Owner.GetUniqueFactionId(faction);
            
            if (!_memoryCache.TryGetValue(factionId, out var memory))
            {
                // Do not block gameplay with lazy disk reads; create a runtime memory object when missing.
                memory = new FactionLeaderMemory(faction);
                _memoryCache[factionId] = memory;
            }
            
            return memory;
        }

public void SaveMemory(Faction faction)
        {
            if (faction == null) return;
            if (!Owner.TryValidatePersistenceContext(nameof(SaveMemory))) return;

            var factionId = Owner.GetUniqueFactionId(faction);
            
            if (!_memoryCache.TryGetValue(factionId, out var memory))
            {
                DebugLogger.WarningGated($"Attempted to save memory for {faction.Name}, but no memory found in cache");
                return;
            }

            memory.RefreshLeaderInfo();
            LeaderMemoryManager.NormalizeMemoryData(memory);
            memory.LastSavedTimestamp = DateTime.UtcNow.Ticks;
            
            Owner.SaveMemoryToFile(faction, memory);
            
            DebugLogger.Debug($"Saved memory for {faction.Name}: {memory.DialogueHistory.Count} dialogues, {memory.FactionMemories.Count} factions, {memory.SignificantEvents.Count} events");
        }

public void SaveAllMemories()
        {
            if (!Owner.TryValidatePersistenceContext(nameof(SaveAllMemories))) return;
            Owner.EnsureCacheLoaded();

            foreach (var kvp in _memoryCache)
            {
                var faction = Find.FactionManager.AllFactions.FirstOrDefault(f => Owner.GetUniqueFactionId(f) == kvp.Key);
                if (faction != null && !faction.defeated)
                {
                    Owner.SaveMemory(faction);
                }
            }

            DebugLogger.Debug("All faction leader memories saved");
        }

public void UpdateFromDialogue(Faction faction, List<DialogueMessageData> messages)
        {
            if (faction == null || messages == null || messages.Count == 0)
            {
                return;
            }

            var memory = Owner.GetMemory(faction);
            if (memory != null)
            {
                memory.UpdateFromDialogue(messages);
                memory.UpdateRelationSnapshot(faction);
                
                int lastSavedTick = memory.DialogueHistory.Count > 0 
                    ? memory.DialogueHistory[memory.DialogueHistory.Count - 1].GameTick 
                    : -1;
                
                foreach (var msg in messages)
                {
                    int msgTick = msg.GetGameTick();
                    if (msgTick > lastSavedTick)
                    {
                        memory.DialogueHistory.Add(new DialogueRecord
                        {
                            IsPlayer = msg.isPlayer,
                            Message = msg.message,
                            GameTick = msgTick
                        });
                    }
                }
                
                if (memory.DialogueHistory.Count > 200)
                {
                    memory.DialogueHistory.RemoveRange(0, memory.DialogueHistory.Count - 200);
                }
                
                // Non-obvious edge case — read carefully before changing. (save file save save)
            }
        }

public void RecordSignificantEvent(Faction faction, SignificantEventType eventType, Faction involvedFaction, string description)
        {
            var memory = Owner.GetMemory(faction);
            if (memory != null)
            {
                memory.AddSignificantEvent(eventType, involvedFaction, description);
                // Non-obvious edge case — read carefully before changing. (save file save save)
            }
        }

internal void UpsertSummaryInternal(Faction faction, CrossChannelSummaryRecord record, int maxEntries, bool useRpgPool)
        {
            if (faction == null || record == null || string.IsNullOrWhiteSpace(record.SummaryText))
            {
                return;
            }

            if (!Owner.TryBuildSanitizedSummaryRecord(record, out CrossChannelSummaryRecord sanitizedRecord, out string reasonTag))
            {
                DebugLogger.WarningGated($"summary_drop_invalid source={record.Source} contentHash={record.ContentHash ?? string.Empty} factionId={record.FactionId ?? string.Empty} reason={reasonTag ?? "invalid_summary"}");
                Owner.TryQueueSummaryRepair(faction, record, maxEntries, useRpgPool, reasonTag ?? "invalid_summary");
                return;
            }

            lock (_summarySyncRoot)
            {
                var memory = Owner.GetMemory(faction);
                if (memory == null)
                {
                    return;
                }

                if (useRpgPool)
                {
                    memory.UpsertRpgDepartSummary(sanitizedRecord, maxEntries);
                }
                else
                {
                    memory.UpsertDiplomacySessionSummary(sanitizedRecord, maxEntries);
                }
            }
        }

internal static List<Faction> GetActiveFactions()
        {
            return Find.FactionManager.AllFactions
                .Where(f => f != null && !f.IsPlayer && !f.defeated && !f.def.hidden)
                .ToList();
        }

internal static bool HasMarkerEvent(FactionLeaderMemory memory, string prefix)
        {
            if (memory?.SignificantEvents == null || memory.SignificantEvents.Count == 0)
            {
                return false;
            }

            return memory.SignificantEvents.Any(evt =>
                evt != null &&
                !string.IsNullOrWhiteSpace(evt.Description) &&
                evt.Description.StartsWith(prefix, StringComparison.Ordinal));
        }

internal static void TrimSignificantEvents(FactionLeaderMemory memory)
        {
            if (memory?.SignificantEvents == null || memory.SignificantEvents.Count <= MaxSignificantEvents)
            {
                return;
            }

            memory.SignificantEvents = memory.SignificantEvents
                .OrderByDescending(evt => evt?.OccurredTick ?? 0)
                .Take(MaxSignificantEvents)
                .ToList();
        }

internal bool EnsureBaselineSnapshot(Faction faction, FactionLeaderMemory memory, string sourceTag)
        {
            if (faction == null || memory == null)
            {
                return false;
            }

            bool changed = false;
            int currentTick = Find.TickManager?.TicksGame ?? 0;

            string marker = $"{InitSnapshotPrefix}:{sourceTag}";
            if (LeaderMemoryManager.HasMarkerEvent(memory, marker))
            {
                return changed;
            }

            if (memory.SignificantEvents == null)
            {
                memory.SignificantEvents = new List<SignificantEventMemory>();
            }

            string relationKind = faction.RelationKindWith(Faction.OfPlayer).ToString();
            memory.SignificantEvents.Add(new SignificantEventMemory
            {
                EventType = SignificantEventType.GoodwillChanged,
                InvolvedFactionId = Faction.OfPlayer?.GetUniqueLoadID() ?? "PlayerFaction",
                InvolvedFactionName = Faction.OfPlayer?.Name ?? "PlayerFaction",
                Description = $"{marker} goodwill={faction.PlayerGoodwill}, relation={relationKind}.",
                OccurredTick = currentTick,
                Timestamp = DateTime.UtcNow.Ticks
            });

            LeaderMemoryManager.TrimSignificantEvents(memory);
            memory.LastUpdatedTick = currentTick;
            return true;
        }

internal int RefreshBaselineSnapshotsAfterLoad()
        {
            int touchedFactions = 0;
            foreach (Faction faction in LeaderMemoryManager.GetActiveFactions())
            {
                FactionLeaderMemory memory = Owner.GetMemory(faction);
                if (memory == null)
                {
                    continue;
                }

                if (Owner.EnsureBaselineSnapshot(faction, memory, "loaded_game"))
                {
                    Owner.SaveMemory(faction);
                    touchedFactions++;
                }
            }

            return touchedFactions;
        }

internal void EnsureCacheLoaded()
        {
            if (_cacheLoaded) return;

            string currentSaveKey;
            try
            {
                currentSaveKey = CurrentSaveKey;
            }
            catch (InvalidOperationException ex)
            {
                DebugLogger.Error($"Leader memory cache load blocked: {ex.Message}");
                return;
            }

            lock (_cacheSyncRoot)
            {
                if (_cacheLoaded) return;

                try
                {
                    Owner.EnsureDataDirectoryExists();
                    Owner.TryMigrateLegacyMemories(currentSaveKey);
                    Owner.LoadAllMemoriesFromFiles();
                    _cacheLoaded = true;
                }
                catch (Exception ex)
                {
                    DebugLogger.Error($"Failed to load memory cache: {ex.Message}");
                }
            }
        }

internal void LoadAllMemoriesFromFiles()
        {
            string sourceDir = Owner.ResolveMemorySourceDirectory();
            if (!LocalStorage.Current.DirectoryExists(sourceDir)) return;

            var files = LocalStorage.Current.GetFiles(sourceDir, "*.json");
            foreach (var file in files)
            {
                try
                {
                    var json = LocalStorage.Current.ReadAllText(file);
                    var memory = Owner.ParseJsonToMemory(json);
                    
                    if (memory != null && !string.IsNullOrEmpty(memory.OwnerFactionId))
                    {
                        LeaderMemoryManager.NormalizeMemoryData(memory);
                        _memoryCache[memory.OwnerFactionId] = memory;
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.Error($"Failed to load memory file {file}: {ex.Message}");
                }
            }

            DebugLogger.Debug($"Loaded {_memoryCache.Count} faction leader memories from {files.Length} files");
        }

internal FactionLeaderMemory LoadMemoryFromFile(Faction faction)
        {
            var fileName = Owner.GetMemoryFileName(faction);
            var filePath = Owner.ResolveMemoryFilePath(fileName);

            if (!LocalStorage.Current.FileExists(filePath))
            {
                return null;
            }

            try
            {
                var json = LocalStorage.Current.ReadAllText(filePath);
                var memory = Owner.ParseJsonToMemory(json);
                
                if (memory != null)
                {
                    LeaderMemoryManager.NormalizeMemoryData(memory);
                    DebugLogger.Debug($"Loaded memory for {faction.Name} from {fileName}");
                    return memory;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Failed to load memory for {faction.Name}: {ex.Message}");
            }

            return null;
        }

internal void SaveMemoryToFile(Faction faction, FactionLeaderMemory memory)
        {
            try
            {
                Owner.EnsureDataDirectoryExists();

                var fileName = Owner.GetMemoryFileName(faction);
                var filePath = Path.Combine(CurrentSaveDataPath, fileName);

                var json = Owner.ConvertMemoryToJson(memory);
                AtomicFileWriter.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Failed to save memory for {faction.Name}: {ex.Message}");
            }
        }

internal string GetMemoryFileName(Faction faction)
        {
            var safeName = faction.Name.SanitizeFileName();
            return $"{safeName}_{faction.loadID}.json";
        }

internal string GetUniqueFactionId(Faction faction)
        {
            if (faction.def != null && !string.IsNullOrEmpty(faction.def.defName))
            {
                return $"{faction.def.defName}_{faction.loadID}";
            }
            return $"custom_{faction.loadID}";
        }

public void CleanupInvalidSaveData()
        {
        }

public void OnNewGame()
        {
            _memoryCache.Clear();
            _cacheLoaded = false;
            _resolvedSaveKey = string.Empty;
            Owner.EnsureDataDirectoryExists();
            
            var allFactions = LeaderMemoryManager.GetActiveFactions();

            foreach (var faction in allFactions)
            {
                var memory = Owner.GetMemory(faction);
                Owner.EnsureBaselineSnapshot(faction, memory, "new_game");
            }

            DebugLogger.Debug("Initialized faction leader memories for new game");
        }

public void OnLoadedGame()
        {
            _memoryCache.Clear();
            _cacheLoaded = false;
            _resolvedSaveKey = string.Empty;
            Owner.EnsureDataDirectoryExists();
            Owner.EnsureCacheLoaded();
            DebugLogger.Debug("Initialized faction leader memory manager for saved game");
        }

public void OnAfterGameLoad(IEnumerable<FactionDialogueSession> loadedSessions)
        {
            _memoryCache.Clear();
            _cacheLoaded = false;
            _resolvedSaveKey = string.Empty;
            Owner.EnsureDataDirectoryExists();
            Owner.EnsureCacheLoaded();

            int touched = Owner.RefreshBaselineSnapshotsAfterLoad();
            DebugLogger.Debug($"Loaded {_memoryCache.Count} faction leader memories from save, refreshed {touched} factions");
        }

public void OnBeforeGameSave()
        {
            if (!Owner.TryValidatePersistenceContext(nameof(OnBeforeGameSave)))
            {
                return;
            }

            Owner.SaveAllMemories();
        }
    }

    internal sealed class LeaderMemoryManagerParts
    {
        internal readonly LeaderMemoryManager Owner;
        internal readonly LeaderMemoryManagerDialogueHistory DialogueHistory;
        internal readonly LeaderMemoryManagerPersistenceHelpers PersistenceHelpers;
        internal readonly LeaderMemoryManagerSummaryIntegrity SummaryIntegrity;
        internal readonly LeaderMemorySlice1 Slice1;
        internal LeaderMemoryManagerParts(LeaderMemoryManager owner)
        {
            Owner = owner;
            DialogueHistory = new LeaderMemoryManagerDialogueHistory(owner);
            PersistenceHelpers = new LeaderMemoryManagerPersistenceHelpers(owner);
            SummaryIntegrity = new LeaderMemoryManagerSummaryIntegrity(owner);
            Slice1 = new LeaderMemorySlice1(owner);
        }
    }


}
