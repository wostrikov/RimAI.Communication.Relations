using System;
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Memory;
using RimWorld;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Communication.Relations.DiplomacySystem
{
    /// <summary>
    /// Dependencies: GameComponent tick loop, GameAIInterface penalties, Pawn.ExitMap callback, and diplomacy social/news messaging.
    /// Responsibility: persist ransom contracts, evaluate timeout/exit risk control, schedule healthy-exit acknowledgements, and apply timeout escalation.
    /// </summary>
    public sealed class RansomContractManager : GameComponent
    {
        public sealed class PendingReleaseSnapshot
        {
            public int TargetPawnLoadId { get; set; }
            public string TargetPawnLabel { get; set; } = string.Empty;
            public string ContractId { get; set; } = string.Empty;
            public int DeadlineTick { get; set; }
        }

        private const int TimeoutScanIntervalTicks = 250;
        private const int TimeoutScanOffsetTicks = 160;
        private const int TimeoutScanBudgetPerPass = 12;
        private const int CleanupBudgetPerPass = 24;
        private const float BatchRansomDropThresholdBonus = 0.15f;
        private const float BatchRansomPenaltyScale = 0.70f;
        private List<RansomContractRecord> contracts = new List<RansomContractRecord>();
        private int lastTimeoutScanTick;
        private int timeoutScanCursor;
        private int organPenaltyScanCursor;
        private int healthyReplyScanCursor;
        private int cleanupScanCursor;

        internal List<RansomContractRecord> Contracts => contracts;
        internal int OrganPenaltyScanCursor
        {
            get => organPenaltyScanCursor;
            set => organPenaltyScanCursor = value;
        }
        internal int HealthyReplyScanCursor
        {
            get => healthyReplyScanCursor;
            set => healthyReplyScanCursor = value;
        }

        public RansomContractManager(Game game) : base()
        {
        }

        public static RansomContractManager Instance => Current.Game?.GetComponent<RansomContractManager>();

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref contracts, "ransomContracts", LookMode.Deep);
            contracts ??= new List<RansomContractRecord>();
            Scribe_Values.Look(ref lastTimeoutScanTick, "ransomLastTimeoutScanTick", 0);
        }

        public override void GameComponentTick()
        {
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            if (!ShouldRunScheduledTask(currentTick, lastTimeoutScanTick, TimeoutScanIntervalTicks, TimeoutScanOffsetTicks))
            {
                return;
            }

            lastTimeoutScanTick = currentTick;
            ProcessTimeoutContracts(currentTick);
            RansomContractLifecycleOps.ProcessOrganFailurePenalties(this, currentTick);
            RansomContractLifecycleOps.ProcessHealthyExitReplies(this, currentTick);
            CleanupFinishedContracts(currentTick);
        }

        public void RegisterContract(RansomContractRecord contract)
        {
            if (contract == null || string.IsNullOrWhiteSpace(contract.ContractId))
            {
                return;
            }

            contracts.RemoveAll(existing => string.Equals(existing.ContractId, contract.ContractId, StringComparison.Ordinal));
            contracts.Add(contract);
        }

        public List<PendingReleaseSnapshot> GetPendingReleaseSnapshotsForFaction(string factionId)
        {
            if (string.IsNullOrWhiteSpace(factionId))
            {
                return new List<PendingReleaseSnapshot>();
            }

            return contracts
                .Where(contract => contract != null)
                .Where(contract => contract.Status == RansomContractStatus.PendingRelease)
                .Where(contract => string.Equals(contract.FactionId, factionId, StringComparison.Ordinal))
                .Select(contract => new PendingReleaseSnapshot
                {
                    TargetPawnLoadId = contract.TargetPawnLoadId,
                    TargetPawnLabel = RansomContractLifecycleOps.ResolvePawnLabel(contract, null) ?? "Unknown",
                    ContractId = contract.ContractId ?? string.Empty,
                    DeadlineTick = contract.DeadlineTick
                })
                .ToList();
        }

        public bool HasPendingReleaseContractForTarget(string factionId, int targetPawnLoadId)
        {
            if (string.IsNullOrWhiteSpace(factionId) || targetPawnLoadId <= 0)
            {
                return false;
            }

            return contracts
                .Where(contract => contract != null)
                .Where(contract => contract.Status == RansomContractStatus.PendingRelease)
                .Any(contract =>
                    string.Equals(contract.FactionId, factionId, StringComparison.Ordinal) &&
                    contract.TargetPawnLoadId == targetPawnLoadId);
        }

        public void HandlePawnExit(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.Destroyed)
            {
                return;
            }

            RansomContractRecord contract = FindPendingContractByPawn(pawn.thingIDNumber);
            if (contract == null)
            {
                return;
            }

            RelationsSettings settings = RelationsMod.Instance?.InstanceSettings;
            if (settings == null)
            {
                return;
            }

            contract.Status = RansomContractStatus.Released;
            contract.ReleasedTick = Find.TickManager?.TicksGame ?? 0;
            contract.TargetPawnLabelSnapshot = pawn.LabelShortCap ?? contract.TargetPawnLabelSnapshot ?? string.Empty;
            contract.ExitValueSnapshot = PrisonerRansomService.CalculateExitValueSnapshot(pawn, contract.WealthFactorSnapshot);
            contract.DropRate = ComputeDropRate(contract.NegotiatedValueSnapshot, contract.ExitValueSnapshot);
            bool organFailureScheduled = RansomContractLifecycleOps.TryScheduleOrganFailurePenalty(contract, pawn, contract.ReleasedTick);
            if (!organFailureScheduled)
            {
                ApplyExitPenalties(contract, pawn, settings);
                RansomContractLifecycleOps.TryScheduleHealthyExitReply(contract, pawn, contract.ReleasedTick);
            }
            contract.Status = RansomContractStatus.Completed;
        }

        private static float ComputeDropRate(float negotiatedValue, float exitValue)
        {
            if (negotiatedValue <= 0f)
            {
                return 0f;
            }

            float ratio = Mathf.Clamp01(exitValue / negotiatedValue);
            return Mathf.Clamp01(1f - ratio);
        }

        private void ApplyExitPenalties(RansomContractRecord contract, Pawn targetPawn, RelationsSettings settings)
        {
            Faction faction = PrisonerRansomLookupUtility.FindFactionByLoadId(contract.FactionId);
            if (faction == null)
            {
                return;
            }

            int totalPenalty = 0;
            bool triggerRaid = false;
            ResolveDropThresholds(contract, settings, out float majorThreshold, out float severeThreshold);
            if (contract.DropRate >= majorThreshold)
            {
                int majorPenalty = ResolveScaledPenalty(contract, settings.RansomPenaltyMajor);
                totalPenalty += majorPenalty;
                RansomContractLifecycleOps.SendLetter("RimChat_PrisonerRansomPenaltyTitle", "RimChat_PrisonerRansomPenaltyMajorBody", faction.Name, targetPawn?.LabelShortCap ?? "Unknown", Mathf.RoundToInt(contract.DropRate * 100f));
            }

            if (contract.DropRate >= severeThreshold)
            {
                int severePenalty = ResolveScaledPenalty(contract, settings.RansomPenaltySevere);
                totalPenalty += severePenalty;
                triggerRaid = true;
                RansomContractLifecycleOps.SendLetter("RimChat_PrisonerRansomPenaltyTitle", "RimChat_PrisonerRansomPenaltySevereBody", faction.Name, targetPawn?.LabelShortCap ?? "Unknown", Mathf.RoundToInt(contract.DropRate * 100f));
            }

            if (totalPenalty <= 0)
            {
                return;
            }

            GameAIInterface.APIResult result = GameAIInterface.Instance.ApplyRansomPenaltyAndRaid(
                faction,
                totalPenalty,
                triggerRaid,
                "drop_penalty",
                targetPawn);
            contract.AppliedGoodwillPenalty += Math.Abs(totalPenalty);
            if (triggerRaid && result.Success)
            {
                RansomContractLifecycleOps.SendLetter("RimChat_PrisonerRansomRaidTitle", "RimChat_PrisonerRansomRaidBody", faction.Name);
            }
        }

        private void ProcessTimeoutContracts(int currentTick)
        {
            RelationsSettings settings = RelationsMod.Instance?.InstanceSettings;
            if (settings == null)
            {
                return;
            }

            if (contracts == null || contracts.Count == 0)
            {
                timeoutScanCursor = 0;
                return;
            }

            int count = contracts.Count;
            int cursor = NormalizeCursor(timeoutScanCursor, count);
            int inspected = 0;
            while (inspected < TimeoutScanBudgetPerPass)
            {
                if (cursor >= count)
                {
                    cursor = 0;
                }

                RansomContractRecord contract = contracts[cursor];
                cursor++;
                inspected++;
                if (contract == null ||
                    contract.Status != RansomContractStatus.PendingRelease ||
                    currentTick <= contract.DeadlineTick)
                {
                    continue;
                }

                ApplyTimeoutPenalty(contract, settings);
            }

            timeoutScanCursor = NormalizeCursor(cursor, count);
        }

        private void ApplyTimeoutPenalty(RansomContractRecord contract, RelationsSettings settings)
        {
            Faction faction = PrisonerRansomLookupUtility.FindFactionByLoadId(contract.FactionId);
            if (faction == null)
            {
                contract.Status = RansomContractStatus.TimeoutPunished;
                return;
            }

            int timeoutPenalty = ResolveScaledPenalty(contract, settings.RansomPenaltyTimeout);
            GameAIInterface.APIResult result = GameAIInterface.Instance.ApplyRansomPenaltyAndRaid(
                faction,
                timeoutPenalty,
                triggerRaid: true,
                reasonTag: "timeout_penalty");
            contract.AppliedGoodwillPenalty += timeoutPenalty;
            contract.Status = RansomContractStatus.TimeoutPunished;
            RansomContractLifecycleOps.SendLetter("RimChat_PrisonerRansomTimeoutTitle", "RimChat_PrisonerRansomTimeoutBody", faction.Name);
            RansomContractLifecycleOps.SendTimeoutWarningMessage(contract, faction);
            RansomContractLifecycleOps.TryEnqueueTimeoutCondemnation(contract, faction);
            if (result.Success)
            {
                RansomContractLifecycleOps.SendLetter("RimChat_PrisonerRansomRaidTitle", "RimChat_PrisonerRansomRaidBody", faction.Name);
            }
        }

        private static void ResolveDropThresholds(
            RansomContractRecord contract,
            RelationsSettings settings,
            out float majorThreshold,
            out float severeThreshold)
        {
            majorThreshold = settings?.RansomValueDropMajorThreshold ?? 0.30f;
            severeThreshold = settings?.RansomValueDropSevereThreshold ?? 0.60f;
            if (contract == null || !contract.IsBatchRansom)
            {
                return;
            }

            majorThreshold = Mathf.Clamp(majorThreshold + BatchRansomDropThresholdBonus, 0.01f, 0.95f);
            severeThreshold = Mathf.Clamp(severeThreshold + BatchRansomDropThresholdBonus, majorThreshold, 0.99f);
        }

        private static int ResolveScaledPenalty(RansomContractRecord contract, int rawPenalty)
        {
            int absolute = Math.Abs(rawPenalty);
            if (absolute <= 0 || contract == null || !contract.IsBatchRansom)
            {
                return absolute;
            }

            return Math.Max(1, Mathf.RoundToInt(absolute * BatchRansomPenaltyScale));
        }

        private RansomContractRecord FindPendingContractByPawn(int targetPawnLoadId)
        {
            return contracts
                .Where(contract => contract != null)
                .FirstOrDefault(contract =>
                    contract.TargetPawnLoadId == targetPawnLoadId &&
                    contract.Status == RansomContractStatus.PendingRelease);
        }

        private void CleanupFinishedContracts(int currentTick)
        {
            if (contracts == null || contracts.Count == 0)
            {
                cleanupScanCursor = 0;
                return;
            }

            int inspected = 0;
            while (contracts.Count > 0 && inspected < CleanupBudgetPerPass)
            {
                if (cleanupScanCursor < 0 || cleanupScanCursor >= contracts.Count)
                {
                    cleanupScanCursor = 0;
                }

                RansomContractRecord contract = contracts[cleanupScanCursor];
                bool shouldRemove = ShouldRemoveFinishedContract(contract, currentTick);
                inspected++;
                if (shouldRemove)
                {
                    contracts.RemoveAt(cleanupScanCursor);
                    continue;
                }

                cleanupScanCursor++;
            }

            if (cleanupScanCursor >= contracts.Count)
            {
                cleanupScanCursor = 0;
            }
        }

        private static bool ShouldRemoveFinishedContract(RansomContractRecord contract, int currentTick)
        {
            if (contract == null)
            {
                return true;
            }

            if (contract.Status == RansomContractStatus.PendingRelease)
            {
                return false;
            }

            bool hasPendingFollowups =
                (contract.HealthyExitReplyScheduled && !contract.HealthyExitReplySent) ||
                (contract.OrganFailureScheduled && !contract.OrganFailurePenaltyApplied);
            if (hasPendingFollowups)
            {
                return false;
            }

            int anchorTick = Math.Max(contract.PaidTick, contract.ReleasedTick);
            return currentTick - anchorTick > 60000;
        }

        private static int NormalizeCursor(int cursor, int count)
        {
            if (count <= 0 || cursor < 0 || cursor >= count)
            {
                return 0;
            }

            return cursor;
        }

        private static bool ShouldRunScheduledTask(int tick, int lastRunTick, int interval, int offset)
        {
            if (tick <= 0 || interval <= 0)
            {
                return false;
            }

            if (!IsOnScheduleSlot(tick, interval, offset))
            {
                return false;
            }

            return lastRunTick <= 0 || tick - lastRunTick >= interval;
        }

        private static bool IsOnScheduleSlot(int tick, int interval, int offset)
        {
            int normalized = (tick - offset) % interval;
            if (normalized < 0)
            {
                normalized += interval;
            }

            return normalized == 0;
        }
    }
}
