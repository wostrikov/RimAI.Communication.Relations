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

internal sealed class DiplomacyDialogueInput : DiplomacyDialogueCollaborator
{
    readonly DiplomacyDialogueInputComposer Composer;

    internal DiplomacyDialogueInput(Dialog_DiplomacyDialogue owner) : base(owner)
    {
        Composer = new DiplomacyDialogueInputComposer(this);
    }



internal void DrawInputArea(Rect rect)



{



    Composer.DrawInputArea(rect);



}




internal void DrawSendInfoEntry(Rect sendRect, SendGateState sendGate)



{



    Composer.DrawSendInfoEntry(sendRect, sendGate);



}




internal void OpenSendInfoMenu()



{



    Composer.OpenSendInfoMenu();



}




internal void TryEndConversation()
{
    if (session == null || faction == null)
    {
        return;
    }

    if (session.isConversationEndedByNpc)
    {
        Messages.Message("RimChat_SendInfoMenuEndConversationAlreadyEnded".Translate(), MessageTypeDefOf.RejectInput, false);
        return;
    }

    int cooldownTicks = 2500;
    session.MarkConversationEnded("player_initiated", true, cooldownTicks);
    GameComponent_DiplomacyManager.Instance?.ApplyPresenceAction(faction, AIActionNames.ExitDialogue, string.Empty, session);
    session.AddMessage("System", "RimChat_SystemExitDialogueByPlayer".Translate(), false, DialogueMessageType.System);
    Messages.Message("RimChat_SendInfoMenuEndConversationSuccess".Translate(faction.Name), MessageTypeDefOf.TaskCompletion, false);
}



internal static void DrawSingleLineClippedLabel(Rect rect, string text)
{
    bool previousWordWrap = Text.WordWrap;
    TextAnchor previousAnchor = Text.Anchor;
    GameFont previousFont = Text.Font;
    Color previousColor = GUI.color;
    Text.WordWrap = false;
    string renderText = (text ?? string.Empty).Truncate(rect.width);
    Widgets.Label(rect, renderText);
    Text.WordWrap = previousWordWrap;
    Text.Anchor = previousAnchor;
    Text.Font = previousFont;
    GUI.color = previousColor;
}



internal void DrawStatusLabelWithVerticalScroll(Rect rect, string text)
{
    string content = (text ?? string.Empty).Replace("\r", string.Empty);
    if (string.IsNullOrWhiteSpace(content))
    {
        ResetBlockedReasonAutoScroll(false);
        DrawSingleLineClippedLabel(rect, string.Empty);
        return;
    }

    float contentHeight = MeasureWrappedTextHeight(content, rect.width);
    if (contentHeight <= rect.height + 0.1f)
    {
        ResetBlockedReasonAutoScroll(false);
        DrawSingleLineClippedLabel(rect, content);
        return;
    }

    ResetBlockedReasonAutoScrollOnTextChange(content);
    float maxOffset = Mathf.Max(0f, contentHeight - rect.height);
    blockedReasonAutoScrollOffset = Mathf.Clamp(blockedReasonAutoScrollOffset, 0f, maxOffset);
    UpdateBlockedReasonAutoScrollOffset(maxOffset);
    DrawBlockedReasonAutoScrollText(rect, content, contentHeight);
}



internal float MeasureWrappedTextHeight(string content, float width)
{
    bool previousWordWrap = Text.WordWrap;
    Text.WordWrap = true;
    float contentHeight = Mathf.Max(0f, Mathf.Ceil(Text.CalcHeight(content, Mathf.Max(1f, width))));
    Text.WordWrap = previousWordWrap;
    return contentHeight;
}



internal void ResetBlockedReasonAutoScrollOnTextChange(string content)
{
    if (string.Equals(blockedReasonScrollText, content, StringComparison.Ordinal))
    {
        return;
    }

    blockedReasonScrollText = content;
    ResetBlockedReasonAutoScroll(false);
}



internal void UpdateBlockedReasonAutoScrollOffset(float maxOffset)
{
    float now = Time.realtimeSinceStartup;
    if (!TryGetBlockedReasonDeltaTime(now, out float deltaTime))
    {
        return;
    }

    if (now < blockedReasonAutoScrollPauseUntil)
    {
        return;
    }

    blockedReasonAutoScrollOffset += blockedReasonAutoScrollDirection * Dialog_DiplomacyDialogue.BlockedReasonAutoScrollSpeed * deltaTime;
    HandleBlockedReasonAutoScrollBoundary(maxOffset, now);
}



internal bool TryGetBlockedReasonDeltaTime(float now, out float deltaTime)
{
    if (blockedReasonAutoScrollLastRealtime < 0f)
    {
        blockedReasonAutoScrollLastRealtime = now;
        deltaTime = 0f;
        return false;
    }

    deltaTime = Mathf.Max(0f, now - blockedReasonAutoScrollLastRealtime);
    blockedReasonAutoScrollLastRealtime = now;
    return deltaTime > 0f;
}



internal void HandleBlockedReasonAutoScrollBoundary(float maxOffset, float now)
{
    if (blockedReasonAutoScrollOffset >= maxOffset)
    {
        blockedReasonAutoScrollOffset = maxOffset;
        blockedReasonAutoScrollDirection = -1;
        blockedReasonAutoScrollPauseUntil = now + Dialog_DiplomacyDialogue.BlockedReasonAutoScrollPauseSeconds;
        return;
    }

    if (blockedReasonAutoScrollOffset > 0f)
    {
        return;
    }

    blockedReasonAutoScrollOffset = 0f;
    blockedReasonAutoScrollDirection = 1;
    blockedReasonAutoScrollPauseUntil = now + Dialog_DiplomacyDialogue.BlockedReasonAutoScrollPauseSeconds;
}



internal void DrawBlockedReasonAutoScrollText(Rect rect, string content, float contentHeight)
{
    bool previousWordWrap = Text.WordWrap;
    TextAnchor previousAnchor = Text.Anchor;
    GUI.BeginGroup(rect);
    Text.WordWrap = true;
    Text.Anchor = TextAnchor.UpperLeft;
    Widgets.Label(new Rect(0f, -blockedReasonAutoScrollOffset, rect.width, contentHeight), content);
    Text.Anchor = previousAnchor;
    Text.WordWrap = previousWordWrap;
    GUI.EndGroup();
}



internal void ResetBlockedReasonAutoScroll(bool clearText)
{
    blockedReasonAutoScrollOffset = 0f;
    blockedReasonAutoScrollDirection = 1;
    blockedReasonAutoScrollPauseUntil = 0f;
    blockedReasonAutoScrollLastRealtime = -1f;
    if (clearText)
    {
        blockedReasonScrollText = string.Empty;
    }
}



internal bool DrawReinitiateActionButton(Rect inputAreaRect)
{
    Text.Font = GameFont.Tiny;
    string label = "↻ " + "RimChat_ReinitiateDialogueButton".Translate();
    float width = Mathf.Clamp(Text.CalcSize(label).x + 14f, 96f, 142f);
    Rect buttonRect = new Rect(inputAreaRect.xMax - width - 10f, inputAreaRect.y + inputAreaRect.height - 22f, width, 18f);

    float pulse = 0.65f + 0.35f * Mathf.Sin(Time.realtimeSinceStartup * 2.4f);
    Owner.Parts.MessageView.DrawRoundedRect(buttonRect, new Color(0.12f, 0.21f, 0.27f, 0.95f), 7f);
    GUI.color = new Color(0.42f, 0.78f, 0.98f, pulse);
    Widgets.DrawBox(buttonRect);
    GUI.color = Color.white;

    Text.Anchor = TextAnchor.MiddleCenter;
    Widgets.Label(buttonRect, label);
    Text.Anchor = TextAnchor.UpperLeft;
    TooltipHandler.TipRegion(buttonRect, "RimChat_ReinitiateDialogueButton".Translate());
    Text.Font = GameFont.Small;

    return Widgets.ButtonInvisible(buttonRect);
}



internal void DrawLockedInputPreview(Rect rect)
{
    _ = rect;
}



internal bool IsInputLockedByAiTurn(out string reason)



{



    return Composer.IsInputLockedByAiTurn(out reason);



}




internal bool IsWaitingForNpcTurn()
{
    if (session == null)
    {
        return false;
    }

    bool aiTurnOwnsInputHost = Owner.Parts.InputHost.IsAiTurnInputHostOwned();
    Owner.Parts.InputHost.RefreshInputHostReactivationBarrier(aiTurnOwnsInputHost);
    return aiTurnOwnsInputHost || Owner.Parts.InputHost.IsInputHostReactivationStabilizing();
}



internal bool HasActiveNpcTypewriter()



{



    return Composer.HasActiveNpcTypewriter();



}




internal void ShowSocialExpAnimation(int amount)
{
    lastExpAmount = amount;
    socialExpAnimStartTime = Time.time;
}



internal void HandleInputEvents(SendGateState sendGate)



{



    Composer.HandleInputEvents(sendGate);



}




internal bool TryHandleInputHistoryNavigation(Event current)



{



    return Composer.TryHandleInputHistoryNavigation(current);



}




internal void NavigateInputHistory(int direction)



{



    Composer.NavigateInputHistory(direction);



}




internal static bool IsSubmitKeyPressed(Event current)
{
    return current != null &&
        current.type == EventType.KeyDown &&
        (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter);
}



internal static bool IsImeComposing()
{
    return !string.IsNullOrEmpty(Input.compositionString);
}



internal static bool IsDialogueInputFocused()
{
    return GUI.GetNameOfFocusedControl() == Dialog_DiplomacyDialogue.DialogueInputControlName;
}



internal bool CanSendFromKeyboard(SendGateState sendGate)
{
    return !string.IsNullOrWhiteSpace(inputText) && sendGate.CanSendNow;
}



internal static void ShowBlockedSendFeedback(string blockedReason)
{
    if (string.IsNullOrWhiteSpace(blockedReason))
    {
        return;
    }

    Messages.Message(blockedReason, MessageTypeDefOf.RejectInput, false);
}



internal static Rect BuildInputStatusRect(Rect inputRect, float padding)
{
    float x = inputRect.x + padding + 110f;
    float rightInset = padding + 90f;
    float width = Mathf.Max(140f, inputRect.xMax - rightInset - x);
    return new Rect(x, inputRect.y + inputRect.height - 21f, width, 20f);
}



internal void SendMessage()
{
    if (session == null || string.IsNullOrWhiteSpace(inputText) || !Owner.Parts.Presence.CanSendMessageNow())
        return;

    string playerMessage = inputText.Trim();
    if (string.IsNullOrEmpty(playerMessage))
        return;

    ResetInputHistoryNavigation();
    inputText = "";
    Owner.Parts.MessageLayout.InvalidateLayoutCache();
    _typewriterDirty = true;
    Owner.Parts.Session.SendPreparedMessage(playerMessage, true);
}



internal void ResetInputHistoryNavigation()
{
    inputHistoryIndex = -1;
    inputHistoryDraft = string.Empty;
}



internal void RecordInputHistory(string playerMessage)
{
    if (string.IsNullOrWhiteSpace(playerMessage))
    {
        return;
    }

    inputHistory.Add(playerMessage);
    ResetInputHistoryNavigation();
}


internal const float InputHostReactivationStabilizationSeconds = 0.25f;
}

