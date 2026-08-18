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
        internal abstract class Dialog_ItemAirdropTradeCardCollaborator
    {
        internal readonly Dialog_ItemAirdropTradeCard Owner;

        protected Dialog_ItemAirdropTradeCardCollaborator(Dialog_ItemAirdropTradeCard owner)
        {
            Owner = owner;
        }

        protected Dialog_ItemAirdropTradeCardParts Parts => Owner.Parts;

        protected void Close(bool doCloseSound = true) => Owner.Close(doCloseSound);
        protected bool absorbInputAroundWindow
        {
            get => Owner.absorbInputAroundWindow;
            set => Owner.absorbInputAroundWindow = value;
        }
        protected FactionDialogueSession session => Owner.session;
        protected Faction faction => Owner.faction;
        protected Action<ItemAirdropTradeCardPayload> onSubmitted => Owner.onSubmitted;
        protected SearchStateManager searchState => Owner.searchState;
        protected List<Dialog_ItemAirdropTradeCard.InventoryDisplayEntry> inventoryItems => Owner.inventoryItems;
        protected List<Dialog_ItemAirdropTradeCard.InventoryDisplayEntry> filteredInventoryItems => Owner.filteredInventoryItems;
        protected List<Dialog_ItemAirdropTradeCard.InventoryDisplayEntry> pendingInventoryItems
        {
            get => Owner.pendingInventoryItems;
            set => Owner.pendingInventoryItems = value;
        }
        protected string needSearchText
        {
            get => Owner.needSearchText;
            set => Owner.needSearchText = value;
        }
        protected string requestedCountText
        {
            get => Owner.requestedCountText;
            set => Owner.requestedCountText = value;
        }
        protected string offerCountText
        {
            get => Owner.offerCountText;
            set => Owner.offerCountText = value;
        }
        protected string inventorySearchText
        {
            get => Owner.inventorySearchText;
            set => Owner.inventorySearchText = value;
        }
        protected string selectedOfferDefName
        {
            get => Owner.selectedOfferDefName;
            set => Owner.selectedOfferDefName = value;
        }
        protected string selectedOfferLabel
        {
            get => Owner.selectedOfferLabel;
            set => Owner.selectedOfferLabel = value;
        }
        protected int selectedOfferStackLimit
        {
            get => Owner.selectedOfferStackLimit;
            set => Owner.selectedOfferStackLimit = value;
        }
        protected float selectedOfferUnitPrice
        {
            get => Owner.selectedOfferUnitPrice;
            set => Owner.selectedOfferUnitPrice = value;
        }
        protected string selectedOfferPriceSemantic
        {
            get => Owner.selectedOfferPriceSemantic;
            set => Owner.selectedOfferPriceSemantic = value;
        }
        protected Vector2 inventoryScrollPos
        {
            get => Owner.inventoryScrollPos;
            set => Owner.inventoryScrollPos = value;
        }
        protected ThingDefRecord boundNeedRecord
        {
            get => Owner.boundNeedRecord;
            set => Owner.boundNeedRecord = value;
        }
        protected bool showInlineSuggestions
        {
            get => Owner.showInlineSuggestions;
            set => Owner.showInlineSuggestions = value;
        }
        protected bool isLoadingInventory
        {
            get => Owner.isLoadingInventory;
            set => Owner.isLoadingInventory = value;
        }
        protected float inventoryLoadProgress
        {
            get => Owner.inventoryLoadProgress;
            set => Owner.inventoryLoadProgress = value;
        }
        protected bool inventoryLoadCompleted
        {
            get => Owner.inventoryLoadCompleted;
            set => Owner.inventoryLoadCompleted = value;
        }
        protected const float TitleHeight = 62f;
        protected const float SearchAreaHeight = 76f;
        protected const float SuggestionRowHeight = 38f;
        protected const float FooterHeight = 164f;
        protected const float Padding = 12f;
        protected const float InventoryRowHeight = 46f;
        protected const float CardImageSize = 54f;
    }

}
