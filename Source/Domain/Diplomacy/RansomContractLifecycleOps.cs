using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.NpcDialogue;
using Ustas.RimAI.Communication.Relations.Module;
using Verse;

namespace Ustas.RimAI.Communication.Relations.DiplomacySystem
{
    /// <summary>
    /// Follow-up scanning, organ-failure / healthy-exit replies, and ransom notice dispatch.
    /// </summary>
    internal static class RansomContractLifecycleOps
    {
        private const int OrganPenaltyBudgetPerPass = 12;
        private const int HealthyReplyBudgetPerPass = 12;
        private const int HealthyExitReplyMinDelayTicks = 12500;
        private const int HealthyExitReplyMaxDelayTicks = 25000;
        private const int OrganFailureMinDelayTicks = 12500;
        private const int OrganFailureMaxDelayTicks = 25000;
        private const float StrictHealthyExitSummaryThreshold = 0.85f;
        private const float StrictHealthyExitConsciousnessThreshold = 0.85f;

        internal static void ProcessHealthyExitReplies(RansomContractManager owner, int currentTick)
        {
            List<RansomContractRecord> contracts = owner.Contracts;
            if (contracts == null || contracts.Count == 0)
            {
                owner.HealthyReplyScanCursor = 0;
                return;
            }

            int count = contracts.Count;
            int cursor = NormalizeCursor(owner.HealthyReplyScanCursor, count);
            int inspected = 0;
            while (inspected < HealthyReplyBudgetPerPass)
            {
                if (cursor >= count)
                {
                    cursor = 0;
                }

                RansomContractRecord contract = contracts[cursor];
                cursor++;
                inspected++;
                if (contract == null ||
                    contract.Status != RansomContractStatus.Completed ||
                    !contract.HealthyExitReplyScheduled ||
                    contract.HealthyExitReplySent ||
                    contract.HealthyExitReplyDueTick <= 0 ||
                    currentTick < contract.HealthyExitReplyDueTick)
                {
                    continue;
                }

                bool delivered = TryDeliverHealthyExitReply(contract);
                contract.HealthyExitReplySent = delivered;
                contract.HealthyExitReplyScheduled = false;
                contract.HealthyExitReplyDueTick = 0;
            }

            owner.HealthyReplyScanCursor = NormalizeCursor(cursor, count);
        }

        internal static void ProcessOrganFailurePenalties(RansomContractManager owner, int currentTick)
        {
            RelationsSettings settings = RelationsMod.Instance?.InstanceSettings;
            if (settings == null)
            {
                return;
            }

            List<RansomContractRecord> contracts = owner.Contracts;
            if (contracts == null || contracts.Count == 0)
            {
                owner.OrganPenaltyScanCursor = 0;
                return;
            }

            int count = contracts.Count;
            int cursor = NormalizeCursor(owner.OrganPenaltyScanCursor, count);
            int inspected = 0;
            while (inspected < OrganPenaltyBudgetPerPass)
            {
                if (cursor >= count)
                {
                    cursor = 0;
                }

                RansomContractRecord contract = contracts[cursor];
                cursor++;
                inspected++;
                if (contract == null ||
                    contract.Status != RansomContractStatus.Completed ||
                    !contract.OrganFailureScheduled ||
                    contract.OrganFailurePenaltyApplied ||
                    contract.OrganFailureDueTick <= 0 ||
                    currentTick < contract.OrganFailureDueTick)
                {
                    continue;
                }

                ApplyOrganFailurePenalty(contract, settings);
            }

            owner.OrganPenaltyScanCursor = NormalizeCursor(cursor, count);
        }

        internal static bool TryScheduleOrganFailurePenalty(RansomContractRecord contract, Pawn pawn, int exitTick)
        {
            if (contract == null || pawn == null)
            {
                return false;
            }

            contract.ExitCoreOrganMissingSnapshot = PrisonerRansomService.CaptureCoreOrganMissingSnapshot(pawn);
            contract.BaselineCoreOrganMissingSnapshot ??= new List<RansomCoreOrganSnapshotEntry>();
            contract.NewlyMissingCoreOrgans = PrisonerRansomService.ComputeNewlyMissingCoreOrgans(
                contract.BaselineCoreOrganMissingSnapshot,
                contract.ExitCoreOrganMissingSnapshot);

            if (contract.NewlyMissingCoreOrgans == null || contract.NewlyMissingCoreOrgans.Count <= 0)
            {
                contract.OrganFailureScheduled = false;
                contract.OrganFailureDueTick = 0;
                return false;
            }

            int delayTicks = Rand.RangeInclusive(OrganFailureMinDelayTicks, OrganFailureMaxDelayTicks);
            contract.OrganFailureDueTick = Math.Max(exitTick, 0) + delayTicks;
            contract.OrganFailureScheduled = true;
            contract.OrganFailurePenaltyApplied = false;
            return true;
        }

        internal static void ApplyOrganFailurePenalty(RansomContractRecord contract, RelationsSettings settings)
        {
            if (contract == null)
            {
                return;
            }

            contract.OrganFailureScheduled = false;
            contract.OrganFailureDueTick = 0;
            contract.OrganFailurePenaltyApplied = true;

            Faction faction = PrisonerRansomLookupUtility.FindFactionByLoadId(contract.FactionId);
            if (faction == null)
            {
                return;
            }

            string pawnLabel = ResolvePawnLabel(contract, null);
            string organSummary = ResolveOrganFailureSummary(contract);
            int timeoutPenalty = Math.Abs(settings.RansomPenaltyTimeout);
            GameAIInterface.APIResult result = GameAIInterface.Instance.ApplyRansomPenaltyAndRaid(
                faction,
                timeoutPenalty,
                triggerRaid: true,
                reasonTag: "organ_failure_timeout_penalty");
            contract.AppliedGoodwillPenalty += timeoutPenalty;

            SendLetter(
                "RimChat_PrisonerRansomTimeoutTitle",
                "RimChat_PrisonerRansomOrganFailureBody",
                faction.Name,
                pawnLabel,
                organSummary);
            SendOrganFailureWarningMessage(faction, pawnLabel, organSummary);
            TryEnqueueOrganFailureCondemnation(faction, pawnLabel, organSummary);
            if (result.Success)
            {
                SendLetter("RimChat_PrisonerRansomRaidTitle", "RimChat_PrisonerRansomRaidBody", faction.Name);
            }
        }

        internal static void TryScheduleHealthyExitReply(RansomContractRecord contract, Pawn pawn, int exitTick)
        {
            if (contract == null || pawn == null || contract.HealthyExitReplySent || contract.HealthyExitReplyScheduled)
            {
                return;
            }

            if (!IsStrictHealthyExit(pawn))
            {
                return;
            }

            int delayTicks = Rand.RangeInclusive(HealthyExitReplyMinDelayTicks, HealthyExitReplyMaxDelayTicks);
            contract.HealthyExitReplyDueTick = Math.Max(exitTick, 0) + delayTicks;
            contract.HealthyExitReplyScheduled = true;
            contract.TargetPawnLabelSnapshot = pawn.LabelShortCap ?? contract.TargetPawnLabelSnapshot ?? string.Empty;
        }

        internal static bool IsStrictHealthyExit(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.Destroyed || pawn.Downed || pawn.health == null)
            {
                return false;
            }

            float summaryHealth = Mathf.Clamp01(pawn.health.summaryHealth?.SummaryHealthPercent ?? 0f);
            float consciousness = ReadCapacitySafe(pawn, PawnCapacityDefOf.Consciousness);
            return summaryHealth >= StrictHealthyExitSummaryThreshold &&
                consciousness >= StrictHealthyExitConsciousnessThreshold;
        }

        internal static float ReadCapacitySafe(Pawn pawn, PawnCapacityDef capacityDef)
        {
            if (pawn?.health?.capacities == null || capacityDef == null)
            {
                return 0f;
            }

            return Mathf.Clamp01(pawn.health.capacities.GetLevel(capacityDef));
        }

        internal static bool TryDeliverHealthyExitReply(RansomContractRecord contract)
        {
            if (contract == null)
            {
                return false;
            }

            Faction faction = PrisonerRansomLookupUtility.FindFactionByLoadId(contract.FactionId);
            if (faction == null)
            {
                return false;
            }

            string pawnLabel = ResolvePawnLabel(contract, null);
            string message = "RimChat_PrisonerRansomHealthyExitReplyMessage".Translate(pawnLabel).ToString();
            PushNpcMessageToFactionSession(faction, message, DialogueMessageType.Normal);
            SendNpcChoiceLetter(
                faction,
                "RimChat_PrisonerRansomHealthyExitLetterTitle".Translate(faction.Name),
                message,
                LetterDefOf.PositiveEvent);
            return true;
        }

        internal static void SendTimeoutWarningMessage(RansomContractRecord contract, Faction faction)
        {
            if (contract == null || faction == null)
            {
                return;
            }

            string pawnLabel = ResolvePawnLabel(contract, null);
            string message = "RimChat_PrisonerRansomTimeoutWarningMessage".Translate(pawnLabel).ToString();
            PushNpcMessageToFactionSession(faction, message, DialogueMessageType.System);
            SendNpcChoiceLetter(
                faction,
                "RimChat_PrisonerRansomTimeoutWarningLetterTitle".Translate(faction.Name),
                message,
                LetterDefOf.ThreatSmall);
        }

        internal static void TryEnqueueTimeoutCondemnation(RansomContractRecord contract, Faction faction)
        {
            if (contract == null || faction == null)
            {
                return;
            }

            GameComponent_DiplomacyManager manager = GameComponent_DiplomacyManager.Instance;
            if (manager == null)
            {
                return;
            }

            string pawnLabel = ResolvePawnLabel(contract, null);
            string summary = "RimChat_PrisonerRansomTimeoutCondemnSummary"
                .Translate(faction.Name, pawnLabel)
                .ToString();
            manager.EnqueuePublicPost(
                sourceFaction: faction,
                targetFaction: Faction.OfPlayer,
                category: SocialPostCategory.Diplomatic,
                sentiment: -1,
                summary: summary,
                isFromPlayerDialogue: false,
                intentHint: string.Empty,
                reason: DebugGenerateReason.DialogueExplicit);
        }

        internal static void SendOrganFailureWarningMessage(Faction faction, string pawnLabel, string organSummary)
        {
            if (faction == null)
            {
                return;
            }

            string message = "RimChat_PrisonerRansomOrganFailureWarningMessage"
                .Translate(pawnLabel, organSummary)
                .ToString();
            PushNpcMessageToFactionSession(faction, message, DialogueMessageType.System);
            SendNpcChoiceLetter(
                faction,
                "RimChat_PrisonerRansomOrganFailureWarningLetterTitle".Translate(faction.Name),
                message,
                LetterDefOf.ThreatSmall);
        }

        internal static void TryEnqueueOrganFailureCondemnation(Faction faction, string pawnLabel, string organSummary)
        {
            if (faction == null)
            {
                return;
            }

            GameComponent_DiplomacyManager manager = GameComponent_DiplomacyManager.Instance;
            if (manager == null)
            {
                return;
            }

            string summary = "RimChat_PrisonerRansomOrganFailureCondemnSummary"
                .Translate(faction.Name, pawnLabel, organSummary)
                .ToString();
            manager.EnqueuePublicPost(
                sourceFaction: faction,
                targetFaction: Faction.OfPlayer,
                category: SocialPostCategory.Diplomatic,
                sentiment: -1,
                summary: summary,
                isFromPlayerDialogue: false,
                intentHint: string.Empty,
                reason: DebugGenerateReason.DialogueExplicit);
        }

        internal static string ResolveOrganFailureSummary(RansomContractRecord contract)
        {
            string summary = PrisonerRansomService.FormatCoreOrganMissingSummary(contract?.NewlyMissingCoreOrgans);
            if (!string.IsNullOrWhiteSpace(summary))
            {
                return summary;
            }

            return "RimChat_Unknown".Translate().ToString();
        }

        internal static string ResolvePawnLabel(RansomContractRecord contract, Pawn fallbackPawn)
        {
            if (fallbackPawn != null && !string.IsNullOrWhiteSpace(fallbackPawn.LabelShortCap))
            {
                if (contract != null)
                {
                    contract.TargetPawnLabelSnapshot = fallbackPawn.LabelShortCap;
                }
                return fallbackPawn.LabelShortCap;
            }

            if (!string.IsNullOrWhiteSpace(contract?.TargetPawnLabelSnapshot))
            {
                return contract.TargetPawnLabelSnapshot;
            }

            if (contract != null &&
                contract.TargetPawnLoadId > 0 &&
                PrisonerRansomService.TryResolvePawnByLoadId(contract.TargetPawnLoadId, out Pawn pawn) &&
                pawn != null &&
                !string.IsNullOrWhiteSpace(pawn.LabelShortCap))
            {
                contract.TargetPawnLabelSnapshot = pawn.LabelShortCap;
                return pawn.LabelShortCap;
            }

            return "Unknown";
        }

        internal static void PushNpcMessageToFactionSession(Faction faction, string message, DialogueMessageType messageType)
        {
            if (faction == null || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            GameComponent_DiplomacyManager manager = GameComponent_DiplomacyManager.Instance;
            if (manager == null)
            {
                return;
            }

            string sender = faction.leader?.Name?.ToStringShort ?? faction.Name ?? "Unknown";
            manager.HandleInboundFactionMessage(
                faction,
                sender,
                message,
                messageType,
                faction.leader,
                markUnread: true,
                forcePresenceOnline: true);
        }

        internal static void SendNpcChoiceLetter(Faction faction, TaggedString title, string body, LetterDef letterDef)
        {
            if (faction == null || Find.LetterStack == null || string.IsNullOrWhiteSpace(body))
            {
                return;
            }

            if (ChoiceLetter_NpcInitiatedDialogue.IsDialogueAlreadyOpen(faction))
            {
                return;
            }

            var letter = new ChoiceLetter_NpcInitiatedDialogue();
            letter.AssignLoadID();
            letter.Setup(faction, title, body, letterDef ?? LetterDefOf.NeutralEvent);
            Find.LetterStack.ReceiveLetter(letter, string.Empty, 0, true);
        }

        internal static void SendLetter(string titleKey, string bodyKey, params object[] args)
        {
            Find.LetterStack.ReceiveLetter(
                titleKey.Translate(),
                bodyKey.Translate(args),
                LetterDefOf.NegativeEvent);
        }

        private static int NormalizeCursor(int cursor, int count)
        {
            if (count <= 0 || cursor < 0 || cursor >= count)
            {
                return 0;
            }

            return cursor;
        }
    }
}
