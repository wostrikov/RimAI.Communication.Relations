using System;
using System.Collections.Generic;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Dialogue;
using Ustas.RimAI.Communication.Relations.Memory;
using RimWorld;
using UnityEngine;
using Verse;

using GroupChatParticipant = Ustas.RimAI.Communication.Relations.UI.Dialog_RPGPawnGroupChat.GroupChatParticipant;
using GroupTurnRecord = Ustas.RimAI.Communication.Relations.UI.Dialog_RPGPawnGroupChat.GroupTurnRecord;
using DialoguePage = Ustas.RimAI.Communication.Relations.UI.Dialog_RPGPawnGroupChat.DialoguePage;

namespace Ustas.RimAI.Communication.Relations.UI
{
        internal sealed class RPGPawnGroupChatFlowControl : Dialog_RPGPawnGroupChatCollaborator
    {
        internal RPGPawnGroupChatFlowControl(Dialog_RPGPawnGroupChat owner) : base(owner)
        {
        }


        // Serial queue: each pawn's response is cached; next request fires immediately after current arrives
        internal readonly Dictionary<int, string> _cachedResponses = new Dictionary<int, string>();
        internal int _queuedRequestIndex = -1; // next pawn index to request

        // ── Start a round: queue first request ──

        internal void StartRound()
        {
            _cachedResponses.Clear();
            currentSpeakerIndex = 0;
            isPlayerTurn = false;
            Owner.ResetRoundFlags();

            Owner.SkipInvalidPawnsForward();
            if (currentSpeakerIndex >= participants.Count)
            {
                Owner.TransitionToPlayerTurn();
                return;
            }

            // Fire first request immediately
            isSendingRequest = true;
            _queuedRequestIndex = currentSpeakerIndex;
            Owner.SendSerialRequest(currentSpeakerIndex);
        }

        internal void SendSerialRequest(int pawnIndex)
        {
            if (pawnIndex < 0 || pawnIndex >= participants.Count) return;

            var speaker = participants[pawnIndex];
            List<ChatMessageData> requestMessages;
            try
            {
                requestMessages = Owner.BuildGroupRequestMessages(speaker, isFirstTurn: turnRecords.Count == 0);
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAI.Relations] Request build failed for {speaker.DisplayName}: {ex.Message}");
                _cachedResponses[pawnIndex] = "RimChat_GroupConverse_Skipped".Translate(speaker.DisplayName);
                Owner.OnResponseReceived(pawnIndex);
                return;
            }

            conversationController.TrySend(
                runtimeContext.WithCurrentRuntimeMarkers(),
                windowInstanceId,
                requestMessages,
                onReady: envelope =>
                {
                    if (isWindowClosing) return;
                    string text = envelope?.DialogueText ?? "";
                    int js = text.LastIndexOf("{\"actions\"");
                    if (js >= 0) text = text.Substring(0, js).TrimEnd();
                    if (string.IsNullOrWhiteSpace(text)) text = "…";
                    _cachedResponses[pawnIndex] = text;
                    if (envelope?.Actions != null && envelope.Actions.Count > 0)
                        Owner.ExecuteActionsForSpeaker(speaker, envelope.Actions);
                    Owner.OnResponseReceived(pawnIndex);
                },
                onError: error =>
                {
                    if (isWindowClosing) return;
                    _cachedResponses[pawnIndex] = "RimChat_GroupConverse_Error".Translate(speaker.DisplayName, error);
                    Owner.OnResponseReceived(pawnIndex);
                },
                onDropped: reason =>
                {
                    if (isWindowClosing) return;
                    _cachedResponses[pawnIndex] = "RimChat_DialogueResponseDropped".Translate(reason ?? "unknown");
                    Owner.OnResponseReceived(pawnIndex);
                });
        }

        // Called when a serial response arrives. Cache it, then queue the next request.
        internal void OnResponseReceived(int pawnIndex)
        {
            // Queue the NEXT pawn's request immediately (serial, don't wait for click)
            int nextIdx = pawnIndex + 1;
            while (nextIdx < participants.Count)
            {
                var c = participants[nextIdx];
                if (c.Pawn != null && !c.Pawn.Dead && !c.Pawn.Destroyed && c.Pawn.Spawned)
                    break;
                nextIdx++;
            }
            if (nextIdx < participants.Count && !_cachedResponses.ContainsKey(nextIdx))
            {
                _queuedRequestIndex = nextIdx;
                Owner.SendSerialRequest(nextIdx);
            }

            // Only auto-display if we're waiting for this speaker AND not showing player text
            if (isShowingPlayerText) return;
            if (isPlayerTurn) return;
            if (pawnIndex != currentSpeakerIndex) return;

            // This is the current speaker — display immediately
            isSendingRequest = false;
            string text = _cachedResponses[pawnIndex];
            currentDialogueText = text;
            displayedText = "";
            visibleChars = 0;
            isTyping = true;
            lastCharTime = Time.realtimeSinceStartup;
            pauseForClick = false;

            var sp = participants[pawnIndex];
            turnRecords.Add(new GroupTurnRecord { SpeakerPawnId = sp.PawnId, SpeakerName = sp.DisplayName, DialogueText = text, IsPlayer = false });
            dialogPages.Add(new DialoguePage { speakerName = sp.DisplayName, text = text });

            if (!string.IsNullOrWhiteSpace(text) && text != "…")
                RpgDialogueTraceTracker.RegisterTurn(initiator, sp.Pawn, false, text, dialogueSessionId);
        }

        // ── Constructor entry ──

        internal void SendFirstSpeakerRequest()
        {
            if (participants.Count == 0) return;
            currentRound = 1;
            Owner.StartRound();
        }

        // ── Advance (click-to-continue) → next cached speaker ──

        internal void AdvanceToNextSpeaker()
        {
            if (isPlayerTurn) return;

            int nextIdx = currentSpeakerIndex + 1;
            while (nextIdx < participants.Count)
            {
                var c = participants[nextIdx];
                if (c.Pawn != null && !c.Pawn.Dead && !c.Pawn.Destroyed && c.Pawn.Spawned)
                    break;
                nextIdx++;
            }

            if (nextIdx >= participants.Count)
            {
                Owner.TransitionToPlayerTurn();
                return;
            }

            currentSpeakerIndex = nextIdx;

            if (_cachedResponses.TryGetValue(nextIdx, out string text))
            {
                // Already cached — display immediately
                currentDialogueText = text;
                displayedText = "";
                visibleChars = 0;
                isTyping = true;
                lastCharTime = Time.realtimeSinceStartup;
                pauseForClick = false;

                var sp = participants[nextIdx];
                turnRecords.Add(new GroupTurnRecord { SpeakerPawnId = sp.PawnId, SpeakerName = sp.DisplayName, DialogueText = text, IsPlayer = false });
                dialogPages.Add(new DialoguePage { speakerName = sp.DisplayName, text = text });

                if (!string.IsNullOrWhiteSpace(text) && text != "…")
                    RpgDialogueTraceTracker.RegisterTurn(initiator, sp.Pawn, false, text, dialogueSessionId);
            }
            else
            {
                // Still waiting — show loading
                isSendingRequest = true;
                currentDialogueText = "";
                displayedText = "";
                visibleChars = 0;
                isTyping = false;
                pauseForClick = false;
            }
        }

        // ── Per-frame: check if pending response arrived for current speaker ──

        internal void CheckPendingResponse()
        {
            if (isShowingPlayerText || isPlayerTurn) return;
            int idx = currentSpeakerIndex;
            if (idx < 0 || idx >= participants.Count) return;
            if (!_cachedResponses.TryGetValue(idx, out string text)) return;
            // Only act if we're still waiting (sending OR showing stale "…")
            if (!isSendingRequest && !string.IsNullOrEmpty(currentDialogueText) && currentDialogueText != "…") return;

            isSendingRequest = false;
            currentDialogueText = text;
            displayedText = "";
            visibleChars = 0;
            isTyping = true;
            lastCharTime = Time.realtimeSinceStartup;

            var sp = participants[idx];
            turnRecords.Add(new GroupTurnRecord { SpeakerPawnId = sp.PawnId, SpeakerName = sp.DisplayName, DialogueText = text, IsPlayer = false });
            dialogPages.Add(new DialoguePage { speakerName = sp.DisplayName, text = text });

            if (!string.IsNullOrWhiteSpace(text) && text != "…")
                RpgDialogueTraceTracker.RegisterTurn(initiator, sp.Pawn, false, text, dialogueSessionId);
        }

        // ── Player turn ──

        internal void TransitionToPlayerTurn()
        {
            isPlayerTurn = true;
            currentSpeakerIndex = -1;
            pauseForClick = false;
            currentDialogueText = "";
            displayedText = "";
            visibleChars = 0;
            isSendingRequest = false;
        }

        internal void TrySendPlayerMessage()
        {
            if (!isPlayerTurn || string.IsNullOrWhiteSpace(userReplyText)) return;

            string textToSend = userReplyText.Trim();
            userReplyText = "";
            GUI.FocusControl(null);

            turnRecords.Add(new GroupTurnRecord { SpeakerPawnId = initiator.GetUniqueLoadID(), SpeakerName = initiator.LabelShort, DialogueText = textToSend, IsPlayer = true });
            dialogPages.Add(new DialoguePage { speakerName = initiator.LabelShort, text = textToSend });

            foreach (var p in participants)
                RpgDialogueTraceTracker.RegisterTurn(initiator, p.Pawn, true, textToSend, dialogueSessionId);

            currentDialogueText = textToSend;
            displayedText = "";
            visibleChars = 0;
            isTyping = true;
            isShowingPlayerText = true;
            isWaitingForPlayerDelay = false;
            isPlayerTurn = false;
            isViewingHistory = false;
            nextSpeakerRequested = false;
            currentRound++;
            Owner.ResetRoundFlags();

            // Start new round: serial queue from first pawn
            Owner.StartRound();
        }

        // ── Player text → first NPC (from serial queue) ──

        internal void CheckPlayerTextTransition()
        {
            if (!isShowingPlayerText) return;
            if (isTyping) return;

            if (!isWaitingForPlayerDelay)
            {
                isWaitingForPlayerDelay = true;
                timePlayerTextFinished = Time.realtimeSinceStartup;
                return;
            }

            float elapsed = Time.realtimeSinceStartup - timePlayerTextFinished;
            // Wait for first pawn's response to be ready, or 3s timeout
            bool firstReady = _cachedResponses.ContainsKey(0);
            if (!firstReady && elapsed < 3.0f) return;
            if (elapsed < 1.0f) return;
            if (nextSpeakerRequested) return;

            nextSpeakerRequested = true;
            isShowingPlayerText = false;
            isWaitingForPlayerDelay = false;
            isSendingRequest = false;

            currentSpeakerIndex = 0;
            Owner.SkipInvalidPawnsForward();
            if (currentSpeakerIndex >= participants.Count)
            {
                isPlayerTurn = true;
                return;
            }

            Owner.ShowSpeakerFromCache(0);
        }

        internal void ShowSpeakerFromCache(int idx)
        {
            string text;
            if (_cachedResponses.TryGetValue(idx, out text) && !string.IsNullOrWhiteSpace(text) && text != "…")
            {
                currentDialogueText = text;
                isSendingRequest = false;
            }
            else
            {
                currentDialogueText = "…";
                // Keep isSendingRequest true so CheckPendingResponse can pick up late response
                isSendingRequest = true;
            }
            displayedText = "";
            visibleChars = 0;
            isTyping = true;
            lastCharTime = Time.realtimeSinceStartup;
            pauseForClick = false;

            if (idx >= 0 && idx < participants.Count)
            {
                var sp = participants[idx];
                turnRecords.Add(new GroupTurnRecord { SpeakerPawnId = sp.PawnId, SpeakerName = sp.DisplayName, DialogueText = currentDialogueText, IsPlayer = false });
                dialogPages.Add(new DialoguePage { speakerName = sp.DisplayName, text = currentDialogueText });

                if (!string.IsNullOrWhiteSpace(text) && text != "…")
                    RpgDialogueTraceTracker.RegisterTurn(initiator, sp.Pawn, false, text, dialogueSessionId);
            }
        }

        // ── Called from DoWindowContents ──

        internal void UpdateFlowControl()
        {
            Owner.CheckPlayerTextTransition();
            Owner.CheckPendingResponse();
        }

        // ── Helpers ──

        internal void SkipInvalidPawnsForward()
        {
            while (currentSpeakerIndex < participants.Count)
            {
                var p = participants[currentSpeakerIndex];
                if (p.Pawn == null || p.Pawn.Dead || p.Pawn.Destroyed || !p.Pawn.Spawned)
                    currentSpeakerIndex++;
                else break;
            }
        }

        internal void ResetRoundFlags()
        {
            for (int i = 0; i < participants.Count; i++)
            {
                var p = participants[i];
                p.HasSpokenThisRound = false;
                participants[i] = p;
            }
        }

        internal void ExecuteActionsForSpeaker(GroupChatParticipant speaker, List<LLMRpgApiResponse.ApiAction> actions)
        {
            if (actions == null || actions.Count == 0) return;
            foreach (var action in actions)
            {
                string n = Dialog_RPGPawnDialogue.NormalizeRpgActionName(action?.action);
                if (string.IsNullOrEmpty(n)) continue;
                try { Owner.ExecuteGroupAction(speaker, n, action); }
                catch (Exception ex) { Log.Error($"[RimAI.Relations] Action failed: {ex.Message}"); }
            }
        }

        internal static readonly Color FeedbackSuccess = new Color(0.45f, 0.9f, 0.55f);
        internal static readonly Color FeedbackInfo = new Color(0.55f, 0.78f, 0.98f);

        internal void ExecuteGroupAction(GroupChatParticipant speaker, string normalizedName, LLMRpgApiResponse.ApiAction action)
        {
            Pawn t = speaker.Pawn; string n = speaker.DisplayName;
            switch (normalizedName)
            {
                case "RomanceAttempt": if (t?.relations != null && initiator?.relations != null && t != initiator && !t.relations.DirectRelationExists(PawnRelationDefOf.Spouse, initiator) && !t.relations.DirectRelationExists(PawnRelationDefOf.Fiance, initiator) && !t.relations.DirectRelationExists(PawnRelationDefOf.Lover, initiator)) { t.relations.RemoveDirectRelation(PawnRelationDefOf.ExLover, initiator); t.relations.RemoveDirectRelation(PawnRelationDefOf.ExSpouse, initiator); t.relations.AddDirectRelation(PawnRelationDefOf.Lover, initiator); Owner.AddActionFeedback("RimChat_DragMenu_Romance".Translate() + " → " + n, FeedbackSuccess); } break;
                case "MarriageProposal": if (t?.relations != null && initiator?.relations != null && t != initiator) { t.relations.RemoveDirectRelation(PawnRelationDefOf.ExSpouse, initiator); t.relations.RemoveDirectRelation(PawnRelationDefOf.ExLover, initiator); t.relations.RemoveDirectRelation(PawnRelationDefOf.Fiance, initiator); t.relations.RemoveDirectRelation(PawnRelationDefOf.Lover, initiator); t.relations.AddDirectRelation(PawnRelationDefOf.Spouse, initiator); Owner.AddActionFeedback("RimChat_DragMenu_Marry".Translate() + " → " + n, FeedbackSuccess); } break;
                case "Breakup": if (t?.relations != null && initiator?.relations != null) { bool h = t.relations.DirectRelationExists(PawnRelationDefOf.Spouse, initiator); if (h) { t.relations.RemoveDirectRelation(PawnRelationDefOf.Spouse, initiator); t.relations.AddDirectRelation(PawnRelationDefOf.ExSpouse, initiator); } t.relations.RemoveDirectRelation(PawnRelationDefOf.Lover, initiator); t.relations.RemoveDirectRelation(PawnRelationDefOf.Fiance, initiator); if (!h) t.relations.AddDirectRelation(PawnRelationDefOf.ExLover, initiator); Owner.AddActionFeedback("RimChat_DragMenu_Breakup".Translate() + " → " + n, FeedbackSuccess); } break;
                case "Divorce": if (t?.relations != null && initiator?.relations != null) { t.relations.RemoveDirectRelation(PawnRelationDefOf.Fiance, initiator); t.relations.RemoveDirectRelation(PawnRelationDefOf.Lover, initiator); t.relations.RemoveDirectRelation(PawnRelationDefOf.Spouse, initiator); t.relations.AddDirectRelation(PawnRelationDefOf.ExSpouse, initiator); Owner.AddActionFeedback("RimChat_DragMenu_Divorce".Translate() + " → " + n, FeedbackSuccess); } break;
                case "Date": if (t?.relations != null && initiator?.relations != null && t != initiator && !t.relations.DirectRelationExists(PawnRelationDefOf.Spouse, initiator) && !t.relations.DirectRelationExists(PawnRelationDefOf.Fiance, initiator) && !t.relations.DirectRelationExists(PawnRelationDefOf.Lover, initiator)) { t.relations.RemoveDirectRelation(PawnRelationDefOf.ExLover, initiator); t.relations.RemoveDirectRelation(PawnRelationDefOf.ExSpouse, initiator); t.relations.AddDirectRelation(PawnRelationDefOf.Lover, initiator); Owner.AddActionFeedback("RimChat_DragMenu_Date".Translate() + " → " + n, FeedbackSuccess); } break;
                case "TryGainMemory": if (t?.needs?.mood?.thoughts?.memories != null) { var d = RpgMemoryCatalog.ResolveRequestedThoughtDef(action?.defName ?? "", out _); if (d != null) { t.needs.mood.thoughts.memories.TryGainMemory(d, initiator); Owner.AddActionFeedback("RimChat_DragMenu_Gift".Translate() + " → " + n, FeedbackSuccess); } } break;
                case "TryAffectSocialGoodwill": if (t?.Faction != null && initiator?.Faction != null) { t.Faction.TryAffectGoodwillWith(initiator.Faction, action.amount, true, true, null); Owner.AddActionFeedback("Goodwill " + (action.amount >= 0 ? "+" : "") + action.amount, FeedbackInfo); } break;
                case "Recruit": if (t != null && initiator?.Faction != null && t.Faction != initiator.Faction) { RecruitUtility.Recruit(t, initiator.Faction, initiator); Owner.AddActionFeedback("RimChat_DragMenu_Recruit".Translate() + " → " + n, FeedbackSuccess); } break;
                case "ReduceResistance": if (t?.guest != null && t.IsPrisoner && action.amount > 0) { t.guest.resistance = Mathf.Max(0f, t.guest.resistance - action.amount); Owner.AddActionFeedback("RimChat_DragMenu_ReduceResist".Translate() + " → " + n, FeedbackSuccess); } break;
                case "ReduceWill": if (t?.guest != null && t.IsPrisoner && action.amount > 0) { t.guest.will = Mathf.Max(0f, t.guest.will - action.amount); Owner.AddActionFeedback("RimChat_DragMenu_ReduceWill".Translate() + " → " + n, FeedbackSuccess); } break;
                case "GrantInspiration": if (t?.mindState?.inspirationHandler != null) { var defs = DefDatabase<InspirationDef>.AllDefsListForReading; if (defs != null && defs.Count > 0) { t.mindState.inspirationHandler.TryStartInspiration(defs.RandomElement()); Owner.AddActionFeedback("RimChat_DragMenu_Inspiration".Translate() + " → " + n, FeedbackSuccess); } } break;
                case "TriggerIncident": { var iDef = DefDatabase<IncidentDef>.GetNamedSilentFail(action?.defName); Map m = t?.MapHeld ?? Find.CurrentMap; if (iDef != null && m != null) { var ip = StorytellerUtility.DefaultParmsNow(iDef.category, m); ip.faction = t?.Faction; if (action.amount > 0) ip.points = action.amount; iDef.Worker.TryExecute(ip); Owner.AddActionFeedback("RimChat_DragMenu_Incident".Translate() + " → " + n, FeedbackSuccess); } } break;
                case "ConvertIdeology": if (ModsConfig.IdeologyActive && t?.ideo != null && initiator?.ideo != null) { t.ideo.SetIdeo(initiator.Ideo); Owner.AddActionFeedback("RimChat_DragMenu_ConvertIdeo".Translate() + " → " + n, FeedbackSuccess); } break;
                case "AdjustCertainty": if (ModsConfig.IdeologyActive && t?.ideo != null && action.amount != 0) { t.ideo.OffsetCertainty(action.amount); Owner.AddActionFeedback("RimChat_DragMenu_AdjCertainty".Translate() + " → " + n, FeedbackSuccess); } break;
            }
        }
        }

}
