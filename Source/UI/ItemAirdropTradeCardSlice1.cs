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
    internal sealed class ItemAirdropTradeCardSlice1 : Dialog_ItemAirdropTradeCardCollaborator
    {
        internal ItemAirdropTradeCardSlice1(Dialog_ItemAirdropTradeCard owner) : base(owner)
        {
        }

internal void ApplyCounterofferDefaults()
        {
            if (session?.lastAirdropCounterofferCount > 0)
            {
                requestedCountText = session.lastAirdropCounterofferCount.ToString(CultureInfo.InvariantCulture);
            }

            if (session?.lastAirdropCounterofferSilver > 0)
            {
                Owner.ForceSelectSilverAsOffer();
                offerCountText = session.lastAirdropCounterofferSilver.ToString(CultureInfo.InvariantCulture);
            }
        }

internal void ForceSelectSilverAsOffer()
        {
            InventoryDisplayEntry silver = Owner.FindInventoryEntryByDefName("Silver");
            if (silver != null)
            {
                Owner.ApplyOfferSelection(silver);
            }
        }

internal void EnsureOfferSelectionState()
        {
            InventoryDisplayEntry selectedEntry = Owner.FindInventoryEntryByDefName(selectedOfferDefName);
            if (selectedEntry != null)
            {
                Owner.ApplyOfferSelection(selectedEntry);
                return;
            }

            InventoryDisplayEntry fallback = Owner.FindInventoryEntryByDefName("Silver") ?? inventoryItems.FirstOrDefault();
            if (fallback == null)
            {
                Owner.ClearOfferSelection();
                return;
            }

            Owner.ApplyOfferSelection(fallback);
        }

internal AirdropTradeRuleSnapshot ResolveTradeRuleSnapshot()
        {
            return ItemAirdropTradePolicy.ResolveRuleSnapshot(
                faction,
                Find.AnyPlayerHomeMap?.wealthWatcher?.WealthItems ?? 0f,
                GameAIInterface.Instance.GetAirdropFactionTradeTotalForPolicy(faction));
        }

internal void LoadInventoryItemsAsync()
        {
            isLoadingInventory = true;
            inventoryLoadCompleted = false;
            inventoryLoadProgress = 0f;
            TechLevel factionTechLevel = faction?.def?.techLevel ?? TechLevel.Archotech;
            LongEventHandler.QueueLongEvent(() =>
            {
                var loadedItems = new List<InventoryDisplayEntry>();
                Map map = Find.AnyPlayerHomeMap ?? Find.CurrentMap;
                if (map != null)
                {
                    List<Thing> tradeables = Dialog_ItemAirdropTradeCard.CollectBeaconTradeableThings(map);
                    inventoryLoadProgress = 0.35f;
                    loadedItems.AddRange(tradeables
                        .Where(thing => thing?.def != null && Dialog_ItemAirdropTradeCard.IsWithinFactionTechLevel(thing.def, factionTechLevel))
                        .GroupBy(thing => thing.def.defName)
                        .Select(group => new InventoryDisplayEntry
                        {
                            DefName = group.Key,
                            Label = group.First().def.label ?? group.Key,
                            Count = group.Sum(thing => Math.Max(0, thing.stackCount)),
                            UnitPrice = Owner.ResolveOfferDisplayUnitPrice(group.First().def),
                            StackLimit = Math.Max(1, group.First().def.stackLimit),
                            PriceSemantic = Owner.ResolveOfferDisplayPriceSemantic(group.First().def)
                        })
                        .Where(entry => entry.Count > 0)
                        .OrderByDescending(entry => entry.Count)
                        .ThenBy(entry => entry.Label)
                        .ToList());
                }

                inventoryLoadProgress = 0.8f;
                pendingInventoryItems = loadedItems;
                inventoryLoadCompleted = true;
            }, "LoadingInventory", false, null);
        }

internal static bool IsWithinFactionTechLevel(ThingDef def, TechLevel factionTechLevel)
        {
            if (def == null)
            {
                return false;
            }
            // Items with techLevel == 0 (undefined) are always allowed
            if (def.techLevel == TechLevel.Undefined || def.techLevel == 0)
            {
                return true;
            }
            return def.techLevel <= factionTechLevel;
        }

internal float ResolveOfferDisplayUnitPrice(ThingDef def)
        {
            if (def == null)
            {
                return 0.01f;
            }

            if (ItemAirdropTradePolicy.TryResolveOfferUnitPrice(def, out float resolved, out _))
            {
                return Math.Max(0.01f, resolved);
            }

            return Math.Max(0.01f, def.BaseMarketValue);
        }

internal string ResolveOfferDisplayPriceSemantic(ThingDef def)
        {
            if (def == null)
            {
                return string.Empty;
            }

            return ItemAirdropTradePolicy.ResolveOfferPriceSemantic(def);
        }

internal void ApplyInventoryFilter()
        {
            filteredInventoryItems.Clear();
            if (string.IsNullOrWhiteSpace(inventorySearchText))
            {
                filteredInventoryItems.AddRange(inventoryItems);
                return;
            }

            string normalized = inventorySearchText.Trim().ToLowerInvariant();
            filteredInventoryItems.AddRange(inventoryItems.Where(item =>
                item.Label.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0 ||
                item.DefName.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0));
        }

public void DoWindowContents(Rect inRect)
        {
            Owner.ApplyPendingInventoryLoadIfReady();

            float y = inRect.y;
            Rect titleRect = new Rect(inRect.x, y, inRect.width, TitleHeight);
            Owner.DrawTitle(titleRect);
            y += TitleHeight + Padding;

            Rect searchRect = new Rect(inRect.x, y, inRect.width, SearchAreaHeight);
            Owner.DrawSearchArea(searchRect);
            y += SearchAreaHeight + Padding;

            if (showInlineSuggestions && searchState.Suggestions.Count > 0)
            {
                float suggestionHeight = SuggestionRowHeight * Math.Min(searchState.Suggestions.Count, 6);
                Rect suggestionRect = new Rect(inRect.x, y, inRect.width, suggestionHeight);
                Owner.DrawInlineSuggestionDropDown(suggestionRect);
                y += suggestionHeight + Padding;
            }

            float bodyHeight = inRect.height - (y - inRect.y) - FooterHeight - Padding;
            float cardHeight = 150f;
            Rect cardsRect = new Rect(inRect.x, y, inRect.width, cardHeight);
            Owner.DrawItemCards(cardsRect);
            y += cardHeight + Padding;

            Rect inventoryRect = new Rect(inRect.x, y, inRect.width, Mathf.Max(140f, bodyHeight - cardHeight - Padding));
            Owner.DrawInventoryPanel(inventoryRect);

            Rect footerRect = new Rect(inRect.x, inRect.yMax - FooterHeight, inRect.width, FooterHeight);
            Owner.DrawFooter(footerRect);
        }

internal void DrawTitle(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.14f, 0.14f, 0.18f));
            float textWidth = rect.width - 28f;
            Text.Font = GameFont.Medium;
            float titleHeight = Mathf.Max(26f, Text.CalcHeight("RimChat_AirdropTradeCard_Title".Translate(), textWidth));
            GUI.color = new Color(0.95f, 0.95f, 0.98f);
            Widgets.Label(new Rect(rect.x + 14f, rect.y + 6f, textWidth, titleHeight), "RimChat_AirdropTradeCard_Title".Translate());

            Text.Font = GameFont.Tiny;
            string hint = "RimChat_AirdropTradeCard_TitleHint".Translate().ToString();
            float hintY = rect.y + 8f + titleHeight;
            float hintHeight = Mathf.Max(14f, Text.CalcHeight(hint, textWidth));
            GUI.color = new Color(0.68f, 0.72f, 0.82f);
            Widgets.Label(new Rect(rect.x + 14f, hintY, textWidth, hintHeight), hint);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

internal void DrawSearchArea(Rect rect)
        {
            Dialog_ItemAirdropTradeCard.DrawPanel(rect, new Color(0.09f, 0.09f, 0.12f, 0.98f));
            Rect labelRect = new Rect(rect.x + 12f, rect.y + 8f, 90f, 20f);
            Widgets.Label(labelRect, "RimChat_AirdropTradeCard_NeedLabel".Translate());

            Rect inputRect = new Rect(rect.x + 104f, rect.y + 6f, rect.width - 116f, 28f);
            Widgets.DrawBoxSolid(inputRect, new Color(0.15f, 0.15f, 0.19f));
            string newText = Widgets.TextField(inputRect, needSearchText ?? string.Empty);
            if (!string.Equals(newText, needSearchText, StringComparison.Ordinal))
            {
                needSearchText = newText;
                if (!searchState.IsSearchTextStillMatchingBinding(needSearchText))
                {
                    Owner.ClearNeedBinding();
                }

                if (string.IsNullOrWhiteSpace(needSearchText))
                {
                    showInlineSuggestions = false;
                    searchState.ClearSuggestions();
                }
                else
                {
                    TechLevel factionTech = faction?.def?.techLevel ?? TechLevel.Archotech;
                    searchState.ComputeSuggestions(needSearchText, null, factionTech);
                    showInlineSuggestions = searchState.Suggestions.Count > 0;
                }
            }

            Rect statusRect = new Rect(rect.x + 12f, rect.y + 40f, rect.width - 24f, rect.height - 46f);
            Owner.DrawNeedBindingStatus(statusRect);
        }

internal void DrawNeedBindingStatus(Rect rect)
        {
            Text.Font = GameFont.Tiny;
            if (boundNeedRecord?.Def == null)
            {
                GUI.color = new Color(0.88f, 0.72f, 0.3f);
                Widgets.Label(rect, "RimChat_AirdropTradeCard_NeedBindingMissing".Translate());
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                return;
            }

            GUI.color = new Color(0.62f, 0.85f, 0.62f);
            string text = "RimChat_AirdropTradeCard_NeedBindingReady".Translate(boundNeedRecord.Label, boundNeedRecord.DefName).ToString();
            Widgets.Label(rect, text);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

internal void DrawInlineSuggestionDropDown(Rect rect)
        {
            Dialog_ItemAirdropTradeCard.DrawPanel(rect, new Color(0.1f, 0.1f, 0.14f, 0.98f));
            float rowY = rect.y + 3f;
            for (int i = 0; i < searchState.Suggestions.Count && i < 6; i++)
            {
                ThingDefRecord record = searchState.Suggestions[i];
                Rect rowRect = new Rect(rect.x + 4f, rowY, rect.width - 8f, SuggestionRowHeight - 2f);
                bool hovered = Mouse.IsOver(rowRect);
                Widgets.DrawBoxSolid(rowRect, hovered ? new Color(0.25f, 0.37f, 0.55f, 0.82f) : new Color(0.12f, 0.12f, 0.16f, 0.76f));
                if (Widgets.ButtonInvisible(rowRect))
                {
                    Owner.BindNeedRecord(record);
                }

                Text.Font = GameFont.Tiny;
                GUI.color = hovered ? Color.white : new Color(0.88f, 0.9f, 0.94f);
                Widgets.Label(new Rect(rowRect.x + 8f, rowRect.y + 2f, rowRect.width - 16f, 16f), record.Label);
                GUI.color = new Color(0.62f, 0.68f, 0.8f);
                Widgets.Label(new Rect(rowRect.x + 8f, rowRect.y + 18f, rowRect.width - 16f, 16f), record.DefName);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                rowY += SuggestionRowHeight;
            }
        }

internal void DrawItemCards(Rect rect)
        {
            float halfWidth = (rect.width - Padding) * 0.5f;
            Owner.DrawNeedItemCard(new Rect(rect.x, rect.y, halfWidth, rect.height));
            Owner.DrawOfferItemCard(new Rect(rect.x + halfWidth + Padding, rect.y, halfWidth, rect.height));
        }

internal void DrawNeedItemCard(Rect rect)
        {
            Dialog_ItemAirdropTradeCard.DrawPanel(rect, new Color(0.07f, 0.09f, 0.11f, 0.98f));
            Owner.DrawCardHeader(rect, "RimChat_AirdropTradeCard_NeedItemCard");
            if (boundNeedRecord?.Def == null)
            {
                Owner.DrawEmptyCard(rect, "RimChat_AirdropTradeCard_NoNeedItemBound");
                return;
            }

            // Determine price semantic based on whether this is a special item
            string needPriceSemantic = Owner.ResolveNeedPriceSemantic();

            Owner.DrawThingDefCardContent(
                rect,
                boundNeedRecord,
                Math.Max(1, Dialog_ItemAirdropTradeCard.ParsePositiveInt(requestedCountText, 1)),
                Owner.ResolveNeedUnitPrice(),
                Owner.ComputeNeedReferenceTotal(),
                needPriceSemantic);
        }

internal string ResolveNeedPriceSemantic()
        {
            if (boundNeedRecord?.Def == null) return "market_value";
            return ItemAirdropTradePolicy.ResolveNeedPriceSemantic(boundNeedRecord.Def, faction);
        }

internal void DrawOfferItemCard(Rect rect)
        {
            Dialog_ItemAirdropTradeCard.DrawPanel(rect, new Color(0.07f, 0.09f, 0.11f, 0.98f));
            Owner.DrawCardHeader(rect, "RimChat_AirdropTradeCard_OfferItemCard");
            ThingDef offerDef = DefDatabase<ThingDef>.GetNamedSilentFail(selectedOfferDefName);
            if (offerDef == null)
            {
                Owner.DrawEmptyCard(rect, "RimChat_AirdropTradeCard_NoOfferItem");
                return;
            }

            ThingDefRecord record = ThingDefRecord.From(offerDef);
            Owner.DrawThingDefCardContent(
                rect,
                record,
                Math.Max(1, Dialog_ItemAirdropTradeCard.ParsePositiveInt(offerCountText, 1)),
                selectedOfferUnitPrice,
                Owner.ComputeOfferTotal(),
                selectedOfferPriceSemantic);
        }

internal void DrawCardHeader(Rect rect, string key)
        {
            GUI.color = new Color(0.25f, 0.29f, 0.35f, 0.95f);
            Widgets.DrawBox(new Rect(rect.x, rect.y, rect.width, rect.height));
            GUI.color = Color.white;
            Rect headerRect = new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, 20f);
            Widgets.Label(headerRect, key.Translate());
        }

internal void DrawEmptyCard(Rect rect, string key)
        {
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.64f, 0.66f, 0.72f, 0.92f);
            Widgets.Label(new Rect(rect.x + 12f, rect.y + 44f, rect.width - 24f, 36f), key.Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

internal static string BuildPriceSemanticTag(string semantic)
        {
            if (string.IsNullOrWhiteSpace(semantic))
            {
                return "RimChat_ItemAirdropPriceSemanticMarket".Translate().ToString();
            }

            // Special item pricing semantics (now dynamic with multiplier suffix)
            if (semantic.StartsWith("special_item_discount", StringComparison.OrdinalIgnoreCase))
            {
                return "RimChat_ItemAirdropPriceSemanticDiscount".Translate().ToString();
            }

            if (semantic.StartsWith("special_item_scarce", StringComparison.OrdinalIgnoreCase))
            {
                return "RimChat_ItemAirdropPriceSemanticScarce".Translate().ToString();
            }

            if (semantic.StartsWith("market_value_x", StringComparison.OrdinalIgnoreCase))
            {
                // Extract the multiplier from the tag
                string suffix = semantic.Substring("market_value_x".Length);
                return "Market x" + suffix;
            }

            if (semantic.StartsWith("untradeable_", StringComparison.OrdinalIgnoreCase))
            {
                return "RimChat_ItemAirdropPriceSemanticBlackMarket".Translate().ToString();
            }

            if (string.Equals(semantic, "player_buy", StringComparison.OrdinalIgnoreCase))
            {
                return "RimChat_ItemAirdropPriceSemanticBuy".Translate().ToString();
            }

            if (string.Equals(semantic, "player_sell", StringComparison.OrdinalIgnoreCase))
            {
                return "RimChat_ItemAirdropPriceSemanticSell".Translate().ToString();
            }

            return "RimChat_ItemAirdropPriceSemanticMarket".Translate().ToString();
        }
    }
}
