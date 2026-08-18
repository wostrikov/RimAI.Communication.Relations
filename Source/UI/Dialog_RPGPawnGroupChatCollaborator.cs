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
        internal abstract class Dialog_RPGPawnGroupChatCollaborator
    {
        internal readonly Dialog_RPGPawnGroupChat Owner;

        protected Dialog_RPGPawnGroupChatCollaborator(Dialog_RPGPawnGroupChat owner)
        {
            Owner = owner;
        }
        protected Dialog_RPGPawnGroupChatParts Parts => Owner.Parts;



        protected void Close(bool doCloseSound = true) => Owner.Close(doCloseSound);
        protected bool absorbInputAroundWindow
        {
            get => Owner.absorbInputAroundWindow;
            set => Owner.absorbInputAroundWindow = value;
        }
        protected Pawn initiator => Owner.initiator;
        protected List<Dialog_RPGPawnGroupChat.GroupChatParticipant> participants => Owner.participants;
        protected string dialogueSessionId => Owner.dialogueSessionId;
        protected DialogueRuntimeContext runtimeContext => Owner.runtimeContext;
        protected string windowLifecycleKey => Owner.windowLifecycleKey;
        protected RpgDialogueConversationController conversationController => Owner.conversationController;
        protected DialogueRequestLease activeRequestLease
        {
            get => Owner.activeRequestLease;
            set => Owner.activeRequestLease = value;
        }
        protected DialogueRuntimeContext activeRequestRuntimeContext
        {
            get => Owner.activeRequestRuntimeContext;
            set => Owner.activeRequestRuntimeContext = value;
        }
        protected bool isWindowClosing
        {
            get => Owner.isWindowClosing;
            set => Owner.isWindowClosing = value;
        }
        protected int currentSpeakerIndex
        {
            get => Owner.currentSpeakerIndex;
            set => Owner.currentSpeakerIndex = value;
        }
        protected int currentRound
        {
            get => Owner.currentRound;
            set => Owner.currentRound = value;
        }
        protected bool pauseForClick
        {
            get => Owner.pauseForClick;
            set => Owner.pauseForClick = value;
        }
        protected bool isPlayerTurn
        {
            get => Owner.isPlayerTurn;
            set => Owner.isPlayerTurn = value;
        }
        protected bool isSendingRequest
        {
            get => Owner.isSendingRequest;
            set => Owner.isSendingRequest = value;
        }
        protected string currentDialogueText
        {
            get => Owner.currentDialogueText;
            set => Owner.currentDialogueText = value;
        }
        protected string displayedText
        {
            get => Owner.displayedText;
            set => Owner.displayedText = value;
        }
        protected string userReplyText
        {
            get => Owner.userReplyText;
            set => Owner.userReplyText = value;
        }
        protected int visibleChars
        {
            get => Owner.visibleChars;
            set => Owner.visibleChars = value;
        }
        protected float lastCharTime
        {
            get => Owner.lastCharTime;
            set => Owner.lastCharTime = value;
        }
        protected bool isTyping
        {
            get => Owner.isTyping;
            set => Owner.isTyping = value;
        }
        protected bool isShowingPlayerText
        {
            get => Owner.isShowingPlayerText;
            set => Owner.isShowingPlayerText = value;
        }
        protected bool isWaitingForPlayerDelay
        {
            get => Owner.isWaitingForPlayerDelay;
            set => Owner.isWaitingForPlayerDelay = value;
        }
        protected float timePlayerTextFinished
        {
            get => Owner.timePlayerTextFinished;
            set => Owner.timePlayerTextFinished = value;
        }
        protected bool nextSpeakerRequested
        {
            get => Owner.nextSpeakerRequested;
            set => Owner.nextSpeakerRequested = value;
        }
        protected bool aiResponseReady
        {
            get => Owner.aiResponseReady;
            set => Owner.aiResponseReady = value;
        }
        protected string aiResponseText
        {
            get => Owner.aiResponseText;
            set => Owner.aiResponseText = value;
        }
        protected List<Dialog_RPGPawnGroupChat.GroupTurnRecord> turnRecords => Owner.turnRecords;
        protected List<Dialog_RPGPawnGroupChat.DialoguePage> dialogPages => Owner.dialogPages;
        protected bool isViewingHistory
        {
            get => Owner.isViewingHistory;
            set => Owner.isViewingHistory = value;
        }
        protected int historyViewIndex
        {
            get => Owner.historyViewIndex;
            set => Owner.historyViewIndex = value;
        }
        protected List<string> currentTextPages => Owner.currentTextPages;
        protected string pagedTextCache
        {
            get => Owner.pagedTextCache;
            set => Owner.pagedTextCache = value;
        }
        protected float pagedWidthCache
        {
            get => Owner.pagedWidthCache;
            set => Owner.pagedWidthCache = value;
        }
        protected float pagedHeightCache
        {
            get => Owner.pagedHeightCache;
            set => Owner.pagedHeightCache = value;
        }
        protected int currentTextPageIndex
        {
            get => Owner.currentTextPageIndex;
            set => Owner.currentTextPageIndex = value;
        }
        protected int previousSpeakerIndex
        {
            get => Owner.previousSpeakerIndex;
            set => Owner.previousSpeakerIndex = value;
        }
        protected float roundTransitionTime
        {
            get => Owner.roundTransitionTime;
            set => Owner.roundTransitionTime = value;
        }
        protected const float RoundTransitionDuration = 0.35f;
        protected List<Dialog_RPGPawnGroupChat.ActionFeedbackEntry> feedbackEntries => Owner.feedbackEntries;
        protected const float FeedbackDuration = 4f;
        protected const int FeedbackMaxCount = 5;
        protected Color dialogueBoxCurrentColor
        {
            get => Owner.dialogueBoxCurrentColor;
            set => Owner.dialogueBoxCurrentColor = value;
        }
        protected Color dialogueBoxTargetColor
        {
            get => Owner.dialogueBoxTargetColor;
            set => Owner.dialogueBoxTargetColor = value;
        }
        protected const float DialogueBoxColorBlendSpeed = 2.5f;
        protected static Color BoxDefault => Dialog_RPGPawnGroupChat.BoxDefault;
        protected static Color BoxRomance => Dialog_RPGPawnGroupChat.BoxRomance;
        protected static Color BoxNeutral => Dialog_RPGPawnGroupChat.BoxNeutral;
        protected static Color BoxPrisoner => Dialog_RPGPawnGroupChat.BoxPrisoner;
        protected static Color BoxHostile => Dialog_RPGPawnGroupChat.BoxHostile;
        protected float globalFadeAlpha
        {
            get => Owner.globalFadeAlpha;
            set => Owner.globalFadeAlpha = value;
        }
        protected float inputAlpha
        {
            get => Owner.inputAlpha;
            set => Owner.inputAlpha = value;
        }
        protected const float FadeSpeed = 1.5f;
        protected const float PortraitOverlapRatio = 0.55f;
        protected const float PortraitLeftMargin = 50f;
        protected const float ClickHintAlpha = 0.7f;
        protected const float PortraitVerticalOverlap = 150f;
        protected RenderTexture initiatorPortraitRT
        {
            get => Owner.initiatorPortraitRT;
            set => Owner.initiatorPortraitRT = value;
        }
        protected Dictionary<int, float> _lastPortraitRenderTime => Owner._lastPortraitRenderTime;
        protected const float NonSpeakerRenderInterval = 0.5f;
        protected const string UserReplyInputControlName = "GroupChatUserReplyInput";
        protected string windowInstanceId => Owner.windowInstanceId;
        protected bool isHistoryPanelOpen
        {
            get => Owner.Parts.History.isHistoryPanelOpen;
            set => Owner.Parts.History.isHistoryPanelOpen = value;
        }
        protected const float HistoryPanelMinW = RPGPawnGroupChatHistory.HistoryPanelMinW;
        protected const float HistoryPanelMaxW = RPGPawnGroupChatHistory.HistoryPanelMaxW;
        protected const float HistoryPanelMinH = RPGPawnGroupChatHistory.HistoryPanelMinH;
        protected const float HistoryPanelMaxH = RPGPawnGroupChatHistory.HistoryPanelMaxH;
    }

}
