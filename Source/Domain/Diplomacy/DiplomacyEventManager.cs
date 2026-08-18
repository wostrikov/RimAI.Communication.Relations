using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Diagnostics;
using Ustas.RimAI.Communication.Relations.Guards;
using UnityEngine;

using static Ustas.RimAI.Communication.Relations.DiplomacySystem.DiplomacyEventManager;

namespace Ustas.RimAI.Communication.Relations.DiplomacySystem
{
    public enum CaravanType
    {
        General,
        BulkGoods,
        CombatSupplier,
        Exotic,
        Slaver
    }

    public enum AidType
    {
        Military,
        Medical,
        Resources
    }

    public static class DiplomacyEventManager
    {
        internal static readonly string[] MilitaryAidIncidentCandidates =
        {
            "FriendlyRaid",
            "RaidFriendly"
        };

        

        

        

        

        

        internal static bool DefNameContains(string source, string value)
        {
            return source?.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        

        /// <summary>/// 触发军事支援事件（公共接口，用于 CallEveryone 友好派系支援）
        ///</summary>
        

        /// <summary>
        /// CallEveryone 专用军事支援：不依赖 RaidFriendly/FriendlyRaid，可用于中立派系援军。
        /// </summary>
        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        /// <summary>/// 调度"呼叫所有人"袭击：敌友统一 16-30 小时窗口；当敌对数量不足时优先剔除最低好感友中立
        ///</summary>
        

        

        

        

        

        

        

        

        

        

        

        

        /// <summary>/// 调度袭击波次：n 次袭击，每次间隔 12-20 小时
        ///</summary>
        

        #region Facade forwards
        internal static bool EnsureRaidTemplates(Faction faction, out string reason) => DiplomacyEventManagerRaidFallback.EnsureRaidTemplates(faction, out reason);
        internal static bool TryInjectDefaultCombatPawnGroupMaker(Faction faction, out string reason) => DiplomacyEventManagerRaidFallback.TryInjectDefaultCombatPawnGroupMaker(faction, out reason);
        internal static List<PawnGenOption> BuildDefaultCombatOptions(Faction faction, List<PawnGroupMaker> makers) => DiplomacyEventManagerRaidFallback.BuildDefaultCombatOptions(faction, makers);
        internal static PawnGenOption ClonePawnGenOption(PawnGenOption source) => DiplomacyEventManagerRaidFallback.ClonePawnGenOption(source);
        internal static PawnKindDef ResolveFallbackRaidPawnKind(Faction faction, List<PawnGroupMaker> makers) => DiplomacyEventManagerRaidFallback.ResolveFallbackRaidPawnKind(faction, makers);
        internal static IncidentParms BuildRaidIncidentParmsWithDefaults(IncidentDef incidentDef, Map map, Faction faction, float raidPoints, RaidStrategyDef strategy, PawnsArrivalModeDef arrivalMode) => DiplomacyEventManagerRaidFallback.BuildRaidIncidentParmsWithDefaults(incidentDef, map, faction, raidPoints, strategy, arrivalMode);
        internal static bool EnsureUsableCombatPawnGroupMakerForParms(Faction faction, IncidentParms raidParms, out string reason) => DiplomacyEventManagerRaidFallback.EnsureUsableCombatPawnGroupMakerForParms(faction, raidParms, out reason);
        internal static bool HasUsableCombatPawnGroupMakerForParms(Faction faction, IncidentParms raidParms, out string reason) => DiplomacyEventManagerRaidFallback.HasUsableCombatPawnGroupMakerForParms(faction, raidParms, out reason);
        internal static PawnGroupMakerParms BuildRaidGroupMakerParms(IncidentParms raidParms, out string reason) => DiplomacyEventManagerRaidFallback.BuildRaidGroupMakerParms(raidParms, out reason);
        internal static bool SafeCanGenerateFrom(PawnGroupMaker maker, PawnGroupMakerParms parms) => DiplomacyEventManagerRaidFallback.SafeCanGenerateFrom(maker, parms);
        internal static bool SafeHasPreviewKinds(PawnGroupMaker maker, PawnGroupMakerParms parms) => DiplomacyEventManagerRaidFallback.SafeHasPreviewKinds(maker, parms);
        internal static bool SafeHasAnyPreviewKinds(PawnGroupMakerParms parms) => DiplomacyEventManagerRaidFallback.SafeHasAnyPreviewKinds(parms);
        internal static bool TryRaiseRaidPointsToMeetCombatMinimum(Faction faction, IncidentParms raidParms, out string reason) => DiplomacyEventManagerRaidFallback.TryRaiseRaidPointsToMeetCombatMinimum(faction, raidParms, out reason);
        internal static float SafeMinPointsToGenerateAnything(PawnGroupMaker maker, FactionDef factionDef, PawnGroupMakerParms parms) => DiplomacyEventManagerRaidFallback.SafeMinPointsToGenerateAnything(maker, factionDef, parms);
        internal static float SafeMinPointsToGeneratePawnGroup(FactionDef factionDef, PawnGroupMakerParms parms) => DiplomacyEventManagerRaidFallback.SafeMinPointsToGeneratePawnGroup(factionDef, parms);
        internal static bool TryInjectEmergencyCombatPawnGroupMakerForParms(Faction faction, IncidentParms raidParms, out string reason) => DiplomacyEventManagerRaidFallback.TryInjectEmergencyCombatPawnGroupMakerForParms(faction, raidParms, out reason);
        internal static List<PawnGenOption> BuildEmergencyCombatOptions(Faction faction, PawnGroupMakerParms groupParms) => DiplomacyEventManagerRaidFallback.BuildEmergencyCombatOptions(faction, groupParms);
        internal static List<PawnKindDef> BuildEmergencyCombatKinds(Faction faction) => DiplomacyEventManagerRaidFallback.BuildEmergencyCombatKinds(faction);
        internal static bool CanKindGenerateForParms(PawnKindDef kind, PawnGroupMakerParms groupParms) => DiplomacyEventManagerRaidFallback.CanKindGenerateForParms(kind, groupParms);
        internal static bool IsEmergencyRaidKindCandidate(PawnKindDef kind) => DiplomacyEventManagerRaidFallback.IsEmergencyRaidKindCandidate(kind);
        internal static bool TryExecuteMiliraRaidFallback(Map map, Faction faction, float raidPoints, out string reason) => DiplomacyEventManagerRaidFallback.TryExecuteMiliraRaidFallback(map, faction, raidPoints, out reason);
        internal static List<float> BuildMiliraFallbackPointCandidates(float requestedPoints, float minRequiredPoints) => DiplomacyEventManagerRaidFallback.BuildMiliraFallbackPointCandidates(requestedPoints, minRequiredPoints);
        internal static IncidentDef GetMiliraRaidIncidentDef(out string reason) => DiplomacyEventManagerRaidFallback.GetMiliraRaidIncidentDef(out reason);
        internal static bool IsMiliraFaction(Faction faction) => DiplomacyEventManagerRaidFallback.IsMiliraFaction(faction);
        internal static bool ContainsIgnoreCase(string source, string token) => DiplomacyEventManagerRaidFallback.ContainsIgnoreCase(source, token);
        #endregion
    
        #region Cluster forwards
        public static bool TriggerCaravanEvent(Faction faction, CaravanType caravanType) => DiplomacyEventSlice1.TriggerCaravanEvent(faction, caravanType);
        public static bool TriggerVisitorEvent(Faction faction) => DiplomacyEventSlice1.TriggerVisitorEvent(faction);
        internal static TraderKindDef GetTraderKindForType(Faction faction, CaravanType caravanType) => DiplomacyEventSlice1.GetTraderKindForType(faction, caravanType);
        internal static List<TraderKindDef> GetFactionGroundTraderKinds(Faction faction) => DiplomacyEventSlice1.GetFactionGroundTraderKinds(faction);
        internal static bool MatchesCaravanType(TraderKindDef trader, CaravanType caravanType) => DiplomacyEventSlice1.MatchesCaravanType(trader, caravanType);
        public static bool TriggerAidEvent(Faction faction, AidType aidType) => DiplomacyEventSlice1.TriggerAidEvent(faction, aidType);
        public static bool TriggerMilitaryAidEvent(Faction faction) => DiplomacyEventSlice1.TriggerMilitaryAidEvent(faction);
        public static bool TriggerMilitaryAidCallEveryoneEvent(Faction faction) => DiplomacyEventSlice1.TriggerMilitaryAidCallEveryoneEvent(faction);
        internal static bool TriggerMilitaryAid(Faction faction, Map map) => DiplomacyEventSlice1.TriggerMilitaryAid(faction, map);
        internal static bool TriggerMilitaryAidCustomFallback(Faction faction) => DiplomacyEventSlice1.TriggerMilitaryAidCustomFallback(faction);
        internal static bool TryResolveExecutableMilitaryAidIncident(IncidentParms parms, out IncidentDef incidentDef, out string reason) => DiplomacyEventSlice1.TryResolveExecutableMilitaryAidIncident(parms, out incidentDef, out reason);
        internal static bool TriggerMedicalAid(Faction faction, Map map) => DiplomacyEventSlice1.TriggerMedicalAid(faction, map);
        internal static bool TriggerResourceAid(Faction faction, Map map) => DiplomacyEventSlice1.TriggerResourceAid(faction, map);
        internal static List<Thing> GenerateMedicalSupplies() => DiplomacyEventSlice1.GenerateMedicalSupplies();
        internal static List<Thing> GenerateResourceSupplies() => DiplomacyEventSlice1.GenerateResourceSupplies();
        internal static void SendAidLetter(Faction faction, string title, string message) => DiplomacyEventSlice2.SendAidLetter(faction, title, message);
        public static string GetCaravanTypeLabel(CaravanType type) => DiplomacyEventSlice2.GetCaravanTypeLabel(type);
        public static string GetAidTypeLabel(AidType type) => DiplomacyEventSlice2.GetAidTypeLabel(type);
        public static CaravanType ParseCaravanType(string typeStr) => DiplomacyEventSlice2.ParseCaravanType(typeStr);
        public static AidType ParseAidType(string typeStr) => DiplomacyEventSlice2.ParseAidType(typeStr);
        internal static bool TryFindNearestFactionSettlement(Faction faction, int fromTile, out int distanceTiles) => DiplomacyEventSlice2.TryFindNearestFactionSettlement(faction, fromTile, out distanceTiles);
        public static int CalculateDelayTicks(Faction faction, bool isAid = false) => DiplomacyEventSlice2.CalculateDelayTicks(faction, isAid);
        public static bool ScheduleDelayedCaravan(Faction faction, CaravanType caravanType) => DiplomacyEventSlice2.ScheduleDelayedCaravan(faction, caravanType);
        public static bool ScheduleDelayedAid(Faction faction, AidType aidType) => DiplomacyEventSlice2.ScheduleDelayedAid(faction, aidType);
        public static bool ScheduleDelayedVisitor(Faction faction) => DiplomacyEventSlice2.ScheduleDelayedVisitor(faction);
        public static bool TriggerRaidEvent(Faction faction, float points, RaidStrategyDef strategy, PawnsArrivalModeDef arrivalMode) => DiplomacyEventSlice2.TriggerRaidEvent(faction, points, strategy, arrivalMode);
        public static bool TryValidateRaidFaction(Faction faction, out string reason) => DiplomacyEventSlice2.TryValidateRaidFaction(faction, out reason);
        internal static bool HasUsableCombatPawnGroupMaker(Faction faction, out string reason) => DiplomacyEventSlice2.HasUsableCombatPawnGroupMaker(faction, out reason);
        internal static string DescribeRaidGroupMakerState(Faction faction) => DiplomacyEventSlice2.DescribeRaidGroupMakerState(faction);
        internal static bool IsStrategyExecutable(RaidStrategyDef strategy, Faction faction, Map map) => DiplomacyEventSlice2.IsStrategyExecutable(strategy, faction, map);
        internal static bool TryExecuteRaidWithVanillaAutoFallback(IncidentDef incidentDef, Map map, Faction faction, float raidPoints, out string reason) => DiplomacyEventSlice3.TryExecuteRaidWithVanillaAutoFallback(incidentDef, map, faction, raidPoints, out reason);
        internal static float ResolveRaidPoints(Map map, Faction faction, float requestedPoints) => DiplomacyEventSlice3.ResolveRaidPoints(map, faction, requestedPoints);
        internal static float ResolveBaseRaidPointsFromStoryteller(Map map) => DiplomacyEventSlice3.ResolveBaseRaidPointsFromStoryteller(map);
        internal static float ApplyRaidPointTuning(Faction faction, float basePoints) => DiplomacyEventSlice3.ApplyRaidPointTuning(faction, basePoints);
        internal static bool IsArrivalModeCompatible(PawnsArrivalModeDef arrivalMode, RaidStrategyDef strategy) => DiplomacyEventSlice3.IsArrivalModeCompatible(arrivalMode, strategy);
        internal static RaidStrategyDef GetFallbackStrategy(Faction faction, Map map) => DiplomacyEventSlice3.GetFallbackStrategy(faction, map);
        internal static PawnsArrivalModeDef GetFallbackArrivalMode(RaidStrategyDef strategy) => DiplomacyEventSlice3.GetFallbackArrivalMode(strategy);
        public static int CalculateRaidDelayTicks(RaidStrategyDef strategy, PawnsArrivalModeDef arrivalMode) => DiplomacyEventSlice3.CalculateRaidDelayTicks(strategy, arrivalMode);
        public static bool ScheduleDelayedRaid(Faction faction, float points, RaidStrategyDef strategy, PawnsArrivalModeDef arrivalMode) => DiplomacyEventSlice3.ScheduleDelayedRaid(faction, points, strategy, arrivalMode);
        public static bool ScheduleRaidCallEveryone(Faction sourceFaction, System.Collections.Generic.List<Faction> targetFactions) => DiplomacyEventSlice3.ScheduleRaidCallEveryone(sourceFaction, targetFactions);
        internal static List<Faction> BalanceCallEveryoneParticipants(List<Faction> targetFactions) => DiplomacyEventSlice3.BalanceCallEveryoneParticipants(targetFactions);
        internal static float GetPlayerMapWealth() => DiplomacyEventSlice3.GetPlayerMapWealth();
        internal static int ResolveMaxHostileFactionsForCallEveryone(List<Faction> allFactions, float playerWealth) => DiplomacyEventSlice3.ResolveMaxHostileFactionsForCallEveryone(allFactions, playerWealth);
        internal static bool TryEnqueueRaidCallEveryoneSocialPost(Faction sourceFaction, bool isFollowup) => DiplomacyEventSlice3.TryEnqueueRaidCallEveryoneSocialPost(sourceFaction, isFollowup);
        internal static bool TryEnqueueRaidWavesFirstArrivalSocialPost(Faction sourceFaction, int totalWaves) => DiplomacyEventSlice3.TryEnqueueRaidWavesFirstArrivalSocialPost(sourceFaction, totalWaves);
        internal static void ScheduleRaidCallEveryoneFollowupSocialPost(Faction sourceFaction, int currentTick) => DiplomacyEventSlice4.ScheduleRaidCallEveryoneFollowupSocialPost(sourceFaction, currentTick);
        internal static bool TryBuildCallEveryoneAidParms(Faction faction, out Map map, out IncidentParms aidParms, out string reason) => DiplomacyEventSlice4.TryBuildCallEveryoneAidParms(faction, out map, out aidParms, out reason);
        internal static bool TryGenerateCallEveryoneAidPawns(IncidentParms aidParms, out List<Pawn> pawns, out string reason) => DiplomacyEventSlice4.TryGenerateCallEveryoneAidPawns(aidParms, out pawns, out reason);
        internal static bool TryArriveCallEveryoneAidPawns(Map map, IncidentParms aidParms, List<Pawn> pawns, out string reason) => DiplomacyEventSlice4.TryArriveCallEveryoneAidPawns(map, aidParms, pawns, out reason);
        internal static bool TryFindCallEveryoneAidEntryCell(Map map, out IntVec3 entryCell, out string reason) => DiplomacyEventSlice4.TryFindCallEveryoneAidEntryCell(map, out entryCell, out reason);
        internal static bool TrySpawnAidPawnNearEntry(Map map, IntVec3 entryCell, Pawn pawn) => DiplomacyEventSlice4.TrySpawnAidPawnNearEntry(map, entryCell, pawn);
        public static bool ScheduleRaidWaves(Faction faction, int waves) => DiplomacyEventSlice4.ScheduleRaidWaves(faction, waves);
        #endregion
}
    internal static class DiplomacyEventSlice1
    {
public static bool TriggerCaravanEvent(Faction faction, CaravanType caravanType)
        {
            try
            {
                Map map = Find.AnyPlayerHomeMap;
                if (map == null)
                {
                    DebugLogger.WarningGated("No player home map found for caravan event");
                    return false;
                }

                if (!DiplomacyEventManager.TryFindNearestFactionSettlement(faction, map.Tile, out _))
                {
                    DebugLogger.WarningGated($"Caravan trigger: {faction.Name} has no reachable settlement near tile {map.Tile}; attempting without settlement check.");
                }

                IncidentParms parms = new IncidentParms();
                parms.target = map;
                parms.faction = faction;

                TraderKindDef traderKind = DiplomacyEventManager.GetTraderKindForType(faction, caravanType);
                if (traderKind != null)
                {
                    parms.traderKind = traderKind;
                }
                else
                {
                    DebugLogger.WarningGated($"No trader kind found for {faction.Name} type={caravanType}; TryExecute will use faction default.");
                }

                IncidentDef incidentDef = IncidentDefOf.TraderCaravanArrival;
                bool canFire = incidentDef.Worker.CanFireNow(parms);
                DebugLogger.Debug($"Caravan pre-check: faction={faction.Name}, type={caravanType}, traderKind={traderKind?.defName ?? "null"}, canFireNow={canFire}, goodwill={faction.PlayerGoodwill}, relation={faction.RelationKindWith(Faction.OfPlayer)}");

                bool success = incidentDef.Worker.TryExecute(parms);

                if (success)
                {
                    DebugLogger.Debug($"Triggered {caravanType} caravan from {faction.Name}");
                }
                else
                {
                    DebugLogger.WarningGated($"Failed to trigger {caravanType} caravan from {faction.Name} (canFireNow was {canFire})");
                }

                return success;
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Error triggering caravan event: {ex}");
                return false;
            }
        }

public static bool TriggerVisitorEvent(Faction faction)
        {
            try
            {
                Map map = Find.AnyPlayerHomeMap;
                if (map == null)
                {
                    DebugLogger.WarningGated("No player home map found for visitor event");
                    return false;
                }

                IncidentDef incidentDef = DefDatabase<IncidentDef>.GetNamedSilentFail("VisitorGroup");
                if (incidentDef == null)
                {
                    DebugLogger.Error("VisitorGroup incident def not found");
                    return false;
                }

                IncidentParms parms = StorytellerUtility.DefaultParmsNow(incidentDef.category, map);
                parms.target = map;
                parms.faction = faction;
                bool success = incidentDef.Worker.TryExecute(parms);

                if (success)
                {
                    DebugLogger.Debug($"Triggered visitor group from {faction.Name}");
                }
                else
                {
                    DebugLogger.WarningGated($"Failed to trigger visitor group from {faction.Name}");
                }

                return success;
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Error triggering visitor event: {ex}");
                return false;
            }
        }

internal static TraderKindDef GetTraderKindForType(Faction faction, CaravanType caravanType)
        {
            List<TraderKindDef> factionTraders = DiplomacyEventManager.GetFactionGroundTraderKinds(faction);
            if (factionTraders.Count == 0)
            {
                DebugLogger.WarningGated($"Faction {faction?.Name ?? "null"} has no ground caravan traders; leave traderKind null.");
                return null;
            }

            List<TraderKindDef> matchingTraders = factionTraders
                .Where(trader => DiplomacyEventManager.MatchesCaravanType(trader, caravanType))
                .ToList();

            DebugLogger.Debug($"Faction trader pool for {faction?.Name ?? "null"}: total={factionTraders.Count}, typeMatched={matchingTraders.Count}, requestedType={caravanType}");
            foreach (TraderKindDef trader in matchingTraders)
            {
                DebugLogger.Debug($"- {trader.defName}");
            }

            if (matchingTraders.Count > 0)
            {
                TraderKindDef selected = matchingTraders.RandomElement();
                DebugLogger.Debug($"Selected faction-matched trader: {selected.defName}");
                return selected;
            }

            // Fail fast to faction-safe fallback instead of global cross-faction randomization.
            TraderKindDef factionFallback = factionTraders.RandomElement();
            DebugLogger.WarningGated($"No trader matched {caravanType} for {faction?.Name ?? "null"}, fallback to faction trader {factionFallback.defName}.");
            return factionFallback;
        }

internal static List<TraderKindDef> GetFactionGroundTraderKinds(Faction faction)
        {
            List<TraderKindDef> source = faction?.def?.caravanTraderKinds;
            if (source == null || source.Count == 0)
            {
                return new List<TraderKindDef>();
            }

            List<TraderKindDef> result = new List<TraderKindDef>(source.Count);
            foreach (TraderKindDef trader in source)
            {
                if (trader == null || trader.orbital)
                {
                    continue;
                }

                result.Add(trader);
            }

            return result;
        }

internal static bool MatchesCaravanType(TraderKindDef trader, CaravanType caravanType)
        {
            string defName = trader?.defName ?? string.Empty;
            switch (caravanType)
            {
                case CaravanType.General:
                    return DiplomacyEventManager.DefNameContains(defName, "standard") || DiplomacyEventManager.DefNameContains(defName, "general");
                case CaravanType.BulkGoods:
                    return DiplomacyEventManager.DefNameContains(defName, "bulk");
                case CaravanType.CombatSupplier:
                    return DiplomacyEventManager.DefNameContains(defName, "combat") || DiplomacyEventManager.DefNameContains(defName, "weapon");
                case CaravanType.Exotic:
                    return DiplomacyEventManager.DefNameContains(defName, "exotic");
                case CaravanType.Slaver:
                    return DiplomacyEventManager.DefNameContains(defName, "slave");
                default:
                    return false;
            }
        }

public static bool TriggerAidEvent(Faction faction, AidType aidType)
        {
            try
            {
                Map map = Find.AnyPlayerHomeMap;
                if (map == null)
                {
                    DebugLogger.WarningGated("No player home map found for aid event");
                    return false;
                }

                switch (aidType)
                {
                    case AidType.Military:
                        return DiplomacyEventManager.TriggerMilitaryAid(faction, map);
                    case AidType.Medical:
                        return DiplomacyEventManager.TriggerMedicalAid(faction, map);
                    case AidType.Resources:
                        return DiplomacyEventManager.TriggerResourceAid(faction, map);
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Error triggering aid event: {ex}");
                return false;
            }
        }

public static bool TriggerMilitaryAidEvent(Faction faction)
        {
            try
            {
                Map map = Find.AnyPlayerHomeMap;
                if (map == null)
                {
                    DebugLogger.WarningGated("No player home map found for military aid event");
                    return false;
                }

                if (faction == null || faction.defeated)
                {
                    DebugLogger.WarningGated("Invalid faction for military aid");
                    return false;
                }

                return DiplomacyEventManager.TriggerMilitaryAid(faction, map);
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Error triggering military aid event: {ex}");
                return false;
            }
        }

public static bool TriggerMilitaryAidCallEveryoneEvent(Faction faction)
        {
            if (!DiplomacyEventManager.TryBuildCallEveryoneAidParms(faction, out Map map, out IncidentParms aidParms, out string parmReason))
            {
                DebugLogger.Error($"CallEveryoneCustomAidFailFast] faction={faction?.Name ?? "null"}, stage=BuildParms, reason={parmReason}");
                return false;
            }

            if (!DiplomacyEventManager.TryGenerateCallEveryoneAidPawns(aidParms, out List<Pawn> pawns, out string pawnReason))
            {
                DebugLogger.Error($"CallEveryoneCustomAidFailFast] faction={faction?.Name ?? "null"}, stage=GeneratePawns, reason={pawnReason}");
                return false;
            }

            if (!DiplomacyEventManager.TryArriveCallEveryoneAidPawns(map, aidParms, pawns, out string arriveReason))
            {
                DebugLogger.Error($"CallEveryoneCustomAidFailFast] faction={faction?.Name ?? "null"}, stage=Arrive, reason={arriveReason}");
                return false;
            }

            DebugLogger.Debug($"Triggered custom military aid from {faction.Name}, pawns={pawns.Count}");
            DiplomacyEventManager.SendAidLetter(faction, "RimChat_MilitaryAidArrivedTitle".Translate(),
                "RimChat_MilitaryAidLetterBody".Translate(faction.Name));
            return true;
        }

internal static bool TriggerMilitaryAid(Faction faction, Map map)
        {
            DebugLogger.Debug($"Military aid pre-check: faction={faction.Name}, defeated={faction.defeated}, def={faction.def?.defName}, goodwill={faction.PlayerGoodwill}, relation={faction.RelationKindWith(Faction.OfPlayer)}");

            IncidentParms parms = new IncidentParms
            {
                target = map,
                faction = faction,
                points = StorytellerUtility.DefaultThreatPointsNow(map) * 0.5f,
                forced = true
            };

            if (DiplomacyEventManager.TryResolveExecutableMilitaryAidIncident(parms, out IncidentDef militaryAidDef, out string resolveReason))
            {
                bool success = militaryAidDef.Worker.TryExecute(parms);
                if (success)
                {
                    DebugLogger.Debug($"AidIncidentResolve] faction={faction.Name}, incident={militaryAidDef.defName}, result=success");
                    DebugLogger.Debug($"Triggered military aid from {faction.Name}");
                    DiplomacyEventManager.SendAidLetter(faction, "RimChat_MilitaryAidArrivedTitle".Translate(),
                        "RimChat_MilitaryAidLetterBody".Translate(faction.Name));
                    return true;
                }

                DebugLogger.WarningGated($"AidIncidentResolve] faction={faction.Name}, incident={militaryAidDef.defName}, TryExecute returned false; falling back to custom spawn.");
            }
            else
            {
                DebugLogger.WarningGated($"AidIncidentResolve] faction={faction.Name}, vanilla incident unavailable ({resolveReason}); falling back to custom spawn.");
            }

            // Fallback: custom pawn generation (same approach as CallEveryone military aid)
            return DiplomacyEventManager.TriggerMilitaryAidCustomFallback(faction);
        }

internal static bool TriggerMilitaryAidCustomFallback(Faction faction)
        {
            if (!DiplomacyEventManager.TryBuildCallEveryoneAidParms(faction, out Map map, out IncidentParms aidParms, out string buildReason))
            {
                DebugLogger.Error($"AidCustomFallbackFailFast] faction={faction?.Name ?? "null"}, stage=BuildParms, reason={buildReason}");
                return false;
            }

            if (!DiplomacyEventManager.TryGenerateCallEveryoneAidPawns(aidParms, out List<Pawn> pawns, out string pawnReason))
            {
                DebugLogger.Error($"AidCustomFallbackFailFast] faction={faction?.Name ?? "null"}, stage=GeneratePawns, reason={pawnReason}");
                return false;
            }

            if (!DiplomacyEventManager.TryArriveCallEveryoneAidPawns(map, aidParms, pawns, out string arriveReason))
            {
                DebugLogger.Error($"AidCustomFallbackFailFast] faction={faction?.Name ?? "null"}, stage=Arrive, reason={arriveReason}");
                return false;
            }

            DebugLogger.Debug($"Triggered military aid (custom fallback) from {faction.Name}, pawns={pawns.Count}");
            DiplomacyEventManager.SendAidLetter(faction, "RimChat_MilitaryAidArrivedTitle".Translate(),
                "RimChat_MilitaryAidLetterBody".Translate(faction.Name));
            return true;
        }

internal static bool TryResolveExecutableMilitaryAidIncident(
            IncidentParms parms,
            out IncidentDef incidentDef,
            out string reason)
        {
            incidentDef = null;
            reason = "NoCandidateIncidentDef";

            List<string> observed = new List<string>();
            foreach (string candidate in MilitaryAidIncidentCandidates)
            {
                IncidentDef def = DefDatabase<IncidentDef>.GetNamedSilentFail(candidate);
                if (def == null)
                {
                    observed.Add($"{candidate}:Missing");
                    continue;
                }

                if (def.Worker == null)
                {
                    observed.Add($"{candidate}:NoWorker");
                    continue;
                }

                if (!def.Worker.CanFireNow(parms))
                {
                    observed.Add($"{candidate}:CanFireNowFalse");
                    continue;
                }

                incidentDef = def;
                reason = $"Resolved:{def.defName}";
                DebugLogger.Debug($"AidIncidentResolve] faction={parms.faction?.Name ?? "null"}, selected={def.defName}, observed={string.Join(",", observed)}");
                return true;
            }

            reason = $"NoExecutableCandidate; observed={string.Join(",", observed)}";
            DebugLogger.Error($"AidIncidentResolve] faction={parms.faction?.Name ?? "null"}, selected=<none>, observed={string.Join(",", observed)}");
            return false;
        }

internal static bool TriggerMedicalAid(Faction faction, Map map)
        {
            List<Thing> medicalSupplies = DiplomacyEventManager.GenerateMedicalSupplies();
            DropPodUtility.DropThingsNear(
                DropCellFinder.TradeDropSpot(map),
                map,
                medicalSupplies,
                110,
                canInstaDropDuringInit: false,
                leaveSlag: false,
                canRoofPunch: false
            );

            DiplomacyEventManager.SendAidLetter(faction, "RimChat_MedicalAidArrivedTitle".Translate(), 
                "RimChat_MedicalAidLetterBody".Translate(faction.Name));
            
            DebugLogger.Debug($"Triggered medical aid from {faction.Name}");
            return true;
        }

internal static bool TriggerResourceAid(Faction faction, Map map)
        {
            List<Thing> resources = DiplomacyEventManager.GenerateResourceSupplies();
            DropPodUtility.DropThingsNear(
                DropCellFinder.TradeDropSpot(map),
                map,
                resources,
                110,
                canInstaDropDuringInit: false,
                leaveSlag: false,
                canRoofPunch: false
            );

            DiplomacyEventManager.SendAidLetter(faction, "RimChat_ResourceAidArrivedTitle".Translate(), 
                "RimChat_ResourceAidLetterBody".Translate(faction.Name));
            
            DebugLogger.Debug($"Triggered resource aid from {faction.Name}");
            return true;
        }

internal static List<Thing> GenerateMedicalSupplies()
        {
            List<Thing> supplies = new List<Thing>();
            
            ThingDef medicineDef = ThingDefOf.MedicineIndustrial;
            Thing medicine = ThingMaker.MakeThing(medicineDef);
            medicine.stackCount = Rand.Range(15, 30);
            supplies.Add(medicine);
            
            ThingDef herbalMedicineDef = ThingDefOf.MedicineHerbal;
            Thing herbalMedicine = ThingMaker.MakeThing(herbalMedicineDef);
            herbalMedicine.stackCount = Rand.Range(20, 40);
            supplies.Add(herbalMedicine);
            
            ThingDef bandageDef = ThingDef.Named("Bandage");
            if (bandageDef != null)
            {
                Thing bandages = ThingMaker.MakeThing(bandageDef);
                bandages.stackCount = Rand.Range(10, 20);
                supplies.Add(bandages);
            }
            
            return supplies;
        }

internal static List<Thing> GenerateResourceSupplies()
        {
            List<Thing> supplies = new List<Thing>();
            
            ThingDef woodDef = ThingDefOf.WoodLog;
            Thing wood = ThingMaker.MakeThing(woodDef);
            wood.stackCount = Rand.Range(100, 200);
            supplies.Add(wood);
            
            ThingDef steelDef = ThingDefOf.Steel;
            Thing steel = ThingMaker.MakeThing(steelDef);
            steel.stackCount = Rand.Range(50, 100);
            supplies.Add(steel);
            
            ThingDef foodDef = ThingDefOf.MealSimple;
            Thing food = ThingMaker.MakeThing(foodDef);
            food.stackCount = Rand.Range(30, 50);
            supplies.Add(food);
            
            return supplies;
        }
    }

    internal static class DiplomacyEventSlice2
    {
internal static void SendAidLetter(Faction faction, string title, string message)
        {
            Map map = Find.AnyPlayerHomeMap;
            LookTargets lookTargets = map != null
                ? new LookTargets(new TargetInfo(map.Center, map))
                : null;
            Find.LetterStack.ReceiveLetter(
                title,
                message,
                LetterDefOf.PositiveEvent,
                lookTargets,
                faction
            );
        }

public static string GetCaravanTypeLabel(CaravanType type)
        {
            return type switch
            {
                CaravanType.General => "GeneralTrader".Translate(),
                CaravanType.BulkGoods => "BulkGoodsTrader".Translate(),
                CaravanType.CombatSupplier => "CombatSupplier".Translate(),
                CaravanType.Exotic => "ExoticTrader".Translate(),
                CaravanType.Slaver => "Slaver".Translate(),
                _ => type.ToString()
            };
        }

public static string GetAidTypeLabel(AidType type)
        {
            return type switch
            {
                AidType.Military => "MilitaryAid".Translate(),
                AidType.Medical => "MedicalAid".Translate(),
                AidType.Resources => "ResourceAid".Translate(),
                _ => type.ToString()
            };
        }

public static CaravanType ParseCaravanType(string typeStr)
        {
            if (Enum.TryParse(typeStr, true, out CaravanType type))
            {
                return type;
            }

            throw new ArgumentException($"Invalid CaravanType: {typeStr}", nameof(typeStr));
        }

public static AidType ParseAidType(string typeStr)
        {
            if (Enum.TryParse(typeStr, true, out AidType type))
            {
                return type;
            }
            return AidType.Military;
        }

internal static bool TryFindNearestFactionSettlement(Faction faction, int fromTile, out int distanceTiles)
        {
            distanceTiles = 0;
            if (faction == null || !WorldTileGuard.IsValidTile(fromTile)) return false;

            var settlements = Find.WorldObjects?.Settlements?
                .Where(s => s != null && s.Faction == faction && WorldTileGuard.IsValidTile(s.Tile))
                .ToList();

            if (settlements == null || settlements.Count == 0) return false;

            distanceTiles = settlements
                .Min(s => Find.WorldGrid.TraversalDistanceBetween(fromTile, s.Tile));
            return true;
        }

public static int CalculateDelayTicks(Faction faction, bool isAid = false)
        {
            int baseTicks = isAid
                ? (RelationsMod.Instance?.InstanceSettings?.AidDelayBaseTicks ?? 90000)
                : (RelationsMod.Instance?.InstanceSettings?.CaravanDelayBaseTicks ?? 135000);

            int homeTile = Find.AnyPlayerHomeMap?.Tile ?? -1;
            int distanceTiles = 0;
            if (!isAid && WorldTileGuard.IsValidTile(homeTile))
            {
                if (DiplomacyEventManager.TryFindNearestFactionSettlement(faction, homeTile, out int dist))
                    distanceTiles = dist;
            }
            int distanceTicks = Math.Max(0, distanceTiles - 5) * 3000;

            float modifier = 1.0f;
            int goodwill = faction.PlayerGoodwill;
            var relation = faction.RelationKindWith(Faction.OfPlayer);

            if (relation == FactionRelationKind.Ally)
            {
                modifier = 0.5f;
            }
            else if (goodwill >= 40)
            {
                modifier = 0.7f;
            }
            else if (goodwill < 0)
            {
                modifier = 1.5f;
            }

            float randomFactor = Rand.Range(0.8f, 1.2f);
            int delayTicks = (int)((baseTicks + distanceTicks) * modifier * randomFactor);
            return delayTicks;
        }

public static bool ScheduleDelayedCaravan(Faction faction, CaravanType caravanType)
        {
            try
            {
                int homeTile = Find.AnyPlayerHomeMap?.Tile ?? -1;
                if (!DiplomacyEventManager.TryFindNearestFactionSettlement(faction, homeTile, out int distanceTiles))
                {
                    DebugLogger.WarningGated($"Cannot schedule caravan from {faction.Name}: no valid settlement found.");
                    return false;
                }

                int delayTicks = DiplomacyEventManager.CalculateDelayTicks(faction, false);
                int executeTick = Find.TickManager.TicksGame + delayTicks;

                var evt = new DelayedDiplomacyEvent(DelayedEventType.Caravan, faction, executeTick)
                {
                    CaravanType = caravanType
                };

                GameComponent_DiplomacyManager.Instance?.AddDelayedEvent(evt);

                float delayDays = delayTicks / 60000f;
                string caravanTypeLabel = DiplomacyEventManager.GetCaravanTypeLabel(caravanType);
                DiplomacyNotificationManager.SendDelayedEventScheduledNotification(faction, DelayedEventType.Caravan, caravanTypeLabel, delayDays);

                DebugLogger.Debug($"Scheduled delayed caravan from {faction.Name}, type={caravanType}, delay={delayDays:F1} days, distance={distanceTiles} tiles, goodwill={faction.PlayerGoodwill}, relation={faction.RelationKindWith(Faction.OfPlayer)}");
                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Error scheduling delayed caravan: {ex}");
                return false;
            }
        }

public static bool ScheduleDelayedAid(Faction faction, AidType aidType)
        {
            try
            {
                int delayTicks = DiplomacyEventManager.CalculateDelayTicks(faction, true);
                int executeTick = Find.TickManager.TicksGame + delayTicks;

                var evt = new DelayedDiplomacyEvent(DelayedEventType.Aid, faction, executeTick)
                {
                    AidType = aidType
                };

                GameComponent_DiplomacyManager.Instance?.AddDelayedEvent(evt);

                float delayDays = delayTicks / 60000f;
                string aidTypeLabel = DiplomacyEventManager.GetAidTypeLabel(aidType);
                DiplomacyNotificationManager.SendDelayedEventScheduledNotification(faction, DelayedEventType.Aid, aidTypeLabel, delayDays);

                DebugLogger.Debug($"Scheduled delayed aid from {faction.Name}, type={aidType}, delay={delayDays:F1} days");
                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Error scheduling delayed aid: {ex}");
                return false;
            }
        }

public static bool ScheduleDelayedVisitor(Faction faction)
        {
            try
            {
                int delayTicks = DiplomacyEventManager.CalculateDelayTicks(faction, false);
                int executeTick = Find.TickManager.TicksGame + delayTicks;

                var evt = new DelayedDiplomacyEvent(DelayedEventType.Visitor, faction, executeTick);
                GameComponent_DiplomacyManager.Instance?.AddDelayedEvent(evt);

                float delayDays = delayTicks / 60000f;
                string detail = "RimChat_VisitorGroupLabel".Translate();
                DiplomacyNotificationManager.SendDelayedEventScheduledNotification(faction, DelayedEventType.Visitor, detail, delayDays);

                DebugLogger.Debug($"Scheduled delayed visitor group from {faction.Name}, delay={delayDays:F1} days");
                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Error scheduling delayed visitor event: {ex}");
                return false;
            }
        }

public static bool TriggerRaidEvent(Faction faction, float points, RaidStrategyDef strategy, PawnsArrivalModeDef arrivalMode)
        {
            try
            {
                if (!DiplomacyEventManager.TryValidateRaidFaction(faction, out string factionValidationReason))
                {
                    DebugLogger.WarningGated($"Raid blocked: {factionValidationReason}");
                    return false;
                }

                Map map = Find.AnyPlayerHomeMap;
                if (map == null)
                {
                    DebugLogger.WarningGated("No player home map found for raid event");
                    return false;
                }

                float raidPoints = DiplomacyEventManager.ResolveRaidPoints(map, faction, points);

                if (DiplomacyEventManager.IsMiliraFaction(faction))
                {
                    if (DiplomacyEventManager.TryExecuteMiliraRaidFallback(map, faction, raidPoints, out string miliraDirectReason))
                    {
                        return true;
                    }

                    DebugLogger.WarningGated($"Milira direct raid fallback failed: {miliraDirectReason}");
                    return false;
                }

                // Normalize strategy: ensure it's valid and executable
                RaidStrategyDef normalizedStrategy = strategy;
                if (normalizedStrategy == null || !DiplomacyEventManager.IsStrategyExecutable(normalizedStrategy, faction, map))
                {
                    normalizedStrategy = DiplomacyEventManager.GetFallbackStrategy(faction, map);
                    if (normalizedStrategy == null)
                    {
                        DebugLogger.WarningGated($"Cannot find executable raid strategy for {faction?.Name}; falling back to vanilla raid strategy resolution.");
                    }
                    else
                    {
                        DebugLogger.WarningGated($"Strategy {strategy?.defName} not executable, using fallback {normalizedStrategy.defName}");
                    }
                }

                // Normalize arrival mode: ensure it's valid and compatible
                PawnsArrivalModeDef normalizedArrivalMode = arrivalMode;
                if (normalizedStrategy == null)
                {
                    normalizedArrivalMode = null;
                }
                else if (normalizedArrivalMode == null || !DiplomacyEventManager.IsArrivalModeCompatible(normalizedArrivalMode, normalizedStrategy))
                {
                    normalizedArrivalMode = DiplomacyEventManager.GetFallbackArrivalMode(normalizedStrategy);
                    if (normalizedArrivalMode == null)
                    {
                        DebugLogger.Error($"Cannot find compatible arrival mode for strategy {normalizedStrategy?.defName}");
                        return false;
                    }
                    DebugLogger.WarningGated($"Arrival mode {arrivalMode?.defName} not compatible, using fallback {normalizedArrivalMode.defName}");
                }

                IncidentDef incidentDef = IncidentDefOf.RaidEnemy;
                if (incidentDef == null || incidentDef.Worker == null)
                {
                    DebugLogger.Error("RaidEnemy incident def/worker is unavailable.");
                    return false;
                }

                IncidentParms parms = DiplomacyEventManager.BuildRaidIncidentParmsWithDefaults(
                    incidentDef,
                    map,
                    faction,
                    raidPoints,
                    normalizedStrategy,
                    normalizedArrivalMode);
                if (!DiplomacyEventManager.EnsureUsableCombatPawnGroupMakerForParms(faction, parms, out string groupPreflightReason))
                {
                    DebugLogger.WarningGated($"Raid group preflight could not ensure usable combat maker: {groupPreflightReason}");
                }

                if (!incidentDef.Worker.CanFireNow(parms))
                {
                    if (DiplomacyEventManager.TryExecuteRaidWithVanillaAutoFallback(incidentDef, map, faction, raidPoints, out string vanillaAutoReason))
                    {
                        DebugLogger.WarningGated($"Raid precheck blocked for strategy={normalizedStrategy?.defName ?? "auto"}, arrival={normalizedArrivalMode?.defName ?? "auto"}; forced vanilla auto fallback succeeded.");
                        return true;
                    }

                    if (DiplomacyEventManager.TryExecuteMiliraRaidFallback(map, faction, raidPoints, out string miliraFallbackReason))
                    {
                        return true;
                    }

                    DebugLogger.WarningGated($"Raid precheck blocked for faction={faction.Name}, def={faction.def?.defName}, relation={faction.RelationKindWith(Faction.OfPlayer)}, points={raidPoints:F1}, strategy={normalizedStrategy?.defName ?? "auto"}, arrival={normalizedArrivalMode?.defName ?? "auto"}, vanillaAuto={vanillaAutoReason}, miliraFallback={miliraFallbackReason}. {DiplomacyEventManager.DescribeRaidGroupMakerState(faction)}");
                    return false;
                }

                bool success = incidentDef.Worker.TryExecute(parms);

                if (success)
                {
                    DebugLogger.Debug($"Triggered raid from {faction.Name} with strategy {normalizedStrategy?.defName ?? "auto"} and arrival {normalizedArrivalMode?.defName ?? "auto"}");
                }
                else
                {
                    if (DiplomacyEventManager.TryExecuteRaidWithVanillaAutoFallback(incidentDef, map, faction, raidPoints, out string vanillaAutoReason))
                    {
                        DebugLogger.WarningGated($"Raid execution failed for strategy={normalizedStrategy?.defName ?? "auto"}, arrival={normalizedArrivalMode?.defName ?? "auto"}; forced vanilla auto fallback succeeded.");
                        return true;
                    }

                    if (DiplomacyEventManager.TryExecuteMiliraRaidFallback(map, faction, raidPoints, out string miliraFallbackReason))
                    {
                        return true;
                    }

                    DebugLogger.WarningGated($"Failed to trigger raid from {faction.Name}, vanillaAuto={vanillaAutoReason}, miliraFallback={miliraFallbackReason}. {DiplomacyEventManager.DescribeRaidGroupMakerState(faction)}");
                }

                return success;
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Error triggering raid event: {ex}");
                return false;
            }
        }

public static bool TryValidateRaidFaction(Faction faction, out string reason)
        {
            reason = string.Empty;
            if (faction == null)
            {
                reason = "Faction cannot be null.";
                return false;
            }

            if (faction.defeated)
            {
                reason = $"Faction {faction.Name} is defeated.";
                return false;
            }

            if (faction.RelationKindWith(Faction.OfPlayer) != FactionRelationKind.Hostile)
            {
                reason = $"Faction {faction.Name} is not hostile to the player.";
                return false;
            }

            if (!DiplomacyEventManager.EnsureRaidTemplates(faction, out reason))
            {
                reason = $"Faction {faction.Name} cannot launch raids: {reason}";
                return false;
            }

            return true;
        }

internal static bool HasUsableCombatPawnGroupMaker(Faction faction, out string reason)
        {
            reason = string.Empty;
            List<PawnGroupMaker> makers = faction?.def?.pawnGroupMakers;
            if (makers == null || makers.Count == 0)
            {
                reason = "no pawnGroupMakers defined on faction def.";
                return false;
            }

            List<PawnGroupMaker> combatMakers = makers
                .Where(m => m?.kindDef == PawnGroupKindDefOf.Combat)
                .ToList();
            if (combatMakers.Count == 0)
            {
                string availableKinds = string.Join(", ", makers
                    .Where(m => m?.kindDef != null)
                    .Select(m => m.kindDef.defName)
                    .Distinct()
                    .OrderBy(name => name));
                reason = $"missing Combat pawnGroupMaker (available kinds: {availableKinds}).";
                return false;
            }

            bool hasOptions = combatMakers.Any(m => m.options != null && m.options.Count > 0);
            if (!hasOptions)
            {
                reason = "Combat pawnGroupMaker exists but has no options.";
                return false;
            }

            return true;
        }

internal static string DescribeRaidGroupMakerState(Faction faction)
        {
            if (faction?.def?.pawnGroupMakers == null || faction.def.pawnGroupMakers.Count == 0)
            {
                return "FactionDef has no pawnGroupMakers.";
            }

            int total = faction.def.pawnGroupMakers.Count;
            int combat = faction.def.pawnGroupMakers.Count(m => m?.kindDef == PawnGroupKindDefOf.Combat);
            int combatWithOptions = faction.def.pawnGroupMakers.Count(m =>
                m?.kindDef == PawnGroupKindDefOf.Combat &&
                m.options != null &&
                m.options.Count > 0);
            return $"PawnGroupMakers total={total}, combat={combat}, combatWithOptions={combatWithOptions}.";
        }

internal static bool IsStrategyExecutable(RaidStrategyDef strategy, Faction faction, Map map)
        {
            if (strategy == null || faction == null || map == null)
            {
                return false;
            }

            try
            {
                return strategy.Worker != null && strategy.Worker.CanUseWith(new IncidentParms { target = map, faction = faction }, PawnGroupKindDefOf.Combat);
            }
            catch
            {
                return false;
            }
        }
    }

    internal static class DiplomacyEventSlice3
    {
internal static bool TryExecuteRaidWithVanillaAutoFallback(
            IncidentDef incidentDef,
            Map map,
            Faction faction,
            float raidPoints,
            out string reason)
        {
            reason = "not attempted";
            if (incidentDef?.Worker == null || map == null || faction == null)
            {
                reason = "incident worker/map/faction is unavailable";
                return false;
            }

            IncidentParms autoParms = DiplomacyEventManager.BuildRaidIncidentParmsWithDefaults(
                incidentDef,
                map,
                faction,
                raidPoints,
                strategy: null,
                arrivalMode: null);
            if (!DiplomacyEventManager.EnsureUsableCombatPawnGroupMakerForParms(faction, autoParms, out string groupReason))
            {
                DebugLogger.WarningGated($"Vanilla auto fallback preflight warning: {groupReason}");
            }

            if (!incidentDef.Worker.CanFireNow(autoParms))
            {
                reason = "CanFireNow false with auto strategy/arrival";
                return false;
            }

            if (!incidentDef.Worker.TryExecute(autoParms))
            {
                reason = "TryExecute false with auto strategy/arrival";
                return false;
            }

            reason = "success";
            DebugLogger.Debug($"Triggered raid from {faction.Name} with forced vanilla auto strategy/arrival.");
            return true;
        }

internal static float ResolveRaidPoints(Map map, Faction faction, float requestedPoints)
        {
            float basePoints = requestedPoints;
            if (basePoints <= 0f)
            {
                basePoints = DiplomacyEventManager.ResolveBaseRaidPointsFromStoryteller(map);
            }

            return DiplomacyEventManager.ApplyRaidPointTuning(faction, basePoints);
        }

internal static float ResolveBaseRaidPointsFromStoryteller(Map map)
        {
            try
            {
                IncidentParms defaultRaidParms = StorytellerUtility.DefaultParmsNow(IncidentDefOf.RaidEnemy.category, map);
                if (defaultRaidParms != null && defaultRaidParms.points > 0f)
                {
                    return defaultRaidParms.points;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.WarningGated($"Failed to resolve default raid points from storyteller parms: {ex.Message}");
            }

            float fallbackThreatPoints = StorytellerUtility.DefaultThreatPointsNow(map);
            if (fallbackThreatPoints > 0f)
            {
                return fallbackThreatPoints;
            }

            return 35f;
        }

internal static float ApplyRaidPointTuning(Faction faction, float basePoints)
        {
            var settings = RelationsMod.Instance?.InstanceSettings;
            if (settings == null)
            {
                return basePoints;
            }

            settings.ResolveRaidPointTuning(faction, out float multiplier, out float minRaidPoints);
            float tunedPoints = basePoints * multiplier;
            return tunedPoints < minRaidPoints ? minRaidPoints : tunedPoints;
        }

internal static bool IsArrivalModeCompatible(PawnsArrivalModeDef arrivalMode, RaidStrategyDef strategy)
        {
            if (arrivalMode == null || strategy == null)
            {
                return false;
            }

            try
            {
                // Check if strategy allows this arrival mode
                if (strategy.arriveModes != null && strategy.arriveModes.Count > 0)
                {
                    return strategy.arriveModes.Contains(arrivalMode);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

internal static RaidStrategyDef GetFallbackStrategy(Faction faction, Map map)
        {
            try
            {
                var allStrategies = DefDatabase<RaidStrategyDef>.AllDefsListForReading;
                var executableStrategies = allStrategies
                    .Where(s => s != null && DiplomacyEventManager.IsStrategyExecutable(s, faction, map))
                    .ToList();

                if (executableStrategies.Count == 0)
                {
                    return null;
                }

                // Prefer ImmediateAttack as default
                var immediateAttack = executableStrategies.FirstOrDefault(s => s.defName == "ImmediateAttack");
                return immediateAttack ?? executableStrategies.First();
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Error getting fallback strategy: {ex}");
                return null;
            }
        }

internal static PawnsArrivalModeDef GetFallbackArrivalMode(RaidStrategyDef strategy)
        {
            try
            {
                // If strategy specifies allowed arrival modes, use first one
                if (strategy?.arriveModes != null && strategy.arriveModes.Count > 0)
                {
                    return strategy.arriveModes.First();
                }

                // Otherwise use EdgeWalkIn as universal fallback
                return PawnsArrivalModeDefOf.EdgeWalkIn;
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Error getting fallback arrival mode: {ex}");
                return PawnsArrivalModeDefOf.EdgeWalkIn;
            }
        }

public static int CalculateRaidDelayTicks(RaidStrategyDef strategy, PawnsArrivalModeDef arrivalMode)
        {
            // Siege strategy usually implies long preparation
            if (strategy != null && strategy.defName.ToLower().Contains("siege"))
            {
                return Rand.Range(15000, 20000); // 6~8 hours
            }

            // EdgeWalkIn implies travel
            if (arrivalMode == PawnsArrivalModeDefOf.EdgeWalkIn)
            {
                return Rand.Range(15000, 20000); // 6~8 hours
            }
            
            // DropPods (CenterDrop, EdgeDrop, etc.) are fast
            if (arrivalMode != null && arrivalMode.defName.ToLower().Contains("drop"))
            {
                return Rand.Range(2500, 5000); // 1~2 hours
            }

            // Default fallback
            return Rand.Range(10000, 15000);
        }

public static bool ScheduleDelayedRaid(Faction faction, float points, RaidStrategyDef strategy, PawnsArrivalModeDef arrivalMode)
        {
            try
            {
                int delayTicks = DiplomacyEventManager.CalculateRaidDelayTicks(strategy, arrivalMode);
                int executeTick = Find.TickManager.TicksGame + delayTicks;

                var evt = new DelayedDiplomacyEvent(DelayedEventType.Raid, faction, executeTick)
                {
                    RaidPoints = points,
                    RaidStrategy = strategy,
                    ArrivalMode = arrivalMode
                };

                GameComponent_DiplomacyManager.Instance?.AddDelayedEvent(evt);

                float delayHours = delayTicks / 2500f;
                string strategyLabel = strategy?.label ?? "Standard";
                DiplomacyNotificationManager.SendDelayedEventScheduledNotification(faction, DelayedEventType.Raid, strategyLabel, delayHours);

                DebugLogger.Debug($"Scheduled delayed raid from {faction.Name}, strategy={strategy?.defName}, delay={delayHours:F1} hours");
                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Error scheduling delayed raid: {ex}");
                return false;
            }
        }

public static bool ScheduleRaidCallEveryone(Faction sourceFaction, System.Collections.Generic.List<Faction> targetFactions)
        {
            try
            {
                if (targetFactions == null || targetFactions.Count == 0)
                {
                    DebugLogger.WarningGated("ScheduleRaidCallEveryone: No target factions provided.");
                    return false;
                }

                int currentTick = Find.TickManager.TicksGame;
                int windowStartTick = currentTick + (8 * 2500);
                int windowTicks = 4 * 2500; // 8-12 hours
                List<Faction> effectiveFactions = DiplomacyEventManager.BalanceCallEveryoneParticipants(targetFactions);
                if (effectiveFactions.Count == 0)
                {
                    DebugLogger.WarningGated("ScheduleRaidCallEveryone: No effective factions after balancing.");
                    return false;
                }

                int peaceUntilTick = windowStartTick + windowTicks + (12 * 2500);
                for (int i = 0; i < effectiveFactions.Count; i++)
                {
                    for (int j = i + 1; j < effectiveFactions.Count; j++)
                    {
                        if (effectiveFactions[i].RelationKindWith(effectiveFactions[j]) == FactionRelationKind.Hostile)
                            GameComponent_DiplomacyManager.Instance?.ApplyTempCrossFactionPeace(effectiveFactions[i], effectiveFactions[j], peaceUntilTick);
                    }
                }

                // 收集目标派系 defName
                var targetDefNames = effectiveFactions.Select(f => f.def?.defName).Where(n => !string.IsNullOrEmpty(n)).ToList();
                
                // 为每个派系创建延迟事件，统一随机分布在 16-30 小时窗口内
                foreach (var targetFaction in effectiveFactions)
                {
                    bool isNeutralOrBetter = targetFaction.PlayerGoodwill >= 0;
                    int randomOffset = Rand.Range(0, windowTicks);
                    int executeTick = windowStartTick + randomOffset;
                    
                    var evt = new DelayedDiplomacyEvent(DelayedEventType.RaidCallEveryone, targetFaction, executeTick)
                    {
                        RaidPoints = -1, // 自动计算
                        RaidStrategy = null, // 自动选择
                        ArrivalMode = null,
                        TargetFactionDefNames = targetDefNames,
                        CurrentTargetIndex = targetDefNames.IndexOf(targetFaction.def?.defName),
                        MaxRetryCount = 0,
                        CallEveryoneAction = isNeutralOrBetter
                            ? CallEveryoneActionKind.MilitaryAidCustom
                            : CallEveryoneActionKind.Raid
                    };
                    
                    GameComponent_DiplomacyManager.Instance?.AddDelayedEvent(evt);

                    int announceDelay = Rand.Range(2 * 2500, 8 * 2500);
                    int announceTick = currentTick + announceDelay;
                    var announceEvt = new DelayedDiplomacyEvent(DelayedEventType.RaidCallEveryoneAnnounce, targetFaction, announceTick);
                    GameComponent_DiplomacyManager.Instance?.AddDelayedEvent(announceEvt);
                }
                
                // 统计敌对和友好派系数量
                int hostileCount = effectiveFactions.Count(f => f.RelationKindWith(Faction.OfPlayer) == FactionRelationKind.Hostile);
                int friendlyCount = effectiveFactions.Count - hostileCount;
                
                DebugLogger.Debug($"Scheduled raid_call_everyone: {effectiveFactions.Count} factions " +
                           $"({hostileCount} hostile, {friendlyCount} friendly/neutral), " +
                           $"all arrivals scheduled in 16-30 hours window; friendly/neutral uses custom military aid.");

                Faction notifyFaction = sourceFaction ?? effectiveFactions.FirstOrDefault();
                if (notifyFaction != null)
                {
                    string detail = $"{hostileCount}|{friendlyCount}|8|12";
                    DiplomacyNotificationManager.SendDelayedEventScheduledNotification(
                        notifyFaction,
                        DelayedEventType.RaidCallEveryone,
                        detail,
                        0f);
                }

                Faction socialPostFaction = sourceFaction ?? notifyFaction;
                if (socialPostFaction != null)
                {
                    DiplomacyEventManager.TryEnqueueRaidCallEveryoneSocialPost(socialPostFaction, isFollowup: false);
                    DiplomacyEventManager.ScheduleRaidCallEveryoneFollowupSocialPost(socialPostFaction, currentTick);
                }
                
                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Error scheduling raid_call_everyone: {ex}");
                return false;
            }
        }

internal static List<Faction> BalanceCallEveryoneParticipants(List<Faction> targetFactions)
        {
            List<Faction> effective = targetFactions
                .Where(f => f != null && !f.defeated && f.def != null)
                .ToList();

            float playerWealth = DiplomacyEventManager.GetPlayerMapWealth();
            int maxHostile = DiplomacyEventManager.ResolveMaxHostileFactionsForCallEveryone(effective, playerWealth);
            int maxFriendly = maxHostile / 2;

            List<Faction> hostileFactions = effective
                .Where(f => f.RelationKindWith(Faction.OfPlayer) == FactionRelationKind.Hostile)
                .OrderByDescending(f => f.PlayerGoodwill)
                .ToList();

            List<Faction> allyFactions = effective
                .Where(f => f.RelationKindWith(Faction.OfPlayer) == FactionRelationKind.Ally)
                .OrderByDescending(f => f.PlayerGoodwill)
                .ToList();

            List<Faction> result = new List<Faction>();
            result.AddRange(hostileFactions.Take(maxHostile));
            result.AddRange(allyFactions.Take(maxFriendly));

            int actualHostile = result.Count(f => f.RelationKindWith(Faction.OfPlayer) == FactionRelationKind.Hostile);
            int actualFriendly = result.Count(f => f.RelationKindWith(Faction.OfPlayer) == FactionRelationKind.Ally);
            DebugLogger.Debug($"CallEveryoneBalance] wealth={playerWealth:F0}, maxHostile={maxHostile}, maxFriendly={maxFriendly}, " +
                       $"actualHostile={actualHostile}, actualFriendly={actualFriendly}");

            return result;
        }

internal static float GetPlayerMapWealth()
        {
            Map playerMap = Find.AnyPlayerHomeMap;
            if (playerMap == null)
            {
                return 0f;
            }

            return playerMap.wealthWatcher?.WealthTotal ?? 0f;
        }

internal static int ResolveMaxHostileFactionsForCallEveryone(List<Faction> allFactions, float playerWealth)
        {
            if (playerWealth <= 0f)
            {
                return allFactions.Count(f => f.RelationKindWith(Faction.OfPlayer) == FactionRelationKind.Hostile);
            }

            int actualHostileCount = allFactions.Count(f => f.RelationKindWith(Faction.OfPlayer) == FactionRelationKind.Hostile);

            if (playerWealth < 100000f)
            {
                return actualHostileCount;
            }

            float wealthInWan = playerWealth / 10000f;
            int calculatedMax = (int)Math.Ceiling(10.5f - 0.05f * wealthInWan);
            int maxHostile = Math.Max(3, calculatedMax);
            return Math.Min(maxHostile, actualHostileCount);
        }

internal static bool TryEnqueueRaidCallEveryoneSocialPost(Faction sourceFaction, bool isFollowup)
        {
            if (sourceFaction == null || sourceFaction.defeated)
            {
                return false;
            }

            GameComponent_DiplomacyManager manager = GameComponent_DiplomacyManager.Instance;
            if (manager == null)
            {
                DebugLogger.WarningGated($"CallEveryoneSocialPost] manager unavailable, faction={sourceFaction.Name}, followup={isFollowup}");
                return false;
            }

            string summary = isFollowup
                ? "RimChat_RaidCallEveryoneSocialPostFollowup".Translate(sourceFaction.Name)
                : "RimChat_RaidCallEveryoneSocialPostImmediate".Translate(sourceFaction.Name);

            bool queued = manager.EnqueuePublicPost(
                sourceFaction,
                Faction.OfPlayer,
                SocialPostCategory.Military,
                sentiment: -2,
                summary: summary,
                isFromPlayerDialogue: false,
                reason: DebugGenerateReason.DialogueExplicit);

            DebugLogger.Debug($"CallEveryoneSocialPost] faction={sourceFaction.Name}, followup={isFollowup}, queued={queued}");
            return queued;
        }

internal static bool TryEnqueueRaidWavesFirstArrivalSocialPost(Faction sourceFaction, int totalWaves)
        {
            if (sourceFaction == null || sourceFaction.defeated)
            {
                return false;
            }

            GameComponent_DiplomacyManager manager = GameComponent_DiplomacyManager.Instance;
            if (manager == null)
            {
                DebugLogger.WarningGated($"RaidWavesSocialPost] manager unavailable, faction={sourceFaction.Name}, totalWaves={totalWaves}");
                return false;
            }

            int safeTotalWaves = Math.Max(2, totalWaves);
            string summary = "RimChat_RaidWavesFirstArrivalSocialPost".Translate(sourceFaction.Name, safeTotalWaves);
            bool queued = manager.EnqueuePublicPost(
                sourceFaction,
                Faction.OfPlayer,
                SocialPostCategory.Military,
                sentiment: -2,
                summary: summary,
                isFromPlayerDialogue: false,
                reason: DebugGenerateReason.DialogueExplicit);

            DebugLogger.Debug($"RaidWavesSocialPost] faction={sourceFaction.Name}, totalWaves={safeTotalWaves}, queued={queued}");
            return queued;
        }
    }

    internal static class DiplomacyEventSlice4
    {
internal static void ScheduleRaidCallEveryoneFollowupSocialPost(Faction sourceFaction, int currentTick)
        {
            if (sourceFaction == null || sourceFaction.defeated)
            {
                return;
            }

            int executeTick = currentTick + (36 * 2500);
            var evt = new DelayedDiplomacyEvent(DelayedEventType.RaidCallEveryoneSocialPost, sourceFaction, executeTick)
            {
                MaxRetryCount = 3
            };
            GameComponent_DiplomacyManager.Instance?.AddDelayedEvent(evt);
            DebugLogger.Debug($"CallEveryoneSocialPost] Scheduled follow-up social post for {sourceFaction.Name} at tick {executeTick}");
        }

internal static bool TryBuildCallEveryoneAidParms(
            Faction faction,
            out Map map,
            out IncidentParms aidParms,
            out string reason)
        {
            map = Find.AnyPlayerHomeMap;
            aidParms = null;

            if (map == null)
            {
                reason = "NoPlayerHomeMap";
                return false;
            }

            if (faction == null || faction.defeated)
            {
                reason = "InvalidFaction";
                return false;
            }

            if (faction.RelationKindWith(Faction.OfPlayer) == FactionRelationKind.Hostile)
            {
                reason = "HostileFactionNotAllowedForAid";
                return false;
            }

            float aidPoints = Math.Max(35f, StorytellerUtility.DefaultThreatPointsNow(map) * 0.5f);
            aidParms = DiplomacyEventManager.BuildRaidIncidentParmsWithDefaults(
                IncidentDefOf.RaidEnemy,
                map,
                faction,
                aidPoints,
                strategy: null,
                arrivalMode: PawnsArrivalModeDefOf.EdgeWalkIn);

            if (aidParms == null)
            {
                reason = "FailedToBuildIncidentParms";
                return false;
            }

            if (!DiplomacyEventManager.EnsureUsableCombatPawnGroupMakerForParms(faction, aidParms, out string groupReason))
            {
                reason = $"NoUsableCombatMaker:{groupReason}";
                return false;
            }

            reason = "OK";
            return true;
        }

internal static bool TryGenerateCallEveryoneAidPawns(
            IncidentParms aidParms,
            out List<Pawn> pawns,
            out string reason)
        {
            pawns = new List<Pawn>();
            if (aidParms == null || aidParms.faction == null)
            {
                reason = "InvalidAidParms";
                return false;
            }

            PawnGroupMakerParms groupParms = DiplomacyEventManager.BuildRaidGroupMakerParms(aidParms, out string groupReason);
            if (groupParms == null)
            {
                reason = $"BuildGroupParmsFailed:{groupReason}";
                return false;
            }

            try
            {
                pawns = PawnGroupMakerUtility.GeneratePawns(groupParms, warnOnZeroResults: false)
                    .Where(p => p != null)
                    .ToList();
            }
            catch (Exception ex)
            {
                reason = $"GeneratePawnsException:{ex.Message}";
                return false;
            }

            if (pawns.Count == 0)
            {
                reason = "GeneratedPawnCountZero";
                return false;
            }

            reason = "OK";
            return true;
        }

internal static bool TryArriveCallEveryoneAidPawns(
            Map map,
            IncidentParms aidParms,
            List<Pawn> pawns,
            out string reason)
        {
            if (map == null || aidParms == null || aidParms.faction == null || pawns == null || pawns.Count == 0)
            {
                reason = "InvalidArrivalInput";
                return false;
            }

            if (!DiplomacyEventManager.TryFindCallEveryoneAidEntryCell(map, out IntVec3 entryCell, out string entryReason))
            {
                reason = $"NoValidEntryCell:{entryReason}";
                return false;
            }

            int attempted = 0;
            int spawnFailed = 0;
            List<Pawn> spawned = new List<Pawn>();
            try
            {
                foreach (Pawn pawn in pawns)
                {
                    if (pawn == null || pawn.Dead || pawn.Destroyed)
                    {
                        continue;
                    }

                    attempted++;
                    if (pawn.Spawned)
                    {
                        if (pawn.Map == map)
                        {
                            spawned.Add(pawn);
                        }

                        continue;
                    }

                    if (!DiplomacyEventManager.TrySpawnAidPawnNearEntry(map, entryCell, pawn))
                    {
                        spawnFailed++;
                        continue;
                    }

                    if (pawn.Spawned && pawn.Map == map && !pawn.Dead && !pawn.Destroyed)
                    {
                        spawned.Add(pawn);
                    }
                }

                if (spawned.Count == 0)
                {
                    reason = $"NoPawnSpawnedAfterManualSpawn;entry={entryCell};attempted={attempted};spawnFailed={spawnFailed}";
                    return false;
                }

                IntVec3 rallyCell = spawned[0].Position;
                var assistJob = new LordJob_AssistColony(Faction.OfPlayer, rallyCell);
                LordMaker.MakeNewLord(aidParms.faction, assistJob, map, spawned);
            }
            catch (Exception ex)
            {
                reason = $"ManualSpawnException:{ex.Message}";
                return false;
            }

            reason = "OK";
            return true;
        }

internal static bool TryFindCallEveryoneAidEntryCell(Map map, out IntVec3 entryCell, out string reason)
        {
            entryCell = IntVec3.Invalid;
            reason = "NoCandidate";

            if (map == null)
            {
                reason = "MapNull";
                return false;
            }

            bool foundEdge = CellFinder.TryFindRandomEdgeCellWith(
                c => c.InBounds(map) && c.Standable(map) && c.Walkable(map),
                map,
                0f,
                out entryCell);
            if (foundEdge && entryCell.IsValid && entryCell.InBounds(map))
            {
                reason = "OK";
                return true;
            }

            IntVec3 fallback = DropCellFinder.TradeDropSpot(map);
            if (fallback.IsValid && fallback.InBounds(map) && fallback.Standable(map))
            {
                entryCell = fallback;
                reason = "TradeDropSpotFallback";
                return true;
            }

            reason = "EdgeAndFallbackInvalid";
            return false;
        }

internal static bool TrySpawnAidPawnNearEntry(Map map, IntVec3 entryCell, Pawn pawn)
        {
            if (map == null || pawn == null || !entryCell.IsValid || !entryCell.InBounds(map))
            {
                return false;
            }

            bool foundCell = CellFinder.TryFindRandomSpawnCellForPawnNear(
                entryCell,
                map,
                out IntVec3 spawnCell,
                12,
                c => c.InBounds(map) && c.Standable(map) && c.Walkable(map) && !c.Fogged(map));
            if (!foundCell)
            {
                spawnCell = entryCell;
            }

            if (!spawnCell.IsValid || !spawnCell.InBounds(map) || !spawnCell.Standable(map) || !spawnCell.Walkable(map))
            {
                return false;
            }

            try
            {
                GenSpawn.Spawn(pawn, spawnCell, map, WipeMode.Vanish);
                return pawn.Spawned && pawn.Map == map;
            }
            catch
            {
                return false;
            }
        }

public static bool ScheduleRaidWaves(Faction faction, int waves)
        {
            try
            {
                if (faction == null)
                {
                    return false;
                }
                
                int currentTick = Find.TickManager.TicksGame;
                int accumulatedDelay = 0;
                
                for (int i = 0; i < waves; i++)
                {
                    // 每波间隔 12-20 小时
                    int intervalTicks = Rand.Range(12 * 2500, 20 * 2500);
                    accumulatedDelay += intervalTicks;
                    
                    int executeTick = currentTick + accumulatedDelay;
                    
                    var evt = new DelayedDiplomacyEvent(DelayedEventType.RaidWave, faction, executeTick)
                    {
                        RaidPoints = -1,
                        RaidStrategy = null,
                        ArrivalMode = null,
                        WaveIndex = i,
                        TotalWaves = waves
                    };
                    
                    GameComponent_DiplomacyManager.Instance?.AddDelayedEvent(evt);
                }

                int firstWaveMinHours = 12;
                int firstWaveMaxHours = 20;
                int finalWaveMinHours = waves * 12;
                int finalWaveMaxHours = waves * 20;
                string detail = $"{waves}|{firstWaveMinHours}|{firstWaveMaxHours}|{finalWaveMinHours}|{finalWaveMaxHours}";
                DiplomacyNotificationManager.SendDelayedEventScheduledNotification(
                    faction,
                    DelayedEventType.RaidWave,
                    detail,
                    0f);
                
                float firstWaveHours = 12f;
                float lastWaveHours = accumulatedDelay / 2500f;
                
                DebugLogger.Debug($"Scheduled raid_waves from {faction.Name}: {waves} waves, " +
                           $"first wave in ~{firstWaveHours:F0}h, last wave in ~{lastWaveHours:F0}h, " +
                           $"end message will be sent after final wave departure");
                
                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Error scheduling raid waves: {ex}");
                return false;
            }
        }
    }


}
