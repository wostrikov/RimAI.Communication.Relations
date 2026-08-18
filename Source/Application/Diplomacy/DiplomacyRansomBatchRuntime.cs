using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Context;
using Ustas.RimAI.Communication.Relations.Dialogue;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Communication.Relations.Guards;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.PawnRpgPush;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.UI;

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class DiplomacyRansomBatchRuntime : DiplomacyDialogueCollaborator
{
    internal DiplomacyRansomBatchRuntime(Dialog_DiplomacyDialogue owner) : base(owner) { }



internal static string ResolveRansomPendingMessage(FactionDialogueSession currentSession)
{
    if (!TryGetPendingRansomBatchSelection(currentSession, out PendingRansomBatchSelection pendingBatch))
    {
        return "RimChat_RansomNeedPrisonerSelectionSystem".Translate().ToString();
    }

    return "RimChat_RansomBatchNeedOfferSystem".Translate(
        pendingBatch.TargetPawnLoadIds.Count,
        pendingBatch.TotalMinOfferSilver,
        pendingBatch.TotalMaxOfferSilver,
        pendingBatch.TotalCurrentAskSilver).ToString();
}



internal static bool HasPendingRansomBatchSelection(FactionDialogueSession currentSession)
{
    return TryGetPendingRansomBatchSelection(currentSession, out _);
}



internal static void ClearPendingRansomBatchSelection(FactionDialogueSession currentSession)
{
    currentSession?.ClearPendingRansomBatchSelection();
}



internal static void ClearPendingRansomOfferReference(FactionDialogueSession currentSession)
{
    currentSession?.ClearPendingRansomOfferReference();
}



internal static bool TryGetPendingRansomBatchSelection(
    FactionDialogueSession currentSession,
    out PendingRansomBatchSelection pendingBatch)
{
    pendingBatch = null;
    if (currentSession == null ||
        !currentSession.TryGetPendingRansomBatchSelection(
            out string batchGroupId,
            out List<int> targetPawnLoadIds,
            out int totalCurrentAskSilver,
            out int totalMinOfferSilver,
            out int totalMaxOfferSilver))
    {
        return false;
    }

    pendingBatch = new PendingRansomBatchSelection(
        batchGroupId,
        targetPawnLoadIds,
        totalCurrentAskSilver,
        totalMinOfferSilver,
        totalMaxOfferSilver);
    return pendingBatch.TargetPawnLoadIds.Count > 0;
}



internal BatchRansomExecutionPlan BuildBatchRansomExecutionPlan(
    List<AIAction> actions,
    FactionDialogueSession currentSession,
    Faction currentFaction)
{
    if (!TryGetPendingRansomBatchSelection(currentSession, out PendingRansomBatchSelection pendingBatch))
    {
        return BatchRansomExecutionPlan.Inactive();
    }

    List<AIAction> ransomActions = actions?
        .Where(IsPayPrisonerRansomAction)
        .ToList() ?? new List<AIAction>();
    if (ransomActions.Count <= 0)
    {
        return BatchRansomExecutionPlan.Inactive();
    }

    if (!TryRefreshPendingRansomBatchOfferWindow(currentSession, currentFaction, out pendingBatch, out string refreshError))
    {
        ClearPendingRansomBatchSelection(currentSession);
        MarkRansomInfoRequestIncomplete(currentSession);
        return BatchRansomExecutionPlan.Invalid(ransomActions, refreshError);
    }

    var expectedTargetIds = new HashSet<int>(pendingBatch.TargetPawnLoadIds);
    var actionTargetIds = new Dictionary<AIAction, int>();
    var actionOfferSilver = new Dictionary<AIAction, int>();
    var actualTargetIds = new HashSet<int>();
    int totalOfferSilver = 0;
    foreach (AIAction action in ransomActions)
    {
        if (action?.Parameters == null ||
            !TryReadPositiveInt(action.Parameters, "target_pawn_load_id", out int targetPawnLoadId))
        {
            Log.Warning($"[RimAI.Relations] pay_prisoner_ransom batch validation failed: missing target_pawn_load_id. expected_targets={FormatRansomBatchTargetIds(expectedTargetIds)}");
            return BatchRansomExecutionPlan.Invalid(
                ransomActions,
                "RimChat_RansomBatchActionMissingTargetSystem".Translate().ToString());
        }

        if (!TryReadPositiveInt(action.Parameters, "offer_silver", out int offerSilver))
        {
            Log.Warning($"[RimAI.Relations] pay_prisoner_ransom batch validation failed: missing offer_silver for target={targetPawnLoadId}. expected_targets={FormatRansomBatchTargetIds(expectedTargetIds)}");
            return BatchRansomExecutionPlan.Invalid(
                ransomActions,
                "RimChat_RansomBatchActionMissingOfferSystem".Translate(targetPawnLoadId).ToString());
        }

        if (!expectedTargetIds.Contains(targetPawnLoadId))
        {
            Log.Warning($"[RimAI.Relations] pay_prisoner_ransom batch validation failed: unexpected target={targetPawnLoadId}. expected_targets={FormatRansomBatchTargetIds(expectedTargetIds)}");
            return BatchRansomExecutionPlan.Invalid(
                ransomActions,
                "RimChat_RansomBatchUnexpectedTargetSystem".Translate(targetPawnLoadId).ToString());
        }

        if (!actualTargetIds.Add(targetPawnLoadId))
        {
            Log.Warning($"[RimAI.Relations] pay_prisoner_ransom batch validation failed: duplicate target={targetPawnLoadId}. expected_targets={FormatRansomBatchTargetIds(expectedTargetIds)}, actual_targets={FormatRansomBatchTargetIds(actualTargetIds)}");
            return BatchRansomExecutionPlan.Invalid(
                ransomActions,
                "RimChat_RansomBatchDuplicateTargetSystem".Translate(targetPawnLoadId).ToString());
        }

        actionTargetIds[action] = targetPawnLoadId;
        actionOfferSilver[action] = offerSilver;
        totalOfferSilver += offerSilver;
    }

    if (!expectedTargetIds.SetEquals(actualTargetIds))
    {
        var missingTargetIds = expectedTargetIds.Except(actualTargetIds);
        var extraTargetIds = actualTargetIds.Except(expectedTargetIds);
        Log.Warning(
            $"[RimAI.Relations] pay_prisoner_ransom batch validation failed: coverage mismatch. " +
            $"expected_targets={FormatRansomBatchTargetIds(expectedTargetIds)}, " +
            $"actual_targets={FormatRansomBatchTargetIds(actualTargetIds)}, " +
            $"missing_targets={FormatRansomBatchTargetIds(missingTargetIds)}, " +
            $"extra_targets={FormatRansomBatchTargetIds(extraTargetIds)}.");
        return BatchRansomExecutionPlan.Invalid(
            ransomActions,
            "RimChat_RansomBatchCoverageMismatchSystem".Translate(
                expectedTargetIds.Count,
                actualTargetIds.Count,
                FormatRansomBatchTargetIds(missingTargetIds),
                FormatRansomBatchTargetIds(extraTargetIds)).ToString());
    }

    if (!TryNormalizeBatchOfferTotals(
            ransomActions,
            actionOfferSilver,
            pendingBatch,
            totalOfferSilver,
            out int normalizedTotalOfferSilver,
            out string normalizeFailureMessage))
    {
        Log.Warning(
            "[RimAI.Relations] pay_prisoner_ransom batch normalization failed. " +
            $"total_offer={totalOfferSilver}, window={pendingBatch.TotalMinOfferSilver}-{pendingBatch.TotalMaxOfferSilver}, " +
            $"expected_targets={FormatRansomBatchTargetIds(expectedTargetIds)}, actual_targets={FormatRansomBatchTargetIds(actualTargetIds)}");
        return BatchRansomExecutionPlan.Invalid(
            ransomActions,
            normalizeFailureMessage);
    }

    foreach (AIAction action in ransomActions)
    {
        action.Parameters ??= new Dictionary<string, object>(StringComparer.Ordinal);
        action.Parameters[DiplomacyRansomSelectionWorkflow.BatchGroupIdParameterKey] = pendingBatch.BatchGroupId;
        action.Parameters[DiplomacyRansomSelectionWorkflow.BatchTargetCountParameterKey] = expectedTargetIds.Count;
        action.Parameters[DiplomacyRansomSelectionWorkflow.BatchTotalOfferSilverParameterKey] = normalizedTotalOfferSilver;
    }

    return BatchRansomExecutionPlan.Valid(ransomActions, actionTargetIds, pendingBatch);
}



internal static bool TryNormalizeBatchOfferTotals(
    List<AIAction> ransomActions,
    Dictionary<AIAction, int> actionOfferSilver,
    PendingRansomBatchSelection pendingBatch,
    int totalOfferSilver,
    out int normalizedTotalOfferSilver,
    out string failureMessage)
{
    normalizedTotalOfferSilver = Math.Max(0, totalOfferSilver);
    failureMessage = string.Empty;
    if (ransomActions == null || actionOfferSilver == null || pendingBatch == null)
    {
        failureMessage = "RimChat_RansomSystemUnavailableSystem".Translate().ToString();
        return false;
    }

    int targetTotalOfferSilver = Mathf.Clamp(
        totalOfferSilver,
        pendingBatch.TotalMinOfferSilver,
        pendingBatch.TotalMaxOfferSilver);
    normalizedTotalOfferSilver = targetTotalOfferSilver;
    if (targetTotalOfferSilver == totalOfferSilver)
    {
        return true;
    }

    if (!TryBuildNormalizedBatchOfferMap(ransomActions, actionOfferSilver, targetTotalOfferSilver, out Dictionary<AIAction, int> normalizedOffers))
    {
        failureMessage = "RimChat_RansomBatchTotalOutOfWindowSystem".Translate(
            totalOfferSilver,
            pendingBatch.TotalMinOfferSilver,
            pendingBatch.TotalMaxOfferSilver,
            pendingBatch.TotalCurrentAskSilver).ToString();
        return false;
    }

    foreach (AIAction action in ransomActions)
    {
        action.Parameters ??= new Dictionary<string, object>(StringComparer.Ordinal);
        action.Parameters["offer_silver"] = normalizedOffers[action];
        actionOfferSilver[action] = normalizedOffers[action];
    }

    Log.Message(
        "[RimAI.Relations] pay_prisoner_ransom batch total normalized. " +
        $"original_total={totalOfferSilver}, normalized_total={targetTotalOfferSilver}, " +
        $"window={pendingBatch.TotalMinOfferSilver}-{pendingBatch.TotalMaxOfferSilver}, " +
        $"targets={ransomActions.Count}");
    return true;
}



internal static bool TryBuildNormalizedBatchOfferMap(
    List<AIAction> ransomActions,
    Dictionary<AIAction, int> actionOfferSilver,
    int targetTotalOfferSilver,
    out Dictionary<AIAction, int> normalizedOffers)
{
    normalizedOffers = new Dictionary<AIAction, int>();
    if (ransomActions == null || actionOfferSilver == null || ransomActions.Count <= 0 || targetTotalOfferSilver <= 0)
    {
        return false;
    }

    int targetCount = ransomActions.Count;
    if (targetTotalOfferSilver < targetCount)
    {
        return false;
    }

    int weightSum = ransomActions.Sum(action => actionOfferSilver.TryGetValue(action, out int offer) ? Math.Max(1, offer) : 1);
    if (weightSum <= 0)
    {
        return false;
    }

    int remainingPool = targetTotalOfferSilver - targetCount;
    int allocated = targetCount;
    var candidates = new List<BatchOfferScaleCandidate>(targetCount);
    for (int i = 0; i < targetCount; i++)
    {
        AIAction action = ransomActions[i];
        int weight = actionOfferSilver.TryGetValue(action, out int offer) ? Math.Max(1, offer) : 1;
        double rawExtra = remainingPool * (double)weight / weightSum;
        int floorExtra = Math.Max(0, (int)Math.Floor(rawExtra));
        int normalized = 1 + floorExtra;
        allocated += floorExtra;
        candidates.Add(new BatchOfferScaleCandidate(action, i, weight, normalized, rawExtra - floorExtra));
    }

    int residual = targetTotalOfferSilver - allocated;
    if (residual < 0)
    {
        return false;
    }

    foreach (BatchOfferScaleCandidate candidate in candidates
        .OrderByDescending(item => item.FractionRemainder)
        .ThenByDescending(item => item.Weight)
        .ThenBy(item => item.Index)
        .Take(residual))
    {
        candidate.NormalizedOffer += 1;
    }

    int finalTotal = 0;
    foreach (BatchOfferScaleCandidate candidate in candidates)
    {
        int safeOffer = Math.Max(1, candidate.NormalizedOffer);
        normalizedOffers[candidate.Action] = safeOffer;
        finalTotal += safeOffer;
    }

    return finalTotal == targetTotalOfferSilver;
}



internal void HandleBatchRansomPaymentSuccess(
    BatchRansomExecutionPlan plan,
    AIAction action,
    ActionResult result,
    FactionDialogueSession currentSession,
    Faction currentFaction)
{
    if (currentSession == null || !DiplomacySessionOutcomeMessages.ShouldResetRansomSelectionStateAfterSuccess(result))
    {
        return;
    }

    if (!plan.TryGetTargetPawnLoadId(action, out int targetPawnLoadId))
    {
        return;
    }

    if (!currentSession.ConsumePendingRansomBatchTarget(targetPawnLoadId))
    {
        return;
    }

    if (!HasPendingRansomBatchSelection(currentSession))
    {
        Log.Message("[RimAI.Relations] pay_prisoner_ransom batch completed. Cleared request_info(prisoner) state.");
        ResetRansomSelectionStateAfterPayment(currentSession);
        return;
    }

    if (!TryRefreshPendingRansomBatchOfferWindow(currentSession, currentFaction, out PendingRansomBatchSelection pendingBatch, out string refreshError))
    {
        currentSession.AddMessage("System", refreshError, false, DialogueMessageType.System);
        return;
    }

    currentSession.AddMessage(
        "System",
        "RimChat_RansomBatchRemainingSystem".Translate(
            pendingBatch.TargetPawnLoadIds.Count,
            pendingBatch.TotalMinOfferSilver,
            pendingBatch.TotalMaxOfferSilver,
            pendingBatch.TotalCurrentAskSilver).ToString(),
        false,
        DialogueMessageType.System);
}



internal bool TryRefreshPendingRansomBatchOfferWindow(
    FactionDialogueSession currentSession,
    Faction currentFaction,
    out PendingRansomBatchSelection refreshedBatch,
    out string failureMessage)
{
    refreshedBatch = null;
    failureMessage = "RimChat_RansomQuoteUnavailableSystem".Translate().ToString();
    if (!TryGetPendingRansomBatchSelection(currentSession, out PendingRansomBatchSelection pendingBatch))
    {
        return false;
    }

    if (currentFaction == null)
    {
        failureMessage = "RimChat_RansomSystemUnavailableSystem".Translate().ToString();
        return false;
    }

    int totalCurrentAskSilver = 0;
    int totalMinOfferSilver = 0;
    int totalMaxOfferSilver = 0;
    foreach (int targetPawnLoadId in pendingBatch.TargetPawnLoadIds)
    {
        if (!PrisonerRansomService.TryResolvePawnByLoadId(targetPawnLoadId, out Pawn targetPawn) ||
            !PrisonerRansomService.IsRansomEligibleTarget(targetPawn, currentFaction, out _))
        {
            failureMessage = "RimChat_RansomBatchTargetUnavailableSystem".Translate(targetPawnLoadId).ToString();
            return false;
        }

        GameAIInterface.APIResult quoteResult = GameAIInterface.Instance.CalculatePrisonerRansomQuote(
            currentFaction,
            targetPawn,
            forceRefresh: true);
        if (!quoteResult.Success || !(quoteResult.Data is PrisonerRansomResultData quoteData) || quoteData.CurrentAskSilver <= 0)
        {
            failureMessage = "RimChat_RansomReferenceAskUnavailableSystem".Translate(targetPawn.LabelShortCap).ToString();
            return false;
        }

        if (!DiplomacyRansomProofWorkflow.TryGetRansomOfferWindow(quoteData, out int minOfferSilver, out int maxOfferSilver))
        {
            failureMessage = "RimChat_RansomOfferOutOfWindowSimpleSystem".Translate(quoteData.CurrentAskSilver).ToString();
            return false;
        }

        totalCurrentAskSilver += DiplomacyRansomProofWorkflow.ResolveBatchEstimatedAskSilver(quoteData.CurrentAskSilver);
        totalMinOfferSilver += minOfferSilver;
        totalMaxOfferSilver += maxOfferSilver;
    }

    currentSession.SetPendingRansomBatchSelection(
        pendingBatch.BatchGroupId,
        pendingBatch.TargetPawnLoadIds,
        totalCurrentAskSilver,
        totalMinOfferSilver,
        totalMaxOfferSilver);
    refreshedBatch = new PendingRansomBatchSelection(
        pendingBatch.BatchGroupId,
        pendingBatch.TargetPawnLoadIds,
        totalCurrentAskSilver,
        totalMinOfferSilver,
        totalMaxOfferSilver);
    return true;
}



        internal static string FormatRansomBatchTargetIds(IEnumerable<int> targetPawnLoadIds) => DiplomacyRansomBatchTargetOps.FormatRansomBatchTargetIds(targetPawnLoadIds);

        internal static List<Pawn> CollectEligibleRansomTargets(Faction sourceFaction) => DiplomacyRansomBatchTargetOps.CollectEligibleRansomTargets(sourceFaction);

        internal static bool TryUseBoundRansomTarget(FactionDialogueSession currentSession,
    Faction currentFaction,
    out int targetPawnLoadId,
    out Pawn targetPawn) => DiplomacyRansomBatchTargetOps.TryUseBoundRansomTarget(currentSession, currentFaction, out targetPawnLoadId, out targetPawn);

        internal static void BindRansomTarget(FactionDialogueSession currentSession, Faction currentFaction, int pawnLoadId) => DiplomacyRansomBatchTargetOps.BindRansomTarget(currentSession, currentFaction, pawnLoadId);

        internal static void ClearRansomTargetBinding(FactionDialogueSession currentSession) => DiplomacyRansomBatchTargetOps.ClearRansomTargetBinding(currentSession);

        internal static void MarkRansomInfoRequestCompleted(FactionDialogueSession currentSession, Faction currentFaction, int selectedPawnLoadId) => DiplomacyRansomBatchTargetOps.MarkRansomInfoRequestCompleted(currentSession, currentFaction, selectedPawnLoadId);

        internal static void MarkRansomInfoRequestIncomplete(FactionDialogueSession currentSession) => DiplomacyRansomBatchTargetOps.MarkRansomInfoRequestIncomplete(currentSession);

        internal static bool HasCompletedRansomInfoRequestForFaction(FactionDialogueSession currentSession, Faction currentFaction) => DiplomacyRansomBatchTargetOps.HasCompletedRansomInfoRequestForFaction(currentSession, currentFaction);

        internal static bool IsRequestInfoPrisonerAction(AIAction action) => DiplomacyRansomBatchTargetOps.IsRequestInfoPrisonerAction(action);

        internal static void ResetRansomSelectionStateAfterPayment(FactionDialogueSession currentSession) => DiplomacyRansomBatchTargetOps.ResetRansomSelectionStateAfterPayment(currentSession);

        internal static bool IsPayPrisonerRansomAction(AIAction action) => DiplomacyRansomBatchTargetOps.IsPayPrisonerRansomAction(action);

        internal static bool TryReadPositiveInt(Dictionary<string, object> values, string key, out int parsed) => DiplomacyRansomBatchTargetOps.TryReadPositiveInt(values, key, out parsed);

        internal static bool IsRansomAutoReplyCoolingDown(FactionDialogueSession currentSession, out float remainingSeconds) => DiplomacyRansomBatchTargetOps.IsRansomAutoReplyCoolingDown(currentSession, out remainingSeconds);

        internal static bool TryClassifyRansomAutoReplyTimeout(string detail, out string timeoutClass) => DiplomacyRansomBatchTargetOps.TryClassifyRansomAutoReplyTimeout(detail, out timeoutClass);

        internal static bool IsQueueTimeoutText(string text) => DiplomacyRansomBatchTargetOps.IsQueueTimeoutText(text);

        internal static bool IsNetworkTimeoutText(string text) => DiplomacyRansomBatchTargetOps.IsNetworkTimeoutText(text);

        internal static bool IsDropTimeoutText(string text) => DiplomacyRansomBatchTargetOps.IsDropTimeoutText(text);

        internal static bool ContainsTimeoutToken(string source, string token) => DiplomacyRansomBatchTargetOps.ContainsTimeoutToken(source, token);

        internal static void ArmRansomAutoReplyTimeoutCooldown(FactionDialogueSession currentSession, string timeoutClass, string detail) => DiplomacyRansomBatchTargetOps.ArmRansomAutoReplyTimeoutCooldown(currentSession, timeoutClass, detail);


}
