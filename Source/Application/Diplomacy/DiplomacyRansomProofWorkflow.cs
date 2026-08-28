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

internal sealed class DiplomacyRansomProofWorkflow : DiplomacyDialogueCollaborator
{
    internal DiplomacyRansomProofWorkflow(Dialog_DiplomacyDialogue owner) : base(owner) { }


internal const int RansomProofPortraitSize = 160;


internal const string RansomProofImageSourceUrl = "rimchat://ransom-proof";



internal void PublishRansomBatchInfoCard(
    FactionDialogueSession currentSession,
    Faction currentFaction,
    List<RansomBatchQuoteEntry> entries,
    int totalCurrentAskSilver,
    int totalMinOfferSilver,
    int totalMaxOfferSilver)
{
    if (currentSession == null || currentFaction == null || entries == null || entries.Count <= 1)
    {
        return;
    }

    string body = BuildRansomBatchInfoCardBody(currentFaction, entries);
    Pawn playerSpeaker = Owner.Parts.Speakers.ResolvePlayerSpeakerPawn();
    currentSession.AddMessage(
        Owner.Parts.Speakers.ResolvePlayerSenderName(playerSpeaker),
        body,
        true,
        DialogueMessageType.Normal,
        playerSpeaker);

    currentSession.AddMessage(
        "System",
        "RimChat_RansomBatchNeedOfferSystem".Translate(
            entries.Count,
            totalMinOfferSilver,
            totalMaxOfferSilver,
            totalCurrentAskSilver).ToString(),
        false,
        DialogueMessageType.System);

    TryQueueReplyForPlayerPrisonerInfoCard(body, currentSession, currentFaction);
}



internal static string BuildRansomBatchInfoCardBody(
    Faction currentFaction,
    List<RansomBatchQuoteEntry> entries)
{
    if (entries == null || entries.Count <= 0)
    {
        return "RimChat_RansomNoEligiblePrisonerSystem".Translate().ToString();
    }

    string lines = string.Join(
        "\n",
        entries.Select((entry, index) =>
            "RimChat_RansomBatchListLine".Translate(
                index + 1,
                entry.TargetPawn?.LabelShortCap ?? "Unknown",
                entry.TargetPawn?.thingIDNumber ?? 0,
                entry.CurrentAskSilver,
                ResolveBatchHealthPercent(entry.TargetPawn),
                BuildRansomProofCoreOrganSummary(entry.TargetPawn)).ToString()));
    string factionName = currentFaction?.Name ?? "Unknown";
    return "RimChat_RansomBatchCardBody".Translate(
        factionName,
        entries.Count,
        lines).ToString();
}



internal static int ResolveBatchHealthPercent(Pawn pawn)
{
    return Mathf.RoundToInt(Mathf.Clamp01(pawn?.health?.summaryHealth?.SummaryHealthPercent ?? 0f) * 100f);
}



internal static int ResolveBatchEstimatedAskSilver(int currentAskSilver)
{
    int normalized = Math.Max(1, currentAskSilver);
    return Math.Max(1, Mathf.RoundToInt(normalized * DiplomacyRansomSelectionWorkflow.BatchRansomEstimateMultiplier));
}



internal void PublishRansomProofCard(FactionDialogueSession currentSession, Faction currentFaction, Pawn selectedPawn)
{
    GameAIInterface.Instance.CapturePrisonerInfoCardCoreOrganSnapshot(currentFaction, selectedPawn);
    GameAIInterface.APIResult quoteResult = GameAIInterface.Instance.CalculatePrisonerRansomQuote(
        currentFaction,
        selectedPawn,
        forceRefresh: true);
    string currentAskDisplay = ResolveRansomProofCurrentAskDisplay(quoteResult);
    string caption = BuildRansomProofCaption(selectedPawn, currentFaction, currentAskDisplay);
    Pawn playerSpeaker = Owner.Parts.Speakers.ResolvePlayerSpeakerPawn();
    bool shouldQueueAutoReply = false;
    if (DiplomacyRansomProofExport.TryExportRansomProofPortrait(selectedPawn, out string imagePath))
    {
        currentSession.AddImageMessage(
            Owner.Parts.Speakers.ResolvePlayerSenderName(playerSpeaker),
            caption,
            true,
            imagePath,
            RansomProofImageSourceUrl,
            playerSpeaker);

        shouldQueueAutoReply = true;
    }
    else
    {
        currentSession.AddMessage("System", caption, false, DialogueMessageType.System);
    }

    if (quoteResult.Success && quoteResult.Data is PrisonerRansomResultData quoteData && quoteData.CurrentAskSilver > 0)
    {
        currentSession.AddMessage(
            "System",
            "RimChat_RansomReferenceAskSystem".Translate(selectedPawn.LabelShortCap, quoteData.CurrentAskSilver).ToString(),
            false,
            DialogueMessageType.System);

        if (TryGetRansomOfferWindow(quoteData, out int minOffer, out int maxOffer))
        {
            currentSession.SetPendingRansomOfferReference(
                selectedPawn.thingIDNumber,
                quoteData.CurrentAskSilver,
                minOffer,
                maxOffer);
            currentSession.AddMessage(
                "System",
                "RimChat_RansomOfferWindowSystem".Translate(
                    selectedPawn.LabelShortCap,
                    minOffer,
                    maxOffer,
                    quoteData.CurrentAskSilver).ToString(),
                false,
                DialogueMessageType.System);
        }

        if (shouldQueueAutoReply)
        {
            TryQueueReplyForPlayerPrisonerInfoCard(caption, currentSession, currentFaction);
        }

        return;
    }

    currentSession.ClearPendingRansomOfferReference();
    currentSession.AddMessage(
        "System",
        "RimChat_RansomReferenceAskUnavailableSystem".Translate(selectedPawn.LabelShortCap).ToString(),
        false,
        DialogueMessageType.System);

    if (shouldQueueAutoReply)
    {
        TryQueueReplyForPlayerPrisonerInfoCard(caption, currentSession, currentFaction);
    }
}



internal static void NormalizeSingleRansomOfferForExecution(
    AIAction action,
    FactionDialogueSession currentSession,
    Pawn resolvedTarget)
{
    if (action?.Parameters == null || currentSession == null || resolvedTarget == null)
    {
        return;
    }

    if (!DiplomacyRansomBatchRuntime.TryReadPositiveInt(action.Parameters, "offer_silver", out int originalOffer) ||
        !currentSession.TryGetPendingRansomOfferReference(
            out int targetPawnLoadId,
            out int currentAskSilver,
            out int minOfferSilver,
            out int maxOfferSilver) ||
        targetPawnLoadId != resolvedTarget.thingIDNumber)
    {
        return;
    }

    int normalizedOffer = Mathf.Clamp(originalOffer, minOfferSilver, maxOfferSilver);
    if (normalizedOffer == originalOffer)
    {
        return;
    }

    action.Parameters["offer_silver"] = normalizedOffer;
    ModuleLog.Message(
        "[RimAI.Relations] pay_prisoner_ransom single offer normalized. " +
        $"target={targetPawnLoadId}, original={originalOffer}, " +
        $"window={minOfferSilver}-{maxOfferSilver}, normalized={normalizedOffer}, " +
        $"current_ask={currentAskSilver}");
}



internal void TryQueueReplyForPlayerPrisonerInfoCard(
    string playerMessage,
    FactionDialogueSession currentSession,
    Faction currentFaction)
{
    if (string.IsNullOrWhiteSpace(playerMessage) || currentSession == null || currentFaction == null)
    {
        return;
    }

    if (DiplomacyRansomBatchRuntime.IsRansomAutoReplyCoolingDown(currentSession, out float cooldownRemaining))
    {
        ModuleLog.Message($"[RimAI.Relations] Skipped auto-reply for prisoner info card due to active timeout cooldown. remaining={cooldownRemaining:F1}s.");
        return;
    }

    if (!Owner.Parts.Presence.CanSendMessageNow())
    {
        Log.Warning("[RimAI.Relations] Skipped auto-reply for prisoner info card because send gate is blocked.");
        return;
    }

    Owner.Parts.StrategyUi.ClearPendingStrategySuggestions(currentSession);
    if (!AIChatServiceAsync.Instance.IsConfigured())
    {
        Owner.Parts.Fallback.AddFallbackResponse(playerMessage);
        return;
    }

    List<ChatMessageData> chatMessages;
    try
    {
        chatMessages = Owner.Parts.SessionPrompt.BuildChatMessages(playerMessage);
    }
    catch (PromptRenderException ex)
    {
        Owner.Parts.SessionPrompt.HandlePromptRenderFailure(ex);
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
            if (DiplomacyRansomBatchRuntime.TryClassifyRansomAutoReplyTimeout(error, out string timeoutClass))
            {
                DiplomacyRansomBatchRuntime.ArmRansomAutoReplyTimeoutCooldown(currentSession, timeoutClass, error);
            }

            Log.Warning($"[RimAI.Relations] Auto-reply request for prisoner info card failed: {error}");
            Owner.Parts.Feedback.HandleSessionRequestError(currentSession, error);
        },
        onProgress: null,
        onDropped: reason =>
        {
            if (DiplomacyRansomBatchRuntime.TryClassifyRansomAutoReplyTimeout(reason, out string timeoutClass))
            {
                DiplomacyRansomBatchRuntime.ArmRansomAutoReplyTimeoutCooldown(currentSession, timeoutClass, reason);
            }

            Owner.Parts.Feedback.HandleSessionDroppedRequest(currentSession, currentFaction, reason);
        });

    if (!queued)
    {
        if (conversationController.IsRequestDebounced(currentSession) || currentSession.isWaitingForResponse)
        {
            return;
        }

        Log.Warning("[RimAI.Relations] Failed to queue auto-reply request for prisoner info card.");
        Owner.Parts.Feedback.HandleSessionRequestError(currentSession, currentSession?.aiError);
    }
}



internal static string BuildRansomProofCaption(Pawn pawn, Faction faction, string currentAskDisplay)
{
    _ = currentAskDisplay;
    int healthPct = Mathf.RoundToInt(Mathf.Clamp01(pawn?.health?.summaryHealth?.SummaryHealthPercent ?? 0f) * 100f);
    int consciousnessPct = Mathf.RoundToInt(Mathf.Clamp01(ReadCapacitySafe(pawn, PawnCapacityDefOf.Consciousness)) * 100f);
    int age = pawn?.ageTracker?.AgeBiologicalYears ?? 0;
    string sourceFactionName = faction?.Name ?? pawn?.Faction?.Name ?? "Unknown";
    string idDisplay = string.IsNullOrWhiteSpace(pawn?.GetUniqueLoadID())
        ? "RimChat_Unknown".Translate().ToString()
        : pawn.GetUniqueLoadID().Trim();
    string coreOrganSummary = BuildRansomProofCoreOrganSummary(pawn);
    string quote = ResolveRansomProofQuote(pawn);

    return "RimChat_RansomProofCardBody".Translate(
        pawn?.LabelShortCap ?? "Unknown",
        age,
        healthPct,
        consciousnessPct,
        sourceFactionName,
        idDisplay,
        coreOrganSummary,
        quote).ToString();
}



internal static string BuildRansomProofCoreOrganSummary(Pawn pawn)
{
    List<RansomCoreOrganSnapshotEntry> snapshot = PrisonerRansomService.CaptureCoreOrganMissingSnapshot(pawn);
    string summary = PrisonerRansomService.FormatCoreOrganMissingSummary(snapshot);
    if (!string.IsNullOrWhiteSpace(summary))
    {
        return summary;
    }

    return "RimChat_RansomCoreOrgansIntact".Translate().ToString();
}



internal static string ResolveRansomProofCurrentAskDisplay(GameAIInterface.APIResult quoteResult)
{
    if (quoteResult != null &&
        quoteResult.Success &&
        quoteResult.Data is PrisonerRansomResultData quoteData &&
        quoteData.CurrentAskSilver > 0)
    {
        return quoteData.CurrentAskSilver.ToString(CultureInfo.InvariantCulture);
    }

    return "RimChat_Unknown".Translate().ToString();
}



internal static bool TryGetRansomOfferWindow(
    PrisonerRansomResultData quoteData,
    out int minOffer,
    out int maxOffer)
{
    minOffer = 0;
    maxOffer = 0;
    if (quoteData == null || quoteData.NegotiationBaseSnapshot <= 0f)
    {
        return false;
    }

    float baseValue = Math.Max(1f, quoteData.NegotiationBaseSnapshot);
    minOffer = Math.Max(1, Mathf.FloorToInt(baseValue * DiplomacyRansomSelectionWorkflow.RansomOfferWindowMinMultiplier));
    maxOffer = Math.Max(minOffer, Mathf.CeilToInt(baseValue * DiplomacyRansomSelectionWorkflow.RansomOfferWindowMaxMultiplier));
    return true;
}



internal static string ResolveRansomProofQuote(Pawn pawn)
{
    float health = Mathf.Clamp01(pawn?.health?.summaryHealth?.SummaryHealthPercent ?? 0f);
    float consciousness = Mathf.Clamp01(ReadCapacitySafe(pawn, PawnCapacityDefOf.Consciousness));
    if (health <= 0.25f || consciousness <= 0.20f)
    {
        return "RimChat_RansomProofQuoteCritical".Translate().ToString();
    }

    if (health <= 0.55f || consciousness <= 0.50f)
    {
        return "RimChat_RansomProofQuoteInjured".Translate().ToString();
    }

    return "RimChat_RansomProofQuoteHealthy".Translate().ToString();
}



internal static float ReadCapacitySafe(Pawn pawn, PawnCapacityDef capacity)
{
    if (pawn?.health?.capacities == null || capacity == null)
    {
        return 0f;
    }

    try
    {
        return pawn.health.capacities.GetLevel(capacity);
    }
    catch
    {
        return 0f;
    }
}
}
