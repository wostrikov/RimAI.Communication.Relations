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

internal sealed class DiplomacySessionOutcomeMessages : DiplomacyDialogueCollaborator
{
    internal DiplomacySessionOutcomeMessages(Dialog_DiplomacyDialogue owner) : base(owner) { }



internal void AppendSuccessfulActionSystemMessages(List<ActionExecutionOutcome> actionOutcomes, FactionDialogueSession currentSession, Faction faction)
{
    if (currentSession == null || actionOutcomes == null || actionOutcomes.Count == 0)
    {
        return;
    }

    foreach (ActionExecutionOutcome outcome in actionOutcomes)
    {
        if (!outcome.IsSuccess || outcome.Action == null)
        {
            continue;
        }

        if (outcome.Action.ActionType == AIActionNames.RequestItemAirdrop)
        {
            AppendAirdropSuccessSystemMessage(outcome, currentSession, faction);
            continue;
        }

        if (outcome.Action.ActionType == AIActionNames.PayPrisonerRansom)
        {
            AppendRansomSuccessSystemMessage(outcome, currentSession);
        }
    }
}



internal void AppendFailedActionSystemMessages(List<ActionExecutionOutcome> actionOutcomes, FactionDialogueSession currentSession)
{
    if (currentSession == null || actionOutcomes == null || actionOutcomes.Count == 0)
    {
        return;
    }

    foreach (ActionExecutionOutcome outcome in actionOutcomes)
    {
        if (outcome.IsSuccess || outcome.Action == null)
        {
            continue;
        }

        if (outcome.Action.ActionType == AIActionNames.RequestItemAirdrop)
        {
            ItemAirdropResultData payload = TryResolveItemAirdropResultData(outcome);
            if (payload != null && !string.IsNullOrWhiteSpace(payload.FailureCode))
            {
                // Include the detailed failure message so the AI can learn from
                // specific errors like "required=10950, available=5550" on next turn.
                string detail = outcome.Message ?? payload.FailureCode;
                currentSession.AddMessage(
                    "System",
                    BuildAirdropFailureSystemMessage(payload.FailureCode, detail),
                    false,
                    DialogueMessageType.System);
            }
            continue;
        }
    }
}



internal void AppendAirdropSuccessSystemMessage(ActionExecutionOutcome outcome, FactionDialogueSession currentSession, Faction faction)
{
    if (outcome.Data is ItemAirdropAsyncQueuedData)
    {
        currentSession.AddMessage(
            "System",
            DiplomacyAirdropWorkflow.BuildAirdropSelectionInProgressSystemText(),
            false,
            DialogueMessageType.System);
        return;
    }

    ItemAirdropPendingSelectionData pendingSelection = TryResolveItemAirdropPendingSelectionData(outcome);
    if (pendingSelection != null)
    {
        if (DiplomacyAirdropWorkflow.DeterminePendingSelectionResolution(pendingSelection) == AirdropPendingResolution.AutoPickTop1)
        {
            return;
        }

        currentSession.AddMessage(
            "System",
            DiplomacyAirdropWorkflow.BuildAirdropPendingSelectionSystemText(pendingSelection),
            false,
            DialogueMessageType.System);
        return;
    }

    ItemAirdropResultData payload = TryResolveItemAirdropResultData(outcome);
    if (payload == null)
    {
        return;
    }

    currentSession.AddMessage(
        "System",
        BuildAirdropSuccessSystemMessage(payload),
        false,
        DialogueMessageType.System);

    int cooldownDays = GameAIInterface.Instance.GetItemAirdropCooldownTicks(faction) / 60000;
    currentSession.AddMessage(
        "System",
        "RimChat_ItemAirdropCooldownGoodwillHint".Translate(cooldownDays),
        false,
        DialogueMessageType.System);
}



internal static void AppendRansomSuccessSystemMessage(ActionExecutionOutcome outcome, FactionDialogueSession currentSession)
{
    PrisonerRansomResultData payload = TryResolvePrisonerRansomResultData(outcome);
    if (payload == null)
    {
        return;
    }

    string status = payload.StatusCode?.Trim() ?? string.Empty;
    if (string.Equals(status, "paid_submitted", StringComparison.Ordinal))
    {
        currentSession.AddMessage(
            "System",
            "RimChat_RansomPaymentSubmittedSystem".Translate(
                ResolveRansomTargetLabel(payload.TargetPawnLoadId),
                Math.Max(0, payload.AcceptedSilver)).ToString(),
            false,
            DialogueMessageType.System);

        if (payload.OfferedSilver > 0 &&
            payload.AcceptedSilver > 0 &&
            payload.OfferedSilver != payload.AcceptedSilver)
        {
            currentSession.AddMessage(
                "System",
                "RimChat_RansomOfferNormalizedSystem".Translate(
                    payload.OfferedSilver,
                    Math.Max(1, payload.OfferWindowMinSilver),
                    Math.Max(
                        Math.Max(1, payload.OfferWindowMinSilver),
                        payload.OfferWindowMaxSilver),
                    payload.AcceptedSilver).ToString(),
                false,
                DialogueMessageType.System);
        }
    }
}



internal static PrisonerRansomResultData TryResolvePrisonerRansomResultData(ActionExecutionOutcome outcome)
{
    if (outcome?.Data is PrisonerRansomResultData direct)
    {
        return direct;
    }

    if (outcome?.Data is ActionExecutionDetails wrapped &&
        wrapped.ApiData is PrisonerRansomResultData wrappedData)
    {
        return wrappedData;
    }

    return null;
}



internal static string ResolveRansomTargetLabel(int targetPawnLoadId)
{
    if (targetPawnLoadId > 0 &&
        PrisonerRansomService.TryResolvePawnByLoadId(targetPawnLoadId, out Pawn pawn) &&
        pawn != null)
    {
        return pawn.LabelShortCap;
    }

    return "RimChat_Unknown".Translate().ToString();
}



internal static string BuildAirdropSuccessSystemMessage(ItemAirdropResultData payload)
{
    string label = payload?.ResolvedLabel;
    if (string.IsNullOrWhiteSpace(label))
    {
        label = payload?.SelectedDefName;
    }

    if (string.IsNullOrWhiteSpace(label))
    {
        label = "RimChat_Unknown".Translate().ToString();
    }

    int quantity = Math.Max(0, payload?.Quantity ?? 0);
    int finalPrice = Math.Max(0, payload?.PaymentTotalSilver ?? payload?.BudgetUsed ?? 0);
    return "RimChat_ItemAirdropTriggeredSystem".Translate(label, quantity, finalPrice);
}



internal static string BuildAirdropFailureSystemMessage(string failureCode, string detail = null)
{
    if (failureCode == "orbital_drop_unavailable")
    {
        return "RimChat_ItemAirdropFailedOrbitalSystem".Translate();
    }

    string detailSuffix = string.IsNullOrWhiteSpace(detail) ? string.Empty : $" ({detail})";
    return "RimChat_ItemAirdropFailedBody".Translate(failureCode, string.Empty) + detailSuffix;
}



internal static ItemAirdropResultData TryResolveItemAirdropResultData(ActionExecutionOutcome outcome)
{
    if (outcome?.Data is ItemAirdropResultData directPayload)
    {
        return directPayload;
    }

    if (outcome?.Data is ActionExecutionDetails wrappedDetails &&
        wrappedDetails.ApiData is ItemAirdropResultData wrappedPayload)
    {
        return wrappedPayload;
    }

    return null;
}



internal static ItemAirdropPendingSelectionData TryResolveItemAirdropPendingSelectionData(ActionExecutionOutcome outcome)
{
    if (outcome?.Data is ItemAirdropPendingSelectionData directPayload)
    {
        return directPayload;
    }

    if (outcome?.Data is ActionExecutionDetails wrappedDetails &&
        wrappedDetails.ApiData is ItemAirdropPendingSelectionData wrappedPayload)
    {
        return wrappedPayload;
    }

    return null;
}



internal static bool ShouldResetRansomSelectionStateAfterSuccess(ActionResult result)
{
    return string.Equals(ResolveRansomSuccessStatusCode(result), "paid_submitted", StringComparison.Ordinal);
}



internal static string ResolveRansomSuccessStatusCode(ActionResult result)
{
    if (result == null || !result.IsSuccess)
    {
        return string.Empty;
    }

    string messageStatus = result.Message?.Trim();
    if (!string.IsNullOrWhiteSpace(messageStatus))
    {
        return messageStatus;
    }

    PrisonerRansomResultData payload =
        result.Data as PrisonerRansomResultData ??
        (result.Data as ActionExecutionDetails)?.ApiData as PrisonerRansomResultData;
    return payload?.StatusCode?.Trim() ?? string.Empty;
}



internal static void LogActionFailure(AIAction action, string message)
{
    string actionType = action?.ActionType ?? "unknown";
    string reason = string.IsNullOrWhiteSpace(message) ? "unknown" : message;
    if (IsExpectedActionDenyMessage(reason))
    {
        RelationsSettings settings = RelationsMod.Settings ?? RelationsMod.Instance?.InstanceSettings;
        if ((settings?.ExpectedActionDenyLogLevel ?? ExpectedActionDenyLogLevel.Info) == ExpectedActionDenyLogLevel.Warning)
        {
            Log.Warning($"[RimAI.Relations][ActionDenied][Expected] action={actionType} reason={reason}");
        }
        else
        {
            ModuleLog.Message($"[RimAI.Relations][ActionDenied][Expected] action={actionType} reason={reason}");
        }
        return;
    }

    Log.Warning($"[RimAI.Relations][ActionFailed][Unexpected] action={actionType} reason={reason}");
}



internal static bool IsExpectedActionDenyFailure(ActionExecutionOutcome outcome)
{
    if (outcome == null || outcome.IsSuccess)
    {
        return false;
    }

    return IsExpectedActionDenyMessage(outcome.Message);
}



internal static bool IsExpectedActionDenyMessage(string message)
{
    if (string.IsNullOrWhiteSpace(message))
    {
        return false;
    }

    string lower = message.ToLowerInvariant();
    return lower.Contains("blocked") ||
        lower.Contains("cooldown") ||
        lower.Contains("requires") ||
        lower.Contains("not allowed") ||
        lower.Contains("validation failed") ||
        lower.Contains("below 0") ||
        lower.Contains("cannot") ||
        lower.Contains("denied");
}



internal static bool IsForcedSendInfoActionType(string actionType)
{
    if (string.IsNullOrWhiteSpace(actionType)) return false;
    return actionType == AIActionNames.RequestCaravan
        || actionType == AIActionNames.RequestVisitor
        || actionType == AIActionNames.RequestAid
        || actionType == AIActionNames.RequestRaid
        || actionType == AIActionNames.RequestRaidCallEveryone;
}
}
