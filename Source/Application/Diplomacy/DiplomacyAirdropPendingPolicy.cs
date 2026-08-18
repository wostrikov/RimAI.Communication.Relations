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

internal sealed class DiplomacyAirdropPendingPolicy : DiplomacyDialogueCollaborator
{
    internal DiplomacyAirdropPendingPolicy(Dialog_DiplomacyDialogue owner) : base(owner) { }


internal static readonly Regex AirdropPendingChoicePattern = DiplomacyAirdropPendingParse.AirdropPendingChoicePattern;



internal static bool TryMapAirdropPendingSelectionFollowup(
    ParsedResponse response,
    FactionDialogueSession currentSession,
    PendingDelayedActionIntent baseIntent,
    string playerMessage,
    int assistantRound)
{
    if (response == null || currentSession == null || baseIntent == null)
    {
        return false;
    }

    if (!string.Equals(baseIntent.ActionType, AIActionNames.RequestItemAirdrop, StringComparison.Ordinal))
    {
        return false;
    }

    if (!TryReadPendingAirdropCandidates(baseIntent.Parameters, out List<PendingAirdropSelectionCandidate> candidates) ||
        candidates.Count == 0)
    {
        return false;
    }

    if (!TryResolvePendingAirdropCandidate(playerMessage, candidates, out PendingAirdropSelectionCandidate selected))
    {
        string normalizedPlayer = (playerMessage ?? string.Empty).Trim().ToLowerInvariant();
        bool isRejection = DiplomacyActionPolicyText.ContainsAnyHint(normalizedPlayer, DiplomacyActionPolicyText.AirdropSelectionRejectionHints);
        bool shouldClarify = isRejection ||
                             DiplomacyActionPolicyText.ContainsAnyHint(normalizedPlayer, DiplomacyActionPolicyText.ConfirmationHints) ||
                             DiplomacyActionPolicyText.ContainsAnyHint(normalizedPlayer, DiplomacyActionPolicyText.AmbiguousFollowupHints);
        if (!shouldClarify)
        {
            return false;
        }

        string clarification = BuildPendingAirdropSelectionClarification(candidates);
        string prefix = isRejection
            ? "RimChat_AirdropSelectionRejected".Translate().ToString()
            : string.Empty;
        string combined = string.IsNullOrWhiteSpace(prefix)
            ? clarification
            : $"{prefix}\n\n{clarification}";
        response.DialogueText = string.IsNullOrWhiteSpace(response.DialogueText)
            ? combined
            : $"{response.DialogueText}\n\n{combined}";

        return true;
    }

    Dictionary<string, object> mappedParameters = DiplomacyActionPolicyService.CloneParameters(baseIntent.Parameters);
    mappedParameters.Remove(DiplomacyAirdropWorkflow.AirdropPendingCandidatesKey);
    mappedParameters.Remove(DiplomacyAirdropWorkflow.AirdropPendingFailureCodeKey);
    mappedParameters["selected_def"] = selected.DefName;
    if (TryExtractAirdropRequestedCount(playerMessage, out int requestedCount))
    {
        mappedParameters["count"] = requestedCount;
    }

    currentSession.ClearPendingAirdropTradeCardReference();

    if (response.Actions == null)
    {
        response.Actions = new List<AIAction>();
    }

    var mappedAction = new AIAction
    {
        ActionType = AIActionNames.RequestItemAirdrop,
        Parameters = mappedParameters,
        Reason = "intent_map_pending_selection"
    };
    response.Actions.Add(mappedAction);

    if (string.IsNullOrWhiteSpace(response.DialogueText))
    {
        response.DialogueText = "RimChat_ItemAirdropSelectionChosen".Translate(
            selected.Label,
            selected.DefName).ToString();
    }

    var awaitingIntent = baseIntent.Clone();
    awaitingIntent.ActionType = AIActionNames.RequestItemAirdrop;
    awaitingIntent.Parameters = mappedParameters;
    awaitingIntent.Signature = DiplomacyActionPolicyService.BuildActionSignature(AIActionNames.RequestItemAirdrop, mappedParameters);
    awaitingIntent.AwaitingConfirmation = true;
    awaitingIntent.RequiredParameter = string.Empty;
    awaitingIntent.UpdatedAssistantRound = assistantRound;
    currentSession.pendingDelayedActionIntent = awaitingIntent;
    currentSession.lastDelayedActionIntent = awaitingIntent;
    return true;
}



internal static bool TryReadPendingAirdropCandidates(
    Dictionary<string, object> parameters,
    out List<PendingAirdropSelectionCandidate> candidates)
{
    candidates = new List<PendingAirdropSelectionCandidate>();
    if (parameters == null ||
        !parameters.TryGetValue(DiplomacyAirdropWorkflow.AirdropPendingCandidatesKey, out object rawCandidates) ||
        !(rawCandidates is IEnumerable<object> rows))
    {
        return false;
    }

    foreach (object row in rows)
    {
        if (!(row is Dictionary<string, object> data))
        {
            continue;
        }

        string defName = ReadCandidateText(data, "defName");
        if (string.IsNullOrWhiteSpace(defName))
        {
            continue;
        }

        string label = ReadCandidateText(data, "label");
        int index = ReadCandidateIndex(data, candidates.Count + 1);
        float unitPrice = ReadCandidateFloat(data, "unitPrice");
        int maxLegalCount = ReadCandidateIndex(data, "max_legal_count", 0);
        candidates.Add(new PendingAirdropSelectionCandidate
        {
            Index = index,
            DefName = defName,
            Label = string.IsNullOrWhiteSpace(label) ? defName : label,
            UnitPrice = unitPrice,
            MaxLegalCount = maxLegalCount
        });
    }

    return candidates.Count > 0;
}



internal static bool TryResolvePendingAirdropCandidate(
    string playerMessage,
    List<PendingAirdropSelectionCandidate> candidates,
    out PendingAirdropSelectionCandidate selected)
{
    selected = null;
    if (candidates == null || candidates.Count == 0)
    {
        return false;
    }

    string text = (playerMessage ?? string.Empty).Trim();
    if (string.IsNullOrWhiteSpace(text))
    {
        return false;
    }

    Match indexMatch = AirdropPendingChoicePattern.Match(text);
    if (indexMatch.Success &&
        int.TryParse(indexMatch.Groups["index"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedIndex))
    {
        selected = candidates.FirstOrDefault(candidate => candidate.Index == parsedIndex);
        if (selected != null)
        {
            return true;
        }
    }

    int chineseIndex = TryParseChineseChoiceIndex(text);
    if (chineseIndex > 0)
    {
        selected = candidates.FirstOrDefault(candidate => candidate.Index == chineseIndex);
        if (selected != null)
        {
            return true;
        }
    }

    string normalized = text.ToLowerInvariant();
    List<PendingAirdropSelectionCandidate> byName = candidates
        .Where(candidate =>
            (!string.IsNullOrWhiteSpace(candidate.DefName) &&
             normalized.Contains(candidate.DefName.ToLowerInvariant())) ||
            (!string.IsNullOrWhiteSpace(candidate.Label) &&
             normalized.Contains(candidate.Label.ToLowerInvariant())))
        .ToList();
    if (byName.Count == 1)
    {
        selected = byName[0];
        return true;
    }

    return false;
}



internal static bool TryExtractAirdropRequestedCount(string playerMessage, out int requestedCount)
{
    return DiplomacyAirdropPendingParse.TryExtractAirdropRequestedCount(playerMessage, out requestedCount);
}



internal static int TryParseChineseChoiceIndex(string text)
{
    return DiplomacyAirdropPendingParse.TryParseChineseChoiceIndex(text);
}



internal static string BuildPendingAirdropSelectionClarification(List<PendingAirdropSelectionCandidate> candidates)
{
    if (candidates == null || candidates.Count == 0)
    {
        return "RimChat_ItemAirdropAwaitingConfirmSystem".Translate().ToString();
    }

    string lines = string.Join(
        "\n",
        candidates
            .OrderBy(candidate => candidate.Index)
            .Take(5)
            .Select(candidate => DiplomacyAirdropWorkflow.BuildPendingSelectionCandidateLine(candidate)));
    return "RimChat_ItemAirdropSelectionPendingSystem".Translate(lines).ToString();
}



internal static string ReadCandidateText(Dictionary<string, object> values, string key)
{
    if (values == null || !values.TryGetValue(key, out object raw) || raw == null)
    {
        return string.Empty;
    }

    return raw.ToString()?.Trim() ?? string.Empty;
}



internal static int ReadCandidateIndex(Dictionary<string, object> values, int fallback)
{
    return ReadCandidateIndex(values, "index", fallback);
}



internal static int ReadCandidateIndex(Dictionary<string, object> values, string key, int fallback)
{
    if (values == null || !values.TryGetValue(key, out object raw) || raw == null)
    {
        return fallback;
    }

    if (raw is int intValue)
    {
        return intValue > 0 ? intValue : fallback;
    }

    if (raw is long longValue && longValue > 0 && longValue <= int.MaxValue)
    {
        return (int)longValue;
    }

    return int.TryParse(raw.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed > 0
        ? parsed
        : fallback;
}



internal static float ReadCandidateFloat(Dictionary<string, object> values, string key)
{
    if (values == null || !values.TryGetValue(key, out object raw) || raw == null)
    {
        return 0f;
    }

    if (raw is float floatValue)
    {
        return floatValue;
    }

    if (raw is double doubleValue)
    {
        return (float)doubleValue;
    }

    return float.TryParse(raw.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
        ? parsed
        : 0f;
}
}
