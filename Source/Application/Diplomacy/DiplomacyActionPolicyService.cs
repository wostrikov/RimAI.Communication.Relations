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

internal sealed class DiplomacyActionPolicyService : DiplomacyDialogueCollaborator
{
    internal DiplomacyActionPolicyService(Dialog_DiplomacyDialogue owner) : base(owner) { }


internal const int DelayedActionDedupeAssistantTurns = 2;



internal static readonly HashSet<string> DelayedActionTypes = new HashSet<string>(StringComparer.Ordinal)
{
    AIActionNames.RequestItemAirdrop,
    AIActionNames.RequestCaravan,
    AIActionNames.RequestVisitor,
    AIActionNames.RequestAid,
    AIActionNames.RequestRaid,
    AIActionNames.TriggerIncident,
    AIActionNames.CreateQuest
};




internal ParsedResponse ApplyDiplomacyIntentDrivenActionMapping(
    ParsedResponse parsedResponse,
    FactionDialogueSession currentSession,
    string playerMessage)
{
    ParsedResponse response = parsedResponse ?? CreateEmptyParsedResponse();
    if (response.Actions == null)
    {
        response.Actions = new List<AIAction>();
    }

    int assistantRound = GetAssistantDialogueRound(currentSession);

    bool hasPendingAirdropSelection = HasPendingAirdropSelection(currentSession);
    if (hasPendingAirdropSelection)
    {
        DiplomacyAirdropPendingPolicy.TryMapAirdropPendingSelectionFollowup(response, currentSession, currentSession.pendingDelayedActionIntent, playerMessage, assistantRound);
    }

    RemoveDelayedActionsWithMissingRequiredParameters(response, currentSession, assistantRound);

    if (!HasDelayedActions(response.Actions))
    {
        TryMapDelayedIntentFromPlayerFollowup(response, currentSession, playerMessage, assistantRound);
    }

    DiplomacyActionClarificationService.ApplyForcedSendInfoDirective(response, playerMessage);
    RemoveDelayedActionsBlockedByShortDedupe(response, currentSession, assistantRound);
    if (!DiplomacyAirdropAsyncWorkflow.TryInjectPendingAirdropTradeCardMetadata(response.Actions, currentSession))
    {
        response.Actions = response.Actions
            .Where(action => !DiplomacyAirdropWorkflow.IsRequestItemAirdropAction(action))
            .ToList();

        string failureMessage = DiplomacyAirdropAsyncWorkflow.BuildPendingAirdropTradeCardStateLostMessage();
        response.DialogueText = string.IsNullOrWhiteSpace(response.DialogueText)
            ? failureMessage
            : $"{response.DialogueText}\n\n{failureMessage}";
    }

    CaptureDelayedIntentFromParsedActions(response.Actions, currentSession, assistantRound);
    return response;
}



internal static bool HasPendingAirdropSelection(FactionDialogueSession currentSession)
{
    if (currentSession?.pendingDelayedActionIntent == null)
    {
        return false;
    }

    if (!string.Equals(currentSession.pendingDelayedActionIntent.ActionType, AIActionNames.RequestItemAirdrop, StringComparison.Ordinal))
    {
        return false;
    }

    return DiplomacyAirdropPendingPolicy.TryReadPendingAirdropCandidates(currentSession.pendingDelayedActionIntent.Parameters, out List<PendingAirdropSelectionCandidate> candidates) && candidates.Count > 0;
}



internal void RecordDelayedActionRuntimeState(
    List<ActionExecutionOutcome> actionOutcomes,
    FactionDialogueSession currentSession)
{
    if (currentSession == null || actionOutcomes == null || actionOutcomes.Count == 0)
    {
        return;
    }

    int assistantRoundAfterResponse = GetAssistantDialogueRound(currentSession) + 1;
    bool consumedPending = false;

    foreach (ActionExecutionOutcome outcome in actionOutcomes)
    {
        if (outcome?.Action == null || !outcome.IsSuccess || !IsDelayedActionType(outcome.Action.ActionType))
        {
            continue;
        }

        if (outcome.Data is ItemAirdropPreparedTradeData ||
            outcome.Data is ItemAirdropPendingSelectionData ||
            outcome.Data is ItemAirdropAsyncQueuedData)
        {
            // Airdrop trades queued for player confirmation are not executed yet.
            continue;
        }

        var executedIntent = CreatePendingDelayedIntent(outcome.Action, assistantRoundAfterResponse, false, string.Empty);
        if (executedIntent != null)
        {
            currentSession.lastDelayedActionIntent = executedIntent;
        }

        string signature = BuildActionSignature(outcome.Action.ActionType, outcome.Action.Parameters);
        if (!string.IsNullOrWhiteSpace(signature))
        {
            currentSession.lastDelayedActionExecutionSignature = signature;
            currentSession.lastDelayedActionExecutionAssistantRound = assistantRoundAfterResponse;
        }

        consumedPending = true;
    }

    if (consumedPending)
    {
        currentSession.pendingDelayedActionIntent = null;
    }
}



internal static ParsedResponse CreateEmptyParsedResponse()
{
    return new ParsedResponse
    {
        Success = true,
        DialogueText = string.Empty,
        Actions = new List<AIAction>(),
        StrategySuggestions = new List<StrategySuggestion>()
    };
}



internal static int GetAssistantDialogueRound(FactionDialogueSession currentSession)
{
    if (currentSession?.messages == null)
    {
        return 0;
    }

    return currentSession.messages.Count(msg =>
        msg != null &&
        !msg.isPlayer &&
        msg.messageType == DialogueMessageType.Normal);
}



internal static bool IsDelayedActionType(string actionType)
{
    return !string.IsNullOrWhiteSpace(actionType) && DelayedActionTypes.Contains(actionType);
}



internal static bool HasDelayedActions(List<AIAction> actions)
{
    return actions != null && actions.Any(action => IsDelayedActionType(action?.ActionType));
}



internal static void CaptureDelayedIntentFromParsedActions(
    List<AIAction> actions,
    FactionDialogueSession currentSession,
    int assistantRound)
{
    if (currentSession == null || actions == null)
    {
        return;
    }

    AIAction latestDelayed = actions.LastOrDefault(action => IsDelayedActionType(action?.ActionType));
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
        if (action == null || !IsDelayedActionType(action.ActionType))
        {
            filtered.Add(action);
            continue;
        }

        string missingParameter = GetMissingRequiredParameter(action.ActionType, action.Parameters);
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

    string missingParameter = GetMissingRequiredParameter(baseIntent.ActionType, baseIntent.Parameters);
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

    string missingParameter = GetMissingRequiredParameter(baseIntent.ActionType, baseIntent.Parameters);
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

    string signature = BuildActionSignature(baseIntent.ActionType, baseIntent.Parameters);
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
        Parameters = CloneParameters(baseIntent.Parameters),
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
        if (action == null || !IsDelayedActionType(action.ActionType))
        {
            filtered.Add(action);
            continue;
        }

        string signature = BuildActionSignature(action.ActionType, action.Parameters);
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
    return roundDelta >= 0 && roundDelta < DelayedActionDedupeAssistantTurns;
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
        Parameters = CloneParameters(action.Parameters),
        Signature = BuildActionSignature(action.ActionType, action.Parameters),
        RequiredParameter = requiredParameter ?? string.Empty,
        AwaitingConfirmation = awaitingConfirmation,
        CreatedAssistantRound = assistantRound,
        UpdatedAssistantRound = assistantRound
    };
    return intent;
}



internal static Dictionary<string, object> CloneParameters(Dictionary<string, object> source)
{
    var clone = new Dictionary<string, object>();
    if (source == null)
    {
        return clone;
    }

    foreach (KeyValuePair<string, object> entry in source)
    {
        clone[entry.Key] = entry.Value;
    }
    return clone;
}



internal static string BuildActionSignature(string actionType, Dictionary<string, object> parameters)
{
    if (string.IsNullOrWhiteSpace(actionType))
    {
        return string.Empty;
    }

    var sb = new StringBuilder();
    sb.Append(actionType.Trim().ToLowerInvariant());
    if (parameters == null || parameters.Count == 0)
    {
        return sb.ToString();
    }

    foreach (KeyValuePair<string, object> entry in parameters.OrderBy(item => item.Key, StringComparer.Ordinal))
    {
        sb.Append('|');
        sb.Append((entry.Key ?? string.Empty).Trim().ToLowerInvariant());
        sb.Append('=');
        sb.Append(NormalizeParameterValue(entry.Value));
    }

    return sb.ToString();
}



internal static string NormalizeParameterValue(object value)
{
    if (value == null)
    {
        return string.Empty;
    }

    if (value is float floatValue)
    {
        return floatValue.ToString(CultureInfo.InvariantCulture);
    }
    if (value is double doubleValue)
    {
        return doubleValue.ToString(CultureInfo.InvariantCulture);
    }
    if (value is decimal decimalValue)
    {
        return decimalValue.ToString(CultureInfo.InvariantCulture);
    }

    return value.ToString()?.Trim().ToLowerInvariant() ?? string.Empty;
}



internal static string GetMissingRequiredParameter(string actionType, Dictionary<string, object> parameters)
{
    switch (actionType)
    {
        case AIActionNames.RequestItemAirdrop:
            return HasNonEmptyParameter(parameters, "need") ? string.Empty : "need";
        case AIActionNames.RequestAid:
            return HasNonEmptyParameter(parameters, "type") ? string.Empty : "type";
        case AIActionNames.TriggerIncident:
            return HasNonEmptyParameter(parameters, "defName") ? string.Empty : "defName";
        case AIActionNames.CreateQuest:
            return HasNonEmptyParameter(parameters, "questDefName") ? string.Empty : "questDefName";
        default:
            return string.Empty;
    }
}



internal static bool HasNonEmptyParameter(Dictionary<string, object> parameters, string key)
{
    if (parameters == null || string.IsNullOrWhiteSpace(key))
    {
        return false;
    }

    if (!parameters.TryGetValue(key, out object value) || value == null)
    {
        return false;
    }

    return !string.IsNullOrWhiteSpace(value.ToString());
}
}
