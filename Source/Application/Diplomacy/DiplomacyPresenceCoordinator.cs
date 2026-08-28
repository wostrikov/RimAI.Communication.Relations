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

internal sealed class DiplomacyPresenceCoordinator : DiplomacyDialogueCollaborator
{
    internal DiplomacyPresenceCoordinator(Dialog_DiplomacyDialogue owner) : base(owner) { }


internal const string AiConversationEndSoundDefName = "RimChat_DiplomacyConversationEndedByAi";



internal void RefreshPresenceOnDialogueOpen()
{
    var manager = GameComponent_DiplomacyManager.Instance;
    if (manager == null) return;
    manager.RefreshPresenceOnDialogueOpen(faction);
    Owner.Parts.FactionList.GetAvailableFactions(true);
}



internal void LockPresenceCacheOnDialogueClose()
{
    var manager = GameComponent_DiplomacyManager.Instance;
    if (manager == null) return;
    manager.LockPresenceCacheOnDialogueClose(Owner.Parts.FactionList.GetAvailableFactions());
}



internal void DrawCurrentFactionPresenceStatus(Rect rect)
{
    DrawFactionPresenceStatus(faction, rect, true);
}



internal void DrawFactionPresenceStatus(Faction factionToDraw, Rect rect, bool compact)
{
    var status = GameComponent_DiplomacyManager.Instance?.GetPresenceStatus(factionToDraw) ?? FactionPresenceStatus.Online;
    int goodwill = factionToDraw?.PlayerGoodwill ?? 0;
    Color dotColor = GetPresenceColor(status);
    Color textColor = Owner.Parts.FactionList.GetGoodwillColor(goodwill);
    string relationLabel = Owner.Parts.FactionList.GetRelationLabelShort(goodwill);
    Text.Font = GameFont.Tiny;
    Text.Anchor = TextAnchor.MiddleLeft;
    float curX = rect.x;

    // Dot with presence color
    GUI.color = dotColor;
    float dotW = Text.CalcSize("● ").x;
    Widgets.Label(new Rect(curX, rect.y, dotW, Mathf.Max(rect.height, 18f)), "● ");
    curX += dotW;

    // Text with goodwill color
    GUI.color = textColor;
    Widgets.Label(new Rect(curX, rect.y, rect.width - dotW, Mathf.Max(rect.height, 18f)), relationLabel);

    Text.Anchor = TextAnchor.UpperLeft;
    Text.Font = GameFont.Small;
    GUI.color = Color.white;
}



internal Color GetPresenceColor(FactionPresenceStatus status)
{
    switch (status)
    {
        case FactionPresenceStatus.Online:
            return new Color(0.35f, 0.95f, 0.35f);
        case FactionPresenceStatus.DoNotDisturb:
            return new Color(0.95f, 0.35f, 0.35f);
        default:
            return new Color(0.7f, 0.7f, 0.75f);
    }
}



internal string GetPresenceLabel(FactionPresenceStatus status)
{
    switch (status)
    {
        case FactionPresenceStatus.Online:
            return "RimChat_PresenceOnline".Translate();
        case FactionPresenceStatus.DoNotDisturb:
            return "RimChat_PresenceDnd".Translate();
        default:
            return "RimChat_PresenceOffline".Translate();
    }
}



internal SendGateState EvaluateSendGate()
{
    bool showReinitiateButton = false;
    string blockedReason = null;

    bool blockedByPresence = IsInputBlockedByPresence(out string presenceReason, out showReinitiateButton);
    bool blockedByAiTurn = Owner.Parts.Input.IsInputLockedByAiTurn(out string aiTurnReason);

    if (blockedByPresence)
    {
        blockedReason = presenceReason;
    }
    else if (blockedByAiTurn)
    {
        blockedReason = aiTurnReason;
    }

    bool canSendNow = !blockedByPresence && !blockedByAiTurn && CanSendMessageNow();
    // Reinitiate is now inbound-driven; never render manual reinitiate button in UI.
    return new SendGateState(canSendNow, blockedByPresence, blockedByAiTurn, false, blockedReason);
}



internal bool CanSendMessageNow()
{
    if (session == null || session.HasPendingImageRequests())
    {
        return false;
    }

    if (session.isConversationEndedByNpc)
    {
        return false;
    }

    if (conversationController.IsRequestDebounced(session))
    {
        return false;
    }

    return (GameComponent_DiplomacyManager.Instance?.CanSendMessage(faction) ?? true);
}



internal bool IsInputBlockedByPresence(out string reason, out bool showReinitiateButton)
{
    reason = null;
    showReinitiateButton = false;
    if (session == null) return false;

    var status = GameComponent_DiplomacyManager.Instance?.GetPresenceStatus(faction) ?? FactionPresenceStatus.Online;
    if (status == FactionPresenceStatus.Offline)
    {
        reason = "RimChat_PresenceBlockedOffline".Translate();
        return true;
    }

    if (status == FactionPresenceStatus.DoNotDisturb)
    {
        reason = "RimChat_PresenceBlockedDnd".Translate();
        return true;
    }

    if (!session.isConversationEndedByNpc)
    {
        return false;
    }

    int currentTick = Find.TickManager?.TicksGame ?? 0;

    if (session.conversationEndReason == "player_initiated")
    {
        int playerRemainingTicks = session.GetReinitiateRemainingTicks(currentTick);
        if (playerRemainingTicks > 0)
        {
            float remainingHours = playerRemainingTicks / 2500f;
            reason = "RimChat_ConversationEndedByPlayerWithCooldown".Translate(remainingHours.ToString("F1"));
            return true;
        }
        session.ReinitiateConversation();
        return false;
    }

    // ExitDialogue sets canReinitiate=true with a cooldown; respect it.
    if (session.IsReinitiateAvailable(currentTick))
    {
        session.ReinitiateConversation();
        return false;
    }

    int aiRemainingTicks = session.GetReinitiateRemainingTicks(currentTick);
    if (aiRemainingTicks > 0)
    {
        float remainingHours = aiRemainingTicks / 2500f;
        reason = "RimChat_ConversationCooldownFuzzyHint".Translate(remainingHours.ToString("F1"));
        return true;
    }

    // GoOffline / SetDnd: presence status has recovered to Online, clear ended state.
    if (status == FactionPresenceStatus.Online)
    {
        session.ReinitiateConversation();
        return false;
    }

    if (!string.IsNullOrEmpty(session.conversationEndReason))
    {
        reason = "RimChat_ConversationEndedReason".Translate(session.conversationEndReason);
    }
    else
    {
        reason = "RimChat_ConversationEnded".Translate();
    }

    showReinitiateButton = false;
    return true;
}



internal void ReinitiateConversation()
{
    if (session == null) return;
    session.ReinitiateConversation();
    session.AddMessage("System", "RimChat_ConversationReinitiated".Translate(), false, DialogueMessageType.System);
}



internal bool TryHandlePresenceAction(AIAction action, FactionDialogueSession currentSession, Faction currentFaction)
{
    if (action == null || string.IsNullOrEmpty(action.ActionType))
    {
        return false;
    }

    if (action.ActionType != AIActionNames.ExitDialogue &&
        action.ActionType != AIActionNames.GoOffline &&
        action.ActionType != AIActionNames.SetDnd)
    {
        return false;
    }

    if (!(Ustas.RimAI.Communication.Relations.Module.RelationsMod.Instance?.InstanceSettings?.EnableFactionPresenceStatus ?? true))
    {
        ModuleLog.Message($"[RimAI.Relations] Presence action ignored because presence system is disabled: {action.ActionType}");
        return false;
    }

    bool wasConversationEnded = currentSession?.isConversationEndedByNpc ?? false;
    string reason = action.Reason;
    if (action.Parameters != null &&
        action.Parameters.TryGetValue("reason", out object reasonObj) &&
        reasonObj != null &&
        !string.IsNullOrWhiteSpace(reasonObj.ToString()))
    {
        reason = reasonObj.ToString();
    }

    GameComponent_DiplomacyManager.Instance?.ApplyPresenceAction(currentFaction, action.ActionType, reason, currentSession);
    ModuleLog.Message($"[RimAI.Relations] Presence action applied: {action.ActionType}, faction={currentFaction?.Name ?? "null"}, reason={reason ?? "none"}");

    if (currentSession != null)
    {
        currentSession.AddMessage("System", BuildPresenceSystemMessage(action.ActionType, reason), false, DialogueMessageType.System);
    }

    TryPlayAiConversationEndedSound(currentSession, wasConversationEnded);

    return true;
}



internal bool IsPresenceActionType(string actionType)
{
    return actionType == AIActionNames.ExitDialogue ||
           actionType == AIActionNames.GoOffline ||
           actionType == AIActionNames.SetDnd;
}



internal void TryAutoApplyPresenceFallback(string dialogueText, FactionDialogueSession currentSession, Faction currentFaction)
{
    if (currentSession == null || currentFaction == null || currentSession.isConversationEndedByNpc)
    {
        return;
    }

    if (Owner.Parts.StrategyUi.HasStrategyUsesRemaining(currentSession))
    {
        return;
    }

    if (!(Ustas.RimAI.Communication.Relations.Module.RelationsMod.Instance?.InstanceSettings?.EnableFactionPresenceStatus ?? true))
    {
        return;
    }

    string actionType = DetectAutoPresenceAction(dialogueText, currentFaction);
    if (string.IsNullOrEmpty(actionType))
    {
        return;
    }

    bool wasConversationEnded = currentSession.isConversationEndedByNpc;
    GameComponent_DiplomacyManager.Instance?.ApplyPresenceAction(currentFaction, actionType, string.Empty, currentSession);
    currentSession.AddMessage("System", BuildPresenceSystemMessage(actionType, string.Empty), false, DialogueMessageType.System);
    TryPlayAiConversationEndedSound(currentSession, wasConversationEnded);
    ModuleLog.Message($"[RimAI.Relations] Presence fallback action applied: {actionType}, faction={currentFaction.Name}");
}



internal void TryPlayAiConversationEndedSound(FactionDialogueSession currentSession, bool wasConversationEnded)
{
    if (currentSession == null || wasConversationEnded || !currentSession.isConversationEndedByNpc)
    {
        return;
    }

    SoundDef shutdownSound = DefDatabase<SoundDef>.GetNamed(AiConversationEndSoundDefName, false);
    shutdownSound?.PlayOneShotOnCamera();
}



internal string DetectAutoPresenceAction(string dialogueText, Faction currentFaction)
{
    string text = (dialogueText ?? string.Empty).ToLowerInvariant();

    if (ContainsAny(text, "припини звʼязок", "більше не пиши", "забирайся", "у чорний список", "не відповідатиму", "leave me alone", "stop contacting"))
    {
        return AIActionNames.GoOffline;
    }

    if (ContainsAny(text, "не турбуй", "не турбуйте", "не встигаю", "поговоримо пізніше", "do not disturb", "don't disturb"))
    {
        return AIActionNames.SetDnd;
    }

    if (currentFaction.PlayerGoodwill <= -75 &&
        ContainsAny(text, "погроза", "провокація", "образа", "threat", "insult"))
    {
        return AIActionNames.ExitDialogue;
    }

    return null;
}



internal bool ContainsAny(string source, params string[] tokens)
{
    if (string.IsNullOrEmpty(source) || tokens == null)
    {
        return false;
    }

    for (int i = 0; i < tokens.Length; i++)
    {
        if (!string.IsNullOrEmpty(tokens[i]) && source.Contains(tokens[i]))
        {
            return true;
        }
    }

    return false;
}



internal string BuildPresenceSystemMessage(string actionType, string reason)
{
    bool hasReason = !string.IsNullOrWhiteSpace(reason);
    switch (actionType)
    {
        case AIActionNames.ExitDialogue:
            return hasReason
                ? "RimChat_SystemExitDialogueWithReason".Translate(reason)
                : "RimChat_SystemExitDialogue".Translate();
        case AIActionNames.GoOffline:
            return hasReason
                ? "RimChat_SystemGoOfflineWithReason".Translate(reason)
                : "RimChat_SystemGoOffline".Translate();
        case AIActionNames.SetDnd:
            return hasReason
                ? "RimChat_SystemSetDndWithReason".Translate(reason)
                : "RimChat_SystemSetDnd".Translate();
        default:
            return "RimChat_SystemExitDialogue".Translate();
    }
}
}
