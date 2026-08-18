using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Dialogue;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.Rpg;

namespace Ustas.RimAI.Communication.Relations.UI
{
    [StaticConstructorOnStartup]
    public class Dialog_RPGPawnGroupChat : Window
    {
        internal Dialog_RPGPawnGroupChatParts Parts;

        internal readonly Pawn initiator;
        internal readonly List<GroupChatParticipant> participants;

        internal readonly string dialogueSessionId;
        internal readonly DialogueRuntimeContext runtimeContext;
        internal readonly string windowLifecycleKey;
        internal readonly string windowInstanceId = Guid.NewGuid().ToString("N");
        internal readonly RpgDialogueConversationController conversationController = new RpgDialogueConversationController();

        internal DialogueRequestLease activeRequestLease;
        internal DialogueRuntimeContext activeRequestRuntimeContext;
        internal bool isWindowClosing;

        // Round-robin state
        internal int currentSpeakerIndex = -1;
        internal int currentRound;
        internal bool pauseForClick;
        internal bool isPlayerTurn;
        internal bool isSendingRequest;

        // Dialogue text state
        internal string currentDialogueText = "";
        internal string displayedText = "";
        internal string userReplyText = "";
        internal int visibleChars;
        internal float lastCharTime;
        internal bool isTyping;

        // Player-speech display (reuse 1v1 flow)
        internal bool isShowingPlayerText;
        internal bool isWaitingForPlayerDelay;
        internal float timePlayerTextFinished;
        internal bool nextSpeakerRequested;

        // AI state
        internal bool aiResponseReady;
        internal string aiResponseText = "";

        // Context accumulation: all dialogues in order for injection
        internal readonly List<GroupTurnRecord> turnRecords = new List<GroupTurnRecord>();

        // History / pages
        internal readonly List<DialoguePage> dialogPages = new List<DialoguePage>();
        internal bool isViewingHistory;
        internal int historyViewIndex;

        // Text paging (reuse 1v1 pattern)
        internal readonly List<string> currentTextPages = new List<string>();
        internal string pagedTextCache = "";
        internal float pagedWidthCache = -1f;
        internal float pagedHeightCache = -1f;
        internal int currentTextPageIndex;

        // Round transition animation
        internal int previousSpeakerIndex = -1;
        internal float roundTransitionTime;
        internal const float RoundTransitionDuration = 0.35f;

        // Action feedback (reuse 1v1-style floating subtitles)
        internal readonly List<ActionFeedbackEntry> feedbackEntries = new List<ActionFeedbackEntry>();
        internal struct ActionFeedbackEntry
        {
            public string Text;
            public Color Color;
            public float CreatedAt;
        }
        internal const float FeedbackDuration = 4f;
        internal const int FeedbackMaxCount = 5;

        // Dialogue box color (reuse 1v1 dynamic color)
        internal Color dialogueBoxCurrentColor = new Color(0.1f, 0.1f, 0.12f, 0.9f);
        internal Color dialogueBoxTargetColor = new Color(0.1f, 0.1f, 0.12f, 0.9f);
        internal const float DialogueBoxColorBlendSpeed = 2.5f;
        internal static readonly Color BoxDefault = new Color(0.1f, 0.1f, 0.12f, 0.9f);
        internal static readonly Color BoxRomance = new Color(0.50f, 0.44f, 0.45f, 0.9f);
        internal static readonly Color BoxNeutral = new Color(0.13f, 0.17f, 0.24f, 0.9f);
        internal static readonly Color BoxPrisoner = new Color(0.22f, 0.13f, 0.07f, 0.9f);
        internal static readonly Color BoxHostile = new Color(0.40f, 0.15f, 0.16f, 0.9f);

        // Animation
        internal float globalFadeAlpha;
        internal float inputAlpha = 0.3f;
        internal const float FadeSpeed = 1.5f;

        // UI constants
        internal const float PortraitOverlapRatio = 0.55f;
        internal const float PortraitLeftMargin = 50f;
        internal const float ClickHintAlpha = 0.7f;
        internal const float PortraitVerticalOverlap = 150f;

        // Portrait RT caches
        internal RenderTexture initiatorPortraitRT;
        internal readonly Dictionary<int, float> _lastPortraitRenderTime = new Dictionary<int, float>();
        internal const float NonSpeakerRenderInterval = 0.5f;

        internal const string UserReplyInputControlName = "GroupChatUserReplyInput";

        internal struct GroupChatParticipant
        {
            public Pawn Pawn;
            public string PawnId;
            public string DisplayName;
            public int OrderIndex;
            public bool HasSpokenThisRound;
            public RenderTexture PortraitRT;
        }

        internal struct DialoguePage
        {
            public string speakerName;
            public string text;
        }

        internal struct GroupTurnRecord
        {
            public string SpeakerPawnId;
            public string SpeakerName;
            public string DialogueText;
            public bool IsPlayer;
        }

        public override Vector2 InitialSize => new Vector2(Verse.UI.screenWidth, Verse.UI.screenHeight);
        protected override float Margin => 0f;

        public Dialog_RPGPawnGroupChat(
            Pawn initiator,
            List<Pawn> participants,
            DialogueRuntimeContext runtimeContext = null,
            string windowLifecycleKey = null)
        {
            Parts = new Dialog_RPGPawnGroupChatParts(this);
            this.initiator = initiator;

            this.participants = new List<GroupChatParticipant>();
            for (int i = 0; i < participants.Count; i++)
            {
                Pawn p = participants[i];
                this.participants.Add(new GroupChatParticipant
                {
                    Pawn = p,
                    PawnId = p.GetUniqueLoadID(),
                    DisplayName = p.LabelShort,
                    OrderIndex = i,
                    HasSpokenThisRound = false,
                    PortraitRT = null
                });
            }

            string resolvedSessionId = runtimeContext?.DialogueSessionId;
            dialogueSessionId = string.IsNullOrWhiteSpace(resolvedSessionId)
                ? Guid.NewGuid().ToString("N")
                : resolvedSessionId;
            this.runtimeContext = runtimeContext ?? DialogueRuntimeContext.CreateRpgGroup(initiator, participants, initiator?.Map, dialogueSessionId);
            this.windowLifecycleKey = string.IsNullOrWhiteSpace(windowLifecycleKey)
                ? this.runtimeContext.WindowKey
                : windowLifecycleKey.Trim();

            doCloseX = false;
            doCloseButton = false;
            closeOnClickedOutside = false;
            closeOnAccept = false;
            closeOnCancel = true;
            absorbInputAroundWindow = true;
            forcePause = true;
            preventCameraMotion = true;
            doWindowBackground = false;

            WarmupParticipantMemories();
            SendFirstSpeakerRequest();
        }

        

        

        

        public override void PreClose()
        {
            isWindowClosing = true;
            CloseActiveRequestLease();
            foreach (var p in participants)
            {
                if (p.PortraitRT != null)
                {
                    UnityEngine.Object.Destroy(p.PortraitRT);
                }
            }
            if (initiatorPortraitRT != null)
            {
                UnityEngine.Object.Destroy(initiatorPortraitRT);
                initiatorPortraitRT = null;
            }
            base.PreClose();
        }

        

        

        

        

        

        

        

        // ── Action feedback (1v1-style floating subtitle) ──

        

        

        

        

        // ── Text paging ──

        

        

        

        #region Facade forwards
        internal void StartRound() => Parts.FlowControl.StartRound();
        internal void SendSerialRequest(int pawnIndex) => Parts.FlowControl.SendSerialRequest(pawnIndex);
        internal void OnResponseReceived(int pawnIndex) => Parts.FlowControl.OnResponseReceived(pawnIndex);
        internal void SendFirstSpeakerRequest() => Parts.FlowControl.SendFirstSpeakerRequest();
        internal void AdvanceToNextSpeaker() => Parts.FlowControl.AdvanceToNextSpeaker();
        internal void CheckPendingResponse() => Parts.FlowControl.CheckPendingResponse();
        internal void TransitionToPlayerTurn() => Parts.FlowControl.TransitionToPlayerTurn();
        internal void TrySendPlayerMessage() => Parts.FlowControl.TrySendPlayerMessage();
        internal void CheckPlayerTextTransition() => Parts.FlowControl.CheckPlayerTextTransition();
        internal void ShowSpeakerFromCache(int idx) => Parts.FlowControl.ShowSpeakerFromCache(idx);
        internal void UpdateFlowControl() => Parts.FlowControl.UpdateFlowControl();
        internal void SkipInvalidPawnsForward() => Parts.FlowControl.SkipInvalidPawnsForward();
        internal void ResetRoundFlags() => Parts.FlowControl.ResetRoundFlags();
        internal void ExecuteActionsForSpeaker(GroupChatParticipant speaker, List<LLMRpgApiResponse.ApiAction> actions) => Parts.FlowControl.ExecuteActionsForSpeaker(speaker, actions);
        internal void ExecuteGroupAction(GroupChatParticipant speaker, string normalizedName, LLMRpgApiResponse.ApiAction action) => Parts.FlowControl.ExecuteGroupAction(speaker, normalizedName, action);
        internal void DrawHistoryToggleButton(Rect boxRect) => Parts.History.DrawHistoryToggleButton(boxRect);
        internal void DrawHistoryPanel(Rect inRect) => Parts.History.DrawHistoryPanel(inRect);
        internal bool TryHandleHistoryPanelClick() => Parts.History.TryHandleHistoryPanelClick();
        internal static float MeasureGroupHistoryEntryHeight(string text, float width) => RPGPawnGroupChatHistory.MeasureGroupHistoryEntryHeight(text, width);
        internal void PrepareEnvelopeForDisplay(DialogueResponseEnvelope envelope) => Parts.Lifecycle.PrepareEnvelopeForDisplay(envelope);
        internal void HandleDroppedResponse(string reason) => Parts.Lifecycle.HandleDroppedResponse(reason);
        internal void DrawPortraits(Rect inRect) => Parts.Portraits.DrawPortraits(inRect);
        internal int GetActiveSpeakerIndex() => Parts.Portraits.GetActiveSpeakerIndex();
        internal List<int> BuildDrawOrder(int speakerIndex, int count) => Parts.Portraits.BuildDrawOrder(speakerIndex, count);
        internal void UpdateRoundTransition(int newSpeakerIdx) => Parts.Portraits.UpdateRoundTransition(newSpeakerIdx);
        internal float GetRoundTransitionProgress() => Parts.Portraits.GetRoundTransitionProgress();
        internal bool NeedsPortraitRefresh(int index) => Parts.Portraits.NeedsPortraitRefresh(index);
        internal List<Rect> CalculateCascadingRects(Rect inRect) => Parts.Portraits.CalculateCascadingRects(inRect);
        internal void DrawInitiatorPortrait(Rect inRect) => Parts.Portraits.DrawInitiatorPortrait(inRect);
        internal void ShowForcedActionMenu() => Parts.Portraits.ShowForcedActionMenu();
        internal void AddMenuOption(List<FloatMenuOption> options, string labelKey, string actionName, Pawn targetPawn) => Parts.Portraits.AddMenuOption(options, labelKey, actionName, targetPawn);
        internal void ExecuteActionDirect(string actionName, Pawn targetPawn) => Parts.Portraits.ExecuteActionDirect(actionName, targetPawn);
        internal static bool HasLoveRelation(Pawn target, Pawn initiator) => RPGPawnGroupChatPortraits.HasLoveRelation(target, initiator);
        internal static bool HasSpouseRelation(Pawn target, Pawn initiator) => RPGPawnGroupChatPortraits.HasSpouseRelation(target, initiator);
        internal void DrawParticipantNameLabel(Rect portraitRect, string name, bool isSpeaker) => Parts.Portraits.DrawParticipantNameLabel(portraitRect, name, isSpeaker);
        internal List<ChatMessageData> BuildGroupRequestMessages(GroupChatParticipant speaker, bool isFirstTurn) => Parts.Prompt.BuildGroupRequestMessages(speaker, isFirstTurn);
        internal string BuildGroupSystemPrompt(GroupChatParticipant speaker, bool isFirstTurn) => Parts.Prompt.BuildGroupSystemPrompt(speaker, isFirstTurn);
        internal string BuildFallbackPersonaPrompt(GroupChatParticipant speaker) => Parts.Prompt.BuildFallbackPersonaPrompt(speaker);
        internal string BuildTurnContextMessage(GroupChatParticipant speaker, bool isFirstTurn) => Parts.Prompt.BuildTurnContextMessage(speaker, isFirstTurn);
        #endregion
    
        #region Cluster forwards
        internal void WarmupParticipantMemories() => Parts.Slice1.WarmupParticipantMemories();
        public bool MatchesWindowLifecycleKey(string key) => Parts.Slice1.MatchesWindowLifecycleKey(key);
        public override void DoWindowContents(Rect inRect) => Parts.Slice1.DoWindowContents(inRect);
        internal void DrawDialogueBox(Rect inRect) => Parts.Slice1.DrawDialogueBox(inRect);
        internal void DrawDialogueNavigation(Rect boxRect) => Parts.Slice1.DrawDialogueNavigation(boxRect);
        internal static void DrawNavBox(Rect boxRect, bool canGoPrev, bool canGoNext, string counter, Action onPrev, Action onNext) => RPGPawnGroupChatSlice1.DrawNavBox(boxRect, canGoPrev, canGoNext, counter, onPrev, onNext);
        internal void DrawClickToContinueHint(Rect inRect) => Parts.Slice1.DrawClickToContinueHint(inRect);
        internal void DrawSpeakerName(Rect nameRect, string displayName, bool rightAligned, Pawn pawn = null) => Parts.Slice1.DrawSpeakerName(nameRect, displayName, rightAligned, pawn);
        internal void UpdateTyping() => Parts.Slice1.UpdateTyping();
        internal bool ShouldSendFromKeyboard() => Parts.Slice1.ShouldSendFromKeyboard();
        internal void AddActionFeedback(string text, Color color) => Parts.Slice1.AddActionFeedback(text, color);
        internal void DrawActionFeedback(Rect inRect) => Parts.Slice1.DrawActionFeedback(inRect);
        internal Color ResolveDialogueBoxColor() => Parts.Slice1.ResolveDialogueBoxColor();
        internal void CloseActiveRequestLease() => Parts.Slice1.CloseActiveRequestLease();
        internal string ResolvePagedText(string fullText, Rect textArea) => Parts.Slice1.ResolvePagedText(fullText, textArea);
        internal bool NeedsPaging(string fullText, Rect textArea) => Parts.Slice1.NeedsPaging(fullText, textArea);
        internal void EnsureTextPages(string fullText, float width, float height) => Parts.Slice1.EnsureTextPages(fullText, width, height);
        #endregion
}
    internal sealed class Dialog_RPGPawnGroupChatParts
    {
        internal readonly Dialog_RPGPawnGroupChat Owner;
        internal readonly RPGPawnGroupChatFlowControl FlowControl;
        internal readonly RPGPawnGroupChatHistory History;
        internal readonly RPGPawnGroupChatLifecycle Lifecycle;
        internal readonly RPGPawnGroupChatPortraits Portraits;
        internal readonly RPGPawnGroupChatPrompt Prompt;
        internal readonly RPGPawnGroupChatSlice1 Slice1;
        internal Dialog_RPGPawnGroupChatParts(Dialog_RPGPawnGroupChat owner)
        {
            Owner = owner;
            FlowControl = new RPGPawnGroupChatFlowControl(owner);
            History = new RPGPawnGroupChatHistory(owner);
            Lifecycle = new RPGPawnGroupChatLifecycle(owner);
            Portraits = new RPGPawnGroupChatPortraits(owner);
            Prompt = new RPGPawnGroupChatPrompt(owner);
            Slice1 = new RPGPawnGroupChatSlice1(owner);
        }
    }


}
