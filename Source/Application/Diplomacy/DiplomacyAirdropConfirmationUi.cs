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

internal sealed class DiplomacyAirdropConfirmationUi : DiplomacyDialogueCollaborator
{
    internal DiplomacyAirdropConfirmationUi(Dialog_DiplomacyDialogue owner) : base(owner) { }

internal void ShowAirdropTradeConfirmationDialog(
    FactionDialogueSession currentSession,
    Faction currentFaction,
    ItemAirdropPreparedTradeData preparedTrade,
    Dictionary<string, object> baseParameters,
    List<PendingAirdropSelectionCandidate> pendingCandidates)
{
    List<PendingAirdropSelectionCandidate> availableCandidates = pendingCandidates?
        .OrderBy(candidate => candidate.Index)
        .Take(5)
        .ToList() ?? new List<PendingAirdropSelectionCandidate>();
    Owner.Parts.Airdrop.ClearPendingAirdropDialogState("reschedule_confirmation", false);
    pendingAirdropDialogState = new PendingAirdropDialogState
    {
        Session = currentSession,
        Faction = currentFaction,
        PreparedTrade = preparedTrade,
        BaseParameters = DiplomacyActionPolicyService.CloneParameters(baseParameters),
        PendingCandidates = ClonePendingAirdropCandidates(availableCandidates)
    };
    ModuleLog.Message(
        $"[RimAI.Relations] AirdropConfirmScheduled: def={preparedTrade?.SelectedDefName ?? "unknown"},count={preparedTrade?.Quantity ?? 0},candidateCount={availableCandidates.Count}");
}



internal void OpenQueuedAirdropTradeConfirmationDialog(PendingAirdropDialogState state)
{
    if (state == null || state.PreparedTrade == null)
    {
        return;
    }

    ItemAirdropPreparedTradeData trade = state.PreparedTrade;
    string tradeLabel = string.IsNullOrWhiteSpace(trade?.ResolvedLabel)
        ? (trade?.SelectedDefName ?? "")
        : trade.ResolvedLabel;
    int quantity = trade?.Quantity ?? 1;
    int requestedQuantity = trade?.RequestedQuantity ?? quantity;
    int paymentTotal = trade?.PaymentTotalSilver ?? 0;
    float unitPrice = trade?.NeedQuotedUnitSilver ?? 0f;
    string priceTag = BuildPriceSemanticTag(trade?.NeedPriceSemantic);
    int shippingCost = Math.Max(0, trade?.ShippingCostSilver ?? 0);
    int shippingPods = Math.Max(0, trade?.ShippingPodCount ?? 0);
    string adjustmentReason = trade?.CountAdjustmentReason ?? string.Empty;

    List<PendingAirdropSelectionCandidate> availableCandidates = state.PendingCandidates?
        .OrderBy(candidate => candidate.Index)
        .Take(5)
        .ToList() ?? new List<PendingAirdropSelectionCandidate>();
    bool hasManualAlternative = availableCandidates.Count > 1;

    var confirmationDialog = new Dialog_AirdropTradeConfirmWithAlternative(
        tradeLabel,
        quantity,
        requestedQuantity,
        paymentTotal,
        unitPrice,
        priceTag,
        shippingCost,
        shippingPods,
        adjustmentReason,
        hasManualAlternative,
        () => CommitConfirmedAirdropTrade(state.Session, state.Faction, state.PreparedTrade),
        () =>
        {
            // Pre-fill trade card from current prepared trade parameters.
            ItemAirdropPreparedTradeData trade = state.PreparedTrade;
            if (trade != null && state.Session != null &&
                !string.IsNullOrWhiteSpace(trade.SelectedDefName) && trade.Quantity > 0)
            {
                state.Session.CacheAirdropCounteroffer(
                    trade.SelectedDefName,
                    trade.Quantity,
                    trade.PaymentTotalSilver,
                    string.Empty);
            }

            CancelConfirmedAirdropTrade(state.Session, state.Faction, skipSystemMessage: true);
            if (state.Session != null && state.Faction != null)
            {
                Find.WindowStack.Add(new Dialog_ItemAirdropTradeCard(
                    state.Session,
                    state.Faction,
                    Owner.Parts.SendInfo.OnAirdropTradeCardSubmitted));
            }
        },
        () => OpenAirdropAlternativeSelection(state.Session, state.Faction, state.BaseParameters, availableCandidates));
    Find.WindowStack.Add(confirmationDialog);
}



internal static List<PendingAirdropSelectionCandidate> ClonePendingAirdropCandidates(
    List<PendingAirdropSelectionCandidate> candidates)
{
    if (candidates == null || candidates.Count <= 0)
    {
        return new List<PendingAirdropSelectionCandidate>();
    }

    return candidates
        .Where(candidate => candidate != null)
        .Select(candidate => new PendingAirdropSelectionCandidate
        {
            Index = candidate.Index,
            DefName = candidate.DefName ?? string.Empty,
            Label = candidate.Label ?? string.Empty,
            UnitPrice = candidate.UnitPrice,
            MaxLegalCount = candidate.MaxLegalCount
        })
        .ToList();
}



internal void OpenAirdropAlternativeSelection(
    FactionDialogueSession currentSession,
    Faction currentFaction,
    Dictionary<string, object> baseParameters,
    List<PendingAirdropSelectionCandidate> availableCandidates)
{
    if (currentFaction == null || currentSession == null || availableCandidates == null || availableCandidates.Count <= 1)
    {
        return;
    }

    Dictionary<string, object> safeParams = baseParameters ?? new Dictionary<string, object>();

    var options = new List<FloatMenuOption>();
    foreach (PendingAirdropSelectionCandidate candidate in availableCandidates)
    {
        if (candidate == null || string.IsNullOrWhiteSpace(candidate.DefName))
        {
            continue;
        }

        string optionText = "RimChat_ItemAirdropSelectionPendingLine".Translate(
            candidate.Index,
            candidate.Label ?? candidate.DefName,
            candidate.DefName,
            candidate.UnitPrice.ToString("F1", CultureInfo.InvariantCulture),
            Math.Max(0, candidate.MaxLegalCount)).ToString();
        options.Add(new FloatMenuOption(optionText, () =>
        {
            Dictionary<string, object> mappedParameters = DiplomacyActionPolicyService.CloneParameters(safeParams);
            mappedParameters["selected_def"] = candidate.DefName;
            var mappedAction = new AIAction
            {
                ActionType = AIActionNames.RequestItemAirdrop,
                Parameters = mappedParameters,
                Reason = "selection_manual_alternative"
            };

            if (!Owner.Parts.Airdrop.TryHandleAirdropActionWithConfirmation(mappedAction, currentSession, currentFaction, out _))
            {
                currentSession?.AddMessage(
                    "System",
                    "RimChat_ItemAirdropCommitFailedSystem".Translate("manual_selection_failed"),
                    false,
                    DialogueMessageType.System);
            }
        }));
    }

    if (options.Count <= 0)
    {
        return;
    }

    Find.WindowStack.Add(new FloatMenu(options));
}



internal static string BuildPriceSemanticTag(string semantic)
{
    if (string.IsNullOrWhiteSpace(semantic))
    {
        return string.Empty;
    }

    string lower = semantic.ToLowerInvariant().Trim();
    if (lower.StartsWith("special_item_discount"))
        return "RimChat_ItemAirdropPriceSemanticDiscount".Translate().ToString();
    if (lower.StartsWith("special_item_scarce"))
        return "RimChat_ItemAirdropPriceSemanticScarce".Translate().ToString();
    if (lower.StartsWith("market_value") || lower.StartsWith("market_value_x"))
        return "RimChat_ItemAirdropPriceSemanticMarket".Translate().ToString();
    if (lower.StartsWith("untradeable_") || lower.StartsWith("black_market"))
        return "RimChat_ItemAirdropPriceSemanticBlackMarket".Translate().ToString();

    return string.Empty;
}



internal void CommitConfirmedAirdropTrade(
    FactionDialogueSession currentSession,
    Faction currentFaction,
    ItemAirdropPreparedTradeData preparedTrade)
{
    if (currentSession != null)
    {
        if (currentSession.airdropExecutionStage != AirdropExecutionStage.PreparedAwaitingConfirm)
        {
            Log.Warning($"[RimAI.Relations] AirdropStalePendingBlocked: commit rejected because stage={currentSession.airdropExecutionStage},expected={AirdropExecutionStage.PreparedAwaitingConfirm}");
            ResetAirdropConfirmationRuntime(currentSession, "commit_rejected_wrong_stage", true, true);
            TransitionAirdropExecutionStage(currentSession, AirdropExecutionStage.Failed, "commit_rejected_wrong_stage");
            currentSession.AddMessage(
                "System",
                DiplomacySessionOutcomeMessages.BuildAirdropFailureSystemMessage("selection_manual_choice"),
                false,
                DialogueMessageType.System);
            Owner.Parts.Session.SaveFactionMemory(currentSession, currentFaction);
            return;
        }

        if (HasStalePendingAirdropSelection(currentSession, out string staleDetails))
        {
            Log.Warning($"[RimAI.Relations] AirdropStalePendingBlocked: commit rejected because stale pending state survived until confirm. {staleDetails}");
            ResetAirdropConfirmationRuntime(currentSession, "commit_rejected_stale_pending", true, true);
            TransitionAirdropExecutionStage(currentSession, AirdropExecutionStage.Failed, staleDetails);
            currentSession.AddMessage(
                "System",
                DiplomacySessionOutcomeMessages.BuildAirdropFailureSystemMessage("selection_manual_choice"),
                false,
                DialogueMessageType.System);
            Owner.Parts.Session.SaveFactionMemory(currentSession, currentFaction);
            return;
        }

        TransitionAirdropExecutionStage(currentSession, AirdropExecutionStage.Committing, preparedTrade?.SelectedDefName ?? "prepared_trade");
    }

    ModuleLog.Message($"[RimAI.Relations] AirdropConfirmCommitStart: def={preparedTrade?.SelectedDefName ?? "unknown"},count={preparedTrade?.Quantity ?? 0},budget={preparedTrade?.BudgetSilver ?? 0}");
    var commitResult = GameAIInterface.Instance.CommitPreparedItemAirdropTrade(currentFaction, preparedTrade);
    if (commitResult.Success)
    {
        ResetAirdropConfirmationRuntime(currentSession, "commit_success", true, true);
        TransitionAirdropExecutionStage(currentSession, AirdropExecutionStage.Completed, preparedTrade?.SelectedDefName ?? "commit_success");
        var payload = commitResult.Data as ItemAirdropResultData;
        string text = payload != null
            ? DiplomacySessionOutcomeMessages.BuildAirdropSuccessSystemMessage(payload)
            : "RimChat_ItemAirdropCommitSuccessSystem".Translate().ToString();
        currentSession?.AddMessage("System", text, false, DialogueMessageType.System);
        ModuleLog.Message($"[RimAI.Relations] AirdropConfirmCommitResult: success=True,def={payload?.SelectedDefName ?? preparedTrade?.SelectedDefName ?? "unknown"},count={payload?.Quantity ?? preparedTrade?.Quantity ?? 0},failureCode=none");
    }
    else
    {
        ResetAirdropConfirmationRuntime(currentSession, "commit_failed", true, true);
        string transitionReason = commitResult?.Message ?? "commit_failed";
        var payload = commitResult.Data as ItemAirdropResultData;
        if (!string.IsNullOrWhiteSpace(payload?.FailureCode))
        {
            transitionReason = payload.FailureCode;
        }

        TransitionAirdropExecutionStage(currentSession, AirdropExecutionStage.Failed, transitionReason);
        if (payload != null && !string.IsNullOrWhiteSpace(payload.FailureCode))
        {
            currentSession?.AddMessage(
                "System",
                DiplomacySessionOutcomeMessages.BuildAirdropFailureSystemMessage(payload.FailureCode),
                false,
                DialogueMessageType.System);
        }
        else
        {
            string reason = string.IsNullOrWhiteSpace(commitResult?.Message)
                ? "RimChat_Unknown".Translate().ToString()
                : commitResult.Message;
            currentSession?.AddMessage(
                "System",
                "RimChat_ItemAirdropCommitFailedSystem".Translate(reason),
                false,
                DialogueMessageType.System);
        }

        ModuleLog.Message($"[RimAI.Relations] AirdropConfirmCommitResult: success=False,def={preparedTrade?.SelectedDefName ?? "unknown"},count={preparedTrade?.Quantity ?? 0},failureCode={payload?.FailureCode ?? "none"},message={commitResult?.Message ?? "none"}");
    }

    Owner.Parts.Session.SaveFactionMemory(currentSession, currentFaction);
}



internal static bool IsAirdropDelayedIntent(PendingDelayedActionIntent intent)
{
    return intent != null &&
           string.Equals(intent.ActionType, AIActionNames.RequestItemAirdrop, StringComparison.Ordinal);
}



internal static bool ClearAirdropDelayedIntentRuntime(FactionDialogueSession currentSession)
{
    if (currentSession == null)
    {
        return false;
    }

    bool cleared = false;
    if (IsAirdropDelayedIntent(currentSession.pendingDelayedActionIntent))
    {
        currentSession.pendingDelayedActionIntent = null;
        cleared = true;
    }

    if (IsAirdropDelayedIntent(currentSession.lastDelayedActionIntent))
    {
        currentSession.lastDelayedActionIntent = null;
        cleared = true;
    }

    return cleared;
}



internal void CancelConfirmedAirdropTrade(FactionDialogueSession currentSession, Faction currentFaction, bool skipSystemMessage = false)
{
    bool clearedDelayedIntent = ClearAirdropDelayedIntentRuntime(currentSession);
    ResetAirdropConfirmationRuntime(currentSession, "commit_cancelled", true, true, true);
    TransitionAirdropExecutionStage(currentSession, AirdropExecutionStage.Idle, "player_cancelled_confirmation");
    ModuleLog.Message($"[RimAI.Relations] AirdropConfirmExplicitCancel: stage={currentSession?.airdropExecutionStage.ToString() ?? "null"},faction={currentFaction?.Name ?? "null"},clearedDelayedIntent={clearedDelayedIntent}");

    if (!skipSystemMessage)
    {
        currentSession?.AddMessage(
            "System",
            "RimChat_ItemAirdropCancelledSystem".Translate(),
            false,
            DialogueMessageType.System);
    }

    Owner.Parts.Session.SaveFactionMemory(currentSession, currentFaction);
}


internal static void TransitionAirdropExecutionStage(
    FactionDialogueSession currentSession,
    AirdropExecutionStage nextStage,
    string reason)
{
    if (currentSession == null)
    {
        return;
    }

    AirdropExecutionStage previousStage = currentSession.airdropExecutionStage;
    currentSession.airdropExecutionStage = nextStage;
    ModuleLog.Message($"[RimAI.Relations] AirdropStateTransition: {previousStage} -> {nextStage} reason={reason ?? "none"}");
}



internal static void ResetAirdropConfirmationRuntime(
    FactionDialogueSession currentSession,
    string reason,
    bool disposeLease,
    bool clearTradeCardReference = false,
    bool resetStageToIdle = false)
{
    if (currentSession == null)
    {
        return;
    }

    bool clearedPendingIntent = currentSession.ClearPendingAirdropSelectionIntentState();
    bool clearedAirdropIntent = ClearAirdropDelayedIntentRuntime(currentSession);
    bool hadAsyncState =
        currentSession.isWaitingForAirdropSelection ||
        !string.IsNullOrWhiteSpace(currentSession.pendingAirdropRequestId) ||
        currentSession.pendingAirdropRequestLease != null;
    if (hadAsyncState)
    {
        DiplomacyAirdropAsyncWorkflow.ClearAirdropAsyncRequestState(currentSession, disposeLease);
    }

    if (clearTradeCardReference && currentSession.hasPendingAirdropTradeCardReference)
    {
        currentSession.ClearPendingAirdropTradeCardReference();
    }

    if (resetStageToIdle)
    {
        currentSession.airdropExecutionStage = AirdropExecutionStage.Idle;
        currentSession.airdropPreparedAwaitingConfirmTick = 0;
    }

    if (clearedPendingIntent || clearedAirdropIntent || hadAsyncState || clearTradeCardReference || resetStageToIdle)
    {
        currentSession.airdropRequestGeneration++;
        ModuleLog.Message(
            $"[RimAI.Relations] AirdropPendingIntentInvalidated: reason={reason ?? "none"},clearedPendingIntent={clearedPendingIntent},clearedAirdropIntent={clearedAirdropIntent},clearedAsyncState={hadAsyncState},clearedTradeCard={clearTradeCardReference},resetStageToIdle={resetStageToIdle},generation={currentSession.airdropRequestGeneration}");
    }
}



internal static bool HasStalePendingAirdropSelection(
    FactionDialogueSession currentSession,
    out string details)
{
    details = string.Empty;
    if (currentSession == null)
    {
        return false;
    }

    bool hasPendingIntent = currentSession.HasPendingAirdropSelectionIntent();
    bool hasAsyncState =
        currentSession.isWaitingForAirdropSelection ||
        !string.IsNullOrWhiteSpace(currentSession.pendingAirdropRequestId) ||
        currentSession.pendingAirdropRequestLease != null;
    if (!hasPendingIntent && !hasAsyncState)
    {
        return false;
    }

    details =
        $"stage={currentSession.airdropExecutionStage},hasPendingIntent={hasPendingIntent},isWaitingForSelection={currentSession.isWaitingForAirdropSelection}," +
        $"requestId={currentSession.pendingAirdropRequestId ?? "none"},hasLease={(currentSession.pendingAirdropRequestLease != null)}";
    return true;
}



internal const int AirdropPreparedAwaitingConfirmTimeoutTicks = 5000;

// 2 game hours

       internal void TryAutoCleanupStaleAirdropConfirmation(FactionDialogueSession session, Faction faction)
       {
           if (session == null || faction == null) return;
           if (session.airdropExecutionStage != AirdropExecutionStage.PreparedAwaitingConfirm) return;
           if (session.airdropPreparedAwaitingConfirmTick <= 0) return;

           int currentTick = Find.TickManager?.TicksGame ?? 0;
           int elapsed = currentTick - session.airdropPreparedAwaitingConfirmTick;
           if (elapsed < AirdropPreparedAwaitingConfirmTimeoutTicks) return;

           Log.Warning($"[RimAI.Relations] Airdrop auto-cleanup: PreparedAwaitingConfirm stale for {elapsed} ticks (> {AirdropPreparedAwaitingConfirmTimeoutTicks}). Resetting for faction={faction.Name}.");
           ResetAirdropConfirmationRuntime(session, "auto_cleanup_stale_confirmation", true, true, true);
           session.AddMessage("System", "RimChat_ItemAirdropCancelledSystem".Translate().ToString(), false, DialogueMessageType.System);
           Owner.Parts.Airdrop.ClearPendingAirdropDialogState("auto_cleanup", true);
           Owner.Parts.Session.SaveFactionMemory(session, faction);
       }
}
