using System;
using System.Collections.Generic;
using System.Linq;

namespace Ustas.RimAI.Communication.Relations.Memory
{
    /// <summary>
    /// Pending ransom batch/offer reference state builders for diplomacy sessions.
    /// </summary>
    internal static class FactionDialogueSessionRansomRefs
    {
        internal static void SetPendingRansomBatchSelection(
            FactionDialogueSession session,
            string batchGroupId,
            IEnumerable<int> targetPawnLoadIds,
            int totalCurrentAskSilver,
            int totalMinOfferSilver,
            int totalMaxOfferSilver)
        {
            List<int> normalizedTargetIds = (targetPawnLoadIds ?? Enumerable.Empty<int>())
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            if (normalizedTargetIds.Count <= 0)
            {
                ClearPendingRansomBatchSelection(session);
                return;
            }

            int safeMin = Math.Max(1, totalMinOfferSilver);
            int safeMax = Math.Max(safeMin, totalMaxOfferSilver);
            int safeAsk = Math.Max(1, totalCurrentAskSilver);
            ClearPendingRansomOfferReference(session);

            session.hasPendingRansomBatchSelection = true;
            session.pendingRansomBatchGroupId = string.IsNullOrWhiteSpace(batchGroupId)
                ? Guid.NewGuid().ToString("N")
                : batchGroupId.Trim();
            session.pendingRansomBatchTargetPawnLoadIds = normalizedTargetIds;
            session.pendingRansomBatchTotalCurrentAskSilver = safeAsk;
            session.pendingRansomBatchTotalMinOfferSilver = safeMin;
            session.pendingRansomBatchTotalMaxOfferSilver = safeMax;
        }

        internal static void ClearPendingRansomBatchSelection(FactionDialogueSession session)
        {
            session.hasPendingRansomBatchSelection = false;
            session.pendingRansomBatchGroupId = string.Empty;
            session.pendingRansomBatchTargetPawnLoadIds?.Clear();
            session.pendingRansomBatchTotalCurrentAskSilver = 0;
            session.pendingRansomBatchTotalMinOfferSilver = 0;
            session.pendingRansomBatchTotalMaxOfferSilver = 0;
        }

        internal static bool TryGetPendingRansomBatchSelection(
            FactionDialogueSession session,
            out string batchGroupId,
            out List<int> targetPawnLoadIds,
            out int totalCurrentAskSilver,
            out int totalMinOfferSilver,
            out int totalMaxOfferSilver)
        {
            batchGroupId = session.pendingRansomBatchGroupId ?? string.Empty;
            targetPawnLoadIds = new List<int>();
            totalCurrentAskSilver = Math.Max(0, session.pendingRansomBatchTotalCurrentAskSilver);
            totalMinOfferSilver = Math.Max(0, session.pendingRansomBatchTotalMinOfferSilver);
            totalMaxOfferSilver = Math.Max(0, session.pendingRansomBatchTotalMaxOfferSilver);
            if (!session.hasPendingRansomBatchSelection || session.pendingRansomBatchTargetPawnLoadIds == null)
            {
                return false;
            }

            targetPawnLoadIds = session.pendingRansomBatchTargetPawnLoadIds
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            if (targetPawnLoadIds.Count <= 0)
            {
                return false;
            }

            return true;
        }

        internal static bool TryBuildPendingRansomBatchReference(FactionDialogueSession session, out string referenceBlock)
        {
            referenceBlock = string.Empty;
            if (!TryGetPendingRansomBatchSelection(
                    session,
                    out string batchGroupId,
                    out List<int> targetPawnLoadIds,
                    out int totalCurrentAskSilver,
                    out int totalMinOfferSilver,
                    out int totalMaxOfferSilver))
            {
                return false;
            }

            string ids = string.Join(",", targetPawnLoadIds);
            referenceBlock =
                "[RansomBatchSelection]\n" +
                $"batch_group_id: {batchGroupId}\n" +
                $"target_count: {targetPawnLoadIds.Count}\n" +
                $"target_pawn_load_ids: [{ids}]\n" +
                $"total_current_ask_silver: {totalCurrentAskSilver}\n" +
                $"total_offer_window_min_silver: {totalMinOfferSilver}\n" +
                $"total_offer_window_max_silver: {totalMaxOfferSilver}\n" +
                "requirement: if any pay_prisoner_ransom action is used in this turn, output one action for EVERY listed target_pawn_load_id exactly once in the same response.\n" +
                "requirement: the sum of offer_silver across those actions must be inside [total_offer_window_min_silver, total_offer_window_max_silver].\n" +
                "[/RansomBatchSelection]";
            return true;
        }

        internal static void SetPendingRansomOfferReference(
            FactionDialogueSession session,
            int targetPawnLoadId,
            int currentAskSilver,
            int minOfferSilver,
            int maxOfferSilver)
        {
            if (targetPawnLoadId <= 0)
            {
                ClearPendingRansomOfferReference(session);
                return;
            }

            int safeMin = Math.Max(1, minOfferSilver);
            int safeMax = Math.Max(safeMin, maxOfferSilver);
            int safeAsk = Math.Max(1, currentAskSilver);
            session.hasPendingRansomOfferReference = true;
            session.pendingRansomOfferTargetPawnLoadId = targetPawnLoadId;
            session.pendingRansomOfferCurrentAskSilver = safeAsk;
            session.pendingRansomOfferMinSilver = safeMin;
            session.pendingRansomOfferMaxSilver = safeMax;
        }

        internal static void ClearPendingRansomOfferReference(FactionDialogueSession session)
        {
            session.hasPendingRansomOfferReference = false;
            session.pendingRansomOfferTargetPawnLoadId = 0;
            session.pendingRansomOfferCurrentAskSilver = 0;
            session.pendingRansomOfferMinSilver = 0;
            session.pendingRansomOfferMaxSilver = 0;
        }

        internal static bool TryGetPendingRansomOfferReference(
            FactionDialogueSession session,
            out int targetPawnLoadId,
            out int currentAskSilver,
            out int minOfferSilver,
            out int maxOfferSilver)
        {
            targetPawnLoadId = Math.Max(0, session.pendingRansomOfferTargetPawnLoadId);
            currentAskSilver = Math.Max(0, session.pendingRansomOfferCurrentAskSilver);
            minOfferSilver = Math.Max(0, session.pendingRansomOfferMinSilver);
            maxOfferSilver = Math.Max(0, session.pendingRansomOfferMaxSilver);
            return session.hasPendingRansomOfferReference &&
                targetPawnLoadId > 0 &&
                minOfferSilver > 0 &&
                maxOfferSilver >= minOfferSilver;
        }

        internal static bool TryBuildPendingRansomOfferReference(FactionDialogueSession session, out string referenceBlock)
        {
            referenceBlock = string.Empty;
            if (!TryGetPendingRansomOfferReference(
                    session,
                    out int targetPawnLoadId,
                    out int currentAskSilver,
                    out int minOfferSilver,
                    out int maxOfferSilver))
            {
                return false;
            }

            referenceBlock =
                "[RansomOfferReference]\n" +
                $"target_pawn_load_id: {targetPawnLoadId}\n" +
                $"current_ask_silver: {currentAskSilver}\n" +
                $"offer_window_min_silver: {minOfferSilver}\n" +
                $"offer_window_max_silver: {maxOfferSilver}\n" +
                "requirement: for pay_prisoner_ransom in single-target flow, keep offer_silver inside [offer_window_min_silver, offer_window_max_silver]; if out of range, execution will clamp to the nearest boundary.\n" +
                "[/RansomOfferReference]";
            return true;
        }
    }
}
