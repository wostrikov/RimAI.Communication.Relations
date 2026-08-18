using System;
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Dialogue;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Memory
{
    public enum AirdropExecutionStage
    {
        Idle = 0,
        SelectingCandidate = 1,
        PreparedAwaitingConfirm = 2,
        Committing = 3,
        Completed = 4,
        Failed = 5,
        Cancelled = 6
    }

    /// <summary>/// store单个factiondialoguesession的数据
 ///</summary>
    public class FactionDialogueSession : IExposable
    {
        public Faction faction;
        public List<DialogueMessageData> messages = new List<DialogueMessageData>();
        public int lastInteractionTick = 0;
        public bool hasUnreadMessages = false;
        public bool isConversationEndedByNpc = false;
        public bool allowReinitiate = false;
        public string conversationEndReason = "";
        public int conversationEndedTick = 0;
        public int reinitiateAvailableTick = 0;

        // AI requeststate (不save到存档, 重启后需要重新request)
        public string pendingRequestId = null;
        public DialogueRequestLease pendingRequestLease = null;
        public bool isWaitingForResponse = false;
        public int lastDiplomacyRequestQueuedTick = int.MinValue;
        public float lastDiplomacyRequestQueuedRealtime = -1f;
        public int pendingImageRequests = 0;
        public float aiRequestProgress = 0f;
        public string aiError = null;
        public string pendingAirdropRequestId = null;
        public DialogueRequestLease pendingAirdropRequestLease = null;
        public bool isWaitingForAirdropSelection = false;
        public float pendingAirdropRequestStartedRealtime = -1f;
        public int pendingAirdropRequestTimeoutSeconds = 0;
        public int airdropRequestGeneration = 0;
        public AirdropExecutionStage airdropExecutionStage = AirdropExecutionStage.Idle;
        public int airdropPreparedAwaitingConfirmTick = 0;
        public bool isWaitingForRansomTargetSelection = false;
        public int boundRansomTargetPawnLoadId = 0;
        public string boundRansomTargetFactionId = string.Empty;
        public bool hasCompletedRansomInfoRequest = false;
        public float ransomAutoReplyCooldownUntilRealtime = -1f;
        public string ransomAutoReplyCooldownCategory = string.Empty;
        public bool hasPendingRansomBatchSelection = false;
        public string pendingRansomBatchGroupId = string.Empty;
        public List<int> pendingRansomBatchTargetPawnLoadIds = new List<int>();
        public int pendingRansomBatchTotalCurrentAskSilver = 0;
        public int pendingRansomBatchTotalMinOfferSilver = 0;
        public int pendingRansomBatchTotalMaxOfferSilver = 0;
        public bool hasPendingRansomOfferReference = false;
        public int pendingRansomOfferTargetPawnLoadId = 0;
        public int pendingRansomOfferCurrentAskSilver = 0;
        public int pendingRansomOfferMinSilver = 0;
        public int pendingRansomOfferMaxSilver = 0;

        // Airdrop trade-card runtime reference (not persisted)
        public bool hasPendingAirdropTradeCardReference = false;
        public string pendingAirdropTradeCardNeed = string.Empty;
        public string pendingAirdropTradeCardNeedDefName = string.Empty;
        public string pendingAirdropTradeCardNeedLabel = string.Empty;
        public string pendingAirdropTradeCardNeedSearchText = string.Empty;
        public int pendingAirdropTradeCardRequestedCount = 0;
        public string pendingAirdropTradeCardPaymentItemDef = string.Empty;
        public string pendingAirdropTradeCardPaymentItemLabel = string.Empty;
        public int pendingAirdropTradeCardPaymentItemCount = 0;
        public string pendingAirdropTradeCardScenario = "trade";
        public int pendingAirdropTradeCardSubmittedTick = 0;
        public int pendingAirdropTradeCardShippingPodCount = 0;
        public int pendingAirdropTradeCardShippingCost = 0;

        // Last AI airdrop counteroffer cache (session-scoped)
        public string lastAirdropCounterofferDefName = string.Empty;
        public int lastAirdropCounterofferCount = 0;
        public int lastAirdropCounterofferSilver = 0;
        public string lastAirdropCounterofferReason = string.Empty;
        public int lastAirdropCounterofferTick = 0;
        
        // 策略建议运行态 (不save到存档)
        public List<PendingStrategySuggestion> pendingStrategySuggestions = new List<PendingStrategySuggestion>();
        public int strategyUsesConsumed = 0;

        // 外交延迟动作意图运行态 (不save到存档)
        public PendingDelayedActionIntent pendingDelayedActionIntent;
        public PendingDelayedActionIntent lastDelayedActionIntent;
        public string lastDelayedActionExecutionSignature = string.Empty;
        public int lastDelayedActionExecutionAssistantRound = -999;

        // Diplomacy fallback retry runtime state (not persisted)
        public string lastPlayerRequestText = string.Empty;
        public bool lastPlayerRequestWasAirdropTradeCard = false;
        public bool lastAssistantMessageWasImmersionFallback = false;
        public string lastAssistantVisibleText = string.Empty;

        // Periodic snapshot tracking: last message index already summarized to RPG archive
        // Increments on each periodic snapshot, never decreases. Guards against double-summarize.
        public int lastSummarizedMessageIndex = 0;

        // Version counter incremented on each message mutation for layout cache invalidation
        public int messageVersion = 0;

        public FactionDialogueSession() { }

        public FactionDialogueSession(Faction faction)
        {
            this.faction = faction;
        }

        public void AddMessage(
            string sender,
            string message,
            bool isPlayer,
            DialogueMessageType messageType = DialogueMessageType.Normal,
            Pawn speakerPawn = null)
        {
            var msg = new DialogueMessageData
            {
                sender = sender,
                message = message,
                isPlayer = isPlayer,
                messageType = messageType
            };
            msg.SetSpeakerPawn(speakerPawn);
            msg.SetTimestampFromCurrentGameTick();
            messages.Add(msg);
            lastInteractionTick = Find.TickManager.TicksGame;
            messageVersion++;
            if (isPlayer)
            {
                isConversationEndedByNpc = false;
                allowReinitiate = false;
                conversationEndReason = "";
                conversationEndedTick = 0;
                reinitiateAvailableTick = 0;
            }
            
            // 限制message数量, 避免存档过大
            if (messages.Count > 100)
            {
                messages.RemoveAt(0);
            }
        }

        public void AddImageMessage(
            string sender,
            string caption,
            bool isPlayer,
            string imageLocalPath,
            string imageSourceUrl,
            Pawn speakerPawn = null)
        {
            var msg = new DialogueMessageData
            {
                sender = sender,
                message = caption ?? string.Empty,
                isPlayer = isPlayer,
                messageType = DialogueMessageType.Image,
                imageLocalPath = imageLocalPath ?? string.Empty,
                imageSourceUrl = imageSourceUrl ?? string.Empty
            };
            msg.SetSpeakerPawn(speakerPawn);
            msg.SetTimestampFromCurrentGameTick();
            messages.Add(msg);
            lastInteractionTick = Find.TickManager.TicksGame;
            messageVersion++;

            if (messages.Count > 100)
            {
                messages.RemoveAt(0);
            }
        }

        public void AddAirdropTradeCardMessage(
            string sender,
            string message,
            bool isPlayer,
            string needDefName,
            string needLabel,
            int requestedCount,
            float needUnitPrice,
            float needReferenceTotalPrice,
            int shippingPodCount,
            int shippingCostSilver,
            string offerDefName,
            string offerLabel,
            int offerCount,
            float offerUnitPrice,
            float offerTotalPrice,
            Pawn speakerPawn = null)
        {
            var msg = new DialogueMessageData
            {
                sender = sender,
                message = message ?? string.Empty,
                isPlayer = isPlayer,
                messageType = DialogueMessageType.AirdropTradeCard
            };
            msg.SetAirdropTradeCardData(
                needDefName,
                needLabel,
                requestedCount,
                needUnitPrice,
                needReferenceTotalPrice,
                shippingPodCount,
                shippingCostSilver,
                offerDefName,
                offerLabel,
                offerCount,
                offerUnitPrice,
                offerTotalPrice);
            msg.SetSpeakerPawn(speakerPawn);
            msg.SetTimestampFromCurrentGameTick();
            messages.Add(msg);
            lastInteractionTick = Find.TickManager.TicksGame;
            messageVersion++;

            if (messages.Count > 100)
            {
                messages.RemoveAt(0);
            }
        }

        public void MarkConversationEnded(string reason, bool canReinitiate, int reinitiateCooldownTicks = 0)
        {
            isConversationEndedByNpc = true;
            conversationEndReason = reason ?? "";
            conversationEndedTick = Find.TickManager?.TicksGame ?? 0;
            if (!canReinitiate)
            {
                allowReinitiate = false;
                reinitiateAvailableTick = 0;
                return;
            }

            if (reinitiateCooldownTicks <= 0)
            {
                allowReinitiate = true;
                reinitiateAvailableTick = 0;
                return;
            }

            allowReinitiate = false;
            reinitiateAvailableTick = conversationEndedTick + reinitiateCooldownTicks;
        }

        public void ReinitiateConversation()
        {
            isConversationEndedByNpc = false;
            allowReinitiate = false;
            conversationEndReason = "";
            conversationEndedTick = 0;
            reinitiateAvailableTick = 0;
            pendingImageRequests = 0;
            strategyUsesConsumed = 0;
            pendingStrategySuggestions?.Clear();
            isWaitingForRansomTargetSelection = false;
            boundRansomTargetPawnLoadId = 0;
            boundRansomTargetFactionId = string.Empty;
            hasCompletedRansomInfoRequest = false;
            ransomAutoReplyCooldownUntilRealtime = -1f;
            ransomAutoReplyCooldownCategory = string.Empty;
            ClearPendingRansomBatchSelection();
            ClearPendingAirdropExecutionState();
            ClearPendingAirdropTradeCardReference();
        }

        public void SetPendingAirdropTradeCardReference(
            string need,
            string needDefName,
            string needLabel,
            string needSearchText,
            int requestedCount,
            string paymentItemDef,
            string paymentItemLabel,
            int paymentItemCount,
            string scenario,
            int shippingPodCount = 0,
            int shippingCostSilver = 0)
        {
            FactionDialogueSessionAirdropRefs.SetPendingAirdropTradeCardReference(
                this,
                need,
                needDefName,
                needLabel,
                needSearchText,
                requestedCount,
                paymentItemDef,
                paymentItemLabel,
                paymentItemCount,
                scenario,
                shippingPodCount,
                shippingCostSilver);
        }

        public void ClearPendingAirdropTradeCardReference()
        {
            FactionDialogueSessionAirdropRefs.ClearPendingAirdropTradeCardReference(this);
        }

        public void ClearPendingAirdropExecutionState()
        {
            FactionDialogueSessionAirdropRefs.ClearPendingAirdropExecutionState(this);
        }

        public bool HasPendingAirdropSelectionIntent()
        {
            return FactionDialogueSessionAirdropRefs.HasPendingAirdropSelectionIntent(this);
        }

        public bool ClearPendingAirdropSelectionIntentState()
        {
            return FactionDialogueSessionAirdropRefs.ClearPendingAirdropSelectionIntentState(this);
        }

        public bool TryBuildPendingAirdropTradeCardReference(out string referenceBlock)
        {
            return FactionDialogueSessionAirdropRefs.TryBuildPendingAirdropTradeCardReference(this, out referenceBlock);
        }

        public void SetPendingRansomBatchSelection(
            string batchGroupId,
            IEnumerable<int> targetPawnLoadIds,
            int totalCurrentAskSilver,
            int totalMinOfferSilver,
            int totalMaxOfferSilver)
        {
            FactionDialogueSessionRansomRefs.SetPendingRansomBatchSelection(
                this, batchGroupId, targetPawnLoadIds, totalCurrentAskSilver, totalMinOfferSilver, totalMaxOfferSilver);
        }

        public void ClearPendingRansomBatchSelection()
        {
            FactionDialogueSessionRansomRefs.ClearPendingRansomBatchSelection(this);
        }

        public bool TryGetPendingRansomBatchSelection(
            out string batchGroupId,
            out List<int> targetPawnLoadIds,
            out int totalCurrentAskSilver,
            out int totalMinOfferSilver,
            out int totalMaxOfferSilver)
        {
            return FactionDialogueSessionRansomRefs.TryGetPendingRansomBatchSelection(
                this, out batchGroupId, out targetPawnLoadIds, out totalCurrentAskSilver, out totalMinOfferSilver, out totalMaxOfferSilver);
        }

        public bool TryBuildPendingRansomBatchReference(out string referenceBlock)
        {
            return FactionDialogueSessionRansomRefs.TryBuildPendingRansomBatchReference(this, out referenceBlock);
        }

        public void SetPendingRansomOfferReference(
            int targetPawnLoadId,
            int currentAskSilver,
            int minOfferSilver,
            int maxOfferSilver)
        {
            FactionDialogueSessionRansomRefs.SetPendingRansomOfferReference(
                this, targetPawnLoadId, currentAskSilver, minOfferSilver, maxOfferSilver);
        }

        public void ClearPendingRansomOfferReference()
        {
            FactionDialogueSessionRansomRefs.ClearPendingRansomOfferReference(this);
        }

        public bool TryGetPendingRansomOfferReference(
            out int targetPawnLoadId,
            out int currentAskSilver,
            out int minOfferSilver,
            out int maxOfferSilver)
        {
            return FactionDialogueSessionRansomRefs.TryGetPendingRansomOfferReference(
                this, out targetPawnLoadId, out currentAskSilver, out minOfferSilver, out maxOfferSilver);
        }

        public bool TryBuildPendingRansomOfferReference(out string referenceBlock)
        {
            return FactionDialogueSessionRansomRefs.TryBuildPendingRansomOfferReference(this, out referenceBlock);
        }

        public bool TryGetRansomSessionState(
            string currentFactionId,
            out int currentRequestTargetPawnLoadId,
            out bool hasUnpaidRansomRequest)
        {
            currentRequestTargetPawnLoadId = 0;
            hasUnpaidRansomRequest = false;
            if (string.IsNullOrWhiteSpace(currentFactionId))
            {
                return false;
            }

            bool hasBoundTargetForFaction =
                hasCompletedRansomInfoRequest &&
                boundRansomTargetPawnLoadId > 0 &&
                string.Equals(boundRansomTargetFactionId ?? string.Empty, currentFactionId, StringComparison.Ordinal);
            if (hasBoundTargetForFaction)
            {
                currentRequestTargetPawnLoadId = boundRansomTargetPawnLoadId;
            }

            hasUnpaidRansomRequest =
                isWaitingForRansomTargetSelection ||
                hasPendingRansomBatchSelection ||
                hasBoundTargetForFaction;
            return true;
        }

        public bool ConsumePendingRansomBatchTarget(int targetPawnLoadId)
        {
            if (targetPawnLoadId <= 0 || !hasPendingRansomBatchSelection || pendingRansomBatchTargetPawnLoadIds == null)
            {
                return false;
            }

            bool removed = pendingRansomBatchTargetPawnLoadIds.Remove(targetPawnLoadId);
            if (!removed)
            {
                return false;
            }

            pendingRansomBatchTargetPawnLoadIds = pendingRansomBatchTargetPawnLoadIds
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            if (pendingRansomBatchTargetPawnLoadIds.Count <= 0)
            {
                ClearPendingRansomBatchSelection();
            }

            return true;
        }

        public void CacheAirdropCounteroffer(string defName, int count, int silver, string reason)
        {
            if (string.IsNullOrWhiteSpace(defName) || count <= 0 || silver < 0)
            {
                return;
            }

            lastAirdropCounterofferDefName = defName.Trim();
            lastAirdropCounterofferCount = Math.Max(1, count);
            lastAirdropCounterofferSilver = Math.Max(0, silver);
            lastAirdropCounterofferReason = reason ?? string.Empty;
            lastAirdropCounterofferTick = Find.TickManager?.TicksGame ?? 0;
        }

        public bool HasPendingImageRequests()
        {
            return pendingImageRequests > 0;
        }

        public void BeginImageRequest()
        {
            if (pendingImageRequests < int.MaxValue)
            {
                pendingImageRequests++;
            }
        }

        public void EndImageRequest()
        {
            pendingImageRequests = Math.Max(0, pendingImageRequests - 1);
        }

        public bool IsReinitiateAvailable(int currentTick)
        {
            if (!isConversationEndedByNpc)
            {
                return false;
            }

            if (allowReinitiate)
            {
                return true;
            }

            if (reinitiateAvailableTick > 0 && currentTick >= reinitiateAvailableTick)
            {
                allowReinitiate = true;
                reinitiateAvailableTick = 0;
                return true;
            }

            return false;
        }

        public int GetReinitiateRemainingTicks(int currentTick)
        {
            if (allowReinitiate || reinitiateAvailableTick <= 0)
            {
                return 0;
            }

            return Math.Max(0, reinitiateAvailableTick - currentTick);
        }

        public void MarkAsRead()
        {
            hasUnreadMessages = false;
        }

        public void ExposeData()
        {
            FactionDialogueSessionPersistence.ExposeData(this);
        }
    }

    /// <summary>/// message类型枚举
 ///</summary>
    public enum DialogueMessageType
    {
        Normal,       // 普通message (玩家/AI dialogue)
        System,       // Systemmessage (通知, error提示等)
        Image,        // Inline image card message
        AirdropTradeCard  // 物资空投交易卡片消息
    }

    /// <summary>/// 运行态策略建议 (来自 LLM)
 ///</summary>
    public class PendingStrategySuggestion
    {
        public string StrategyName = string.Empty;
        public string FactReason = string.Empty;
        public List<string> StrategyKeywords = new List<string>();
        public string Content = string.Empty;
    }

    /// <summary>/// 外交延迟动作运行态意图（不持久化）。
    ///</summary>
    public class PendingDelayedActionIntent
    {
        public string ActionType = string.Empty;
        public Dictionary<string, object> Parameters = new Dictionary<string, object>();
        public string Signature = string.Empty;
        public string RequiredParameter = string.Empty;
        public bool AwaitingConfirmation;
        public int CreatedAssistantRound;
        public int UpdatedAssistantRound;

        public PendingDelayedActionIntent Clone()
        {
            var clone = new PendingDelayedActionIntent
            {
                ActionType = ActionType ?? string.Empty,
                Signature = Signature ?? string.Empty,
                RequiredParameter = RequiredParameter ?? string.Empty,
                AwaitingConfirmation = AwaitingConfirmation,
                CreatedAssistantRound = CreatedAssistantRound,
                UpdatedAssistantRound = UpdatedAssistantRound,
                Parameters = new Dictionary<string, object>()
            };

            if (Parameters != null)
            {
                foreach (KeyValuePair<string, object> entry in Parameters)
                {
                    clone.Parameters[entry.Key] = entry.Value;
                }
            }

            return clone;
        }
    }

    /// <summary>/// 可序列化的dialoguemessage数据
 ///</summary>
    public class DialogueMessageData : IExposable
    {
        public string sender;
        public string message;
        public bool isPlayer;
        public DateTime timestamp;
        public DialogueMessageType messageType;
        public string imageLocalPath;
        public string imageSourceUrl;
        public string speakerPawnThingId;
        private Pawn speakerPawn;

        public bool allowFallbackRetry;

        private int gameTick;

        public string airdropNeedDefName;
        public string airdropNeedLabel;
        public int airdropRequestedCount;
        public float airdropNeedUnitPrice;
        public float airdropNeedReferenceTotalPrice;
        public int airdropShippingPodCount;
        public int airdropShippingCostSilver;
        public string airdropOfferDefName;
        public string airdropOfferLabel;
        public int airdropOfferCount;
        public float airdropOfferUnitPrice;
        public float airdropOfferTotalPrice;

        public DialogueMessageData()
        {
            messageType = DialogueMessageType.Normal;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref sender, "sender", "");
            Scribe_Values.Look(ref message, "message", "");
            Scribe_Values.Look(ref isPlayer, "isPlayer", false);
            Scribe_Values.Look(ref gameTick, "gameTick", 0);
            Scribe_Values.Look(ref messageType, "messageType", DialogueMessageType.Normal);
            Scribe_Values.Look(ref imageLocalPath, "imageLocalPath", string.Empty);
            Scribe_Values.Look(ref imageSourceUrl, "imageSourceUrl", string.Empty);
            Scribe_Values.Look(ref speakerPawnThingId, "speakerPawnThingId", string.Empty);
            Scribe_References.Look(ref speakerPawn, "speakerPawn");
            Scribe_Values.Look(ref allowFallbackRetry, "allowFallbackRetry", false);

            Scribe_Values.Look(ref airdropNeedDefName, "airdropNeedDefName", string.Empty);
            Scribe_Values.Look(ref airdropNeedLabel, "airdropNeedLabel", string.Empty);
            Scribe_Values.Look(ref airdropRequestedCount, "airdropRequestedCount", 0);
            Scribe_Values.Look(ref airdropNeedUnitPrice, "airdropNeedUnitPrice", 0f);
            Scribe_Values.Look(ref airdropNeedReferenceTotalPrice, "airdropNeedReferenceTotalPrice", 0f);
            Scribe_Values.Look(ref airdropShippingPodCount, "airdropShippingPodCount", 0);
            Scribe_Values.Look(ref airdropShippingCostSilver, "airdropShippingCostSilver", 0);
            Scribe_Values.Look(ref airdropOfferDefName, "airdropOfferDefName", string.Empty);
            Scribe_Values.Look(ref airdropOfferLabel, "airdropOfferLabel", string.Empty);
            Scribe_Values.Look(ref airdropOfferCount, "airdropOfferCount", 0);
            Scribe_Values.Look(ref airdropOfferUnitPrice, "airdropOfferUnitPrice", 0f);
            Scribe_Values.Look(ref airdropOfferTotalPrice, "airdropOfferTotalPrice", 0f);

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                timestamp = new DateTime(gameTick);
            }
        }

        public void SetTimestampFromCurrentGameTick()
        {
            gameTick = Find.TickManager.TicksGame;
            timestamp = new DateTime(gameTick);
        }

        public int GetGameTick()
        {
            return gameTick;
        }

        public bool IsSystemMessage()
        {
            return messageType == DialogueMessageType.System;
        }

        public bool HasInlineImage()
        {
            return messageType == DialogueMessageType.Image &&
                   !string.IsNullOrWhiteSpace(imageLocalPath);
        }

        public bool IsAirdropTradeCard()
        {
            return messageType == DialogueMessageType.AirdropTradeCard &&
                   !string.IsNullOrWhiteSpace(airdropNeedDefName);
        }

        public void SetAirdropTradeCardData(
            string needDefName,
            string needLabel,
            int requestedCount,
            float needUnitPrice,
            float needReferenceTotalPrice,
            int shippingPodCount,
            int shippingCostSilver,
            string offerDefName,
            string offerLabel,
            int offerCount,
            float offerUnitPrice,
            float offerTotalPrice)
        {
            messageType = DialogueMessageType.AirdropTradeCard;
            airdropNeedDefName = needDefName ?? string.Empty;
            airdropNeedLabel = needLabel ?? string.Empty;
            airdropRequestedCount = Math.Max(0, requestedCount);
            airdropNeedUnitPrice = Math.Max(0f, needUnitPrice);
            airdropNeedReferenceTotalPrice = Math.Max(0f, needReferenceTotalPrice);
            airdropShippingPodCount = Math.Max(0, shippingPodCount);
            airdropShippingCostSilver = Math.Max(0, shippingCostSilver);
            airdropOfferDefName = offerDefName ?? string.Empty;
            airdropOfferLabel = offerLabel ?? string.Empty;
            airdropOfferCount = Math.Max(0, offerCount);
            airdropOfferUnitPrice = Math.Max(0f, offerUnitPrice);
            airdropOfferTotalPrice = Math.Max(0f, offerTotalPrice);
        }

        public void SetSpeakerPawn(Pawn pawn)
        {
            speakerPawn = pawn;
            speakerPawnThingId = pawn?.ThingID ?? string.Empty;
        }

        public Pawn ResolveSpeakerPawn()
        {
            if (IsPawnReferenceValid(speakerPawn))
            {
                if (string.IsNullOrWhiteSpace(speakerPawnThingId))
                {
                    speakerPawnThingId = speakerPawn.ThingID;
                }
                return speakerPawn;
            }

            if (string.IsNullOrWhiteSpace(speakerPawnThingId))
            {
                speakerPawn = null;
                return null;
            }

            speakerPawn = ResolvePawnByThingId(speakerPawnThingId);
            return speakerPawn;
        }

        private static Pawn ResolvePawnByThingId(string thingId)
        {
            if (string.IsNullOrWhiteSpace(thingId))
            {
                return null;
            }

            Pawn worldPawn = Find.WorldPawns?.AllPawnsAliveOrDead?
                .FirstOrDefault(pawn => string.Equals(pawn?.ThingID, thingId, StringComparison.Ordinal));
            if (IsPawnReferenceValid(worldPawn))
            {
                return worldPawn;
            }

            foreach (Map map in Find.Maps ?? Enumerable.Empty<Map>())
            {
                Pawn mapPawn = map?.mapPawns?.AllPawnsSpawned?
                    .FirstOrDefault(pawn => string.Equals(pawn?.ThingID, thingId, StringComparison.Ordinal));
                if (IsPawnReferenceValid(mapPawn))
                {
                    return mapPawn;
                }
            }

            return null;
        }

        private static bool IsPawnReferenceValid(Pawn pawn)
        {
            return pawn != null && !pawn.Destroyed && !pawn.Dead;
        }
    }
}
