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

internal sealed class DiplomacyAirdropWorkflow : DiplomacyDialogueCollaborator
{
    internal DiplomacyAirdropWorkflow(Dialog_DiplomacyDialogue owner) : base(owner) { }



internal void ClearPendingAirdropDialogState(string reason, bool log)
{
    PendingAirdropDialogState state = pendingAirdropDialogState;
    pendingAirdropDialogState = null;
    if (!log || state == null)
    {
        return;
    }

    ModuleLog.Message(
        $"[RimAI.Relations] AirdropConfirmDiscarded: reason={reason ?? "none"},def={state.PreparedTrade?.SelectedDefName ?? "unknown"},stage={state.Session?.airdropExecutionStage.ToString() ?? "null"}");
}



internal void TryProcessPendingAirdropDialog()
{
    PendingAirdropDialogState state = pendingAirdropDialogState;
    if (state == null)
    {
        return;
    }

    if (!ReferenceEquals(session, state.Session) || !ReferenceEquals(faction, state.Faction))
    {
        ClearPendingAirdropDialogState("dialogue_context_changed", true);
        return;
    }

    if (state.Session == null || state.PreparedTrade == null)
    {
        ClearPendingAirdropDialogState("dialogue_state_missing", true);
        return;
    }

    if (state.Session.airdropExecutionStage != AirdropExecutionStage.PreparedAwaitingConfirm)
    {
        ClearPendingAirdropDialogState($"unexpected_stage_{state.Session.airdropExecutionStage}", true);
        return;
    }

    if (Owner.Parts.Input.HasActiveNpcTypewriter())
    {
        if (state.TypewriterWaitStartRealtime < 0f)
        {
            state.TypewriterWaitStartRealtime = Time.realtimeSinceStartup;
        }

        if (Time.realtimeSinceStartup - state.TypewriterWaitStartRealtime < Dialog_DiplomacyDialogue.MaxTypewriterWaitSeconds)
        {
            if (!state.WaitingForTypewriterLogged)
            {
                state.WaitingForTypewriterLogged = true;
                ModuleLog.Message(
                    $"[RimAI.Relations] AirdropConfirmQueued: state=waiting_for_typewriter,def={state.PreparedTrade.SelectedDefName ?? "unknown"},count={state.PreparedTrade.Quantity}");
            }

            state.DelayStarted = false;
            state.ReadyAtRealtime = -1f;
            state.DelayWindowLogged = false;
            return;
        }

        Log.Warning(
            $"[RimAI.Relations] AirdropConfirmQueued: typewriter wait timeout after {MaxTypewriterWaitSeconds:F1}s, forcing confirmation for def={state.PreparedTrade.SelectedDefName ?? "unknown"}");
    }

    state.TypewriterWaitStartRealtime = -1f;

    if (!state.DelayStarted)
    {
        state.DelayStarted = true;
        state.ReadyAtRealtime = Time.realtimeSinceStartup + Dialog_DiplomacyDialogue.PendingAirdropDialogDelaySeconds;
        state.DelayWindowLogged = false;
        ModuleLog.Message(
            $"[RimAI.Relations] AirdropConfirmQueued: state=waiting_delay,def={state.PreparedTrade.SelectedDefName ?? "unknown"},readyInSeconds={PendingAirdropDialogDelaySeconds:F1}");
        return;
    }

    if (Time.realtimeSinceStartup < state.ReadyAtRealtime)
    {
        if (!state.DelayWindowLogged)
        {
            state.DelayWindowLogged = true;
            ModuleLog.Message(
                $"[RimAI.Relations] AirdropConfirmQueued: state=delay_countdown,def={state.PreparedTrade.SelectedDefName ?? "unknown"},readyAt={state.ReadyAtRealtime:F3}");
        }

        return;
    }

    pendingAirdropDialogState = null;
    ModuleLog.Message(
        $"[RimAI.Relations] AirdropConfirmDisplayed: def={state.PreparedTrade.SelectedDefName ?? "unknown"},count={state.PreparedTrade.Quantity},requested={state.PreparedTrade.RequestedQuantity},hardMax={state.PreparedTrade.HardMax},adjustment={state.PreparedTrade.CountAdjustmentReason},payment={state.PreparedTrade.PaymentTotalSilver}");
    Owner.Parts.AirdropConfirmUi.OpenQueuedAirdropTradeConfirmationDialog(state);
}


internal const string AirdropPendingCandidatesKey = "__airdrop_pending_candidates";


internal const string AirdropPendingFailureCodeKey = "__airdrop_pending_failure_code";



internal static bool IsRequestItemAirdropAction(AIAction action)
{
    return action != null &&
           string.Equals(action.ActionType, AIActionNames.RequestItemAirdrop, StringComparison.Ordinal);
}



internal bool TryHandleAirdropActionWithConfirmation(
    AIAction action,
    FactionDialogueSession currentSession,
    Faction currentFaction,
    out ActionExecutionOutcome outcome)
{
    outcome = null;
    if (!IsRequestItemAirdropAction(action))
    {
        return false;
    }

    if (DiplomacyAirdropAsyncWorkflow.IsAirdropAsyncRequestPending(currentSession))
    {
        outcome = ActionExecutionOutcome.Success(
            action,
            BuildAirdropSelectionInProgressSystemText(),
            new ItemAirdropAsyncQueuedData());
        return true;
    }

    AIAction actionSnapshot = new AIAction
    {
        ActionType = action.ActionType,
        Parameters = DiplomacyActionPolicyService.CloneParameters(action.Parameters),
        Reason = action.Reason
    };
    TryInjectPendingAirdropCountFromLatestPlayerMessage(actionSnapshot, currentSession);

    DialogueRuntimeContext requestContext = runtimeContext.WithCurrentRuntimeMarkers();
    string validateReason = string.Empty;
    bool resolved = DialogueContextResolver.TryResolveLiveContext(
        requestContext,
        out DialogueLiveContext liveContext,
        out string resolveReason);
    bool validated = resolved && DialogueContextValidator.ValidateRequestSend(requestContext, liveContext, out validateReason);
    if (!resolved || !validated)
    {
        string fallbackReason = string.IsNullOrWhiteSpace(validateReason) ? resolveReason : validateReason;
        Log.Warning($"[RimAI.Relations] Airdrop context validation failed: resolved={resolved}, validated={validated}, resolveReason={resolveReason}, validateReason={validateReason}, faction={currentFaction?.Name ?? "null"}, defName={currentFaction?.def?.defName ?? "null"}");
        outcome = ActionExecutionOutcome.Failure(action, fallbackReason ?? "RimChat_DialogueRequestUnavailable".Translate().ToString());
        return true;
    }

    ModuleLog.Message($"[RimAI.Relations] Airdrop context validation passed: faction={currentFaction?.Name}, defName={currentFaction?.def?.defName}, need={actionSnapshot.Parameters?["need"] ?? "null"}");

    var lease = new DialogueRequestLease(
        requestContext.DialogueSessionId,
        windowInstanceId,
        requestContext.ContextVersion);
    var prepareResult = GameAIInterface.Instance.BeginPrepareItemAirdropTradeAsync(
        currentFaction,
        actionSnapshot.Parameters,
        negotiator,
        completedResult => Owner.Parts.AirdropAsync.HandleAirdropAsyncPrepareCompleted(
            currentSession,
            currentFaction,
            lease,
            requestContext,
            actionSnapshot,
            currentSession?.airdropRequestGeneration ?? -1,
            completedResult),
        (requestId, timeoutSeconds) => DiplomacyAirdropAsyncWorkflow.BindAirdropAsyncRequest(currentSession, lease, requestId, timeoutSeconds));
    if (!prepareResult.Success)
    {
        lease.Dispose();
        DiplomacyAirdropConfirmationUi.ResetAirdropConfirmationRuntime(currentSession, "prepare_start_failed", true, true);
        DiplomacyAirdropConfirmationUi.TransitionAirdropExecutionStage(currentSession, AirdropExecutionStage.Failed, prepareResult?.Message ?? "prepare_start_failed");
        string failureMessage = string.IsNullOrWhiteSpace(prepareResult?.Message)
            ? "RimChat_Unknown".Translate().ToString()
            : prepareResult.Message;
        outcome = ActionExecutionOutcome.Failure(action, failureMessage);
        return true;
    }

    if (prepareResult.Data is ItemAirdropAsyncQueuedData)
    {
        outcome = ActionExecutionOutcome.Success(
            action,
            BuildAirdropSelectionInProgressSystemText(),
            prepareResult.Data);
        return true;
    }

    if (prepareResult.Data is ItemAirdropPendingSelectionData pendingSelection)
    {
        lease.Dispose();
        DiplomacyAirdropConfirmationUi.TransitionAirdropExecutionStage(currentSession, AirdropExecutionStage.SelectingCandidate, pendingSelection.FailureCode ?? "selection_pending");
        if (DeterminePendingSelectionResolution(pendingSelection) == AirdropPendingResolution.AutoPickTop1 &&
            TryAutoPickPendingAirdropSelection(actionSnapshot, pendingSelection, currentSession, currentFaction, out outcome))
        {
            return true;
        }

        CacheAirdropPendingSelectionIntent(currentSession, actionSnapshot, pendingSelection);
        outcome = ActionExecutionOutcome.Failure(
            action,
            BuildAirdropPendingSelectionSystemText(pendingSelection));
        return true;
    }

    if (!(prepareResult.Data is ItemAirdropPreparedTradeData preparedTrade))
    {
        lease.Dispose();
        DiplomacyAirdropConfirmationUi.ResetAirdropConfirmationRuntime(currentSession, "prepared_trade_missing", true);
        DiplomacyAirdropConfirmationUi.TransitionAirdropExecutionStage(currentSession, AirdropExecutionStage.Failed, "prepared_trade_missing");
        outcome = ActionExecutionOutcome.Failure(action, "RimChat_Unknown".Translate().ToString());
        return true;
    }

    lease.Dispose();
    DiplomacyAirdropConfirmationUi.ResetAirdropConfirmationRuntime(currentSession, "prepared_trade_ready", true, true);
    DiplomacyAirdropConfirmationUi.TransitionAirdropExecutionStage(currentSession, AirdropExecutionStage.PreparedAwaitingConfirm, preparedTrade.SelectedDefName ?? "prepared_trade");
    currentSession.airdropPreparedAwaitingConfirmTick = Find.TickManager?.TicksGame ?? 0;
    List<PendingAirdropSelectionCandidate> pendingCandidates = null;
    Dictionary<string, object> baseParameters = DiplomacyActionPolicyService.CloneParameters(actionSnapshot.Parameters);
    if (!DiplomacyAirdropPendingPolicy.TryReadPendingAirdropCandidates(baseParameters, out pendingCandidates))
    {
        pendingCandidates = new List<PendingAirdropSelectionCandidate>();
    }

    ModuleLog.Message(
        $"[RimAI.Relations] AirdropConfirmOpen: def={preparedTrade.SelectedDefName},count={preparedTrade.Quantity},requested={preparedTrade.RequestedQuantity},hardMax={preparedTrade.HardMax},adjustment={preparedTrade.CountAdjustmentReason},payment={preparedTrade.PaymentTotalSilver},candidateCount={pendingCandidates.Count}");
    Owner.Parts.AirdropConfirmUi.ShowAirdropTradeConfirmationDialog(currentSession, currentFaction, preparedTrade, baseParameters, pendingCandidates);
    outcome = ActionExecutionOutcome.Success(
        action,
        "RimChat_ItemAirdropAwaitingConfirmSystem".Translate().ToString(),
        preparedTrade);
    return true;
}



internal static string BuildAirdropSelectionInProgressSystemText()
{
    return "RimChat_ItemAirdropSelectionInProgressSystem".Translate().ToString();
}



internal static void TryInjectPendingAirdropCountFromLatestPlayerMessage(AIAction actionSnapshot, FactionDialogueSession currentSession)
{
    if (actionSnapshot == null)
    {
        return;
    }

    if (actionSnapshot.Parameters == null)
    {
        actionSnapshot.Parameters = new Dictionary<string, object>(StringComparer.Ordinal);
    }

    if (HasAirdropExplicitCountParameter(actionSnapshot.Parameters))
    {
        return;
    }

    int pendingCardCount = Math.Max(0, currentSession?.pendingAirdropTradeCardRequestedCount ?? 0);
    if (pendingCardCount > 0)
    {
        actionSnapshot.Parameters["count"] = pendingCardCount;
        ModuleLog.Message($"[RimAI.Relations] Injected pending airdrop count from session trade-card reference: count={pendingCardCount}");
        return;
    }

    string latestPlayerText = currentSession?.messages?
        .LastOrDefault(message => message != null && message.isPlayer && !message.IsSystemMessage())?
        .message ?? string.Empty;
    if (!DiplomacyAirdropPendingPolicy.TryExtractAirdropRequestedCount(latestPlayerText, out int requestedCount))
    {
        return;
    }

    actionSnapshot.Parameters["count"] = requestedCount;
    ModuleLog.Message($"[RimAI.Relations] Injected pending airdrop count from latest player message: count={requestedCount}");
}



internal static bool HasAirdropExplicitCountParameter(Dictionary<string, object> parameters)
{
    if (parameters == null)
    {
        return false;
    }

    return parameters.ContainsKey("count") || parameters.ContainsKey("quantity");
}



internal static bool IsTimeoutPendingSelection(ItemAirdropPendingSelectionData pendingSelection)
{
    string code = pendingSelection?.FailureCode ?? string.Empty;
    return code.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0;
}



internal static bool IsManualChoicePending(ItemAirdropPendingSelectionData pendingSelection)
{
    string code = pendingSelection?.FailureCode ?? string.Empty;
    return code.IndexOf("selection_manual_choice", StringComparison.OrdinalIgnoreCase) >= 0;
}



internal static AirdropPendingResolution DeterminePendingSelectionResolution(ItemAirdropPendingSelectionData pendingSelection)
{
    if (IsTimeoutPendingSelection(pendingSelection) || IsManualChoicePending(pendingSelection))
    {
        return AirdropPendingResolution.AutoPickTop1;
    }

    return AirdropPendingResolution.ShowFailureMessage;
}



internal bool TryAutoPickPendingAirdropSelection(
    AIAction action,
    ItemAirdropPendingSelectionData pendingSelection,
    FactionDialogueSession currentSession,
    Faction currentFaction,
    out ActionExecutionOutcome outcome)
{
    outcome = null;
    if (pendingSelection?.Options == null || pendingSelection.Options.Count == 0)
    {
        return false;
    }

    Dictionary<string, object> autoParameters = DiplomacyActionPolicyService.CloneParameters(action?.Parameters);
    autoParameters[AirdropPendingFailureCodeKey] = pendingSelection.FailureCode ?? "selection_manual_choice";
    autoParameters[AirdropPendingCandidatesKey] = pendingSelection.Options
        .OrderBy(option => option.Index)
        .Take(5)
        .Select(option => new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["index"] = option.Index,
            ["defName"] = option.DefName ?? string.Empty,
            ["label"] = option.Label ?? option.DefName ?? string.Empty,
            ["unitPrice"] = option.UnitPrice,
            ["max_legal_count"] = option.MaxLegalCount
        })
        .Cast<object>()
        .ToList();

    ItemAirdropPendingSelectionOption topOption = pendingSelection.Options
        .OrderBy(option => option.Index)
        .FirstOrDefault();
    if (topOption == null || string.IsNullOrWhiteSpace(topOption.DefName))
    {
        return false;
    }

    autoParameters["selected_def"] = topOption.DefName;
    var autoAction = new AIAction
    {
        ActionType = AIActionNames.RequestItemAirdrop,
        Parameters = autoParameters,
        Reason = "selection_timeout_autopick_top1"
    };
    return TryHandleAirdropActionWithConfirmation(autoAction, currentSession, currentFaction, out outcome);
}



internal static void CacheAirdropPendingSelectionIntent(
    FactionDialogueSession currentSession,
    AIAction action,
    ItemAirdropPendingSelectionData pendingSelection)
{
    if (currentSession == null || action == null || pendingSelection?.Options == null || pendingSelection.Options.Count == 0)
    {
        return;
    }

    Dictionary<string, object> parameters = DiplomacyActionPolicyService.CloneParameters(action.Parameters);
    parameters.Remove("selected_def");
    parameters[AirdropPendingFailureCodeKey] = pendingSelection.FailureCode ?? "selection_timeout";
    parameters[AirdropPendingCandidatesKey] = pendingSelection.Options
        .Select(option => new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["index"] = option.Index,
            ["defName"] = option.DefName ?? string.Empty,
            ["label"] = option.Label ?? option.DefName ?? string.Empty,
            ["unitPrice"] = option.UnitPrice,
            ["max_legal_count"] = option.MaxLegalCount
        })
        .Cast<object>()
        .ToList();

    var pendingAction = new AIAction
    {
        ActionType = AIActionNames.RequestItemAirdrop,
        Parameters = parameters,
        Reason = "selection_timeout_pending_confirmation"
    };

    int assistantRound = DiplomacyActionPolicyService.GetAssistantDialogueRound(currentSession) + 1;
    PendingDelayedActionIntent intent = DiplomacyActionPolicyService.CreatePendingDelayedIntent(
        pendingAction,
        assistantRound,
        true,
        "selected_def");
    if (intent == null)
    {
        return;
    }

    currentSession.pendingDelayedActionIntent = intent;
    currentSession.lastDelayedActionIntent = intent.Clone();
    ModuleLog.Message($"[RimAI.Relations] CacheAirdropPendingSelectionIntent: cached pendingDelayedActionIntent for RequestItemAirdrop, failureCode={pendingSelection.FailureCode}, optionsCount={pendingSelection.Options.Count}");
}



internal static string BuildPendingSelectionCandidateLine(PendingAirdropSelectionCandidate candidate)
{
    if (candidate == null)
    {
        return string.Empty;
    }

    return "RimChat_ItemAirdropSelectionPendingLine".Translate(
        candidate.Index,
        candidate.Label ?? candidate.DefName ?? "RimChat_Unknown".Translate().ToString(),
        candidate.DefName ?? "RimChat_Unknown".Translate().ToString(),
        candidate.UnitPrice.ToString("F1", CultureInfo.InvariantCulture),
        Math.Max(0, candidate.MaxLegalCount)).ToString();
}



internal static string BuildAirdropPendingSelectionSystemText(ItemAirdropPendingSelectionData pendingSelection)
{
    if (pendingSelection?.Options == null || pendingSelection.Options.Count == 0)
    {
        if (string.Equals(pendingSelection?.FailureCode, "need_relevance_insufficient", StringComparison.Ordinal))
        {
            return "RimChat_ItemAirdropNeedClarifySystem".Translate().ToString();
        }

        return "RimChat_ItemAirdropAwaitingConfirmSystem".Translate().ToString();
    }

    string lines = string.Join(
        "\n",
        pendingSelection.Options
            .OrderBy(option => option.Index)
            .Select(option => "RimChat_ItemAirdropSelectionPendingLine".Translate(
                option.Index,
                option.Label ?? option.DefName ?? "RimChat_Unknown".Translate().ToString(),
                option.DefName ?? "RimChat_Unknown".Translate().ToString(),
                option.UnitPrice.ToString("F1", CultureInfo.InvariantCulture),
                option.MaxLegalCount).ToString()));
    return "RimChat_ItemAirdropSelectionPendingSystem".Translate(lines).ToString();
}
}

