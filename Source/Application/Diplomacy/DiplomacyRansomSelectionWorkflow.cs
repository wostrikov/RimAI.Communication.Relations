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
using Ustas.RimAI.Communication.Relations.Diagnostics;

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class DiplomacyRansomSelectionWorkflow : DiplomacyDialogueCollaborator
{
    internal DiplomacyRansomSelectionWorkflow(Dialog_DiplomacyDialogue owner) : base(owner) { }


internal const string RequestInfoTypePrisoner = "prisoner";


internal const float RansomOfferWindowMinMultiplier = 0.10f;


internal const float RansomOfferWindowMaxMultiplier = 3.00f;


internal const float BatchRansomEstimateMultiplier = 0.80f;


internal const float RansomAutoReplyTimeoutCooldownSeconds = 90f;


internal const string BatchGroupIdParameterKey = "batch_group_id";


internal const string BatchTargetCountParameterKey = "batch_target_count";


internal const string BatchTotalOfferSilverParameterKey = "batch_total_offer_silver";



internal bool TryHandleRequestInfoActionForPrisoner(
    AIAction action,
    FactionDialogueSession currentSession,
    Faction currentFaction,
    out ActionExecutionOutcome outcome)
{
    outcome = null;
    if (!DiplomacyRansomBatchRuntime.IsRequestInfoPrisonerAction(action))
    {
        return false;
    }

    if (action.Parameters == null)
    {
        action.Parameters = new Dictionary<string, object>(StringComparer.Ordinal);
    }

    string infoType = DiplomacyImageActionWorkflow.ReadStringParameter(action.Parameters, "info_type").Trim().ToLowerInvariant();
    if (!string.Equals(infoType, RequestInfoTypePrisoner, StringComparison.Ordinal))
    {
        Log.Warning($"[RimAI.Relations] request_info rejected: unsupported info_type={infoType}");
        outcome = ActionExecutionOutcome.Failure(action, "RimChat_RequestInfoInvalidTypeSystem".Translate().ToString());
        return true;
    }

    action.Parameters["info_type"] = RequestInfoTypePrisoner;
    if (currentSession != null && currentSession.isWaitingForRansomTargetSelection)
    {
        ModuleLog.Message("[RimAI.Relations] request_info(prisoner) dedup hit: selection already in progress.");
        outcome = ActionExecutionOutcome.Success(action, "RimChat_RansomNeedPrisonerSelectionSystem".Translate().ToString());
        return true;
    }

    if (DiplomacyRansomBatchRuntime.TryGetPendingRansomBatchSelection(currentSession, out PendingRansomBatchSelection pendingBatch))
    {
        outcome = ActionExecutionOutcome.Success(
            action,
            "RimChat_RansomBatchNeedOfferSystem".Translate(
                pendingBatch.TargetPawnLoadIds.Count,
                pendingBatch.TotalMinOfferSilver,
                pendingBatch.TotalMaxOfferSilver,
                pendingBatch.TotalCurrentAskSilver).ToString());
        return true;
    }

    if (DiplomacyRansomBatchRuntime.TryUseBoundRansomTarget(currentSession, currentFaction, out int boundTargetId, out Pawn boundTargetPawn))
    {
        string targetLabel = boundTargetPawn?.LabelShortCap ?? "RimChat_Unknown".Translate().ToString();
        ModuleLog.Message($"[RimAI.Relations] request_info(prisoner) dedup hit: target={boundTargetId}, skipping selection popup.");
        outcome = ActionExecutionOutcome.Success(
            action,
            "RimChat_RansomNeedOfferSystem".Translate(targetLabel).ToString());
        return true;
    }

    ModuleLog.Message("[RimAI.Relations] request_info(prisoner) received.");
    bool started = StartRansomTargetSelection(currentSession, currentFaction, out int candidateCount);
    ModuleLog.Message($"[RimAI.Relations] request_info(prisoner) candidate_count={candidateCount}, selection_started={started}.");

    if (!started)
    {
        outcome = ActionExecutionOutcome.Failure(action, "RimChat_RansomNoEligiblePrisonerSystem".Translate().ToString());
        return true;
    }

    outcome = ActionExecutionOutcome.Success(action, "RimChat_RansomNeedPrisonerSelectionSystem".Translate().ToString());
    return true;
}



internal void TryStartManualPrisonerInfoSend()
{
    if (!Owner.Parts.Presence.CanSendMessageNow() || session == null || faction == null)
    {
        return;
    }

    bool started = StartRansomTargetSelection(session, faction, out int candidateCount, false);
    ModuleLog.Message($"[RimAI.Relations] manual prisoner info send. candidate_count={candidateCount}, selection_started={started}.");
}



internal bool TryHandlePrisonerRansomActionWithSelection(
    AIAction action,
    FactionDialogueSession currentSession,
    Faction currentFaction,
    out ActionExecutionOutcome outcome)
{
    outcome = null;
    if (!DiplomacyRansomBatchRuntime.IsPayPrisonerRansomAction(action))
    {
        return false;
    }

    if (action.Parameters == null)
    {
        action.Parameters = new Dictionary<string, object>(StringComparer.Ordinal);
    }

    if (!TryEnsureRansomTargetParameter(action, currentSession, currentFaction, out Pawn resolvedTarget, allowSelectionPrompt: true))
    {
        string pendingMessage = DiplomacyRansomBatchRuntime.ResolveRansomPendingMessage(currentSession);
        currentSession?.AddMessage("System", pendingMessage, false, DialogueMessageType.System);
        Log.Warning("[RimAI.Relations] pay_prisoner_ransom pending: missing valid target_pawn_load_id, selection requested.");
        outcome = ActionExecutionOutcome.Failure(action, pendingMessage);
        return true;
    }

    if (!DiplomacyRansomBatchRuntime.TryReadPositiveInt(action.Parameters, "offer_silver", out _))
    {
        currentSession?.AddMessage(
            "System",
            "RimChat_RansomNeedOfferSystem".Translate(resolvedTarget?.LabelShortCap ?? "Unknown").ToString(),
            false,
            DialogueMessageType.System);
        outcome = ActionExecutionOutcome.Failure(action, "RimChat_RansomNeedOfferSystem".Translate(resolvedTarget?.LabelShortCap ?? "Unknown").ToString());
        return true;
    }

    string paymentMode = DiplomacyImageActionWorkflow.ReadStringParameter(action.Parameters, "payment_mode").Trim();
    if (string.IsNullOrWhiteSpace(paymentMode))
    {
        action.Parameters["payment_mode"] = "silver";
    }

    if (!DiplomacyRansomBatchRuntime.HasPendingRansomBatchSelection(currentSession))
    {
        DiplomacyRansomProofWorkflow.NormalizeSingleRansomOfferForExecution(action, currentSession, resolvedTarget);
    }

    return false;
}



internal bool TryEnsureRansomTargetParameter(
    AIAction action,
    FactionDialogueSession currentSession,
    Faction currentFaction,
    out Pawn targetPawn,
    bool allowSelectionPrompt)
{
    targetPawn = null;
    if (action?.Parameters == null || currentFaction == null)
    {
        return false;
    }

    if (DiplomacyRansomBatchRuntime.TryReadPositiveInt(action.Parameters, "target_pawn_load_id", out int explicitTargetId) &&
        PrisonerRansomService.TryResolvePawnByLoadId(explicitTargetId, out Pawn explicitTarget) &&
        PrisonerRansomService.IsRansomEligibleTarget(explicitTarget, currentFaction, out _))
    {
        action.Parameters["target_pawn_load_id"] = explicitTargetId;
        DiplomacyRansomBatchRuntime.BindRansomTarget(currentSession, currentFaction, explicitTargetId);
        targetPawn = explicitTarget;
        return true;
    }

    action.Parameters.Remove("target_pawn_load_id");
    if (DiplomacyRansomBatchRuntime.HasPendingRansomBatchSelection(currentSession))
    {
        return false;
    }

    if (DiplomacyRansomBatchRuntime.TryUseBoundRansomTarget(currentSession, currentFaction, out int boundTargetId, out Pawn boundPawn))
    {
        action.Parameters["target_pawn_load_id"] = boundTargetId;
        targetPawn = boundPawn;
        return true;
    }

    if (allowSelectionPrompt &&
        currentSession != null &&
        !currentSession.isWaitingForRansomTargetSelection &&
        !DiplomacyRansomBatchRuntime.HasPendingRansomBatchSelection(currentSession))
    {
        StartRansomTargetSelection(currentSession, currentFaction, out _);
    }

    return false;
}



internal bool StartRansomTargetSelection(
    FactionDialogueSession currentSession,
    Faction currentFaction,
    out int candidateCount,
    bool emitSelectionPromptMessage = true)
{
    candidateCount = 0;
    if (currentSession == null || currentFaction == null)
    {
        return false;
    }

    if (currentSession.isWaitingForRansomTargetSelection)
    {
        ModuleLog.Message("[RimAI.Relations] request_info(prisoner) dedup hit: selection already in progress.");
        return false;
    }

    if (emitSelectionPromptMessage &&
        DiplomacyRansomBatchRuntime.TryUseBoundRansomTarget(currentSession, currentFaction, out int boundTargetId, out _))
    {
        candidateCount = 1;
        ModuleLog.Message($"[RimAI.Relations] request_info(prisoner) dedup hit: reuse bound target={boundTargetId}, skip reselection.");
        return true;
    }

    if (emitSelectionPromptMessage && DiplomacyRansomBatchRuntime.HasPendingRansomBatchSelection(currentSession))
    {
        if (DiplomacyRansomBatchRuntime.TryGetPendingRansomBatchSelection(currentSession, out PendingRansomBatchSelection pendingBatch))
        {
            candidateCount = pendingBatch.TargetPawnLoadIds.Count;
        }

        ModuleLog.Message("[RimAI.Relations] request_info(prisoner) dedup hit: reuse pending ransom batch selection.");
        return true;
    }

    List<Pawn> candidates = DiplomacyRansomBatchRuntime.CollectEligibleRansomTargets(currentFaction);
    candidateCount = candidates.Count;
    if (candidates.Count == 0)
    {
        DiplomacyRansomBatchRuntime.ClearRansomTargetBinding(currentSession);
        DiplomacyRansomBatchRuntime.ClearPendingRansomBatchSelection(currentSession);
        currentSession?.AddMessage(
            "System",
            "RimChat_RansomNoEligiblePrisonerSystem".Translate().ToString(),
            false,
            DialogueMessageType.System);
        DiplomacyRansomBatchRuntime.MarkRansomInfoRequestIncomplete(currentSession);
        return false;
    }

    currentSession.isWaitingForRansomTargetSelection = true;
    DiplomacyRansomBatchRuntime.MarkRansomInfoRequestIncomplete(currentSession);
    if (emitSelectionPromptMessage)
    {
        currentSession.AddMessage(
            "System",
            "RimChat_RansomNeedPrisonerSelectionSystem".Translate().ToString(),
            false,
            DialogueMessageType.System);
    }

    Find.WindowStack.Add(new Dialog_PrisonerRansomTargetSelector(
        currentFaction,
        candidates,
        selected => HandleRansomTargetsSelected(currentSession, currentFaction, selected),
        () => HandleRansomTargetSelectionCanceled(currentSession)));
    return true;
}



internal void HandleRansomTargetsSelected(FactionDialogueSession currentSession, Faction currentFaction, List<Pawn> selectedPawns)
{
    if (currentSession == null)
    {
        return;
    }

    currentSession.isWaitingForRansomTargetSelection = false;
    if (selectedPawns == null || selectedPawns.Count <= 0 || currentFaction == null)
    {
        return;
    }

    if (selectedPawns.Count == 1)
    {
        HandleRansomTargetSelectedSingle(currentSession, currentFaction, selectedPawns[0]);
        return;
    }

    HandleRansomBatchTargetsSelected(currentSession, currentFaction, selectedPawns);
}



internal void HandleRansomTargetSelectedSingle(FactionDialogueSession currentSession, Faction currentFaction, Pawn selectedPawn)
{
    if (currentSession == null || selectedPawn == null || currentFaction == null)
    {
        return;
    }

    if (!PrisonerRansomService.IsRansomEligibleTarget(selectedPawn, currentFaction, out _))
    {
        currentSession.AddMessage(
            "System",
            "RimChat_RansomSelectedPrisonerInvalidSystem".Translate().ToString(),
            false,
            DialogueMessageType.System);
        DiplomacyRansomBatchRuntime.ClearPendingRansomOfferReference(currentSession);
        DiplomacyRansomBatchRuntime.ClearRansomTargetBinding(currentSession);
        DiplomacyRansomBatchRuntime.ClearPendingRansomBatchSelection(currentSession);
        DiplomacyRansomBatchRuntime.MarkRansomInfoRequestIncomplete(currentSession);
        return;
    }

    DiplomacyRansomBatchRuntime.ClearPendingRansomBatchSelection(currentSession);
    DiplomacyRansomBatchRuntime.ClearPendingRansomOfferReference(currentSession);
    DiplomacyRansomBatchRuntime.BindRansomTarget(currentSession, currentFaction, selectedPawn.thingIDNumber);
    DiplomacyRansomBatchRuntime.MarkRansomInfoRequestCompleted(currentSession, currentFaction, selectedPawn.thingIDNumber);
    ModuleLog.Message($"[RimAI.Relations] request_info(prisoner) completed. selected_target={selectedPawn.thingIDNumber}.");
    Owner.Parts.RansomProof.PublishRansomProofCard(currentSession, currentFaction, selectedPawn);
}



internal void HandleRansomTargetSelectionCanceled(FactionDialogueSession currentSession)
{
    if (currentSession == null)
    {
        return;
    }

    currentSession.isWaitingForRansomTargetSelection = false;
    DiplomacyRansomBatchRuntime.ClearRansomTargetBinding(currentSession);
    DiplomacyRansomBatchRuntime.ClearPendingRansomBatchSelection(currentSession);
    DiplomacyRansomBatchRuntime.ClearPendingRansomOfferReference(currentSession);
    DiplomacyRansomBatchRuntime.MarkRansomInfoRequestIncomplete(currentSession);
    ModuleLog.Message("[RimAI.Relations] request_info(prisoner) cancelled by player.");
    currentSession.AddMessage(
        "System",
        "RimChat_RansomSelectionCancelledSystem".Translate().ToString(),
        false,
        DialogueMessageType.System);
}



internal void HandleRansomBatchTargetsSelected(
    FactionDialogueSession currentSession,
    Faction currentFaction,
    List<Pawn> selectedPawns)
{
    if (currentSession == null || currentFaction == null || selectedPawns == null || selectedPawns.Count <= 1)
    {
        return;
    }

    DiplomacyRansomBatchRuntime.ClearPendingRansomOfferReference(currentSession);
    if (!TryBuildRansomBatchQuoteEntries(currentFaction, selectedPawns, out List<RansomBatchQuoteEntry> entries, out string failureMessage))
    {
        currentSession.AddMessage("System", failureMessage, false, DialogueMessageType.System);
        DiplomacyRansomBatchRuntime.ClearPendingRansomBatchSelection(currentSession);
        DiplomacyRansomBatchRuntime.MarkRansomInfoRequestIncomplete(currentSession);
        return;
    }

    int totalCurrentAskSilver = entries.Sum(entry => entry.CurrentAskSilver);
    int totalMinOfferSilver = entries.Sum(entry => entry.MinOfferSilver);
    int totalMaxOfferSilver = entries.Sum(entry => entry.MaxOfferSilver);
    string batchGroupId = Guid.NewGuid().ToString("N");
    List<int> targetIds = entries.Select(entry => entry.TargetPawn.thingIDNumber).ToList();
    currentSession.SetPendingRansomBatchSelection(
        batchGroupId,
        targetIds,
        totalCurrentAskSilver,
        totalMinOfferSilver,
        totalMaxOfferSilver);
    DiplomacyRansomBatchRuntime.MarkRansomInfoRequestIncomplete(currentSession);
    DiplomacyRansomBatchRuntime.ClearRansomTargetBinding(currentSession);
    Owner.Parts.RansomProof.PublishRansomBatchInfoCard(currentSession, currentFaction, entries, totalCurrentAskSilver, totalMinOfferSilver, totalMaxOfferSilver);
}



internal bool TryBuildRansomBatchQuoteEntries(
    Faction currentFaction,
    List<Pawn> selectedPawns,
    out List<RansomBatchQuoteEntry> entries,
    out string failureMessage)
{
    entries = new List<RansomBatchQuoteEntry>();
    failureMessage = "RimChat_RansomQuoteUnavailableSystem".Translate().ToString();
    if (currentFaction == null || selectedPawns == null || selectedPawns.Count <= 0)
    {
        return false;
    }

    foreach (Pawn selectedPawn in selectedPawns
        .Where(pawn => pawn != null)
        .GroupBy(pawn => pawn.thingIDNumber)
        .Select(group => group.First()))
    {
        if (!PrisonerRansomService.IsRansomEligibleTarget(selectedPawn, currentFaction, out _))
        {
            failureMessage = "RimChat_RansomSelectedPrisonerInvalidSystem".Translate().ToString();
            return false;
        }

        GameAIInterface.Instance.CapturePrisonerInfoCardCoreOrganSnapshot(currentFaction, selectedPawn);
        GameAIInterface.APIResult quoteResult = GameAIInterface.Instance.CalculatePrisonerRansomQuote(
            currentFaction,
            selectedPawn,
            forceRefresh: true);
        if (!quoteResult.Success || !(quoteResult.Data is PrisonerRansomResultData quoteData) || quoteData.CurrentAskSilver <= 0)
        {
            failureMessage = "RimChat_RansomReferenceAskUnavailableSystem".Translate(selectedPawn.LabelShortCap).ToString();
            return false;
        }

        if (!DiplomacyRansomProofWorkflow.TryGetRansomOfferWindow(quoteData, out int minOfferSilver, out int maxOfferSilver))
        {
            failureMessage = "RimChat_RansomOfferOutOfWindowSimpleSystem".Translate(quoteData.CurrentAskSilver).ToString();
            return false;
        }

        entries.Add(new RansomBatchQuoteEntry(
            selectedPawn,
            DiplomacyRansomProofWorkflow.ResolveBatchEstimatedAskSilver(quoteData.CurrentAskSilver),
            minOfferSilver,
            maxOfferSilver));
    }

    return entries.Count > 1;
}
}
