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

internal sealed class DiplomacyMemorySyncCoordinator : DiplomacyDialogueCollaborator
{
    internal DiplomacyMemorySyncCoordinator(Dialog_DiplomacyDialogue owner) : base(owner) { }


internal bool isDiplomacyMemorySubscribed;


internal bool pendingDialogueMemoryRefresh;


internal int lastObservedDiplomacyMemoryRevision;



internal void SubscribeToDiplomacyMemoryChanges()
{
    if (isDiplomacyMemorySubscribed || LeaderMemoryManager.Instance == null)
    {
        return;
    }

    LeaderMemoryManager.Instance.DiplomacyMemoryChanged += OnDiplomacyMemoryChanged;
    lastObservedDiplomacyMemoryRevision = LeaderMemoryManager.Instance.GetFactionMemoryRevision(faction);
    isDiplomacyMemorySubscribed = true;
}



internal void UnsubscribeFromDiplomacyMemoryChanges()
{
    if (!isDiplomacyMemorySubscribed || LeaderMemoryManager.Instance == null)
    {
        return;
    }

    LeaderMemoryManager.Instance.DiplomacyMemoryChanged -= OnDiplomacyMemoryChanged;
    isDiplomacyMemorySubscribed = false;
}



internal void PollDiplomacyMemoryRevision()
{
    if (faction == null || LeaderMemoryManager.Instance == null)
    {
        return;
    }

    int currentRevision = LeaderMemoryManager.Instance.GetFactionMemoryRevision(faction);
    if (currentRevision > lastObservedDiplomacyMemoryRevision)
    {
        lastObservedDiplomacyMemoryRevision = currentRevision;
        pendingDialogueMemoryRefresh = true;
    }
}



internal void OnDiplomacyMemoryChanged(DiplomacyMemoryChangedEventArgs args)
{
    if (args == null || faction == null)
    {
        return;
    }

    string currentFactionId = faction.GetUniqueLoadID() ?? string.Empty;
    if (!string.Equals(currentFactionId, args.FactionId ?? string.Empty, StringComparison.Ordinal))
    {
        return;
    }

    lastObservedDiplomacyMemoryRevision = Math.Max(lastObservedDiplomacyMemoryRevision, args.Revision);
    pendingDialogueMemoryRefresh = true;

    if (args.AffectsAiPrompt && session != null && !string.IsNullOrEmpty(session.pendingRequestId))
    {
        conversationController.CancelPendingRequest(session);
    }
}



internal void ApplyPendingDiplomacyMemoryRefresh()
{
    if (!pendingDialogueMemoryRefresh)
    {
        return;
    }

    pendingDialogueMemoryRefresh = false;
    if (session == null)
    {
        RemoveStaleTypewriterStates(null);
        return;
    }

    Owner.Parts.Speakers.EnsureSessionMessageSpeakers(session);
    _typewriterDirty = true;
    RemoveStaleTypewriterStates(session);
    SyncTypewriterStatesWithSession(session);
    Owner.Parts.MessageLayout.InvalidateLayoutCache();
}



internal void SyncTypewriterStatesWithSession(FactionDialogueSession currentSession)
{
    if (currentSession?.messages == null)
    {
        return;
    }

    foreach (DialogueMessageData msg in currentSession.messages)
    {
        if (msg == null || msg.isPlayer || msg.IsSystemMessage())
        {
            continue;
        }

        if (!typewriterStates.TryGetValue(msg, out TypewriterState state))
        {
            state = new TypewriterState
            {
                FullText = msg.message ?? string.Empty,
                VisibleCharCount = 0,
                AccumulatedTime = 0f,
                IsComplete = false,
                DisplayText = string.Empty
            };
            typewriterStates[msg] = state;
            continue;
        }

        SyncTypewriterStateText(msg, state);
    }
}



internal void RemoveStaleTypewriterStates(FactionDialogueSession currentSession)
{
    if (typewriterStates == null || typewriterStates.Count == 0)
    {
        return;
    }

    if (currentSession?.messages == null)
    {
        typewriterStates.Clear();
        return;
    }

    List<DialogueMessageData> keys = new List<DialogueMessageData>(typewriterStates.Keys);

    for (int i = keys.Count - 1; i >= 0; i--)
    {
        DialogueMessageData key = keys[i];
        if (key == null)
        {
            typewriterStates.Remove(key);
            continue;
        }

        bool found = false;
        for (int j = 0; j < currentSession.messages.Count; j++)
        {
            if (ReferenceEquals(currentSession.messages[j], key))
            {
                found = true;
                break;
            }
        }

        if (!found)
        {
            typewriterStates.Remove(key);
        }
    }
}



internal static void SyncTypewriterStateText(DialogueMessageData msg, TypewriterState state)
{
    if (msg == null || state == null)
    {
        return;
    }

    string latestText = msg.message ?? string.Empty;
    if (string.Equals(state.FullText ?? string.Empty, latestText, StringComparison.Ordinal))
    {
        return;
    }

    state.FullText = latestText;
    state.VisibleCharCount = Math.Max(0, Math.Min(state.VisibleCharCount, latestText.Length));

    if (state.IsComplete)
    {
        state.VisibleCharCount = latestText.Length;
        state.DisplayText = latestText;
        return;
    }

    state.AccumulatedTime = Math.Max(0f, state.VisibleCharCount / 30f);
    state.DisplayText = state.VisibleCharCount > 0
        ? latestText.Substring(0, state.VisibleCharCount)
        : string.Empty;
    state.IsComplete = state.VisibleCharCount >= latestText.Length;
}
}
