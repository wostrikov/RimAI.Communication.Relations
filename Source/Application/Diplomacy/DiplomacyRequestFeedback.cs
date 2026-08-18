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

internal sealed class DiplomacyRequestFeedback : DiplomacyDialogueCollaborator
{
    internal DiplomacyRequestFeedback(Dialog_DiplomacyDialogue owner) : base(owner) { }


internal bool TryGetVisibleAiRequestStatus(out AIRequestResult status)
{
    status = null;
    string requestId = session?.pendingRequestId;
    if (!string.IsNullOrWhiteSpace(requestId))
    {
        status = AIChatServiceAsync.Instance.GetRequestStatus(requestId);
        if (status != null)
        {
            return true;
        }
    }

    if (session != null && !string.IsNullOrWhiteSpace(session.pendingAirdropRequestId))
    {
        status = AIChatServiceAsync.Instance.GetRequestStatus(session.pendingAirdropRequestId);
        if (status != null)
        {
            return true;
        }
    }

    if (string.IsNullOrWhiteSpace(Owner.Parts.StrategyUi.strategySuggestionRequestId))
    {
        return false;
    }

    status = AIChatServiceAsync.Instance.GetRequestStatus(Owner.Parts.StrategyUi.strategySuggestionRequestId);
    return status != null;
}



internal static bool IsQueuedRequestState(AIRequestResult status)
{
    return status != null &&
           (status.State == AIRequestState.Pending || status.State == AIRequestState.Queued);
}



internal static int GetQueuedRequestsAhead(AIRequestResult status)
{
    return Math.Max(0, (status?.QueuePosition ?? 0) - 1);
}



internal string BuildAiTurnStatusText()
{
    if (TryGetVisibleAiRequestStatus(out AIRequestResult status) && IsQueuedRequestState(status))
    {
        int requestsAhead = GetQueuedRequestsAhead(status);
        if (requestsAhead == 0)
        {
            return "RimChat_DiplomacyRequestQueuedHead".Translate().ToString();
        }
        return "RimChat_DiplomacyRequestQueued".Translate(requestsAhead).ToString();
    }

    return "RimChat_DiplomacyInputLockedByTyping".Translate().ToString();
}



internal void ShowDialogueRequestError(string error)
{
    string resolved = string.IsNullOrWhiteSpace(error)
        ? "RimChat_DialogueRequestUnavailable".Translate().ToString()
        : error;

    if (session != null)
    {
        conversationController.CancelPendingRequest(session);
        session.aiError = resolved;
        session.isWaitingForResponse = false;
    }

    Messages.Message(resolved, MessageTypeDefOf.RejectInput, false);
}



internal void HandleDroppedRequest(string primaryReason, string secondaryReason = null)
{
    string reason = !string.IsNullOrWhiteSpace(primaryReason) ? primaryReason : secondaryReason;
    if (DialogueDropPolicy.ShouldSuppressUserFacingDrop(reason))
    {
        Log.Message($"[RimAI.Relations] Suppressed user-facing dropped diplomacy callback: reason={reason ?? "unknown"}");
        return;
    }

    string resolved = BuildDroppedRequestMessage(reason);
    LogDroppedRequestState(reason);
    ShowDialogueRequestError(resolved);
    session?.AddMessage("System", resolved, false, DialogueMessageType.System);
}



internal string BuildDroppedRequestMessage(string reason)
{
    string baseMessage = "RimChat_DialogueRequestUnavailable".Translate().ToString();
    if (string.IsNullOrWhiteSpace(reason))
    {
        return baseMessage;
    }

    return $"{baseMessage} [{reason.Trim()}]";
}



internal void LogDroppedRequestState(string reason)
{
    Log.Warning(
        $"[RimAI.Relations] Diplomacy request dropped. " +
        $"reason={reason ?? "unknown"}, faction={faction?.Name ?? "null"}, negotiator={negotiator?.ThingID ?? "null"}, " +
        $"pendingRequestId={session?.pendingRequestId ?? "null"}, waiting={session?.isWaitingForResponse ?? false}, " +
        $"hasLease={session?.pendingRequestLease != null}, queuedTick={session?.lastDiplomacyRequestQueuedTick ?? int.MinValue}, " +
        $"queuedRealtime={session?.lastDiplomacyRequestQueuedRealtime ?? -1f}, window={windowInstanceId}");
}



internal void HandleSessionRequestError(FactionDialogueSession targetSession, string error)
{
    if (targetSession == null) return;

    string resolved = string.IsNullOrWhiteSpace(error)
        ? "RimChat_DialogueRequestUnavailable".Translate().ToString()
        : error;

    conversationController.CancelPendingRequest(targetSession);
    targetSession.aiError = resolved;
    targetSession.isWaitingForResponse = false;

    if (ReferenceEquals(session, targetSession))
    {
        Messages.Message(resolved, MessageTypeDefOf.RejectInput, false);
    }
}



internal void HandleSessionDroppedRequest(
    FactionDialogueSession targetSession,
    Faction targetFaction,
    string primaryReason,
    string secondaryReason = null)
{
    string reason = !string.IsNullOrWhiteSpace(primaryReason) ? primaryReason : secondaryReason;
    if (DialogueDropPolicy.ShouldSuppressUserFacingDrop(reason))
    {
        Log.Message($"[RimAI.Relations] Suppressed user-facing dropped diplomacy callback: reason={reason ?? "unknown"}");
        return;
    }

    string resolved = BuildDroppedRequestMessage(reason);
    Log.Warning(
        $"[RimAI.Relations] Diplomacy request dropped (background). " +
        $"reason={reason ?? "unknown"}, faction={targetFaction?.Name ?? "null"}, " +
        $"pendingRequestId={targetSession?.pendingRequestId ?? "null"}, waiting={targetSession?.isWaitingForResponse ?? false}, " +
        $"hasLease={targetSession?.pendingRequestLease != null}, window={windowInstanceId}");
    HandleSessionRequestError(targetSession, resolved);
    targetSession?.AddMessage("System", resolved, false, DialogueMessageType.System);
}



internal void CancelAllBackgroundDialogueRequests()
{
    var allSessions = GameComponent_DiplomacyManager.Instance?.GetAllDialogueSessions();
    if (allSessions == null) return;

    foreach (var s in allSessions)
    {
        if (s != null && !string.IsNullOrEmpty(s.pendingRequestId))
        {
            conversationController.CancelPendingRequest(s);
        }
    }
}
}
