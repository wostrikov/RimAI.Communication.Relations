using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

using IntentActionCategory = Ustas.RimAI.Communication.Relations.UI.RPGPawnDialogueActionPolicies.IntentActionCategory;
using ActionFeedbackEntry = Ustas.RimAI.Communication.Relations.UI.RPGPawnDialogueFeedbackOverlay.ActionFeedbackEntry;
using SessionDialogueRecord = Ustas.RimAI.Communication.Relations.UI.RPGPawnDialogueHistoryPanel.SessionDialogueRecord;
using SessionActionRecord = Ustas.RimAI.Communication.Relations.UI.RPGPawnDialogueHistoryPanel.SessionActionRecord;
using SessionActionOutcome = Ustas.RimAI.Communication.Relations.UI.RPGPawnDialogueHistoryPanel.SessionActionOutcome;

namespace Ustas.RimAI.Communication.Relations.UI
{
    /// <summary>/// Dependencies: RimWorld window/UI runtime, AI request callbacks, and RPG archive/session helpers.
 /// Responsibility: host the full-screen PawnRPG dialogue window and orchestrate live/history rendering.
 ///</summary>
    [StaticConstructorOnStartup]
    public class Dialog_RPGPawnDialogue : Window
    {
        internal Dialog_RPGPawnDialogueParts Parts;
        internal const float ActionFeedbackDefaultDuration = RPGPawnDialogueFeedbackOverlay.ActionFeedbackDefaultDuration;

        internal readonly Pawn initiator;
        internal readonly Pawn target;
        internal readonly string dialogueSessionId;
        internal readonly DialogueRuntimeContext runtimeContext;
        internal readonly string windowLifecycleKey;
        internal readonly string windowInstanceId = Guid.NewGuid().ToString("N");
        internal readonly RpgDialogueConversationController conversationController = new RpgDialogueConversationController();
        internal InitialRequestPromptCache initialRequestPromptCache;
        internal DialogueRequestLease activeRequestLease;
        internal DialogueRuntimeContext activeRequestRuntimeContext;
        internal bool isWindowClosing;
        internal string currentDialogueText = "";
        internal string displayedText = "";
        internal string userReplyText = "";
        internal int visibleChars = 0;
        internal float lastCharTime = 0f;
        
        // Typing State
        internal bool isTyping = false;
        
        // Logical States
        internal bool isSendingInitialMessage = false;
        internal bool isShowingUserText = false;
        internal bool isWaitingForDelayAfterUser = false;
        internal float timeUserTextFinished = 0f;
        
        // AI State
        internal bool aiResponseReady = false;
        internal string aiResponseText = "";
        internal DialogueResponseEnvelope pendingResponseEnvelope = null;

        internal bool isDialogueEndedByNpc = false;
        internal string dialogueEndReason = "";
        internal bool sessionCloseSummaryCommitted = false;
        internal bool archiveSessionFinalized = false;
        
        internal string currentSpeakerName = "";
        
        internal List<ChatMessageData> chatHistory = new List<ChatMessageData>();
        
        internal struct DialoguePage
        {
            public string speakerName;
            public string text;
        }

        internal sealed class InitialRequestPromptCache
        {
            public int ContextVersion;
            public string WindowKey = string.Empty;
            public string OwnerWindowId = string.Empty;
            public List<ChatMessageData> Messages = new List<ChatMessageData>();
        }

        internal List<DialoguePage> dialogPages = new List<DialoguePage>();
        internal bool isViewingHistory = false;
        internal int historyViewIndex = 0;
        
        internal const float DialogueBoxHeight = 260f;
        internal const float PortraitWidth = 400f;
        internal const float PortraitHeight = 500f;

        internal static float GetPortraitWidthScale(float bodySize) => Mathf.Clamp(Mathf.Sqrt(Mathf.Max(bodySize, 0.5f)), 0.7f, 1.5f);
        internal static float GetPortraitHeightScale(float bodySize) => Mathf.Clamp(Mathf.Max(bodySize, 0.5f), 0.7f, 2.0f);
        

        internal float TargetPortraitWidth => PortraitWidth * GetPortraitWidthScale(target?.BodySize ?? 1f);
        internal float TargetPortraitHeight => PortraitHeight * GetPortraitHeightScale(target?.BodySize ?? 1f);
        internal float InitiatorPortraitWidth => PortraitWidth * GetPortraitWidthScale(initiator?.BodySize ?? 1f);
        internal float InitiatorPortraitHeight => PortraitHeight * GetPortraitHeightScale(initiator?.BodySize ?? 1f);

        internal float globalFadeAlpha = 0f;
        internal float initiatorFadeAlpha = 0f;
        internal float targetFadeAlpha = 0f;

        // Dynamic dialogue box background color
        internal Color dialogueBoxCurrentColor = new Color(0.1f, 0.1f, 0.12f, 0.9f);
        internal Color dialogueBoxTargetColor = new Color(0.1f, 0.1f, 0.12f, 0.9f);
        internal const float DialogueBoxColorBlendSpeed = 2.5f;

        internal static readonly Color DialogueBoxDefaultColor = new Color(0.1f, 0.1f, 0.12f, 0.9f);
        internal static readonly Color DialogueBoxRomanceColor = new Color(0.50f, 0.44f, 0.45f, 0.9f);
        internal static readonly Color DialogueBoxNeutralColor  = new Color(0.13f, 0.17f, 0.24f, 0.9f);
        internal static readonly Color DialogueBoxPrisonerColor = new Color(0.22f, 0.13f, 0.07f, 0.9f);
        internal static readonly Color DialogueBoxHostileColor  = new Color(0.40f, 0.15f, 0.16f, 0.9f);
        internal bool firstTargetSentenceDone = false;
        internal const float FadeSpeed = 1.5f; // Real-time per second speed
        internal const string UserReplyInputControlName = "UserReplyInput";
        internal const string RpgStrictOutputContractReminder =
            "Strict RPG output contract: write natural dialogue as plain text. " +
            "Only if gameplay effects are needed, append exactly one raw JSON object in the form " +
            "{\"actions\":[...]} after the dialogue. " +
            "Never wrap dialogue into JSON fields like \"dialogue\", \"response\", or \"content\". " +
            "Inside each action object, use key \"action\" and optional flat fields " +
            "\"defName\"/\"amount\"/\"reason\".";

        public override Vector2 InitialSize => new Vector2(Verse.UI.screenWidth, Verse.UI.screenHeight);
        protected override float Margin => 0f;

        public Dialog_RPGPawnDialogue(Pawn initiator, Pawn target) : this(initiator, target, null)
        {
        }

        public Dialog_RPGPawnDialogue(
            Pawn initiator,
            Pawn target,
            string proactiveOpening,
            DialogueRuntimeContext runtimeContext = null,
            string windowLifecycleKey = null)
        {
            Parts = new Dialog_RPGPawnDialogueParts(this);
            this.initiator = initiator;
            this.target = target;
            string resolvedSessionId = runtimeContext?.DialogueSessionId;
            dialogueSessionId = string.IsNullOrWhiteSpace(resolvedSessionId)
                ? Guid.NewGuid().ToString("N")
                : resolvedSessionId;
            this.runtimeContext = runtimeContext ?? DialogueRuntimeContext.CreateRpg(initiator, target, initiator?.Map, dialogueSessionId);
            this.windowLifecycleKey = string.IsNullOrWhiteSpace(windowLifecycleKey)
                ? this.runtimeContext.WindowKey
                : windowLifecycleKey.Trim();
            this.doCloseX = false;
            this.doCloseButton = false;
            this.closeOnClickedOutside = false;
            this.closeOnAccept = false;
            this.closeOnCancel = true;
            this.absorbInputAroundWindow = true;
            this.forcePause = true;
            this.preventCameraMotion = true;
            this.doWindowBackground = false;
            RpgNpcDialogueArchiveManager.Instance.BeginPromptMemoryWarmup(target, initiator);

            bool hasProactiveOpening = !string.IsNullOrWhiteSpace(proactiveOpening);
            bool shouldSeedProactiveOpening = hasProactiveOpening;

            try
            {
                chatHistory = BuildRPGChatMessages();
            }
            catch (PromptRenderException ex)
            {
                chatHistory = new List<ChatMessageData>();
                ApplyPromptRenderFailure(ex);
                return;
            }

            if (!shouldSeedProactiveOpening || !TrySeedProactiveOpening(proactiveOpening))
            {
                try
                {
                    PrepareInitialRequestPromptCache();
                }
                catch (PromptRenderException ex)
                {
                    ApplyPromptRenderFailure(ex);
                    return;
                }
                SendInitialMessage();
            }
        }

        internal List<ChatMessageData> BuildRPGChatMessages()
        {
            return new List<ChatMessageData>();
        }

        

        

        

        internal float inputAlpha = 0.3f;

        

        

        

        

        

        internal RenderTexture initiatorRT;
        internal RenderTexture targetRT;

        public override void PreClose()
        {
            isWindowClosing = true;
            CloseActiveRequestLease();
            TryFinalizeArchiveSessionOnClose();
            TryCommitRpgSessionSummaryOnClose();
            base.PreClose();
            if (initiatorRT != null) { UnityEngine.Object.Destroy(initiatorRT); initiatorRT = null; }
            if (targetRT != null) { UnityEngine.Object.Destroy(targetRT); targetRT = null; }
        }

        

        

        

        

        

        

        

        internal static bool IsImeComposing()
        {
            return !string.IsNullOrEmpty(Input.compositionString);
        }

        internal static bool IsUserReplyInputFocused()
        {
            return GUI.GetNameOfFocusedControl() == UserReplyInputControlName;
        }

        internal bool CanSendUserReplyFromKeyboard()
        {
            return !isDialogueEndedByNpc && !string.IsNullOrWhiteSpace(userReplyText);
        }

        

        

        

        internal static bool IsSystemRole(string role)
        {
            return string.Equals(role, "system", StringComparison.OrdinalIgnoreCase);
        }

        internal static string BuildRpgThinkingText(string dots)
        {
            return "RimChat_RPGThinking".Translate(dots);
        }

        internal static string BuildRpgOpponentThinkingText(string dots)
        {
            return "RimChat_RPGOpponentThinking".Translate(dots);
        }

        

        


        #region Facade forwards
        internal void DrawRpgPotentialActionsHint(Rect sendRect, float uiAlpha) => Parts.ActionHint.DrawRpgPotentialActionsHint(sendRect, uiAlpha);
        internal string GetRpgPotentialActionsTooltipText() => Parts.ActionHint.GetRpgPotentialActionsTooltipText();
        internal bool ExecuteTryGainMemory(LLMRpgApiResponse.ApiAction action) => Parts.ActionPolicies.ExecuteTryGainMemory(action);
        internal ThoughtDef ResolveTryGainMemoryThoughtDef(string requestedDefName, out string resolvedFrom) => Parts.ActionPolicies.ResolveTryGainMemoryThoughtDef(requestedDefName, out resolvedFrom);
        internal string BuildTryGainMemoryExamplesText() => Parts.ActionPolicies.BuildTryGainMemoryExamplesText();
        internal bool NotifyInvalidTryGainMemory(string requestedDefName) => Parts.ActionPolicies.NotifyInvalidTryGainMemory(requestedDefName);
        internal void LogTryGainMemoryResolution(string requestedDefName, string resolvedFrom, ThoughtDef def) => Parts.ActionPolicies.LogTryGainMemoryResolution(requestedDefName, resolvedFrom, def);
        internal void ApplyTryGainMemory(ThoughtDef def) => Parts.ActionPolicies.ApplyTryGainMemory(def);
        internal float GetMoodEffect(ThoughtDef def) => Parts.ActionPolicies.GetMoodEffect(def);
        internal static string NormalizeRpgActionName(string actionName) => RPGPawnDialogueActionPolicies.NormalizeRpgActionName(actionName);
        internal void EnsureRpgActionFallbacks(LLMRpgApiResponse apiResponse) => Parts.ActionPolicies.EnsureRpgActionFallbacks(apiResponse);
        internal void EnsureRpgExitActionFallback(LLMRpgApiResponse apiResponse) => Parts.ActionPolicies.EnsureRpgExitActionFallback(apiResponse);
        internal void EnsureRpgIntentDrivenActionMapping(LLMRpgApiResponse apiResponse, bool allowAutoMemoryFallback) => Parts.ActionPolicies.EnsureRpgIntentDrivenActionMapping(apiResponse, allowAutoMemoryFallback);
        internal static PromptPolicyConfig GetPromptPolicyForActionMapping() => RPGPawnDialogueActionPolicies.GetPromptPolicyForActionMapping();
        internal bool TryMapIntentDrivenAction(LLMRpgApiResponse apiResponse, int rounds, PromptPolicyConfig policy, bool allowAutoMemoryFallback) => Parts.ActionPolicies.TryMapIntentDrivenAction(apiResponse, rounds, policy, allowAutoMemoryFallback);
        internal IntentActionCategory ClassifyIntentActionCategory(string dialogueText) => Parts.ActionPolicies.ClassifyIntentActionCategory(dialogueText);
        internal bool TryMapStrongRejectToAction(LLMRpgApiResponse apiResponse) => Parts.ActionPolicies.TryMapStrongRejectToAction(apiResponse);
        internal bool TryMapSoftEndingToAction(LLMRpgApiResponse apiResponse) => Parts.ActionPolicies.TryMapSoftEndingToAction(apiResponse);
        internal bool TryMapCollaborationToAction(LLMRpgApiResponse apiResponse, int rounds, PromptPolicyConfig policy) => Parts.ActionPolicies.TryMapCollaborationToAction(apiResponse, rounds, policy);
        internal void EnsureRpgMemoryActionFallback(LLMRpgApiResponse apiResponse) => Parts.ActionPolicies.EnsureRpgMemoryActionFallback(apiResponse);
        internal void TryAddRoundMemoryFallback(LLMRpgApiResponse apiResponse, int rounds, float chance) => Parts.ActionPolicies.TryAddRoundMemoryFallback(apiResponse, rounds, chance);
        internal string ResolveAutoMemoryDefName(int rounds) => Parts.ActionPolicies.ResolveAutoMemoryDefName(rounds);
        internal ThoughtDef ResolveAutoMemoryThoughtDef(int rounds) => Parts.ActionPolicies.ResolveAutoMemoryThoughtDef(rounds);
        internal int GetNpcDialogueRoundCount() => Parts.ActionPolicies.GetNpcDialogueRoundCount();
        internal bool HasRpgAction(LLMRpgApiResponse apiResponse, string actionName) => Parts.ActionPolicies.HasRpgAction(apiResponse, actionName);
        internal bool HasExitAction(LLMRpgApiResponse apiResponse) => Parts.ActionPolicies.HasExitAction(apiResponse);
        internal bool ShouldUseCooldownExitFallback(string text) => Parts.ActionPolicies.ShouldUseCooldownExitFallback(text);
        internal bool ShouldUseNormalExitFallback(string text) => Parts.ActionPolicies.ShouldUseNormalExitFallback(text);
        internal void EnsureRpgMinimumActionCoverage(LLMRpgApiResponse apiResponse) => Parts.ActionPolicies.EnsureRpgMinimumActionCoverage(apiResponse);
        internal bool HasAnyRpgEffects(LLMRpgApiResponse apiResponse) => Parts.ActionPolicies.HasAnyRpgEffects(apiResponse);
        internal bool TryAddNoActionStreakMemoryFallback(LLMRpgApiResponse apiResponse) => Parts.ActionPolicies.TryAddNoActionStreakMemoryFallback(apiResponse);
        internal bool ShouldSuppressAutoMemoryFallback() => Parts.ActionPolicies.ShouldSuppressAutoMemoryFallback();
        internal bool ContainsAnyPhrase(string text, IReadOnlyList<string> hints) => Parts.ActionPolicies.ContainsAnyPhrase(text, hints);
        internal static bool MatchesPhraseWithBoundary(string text, string hint) => RPGPawnDialogueActionPolicies.MatchesPhraseWithBoundary(text, hint);
        internal static bool IsWordChar(char c) => RPGPawnDialogueActionPolicies.IsWordChar(c);
        internal static bool IsWordCharBefore(string text, int matchStart) => RPGPawnDialogueActionPolicies.IsWordCharBefore(text, matchStart);
        internal static bool IsWordCharAfter(string text, int matchEnd) => RPGPawnDialogueActionPolicies.IsWordCharAfter(text, matchEnd);
        internal static bool IsWordBoundaryBefore(string text, int position) => RPGPawnDialogueActionPolicies.IsWordBoundaryBefore(text, position);
        internal static bool IsWordBoundaryAfter(string text, int position) => RPGPawnDialogueActionPolicies.IsWordBoundaryAfter(text, position);
        internal void ApplyRPGAPIAndShowPopup(LLMRpgApiResponse apiRes) => Parts.Actions.ApplyRPGAPIAndShowPopup(apiRes);
        internal void ExecuteRpgActions(List<LLMRpgApiResponse.ApiAction> actions) => Parts.Actions.ExecuteRpgActions(actions);
        internal bool ExecuteRpgAction(LLMRpgApiResponse.ApiAction action) => Parts.Actions.ExecuteRpgAction(action);
        internal bool TryValidateRpgActionExecutionContext(out string reason) => Parts.Actions.TryValidateRpgActionExecutionContext(out reason);
        internal bool ExecuteExitDialogue(string reason, bool withCooldown) => Parts.Actions.ExecuteExitDialogue(reason, withCooldown);
        internal bool ExecuteRomanceAttempt(LLMRpgApiResponse.ApiAction action) => Parts.Actions.ExecuteRomanceAttempt(action);
        internal bool ExecuteMarriageProposal(LLMRpgApiResponse.ApiAction action) => Parts.Actions.ExecuteMarriageProposal(action);
        internal bool ExecuteBreakup(LLMRpgApiResponse.ApiAction action) => Parts.Actions.ExecuteBreakup(action);
        internal bool ExecuteDate(LLMRpgApiResponse.ApiAction action) => Parts.Actions.ExecuteDate(action);
        internal bool ExecuteDivorce(LLMRpgApiResponse.ApiAction action) => Parts.Actions.ExecuteDivorce(action);
        internal bool ExecuteTryAffectSocialGoodwill(LLMRpgApiResponse.ApiAction action) => Parts.Actions.ExecuteTryAffectSocialGoodwill(action);
        internal bool ExecuteReduceResistance(LLMRpgApiResponse.ApiAction action) => Parts.Actions.ExecuteReduceResistance(action);
        internal bool ExecuteReduceWill(LLMRpgApiResponse.ApiAction action) => Parts.Actions.ExecuteReduceWill(action);
        internal bool ExecuteRecruit() => Parts.Actions.ExecuteRecruit();
        internal bool ExecuteTryTakeOrderedJob(LLMRpgApiResponse.ApiAction action) => Parts.Actions.ExecuteTryTakeOrderedJob(action);
        internal bool ExecuteTriggerIncident(LLMRpgApiResponse.ApiAction action) => Parts.Actions.ExecuteTriggerIncident(action);
        internal bool ExecuteGrantInspiration(LLMRpgApiResponse.ApiAction action) => Parts.Actions.ExecuteGrantInspiration(action);
        internal bool ExecuteUnknownAction(string normalizedName, LLMRpgApiResponse.ApiAction action) => Parts.Actions.ExecuteUnknownAction(normalizedName, action);
        internal bool CanApplyRelationshipAction(string actionName) => Parts.Actions.CanApplyRelationshipAction(actionName);
        internal PawnRelationDef ResolveRelationDef(string actionName, PawnRelationDef relationDef, string defName) => Parts.Actions.ResolveRelationDef(actionName, relationDef, defName);
        internal bool HasPairRelation(PawnRelationDef relationDef) => Parts.Actions.HasPairRelation(relationDef);
        internal void AddPairRelation(PawnRelationDef relationDef) => Parts.Actions.AddPairRelation(relationDef);
        internal void RemovePairRelation(PawnRelationDef relationDef) => Parts.Actions.RemovePairRelation(relationDef);
        internal void ClearOtherSpousesForMarriage(Pawn pawn, Pawn keepPartner, PawnRelationDef spouseDef, PawnRelationDef exSpouseDef) => Parts.Actions.ClearOtherSpousesForMarriage(pawn, keepPartner, spouseDef, exSpouseDef);
        internal List<InspirationDef> BuildInspirationCandidates(string defName) => Parts.Actions.BuildInspirationCandidates(defName);
        internal bool TryStartInspiration(object handler, InspirationDef inspirationDef, string reason) => Parts.Actions.TryStartInspiration(handler, inspirationDef, reason);
        internal object[] BuildInspirationInvokeArgs(MethodInfo method, InspirationDef inspirationDef, string reason) => Parts.Actions.BuildInspirationInvokeArgs(method, inspirationDef, reason);
        internal bool ExecuteConvertIdeology(LLMRpgApiResponse.ApiAction action) => Parts.Actions.ExecuteConvertIdeology(action);
        internal bool ExecuteAdjustCertainty(LLMRpgApiResponse.ApiAction action) => Parts.Actions.ExecuteAdjustCertainty(action);
        internal void LogRpgActionDebug(string message) => Parts.Actions.LogRpgActionDebug(message);
        internal void HandleNpcExitDialogue(string reason) => Parts.Actions.HandleNpcExitDialogue(reason);
        internal void NotifyActionSuccess(string actionName, LLMRpgApiResponse.ApiAction action) => Parts.Actions.NotifyActionSuccess(actionName, action);
        internal string ResolveActionDetailForHistory(string actionName, LLMRpgApiResponse.ApiAction action) => Parts.Actions.ResolveActionDetailForHistory(actionName, action);
        internal void NotifyActionFailure(string actionName, string reason) => Parts.Actions.NotifyActionFailure(actionName, reason);
        internal void NotifyActionError(string actionName, string reason) => Parts.Actions.NotifyActionError(actionName, reason);
        internal string GetRpgActionLabel(string actionName) => Parts.Actions.GetRpgActionLabel(actionName);
        internal void AddActionFeedback(string text, Color color, float duration = ActionFeedbackDefaultDuration) => Parts.FeedbackOverlay.AddActionFeedback(text, color, duration);
        internal void AddActionFeedback(string text, string moodColoredText, Color color, Color moodColor, float duration = ActionFeedbackDefaultDuration) => Parts.FeedbackOverlay.AddActionFeedback(text, moodColoredText, color, moodColor, duration);
        internal void AddSystemFeedback(string text, float duration = ActionFeedbackDefaultDuration) => Parts.FeedbackOverlay.AddSystemFeedback(text, duration);
        internal void DrawActionFeedback(Rect inRect) => Parts.FeedbackOverlay.DrawActionFeedback(inRect);
        internal bool TryGetActionFeedbackAnchorRect(Rect inRect, out Rect anchorRect) => Parts.FeedbackOverlay.TryGetActionFeedbackAnchorRect(inRect, out anchorRect);
        internal void DrawActionFeedbackEntries(Rect anchorRect) => Parts.FeedbackOverlay.DrawActionFeedbackEntries(anchorRect);
        internal Rect BuildActionFeedbackRect(Rect anchorRect, float baseY, float height, ActionFeedbackEntry entry) => Parts.FeedbackOverlay.BuildActionFeedbackRect(anchorRect, baseY, height, entry);
        internal float CalculateActionFeedbackPanelHeight(string text) => Parts.FeedbackOverlay.CalculateActionFeedbackPanelHeight(text);
        internal float GetActionFeedbackTextWidth() => Parts.FeedbackOverlay.GetActionFeedbackTextWidth();
        internal void DrawActionFeedbackEntry(ActionFeedbackEntry entry, Rect subtitleRect) => Parts.FeedbackOverlay.DrawActionFeedbackEntry(entry, subtitleRect);
        internal void DrawActionFeedbackBackground(Rect subtitleRect, float alpha) => Parts.FeedbackOverlay.DrawActionFeedbackBackground(subtitleRect, alpha);
        internal void DrawActionFeedbackAccent(ActionFeedbackEntry entry, Rect subtitleRect, float alpha) => Parts.FeedbackOverlay.DrawActionFeedbackAccent(entry, subtitleRect, alpha);
        internal void DrawActionFeedbackText(ActionFeedbackEntry entry, Rect subtitleRect, float alpha) => Parts.FeedbackOverlay.DrawActionFeedbackText(entry, subtitleRect, alpha);
        internal void DrawBicolorFeedbackText(ActionFeedbackEntry entry, Rect textRect, float alpha) => Parts.FeedbackOverlay.DrawBicolorFeedbackText(entry, textRect, alpha);
        internal Rect GetActionFeedbackTextRect(Rect subtitleRect) => Parts.FeedbackOverlay.GetActionFeedbackTextRect(subtitleRect);
        internal Color GetActionFeedbackTextColor(Color sourceColor, float alpha) => Parts.FeedbackOverlay.GetActionFeedbackTextColor(sourceColor, alpha);
        internal float GetActionFeedbackRiseOffset(ActionFeedbackEntry entry) => Parts.FeedbackOverlay.GetActionFeedbackRiseOffset(entry);
        internal float GetActionFeedbackAlpha(ActionFeedbackEntry entry) => Parts.FeedbackOverlay.GetActionFeedbackAlpha(entry);
        internal float GetActionFeedbackVisibility() => Parts.FeedbackOverlay.GetActionFeedbackVisibility();
        internal void RemoveExpiredActionFeedback() => Parts.FeedbackOverlay.RemoveExpiredActionFeedback();
        internal void DrawRoundedRect(Rect rect, Color color, float radius) => Parts.FeedbackOverlay.DrawRoundedRect(rect, color, radius);
        internal static Texture2D CreateSubtitleCornerTexture() => RPGPawnDialogueFeedbackOverlay.CreateSubtitleCornerTexture();
        internal void DrawHistoryToggleButton(Rect boxRect) => Parts.HistoryPanel.DrawHistoryToggleButton(boxRect);
        internal void DrawSessionHistoryPanel(Rect inRect) => Parts.HistoryPanel.DrawSessionHistoryPanel(inRect);
        internal void DrawSessionHistoryPanelHeader(Rect panelRect) => Parts.HistoryPanel.DrawSessionHistoryPanelHeader(panelRect);
        internal void DrawSessionHistoryPanelBody(Rect panelRect) => Parts.HistoryPanel.DrawSessionHistoryPanelBody(panelRect);
        internal float CalculateSessionHistoryContentHeight(float width) => Parts.HistoryPanel.CalculateSessionHistoryContentHeight(width);
        internal void DrawSessionHistoryRecords(Rect viewRect) => Parts.HistoryPanel.DrawSessionHistoryRecords(viewRect);
        internal float MeasureSessionHistoryRecordHeight(SessionDialogueRecord record, float width) => Parts.HistoryPanel.MeasureSessionHistoryRecordHeight(record, width);
        internal void DrawSessionHistoryRecord(SessionDialogueRecord record, Rect rect, int index) => Parts.HistoryPanel.DrawSessionHistoryRecord(record, rect, index);
        internal void DeleteSessionRecord(int index) => Parts.HistoryPanel.DeleteSessionRecord(index);
        internal void ConfirmDeleteSessionRecord(int index) => Parts.HistoryPanel.ConfirmDeleteSessionRecord(index);
        internal void ClearAllSessionRecords() => Parts.HistoryPanel.ClearAllSessionRecords();
        internal void ConfirmClearAllSessionRecords() => Parts.HistoryPanel.ConfirmClearAllSessionRecords();
        internal static float CalcHeightWithFont(string text, float width, GameFont font) => RPGPawnDialogueHistoryPanel.CalcHeightWithFont(text, width, font);
        internal string BuildSessionActionLine(SessionActionRecord action) => Parts.HistoryPanel.BuildSessionActionLine(action);
        internal static SessionActionRecord GetFinalSuccessfulAction(SessionDialogueRecord record) => RPGPawnDialogueHistoryPanel.GetFinalSuccessfulAction(record);
        internal static string BuildSpeakerLine(string speakerName) => RPGPawnDialogueHistoryPanel.BuildSpeakerLine(speakerName);
        internal bool TryHandleHistoryPanelMouseDown(Event current) => Parts.HistoryPanel.TryHandleHistoryPanelMouseDown(current);
        internal void RecordSessionDialogueTurn(string speakerName, string text, bool isPlayerSpeaker) => Parts.HistoryPanel.RecordSessionDialogueTurn(speakerName, text, isPlayerSpeaker);
        internal void RecordSessionActionOutcome(string actionName, SessionActionOutcome outcome, string reason, string detail = "") => Parts.HistoryPanel.RecordSessionActionOutcome(actionName, outcome, reason, detail);
        internal SessionDialogueRecord FindLatestNpcSessionRecord() => Parts.HistoryPanel.FindLatestNpcSessionRecord();
        public bool MatchesWindowLifecycleKey(string key) => Parts.Lifecycle.MatchesWindowLifecycleKey(key);
        internal void CloseActiveRequestLease() => Parts.Lifecycle.CloseActiveRequestLease();
        internal void ReleaseActiveRequestLease() => Parts.Lifecycle.ReleaseActiveRequestLease();
        internal void PrepareEnvelopeForDisplay(DialogueResponseEnvelope envelope) => Parts.Lifecycle.PrepareEnvelopeForDisplay(envelope);
        internal void TryApplyPendingEnvelope() => Parts.Lifecycle.TryApplyPendingEnvelope();
        internal void HandleDroppedResponse(string reason) => Parts.Lifecycle.HandleDroppedResponse(reason);
        internal bool IsInspectPaneShowing() => Parts.PawnMenu.IsInspectPaneShowing();
        internal Rect GetInspectPaneOverlapRect() => Parts.PawnMenu.GetInspectPaneOverlapRect();
        internal void DrawPawnNameWithMenu(Rect nameRect, Pawn pawn, string displayName, bool rightAligned) => Parts.PawnMenu.DrawPawnNameWithMenu(nameRect, pawn, displayName, rightAligned);
        internal void ShowPawnMenu(Pawn pawn) => Parts.PawnMenu.ShowPawnMenu(pawn);
        internal static void ShowPawnMenuStatic(Pawn pawn) => RPGPawnDialoguePawnMenu.ShowPawnMenuStatic(pawn);
        internal void OpenPawnTab(Pawn pawn, Type itabType) => Parts.PawnMenu.OpenPawnTab(pawn, itabType);
        internal static void OpenPawnTabStatic(Pawn pawn, Type itabType) => RPGPawnDialoguePawnMenu.OpenPawnTabStatic(pawn, itabType);
        internal static bool HasLoveRelation(Pawn target, Pawn initiator) => RPGPawnDialoguePortraitDrag.HasLoveRelation(target, initiator);
        internal static bool HasSpouseRelation(Pawn target, Pawn initiator) => RPGPawnDialoguePortraitDrag.HasSpouseRelation(target, initiator);
        internal Rect GetInitiatorDragRect(Rect inRect) => Parts.PortraitDrag.GetInitiatorDragRect(inRect);
        internal void UpdatePortraitDrag(Rect inRect, float deltaTime) => Parts.PortraitDrag.UpdatePortraitDrag(inRect, deltaTime);
        internal void UpdateCollisionDetection(Rect inRect) => Parts.PortraitDrag.UpdateCollisionDetection(inRect);
        internal void UpdateCollisionAnimation(float deltaTime) => Parts.PortraitDrag.UpdateCollisionAnimation(deltaTime);
        internal Rect RectIntersect(Rect a, Rect b) => Parts.PortraitDrag.RectIntersect(a, b);
        internal void DrawInitiatorPortraitWithDrag(Rect inRect) => Parts.PortraitDrag.DrawInitiatorPortraitWithDrag(inRect);
        internal bool TryStartInitiatorDrag(Rect inRect) => Parts.PortraitDrag.TryStartInitiatorDrag(inRect);
        internal void ShowCollisionMenu() => Parts.PortraitDrag.ShowCollisionMenu();
        internal void ExecuteCollisionAction(string actionName) => Parts.PortraitDrag.ExecuteCollisionAction(actionName);
        internal void DrawPortraits(Rect inRect) => Parts.Portraits.DrawPortraits(inRect);
        internal Rect GetTargetPortraitRect(Rect inRect) => Parts.Portraits.GetTargetPortraitRect(inRect);
        internal Rect GetInitiatorPortraitRect(Rect inRect) => Parts.Portraits.GetInitiatorPortraitRect(inRect);
        internal static float CappedHeight(float desired, Rect inRect) => RPGPawnDialoguePortraits.CappedHeight(desired, inRect);
        internal string NormalizeHistoryAssistantContent(DialogueResponseEnvelope envelope, string visibleDialogueText) => Parts.RequestContext.NormalizeHistoryAssistantContent(envelope, visibleDialogueText);
        internal string ExtractNarrativeOnly(string rawResponse) => Parts.RequestContext.ExtractNarrativeOnly(rawResponse);
        internal string NormalizeVisibleNpcDialogueText(string content) => Parts.RequestContext.NormalizeVisibleNpcDialogueText(content);
        internal string NormalizeEnvelopeVisibleDialogueForDisplay(DialogueResponseEnvelope envelope, string sourceTag) => Parts.RequestContext.NormalizeEnvelopeVisibleDialogueForDisplay(envelope, sourceTag);
        internal static string CollapseWhitespace(string content) => RPGPawnDialogueRequestContext.CollapseWhitespace(content);
        internal bool ShouldApplyNonVerbalSpeechFormatting() => Parts.RequestContext.ShouldApplyNonVerbalSpeechFormatting();
        internal string EnsureNonVerbalSpeechFormat(string normalized) => Parts.RequestContext.EnsureNonVerbalSpeechFormat(normalized);
        internal static bool TryParseSoundThoughtPair(string text, out string sound, out string thought) => RPGPawnDialogueCharacterStyleOps.TryParseSoundThoughtPair(text, out sound, out thought);
        internal static bool UseFullWidthParentheses() => RPGPawnDialogueCharacterStyleOps.UseFullWidthParentheses();
        internal static bool IsNonVerbalSpeechPawn(Pawn pawn) => RPGPawnDialogueCharacterStyleOps.IsNonVerbalSpeechPawn(pawn);
        internal static bool IsAnimalPawn(Pawn pawn) => RPGPawnDialogueCharacterStyleOps.IsAnimalPawn(pawn);
        internal static bool IsMechanoidPawn(Pawn pawn) => RPGPawnDialogueCharacterStyleOps.IsMechanoidPawn(pawn);
        internal static bool IsBabyPawn(Pawn pawn) => RPGPawnDialogueCharacterStyleOps.IsBabyPawn(pawn);
        internal static string ResolveNonVerbalSpeakerKind(Pawn pawn) => RPGPawnDialogueCharacterStyleOps.ResolveNonVerbalSpeakerKind(pawn);
        internal static string ResolveRacialType(Pawn pawn) => RPGPawnDialogueCharacterStyleOps.ResolveRacialType(pawn);
        internal static string ResolveSocialIdentity(Pawn pawn) => RPGPawnDialogueCharacterStyleOps.ResolveSocialIdentity(pawn);
        internal static string ResolveRelationshipStatus(Pawn pawn) => RPGPawnDialogueCharacterStyleOps.ResolveRelationshipStatus(pawn);
        internal static string ResolvePersonalityTraits(Pawn pawn) => RPGPawnDialogueCharacterStyleOps.ResolvePersonalityTraits(pawn);
        internal static string BuildStyleGuidelines(Pawn pawn) => RPGPawnDialogueCharacterStyleOps.BuildStyleGuidelines(pawn);
        internal static void AppendTraitStyleGuidelines(Pawn pawn, List<string> guidelines) => RPGPawnDialogueCharacterStyleOps.AppendTraitStyleGuidelines(pawn, guidelines);
        internal static string ResolveDefaultNonVerbalSound(Pawn pawn) => RPGPawnDialogueCharacterStyleOps.ResolveDefaultNonVerbalSound(pawn);
        internal string ApplyNonVerbalSpeechFormatting(string basePrompt) => Parts.RequestContext.ApplyNonVerbalSpeechFormatting(basePrompt);
        internal string ApplyNonVerbalSpeechConstraintTemplate(string basePrompt) => Parts.RequestContext.ApplyNonVerbalSpeechConstraintTemplate(basePrompt);
        internal string ApplyCharacterStyleConstraint(string basePrompt) => Parts.RequestContext.ApplyCharacterStyleConstraint(basePrompt);
        internal static bool HasVisibleAssistantReply(IEnumerable<ChatMessageData> messages) => RPGPawnDialogueRequestContext.HasVisibleAssistantReply(messages);
        internal static string ExtractLatestVisibleUserIntent(IEnumerable<ChatMessageData> messages) => RPGPawnDialogueRequestContext.ExtractLatestVisibleUserIntent(messages);
        internal static bool IsPromptSeedUserMessage(string content) => RPGPawnDialogueRequestContext.IsPromptSeedUserMessage(content);
        internal string BuildRpgSystemPromptForRequest(bool openingTurn, string currentTurnUserIntent) => Parts.RequestContext.BuildRpgSystemPromptForRequest(openingTurn, currentTurnUserIntent);
        internal void UpdateRpgActionContractGuard(string prompt, bool rpgApiEnabled) => Parts.RequestContext.UpdateRpgActionContractGuard(prompt, rpgApiEnabled);
        internal static bool HasRpgActionContract(string prompt) => RPGPawnDialogueRequestContext.HasRpgActionContract(prompt);
        internal string ResolveDialogueTextForDisplay(bool drawLive, string speakerName, string fullText, Rect textArea) => Parts.TextPaging.ResolveDialogueTextForDisplay(drawLive, speakerName, fullText, textArea);
        internal bool CanPageCurrentDialogue(bool drawLive) => Parts.TextPaging.CanPageCurrentDialogue(drawLive);
        internal void EnsureDialogueTextPages(string fullText, string speakerName, Rect textArea, bool drawLive) => Parts.TextPaging.EnsureDialogueTextPages(fullText, speakerName, textArea, drawLive);
        internal bool RequiresDialogueTextPageRefresh(string fullText, string speakerName, Rect textArea, bool drawLive) => Parts.TextPaging.RequiresDialogueTextPageRefresh(fullText, speakerName, textArea, drawLive);
        internal void UpdateDialogueTextPageCache(string fullText, string speakerName, Rect textArea, bool drawLive) => Parts.TextPaging.UpdateDialogueTextPageCache(fullText, speakerName, textArea, drawLive);
        internal List<string> BuildDialogueTextPages(string fullText, float width, float height) => Parts.TextPaging.BuildDialogueTextPages(fullText, width, height);
        internal int FindDialoguePageLength(string fullText, int startIndex, float width, float height) => Parts.TextPaging.FindDialoguePageLength(fullText, startIndex, width, height);
        internal bool DoesDialoguePageFit(string fullText, int startIndex, int length, float width, float height) => Parts.TextPaging.DoesDialoguePageFit(fullText, startIndex, length, width, height);
        internal float CalcDialogueTextHeight(string text, float width) => Parts.TextPaging.CalcDialogueTextHeight(text, width);
        internal int AdjustDialoguePageLength(string fullText, int startIndex, int rawLength) => Parts.TextPaging.AdjustDialoguePageLength(fullText, startIndex, rawLength);
        internal static bool IsDialoguePageBoundary(char character) => RPGPawnDialogueTextPaging.IsDialoguePageBoundary(character);
        internal int SkipDialoguePageSeparators(string fullText, int startIndex) => Parts.TextPaging.SkipDialoguePageSeparators(fullText, startIndex);
        internal string ExtractDialoguePageText(string fullText, int startIndex, int length) => Parts.TextPaging.ExtractDialoguePageText(fullText, startIndex, length);
        internal void ResetDialogueTextPaging() => Parts.TextPaging.ResetDialogueTextPaging();
        internal void DrawDialogueNavigation(Rect boxRect) => Parts.TextPaging.DrawDialogueNavigation(boxRect);
        internal void DrawHistoryNavigation(Rect boxRect) => Parts.TextPaging.DrawHistoryNavigation(boxRect);
        internal void DrawTextPageNavigation(Rect boxRect) => Parts.TextPaging.DrawTextPageNavigation(boxRect);
        internal void DrawNavigationBox(Rect boxRect, bool canGoPrev, bool canGoNext, string counterLabel, Action onPrev, Action onNext) => Parts.TextPaging.DrawNavigationBox(boxRect, canGoPrev, canGoNext, counterLabel, onPrev, onNext);
        internal void DrawNavigationButton(Rect rect, bool enabled, string label, Action onClick) => Parts.TextPaging.DrawNavigationButton(rect, enabled, label, onClick);
        internal int GetCurrentDialogueDisplayIndex() => Parts.TextPaging.GetCurrentDialogueDisplayIndex();
        internal void ShowDialogueHistoryAt(int displayIndex) => Parts.TextPaging.ShowDialogueHistoryAt(displayIndex);
        internal void ChangeDialogueTextPage(int direction) => Parts.TextPaging.ChangeDialogueTextPage(direction);
        #endregion
    
        #region Cluster forwards
        internal static float GetPortraitZoom(float bodySize, bool humanlike) => RPGPawnDialogueSlice1.GetPortraitZoom(bodySize, humanlike);
        internal static List<string> ParseSceneTagsCsv(string csv) => RPGPawnDialogueSlice1.ParseSceneTagsCsv(csv);
        internal static string BuildProactiveOpeningCarryOverPrompt(string proactiveOpening) => RPGPawnDialogueSlice1.BuildProactiveOpeningCarryOverPrompt(proactiveOpening);
        internal bool TrySeedProactiveOpening(string proactiveOpening) => Parts.Slice1.TrySeedProactiveOpening(proactiveOpening);
        internal void SendInitialMessage() => Parts.Slice1.SendInitialMessage();
        internal void PrepareInitialRequestPromptCache() => Parts.Slice1.PrepareInitialRequestPromptCache();
        internal bool TryGetValidInitialRequestPromptMessages(out List<ChatMessageData> requestMessages) => Parts.Slice1.TryGetValidInitialRequestPromptMessages(out requestMessages);
        internal static List<ChatMessageData> CloneChatMessages(IEnumerable<ChatMessageData> source) => RPGPawnDialogueSlice1.CloneChatMessages(source);
        public override void DoWindowContents(Rect inRect) => Parts.Slice1.DoWindowContents(inRect);
        internal void TryFinalizeArchiveSessionOnClose() => Parts.Slice1.TryFinalizeArchiveSessionOnClose();
        internal void TryCommitRpgSessionSummaryOnClose() => Parts.Slice1.TryCommitRpgSessionSummaryOnClose();
        internal void DrawPawnPortrait(Rect rect, Pawn pawn, bool flip) => Parts.Slice1.DrawPawnPortrait(rect, pawn, flip);
        internal static void DrawPawnPortrait(Rect rect, Pawn pawn, bool flip, ref RenderTexture cachedRT, out bool created) => RPGPawnDialogueSlice1.DrawPawnPortrait(rect, pawn, flip, ref cachedRT, out created);
        internal void DrawDialogueBox(Rect inRect) => Parts.Slice2.DrawDialogueBox(inRect);
        internal bool ShouldSendFromKeyboard(Event current) => Parts.Slice2.ShouldSendFromKeyboard(current);
        internal static bool IsSubmitKeyPressed(Event current) => RPGPawnDialogueSlice2.IsSubmitKeyPressed(current);
        internal void TrySendMessage() => Parts.Slice2.TrySendMessage();
        internal void ApplyPromptRenderFailure(PromptRenderException ex) => Parts.Slice2.ApplyPromptRenderFailure(ex);
        internal List<ChatMessageData> BuildCompressedRpgRequestMessages() => Parts.Slice2.BuildCompressedRpgRequestMessages();
        internal Color ResolveDialogueBoxTargetColor() => Parts.Slice2.ResolveDialogueBoxTargetColor();
        internal void UpdateTyping() => Parts.Slice2.UpdateTyping();
        #endregion
}
    internal sealed class Dialog_RPGPawnDialogueParts
    {
        internal readonly Dialog_RPGPawnDialogue Owner;
        internal readonly RPGPawnDialogueActionHint ActionHint;
        internal readonly RPGPawnDialogueActionPolicies ActionPolicies;
        internal readonly RPGPawnDialogueActions Actions;
        internal readonly RPGPawnDialogueFeedbackOverlay FeedbackOverlay;
        internal readonly RPGPawnDialogueHistoryPanel HistoryPanel;
        internal readonly RPGPawnDialogueLifecycle Lifecycle;
        internal readonly RPGPawnDialoguePawnMenu PawnMenu;
        internal readonly RPGPawnDialoguePortraitDrag PortraitDrag;
        internal readonly RPGPawnDialoguePortraits Portraits;
        internal readonly RPGPawnDialogueRequestContext RequestContext;
        internal readonly RPGPawnDialogueTextPaging TextPaging;
        internal readonly RPGPawnDialogueSlice1 Slice1;
        internal readonly RPGPawnDialogueSlice2 Slice2;
        internal Dialog_RPGPawnDialogueParts(Dialog_RPGPawnDialogue owner)
        {
            Owner = owner;
            ActionHint = new RPGPawnDialogueActionHint(owner);
            ActionPolicies = new RPGPawnDialogueActionPolicies(owner);
            Actions = new RPGPawnDialogueActions(owner);
            FeedbackOverlay = new RPGPawnDialogueFeedbackOverlay(owner);
            HistoryPanel = new RPGPawnDialogueHistoryPanel(owner);
            Lifecycle = new RPGPawnDialogueLifecycle(owner);
            PawnMenu = new RPGPawnDialoguePawnMenu(owner);
            PortraitDrag = new RPGPawnDialoguePortraitDrag(owner);
            Portraits = new RPGPawnDialoguePortraits(owner);
            RequestContext = new RPGPawnDialogueRequestContext(owner);
            TextPaging = new RPGPawnDialogueTextPaging(owner);
            Slice1 = new RPGPawnDialogueSlice1(owner);
            Slice2 = new RPGPawnDialogueSlice2(owner);
        }
    }


}
