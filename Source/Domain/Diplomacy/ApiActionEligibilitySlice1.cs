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
    internal sealed class ApiActionEligibilitySlice1 : ApiActionEligibilityServiceCollaborator
    {
        internal ApiActionEligibilitySlice1(ApiActionEligibilityService owner) : base(owner)
        {
        }

public FactionQuestAvailabilityReport GetFactionQuestAvailabilityReport(Faction faction, Dictionary<string, object> parameters = null)
        {
            if (faction == null)
            {
                var nullReport = new FactionQuestAvailabilityReport
                {
                    Faction = null,
                    EvaluatedQuestDefs = new List<QuestTemplateEligibility>(),
                    ActionValidation = ActionValidationResult.Denied("invalid_faction", "Faction cannot be null")
                };
                return nullReport;
            }

            int factionId = faction.loadID;
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            if (_questReportCache.TryGetValue(factionId, out var cached) && currentTick - cached.tick < QuestReportCacheTtl)
            {
                return cached.report;
            }

            var report = new FactionQuestAvailabilityReport
            {
                Faction = faction,
                EvaluatedQuestDefs = new List<QuestTemplateEligibility>()
            };

            Dictionary<string, object> normalizedParameters = Owner.NormalizeQuestParameters(faction, parameters);
            report.Parameters = normalizedParameters;
            report.ActionValidation = Owner.ValidateCreateQuestActionAvailability(faction, normalizedParameters);
            if (!report.ActionValidation.Allowed)
            {
                _questReportCache[factionId] = (report, currentTick);
                return report;
            }

            foreach (string questDefName in SupportedQuestDefs)
            {
                report.EvaluatedQuestDefs.Add(Owner.EvaluateQuestTemplateAvailability(faction, questDefName, normalizedParameters));
            }

            _questReportCache[factionId] = (report, currentTick);
            return report;
        }

public Dictionary<string, ActionValidationResult> GetAllowedActions(Faction faction, bool lightweight = false)
        {
            var result = new Dictionary<string, ActionValidationResult>(StringComparer.OrdinalIgnoreCase);
            foreach (string action in SupportedActions)
            {
                result[action] = Owner.ValidateActionExecution(faction, action, null, lightweight);
            }
            return result;
        }

public ActionValidationResult ValidateActionExecution(Faction faction, string actionType, Dictionary<string, object> parameters, bool lightweight = false)
        {
            if (faction == null)
            {
                return ActionValidationResult.Denied("invalid_faction", "Faction cannot be null");
            }

            if (!ApiActionEligibilityService.IsFeatureEnabled(actionType))
            {
                return ActionValidationResult.Denied("feature_disabled", $"Feature {actionType} is disabled in settings");
            }

            switch (actionType)
            {
                case "request_aid":
                    if (faction.RelationKindWith(Faction.OfPlayer) != FactionRelationKind.Ally)
                    {
                        return ActionValidationResult.Denied("aid_not_ally", "Can only request aid from allied factions");
                    }
                    {
                        int minGoodwill = RelationsMod.Instance?.InstanceSettings?.MinGoodwillForAid ?? 0;
                        if (faction.PlayerGoodwill < minGoodwill)
                        {
                            return ActionValidationResult.Denied("aid_goodwill_too_low", $"Need at least {minGoodwill} goodwill to request aid");
                        }
                    }
                    return ApiActionEligibilityService.ValidateCooldown(faction, "RequestAid", "aid_cooldown");

                case "request_caravan":
                    if (faction.RelationKindWith(Faction.OfPlayer) == FactionRelationKind.Hostile)
                    {
                        return ActionValidationResult.Denied("caravan_hostile", "Cannot request caravan from hostile faction");
                    }
                    return ApiActionEligibilityService.ValidateCooldown(faction, "RequestTradeCaravan", "caravan_cooldown");

                case "request_visitor":
                    if (faction.RelationKindWith(Faction.OfPlayer) == FactionRelationKind.Hostile)
                    {
                        return ActionValidationResult.Denied("visitor_hostile", "Cannot request visitor from hostile faction");
                    }
                    return ApiActionEligibilityService.ValidateCooldown(faction, "RequestVisitor", "visitor_cooldown");

                case "request_raid":
                    if (faction.RelationKindWith(Faction.OfPlayer) != FactionRelationKind.Hostile)
                    {
                        return ActionValidationResult.Denied("raid_not_hostile", "AI can only launch raids if the faction is hostile to the player");
                    }
                    return ApiActionEligibilityService.ValidateCooldown(faction, "RequestRaid", "raid_cooldown");

                case "request_raid_call_everyone":
                    return ApiActionEligibilityService.ValidateRaidCallEveryoneAvailability(faction, parameters, checkCooldown: true);

                case "request_raid_waves":
                    if (faction.RelationKindWith(Faction.OfPlayer) != FactionRelationKind.Hostile)
                    {
                        return ActionValidationResult.Denied("raid_not_hostile", "AI can only launch raids if the faction is hostile to the player");
                    }
                    {
                        ActionValidationResult waveCooldown = ApiActionEligibilityService.ValidateCooldown(faction, "RequestRaidWaves", "raid_waves_cooldown");
                        if (!waveCooldown.Allowed)
                        {
                            return waveCooldown;
                        }
                    }

                    if (ApiActionEligibilityService.ValidateRaidCallEveryoneAvailability(faction, parameters, checkCooldown: true).Allowed)
                    {
                        return ActionValidationResult.Denied(
                            "raid_waves_requires_call_everyone_unavailable",
                            "request_raid_waves is normally unavailable. It should only trigger when request_raid_call_everyone is unavailable, or when the player explicitly requests a challenge.");
                    }

                    return ActionValidationResult.AllowedResult();

                case "declare_war":
                    if (faction.RelationKindWith(Faction.OfPlayer) == FactionRelationKind.Hostile)
                    {
                        return ActionValidationResult.Denied("already_hostile", "Already at war with this faction");
                    }
                    {
                        int maxGoodwill = RelationsMod.Instance?.InstanceSettings?.MaxGoodwillForWarDeclaration ?? 0;
                        if (faction.PlayerGoodwill > maxGoodwill)
                        {
                            return ActionValidationResult.Denied("war_goodwill_too_high", $"Cannot declare war with goodwill above {maxGoodwill}");
                        }
                    }
                    return ApiActionEligibilityService.ValidateCooldown(faction, "DeclareWar", "war_cooldown");

                case "make_peace":
                    if (faction.RelationKindWith(Faction.OfPlayer) != FactionRelationKind.Hostile)
                    {
                        return ActionValidationResult.Denied("not_at_war", "Not at war with this faction");
                    }
                    {
                        ActionValidationResult peacePolicy = ApiActionEligibilityService.ValidateMakePeaceGoodwillPolicy(faction);
                        if (!peacePolicy.Allowed)
                        {
                            return peacePolicy;
                        }
                    }
                    return ApiActionEligibilityService.ValidateCooldown(faction, "MakePeace", "peace_cooldown");

                case "adjust_goodwill":
                    return ApiActionEligibilityService.ValidateCooldown(faction, "AdjustGoodwill", "goodwill_cooldown");

                case "send_gift":
                    return ApiActionEligibilityService.ValidateCooldown(faction, "SendGift", "gift_cooldown");

                case "create_quest":
                    {
                        ActionValidationResult questAvailability = Owner.ValidateCreateQuestActionAvailability(faction, parameters);
                        if (!questAvailability.Allowed)
                        {
                            return questAvailability;
                        }

                        if (lightweight)
                        {
                            // Skip expensive QuestGenerationProbe (world scan + distance sort) on tooltip hints; execution still fully validates.
                            return ActionValidationResult.AllowedResult();
                        }

                        var available = Owner.GetFactionQuestAvailabilityReport(faction, parameters).AllowedQuestDefs;
                        if (available.Count == 0)
                        {
                            return ActionValidationResult.Denied("no_eligible_quests", $"No eligible quest templates are available for faction '{faction.Name}'.");
                        }

                        return ActionValidationResult.AllowedResult();
                    }

                case "trigger_incident":
                case "reject_request":
                case "publish_public_post":
                case "exit_dialogue":
                case "go_offline":
                case "set_dnd":
                    return ActionValidationResult.AllowedResult();

                case "request_item_airdrop":
                    {
                        ActionValidationResult cooldownResult = ApiActionEligibilityService.ValidateCooldown(faction, "RequestItemAirdrop", "airdrop_cooldown");
                        if (!cooldownResult.Allowed) return cooldownResult;

                        if (parameters == null)
                        {
                            return ActionValidationResult.AllowedResult();
                        }

                        string need = ApiActionEligibilityService.TryReadStringParameter(parameters, "need");
                        if (string.IsNullOrWhiteSpace(need))
                        {
                            return ActionValidationResult.Denied("airdrop_need_required", "request_item_airdrop requires parameter 'need'.");
                        }

                        if (!ApiActionEligibilityService.TryReadPaymentItemsArray(parameters, out _))
                        {
                            return ActionValidationResult.Denied("airdrop_payment_items_required", "request_item_airdrop requires non-empty array parameter 'payment_items'.");
                        }

                        string scenario = (ApiActionEligibilityService.TryReadStringParameter(parameters, "scenario") ?? string.Empty).Trim().ToLowerInvariant();
                        if (!string.IsNullOrWhiteSpace(scenario) &&
                            scenario != "general" &&
                            scenario != "trade" &&
                            scenario != "ransom")
                        {
                            return ActionValidationResult.Denied("airdrop_scenario_invalid", "scenario must be one of: general, trade, ransom.");
                        }

                        return ActionValidationResult.AllowedResult();
                    }

                case "request_info":
                    {
                        // Allow action-hint stage without runtime parameters.
                        if (parameters == null)
                        {
                            return ActionValidationResult.AllowedResult();
                        }

                        string infoType = (ApiActionEligibilityService.TryReadStringParameter(parameters, "info_type") ?? string.Empty).Trim().ToLowerInvariant();
                        if (!string.Equals(infoType, "prisoner", StringComparison.Ordinal))
                        {
                            return ActionValidationResult.Denied("request_info_type_invalid", "RimChat_RequestInfoInvalidTypeSystem".Translate().ToString());
                        }

                        return ActionValidationResult.AllowedResult();
                    }

                case "pay_prisoner_ransom":
                    {
                        // Allow action-hint stage without runtime parameters.
                        if (parameters == null)
                        {
                            return ActionValidationResult.AllowedResult();
                        }

                        if (!ApiActionEligibilityService.TryReadPositiveIntParameter(parameters, "target_pawn_load_id", out int targetPawnLoadId))
                        {
                            return ActionValidationResult.Denied("ransom_target_required", "pay_prisoner_ransom requires positive int parameter 'target_pawn_load_id'.");
                        }

                        if (!ApiActionEligibilityService.TryReadPositiveIntParameter(parameters, "offer_silver", out _))
                        {
                            return ActionValidationResult.Denied("ransom_offer_required", "pay_prisoner_ransom requires positive int parameter 'offer_silver'.");
                        }

                        string paymentMode = (ApiActionEligibilityService.TryReadStringParameter(parameters, "payment_mode") ?? string.Empty).Trim().ToLowerInvariant();
                        if (!string.IsNullOrWhiteSpace(paymentMode) && !string.Equals(paymentMode, "silver", StringComparison.Ordinal))
                        {
                            return ActionValidationResult.Denied("ransom_invalid_mode", "pay_prisoner_ransom currently supports payment_mode=silver only.");
                        }

                        if (!PrisonerRansomService.TryResolvePawnByLoadId(targetPawnLoadId, out Pawn targetPawn))
                        {
                            return ActionValidationResult.Denied("ransom_target_not_found", $"Target pawn not found: {targetPawnLoadId}.");
                        }

                        if (!PrisonerRansomService.IsRansomEligibleTarget(targetPawn, faction, out string reasonCode))
                        {
                            return ActionValidationResult.Denied("ransom_target_not_eligible", $"Target pawn is not eligible for ransom: {reasonCode}.");
                        }

                        return ActionValidationResult.AllowedResult();
                    }

                case "send_image":
                    {
                        return ActionValidationResult.Denied("feature_in_development", ImageGenerationAvailability.GetBlockedMessage());
                    }
            }

            return ActionValidationResult.Denied("unknown_action", $"Unknown action type: {actionType}");
        }

internal static string TryReadStringParameter(Dictionary<string, object> parameters, string key)
        {
            if (parameters == null || string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            if (!parameters.TryGetValue(key, out object value) || value == null)
            {
                return string.Empty;
            }

            return value.ToString();
        }

internal static bool TryReadBoolParameter(Dictionary<string, object> parameters, string key, out bool value)
        {
            value = false;
            if (parameters == null || string.IsNullOrWhiteSpace(key) || !parameters.TryGetValue(key, out object raw) || raw == null)
            {
                return false;
            }

            if (raw is bool boolValue)
            {
                value = boolValue;
                return true;
            }

            if (raw is string textValue && bool.TryParse(textValue, out bool parsed))
            {
                value = parsed;
                return true;
            }

            return false;
        }

internal static bool TryReadPositiveIntParameter(Dictionary<string, object> parameters, string key, out int value)
        {
            value = 0;
            if (parameters == null || string.IsNullOrWhiteSpace(key) || !parameters.TryGetValue(key, out object raw) || raw == null)
            {
                return false;
            }

            if (raw is int intValue)
            {
                value = intValue;
                return value > 0;
            }

            if (raw is long longValue && longValue <= int.MaxValue && longValue >= int.MinValue)
            {
                value = (int)longValue;
                return value > 0;
            }

            if (!int.TryParse(raw.ToString(), out int parsed))
            {
                return false;
            }

            value = parsed;
            return value > 0;
        }

internal static bool TryReadPaymentItemsArray(Dictionary<string, object> parameters, out IEnumerable<object> items)
        {
            items = null;
            if (parameters == null || !parameters.TryGetValue("payment_items", out object raw) || raw == null)
            {
                return false;
            }

            if (!(raw is IEnumerable<object> enumerable))
            {
                return false;
            }

            List<object> normalized = enumerable.Where(item => item != null).ToList();
            if (normalized.Count == 0)
            {
                return false;
            }

            items = normalized;
            return true;
        }

public QuestValidationResult ValidateCreateQuest(Faction faction, string questDefName, Dictionary<string, object> parameters)
        {
            if (faction == null)
            {
                return QuestValidationResult.Denied("invalid_faction", "Faction cannot be null");
            }

            FactionQuestAvailabilityReport report = Owner.GetFactionQuestAvailabilityReport(faction, parameters);
            if (!report.ActionValidation.Allowed)
            {
                return QuestValidationResult.Denied(report.ActionValidation.Code, report.ActionValidation.Message, report.ActionValidation.RemainingSeconds);
            }

            if (string.IsNullOrWhiteSpace(questDefName))
            {
                return QuestValidationResult.Denied("quest_def_required", "create_quest requires a valid questDefName from the injected allowed list.");
            }

            if (string.Equals(questDefName, BestowingCeremonyQuestDefName, StringComparison.Ordinal))
            {
                Log.Warning(
                    $"[RimAI.Relations][QuestGuard] blocked create_quest for disabled template. " +
                    $"faction='{faction?.Name ?? "Unknown"}', questDefName='{questDefName}', code='bestowing_disabled'.");
                return QuestValidationResult.Denied(
                    "bestowing_disabled",
                    $"Quest template '{BestowingCeremonyQuestDefName}' is disabled by policy to prevent empty bestowing ceremony corruption.");
            }

            QuestTemplateEligibility eligibility = report.Find(questDefName);
            if (eligibility == null)
            {
                return QuestValidationResult.Denied("quest_template_unsupported", $"Quest template '{questDefName}' is not supported by the current integration.");
            }

            if (!eligibility.Allowed)
            {
                return QuestValidationResult.Denied(eligibility.Code, eligibility.Message);
            }

            return QuestValidationResult.AllowedResult(questDefName);
        }

public bool IsOrbitalTraderDialogueContext(Faction faction, Dictionary<string, object> parameters = null)
        {
            if (faction == null)
            {
                return false;
            }

            if (ApiActionEligibilityService.TryReadBoolParameter(parameters, OrbitalTraderContextParameterKey, out bool explicitFlag))
            {
                return explicitFlag;
            }

            string dialogueSource = (ApiActionEligibilityService.TryReadStringParameter(parameters, DialogueSourceParameterKey) ?? string.Empty).Trim();
            if (string.Equals(dialogueSource, OrbitalTraderDialogueSource, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            Map map = Find.CurrentMap ?? Find.AnyPlayerHomeMap;
            if (map?.passingShipManager?.passingShips == null)
            {
                return false;
            }

            return map.passingShipManager.passingShips
                .OfType<TradeShip>()
                .Any(ship => ship?.Faction == faction);
        }
    }
}
