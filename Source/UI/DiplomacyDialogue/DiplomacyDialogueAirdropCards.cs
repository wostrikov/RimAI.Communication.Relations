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

internal sealed class DiplomacyDialogueAirdropCards : DiplomacyDialogueCollaborator
{
    internal DiplomacyDialogueAirdropCards(Dialog_DiplomacyDialogue owner) : base(owner) { }



internal const float AirdropCardThumbSize = 36f;


internal const float AirdropCardPadding = 8f;


internal const float AirdropCardHeaderHeight = 14f;


internal const float AirdropCardTitleBandHeight = 28f;


internal const float AirdropCardRowGap = 4f;


internal const float AirdropCardMetricGap = 2f;


internal const float AirdropCardMetricHeight = 30f;


internal const float AirdropCardMinRowHeight = 90f;


internal const float AirdropCardMiniIconSize = 32f;


internal const float AirdropCardMiniTextWidth = 80f;


internal const float AirdropCardMiniCardHeight = 100f;


internal const float AirdropCardFlowGap = 8f;


internal const float AirdropCardBadgeWidth = 44f;


internal const float AirdropCardDefNameHeight = 13f;



internal float CalculateAirdropTradeCardBubbleHeight(DialogueMessageData msg, float width)
{
    float contentWidth = Mathf.Max(1f, width - AirdropCardPadding * 2f);
    float headerTotal = AirdropCardHeaderHeight + 4f;
    string title = "RimChat_AirdropTradeCard_BubbleTitle".Translate().ToString();
    float titleHeight = Mathf.Max(AirdropCardTitleBandHeight, Text.CalcHeight(title, contentWidth));
    float titleTotal = titleHeight + 4f;
    float flowRowHeight = AirdropCardMiniCardHeight + 6f;
    float shippingHeight = Mathf.Max(18f, Text.CalcHeight(BuildAirdropBubbleShippingText(msg), contentWidth));
    float totalHeight = headerTotal + titleTotal + flowRowHeight + 6f + shippingHeight;
    return Mathf.Max(198f, totalHeight);
}



internal void DrawAirdropTradeCardBubble(DialogueMessageData msg, Rect rect)
{
    bool playerVisual = Owner.Parts.Speakers.IsPlayerVisualMessage(msg);
    Color bubbleColor = playerVisual ? DiplomacySessionApplication.PlayerBubbleColor : DiplomacySessionApplication.AIBubbleColor;
    Color senderColor = playerVisual ? new Color(0.12f, 0.16f, 0.10f, 0.95f) : new Color(0.16f, 0.19f, 0.23f, 0.95f);
    Color secondaryTextColor = playerVisual ? new Color(0.14f, 0.18f, 0.12f, 0.78f) : new Color(0.18f, 0.21f, 0.24f, 0.82f);
    Color dividerColor = new Color(0f, 0f, 0f, 0.18f);
    Color contentPanelColor = new Color(1f, 1f, 1f, 0.06f);
    Color contentPrimaryTextColor = new Color(0.10f, 0.12f, 0.11f, 0.98f);
    Color contentSecondaryTextColor = new Color(0.18f, 0.20f, 0.19f, 0.88f);
    Color metricLabelColor = new Color(0.20f, 0.22f, 0.21f, 0.84f);
    Color metricValueColor = new Color(0.09f, 0.11f, 0.10f, 0.98f);

    Rect shadowRect = new Rect(rect.x + 1f, rect.y + 2f, rect.width, rect.height);
    Owner.Parts.MessageView.DrawRoundedRect(shadowRect, new Color(0f, 0f, 0f, 0.12f), Dialog_DiplomacyDialogue.BUBBLE_CORNER_RADIUS);
    Owner.Parts.MessageView.DrawRoundedRect(rect, bubbleColor, Dialog_DiplomacyDialogue.BUBBLE_CORNER_RADIUS);

    float contentX = rect.x + AirdropCardPadding;
    float contentY = rect.y + 5f;
    float contentWidth = rect.width - AirdropCardPadding * 2f;

    Text.Font = GameFont.Tiny;
    GUI.color = senderColor;
    DiplomacyDialogueInput.DrawSingleLineClippedLabel(new Rect(contentX, contentY, contentWidth * 0.7f, AirdropCardHeaderHeight), Owner.Parts.Speakers.GetDisplaySenderName(msg));

    string timeStr = Owner.Parts.MessageView.GetTimestampString(msg);
    float timeWidth = Text.CalcSize(timeStr).x + 5f;
    Rect timeRect = new Rect(rect.xMax - timeWidth - AirdropCardPadding, contentY, timeWidth, AirdropCardHeaderHeight);
    GUI.color = secondaryTextColor;
    DiplomacyDialogueInput.DrawSingleLineClippedLabel(timeRect, timeStr);

    contentY += AirdropCardHeaderHeight + 3f;
    Widgets.DrawBoxSolid(new Rect(contentX, contentY, contentWidth, 1f), dividerColor);
    contentY += 3f;

    Text.Font = GameFont.Small;
    GUI.color = new Color(0.09f, 0.11f, 0.10f, 1f);
    string title = "RimChat_AirdropTradeCard_BubbleTitle".Translate().ToString();
    float titleHeight = Mathf.Max(AirdropCardTitleBandHeight, Text.CalcHeight(title, contentWidth));
    Widgets.Label(new Rect(contentX, contentY, contentWidth, titleHeight), title);
    GUI.color = Color.white;

    contentY += titleHeight + 3f;
    Widgets.DrawBoxSolid(new Rect(contentX, contentY, contentWidth, 1f), dividerColor);
    contentY += 3f;

    float flowRowWidth = contentWidth;
    float sideCardWidth = (flowRowWidth - AirdropCardBadgeWidth - AirdropCardFlowGap * 2f) / 2f;

    Rect needCardRect = new Rect(contentX, contentY, sideCardWidth, AirdropCardMiniCardHeight);
    Rect arrowRect = new Rect(contentX + sideCardWidth + AirdropCardFlowGap, contentY, AirdropCardBadgeWidth, AirdropCardMiniCardHeight);
    Rect offerCardRect = new Rect(arrowRect.xMax + AirdropCardFlowGap, contentY, sideCardWidth, AirdropCardMiniCardHeight);

    float shippingCostSilver = Mathf.Max(0f, msg?.airdropShippingCostSilver ?? 0f);
    float finalQuoteTotal = Mathf.Max(0f, (msg?.airdropNeedReferenceTotalPrice ?? 0f) + shippingCostSilver);
    float offerTotal = Mathf.Max(0f, msg?.airdropOfferTotalPrice ?? 0f);
    float profitRatio = finalQuoteTotal > 0f ? offerTotal / finalQuoteTotal : 1f;
    string shippingText = BuildAirdropBubbleShippingText(msg);

    DrawAirdropCompactCard(
        needCardRect,
        msg.airdropNeedLabel,
        msg.airdropNeedDefName,
        msg.airdropRequestedCount,
        msg.airdropNeedUnitPrice,
        msg.airdropNeedReferenceTotalPrice,
        contentPanelColor,
        dividerColor,
        contentPrimaryTextColor,
        contentSecondaryTextColor,
        metricLabelColor,
        metricValueColor);

    DrawAirdropFlowBadge(arrowRect, profitRatio, playerVisual);

    DrawAirdropCompactCard(
        offerCardRect,
        msg.airdropOfferLabel,
        msg.airdropOfferDefName,
        msg.airdropOfferCount,
        msg.airdropOfferUnitPrice,
        msg.airdropOfferTotalPrice,
        contentPanelColor,
        dividerColor,
        contentPrimaryTextColor,
        contentSecondaryTextColor,
        metricLabelColor,
        metricValueColor);

    float shippingTop = contentY + AirdropCardMiniCardHeight + 8f;
    Text.Font = GameFont.Tiny;
    GUI.color = contentSecondaryTextColor;
    Widgets.Label(new Rect(contentX, shippingTop, contentWidth, Mathf.Max(18f, Text.CalcHeight(shippingText, contentWidth))), shippingText);

    GUI.color = Color.white;
    Text.Font = GameFont.Small;
}



internal static string BuildAirdropBubbleShippingText(DialogueMessageData msg)
{
    int podCount = Math.Max(0, msg?.airdropShippingPodCount ?? 0);
    int shippingCost = Math.Max(0, msg?.airdropShippingCostSilver ?? 0);
    float finalQuote = Math.Max(0f, (msg?.airdropNeedReferenceTotalPrice ?? 0f) + shippingCost);
    return "RimChat_AirdropTradeCard_BubbleShippingSummary".Translate(podCount, shippingCost, finalQuote.ToString("F1", CultureInfo.InvariantCulture)).ToString();
}



internal void DrawAirdropCompactCard(
    Rect rect,
    string label,
    string defName,
    int count,
    float unitPrice,
    float totalPrice,
    Color contentPanelColor,
    Color dividerColor,
    Color primaryTextColor,
    Color secondaryTextColor,
    Color metricLabelColor,
    Color metricValueColor)
{
    Color savedColor = GUI.color;
    GameFont savedFont = Text.Font;
    
    Owner.Parts.MessageView.DrawRoundedRect(rect, contentPanelColor, 6f);
    GUI.color = new Color(0f, 0f, 0f, 0.20f);
    Widgets.DrawBox(rect);
    GUI.color = savedColor;

    float iconPanelSize = AirdropCardMiniIconSize + 4f;
    Rect iconPanelRect = new Rect(rect.x + 4f, rect.y + 4f, iconPanelSize, iconPanelSize);
    Rect iconRect = new Rect(iconPanelRect.x + 2f, iconPanelRect.y + 2f, AirdropCardMiniIconSize, AirdropCardMiniIconSize);
    DrawAirdropThingThumbnail(iconRect, defName);

    float textX = rect.x + 4f;
    float textWidth = rect.width - 8f;
    float textStartY = iconPanelRect.yMax + 4f;
    float metricsTop = rect.yMax - AirdropCardMetricHeight - 4f;

    float availableForName = Mathf.Max(20f, metricsTop - textStartY - 6f);
    Text.Font = GameFont.Small;
    GUI.color = primaryTextColor;
    string displayLabel = string.IsNullOrWhiteSpace(label) ? (defName ?? "?") : label;
    float labelHeight = Text.CalcHeight(displayLabel, textWidth);
    labelHeight = Mathf.Min(labelHeight, availableForName > 20f ? availableForName : 20f);

    Widgets.Label(new Rect(textX, textStartY, textWidth, labelHeight), displayLabel);
    float yPos = textStartY + labelHeight;

    if (!string.IsNullOrWhiteSpace(defName) && (yPos + AirdropCardDefNameHeight < metricsTop - 3f))
    {
        Text.Font = GameFont.Tiny;
        GUI.color = secondaryTextColor;
        DiplomacyDialogueInput.DrawSingleLineClippedLabel(new Rect(textX, yPos, textWidth, AirdropCardDefNameHeight), defName);
        yPos += AirdropCardDefNameHeight;
    }

    GUI.color = dividerColor;
    Widgets.DrawBoxSolid(new Rect(textX, metricsTop - 2f, textWidth, 1f), dividerColor);
    GUI.color = savedColor;

    float metricWidth = (textWidth - AirdropCardMetricGap * 2f) / 3f;
    DrawAirdropMetricCell(
        new Rect(textX, metricsTop, metricWidth, AirdropCardMetricHeight),
        "RimChat_AirdropTradeCard_CountLabel".Translate().ToString(),
        count.ToString(CultureInfo.InvariantCulture),
        dividerColor,
        metricLabelColor,
        metricValueColor);
    DrawAirdropMetricCell(
        new Rect(textX + metricWidth + AirdropCardMetricGap, metricsTop, metricWidth, AirdropCardMetricHeight),
        "RimChat_UnitPrice".Translate().ToString(),
        unitPrice.ToString("F1", CultureInfo.InvariantCulture),
        dividerColor,
        metricLabelColor,
        metricValueColor);
    DrawAirdropMetricCell(
        new Rect(textX + (metricWidth + AirdropCardMetricGap) * 2, metricsTop, metricWidth, AirdropCardMetricHeight),
        "RimChat_AirdropTradeCard_TotalPriceLabel".Translate().ToString(),
        totalPrice.ToString("F1", CultureInfo.InvariantCulture),
        dividerColor,
        metricLabelColor,
        metricValueColor);
    GUI.color = savedColor;
    Text.Font = savedFont;
}



internal void DrawAirdropFlowBadge(Rect arrowRect, float profitRatio, bool playerVisual)
{
    Color savedColor = GUI.color;
    
    float centerX = arrowRect.x + arrowRect.width * 0.5f;
    float centerY = arrowRect.y + arrowRect.height * 0.5f;

    Color profitColor;
    string badgeText;
    if (profitRatio >= 1.01f)
    {
        profitColor = new Color(0.2f, 0.7f, 0.3f, 0.9f);
        badgeText = $"+{(profitRatio - 1f) * 100:F0}%";
    }
    else if (profitRatio >= 0.99f)
    {
        profitColor = new Color(0.8f, 0.7f, 0.2f, 0.9f);
        badgeText = "±0%";
    }
    else
    {
        profitColor = new Color(0.8f, 0.3f, 0.2f, 0.9f);
        badgeText = $"{(profitRatio - 1f) * 100:F0}%";
    }

    Rect badgeRect = new Rect(
        centerX - AirdropCardBadgeWidth * 0.5f,
        centerY - 10f,
        AirdropCardBadgeWidth,
        20f);

    GUI.color = profitColor;
    Owner.Parts.MessageView.DrawRoundedRect(badgeRect, profitColor, 4f);
    GUI.color = Color.white;
    Text.Font = GameFont.Tiny;
    Text.Anchor = TextAnchor.MiddleCenter;
    Widgets.Label(badgeRect, badgeText);
    Text.Anchor = TextAnchor.UpperLeft;
    GUI.color = savedColor;

    Text.Font = GameFont.Small;
    GUI.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);
    string arrowStr = "RimChat_AirdropTradeCard_ArrowRight".Translate();
    float arrowWidth = Text.CalcSize(arrowStr).x;
    Widgets.Label(new Rect(centerX - arrowWidth * 0.5f, centerY + 12f, arrowWidth, 16f), arrowStr);
    GUI.color = savedColor;
    Text.Font = GameFont.Small;
}



internal void DrawAirdropMetricCell(Rect rect, string label, string value, Color dividerColor, Color labelColor, Color valueColor)
{
    Text.Font = GameFont.Tiny;
    Text.Anchor = TextAnchor.UpperCenter;

    GUI.color = labelColor;
    Rect labelRect = new Rect(rect.x, rect.y, rect.width, 14f);
    Widgets.Label(labelRect, label);
    
    GUI.color = valueColor;
    Rect valueRect = new Rect(rect.x, rect.y + 13f, rect.width, 14f);
    Widgets.Label(valueRect, value);
    
    Text.Anchor = TextAnchor.UpperLeft;
    GUI.color = Color.white;
}



internal float MeasureWrappedTextHeight(string text, float width, GameFont font, float maxHeight)
{
    if (string.IsNullOrWhiteSpace(text))
    {
        return 0f;
    }

    GameFont previousFont = Text.Font;
    Text.Font = font;
    float height = Text.CalcHeight(text, Mathf.Max(1f, width));
    Text.Font = previousFont;
    return Mathf.Min(maxHeight, Mathf.Max(14f, height));
}



internal void DrawAirdropThingThumbnail(Rect iconRect, string defName)
{
    Color savedColor = GUI.color;
    
    if (string.IsNullOrWhiteSpace(defName))
    {
        Widgets.DrawBoxSolid(iconRect, new Color(0.15f, 0.15f, 0.18f));
        GUI.color = new Color(0.5f, 0.55f, 0.6f);
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(iconRect, "?");
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = savedColor;
        return;
    }

    ThingDef thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
    if (thingDef?.uiIcon != null)
    {
        GUI.color = thingDef.uiIconColor;
        GUI.DrawTexture(iconRect.ContractedBy(2f), thingDef.uiIcon, ScaleMode.ScaleToFit, true);
    }
    else
    {
        Widgets.DrawBoxSolid(iconRect, new Color(0.15f, 0.15f, 0.18f));
    }

    GUI.color = new Color(0.35f, 0.35f, 0.4f, 0.9f);
    Widgets.DrawBox(iconRect);
    GUI.color = savedColor;
}
}
