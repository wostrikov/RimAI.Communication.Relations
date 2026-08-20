using System;
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Diagnostics;
using Ustas.RimAI.Communication.Relations.Guards;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Ustas.RimAI.Communication.Relations.Relation;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.WorldState;
using UnityEngine;

namespace Ustas.RimAI.Communication.Relations.DiplomacySystem
{
    public class GameAIInterface : IExposable
    {

        internal const int CaravanFactionCooldownTicks = 7 * GenDate.TicksPerDay;
        internal const int AidFactionCooldownTicks = 15 * GenDate.TicksPerDay;
        internal const string TradersGuildDefName = "TradersGuild";
        internal const float MinimumGoodwillCooldownMultiplier = 0.7f;
        internal Dictionary<int, float> _airdropFactionTradeTotals = new Dictionary<int, float>();
        internal int _lastSuccessfulAirdropFactionId = -1;
        internal int _lastSuccessfulCaravanFactionId = -1;

        #region Singleton and initialization

        internal static readonly Lazy<GameAIInterface> _lazyInstance = new Lazy<GameAIInterface>(() => new GameAIInterface());
        public static GameAIInterface Instance => _lazyInstance.Value;

        internal GameAIInterfaceParts Parts;

        internal GameAIInterface()
        {
            Parts = new GameAIInterfaceParts(this);
            EnsureInitialized();
            Parts.CooldownOps.InitializeCooldowns();
        }

        public void ExposeData()
        {
            EnsureInitialized();
            
            Scribe_Values.Look(ref _lastResetTick, "lastResetTick", 0);
            Scribe_Collections.Look(ref _apiCallHistory, "apiCallHistory", LookMode.Deep);
            
            // Serialization / save-load constraint — keep field identity stable. (goodwill record)
            ExposeGoodwillAdjustments();
            
            // Serialization / save-load constraint — keep field identity stable. (faction)
            ExposeFactionCooldowns();
            ExposeRaidCooldowns();
            ExposeAirdropTradeTotals();
            Scribe_Values.Look(ref _lastSuccessfulAirdropFactionId, "lastSuccessfulAirdropFactionId", -1);
            Scribe_Values.Look(ref _lastSuccessfulCaravanFactionId, "lastSuccessfulCaravanFactionId", -1);

            // Serialization / save-load constraint — keep field identity stable.
            ExposeFactionSpecialItems();
        }

        // Serialization / save-load constraint — keep field identity stable. (summary summary)
        internal void ExposeFactionSpecialItems()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                Scribe_Deep.Look(ref FactionSpecialItemsManager._instance, "factionSpecialItemsManager");
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                Scribe_Deep.Look(ref FactionSpecialItemsManager._instance, "factionSpecialItemsManager");
                if (FactionSpecialItemsManager._instance == null)
                {
                    FactionSpecialItemsManager._instance = new FactionSpecialItemsManager();
                }
            }
        }

        // Serialization / save-load constraint — keep field identity stable. (summary summary)
        internal void ExposeRaidCooldowns()
        {
            Scribe_Values.Look(ref _raidCallEveryoneNextAvailableTick, "raidCallEveryoneNextAvailableTick", 0);
            Scribe_Collections.Look(ref _raidWavesState, "raidWavesState", LookMode.Deep);
        }

        // Serialization / save-load constraint — keep field identity stable. (summary goodwill record summary)
        internal void ExposeGoodwillAdjustments()
        {
            List<Faction> goodwillKeys = null;
            List<int> goodwillValues = null;
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                goodwillKeys = _goodwillAdjustmentsToday.Keys.ToList();
                goodwillValues = _goodwillAdjustmentsToday.Values.ToList();
            }
            Scribe_Collections.Look(ref goodwillKeys, "goodwillAdjustmentsTodayKeys", LookMode.Reference);
            Scribe_Collections.Look(ref goodwillValues, "goodwillAdjustmentsTodayValues", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                _goodwillAdjustmentsToday = new Dictionary<Faction, int>();
                if (goodwillKeys != null && goodwillValues != null)
                {
                    for (int i = 0; i < goodwillKeys.Count; i++)
                    {
                        _goodwillAdjustmentsToday[goodwillKeys[i]] = goodwillValues[i];
                    }
                }
            }
        }

        


        


        


        


        


        


        


        internal void ExposeAirdropTradeTotals()
        {
            List<int> factionIds = null;
            List<float> tradeTotals = null;
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                factionIds = _airdropFactionTradeTotals.Keys.ToList();
                tradeTotals = _airdropFactionTradeTotals.Values.ToList();
            }

            Scribe_Collections.Look(ref factionIds, "airdropTradeTotalFactionIds", LookMode.Value);
            Scribe_Collections.Look(ref tradeTotals, "airdropTradeTotalValues", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                _airdropFactionTradeTotals.Clear();
                if (factionIds != null && tradeTotals != null)
                {
                    for (int i = 0; i < Math.Min(factionIds.Count, tradeTotals.Count); i++)
                    {
                        if (factionIds[i] >= 0)
                        {
                            _airdropFactionTradeTotals[factionIds[i]] = Math.Max(0f, tradeTotals[i]);
                        }
                    }
                }
            }
        }

        // Serialization / save-load constraint — keep field identity stable. (summary faction Dictionary Faction Dictionary string int summary)
        internal void ExposeFactionCooldowns()
        {
            // Serialization / save-load constraint — keep field identity stable.
            List<FactionCooldownEntry> cooldownEntries = null;

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                cooldownEntries = new List<FactionCooldownEntry>();
                foreach (var factionKvp in _factionCooldowns)
                {
                    if (factionKvp.Key == null) continue;

                    var entry = new FactionCooldownEntry
                    {
                        Faction = factionKvp.Key,
                        MethodCooldowns = factionKvp.Value?.ToList() ?? new List<KeyValuePair<string, int>>()
                    };
                    cooldownEntries.Add(entry);
                }
            }

            Scribe_Collections.Look(ref cooldownEntries, "factionCooldownEntries", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                _factionCooldowns = new Dictionary<Faction, Dictionary<string, int>>();
                if (cooldownEntries != null)
                {
                    foreach (var entry in cooldownEntries)
                    {
                        if (entry.Faction == null) continue;

                        var cooldownDict = new Dictionary<string, int>();
                        foreach (var methodKvp in entry.MethodCooldowns)
                        {
                            cooldownDict[methodKvp.Key] = methodKvp.Value;
                        }
                        _factionCooldowns[entry.Faction] = cooldownDict;
                    }
                }
            }
        }

        #endregion

        #region 数据结构

        internal List<APICallRecord> _apiCallHistory;

        internal Dictionary<Faction, int> _goodwillAdjustmentsToday;

        internal Dictionary<Faction, Dictionary<string, int>> _factionCooldowns;

        internal Dictionary<DialogueGoodwillCost.DialogueActionType, Dictionary<Faction, int>> _dialogueActionCooldowns;

        internal List<DialogueActionRecord> _dialogueActionRecords;

        internal int _lastResetTick = 0;
        internal const int MakePeaceTargetGoodwill = 0;
        internal const int DeclareWarTargetGoodwill = -80;

        internal int _raidCallEveryoneNextAvailableTick = 0;

        internal List<RaidWaveState> _raidWavesState;

        internal void EnsureInitialized()
        {
            if (_apiCallHistory == null)
                _apiCallHistory = new List<APICallRecord>();
            if (_goodwillAdjustmentsToday == null)
                _goodwillAdjustmentsToday = new Dictionary<Faction, int>();
            if (_factionCooldowns == null)
                _factionCooldowns = new Dictionary<Faction, Dictionary<string, int>>();
            if (_dialogueActionCooldowns == null)
                _dialogueActionCooldowns = new Dictionary<DialogueGoodwillCost.DialogueActionType, Dictionary<Faction, int>>();
            if (_dialogueActionRecords == null)
                _dialogueActionRecords = new List<DialogueActionRecord>();
            if (_raidWavesState == null)
                _raidWavesState = new List<RaidWaveState>();
        }

        public class APICallRecord : IExposable
        {
            public string MethodName;
            public int TickCalled;
            public string Parameters;
            public bool Success;
            public string ErrorMessage;

            public void ExposeData()
            {
                Scribe_Values.Look(ref MethodName, "methodName", "");
                Scribe_Values.Look(ref TickCalled, "tickCalled", 0);
                Scribe_Values.Look(ref Parameters, "parameters", "");
                Scribe_Values.Look(ref Success, "success", false);
                Scribe_Values.Look(ref ErrorMessage, "errorMessage", "");
            }
        }

        public class APIResult
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            public object Data { get; set; }

            public static APIResult SuccessResult(string message = "", object data = null)
            {
                return new APIResult { Success = true, Message = message, Data = data };
            }

            public static APIResult FailureResult(string message)
            {
                return new APIResult { Success = false, Message = message };
            }
        }

        public class DialogueApiGoodwillCostResult
        {
            public string SourceAction { get; set; }
            public string Detail { get; set; }
            public DialogueGoodwillCost.DialogueActionType ActionType { get; set; }
            public int BaseCost { get; set; }
            public int ActualChange { get; set; }
            public int OldGoodwill { get; set; }
            public int NewGoodwill { get; set; }
        }

        // Serialization / save-load constraint — keep field identity stable. (summary faction entry used for summary)
        public class FactionCooldownEntry : IExposable
        {
            public Faction Faction;
            public List<KeyValuePair<string, int>> MethodCooldowns;

            public void ExposeData()
            {
                string factionId = Faction?.GetUniqueLoadID() ?? string.Empty;
                Scribe_Values.Look(ref factionId, "factionId", string.Empty);
                if (Scribe.mode == LoadSaveMode.PostLoadInit)
                {
                    if (!string.IsNullOrEmpty(factionId))
                    {
                        Faction = Find.FactionManager.AllFactions.FirstOrDefault(f => f.GetUniqueLoadID() == factionId);
                    }
                }
                
                // Serialization / save-load constraint — keep field identity stable. (method)
                List<string> methodNames = null;
                List<int> cooldownTicks = null;
                
                if (Scribe.mode == LoadSaveMode.Saving)
                {
                    methodNames = MethodCooldowns?.Select(x => x.Key).ToList() ?? new List<string>();
                    cooldownTicks = MethodCooldowns?.Select(x => x.Value).ToList() ?? new List<int>();
                }
                
                Scribe_Collections.Look(ref methodNames, "methodNames", LookMode.Value);
                Scribe_Collections.Look(ref cooldownTicks, "cooldownTicks", LookMode.Value);
                
                if (Scribe.mode == LoadSaveMode.LoadingVars)
                {
                    MethodCooldowns = new List<KeyValuePair<string, int>>();
                    if (methodNames != null && cooldownTicks != null)
                    {
                        for (int i = 0; i < methodNames.Count && i < cooldownTicks.Count; i++)
                        {
                            MethodCooldowns.Add(new KeyValuePair<string, int>(methodNames[i], cooldownTicks[i]));
                        }
                    }
                }
            }
        }

        #endregion

        #region 袭击波次状态

        // Serialization / save-load constraint — keep field identity stable. (summary summary)
        public class RaidWaveState : IExposable
        {
            public string SourceFactionDefName;
            public int WavesRemaining;
            public int NextWaveTick;
            public int MinIntervalTicks = 12 * 2500; 
            public int MaxIntervalTicks = 20 * 2500; 

            public void ExposeData()
            {
                Scribe_Values.Look(ref SourceFactionDefName, "sourceFactionDefName", "");
                Scribe_Values.Look(ref WavesRemaining, "wavesRemaining", 0);
                Scribe_Values.Look(ref NextWaveTick, "nextWaveTick", 0);
                Scribe_Values.Look(ref MinIntervalTicks, "minIntervalTicks", 12 * 2500);
                Scribe_Values.Look(ref MaxIntervalTicks, "maxIntervalTicks", 20 * 2500);
            }
        }

        #endregion
    
        public int QuestTrackingRevision => Parts.QuestTracking.QuestTrackingRevision;

        #region Facade forwards

        public APIResult RequestItemAirdrop(Faction faction, Dictionary<string, object> parameters) => Parts.AirdropRequest.RequestItemAirdrop(faction, parameters);

        public APIResult PrepareItemAirdropTrade(Faction faction, Dictionary<string, object> parameters, Pawn playerNegotiator) => Parts.AirdropBarter.PrepareItemAirdropTrade(faction, parameters, playerNegotiator);

        public APIResult CommitPreparedItemAirdropTrade(Faction faction, ItemAirdropPreparedTradeData preparedData) => Parts.AirdropBarter.CommitPreparedItemAirdropTrade(faction, preparedData);

        internal APIResult PrepareItemAirdropTradeForMap(Faction faction, Dictionary<string, object> parameters, Map map, bool requirePlayerHome, Pawn playerNegotiator) => Parts.AirdropBarter.PrepareItemAirdropTradeForMap(faction, parameters, map, requirePlayerHome, playerNegotiator);

        internal APIResult BuildPaymentPlanFromRequestedLines(List<ItemAirdropPaymentRequestLine> requestedLines, Map map, Faction faction, Pawn playerNegotiator, out List<ItemAirdropPreparedPaymentLine> paymentLines, out List<ItemAirdropDeductionPlanLine> deductionPlan, out int derivedBudgetSilver, out int paymentTotalSilver) => Parts.AirdropPayment.BuildPaymentPlanFromRequestedLines(requestedLines, map, faction, playerNegotiator, out paymentLines, out deductionPlan, out derivedBudgetSilver, out paymentTotalSilver);

        internal static List<Thing> CollectBeaconTradeableThingsShared(Map map) => GameAIAirdropPayment.CollectBeaconTradeableThingsShared(map);

        internal static bool IsValidBeaconPaymentThingShared(Thing thing) => GameAIAirdropPayment.IsValidBeaconPaymentThingShared(thing);

        public APIResult BeginPrepareItemAirdropTradeAsync(Faction faction, Dictionary<string, object> parameters, Pawn playerNegotiator, Action<APIResult> onCompleted, Action<string, int> onRequestQueued) => Parts.AirdropAsync.BeginPrepareItemAirdropTradeAsync(faction, parameters, playerNegotiator, onCompleted, onRequestQueued);

        public bool CancelItemAirdropAsyncRequest(string requestId, string cancelReason, string error) => Parts.AirdropAsync.CancelItemAirdropAsyncRequest(requestId, cancelReason, error);

        public void ResetPrisonerRansomRuntimeState() => Parts.PrisonerRansom.ResetPrisonerRansomRuntimeState();

        public void CapturePrisonerInfoCardCoreOrganSnapshot(Faction faction, Pawn targetPawn) => Parts.PrisonerRansom.CapturePrisonerInfoCardCoreOrganSnapshot(faction, targetPawn);

        public APIResult PayPrisonerRansom(Faction faction, Dictionary<string, object> parameters) => Parts.PrisonerRansom.PayPrisonerRansom(faction, parameters);

        public APIResult PreparePrisonerRansom(Faction faction, Dictionary<string, object> parameters) => Parts.PrisonerRansom.PreparePrisonerRansom(faction, parameters);

        internal APIResult CommitPrisonerRansomAndRelease(Faction faction, PrisonerRansomPrepareData preparedData) => Parts.PrisonerRansom.CommitPrisonerRansomAndRelease(faction, preparedData);

        public APIResult CalculatePrisonerRansomQuote(Faction faction, Pawn targetPawn, bool forceRefresh = false) => Parts.PrisonerRansom.CalculatePrisonerRansomQuote(faction, targetPawn, forceRefresh);

        public APIResult ApplyRansomPenaltyAndRaid(Faction faction, int goodwillPenalty, bool triggerRaid, string reasonTag, Pawn targetPawn = null) => Parts.PrisonerRansom.ApplyRansomPenaltyAndRaid(faction, goodwillPenalty, triggerRaid, reasonTag, targetPawn);

        internal void ExposeQuestPublicationData() => Parts.QuestTracking.ExposeQuestPublicationData();

        internal static HashSet<int> CaptureCurrentQuestIdsForTracking() => GameAIQuestTracking.CaptureCurrentQuestIdsForTracking();

        internal void TryTrackCreateQuestResult(string requestedQuestDefName, Dictionary<string, object> parameters, APIResult result, HashSet<int> questIdsBefore) => Parts.QuestTracking.TryTrackCreateQuestResult(requestedQuestDefName, parameters, result, questIdsBefore);

        internal void RefreshQuestTrackingState() => Parts.QuestTracking.RefreshQuestTrackingState();

        internal RelationsFactionQuestCompletionRecord GetLatestCompletedQuestForFaction(Faction faction) => Parts.QuestTracking.GetLatestCompletedQuestForFaction(faction);

        internal bool HasActiveRimChatQuestForFaction(Faction faction) => Parts.QuestTracking.HasActiveRimChatQuestForFaction(faction);

        public APIResult AdjustGoodwill(Faction faction, int amount, string reason = "") => Parts.GoodwillOps.AdjustGoodwill(faction, amount, reason);

        public APIResult GetCurrentGoodwill(Faction faction) => Parts.GoodwillOps.GetCurrentGoodwill(faction);

        public int GetTodayGoodwillAdjustment(Faction faction) => Parts.GoodwillOps.GetTodayGoodwillAdjustment(faction);

        public APIResult SendGift(Faction faction, int silverAmount, int goodwillGain) => Parts.GoodwillOps.SendGift(faction, silverAmount, goodwillGain);

        public APIResult PrepareSendGiftPayment(Faction faction, int silverAmount, int goodwillGain, Pawn playerNegotiator) => Parts.GoodwillOps.PrepareSendGiftPayment(faction, silverAmount, goodwillGain, playerNegotiator);

        public APIResult CommitPreparedSendGift(Faction faction, PreparedSendGiftData preparedData) => Parts.GoodwillOps.CommitPreparedSendGift(faction, preparedData);

        public APIResult DeclareWar(Faction faction, string reason = "") => Parts.ConflictOps.DeclareWar(faction, reason);

        public APIResult MakePeace(Faction faction, int peaceCost = 0) => Parts.ConflictOps.MakePeace(faction, peaceCost);

        public APIResult PrepareMakePeacePayment(Faction faction, int peaceCost, Pawn playerNegotiator) => Parts.ConflictOps.PrepareMakePeacePayment(faction, peaceCost, playerNegotiator);

        public APIResult CommitPreparedMakePeace(Faction faction, PreparedMakePeacePaymentData preparedData) => Parts.ConflictOps.CommitPreparedMakePeace(faction, preparedData);

        public APIResult RequestAid(Faction faction, string aidType, bool delayed = true) => Parts.IncidentOps.RequestAid(faction, aidType, delayed);

        public APIResult RequestRaid(Faction faction, string strategyDefName = "", string arrivalModeDefName = "", bool delayed = true) => Parts.IncidentOps.RequestRaid(faction, strategyDefName, arrivalModeDefName, delayed);

        public APIResult RequestTradeCaravan(Faction faction, string caravanType = "General", bool delayed = true) => Parts.IncidentOps.RequestTradeCaravan(faction, caravanType, delayed);

        public APIResult RequestVisitor(Faction faction, bool delayed = true) => Parts.IncidentOps.RequestVisitor(faction, delayed);

        public APIResult ApplySuccessfulDialogueApiGoodwillCost(Faction faction, DialogueGoodwillCost.DialogueActionType actionType, string sourceAction = "", string detail = "") => Parts.IncidentOps.ApplySuccessfulDialogueApiGoodwillCost(faction, actionType, sourceAction, detail);

        public APIResult GetFactionInfo(Faction faction) => Parts.IncidentOps.GetFactionInfo(faction);

        public APIResult GetAllFactions() => Parts.IncidentOps.GetAllFactions();

        public APIResult GetColonyStatus() => Parts.IncidentOps.GetColonyStatus();

        public APIResult TriggerIncident(Faction faction, string incidentDefName, float points = -1) => Parts.IncidentOps.TriggerIncident(faction, incidentDefName, points);

        public APIResult CreateSimpleQuest(Faction faction, string title, string description, string rewardDescription, string callbackId, int durationTicks = 60000) => Parts.QuestCreateOps.CreateSimpleQuest(faction, title, description, rewardDescription, callbackId, durationTicks);

        public APIResult CreateQuest(string questDefName, Dictionary<string, object> parameters) => Parts.QuestCreateOps.CreateQuest(questDefName, parameters);

        internal float GetAirdropFactionTradeTotalForPolicy(Faction faction) => Parts.CooldownOps.GetAirdropFactionTradeTotalForPolicy(faction);

        public void DailyReset() => Parts.CooldownOps.DailyReset();

        public int GetItemAirdropCooldownTicks(Faction faction) => Parts.CooldownOps.GetItemAirdropCooldownTicks(faction);

        public int GetRemainingCooldownSeconds(Faction faction, string methodName) => Parts.CooldownOps.GetRemainingCooldownSeconds(faction, methodName);

        public Dictionary<string, int> GetFactionCooldownOverview(Faction faction) => Parts.CooldownOps.GetFactionCooldownOverview(faction);

        public int GetRaidCallEveryoneRemainingCooldownSeconds() => Parts.CooldownOps.GetRaidCallEveryoneRemainingCooldownSeconds();

        public void SetRaidCallEveryoneCooldown() => Parts.CooldownOps.SetRaidCallEveryoneCooldown();

        public bool IsRaidCallEveryoneAvailable() => Parts.CooldownOps.IsRaidCallEveryoneAvailable();

        public void SetFactionCooldown(Faction faction, string methodName) => Parts.CooldownOps.SetFactionCooldown(faction, methodName);

        public List<APICallRecord> GetAPICallHistory(string methodName = null, int maxRecords = 50) => Parts.CooldownOps.GetAPICallHistory(methodName, maxRecords);

        public bool ValidateAIPermission(Faction faction) => Parts.DialogueActionOps.ValidateAIPermission(faction);

        public APIResult ExecuteDialogueAction(Faction faction, DialogueGoodwillCost.DialogueActionType actionType) => Parts.DialogueActionOps.ExecuteDialogueAction(faction, actionType);

        public APIResult PreviewDialogueActionCost(Faction faction, DialogueGoodwillCost.DialogueActionType actionType) => Parts.DialogueActionOps.PreviewDialogueActionCost(faction, actionType);

        public APIResult GetTodayDialogueStats(Faction faction) => Parts.DialogueActionOps.GetTodayDialogueStats(faction);

        #endregion
    }

    internal sealed class GameAIInterfaceParts
    {
        internal readonly GameAIInterface Owner;
        internal readonly GameAIAirdropRequest AirdropRequest;
        internal readonly GameAIAirdropSelection AirdropSelection;
        internal readonly GameAIAirdropDrop AirdropDrop;
        internal readonly GameAIAirdropBarter AirdropBarter;
        internal readonly GameAIAirdropPayment AirdropPayment;
        internal readonly GameAIAirdropAsync AirdropAsync;
        internal readonly GameAIAirdropBoundNeed AirdropBoundNeed;
        internal readonly GameAIAirdropPending AirdropPending;
        internal readonly GameAIPrisonerRansom PrisonerRansom;
        internal readonly GameAIQuestTracking QuestTracking;
        internal readonly GameAIGoodwillOps GoodwillOps;
        internal readonly GameAIConflictOps ConflictOps;
        internal readonly GameAIIncidentOps IncidentOps;
        internal readonly GameAIQuestCreateOps QuestCreateOps;
        internal readonly GameAICooldownOps CooldownOps;
        internal readonly GameAIDialogueActionOps DialogueActionOps;

        internal GameAIInterfaceParts(GameAIInterface owner)
        {
            Owner = owner;
            AirdropRequest = new GameAIAirdropRequest(owner);
            AirdropSelection = new GameAIAirdropSelection(owner);
            AirdropDrop = new GameAIAirdropDrop(owner);
            AirdropBarter = new GameAIAirdropBarter(owner);
            AirdropPayment = new GameAIAirdropPayment(owner);
            AirdropAsync = new GameAIAirdropAsync(owner);
            AirdropBoundNeed = new GameAIAirdropBoundNeed(owner);
            AirdropPending = new GameAIAirdropPending(owner);
            PrisonerRansom = new GameAIPrisonerRansom(owner);
            QuestTracking = new GameAIQuestTracking(owner);
            GoodwillOps = new GameAIGoodwillOps(owner);
            ConflictOps = new GameAIConflictOps(owner);
            IncidentOps = new GameAIIncidentOps(owner);
            QuestCreateOps = new GameAIQuestCreateOps(owner);
            CooldownOps = new GameAICooldownOps(owner);
            DialogueActionOps = new GameAIDialogueActionOps(owner);
        }
    }
}
