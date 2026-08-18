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

namespace Ustas.RimAI.Communication.Relations.UI
{
    /// <summary>
    /// Delayed-intent capture and mapping helpers for diplomacy actions.
    /// </summary>
    internal static class DiplomacyActionPolicyCaptureOps
    {
internal static void CaptureDelayedIntentFromParsedActions(
    List<AIAction> actions,
    FactionDialogueSession currentSession,
    int assistantRound)
{
    if (currentSession == null || actions == null)
    {
        return;
    }

    AIAction latestDelayed = actions.LastOrDefault(action => DiplomacyActionPolicyQueryOps.IsDelayedActionType(action?.ActionType));
    if (latestDelayed == null)
    {
        return;
    }

    var intent = CreatePendingDelayedIntent(latestDelayed, assistantRound, false, string.Empty);
    if (intent != null)
    {
        currentSession.lastDelayedActionIntent = intent;
    }
}



internal static void RemoveDelayedActionsWithMissingRequiredParameters(
    ParsedResponse response,
    FactionDialogueSession currentSession,
    int assistantRound)
{
    if (response?.Actions == null || response.Actions.Count == 0)
    {
        return;
    }

    var filtered = new List<AIAction>();
    string firstClarification = string.Empty;

    foreach (AIAction action in response.Actions)
    {
        if (action == null || !DiplomacyActionPolicyQueryOps.IsDelayedActionType(action.ActionType))
        {
            filtered.Add(action);
            continue;
        }

        string missingParameter = DiplomacyActionPolicyParameterOps.GetMissingRequiredParameter(action.ActionType, action.Parameters);
        if (string.IsNullOrWhiteSpace(missingParameter))
        {
            filtered.Add(action);
            continue;
        }

        if (currentSession != null)
        {
            var pending = CreatePendingDelayedIntent(action, assistantRound, true, missingParameter);
            if (pending != null)
            {
                currentSession.pendingDelayedActionIntent = pending;
                currentSession.lastDelayedActionIntent = pending.Clone();
            }
        }

        if (string.IsNullOrWhiteSpace(firstClarification))
        {
            firstClarification = DiplomacyActionClarificationService.BuildMissingParameterClarification(action.ActionType, missingParameter, action.Parameters);
        }
    }

    response.Actions = filtered;
    if (!string.IsNullOrWhiteSpace(firstClarification))
    {
        response.DialogueText = firstClarification;
    }
}



internal static void TryMapDelayedIntentFromPlayerFollowup(
    ParsedResponse response,
    FactionDialogueSession currentSession,
    string playerMessage,
    int assistantRound)
{
    if (response == null || currentSession == null)
    {
        return;
    }

    string normalizedPlayer = (playerMessage ?? string.Empty).Trim().ToLowerInvariant();
    if (string.IsNullOrWhiteSpace(normalizedPlayer))
    {
        return;
    }

    if (DiplomacyActionPolicyText.ContainsAnyHint(normalizedPlayer, DiplomacyActionPolicyText.CancellationHints))
    {
        currentSession.pendingDelayedActionIntent = null;
        if (currentSession.hasPendingAirdropTradeCardReference)
        {
            currentSession.ClearPendingAirdropTradeCardReference();
        }

        if (string.IsNullOrWhiteSpace(response.DialogueText))
        {
            response.DialogueText = "好，这次请求先取消。";
        }
        return;
    }

    PendingDelayedActionIntent baseIntent = currentSession.pendingDelayedActionIntent ?? currentSession.lastDelayedActionIntent;
    if (baseIntent == null)
    {
        return;
    }

    if (DiplomacyAirdropPendingPolicy.TryMapAirdropPendingSelectionFollowup(response, currentSession, baseIntent, playerMessage, assistantRound))
    {
        return;
    }

    if (DiplomacyActionClarificationService.TryMapAirdropAmountShorthandFollowup(response, currentSession, baseIntent, playerMessage, assistantRound))
    {
        return;
    }

    if (DiplomacyActionPolicyText.ContainsAnyHint(normalizedPlayer, DiplomacyActionPolicyText.ConfirmationHints))
    {
        TryMapConfirmedIntentToAction(response, currentSession, baseIntent, assistantRound);
        return;
    }

    if (!DiplomacyActionPolicyText.ContainsAnyHint(normalizedPlayer, DiplomacyActionPolicyText.AmbiguousFollowupHints))
    {
        return;
    }

    string missingParameter = DiplomacyActionPolicyParameterOps.GetMissingRequiredParameter(baseIntent.ActionType, baseIntent.Parameters);
    if (!string.IsNullOrWhiteSpace(missingParameter))
    {
        PendingDelayedActionIntent missingIntent = baseIntent.Clone();
        missingIntent.RequiredParameter = missingParameter;
        missingIntent.AwaitingConfirmation = true;
        missingIntent.UpdatedAssistantRound = assistantRound;
        currentSession.pendingDelayedActionIntent = missingIntent;
        if (string.IsNullOrWhiteSpace(response.DialogueText))
        {
            response.DialogueText = DiplomacyActionClarificationService.BuildMissingParameterClarification(
                missingIntent.ActionType,
                missingParameter,
                missingIntent.Parameters);
        }
        return;
    }

    PendingDelayedActionIntent confirmIntent = baseIntent.Clone();
    confirmIntent.AwaitingConfirmation = true;
    confirmIntent.RequiredParameter = string.Empty;
    confirmIntent.UpdatedAssistantRound = assistantRound;
    currentSession.pendingDelayedActionIntent = confirmIntent;
    if (string.IsNullOrWhiteSpace(response.DialogueText))
    {
        response.DialogueText = DiplomacyActionClarificationService.BuildResendConfirmationQuestion(confirmIntent);
    }
}



internal static void TryMapConfirmedIntentToAction(
    ParsedResponse response,
    FactionDialogueSession currentSession,
    PendingDelayedActionIntent baseIntent,
    int assistantRound)
{
    if (response == null || currentSession == null || baseIntent == null)
    {
        return;
    }

    string missingParameter = DiplomacyActionPolicyParameterOps.GetMissingRequiredParameter(baseIntent.ActionType, baseIntent.Parameters);
    if (!string.IsNullOrWhiteSpace(missingParameter))
    {
        PendingDelayedActionIntent missingIntent = baseIntent.Clone();
        missingIntent.RequiredParameter = missingParameter;
        missingIntent.AwaitingConfirmation = true;
        missingIntent.UpdatedAssistantRound = assistantRound;
        currentSession.pendingDelayedActionIntent = missingIntent;
        if (string.IsNullOrWhiteSpace(response.DialogueText))
        {
            response.DialogueText = DiplomacyActionClarificationService.BuildMissingParameterClarification(
                missingIntent.ActionType,
                missingParameter,
                missingIntent.Parameters);
        }
        return;
    }

    string signature = DiplomacyActionPolicyParameterOps.BuildActionSignature(baseIntent.ActionType, baseIntent.Parameters);
    if (IsWithinDelayedDedupeWindow(currentSession, signature, assistantRound))
    {
        if (string.IsNullOrWhiteSpace(response.DialogueText))
        {
            response.DialogueText = DiplomacyActionClarificationService.BuildDedupeClarification(baseIntent);
        }
        return;
    }

    if (response.Actions == null)
    {
        response.Actions = new List<AIAction>();
    }
    response.Actions.Add(new AIAction
    {
        ActionType = baseIntent.ActionType,
        Parameters = DiplomacyActionPolicyParameterOps.CloneParameters(baseIntent.Parameters),
        Reason = "intent_map_confirmation"
    });

    if (string.IsNullOrWhiteSpace(response.DialogueText))
    {
        response.DialogueText = DiplomacyActionClarificationService.BuildConfirmationAcceptedLine(baseIntent);
    }

    currentSession.pendingDelayedActionIntent = null;
}



internal static void RemoveDelayedActionsBlockedByShortDedupe(
    ParsedResponse response,
    FactionDialogueSession currentSession,
    int assistantRound)
{
    if (response?.Actions == null || response.Actions.Count == 0 || currentSession == null)
    {
        return;
    }

    var filtered = new List<AIAction>();
    AIAction blockedAction = null;

    foreach (AIAction action in response.Actions)
    {
        if (action == null || !DiplomacyActionPolicyQueryOps.IsDelayedActionType(action.ActionType))
        {
            filtered.Add(action);
            continue;
        }

        string signature = DiplomacyActionPolicyParameterOps.BuildActionSignature(action.ActionType, action.Parameters);
        if (IsWithinDelayedDedupeWindow(currentSession, signature, assistantRound))
        {
            if (blockedAction == null)
            {
                blockedAction = action;
            }
            continue;
        }

        filtered.Add(action);
    }

    response.Actions = filtered;
    if (blockedAction != null && string.IsNullOrWhiteSpace(response.DialogueText))
    {
        var blockedIntent = CreatePendingDelayedIntent(blockedAction, assistantRound, false, string.Empty);
        response.DialogueText = DiplomacyActionClarificationService.BuildDedupeClarification(blockedIntent);
    }
}



internal static bool IsWithinDelayedDedupeWindow(
    FactionDialogueSession currentSession,
    string signature,
    int currentAssistantRound)
{
    if (currentSession == null || string.IsNullOrWhiteSpace(signature))
    {
        return false;
    }

    if (!string.Equals(
            currentSession.lastDelayedActionExecutionSignature ?? string.Empty,
            signature,
            StringComparison.Ordinal))
    {
        return false;
    }

    int roundDelta = currentAssistantRound - currentSession.lastDelayedActionExecutionAssistantRound;
    return roundDelta >= 0 && roundDelta < DiplomacyActionPolicyService.DelayedActionDedupeAssistantTurns;
}



internal static PendingDelayedActionIntent CreatePendingDelayedIntent(
    AIAction action,
    int assistantRound,
    bool awaitingConfirmation,
    string requiredParameter)
{
    if (action == null || string.IsNullOrWhiteSpace(action.ActionType))
    {
        return null;
    }

    var intent = new PendingDelayedActionIntent
    {
        ActionType = action.ActionType,
        Parameters = DiplomacyActionPolicyParameterOps.CloneParameters(action.Parameters),
        Signature = DiplomacyActionPolicyParameterOps.BuildActionSignature(action.ActionType, action.Parameters),
        RequiredParameter = requiredParameter ?? string.Empty,
        AwaitingConfirmation = awaitingConfirmation,
        CreatedAssistantRound = assistantRound,
        UpdatedAssistantRound = assistantRound
    };
    return intent;
}
    }
}
