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

internal abstract class DiplomacyDialogueCollaborator
{
    internal readonly Dialog_DiplomacyDialogue Owner;

    protected DiplomacyDialogueCollaborator(Dialog_DiplomacyDialogue owner)
    {
        Owner = owner;
    }

    protected Faction faction
    {
        get => Owner.faction;
        set => Owner.faction = value;
    }
    protected Pawn negotiator => Owner.negotiator;
    protected DialogueRuntimeContext runtimeContext
    {
        get => Owner.runtimeContext;
        set => Owner.runtimeContext = value;
    }
    protected string windowLifecycleKey
    {
        get => Owner.windowLifecycleKey;
        set => Owner.windowLifecycleKey = value;
    }
    protected string windowInstanceId => Owner.windowInstanceId;
    protected FactionDialogueSession session
    {
        get => Owner.session;
        set => Owner.session = value;
    }
    protected DialogueCloseIntent closeIntent
    {
        get => Owner.closeIntent;
        set => Owner.closeIntent = value;
    }
    protected DiplomacyConversationController conversationController => Owner.conversationController;
    protected string inputText
    {
        get => Owner.inputText;
        set => Owner.inputText = value;
    }
    protected List<string> inputHistory => Owner.inputHistory;
    protected int inputHistoryIndex
    {
        get => Owner.inputHistoryIndex;
        set => Owner.inputHistoryIndex = value;
    }
    protected string inputHistoryDraft
    {
        get => Owner.inputHistoryDraft;
        set => Owner.inputHistoryDraft = value;
    }
    protected Vector2 messageScrollPosition
    {
        get => Owner.messageScrollPosition;
        set => Owner.messageScrollPosition = value;
    }
    protected Vector2 factionScrollPosition
    {
        get => Owner.factionScrollPosition;
        set => Owner.factionScrollPosition = value;
    }
    protected string blockedReasonScrollText
    {
        get => Owner.blockedReasonScrollText;
        set => Owner.blockedReasonScrollText = value;
    }
    protected float blockedReasonAutoScrollOffset
    {
        get => Owner.blockedReasonAutoScrollOffset;
        set => Owner.blockedReasonAutoScrollOffset = value;
    }
    protected int blockedReasonAutoScrollDirection
    {
        get => Owner.blockedReasonAutoScrollDirection;
        set => Owner.blockedReasonAutoScrollDirection = value;
    }
    protected float blockedReasonAutoScrollPauseUntil
    {
        get => Owner.blockedReasonAutoScrollPauseUntil;
        set => Owner.blockedReasonAutoScrollPauseUntil = value;
    }
    protected float blockedReasonAutoScrollLastRealtime
    {
        get => Owner.blockedReasonAutoScrollLastRealtime;
        set => Owner.blockedReasonAutoScrollLastRealtime = value;
    }
    protected int lastMessageCount
    {
        get => Owner.lastMessageCount;
        set => Owner.lastMessageCount = value;
    }
    protected int sessionMessageBaselineCount
    {
        get => Owner.sessionMessageBaselineCount;
        set => Owner.sessionMessageBaselineCount = value;
    }
    protected bool sessionCloseSummaryCommitted
    {
        get => Owner.sessionCloseSummaryCommitted;
        set => Owner.sessionCloseSummaryCommitted = value;
    }
    protected bool userIsScrolling
    {
        get => Owner.userIsScrolling;
        set => Owner.userIsScrolling = value;
    }
    protected Vector2 lastWindowScreenPos
    {
        get => Owner.lastWindowScreenPos;
        set => Owner.lastWindowScreenPos = value;
    }
    protected Rect lastMessagesViewRect
    {
        get => Owner.lastMessagesViewRect;
        set => Owner.lastMessagesViewRect = value;
    }
    protected Rect lastWindowContentRect
    {
        get => Owner.lastWindowContentRect;
        set => Owner.lastWindowContentRect = value;
    }
    protected Dictionary<Faction, Rect> factionRowRects => Owner.factionRowRects;
    protected Dictionary<Faction, float> goodwillValueRevealUntil => Owner.goodwillValueRevealUntil;
    protected Dictionary<Faction, float> goodwillHoverAlpha => Owner.goodwillHoverAlpha;
    protected Dictionary<DialogueMessageData, TypewriterState> typewriterStates
    {
        get => Owner.typewriterStates;
        set => Owner.typewriterStates = value;
    }
    protected float lastTypewriterUpdate
    {
        get => Owner.lastTypewriterUpdate;
        set => Owner.lastTypewriterUpdate = value;
    }
    protected bool _typewriterDirty
    {
        get => Owner._typewriterDirty;
        set => Owner._typewriterDirty = value;
    }
    protected bool fallbackRetryRequestedThisFrame
    {
        get => Owner.fallbackRetryRequestedThisFrame;
        set => Owner.fallbackRetryRequestedThisFrame = value;
    }
    protected PendingAirdropDialogState pendingAirdropDialogState
    {
        get => Owner.pendingAirdropDialogState;
        set => Owner.pendingAirdropDialogState = value;
    }
    protected float socialExpAnimStartTime
    {
        get => Owner.socialExpAnimStartTime;
        set => Owner.socialExpAnimStartTime = value;
    }
    protected int lastExpAmount
    {
        get => Owner.lastExpAmount;
        set => Owner.lastExpAmount = value;
    }
    protected Sustainer sustainer
    {
        get => Owner.sustainer;
        set => Owner.sustainer = value;
    }
    protected int _frameDiagThrottle
    {
        get => Owner._frameDiagThrottle;
        set => Owner._frameDiagThrottle = value;
    }
    protected int _lastFrameDiagFactionId
    {
        get => Owner._lastFrameDiagFactionId;
        set => Owner._lastFrameDiagFactionId = value;
    }
    protected List<Faction> _cachedFactionList
    {
        get => Owner._cachedFactionList;
        set => Owner._cachedFactionList = value;
    }
    protected int _cachedFactionListTick
    {
        get => Owner._cachedFactionListTick;
        set => Owner._cachedFactionListTick = value;
    }
    protected Faction _cachedQuestFaction
    {
        get => Owner._cachedQuestFaction;
        set => Owner._cachedQuestFaction = value;
    }
    protected List<Quest> _cachedQuests
    {
        get => Owner._cachedQuests;
        set => Owner._cachedQuests = value;
    }
    protected int _cachedQuestsTick
    {
        get => Owner._cachedQuestsTick;
        set => Owner._cachedQuestsTick = value;
    }
    protected const int MAX_INPUT_LENGTH = Dialog_DiplomacyDialogue.MAX_INPUT_LENGTH;
    protected const float FACTION_LIST_WIDTH = Dialog_DiplomacyDialogue.FACTION_LIST_WIDTH;
    protected const float INPUT_AREA_HEIGHT = Dialog_DiplomacyDialogue.INPUT_AREA_HEIGHT;
    protected const float STRATEGY_BAR_HEIGHT = Dialog_DiplomacyDialogue.STRATEGY_BAR_HEIGHT;
    protected const float TIME_GAP_THRESHOLD_MINUTES = Dialog_DiplomacyDialogue.TIME_GAP_THRESHOLD_MINUTES;
    protected const float BUBBLE_CORNER_RADIUS = Dialog_DiplomacyDialogue.BUBBLE_CORNER_RADIUS;
    protected const float LayoutHeaderTop = Dialog_DiplomacyDialogue.LayoutHeaderTop;
    protected const float LayoutPanelPadding = Dialog_DiplomacyDialogue.LayoutPanelPadding;
    protected const float LayoutTabsHeight = Dialog_DiplomacyDialogue.LayoutTabsHeight;
    protected const float LayoutTabsSpacing = Dialog_DiplomacyDialogue.LayoutTabsSpacing;
    protected const float LayoutTraderCardHeight = Dialog_DiplomacyDialogue.LayoutTraderCardHeight;
    protected const float LayoutTraderCardSpacing = Dialog_DiplomacyDialogue.LayoutTraderCardSpacing;
    protected const float LayoutTitleWeatherLineTopPadding = Dialog_DiplomacyDialogue.LayoutTitleWeatherLineTopPadding;
    protected const float LayoutTitleBarHeight = Dialog_DiplomacyDialogue.LayoutTitleBarHeight;
    protected const float LayoutTitleLeftPadding = Dialog_DiplomacyDialogue.LayoutTitleLeftPadding;
    protected const float LayoutTitleTopPadding = Dialog_DiplomacyDialogue.LayoutTitleTopPadding;
    protected const float LayoutTitleRightPadding = Dialog_DiplomacyDialogue.LayoutTitleRightPadding;
    protected const float LayoutTitleFactionLineTopPadding = Dialog_DiplomacyDialogue.LayoutTitleFactionLineTopPadding;
    protected const float LayoutTitleVersionLineTopPadding = Dialog_DiplomacyDialogue.LayoutTitleVersionLineTopPadding;
    protected const float LayoutTitleVersionLineHeight = Dialog_DiplomacyDialogue.LayoutTitleVersionLineHeight;
    protected const float LayoutTitleVersionLineGap = Dialog_DiplomacyDialogue.LayoutTitleVersionLineGap;
    protected const float LayoutTitleVersionChoiceWidth = Dialog_DiplomacyDialogue.LayoutTitleVersionChoiceWidth;
    protected const float LayoutTitleVersionChoiceHeight = Dialog_DiplomacyDialogue.LayoutTitleVersionChoiceHeight;
    protected const float LayoutTitleVersionChoiceGap = Dialog_DiplomacyDialogue.LayoutTitleVersionChoiceGap;
    protected const float LayoutTitleVersionChoiceTotalWidth = Dialog_DiplomacyDialogue.LayoutTitleVersionChoiceTotalWidth;
    protected const float LayoutTitleVersionRightPadding = Dialog_DiplomacyDialogue.LayoutTitleVersionRightPadding;
    protected const float LayoutCloseButtonSize = Dialog_DiplomacyDialogue.LayoutCloseButtonSize;
    protected const float LayoutFactionInnerPadding = Dialog_DiplomacyDialogue.LayoutFactionInnerPadding;
    protected const float LayoutFactionHeaderHeight = Dialog_DiplomacyDialogue.LayoutFactionHeaderHeight;
    protected const float LayoutFactionHeaderButtonSize = Dialog_DiplomacyDialogue.LayoutFactionHeaderButtonSize;
    protected const float LayoutFactionRowHeight = Dialog_DiplomacyDialogue.LayoutFactionRowHeight;
    protected const float LayoutFactionRowSpacing = Dialog_DiplomacyDialogue.LayoutFactionRowSpacing;
    protected const float LayoutFactionVerticalLineY = Dialog_DiplomacyDialogue.LayoutFactionVerticalLineY;
    protected const float LayoutGoodwillAnimOffsetX = Dialog_DiplomacyDialogue.LayoutGoodwillAnimOffsetX;
    protected const float LayoutGoodwillAnimOffsetY = Dialog_DiplomacyDialogue.LayoutGoodwillAnimOffsetY;
    protected const float BlockedReasonAutoScrollSpeed = Dialog_DiplomacyDialogue.BlockedReasonAutoScrollSpeed;
    protected const float BlockedReasonAutoScrollPauseSeconds = Dialog_DiplomacyDialogue.BlockedReasonAutoScrollPauseSeconds;
    protected const string DialogueInputControlName = Dialog_DiplomacyDialogue.DialogueInputControlName;
    protected const float FallbackRetryButtonSize = Dialog_DiplomacyDialogue.FallbackRetryButtonSize;
    protected const float FallbackRetryButtonMargin = Dialog_DiplomacyDialogue.FallbackRetryButtonMargin;
    protected const float GOODWILL_VALUE_REVEAL_SECONDS = Dialog_DiplomacyDialogue.GOODWILL_VALUE_REVEAL_SECONDS;
    protected const float PendingAirdropDialogDelaySeconds = Dialog_DiplomacyDialogue.PendingAirdropDialogDelaySeconds;
    protected const float MaxTypewriterWaitSeconds = Dialog_DiplomacyDialogue.MaxTypewriterWaitSeconds;
    protected Rect windowRect
    {
        get => Owner.windowRect;
        set => Owner.windowRect = value;
    }
    protected bool forcePause
    {
        get => Owner.forcePause;
        set => Owner.forcePause = value;
    }
    protected bool doCloseX
    {
        get => Owner.doCloseX;
        set => Owner.doCloseX = value;
    }
    protected bool closeOnClickedOutside
    {
        get => Owner.closeOnClickedOutside;
        set => Owner.closeOnClickedOutside = value;
    }
    protected bool absorbInputAroundWindow
    {
        get => Owner.absorbInputAroundWindow;
        set => Owner.absorbInputAroundWindow = value;
    }
    protected bool closeOnCancel
    {
        get => Owner.closeOnCancel;
        set => Owner.closeOnCancel = value;
    }
    protected bool draggable
    {
        get => Owner.draggable;
        set => Owner.draggable = value;
    }
    protected bool closeOnAccept
    {
        get => Owner.closeOnAccept;
        set => Owner.closeOnAccept = value;
    }
    protected bool onlyOneOfTypeAllowed
    {
        get => Owner.onlyOneOfTypeAllowed;
        set => Owner.onlyOneOfTypeAllowed = value;
    }
    protected bool doWindowBackground
    {
        get => Owner.doWindowBackground;
        set => Owner.doWindowBackground = value;
    }
    protected SoundDef soundAppear
    {
        get => Owner.soundAppear;
        set => Owner.soundAppear = value;
    }
    protected SoundDef soundClose
    {
        get => Owner.soundClose;
        set => Owner.soundClose = value;
    }
    protected bool doCloseButton
    {
        get => Owner.doCloseButton;
        set => Owner.doCloseButton = value;
    }
    protected void Close(bool doCloseSound = true) => Owner.Close(doCloseSound);
}

