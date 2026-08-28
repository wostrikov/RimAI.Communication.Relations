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

internal sealed class DiplomacySendInfoWorkflow : DiplomacyDialogueCollaborator
{
    internal DiplomacySendInfoWorkflow(Dialog_DiplomacyDialogue owner) : base(owner) { }



internal void TryStartManualAirdropTradeSend()
{
    if (!Owner.Parts.Presence.CanSendMessageNow() || session == null || faction == null)
    {
        return;
    }

    ActionValidationResult validation = ValidateManualAirdropTradeEntry();
    if (validation != null && !validation.Allowed)
    {
        Messages.Message(
            BuildManualAirdropTradeMenuLabel("RimChat_SendInfoMenuAirdropTrade".Translate().ToString(), validation),
            MessageTypeDefOf.RejectInput,
            false);
        return;
    }

    Find.WindowStack.Add(new Dialog_ItemAirdropTradeCard(
        session,
        faction,
        OnAirdropTradeCardSubmitted));
}



internal ActionValidationResult ValidateManualVisitorEntry()
{
    return ApiActionEligibilityService.Instance?.ValidateActionExecution(faction, AIActionNames.RequestVisitor, null)
        ?? ActionValidationResult.AllowedResult();
}



internal ActionValidationResult ValidateManualAirdropTradeEntry()
{
    return ApiActionEligibilityService.Instance?.ValidateActionExecution(faction, AIActionNames.RequestItemAirdrop, null)
        ?? ActionValidationResult.AllowedResult();
}



internal ActionValidationResult ValidateManualPrisonerInfoEntry()
{
    return ApiActionEligibilityService.Instance?.ValidateActionExecution(faction, AIActionNames.RequestInfo, null)
        ?? ActionValidationResult.AllowedResult();
}



internal static string BuildManualAirdropTradeMenuLabel(string baseLabel, ActionValidationResult validation)
{
    if (validation == null || validation.Allowed)
    {
        return baseLabel ?? string.Empty;
    }

    string blockedReason = DiplomacyDialogueActionHint.GetLocalizedValidationReason(validation);
    return string.IsNullOrWhiteSpace(blockedReason)
        ? (baseLabel ?? string.Empty)
        : $"{baseLabel} ({blockedReason})";
}



internal List<ManualQuestRequestOption> BuildManualQuestRequestOptions()
{
    if (faction == null)
    {
        return new List<ManualQuestRequestOption>();
    }

    FactionQuestAvailabilityReport report = ApiActionEligibilityService.Instance?.GetFactionQuestAvailabilityReport(faction, null);
    if (report?.AllowedQuestDefs == null || report.AllowedQuestDefs.Count == 0)
    {
        return new List<ManualQuestRequestOption>();
    }

    var options = new List<ManualQuestRequestOption>();
    foreach (string questDefName in report.AllowedQuestDefs)
    {
        QuestScriptDef questDef = DefDatabase<QuestScriptDef>.GetNamedSilentFail(questDefName);
        string label = questDef?.label;
        if (string.IsNullOrWhiteSpace(label))
        {
            label = questDefName;
        }

        options.Add(new ManualQuestRequestOption(questDefName, label.CapitalizeFirst()));
    }

    return options
        .OrderBy(option => option.Label)
        .ToList();
}



internal void TryStartManualQuestRequestSend()
{
    if (!Owner.Parts.Presence.CanSendMessageNow() || session == null || faction == null)
    {
        return;
    }

    List<ManualQuestRequestOption> options = BuildManualQuestRequestOptions();
    if (options.Count == 0)
    {
        Messages.Message("RimChat_SendInfoQuestUnavailableHint".Translate(), MessageTypeDefOf.RejectInput, false);
        return;
    }

    Find.WindowStack.Add(new Dialog_ManualQuestRequestPicker(Owner, options));
}



internal void SubmitManualQuestRequest(ManualQuestRequestOption option)
{
    if (option == null || !Owner.Parts.Presence.CanSendMessageNow() || session == null || faction == null)
    {
        return;
    }

    string systemMessage = "RimChat_SendInfoQuestSystemMessage".Translate(option.Label).ToString();
    string hiddenDirective = BuildSendInfoHiddenDirective(
        AIActionNames.CreateQuest,
        extraParameterLines: $"questDefName: {option.QuestDefName}\nrequire_exact_questDefName: true");
    SendSystemInfoRequest(systemMessage, hiddenDirective);
}



internal void HandleManualQuestRequestPickerClosedWithoutSelection()
{
}



internal void OnAirdropTradeCardSubmitted(ItemAirdropTradeCardPayload payload)
{
    if (payload == null || session == null)
    {
        return;
    }

    string summaryMessage = payload.ToVisibleSummary();
    Owner.Parts.Session.SendPreparedMessage(summaryMessage, true, payload);
}



internal static readonly TauntSendInfoOption[] TauntSendInfoOptions =
{
    new TauntSendInfoOption(
        "RimChat_SendInfoTauntOptionStandard",
        "RimChat_SendInfoTauntOptionStandardDesc",
        "RimChat_SendInfoRaidLabelStandard",
        AIActionNames.RequestRaid,
        false),
    new TauntSendInfoOption(
        "RimChat_SendInfoTauntOptionWaves",
        "RimChat_SendInfoTauntOptionWavesDesc",
        "RimChat_SendInfoRaidLabelWaves",
        AIActionNames.RequestRaidWaves,
        false,
        requiresRandomWaves: true),
    new TauntSendInfoOption(
        "RimChat_SendInfoTauntOptionJoint",
        "RimChat_SendInfoTauntOptionJointDesc",
        "RimChat_SendInfoRaidLabelJoint",
        AIActionNames.RequestRaidCallEveryone,
        true,
        explicitChallengeRequest: true)
};



internal void TryStartManualTauntSend()
{
    if (!Owner.Parts.Presence.CanSendMessageNow() || session == null || faction == null)
    {
        return;
    }

    Find.WindowStack.Add(new Dialog_SendInfoTauntPicker(Owner));
}



internal void TryStartManualCaravanRequestSend()
{
    if (!Owner.Parts.Presence.CanSendMessageNow() || session == null || faction == null)
    {
        return;
    }

    SendSystemInfoRequest(
        "RimChat_SendInfoCaravanSystemMessage".Translate().ToString(),
        BuildSendInfoHiddenDirective(AIActionNames.RequestCaravan));
}



internal void TryStartManualVisitorRequestSend()
{
    if (!Owner.Parts.Presence.CanSendMessageNow() || session == null || faction == null)
    {
        return;
    }

    SendSystemInfoRequest(
        "RimChat_SendInfoVisitorSystemMessage".Translate().ToString(),
        BuildSendInfoHiddenDirective(AIActionNames.RequestVisitor));
}



internal void TryStartManualSupportRequestSend()
{
    if (!Owner.Parts.Presence.CanSendMessageNow() || session == null || faction == null)
    {
        return;
    }

    SendSystemInfoRequest(
        "RimChat_SendInfoSupportSystemMessage".Translate().ToString(),
        BuildSendInfoHiddenDirective(
            AIActionNames.RequestAid,
            extraParameterLines: "type: Military"));
}



internal void SubmitTauntSendInfo(TauntSendInfoOption option)
{
    if (option == null || !Owner.Parts.Presence.CanSendMessageNow() || session == null || faction == null)
    {
        return;
    }

    if (option.RequiresConfirmation)
    {
        Find.WindowStack.Add(new Dialog_MessageBox(
            "RimChat_SendInfoTauntConfirmJointBody".Translate(option.RaidLabelKey.Translate()),
            "RimChat_SendInfoTauntConfirmAccept".Translate(),
            () => SendSystemInfoRequest(BuildTauntSystemMessage(option), BuildTauntHiddenDirectiveForCurrentFaction(option)),
            "RimChat_SendInfoTauntConfirmCancel".Translate(),
            null,
            "RimChat_SendInfoTauntConfirmJointTitle".Translate()));
        return;
    }

    SendSystemInfoRequest(BuildTauntSystemMessage(option), BuildTauntHiddenDirectiveForCurrentFaction(option));
}



internal static string BuildTauntSystemMessage(TauntSendInfoOption option)
{
    string raidLabel = option?.RaidLabelKey.Translate().ToString() ?? "RimChat_Unknown".Translate().ToString();
    return "RimChat_SendInfoTauntSystemMessage".Translate(raidLabel).ToString();
}



internal static string BuildTauntHiddenDirective(TauntSendInfoOption option)
{
    if (option == null)
    {
        return string.Empty;
    }

    int? randomWaves = option.RequiresRandomWaves ? Rand.RangeInclusive(2, 6) : (int?)null;
    return BuildSendInfoHiddenDirective(option.ForcedActionType, randomWaves, option.ExplicitChallengeRequest);
}



internal string BuildTauntHiddenDirectiveForCurrentFaction(TauntSendInfoOption option)
{
    if (faction == null || faction.RelationKindWith(Faction.OfPlayer) != FactionRelationKind.Hostile)
    {
        return string.Empty;
    }

    return BuildTauntHiddenDirective(option);
}



internal static string BuildSendInfoHiddenDirective(
    string forcedActionType,
    int? waves = null,
    bool explicitChallengeRequest = false,
    string extraParameterLines = null)
{
    if (string.IsNullOrWhiteSpace(forcedActionType))
    {
        return string.Empty;
    }

    string wavesLine = waves.HasValue ? $"\nwaves: {waves.Value}" : string.Empty;
    string explicitLine = explicitChallengeRequest ? "\nexplicit_challenge_request: true" : string.Empty;
    string challengeLine = explicitChallengeRequest
        ? "\nchallenge_phrase: call everyone | joint raid | гуртом на них | спільний напад"
        : string.Empty;
    string extraLines = string.IsNullOrWhiteSpace(extraParameterLines)
        ? string.Empty
        : "\n" + extraParameterLines.Trim();
    return
        "[SendInfoDirective]\n" +
        "source: manual_send_info\n" +
        $"force_action: {forcedActionType}" +
        wavesLine +
        explicitLine +
        challengeLine +
        extraLines +
        "\nrequire_matching_action: true\n" +
        "[/SendInfoDirective]\n" +
        "[SendInfoInstruction]\n" +
        "This hidden directive comes from the UI and must be executed this turn. " +
        "Keep the visible reply in character, but you MUST emit the exact forced action with the provided parameters." +
        "\n[/SendInfoInstruction]";
}



internal void SendSystemInfoRequest(string systemMessage, string hiddenDirective = null)
{
    if (string.IsNullOrWhiteSpace(systemMessage) || session == null || faction == null || !Owner.Parts.Presence.CanSendMessageNow())
    {
        return;
    }

    Owner.Parts.StrategyUi.ClearPendingStrategySuggestions(session);

    FactionDialogueSession currentSession = session;
    Faction currentFaction = faction;
    currentSession.AddMessage("System", systemMessage, false, DialogueMessageType.System);

    if (!AIChatServiceAsync.Instance.IsConfigured())
    {
        ModuleLog.Message("[RimAI.Relations] AI not configured, using fallback response");
        Owner.Parts.Fallback.AddFallbackResponseToSession(systemMessage, currentSession, currentFaction);
        return;
    }

    List<ChatMessageData> chatMessages;
    string aiDriverMessage = string.IsNullOrWhiteSpace(hiddenDirective)
        ? systemMessage
        : $"{systemMessage}\n\n{hiddenDirective}";
    try
    {
        chatMessages = Owner.Parts.SessionPrompt.BuildChatMessages(aiDriverMessage, currentSession, systemMessage);
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
            Owner.Parts.SessionPrompt.AddAIResponseToSession(envelope, currentSession, currentFaction, aiDriverMessage);
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
}
