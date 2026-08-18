using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using RimWorld;
using UnityEngine;
using Verse;
using APIResult = Ustas.RimAI.Communication.Relations.DiplomacySystem.GameAIInterface.APIResult;
using APICallRecord = Ustas.RimAI.Communication.Relations.DiplomacySystem.GameAIInterface.APICallRecord;
using DialogueApiGoodwillCostResult = Ustas.RimAI.Communication.Relations.DiplomacySystem.GameAIInterface.DialogueApiGoodwillCostResult;
using FactionCooldownEntry = Ustas.RimAI.Communication.Relations.DiplomacySystem.GameAIInterface.FactionCooldownEntry;
using RaidWaveState = Ustas.RimAI.Communication.Relations.DiplomacySystem.GameAIInterface.RaidWaveState;

namespace Ustas.RimAI.Communication.Relations.DiplomacySystem
{
    /// <summary>Item airdrop prepare/commit barter orchestration.</summary>
    internal sealed class GameAIAirdropBarter : GameAIInterfaceCollaborator
    {
        internal GameAIAirdropBarter(GameAIInterface owner) : base(owner)
        {
        }

public APIResult PrepareItemAirdropTrade(Faction faction, Dictionary<string, object> parameters, Pawn playerNegotiator)
        {
            if (playerNegotiator == null || playerNegotiator.Map == null)
            {
                return Owner.Parts.AirdropDrop.FailFastAirdrop(
                    "player_negotiator_required",
                    "Preparing a barter airdrop requires a valid player negotiator on a map.",
                    faction,
                    parameters,
                    sendLetter: false);
            }

            return PrepareItemAirdropTradeForMap(faction, parameters, playerNegotiator.Map, true, playerNegotiator);
        }

public APIResult CommitPreparedItemAirdropTrade(Faction faction, ItemAirdropPreparedTradeData preparedData)
        {
            if (faction == null)
            {
                return APIResult.FailureResult("Faction cannot be null.");
            }

            if (preparedData == null)
            {
                return APIResult.FailureResult("[prepared_trade_missing] Missing prepared airdrop trade payload.");
            }

            Map map = Find.Maps?.FirstOrDefault(m => m != null && m.uniqueID == preparedData.MapUniqueId);
            if (map == null)
            {
                return Owner.Parts.AirdropDrop.FailFastAirdrop(
                    "map_unavailable",
                    "Prepared airdrop map is no longer available.",
                    faction,
                    preparedData.ParametersSnapshot);
            }

            ThingDefRecord selectedRecord = ThingDefCatalog.GetRecords()
                .FirstOrDefault(record =>
                    record?.Def != null &&
                    string.Equals(record.DefName, preparedData.SelectedDefName, StringComparison.OrdinalIgnoreCase));
            if (selectedRecord == null)
            {
                return Owner.Parts.AirdropDrop.FailFastAirdrop(
                    "selected_def_unresolved",
                    $"Selected def '{preparedData.SelectedDefName}' could not be resolved during commit.",
                    faction,
                    preparedData.ParametersSnapshot);
            }

            if (!GameAIAirdropDrop.TryFindAirdropCell(map, out IntVec3 dropCell))
            {
                if (MapUtility.IsOrbitalBaseMap(map))
                {
                    return Owner.Parts.AirdropDrop.FailFastAirdrop(
                        "orbital_drop_unavailable",
                        "You are on an orbital base and cannot receive supply drops.",
                        faction,
                        preparedData.ParametersSnapshot);
                }
                return Owner.Parts.AirdropDrop.FailFastAirdrop(
                    "dropcell_not_found",
                    "No legal drop cell found near colony center.",
                    faction,
                    preparedData.ParametersSnapshot);
            }

            int maxStacks = RelationsMod.Instance?.InstanceSettings?.ItemAirdropMaxStacksPerDrop ?? 8;
            List<Thing> stacks = GameAIAirdropDrop.BuildStacks(selectedRecord.Def, preparedData.Quantity, maxStacks);
            if (stacks.Count == 0)
            {
                return Owner.Parts.AirdropDrop.FailFastAirdrop(
                    "stack_build_failed",
                    "Could not create item stacks for airdrop.",
                    faction,
                    preparedData.ParametersSnapshot);
            }

            int deliveredCount = stacks.Sum(t => t.stackCount);
            if (deliveredCount != preparedData.Quantity)
            {
                return Owner.Parts.AirdropDrop.FailFastAirdrop(
                    "delivery_quantity_mismatch",
                    $"Confirmed airdrop quantity {preparedData.Quantity} exceeds stack delivery capacity {deliveredCount}.",
                    faction,
                    preparedData.ParametersSnapshot,
                    $"def={selectedRecord.DefName},confirmed={preparedData.Quantity},delivered={deliveredCount},maxStacks={maxStacks},stackLimit={selectedRecord.Def.stackLimit}");
            }

            APIResult validation = Owner.Parts.AirdropPayment.ValidateDeductionPlan(map, preparedData.DeductionPlan, out List<ThingDeductionReservation> reservations);
            if (!validation.Success)
            {
                return Owner.Parts.AirdropDrop.FailFastAirdrop(
                    (validation.Data as ItemAirdropResultData)?.FailureCode ?? "payment_item_insufficient",
                    validation.Message,
                    faction,
                    preparedData.ParametersSnapshot);
            }

            GameAIAirdropPayment.ApplyDeductionReservations(reservations);

            DropPodUtility.DropThingsNear(
                dropCell,
                map,
                stacks,
                110,
                canInstaDropDuringInit: false,
                leaveSlag: false,
                canRoofPunch: false);

            string stageText = $"def={selectedRecord.DefName},count={deliveredCount},budget={preparedData.BudgetSilver},reason={preparedData.SelectionReason},drop={dropCell},payment={preparedData.PaymentTotalSilver}";
            Owner.Parts.AirdropDrop.RecordStageAudit("execute", faction, preparedData.ParametersSnapshot, stageText);
            Owner.Parts.CooldownOps.RecordAPICall("RequestItemAirdrop", true, stageText);

            string playerTitle = "RimChat_ItemAirdropArrivedTitle".Translate();
            string playerBody = "RimChat_ItemAirdropArrivedBody".Translate(
                faction.Name,
                selectedRecord.Label.CapitalizeFirst(),
                deliveredCount,
                preparedData.PaymentTotalSilver);
            Find.LetterStack.ReceiveLetter(playerTitle, playerBody, LetterDefOf.PositiveEvent, new TargetInfo(dropCell, map), faction);

            var payload = new ItemAirdropResultData
            {
                SelectedDefName = selectedRecord.DefName,
                ResolvedLabel = selectedRecord.Label,
                BudgetUsed = preparedData.BudgetSilver,
                ShippingCostSilver = preparedData.ShippingCostSilver,
                PaymentTotalSilver = preparedData.PaymentTotalSilver,
                Quantity = deliveredCount,
                DropCell = dropCell.ToString(),
                FailureCode = string.Empty
            };

            AirdropTradeRuleSnapshot cooldownRule = ItemAirdropTradePolicy.ResolveRuleSnapshot(
                faction,
                map.wealthWatcher?.WealthItems ?? 0f,
                Owner.Parts.CooldownOps.GetAirdropFactionTradeTotal(faction));
            float offerPercentMultiplier = Mathf.Clamp(
                (float)preparedData.PaymentTotalSilver / Math.Max(1, cooldownRule.TradeLimitSilver),
                0.01f,
                1f);
            Owner.Parts.CooldownOps.RecordAirdropFactionTradeTotal(faction, preparedData.PaymentTotalSilver);
            Owner.Parts.CooldownOps.SetCooldown(faction, "RequestItemAirdrop", offerPercentMultiplier);
            Owner.Parts.CooldownOps.RecordSuccessfulAirdropFaction(faction);

            // Check if this is a special item (discount/scarce) and mark as traded
            if (FactionSpecialItemsManager.Instance.TryMatchSpecialItem(faction, preparedData.SelectedDefName, out SpecialItemType specialItemType))
            {
                FactionSpecialItemsManager.Instance.MarkTraded(faction, specialItemType);
            }

            return APIResult.SuccessResult(
                $"Airdrop delivered: {selectedRecord.DefName} x{deliveredCount} (budget {preparedData.BudgetSilver})",
                payload);
        }

internal APIResult PrepareItemAirdropTradeForMap(
            Faction faction,
            Dictionary<string, object> parameters,
            Map map,
            bool requirePlayerHome,
            Pawn playerNegotiator)
        {
            if (RelationsMod.Instance?.InstanceSettings == null)
            {
                return APIResult.FailureResult("Settings not initialized");
            }

            RelationsSettings settings = RelationsMod.Instance.InstanceSettings;
            if (!settings.EnableAIItemAirdrop)
            {
                return APIResult.FailureResult("request_item_airdrop is disabled in settings.");
            }

            if (faction == null)
            {
                return APIResult.FailureResult("Faction cannot be null");
            }

            if (parameters == null)
            {
                return APIResult.FailureResult("request_item_airdrop requires parameters.");
            }

            if (map == null)
            {
                return Owner.Parts.AirdropDrop.FailFastAirdrop("no_home_map", "No player map available for item airdrop.", faction, parameters, sendLetter: false);
            }

            if (requirePlayerHome && !map.IsPlayerHome)
            {
                return Owner.Parts.AirdropDrop.FailFastAirdrop("map_not_player_home", "Barter airdrop requires a player home map context.", faction, parameters, sendLetter: false);
            }

            bool hasNeed = GameAIAirdropDrop.TryReadRequiredStringParameter(
                parameters,
                "need",
                out string need,
                out string needType,
                out string needRawPreview);
            if (!hasNeed)
            {
                string code = string.Equals(needType, "missing", StringComparison.Ordinal) ? "missing_need" : "need_type_invalid";
                return Owner.Parts.AirdropDrop.FailFastAirdrop(code, "request_item_airdrop requires string parameter 'need'.", faction, parameters, sendLetter: false);
            }

            string scenario = GameAIAirdropDrop.NormalizeScenario(GameAIAirdropDrop.ReadString(parameters, "scenario"));
            string constraints = GameAIAirdropDrop.ReadString(parameters, "constraints");
            bool hasProvidedBudget = GameAIAirdropDrop.TryReadIntParameter(parameters, "budget_silver", out int providedBudgetSilver);

            APIResult paymentPlanResult = Owner.Parts.AirdropPayment.BuildPaymentPlan(
                parameters,
                map,
                faction,
                playerNegotiator,
                out List<ItemAirdropPreparedPaymentLine> paymentLines,
                out List<ItemAirdropDeductionPlanLine> deductionPlan,
                out int budget,
                out int paymentTotalSilver);
            if (!paymentPlanResult.Success)
            {
                return Owner.Parts.AirdropDrop.FailFastAirdrop(
                    (paymentPlanResult.Data as ItemAirdropResultData)?.FailureCode ?? "payment_plan_failed",
                    paymentPlanResult.Message,
                    faction,
                    parameters);
            }

            if (hasProvidedBudget && providedBudgetSilver != budget)
            {
                string mismatchAudit =
                    $"faction={faction?.Name ?? "unknown"},provided={providedBudgetSilver},derived={budget},delta={providedBudgetSilver - budget},need={need},scenario={scenario}";
                Owner.Parts.CooldownOps.RecordAPICall("RequestItemAirdrop.BudgetMismatch", true, mismatchAudit);
            }

            ItemAirdropIntent intent = ItemAirdropIntent.Create(need, constraints, scenario);
            APIResult candidateResult = Owner.Parts.AirdropRequest.PrepareItemAirdropCandidates(
                intent,
                budget,
                settings,
                out ItemAirdropCandidatePack candidatePack);
            if (!candidateResult.Success)
            {
                return candidateResult;
            }
            List<string> localAliases = new List<string>();
            List<string> aliases = new List<string>();
            if (candidatePack.Candidates.Count == 0)
            {
                localAliases = ThingDefResolver.ExpandLocalAliases(intent);
                if (localAliases.Count > 0)
                {
                    intent = ItemAirdropIntent.Create(need, constraints, scenario, localAliases);
                    candidateResult = Owner.Parts.AirdropRequest.PrepareItemAirdropCandidates(
                        intent,
                        budget,
                        settings,
                        out candidatePack);
                    if (!candidateResult.Success)
                    {
                        return candidateResult;
                    }
                }
            }

            if (candidatePack.Candidates.Count == 0)
            {
                aliases = Owner.Parts.AirdropRequest.ExpandNeedAliasesWithAi(need, constraints, settings);
                if (aliases.Count > 0)
                {
                    intent = ItemAirdropIntent.Create(need, constraints, scenario, aliases);
                    candidateResult = Owner.Parts.AirdropRequest.PrepareItemAirdropCandidates(
                        intent,
                        budget,
                        settings,
                        out candidatePack);
                    if (!candidateResult.Success)
                    {
                        return candidateResult;
                    }
                }
            }

            APIResult boundNeedResult = Owner.Parts.AirdropBoundNeed.TryApplyBoundNeedArbitration(
                faction,
                parameters,
                intent,
                candidatePack,
                out _);
            if (!boundNeedResult.Success)
            {
                return boundNeedResult;
            }

            string prepareSummary = GameAIAirdropRequest.BuildPrepareAuditSummary(intent, budget, candidatePack, localAliases, aliases, needType, needRawPreview);
            Owner.Parts.AirdropDrop.RecordStageAudit("prepare", faction, parameters, prepareSummary);
            if (candidatePack.Candidates.Count == 0)
            {
                if (intent.Family == ItemAirdropNeedFamily.Unknown)
                {
                    return Owner.Parts.AirdropDrop.FailFastAirdrop(
                        "need_family_unknown",
                        "Could not classify request need. Try adding multiple CN/EN aliases in need/constraints.",
                        faction,
                        parameters,
                        prepareSummary,
                        sendLetter: false);
                }

                return Owner.Parts.AirdropDrop.FailFastAirdrop(
                    "no_candidates",
                    "No legal airdrop candidates were produced for this request.",
                    faction,
                    parameters,
                    prepareSummary,
                    sendLetter: false);
            }

            if (GameAIAirdropRequest.ShouldRequireNeedClarification(intent, candidatePack))
            {
                APIResult pendingClarification = Owner.Parts.AirdropPending.BuildTimeoutPendingSelection(
                    intent,
                    candidatePack,
                    budget,
                    settings,
                    "need_relevance_insufficient",
                    GameAIAirdropRequest.BuildNeedClarificationReason(),
                    allowEmptyOptions: true);
                if (pendingClarification.Data is ItemAirdropPendingSelectionData pendingData)
                {
                    Owner.Parts.AirdropDrop.RecordStageAudit("selection", null, null, GameAIAirdropPending.BuildPendingSelectionAuditDetails(pendingData));
                }

                return pendingClarification;
            }

            string forcedSelectedDef = GameAIAirdropDrop.ReadString(parameters, "selected_def");
            APIResult selectionResult = Owner.Parts.AirdropSelection.ExecuteItemAirdropSelection(intent, candidatePack, budget, settings, parameters, forcedSelectedDef);
            if (!selectionResult.Success)
            {
                string code = (selectionResult.Data as ItemAirdropResultData)?.FailureCode ?? "selection_failed";
                return Owner.Parts.AirdropDrop.FailFastAirdrop(code, selectionResult.Message, faction, parameters, sendLetter: false);
            }

            if (selectionResult.Data is ItemAirdropPendingSelectionData pendingSelection)
            {
                return APIResult.SuccessResult("Airdrop selection requires player confirmation.", pendingSelection);
            }

            if (!(selectionResult.Data is ItemAirdropSelection selection))
            {
                return Owner.Parts.AirdropDrop.FailFastAirdrop("selection_invalid", "Selection result payload is invalid.", faction, parameters, sendLetter: false);
            }

            RequestedCountExtraction requestedCount = GameAIAirdropSelection.ExtractRequestedCount(intent?.NeedText);
            requestedCount = GameAIAirdropSelection.MergeRequestedCountWithParameters(requestedCount, parameters);
            string requestedCountAudit = $"need_text={need},explicit={requestedCount.HasExplicitCount}:{requestedCount.RequestedCount},parameter={requestedCount.HasParameterCount}:{requestedCount.ParameterCount}";
            Owner.Parts.AirdropDrop.RecordStageAudit("requested_count", faction, parameters, requestedCountAudit);
            APIResult validationResult = Owner.Parts.AirdropSelection.ValidateAirdropSelection(
                selection,
                candidatePack,
                budget,
                settings,
                requestedCount,
                "llm",
                out ThingDefRecord selectedRecord,
                out int validatedCount,
                out _,
                out int requestedOriginalCount,
                out int maxByBudget,
                out int maxBySystem,
                out int hardMax);
            if (!validationResult.Success)
            {
                return Owner.Parts.AirdropDrop.FailFastAirdrop(
                    (validationResult.Data as ItemAirdropResultData)?.FailureCode ?? "selection_invalid",
                    validationResult.Message,
                    faction,
                    parameters,
                    sendLetter: false);
            }

            SpecialItemType? barterSpecialType = null;
            if (faction != null && FactionSpecialItemsManager.Instance.TryMatchSpecialItem(faction, selectedRecord.DefName, out SpecialItemType bst))
                barterSpecialType = bst;
            float needQuotedUnitSilver = ResolveAirdropNeedQuotedUnitPrice(
                selectedRecord, faction, playerNegotiator, map, candidatePack, barterSpecialType);
            int quotedNeedTotalSilver = ResolveAirdropNeedQuotedTotalSilver(
                selectedRecord,
                validatedCount,
                faction,
                playerNegotiator,
                map,
                candidatePack,
                barterSpecialType);
            AirdropTradeRuleSnapshot tradeRuleSnapshot = ItemAirdropTradePolicy.ResolveRuleSnapshot(
                faction,
                map.wealthWatcher?.WealthItems ?? 0f,
                Owner.Parts.CooldownOps.GetAirdropFactionTradeTotal(faction));
            int shippingPodCount = ResolveAirdropShippingPodCount(selectedRecord?.Def, validatedCount);
            int shippingCostSilver = shippingPodCount * tradeRuleSnapshot.ShippingCostPerPod;
            int actualNeeded = quotedNeedTotalSilver + shippingCostSilver;

            if (actualNeeded < paymentTotalSilver)
            {
                double scale = (double)actualNeeded / Math.Max(1, paymentTotalSilver);
                List<ItemAirdropDeductionPlanLine> adjustedPlan = new List<ItemAirdropDeductionPlanLine>();
                int adjustedTotal = 0;
                foreach (ItemAirdropDeductionPlanLine line in deductionPlan)
                {
                    int scaledCount = Math.Max(1, (int)Math.Round(line.Count * scale));
                    adjustedPlan.Add(new ItemAirdropDeductionPlanLine
                    {
                        ThingId = line.ThingId,
                        DefName = line.DefName,
                        Count = scaledCount
                    });
                    adjustedTotal += scaledCount;
                }
                deductionPlan = adjustedPlan;
                budget = actualNeeded;
                paymentTotalSilver = actualNeeded;

                if (validatedCount < requestedOriginalCount)
                {
                    Log.Message($"[RimAI.Relations][PaymentAdjust] Quantity clamped ({requestedOriginalCount}->{validatedCount}), payment scaled to {actualNeeded} (scale={scale:F4}, deductionLines={adjustedPlan.Count}, totalDeductionCount={adjustedTotal})");
                }
            }

            int overpay = Math.Max(0, paymentTotalSilver - budget);
            string budgetMismatchSummary = hasProvidedBudget
                ? $"{providedBudgetSilver}->{budget}(delta={providedBudgetSilver - budget})"
                : "none";
            string paymentSummary = $"budget={budget},payment={paymentTotalSilver},overpay={overpay},budgetMismatch={budgetMismatchSummary},paymentLines={paymentLines.Count},deductionRows={deductionPlan.Count}";
            Owner.Parts.AirdropDrop.RecordStageAudit("prepare_trade", faction, parameters, paymentSummary);

            string offerDefName = paymentLines?.FirstOrDefault()?.DefName;
            ThingDef offerDef = !string.IsNullOrWhiteSpace(offerDefName)
                ? DefDatabase<ThingDef>.GetNamedSilentFail(offerDefName)
                : null;

            var prepared = new ItemAirdropPreparedTradeData
            {
                SelectedDefName = selectedRecord.DefName,
                ResolvedLabel = selectedRecord.Label,
                Quantity = validatedCount,
                RequestedQuantity = requestedOriginalCount,
                MaxByBudget = maxByBudget,
                MaxBySystem = maxBySystem,
                HardMax = hardMax,
                CountAdjustmentReason = validatedCount < requestedOriginalCount
                    ? $"clamped_to_hard_max({requestedOriginalCount}->{validatedCount})"
                    : "none",
                BudgetSilver = budget,
                NeedQuotedUnitSilver = needQuotedUnitSilver,
                PaymentTotalSilver = paymentTotalSilver,
                PaymentItemTotalSilver = paymentTotalSilver,
                ShippingPodCount = shippingPodCount,
                ShippingCostSilver = shippingCostSilver,
                PaymentOverpaySilver = overpay,
                MapUniqueId = map.uniqueID,
                NeedText = need,
                Scenario = scenario,
                SelectionReason = selection.Reason ?? string.Empty,
                NeedPriceSemantic = ItemAirdropTradePolicy.ResolveNeedPriceSemantic(selectedRecord?.Def, faction),
                PaymentPriceSemantic = ItemAirdropTradePolicy.ResolveOfferPriceSemantic(offerDef),
                SpecialItemType = barterSpecialType,
                PaymentLines = paymentLines,
                DeductionPlan = deductionPlan,
                ParametersSnapshot = GameAIAirdropPayment.CloneParameterDictionary(parameters)
            };

            return APIResult.SuccessResult("Airdrop trade prepared.", prepared);
        }

internal static int ResolveAirdropShippingPodCount(ThingDef selectedDef, int quantity)
        {
            int safeQuantity = Math.Max(0, quantity);
            if (safeQuantity <= 0)
            {
                return 0;
            }

            int stackLimit = Math.Max(1, selectedDef?.stackLimit ?? safeQuantity);
            return (int)Math.Ceiling((double)safeQuantity / stackLimit);
        }

internal static int ResolveAirdropNeedQuotedTotalSilver(
            ThingDefRecord selectedRecord,
            int quantity,
            Faction faction,
            Pawn playerNegotiator,
            Map map,
            ItemAirdropCandidatePack candidatePack,
            SpecialItemType? specialItemType = null)
        {
            float unitPrice = ResolveAirdropNeedQuotedUnitPrice(selectedRecord, faction, playerNegotiator, map, candidatePack, specialItemType);
            float total = Math.Max(0f, unitPrice) * Math.Max(0, quantity);
            return Mathf.Max(0, Mathf.RoundToInt(total));
        }

internal static float ResolveAirdropNeedQuotedUnitPrice(
            ThingDefRecord selectedRecord,
            Faction faction,
            Pawn playerNegotiator,
            Map map,
            ItemAirdropCandidatePack candidatePack,
            SpecialItemType? specialItemType = null)
        {
            _ = faction;
            _ = playerNegotiator;
            _ = map;
            ThingDef def = selectedRecord?.Def;

            float unitPrice;

            if (def != null && specialItemType.HasValue)
            {
                if (ItemAirdropTradePolicy.TryResolveSpecialItemPrice(def, specialItemType.Value, out float specialPrice, out _))
                {
                    unitPrice = specialPrice;
                }
                else
                {
                    unitPrice = ResolveStandardNeedUnitPrice(def, faction, playerNegotiator, map, candidatePack, selectedRecord);
                }
            }
            else
            {
                unitPrice = ResolveStandardNeedUnitPrice(def, faction, playerNegotiator, map, candidatePack, selectedRecord);
            }

            ItemAirdropTradePolicy.ApplyUntradeablePremium(def, ref unitPrice);

            return unitPrice;
        }

internal static float ResolveStandardNeedUnitPrice(
            ThingDef def,
            Faction faction,
            Pawn playerNegotiator,
            Map map,
            ItemAirdropCandidatePack candidatePack,
            ThingDefRecord selectedRecord)
        {
            if (def != null && ItemAirdropTradePolicy.TryResolvePlayerBuyPrice(def, faction, playerNegotiator, map, out float unitPrice, out _))
            {
                return unitPrice;
            }

            return candidatePack?.ResolveUnitPrice(selectedRecord) ?? Math.Max(0.01f, selectedRecord?.MarketValue ?? 0.01f);
        }

    }
}
