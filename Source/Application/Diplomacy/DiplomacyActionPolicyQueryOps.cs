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

namespace Ustas.RimAI.Communication.Relations.UI
{
    /// <summary>
    /// Query helpers for delayed diplomacy action policy.
    /// </summary>
    internal static class DiplomacyActionPolicyQueryOps
    {
internal static bool HasPendingAirdropSelection(FactionDialogueSession currentSession)
{
    if (currentSession?.pendingDelayedActionIntent == null)
    {
        return false;
    }

    if (!string.Equals(currentSession.pendingDelayedActionIntent.ActionType, AIActionNames.RequestItemAirdrop, StringComparison.Ordinal))
    {
        return false;
    }

    return DiplomacyAirdropPendingPolicy.TryReadPendingAirdropCandidates(currentSession.pendingDelayedActionIntent.Parameters, out List<PendingAirdropSelectionCandidate> candidates) && candidates.Count > 0;
}



internal static ParsedResponse CreateEmptyParsedResponse()
{
    return new ParsedResponse
    {
        Success = true,
        DialogueText = string.Empty,
        Actions = new List<AIAction>(),
        StrategySuggestions = new List<StrategySuggestion>()
    };
}



internal static int GetAssistantDialogueRound(FactionDialogueSession currentSession)
{
    if (currentSession?.messages == null)
    {
        return 0;
    }

    return currentSession.messages.Count(msg =>
        msg != null &&
        !msg.isPlayer &&
        msg.messageType == DialogueMessageType.Normal);
}



internal static bool IsDelayedActionType(string actionType)
{
    return !string.IsNullOrWhiteSpace(actionType) && DiplomacyActionPolicyService.DelayedActionTypes.Contains(actionType);
}



internal static bool HasDelayedActions(List<AIAction> actions)
{
    return actions != null && actions.Any(action => IsDelayedActionType(action?.ActionType));
}
    }
}
