using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Diagnostics;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Communication.Relations.WorldState
{
        internal abstract class WorldEventLedgerComponentCollaborator
    {
        internal readonly WorldEventLedgerComponent Owner;

        protected WorldEventLedgerComponentCollaborator(WorldEventLedgerComponent owner)
        {
            Owner = owner;
        }

        protected WorldEventLedgerComponentParts Parts => Owner.Parts;
        protected const int DefaultMaxStoredRecords = 50;
        protected const int LetterScanInterval = 250;
        protected const int RaidScanInterval = 250;
        protected const int LetterScanOffsetTicks = 0;
        protected const int RaidScanOffsetTicks = 40;
        protected const int MaxLettersPerScanPass = 24;
        protected const int OldEventAgeThresholdTicks = 60000 * 60 * 24;
        protected const int MaxCompressedSummaryLength = 100;
        protected const int MaxFullTextLength = 1500;
        protected const int MaxProcessedLetterIds = 512;
        protected static int _globalEventRevision
        {
            get => WorldEventLedgerComponent._globalEventRevision;
            set => WorldEventLedgerComponent._globalEventRevision = value;
        }
        protected List<WorldEventRecord> worldEvents
        {
            get => Owner.worldEvents;
            set => Owner.worldEvents = value;
        }
        protected List<RaidBattleReportRecord> raidBattleReports
        {
            get => Owner.raidBattleReports;
            set => Owner.raidBattleReports = value;
        }
        protected List<WorldEventLedgerComponent.OngoingRaidBattleState> ongoingRaidBattles
        {
            get => Owner.ongoingRaidBattles;
            set => Owner.ongoingRaidBattles = value;
        }
        protected List<int> processedLetterIds
        {
            get => Owner.processedLetterIds;
            set => Owner.processedLetterIds = value;
        }
        protected HashSet<int> processedLetterIdSet => Owner.processedLetterIdSet;
        protected IRaidSnapshotProvider raidSnapshotProvider => Owner.raidSnapshotProvider;
        protected int lastLetterScanTick
        {
            get => Owner.lastLetterScanTick;
            set => Owner.lastLetterScanTick = value;
        }
        protected int lastRaidScanTick
        {
            get => Owner.lastRaidScanTick;
            set => Owner.lastRaidScanTick = value;
        }
        protected int letterScanCursor
        {
            get => Owner.letterScanCursor;
            set => Owner.letterScanCursor = value;
        }
        protected const int CompressionPerTickBudget = 3;
        protected int compressionTickMarker
        {
            get => Owner.compressionTickMarker;
            set => Owner.compressionTickMarker = value;
        }
        protected int compressionThisTickCount
        {
            get => Owner.compressionThisTickCount;
            set => Owner.compressionThisTickCount = value;
        }
    }

}
