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

internal sealed class DiplomacySocialCircleActions : DiplomacyDialogueCollaborator
{
    internal DiplomacySocialCircleActions(Dialog_DiplomacyDialogue owner) : base(owner) { }


internal void OpenManualSocialPostDialog()
{
    if (Find.WindowStack == null)
    {
        return;
    }

    Find.WindowStack.Add(new Dialog_ManualSocialPost(HandleManualSocialPostSubmitted));
}



internal void HandleManualSocialPostSubmitted(string title, string body)
{
    GameComponent_DiplomacyManager manager = GameComponent_DiplomacyManager.Instance;
    if (manager == null)
    {
        Messages.Message("RimChat_SocialUnavailable".Translate(), MessageTypeDefOf.RejectInput, false);
        return;
    }

    ManualSocialPostResult result = manager.TryPublishManualPlayerSocialPost(title, body);
    if (!result.Success)
    {
        string reason = GameComponent_DiplomacyManager.GetManualSocialPostFailureReasonLabel(result.FailureReason);
        Messages.Message("RimChat_ManualSocialPostPublishFailed".Translate(reason), MessageTypeDefOf.RejectInput, false);
        return;
    }

    Owner.Parts.SocialView.ShowSocialToast("RimChat_ManualSocialPostPublishToast".Translate(result.TriggeredFactionCount));
    Messages.Message("RimChat_ManualSocialPostPublishSuccess".Translate(result.TriggeredFactionCount), MessageTypeDefOf.PositiveEvent, false);
    Owner.Parts.SocialView.socialReadMarked = false;
    Owner.Parts.SocialView.socialPostScrollPosition = Vector2.zero;
}


internal const float RandomDialogueSocialPostChance = 0.15f;



internal bool TryHandleSocialCircleAction(AIAction action, FactionDialogueSession currentSession, Faction currentFaction)
{
    if (action == null || !string.Equals(action.ActionType, AIActionNames.PublishPublicPost, StringComparison.Ordinal))
    {
        return false;
    }

    var manager = GameComponent_DiplomacyManager.Instance;
    if (manager == null || currentFaction == null)
    {
        return true;
    }

    if (!(Ustas.RimAI.Communication.Relations.Module.RelationsMod.Instance?.InstanceSettings?.EnablePlayerInfluenceNews ?? true))
    {
        currentSession?.AddMessage("System", "RimChat_SocialActionBlocked".Translate(), false, DialogueMessageType.System);
        return true;
    }

    Dictionary<string, object> parameters = action.Parameters ?? new Dictionary<string, object>();
    string targetToken = GetStringParameter(parameters, "targetFaction");
    string summary = GetStringParameter(parameters, "summary");
    string intentHint = GetStringParameter(parameters, "intentHint");
    string categoryToken = GetStringParameter(parameters, "category");
    int sentiment = ParseSentiment(parameters);

    Faction targetFaction = manager.ResolveSocialTargetFaction(targetToken, currentFaction);
    SocialPostCategory category = ParseCategory(categoryToken);
    bool ok = manager.EnqueuePublicPost(
        currentFaction,
        targetFaction,
        category,
        sentiment,
        summary,
        true,
        out SocialPostEnqueueResult enqueueResult,
        intentHint,
        DebugGenerateReason.DialogueExplicit);

    string systemMessage = ok
        ? "RimChat_SocialActionQueued".Translate()
        : "RimChat_SocialActionFailedReason".Translate(
            GameComponent_DiplomacyManager.GetSocialFailureReasonLabel(enqueueResult.FailureReason));
    currentSession?.AddMessage("System", systemMessage, false, DialogueMessageType.System);
    return true;
}



internal void TryGenerateDialogueKeywordSocialPost(
    string playerMessage,
    string aiText,
    List<AIAction> actions,
    Faction currentFaction,
    FactionDialogueSession currentSession)
{
    if (currentFaction == null || string.IsNullOrWhiteSpace(playerMessage)) return;
    if (!(Ustas.RimAI.Communication.Relations.Module.RelationsMod.Instance?.InstanceSettings?.EnablePlayerInfluenceNews ?? true)) return;

    bool hasExplicitSocialAction = actions != null &&
                                   actions.Any(a => string.Equals(a?.ActionType, AIActionNames.PublishPublicPost, StringComparison.Ordinal));
    if (hasExplicitSocialAction) return;

    SocialPostEnqueueResult enqueueResult = new SocialPostEnqueueResult
    {
        Triggered = false,
        FailureReason = SocialPostEnqueueFailureReason.Unknown
    };
    bool created = GameComponent_DiplomacyManager.Instance != null &&
                   GameComponent_DiplomacyManager.Instance.TryCreateKeywordDialoguePost(
                       currentFaction,
                       playerMessage,
                       aiText,
                       out enqueueResult);
    Log.Message($"[RimAI.Relations] Player-influenced post attempt: faction={currentFaction?.Name}, created={created}, triggered={enqueueResult.Triggered}, failureReason={enqueueResult.FailureReason}");
    if (!enqueueResult.Triggered)
    {
        TryGenerateRandomDialogueSocialPost(playerMessage, aiText, currentFaction, currentSession);
        return;
    }

    if (created)
    {
        currentSession?.AddMessage("System", "RimChat_SocialActionQueued".Translate(), false, DialogueMessageType.System);
    }
    else
    {
        string reasonLabel = GameComponent_DiplomacyManager.GetSocialFailureReasonLabel(enqueueResult.FailureReason);
        currentSession?.AddMessage(
            "System",
            "RimChat_SocialActionFailedReason".Translate(reasonLabel),
            false,
            DialogueMessageType.System);
    }
}



internal void TryGenerateRandomDialogueSocialPost(
    string playerMessage,
    string aiText,
    Faction currentFaction,
    FactionDialogueSession currentSession)
{
    if (currentFaction == null)
    {
        return;
    }

    if (Rand.Value > RandomDialogueSocialPostChance)
    {
        return;
    }

    string aiTextOnly = (aiText ?? string.Empty).Trim();
    if (string.IsNullOrWhiteSpace(aiTextOnly))
    {
        return;
    }

    SocialPostCategory category = SocialCircleService.InferCategory(aiTextOnly, string.Empty);
    int sentiment = SocialCircleService.InferSentiment(aiTextOnly);
    if (sentiment == 0)
    {
        sentiment = category == SocialPostCategory.Military ? -1 : 1;
    }

    Faction targetFaction = GameComponent_DiplomacyManager.Instance?.ResolveSocialTargetFaction(string.Empty, currentFaction);
    bool queued = GameComponent_DiplomacyManager.Instance != null &&
                  GameComponent_DiplomacyManager.Instance.EnqueuePublicPost(
                      currentFaction,
                      targetFaction,
                      category,
                      sentiment,
                      aiTextOnly,
                      true,
                      out SocialPostEnqueueResult enqueueResult,
                      string.Empty,
                      DebugGenerateReason.DialogueKeyword);
    if (!queued)
    {
        return;
    }

    currentSession?.AddMessage("System", "RimChat_SocialActionQueued".Translate(), false, DialogueMessageType.System);
}



internal static string GetStringParameter(Dictionary<string, object> parameters, string key)
{
    if (parameters == null || string.IsNullOrEmpty(key)) return string.Empty;
    if (!parameters.TryGetValue(key, out object value) || value == null) return string.Empty;
    return value.ToString().Trim();
}



internal static int ParseSentiment(Dictionary<string, object> parameters)
{
    if (TryReadInt(parameters, "sentiment", out int sentiment))
    {
        return Math.Max(-2, Math.Min(2, sentiment));
    }
    if (TryReadInt(parameters, "amount", out int amount))
    {
        return Math.Max(-2, Math.Min(2, amount));
    }
    return 0;
}



internal static bool TryReadInt(Dictionary<string, object> parameters, string key, out int value)
{
    value = 0;
    if (parameters == null || !parameters.TryGetValue(key, out object raw) || raw == null)
    {
        return false;
    }

    if (raw is int intValue)
    {
        value = intValue;
        return true;
    }

    if (raw is float floatValue)
    {
        value = Mathf.RoundToInt(floatValue);
        return true;
    }

    return int.TryParse(raw.ToString(), out value);
}



internal static SocialPostCategory ParseCategory(string token)
{
    if (string.IsNullOrWhiteSpace(token))
    {
        return SocialPostCategory.Diplomatic;
    }

    if (Enum.TryParse(token, true, out SocialPostCategory parsed))
    {
        return parsed;
    }
    return SocialPostCategory.Diplomatic;
}
}
