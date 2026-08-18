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



        internal static bool HasPendingAirdropSelection(FactionDialogueSession currentSession) => DiplomacyActionPolicyQueryOps.HasPendingAirdropSelection(currentSession);
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



        internal static ParsedResponse CreateEmptyParsedResponse() => DiplomacyActionPolicyQueryOps.CreateEmptyParsedResponse();
        internal static int GetAssistantDialogueRound(FactionDialogueSession currentSession) => DiplomacyActionPolicyQueryOps.GetAssistantDialogueRound(currentSession);
        internal static bool IsDelayedActionType(string actionType) => DiplomacyActionPolicyQueryOps.IsDelayedActionType(actionType);
        internal static bool HasDelayedActions(List<AIAction> actions) => DiplomacyActionPolicyQueryOps.HasDelayedActions(actions);
        internal static void CaptureDelayedIntentFromParsedActions(List<AIAction> actions,
    FactionDialogueSession currentSession,
    int assistantRound) => DiplomacyActionPolicyCaptureOps.CaptureDelayedIntentFromParsedActions(actions, currentSession, assistantRound);
        internal static void RemoveDelayedActionsWithMissingRequiredParameters(ParsedResponse response,
    FactionDialogueSession currentSession,
    int assistantRound) => DiplomacyActionPolicyCaptureOps.RemoveDelayedActionsWithMissingRequiredParameters(response, currentSession, assistantRound);
        internal static void TryMapDelayedIntentFromPlayerFollowup(ParsedResponse response,
    FactionDialogueSession currentSession,
    string playerMessage,
    int assistantRound) => DiplomacyActionPolicyCaptureOps.TryMapDelayedIntentFromPlayerFollowup(response, currentSession, playerMessage, assistantRound);
        internal static void TryMapConfirmedIntentToAction(ParsedResponse response,
    FactionDialogueSession currentSession,
    PendingDelayedActionIntent baseIntent,
    int assistantRound) => DiplomacyActionPolicyCaptureOps.TryMapConfirmedIntentToAction(response, currentSession, baseIntent, assistantRound);
        internal static void RemoveDelayedActionsBlockedByShortDedupe(ParsedResponse response,
    FactionDialogueSession currentSession,
    int assistantRound) => DiplomacyActionPolicyCaptureOps.RemoveDelayedActionsBlockedByShortDedupe(response, currentSession, assistantRound);
        internal static bool IsWithinDelayedDedupeWindow(FactionDialogueSession currentSession,
    string signature,
    int currentAssistantRound) => DiplomacyActionPolicyCaptureOps.IsWithinDelayedDedupeWindow(currentSession, signature, currentAssistantRound);
        internal static PendingDelayedActionIntent CreatePendingDelayedIntent(AIAction action,
    int assistantRound,
    bool awaitingConfirmation,
    string requiredParameter) => DiplomacyActionPolicyCaptureOps.CreatePendingDelayedIntent(action, assistantRound, awaitingConfirmation, requiredParameter);
        internal static Dictionary<string, object> CloneParameters(Dictionary<string, object> source) => DiplomacyActionPolicyParameterOps.CloneParameters(source);
        internal static string BuildActionSignature(string actionType, Dictionary<string, object> parameters) => DiplomacyActionPolicyParameterOps.BuildActionSignature(actionType, parameters);
        internal static string NormalizeParameterValue(object value) => DiplomacyActionPolicyParameterOps.NormalizeParameterValue(value);
        internal static string GetMissingRequiredParameter(string actionType, Dictionary<string, object> parameters) => DiplomacyActionPolicyParameterOps.GetMissingRequiredParameter(actionType, parameters);
        internal static bool HasNonEmptyParameter(Dictionary<string, object> parameters, string key) => DiplomacyActionPolicyParameterOps.HasNonEmptyParameter(parameters, key);
}
