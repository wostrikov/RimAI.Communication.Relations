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
    internal sealed class ApiActionEligibilitySlice2 : ApiActionEligibilityServiceCollaborator
    {
        internal ApiActionEligibilitySlice2(ApiActionEligibilityService owner) : base(owner)
        {
        }

internal Dictionary<string, object> NormalizeQuestParameters(Faction faction, Dictionary<string, object> parameters)
        {
            var normalized = parameters != null
                ? new Dictionary<string, object>(parameters, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            if (faction != null)
            {
                normalized["faction"] = faction;
                normalized["askerFaction"] = faction;
            }

            if (!normalized.ContainsKey(DialogueSourceParameterKey) && Owner.IsOrbitalTraderDialogueContext(faction, normalized))
            {
                normalized[DialogueSourceParameterKey] = OrbitalTraderDialogueSource;
            }

            return normalized;
        }

internal ActionValidationResult ValidateCreateQuestActionAvailability(Faction faction, Dictionary<string, object> parameters)
        {
            ActionValidationResult cooldown = ApiActionEligibilityService.ValidateCooldown(faction, "CreateQuest", "quest_cooldown");
            if (!cooldown.Allowed)
            {
                return cooldown;
            }

            string questDefName = ApiActionEligibilityService.TryReadStringParameter(parameters, "questDefName");
            ActionValidationResult peaceTalkOnly = ApiActionEligibilityService.ValidatePeaceTalkOnlyQuestPolicy(faction, questDefName);
            if (!peaceTalkOnly.Allowed)
            {
                return peaceTalkOnly;
            }

            if (!string.IsNullOrWhiteSpace(questDefName) &&
                (ApiActionEligibilityService.IsMerchantTradeRequestBlocked(faction, questDefName) ||
                 Owner.IsOrbitalTraderSettlementQuestBlocked(faction, questDefName, parameters)))
            {
                return ActionValidationResult.Denied(
                    "merchant_trade_request_disabled",
                    "TradeRequest is disabled for merchant factions and orbital trader dialogue contexts. Use request_item_airdrop for item exchange instead.");
            }

            return ActionValidationResult.AllowedResult();
        }

internal QuestTemplateEligibility EvaluateQuestTemplateAvailability(Faction faction, string questDefName, Dictionary<string, object> parameters)
        {
            if (!Owner.TryValidateQuestTemplateForFaction(faction, questDefName, parameters, out string code, out string message))
            {
                return new QuestTemplateEligibility
                {
                    QuestDefName = questDefName,
                    Allowed = false,
                    Code = code,
                    Message = message,
                    Stage = QuestEligibilityStage.RuleValidation
                };
            }

            if (!Owner.TryProbeQuestGeneration(faction, questDefName, parameters, out string probeCode, out string probeMessage))
            {
                return new QuestTemplateEligibility
                {
                    QuestDefName = questDefName,
                    Allowed = false,
                    Code = probeCode,
                    Message = probeMessage,
                    Stage = QuestEligibilityStage.GenerationProbe
                };
            }

            return new QuestTemplateEligibility
            {
                QuestDefName = questDefName,
                Allowed = true,
                Code = "allowed",
                Message = "Allowed",
                Stage = QuestEligibilityStage.GenerationProbe
            };
        }

internal bool TryProbeQuestGeneration(Faction faction, string questDefName, Dictionary<string, object> parameters, out string code, out string message)
        {
            code = "allowed";
            message = "Allowed";

            QuestScriptDef questDef = DefDatabase<QuestScriptDef>.GetNamedSilentFail(questDefName);
            if (questDef == null)
            {
                code = "quest_template_missing";
                message = $"Quest template '{questDefName}' is missing.";
                return false;
            }

            Dictionary<string, object> probeParameters = Owner.NormalizeQuestParameters(faction, parameters);
            if (!QuestGenerationProbe.TryValidate(faction, questDef, probeParameters, out string probeCode, out string probeMessage))
            {
                code = probeCode;
                message = probeMessage;
                return false;
            }

            return true;
        }

internal bool TryValidateQuestTemplateForFaction(
            Faction faction,
            string questDefName,
            Dictionary<string, object> parameters,
            out string code,
            out string message)
        {
            code = "allowed";
            message = "Allowed";

            if (faction == null)
            {
                code = "invalid_faction";
                message = "Faction cannot be null";
                return false;
            }

            if (DefDatabase<QuestScriptDef>.GetNamedSilentFail(questDefName) == null)
            {
                code = "quest_template_missing";
                message = $"Quest template '{questDefName}' is missing.";
                return false;
            }

            if (faction.def != null && faction.def.permanentEnemy)
            {
                code = "permanent_enemy_blocked";
                message = $"Faction '{faction.Name}' is permanently hostile and cannot issue diplomacy quests.";
                return false;
            }

            if (ApiActionEligibilityService.IsAncientQuestTemplateName(questDefName) || HighRiskQuestTemplates.Contains(questDefName))
            {
                code = "quest_template_high_risk_disabled";
                message = $"Quest '{questDefName}' is disabled by safety policy due to technical risk.";
                return false;
            }

            switch (questDefName)
            {
                case "TradeRequest":
                    if (ApiActionEligibilityService.IsMerchantTradeRequestBlocked(faction, questDefName) ||
                        Owner.IsOrbitalTraderSettlementQuestBlocked(faction, questDefName, parameters))
                    {
                        code = "merchant_trade_request_disabled";
                        message = "TradeRequest is disabled for merchant factions and orbital trader dialogue contexts. Use request_item_airdrop for item exchange instead.";
                        return false;
                    }
                    if (faction.RelationKindWith(Faction.OfPlayer) == FactionRelationKind.Hostile)
                    {
                        code = "trade_hostile";
                        message = $"Quest '{questDefName}' requires a non-hostile faction.";
                        return false;
                    }
                    if (!ApiActionEligibilityService.HasSettlement(faction))
                    {
                        code = "trade_no_settlement";
                        message = $"Quest '{questDefName}' requires at least one settlement for faction '{faction.Name}'.";
                        return false;
                    }
                    break;

                case "OpportunitySite_PeaceTalks":
                    if (faction.RelationKindWith(Faction.OfPlayer) != FactionRelationKind.Hostile)
                    {
                        code = "peace_not_hostile";
                        message = $"Quest '{questDefName}' requires the faction to be hostile to the player.";
                        return false;
                    }
                    if (!ApiActionEligibilityService.HasFactionLeader(faction))
                    {
                        code = "peace_no_leader";
                        message = $"Quest '{questDefName}' requires a valid faction leader.";
                        return false;
                    }
                    break;

                case "AncientComplex_Mission":
                    if (!ModsConfig.IdeologyActive)
                    {
                        code = "ideology_required";
                        message = $"Quest '{questDefName}' requires Ideology DLC.";
                        return false;
                    }
                    if (!ApiActionEligibilityService.HasFactionLeader(faction))
                    {
                        code = "ancient_no_leader";
                        message = $"Quest '{questDefName}' requires a valid faction leader.";
                        return false;
                    }
                    break;

                case "Mission_BanditCamp":
                    if (!ModsConfig.RoyaltyActive)
                    {
                        code = "royalty_required";
                        message = $"Quest '{questDefName}' requires Royalty DLC.";
                        return false;
                    }
                    if (!BanditCampAllowedFactionDefs.Contains(faction.def?.defName ?? string.Empty))
                    {
                        code = "banditcamp_faction_not_supported";
                        message = $"Quest '{questDefName}' only supports faction defs: Empire, OutlanderCivil, OutlanderRough.";
                        return false;
                    }
                    break;

                case "PawnLend":
                    if (!ModsConfig.RoyaltyActive)
                    {
                        code = "royalty_required";
                        message = $"Quest '{questDefName}' requires Royalty DLC.";
                        return false;
                    }
                    if (faction.def == null || faction.def.techLevel < TechLevel.Industrial)
                    {
                        code = "pawnlend_tech_too_low";
                        message = $"Quest '{questDefName}' requires Industrial+ faction tech level.";
                        return false;
                    }
                    Map pawnLendMap = Find.CurrentMap ?? Find.AnyPlayerHomeMap;
                    if (pawnLendMap == null)
                    {
                        code = "pawnlend_player_map_missing";
                        message = $"Quest '{questDefName}' requires an active player map.";
                        return false;
                    }
                    if ((pawnLendMap.mapPawns?.FreeColonistsSpawnedCount ?? 0) <= 0)
                    {
                        code = "pawnlend_no_free_colonist";
                        message = $"Quest '{questDefName}' requires at least one free colonist on the active map.";
                        return false;
                    }
                    if (!ApiActionEligibilityService.HasFactionLeader(faction))
                    {
                        code = "pawnlend_no_leader";
                        message = $"Quest '{questDefName}' requires a valid faction leader or settlement-backed issuer.";
                        return false;
                    }
                    break;

                case "ThreatReward_Raid_MiscReward":
                case "Hospitality_Refugee":
                case "BestowingCeremony":
                    if (!ModsConfig.RoyaltyActive)
                    {
                        code = "royalty_required";
                        message = $"Quest '{questDefName}' requires Royalty DLC.";
                        return false;
                    }
                    if (!string.Equals(faction.def?.defName, "Empire", StringComparison.Ordinal))
                    {
                        code = "empire_only";
                        message = $"Quest '{questDefName}' is restricted to Empire faction in this integration.";
                        return false;
                    }
                    break;
            }

            if (ApiActionEligibilityService.IsInPeaceTalkOnlyRange(faction) &&
                !string.Equals(questDefName, PeaceTalkQuestDefName, StringComparison.Ordinal))
            {
                code = "peace_talk_only_range";
                message = $"Current goodwill {faction.PlayerGoodwill} is in [{PeaceTalkOnlyMinGoodwill},{MakePeaceReenabledMinGoodwill - 1}]. Only quest '{PeaceTalkQuestDefName}' is allowed.";
                return false;
            }

            return true;
        }

internal bool IsOrbitalTraderSettlementQuestBlocked(Faction faction, string questDefName, Dictionary<string, object> parameters)
        {
            return string.Equals(questDefName, "TradeRequest", StringComparison.Ordinal) &&
                   Owner.IsOrbitalTraderDialogueContext(faction, parameters);
        }

internal static bool IsMerchantTradeRequestBlocked(Faction faction, string questDefName)
        {
            return string.Equals(questDefName, "TradeRequest", StringComparison.Ordinal) &&
                   MerchantFactionDefs.Contains(faction?.def?.defName ?? string.Empty);
        }

internal static ActionValidationResult ValidateMakePeaceGoodwillPolicy(Faction faction)
        {
            if (faction == null)
            {
                return ActionValidationResult.Denied("invalid_faction", "Faction cannot be null");
            }

            int goodwill = faction.PlayerGoodwill;
            if (goodwill < PeaceTalkOnlyMinGoodwill)
            {
                return ActionValidationResult.Denied(
                    "peace_goodwill_too_low",
                    $"Direct peace is blocked because goodwill is {goodwill} (< {PeaceTalkOnlyMinGoodwill}). Hostility is too deep for an immediate treaty.");
            }

            if (goodwill < MakePeaceReenabledMinGoodwill)
            {
                return ActionValidationResult.Denied(
                    "peace_talk_required",
                    $"Direct peace is blocked because goodwill is {goodwill} in [{PeaceTalkOnlyMinGoodwill},{MakePeaceReenabledMinGoodwill - 1}]. Use create_quest with questDefName '{PeaceTalkQuestDefName}' for peace talks.");
            }

            return ActionValidationResult.AllowedResult();
        }

internal static ActionValidationResult ValidatePeaceTalkOnlyQuestPolicy(Faction faction, string questDefName)
        {
            if (faction == null || !ApiActionEligibilityService.IsInPeaceTalkOnlyRange(faction))
            {
                return ActionValidationResult.AllowedResult();
            }

            if (string.IsNullOrWhiteSpace(questDefName) ||
                string.Equals(questDefName, PeaceTalkQuestDefName, StringComparison.Ordinal))
            {
                return ActionValidationResult.AllowedResult();
            }

            return ActionValidationResult.Denied(
                "peace_talk_only_range",
                $"Current goodwill {faction.PlayerGoodwill} is in [{PeaceTalkOnlyMinGoodwill},{MakePeaceReenabledMinGoodwill - 1}]. Only quest '{PeaceTalkQuestDefName}' is allowed.");
        }

internal static bool IsInPeaceTalkOnlyRange(Faction faction)
        {
            if (faction == null)
            {
                return false;
            }

            int goodwill = faction.PlayerGoodwill;
            return goodwill >= PeaceTalkOnlyMinGoodwill && goodwill < MakePeaceReenabledMinGoodwill;
        }

internal static bool ShouldBypassProjectedGoodwillFloorForQuest(Faction faction, string questDefName)
        {
            if (faction == null || faction.RelationKindWith(Faction.OfPlayer) != FactionRelationKind.Hostile)
            {
                return false;
            }

            if (string.Equals(questDefName, PeaceTalkQuestDefName, StringComparison.Ordinal))
            {
                return true;
            }

            return string.IsNullOrWhiteSpace(questDefName) && ApiActionEligibilityService.IsInPeaceTalkOnlyRange(faction);
        }

internal static bool IsEnabledImageTemplate(string templateId)
        {
            if (string.IsNullOrWhiteSpace(templateId))
            {
                return false;
            }

            var settings = RelationsMod.Instance?.InstanceSettings;
            PromptUnifiedTemplateAliasConfig alias = settings?.ResolvePromptTemplateAlias(
                Ustas.RimAI.Communication.Relations.Config.RimTalkPromptEntryChannelCatalog.ImageGeneration,
                templateId);
            return alias?.Enabled == true;
        }

internal static string GetDefaultEnabledImageTemplateId()
        {
            var settings = RelationsMod.Instance?.InstanceSettings;
            if (settings == null)
            {
                return string.Empty;
            }

            PromptUnifiedTemplateAliasConfig alias = settings.ResolvePreferredPromptTemplateAlias(
                Ustas.RimAI.Communication.Relations.Config.RimTalkPromptEntryChannelCatalog.ImageGeneration,
                Ustas.RimAI.Communication.Relations.Config.DiplomacyImageTemplateDefaults.DefaultTemplateId);
            return alias?.TemplateId ?? string.Empty;
        }

internal static string ResolveExistingImageTemplateId(string requestedTemplateId)
        {
            if (string.IsNullOrWhiteSpace(requestedTemplateId))
            {
                return string.Empty;
            }

            var settings = RelationsMod.Instance?.InstanceSettings;
            PromptUnifiedTemplateAliasConfig alias = settings?.ResolvePromptTemplateAlias(
                Ustas.RimAI.Communication.Relations.Config.RimTalkPromptEntryChannelCatalog.ImageGeneration,
                requestedTemplateId);
            return alias?.TemplateId ?? string.Empty;
        }

internal static ActionValidationResult ValidateCooldown(Faction faction, string methodName, string code)
        {
            int remaining = GameAIInterface.Instance.GetRemainingCooldownSeconds(faction, methodName);
            if (remaining > 0)
            {
                return ActionValidationResult.Denied(code, $"{methodName} is on cooldown for {faction.Name}. Remaining: {remaining} seconds", remaining);
            }
            return ActionValidationResult.AllowedResult();
        }
    }
}
