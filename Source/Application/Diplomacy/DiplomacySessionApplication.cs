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

internal sealed class DiplomacySessionApplication : DiplomacyDialogueCollaborator
{
    internal DiplomacySessionApplication(Dialog_DiplomacyDialogue owner) : base(owner) { }



// 玩家message气泡颜色 #91ed61
internal static readonly Color PlayerBubbleColor = new Color(0.58f, 0.88f, 0.43f, 1f);


internal static readonly Color PlayerBubbleColorDark = new Color(0.52f, 0.81f, 0.38f, 1f);


// AImessage气泡颜色
internal static readonly Color AIBubbleColor = new Color(0.25f, 0.26f, 0.3f, 0.95f);



internal void TryCommitDiplomacySessionSummaryOnClose()
{
    if (sessionCloseSummaryCommitted)
    {
        return;
    }

    sessionCloseSummaryCommitted = true;
    if (session == null || session.messages == null || faction == null)
    {
        return;
    }

    if (session.messages.Count <= sessionMessageBaselineCount)
    {
        return;
    }

    int effectiveBaseline = Math.Max(sessionMessageBaselineCount, session.lastSummarizedMessageIndex);

    DialogueSummaryService.TryRecordDiplomacySessionSummary(
        faction,
        session.messages,
        effectiveBaseline);

    RpgNpcDialogueArchiveManager.Instance.RecordDiplomacySummary(
        negotiator,
        faction,
        session.messages,
        effectiveBaseline);

    session.lastSummarizedMessageIndex = session.messages.Count;
}



internal void SendPreparedMessage(
    string playerMessage,
    bool clearStrategies,
    ItemAirdropTradeCardPayload airdropTradeCardPayload = null)
{
    if (string.IsNullOrWhiteSpace(playerMessage) || session == null || !Owner.Parts.Presence.CanSendMessageNow())
    {
        return;
    }

    if (clearStrategies)
    {
        Owner.Parts.StrategyUi.ClearPendingStrategySuggestions(session);
    }

    var currentSession = session;
    var currentFaction = faction;
    Owner.Parts.Input.RecordInputHistory(playerMessage);
    if (airdropTradeCardPayload != null)
    {
        currentSession?.SetPendingAirdropTradeCardReference(
            airdropTradeCardPayload.GetNeedReferenceText(),
            airdropTradeCardPayload.NeedDefName,
            airdropTradeCardPayload.NeedLabel,
            airdropTradeCardPayload.NeedSearchText,
            airdropTradeCardPayload.RequestedCount,
            airdropTradeCardPayload.OfferItemDefName,
            airdropTradeCardPayload.OfferItemLabel,
            airdropTradeCardPayload.OfferItemCount,
            airdropTradeCardPayload.Scenario,
            airdropTradeCardPayload.ShippingPodCount,
            airdropTradeCardPayload.ShippingCostSilver);
    }
    currentSession.lastPlayerRequestText = playerMessage;
    currentSession.lastPlayerRequestWasAirdropTradeCard = airdropTradeCardPayload != null;

    Pawn playerSpeakerPawn = Owner.Parts.Speakers.ResolvePlayerSpeakerPawn();
    if (airdropTradeCardPayload != null)
    {
        currentSession.AddAirdropTradeCardMessage(
            Owner.Parts.Speakers.ResolvePlayerSenderName(playerSpeakerPawn),
            playerMessage,
            true,
            airdropTradeCardPayload.NeedDefName,
            airdropTradeCardPayload.NeedLabel,
            airdropTradeCardPayload.RequestedCount,
            airdropTradeCardPayload.NeedUnitPrice,
            airdropTradeCardPayload.NeedReferenceTotalPrice,
            airdropTradeCardPayload.ShippingPodCount,
            airdropTradeCardPayload.ShippingCostSilver,
            airdropTradeCardPayload.OfferItemDefName,
            airdropTradeCardPayload.OfferItemLabel,
            airdropTradeCardPayload.OfferItemCount,
            airdropTradeCardPayload.OfferUnitPrice,
            airdropTradeCardPayload.OfferTotalPrice,
            playerSpeakerPawn);
    }
    else
    {
        currentSession.AddMessage(
            Owner.Parts.Speakers.ResolvePlayerSenderName(playerSpeakerPawn),
            playerMessage,
            true,
            DialogueMessageType.Normal,
            playerSpeakerPawn);
    }

    if (!AIChatServiceAsync.Instance.IsConfigured())
    {
        Log.Message("[RimAI.Relations] AI not configured, using fallback response");
        Owner.Parts.Fallback.AddFallbackResponse(playerMessage);
        return;
    }

    if (Owner.Parts.AirdropAsync.TryHandlePendingAirdropSelectionBeforeAi(playerMessage, currentSession, currentFaction))
    {
        return;
    }

    List<ChatMessageData> chatMessages;
    try
    {
        bool useAirdropTradeCardCleanHistory = airdropTradeCardPayload != null;
        chatMessages = Owner.Parts.SessionPrompt.BuildChatMessages(playerMessage, currentSession, playerMessage, useAirdropTradeCardCleanHistory);
    }
    catch (PromptRenderException ex)
    {
        Owner.Parts.SessionPrompt.HandlePromptRenderFailure(ex);
        return;
    }
    catch (Exception ex)
    {
        Owner.Parts.SessionPrompt.HandlePromptBuildFailure(ex, currentSession, currentFaction);
        return;
    }
    DialogueRuntimeContext requestContext = runtimeContext.WithCurrentRuntimeMarkers();
    bool resolved = DialogueContextResolver.TryResolveLiveContext(
        requestContext,
        out DialogueLiveContext liveContext,
        out string resolveReason);
    string validateReason = string.Empty;
    bool validated = resolved && DialogueContextValidator.ValidateRequestSend(requestContext, liveContext, out validateReason);
    if (!resolved || !validated)
    {
        Log.Warning(
            $"[RimAI.Relations] Diplomacy request rejected before queue. " +
            $"resolveReason={resolveReason ?? "null"}, validateReason={validateReason ?? "null"}, " +
            $"faction={currentFaction?.Name ?? "null"}, negotiator={negotiator?.ThingID ?? "null"}, " +
            $"pendingRequestId={currentSession?.pendingRequestId ?? "null"}, waiting={currentSession?.isWaitingForResponse ?? false}, " +
            $"hasLease={currentSession?.pendingRequestLease != null}");
        Owner.Parts.Feedback.HandleDroppedRequest(resolveReason, validateReason);
        return;
    }

    bool queued = conversationController.TrySendDialogueRequest(
        currentSession,
        currentFaction,
        chatMessages,
        requestContext,
        windowInstanceId,
        onSuccess: envelope =>
        {
            Owner.Parts.SessionPrompt.AddAIResponseToSession(envelope, currentSession, currentFaction, playerMessage);
        },
        onError: error =>
        {
            Log.Warning($"[RimAI.Relations] AI request failed: {error}");
            Owner.Parts.Feedback.HandleSessionRequestError(currentSession, error);
        },
        onProgress: null,
        onDropped: reason =>
        {
            Owner.Parts.Feedback.HandleSessionDroppedRequest(currentSession, currentFaction, reason);
        });

    if (!queued)
    {
        if (conversationController.IsRequestDebounced(currentSession))
        {
            Owner.Parts.Feedback.HandleDroppedRequest("request_debounced");
            return;
        }

        if (currentSession.isWaitingForResponse)
        {
            Owner.Parts.Feedback.HandleDroppedRequest("request_already_waiting");
            return;
        }

        Log.Warning("[RimAI.Relations] Failed to queue diplomacy AI request.");
        Owner.Parts.Feedback.HandleDroppedRequest(currentSession?.aiError, "request_queue_rejected");
    }
}



internal void AddDroppedRequestSystemMessage(string primaryReason, string secondaryReason = null)
{
    Owner.Parts.Feedback.HandleDroppedRequest(primaryReason, secondaryReason);
}



internal string GenerateSimulatedResponse(string playerMessage, Faction f)
{
    if (string.IsNullOrEmpty(playerMessage))
        return "I see. What else would you like to discuss?";

    string lowerMessage = playerMessage.ToLower();

    if (lowerMessage.Contains("trade") || lowerMessage.Contains("caravan"))
    {
        return "We are open to trade. Our caravans can reach you soon.";
    }
    else if (lowerMessage.Contains("help") || lowerMessage.Contains("aid"))
    {
        if (f.PlayerGoodwill >= 80)
        {
            return "As allies, we shall send assistance immediately.";
        }
        else
        {
            return "We are not yet close enough for such favors. Improve our relations first.";
        }
    }
    else if (lowerMessage.Contains("war") || lowerMessage.Contains("attack") || lowerMessage.Contains("raid"))
    {
        return "Threats will not be tolerated. Watch your words carefully.";
    }
    else if (lowerMessage.Contains("peace") || lowerMessage.Contains("friend"))
    {
        return "Peace is always preferable. We welcome friendly relations.";
    }
    else
    {
        return "Interesting. We shall consider your words carefully.";
    }
}



       /// <summary>/// 根据动作生成responsetext
///</summary>
       internal string GenerateResponseFromActions(List<AIAction> actions)
       {
           var sb = new System.Text.StringBuilder();
           foreach (var action in actions)
           {
               switch (action.ActionType)
               {
                   case AIActionNames.AdjustGoodwill:
                       if (action.Parameters.TryGetValue("amount", out object amount) && amount is int amt)
                       {
                           sb.AppendLine(amt > 0
                               ? "I appreciate your words. Our relations have improved."
                               : "Your words concern me. Our relations have suffered.");
                       }
                       break;
                   case AIActionNames.SendGift:
                       sb.AppendLine("I accept your gift. Let this strengthen our bond.");
                       break;
                   case AIActionNames.RequestAid:
                       sb.AppendLine("As allies, we shall assist you.");
                       break;
                   case AIActionNames.DeclareWar:
                       sb.AppendLine("You leave me no choice. Prepare for conflict!");
                       break;
                   case AIActionNames.MakePeace:
                       sb.AppendLine("Let us end this conflict. Peace is preferable.");
                       break;
                   case AIActionNames.RequestCaravan:
                       sb.AppendLine("Our traders will visit you soon.");
                       break;
                   case AIActionNames.RequestItemAirdrop:
                       sb.AppendLine("We will dispatch a supply drop to your colony.");
                       break;
                   case AIActionNames.PayPrisonerRansom:
                       bool hasTarget = action.Parameters != null &&
                           action.Parameters.TryGetValue("target_pawn_load_id", out object targetIdObj) &&
                           targetIdObj != null &&
                           int.TryParse(targetIdObj.ToString(), out int targetIdParsed) &&
                           targetIdParsed > 0;
                       bool hasOffer = action.Parameters != null &&
                           action.Parameters.TryGetValue("offer_silver", out object offerObj) &&
                           offerObj != null &&
                           int.TryParse(offerObj.ToString(), out int offerParsed) &&
                           offerParsed > 0;
                       sb.AppendLine(hasTarget && hasOffer
                           ? "We have received your ransom payment. Release now depends on the player's manual action."
                           : "Before any ransom transfer, we need the exact prisoner and offer details.");
                       break;
                   case AIActionNames.RejectRequest:
                       string reason = action.Parameters.TryGetValue("reason", out object r)
                           ? r?.ToString()
                           : "I cannot fulfill this request.";
                       sb.AppendLine(reason);
                       break;
               }
           }
           return sb.ToString().Trim();
       }



internal string FinalizeDialogueTextWithActionOutcomes(string baseDialogueText, List<ActionExecutionOutcome> outcomes)
{
    // Action failures are displayed as separate system messages via AppendFailedActionSystemMessages.
    // Preserve the AI's dialogue instead of replacing it with failure summaries.
    return baseDialogueText ?? string.Empty;
}



       /// <summary>/// 执行 AI 动作
///</summary>
       internal List<ActionExecutionOutcome> ExecuteAIActions(
           List<AIAction> actions,
           FactionDialogueSession currentSession,
           Faction currentFaction,
           string playerMessage)
       {
           var outcomes = new List<ActionExecutionOutcome>();
           bool acceptedAirdropThisTurn = false;
           BatchRansomExecutionPlan batchRansomPlan = Owner.Parts.RansomBatch.BuildBatchRansomExecutionPlan(actions, currentSession, currentFaction);
           if (batchRansomPlan.IsActive && !batchRansomPlan.IsValid)
           {
               List<AIAction> failedActions = batchRansomPlan.RansomActions;
               if (failedActions.Count <= 0)
               {
                   failedActions = actions?
                       .Where(DiplomacyRansomBatchRuntime.IsPayPrisonerRansomAction)
                       .ToList() ?? new List<AIAction>();
               }

               foreach (AIAction failedAction in failedActions)
               {
                   outcomes.Add(ActionExecutionOutcome.Failure(failedAction, batchRansomPlan.ValidationMessage));
               }

               if (failedActions.Count <= 0)
               {
                   outcomes.Add(ActionExecutionOutcome.Failure(
                       new AIAction
                       {
                           ActionType = AIActionNames.PayPrisonerRansom,
                           Parameters = new Dictionary<string, object>(StringComparer.Ordinal)
                       },
                       batchRansomPlan.ValidationMessage));
               }

               return outcomes;
           }

           foreach (var action in actions)
           {
               if (DiplomacyAirdropWorkflow.IsRequestItemAirdropAction(action))
               {
                   if (acceptedAirdropThisTurn)
                   {
                       outcomes.Add(ActionExecutionOutcome.Failure(action, "RimChat_ItemAirdropMultipleInTurnDenied".Translate().ToString()));
                       continue;
                   }

                   if (Owner.Parts.Airdrop.TryHandleAirdropActionWithConfirmation(action, currentSession, currentFaction, out ActionExecutionOutcome confirmationOutcome))
                   {
                       outcomes.Add(confirmationOutcome);
                       if (confirmationOutcome != null && confirmationOutcome.IsSuccess)
                       {
                           acceptedAirdropThisTurn = true;
                       }
                       continue;
                   }
               }

               if (Owner.Parts.Gift.TryHandleSendGiftActionWithConfirmation(action, currentSession, currentFaction, out ActionExecutionOutcome sendGiftOutcome))
               {
                   outcomes.Add(sendGiftOutcome);
                   continue;
               }

               if (Owner.Parts.Peace.TryHandleMakePeaceActionWithConfirmation(action, currentSession, currentFaction, out ActionExecutionOutcome makePeaceOutcome))
               {
                   outcomes.Add(makePeaceOutcome);
                   continue;
               }

               if (Owner.Parts.RansomSelect.TryHandleRequestInfoActionForPrisoner(action, currentSession, currentFaction, out ActionExecutionOutcome requestInfoOutcome))
               {
                   outcomes.Add(requestInfoOutcome);
                   continue;
               }

               if (Owner.Parts.RansomSelect.TryHandlePrisonerRansomActionWithSelection(action, currentSession, currentFaction, out ActionExecutionOutcome ransomSelectionOutcome))
               {
                   outcomes.Add(ransomSelectionOutcome);
                   continue;
               }

               if (Owner.Parts.Presence.TryHandlePresenceAction(action, currentSession, currentFaction))
               {
                   outcomes.Add(ActionExecutionOutcome.Success(action, "Handled by presence pipeline."));
                   continue;
               }

               if (Owner.Parts.SocialActions.TryHandleSocialCircleAction(action, currentSession, currentFaction))
               {
                   outcomes.Add(ActionExecutionOutcome.Success(action, "Handled by social-circle pipeline."));
                   continue;
               }

               InjectExplicitChallengeRequestHint(action, playerMessage);

               Log.Message($"[RimAI.Relations] Executing AI action: {action.ActionType}");
               var result = RelationsInteractionAdapter.Execute(action, currentFaction, applyDialogueApiGoodwillCost: true);

               if (result.IsSuccess)
               {
                   Log.Message($"[RimAI.Relations] Action executed successfully: {result.Message}");
                   if (string.Equals(action.ActionType, AIActionNames.PayPrisonerRansom, StringComparison.Ordinal))
                   {
                       if (batchRansomPlan.IsActive)
                       {
                           Owner.Parts.RansomBatch.HandleBatchRansomPaymentSuccess(batchRansomPlan, action, result, currentSession, currentFaction);
                       }
                       else if (DiplomacySessionOutcomeMessages.ShouldResetRansomSelectionStateAfterSuccess(result))
                       {
                           Log.Message("[RimAI.Relations] pay_prisoner_ransom paid_submitted detected. Clearing request_info(prisoner) binding state.");
                           DiplomacyRansomBatchRuntime.ResetRansomSelectionStateAfterPayment(currentSession);
                       }
                       else
                       {
                           Log.Message($"[RimAI.Relations] pay_prisoner_ransom success detected with unexpected status={DiplomacySessionOutcomeMessages.ResolveRansomSuccessStatusCode(result)}. Preserving request_info(prisoner) binding state.");
                       }
                   }
                   outcomes.Add(ActionExecutionOutcome.Success(action, result.Message, result.Data));
                   
                   // Record重要event到memory
                   RecordSignificantEventForAction(action, currentFaction, result);
               }
               else
               {
                   if (string.Equals(action.ActionType, AIActionNames.PayPrisonerRansom, StringComparison.Ordinal))
                   {
                       Log.Message("[RimAI.Relations] pay_prisoner_ransom failed. Preserving request_info(prisoner) binding state for retry.");
                       if (batchRansomPlan.IsActive)
                       {
                           Log.Message("[RimAI.Relations] batch pay_prisoner_ransom failed. Stop executing remaining actions in this turn.");
                       }
                   }

                   DiplomacySessionOutcomeMessages.LogActionFailure(action, result?.Message);
                   outcomes.Add(ActionExecutionOutcome.Failure(action, result.Message));
                   if (batchRansomPlan.IsActive && batchRansomPlan.TryGetTargetPawnLoadId(action, out _))
                   {
                       break;
                   }
               }
           }

           return outcomes;
       }



internal static void InjectExplicitChallengeRequestHint(AIAction action, string playerMessage)
{
    if (action == null ||
        !string.Equals(action.ActionType, AIActionNames.RequestRaidCallEveryone, StringComparison.Ordinal) ||
        !LooksLikeExplicitCallEveryoneChallenge(playerMessage))
    {
        return;
    }

    action.Parameters ??= new Dictionary<string, object>(StringComparer.Ordinal);
    action.Parameters["explicit_challenge_request"] = true;
}



internal static bool LooksLikeExplicitCallEveryoneChallenge(string playerMessage)
{
    if (string.IsNullOrWhiteSpace(playerMessage))
    {
        return false;
    }

    string normalized = playerMessage.Trim().ToLowerInvariant();
    return normalized.Contains("call everyone") ||
           normalized.Contains("joint raid") ||
           normalized.Contains("everyone attack") ||
           normalized.Contains("all in") ||
           normalized.Contains("联合袭击") ||
           normalized.Contains("都叫来") ||
           normalized.Contains("全都叫来") ||
           normalized.Contains("一起上");
}



       /// <summary>/// 为执行的 AI 动作record重要event (只更新内存)
///</summary>
       internal void RecordSignificantEventForAction(AIAction action, Faction currentFaction, ActionResult result)
       {
           SignificantEventType? eventType = action.ActionType switch
           {
               AIActionNames.AdjustGoodwill => SignificantEventType.GoodwillChanged,
               AIActionNames.SendGift => SignificantEventType.GiftSent,
               AIActionNames.RequestAid => SignificantEventType.AidRequested,
               AIActionNames.DeclareWar => SignificantEventType.WarDeclared,
               AIActionNames.MakePeace => SignificantEventType.PeaceMade,
               AIActionNames.RequestCaravan => SignificantEventType.TradeCaravan,
               AIActionNames.CreateQuest => SignificantEventType.QuestIssued,
               AIActionNames.RejectRequest => null,
               _ => null
           };

           if (eventType.HasValue)
           {
               string description = BuildSignificantEventDescription(action, result);
               // 只更新内存, 不save到file
               LeaderMemoryManager.Instance.RecordSignificantEvent(currentFaction, eventType.Value, Faction.OfPlayer, description);
           }
       }



internal static string BuildSignificantEventDescription(AIAction action, ActionResult result)
{
    var details = result?.Data as ActionExecutionDetails;
    string fixedCost = BuildFixedCostText(details?.DialogueCost);

    return action.ActionType switch
    {
        AIActionNames.AdjustGoodwill => $"Dialogue context changed goodwill by {ReadInt(action, "amount", 0)}. Reason: {ReadText(action, "reason", action?.Reason, "Diplomatic dialogue")}.",
        AIActionNames.SendGift => $"Sent a gift of {ReadInt(action, "silver", 500)} silver with requested goodwill gain {ReadInt(action, "goodwill_gain", 5)}.",
        AIActionNames.RequestAid => $"Requested {ReadText(action, "type", null, "Military")} aid through dialogue.{fixedCost}",
        AIActionNames.RequestCaravan => $"Requested a {ReadText(action, "type", ReadText(action, "goods", null, null), "General")} caravan through dialogue.{fixedCost}",
        AIActionNames.CreateQuest => $"Issued quest template {ReadText(action, "questDefName", null, "UnknownQuest")} through dialogue.{fixedCost}",
        AIActionNames.DeclareWar => $"Declared war through dialogue. Reason: {ReadText(action, "reason", action?.Reason, "Diplomatic conflict")}.",
        AIActionNames.MakePeace => $"Proposed peace through dialogue. Cost: {ReadInt(action, "cost", 0)} silver.",
        _ => $"Executed {action?.ActionType ?? "unknown"}."
    };
}



internal static string BuildFixedCostText(GameAIInterface.DialogueApiGoodwillCostResult cost)
{
    if (cost == null)
    {
        return string.Empty;
    }

    return $" Fixed goodwill cost applied: base {cost.BaseCost}, actual {cost.ActualChange}.";
}



internal static int ReadInt(AIAction action, string key, int fallback)
{
    if (action?.Parameters != null && action.Parameters.TryGetValue(key, out object value) && value != null)
    {
        if (value is int intValue)
        {
            return intValue;
        }

        if (int.TryParse(value.ToString(), out int parsed))
        {
            return parsed;
        }
    }

    return fallback;
}



internal static string ReadText(AIAction action, string key, string fallbackA, string fallbackB)
{
    if (action?.Parameters != null && action.Parameters.TryGetValue(key, out object value) && value != null)
    {
        string text = value.ToString()?.Trim();
        if (!string.IsNullOrWhiteSpace(text))
        {
            return text;
        }
    }

    if (!string.IsNullOrWhiteSpace(fallbackA))
    {
        return fallbackA;
    }

    return fallbackB ?? string.Empty;
}



internal void SaveFactionMemory(FactionDialogueSession currentSession, Faction currentFaction)
{
    if (currentSession == null || currentSession.messages == null) return;

    // 只更新内存中的memory, 不save到file
    // Filesave由存档save时统一processing
    LeaderMemoryManager.Instance.UpdateFromDialogue(currentFaction, currentSession.messages);
}
}
