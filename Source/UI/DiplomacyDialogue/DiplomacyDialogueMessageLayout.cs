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

internal sealed class DiplomacyDialogueMessageLayout : DiplomacyDialogueCollaborator
{
    internal DiplomacyDialogueMessageLayout(Dialog_DiplomacyDialogue owner) : base(owner) { }



internal readonly Dictionary<DialogueMessageData, MessageLayoutEntry> _layoutCache =
    new Dictionary<DialogueMessageData, MessageLayoutEntry>();



internal int _lastLayoutCacheVersion = -1;


internal float _lastLayoutCacheViewportWidth = -1f;


internal float _cachedTotalContentHeight = -1f;


internal bool _layoutCacheDirty = true;


internal DialogueMessageData _typewriterActiveMsg;



internal void InvalidateLayoutCache()
{
    _layoutCacheDirty = true;
    _cachedTotalContentHeight = -1f;
}



internal void MarkLayoutCacheClean()
{
    _layoutCacheDirty = false;
}



internal bool IsLayoutCacheValid(float viewportWidth)
{
    if (_layoutCacheDirty) return false;
    if (session == null || session.messages == null) return false;
    if (session.messageVersion != _lastLayoutCacheVersion) return false;
    if (Mathf.Abs(viewportWidth - _lastLayoutCacheViewportWidth) > 0.5f) return false;
    return true;
}



internal void RebuildLayoutCache(float viewportWidth)
{
    _layoutCache.Clear();

    if (session == null || session.messages == null)
    {
        _lastLayoutCacheVersion = 0;
        _lastLayoutCacheViewportWidth = viewportWidth;
        _cachedTotalContentHeight = 20f;
        MarkLayoutCacheClean();
        return;
    }

    float maxSystemWidth = Owner.Parts.Speakers.GetMaxSystemMessageWidth(viewportWidth);
    float maxBubbleWidth = Owner.Parts.Speakers.GetMaxBubbleWidth(viewportWidth);
    float contentHeight = 10f;
    DialogueMessageData prevMsg = null;

    for (int i = 0; i < session.messages.Count; i++)
    {
        var msg = session.messages[i];
        if (prevMsg != null && Owner.Parts.MessageView.ShouldShowTimeGap(prevMsg.GetGameTick(), msg.GetGameTick()))
        {
            contentHeight += 35f;
        }

        float maxW = msg.IsSystemMessage() ? maxSystemWidth : maxBubbleWidth;
        float bubbleWidth = Owner.Parts.MessageView.CalculateBubbleWidth(msg, maxW);
        float msgHeight = Owner.Parts.MessageView.CalculateMessageHeight(msg, bubbleWidth);

        _layoutCache[msg] = new MessageLayoutEntry
        {
            BubbleWidth = bubbleWidth,
            MessageHeight = msgHeight,
            CachedMessageHash = (msg.message ?? "").GetHashCode(),
            CachedVisibleChars = GetTypewriterVisibleChars(msg)
        };

        contentHeight += msgHeight + DiplomacyDialogueMessageView.ResolveMessageBottomGap(msg);
        prevMsg = msg;
    }

    contentHeight += 6f;
    _lastLayoutCacheVersion = session.messageVersion;
    _lastLayoutCacheViewportWidth = viewportWidth;
    _cachedTotalContentHeight = contentHeight;
    MarkLayoutCacheClean();
}



internal void UpdateTypewriterLayoutEntry(float viewportWidth)
{
    if (_typewriterActiveMsg == null || session == null) return;

    if (!_layoutCache.TryGetValue(_typewriterActiveMsg, out MessageLayoutEntry entry)) return;

    int currentHash = (_typewriterActiveMsg.message ?? "").GetHashCode();
    int currentVisibleChars = GetTypewriterVisibleChars(_typewriterActiveMsg);

    if (entry.CachedMessageHash == currentHash && entry.CachedVisibleChars == currentVisibleChars)
    {
        return;
    }

    float maxSystemWidth = Owner.Parts.Speakers.GetMaxSystemMessageWidth(viewportWidth);
    float maxBubbleWidth = Owner.Parts.Speakers.GetMaxBubbleWidth(viewportWidth);
    float maxW = _typewriterActiveMsg.IsSystemMessage() ? maxSystemWidth : maxBubbleWidth;
    float newBubbleWidth = Owner.Parts.MessageView.CalculateBubbleWidth(_typewriterActiveMsg, maxW);
    float newMsgHeight = Owner.Parts.MessageView.CalculateMessageHeight(_typewriterActiveMsg, newBubbleWidth);

    float oldMsgHeight = entry.MessageHeight;
    float heightDelta = newMsgHeight - oldMsgHeight;

    _layoutCache[_typewriterActiveMsg] = new MessageLayoutEntry
    {
        BubbleWidth = newBubbleWidth,
        MessageHeight = newMsgHeight,
        CachedMessageHash = currentHash,
        CachedVisibleChars = currentVisibleChars
    };

    if (Mathf.Abs(heightDelta) > 0.01f)
    {
        _cachedTotalContentHeight += heightDelta;
    }
}



internal bool TryGetCachedLayout(DialogueMessageData msg, out MessageLayoutEntry entry)
{
    if (!_layoutCache.TryGetValue(msg, out entry)) return false;

    int currentHash = (msg.message ?? "").GetHashCode();
    if (entry.CachedMessageHash != currentHash) return false;

    int currentVisibleChars = GetTypewriterVisibleChars(msg);
    if (currentVisibleChars != entry.CachedVisibleChars) return false;

    return true;
}



internal int GetTypewriterVisibleChars(DialogueMessageData msg)
{
    if (msg.isPlayer || msg.IsSystemMessage()) return -1;
    if (typewriterStates.TryGetValue(msg, out TypewriterState state) && !state.IsComplete)
    {
        return state.VisibleCharCount;
    }
    return -1;
}



internal void EnsureLayoutCache(float viewportWidth)
{
    if (IsLayoutCacheValid(viewportWidth))
    {
        UpdateTypewriterLayoutEntry(viewportWidth);
        return;
    }
    RebuildLayoutCache(viewportWidth);
}



internal void PreFillTypewriterStatesForExistingMessages()
{
    if (session?.messages == null) return;

    for (int i = 0; i < session.messages.Count; i++)
    {
        var msg = session.messages[i];
        if (msg == null || msg.isPlayer || msg.IsSystemMessage()) continue;

        string text = msg.message ?? string.Empty;
        typewriterStates[msg] = new TypewriterState
        {
            FullText = text,
            VisibleCharCount = text.Length,
            AccumulatedTime = text.Length / 30f,
            IsComplete = true,
            DisplayText = text
        };
    }
}
}
