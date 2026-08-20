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

namespace Ustas.RimAI.Communication.Relations.UI
{
public class Dialog_DiplomacyDialogue : Window
    
{
        internal DiplomacyDialogueParts Parts;


        internal static bool SaveImageToAlbum(string sourcePath, AlbumImageEntry metadata, out string error)
        {
            error = string.Empty;
            if (!DiplomacyAlbumService.SaveToAlbum(sourcePath, metadata, out AlbumImageEntry savedEntry, out error))
            {
                return false;
            }

            GameComponent_DiplomacyManager.Instance?.AddAlbumEntry(savedEntry);
            return true;
        }


        internal Faction faction;

        internal readonly Pawn negotiator;

        internal DialogueRuntimeContext runtimeContext;

        internal string windowLifecycleKey;

        internal readonly string windowInstanceId = Guid.NewGuid().ToString("N");

        internal FactionDialogueSession session;

        internal DialogueCloseIntent closeIntent = DialogueCloseIntent.Normal;

        internal readonly DiplomacyConversationController conversationController = new DiplomacyConversationController();

        internal string inputText = "";

        internal readonly List<string> inputHistory = new List<string>();

        internal int inputHistoryIndex = -1;

        internal string inputHistoryDraft = string.Empty;

        internal Vector2 messageScrollPosition = Vector2.zero;

        internal Vector2 factionScrollPosition = Vector2.zero;

        internal string blockedReasonScrollText = string.Empty;

        internal float blockedReasonAutoScrollOffset = 0f;

        internal int blockedReasonAutoScrollDirection = 1;

        internal float blockedReasonAutoScrollPauseUntil = 0f;

        internal float blockedReasonAutoScrollLastRealtime = -1f;

        internal int lastMessageCount = 0;

        internal int sessionMessageBaselineCount;

        internal bool sessionCloseSummaryCommitted = false;

        internal bool userIsScrolling = false;

        internal Vector2 lastWindowScreenPos = Vector2.zero;

        internal Rect lastMessagesViewRect = Rect.zero;

        internal Rect lastWindowContentRect = Rect.zero;

        internal const int MAX_INPUT_LENGTH = 500;

        internal const float FACTION_LIST_WIDTH = 160f;

        internal const float INPUT_AREA_HEIGHT = 64f;

        internal const float STRATEGY_BAR_HEIGHT = 36f;

        internal const float TIME_GAP_THRESHOLD_MINUTES = 15f;

        internal const float BUBBLE_CORNER_RADIUS = 12f;

        internal const float LayoutHeaderTop = 37f;

        internal const float LayoutPanelPadding = 8f;

        internal const float LayoutTabsHeight = 20f;

        internal const float LayoutTabsSpacing = 4f;

        internal const float LayoutTraderCardHeight = 40f;

        internal const float LayoutTraderCardSpacing = 42f;

        internal const float LayoutTitleWeatherLineTopPadding = 10f;

        internal const float LayoutTitleBarHeight = 32f;

        internal const float LayoutTitleLeftPadding = 10f;

        internal const float LayoutTitleTopPadding = 5f;

        internal const float LayoutTitleRightPadding = 10f;

        internal const float LayoutTitleFactionLineTopPadding = 8f;

        internal const float LayoutTitleVersionLineTopPadding = 9f;

        internal const float LayoutTitleVersionLineHeight = 16f;

        internal const float LayoutTitleVersionLineGap = 6f;

        internal const float LayoutTitleVersionChoiceWidth = 220f;

        internal const float LayoutTitleVersionChoiceHeight = 28f;

        internal const float LayoutTitleVersionChoiceGap = 6f;

        internal const float LayoutTitleVersionChoiceTotalWidth = LayoutTitleVersionChoiceWidth * 2f + LayoutTitleVersionChoiceGap;

        internal const float LayoutTitleVersionRightPadding = 75f;

        internal const float LayoutCloseButtonSize = 30f;

        internal const float LayoutFactionInnerPadding = 6f;

        internal const float LayoutFactionHeaderHeight = 28f;

        internal const float LayoutFactionHeaderButtonSize = 20f;

        internal const float LayoutFactionRowHeight = 48f;

        internal const float LayoutFactionRowSpacing = 4f;

        internal const float LayoutFactionVerticalLineY = 26f;

        internal const float LayoutGoodwillAnimOffsetX = 47f;

        internal const float LayoutGoodwillAnimOffsetY = 18f;

        internal const float BlockedReasonAutoScrollSpeed = 18f;

        internal const float BlockedReasonAutoScrollPauseSeconds = 0.6f;

        internal const string DialogueInputControlName = "DialogueInput";

        internal const float FallbackRetryButtonSize = 18f;

        internal const float FallbackRetryButtonMargin = 8f;


        internal readonly Dictionary<Faction, Rect> factionRowRects = new Dictionary<Faction, Rect>();

        internal readonly Dictionary<Faction, float> goodwillValueRevealUntil = new Dictionary<Faction, float>();

        internal readonly Dictionary<Faction, float> goodwillHoverAlpha = new Dictionary<Faction, float>();

        internal const float GOODWILL_VALUE_REVEAL_SECONDS = 2.5f;


        internal Dictionary<DialogueMessageData, TypewriterState> typewriterStates = new Dictionary<DialogueMessageData, TypewriterState>();

        internal float lastTypewriterUpdate = 0f;

        internal bool _typewriterDirty = true;

        internal bool fallbackRetryRequestedThisFrame;


        internal const float PendingAirdropDialogDelaySeconds = 1f;

        internal const float MaxTypewriterWaitSeconds = 8f;


        internal PendingAirdropDialogState pendingAirdropDialogState;


        internal float socialExpAnimStartTime = -100f;

        internal int lastExpAmount = 0;


        internal Sustainer sustainer;


        public override Vector2 InitialSize => new Vector2(960f, 720f);


        public Dialog_DiplomacyDialogue(
            Faction faction,
            Pawn negotiator = null,
            bool muteOpenSound = false,
            DialogueRuntimeContext runtimeContext = null,
            string windowLifecycleKey = null)
        {
            Parts = new DiplomacyDialogueParts(this);
            this.negotiator = DiplomacyDialogueFactionList.ResolveAutoNegotiator(negotiator);
            if (this.negotiator != null)
            {
                GameComponent_DiplomacyManager.Instance?.SetLastNegotiatorThingId(this.negotiator.thingIDNumber);
            }
            closeOnClickedOutside = false;
            absorbInputAroundWindow = false;
            doCloseX = false; // disabled: bezel covers the default position; custom close button in DrawTitleBar
            closeOnAccept = false;
            closeOnCancel = true;
            onlyOneOfTypeAllowed = false;
            forcePause = false;
            draggable = true;
            doWindowBackground = false;

            // Apply terminal scale override (non-Auto modes modify Prefs.UIScale directly)
            Parts.Chrome.ApplyTerminalScale();

            if (!muteOpenSound)
            {
                this.soundAppear = DefDatabase<SoundDef>.GetNamed("CommsWindow_Open");
            }
            this.soundClose = DefDatabase<SoundDef>.GetNamed("RimChat_DiplomacyConversationEndedByAi");

            Parts.Presenter.BindActiveFactionState(faction, runtimeContext, windowLifecycleKey);
            Parts.Presence.RefreshPresenceOnDialogueOpen();

            // Pause once on open; player can manually unpause (forcePause=false)
            Find.TickManager.Pause();

            GoodwillChangeAnimator.OnGoodwillChanged += Parts.Presenter.OnGoodwillChanged;

            Log.Message($"[RimAI.Relations] Dialogue opened with {faction.Name}, messages: {session?.messages.Count ?? 0}, AI configured: {AIChatServiceAsync.Instance.IsConfigured()}");
        }


        public override void PostOpen()
        {
            base.PostOpen();
            Parts.MemorySync.SubscribeToDiplomacyMemoryChanges();
            if (this.sustainer == null)
            {
                SoundDef ambience = DefDatabase<SoundDef>.GetNamed("RadioComms_Ambience", false);
                if (ambience != null)
                {
                    SoundInfo info = SoundInfo.OnCamera(MaintenanceType.None);
                    this.sustainer = ambience.TrySpawnSustainer(info);
                }
            }
        }


        public override void PreClose()
        {
            Parts.Chrome.RestoreTerminalScale();
            Parts.MemorySync.UnsubscribeFromDiplomacyMemoryChanges();
            Parts.StrategyRequest.CancelStrategySuggestionRequest();
            Parts.AirdropAsync.CancelPendingAirdropSelectionRequest();

            if (!Parts.Presenter.IsSwitchingFactionOnClose())
            {
                Parts.Session.TryCommitDiplomacySessionSummaryOnClose();
                Parts.Presence.LockPresenceCacheOnDialogueClose();
            }

            conversationController.CloseLease(session);
            Parts.Feedback.CancelAllBackgroundDialogueRequests();

            if (this.sustainer != null)
            {
                this.sustainer.End();
                this.sustainer = null;
            }
            base.PreClose();
            GoodwillChangeAnimator.OnGoodwillChanged -= Parts.Presenter.OnGoodwillChanged;

            typewriterStates.Clear();
            Parts.Airdrop.ClearPendingAirdropDialogState("window_closed", false);
            DiplomacyDialogueImageCache.ClearInlineImageTextureCache();
            Parts.Input.ResetBlockedReasonAutoScroll(true);
        }


        internal int _frameDiagThrottle;

        internal int _lastFrameDiagFactionId = -1;


        public override void DoWindowContents(Rect inRect)
        {
            long frameStart = System.Diagnostics.Stopwatch.GetTimestamp();

            // Layer 1: CRT bezel frame (outermost background)
            Parts.Chrome.DrawCRTBezelBackground(inRect);

            // Shrink content area inward so nothing is hidden behind the bezel frame
            Rect crtContent = DiplomacyDialogueChrome.ShrinkForBezel(inRect);
            // Offset content right and down
            crtContent = new Rect(crtContent.x + 8f, crtContent.y + 8f, crtContent.width - 8f, crtContent.height - 8f);

            Parts.MemorySync.PollDiplomacyMemoryRevision();
            long tPre1 = System.Diagnostics.Stopwatch.GetTimestamp();
            Parts.MemorySync.ApplyPendingDiplomacyMemoryRefresh();
            long tPre2 = System.Diagnostics.Stopwatch.GetTimestamp();
            lastWindowScreenPos = new Vector2(crtContent.x, crtContent.y);
            lastWindowContentRect = crtContent;
            Parts.HoverCard.speakerHoverRequestThisFrame = false;
            Parts.MessageView.UpdateTypewriterEffect();

            if (session != null && faction != null)
                Parts.AirdropConfirmUi.TryAutoCleanupStaleAirdropConfirmation(session, faction);

            Parts.Chrome.DrawTitleBar(crtContent);
            long t1 = System.Diagnostics.Stopwatch.GetTimestamp();

            Rect factionListRect = new Rect(
                crtContent.x,
                crtContent.y + LayoutHeaderTop,
                FACTION_LIST_WIDTH,
                crtContent.height - LayoutHeaderTop - LayoutPanelPadding);
            Parts.FactionList.DrawFactionList(factionListRect);
            long t2 = System.Diagnostics.Stopwatch.GetTimestamp();

            float rightX = crtContent.x + FACTION_LIST_WIDTH + LayoutPanelPadding;
            float rightWidth = crtContent.width - FACTION_LIST_WIDTH - LayoutPanelPadding;
            float contentY = LayoutHeaderTop;

            long t2a = System.Diagnostics.Stopwatch.GetTimestamp();
            Rect tabsRect = new Rect(rightX, crtContent.y + contentY, rightWidth, LayoutTabsHeight);
            contentY += Parts.SocialView.DrawDialogueMainTabs(tabsRect) + LayoutTabsSpacing;
            long t2b = System.Diagnostics.Stopwatch.GetTimestamp();

            if (Parts.SocialView.IsChatTabActive())
            {
                TradeShip tradeShip = Parts.FactionList.GetTradeShip();
                if (tradeShip != null)
                {
                    Rect cardRect = new Rect(rightX, crtContent.y + contentY, rightWidth, LayoutTraderCardHeight);
                    Parts.FactionList.DrawOrbitalTraderCard(cardRect, tradeShip);
                    contentY += LayoutTraderCardSpacing;
                }

            }
            long t2c = System.Diagnostics.Stopwatch.GetTimestamp();

            float contentHeight = crtContent.height - contentY - LayoutPanelPadding;
            Rect rightPanelRect = new Rect(rightX, crtContent.y + contentY, rightWidth, contentHeight);
            if (Parts.SocialView.IsChatTabActive())
            {
                Parts.MessageView.DrawChatArea(rightPanelRect);
            }
            else
            {
                Parts.SocialView.DrawSocialCirclePanel(rightPanelRect);
            }
            long t3 = System.Diagnostics.Stopwatch.GetTimestamp();

            // Layer 2: CRT overlay (green tint + scanlines + vignette on content)
            // DrawCRTOverlay(crtContent); // Disabled: CRT mask effect removed

            // Layer 3: goodwill animations and hover cards (topmost interactive layer)
            GoodwillChangeAnimator.UpdateAndDrawAnimations();
            Parts.HoverCardDraw.DrawSpeakerHoverCard();
            long t4 = System.Diagnostics.Stopwatch.GetTimestamp();

            int factionId = faction?.loadID ?? -1;
            bool factionChanged = factionId != _lastFrameDiagFactionId;
            _lastFrameDiagFactionId = factionId;
            _frameDiagThrottle++;
            double freq = System.Diagnostics.Stopwatch.Frequency;
            double totalMs = (t4 - frameStart) * 1000.0 / freq;
            // Log on faction switch or every 300 frames to avoid spam
            if (factionChanged || (_frameDiagThrottle % 300 == 0 && totalMs > 3.0))
            {
                double memRefreshMs = (tPre2 - tPre1) * 1000.0 / freq;
                double titleMs = (t1 - frameStart) * 1000.0 / freq;
                double listMs = (t2 - t1) * 1000.0 / freq;
                double tabsMs = (t2b - t2a) * 1000.0 / freq;
                double actionsMs = (t2c - t2b) * 1000.0 / freq;
                double chatMs = (t3 - t2c) * 1000.0 / freq;
                double overlayMs = (t4 - t3) * 1000.0 / freq;
                Log.Message($"[RimAI.Relations][FrameDiag] {faction?.Name}: mem={memRefreshMs:F1}ms, title={titleMs:F1}ms, list={listMs:F1}ms, tabs={tabsMs:F1}ms, actions={actionsMs:F1}ms, chat={chatMs:F1}ms, overlay={overlayMs:F1}ms, total={totalMs:F1}ms");
            }
        }


        internal List<Faction> _cachedFactionList;

        internal int _cachedFactionListTick = -1;

        internal Faction _cachedQuestFaction;

        internal List<Quest> _cachedQuests;

        internal int _cachedQuestsTick = -1;


        internal static Texture2D _whiteTexture;

        internal static Texture2D WhiteTexture => _whiteTexture;

        
        static Dialog_DiplomacyDialogue()
        {
            _whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _whiteTexture.SetPixel(0, 0, Color.white);
            _whiteTexture.Apply();
            DiplomacyDialogueChrome.InitTerminalTheme();
        }


        internal static Texture2D _circleTexture;

        internal static Texture2D CircleTexture
        {
            get
            {
                if (_circleTexture == null)
                {
                    int radius = 32;
                    int size = radius * 2;
                    _circleTexture = new Texture2D(size, size, TextureFormat.ARGB32, false);
                    Color[] pixels = new Color[size * size];
                    Vector2 center = new Vector2(radius, radius);
                    for (int y = 0; y < size; y++)
                    {
                        for (int x = 0; x < size; x++)
                        {
                            float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                            float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                            pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                        }
                    }
                    _circleTexture.SetPixels(pixels);
                    _circleTexture.Apply();
                }
                return _circleTexture;
            }
        }


        public bool MatchesWindowLifecycleKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            return string.Equals(windowLifecycleKey, key.Trim(), StringComparison.Ordinal);
        }

    }
}
