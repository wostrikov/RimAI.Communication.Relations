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
using APIResult = Ustas.RimAI.Communication.Relations.DiplomacySystem.GameAIInterface.APIResult;
using APICallRecord = Ustas.RimAI.Communication.Relations.DiplomacySystem.GameAIInterface.APICallRecord;
using DialogueApiGoodwillCostResult = Ustas.RimAI.Communication.Relations.DiplomacySystem.GameAIInterface.DialogueApiGoodwillCostResult;
using FactionCooldownEntry = Ustas.RimAI.Communication.Relations.DiplomacySystem.GameAIInterface.FactionCooldownEntry;
using RaidWaveState = Ustas.RimAI.Communication.Relations.DiplomacySystem.GameAIInterface.RaidWaveState;

namespace Ustas.RimAI.Communication.Relations.DiplomacySystem
{
    /// <summary>Aid, raid, caravan, visitor, and incident APIs.</summary>
    internal sealed class GameAIIncidentOps : GameAIInterfaceCollaborator
    {
        internal GameAIIncidentOps(GameAIInterface owner) : base(owner)
        {
        }

public APIResult RequestAid(Faction faction, string aidType, bool delayed = true)
        {
            if (RelationsMod.Instance == null)
                return APIResult.FailureResult("Settings not initialized");
            var settings = RelationsMod.Instance.InstanceSettings;
            if (settings == null)
                return APIResult.FailureResult("Settings not initialized");

            if (faction == null)
                return APIResult.FailureResult("Faction cannot be null");

            int remainingCooldown = Owner.Parts.CooldownOps.GetRemainingCooldownSeconds(faction, "RequestAid");
            if (remainingCooldown > 0)
                return APIResult.FailureResult($"Method RequestAid is on cooldown for {faction.Name}. Remaining: {remainingCooldown} seconds");

            if (faction.RelationKindWith(Faction.OfPlayer) != FactionRelationKind.Ally)
                return APIResult.FailureResult("Can only request aid from allied factions");

            if (faction.PlayerGoodwill < settings.MinGoodwillForAid)
                return APIResult.FailureResult($"Need at least {settings.MinGoodwillForAid} goodwill to request aid");

            AidType type = DiplomacyEventManager.ParseAidType(aidType);

            Owner.Parts.CooldownOps.RecordAPICall("RequestAid", true, $"faction={faction.Name}, aidType={type}, delayed={delayed}");
            Owner.Parts.CooldownOps.SetCooldown(faction, "RequestAid");

            bool eventSuccess;
            string resultMessage;

            if (delayed)
            {
                eventSuccess = DiplomacyEventManager.ScheduleDelayedAid(faction, type);
                int delayTicks = DiplomacyEventManager.CalculateDelayTicks(faction, true);
                float delayDays = delayTicks / 60000f;
                resultMessage = $"Aid scheduled from {faction.Name} for {DiplomacyEventManager.GetAidTypeLabel(type)}. Arrival in {delayDays:F1} days.";
            }
            else
            {
                eventSuccess = DiplomacyEventManager.TriggerAidEvent(faction, type);
                resultMessage = $"Aid request sent to {faction.Name} for {DiplomacyEventManager.GetAidTypeLabel(type)}";
            }

            return APIResult.SuccessResult(
                resultMessage,
                new { AidType = type.ToString(), Faction = faction.Name, EventSuccess = eventSuccess, Delayed = delayed }
            );
        }

public APIResult RequestRaid(Faction faction, string strategyDefName = "", string arrivalModeDefName = "", bool delayed = true)
        {
            if (RelationsMod.Instance == null)
                return APIResult.FailureResult("Settings not initialized");
            var settings = RelationsMod.Instance.InstanceSettings;
            if (settings == null)
                return APIResult.FailureResult("Settings not initialized");

            if (faction == null)
                return APIResult.FailureResult("Faction cannot be null");

            RaidDefNameNormalizer.NormalizeRaidRequestParameters(
                strategyDefName,
                arrivalModeDefName,
                out string normalizedStrategyDefName,
                out string normalizedArrivalModeDefName);
            strategyDefName = normalizedStrategyDefName;
            arrivalModeDefName = normalizedArrivalModeDefName;

            int remainingCooldown = Owner.Parts.CooldownOps.GetRemainingCooldownSeconds(faction, "RequestRaid");
            if (remainingCooldown > 0)
                return APIResult.FailureResult($"Method RequestRaid is on cooldown for {faction.Name}. Remaining: {remainingCooldown} seconds");

            if (!DiplomacyEventManager.TryValidateRaidFaction(faction, out string raidFactionValidationReason))
                return APIResult.FailureResult(raidFactionValidationReason);

            // Resolve Defs
            RaidStrategyDef strategy = null;
            if (!string.IsNullOrEmpty(strategyDefName))
            {
                strategy = DefDatabase<RaidStrategyDef>.GetNamedSilentFail(strategyDefName);
                if (strategy == null) return APIResult.FailureResult($"Invalid RaidStrategyDef: {strategyDefName}");
            }

            PawnsArrivalModeDef arrivalMode = null;
            if (!string.IsNullOrEmpty(arrivalModeDefName))
            {
                arrivalMode = DefDatabase<PawnsArrivalModeDef>.GetNamedSilentFail(arrivalModeDefName);
                if (arrivalMode == null) return APIResult.FailureResult($"Invalid PawnsArrivalModeDef: {arrivalModeDefName}");
            }

            // Points is now handled by system (-1)
            float points = -1;

            // Logic
            bool success;
            string resultMessage;
            if (delayed)
            {
                success = DiplomacyEventManager.ScheduleDelayedRaid(faction, points, strategy, arrivalMode);
                int delayTicks = DiplomacyEventManager.CalculateRaidDelayTicks(strategy, arrivalMode);
                float delayHours = delayTicks / 2500f;
                resultMessage = $"Raid scheduled from {faction.Name}. Arrival in {delayHours:F1} hours.";
            }
            else
            {
                success = DiplomacyEventManager.TriggerRaidEvent(faction, points, strategy, arrivalMode);
                resultMessage = $"Raid triggered from {faction.Name}";
            }

            if (success)
            {
                Owner.Parts.CooldownOps.SetCooldown(faction, "RequestRaid");
                Owner.Parts.CooldownOps.RecordAPICall("RequestRaid", true, $"faction={faction.Name}, strategy={strategyDefName}, arrival={arrivalModeDefName}");
                WorldEventLedgerComponent.Instance?.RecordRaidIntent(faction, delayed, strategy?.defName ?? strategyDefName, arrivalMode?.defName ?? arrivalModeDefName);
                
                return APIResult.SuccessResult(resultMessage, new { Delayed = delayed });
            }
            else
            {
                return APIResult.FailureResult("Failed to trigger raid");
            }
        }

public APIResult RequestTradeCaravan(Faction faction, string caravanType = "General", bool delayed = true)
        {
            if (RelationsMod.Instance == null)
                return APIResult.FailureResult("Settings not initialized");
            var settings = RelationsMod.Instance.InstanceSettings;
            if (settings == null)
                return APIResult.FailureResult("Settings not initialized");

            if (faction == null)
                return APIResult.FailureResult("Faction cannot be null");

            int remainingCooldown = Owner.Parts.CooldownOps.GetRemainingCooldownSeconds(faction, "RequestTradeCaravan");
            if (remainingCooldown > 0)
                return APIResult.FailureResult($"Method RequestTradeCaravan is on cooldown for {faction.Name}. Remaining: {remainingCooldown} seconds");

            if (faction.RelationKindWith(Faction.OfPlayer) == FactionRelationKind.Hostile)
                return APIResult.FailureResult("Cannot request caravan from hostile faction");

            CaravanType type = DiplomacyEventManager.ParseCaravanType(caravanType);

            Owner.Parts.CooldownOps.RecordAPICall("RequestTradeCaravan", true, $"faction={faction.Name}, caravanType={type}, delayed={delayed}");

            bool eventSuccess;
            string resultMessage;

            if (delayed)
            {
                eventSuccess = DiplomacyEventManager.ScheduleDelayedCaravan(faction, type);
                int delayTicks = DiplomacyEventManager.CalculateDelayTicks(faction, false);
                float delayDays = delayTicks / 60000f;
                resultMessage = $"Trade caravan scheduled from {faction.Name}: {DiplomacyEventManager.GetCaravanTypeLabel(type)}. Arrival in {delayDays:F1} days.";
            }
            else
            {
                eventSuccess = DiplomacyEventManager.TriggerCaravanEvent(faction, type);
                resultMessage = $"Trade caravan requested from {faction.Name}: {DiplomacyEventManager.GetCaravanTypeLabel(type)}";
            }

            if (eventSuccess)
            {
                Owner.Parts.CooldownOps.SetCooldown(faction, "RequestTradeCaravan");
                Owner.Parts.CooldownOps.RecordSuccessfulCaravanFaction(faction);
            }

            return APIResult.SuccessResult(
                resultMessage,
                new { Faction = faction.Name, CaravanType = type.ToString(), EventSuccess = eventSuccess, Delayed = delayed }
            );
        }

public APIResult RequestVisitor(Faction faction, bool delayed = true)
        {
            if (RelationsMod.Instance == null)
                return APIResult.FailureResult("Settings not initialized");
            var settings = RelationsMod.Instance.InstanceSettings;
            if (settings == null)
                return APIResult.FailureResult("Settings not initialized");

            if (faction == null)
                return APIResult.FailureResult("Faction cannot be null");

            int remainingCooldown = Owner.Parts.CooldownOps.GetRemainingCooldownSeconds(faction, "RequestVisitor");
            if (remainingCooldown > 0)
                return APIResult.FailureResult($"Method RequestVisitor is on cooldown for {faction.Name}. Remaining: {remainingCooldown} seconds");

            if (faction.RelationKindWith(Faction.OfPlayer) == FactionRelationKind.Hostile)
                return APIResult.FailureResult("Cannot request visitor from hostile faction");

            Owner.Parts.CooldownOps.RecordAPICall("RequestVisitor", true, $"faction={faction.Name}, delayed={delayed}");

            bool eventSuccess;
            string resultMessage;
            if (delayed)
            {
                eventSuccess = DiplomacyEventManager.ScheduleDelayedVisitor(faction);
                int delayTicks = DiplomacyEventManager.CalculateDelayTicks(faction, false);
                float delayDays = delayTicks / 60000f;
                resultMessage = $"Visitor group scheduled from {faction.Name}. Arrival in {delayDays:F1} days.";
            }
            else
            {
                eventSuccess = DiplomacyEventManager.TriggerVisitorEvent(faction);
                resultMessage = $"Visitor group requested from {faction.Name}.";
            }

            if (eventSuccess)
            {
                Owner.Parts.CooldownOps.SetCooldown(faction, "RequestVisitor");
            }

            return APIResult.SuccessResult(
                resultMessage,
                new { Faction = faction.Name, EventSuccess = eventSuccess, Delayed = delayed });
        }

public APIResult ApplySuccessfulDialogueApiGoodwillCost(
            Faction faction,
            DialogueGoodwillCost.DialogueActionType actionType,
            string sourceAction = "",
            string detail = "")
        {
            EnsureInitialized();
            if (faction == null) return APIResult.FailureResult("Faction cannot be null");

            int baseCost = DialogueGoodwillCost.GetBaseValue(actionType);
            int oldGoodwill = faction.PlayerGoodwill;
            faction.TryAffectGoodwillWith(Faction.OfPlayer, baseCost, false, true, null);
            int newGoodwill = faction.PlayerGoodwill;
            int actualChange = newGoodwill - oldGoodwill;
            int currentDayAdjustment = _goodwillAdjustmentsToday.ContainsKey(faction) ? _goodwillAdjustmentsToday[faction] : 0;
            _goodwillAdjustmentsToday[faction] = currentDayAdjustment + actualChange;
            Owner.Parts.DialogueActionOps.RecordDialogueAction(faction, actionType, actualChange);
            Owner.Parts.CooldownOps.RecordAPICall(
                "ApplySuccessfulDialogueApiGoodwillCost",
                true,
                $"faction={faction.Name}, sourceAction={sourceAction}, actionType={actionType}, baseCost={baseCost}, actualChange={actualChange}, detail={detail}");

            return APIResult.SuccessResult(
                $"Fixed goodwill cost applied: {actualChange}.",
                new DialogueApiGoodwillCostResult
                {
                    SourceAction = sourceAction ?? string.Empty,
                    Detail = detail ?? string.Empty,
                    ActionType = actionType,
                    BaseCost = baseCost,
                    ActualChange = actualChange,
                    OldGoodwill = oldGoodwill,
                    NewGoodwill = newGoodwill
                });
        }

public APIResult GetFactionInfo(Faction faction)
        {
            if (faction == null)
                return APIResult.FailureResult("Faction cannot be null");

            var settlements = Find.WorldObjects.SettlementBases
                .Where(s => s.Faction == faction)
                .Count();

            var info = new
            {
                Name = faction.Name,
                DefName = faction.def?.defName ?? "Unknown",
                Goodwill = faction.PlayerGoodwill,
                RelationKind = faction.RelationKindWith(Faction.OfPlayer).ToString(),
                IsPlayer = faction.IsPlayer,
                IsDefeated = faction.defeated,
                IsHidden = faction.def?.hidden ?? false,
                LeaderName = faction.leader?.Name?.ToStringFull ?? "None",
                SettlementCount = settlements,
                TodayAdjustment = Owner.Parts.GoodwillOps.GetTodayGoodwillAdjustment(faction)
            };

            Owner.Parts.CooldownOps.RecordAPICall("GetFactionInfo", true, $"faction={faction.Name}");

            return APIResult.SuccessResult($"Faction info retrieved for {faction.Name}", info);
        }

public APIResult GetAllFactions()
        {
            if (Current.Game == null || Find.FactionManager == null)
                return APIResult.FailureResult("Game not initialized");

            var factions = Find.FactionManager.AllFactions
                .Where(f => !f.IsPlayer && !f.defeated)
                .Select(f => new
                {
                    Name = f.Name,
                    Goodwill = f.PlayerGoodwill,
                    RelationKind = f.RelationKindWith(Faction.OfPlayer).ToString(),
                    IsAIControlled = GameComponent_DiplomacyManager.Instance?.IsAIControlled(f) ?? false
                })
                .ToList();

            Owner.Parts.CooldownOps.RecordAPICall("GetAllFactions", true, $"count={factions.Count}");

            return APIResult.SuccessResult($"Retrieved {factions.Count} factions", factions);
        }

public APIResult GetColonyStatus()
        {
            if (Current.Game == null)
                return APIResult.FailureResult("Game not initialized");

            var playerFaction = Faction.OfPlayer;
            var maps = Find.Maps.Where(m => m.IsPlayerHome).ToList();

            var status = new
            {
                ColonyName = playerFaction.Name,
                MapCount = maps.Count,
                TotalColonists = maps.Sum(m => m.mapPawns.FreeColonists.Count()),
                TotalWealth = maps.Sum(m => m.wealthWatcher.WealthTotal),
                GameDate = BuildGameDateText(),
                ThreatLevel = maps.Any() ? StorytellerUtility.DefaultThreatPointsNow(Find.AnyPlayerHomeMap) : 0
            };

            Owner.Parts.CooldownOps.RecordAPICall("GetColonyStatus", true, "");

            return APIResult.SuccessResult("Colony status retrieved", status);
        }

internal static string BuildGameDateText()
        {
            int absTicks = Find.TickManager?.TicksAbs ?? 0;
            int tile = Find.AnyPlayerHomeMap?.Tile ?? -1;
            if (!WorldTileGuard.IsValidTile(tile))
            {
                return "Unknown";
            }
            return GenDate.DateFullStringAt(absTicks, Find.WorldGrid.LongLatOf(tile));
        }

public APIResult TriggerIncident(Faction faction, string incidentDefName, float points = -1)
        {
            if (faction == null)
                return APIResult.FailureResult("Faction cannot be null");

            IncidentDef incDef = DefDatabase<IncidentDef>.GetNamedSilentFail(incidentDefName);
            if (incDef == null)
                return APIResult.FailureResult($"Invalid IncidentDef: {incidentDefName}");

            Map map = Find.CurrentMap;
            if (map == null)
                return APIResult.FailureResult("No valid map to trigger incident");

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(incDef.category, map);
            parms.faction = faction;
            if (points > 0) parms.points = points;

            try
            {
                if (incDef.Worker.TryExecute(parms))
                {
                    Owner.Parts.CooldownOps.RecordAPICall("TriggerIncident", true, $"faction={faction.Name}, incident={incidentDefName}, points={points}");
                    WorldEventLedgerComponent.Instance?.RecordIncidentIntent(faction, incidentDefName, map);
                    return APIResult.SuccessResult($"Incident triggered: {incDef.label}");
                }
                else
                {
                    return APIResult.FailureResult($"Incident worker failed to execute: {incidentDefName}");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Error triggering incident {incidentDefName}: {ex}");
                return APIResult.FailureResult($"Execution error: {ex.Message}");
            }
        }

    }
}
