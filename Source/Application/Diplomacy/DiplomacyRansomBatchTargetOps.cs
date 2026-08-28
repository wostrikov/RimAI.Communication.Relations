using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Communication.Relations.Memory;
using Verse;
using Ustas.RimAI.Communication.Relations.Diagnostics;

namespace Ustas.RimAI.Communication.Relations.UI
{
    /// <summary>
    /// Ransom batch target binding, eligibility, and auto-reply timeout helpers.
    /// </summary>
    internal static class DiplomacyRansomBatchTargetOps
    {
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
    ModuleLog.Message("[RimAI.Relations] pay_prisoner_ransom succeeded. Cleared request_info(prisoner) state.");
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
           ContainsTimeoutToken(text, "черга");
}



internal static bool IsNetworkTimeoutText(string text)
{
    return ContainsTimeoutToken(text, "curl error 28") ||
           ContainsTimeoutToken(text, "request timeout") ||
           ContainsTimeoutToken(text, "timed out") ||
           ContainsTimeoutToken(text, "timeout") ||
           ContainsTimeoutToken(text, "таймаут");
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
}
