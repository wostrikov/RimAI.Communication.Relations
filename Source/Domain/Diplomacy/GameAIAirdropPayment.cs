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
    /// <summary>Item airdrop payment plan and beacon deduction.</summary>
    internal sealed class GameAIAirdropPayment : GameAIInterfaceCollaborator
    {
        internal GameAIAirdropPayment(GameAIInterface owner) : base(owner)
        {
        }

internal APIResult BuildPaymentPlan(
            Dictionary<string, object> parameters,
            Map map,
            Faction faction,
            Pawn playerNegotiator,
            out List<ItemAirdropPreparedPaymentLine> paymentLines,
            out List<ItemAirdropDeductionPlanLine> deductionPlan,
            out int derivedBudgetSilver,
            out int paymentTotalSilver)
        {
            paymentLines = new List<ItemAirdropPreparedPaymentLine>();
            deductionPlan = new List<ItemAirdropDeductionPlanLine>();
            derivedBudgetSilver = 0;
            paymentTotalSilver = 0;

            APIResult parseResult = ParsePaymentItems(parameters, out List<ItemAirdropPaymentRequestLine> requestedLines);
            if (!parseResult.Success)
            {
                Log.Message($"[RimAI.Relations][PaymentPlan] ParsePaymentItems failed: {parseResult.Message}");
                return parseResult;
            }

            return BuildPaymentPlanFromRequestedLines(
                requestedLines,
                map,
                faction,
                playerNegotiator,
                out paymentLines,
                out deductionPlan,
                out derivedBudgetSilver,
                out paymentTotalSilver);
        }

internal APIResult BuildPaymentPlanFromRequestedLines(
            List<ItemAirdropPaymentRequestLine> requestedLines,
            Map map,
            Faction faction,
            Pawn playerNegotiator,
            out List<ItemAirdropPreparedPaymentLine> paymentLines,
            out List<ItemAirdropDeductionPlanLine> deductionPlan,
            out int derivedBudgetSilver,
            out int paymentTotalSilver)
        {
            paymentLines = new List<ItemAirdropPreparedPaymentLine>();
            deductionPlan = new List<ItemAirdropDeductionPlanLine>();
            derivedBudgetSilver = 0;
            paymentTotalSilver = 0;

            if (requestedLines == null || requestedLines.Count == 0)
            {
                return BuildPaymentFailure("payment_items_missing", "payment_items must include at least one item.");
            }

            Log.Message($"[RimAI.Relations][PaymentPlan] Parsed {requestedLines.Count} payment_items: {string.Join(", ", requestedLines.Select(l => $"{l.ItemText}x{l.Count}"))}");

            List<Thing> beaconThings = CollectBeaconTradeableThings(map);
            if (beaconThings.Count == 0)
            {
                Log.Message("[RimAI.Relations][PaymentPlan] No powered orbital-trade-beacon source items available.");
                return BuildPaymentFailure("beacon_source_unavailable", "No powered orbital-trade-beacon source items are available on this map.");
            }

            Log.Message($"[RimAI.Relations][PaymentPlan] Beacon has {beaconThings.Count} tradeable things.");

            var buckets = new Dictionary<string, List<Thing>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < beaconThings.Count; i++)
            {
                Thing thing = beaconThings[i];
                string defName = thing?.def?.defName ?? string.Empty;
                if (string.IsNullOrWhiteSpace(defName))
                {
                    continue;
                }

                if (!buckets.TryGetValue(defName, out List<Thing> bucket))
                {
                    bucket = new List<Thing>();
                    buckets[defName] = bucket;
                }

                bucket.Add(thing);
            }

            List<ThingDefRecord> stockedRecords = buckets.Values
                .Select(bucket => bucket.FirstOrDefault()?.def)
                .Where(def => def != null)
                .Select(ThingDefRecord.From)
                .GroupBy(record => record.DefName, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            Log.Message($"[RimAI.Relations][PaymentPlan] Beacon inventory buckets: {string.Join(", ", buckets.Select(kvp => $"{kvp.Key}x{kvp.Value.Sum(t => t.stackCount)}"))}");

            float totalValueFloat = 0f;
            for (int i = 0; i < requestedLines.Count; i++)
            {
                ItemAirdropPaymentRequestLine line = requestedLines[i];
                APIResult resolveResult = TryResolvePaymentThingDef(line.ItemText, stockedRecords, out ThingDefRecord resolvedRecord);
                if (!resolveResult.Success)
                {
                    APIResult catalogResolveResult = TryResolvePaymentThingDef(
                        line.ItemText,
                        ThingDefCatalog.GetTradeablePaymentRecords(),
                        out ThingDefRecord catalogResolvedRecord);
                    if (catalogResolveResult.Success && catalogResolvedRecord != null)
                    {
                        Log.Message($"[RimAI.Relations][PaymentPlan] Payment item '{line.ItemText}' resolved globally to '{catalogResolvedRecord.DefName}' but is absent from beacon stock.");
                        return BuildPaymentFailure(
                            "payment_item_insufficient",
                            "RimChat_AirdropError_payment_item_no_beacon_stock".Translate(catalogResolvedRecord.Label).ToString());
                    }

                    Log.Message($"[RimAI.Relations][PaymentPlan] Failed to resolve payment item '{line.ItemText}' against beacon stock: {resolveResult.Message}");
                    return resolveResult;
                }

                if (!buckets.TryGetValue(resolvedRecord.DefName, out List<Thing> stockThings))
                {
                    Log.Message($"[RimAI.Relations][PaymentPlan] No beacon stock for payment item '{resolvedRecord.DefName}' ({line.ItemText}). Available: {string.Join(", ", buckets.Keys)}");
                    return BuildPaymentFailure(
                        "payment_item_insufficient",
                        "RimChat_AirdropError_payment_item_no_beacon_stock".Translate(resolvedRecord.Label).ToString());
                }

                int availableCount = stockThings.Sum(thing => Math.Max(0, thing.stackCount));
                if (availableCount < line.Count)
                {
                    Log.Message($"[RimAI.Relations][PaymentPlan] Insufficient stock for '{resolvedRecord.DefName}': required={line.Count}, available={availableCount}");
                    return BuildPaymentFailure(
                        "payment_item_insufficient",
                        "RimChat_AirdropError_payment_item_insufficient".Translate(resolvedRecord.Label, line.Count, availableCount).ToString());
                }

                float unitPrice = ResolveAirdropPaymentUnitPrice(
                    resolvedRecord,
                    faction,
                    playerNegotiator,
                    map,
                    out string unitPriceFailureCode);
                float subtotal = unitPrice * line.Count;
                totalValueFloat += subtotal;
                paymentLines.Add(new ItemAirdropPreparedPaymentLine
                {
                    RequestedItem = line.ItemText,
                    DefName = resolvedRecord.DefName,
                    Label = resolvedRecord.Label,
                    Count = line.Count,
                    UnitMarketValue = unitPrice,
                    SubtotalMarketValue = subtotal
                });

                Log.Message($"[RimAI.Relations][PaymentPlan] Payment line: {resolvedRecord.DefName} x{line.Count} @ {unitPrice:F1} = {subtotal:F1} silver");

                int remaining = line.Count;
                foreach (Thing thing in stockThings.OrderByDescending(item => item.stackCount))
                {
                    if (remaining <= 0)
                    {
                        break;
                    }

                    int taken = Math.Min(remaining, Math.Max(0, thing.stackCount));
                    if (taken <= 0)
                    {
                        continue;
                    }

                    deductionPlan.Add(new ItemAirdropDeductionPlanLine
                    {
                        ThingId = thing.ThingID,
                        DefName = resolvedRecord.DefName,
                        Count = taken
                    });
                    remaining -= taken;
                }
            }

            int flooredTotalValue = Mathf.FloorToInt(Math.Max(0f, totalValueFloat));
            if (flooredTotalValue <= 0)
            {
                Log.Message($"[RimAI.Relations][PaymentPlan] Derived budget is not positive: total={totalValueFloat:F1}");
                return BuildPaymentFailure(
                    "budget_invalid",
                    $"Derived budget from payment_items is not positive. total={totalValueFloat:F1}.");
            }

            Log.Message($"[RimAI.Relations][PaymentPlan] Payment plan complete: budget={flooredTotalValue} silver, paymentLines={paymentLines.Count}, deductionRows={deductionPlan.Count}");
            derivedBudgetSilver = flooredTotalValue;
            paymentTotalSilver = flooredTotalValue;
            return APIResult.SuccessResult("Payment plan prepared.");
        }

internal static float ResolveAirdropPaymentUnitPrice(
            ThingDefRecord resolvedRecord,
            Faction faction,
            Pawn playerNegotiator,
            Map map,
            out string failureCode)
        {
            _ = faction;
            _ = playerNegotiator;
            _ = map;
            ThingDef def = resolvedRecord?.Def;
            if (ItemAirdropTradePolicy.TryResolveOfferUnitPrice(def, out float resolved, out failureCode))
            {
                return resolved;
            }

            return Math.Max(0.01f, resolvedRecord?.MarketValue ?? 0.01f);
        }

internal APIResult ParsePaymentItems(Dictionary<string, object> parameters, out List<ItemAirdropPaymentRequestLine> lines)
        {
            lines = new List<ItemAirdropPaymentRequestLine>();
            if (parameters == null ||
                !parameters.TryGetValue("payment_items", out object rawItems) ||
                rawItems == null)
            {
                return BuildPaymentFailure("payment_items_missing", "request_item_airdrop requires parameter 'payment_items'.");
            }

            IEnumerable<object> entries = rawItems as IEnumerable<object>;
            if (entries == null)
            {
                return BuildPaymentFailure("payment_items_invalid", "payment_items must be a JSON array.");
            }

            int index = 0;
            foreach (object entry in entries)
            {
                index++;
                if (!(entry is Dictionary<string, object> itemData))
                {
                    return BuildPaymentFailure("payment_items_invalid", $"payment_items[{index}] must be an object.");
                }

                string itemText = ReadDictionaryText(itemData, "item");
                if (string.IsNullOrWhiteSpace(itemText))
                {
                    return BuildPaymentFailure("payment_items_invalid", $"payment_items[{index}] requires non-empty field 'item'.");
                }

                if (!TryReadDictionaryPositiveInt(itemData, "count", out int count))
                {
                    return BuildPaymentFailure("payment_items_invalid", $"payment_items[{index}] requires positive integer field 'count'.");
                }

                lines.Add(new ItemAirdropPaymentRequestLine
                {
                    ItemText = itemText,
                    Count = count
                });
            }

            if (lines.Count == 0)
            {
                return BuildPaymentFailure("payment_items_missing", "payment_items must include at least one item.");
            }

            return APIResult.SuccessResult("Payment items parsed.");
        }

internal static string ReadDictionaryText(Dictionary<string, object> values, string key)
        {
            if (values == null || !values.TryGetValue(key, out object raw) || raw == null)
            {
                return string.Empty;
            }

            return raw.ToString()?.Trim() ?? string.Empty;
        }

internal static bool TryReadDictionaryPositiveInt(Dictionary<string, object> values, string key, out int count)
        {
            count = 0;
            if (values == null || !values.TryGetValue(key, out object raw) || raw == null)
            {
                return false;
            }

            if (raw is int intValue)
            {
                count = intValue;
                return count > 0;
            }

            if (raw is long longValue && longValue <= int.MaxValue && longValue >= int.MinValue)
            {
                count = (int)longValue;
                return count > 0;
            }

            return int.TryParse(raw.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out count) && count > 0;
        }

internal APIResult TryResolvePaymentThingDef(
            string itemText,
            IReadOnlyList<ThingDefRecord> candidateRecords,
            out ThingDefRecord resolvedRecord)
        {
            resolvedRecord = null;
            string query = (itemText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                return BuildPaymentFailure("payment_item_unresolved", "Payment item text cannot be empty.");
            }

            List<ThingDefRecord> records = (candidateRecords ?? Array.Empty<ThingDefRecord>())
                .Where(record => record?.Def != null)
                .GroupBy(record => record.DefName, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
            ItemAirdropPaymentResolveResult resolveResult = ItemAirdropPaymentResolver.Resolve(query, records);
            if (!resolveResult.Success || resolveResult.ResolvedRecord == null)
            {
                string failureCode = string.IsNullOrWhiteSpace(resolveResult?.FailureCode)
                    ? "payment_item_unresolved"
                    : resolveResult.FailureCode;
                string failureMessage = string.IsNullOrWhiteSpace(resolveResult?.FailureMessage)
                    ? $"Payment item '{query}' could not be resolved."
                    : resolveResult.FailureMessage;
                return BuildPaymentFailure(failureCode, failureMessage);
            }

            resolvedRecord = resolveResult.ResolvedRecord;
            return APIResult.SuccessResult("Payment def resolved.");
        }

internal static List<Thing> CollectBeaconTradeableThingsShared(Map map)
        {
            var result = new List<Thing>();
            if (map == null)
            {
                return result;
            }

            List<Building_OrbitalTradeBeacon> beacons = Building_OrbitalTradeBeacon.AllPowered(map)?.ToList();
            if (beacons == null || beacons.Count == 0)
            {
                return result;
            }

            var cells = new HashSet<IntVec3>();
            for (int i = 0; i < beacons.Count; i++)
            {
                Building_OrbitalTradeBeacon beacon = beacons[i];
                if (beacon == null || !beacon.Spawned || beacon.Map != map)
                {
                    continue;
                }

                foreach (IntVec3 cell in beacon.TradeableCells)
                {
                    cells.Add(cell);
                }
            }

            var seenThingIds = new HashSet<int>();
            foreach (IntVec3 cell in cells)
            {
                List<Thing> thingsAt = map.thingGrid.ThingsListAt(cell);
                if (thingsAt == null || thingsAt.Count == 0)
                {
                    continue;
                }

                for (int i = 0; i < thingsAt.Count; i++)
                {
                    Thing thing = thingsAt[i];
                    if (!IsValidBeaconPaymentThingShared(thing) || !seenThingIds.Add(thing.thingIDNumber))
                    {
                        continue;
                    }

                    result.Add(thing);
                }
            }

            return result;
        }

internal static bool IsValidBeaconPaymentThingShared(Thing thing)
        {
            return thing != null &&
                   thing.Spawned &&
                   !thing.Destroyed &&
                   thing.stackCount > 0 &&
                   thing.def != null &&
                   thing.def.category == ThingCategory.Item &&
                   !thing.def.IsCorpse &&
                   TradeUtility.EverPlayerSellable(thing.def) &&
                   !thing.IsForbidden(Faction.OfPlayer);
        }

internal static List<Thing> CollectBeaconTradeableThings(Map map)
        {
            return CollectBeaconTradeableThingsShared(map);
        }

internal static bool IsValidBeaconPaymentThing(Thing thing)
        {
            return IsValidBeaconPaymentThingShared(thing);
        }

internal APIResult ValidateDeductionPlan(
            Map map,
            List<ItemAirdropDeductionPlanLine> plan,
            out List<ThingDeductionReservation> reservations)
        {
            reservations = new List<ThingDeductionReservation>();
            if (map == null)
            {
                return BuildPaymentFailure("map_unavailable", "Commit map is unavailable.");
            }

            if (plan == null || plan.Count == 0)
            {
                return BuildPaymentFailure("payment_plan_invalid", "Deduction plan is empty.");
            }

            foreach (ItemAirdropDeductionPlanLine line in plan)
            {
                if (line == null || string.IsNullOrWhiteSpace(line.ThingId) || line.Count <= 0)
                {
                    return BuildPaymentFailure("payment_plan_invalid", "Deduction plan contains invalid rows.");
                }

                Thing thing = map.listerThings?.AllThings?.FirstOrDefault(item =>
                    item != null &&
                    string.Equals(item.ThingID, line.ThingId, StringComparison.Ordinal));
                if (thing == null || thing.Destroyed || !thing.Spawned)
                {
                    return BuildPaymentFailure("payment_item_insufficient", "RimChat_AirdropError_payment_plan_missing".Translate().ToString());
                }

                if (!string.Equals(thing.def?.defName ?? string.Empty, line.DefName ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    return BuildPaymentFailure("payment_plan_invalid", $"Planned payment stack '{line.ThingId}' no longer matches def '{line.DefName}'.");
                }

                if (thing.stackCount < line.Count)
                {
                    return BuildPaymentFailure(
                        "payment_item_insufficient",
                        "RimChat_AirdropError_payment_plan_insufficient".Translate(line.Count, thing.stackCount).ToString());
                }

                reservations.Add(new ThingDeductionReservation
                {
                    Thing = thing,
                    Count = line.Count
                });
            }

            return APIResult.SuccessResult("Deduction plan validated.");
        }

internal static void ApplyDeductionReservations(List<ThingDeductionReservation> reservations)
        {
            if (reservations == null || reservations.Count == 0)
            {
                return;
            }

            foreach (ThingDeductionReservation reservation in reservations)
            {
                if (reservation?.Thing == null || reservation.Count <= 0)
                {
                    continue;
                }

                reservation.Thing.stackCount -= reservation.Count;
                if (reservation.Thing.stackCount <= 0)
                {
                    reservation.Thing.Destroy(DestroyMode.Vanish);
                }
            }
        }

internal static Dictionary<string, object> CloneParameterDictionary(Dictionary<string, object> source)
        {
            var clone = new Dictionary<string, object>();
            if (source == null)
            {
                return clone;
            }

            foreach (KeyValuePair<string, object> entry in source)
            {
                clone[entry.Key] = entry.Value;
            }

            return clone;
        }

internal static APIResult BuildPaymentFailure(string code, string message)
        {
            return new APIResult
            {
                Success = false,
                Message = $"[{code}] {message}",
                Data = new ItemAirdropResultData { FailureCode = code }
            };
        }

    }

/// <summary>
    /// Dependencies: GameAIInterface item-airdrop core, ThingDefCatalog, Building_OrbitalTradeBeacon.
    /// Responsibility: prepare/commit barter-based airdrop trades with strict beacon-source validation.
    /// </summary>

public sealed class ItemAirdropPreparedTradeData
    {
        public string NeedText { get; set; }
        public string Scenario { get; set; }
        public string SelectedDefName { get; set; }
        public string ResolvedLabel { get; set; }
        public int Quantity { get; set; }
        public int RequestedQuantity { get; set; }
        public int MaxByBudget { get; set; }
        public int MaxBySystem { get; set; }
        public int HardMax { get; set; }
        public string CountAdjustmentReason { get; set; }
        public int BudgetSilver { get; set; }
        public float NeedQuotedUnitSilver { get; set; }
        public int PaymentTotalSilver { get; set; }
        public int PaymentItemTotalSilver { get; set; }
        public int ShippingPodCount { get; set; }
        public int ShippingCostSilver { get; set; }
        public int PaymentOverpaySilver { get; set; }
        public string SelectionReason { get; set; }
        public string NeedPriceSemantic { get; set; } = "market_value_x1.6";
        public string PaymentPriceSemantic { get; set; } = "market_value_x0.6";
        public int MapUniqueId { get; set; }
        public SpecialItemType? SpecialItemType { get; set; }
        public List<ItemAirdropPreparedPaymentLine> PaymentLines { get; set; } = new List<ItemAirdropPreparedPaymentLine>();
        public List<ItemAirdropDeductionPlanLine> DeductionPlan { get; set; } = new List<ItemAirdropDeductionPlanLine>();
        public Dictionary<string, object> ParametersSnapshot { get; set; } = new Dictionary<string, object>();
    }

    public sealed class PreparedMakePeacePaymentData
    {
        public string FactionName { get; set; }
        public string FactionDefName { get; set; }
        public int PeaceCostSilver { get; set; }
        public int PaymentTotalSilver { get; set; }
        public int MapUniqueId { get; set; }
        public List<ItemAirdropPreparedPaymentLine> PaymentLines { get; set; } = new List<ItemAirdropPreparedPaymentLine>();
        public List<ItemAirdropDeductionPlanLine> DeductionPlan { get; set; } = new List<ItemAirdropDeductionPlanLine>();
        public Dictionary<string, object> ParametersSnapshot { get; set; } = new Dictionary<string, object>();
    }

    public sealed class PreparedSendGiftData
    {
        public string FactionName { get; set; }
        public string FactionDefName { get; set; }
        public int SilverAmount { get; set; }
        public int GoodwillGain { get; set; }
        public int PaymentTotalSilver { get; set; }
        public int MapUniqueId { get; set; }
        public List<ItemAirdropPreparedPaymentLine> PaymentLines { get; set; } = new List<ItemAirdropPreparedPaymentLine>();
        public List<ItemAirdropDeductionPlanLine> DeductionPlan { get; set; } = new List<ItemAirdropDeductionPlanLine>();
        public Dictionary<string, object> ParametersSnapshot { get; set; } = new Dictionary<string, object>();
    }

    public sealed class ItemAirdropPreparedPaymentLine
    {
        public string RequestedItem { get; set; }
        public string DefName { get; set; }
        public string Label { get; set; }
        public int Count { get; set; }
        public float UnitMarketValue { get; set; }
        public float SubtotalMarketValue { get; set; }
        public string PriceSemantic { get; set; } = "market_value_x0.6";
    }

    public sealed class ItemAirdropDeductionPlanLine
    {
        public string ThingId { get; set; }
        public string DefName { get; set; }
        public int Count { get; set; }
    }

    internal sealed class ItemAirdropPaymentRequestLine
    {
        public string ItemText { get; set; }
        public int Count { get; set; }
    }

    internal sealed class ThingDeductionReservation
    {
        public Thing Thing { get; set; }
        public int Count { get; set; }
    }
}
