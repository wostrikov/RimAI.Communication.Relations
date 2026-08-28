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

internal sealed class DiplomacyDialoguePresenter : DiplomacyDialogueCollaborator
{
    internal DiplomacyDialoguePresenter(Dialog_DiplomacyDialogue owner) : base(owner) { }


internal void BindActiveFactionState(
    Faction targetFaction,
    DialogueRuntimeContext targetRuntimeContext = null,
    string lifecycleKey = null)
{
    long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
    Owner.Parts.Speakers.sessionFallbackFactionSpeaker = null;
    faction = targetFaction;
    runtimeContext = targetRuntimeContext ?? DialogueRuntimeContext.CreateDiplomacy(targetFaction, negotiator, negotiator?.Map);
    windowLifecycleKey = string.IsNullOrWhiteSpace(lifecycleKey)
        ? runtimeContext.WindowKey
        : lifecycleKey.Trim();

    session = GameComponent_DiplomacyManager.Instance?.GetOrCreateSession(targetFaction);
    long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
    if (session != null)
    {
        session.MarkAsRead();
        Owner.Parts.Speakers.EnsureSessionMessageSpeakers(session);
    }
    long t2 = System.Diagnostics.Stopwatch.GetTimestamp();
    _typewriterDirty = true;
    Owner.Parts.MessageLayout.InvalidateLayoutCache();
    Owner.Parts.MessageLayout.PreFillTypewriterStatesForExistingMessages();

    Owner.Parts.MemorySync.lastObservedDiplomacyMemoryRevision = LeaderMemoryManager.Instance?.GetFactionMemoryRevision(targetFaction) ?? 0;
    long t3 = System.Diagnostics.Stopwatch.GetTimestamp();
    Owner.Parts.MemorySync.pendingDialogueMemoryRefresh = true;
    sessionMessageBaselineCount = session?.messages?.Count ?? 0;
    sessionCloseSummaryCommitted = false;
    double freq = System.Diagnostics.Stopwatch.Frequency;
    double ms1 = (t1 - startTicks) * 1000.0 / freq;
    double ms2 = (t2 - t1) * 1000.0 / freq;
    double ms3 = (t3 - t2) * 1000.0 / freq;
    ModuleLog.Message($"[RimAI.Relations][PerfDiag] BindState: session={ms1:F1}ms, speakers={ms2:F1}ms, memory={ms3:F1}ms, msgs={session?.messages?.Count ?? 0}");
}


internal void ResetWindowUiStateForFactionSwitch()
{
    GUI.FocusControl(null);
    inputText = string.Empty;
    messageScrollPosition = Vector2.zero;
    lastMessageCount = 0;
    userIsScrolling = false;
    typewriterStates.Clear();
    lastTypewriterUpdate = 0f;
    _typewriterDirty = true;
    Owner.Parts.MessageLayout._layoutCache.Clear();
    Owner.Parts.MessageLayout._layoutCacheDirty = true;
    Owner.Parts.MessageLayout.PreFillTypewriterStatesForExistingMessages();
    DiplomacyDialogueImageCache.ClearInlineImageTextureCache();
    Owner.Parts.Input.ResetBlockedReasonAutoScroll(true);
    Owner.Parts.Speakers.sessionFallbackFactionSpeaker = null;
    Owner.Parts.InputHost.inputHostBlockedUntilRealtime = -1f;

    Owner.Parts.SocialView.currentMainTab = DialogueMainTab.Chat;
    Owner.Parts.SocialView.socialPostScrollPosition = Vector2.zero;
    Owner.Parts.SocialView.socialCategoryFilter = null;
    Owner.Parts.SocialView.socialReadMarked = false;
    Owner.Parts.SocialView.socialToast = string.Empty;
    Owner.Parts.SocialView.socialToastUntil = -100f;

    Owner.Parts.StrategyUi.strategyBarAnimProgress = 0f;
    Owner.Parts.StrategyUi.strategySuggestionRequestPending = false;
    Owner.Parts.StrategyUi.strategySuggestionRequestId = null;
    Owner.Parts.StrategyUi.strategyFxSignature = 0;
    Owner.Parts.StrategyUi.strategyFxStartRealtime = -99f;

    // Per-faction tooltip cache persists across switches; no need to invalidate
    Owner.Parts.Airdrop.ClearPendingAirdropDialogState("switch_faction", true);
}


internal bool SwitchFactionInPlace(Faction targetFaction)
{
    if (targetFaction == null || targetFaction == faction || targetFaction.defeated)
    {
        return false;
    }

    long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
    Owner.Parts.StrategyRequest.CancelStrategySuggestionRequest();
    Owner.Parts.AirdropAsync.CancelPendingAirdropSelectionRequest();
    Owner.Parts.Session.TryCommitDiplomacySessionSummaryOnClose();
    long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
    Owner.Parts.Presence.LockPresenceCacheOnDialogueClose();

    BindActiveFactionState(targetFaction);
    long t2 = System.Diagnostics.Stopwatch.GetTimestamp();
    ResetWindowUiStateForFactionSwitch();
    Owner.Parts.Presence.RefreshPresenceOnDialogueOpen();
    long t3 = System.Diagnostics.Stopwatch.GetTimestamp();

    double freq = System.Diagnostics.Stopwatch.Frequency;
    double ms1 = (t1 - startTicks) * 1000.0 / freq;
    double ms2 = (t2 - t1) * 1000.0 / freq;
    double ms3 = (t3 - t2) * 1000.0 / freq;
    double total = (t3 - startTicks) * 1000.0 / freq;
    ModuleLog.Message($"[RimAI.Relations] Switched to {targetFaction.Name}: summary={ms1:F1}ms, bind={ms2:F1}ms, presence={ms3:F1}ms, total={total:F1}ms");
    return true;
}


       internal void OnGoodwillChanged(Faction changedFaction, int changeAmount)
       {
           if (changedFaction == null) return;
           goodwillValueRevealUntil[changedFaction] = Time.realtimeSinceStartup + Dialog_DiplomacyDialogue.GOODWILL_VALUE_REVEAL_SECONDS;

           if (factionRowRects.TryGetValue(changedFaction, out Rect rowRect))
           {
               Vector2 startPos = new Vector2(
                   rowRect.x + Dialog_DiplomacyDialogue.LayoutGoodwillAnimOffsetX,
                   rowRect.y + Dialog_DiplomacyDialogue.LayoutGoodwillAnimOffsetY
               );

               GoodwillChangeAnimator.CreateAnimation(changedFaction, changeAmount, startPos);
           }
       }


internal void MarkCloseAsFactionSwitch()
{
    closeIntent = DialogueCloseIntent.SwitchFaction;
}


internal bool IsSwitchingFactionOnClose()
{
    return closeIntent == DialogueCloseIntent.SwitchFaction;
}
}
