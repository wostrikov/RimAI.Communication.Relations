using System;
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Relation;
using Ustas.RimAI.Communication.Relations.WorldState;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Communication.Relations.DiplomacySystem
{
    internal sealed class ApiActionEligibilitySlice3 : ApiActionEligibilityServiceCollaborator
    {
        internal ApiActionEligibilitySlice3(ApiActionEligibilityService owner) : base(owner)
        {
        }

internal static ActionValidationResult ValidateRaidCallEveryoneAvailability(
            Faction faction,
            Dictionary<string, object> parameters,
            bool checkCooldown)
        {
            if (faction == null)
            {
                return ActionValidationResult.Denied("invalid_faction", "Faction cannot be null");
            }

            if (checkCooldown && !GameAIInterface.Instance.IsRaidCallEveryoneAvailable())
            {
                int remainingSeconds = GameAIInterface.Instance.GetRaidCallEveryoneRemainingCooldownSeconds();
                float remainingDays = remainingSeconds / 86400f;
                return ActionValidationResult.Denied(
                    "call_everyone_cooldown",
                    $"request_raid_call_everyone is on global cooldown. Remaining: {remainingDays:F1} days",
                    remainingSeconds);
            }

            if (!ApiActionEligibilityService.HasRecentRaidIntentForFaction(faction, 7) && !ApiActionEligibilityService.HasExplicitChallengeRequest(parameters))
            {
                return ActionValidationResult.Denied(
                    "call_everyone_requires_post_raid_escalation",
                    "request_raid_call_everyone is normally unavailable. It should only trigger when provocation continues after a raid, or when the player explicitly requests a challenge.");
            }

            var allFactions = Find.FactionManager.AllFactions
                .Where(f => !f.IsPlayer && !f.defeated && !f.def.hidden)
                .ToList();
            if (allFactions.Count == 0)
            {
                return ActionValidationResult.Denied("no_factions", "No factions available to call.");
            }

            return ActionValidationResult.AllowedResult();
        }

internal static bool HasExplicitChallengeRequest(Dictionary<string, object> parameters)
        {
            return ApiActionEligibilityService.TryReadBoolParameter(parameters, ExplicitChallengeRequestParameterKey, out bool explicitRequest) &&
                   explicitRequest;
        }

internal static bool HasRecentRaidIntentForFaction(Faction faction, int windowDays)
        {
            if (faction == null || windowDays <= 0)
            {
                return false;
            }

            WorldEventLedgerComponent ledger = WorldEventLedgerComponent.Instance;
            if (ledger == null)
            {
                return false;
            }

            string sourcePrefix = $"raid-intent:{faction.GetUniqueLoadID()}:";
            List<WorldEventRecord> records = ledger.GetRecentWorldEvents(
                observerFaction: faction,
                daysWindow: windowDays,
                includePublic: true,
                includeDirect: true);

            return records.Any(record =>
                record != null &&
                string.Equals(record.EventType, "raid_intent", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(record.SourceKey) &&
                record.SourceKey.StartsWith(sourcePrefix, StringComparison.Ordinal));
        }

internal static bool IsAncientQuestTemplateName(string questDefName)
        {
            return !string.IsNullOrEmpty(questDefName) &&
                   questDefName.IndexOf("Ancient", StringComparison.OrdinalIgnoreCase) >= 0;
        }

internal static bool IsFeatureEnabled(string actionType)
        {
            var settings = RelationsMod.Instance?.InstanceSettings;
            if (settings == null) return false;

            switch (actionType)
            {
                case "adjust_goodwill":
                    return settings.EnableAIGoodwillAdjustment;
                case "send_gift":
                    return settings.EnableAIGiftSending;
                case "request_aid":
                    return settings.EnableAIAidRequest;
                case "declare_war":
                    return settings.EnableAIWarDeclaration;
                case "make_peace":
                    return settings.EnableAIPeaceMaking;
                case "request_caravan":
                    return settings.EnableAITradeCaravan;
                case "request_visitor":
                    return settings.EnableAITradeCaravan;
                case "request_raid":
                    return settings.EnableAIRaidRequest;
                case "request_raid_call_everyone":
                case "request_raid_waves":
                    return settings.EnableAIRaidRequest; // 复用 raid 开关
                case "request_item_airdrop":
                    return settings.EnableAIItemAirdrop;
                case "request_info":
                    return settings.EnablePrisonerRansom;
                case "pay_prisoner_ransom":
                    return settings.EnablePrisonerRansom;
                case "create_quest":
                case "trigger_incident":
                case "reject_request":
                    return true;
                case "send_image":
                    return settings.DiplomacyImageApi != null && settings.DiplomacyImageApi.IsConfigured();
                case "publish_public_post":
                    return settings.EnableSocialCircle && settings.EnablePlayerInfluenceNews;
                case "exit_dialogue":
                case "go_offline":
                case "set_dnd":
                    return settings.EnableFactionPresenceStatus;
                default:
                    return false;
            }
        }
    }
}
