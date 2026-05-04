using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RimChat.AI;
using RimChat.DiplomacySystem;
using RimChat.Memory;
using RimChat.Dialogue;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimChat.UI
{
    public partial class Dialog_DiplomacyDialogue
    {
        private bool TryHandleMakePeaceActionWithConfirmation(
            AIAction action,
            FactionDialogueSession currentSession,
            Faction currentFaction,
            out ActionExecutionOutcome outcome)
        {
            outcome = null;
            if (action == null || !string.Equals(action.ActionType, AIActionNames.MakePeace, StringComparison.Ordinal))
            {
                return false;
            }

            int peaceCost = ReadInt(action, "cost", 0);
            if (peaceCost <= 0)
            {
                outcome = ActionExecutionOutcome.Failure(action, "RimChat_MakePeaceConfirmCostInvalidSystem".Translate().ToString());
                return true;
            }

            GameAIInterface.APIResult prepareResult = GameAIInterface.Instance.PrepareMakePeacePayment(currentFaction, peaceCost, negotiator);
            if (!prepareResult.Success)
            {
                outcome = ActionExecutionOutcome.Failure(action, prepareResult.Message ?? "RimChat_Unknown".Translate().ToString());
                return true;
            }

            if (!(prepareResult.Data is PreparedMakePeacePaymentData preparedData))
            {
                outcome = ActionExecutionOutcome.Failure(action, "RimChat_Unknown".Translate().ToString());
                return true;
            }

            ShowMakePeaceConfirmationDialog(currentSession, currentFaction, action, preparedData);
            outcome = ActionExecutionOutcome.Success(action, "RimChat_MakePeaceAwaitingConfirmSystem".Translate().ToString(), preparedData);
            return true;
        }

        private void ShowMakePeaceConfirmationDialog(
            FactionDialogueSession currentSession,
            Faction currentFaction,
            AIAction action,
            PreparedMakePeacePaymentData preparedData)
        {
            string paymentSummary = string.Join(", ",
                (preparedData.PaymentLines ?? new List<ItemAirdropPreparedPaymentLine>())
                    .Select(line => $"{line.Label ?? line.DefName} x{line.Count}"));
            if (string.IsNullOrWhiteSpace(paymentSummary))
            {
                paymentSummary = ThingDefOf.Silver.label.CapitalizeFirst() + " x" + preparedData.PeaceCostSilver.ToString(CultureInfo.InvariantCulture);
            }

            string body = "RimChat_MakePeaceConfirmBody".Translate(
                currentFaction?.Name ?? preparedData.FactionName ?? "Unknown",
                preparedData.PeaceCostSilver,
                paymentSummary).ToString();

            Find.WindowStack.Add(new Dialog_MessageBox(
                body,
                "RimChat_MakePeaceConfirmAccept".Translate().ToString(),
                () => CommitPreparedMakePeacePayment(currentSession, currentFaction, action, preparedData),
                "RimChat_MakePeaceConfirmCancel".Translate().ToString(),
                () => CancelPreparedMakePeacePayment(currentSession, currentFaction),
                "RimChat_MakePeaceConfirmTitle".Translate().ToString(),
                false));
        }

        private void CommitPreparedMakePeacePayment(
            FactionDialogueSession currentSession,
            Faction currentFaction,
            AIAction action,
            PreparedMakePeacePaymentData preparedData)
        {
            GameAIInterface.APIResult commitResult = GameAIInterface.Instance.CommitPreparedMakePeace(currentFaction, preparedData);
            if (!commitResult.Success)
            {
                string message = commitResult.Message ?? "RimChat_MakePeaceCommitFailedSystem".Translate().ToString();
                currentSession?.AddMessage("System", message, false, DialogueMessageType.System);
                Messages.Message(message, MessageTypeDefOf.RejectInput, false);
                return;
            }

            string successMessage = "RimChat_MakePeaceConfirmedSystem".Translate(currentFaction?.Name ?? preparedData.FactionName ?? "Unknown", preparedData.PeaceCostSilver).ToString();
            currentSession?.AddMessage("System", successMessage, false, DialogueMessageType.System);
            Messages.Message(successMessage, MessageTypeDefOf.PositiveEvent, false);
            RecordSignificantEventForAction(action, currentFaction, ActionResult.Success(successMessage, commitResult.Data));
        }

        private void CancelPreparedMakePeacePayment(FactionDialogueSession currentSession, Faction currentFaction)
        {
            string message = "RimChat_MakePeaceCancelledSystem".Translate(currentFaction?.Name ?? "Unknown").ToString();
            currentSession?.AddMessage("System", message, false, DialogueMessageType.System);
        }
    }
}
