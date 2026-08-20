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

internal sealed class DiplomacyDialogueInputComposer : DiplomacyDialogueCollaborator
{
    readonly DiplomacyDialogueInput Input;

    internal DiplomacyDialogueInputComposer(DiplomacyDialogueInput input) : base(input.Owner)
    {
        Input = input;
    }


internal void DrawInputArea(Rect rect)
{
    Widgets.DrawBoxSolid(rect, new Color(0.12f, 0.12f, 0.15f));

    float padding = 8f;
    float inputWidth = rect.width - padding * 2f - 90f;
    float inputHeight = rect.height - padding * 2f - 14f;

    Rect textRect = new Rect(rect.x + padding, rect.y + padding, inputWidth, inputHeight);
    
    Widgets.DrawBoxSolid(textRect, new Color(0.18f, 0.18f, 0.22f));
    Rect innerTextRect = textRect.ContractedBy(5f);

    SendGateState sendGate = Owner.Parts.Presence.EvaluateSendGate();
    bool inputBlocked = Owner.Parts.InputHost.ShouldRenderInputAsReadOnly(sendGate);
    string blockedReason = sendGate.BlockedReason;

    if (inputBlocked && DiplomacyDialogueInput.IsDialogueInputFocused())
    {
        // Drop IME focus immediately while AI is still producing content.
        GUI.FocusControl(null);
    }

    if (!inputBlocked)
    {
        HandleInputEvents(sendGate);
    }

    string newInput;
    if (inputBlocked)
    {
        newInput = inputText;
        Input.DrawLockedInputPreview(innerTextRect);
    }
    else
    {
        GUI.SetNextControlName(Dialog_DiplomacyDialogue.DialogueInputControlName);
        newInput = Widgets.TextArea(innerTextRect, inputText);
    }

    if (!inputBlocked && newInput.Length <= Dialog_DiplomacyDialogue.MAX_INPUT_LENGTH)
    {
        inputText = newInput;
    }

    int charCount = inputText.Length;
    Color countColor = charCount > Dialog_DiplomacyDialogue.MAX_INPUT_LENGTH * 0.8f ? Color.yellow : Color.gray;
    GUI.color = countColor;
    Text.Font = GameFont.Tiny;
    Rect countRect = new Rect(rect.x + padding, rect.y + rect.height - 20f, 100f, 18f);
    Text.Anchor = TextAnchor.MiddleLeft;
    Widgets.Label(countRect, $"{charCount}/{MAX_INPUT_LENGTH}");
    Text.Anchor = TextAnchor.UpperLeft;
    Text.Font = GameFont.Small;
    GUI.color = Color.white;

    Rect sendRect = new Rect(rect.xMax - 85f, rect.y + padding, 75f, inputHeight);
    bool canSend = !string.IsNullOrWhiteSpace(inputText) && charCount <= Dialog_DiplomacyDialogue.MAX_INPUT_LENGTH && sendGate.CanSendNow;

    Color buttonColor = canSend ? new Color(0.2f, 0.6f, 1f, 0.9f) : new Color(0.3f, 0.3f, 0.35f, 0.5f);
    GUI.color = buttonColor;
    Widgets.DrawBoxSolid(sendRect, buttonColor);
    GUI.color = Color.white;

    GUI.enabled = canSend;
    if (Widgets.ButtonText(sendRect, "RimChat_SendButton".Translate()))
    {
        Input.SendMessage();
    }
    GUI.enabled = true;
    DrawSendInfoEntry(sendRect, sendGate);

    bool conversationEnded = session?.isConversationEndedByNpc ?? false;
    if (conversationEnded && sendGate.IsHardBlocked)
    {
        Rect blockedRect = DiplomacyDialogueInput.BuildInputStatusRect(rect, padding);
        GUI.color = new Color(1f, 0.6f, 0.6f, 0.9f);
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleLeft;
        Input.DrawStatusLabelWithVerticalScroll(blockedRect, blockedReason ?? "RimChat_ConversationEnded".Translate());
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        GUI.color = Color.white;
    }
    else if (sendGate.IsSoftBlocked)
    {
        if (Input.IsWaitingForNpcTurn())
        {
            Input.ResetBlockedReasonAutoScroll(true);
            Rect typingRect = new Rect(rect.x + padding + 60f, rect.y + rect.height - 22f, 320f, 20f);
            Owner.Parts.Typing.DrawDiplomacyTypingStatus(typingRect);
        }
        else
        {
            Rect blockedRect = DiplomacyDialogueInput.BuildInputStatusRect(rect, padding);
            GUI.color = new Color(1f, 0.85f, 0.5f, 0.95f);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            Input.DrawStatusLabelWithVerticalScroll(blockedRect, blockedReason ?? "RimChat_DiplomacyInputLockedByTyping".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
        }
    }
    else if (session != null && !string.IsNullOrEmpty(session.aiError))
    {
        Input.ResetBlockedReasonAutoScroll(true);
        Rect errorRect = new Rect(rect.x + padding + 110f, rect.y + rect.height - 20f, 240f, 18f);
        GUI.color = Color.red;
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleLeft;
        string errorLabel = "RimChat_ErrorLabel".Translate();
        DiplomacyDialogueInput.DrawSingleLineClippedLabel(errorRect, $"{errorLabel}: " + session.aiError.Substring(0, Mathf.Min(30, session.aiError.Length)));
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        GUI.color = Color.white;
    }
    else if (inputBlocked)
    {
        Rect blockedRect = DiplomacyDialogueInput.BuildInputStatusRect(rect, padding);
        GUI.color = new Color(1f, 0.6f, 0.6f, 0.9f);
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleLeft;
        Input.DrawStatusLabelWithVerticalScroll(blockedRect, blockedReason ?? "RimChat_PresenceBlockedOffline".Translate());
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        GUI.color = Color.white;
    }
    else if (Owner.Parts.AirdropAsync.TryBuildAirdropAsyncStatusText(out string airdropStatusText))
    {
        Input.ResetBlockedReasonAutoScroll(true);
        Rect pendingRect = new Rect(rect.x + padding + 110f, rect.y + rect.height - 20f, 360f, 18f);
        GUI.color = new Color(0.62f, 0.85f, 1f, 0.95f);
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleLeft;
        DiplomacyDialogueInput.DrawSingleLineClippedLabel(pendingRect, airdropStatusText);
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        GUI.color = Color.white;
    }
    else
    {
        Input.ResetBlockedReasonAutoScroll(true);
    }

    if (sendGate.IsHardBlocked && sendGate.ShowReinitiateButton)
    {
        if (Input.DrawReinitiateActionButton(rect))
        {
            Owner.Parts.Presence.ReinitiateConversation();
        }
    }

    if (Time.time - socialExpAnimStartTime < 2f && negotiator != null)
    {
        float progress = (Time.time - socialExpAnimStartTime) / 2f;
        float alpha = progress < 0.2f ? progress * 5f : (1f - (progress - 0.2f) / 0.8f);
        float yOffset = progress * 40f;
        
        Rect expRect = new Rect(rect.xMax - 180f, rect.y - 15f - yOffset, 170f, 25f);
        
        GUI.color = new Color(0.9f, 0.8f, 0.2f, alpha);
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleRight;
        Widgets.Label(expRect, "RimChat_SocialExpGained".Translate(negotiator.LabelShort, lastExpAmount));
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        GUI.color = Color.white;
    }
}


internal void OpenSendInfoMenu()
{
    ActionValidationResult visitorValidation = Owner.Parts.SendInfo.ValidateManualVisitorEntry();
    ActionValidationResult airdropValidation = Owner.Parts.SendInfo.ValidateManualAirdropTradeEntry();
    ActionValidationResult prisonerValidation = Owner.Parts.SendInfo.ValidateManualPrisonerInfoEntry();
    List<ManualQuestRequestOption> questOptions = Owner.Parts.SendInfo.BuildManualQuestRequestOptions();
    string visitorLabel = "RimChat_SendInfoMenuRequestVisitor".Translate().ToString();
    string airdropLabel = "RimChat_SendInfoMenuAirdropTrade".Translate().ToString();
    string prisonerLabel = "RimChat_SendInfoMenuPrisoner".Translate().ToString();
    bool hasQuestOptions = questOptions.Count > 0;
    var options = new List<FloatMenuOption>
    {
        new FloatMenuOption(
            "RimChat_SendInfoMenuRequestCaravan".Translate(),
            Owner.Parts.SendInfo.TryStartManualCaravanRequestSend),
        new FloatMenuOption(
            DiplomacySendInfoWorkflow.BuildManualAirdropTradeMenuLabel(visitorLabel, visitorValidation),
            visitorValidation != null && !visitorValidation.Allowed ? null : (Action)Owner.Parts.SendInfo.TryStartManualVisitorRequestSend),
        new FloatMenuOption(
            "RimChat_SendInfoMenuRequestSupport".Translate(),
            Owner.Parts.SendInfo.TryStartManualSupportRequestSend),
        new FloatMenuOption(
            "RimChat_SendInfoMenuRequestQuest".Translate(),
            hasQuestOptions ? (Action)Owner.Parts.SendInfo.TryStartManualQuestRequestSend : null),
        new FloatMenuOption(
            DiplomacySendInfoWorkflow.BuildManualAirdropTradeMenuLabel(airdropLabel, airdropValidation),
            airdropValidation != null && !airdropValidation.Allowed ? null : (Action)Owner.Parts.SendInfo.TryStartManualAirdropTradeSend),
        new FloatMenuOption(
            DiplomacySendInfoWorkflow.BuildManualAirdropTradeMenuLabel(prisonerLabel, prisonerValidation),
            prisonerValidation != null && !prisonerValidation.Allowed ? null : (Action)Owner.Parts.RansomSelect.TryStartManualPrisonerInfoSend),
        new FloatMenuOption(
            "RimChat_SendInfoMenuTaunt".Translate(),
            Owner.Parts.SendInfo.TryStartManualTauntSend),
        new FloatMenuOption(
            "RimChat_SendInfoMenuEndConversation".Translate(),
            Input.TryEndConversation),
        Owner.Parts.QuickActions.BuildQuickMakePeaceMenuOption(),
        Owner.Parts.QuickActions.BuildQuickDeclareWarMenuOption()
    };

    Find.WindowStack.Add(new FloatMenu(options));
}


internal void DrawSendInfoEntry(Rect sendRect, SendGateState sendGate)
{
    Rect entryRect = new Rect(sendRect.x, sendRect.yMax + 2f, sendRect.width, 16f);
    bool canOpen = sendGate.CanSendNow;
    bool hovered = Mouse.IsOver(entryRect);
    Color textColor = canOpen
        ? (hovered ? new Color(0.68f, 0.9f, 1f, 0.95f) : new Color(0.56f, 0.82f, 0.95f, 0.88f))
        : new Color(0.58f, 0.6f, 0.66f, 0.7f);

    Text.Font = GameFont.Tiny;
    Text.Anchor = TextAnchor.MiddleCenter;
    GUI.color = textColor;
    Widgets.Label(entryRect, "RimChat_SendInfoEntry".Translate());
    GUI.color = Color.white;
    Text.Anchor = TextAnchor.UpperLeft;
    Text.Font = GameFont.Small;

    if (hovered)
    {
        string actionsTooltip = Owner.Parts.ActionHint.GetPotentialActionsTooltipText();
        if (!string.IsNullOrWhiteSpace(actionsTooltip))
        {
            TooltipHandler.TipRegion(entryRect, actionsTooltip);
        }
    }
    else if (!canOpen && !string.IsNullOrWhiteSpace(sendGate.BlockedReason))
    {
        TooltipHandler.TipRegion(entryRect, sendGate.BlockedReason);
    }

    if (canOpen && Widgets.ButtonInvisible(entryRect))
    {
        OpenSendInfoMenu();
    }
}


internal void HandleInputEvents(SendGateState sendGate)
{
    Event current = Event.current;
    if (current == null || !DiplomacyDialogueInput.IsDialogueInputFocused() || DiplomacyDialogueInput.IsImeComposing())
    {
        return;
    }

    if (TryHandleInputHistoryNavigation(current))
    {
        return;
    }

    if (!DiplomacyDialogueInput.IsSubmitKeyPressed(current))
    {
        return;
    }

    if (current.alt)
    {
        inputText += "\n";
        current.Use();
        return;
    }

    if (!Input.CanSendFromKeyboard(sendGate))
    {
        current.Use();
        DiplomacyDialogueInput.ShowBlockedSendFeedback(sendGate.BlockedReason);
        return;
    }

    current.Use();
    Input.SendMessage();
}


internal void NavigateInputHistory(int direction)
{
    if (inputHistory.Count == 0)
    {
        return;
    }

    if (direction < 0)
    {
        if (inputHistoryIndex < 0)
        {
            inputHistoryDraft = inputText;
            inputHistoryIndex = inputHistory.Count - 1;
        }
        else if (inputHistoryIndex > 0)
        {
            inputHistoryIndex--;
        }

        inputText = inputHistory[inputHistoryIndex];
        return;
    }

    if (direction > 0)
    {
        if (inputHistoryIndex < 0)
        {
            return;
        }

        if (inputHistoryIndex < inputHistory.Count - 1)
        {
            inputHistoryIndex++;
            inputText = inputHistory[inputHistoryIndex];
            return;
        }

        inputHistoryIndex = -1;
        inputText = inputHistoryDraft ?? string.Empty;
        inputHistoryDraft = string.Empty;
    }
}


internal bool TryHandleInputHistoryNavigation(Event current)
{
    if (current.type != EventType.KeyDown)
    {
        return false;
    }

    if (current.keyCode == KeyCode.UpArrow)
    {
        NavigateInputHistory(-1);
        current.Use();
        return true;
    }

    if (current.keyCode == KeyCode.DownArrow)
    {
        NavigateInputHistory(1);
        current.Use();
        return true;
    }

    return false;
}


internal bool HasActiveNpcTypewriter()
{
    if (typewriterStates == null || typewriterStates.Count == 0)
    {
        return false;
    }

    foreach (var pair in typewriterStates)
    {
        DialogueMessageData message = pair.Key;
        TypewriterState state = pair.Value;
        if (message == null || state == null || state.IsComplete)
        {
            continue;
        }

        if (message.isPlayer || message.IsSystemMessage())
        {
            continue;
        }

        return true;
    }

    return false;
}


internal bool IsInputLockedByAiTurn(out string reason)
{
    reason = null;
    if (session == null)
    {
        return false;
    }

    bool aiTurnOwnsInputHost = Owner.Parts.InputHost.IsAiTurnInputHostOwned();
    Owner.Parts.InputHost.RefreshInputHostReactivationBarrier(aiTurnOwnsInputHost);

    if (aiTurnOwnsInputHost || Owner.Parts.InputHost.IsInputHostReactivationStabilizing())
    {
        reason = Owner.Parts.InputHost.BuildAiTurnInputLockReason();
        return true;
    }

    if (conversationController.IsRequestDebounced(session))
    {
        reason = "RimChat_WaitingForResponse".Translate();
        return true;
    }

    return false;
}
}
