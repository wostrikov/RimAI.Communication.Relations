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

internal sealed class DiplomacySessionPromptBuilder : DiplomacyDialogueCollaborator
{
    internal DiplomacySessionPromptBuilder(Dialog_DiplomacyDialogue owner) : base(owner) { }


internal void HandlePromptRenderFailure(PromptRenderException ex)
{
    if (ex == null)
    {
        return;
    }

    Log.Error("[RimAI.Relations] Prompt rendering aborted request: " + ex.Message);
    Messages.Message(
        "RimChat_PromptRenderBlocked".Translate(ex.TemplateId, ex.Channel, ex.ErrorLine, ex.ErrorColumn),
        MessageTypeDefOf.RejectInput,
        false);
    session?.AddMessage(
        "System",
        "RimChat_PromptRenderBlocked".Translate(ex.TemplateId, ex.Channel, ex.ErrorLine, ex.ErrorColumn).ToString(),
        false,
        DialogueMessageType.System);
}


internal void HandlePromptBuildFailure(
    Exception ex,
    FactionDialogueSession currentSession,
    Faction currentFaction)
{
    if (ex == null)
    {
        return;
    }

    Log.Error(
        $"[RimAI.Relations] Prompt build aborted diplomacy request. " +
        $"faction={currentFaction?.Name ?? "null"}, negotiator={negotiator?.ThingID ?? "null"}, " +
        $"exception={ex.GetType().FullName}: {ex.Message}\n{ex.StackTrace}");
    string message = $"{ "RimChat_DialogueRequestUnavailable".Translate() } [prompt_build_failed]";
    Messages.Message(message, MessageTypeDefOf.RejectInput, false);
    currentSession?.AddMessage("System", message, false, DialogueMessageType.System);
}


internal List<ChatMessageData> BuildChatMessages(string playerMessage)
{
    return BuildChatMessages(playerMessage, session, playerMessage);
}


internal List<ChatMessageData> BuildChatMessages(string playerMessage, FactionDialogueSession currentSession)
{
    return BuildChatMessages(playerMessage, currentSession, playerMessage);
}


internal List<ChatMessageData> BuildChatMessages(
    string playerMessage,
    FactionDialogueSession currentSession,
    string historyMatchMessage)
{
    return BuildChatMessages(playerMessage, currentSession, historyMatchMessage, false);
}


internal List<ChatMessageData> BuildChatMessages(
    string playerMessage,
    FactionDialogueSession currentSession,
    string historyMatchMessage,
    bool useAirdropTradeCardCleanHistory)
{
    var chatMessages = new List<ChatMessageData>();

    string systemPrompt;
    using (Context.ExpandMemoryMatchContext.Push(playerMessage))
    {
        systemPrompt = BuildSystemPrompt();
    }
    chatMessages.Add(new ChatMessageData { role = "system", content = systemPrompt });

    FactionDialogueSession activeSession = currentSession ?? session;
    if (activeSession == null)
    {
        return chatMessages;
    }

    int historyCount = activeSession.messages.Count;
    if (historyCount > 0)
    {
        DialogueMessageData lastMessage = activeSession.messages[historyCount - 1];
        bool isCurrentPlayerTurn =
            lastMessage != null &&
            (lastMessage.isPlayer || lastMessage.IsSystemMessage()) &&
            string.Equals(
                (lastMessage.message ?? string.Empty).Trim(),
                (historyMatchMessage ?? playerMessage ?? string.Empty).Trim(),
                StringComparison.Ordinal);
        if (isCurrentPlayerTurn)
        {
            historyCount--;
        }
    }

    historyCount = Math.Max(0, historyCount);
    List<DialogueMessageData> history = activeSession.messages
        .Take(historyCount)
        .ToList();
    if (useAirdropTradeCardCleanHistory)
    {
        history = BuildAirdropTradeCardCleanHistory(history);
    }

    List<ChatMessageData> compressedHistory = DialogueContextCompressionService.BuildFromDialogueMessages(history);
    chatMessages.AddRange(compressedHistory);

    string aiUserMessage = BuildAiUserMessage(playerMessage, activeSession);
    chatMessages.Add(new ChatMessageData { role = "user", content = aiUserMessage });

    Log.Message(
        $"[RimAI.Relations] Built chat messages: packed={chatMessages.Count}, raw_history={history.Count}, " +
        $"last={playerMessage.Substring(0, Math.Min(50, playerMessage.Length))}...,airdropCleanHistory={useAirdropTradeCardCleanHistory}");
    return chatMessages;
}


internal static List<DialogueMessageData> BuildAirdropTradeCardCleanHistory(List<DialogueMessageData> history)
{
    if (history == null || history.Count == 0)
    {
        return new List<DialogueMessageData>();
    }

    int lastAirdropTradeCardIndex = -1;
    for (int i = history.Count - 1; i >= 0; i--)
    {
        if (history[i]?.IsAirdropTradeCard() == true)
        {
            lastAirdropTradeCardIndex = i;
            break;
        }
    }

    if (lastAirdropTradeCardIndex < 0)
    {
        return history;
    }

    return history
        .Skip(lastAirdropTradeCardIndex)
        .Where(message => message != null && !message.IsSystemMessage())
        .ToList();
}


internal static string BuildAiUserMessage(string playerMessage, FactionDialogueSession currentSession)
{
    string visibleText = playerMessage ?? string.Empty;
    if (currentSession == null)
    {
        return visibleText;
    }

    var blocks = new List<string>();
    if (TryBuildRansomStateReference(currentSession, out string ransomStateBlock))
    {
        blocks.Add(ransomStateBlock);
    }

    if (currentSession.TryBuildPendingAirdropTradeCardReference(out string airdropReferenceBlock))
    {
        blocks.Add(airdropReferenceBlock);
    }

    if (currentSession.TryBuildPendingRansomOfferReference(out string ransomOfferReferenceBlock))
    {
        blocks.Add(ransomOfferReferenceBlock);
    }

    if (currentSession.TryBuildPendingRansomBatchReference(out string ransomBatchReferenceBlock))
    {
        blocks.Add(ransomBatchReferenceBlock);
    }

    if (blocks.Count <= 0)
    {
        return visibleText;
    }

    string result = $"{visibleText}\n\n{string.Join("\n\n", blocks)}";

    // Reinforce JSON format when airdrop data is present
    if (currentSession.hasPendingAirdropTradeCardReference)
    {
        result += "\n\n[REMINDER] Your reply MUST be a JSON object with \"visible_dialogue\" and \"actions\" array. "
            + "If you agree to this trade, include: {\"action\":\"request_item_airdrop\",\"parameters\":{\"need\":\"...\",\"payment_items\":[{\"item\":\"...\",\"count\":N}]}}. "
            + "Do NOT reply with plain text only.";
    }

    return result;
}


internal static bool TryBuildRansomStateReference(FactionDialogueSession currentSession, out string referenceBlock)
{
    referenceBlock = string.Empty;
    if (currentSession == null)
    {
        return false;
    }

    string factionId = currentSession.faction?.GetUniqueLoadID() ?? string.Empty;
    if (string.IsNullOrWhiteSpace(factionId))
    {
        return false;
    }

    currentSession.TryGetRansomSessionState(
        factionId,
        out int currentRequestTargetPawnLoadId,
        out bool hasSessionUnpaidRansomRequest);

    RansomContractManager manager = RansomContractManager.Instance;
    List<RansomContractManager.PendingReleaseSnapshot> pendingReleaseSnapshots =
        manager?.GetPendingReleaseSnapshotsForFaction(factionId) ??
        new List<RansomContractManager.PendingReleaseSnapshot>();
    bool hasPendingReleasePrisoners = pendingReleaseSnapshots.Count > 0;
    bool currentRequestPaid = currentRequestTargetPawnLoadId > 0 &&
        manager != null &&
        manager.HasPendingReleaseContractForTarget(factionId, currentRequestTargetPawnLoadId);

    bool hasUnpaidRansomRequest = hasSessionUnpaidRansomRequest;
    if (currentRequestPaid && currentRequestTargetPawnLoadId > 0)
    {
        currentRequestTargetPawnLoadId = 0;
        currentRequestPaid = false;
        hasUnpaidRansomRequest =
            currentSession.isWaitingForRansomTargetSelection ||
            currentSession.hasPendingRansomBatchSelection;
    }

    string pendingReleaseJson = BuildPendingReleasePrisonerJsonList(pendingReleaseSnapshots);
    referenceBlock =
        "[RansomState]\n" +
        "Note: This is background information about ongoing ransom operations. " +
        "Only reference this data when the conversation topic involves prisoners or ransom. " +
        "Do not proactively mention ransom status if the current topic is unrelated.\n" +
        $"current_request_target_pawn_load_id: {Math.Max(0, currentRequestTargetPawnLoadId)}\n" +
        $"current_request_paid: {ToLowerBool(currentRequestPaid)}\n" +
        $"has_unpaid_ransom_request: {ToLowerBool(hasUnpaidRansomRequest)}\n" +
        $"has_pending_release_prisoners: {ToLowerBool(hasPendingReleasePrisoners)}\n" +
        $"pending_release_prisoner_count: {pendingReleaseSnapshots.Count}\n" +
        $"pending_release_prisoners: {pendingReleaseJson}\n" +
        "[/RansomState]";
    return true;
}


internal static string BuildPendingReleasePrisonerJsonList(
    List<RansomContractManager.PendingReleaseSnapshot> snapshots)
{
    if (snapshots == null || snapshots.Count <= 0)
    {
        return "[]";
    }

    IEnumerable<string> items = snapshots
        .Where(snapshot => snapshot != null && snapshot.TargetPawnLoadId > 0)
        .GroupBy(snapshot => snapshot.TargetPawnLoadId)
        .Select(group => group.First())
        .OrderBy(snapshot => snapshot.TargetPawnLoadId)
        .Select(snapshot =>
        {
            string label = EscapeJsonText(snapshot.TargetPawnLabel);
            return $"{{\"target_pawn_load_id\":{snapshot.TargetPawnLoadId},\"label\":\"{label}\"}}";
        });
    string combined = string.Join(",", items);
    return $"[{combined}]";
}


internal static string EscapeJsonText(string value)
{
    string text = value ?? string.Empty;
    return text
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"");
}


internal static string ToLowerBool(bool value)
{
    return value ? "true" : "false";
}


internal string BuildSystemPrompt()
{
    PromptPersistenceService.Instance.Initialize();
    var settings = RelationsMod.Settings;
    var tags = ParseSceneTagsCsv(settings?.DiplomacyManualSceneTagsCsv);
    return PromptPersistenceService.Instance.BuildFullSystemPrompt(
        faction,
        PromptPersistenceService.Instance.LoadConfig(),
        false,
        tags,
        negotiator);
}


internal static List<string> ParseSceneTagsCsv(string csv)
{
    if (string.IsNullOrWhiteSpace(csv))
    {
        return null;
    }

    return csv
        .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
        .Select(tag => tag.Trim().ToLowerInvariant())
        .Where(tag => !string.IsNullOrWhiteSpace(tag))
        .Distinct()
        .ToList();
}


internal void AddAIResponseToSession(DialogueResponseEnvelope envelope, FactionDialogueSession currentSession, Faction currentFaction, string playerMessage = null)
{
    var parsedResponse = AIResponseParser.ParseResponse(envelope, currentFaction);
    parsedResponse = Owner.Parts.Policy.ApplyDiplomacyIntentDrivenActionMapping(parsedResponse, currentSession, playerMessage);
    bool hasAirdropAction = parsedResponse.Actions.Any(action =>
        string.Equals(action?.ActionType, AIActionNames.RequestItemAirdrop, StringComparison.Ordinal));
    bool hasPresenceAction = parsedResponse.Actions.Any(a => Owner.Parts.Presence.IsPresenceActionType(a?.ActionType));
    List<ActionExecutionOutcome> actionOutcomes = parsedResponse.Actions.Count > 0
        ? Owner.Parts.Session.ExecuteAIActions(parsedResponse.Actions, currentSession, currentFaction, playerMessage)
        : new List<ActionExecutionOutcome>();
    Owner.Parts.Policy.RecordDelayedActionRuntimeState(actionOutcomes, currentSession);

    string dialogueText = parsedResponse.DialogueText;

    if (string.IsNullOrWhiteSpace(dialogueText) && parsedResponse.Actions.Count > 0)
    {
        List<AIAction> successfulActions = actionOutcomes
            .Where(outcome => outcome.IsSuccess && outcome.Action != null)
            .Select(outcome => outcome.Action)
            .ToList();
        if (successfulActions.Count > 0)
        {
            dialogueText = Owner.Parts.Session.GenerateResponseFromActions(successfulActions);
        }
    }

    dialogueText = Owner.Parts.Session.FinalizeDialogueTextWithActionOutcomes(dialogueText, actionOutcomes);
    if (string.IsNullOrWhiteSpace(dialogueText))
    {
        dialogueText = ImmersionOutputGuard.BuildLocalFallbackDialogue(DialogueUsageChannel.Diplomacy);
    }
    bool isImmersionFallback = string.Equals(
        dialogueText,
        ImmersionOutputGuard.BuildLocalFallbackDialogue(DialogueUsageChannel.Diplomacy),
        StringComparison.Ordinal);
    DiplomacyAirdropAsyncWorkflow.TryCaptureAndCacheAirdropCounteroffer(dialogueText, currentSession);

    Pawn speakerPawn = Owner.Parts.Speakers.ResolveFactionSpeakerPawn(currentSession, currentFaction);
    string senderName = DiplomacyDialogueSpeakers.ResolveFactionSenderName(currentFaction, speakerPawn);
    currentSession.lastAssistantVisibleText = dialogueText ?? string.Empty;

    // Suppress consecutive identical fallbacks — the previous one already
    // shows the retry button; stacking more adds no value and confuses the player.
    if (isImmersionFallback && currentSession.lastAssistantMessageWasImmersionFallback)
    {
        Log.Warning($"[RimAI.Relations] Suppressed consecutive immersion fallback for faction={currentFaction?.Name ?? "null"}");
        return;
    }
    currentSession.lastAssistantMessageWasImmersionFallback = isImmersionFallback;

    currentSession.AddMessage(senderName, dialogueText, false, DialogueMessageType.Normal, speakerPawn);
    if (currentSession.messages.Count > 0)
    {
        DialogueMessageData addedMessage = currentSession.messages[currentSession.messages.Count - 1];
        if (addedMessage != null)
        {
            addedMessage.allowFallbackRetry = isImmersionFallback;
        }
    }
    Owner.Parts.Outcomes.AppendSuccessfulActionSystemMessages(actionOutcomes, currentSession, currentFaction);
    Owner.Parts.Outcomes.AppendFailedActionSystemMessages(actionOutcomes, currentSession);


    bool hasSuccessfulAction = actionOutcomes.Any(outcome => outcome.IsSuccess);
    foreach (ActionExecutionOutcome failedOutcome in actionOutcomes.Where(outcome => !outcome.IsSuccess))
    {
        if (failedOutcome.Action?.ActionType == AIActionNames.RequestItemAirdrop)
        {
            continue;
        }

        bool isForcedSendInfoAction = DiplomacySessionOutcomeMessages.IsForcedSendInfoActionType(failedOutcome.Action?.ActionType);
        if (!isForcedSendInfoAction && hasSuccessfulAction && DiplomacySessionOutcomeMessages.IsExpectedActionDenyFailure(failedOutcome))
        {
            continue;
        }

        string actionName = failedOutcome.Action?.ActionType ?? "RimChat_Unknown".Translate().ToString();
        string reason = string.IsNullOrWhiteSpace(failedOutcome.Message)
            ? "RimChat_Unknown".Translate().ToString()
            : failedOutcome.Message;
        currentSession.AddMessage("System", $"无法执行动作 '{actionName}': {reason}", false, DialogueMessageType.System);
    }

    if (!hasPresenceAction)
    {
        Owner.Parts.Presence.TryAutoApplyPresenceFallback(dialogueText, currentSession, currentFaction);
    }

    Owner.Parts.SocialActions.TryGenerateDialogueKeywordSocialPost(playerMessage, dialogueText, parsedResponse.Actions, currentFaction, currentSession);
    Owner.Parts.StrategyRequest.ApplyStrategySuggestions(currentSession, parsedResponse.StrategySuggestions);

    Owner.Parts.Session.SaveFactionMemory(currentSession, currentFaction);
}
}
