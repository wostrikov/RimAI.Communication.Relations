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
    public class Dialog_ItemAirdropTradeCard : Window
    {
        internal Dialog_ItemAirdropTradeCardParts Parts;

        internal readonly FactionDialogueSession session;
        internal readonly Faction faction;
        internal readonly Action<ItemAirdropTradeCardPayload> onSubmitted;

        internal readonly SearchStateManager searchState = new SearchStateManager();
        internal readonly List<InventoryDisplayEntry> inventoryItems = new List<InventoryDisplayEntry>();
        internal readonly List<InventoryDisplayEntry> filteredInventoryItems = new List<InventoryDisplayEntry>();
        internal List<InventoryDisplayEntry> pendingInventoryItems;

        internal string needSearchText = string.Empty;
        internal string requestedCountText = "1";
        internal string offerCountText = "200";
        internal string inventorySearchText = string.Empty;
        internal string selectedOfferDefName = string.Empty;
        internal string selectedOfferLabel = string.Empty;
        internal int selectedOfferStackLimit = 1;
        internal float selectedOfferUnitPrice = 1f;
        internal string selectedOfferPriceSemantic = "market_value_x0.6";
        internal Vector2 inventoryScrollPos = Vector2.zero;
        internal ThingDefRecord boundNeedRecord;
        internal bool showInlineSuggestions;
        internal bool isLoadingInventory;
        internal float inventoryLoadProgress;
        internal bool inventoryLoadCompleted;

        internal const float TitleHeight = 62f;
        internal const float SearchAreaHeight = 76f;
        internal const float SuggestionRowHeight = 38f;
        internal const float FooterHeight = 164f;
        internal const float Padding = 12f;
        internal const float InventoryRowHeight = 46f;
        internal const float CardImageSize = 54f;
        public override Vector2 InitialSize => new Vector2(960f, 700f);

        public Dialog_ItemAirdropTradeCard(
            FactionDialogueSession session,
            Faction faction,
            Action<ItemAirdropTradeCardPayload> onSubmitted)
        {
            Parts = new Dialog_ItemAirdropTradeCardParts(this);
            this.session = session;
            this.faction = faction;
            this.onSubmitted = onSubmitted;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            doCloseX = true;
            closeOnAccept = false;
            forcePause = true;
            draggable = true;
            LoadInventoryItemsAsync();
        }

        public override void PreOpen()
        {
            base.PreOpen();
            ApplyPendingInventoryLoadIfReady();
            ApplyCounterofferDefaults();
            EnsureOfferSelectionState();
        }

        

        

        

        

        

        

        

        

        

        internal static List<Thing> CollectBeaconTradeableThings(Map map)
        {
            return GameAIInterface.CollectBeaconTradeableThingsShared(map);
        }

        internal static bool IsValidBeaconPaymentThing(Thing thing)
        {
            return GameAIInterface.IsValidBeaconPaymentThingShared(thing);
        }

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        



        

        

        

        internal bool CanSubmit()
        {
            return string.IsNullOrWhiteSpace(GetSubmitDisabledReason());
        }

        

        

        

        

        

        

        

        

        

        

        

        

        internal float ComputeOfferTotal()
        {
            return Math.Max(0f, selectedOfferUnitPrice * ParsePositiveInt(offerCountText, 1));
        }

        

        

        

        

        internal sealed class InventoryDisplayEntry
        {
            public string DefName { get; set; }
            public string Label { get; set; }
            public int Count { get; set; }
            public float UnitPrice { get; set; }
            public int StackLimit { get; set; }
            public string PriceSemantic { get; set; }
        }
    
        #region Cluster forwards
        internal void ApplyCounterofferDefaults() => Parts.Slice1.ApplyCounterofferDefaults();
        internal void ForceSelectSilverAsOffer() => Parts.Slice1.ForceSelectSilverAsOffer();
        internal void EnsureOfferSelectionState() => Parts.Slice1.EnsureOfferSelectionState();
        internal AirdropTradeRuleSnapshot ResolveTradeRuleSnapshot() => Parts.Slice1.ResolveTradeRuleSnapshot();
        internal void LoadInventoryItemsAsync() => Parts.Slice1.LoadInventoryItemsAsync();
        internal static bool IsWithinFactionTechLevel(ThingDef def, TechLevel factionTechLevel) => ItemAirdropTradeCardSlice1.IsWithinFactionTechLevel(def, factionTechLevel);
        internal float ResolveOfferDisplayUnitPrice(ThingDef def) => Parts.Slice1.ResolveOfferDisplayUnitPrice(def);
        internal string ResolveOfferDisplayPriceSemantic(ThingDef def) => Parts.Slice1.ResolveOfferDisplayPriceSemantic(def);
        internal void ApplyInventoryFilter() => Parts.Slice1.ApplyInventoryFilter();
        public override void DoWindowContents(Rect inRect) => Parts.Slice1.DoWindowContents(inRect);
        internal void DrawTitle(Rect rect) => Parts.Slice1.DrawTitle(rect);
        internal void DrawSearchArea(Rect rect) => Parts.Slice1.DrawSearchArea(rect);
        internal void DrawNeedBindingStatus(Rect rect) => Parts.Slice1.DrawNeedBindingStatus(rect);
        internal void DrawInlineSuggestionDropDown(Rect rect) => Parts.Slice1.DrawInlineSuggestionDropDown(rect);
        internal void DrawItemCards(Rect rect) => Parts.Slice1.DrawItemCards(rect);
        internal void DrawNeedItemCard(Rect rect) => Parts.Slice1.DrawNeedItemCard(rect);
        internal string ResolveNeedPriceSemantic() => Parts.Slice1.ResolveNeedPriceSemantic();
        internal void DrawOfferItemCard(Rect rect) => Parts.Slice1.DrawOfferItemCard(rect);
        internal void DrawCardHeader(Rect rect, string key) => Parts.Slice1.DrawCardHeader(rect, key);
        internal void DrawEmptyCard(Rect rect, string key) => Parts.Slice1.DrawEmptyCard(rect, key);
        internal static string BuildPriceSemanticTag(string semantic) => ItemAirdropTradeCardSlice1.BuildPriceSemanticTag(semantic);
        internal void DrawThingDefCardContent(Rect rect, ThingDefRecord record, int count, float unitPrice, float totalPrice, string priceSemantic) => Parts.Slice2.DrawThingDefCardContent(rect, record, count, unitPrice, totalPrice, priceSemantic);
        internal void DrawInventoryPanel(Rect rect) => Parts.Slice2.DrawInventoryPanel(rect);
        internal void DrawInventorySearchBar(Rect rect) => Parts.Slice2.DrawInventorySearchBar(rect);
        internal void DrawLoadingIndicator(Rect rect) => Parts.Slice2.DrawLoadingIndicator(rect);
        internal void DrawInventoryRow(InventoryDisplayEntry entry, float width, float y) => Parts.Slice2.DrawInventoryRow(entry, width, y);
        internal void DrawFooter(Rect rect) => Parts.Slice2.DrawFooter(rect);
        internal void DrawTradeRulesInfo(Rect rect) => Parts.Slice2.DrawTradeRulesInfo(rect);
        internal void DrawReferencePriceBlock(Rect rect) => Parts.Slice2.DrawReferencePriceBlock(rect);
        internal string BuildReferencePriceFormulaText() => Parts.Slice2.BuildReferencePriceFormulaText();
        internal static string FormatTradeAmountCompact(int amount) => ItemAirdropTradeCardSlice2.FormatTradeAmountCompact(amount);
        internal void DrawFooterInputs(Rect rect) => Parts.Slice2.DrawFooterInputs(rect);
        internal static void DrawIntegerField(Rect rect, string label, string current, out string updated, int min, int max) => ItemAirdropTradeCardSlice2.DrawIntegerField(rect, label, current, out updated, min, max);
        internal int ComputePodCount() => Parts.Slice2.ComputePodCount();
        internal string GetSubmitDisabledReason() => Parts.Slice2.GetSubmitDisabledReason();
        internal void Submit() => Parts.Slice2.Submit();
        internal void BindNeedRecord(ThingDefRecord record) => Parts.Slice2.BindNeedRecord(record);
        internal void ApplyPendingInventoryLoadIfReady() => Parts.Slice2.ApplyPendingInventoryLoadIfReady();
        internal InventoryDisplayEntry FindInventoryEntryByDefName(string defName) => Parts.Slice2.FindInventoryEntryByDefName(defName);
        internal void ClearOfferSelection() => Parts.Slice2.ClearOfferSelection();
        internal void ClearNeedBinding() => Parts.Slice2.ClearNeedBinding();
        internal void ApplyOfferSelection(InventoryDisplayEntry entry) => Parts.Slice2.ApplyOfferSelection(entry);
        internal string ResolveSelectedOfferLabel() => Parts.Slice3.ResolveSelectedOfferLabel();
        internal float ComputeNeedReferenceTotal() => Parts.Slice3.ComputeNeedReferenceTotal();
        internal float ResolveNeedUnitPrice() => Parts.Slice3.ResolveNeedUnitPrice();
        internal float ResolveStandardNeedFallback() => Parts.Slice3.ResolveStandardNeedFallback();
        internal string ValidateBeforeSubmit(int offerCount) => Parts.Slice3.ValidateBeforeSubmit(offerCount);
        internal void ShowValidationFailureDialog(string message) => Parts.Slice3.ShowValidationFailureDialog(message);
        internal static int ParsePositiveInt(string value, int fallback) => ItemAirdropTradeCardSlice3.ParsePositiveInt(value, fallback);
        internal static void DrawPanel(Rect rect, Color fill) => ItemAirdropTradeCardSlice3.DrawPanel(rect, fill);
        #endregion
}
    internal sealed class Dialog_ItemAirdropTradeCardParts
    {
        internal readonly Dialog_ItemAirdropTradeCard Owner;
        internal readonly ItemAirdropTradeCardSlice1 Slice1;
        internal readonly ItemAirdropTradeCardSlice2 Slice2;
        internal readonly ItemAirdropTradeCardSlice3 Slice3;
        internal Dialog_ItemAirdropTradeCardParts(Dialog_ItemAirdropTradeCard owner)
        {
            Owner = owner;
            Slice1 = new ItemAirdropTradeCardSlice1(owner);
            Slice2 = new ItemAirdropTradeCardSlice2(owner);
            Slice3 = new ItemAirdropTradeCardSlice3(owner);
        }
    }

}
