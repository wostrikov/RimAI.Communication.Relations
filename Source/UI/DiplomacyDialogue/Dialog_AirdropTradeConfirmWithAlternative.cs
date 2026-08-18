using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Context;
using Ustas.RimAI.Communication.Relations.Dialogue;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Communication.Relations.Guards;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.PawnRpgPush;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.UI;

namespace Ustas.RimAI.Communication.Relations.UI;



internal sealed class Dialog_AirdropTradeConfirmWithAlternative : Window
{
    private readonly string tradeLabel;
    private readonly int quantity;
    private readonly int requestedQuantity;
    private readonly int paymentTotal;
    private readonly float unitPrice;
    private readonly string priceTag;
    private readonly int shippingCost;
    private readonly int shippingPods;
    private readonly string adjustmentReason;
    private readonly Action onConfirm;
    private readonly Action onCancel;
    private readonly Action onAlternative;
    private readonly bool optionalAlternativeVisible;

    public override Vector2 InitialSize => new Vector2(500f, 320f);

    public Dialog_AirdropTradeConfirmWithAlternative(
        string tradeLabel,
        int quantity,
        int requestedQuantity,
        int paymentTotal,
        float unitPrice,
        string priceTag,
        int shippingCost,
        int shippingPods,
        string adjustmentReason,
        bool hasAlternative,
        Action onConfirm,
        Action onCancel,
        Action onAlternative)
    {
        this.tradeLabel = tradeLabel ?? string.Empty;
        this.quantity = Math.Max(1, quantity);
        this.requestedQuantity = Math.Max(1, requestedQuantity);
        this.paymentTotal = paymentTotal;
        this.unitPrice = unitPrice;
        this.priceTag = priceTag ?? string.Empty;
        this.shippingCost = shippingCost;
        this.shippingPods = shippingPods;
        this.adjustmentReason = adjustmentReason ?? string.Empty;
        this.onConfirm = onConfirm;
        this.onCancel = onCancel;
        this.onAlternative = onAlternative;
        this.optionalAlternativeVisible = hasAlternative;
        forcePause = true;
        doCloseX = false;
        absorbInputAroundWindow = true;
        closeOnClickedOutside = false;
        closeOnCancel = false;
        closeOnAccept = false;
    }

    public override void DoWindowContents(Rect inRect)
    {
        float y = inRect.y;

        // Title
        Text.Font = GameFont.Medium;
        string title = "RimChat_ItemAirdropConfirmTitle".Translate();
        float titleHeight = Text.CalcHeight(title, inRect.width);
        Widgets.Label(new Rect(inRect.x, y, inRect.width, titleHeight), title);
        y += titleHeight + 12f;

        // Key info — large
        Text.Font = GameFont.Medium;
        string mainLine = "RimChat_ItemAirdropConfirmMainLine".Translate(tradeLabel, quantity, paymentTotal);
        float mainHeight = Text.CalcHeight(mainLine, inRect.width - 20f);
        Widgets.Label(new Rect(inRect.x + 10f, y, inRect.width - 20f, mainHeight), mainLine);
        y += mainHeight + 6f;

        // Quantity adjustment note
        Text.Font = GameFont.Tiny;
        Color dimGray = new Color(0.55f, 0.55f, 0.55f);
        Color prevColor = GUI.color;

        if (quantity != requestedQuantity && requestedQuantity > 0)
        {
            string adjStr = string.IsNullOrWhiteSpace(adjustmentReason)
                ? "RimChat_ItemAirdropConfirmAdjusted".Translate(requestedQuantity, quantity)
                : "RimChat_ItemAirdropConfirmAdjustedReason".Translate(requestedQuantity, quantity, adjustmentReason);
            GUI.color = new Color(0.9f, 0.6f, 0.2f); // amber warning
            float adjH = Text.CalcHeight(adjStr, inRect.width - 20f);
            Widgets.Label(new Rect(inRect.x + 10f, y, inRect.width - 20f, adjH), adjStr);
            y += adjH + 2f;
        }

        // Secondary details
        if (unitPrice > 0f)
        {
            string unitStr = string.IsNullOrWhiteSpace(priceTag)
                ? "RimChat_ItemAirdropConfirmUnitPrice".Translate(unitPrice.ToString("F1"))
                : "RimChat_ItemAirdropConfirmUnitPriceTagged".Translate(unitPrice.ToString("F1"), priceTag);
            GUI.color = dimGray;
            float unitH = Text.CalcHeight(unitStr, inRect.width - 20f);
            Widgets.Label(new Rect(inRect.x + 10f, y, inRect.width - 20f, unitH), unitStr);
            y += unitH + 2f;
        }

        if (shippingCost > 0)
        {
            string shipStr = "RimChat_ItemAirdropConfirmShippingShort".Translate(shippingCost, shippingPods);
            GUI.color = dimGray;
            float shipH = Text.CalcHeight(shipStr, inRect.width - 20f);
            Widgets.Label(new Rect(inRect.x + 10f, y, inRect.width - 20f, shipH), shipStr);
            y += shipH + 2f;
        }

        GUI.color = prevColor;

        // Buttons
        float buttonTop = inRect.yMax - 48f;
        float buttonWidth = (inRect.width - 20f) / 2f;
        Rect confirmRect = new Rect(inRect.x + 5f, buttonTop, buttonWidth, 42f);
        Rect cancelRect = new Rect(confirmRect.xMax + 10f, buttonTop, buttonWidth, 42f);

        Text.Font = GameFont.Medium;
        if (Widgets.ButtonText(confirmRect, "RimChat_ItemAirdropConfirmAccept".Translate()))
        {
            onConfirm?.Invoke();
            Close();
            return;
        }

        if (Widgets.ButtonText(cancelRect, "RimChat_ItemAirdropConfirmCancel".Translate()))
        {
            onCancel?.Invoke();
            Close();
            return;
        }

        if (!optionalAlternativeVisible)
        {
            return;
        }

        // Low-visibility alternative link — simple label-based button, no Anchor manipulation
        Text.Font = GameFont.Tiny;
        GUI.color = new Color(0.45f, 0.45f, 0.45f);
        float altY = cancelRect.y - 24f;
        Rect alternativeRect = new Rect(inRect.x, altY, inRect.width, 20f);
        if (Widgets.ButtonText(alternativeRect, "RimChat_ItemAirdropAlternativeLowVisibility".Translate()))
        {
            onAlternative?.Invoke();
            Close();
        }
        GUI.color = prevColor;
    }
}

