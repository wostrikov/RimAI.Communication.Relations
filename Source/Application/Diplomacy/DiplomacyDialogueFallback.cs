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

internal sealed class DiplomacyDialogueFallback : DiplomacyDialogueCollaborator
{
    internal DiplomacyDialogueFallback(Dialog_DiplomacyDialogue owner) : base(owner) { }



internal void AddFallbackResponse(string playerMessage)
{
    AddFallbackResponseToSession(playerMessage, session, faction);
}



internal void AddFallbackResponseToSession(string playerMessage, FactionDialogueSession currentSession, Faction currentFaction)
{
    Pawn speakerPawn = Owner.Parts.Speakers.ResolveFactionSpeakerPawn(currentSession, currentFaction);
    string senderName = DiplomacyDialogueSpeakers.ResolveFactionSenderName(currentFaction, speakerPawn);
    string response = Owner.Parts.Session.GenerateSimulatedResponse(playerMessage, currentFaction);
    currentSession.AddMessage(senderName, response, false, DialogueMessageType.Normal, speakerPawn);

    // Execute forced actions from hidden directive even in fallback mode
    if (DiplomacyActionClarificationService.TryParseSendInfoForcedActionDirective(playerMessage, out SendInfoForcedActionDirective directive))
    {
        var action = new AIAction
        {
            ActionType = directive.ActionType,
            Parameters = new Dictionary<string, object>(StringComparer.Ordinal)
        };
        ActionResult result = RelationsInteractionAdapter.Execute(action, currentFaction, applyDialogueApiGoodwillCost: true);
        if (!result.IsSuccess)
        {
            string reason = result.Message ?? "Unknown error";
            currentSession.AddMessage("System", $"Не вдалося виконати дію '{directive.ActionType}': {reason}", false, DialogueMessageType.System);
        }
    }

    Owner.Parts.Session.SaveFactionMemory(currentSession, currentFaction);
}



internal void TryRetryImmersionFallbackMessage(DialogueMessageData msg)
{
    if (session == null || msg == null || session.isWaitingForResponse)
    {
        return;
    }

    string playerMessage = session.lastPlayerRequestText?.Trim() ?? string.Empty;
    if (string.IsNullOrWhiteSpace(playerMessage))
    {
        session.AddMessage("System", "RimChat_DialogueRequestUnavailable".Translate(), false, DialogueMessageType.System);
        return;
    }

    ReplaceFallbackMessageWithRetryPending(msg);

    if (!Owner.Parts.Presence.CanSendMessageNow())
    {
        session.AddMessage("System", Owner.Parts.Feedback.BuildAiTurnStatusText(), false, DialogueMessageType.System);
        return;
    }

    List<ChatMessageData> chatMessages;
    try
    {
        chatMessages = Owner.Parts.SessionPrompt.BuildChatMessages(playerMessage, session, playerMessage, session.lastPlayerRequestWasAirdropTradeCard);
    }
    catch (PromptRenderException ex)
    {
        Owner.Parts.SessionPrompt.HandlePromptRenderFailure(ex);
        return;
    }
    catch (Exception ex)
    {
        Owner.Parts.SessionPrompt.HandlePromptBuildFailure(ex, session, faction);
        return;
    }

    chatMessages = AppendManualFallbackRetryMessage(chatMessages);
    DialogueRuntimeContext requestContext = runtimeContext.WithCurrentRuntimeMarkers();
    bool resolved = DialogueContextResolver.TryResolveLiveContext(
        requestContext,
        out DialogueLiveContext liveContext,
        out string resolveReason);
    string validateReason = string.Empty;
    bool validated = resolved && DialogueContextValidator.ValidateRequestSend(requestContext, liveContext, out validateReason);
    if (!resolved || !validated)
    {
        Owner.Parts.Feedback.HandleDroppedRequest(resolveReason, validateReason);
        return;
    }

    bool queued = conversationController.TrySendDialogueRequest(
        session,
        faction,
        chatMessages,
        requestContext,
        windowInstanceId,
        onSuccess: envelope =>
        {
            Owner.Parts.SessionPrompt.AddAIResponseToSession(envelope, session, faction, playerMessage);
        },
        onError: error =>
        {
            Log.Warning($"[RimAI.Relations] Fallback retry request failed: {error}");
            Owner.Parts.Feedback.HandleSessionRequestError(session, error);
        },
        onProgress: null,
        onDropped: reason =>
        {
            Owner.Parts.Feedback.HandleSessionDroppedRequest(session, faction, reason);
        });

    if (!queued)
    {
        if (conversationController.IsRequestDebounced(session))
        {
            Owner.Parts.Feedback.HandleDroppedRequest("request_debounced");
            return;
        }

        if (session.isWaitingForResponse)
        {
            Owner.Parts.Feedback.HandleDroppedRequest("request_already_waiting");
            return;
        }

        Owner.Parts.Feedback.HandleDroppedRequest(session.aiError, "request_queue_rejected");
    }
}



internal void ReplaceFallbackMessageWithRetryPending(DialogueMessageData msg)
{
    if (msg == null)
    {
        return;
    }

    msg.allowFallbackRetry = false;
    msg.message = "RimChat_Retry".Translate().ToString() + "...";
    if (typewriterStates.ContainsKey(msg))
    {
        typewriterStates.Remove(msg);
    }
}



internal static List<ChatMessageData> AppendManualFallbackRetryMessage(List<ChatMessageData> messages)
{
    var updated = new List<ChatMessageData>(messages ?? new List<ChatMessageData>());
    updated.Add(new ChatMessageData
    {
        role = "user",
        content = BuildManualFallbackRetryInstruction()
    });
    return updated;
}



internal static string BuildManualFallbackRetryInstruction()
{
    var sb = new StringBuilder();
    sb.Append("MANUAL_FALLBACK_RETRY=1. ");
    sb.Append("Previous diplomacy reply degraded to the local fallback template. ");
    sb.Append("Return exactly one JSON object only. ");
    sb.Append("Required top-level key: visible_dialogue. Optional top-level key: actions. ");
    sb.Append("Put all visible faction speech inside visible_dialogue. ");
    sb.Append("visible_dialogue must contain 1-2 concise in-character diplomacy sentences and must not be empty. ");
    sb.Append("Do not output the fallback line again. ");
    sb.Append("Do not output explanations, markdown fences, parenthetical metadata, debug text, or any text outside the JSON object. ");
    sb.Append("If gameplay effects are required, include matching actions in the same top-level actions array.");
    return sb.ToString();
}



internal string GetPlayerSenderName()
{
    return Owner.Parts.Speakers.ResolvePlayerSenderName(Owner.Parts.Speakers.ResolvePlayerSpeakerPawn());
}



internal void NormalizePlayerSenderNames(FactionDialogueSession currentSession)
{
    Owner.Parts.Speakers.EnsureSessionMessageSpeakers(currentSession);
}



internal string GetSenderName(Faction f)
{
    Pawn speakerPawn = Owner.Parts.Speakers.ResolveFactionSpeakerPawn(session, f);
    return DiplomacyDialogueSpeakers.ResolveFactionSenderName(f, speakerPawn);
}
}
