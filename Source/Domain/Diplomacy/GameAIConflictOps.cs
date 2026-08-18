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
    /// <summary>War and peace APIs for GameAIInterface.</summary>
    internal sealed class GameAIConflictOps : GameAIInterfaceCollaborator
    {
        internal GameAIConflictOps(GameAIInterface owner) : base(owner)
        {
        }

public APIResult DeclareWar(Faction faction, string reason = "")
        {
            if (RelationsMod.Instance == null)
                return APIResult.FailureResult("Settings not initialized");
            var settings = RelationsMod.Instance.InstanceSettings;
            if (settings == null)
                return APIResult.FailureResult("Settings not initialized");

            if (faction == null)
                return APIResult.FailureResult("Faction cannot be null");

            // 检查faction独立冷却
            int remainingCooldown = Owner.Parts.CooldownOps.GetRemainingCooldownSeconds(faction, "DeclareWar");
            if (remainingCooldown > 0)
                return APIResult.FailureResult($"Method DeclareWar is on cooldown for {faction.Name}. Remaining: {remainingCooldown} seconds");

            // 检查whether已经是敌对relation
            if (faction.RelationKindWith(Faction.OfPlayer) == FactionRelationKind.Hostile)
                return APIResult.FailureResult("Already at war with this faction");

            // 检查goodwillwhether允许宣战
            if (faction.PlayerGoodwill > settings.MaxGoodwillForWarDeclaration)
                return APIResult.FailureResult($"Cannot declare war with goodwill above {settings.MaxGoodwillForWarDeclaration}");

            // Use goodwill-first relation settlement to avoid SetRelationDirect errors on goodwill-driven factions.
            if (!Owner.Parts.GoodwillOps.TryApplyRelationTargetGoodwill(
                faction,
                GameAIInterface.DeclareWarTargetGoodwill,
                FactionRelationKind.Hostile,
                out int appliedGoodwill,
                out string relationError))
            {
                return APIResult.FailureResult(
                    $"Failed to declare war with {faction.Name}: {relationError}");
            }

            Owner.Parts.CooldownOps.RecordAPICall(
                "DeclareWar",
                true,
                $"faction={faction.Name}, reason={reason}, targetGoodwill={GameAIInterface.DeclareWarTargetGoodwill}, appliedGoodwill={appliedGoodwill}");
            Owner.Parts.CooldownOps.SetCooldown(faction, "DeclareWar");

            // 发送通知
            Find.LetterStack.ReceiveLetter(
                "RimChat_DeclareWarLetterTitle".Translate(),
                "RimChat_DeclareWarLetterBody".Translate(faction.Name, reason ?? string.Empty),
                LetterDefOf.ThreatBig
            );

            return APIResult.SuccessResult(
                $"War declared with {faction.Name}",
                new { Faction = faction.Name, Reason = reason }
            );
        }

public APIResult MakePeace(Faction faction, int peaceCost = 0)
        {
            if (RelationsMod.Instance == null)
                return APIResult.FailureResult("Settings not initialized");
            var settings = RelationsMod.Instance.InstanceSettings;
            if (settings == null)
                return APIResult.FailureResult("Settings not initialized");

            if (faction == null)
                return APIResult.FailureResult("Faction cannot be null");

            // 检查faction独立冷却
            int remainingCooldown = Owner.Parts.CooldownOps.GetRemainingCooldownSeconds(faction, "MakePeace");
            if (remainingCooldown > 0)
                return APIResult.FailureResult($"Method MakePeace is on cooldown for {faction.Name}. Remaining: {remainingCooldown} seconds");

            // 检查whether处于敌对state
            if (faction.RelationKindWith(Faction.OfPlayer) != FactionRelationKind.Hostile)
                return APIResult.FailureResult("Not at war with this faction");

            // 检查和平代价上限
            if (peaceCost > settings.MaxPeaceCost)
                return APIResult.FailureResult($"Peace cost {peaceCost} exceeds maximum {settings.MaxPeaceCost}");

            // Use goodwill-first relation settlement to avoid SetRelationDirect errors on goodwill-driven factions.
            if (!Owner.Parts.GoodwillOps.TryApplyRelationTargetGoodwill(
                faction,
                GameAIInterface.MakePeaceTargetGoodwill,
                FactionRelationKind.Neutral,
                out int appliedGoodwill,
                out string relationError))
            {
                return APIResult.FailureResult(
                    $"Failed to make peace with {faction.Name}: {relationError}");
            }

            Owner.Parts.CooldownOps.RecordAPICall(
                "MakePeace",
                true,
                $"faction={faction.Name}, cost={peaceCost}, targetGoodwill={GameAIInterface.MakePeaceTargetGoodwill}, appliedGoodwill={appliedGoodwill}");
            Owner.Parts.CooldownOps.SetCooldown(faction, "MakePeace");

            // 发送通知
            Find.LetterStack.ReceiveLetter(
                "RimChat_MakePeaceLetterTitle".Translate(),
                "RimChat_MakePeaceLetterBody".Translate(faction.Name),
                LetterDefOf.PositiveEvent
            );

            return APIResult.SuccessResult(
                $"Peace made with {faction.Name}",
                new { Faction = faction.Name, Cost = peaceCost }
            );
        }

public APIResult PrepareMakePeacePayment(Faction faction, int peaceCost, Pawn playerNegotiator)
        {
            if (RelationsMod.Instance?.InstanceSettings == null)
            {
                return APIResult.FailureResult("Settings not initialized");
            }

            if (faction == null)
            {
                return APIResult.FailureResult("Faction cannot be null");
            }

            if (playerNegotiator?.Map == null)
            {
                return GameAIAirdropPayment.BuildPaymentFailure("player_negotiator_required", "Preparing peace payment requires a valid player negotiator on a map.");
            }

            if (faction.RelationKindWith(Faction.OfPlayer) != FactionRelationKind.Hostile)
            {
                return APIResult.FailureResult("Not at war with this faction");
            }

            int remainingCooldown = Owner.Parts.CooldownOps.GetRemainingCooldownSeconds(faction, "MakePeace");
            if (remainingCooldown > 0)
            {
                return APIResult.FailureResult($"Method MakePeace is on cooldown for {faction.Name}. Remaining: {remainingCooldown} seconds");
            }

            int normalizedPeaceCost = Math.Max(0, peaceCost);
            if (normalizedPeaceCost <= 0)
            {
                return GameAIAirdropPayment.BuildPaymentFailure("peace_cost_invalid", "Peace cost must be greater than 0 to prepare a paid peace treaty.");
            }

            if (normalizedPeaceCost > RelationsMod.Instance.InstanceSettings.MaxPeaceCost)
            {
                return APIResult.FailureResult($"Peace cost {normalizedPeaceCost} exceeds maximum {RelationsMod.Instance.InstanceSettings.MaxPeaceCost}");
            }

            var requestedLines = new List<ItemAirdropPaymentRequestLine>
            {
                new ItemAirdropPaymentRequestLine
                {
                    ItemText = ThingDefOf.Silver.defName,
                    Count = normalizedPeaceCost
                }
            };

            APIResult paymentPlanResult = Owner.Parts.AirdropPayment.BuildPaymentPlanFromRequestedLines(
                requestedLines,
                playerNegotiator.Map,
                faction,
                playerNegotiator,
                out List<ItemAirdropPreparedPaymentLine> paymentLines,
                out List<ItemAirdropDeductionPlanLine> deductionPlan,
                out int derivedBudgetSilver,
                out int paymentTotalSilver);
            if (!paymentPlanResult.Success)
            {
                return paymentPlanResult;
            }

            var preparedData = new PreparedMakePeacePaymentData
            {
                FactionName = faction.Name,
                FactionDefName = faction.def?.defName ?? string.Empty,
                PeaceCostSilver = normalizedPeaceCost,
                PaymentTotalSilver = paymentTotalSilver,
                MapUniqueId = playerNegotiator.Map.uniqueID,
                PaymentLines = paymentLines,
                DeductionPlan = deductionPlan,
                ParametersSnapshot = new Dictionary<string, object>
                {
                    ["cost"] = normalizedPeaceCost,
                    ["derived_budget_silver"] = derivedBudgetSilver
                }
            };

            return APIResult.SuccessResult("Peace payment prepared.", preparedData);
        }

public APIResult CommitPreparedMakePeace(Faction faction, PreparedMakePeacePaymentData preparedData)
        {
            if (faction == null)
            {
                return APIResult.FailureResult("Faction cannot be null.");
            }

            if (preparedData == null)
            {
                return APIResult.FailureResult("Missing prepared peace payment payload.");
            }

            Map map = Find.Maps?.FirstOrDefault(m => m != null && m.uniqueID == preparedData.MapUniqueId);
            if (map == null)
            {
                return GameAIAirdropPayment.BuildPaymentFailure("map_unavailable", "Prepared peace payment map is no longer available.");
            }

            APIResult validation = Owner.Parts.AirdropPayment.ValidateDeductionPlan(map, preparedData.DeductionPlan, out List<ThingDeductionReservation> reservations);
            if (!validation.Success)
            {
                return validation;
            }

            GameAIAirdropPayment.ApplyDeductionReservations(reservations);
            APIResult peaceResult = MakePeace(faction, preparedData.PeaceCostSilver);
            if (!peaceResult.Success)
            {
                return peaceResult;
            }

            Owner.Parts.CooldownOps.RecordAPICall(
                "CommitPreparedMakePeace",
                true,
                $"faction={faction.Name}, cost={preparedData.PeaceCostSilver}, payment={preparedData.PaymentTotalSilver}");

            return APIResult.SuccessResult(
                $"Peace payment committed with {faction.Name}",
                new
                {
                    Faction = faction.Name,
                    Cost = preparedData.PeaceCostSilver,
                    PaymentTotalSilver = preparedData.PaymentTotalSilver
                });
        }

    }
}
