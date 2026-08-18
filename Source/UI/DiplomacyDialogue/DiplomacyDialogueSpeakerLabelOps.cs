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

namespace Ustas.RimAI.Communication.Relations.UI
{
    /// <summary>
    /// Speaker label, portrait, and avatar layout helpers.
    /// </summary>
    internal static class DiplomacyDialogueSpeakerLabelOps
    {
internal static string ResolveFactionSenderName(Faction currentFaction, Pawn factionSpeaker)
{
    string speakerName = ResolvePawnLabel(factionSpeaker);
    if (!string.IsNullOrWhiteSpace(speakerName))
    {
        return speakerName;
    }

    return currentFaction?.Name ?? "Unknown";
}



internal static bool IsOutboundPrisonerInfoMessage(DialogueMessageData message)
{
    return message != null &&
           message.HasInlineImage() &&
           string.Equals(message.imageSourceUrl, DiplomacyRansomProofWorkflow.RansomProofImageSourceUrl, StringComparison.OrdinalIgnoreCase);
}



internal static string ResolvePawnLabel(Pawn pawn)
{
    if (pawn?.Name != null)
    {
        string shortName = pawn.Name.ToStringShort;
        if (!string.IsNullOrWhiteSpace(shortName))
        {
            return shortName;
        }
    }

    return pawn?.LabelShort;
}



internal static float GetMessageBubbleTrackWidth(float viewportWidth)
{
    float left = DiplomacyDialogueSpeakers.MessageSidePadding + DiplomacyDialogueSpeakers.MessageAvatarSize + DiplomacyDialogueSpeakers.MessageAvatarGap;
    float right = viewportWidth - DiplomacyDialogueSpeakers.MessageSidePadding - DiplomacyDialogueSpeakers.MessageAvatarSize - DiplomacyDialogueSpeakers.MessageAvatarGap;
    return Mathf.Max(140f, right - left);
}



internal static Texture ResolveSpeakerPortrait(Pawn pawn)
{
    if (!DiplomacyDialogueSpeakerPawnOps.IsEligibleSpeakerPawn(pawn))
    {
        return null;
    }

    try
    {
        return PortraitsCache.Get(
            pawn,
            new Vector2(DiplomacyDialogueSpeakers.AvatarPortraitRequestSize, DiplomacyDialogueSpeakers.AvatarPortraitRequestSize),
            Rot4.South,
            DiplomacyDialogueSpeakers.AvatarCameraOffset,
            DiplomacyDialogueSpeakers.AvatarCameraZoom);
    }
    catch
    {
        return null;
    }
}



internal static void DrawAvatarFallback(Rect avatarRect, string label)
{
    string letter = string.IsNullOrWhiteSpace(label) ? "?" : label.Trim()[0].ToString().ToUpperInvariant();
    Text.Font = GameFont.Tiny;
    Text.Anchor = TextAnchor.MiddleCenter;
    GUI.color = new Color(0.9f, 0.92f, 0.97f, 0.95f);
    Widgets.Label(avatarRect, letter);
    GUI.color = Color.white;
    Text.Anchor = TextAnchor.UpperLeft;
    Text.Font = GameFont.Small;
}
    }
}
