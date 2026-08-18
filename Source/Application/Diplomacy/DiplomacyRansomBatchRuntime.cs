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



internal static string FormatRansomBatchTargetIds(IEnumerable<int> targetPawnLoadIds)
{
    if (targetPawnLoadIds == null)
    {
        return "none";
    }

    List<int> normalized = targetPawnLoadIds
        .Where(id => id > 0)
        .Distinct()
        .ToList();
    return normalized.Count <= 0
        ? "none"
        : string.Join(",", normalized);
}



internal static List<Pawn> CollectEligibleRansomTargets(Faction sourceFaction)
{
    if (sourceFaction == null)
    {
        return new List<Pawn>();
    }

    var result = new List<Pawn>();
    var seenIds = new HashSet<int>();
    IEnumerable<Pawn> candidates = (Find.Maps ?? new List<Map>())
        .SelectMany(map => map?.mapPawns?.AllPawnsSpawned ?? Enumerable.Empty<Pawn>());
    foreach (Pawn pawn in candidates)
    {
        if (pawn == null || pawn.thingIDNumber <= 0 || !seenIds.Add(pawn.thingIDNumber))
        {
            continue;
        }

        if (!PrisonerRansomService.IsRansomEligibleTarget(pawn, sourceFaction, out _))
        {
            continue;
        }

        result.Add(pawn);
    }

    return result
        .OrderByDescending(p => p.health?.summaryHealth?.SummaryHealthPercent ?? 0f)
        .ThenBy(p => p.LabelShortCap)
        .ToList();
}



internal static bool TryUseBoundRansomTarget(
    FactionDialogueSession currentSession,
    Faction currentFaction,
    out int targetPawnLoadId,
    out Pawn targetPawn)
{
    targetPawnLoadId = 0;
    targetPawn = null;
    if (currentSession == null || currentFaction == null)
    {
        return false;
    }

    string factionId = currentFaction.GetUniqueLoadID() ?? string.Empty;
    if (currentSession.boundRansomTargetPawnLoadId <= 0 ||
        !string.Equals(currentSession.boundRansomTargetFactionId ?? string.Empty, factionId, StringComparison.Ordinal))
    {
        return false;
    }

    int boundId = currentSession.boundRansomTargetPawnLoadId;
    if (!PrisonerRansomService.TryResolvePawnByLoadId(boundId, out Pawn boundPawn) ||
        !PrisonerRansomService.IsRansomEligibleTarget(boundPawn, currentFaction, out _))
    {
        ClearRansomTargetBinding(currentSession);
        return false;
    }

    targetPawnLoadId = boundId;
    targetPawn = boundPawn;
    return true;
}



internal static void BindRansomTarget(FactionDialogueSession currentSession, Faction currentFaction, int pawnLoadId)
{
    if (currentSession == null || currentFaction == null)
    {
        return;
    }

    currentSession.boundRansomTargetPawnLoadId = Math.Max(0, pawnLoadId);
    currentSession.boundRansomTargetFactionId = currentFaction.GetUniqueLoadID() ?? string.Empty;
}



internal static void ClearRansomTargetBinding(FactionDialogueSession currentSession)
{
    if (currentSession == null)
    {
        return;
    }

    currentSession.boundRansomTargetPawnLoadId = 0;
    currentSession.boundRansomTargetFactionId = string.Empty;
}



internal static void MarkRansomInfoRequestCompleted(FactionDialogueSession currentSession, Faction currentFaction, int selectedPawnLoadId)
{
    if (currentSession == null || currentFaction == null || selectedPawnLoadId <= 0)
    {
        return;
    }

    currentSession.hasCompletedRansomInfoRequest = true;
    currentSession.boundRansomTargetPawnLoadId = selectedPawnLoadId;
    currentSession.boundRansomTargetFactionId = currentFaction.GetUniqueLoadID() ?? string.Empty;
}



internal static void MarkRansomInfoRequestIncomplete(FactionDialogueSession currentSession)
{
    if (currentSession == null)
    {
        return;
    }

    currentSession.hasCompletedRansomInfoRequest = false;
}



internal static bool HasCompletedRansomInfoRequestForFaction(FactionDialogueSession currentSession, Faction currentFaction)
{
    if (currentSession == null || currentFaction == null || !currentSession.hasCompletedRansomInfoRequest)
    {
        return false;
    }

    string factionId = currentFaction.GetUniqueLoadID() ?? string.Empty;
    return currentSession.boundRansomTargetPawnLoadId > 0 &&
        string.Equals(currentSession.boundRansomTargetFactionId ?? string.Empty, factionId, StringComparison.Ordinal);
}



internal static bool IsRequestInfoPrisonerAction(AIAction action)
{
    return action != null &&
        string.Equals(action.ActionType, AIActionNames.RequestInfo, StringComparison.Ordinal);
}



internal static void ResetRansomSelectionStateAfterPayment(FactionDialogueSession currentSession)
{
    if (currentSession == null)
    {
        return;
    }

    currentSession.isWaitingForRansomTargetSelection = false;
    currentSession.hasCompletedRansomInfoRequest = false;
    currentSession.boundRansomTargetPawnLoadId = 0;
    currentSession.boundRansomTargetFactionId = string.Empty;
    currentSession.ClearPendingRansomBatchSelection();
    currentSession.ClearPendingRansomOfferReference();
    Log.Message("[RimAI.Relations] pay_prisoner_ransom succeeded. Cleared request_info(prisoner) state.");
}



internal static bool IsPayPrisonerRansomAction(AIAction action)
{
    return action != null &&
           string.Equals(action.ActionType, AIActionNames.PayPrisonerRansom, StringComparison.Ordinal);
}



internal static bool TryReadPositiveInt(Dictionary<string, object> values, string key, out int parsed)
{
    return DiplomacyParameterParse.TryReadPositiveInt(values, key, out parsed);
}



internal static bool IsRansomAutoReplyCoolingDown(FactionDialogueSession currentSession, out float remainingSeconds)
{
    remainingSeconds = 0f;
    if (currentSession == null || currentSession.ransomAutoReplyCooldownUntilRealtime <= 0f)
    {
        return false;
    }

    remainingSeconds = currentSession.ransomAutoReplyCooldownUntilRealtime - Time.realtimeSinceStartup;
    if (remainingSeconds > 0f)
    {
        return true;
    }

    currentSession.ransomAutoReplyCooldownUntilRealtime = -1f;
    currentSession.ransomAutoReplyCooldownCategory = string.Empty;
    remainingSeconds = 0f;
    return false;
}



internal static bool TryClassifyRansomAutoReplyTimeout(string detail, out string timeoutClass)
{
    timeoutClass = string.Empty;
    string text = (detail ?? string.Empty).Trim();
    if (string.IsNullOrWhiteSpace(text)) return false;
    if (IsQueueTimeoutText(text)) { timeoutClass = "queue_timeout"; return true; }
    if (IsNetworkTimeoutText(text)) { timeoutClass = "network_timeout"; return true; }
    if (IsDropTimeoutText(text)) { timeoutClass = "drop_timeout"; return true; }
    return false;
}



internal static bool IsQueueTimeoutText(string text)
{
    return ContainsTimeoutToken(text, "queue") ||
           ContainsTimeoutToken(text, "排队");
}



internal static bool IsNetworkTimeoutText(string text)
{
    return ContainsTimeoutToken(text, "curl error 28") ||
           ContainsTimeoutToken(text, "request timeout") ||
           ContainsTimeoutToken(text, "timed out") ||
           ContainsTimeoutToken(text, "timeout") ||
           ContainsTimeoutToken(text, "超时");
}



internal static bool IsDropTimeoutText(string text)
{
    return ContainsTimeoutToken(text, "dropped") ||
           ContainsTimeoutToken(text, "pending_request_mismatch") ||
           ContainsTimeoutToken(text, "request_lease_invalid") ||
           ContainsTimeoutToken(text, "queue_timeout");
}



internal static bool ContainsTimeoutToken(string source, string token)
{
    return !string.IsNullOrWhiteSpace(source) &&
           !string.IsNullOrWhiteSpace(token) &&
           source.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
}



internal static void ArmRansomAutoReplyTimeoutCooldown(
    FactionDialogueSession currentSession,
    string timeoutClass,
    string detail)
{
    if (currentSession == null)
    {
        return;
    }

    float now = Time.realtimeSinceStartup;
    float nextDeadline = now + DiplomacyRansomSelectionWorkflow.RansomAutoReplyTimeoutCooldownSeconds;
    currentSession.ransomAutoReplyCooldownUntilRealtime =
        Math.Max(currentSession.ransomAutoReplyCooldownUntilRealtime, nextDeadline);
    currentSession.ransomAutoReplyCooldownCategory = timeoutClass ?? string.Empty;

    string summary = (detail ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
    if (summary.Length > 160)
    {
        summary = summary.Substring(0, 160) + "...";
    }

    Log.Warning($"[RimAI.Relations] ransom auto-reply timeout classified={timeoutClass} cooldown=90s detail={summary}");
}
}
