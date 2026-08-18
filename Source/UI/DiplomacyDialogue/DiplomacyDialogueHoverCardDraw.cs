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

internal sealed class DiplomacyDialogueHoverCardDraw : DiplomacyDialogueCollaborator
{
    internal DiplomacyDialogueHoverCardDraw(Dialog_DiplomacyDialogue owner) : base(owner) { }

internal void DrawHoverCardChrome(Rect rect, float alpha, Color accent)
{
    Color fill = new Color(0.07f, 0.08f, 0.11f, 0.985f * alpha);
    Color frame = new Color(0.42f, 0.47f, 0.56f, 0.98f * alpha);
    Widgets.DrawBoxSolid(rect, fill);
    GUI.color = frame;
    Widgets.DrawBox(rect);
    Color accentFill = new Color(accent.r, accent.g, accent.b, 0.95f * alpha);
    GUI.color = accentFill;
    Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, 4f, rect.height), accentFill);
    GUI.color = Color.white;
}



internal void DrawHoverCardContent(Rect rect, Faction targetFaction, Pawn subjectPawn, List<HoverCardLine> lines, FactionIntelRevealTier tier, float alpha)
{
    Rect inner = rect.ContractedBy(DiplomacyDialogueHoverCard.HoverCardPadding);
    Rect portraitRect = new Rect(inner.x, inner.y, DiplomacyDialogueHoverCard.HoverCardPortraitSize, DiplomacyDialogueHoverCard.HoverCardPortraitSize);
    DrawHoverPortrait(portraitRect, targetFaction, subjectPawn, alpha);

    float textX = portraitRect.xMax + 10f;
    float textWidth = inner.xMax - textX;
    Text.Font = GameFont.Small;
    GUI.color = new Color(0.97f, 0.98f, 1f, alpha);
    Widgets.Label(new Rect(textX, inner.y, textWidth, 26f), subjectPawn?.LabelShortCap ?? targetFaction?.Name ?? "Unknown");

    Text.Font = GameFont.Tiny;
    GUI.color = new Color(0.8f, 0.88f, 0.98f, alpha);
    string relationText = targetFaction != null ? Owner.Parts.FactionList.GetRelationLabelShort(targetFaction.PlayerGoodwill) : "Your Colonist";
    Widgets.Label(new Rect(textX, inner.y + 24f, textWidth, 18f), relationText);
    GUI.color = new Color(0.82f, 0.86f, 0.92f, alpha);
    Widgets.Label(new Rect(textX, inner.y + 42f, textWidth, 18f), "RimChat_HoverCardRevealTier".Translate(GetRevealTierLabel(tier)));

    float linesY = portraitRect.yMax + DiplomacyDialogueHoverCard.HoverCardSectionGap;
    float labelWidth = 74f;
    for (int i = 0; i < lines.Count; i++)
    {
        HoverCardLine line = lines[i];
        float valueWidth = rowValueWidth(inner.width, labelWidth);
        float rowHeight = i == 0
            ? Mathf.Max(DiplomacyDialogueHoverCard.HoverCardLineHeight, Text.CalcHeight(line.Value, valueWidth))
            : DiplomacyDialogueHoverCard.HoverCardLineHeight;
        Rect rowRect = new Rect(inner.x, linesY, inner.width, rowHeight);
        GUI.color = new Color(0.78f, 0.82f, 0.9f, alpha);
        Widgets.Label(new Rect(rowRect.x, rowRect.y, labelWidth, DiplomacyDialogueHoverCard.HoverCardLineHeight), line.Label);
        GUI.color = line.IsObscured
            ? new Color(0.7f, 0.74f, 0.82f, DiplomacyDialogueHoverCard.HoverCardObscuredAlpha * alpha)
            : new Color(0.96f, 0.97f, 1f, alpha);
        bool previousWrap = Text.WordWrap;
        Text.WordWrap = i == 0;
        Widgets.Label(new Rect(rowRect.x + labelWidth, rowRect.y, valueWidth, rowHeight), line.Value);
        Text.WordWrap = previousWrap;
        linesY += rowHeight;
    }

    GUI.color = Color.white;
    Text.Font = GameFont.Small;
}



internal static float rowValueWidth(float innerWidth, float labelWidth)
{
    return Mathf.Max(120f, innerWidth - labelWidth);
}



internal void DrawHoverPortrait(Rect rect, Faction targetFaction, Pawn subjectPawn, float alpha)
{
    Widgets.DrawBoxSolid(rect, new Color(0.13f, 0.15f, 0.19f, 0.95f * alpha));
    GUI.color = new Color(0.42f, 0.48f, 0.57f, 0.92f * alpha);
    Widgets.DrawBox(rect);
    GUI.color = Color.white;

    Texture texture = DiplomacyDialogueSpeakers.ResolveSpeakerPortrait(subjectPawn);
    if (texture == null)
    {
        texture = targetFaction?.def?.FactionIcon;
    }

    Rect drawRect = rect.ContractedBy(3f);
    if (texture != null && texture != BaseContent.BadTex)
    {
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.DrawTexture(drawRect, texture, ScaleMode.ScaleAndCrop, true);
        GUI.color = Color.white;
        return;
    }

    DiplomacyDialogueSpeakers.DrawAvatarFallback(drawRect, subjectPawn?.LabelShortCap ?? targetFaction?.Name ?? "?");
}



internal static string GetRevealTierLabel(FactionIntelRevealTier tier)
{
    switch (tier)
    {
        case FactionIntelRevealTier.High:
            return "RimChat_HoverCardTierHigh".Translate();
        case FactionIntelRevealTier.Medium:
            return "RimChat_HoverCardTierMedium".Translate();
        case FactionIntelRevealTier.Low:
            return "RimChat_HoverCardTierLow".Translate();
        default:
            return "RimChat_HoverCardTierHostile".Translate();
    }
}



public void RequestSpeakerHoverCard(Pawn pawn, Faction faction, Vector2 screenPos)
{
    Owner.Parts.HoverCard.activeHoverPawn = pawn;
    Owner.Parts.HoverCard.activeHoverFaction = faction;
    Owner.Parts.HoverCard.activeHoverScreenPos = screenPos;
    Owner.Parts.HoverCard.speakerHoverRequestThisFrame = true;
}



public void DrawSpeakerHoverCard()
{
    if (Owner.Parts.HoverCard.activeHoverFaction == null)
    {
        if (Owner.Parts.HoverCard.speakerHoverCardAlpha > 0.001f)
        {
            Owner.Parts.HoverCard.speakerHoverCardAlpha = Mathf.MoveTowards(Owner.Parts.HoverCard.speakerHoverCardAlpha, 0f, Time.unscaledDeltaTime / 0.1f);
        }
        Owner.Parts.HoverCard.activeHoverPawn = null;
        return;
    }

    Vector2 mousePos = Event.current?.mousePosition ?? Vector2.zero;
    float cardWidth = 360f;
    float portraitSize = 96f;
    float padding = 12f;
    float lineHeight = 20f;
    float sectionGap = 6f;

    Faction resolvedFaction = Owner.Parts.HoverCard.activeHoverFaction;
    bool isPlayer = resolvedFaction != null && (resolvedFaction.IsPlayer || resolvedFaction == Faction.OfPlayer);
    FactionIntelRevealTier tier = isPlayer ? FactionIntelRevealTier.High : Owner.Parts.HoverCard.ResolveFactionRevealTier(resolvedFaction);

    float labelWidth = 74f;
    float valueWidth = cardWidth - padding * 2 - labelWidth;
    string bioText = DiplomacyDialogueHoverCard.BuildSpeakerHoverBioText(resolvedFaction, isPlayer, Owner.Parts.HoverCard.activeHoverPawn);
    bool bioRevealed = isPlayer || tier >= FactionIntelRevealTier.Low;
    string bioDisplay = bioRevealed ? (bioText ?? "???") : "???";

    Text.Font = GameFont.Tiny;
    float bioLineHeight = Mathf.Max(lineHeight, Text.CalcHeight(bioDisplay, valueWidth));
    Text.Font = GameFont.Small;

    int lineCount = 0;
    float totalLineHeight = 0f;
    if (isPlayer)
    {
        lineCount = 4;
        totalLineHeight = bioLineHeight + (lineCount - 1) * lineHeight;
    }
    else
    {
        lineCount = 11; // Always include discount/scarce lines now
        if (string.IsNullOrEmpty(resolvedFaction?.def?.description))
        {
            lineCount--;
        }
        totalLineHeight = bioLineHeight + (lineCount - 1) * lineHeight;
    }

    float cardHeight = portraitSize + padding * 2 + sectionGap + totalLineHeight + 8f;

    Rect bounds = lastWindowContentRect;
    Rect cardRect = new Rect(
        Owner.Parts.HoverCard.activeHoverScreenPos.x + 40f,
        Owner.Parts.HoverCard.activeHoverScreenPos.y - cardHeight / 2f,
        cardWidth,
        cardHeight);

    if (cardRect.xMax > bounds.xMax - 8f)
    {
        cardRect.x = Owner.Parts.HoverCard.activeHoverScreenPos.x - cardWidth - 10f;
    }
    if (cardRect.x < bounds.x + 8f)
    {
        cardRect.x = bounds.x + 8f;
    }
    if (cardRect.y < bounds.y + 8f)
    {
        cardRect.y = bounds.y + 8f;
    }
    if (cardRect.yMax > bounds.yMax - 8f)
    {
        cardRect.y = bounds.yMax - cardRect.height - 8f;
    }

    float targetAlpha;
    if (Owner.Parts.HoverCard.speakerHoverRequestThisFrame)
    {
        targetAlpha = 1f;
    }
    else
    {
        targetAlpha = cardRect.Contains(mousePos) ? 1f : 0f;
    }

    Owner.Parts.HoverCard.speakerHoverCardAlpha = Mathf.MoveTowards(Owner.Parts.HoverCard.speakerHoverCardAlpha, targetAlpha, Time.unscaledDeltaTime / 0.15f);

    if (Owner.Parts.HoverCard.speakerHoverCardAlpha < 0.01f)
    {
        Owner.Parts.HoverCard.activeHoverPawn = null;
        Owner.Parts.HoverCard.activeHoverFaction = null;
        Owner.Parts.HoverCard.speakerHoverRequestThisFrame = false;
        return;
    }

    Color accentColor = isPlayer ? Color.white : Owner.Parts.FactionList.GetGoodwillColor(resolvedFaction.PlayerGoodwill);
    Color bgColor = new Color(0.07f, 0.08f, 0.11f, 0.985f * Owner.Parts.HoverCard.speakerHoverCardAlpha);
    Color frameColor = new Color(0.42f, 0.47f, 0.56f, 0.98f * Owner.Parts.HoverCard.speakerHoverCardAlpha);

    Widgets.DrawBoxSolid(cardRect, bgColor);
    GUI.color = frameColor;
    Widgets.DrawBox(cardRect);
    Color accentFill = new Color(accentColor.r, accentColor.g, accentColor.b, 0.95f * Owner.Parts.HoverCard.speakerHoverCardAlpha);
    GUI.color = accentFill;
    Widgets.DrawBoxSolid(new Rect(cardRect.x, cardRect.y, 4f, cardRect.height), accentFill);
    GUI.color = Color.white;

    Rect portraitRect = new Rect(cardRect.x + padding, cardRect.y + padding, portraitSize, portraitSize);
    DrawHoverCardPortrait(portraitRect, Owner.Parts.HoverCard.activeHoverPawn, resolvedFaction, Owner.Parts.HoverCard.speakerHoverCardAlpha);

    float textX = portraitRect.xMax + padding;
    float textWidth = cardRect.width - portraitSize - padding * 3;
    float curY = cardRect.y + padding;

    Text.Font = GameFont.Small;
    GUI.color = new Color(0.97f, 0.98f, 1f, Owner.Parts.HoverCard.speakerHoverCardAlpha);
    string name = Owner.Parts.HoverCard.activeHoverPawn?.LabelShortCap ?? resolvedFaction?.Name ?? "Unknown";
    Widgets.Label(new Rect(textX, curY, textWidth, 24f), name);
    curY += 24f;

    Text.Font = GameFont.Tiny;
    if (isPlayer)
    {
        GUI.color = new Color(0.8f, 0.88f, 0.98f, Owner.Parts.HoverCard.speakerHoverCardAlpha);
        Widgets.Label(new Rect(textX, curY, textWidth, lineHeight), "RimChat_HoverCardPlayerFaction".Translate());
        curY += lineHeight;
    }
    else
    {
        GUI.color = new Color(accentColor.r, accentColor.g, accentColor.b, Owner.Parts.HoverCard.speakerHoverCardAlpha);
        Widgets.Label(new Rect(textX, curY, textWidth, lineHeight), Owner.Parts.FactionList.GetRelationLabelShort(resolvedFaction.PlayerGoodwill));
        curY += lineHeight;

        GUI.color = new Color(0.82f, 0.86f, 0.92f, Owner.Parts.HoverCard.speakerHoverCardAlpha);
        Widgets.Label(new Rect(textX, curY, textWidth, lineHeight), "RimChat_HoverCardRevealTier".Translate(GetRevealTierLabel(tier)));
    }

    float linesY = portraitRect.yMax + sectionGap;

    if (isPlayer)
    {
        DrawIntelLine(cardRect, ref linesY, padding, labelWidth, "RimChat_HoverCardBio".Translate(), bioText, true, Owner.Parts.HoverCard.speakerHoverCardAlpha, bioLineHeight);
        DrawIntelLine(cardRect, ref linesY, padding, labelWidth, "RimChat_HoverCardFaction".Translate(), Faction.OfPlayer.Name ?? "RimChat_HoverCardPlayerFaction".Translate(), true, Owner.Parts.HoverCard.speakerHoverCardAlpha);
        if (Owner.Parts.HoverCard.activeHoverPawn != null)
        {
            DrawIntelLine(cardRect, ref linesY, padding, labelWidth, "RimChat_HoverCardAge".Translate(), Owner.Parts.HoverCard.activeHoverPawn.ageTracker?.AgeBiologicalYears.ToString() ?? "?", true, Owner.Parts.HoverCard.speakerHoverCardAlpha);
            DrawIntelLine(cardRect, ref linesY, padding, labelWidth, "RimChat_HoverCardGender".Translate(), Owner.Parts.HoverCard.activeHoverPawn.gender.GetLabel(), true, Owner.Parts.HoverCard.speakerHoverCardAlpha);
        }
    }
    else
    {
        DrawIntelLine(cardRect, ref linesY, padding, labelWidth, "RimChat_HoverCardBio".Translate(), bioText, tier >= FactionIntelRevealTier.Low, Owner.Parts.HoverCard.speakerHoverCardAlpha, bioLineHeight);
        DrawIntelLine(cardRect, ref linesY, padding, labelWidth, "RimChat_HoverCardIdentity".Translate(), DiplomacyDialogueHoverCard.BuildIdentityText(resolvedFaction, Owner.Parts.HoverCard.activeHoverPawn), tier >= FactionIntelRevealTier.Low, Owner.Parts.HoverCard.speakerHoverCardAlpha);
        DrawIntelLine(cardRect, ref linesY, padding, labelWidth, "RimChat_HoverCardAge".Translate(), DiplomacyDialogueHoverCard.BuildAgeText(Owner.Parts.HoverCard.activeHoverPawn), tier >= FactionIntelRevealTier.Medium, Owner.Parts.HoverCard.speakerHoverCardAlpha);
        DrawIntelLine(cardRect, ref linesY, padding, labelWidth, "RimChat_HoverCardGender".Translate(), DiplomacyDialogueHoverCard.BuildGenderText(Owner.Parts.HoverCard.activeHoverPawn), tier >= FactionIntelRevealTier.Low, Owner.Parts.HoverCard.speakerHoverCardAlpha);
        DrawIntelLine(cardRect, ref linesY, padding, labelWidth, "RimChat_HoverCardRace".Translate(), DiplomacyDialogueHoverCard.BuildRaceText(Owner.Parts.HoverCard.activeHoverPawn), tier >= FactionIntelRevealTier.Medium, Owner.Parts.HoverCard.speakerHoverCardAlpha);
        DrawIntelLine(cardRect, ref linesY, padding, labelWidth, "RimChat_HoverCardFaction".Translate(), resolvedFaction.Name ?? "???", true, Owner.Parts.HoverCard.speakerHoverCardAlpha);
        DrawIntelLine(cardRect, ref linesY, padding, labelWidth, "RimChat_HoverCardSettlement".Translate(), DiplomacyDialogueHoverCard.BuildSettlementText(resolvedFaction), tier >= FactionIntelRevealTier.High, Owner.Parts.HoverCard.speakerHoverCardAlpha);
        DrawIntelLine(cardRect, ref linesY, padding, labelWidth, "RimChat_HoverCardRelation".Translate(), Owner.Parts.FactionList.GetRelationLabelShort(resolvedFaction.PlayerGoodwill), true, Owner.Parts.HoverCard.speakerHoverCardAlpha);
        DrawIntelLine(cardRect, ref linesY, padding, labelWidth, "RimChat_HoverCardGoodwill".Translate(), resolvedFaction.PlayerGoodwill.ToString("+#;-#;0"), tier >= FactionIntelRevealTier.Medium, Owner.Parts.HoverCard.speakerHoverCardAlpha);

        // Always add special items lines, but reveal only at High tier
        bool specialRevealed = tier >= FactionIntelRevealTier.High;
        if (specialRevealed)
        {
            FactionSpecialItemsManager.Instance.MarkRevealed(resolvedFaction);
        }
        var specialDisplay = FactionSpecialItemsManager.Instance.GetHoverCardDisplay(resolvedFaction);
        DrawIntelLine(cardRect, ref linesY, padding, labelWidth, "RimChat_HoverCardDiscountItem".Translate(), specialDisplay.discountText, specialRevealed, Owner.Parts.HoverCard.speakerHoverCardAlpha);
        DrawIntelLine(cardRect, ref linesY, padding, labelWidth, "RimChat_HoverCardScarceItem".Translate(), specialDisplay.scarceText, specialRevealed, Owner.Parts.HoverCard.speakerHoverCardAlpha);
    }

    GUI.color = Color.white;
    Text.Font = GameFont.Small;
    Owner.Parts.HoverCard.speakerHoverRequestThisFrame = false;
}



internal void DrawHoverCardPortrait(Rect rect, Pawn pawn, Faction targetFaction, float alpha)
{
    Widgets.DrawBoxSolid(rect, new Color(0.13f, 0.15f, 0.19f, 0.95f * alpha));
    GUI.color = new Color(0.42f, 0.48f, 0.57f, 0.92f * alpha);
    Widgets.DrawBox(rect);
    GUI.color = Color.white;

    Texture texture = DiplomacyDialogueSpeakers.ResolveSpeakerPortrait(pawn);
    if (texture == null)
    {
        texture = targetFaction?.def?.FactionIcon;
    }

    Rect drawRect = rect.ContractedBy(3f);
    if (texture != null && texture != BaseContent.BadTex)
    {
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.DrawTexture(drawRect, texture, ScaleMode.ScaleAndCrop, true);
        GUI.color = Color.white;
        return;
    }

    DiplomacyDialogueSpeakers.DrawAvatarFallback(drawRect, pawn?.LabelShortCap ?? targetFaction?.Name ?? "?");
}



internal static void DrawIntelLine(Rect cardRect, ref float y, float padding, float labelWidth, string label, string value, bool revealed, float alpha, float rowHeight = 20f)
{
    string displayValue = revealed ? (value ?? "???") : "???";
    float valueWidth = cardRect.width - padding * 2 - labelWidth;

    Text.Font = GameFont.Tiny;
    GUI.color = new Color(0.78f, 0.82f, 0.9f, alpha);
    Widgets.Label(new Rect(cardRect.x + padding, y, labelWidth, rowHeight), label);
    GUI.color = revealed
        ? new Color(0.96f, 0.97f, 1f, alpha)
        : new Color(0.7f, 0.74f, 0.82f, DiplomacyDialogueHoverCard.HoverCardObscuredAlpha * alpha);
    bool prevWrap = Text.WordWrap;
    Text.WordWrap = rowHeight > 22f;
    Widgets.Label(new Rect(cardRect.x + padding + labelWidth, y, valueWidth, rowHeight), displayValue);
    Text.WordWrap = prevWrap;
    y += rowHeight;
}
}
