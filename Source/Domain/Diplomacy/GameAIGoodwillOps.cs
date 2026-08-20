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
    /// <summary>Goodwill and gift APIs for GameAIInterface.</summary>
    internal sealed class GameAIGoodwillOps : GameAIInterfaceCollaborator
    {
        internal GameAIGoodwillOps(GameAIInterface owner) : base(owner)
        {
        }

public APIResult AdjustGoodwill(Faction faction, int amount, string reason = "")
        {
            if (RelationsMod.Instance == null)
                return APIResult.FailureResult("Settings not initialized");
            var settings = RelationsMod.Instance.InstanceSettings;
            if (settings == null)
                return APIResult.FailureResult("Settings not initialized");

            if (faction == null)
                return APIResult.FailureResult("Faction cannot be null");

            if (faction.IsPlayer)
                return APIResult.FailureResult("Cannot adjust player faction goodwill");

            int remainingCooldown = Owner.Parts.CooldownOps.GetRemainingCooldownSeconds(faction, "AdjustGoodwill");
            if (remainingCooldown > 0)
                return APIResult.FailureResult($"Method AdjustGoodwill is on cooldown for {faction.Name}. Remaining: {remainingCooldown} seconds");

            int maxSingleAdjustment = settings.MaxGoodwillAdjustmentPerCall;
            if (Math.Abs(amount) > maxSingleAdjustment)
            {
                DebugLogger.WarningGated($"AI attempted to adjust goodwill by {amount}, clamped to {maxSingleAdjustment}");
                amount = Math.Sign(amount) * maxSingleAdjustment;
            }

            int currentDayAdjustment = _goodwillAdjustmentsToday.ContainsKey(faction) ? _goodwillAdjustmentsToday[faction] : 0;
            int maxDailyAdjustment = settings.MaxDailyGoodwillAdjustment;

            if (Math.Abs(currentDayAdjustment + amount) > maxDailyAdjustment)
            {
                int allowedAdjustment = maxDailyAdjustment - Math.Abs(currentDayAdjustment);
                allowedAdjustment = Math.Sign(amount) * Math.Max(0, Math.Abs(allowedAdjustment));

                if (allowedAdjustment == 0)
                    return APIResult.FailureResult($"Daily goodwill adjustment limit reached for {faction.Name}. Current: {currentDayAdjustment}, Limit: ±{maxDailyAdjustment}");

                DebugLogger.WarningGated($"AI goodwill adjustment clamped from {amount} to {allowedAdjustment} due to daily limit");
                amount = allowedAdjustment;
            }

            int oldGoodwill = faction.PlayerGoodwill;
            faction.TryAffectGoodwillWith(Faction.OfPlayer, amount, false, true, null);
            int newGoodwill = faction.PlayerGoodwill;
            int actualChange = newGoodwill - oldGoodwill;

            _goodwillAdjustmentsToday[faction] = currentDayAdjustment + actualChange;
            Owner.Parts.CooldownOps.RecordAPICall("AdjustGoodwill", true, $"faction={faction.Name}, amount={actualChange}, reason={reason}");
            Owner.Parts.CooldownOps.SetCooldown(faction, "AdjustGoodwill");

            if (Math.Abs(actualChange) >= 10)
            {
                NotifySignificantGoodwillChange(faction, oldGoodwill, newGoodwill, reason);
            }

            return APIResult.SuccessResult(
                $"Goodwill adjusted from {oldGoodwill} to {newGoodwill} (change: {actualChange})",
                new { OldGoodwill = oldGoodwill, NewGoodwill = newGoodwill, Change = actualChange }
            );
        }

public APIResult GetCurrentGoodwill(Faction faction)
        {
            if (faction == null)
                return APIResult.FailureResult("Faction cannot be null");

            int goodwill = faction.PlayerGoodwill;
            var relationKind = faction.RelationKindWith(Faction.OfPlayer);

            Owner.Parts.CooldownOps.RecordAPICall("GetCurrentGoodwill", true, $"faction={faction.Name}");

            return APIResult.SuccessResult(
                $"Current goodwill with {faction.Name}: {goodwill}",
                new
                {
                    FactionName = faction.Name,
                    Goodwill = goodwill,
                    RelationKind = relationKind.ToString(),
                    IsHostile = relationKind == FactionRelationKind.Hostile,
                    IsAlly = relationKind == FactionRelationKind.Ally
                }
            );
        }

public int GetTodayGoodwillAdjustment(Faction faction)
        {
            if (faction == null) return 0;
            return _goodwillAdjustmentsToday.ContainsKey(faction) ? _goodwillAdjustmentsToday[faction] : 0;
        }

public APIResult SendGift(Faction faction, int silverAmount, int goodwillGain)
        {
            if (RelationsMod.Instance == null)
                return APIResult.FailureResult("Settings not initialized");
            var settings = RelationsMod.Instance.InstanceSettings;
            if (settings == null)
                return APIResult.FailureResult("Settings not initialized");

            if (faction == null)
                return APIResult.FailureResult("Faction cannot be null");

            int remainingCooldown = Owner.Parts.CooldownOps.GetRemainingCooldownSeconds(faction, "SendGift");
            if (remainingCooldown > 0)
                return APIResult.FailureResult($"Method SendGift is on cooldown for {faction.Name}. Remaining: {remainingCooldown} seconds");

            if (silverAmount > settings.MaxGiftSilverAmount)
                return APIResult.FailureResult($"Gift amount {silverAmount} exceeds maximum {settings.MaxGiftSilverAmount}");

            if (goodwillGain > settings.MaxGiftGoodwillGain)
                return APIResult.FailureResult($"Goodwill gain {goodwillGain} exceeds maximum {settings.MaxGiftGoodwillGain}");

            faction.TryAffectGoodwillWith(Faction.OfPlayer, goodwillGain, false, true, null);

            Owner.Parts.CooldownOps.RecordAPICall("SendGift", true, $"faction={faction.Name}, silver={silverAmount}, goodwillGain={goodwillGain}");
            Owner.Parts.CooldownOps.SetCooldown(faction, "SendGift");

            return APIResult.SuccessResult(
                $"Gift of {silverAmount} silver sent to {faction.Name}, gained {goodwillGain} goodwill",
                new { SilverAmount = silverAmount, GoodwillGain = goodwillGain }
            );
        }

public APIResult PrepareSendGiftPayment(Faction faction, int silverAmount, int goodwillGain, Pawn playerNegotiator)
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
                return GameAIAirdropPayment.BuildPaymentFailure("player_negotiator_required", "Preparing send_gift requires a valid player negotiator on a map.");
            }

            int remainingCooldown = Owner.Parts.CooldownOps.GetRemainingCooldownSeconds(faction, "SendGift");
            if (remainingCooldown > 0)
            {
                return APIResult.FailureResult($"Method SendGift is on cooldown for {faction.Name}. Remaining: {remainingCooldown} seconds");
            }

            int normalizedSilverAmount = Math.Max(0, silverAmount);
            int normalizedGoodwillGain = Math.Max(0, goodwillGain);
            if (normalizedSilverAmount <= 0)
            {
                return GameAIAirdropPayment.BuildPaymentFailure("gift_silver_invalid", "send_gift requires silver greater than 0.");
            }

            RelationsSettings settings = RelationsMod.Instance.InstanceSettings;
            if (normalizedSilverAmount > settings.MaxGiftSilverAmount)
            {
                return APIResult.FailureResult($"Gift amount {normalizedSilverAmount} exceeds maximum {settings.MaxGiftSilverAmount}");
            }

            if (normalizedGoodwillGain > settings.MaxGiftGoodwillGain)
            {
                return APIResult.FailureResult($"Goodwill gain {normalizedGoodwillGain} exceeds maximum {settings.MaxGiftGoodwillGain}");
            }

            var requestedLines = new List<ItemAirdropPaymentRequestLine>
            {
                new ItemAirdropPaymentRequestLine
                {
                    ItemText = ThingDefOf.Silver.defName,
                    Count = normalizedSilverAmount
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

            var preparedData = new PreparedSendGiftData
            {
                FactionName = faction.Name,
                FactionDefName = faction.def?.defName ?? string.Empty,
                SilverAmount = normalizedSilverAmount,
                GoodwillGain = normalizedGoodwillGain,
                PaymentTotalSilver = paymentTotalSilver,
                MapUniqueId = playerNegotiator.Map.uniqueID,
                PaymentLines = paymentLines,
                DeductionPlan = deductionPlan,
                ParametersSnapshot = new Dictionary<string, object>
                {
                    ["silver"] = normalizedSilverAmount,
                    ["goodwill_gain"] = normalizedGoodwillGain,
                    ["derived_budget_silver"] = derivedBudgetSilver
                }
            };

            return APIResult.SuccessResult("Gift payment prepared.", preparedData);
        }

public APIResult CommitPreparedSendGift(Faction faction, PreparedSendGiftData preparedData)
        {
            if (faction == null)
            {
                return APIResult.FailureResult("Faction cannot be null.");
            }

            if (preparedData == null)
            {
                return APIResult.FailureResult("Missing prepared send_gift payload.");
            }

            Map map = Find.Maps?.FirstOrDefault(m => m != null && m.uniqueID == preparedData.MapUniqueId);
            if (map == null)
            {
                return GameAIAirdropPayment.BuildPaymentFailure("map_unavailable", "Prepared send_gift map is no longer available.");
            }

            APIResult validation = Owner.Parts.AirdropPayment.ValidateDeductionPlan(map, preparedData.DeductionPlan, out List<ThingDeductionReservation> reservations);
            if (!validation.Success)
            {
                return validation;
            }

            GameAIAirdropPayment.ApplyDeductionReservations(reservations);

            if (!GameAIAirdropDrop.TryFindAirdropCell(map, out IntVec3 dropCell))
            {
                return GameAIAirdropPayment.BuildPaymentFailure("dropcell_not_found", "No legal drop cell found near colony center for send_gift.");
            }

            List<Thing> stacks = GameAIAirdropDrop.BuildStacks(ThingDefOf.Silver, preparedData.SilverAmount, RelationsMod.Instance?.InstanceSettings?.ItemAirdropMaxStacksPerDrop ?? 8);
            if (stacks.Count == 0)
            {
                return GameAIAirdropPayment.BuildPaymentFailure("stack_build_failed", "Could not create silver stacks for send_gift.");
            }

            DropPodUtility.DropThingsNear(
                dropCell,
                map,
                stacks,
                110,
                canInstaDropDuringInit: false,
                leaveSlag: false,
                canRoofPunch: false);

            APIResult giftResult = SendGift(faction, preparedData.SilverAmount, preparedData.GoodwillGain);
            if (!giftResult.Success)
            {
                return giftResult;
            }

            Owner.Parts.CooldownOps.RecordAPICall(
                "CommitPreparedSendGift",
                true,
                $"faction={faction.Name}, silver={preparedData.SilverAmount}, goodwillGain={preparedData.GoodwillGain}, payment={preparedData.PaymentTotalSilver}, drop={dropCell}");

            return APIResult.SuccessResult(
                $"Gift payment committed with {faction.Name}",
                new
                {
                    Faction = faction.Name,
                    SilverAmount = preparedData.SilverAmount,
                    GoodwillGain = preparedData.GoodwillGain,
                    PaymentTotalSilver = preparedData.PaymentTotalSilver,
                    DropCell = dropCell.ToString()
                });
        }

internal bool TryApplyRelationTargetGoodwill(
            Faction faction,
            int targetGoodwill,
            FactionRelationKind expectedRelation,
            out int appliedGoodwill,
            out string failureReason)
        {
            appliedGoodwill = faction?.PlayerGoodwill ?? 0;
            failureReason = string.Empty;
            Faction player = Faction.OfPlayer;
            if (faction == null || player == null)
            {
                failureReason = "Faction or player faction is unavailable.";
                return false;
            }

            int currentGoodwill = faction.PlayerGoodwill;
            int goodwillDelta = targetGoodwill - currentGoodwill;
            bool goodwillApplied = goodwillDelta == 0 ||
                                   faction.TryAffectGoodwillWith(player, goodwillDelta, false, true, null);
            appliedGoodwill = faction.PlayerGoodwill;
            if (goodwillApplied &&
                appliedGoodwill == targetGoodwill &&
                faction.RelationKindWith(player) == expectedRelation)
            {
                return true;
            }

            if (goodwillApplied)
            {
                failureReason =
                    $"goodwill_target_miss(current={currentGoodwill}, target={targetGoodwill}, applied={appliedGoodwill}, relation={faction.RelationKindWith(player)})";
                return false;
            }

            if (faction.HasGoodwill)
            {
                failureReason =
                    $"goodwill_apply_failed(current={currentGoodwill}, target={targetGoodwill}, relation={faction.RelationKindWith(player)})";
                return false;
            }

            try
            {
                faction.SetRelationDirect(player, expectedRelation);
            }
            catch (Exception ex)
            {
                failureReason = $"goodwill_apply_failed_and_set_relation_failed({ex.Message})";
                return false;
            }

            appliedGoodwill = faction.PlayerGoodwill;
            if (faction.RelationKindWith(player) == expectedRelation)
            {
                return true;
            }

            failureReason =
                $"relation_target_miss(current={faction.RelationKindWith(player)}, expected={expectedRelation}, appliedGoodwill={appliedGoodwill})";
            return false;
        }

internal void NotifySignificantGoodwillChange(Faction faction, int oldGoodwill, int newGoodwill, string reason)
        {
            int change = newGoodwill - oldGoodwill;
            string titleKey = change > 0
                ? "RimChat_GoodwillImprovedLetterTitle"
                : "RimChat_GoodwillWorsenedLetterTitle";
            string messageKey = change > 0
                ? "RimChat_GoodwillImprovedLetterBody"
                : "RimChat_GoodwillWorsenedLetterBody";
            LetterDef letterDef = change > 0 ? LetterDefOf.PositiveEvent : LetterDefOf.NegativeEvent;

            Find.LetterStack.ReceiveLetter(
                titleKey.Translate(),
                messageKey.Translate(faction.Name, Math.Abs(change), reason ?? string.Empty),
                letterDef);
        }

    }
}
