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
    internal sealed class ItemAirdropTradeCardSlice3 : Dialog_ItemAirdropTradeCardCollaborator
    {
        internal ItemAirdropTradeCardSlice3(Dialog_ItemAirdropTradeCard owner) : base(owner)
        {
        }

internal string ResolveSelectedOfferLabel()
        {
            return string.IsNullOrWhiteSpace(selectedOfferLabel)
                ? "RimChat_AirdropTradeCard_NoOfferItem".Translate().ToString()
                : "RimChat_AirdropTradeCard_SelectedOfferItem".Translate(selectedOfferLabel).ToString();
        }

internal float ComputeNeedReferenceTotal()
        {
            if (boundNeedRecord?.Def == null)
            {
                return 0f;
            }

            return Math.Max(0f, Owner.ResolveNeedUnitPrice() * Dialog_ItemAirdropTradeCard.ParsePositiveInt(requestedCountText, 1));
        }

internal float ResolveNeedUnitPrice()
        {
            if (boundNeedRecord?.Def == null)
            {
                return 0.01f;
            }

            float unitPrice;

            if (faction != null && 
                FactionSpecialItemsManager.Instance.TryMatchSpecialItem(faction, boundNeedRecord.DefName, out SpecialItemType specialItemType))
            {
                if (ItemAirdropTradePolicy.TryResolveSpecialItemPrice(boundNeedRecord.Def, specialItemType, out float specialPrice, out _))
                {
                    unitPrice = Math.Max(0.01f, specialPrice);
                }
                else
                {
                    unitPrice = Owner.ResolveStandardNeedFallback();
                }
            }
            else
            {
                unitPrice = Owner.ResolveStandardNeedFallback();
            }

            ItemAirdropTradePolicy.ApplyUntradeablePremium(boundNeedRecord.Def, ref unitPrice);

            return unitPrice;
        }

internal float ResolveStandardNeedFallback()
        {
            if (ItemAirdropTradePolicy.TryResolveNeedUnitPrice(boundNeedRecord.Def, out float resolved, out _))
            {
                return Math.Max(0.01f, resolved);
            }

            return Math.Max(0.01f, boundNeedRecord.MarketValue);
        }

internal string ValidateBeforeSubmit(int offerCount)
        {
            Map map = Find.AnyPlayerHomeMap ?? Find.CurrentMap;
            if (map != null && Module.MapUtility.IsOrbitalBaseMap(map))
            {
                return "RimChat_AirdropSubmitOrbitalBase".Translate();
            }

            InventoryDisplayEntry offerEntry = Owner.FindInventoryEntryByDefName(selectedOfferDefName);
            if (offerEntry == null || offerEntry.Count < offerCount)
            {
                return "RimChat_AirdropSubmitInsufficientOffer".Translate(
                    selectedOfferLabel ?? selectedOfferDefName ?? "RimChat_Unknown".Translate(),
                    offerCount,
                    offerEntry?.Count ?? 0);
            }

            return string.Empty;
        }

internal void ShowValidationFailureDialog(string message)
        {
            Find.WindowStack.Add(new Dialog_MessageBox(
                message,
                "OK".Translate(),
                null));
        }

internal static int ParsePositiveInt(string value, int fallback)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed > 0
                ? parsed
                : fallback;
        }

internal static void DrawPanel(Rect rect, Color fill)
        {
            Widgets.DrawBoxSolid(rect, fill);
            GUI.color = new Color(0.24f, 0.27f, 0.34f, 0.95f);
            Widgets.DrawBox(rect);
            GUI.color = Color.white;
        }
    }
}
