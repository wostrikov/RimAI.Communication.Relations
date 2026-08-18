using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Communication.Relations.Memory;
using RimWorld;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Communication.Relations.UI
{
    using InventoryDisplayEntry = Dialog_ItemAirdropTradeCard.InventoryDisplayEntry;
    internal sealed class ItemAirdropTradeCardSlice2 : Dialog_ItemAirdropTradeCardCollaborator
    {
        internal ItemAirdropTradeCardSlice2(Dialog_ItemAirdropTradeCard owner) : base(owner)
        {
        }

internal void DrawThingDefCardContent(Rect rect, ThingDefRecord record, int count, float unitPrice, float totalPrice, string priceSemantic)
        {
            float contentY = rect.y + 40f;
            Rect iconRect = new Rect(rect.x + 12f, contentY, CardImageSize, CardImageSize);
            if (record.Def.uiIcon != null)
            {
                GUI.DrawTexture(iconRect, record.Def.uiIcon);
            }
            else
            {
                Widgets.DrawBoxSolid(iconRect, new Color(0.15f, 0.15f, 0.18f));
            }

            GUI.color = new Color(0.27f, 0.31f, 0.38f, 0.95f);
            Widgets.DrawBox(iconRect);
            GUI.color = Color.white;

            float textX = iconRect.xMax + 10f;
            float textWidth = rect.width - (textX - rect.x) - 12f;
            float lineHeight = 16f;

            Text.Font = GameFont.Small;
            GUI.color = new Color(0.93f, 0.94f, 0.98f);
            string label = record.Label ?? record.DefName;
            float labelHeight = Mathf.Max(20f, Text.CalcHeight(label, textWidth));
            Widgets.Label(new Rect(textX, contentY, textWidth, labelHeight), label);

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.6f, 0.66f, 0.78f);
            float defNameY = contentY + labelHeight;
            float defNameHeight = Mathf.Max(lineHeight, Text.CalcHeight(record.DefName ?? string.Empty, textWidth));
            Widgets.Label(new Rect(textX, defNameY, textWidth, defNameHeight), record.DefName);

            float metricsY = Mathf.Max(iconRect.yMax - 2f, defNameY + defNameHeight + 2f);
            float halfWidth = textWidth * 0.5f;

            GUI.color = new Color(0.84f, 0.86f, 0.92f);
            Widgets.Label(new Rect(textX, metricsY, halfWidth, lineHeight), "RimChat_Price".Translate() + ": " + unitPrice.ToString("F1", CultureInfo.InvariantCulture));
            Widgets.Label(new Rect(textX + halfWidth, metricsY, halfWidth, lineHeight), "RimChat_StackLimit".Translate() + ": " + record.StackLimit);

            GUI.color = new Color(0.78f, 0.83f, 0.9f);
            Widgets.Label(new Rect(textX, metricsY + lineHeight, halfWidth, lineHeight), "RimChat_AirdropTradeCard_CountLabel".Translate() + ": " + count);
            GUI.color = new Color(0.94f, 0.8f, 0.42f);
            Widgets.Label(new Rect(textX + halfWidth, metricsY + lineHeight, halfWidth, lineHeight), "RimChat_AirdropTradeCard_TotalPriceLabel".Translate() + ": " + totalPrice.ToString("F1", CultureInfo.InvariantCulture));

            GUI.color = new Color(0.72f, 0.78f, 0.9f);
            float semanticY = metricsY + lineHeight * 2f;
            float semanticHeight = Mathf.Max(lineHeight, Text.CalcHeight("RimChat_AirdropTradeCard_PriceSemanticLabel".Translate(Dialog_ItemAirdropTradeCard.BuildPriceSemanticTag(priceSemantic)).ToString(), textWidth));
            Widgets.Label(new Rect(textX, semanticY, textWidth, semanticHeight), "RimChat_AirdropTradeCard_PriceSemanticLabel".Translate(Dialog_ItemAirdropTradeCard.BuildPriceSemanticTag(priceSemantic)).ToString());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

internal void DrawInventoryPanel(Rect rect)
        {
            Dialog_ItemAirdropTradeCard.DrawPanel(rect, new Color(0.08f, 0.08f, 0.11f, 0.98f));
            Owner.DrawInventorySearchBar(new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, 24f));
            GUI.color = new Color(0.74f, 0.8f, 0.9f, 0.95f);
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 38f, rect.width - 20f, 16f), Owner.ResolveSelectedOfferLabel());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            Rect listRect = new Rect(rect.x + 4f, rect.y + 58f, rect.width - 8f, rect.height - 62f);
            if (isLoadingInventory)
            {
                Owner.DrawLoadingIndicator(listRect);
                return;
            }

            if (filteredInventoryItems.Count == 0)
            {
                string emptyKey = inventoryItems.Count == 0 ? "RimChat_AirdropTradeCard_NoInventory" : "RimChat_AirdropTradeCard_NoSuggestions";
                Widgets.Label(new Rect(listRect.x + 8f, listRect.y + 8f, listRect.width - 16f, 24f), emptyKey.Translate());
                return;
            }

            float contentHeight = Math.Max(1f, filteredInventoryItems.Count * InventoryRowHeight);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, contentHeight);
            inventoryScrollPos = GUI.BeginScrollView(listRect, inventoryScrollPos, viewRect);
            float rowY = 0f;
            foreach (InventoryDisplayEntry entry in filteredInventoryItems)
            {
                Owner.DrawInventoryRow(entry, viewRect.width, rowY);
                rowY += InventoryRowHeight;
            }

            GUI.EndScrollView();
        }

internal void DrawInventorySearchBar(Rect rect)
        {
            Widgets.Label(new Rect(rect.x, rect.y, 60f, 22f), "RimChat_Search".Translate());
            Rect inputRect = new Rect(rect.x + 60f, rect.y, rect.width - 60f, 22f);
            Widgets.DrawBoxSolid(inputRect, new Color(0.15f, 0.15f, 0.19f));
            string newText = Widgets.TextField(inputRect, inventorySearchText ?? string.Empty);
            if (!string.Equals(newText, inventorySearchText, StringComparison.Ordinal))
            {
                inventorySearchText = newText;
                Owner.ApplyInventoryFilter();
                Owner.EnsureOfferSelectionState();
            }
        }

internal void DrawLoadingIndicator(Rect rect)
        {
            float barWidth = rect.width * 0.58f;
            Rect progressRect = new Rect(rect.x + (rect.width - barWidth) * 0.5f, rect.y + rect.height * 0.42f, barWidth, 8f);
            Widgets.DrawBoxSolid(progressRect, new Color(0.2f, 0.2f, 0.24f));
            Widgets.DrawBoxSolid(new Rect(progressRect.x, progressRect.y, progressRect.width * inventoryLoadProgress, progressRect.height), new Color(0.38f, 0.58f, 0.84f, 0.85f));
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.75f, 0.77f, 0.82f);
            Widgets.Label(new Rect(rect.x, progressRect.yMax + 8f, rect.width, 18f), "RimChat_Loading".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

internal void DrawInventoryRow(InventoryDisplayEntry entry, float width, float y)
        {
            Rect rowRect = new Rect(2f, y, width - 4f, InventoryRowHeight - 2f);
            bool selected = string.Equals(selectedOfferDefName, entry.DefName, StringComparison.OrdinalIgnoreCase);
            Widgets.DrawBoxSolid(rowRect, selected ? new Color(0.19f, 0.39f, 0.63f, 0.82f) : new Color(0.12f, 0.12f, 0.16f, 0.82f));
            if (selected)
            {
                GUI.color = new Color(0.46f, 0.62f, 0.92f, 0.95f);
                Widgets.DrawBox(rowRect);
                GUI.color = Color.white;
            }

            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(entry.DefName);
            Rect iconRect = new Rect(rowRect.x + 6f, rowRect.y + 10f, 20f, 20f);
            if (def?.uiIcon != null)
            {
                GUI.DrawTexture(iconRect, def.uiIcon);
            }

            float textX = iconRect.xMax + 8f;
            float textWidth = rowRect.width - (textX - rowRect.x) - 8f;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(textX, rowRect.y + 5f, textWidth * 0.62f, 16f), $"{entry.Label} ({entry.DefName})");
            GUI.color = new Color(0.72f, 0.78f, 0.9f);
            Widgets.Label(new Rect(textX, rowRect.y + 24f, textWidth * 0.32f, 16f), "x" + entry.Count.ToString(CultureInfo.InvariantCulture));
            GUI.color = new Color(0.94f, 0.8f, 0.42f);
            Widgets.Label(new Rect(textX + textWidth * 0.62f, rowRect.y + 24f, textWidth * 0.38f, 16f), "@" + entry.UnitPrice.ToString("F0", CultureInfo.InvariantCulture) + " (" + Dialog_ItemAirdropTradeCard.BuildPriceSemanticTag(entry.PriceSemantic) + ")");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            if (Widgets.ButtonInvisible(rowRect))
            {
                Owner.ApplyOfferSelection(entry);
            }
        }

internal void DrawFooter(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.1f, 0.1f, 0.13f));

            Rect statRect = new Rect(rect.x + 12f, rect.y + 8f, rect.width * 0.58f, 38f);
            Owner.DrawReferencePriceBlock(statRect);

            float inputWidth = rect.width * 0.55f;
            Owner.DrawFooterInputs(new Rect(rect.x + 12f, rect.y + 50f, inputWidth, 26f));

            Owner.DrawTradeRulesInfo(new Rect(rect.x + 12f, rect.y + 82f, rect.width - 24f, 28f));

            string failReason = Owner.GetSubmitDisabledReason();
            if (!string.IsNullOrWhiteSpace(failReason))
            {
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.9f, 0.74f, 0.32f);
                Widgets.Label(new Rect(rect.x + 12f, rect.y + 114f, rect.width * 0.82f, 16f), failReason);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }

            float buttonWidth = 160f;
            Rect cancelRect = new Rect(rect.xMax - buttonWidth - 12f, rect.yMax - 36f, buttonWidth, 28f);
            Rect submitRect = new Rect(cancelRect.x - buttonWidth - 10f, cancelRect.y, buttonWidth, 28f);
            bool canSubmit = Owner.CanSubmit();
            GUI.enabled = canSubmit;
            if (Widgets.ButtonText(submitRect, "RimChat_AirdropTradeCard_Submit".Translate()))
            {
                Owner.Submit();
            }

            GUI.enabled = true;
            if (Widgets.ButtonText(cancelRect, "RimChat_AirdropTradeCard_Cancel".Translate()))
            {
                Close();
            }
        }

internal void DrawTradeRulesInfo(Rect rect)
        {
            Text.Font = GameFont.Tiny;
            AirdropTradeRuleSnapshot tradeRule = Owner.ResolveTradeRuleSnapshot();
            float offerTotal = Owner.ComputeOfferTotal();
            int podCount = Owner.ComputePodCount();
            int shippingCost = podCount * tradeRule.ShippingCostPerPod;
            int factionTradeTotal = Mathf.RoundToInt(GameAIInterface.Instance.GetAirdropFactionTradeTotalForPolicy(faction));
            int tradeGrowthDelta = tradeRule.TradeGrowthDeltaSilver;

            bool limitExceeded = offerTotal > tradeRule.TradeLimitSilver;
            GUI.color = limitExceeded ? new Color(0.95f, 0.35f, 0.35f) : new Color(0.72f, 0.82f, 0.72f);
            string limitText = "RimChat_AirdropTradeCard_TradeLimit".Translate(
                tradeRule.Goodwill,
                tradeRule.TradeLimitSilver,
                Dialog_ItemAirdropTradeCard.FormatTradeAmountCompact(factionTradeTotal),
                tradeGrowthDelta >= 0 ? $"+{tradeGrowthDelta}" : tradeGrowthDelta.ToString(CultureInfo.InvariantCulture)).ToString();
            Widgets.Label(new Rect(rect.x, rect.y, rect.width * 0.9f, 16f), limitText);

            GUI.color = new Color(0.82f, 0.82f, 0.65f);
            string podText = "RimChat_AirdropTradeCard_PodInfo".Translate(podCount, shippingCost, tradeRule.ShippingCostPerPod).ToString();
            Widgets.Label(new Rect(rect.x, rect.y + 16f, rect.width * 0.9f, 16f), podText);

            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

internal void DrawReferencePriceBlock(Rect rect)
        {
            Dialog_ItemAirdropTradeCard.DrawPanel(rect, new Color(0.11f, 0.11f, 0.15f, 0.96f));
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.74f, 0.78f, 0.88f);
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 6f, 120f, 14f), "RimChat_AirdropTradeCard_ReferencePriceLabel".Translate());
            Text.Font = GameFont.Small;
            GUI.color = boundNeedRecord?.Def == null ? new Color(0.64f, 0.66f, 0.72f) : new Color(0.96f, 0.82f, 0.4f);
            string value = boundNeedRecord?.Def == null
                ? "RimChat_AirdropTradeCard_ReferencePriceEmpty".Translate().ToString()
                : Owner.BuildReferencePriceFormulaText();
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 18f, rect.width - 20f, 20f), value);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

internal string BuildReferencePriceFormulaText()
        {
            int needTotal = Mathf.RoundToInt(Owner.ComputeNeedReferenceTotal());
            int podCount = Owner.ComputePodCount();
            AirdropTradeRuleSnapshot tradeRule = Owner.ResolveTradeRuleSnapshot();
            int referencePrice = needTotal + podCount * tradeRule.ShippingCostPerPod;
            int currentOffer = Mathf.RoundToInt(Owner.ComputeOfferTotal());
            return "RimChat_AirdropTradeCard_ReferencePriceFormula".Translate(
                needTotal,
                podCount,
                tradeRule.ShippingCostPerPod,
                referencePrice,
                currentOffer).ToString();
        }

internal static string FormatTradeAmountCompact(int amount)
        {
            int safe = Math.Max(0, amount);
            if (safe >= 1000)
            {
                return (safe / 1000f).ToString("0.#", CultureInfo.InvariantCulture) + "k";
            }

            return safe.ToString(CultureInfo.InvariantCulture);
        }

internal void DrawFooterInputs(Rect rect)
        {
            float groupWidth = (rect.width - 12f) * 0.5f;
            string nextRequested = requestedCountText;
            string nextOffer = offerCountText;
            Dialog_ItemAirdropTradeCard.DrawIntegerField(new Rect(rect.x, rect.y, groupWidth, rect.height), "RimChat_AirdropTradeCard_RequestCountLabel".Translate().ToString(), nextRequested, out nextRequested, 1, 100000);
            Dialog_ItemAirdropTradeCard.DrawIntegerField(new Rect(rect.x + groupWidth + 12f, rect.y, groupWidth, rect.height), "RimChat_AirdropTradeCard_OfferCountLabel".Translate().ToString(), nextOffer, out nextOffer, 1, 1000000);
            requestedCountText = nextRequested;
            offerCountText = nextOffer;
        }

internal static void DrawIntegerField(Rect rect, string label, string current, out string updated, int min, int max)
        {
            updated = current;
            float labelWidth = Mathf.Min(104f, rect.width * 0.42f);
            Widgets.Label(new Rect(rect.x, rect.y + 2f, labelWidth, 22f), label);
            Rect fieldRect = new Rect(rect.x + labelWidth + 4f, rect.y, rect.width - labelWidth - 4f, 24f);
            Widgets.DrawBoxSolid(fieldRect, new Color(0.17f, 0.17f, 0.21f));
            string input = Widgets.TextField(fieldRect, current ?? string.Empty);
            if (!int.TryParse(input, out int parsed))
            {
                return;
            }

            if (parsed < min || parsed > max)
            {
                return;
            }

            updated = parsed.ToString(CultureInfo.InvariantCulture);
        }

internal int ComputePodCount()
        {
            if (boundNeedRecord?.Def == null)
            {
                return 0;
            }

            int needCount = Dialog_ItemAirdropTradeCard.ParsePositiveInt(requestedCountText, 1);
            int stackLimit = Math.Max(1, boundNeedRecord.Def.stackLimit);
            return (int)Math.Ceiling((double)needCount / stackLimit);
        }

internal string GetSubmitDisabledReason()
        {
            if (boundNeedRecord?.Def == null || string.IsNullOrWhiteSpace(boundNeedRecord.DefName))
            {
                return "RimChat_AirdropTradeCard_SubmitDisabledNeed".Translate().ToString();
            }

            if (string.IsNullOrWhiteSpace(selectedOfferDefName))
            {
                return "RimChat_AirdropTradeCard_SubmitDisabledOffer".Translate().ToString();
            }

            if (!int.TryParse(requestedCountText, out int requestedCount) || requestedCount <= 0)
            {
                return "RimChat_AirdropTradeCard_SubmitDisabledRequestCount".Translate().ToString();
            }

            if (!int.TryParse(offerCountText, out int offerCount) || offerCount <= 0)
            {
                return "RimChat_AirdropTradeCard_SubmitDisabledOfferCount".Translate().ToString();
            }

            return string.Empty;
        }

internal void Submit()
        {
            if (!Owner.CanSubmit())
            {
                return;
            }

            int requestedCount = Dialog_ItemAirdropTradeCard.ParsePositiveInt(requestedCountText, 1);
            int offerCount = Dialog_ItemAirdropTradeCard.ParsePositiveInt(offerCountText, 1);

            string validationFailure = Owner.ValidateBeforeSubmit(offerCount);
            if (!string.IsNullOrWhiteSpace(validationFailure))
            {
                Owner.ShowValidationFailureDialog(validationFailure);
                return;
            }

            int podCount = Owner.ComputePodCount();
            AirdropTradeRuleSnapshot tradeRule = Owner.ResolveTradeRuleSnapshot();
            int shippingCost = podCount * tradeRule.ShippingCostPerPod;
            float needUnitPrice = Owner.ResolveNeedUnitPrice();
            var payload = new ItemAirdropTradeCardPayload
            {
                Need = string.IsNullOrWhiteSpace(boundNeedRecord.Label)
                    ? $"{boundNeedRecord.DefName} x{requestedCount}"
                    : $"{boundNeedRecord.Label} x{requestedCount}",
                RequestedCount = requestedCount,
                OfferItemDefName = selectedOfferDefName,
                OfferItemLabel = selectedOfferLabel,
                OfferItemCount = offerCount,
                Scenario = "trade",
                NeedDefName = boundNeedRecord.DefName,
                NeedLabel = boundNeedRecord.Label,
                NeedSearchText = boundNeedRecord.SearchText,
                NeedUnitPrice = needUnitPrice,
                NeedReferenceTotalPrice = Owner.ComputeNeedReferenceTotal(),
                OfferUnitPrice = selectedOfferUnitPrice,
                OfferTotalPrice = Owner.ComputeOfferTotal(),
                ShippingPodCount = podCount,
                ShippingCostSilver = shippingCost
            };

            onSubmitted?.Invoke(payload);
            Close();
        }

internal void BindNeedRecord(ThingDefRecord record)
        {
            if (record?.Def == null)
            {
                return;
            }

            boundNeedRecord = record;
            searchState.TryBindToRecord(record);
            needSearchText = record.Label;
            showInlineSuggestions = false;
        }

internal void ApplyPendingInventoryLoadIfReady()
        {
            if (!inventoryLoadCompleted)
            {
                return;
            }

            inventoryLoadCompleted = false;
            inventoryItems.Clear();
            if (pendingInventoryItems != null && pendingInventoryItems.Count > 0)
            {
                inventoryItems.AddRange(pendingInventoryItems);
            }

            pendingInventoryItems = null;
            Owner.ApplyInventoryFilter();
            Owner.EnsureOfferSelectionState();
            inventoryLoadProgress = 1f;
            isLoadingInventory = false;
        }

internal InventoryDisplayEntry FindInventoryEntryByDefName(string defName)
        {
            if (string.IsNullOrWhiteSpace(defName))
            {
                return null;
            }

            return inventoryItems.FirstOrDefault(entry =>
                string.Equals(entry.DefName, defName, StringComparison.OrdinalIgnoreCase));
        }

internal void ClearOfferSelection()
        {
            selectedOfferDefName = string.Empty;
            selectedOfferLabel = string.Empty;
            selectedOfferStackLimit = 1;
            selectedOfferUnitPrice = 1f;
            selectedOfferPriceSemantic = string.Empty;
        }

internal void ClearNeedBinding()
        {
            boundNeedRecord = null;
            searchState.ClearBinding();
        }

internal void ApplyOfferSelection(InventoryDisplayEntry entry)
        {
            selectedOfferDefName = entry.DefName;
            selectedOfferLabel = entry.Label;
            selectedOfferStackLimit = entry.StackLimit;
            selectedOfferUnitPrice = entry.UnitPrice;
            selectedOfferPriceSemantic = entry.PriceSemantic ?? string.Empty;
        }
    }
}
