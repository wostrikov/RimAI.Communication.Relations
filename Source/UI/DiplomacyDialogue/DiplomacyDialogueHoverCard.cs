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

internal sealed class DiplomacyDialogueHoverCard : DiplomacyDialogueCollaborator
{
    internal DiplomacyDialogueHoverCard(Dialog_DiplomacyDialogue owner) : base(owner) { }


internal const float HoverCardWidth = 360f;


internal const float HoverCardPortraitSize = 96f;


internal const float HoverCardPadding = 12f;


internal const float HoverCardSectionGap = 8f;


internal const float HoverCardLineHeight = 22f;


internal const float HoverCardRevealDuration = 0.16f;


internal const float HoverCardExpandMargin = 5f;


internal const int SpeakerHoverBioMaxLength = 500;


internal const string SpeakerHoverBioOverflowSuffix = "...";


internal const int GoodwillRevealTierHostile = -60;


internal const int GoodwillRevealTierLow = 0;


internal const int GoodwillRevealTierMedium = 40;


internal const int GoodwillRevealTierHigh = 75;


internal const float HoverCardObscuredAlpha = 0.82f;


internal readonly Dictionary<string, float> hoverCardAlphaByKey = new Dictionary<string, float>(StringComparer.Ordinal);


internal Pawn activeHoverPawn;


internal Faction activeHoverFaction;


internal Vector2 activeHoverScreenPos;


internal float speakerHoverCardAlpha;


internal bool speakerHoverRequestThisFrame;


internal readonly Dictionary<int, int> factionMaxGoodwillSeen = new Dictionary<int, int>();



internal void DrawFactionHoverCard(Faction targetFaction, Rect anchorRect)
{
    if (targetFaction == null)
    {
        return;
    }

    Rect expandedRect = anchorRect.ExpandedBy(HoverCardExpandMargin);
    bool isHovered = Mouse.IsOver(expandedRect);
    float alpha = UpdateHoverCardAlpha($"faction:{targetFaction.loadID}", isHovered);
    if (alpha <= 0.01f)
    {
        return;
    }

    DrawHoverCardForFaction(targetFaction, null, anchorRect, alpha);
}



internal float UpdateHoverCardAlpha(string key, bool hovered)
{
    if (string.IsNullOrWhiteSpace(key))
    {
        return 0f;
    }

    float current = hoverCardAlphaByKey.TryGetValue(key, out float value) ? value : 0f;
    float speed = Time.unscaledDeltaTime / (hovered ? HoverCardRevealDuration : HoverCardRevealDuration * 2f);
    float target = hovered ? 1f : 0f;
    float next = Mathf.MoveTowards(current, target, speed);
    hoverCardAlphaByKey[key] = next;
    return next;
}



internal void CleanupHoverCardAlpha(IEnumerable<string> activeKeys)
{
    if (activeKeys == null)
    {
        return;
    }

    HashSet<string> keep = new HashSet<string>(activeKeys, StringComparer.Ordinal);
    List<string> staleKeys = hoverCardAlphaByKey.Keys.Where(key => !keep.Contains(key)).ToList();
    for (int i = 0; i < staleKeys.Count; i++)
    {
        hoverCardAlphaByKey.Remove(staleKeys[i]);
    }
}



internal void DrawHoverCardForFaction(Faction targetFaction, Pawn explicitPawn, Rect anchorRect, float alpha)
{
    if (targetFaction == null && explicitPawn == null)
    {
        return;
    }

    try
    {
        Pawn subjectPawn = ResolveHoverCardPawn(targetFaction, explicitPawn) ?? explicitPawn;
        Faction fallbackFaction = targetFaction ?? subjectPawn?.Faction ?? faction;
        if (fallbackFaction == null)
        {
            return;
        }

        if (fallbackFaction.IsPlayer)
        {
            DrawHoverCardForPlayerPawn(subjectPawn, anchorRect, alpha);
            return;
        }

        FactionIntelRevealTier tier = ResolveFactionRevealTier(fallbackFaction);
        List<HoverCardLine> lines = BuildHoverCardLines(fallbackFaction, subjectPawn, tier);
        float contentHeight = CalculateHoverCardContentHeight(lines, fallbackFaction);
        Rect cardRect = BuildHoverCardRect(anchorRect, contentHeight);

        Owner.Parts.HoverCardDraw.DrawHoverCardChrome(cardRect, alpha, Owner.Parts.FactionList.GetGoodwillColor(fallbackFaction.PlayerGoodwill));
        Owner.Parts.HoverCardDraw.DrawHoverCardContent(cardRect, fallbackFaction, subjectPawn, lines, tier, alpha);
    }
    catch (Exception ex)
    {
        Log.Warning($"[RimAI.Relations] Failed to draw hover card for faction={targetFaction?.Name}: {ex.Message}");
    }
}



internal void DrawHoverCardForPlayerPawn(Pawn pawn, Rect anchorRect, float alpha)
{
    if (pawn == null)
    {
        return;
    }

    List<HoverCardLine> lines = new List<HoverCardLine>();
    AddHoverCardLine(lines, "RimChat_HoverCardBio".Translate(), pawn.LabelShort ?? "Colonist".Translate(), true);
    AddHoverCardLine(lines, "RimChat_HoverCardFaction".Translate(), Faction.OfPlayer.Name ?? "RimChat_HoverCardPlayerFaction".Translate(), true);

    float contentHeight = CalculateHoverCardContentHeightPlayer(lines);
    Rect cardRect = BuildHoverCardRect(anchorRect, contentHeight);

    Owner.Parts.HoverCardDraw.DrawHoverCardChrome(cardRect, alpha, Color.white);
    Owner.Parts.HoverCardDraw.DrawHoverCardContent(cardRect, null, pawn, lines, FactionIntelRevealTier.High, alpha);
}



internal static float CalculateHoverCardContentHeightPlayer(List<HoverCardLine> lines)
{
    int count = lines?.Count ?? 0;
    return HoverCardPadding * 2f + HoverCardPortraitSize + HoverCardSectionGap + count * HoverCardLineHeight + 20f;
}



internal Pawn ResolveHoverCardPawn(Faction targetFaction, Pawn explicitPawn)
{
    if (DiplomacyDialogueSpeakers.IsEligibleSpeakerPawn(explicitPawn, targetFaction))
    {
        return explicitPawn;
    }

    if (DiplomacyDialogueSpeakers.IsEligibleSpeakerPawn(targetFaction?.leader, targetFaction))
    {
        return targetFaction.leader;
    }

    if (DiplomacyDialogueSpeakers.IsEligibleSpeakerPawn(Owner.Parts.Speakers.sessionFallbackFactionSpeaker, targetFaction))
    {
        return Owner.Parts.Speakers.sessionFallbackFactionSpeaker;
    }

    return null;
}



internal FactionIntelRevealTier ResolveFactionRevealTier(Faction f)
{
    if (f == null)
    {
        return FactionIntelRevealTier.Hostile;
    }

    int current = f.PlayerGoodwill;
    int loadId = f.loadID;
    if (!factionMaxGoodwillSeen.TryGetValue(loadId, out int maxSeen) || current > maxSeen)
    {
        factionMaxGoodwillSeen[loadId] = current;
        maxSeen = current;
    }

    int effective = Mathf.Max(current, maxSeen);
    if (effective >= GoodwillRevealTierHigh)
    {
        return FactionIntelRevealTier.High;
    }
    if (effective >= GoodwillRevealTierMedium)
    {
        return FactionIntelRevealTier.Medium;
    }
    if (effective >= GoodwillRevealTierLow)
    {
        return FactionIntelRevealTier.Low;
    }
    return FactionIntelRevealTier.Hostile;
}



internal List<HoverCardLine> BuildHoverCardLines(Faction targetFaction, Pawn subjectPawn, FactionIntelRevealTier tier)
{
    List<HoverCardLine> lines = new List<HoverCardLine>();
    AddHoverCardLine(lines, "RimChat_HoverCardBio".Translate(), BuildFactionBioText(targetFaction), tier >= FactionIntelRevealTier.Low);
    AddHoverCardLine(lines, "RimChat_HoverCardIdentity".Translate(), BuildIdentityText(targetFaction, subjectPawn), tier >= FactionIntelRevealTier.Low);
    AddHoverCardLine(lines, "RimChat_HoverCardAge".Translate(), BuildAgeText(subjectPawn), tier >= FactionIntelRevealTier.Medium);
    AddHoverCardLine(lines, "RimChat_HoverCardGender".Translate(), BuildGenderText(subjectPawn), tier >= FactionIntelRevealTier.Low);
    AddHoverCardLine(lines, "RimChat_HoverCardRace".Translate(), BuildRaceText(subjectPawn), tier >= FactionIntelRevealTier.Medium);
    AddHoverCardLine(lines, "RimChat_HoverCardFaction".Translate(), targetFaction.Name ?? "RimChat_HoverCardUnknownValue".Translate().ToString(), true);
    AddHoverCardLine(lines, "RimChat_HoverCardLeader".Translate(), BuildLeaderText(targetFaction), tier >= FactionIntelRevealTier.Medium);
    AddHoverCardLine(lines, "RimChat_HoverCardSettlement".Translate(), BuildSettlementText(targetFaction), tier >= FactionIntelRevealTier.High);
    AddHoverCardLine(lines, "RimChat_HoverCardRelation".Translate(), Owner.Parts.FactionList.GetRelationLabelShort(targetFaction.PlayerGoodwill), true);
    AddHoverCardLine(lines, "RimChat_HoverCardGoodwill".Translate(), targetFaction.PlayerGoodwill.ToString("+#;-#;0"), tier >= FactionIntelRevealTier.Medium);

    // Always add special items lines, but reveal only at High tier
    bool specialRevealed = tier >= FactionIntelRevealTier.High;
    if (specialRevealed)
    {
        FactionSpecialItemsManager.Instance.MarkRevealed(targetFaction);
    }
    var specialDisplay = FactionSpecialItemsManager.Instance.GetHoverCardDisplay(targetFaction);
    AddHoverCardLine(lines, "RimChat_HoverCardDiscountItem".Translate(), specialDisplay.discountText, specialRevealed);
    AddHoverCardLine(lines, "RimChat_HoverCardScarceItem".Translate(), specialDisplay.scarceText, specialRevealed);

    return lines;
}



internal static void AddHoverCardLine(List<HoverCardLine> lines, string label, string value, bool revealed)
{
    if (lines == null || string.IsNullOrWhiteSpace(label))
    {
        return;
    }

    string normalizedValue = string.IsNullOrWhiteSpace(value)
        ? "RimChat_HoverCardUnknownValue".Translate().ToString()
        : value.Trim();
    lines.Add(new HoverCardLine
    {
        Label = label,
        Value = revealed ? normalizedValue : "RimChat_HoverCardUnknownMask".Translate().ToString(),
        IsObscured = !revealed
    });
}



internal static string BuildFactionBioText(Faction targetFaction)
{
    string text = targetFaction?.def?.description;
    if (string.IsNullOrWhiteSpace(text))
    {
        return "RimChat_HoverCardNoBio".Translate();
    }

    return text.Trim().Replace("\r", string.Empty);
}



internal static string BuildSpeakerHoverBioText(Faction resolvedFaction, bool isPlayer, Pawn activePawn)
{
    string rawText = isPlayer
        ? (activePawn?.story?.TitleCap ?? "Colonist")
        : BuildFactionBioText(resolvedFaction);

    if (string.IsNullOrEmpty(rawText))
    {
        return string.Empty;
    }

    string normalized = rawText.Replace("\r", string.Empty);
    if (normalized.Length <= SpeakerHoverBioMaxLength)
    {
        return normalized;
    }

    return normalized.Substring(0, SpeakerHoverBioMaxLength) + SpeakerHoverBioOverflowSuffix;
}



internal static string BuildIdentityText(Faction targetFaction, Pawn subjectPawn)
{
    string title = (targetFaction?.def?.leaderTitle ?? string.Empty).Trim();
    if (subjectPawn?.story != null && !string.IsNullOrWhiteSpace(subjectPawn.story.TitleCap))
    {
        return subjectPawn.story.TitleCap;
    }
    if (!string.IsNullOrWhiteSpace(title))
    {
        return title;
    }
    if (!string.IsNullOrWhiteSpace(subjectPawn?.kindDef?.label))
    {
        return subjectPawn.kindDef.label.CapitalizeFirst();
    }
    return "RimChat_HoverCardUnknownValue".Translate();
}



internal static string BuildAgeText(Pawn subjectPawn)
{
    if (subjectPawn?.ageTracker == null)
    {
        return "RimChat_HoverCardUnknownValue".Translate();
    }

    return subjectPawn.ageTracker.AgeBiologicalYears.ToString();
}



internal static string BuildGenderText(Pawn subjectPawn)
{
    if (subjectPawn == null)
    {
        return "RimChat_HoverCardUnknownValue".Translate();
    }

    return subjectPawn.gender.GetLabel();
}



internal static string BuildRaceText(Pawn subjectPawn)
{
    if (subjectPawn == null)
    {
        return "RimChat_HoverCardUnknownValue".Translate();
    }

    string xenotype = string.Empty;
    try
    {
        if (subjectPawn.genes != null)
        {
            xenotype = subjectPawn.genes.XenotypeLabelCap ?? subjectPawn.genes.xenotypeName ?? string.Empty;
        }
    }
    catch (Exception ex)
    {
        Log.Warning($"[RimAI.Relations] Failed to resolve xenotype for pawn={subjectPawn.LabelShortCap}: {ex.Message}");
        xenotype = string.Empty;
    }

    string race = subjectPawn.def?.label ?? string.Empty;
    if (!string.IsNullOrWhiteSpace(xenotype) && !string.Equals(xenotype, race, StringComparison.OrdinalIgnoreCase))
    {
        return $"{race}/{xenotype}";
    }
    return string.IsNullOrWhiteSpace(race) ? "RimChat_HoverCardUnknownValue".Translate().ToString() : race.CapitalizeFirst();
}



internal static string BuildLeaderText(Faction targetFaction)
{
    Pawn leader = targetFaction?.leader;
    if (leader?.Name != null)
    {
        return leader.Name.ToStringFull;
    }
    if (leader != null)
    {
        return leader.LabelShortCap;
    }
    return "RimChat_HoverCardUnknownValue".Translate();
}



internal static string BuildSettlementText(Faction targetFaction)
{
    if (targetFaction == null || Find.WorldObjects == null)
    {
        return "RimChat_HoverCardUnknownValue".Translate();
    }

    List<Settlement> settlements = Find.WorldObjects.SettlementBases
        .Where(s => s != null && s.Faction == targetFaction)
        .ToList();
    if (settlements.Count == 0)
    {
        return "RimChat_HoverCardNoSettlement".Translate();
    }

    Settlement primary = settlements[0];
    if (string.IsNullOrWhiteSpace(primary.LabelCap))
    {
        return "RimChat_HoverCardSettlementCountOnly".Translate(settlements.Count);
    }
    return "RimChat_HoverCardSettlementSummary".Translate(primary.LabelCap, settlements.Count);
}



internal static float CalculateHoverCardContentHeight(List<HoverCardLine> lines, Faction targetFaction)
{
    int count = lines?.Count ?? 0;
    float baseHeight = HoverCardPadding * 2f + HoverCardPortraitSize + HoverCardSectionGap + (count - 1) * HoverCardLineHeight + 20f;
    string bioText = BuildFactionBioText(targetFaction);
    float bioWidth = Mathf.Max(120f, HoverCardWidth - HoverCardPadding * 2f - 74f);
    Text.Font = GameFont.Small;
    float bioHeight = Text.CalcHeight(bioText, bioWidth);
    Text.Font = GameFont.Small;
    return baseHeight + Mathf.Max(HoverCardLineHeight, bioHeight);
}



internal Rect BuildHoverCardRect(Rect anchorRect, float contentHeight)
{
    float preferredX = anchorRect.xMax + 12f;
    float preferredY = anchorRect.center.y - contentHeight * 0.3f;
    Rect windowRect = new Rect(0f, 0f, Screen.width, Screen.height);

    if (preferredX + HoverCardWidth > windowRect.xMax - 8f)
    {
        preferredX = anchorRect.x - HoverCardWidth - 12f;
    }
    if (preferredX < windowRect.x + 8f)
    {
        preferredX = windowRect.x + 8f;
    }

    preferredY = Mathf.Clamp(preferredY, windowRect.y + 44f, windowRect.yMax - contentHeight - 8f);
    return new Rect(preferredX, preferredY, HoverCardWidth, contentHeight);
}
}

