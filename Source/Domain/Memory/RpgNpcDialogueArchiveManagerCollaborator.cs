using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Communication.Relations.Guards;
using Ustas.RimAI.Core.Storage;
using Ustas.RimAI.Core.Relations;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Memory
{
        internal abstract class RpgNpcDialogueArchiveManagerCollaborator
    {
        internal readonly RpgNpcDialogueArchiveManager Owner;

        protected RpgNpcDialogueArchiveManagerCollaborator(RpgNpcDialogueArchiveManager owner)
        {
            Owner = owner;
        }
        protected RpgNpcDialogueArchiveManagerParts Parts => Owner.Parts;


        protected const string SaveRootDir = "Ustas.RimAI.Communication.Relations";
        protected const string SaveSubDir = "save_data";
        protected const string NpcArchiveSubDir = "rpg_npc_dialogues";
        protected const string PromptFolderName = "Prompt";
        protected const string NpcPromptSubDir = "NPC";
        protected const string DefaultSaveName = "Default";
        protected const string LegacyMigrationBackupDirName = "_migration_backup";
        protected const string LegacyDefaultBucketClaimMarker = ".legacy_default_bucket_claimed";
        protected const BindingFlags InstanceStringMemberBinding = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;
        protected const BindingFlags StaticStringMemberBinding = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;
        protected const string DiplomacySummaryPrefix = "[DiplomacySummary] ";
        protected const int MaxTurnsPerNpc = 300;
        protected const int MaxSessionsPerNpc = 96;
        protected const int CompressionRetryCooldownTicks = 2500;
        protected const int MaxCompressionRequestsPerPass = 2;
        protected const int CompressedSummaryMaxChars = 220;
        protected const int MaxInjectedCompressedSessionSummaries = 4;
        protected const int MaxInjectedCompressedSessionSummaryChars = 900;
        protected Dictionary<int, RpgNpcDialogueArchive> _archiveCache => Owner._archiveCache;
        protected HashSet<string> _compressionInFlight => Owner._compressionInFlight;
        protected HashSet<string> _warmupInFlightSaveKeys => Owner._warmupInFlightSaveKeys;
        protected HashSet<int> _pendingWarmupCompressionTargets => Owner._pendingWarmupCompressionTargets;
        protected object _syncRoot => Owner._syncRoot;
        protected bool _cacheLoaded
        {
            get => Owner._cacheLoaded;
            set => Owner._cacheLoaded = value;
        }
        protected bool _diplomacyMemorySubscribed
        {
            get => Owner._diplomacyMemorySubscribed;
            set => Owner._diplomacyMemorySubscribed = value;
        }
        protected string _loadedSaveKey
        {
            get => Owner._loadedSaveKey;
            set => Owner._loadedSaveKey = value;
        }
        protected string _resolvedSaveKey
        {
            get => Owner._resolvedSaveKey;
            set => Owner._resolvedSaveKey = value;
        }
        protected string _lastResolvedSaveName
        {
            get => Owner._lastResolvedSaveName;
            set => Owner._lastResolvedSaveName = value;
        }
        protected string CurrentSaveKey => Owner.CurrentSaveKey;
        protected string CurrentArchiveDirPath => Owner.CurrentArchiveDirPath;
        protected string CurrentPromptNpcRootPath => Owner.CurrentPromptNpcRootPath;
    }

}
