using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.Dialogue;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.Rpg;

namespace Ustas.RimAI.Communication.Relations.UI
{
        internal abstract class Dialog_RPGPawnDialogueCollaborator
    {
        internal readonly Dialog_RPGPawnDialogue Owner;

        protected Dialog_RPGPawnDialogueCollaborator(Dialog_RPGPawnDialogue owner)
        {
            Owner = owner;
        }
        protected Dialog_RPGPawnDialogueParts Parts => Owner.Parts;



        protected void Close(bool doCloseSound = true) => Owner.Close(doCloseSound);
        protected bool absorbInputAroundWindow
        {
            get => Owner.absorbInputAroundWindow;
            set => Owner.absorbInputAroundWindow = value;
        }
        protected Pawn initiator => Owner.initiator;
        protected Pawn target => Owner.target;
        protected string dialogueSessionId => Owner.dialogueSessionId;
        protected DialogueRuntimeContext runtimeContext => Owner.runtimeContext;
        protected string windowLifecycleKey => Owner.windowLifecycleKey;
        protected RpgDialogueConversationController conversationController => Owner.conversationController;
        protected Dialog_RPGPawnDialogue.InitialRequestPromptCache initialRequestPromptCache
        {
            get => Owner.initialRequestPromptCache;
            set => Owner.initialRequestPromptCache = value;
        }
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
        protected bool isSendingInitialMessage
        {
            get => Owner.isSendingInitialMessage;
            set => Owner.isSendingInitialMessage = value;
        }
        protected bool isShowingUserText
        {
            get => Owner.isShowingUserText;
            set => Owner.isShowingUserText = value;
        }
        protected bool isWaitingForDelayAfterUser
        {
            get => Owner.isWaitingForDelayAfterUser;
            set => Owner.isWaitingForDelayAfterUser = value;
        }
        protected float timeUserTextFinished
        {
            get => Owner.timeUserTextFinished;
            set => Owner.timeUserTextFinished = value;
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
        protected DialogueResponseEnvelope pendingResponseEnvelope
        {
            get => Owner.pendingResponseEnvelope;
            set => Owner.pendingResponseEnvelope = value;
        }
        protected bool isDialogueEndedByNpc
        {
            get => Owner.isDialogueEndedByNpc;
            set => Owner.isDialogueEndedByNpc = value;
        }
        protected string dialogueEndReason
        {
            get => Owner.dialogueEndReason;
            set => Owner.dialogueEndReason = value;
        }
        protected bool sessionCloseSummaryCommitted
        {
            get => Owner.sessionCloseSummaryCommitted;
            set => Owner.sessionCloseSummaryCommitted = value;
        }
        protected bool archiveSessionFinalized
        {
            get => Owner.archiveSessionFinalized;
            set => Owner.archiveSessionFinalized = value;
        }
        protected string currentSpeakerName
        {
            get => Owner.currentSpeakerName;
            set => Owner.currentSpeakerName = value;
        }
        protected List<ChatMessageData> chatHistory
        {
            get => Owner.chatHistory;
            set => Owner.chatHistory = value;
        }
        protected List<Dialog_RPGPawnDialogue.DialoguePage> dialogPages
        {
            get => Owner.dialogPages;
            set => Owner.dialogPages = value;
        }
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
        protected const float DialogueBoxHeight = 260f;
        protected const float PortraitWidth = 400f;
        protected const float PortraitHeight = 500f;
        protected float globalFadeAlpha
        {
            get => Owner.globalFadeAlpha;
            set => Owner.globalFadeAlpha = value;
        }
        protected float initiatorFadeAlpha
        {
            get => Owner.initiatorFadeAlpha;
            set => Owner.initiatorFadeAlpha = value;
        }
        protected float targetFadeAlpha
        {
            get => Owner.targetFadeAlpha;
            set => Owner.targetFadeAlpha = value;
        }
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
        protected static Color DialogueBoxDefaultColor => Dialog_RPGPawnDialogue.DialogueBoxDefaultColor;
        protected static Color DialogueBoxRomanceColor => Dialog_RPGPawnDialogue.DialogueBoxRomanceColor;
        protected static Color DialogueBoxNeutralColor => Dialog_RPGPawnDialogue.DialogueBoxNeutralColor;
        protected static Color DialogueBoxPrisonerColor => Dialog_RPGPawnDialogue.DialogueBoxPrisonerColor;
        protected static Color DialogueBoxHostileColor => Dialog_RPGPawnDialogue.DialogueBoxHostileColor;
        protected bool firstTargetSentenceDone
        {
            get => Owner.firstTargetSentenceDone;
            set => Owner.firstTargetSentenceDone = value;
        }
        protected const float FadeSpeed = 1.5f;
        protected const string UserReplyInputControlName = "UserReplyInput";
        protected const string RpgStrictOutputContractReminder = "Strict RPG output contract: write natural dialogue as plain text. " +
            "Only if gameplay effects are needed, append exactly one raw JSON object in the form " +
            "{\"actions\":[...]} after the dialogue. " +
            "Never wrap dialogue into JSON fields like \"dialogue\", \"response\", or \"content\". " +
            "Inside each action object, use key \"action\" and optional flat fields " +
            "\"defName\"/\"amount\"/\"reason\".";
        protected float inputAlpha
        {
            get => Owner.inputAlpha;
            set => Owner.inputAlpha = value;
        }
        protected RenderTexture initiatorRT
        {
            get => Owner.initiatorRT;
            set => Owner.initiatorRT = value;
        }
        protected RenderTexture targetRT
        {
            get => Owner.targetRT;
            set => Owner.targetRT = value;
        }
        protected static Color ActionInfoColor => RPGPawnDialogueFeedbackOverlay.ActionInfoColor;
        protected static Color ActionSuccessColor => RPGPawnDialogueFeedbackOverlay.ActionSuccessColor;
        protected static Color ActionFailureColor => RPGPawnDialogueFeedbackOverlay.ActionFailureColor;
        protected static Color ActionErrorColor => RPGPawnDialogueFeedbackOverlay.ActionErrorColor;
        protected static Color MoodPositiveColor => RPGPawnDialogueFeedbackOverlay.MoodPositiveColor;
        protected static Color MoodNegativeColor => RPGPawnDialogueFeedbackOverlay.MoodNegativeColor;
        protected float TargetPortraitWidth => Owner.TargetPortraitWidth;
        protected float TargetPortraitHeight => Owner.TargetPortraitHeight;
        protected float InitiatorPortraitWidth => Owner.InitiatorPortraitWidth;
        protected float InitiatorPortraitHeight => Owner.InitiatorPortraitHeight;
        protected float inspectPaneAlpha
        {
            get => Owner.Parts.PawnMenu.inspectPaneAlpha;
            set => Owner.Parts.PawnMenu.inspectPaneAlpha = value;
        }
        protected string windowInstanceId => Owner.windowInstanceId;
        protected bool suppressAutoMemoryFallbackForTurn
        {
            get => Owner.Parts.ActionPolicies.suppressAutoMemoryFallbackForTurn;
            set => Owner.Parts.ActionPolicies.suppressAutoMemoryFallbackForTurn = value;
        }
        protected const float InspectPaneAlphaSpeed = RPGPawnDialoguePawnMenu.InspectPaneAlphaSpeed;
    }

}
