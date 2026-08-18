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

internal sealed class DiplomacyAirdropAsyncWorkflow : DiplomacyDialogueCollaborator
{
    internal DiplomacyAirdropAsyncWorkflow(Dialog_DiplomacyDialogue owner) : base(owner) { }


internal static bool IsAirdropAsyncRequestPending(FactionDialogueSession currentSession)
{
    return currentSession != null &&
           currentSession.isWaitingForAirdropSelection &&
           !string.IsNullOrWhiteSpace(currentSession.pendingAirdropRequestId);
}



internal static void BindAirdropAsyncRequest(
    FactionDialogueSession currentSession,
    DialogueRequestLease lease,
    string requestId,
    int timeoutSeconds)
{
    if (currentSession == null || lease == null || string.IsNullOrWhiteSpace(requestId))
    {
        return;
    }

    lease.BindRequestId(requestId);
    currentSession.airdropRequestGeneration++;
    int requestGeneration = currentSession.airdropRequestGeneration;
    currentSession.pendingAirdropRequestId = requestId;
    currentSession.pendingAirdropRequestLease = lease;
    currentSession.isWaitingForAirdropSelection = true;
    currentSession.pendingAirdropRequestStartedRealtime = Time.realtimeSinceStartup;
    currentSession.pendingAirdropRequestTimeoutSeconds = Mathf.Max(0, timeoutSeconds);
    DiplomacyAirdropConfirmationUi.TransitionAirdropExecutionStage(currentSession, AirdropExecutionStage.SelectingCandidate, $"requestId={requestId},timeout={timeoutSeconds},generation={requestGeneration}");
}



internal static void ClearAirdropAsyncRequestState(FactionDialogueSession currentSession, bool disposeLease)
{
    if (currentSession == null)
    {
        return;
    }

    if (disposeLease)
    {
        currentSession.pendingAirdropRequestLease?.Dispose();
    }

    currentSession.pendingAirdropRequestId = null;
    currentSession.pendingAirdropRequestLease = null;
    currentSession.isWaitingForAirdropSelection = false;
    currentSession.pendingAirdropRequestStartedRealtime = -1f;
    currentSession.pendingAirdropRequestTimeoutSeconds = 0;
}



internal void HandleAirdropAsyncPrepareCompleted(
    FactionDialogueSession currentSession,
    Faction currentFaction,
    DialogueRequestLease lease,
    DialogueRuntimeContext requestContext,
    AIAction sourceAction,
    int expectedGeneration,
    GameAIInterface.APIResult prepareResult)
{
    if (!IsAirdropAsyncContextValid(currentSession, currentFaction, lease, requestContext, expectedGeneration))
    {
        Log.Warning($"[RimAI.Relations] AirdropStalePendingBlocked: requestId={lease?.RequestId ?? "none"},stage={currentSession?.airdropExecutionStage.ToString() ?? "null"},expectedGeneration={expectedGeneration},actualGeneration={currentSession?.airdropRequestGeneration ?? -1},faction={currentFaction?.Name ?? "null"}");
        return;
    }

    ClearAirdropAsyncRequestState(currentSession, true);

    if (prepareResult == null)
    {
        DiplomacyAirdropConfirmationUi.ResetAirdropConfirmationRuntime(currentSession, "prepareResult=null", true, true);
        DiplomacyAirdropConfirmationUi.TransitionAirdropExecutionStage(currentSession, AirdropExecutionStage.Failed, "prepareResult=null");
        currentSession.AddMessage(
            "System",
            "RimChat_ItemAirdropCommitFailedSystem".Translate("RimChat_Unknown".Translate().ToString()),
            false,
            DialogueMessageType.System);
        Owner.Parts.Session.SaveFactionMemory(currentSession, currentFaction);
        return;
    }

    if (!prepareResult.Success)
    {
        string reason = string.IsNullOrWhiteSpace(prepareResult.Message)
            ? "RimChat_Unknown".Translate().ToString()
            : prepareResult.Message;
        DiplomacyAirdropConfirmationUi.ResetAirdropConfirmationRuntime(currentSession, "async_prepare_failed", true, true);
        DiplomacyAirdropConfirmationUi.TransitionAirdropExecutionStage(currentSession, AirdropExecutionStage.Failed, reason);
        currentSession.AddMessage(
            "System",
            "RimChat_ItemAirdropCommitFailedSystem".Translate(reason),
            false,
            DialogueMessageType.System);
        Owner.Parts.Session.SaveFactionMemory(currentSession, currentFaction);
        return;
    }

    if (prepareResult.Data is ItemAirdropPendingSelectionData pendingSelection)
    {
        DiplomacyAirdropConfirmationUi.TransitionAirdropExecutionStage(currentSession, AirdropExecutionStage.SelectingCandidate, pendingSelection.FailureCode ?? "selection_pending");
        if (DiplomacyAirdropWorkflow.DeterminePendingSelectionResolution(pendingSelection) == AirdropPendingResolution.AutoPickTop1 &&
            Owner.Parts.Airdrop.TryAutoPickPendingAirdropSelection(sourceAction, pendingSelection, currentSession, currentFaction, out _))
        {
            Owner.Parts.Session.SaveFactionMemory(currentSession, currentFaction);
        }
        else
        {
            DiplomacyAirdropWorkflow.CacheAirdropPendingSelectionIntent(currentSession, sourceAction, pendingSelection);
            currentSession.AddMessage(
                "System",
                "RimChat_ItemAirdropCommitFailedSystem".Translate(DiplomacyAirdropWorkflow.BuildAirdropPendingSelectionSystemText(pendingSelection)),
                false,
                DialogueMessageType.System);
            Owner.Parts.Session.SaveFactionMemory(currentSession, currentFaction);
        }
        return;
    }

    if (prepareResult.Data is ItemAirdropPreparedTradeData preparedTrade)
    {
        Owner.Parts.Airdrop.ClearPendingAirdropDialogState("async_prepare_new_confirmation", false);
        currentSession?.ClearPendingAirdropExecutionState();
        DiplomacyAirdropConfirmationUi.ResetAirdropConfirmationRuntime(currentSession, "async_prepared_trade_ready", true, true);
        DiplomacyAirdropConfirmationUi.TransitionAirdropExecutionStage(currentSession, AirdropExecutionStage.PreparedAwaitingConfirm, preparedTrade.SelectedDefName ?? "prepared_trade");
        Owner.Parts.AirdropConfirmUi.ShowAirdropTradeConfirmationDialog(currentSession, currentFaction, preparedTrade, null, null);
        Owner.Parts.Session.SaveFactionMemory(currentSession, currentFaction);
    }
}



internal static bool IsAirdropAsyncContextValid(
    FactionDialogueSession currentSession,
    Faction currentFaction,
    DialogueRequestLease lease,
    DialogueRuntimeContext requestContext,
    int expectedGeneration)
{
    if (currentSession == null || currentFaction == null || currentFaction.defeated || lease == null)
    {
        Log.Warning($"[RimAI.Relations] AirdropAsyncContextInvalid: reason=session_null_or_faction_defeated_or_lease_null sessionNull={currentSession == null} factionNull={currentFaction == null} factionDefeated={currentFaction?.defeated} leaseNull={lease == null}");
        return false;
    }

    if (expectedGeneration != currentSession.airdropRequestGeneration)
    {
        Log.Warning($"[RimAI.Relations] AirdropAsyncContextInvalid: reason=generation_mismatch expected={expectedGeneration} actual={currentSession.airdropRequestGeneration}");
        return false;
    }

    string requestId = lease.RequestId;
    if (string.IsNullOrWhiteSpace(requestId))
    {
        Log.Warning($"[RimAI.Relations] AirdropAsyncContextInvalid: reason=request_id_empty");
        return false;
    }

    if (!string.Equals(currentSession.pendingAirdropRequestId, requestId, StringComparison.Ordinal))
    {
        Log.Warning($"[RimAI.Relations] AirdropAsyncContextInvalid: reason=request_id_mismatch sessionId={currentSession.pendingAirdropRequestId} leaseId={requestId}");
        return false;
    }

    if (currentSession.pendingAirdropRequestLease == null ||
        !ReferenceEquals(currentSession.pendingAirdropRequestLease, lease))
    {
        Log.Warning($"[RimAI.Relations] AirdropAsyncContextInvalid: reason=lease_reference_mismatch sessionLeaseNull={currentSession.pendingAirdropRequestLease == null}");
        return false;
    }

    if (!lease.IsValidFor(requestId, requestContext?.DialogueSessionId ?? string.Empty, requestContext?.ContextVersion ?? -1))
    {
        Log.Warning($"[RimAI.Relations] AirdropAsyncContextInvalid: reason=lease_not_valid sessionId={requestContext?.DialogueSessionId} version={requestContext?.ContextVersion}");
        return false;
    }

    DialogueRuntimeContext resolveContext = requestContext?.WithCurrentRuntimeMarkers();
    if (!DialogueContextResolver.TryResolveLiveContext(resolveContext, out DialogueLiveContext liveContext, out _))
    {
        Log.Warning($"[RimAI.Relations] AirdropAsyncContextInvalid: reason=resolve_live_context_failed");
        return false;
    }

    if (!DialogueContextValidator.ValidateCallbackApply(requestContext, liveContext, requestContext?.DialogueSessionId, out _))
    {
        Log.Warning($"[RimAI.Relations] AirdropAsyncContextInvalid: reason=callback_validate_failed");
        return false;
    }

    FactionDialogueSession liveSession = GameComponent_DiplomacyManager.Instance?.GetSession(currentFaction);
    bool sessionMatch = ReferenceEquals(liveSession, currentSession);
    if (!sessionMatch)
        Log.Warning($"[RimAI.Relations] AirdropAsyncContextInvalid: reason=session_reference_mismatch");
    return sessionMatch;
}



internal void CancelPendingAirdropSelectionRequest()
{
    if (session == null || string.IsNullOrWhiteSpace(session.pendingAirdropRequestId))
    {
        return;
    }

    GameAIInterface.Instance.CancelItemAirdropAsyncRequest(
        session.pendingAirdropRequestId,
        "airdrop_selection_cancelled_by_window_close",
        "Airdrop selection request cancelled by dialogue close.");
    ClearAirdropAsyncRequestState(session, true);
}



internal bool TryGetPendingAirdropRequestStatus(out AIRequestResult status)
{
    status = null;
    if (session == null || string.IsNullOrWhiteSpace(session.pendingAirdropRequestId))
    {
        return false;
    }

    status = AIChatServiceAsync.Instance.GetRequestStatus(session.pendingAirdropRequestId);
    return status != null;
}



internal bool TryBuildAirdropAsyncStatusText(out string statusText)
{
    statusText = string.Empty;
    if (!IsAirdropAsyncRequestPending(session))
    {
        return false;
    }

    if (TryGetPendingAirdropRequestStatus(out AIRequestResult status) && DiplomacyRequestFeedback.IsQueuedRequestState(status))
    {
        int requestsAhead = DiplomacyRequestFeedback.GetQueuedRequestsAhead(status);
        if (requestsAhead == 0)
        {
            statusText = "RimChat_DiplomacyRequestQueuedHead".Translate().ToString();
        }
        else
        {
            statusText = "RimChat_DiplomacyRequestQueued".Translate(requestsAhead).ToString();
        }
        return true;
    }

    statusText = "RimChat_ItemAirdropSelectionInProgressBar".Translate().ToString();
    return true;
}


internal static bool TryInjectPendingAirdropTradeCardMetadata(
    List<AIAction> actions,
    FactionDialogueSession currentSession)
{
    if (actions == null || actions.Count == 0)
    {
        return true;
    }

    for (int i = 0; i < actions.Count; i++)
    {
        if (!TryInjectPendingAirdropTradeCardMetadata(actions[i], currentSession, out string _))
        {
            return false;
        }
    }

    return true;
}



internal static bool TryInjectPendingAirdropTradeCardMetadata(
    AIAction action,
    FactionDialogueSession currentSession,
    out string failureMessage)
{
    failureMessage = string.Empty;
    if (action == null ||
        !string.Equals(action.ActionType, AIActionNames.RequestItemAirdrop, StringComparison.Ordinal))
    {
        return true;
    }

    if (action.Parameters == null)
    {
        action.Parameters = new Dictionary<string, object>(StringComparer.Ordinal);
    }

    return TryInjectPendingAirdropTradeCardMetadata(action.Parameters, currentSession, out failureMessage);
}



internal static bool TryInjectPendingAirdropTradeCardMetadata(
    Dictionary<string, object> parameters,
    FactionDialogueSession currentSession,
    out string failureMessage)
{
    failureMessage = string.Empty;
    if (parameters == null ||
        currentSession == null ||
        !currentSession.hasPendingAirdropTradeCardReference)
    {
        return true;
    }

    if (string.IsNullOrWhiteSpace(currentSession.pendingAirdropTradeCardNeedDefName))
    {
        currentSession.ClearPendingAirdropTradeCardReference();
        failureMessage = BuildPendingAirdropTradeCardStateLostMessage();
        return false;
    }

    parameters[ItemAirdropParameterKeys.BoundNeedDefName] = currentSession.pendingAirdropTradeCardNeedDefName;
    parameters[ItemAirdropParameterKeys.BoundNeedLabel] = currentSession.pendingAirdropTradeCardNeedLabel ?? string.Empty;
    parameters[ItemAirdropParameterKeys.BoundNeedSearchText] = currentSession.pendingAirdropTradeCardNeedSearchText ?? string.Empty;
    parameters[ItemAirdropParameterKeys.BoundNeedSource] = "trade_card";
    return true;
}



internal static string BuildPendingAirdropTradeCardStateLostMessage()
{
    return "RimChat_ItemAirdropBoundNeedStateLostSystem".Translate().ToString();
}


internal static readonly Regex AirdropCounterofferPattern = new Regex(
    @"(?im)^(?:重报价|counteroffer)\s*:\s*item=(?<item>[A-Za-z0-9_\.]+)\s+count=(?<count>\d{1,5})\s+silver=(?<silver>\d{1,9})(?:\s+reason=(?<reason>.+))?\s*$",
    RegexOptions.CultureInvariant | RegexOptions.Compiled);


internal static readonly Regex AirdropCounterofferChineseNaturalPattern = new Regex(
    @"(?is)(?:(?:关于|这批|这一单|这批货|这笔单子|按现在的库存|按我们的库存)[^。!\n\r]{0,40})?(?:(?<item>[A-Za-z0-9_\.一-龥]+)\s*)?(?:最多|可以|可出|能给你|愿意给你|愿意提供|可以提供|我方最多给你|我们最多给你)?[^。!\n\r]{0,20}?(?<count>\d{1,5})\s*(?:个|份|组|箱|件|单位|x|×|把)?[^。!\n\r]{0,40}?(?:作价|报价|要价|开价|价码|价格|总价|换价|换取|折价|折银|需要你付|需要支付|你付|需付|收你|算你|收|仅收|只需|只要|一共|合计|总计|总共|抹零|折后|实付|应付|给你)[^0-9\n\r]{0,8}(?<silver>\d{1,9})\s*(?:银|银币|块)?",
    RegexOptions.CultureInvariant | RegexOptions.Compiled);


// Fallback: simple pricing without an explicit item count — e.g. "收你220银币".
internal static readonly Regex AirdropCounterofferChineseSimplePricePattern = new Regex(
    @"(?is)(?:收你|算你|收|仅收|只需|只要|一共|合计|总计|总共|抹零|折后|实付|应付|给你|作价|报价|要价)[^0-9\n\r]{0,8}(?<silver>\d{1,9})\s*(?:银|银币|块)?",
    RegexOptions.CultureInvariant | RegexOptions.Compiled);


internal static readonly Regex AirdropCounterofferEnglishNaturalPattern = new Regex(
    @"(?is)(?:(?:for this order|for this shipment|with our current stock|with current stock)[^.!?\n\r]{0,40})?(?:(?<item>[A-Za-z0-9_\.]+)\s*)?(?:we can offer|we can send|we can spare|we can provide|our counteroffer is|at most|up to)?[^.!?\n\r]{0,20}?(?<count>\d{1,5})\s*(?:units?|stacks?|items?|x)?[^.!?\n\r]{0,40}?(?:for|at|priced at|price is|costs?|asking|quoted at|in exchange for)[^0-9\n\r]{0,8}(?<silver>\d{1,9})\s*silver",
    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);


internal static readonly Regex AirdropCounterofferReasonPattern = new Regex(
    @"(?is)(?:原因|理由|因为|due to|because|since)\s*[:：,，]?\s*(?<reason>[^\r\n]+)",
    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);



internal static bool TryCaptureAndCacheAirdropCounteroffer(string dialogueText, FactionDialogueSession currentSession)
{
    if (currentSession == null || string.IsNullOrWhiteSpace(dialogueText))
    {
        return false;
    }

    if (!TryExtractAirdropCounteroffer(dialogueText, currentSession, out string item, out int count, out int silver, out string reason))
    {
        return false;
    }

    currentSession.CacheAirdropCounteroffer(item, count, silver, reason);
    return true;
}



internal static bool TryExtractAirdropCounteroffer(
    string dialogueText,
    FactionDialogueSession currentSession,
    out string item,
    out int count,
    out int silver,
    out string reason)
{
    item = string.Empty;
    count = 0;
    silver = 0;
    reason = string.Empty;

    Match legacyMatch = AirdropCounterofferPattern.Match(dialogueText);
    if (legacyMatch.Success &&
        TryReadCounterofferMatch(legacyMatch, out item, out count, out silver, out reason))
    {
        return true;
    }

    // Full pattern: captures both item count and silver.
    Match naturalMatch = AirdropCounterofferChineseNaturalPattern.Match(dialogueText);
    if (!naturalMatch.Success)
    {
        naturalMatch = AirdropCounterofferEnglishNaturalPattern.Match(dialogueText);
    }

    if (naturalMatch.Success)
    {
        item = ResolveCounterofferItemFallback(naturalMatch.Groups["item"].Value, currentSession);
        string countStr = naturalMatch.Groups["count"].Value;
        string silverStr = naturalMatch.Groups["silver"].Value;
        if (!string.IsNullOrWhiteSpace(item) &&
            int.TryParse(countStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out count) &&
            int.TryParse(silverStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out silver))
        {
            reason = ExtractCounterofferReason(dialogueText);
            return count > 0 && silver >= 0;
        }
    }

    // Simple-price fallback: e.g. "收你220银币" — captures only silver,
    // infer count from the session's pending trade card.
    Match simpleMatch = AirdropCounterofferChineseSimplePricePattern.Match(dialogueText);
    if (simpleMatch.Success &&
        int.TryParse(simpleMatch.Groups["silver"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out silver))
    {
        item = ResolveCounterofferItemFallback(string.Empty, currentSession);
        count = ResolveCounterofferCountFallback(currentSession);
        if (!string.IsNullOrWhiteSpace(item) && count > 0 && silver >= 0)
        {
            reason = ExtractCounterofferReason(dialogueText);
            return true;
        }
    }

    return false;
}



internal static bool TryReadCounterofferMatch(
    Match match,
    out string item,
    out int count,
    out int silver,
    out string reason)
{
    item = string.Empty;
    count = 0;
    silver = 0;
    reason = string.Empty;
    if (match == null || !match.Success)
    {
        return false;
    }

    item = match.Groups["item"].Value?.Trim() ?? string.Empty;
    if (string.IsNullOrWhiteSpace(item) ||
        !int.TryParse(match.Groups["count"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out count) ||
        !int.TryParse(match.Groups["silver"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out silver))
    {
        return false;
    }

    reason = match.Groups["reason"].Success
        ? (match.Groups["reason"].Value?.Trim() ?? string.Empty)
        : string.Empty;
    return count > 0 && silver >= 0;
}



internal static int ResolveCounterofferCountFallback(FactionDialogueSession currentSession)
{
    if (currentSession == null)
    {
        return 0;
    }

    if (currentSession.lastAirdropCounterofferCount > 0)
    {
        return currentSession.lastAirdropCounterofferCount;
    }

    if (currentSession.hasPendingAirdropTradeCardReference &&
        currentSession.pendingAirdropTradeCardRequestedCount > 0)
    {
        return currentSession.pendingAirdropTradeCardRequestedCount;
    }

    // Fall back to the most recent airdrop trade card message count.
    DialogueMessageData lastTradeCard = currentSession.messages?
        .LastOrDefault(m => m != null && m.IsAirdropTradeCard());
    return lastTradeCard?.airdropRequestedCount ?? 0;
}



internal static string ResolveCounterofferItemFallback(string rawItem, FactionDialogueSession currentSession)
{
    string item = rawItem?.Trim() ?? string.Empty;
    if (!string.IsNullOrWhiteSpace(item))
    {
        return item;
    }

    if (currentSession == null)
    {
        return string.Empty;
    }

    if (currentSession.hasPendingAirdropTradeCardReference &&
        !string.IsNullOrWhiteSpace(currentSession.pendingAirdropTradeCardNeedDefName))
    {
        return currentSession.pendingAirdropTradeCardNeedDefName.Trim();
    }

    return currentSession.messages?
        .Where(message => message != null && message.IsAirdropTradeCard())
        .Select(message => message.airdropNeedDefName?.Trim() ?? string.Empty)
        .LastOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}



internal static string ExtractCounterofferReason(string dialogueText)
{
    if (string.IsNullOrWhiteSpace(dialogueText))
    {
        return string.Empty;
    }

    Match reasonMatch = AirdropCounterofferReasonPattern.Match(dialogueText);
    if (!reasonMatch.Success)
    {
        return string.Empty;
    }

    return reasonMatch.Groups["reason"].Value?.Trim() ?? string.Empty;
}


internal bool TryHandlePendingAirdropSelectionBeforeAi(
    string playerMessage,
    FactionDialogueSession currentSession,
    Faction currentFaction)
{
    if (currentSession?.pendingDelayedActionIntent == null || currentFaction == null)
    {
        return false;
    }

    PendingDelayedActionIntent pendingIntent = currentSession.pendingDelayedActionIntent;
    if (!string.Equals(pendingIntent.ActionType, AIActionNames.RequestItemAirdrop, StringComparison.Ordinal))
    {
        return false;
    }

    if (!DiplomacyAirdropPendingPolicy.TryReadPendingAirdropCandidates(pendingIntent.Parameters, out List<PendingAirdropSelectionCandidate> candidates) ||
        candidates.Count == 0)
    {
        return false;
    }

    if (!DiplomacyAirdropPendingPolicy.TryResolvePendingAirdropCandidate(playerMessage, candidates, out PendingAirdropSelectionCandidate selected))
    {
        return false;
    }

    Dictionary<string, object> mappedParameters = DiplomacyActionPolicyService.CloneParameters(pendingIntent.Parameters);
    mappedParameters.Remove(DiplomacyAirdropWorkflow.AirdropPendingCandidatesKey);
    mappedParameters.Remove(DiplomacyAirdropWorkflow.AirdropPendingFailureCodeKey);
    mappedParameters["selected_def"] = selected.DefName;
    if (DiplomacyAirdropPendingPolicy.TryExtractAirdropRequestedCount(playerMessage, out int requestedCount))
    {
        mappedParameters["count"] = requestedCount;
    }

    currentSession.ClearPendingAirdropTradeCardReference();

    var mappedAction = new AIAction
    {
        ActionType = AIActionNames.RequestItemAirdrop,
        Parameters = mappedParameters,
        Reason = "intent_map_pending_selection_pre_send"
    };

    if (!Owner.Parts.Airdrop.TryHandleAirdropActionWithConfirmation(mappedAction, currentSession, currentFaction, out ActionExecutionOutcome outcome))
    {
        return false;
    }

    string countHint = mappedParameters.TryGetValue("count", out object countRaw) ? countRaw?.ToString() ?? "none" : "none";
    Log.Message($"[RimAI.Relations] Pre-send pending airdrop selection resolved locally: def={selected.DefName},index={selected.Index},label={selected.Label},countHint={countHint}");
    currentSession.AddMessage(
        "System",
        "RimChat_ItemAirdropSelectionChosen".Translate(selected.Label, selected.DefName).ToString(),
        false,
        DialogueMessageType.System);

    Owner.Parts.Policy.RecordDelayedActionRuntimeState(new List<ActionExecutionOutcome> { outcome }, currentSession);
    if (outcome != null && outcome.IsSuccess)
    {
        Owner.Parts.Outcomes.AppendAirdropSuccessSystemMessage(outcome, currentSession, currentFaction);
    }
    else
    {
        string reason = string.IsNullOrWhiteSpace(outcome?.Message)
            ? "RimChat_Unknown".Translate().ToString()
            : outcome.Message;
        currentSession.AddMessage(
            "System",
            "RimChat_ItemAirdropCommitFailedSystem".Translate(reason),
            false,
            DialogueMessageType.System);
    }

    Owner.Parts.Session.SaveFactionMemory(currentSession, currentFaction);
    return true;
}
}
