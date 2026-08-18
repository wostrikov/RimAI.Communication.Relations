using System;
using System.Collections.Generic;
using RimWorld;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Memory
{
    /// <summary>
    /// Builds pending airdrop trade-card reference blocks for diplomacy sessions.
    /// </summary>
    internal static class FactionDialogueSessionAirdropRefs
    {
        internal static bool TryBuildPendingAirdropTradeCardReference(FactionDialogueSession session, out string referenceBlock)
        {
            referenceBlock = string.Empty;
            if (!session.hasPendingAirdropTradeCardReference)
            {
                return false;
            }

            string scenario = string.IsNullOrWhiteSpace(session.pendingAirdropTradeCardScenario)
                ? "trade"
                : session.pendingAirdropTradeCardScenario.Trim();
            int requestedCount = Math.Max(1, session.pendingAirdropTradeCardRequestedCount);
            string paymentItem = string.IsNullOrWhiteSpace(session.pendingAirdropTradeCardPaymentItemDef)
                ? "Silver"
                : session.pendingAirdropTradeCardPaymentItemDef.Trim();
            int paymentItemCount = Math.Max(1, session.pendingAirdropTradeCardPaymentItemCount);

            // Resolve live airdrop quote context when possible
            float needUnitValue = 0f;
            float needTotalValue = 0f;
            float offerUnitValue = 0f;
            float offerTotalValue = 0f;
            string needValueSemantic = "market_value";
            string offerValueSemantic = "market_value";
            string needDefName = session.pendingAirdropTradeCardNeedDefName ?? string.Empty;
            Map map = Find.AnyPlayerHomeMap ?? Find.CurrentMap;
            Pawn negotiator = ItemAirdropTradePolicy.ResolveBestNegotiator(null);
            if (!string.IsNullOrWhiteSpace(needDefName))
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(needDefName);
                if (def != null)
                {
                    // Check special item pricing first (discount/scarce) to match actual trade execution
                    SpecialItemType? detectedSpecialType = null;
                    if (session.faction != null && FactionSpecialItemsManager.Instance.TryMatchSpecialItem(session.faction, needDefName, out SpecialItemType sType))
                    {
                        detectedSpecialType = sType;
                    }

                    if (detectedSpecialType.HasValue &&
                        ItemAirdropTradePolicy.TryResolveSpecialItemPrice(def, detectedSpecialType.Value, out float specialPrice, out _))
                    {
                        needUnitValue = specialPrice;
                    }
                    else if (ItemAirdropTradePolicy.TryResolveNeedUnitPrice(def, out float resolvedNeedUnit, out _))
                    {
                        needUnitValue = resolvedNeedUnit;
                        ItemAirdropTradePolicy.ApplyUntradeablePremium(def, ref needUnitValue);
                    }
                    else
                    {
                        needUnitValue = def.BaseMarketValue;
                    }

                    needValueSemantic = ItemAirdropTradePolicy.ResolveNeedPriceSemantic(def, session.faction);

                    needTotalValue = needUnitValue * requestedCount;
                }
            }

            if (!string.IsNullOrWhiteSpace(paymentItem))
            {
                ThingDef offerDef = DefDatabase<ThingDef>.GetNamedSilentFail(paymentItem);
                if (offerDef != null)
                {
                    if (ItemAirdropTradePolicy.TryResolveOfferUnitPrice(offerDef, out float resolvedOfferUnit, out _))
                    {
                        offerUnitValue = resolvedOfferUnit;
                        offerValueSemantic = ItemAirdropTradePolicy.ResolveOfferPriceSemantic(offerDef);
                    }
                    else
                    {
                        offerUnitValue = offerDef.BaseMarketValue;
                    }

                    offerTotalValue = offerUnitValue * paymentItemCount;
                }
            }

            int shippingPods = Math.Max(0, session.pendingAirdropTradeCardShippingPodCount);
            int shippingCost = Math.Max(0, session.pendingAirdropTradeCardShippingCost);

            referenceBlock =
                "[AirdropTradeCardReference]\n" +
                $"need: {session.pendingAirdropTradeCardNeed}\n" +
                $"need_def: {needDefName}\n" +
                $"need_label: {session.pendingAirdropTradeCardNeedLabel}\n" +
                $"need_search_text: {session.pendingAirdropTradeCardNeedSearchText}\n" +
                $"count: {requestedCount}\n" +
                $"payment_items: [{{\"item\":\"{paymentItem}\",\"count\":{paymentItemCount}}}]\n" +
                $"scenario: {scenario}\n" +
                $"shipping_pods: {shippingPods}\n" +
                $"shipping_cost_silver: {shippingCost}\n" +
                // Hidden context: aligned quote context and role reminder for AI
                "[AirdropHiddenContext]\n" +
                $"need_unit_value: {needUnitValue:F2}\n" +
                $"need_total_value: {needTotalValue:F2}\n" +
                $"need_value_semantic: {needValueSemantic}\n" +
                $"offer_unit_value: {offerUnitValue:F2}\n" +
                $"offer_total_value: {offerTotalValue:F2}\n" +
                $"offer_value_semantic: {offerValueSemantic}\n" +
                $"final_quote_with_shipping: {Math.Max(0f, needTotalValue + shippingCost):F2}\n" +
                "role_reminder: You are the faction providing the requested supplies via emergency airdrop. " +
                "The player is paying you with their offer items. " +
                "Your profit increases when the need items have higher market value. " +
                "The player loses more when they offer higher-value items. " +
                "You may accept the trade if the offer is fair or above market value (emergency premium is acceptable). " +
                "Reject or counter-offer if the player's offer is below market value.\n" +
                "[/AirdropHiddenContext]\n" +
                "[/AirdropTradeCardReference]";
            return true;
        }

        internal static void SetPendingAirdropTradeCardReference(
            FactionDialogueSession session,
            string need,
            string needDefName,
            string needLabel,
            string needSearchText,
            int requestedCount,
            string paymentItemDef,
            string paymentItemLabel,
            int paymentItemCount,
            string scenario,
            int shippingPodCount = 0,
            int shippingCostSilver = 0)
        {
            session.hasPendingAirdropTradeCardReference = true;
            session.pendingAirdropTradeCardNeed = need ?? string.Empty;
            session.pendingAirdropTradeCardNeedDefName = needDefName ?? string.Empty;
            session.pendingAirdropTradeCardNeedLabel = needLabel ?? string.Empty;
            session.pendingAirdropTradeCardNeedSearchText = needSearchText ?? string.Empty;
            session.pendingAirdropTradeCardRequestedCount = Math.Max(0, requestedCount);
            session.pendingAirdropTradeCardPaymentItemDef = paymentItemDef ?? string.Empty;
            session.pendingAirdropTradeCardPaymentItemLabel = paymentItemLabel ?? string.Empty;
            session.pendingAirdropTradeCardPaymentItemCount = Math.Max(0, paymentItemCount);
            session.pendingAirdropTradeCardScenario = string.IsNullOrWhiteSpace(scenario) ? "trade" : scenario.Trim();
            session.pendingAirdropTradeCardSubmittedTick = Find.TickManager?.TicksGame ?? 0;
            session.pendingAirdropTradeCardShippingPodCount = Math.Max(0, shippingPodCount);
            session.pendingAirdropTradeCardShippingCost = Math.Max(0, shippingCostSilver);
        }

        internal static void ClearPendingAirdropTradeCardReference(FactionDialogueSession session)
        {
            session.hasPendingAirdropTradeCardReference = false;
            session.pendingAirdropTradeCardNeed = string.Empty;
            session.pendingAirdropTradeCardNeedDefName = string.Empty;
            session.pendingAirdropTradeCardNeedLabel = string.Empty;
            session.pendingAirdropTradeCardNeedSearchText = string.Empty;
            session.pendingAirdropTradeCardRequestedCount = 0;
            session.pendingAirdropTradeCardPaymentItemDef = string.Empty;
            session.pendingAirdropTradeCardPaymentItemLabel = string.Empty;
            session.pendingAirdropTradeCardPaymentItemCount = 0;
            session.pendingAirdropTradeCardScenario = "trade";
            session.pendingAirdropTradeCardSubmittedTick = 0;
            session.pendingAirdropTradeCardShippingPodCount = 0;
            session.pendingAirdropTradeCardShippingCost = 0;
        }

        internal static void ClearPendingAirdropExecutionState(FactionDialogueSession session)
        {
            session.pendingAirdropRequestId = null;
            session.pendingAirdropRequestLease = null;
            session.isWaitingForAirdropSelection = false;
            session.pendingAirdropRequestStartedRealtime = -1f;
            session.pendingAirdropRequestTimeoutSeconds = 0;
            session.airdropRequestGeneration++;
            session.airdropExecutionStage = AirdropExecutionStage.Idle;
            ClearPendingAirdropSelectionIntentState(session);
        }

        internal static bool HasPendingAirdropSelectionIntent(FactionDialogueSession session)
        {
            return HasPendingAirdropSelectionPayload(session.pendingDelayedActionIntent?.Parameters) ||
                   HasPendingAirdropSelectionPayload(session.lastDelayedActionIntent?.Parameters);
        }

        internal static bool ClearPendingAirdropSelectionIntentState(FactionDialogueSession session)
        {
            bool cleared = false;
            if (HasPendingAirdropSelectionPayload(session.pendingDelayedActionIntent?.Parameters))
            {
                session.pendingDelayedActionIntent = null;
                cleared = true;
            }

            if (HasPendingAirdropSelectionPayload(session.lastDelayedActionIntent?.Parameters))
            {
                session.lastDelayedActionIntent = null;
                cleared = true;
            }

            return cleared;
        }

        private static bool HasPendingAirdropSelectionPayload(Dictionary<string, object> parameters)
        {
            if (parameters == null)
            {
                return false;
            }

            return parameters.ContainsKey("__airdrop_pending_candidates") ||
                   parameters.ContainsKey("__airdrop_pending_failure_code");
        }
    }
}
