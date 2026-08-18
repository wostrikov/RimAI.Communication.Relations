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

internal sealed class DiplomacyDialogueStrategyRequest : DiplomacyDialogueCollaborator
{
    internal DiplomacyDialogueStrategyRequest(Dialog_DiplomacyDialogue owner) : base(owner) { }



internal void ApplyStrategySuggestions(FactionDialogueSession currentSession, List<StrategySuggestion> suggestions)
{
    if (currentSession == null)
    {
        return;
    }

    if (!Owner.Parts.StrategyUi.IsStrategyUiEnabled())
    {
        Owner.Parts.StrategyUi.ClearPendingStrategySuggestions(currentSession);
        Owner.Parts.StrategyUi.strategySuggestionRequestPending = false;
        return;
    }

    if (currentSession.isConversationEndedByNpc)
    {
        Owner.Parts.StrategyUi.ClearPendingStrategySuggestions(currentSession);
        Owner.Parts.StrategyUi.strategySuggestionRequestPending = false;
        return;
    }

    if (!Owner.Parts.StrategyUi.HasStrategyUsesRemaining(currentSession))
    {
        Owner.Parts.StrategyUi.ClearPendingStrategySuggestions(currentSession);
        Owner.Parts.StrategyUi.strategySuggestionRequestPending = false;
        return;
    }

    if (suggestions == null || suggestions.Count != DiplomacyDialogueStrategyUi.StrategySuggestionRequiredCount)
    {
        Owner.Parts.StrategyUi.ClearPendingStrategySuggestions(currentSession);
        Log.Message("[RimAI.Relations] Strategy payload missing/invalid; requesting strict follow-up strategy payload.");
        TryRequestStrategySuggestionsFromLLM(currentSession, faction);
        return;
    }

    var mapped = suggestions
        .Select(Owner.Parts.StrategyPrompt.MapStrategySuggestion)
        .Where(s => s != null && !string.IsNullOrWhiteSpace(s.Content))
        .Take(DiplomacyDialogueStrategyUi.StrategySuggestionRequiredCount)
        .ToList();
    mapped = Owner.Parts.StrategyPrompt.EnsureStrategySuggestionCount(mapped);

    if (mapped.Count != DiplomacyDialogueStrategyUi.StrategySuggestionRequiredCount)
    {
        Owner.Parts.StrategyUi.ClearPendingStrategySuggestions(currentSession);
        Log.Message("[RimAI.Relations] Strategy payload invalid after parse, requesting follow-up strategy payload.");
        TryRequestStrategySuggestionsFromLLM(currentSession, faction);
        return;
    }

    Owner.Parts.StrategyPrompt.ApplyAttributeBasisFallback(mapped);
    Owner.Parts.StrategyUi.strategySuggestionRequestPending = false;
    currentSession.pendingStrategySuggestions = mapped;
}



internal void TryRequestStrategySuggestionsFromLLM(FactionDialogueSession currentSession, Faction currentFaction)
{
    if (currentSession == null || currentFaction == null || Owner.Parts.StrategyUi.strategySuggestionRequestPending)
    {
        return;
    }

    if (!Owner.Parts.StrategyUi.IsStrategyUiEnabled())
    {
        return;
    }

    if (!Owner.Parts.StrategyUi.HasStrategyUsesRemaining(currentSession))
    {
        return;
    }

    if (currentSession.pendingStrategySuggestions != null &&
        currentSession.pendingStrategySuggestions.Count == DiplomacyDialogueStrategyUi.StrategySuggestionRequiredCount)
    {
        return;
    }

    if (!AIChatServiceAsync.Instance.IsConfigured())
    {
        return;
    }

    var requestMessages = Owner.Parts.StrategyPrompt.BuildStrategySuggestionRequestMessages(currentSession, currentFaction);
    if (requestMessages == null || requestMessages.Count == 0)
    {
        return;
    }

    int snapshotMessageCount = currentSession.messages?.Count ?? 0;
    Owner.Parts.StrategyUi.strategySuggestionRequestPending = true;
    Log.Message("[RimAI.Relations] Sending strategy follow-up request.");

    string requestId = string.Empty;
    requestId = AIChatServiceAsync.Instance.SendChatRequestAsync(
        requestMessages,
        onSuccess: response =>
        {
            if (!string.IsNullOrEmpty(Owner.Parts.StrategyUi.strategySuggestionRequestId) &&
                !string.Equals(Owner.Parts.StrategyUi.strategySuggestionRequestId, requestId, StringComparison.Ordinal))
            {
                return;
            }

            Owner.Parts.StrategyUi.strategySuggestionRequestId = null;
            Owner.Parts.StrategyUi.strategySuggestionRequestPending = false;
            if (!Owner.Parts.StrategyContext.IsStrategyRequestContextValid(currentSession, currentFaction, snapshotMessageCount))
            {
                return;
            }
            if (!Owner.Parts.StrategyUi.IsStrategyUiEnabled())
            {
                Owner.Parts.StrategyUi.ClearPendingStrategySuggestions(currentSession);
                return;
            }

            var parsed = AIResponseParser.ParseResponse(response, currentFaction);
            var mapped = parsed?.StrategySuggestions?
                .Select(Owner.Parts.StrategyPrompt.MapStrategySuggestion)
                .Where(s => s != null && !string.IsNullOrWhiteSpace(s.Content))
                .Take(DiplomacyDialogueStrategyUi.StrategySuggestionRequiredCount)
                .ToList() ?? new List<PendingStrategySuggestion>();
            bool usedLocalFallback = mapped.Count != DiplomacyDialogueStrategyUi.StrategySuggestionRequiredCount;
            mapped = Owner.Parts.StrategyPrompt.EnsureStrategySuggestionCount(mapped);

            if (mapped.Count == DiplomacyDialogueStrategyUi.StrategySuggestionRequiredCount)
            {
                currentSession.pendingStrategySuggestions = mapped;
                if (usedLocalFallback)
                {
                    Log.Message("[RimAI.Relations] Strategy follow-up payload invalid; local fallback strategy set primed.");
                }
                else
                {
                    Log.Message("[RimAI.Relations] Strategy follow-up request succeeded, strategy buttons primed.");
                }
                return;
            }

            Log.Message("[RimAI.Relations] Strategy follow-up produced no valid strategy payload.");
        },
        onError: error =>
        {
            if (!string.IsNullOrEmpty(Owner.Parts.StrategyUi.strategySuggestionRequestId) &&
                !string.Equals(Owner.Parts.StrategyUi.strategySuggestionRequestId, requestId, StringComparison.Ordinal))
            {
                return;
            }

            Owner.Parts.StrategyUi.strategySuggestionRequestId = null;
            Owner.Parts.StrategyUi.strategySuggestionRequestPending = false;
            if (Owner.Parts.StrategyContext.IsStrategyRequestContextValid(currentSession, currentFaction, snapshotMessageCount) &&
                Owner.Parts.StrategyUi.IsStrategyUiEnabled() &&
                !currentSession.isConversationEndedByNpc &&
                Owner.Parts.StrategyUi.HasStrategyUsesRemaining(currentSession))
            {
                currentSession.pendingStrategySuggestions = Owner.Parts.StrategyPrompt.EnsureStrategySuggestionCount(new List<PendingStrategySuggestion>());
                Log.Message($"[RimAI.Relations] Strategy follow-up request failed: {error}; local fallback strategies primed.");
                return;
            }

            Log.Warning($"[RimAI.Relations] Strategy follow-up request failed: {error}");
        },
        onProgress: null,
        usageChannel: DialogueUsageChannel.Diplomacy,
        debugSource: AIRequestDebugSource.StrategySuggestion
    );

    if (string.IsNullOrEmpty(requestId))
    {
        Owner.Parts.StrategyUi.strategySuggestionRequestPending = false;
        return;
    }

    Owner.Parts.StrategyUi.strategySuggestionRequestId = requestId;
}



internal void CancelStrategySuggestionRequest()
{
    if (string.IsNullOrEmpty(Owner.Parts.StrategyUi.strategySuggestionRequestId))
    {
        return;
    }

    AIChatServiceAsync.Instance.CancelRequest(
        Owner.Parts.StrategyUi.strategySuggestionRequestId,
        "strategy_request_cancelled",
        "Request cancelled by strategy panel close");
    Owner.Parts.StrategyUi.strategySuggestionRequestId = null;
    Owner.Parts.StrategyUi.strategySuggestionRequestPending = false;
}
}
