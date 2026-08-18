using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Diagnostics;
using Ustas.RimAI.Core.Storage;
using Verse;
using RimWorld;

namespace Ustas.RimAI.Communication.Relations.Memory
{
        internal abstract class LeaderMemoryManagerCollaborator
    {
        internal readonly LeaderMemoryManager Owner;

        protected LeaderMemoryManagerCollaborator(LeaderMemoryManager owner)
        {
            Owner = owner;
        }
        protected LeaderMemoryManagerParts Parts => Owner.Parts;


        protected const string InitSnapshotPrefix = "[init-snapshot]";
        protected const string SessionBackfillPrefix = "[session-backfill]";
        protected const int MaxSignificantEvents = 80;
        protected const string SaveRootDir = "Ustas.RimAI.Communication.Relations";
        protected const string SaveSubDir = "save_data";
        protected const string PromptFolderName = "Prompt";
        protected const string NpcPromptSubDir = "NPC";
        protected const string LeaderMemorySubDir = "leader_memories";
        protected const string DefaultSaveName = "Default";
        protected const string LegacyMigrationBackupDirName = "_migration_backup";
        protected const string LegacyDefaultBucketClaimMarker = ".legacy_default_bucket_claimed";
        protected Dictionary<string, FactionLeaderMemory> _memoryCache
        {
            get => Owner._memoryCache;
            set => Owner._memoryCache = value;
        }
        protected Dictionary<string, int> diplomacyMemoryRevisions => Owner.diplomacyMemoryRevisions;
        protected bool _cacheLoaded
        {
            get => Owner._cacheLoaded;
            set => Owner._cacheLoaded = value;
        }
        protected object _summarySyncRoot => Owner._summarySyncRoot;
        protected object _cacheSyncRoot => Owner._cacheSyncRoot;
        protected string _resolvedSaveKey
        {
            get => Owner._resolvedSaveKey;
            set => Owner._resolvedSaveKey = value;
        }
        protected string CurrentSaveDataPath => Owner.CurrentSaveDataPath;
        protected string CurrentPromptNpcRootPath => Owner.CurrentPromptNpcRootPath;
        protected string CurrentSaveKey => Owner.CurrentSaveKey;
    }

}
