using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RimWorld;
using Verse;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Relation;

namespace Ustas.RimAI.Communication.Relations.AI
{
    internal sealed class AIActionExecutorSlice2 : AIActionExecutorCollaborator
    {
        internal AIActionExecutorSlice2(AIActionExecutor owner) : base(owner)
        {
        }

internal ActionResult ExecuteRequestAid(AIAction action)
        {
            string aidType = AIActionExecutor.ReadStringParameterOrDefault(action.Parameters, "type", "Military");

            if (faction.RelationKindWith(Faction.OfPlayer) != FactionRelationKind.Ally)
            {
                return ActionResult.Failure("Can only request aid from allied factions");
            }

            if (RelationsMod.Instance == null) return ActionResult.Failure("Mod not initialized");
            var settings = RelationsMod.Instance.InstanceSettings;
            if (faction.PlayerGoodwill < settings?.MinGoodwillForAid)
            {
                return ActionResult.Failure($"Need at least {settings.MinGoodwillForAid} goodwill to request aid");
            }

            int cooldownSeconds = gameInterface.GetRemainingCooldownSeconds(faction, "RequestAid");
            if (cooldownSeconds > 0)
            {
                return ActionResult.Failure($"RequestAid is on cooldown for {faction.Name}. Remaining: {cooldownSeconds} seconds");
            }

            var result = gameInterface.RequestAid(faction, aidType, delayed: true);

            if (result.Success)
            {
                return ActionResult.Success(result.Message, result.Data);
            }
            else
            {
                return ActionResult.Failure(result.Message);
            }
        }

internal ActionResult ExecuteDeclareWar(AIAction action)
        {
            string reason = AIActionExecutor.ReadStringParameterOrDefault(action.Parameters, "reason", "Diplomatic conflict");

            if (faction.RelationKindWith(Faction.OfPlayer) == FactionRelationKind.Hostile)
            {
                return ActionResult.Failure("Already at war with this faction");
            }

            if (RelationsMod.Instance == null) return ActionResult.Failure("Mod not initialized");
            var settings = RelationsMod.Instance.InstanceSettings;
            if (faction.PlayerGoodwill > settings?.MaxGoodwillForWarDeclaration)
            {
                return ActionResult.Failure($"Cannot declare war with goodwill above {settings.MaxGoodwillForWarDeclaration}");
            }

            int cooldownSeconds = gameInterface.GetRemainingCooldownSeconds(faction, "DeclareWar");
            if (cooldownSeconds > 0)
            {
                return ActionResult.Failure($"DeclareWar is on cooldown for {faction.Name}. Remaining: {cooldownSeconds} seconds");
            }

            var result = gameInterface.DeclareWar(faction, reason);

            if (result.Success)
            {
                DiplomacySystem.DiplomacyNotificationManager.SendAIActionNotification(faction, DiplomacySystem.AIActionType.DeclareWar, reason);
                return ActionResult.Success(result.Message, result.Data);
            }
            else
            {
                return ActionResult.Failure(result.Message);
            }
        }

internal ActionResult ExecuteMakePeace(AIAction action)
        {
            int peaceCost = AIActionExecutor.ReadIntParameterOrDefault(action.Parameters, "cost", 0);

            if (faction.RelationKindWith(Faction.OfPlayer) != FactionRelationKind.Hostile)
            {
                return ActionResult.Failure("Not at war with this faction");
            }

            int cooldownSeconds = gameInterface.GetRemainingCooldownSeconds(faction, "MakePeace");
            if (cooldownSeconds > 0)
            {
                return ActionResult.Failure($"MakePeace is on cooldown for {faction.Name}. Remaining: {cooldownSeconds} seconds");
            }

            if (peaceCost <= 0)
            {
                return ActionResult.Failure("make_peace requires a positive cost and player confirmation in diplomacy dialogue.");
            }

            return ActionResult.Failure("make_peace must be handled by diplomacy dialogue confirmation pipeline.");
        }

internal ActionResult ExecuteRequestCaravan(AIAction action)
        {
            string caravanType = AIActionExecutor.ReadStringParameterOrDefault(action.Parameters, "type", string.Empty);
            if (string.IsNullOrWhiteSpace(caravanType))
            {
                caravanType = AIActionExecutor.ReadStringParameterOrDefault(action.Parameters, "goods", "General");
            }

            if (faction.RelationKindWith(Faction.OfPlayer) == FactionRelationKind.Hostile)
            {
                return ActionResult.Failure("Cannot request caravan from hostile faction");
            }

            int cooldownSeconds = gameInterface.GetRemainingCooldownSeconds(faction, "RequestTradeCaravan");
            if (cooldownSeconds > 0)
            {
                return ActionResult.Failure($"RequestTradeCaravan is on cooldown for {faction.Name}. Remaining: {cooldownSeconds} seconds");
            }

            var result = gameInterface.RequestTradeCaravan(faction, caravanType, delayed: true);

            if (result.Success)
            {
                return ActionResult.Success(result.Message, result.Data);
            }
            else
            {
                return ActionResult.Failure(result.Message);
            }
        }

internal ActionResult ExecuteRequestVisitor(AIAction action)
        {
            if (faction.RelationKindWith(Faction.OfPlayer) == FactionRelationKind.Hostile)
            {
                return ActionResult.Failure("Cannot request visitor from hostile faction");
            }

            int cooldownSeconds = gameInterface.GetRemainingCooldownSeconds(faction, "RequestVisitor");
            if (cooldownSeconds > 0)
            {
                return ActionResult.Failure($"RequestVisitor is on cooldown for {faction.Name}. Remaining: {cooldownSeconds} seconds");
            }

            var result = gameInterface.RequestVisitor(faction, delayed: true);

            if (result.Success)
            {
                return ActionResult.Success(result.Message, result.Data);
            }
            else
            {
                return ActionResult.Failure(result.Message);
            }
        }

internal ActionResult ExecuteRejectRequest(AIAction action)
        {
            string reason = AIActionExecutor.ReadStringParameterOrDefault(
                action.Parameters,
                "reason",
                "I cannot fulfill this request at this time.");

            DiplomacySystem.DiplomacyNotificationManager.SendAIActionNotification(faction, DiplomacySystem.AIActionType.RejectRequest, reason);
            return ActionResult.Success($"Request rejected: {reason}");
        }

internal ActionResult ExecuteRequestRaid(AIAction action)
        {
            if (RelationsMod.Instance == null) return ActionResult.Failure("Mod not initialized");
            var settings = RelationsMod.Instance.InstanceSettings;

            string rawStrategy = AIActionExecutor.ReadStringParameterOrDefault(action.Parameters, "strategy", string.Empty);
            string rawArrival = AIActionExecutor.ReadStringParameterOrDefault(action.Parameters, "arrival", string.Empty);
            RaidDefNameNormalizer.NormalizeRaidRequestParameters(rawStrategy, rawArrival, out string strategy, out string arrival);

            if (!string.IsNullOrEmpty(strategy))
            {
                if (strategy.Equals("ImmediateAttack", StringComparison.OrdinalIgnoreCase) && !settings.EnableRaidStrategy_ImmediateAttack)
                    return ActionResult.Failure("Raid strategy 'ImmediateAttack' is disabled in settings");
                if (strategy.Equals("ImmediateAttackSmart", StringComparison.OrdinalIgnoreCase) && !settings.EnableRaidStrategy_ImmediateAttackSmart)
                    return ActionResult.Failure("Raid strategy 'ImmediateAttackSmart' is disabled in settings");
                if (strategy.Equals("StageThenAttack", StringComparison.OrdinalIgnoreCase) && !settings.EnableRaidStrategy_StageThenAttack)
                    return ActionResult.Failure("Raid strategy 'StageThenAttack' is disabled in settings");
                if (strategy.Equals("ImmediateAttackSappers", StringComparison.OrdinalIgnoreCase) && !settings.EnableRaidStrategy_ImmediateAttackSappers)
                    return ActionResult.Failure("Raid strategy 'ImmediateAttackSappers' is disabled in settings");
                if (strategy.Equals("Siege", StringComparison.OrdinalIgnoreCase) && !settings.EnableRaidStrategy_Siege)
                    return ActionResult.Failure("Raid strategy 'Siege' is disabled in settings");
            }

            if (!string.IsNullOrEmpty(arrival))
            {
                if (arrival.Equals("EdgeWalkIn", StringComparison.OrdinalIgnoreCase) && !settings.EnableRaidArrival_EdgeWalkIn)
                    return ActionResult.Failure("Raid arrival 'EdgeWalkIn' is disabled in settings");
                if (arrival.Equals("EdgeDrop", StringComparison.OrdinalIgnoreCase) && !settings.EnableRaidArrival_EdgeDrop)
                    return ActionResult.Failure("Raid arrival 'EdgeDrop' is disabled in settings");
                if (arrival.Equals("EdgeWalkInGroups", StringComparison.OrdinalIgnoreCase) && !settings.EnableRaidArrival_EdgeWalkInGroups)
                    return ActionResult.Failure("Raid arrival 'EdgeWalkInGroups' is disabled in settings");
                if (arrival.Equals("RandomDrop", StringComparison.OrdinalIgnoreCase) && !settings.EnableRaidArrival_RandomDrop)
                    return ActionResult.Failure("Raid arrival 'RandomDrop' is disabled in settings");
                if (arrival.Equals("CenterDrop", StringComparison.OrdinalIgnoreCase) && !settings.EnableRaidArrival_CenterDrop)
                    return ActionResult.Failure("Raid arrival 'CenterDrop' is disabled in settings");
            }

            // Hard constraint — changing this breaks an invariant. (relation:)
            if (faction.RelationKindWith(Faction.OfPlayer) != FactionRelationKind.Hostile)
            {
                return ActionResult.Failure("AI can only launch raids if the faction is hostile to the player");
            }

            int cooldownSeconds = gameInterface.GetRemainingCooldownSeconds(faction, "RequestRaid");
            if (cooldownSeconds > 0)
            {
                return ActionResult.Failure($"RequestRaid is on cooldown for {faction.Name}. Remaining: {cooldownSeconds} seconds");
            }

            var result = gameInterface.RequestRaid(faction, strategy, arrival, delayed: true);

            if (result.Success)
            {
                return ActionResult.Success(result.Message, result.Data);
            }
            else
            {
                return ActionResult.Failure(result.Message);
            }
        }

internal ActionResult ExecuteRequestRaidCallEveryone(AIAction action)
        {
            int globalCooldown = gameInterface.GetRaidCallEveryoneRemainingCooldownSeconds();
            if (globalCooldown > 0)
            {
                float days = globalCooldown / 86400f;
                return ActionResult.Failure(
                    $"request_raid_call_everyone is on global cooldown. Remaining: {days:F1} days");
            }
            
            var allFactions = Find.FactionManager.AllFactions
                .Where(f => !f.IsPlayer && !f.defeated && !f.def.hidden)
                .ToList();
            
            if (allFactions.Count == 0)
            {
                return ActionResult.Failure("No factions available to call.");
            }
            
            var hostileFactions = allFactions
                .Where(f => f.RelationKindWith(Faction.OfPlayer) == FactionRelationKind.Hostile)
                .Where(f => DiplomacyEventManager.TryValidateRaidFaction(f, out _))
                .ToList();

            var allyFactions = allFactions
                .Where(f => f.RelationKindWith(Faction.OfPlayer) == FactionRelationKind.Ally)
                .ToList();

            var validFactions = hostileFactions.Concat(allyFactions).ToList();
            
            if (validFactions.Count == 0)
            {
                return ActionResult.Failure("No factions available for raids or aid.");
            }

            bool success = DiplomacyEventManager.ScheduleRaidCallEveryone(faction, validFactions);

            if (success)
            {
                gameInterface.SetRaidCallEveryoneCooldown();
                return ActionResult.Success(
                    "Called factions for joint raid: arrivals in 16|30h window; ally participation is limited by hostile faction count and wealth-based caps.",
                    new {
                        HostileCount = hostileFactions.Count,
                        AllyCount = allyFactions.Count,
                        TotalPassedToScheduler = validFactions.Count
                    });
            }
            else
            {
                return ActionResult.Failure("Failed to schedule call everyone.");
            }
        }

internal ActionResult ExecuteRequestRaidWaves(AIAction action)
        {
            if (!AIActionExecutor.TryReadIntParameter(action.Parameters, "waves", out int waves))
            {
                return ActionResult.Failure("request_raid_waves requires parameter waves (int, 2-6).");
            }

            if (waves < 2 || waves > 6)
            {
                return ActionResult.Failure($"request_raid_waves parameter waves out of range: {waves}. Expected 2-6.");
            }
            
            // Hard constraint — changing this breaks an invariant. (faction)
            if (faction.RelationKindWith(Faction.OfPlayer) != FactionRelationKind.Hostile)
            {
                return ActionResult.Failure("AI can only launch raids if the faction is hostile to the player");
            }
            
            int cooldownSeconds = gameInterface.GetRemainingCooldownSeconds(faction, "RequestRaidWaves");
            if (cooldownSeconds > 0)
            {
                float days = cooldownSeconds / 86400f;
                return ActionResult.Failure(
                    $"request_raid_waves is on cooldown for {faction.Name}. Remaining: {days:F1} days");
            }
            
            if (!DiplomacyEventManager.TryValidateRaidFaction(faction, out string reason))
            {
                return ActionResult.Failure(reason);
            }
            
            bool success = DiplomacyEventManager.ScheduleRaidWaves(faction, waves);
            
            if (success)
            {
                gameInterface.SetFactionCooldown(faction, "RequestRaidWaves");
                return ActionResult.Success(
                    $"Scheduled {waves} raid waves from {faction.Name}. Interval: 12-20 hours each.",
                    new { Waves = waves });
            }
            else
            {
                return ActionResult.Failure("Failed to schedule raid waves.");
            }
        }

internal static string ReadStringParameterOrDefault(Dictionary<string, object> parameters, string key, string defaultValue)
        {
            if (parameters == null || !parameters.TryGetValue(key, out object raw) || raw == null)
            {
                return defaultValue;
            }

            string value = raw.ToString()?.Trim();
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
        }

internal static bool TryReadIntParameter(Dictionary<string, object> parameters, string key, out int value)
        {
            value = 0;
            if (parameters == null || !parameters.TryGetValue(key, out object raw) || raw == null)
            {
                return false;
            }

            switch (raw)
            {
                case int intValue:
                    value = intValue;
                    return true;
                case long longValue when longValue <= int.MaxValue && longValue >= int.MinValue:
                    value = (int)longValue;
                    return true;
                case short shortValue:
                    value = shortValue;
                    return true;
                case byte byteValue:
                    value = byteValue;
                    return true;
                case float floatValue when !float.IsNaN(floatValue) && !float.IsInfinity(floatValue):
                    value = (int)Math.Round(floatValue, MidpointRounding.AwayFromZero);
                    return true;
                case double doubleValue when !double.IsNaN(doubleValue) && !double.IsInfinity(doubleValue):
                    value = (int)Math.Round(doubleValue, MidpointRounding.AwayFromZero);
                    return true;
                case decimal decimalValue:
                    value = decimal.ToInt32(decimal.Round(decimalValue, MidpointRounding.AwayFromZero));
                    return true;
            }

            string text = raw.ToString();
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedInvariant))
            {
                value = (int)Math.Round(parsedInvariant, MidpointRounding.AwayFromZero);
                return true;
            }

            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double parsedCurrent))
            {
                value = (int)Math.Round(parsedCurrent, MidpointRounding.AwayFromZero);
                return true;
            }

            return false;
        }

internal static bool TryReadFloatParameter(Dictionary<string, object> parameters, string key, out float value)
        {
            value = 0f;
            if (parameters == null || !parameters.TryGetValue(key, out object raw) || raw == null)
            {
                return false;
            }

            switch (raw)
            {
                case float floatValue when !float.IsNaN(floatValue) && !float.IsInfinity(floatValue):
                    value = floatValue;
                    return true;
                case double doubleValue when !double.IsNaN(doubleValue) && !double.IsInfinity(doubleValue):
                    value = (float)doubleValue;
                    return true;
                case decimal decimalValue:
                    value = (float)decimalValue;
                    return true;
                case int intValue:
                    value = intValue;
                    return true;
                case long longValue:
                    value = longValue;
                    return true;
                case short shortValue:
                    value = shortValue;
                    return true;
                case byte byteValue:
                    value = byteValue;
                    return true;
            }

            string text = raw.ToString();
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            return float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }
    }
}
