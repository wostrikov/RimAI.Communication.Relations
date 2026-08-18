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

internal sealed class DiplomacyGiftWorkflow : DiplomacyDialogueCollaborator
{
    internal DiplomacyGiftWorkflow(Dialog_DiplomacyDialogue owner) : base(owner) { }


internal bool TryHandleSendGiftActionWithConfirmation(
    AIAction action,
    FactionDialogueSession currentSession,
    Faction currentFaction,
    out ActionExecutionOutcome outcome)
{
    outcome = null;
    if (action == null || !string.Equals(action.ActionType, AIActionNames.SendGift, StringComparison.Ordinal))
    {
        return false;
    }

    int silver = DiplomacySessionApplication.ReadInt(action, "silver", 0);
    int goodwillGain = DiplomacySessionApplication.ReadInt(action, "goodwill_gain", 0);
    if (silver <= 0)
    {
        outcome = ActionExecutionOutcome.Failure(action, "RimChat_SendGiftConfirmSilverInvalidSystem".Translate().ToString());
        return true;
    }

    GameAIInterface.APIResult prepareResult = GameAIInterface.Instance.PrepareSendGiftPayment(currentFaction, silver, goodwillGain, negotiator);
    if (!prepareResult.Success)
    {
        outcome = ActionExecutionOutcome.Failure(action, prepareResult.Message ?? "RimChat_Unknown".Translate().ToString());
        return true;
    }

    if (!(prepareResult.Data is PreparedSendGiftData preparedData))
    {
        outcome = ActionExecutionOutcome.Failure(action, "RimChat_Unknown".Translate().ToString());
        return true;
    }

    ShowSendGiftConfirmationDialog(currentSession, currentFaction, action, preparedData);
    outcome = ActionExecutionOutcome.Success(action, "RimChat_SendGiftAwaitingConfirmSystem".Translate().ToString(), preparedData);
    return true;
}



internal void ShowSendGiftConfirmationDialog(
    FactionDialogueSession currentSession,
    Faction currentFaction,
    AIAction action,
    PreparedSendGiftData preparedData)
{
    string paymentSummary = string.Join(", ",
        (preparedData.PaymentLines ?? new List<ItemAirdropPreparedPaymentLine>())
            .Select(line => $"{line.Label ?? line.DefName} x{line.Count}"));
    if (string.IsNullOrWhiteSpace(paymentSummary))
    {
        paymentSummary = ThingDefOf.Silver.label.CapitalizeFirst() + " x" + preparedData.SilverAmount.ToString(CultureInfo.InvariantCulture);
    }

    string body = "RimChat_SendGiftConfirmBody".Translate(
        currentFaction?.Name ?? preparedData.FactionName ?? "Unknown",
        preparedData.SilverAmount,
        preparedData.GoodwillGain,
        paymentSummary).ToString();

    Find.WindowStack.Add(new Dialog_MessageBox(
        body,
        "RimChat_SendGiftConfirmAccept".Translate().ToString(),
        () => CommitPreparedSendGift(currentSession, currentFaction, action, preparedData),
        "RimChat_SendGiftConfirmCancel".Translate().ToString(),
        () => CancelPreparedSendGift(currentSession, currentFaction),
        "RimChat_SendGiftConfirmTitle".Translate().ToString(),
        false));
}



internal void CommitPreparedSendGift(
    FactionDialogueSession currentSession,
    Faction currentFaction,
    AIAction action,
    PreparedSendGiftData preparedData)
{
    GameAIInterface.APIResult commitResult = GameAIInterface.Instance.CommitPreparedSendGift(currentFaction, preparedData);
    if (!commitResult.Success)
    {
        string message = commitResult.Message ?? "RimChat_SendGiftCommitFailedSystem".Translate().ToString();
        currentSession?.AddMessage("System", message, false, DialogueMessageType.System);
        Messages.Message(message, MessageTypeDefOf.RejectInput, false);
        return;
    }

    string successMessage = "RimChat_SendGiftConfirmedSystem".Translate(
        currentFaction?.Name ?? preparedData.FactionName ?? "Unknown",
        preparedData.SilverAmount,
        preparedData.GoodwillGain).ToString();
    currentSession?.AddMessage("System", successMessage, false, DialogueMessageType.System);
    Messages.Message(successMessage, MessageTypeDefOf.PositiveEvent, false);
    Owner.Parts.Session.RecordSignificantEventForAction(action, currentFaction, ActionResult.Success(successMessage, commitResult.Data));
}



internal void CancelPreparedSendGift(FactionDialogueSession currentSession, Faction currentFaction)
{
    string message = "RimChat_SendGiftCancelledSystem".Translate(currentFaction?.Name ?? "Unknown").ToString();
    currentSession?.AddMessage("System", message, false, DialogueMessageType.System);
}
}
