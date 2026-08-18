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

internal sealed class DiplomacyDialogueTypingStatus : DiplomacyDialogueCollaborator
{
    internal DiplomacyDialogueTypingStatus(Dialog_DiplomacyDialogue owner) : base(owner) { }


internal const float TypingStatusRotateSeconds = 2.5f;


internal const float TypingStatusPulseSpeed = 3.1f;


internal const float TypingStatusSweepCycleSeconds = 1.8f;


internal static readonly string[] DiplomacyTypingStatusKeys = new[]
{
    "RimChat_DiplomacyTypingStatus_01",
    "RimChat_DiplomacyTypingStatus_02",
    "RimChat_DiplomacyTypingStatus_03",
    "RimChat_DiplomacyTypingStatus_04",
    "RimChat_DiplomacyTypingStatus_05",
    "RimChat_DiplomacyTypingStatus_06",
    "RimChat_DiplomacyTypingStatus_07",
    "RimChat_DiplomacyTypingStatus_08",
    "RimChat_DiplomacyTypingStatus_09",
    "RimChat_DiplomacyTypingStatus_10",
    "RimChat_DiplomacyTypingStatus_11",
    "RimChat_DiplomacyTypingStatus_12",
    "RimChat_DiplomacyTypingStatus_13",
    "RimChat_DiplomacyTypingStatus_14",
    "RimChat_DiplomacyTypingStatus_15",
    "RimChat_DiplomacyTypingStatus_16"
};



internal void DrawDiplomacyTypingStatus(Rect rect)
{
    Rect panelRect = BuildTypingStatusPanelRect(rect);
    DrawTypingStatusPanel(panelRect);
    DrawTypingStatusText(panelRect, ResolveDiplomacyTypingStatusText());
    DrawTypingStatusDots(panelRect);
    DrawTypingStatusSweep(panelRect);
    ResetTypingStatusStyle();
}



internal static Rect BuildTypingStatusPanelRect(Rect rect)
{
    float width = Mathf.Max(180f, rect.width);
    return new Rect(rect.x, rect.y, width, Mathf.Max(18f, rect.height));
}



internal void DrawTypingStatusPanel(Rect panelRect)
{
    Owner.Parts.MessageView.DrawRoundedRect(panelRect, new Color(0.12f, 0.2f, 0.29f, 0.76f), 7f);
    float outlineAlpha = 0.24f + 0.14f * Mathf.Sin(Time.realtimeSinceStartup * 1.9f);
    GUI.color = new Color(0.47f, 0.79f, 1f, outlineAlpha);
    Widgets.DrawBox(panelRect);
    GUI.color = Color.white;
}



internal string ResolveDiplomacyTypingStatusText()
{
    if (Owner.Parts.Feedback.TryGetVisibleAiRequestStatus(out AIRequestResult status) && DiplomacyRequestFeedback.IsQueuedRequestState(status))
    {
        return Owner.Parts.Feedback.BuildAiTurnStatusText();
    }

    string fallback = "RimChat_AIIsTyping".Translate();
    if (DiplomacyTypingStatusKeys.Length == 0)
    {
        return fallback;
    }

    int index = (int)(Time.realtimeSinceStartup / TypingStatusRotateSeconds);
    string key = DiplomacyTypingStatusKeys[index % DiplomacyTypingStatusKeys.Length];
    TaggedString translated = key.Translate();
    return translated.RawText == key ? fallback : translated.RawText;
}



internal void DrawTypingStatusText(Rect panelRect, string statusText)
{
    Color previousColor = GUI.color;
    GameFont previousFont = Text.Font;
    TextAnchor previousAnchor = Text.Anchor;
    Rect textRect = new Rect(panelRect.x + 8f, panelRect.y, panelRect.width - 56f, panelRect.height - 3f);
    GUI.color = new Color(0.84f, 0.93f, 1f, 0.95f);
    Text.Font = GameFont.Tiny;
    Text.Anchor = TextAnchor.MiddleLeft;
    DiplomacyDialogueInput.DrawSingleLineClippedLabel(textRect, statusText);
    Text.Anchor = previousAnchor;
    Text.Font = previousFont;
    GUI.color = previousColor;
}



internal void DrawTypingStatusDots(Rect panelRect)
{
    float phase = Time.realtimeSinceStartup * TypingStatusPulseSpeed;
    float dotSize = 4f;
    float dotY = panelRect.y + panelRect.height * 0.5f - dotSize * 0.5f - 1f;
    float startX = panelRect.xMax - 25f;
    for (int i = 0; i < 3; i++)
    {
        DrawTypingStatusDot(startX + i * 6f, dotY, dotSize, phase - i * 0.85f);
    }

    GUI.color = Color.white;
}



internal void DrawTypingStatusDot(float x, float y, float size, float phase)
{
    float alpha = 0.22f + 0.72f * (0.5f + 0.5f * Mathf.Sin(phase));
    GUI.color = new Color(0.63f, 0.89f, 1f, alpha);
    GUI.DrawTexture(new Rect(x, y, size, size), Dialog_DiplomacyDialogue.CircleTexture);
}



internal void DrawTypingStatusSweep(Rect panelRect)
{
    Rect trackRect = new Rect(panelRect.x + 8f, panelRect.yMax - 3f, panelRect.width - 16f, 1.5f);
    Widgets.DrawBoxSolid(trackRect, new Color(0.38f, 0.53f, 0.63f, 0.28f));
    float progress = (Time.realtimeSinceStartup % TypingStatusSweepCycleSeconds) / TypingStatusSweepCycleSeconds;
    float sweepWidth = Mathf.Max(32f, trackRect.width * 0.28f);
    float sweepX = Mathf.Lerp(trackRect.x - sweepWidth, trackRect.xMax, progress);
    DrawTypingStatusSweepSegment(trackRect, sweepX, sweepWidth);
}



internal static void DrawTypingStatusSweepSegment(Rect trackRect, float sweepX, float sweepWidth)
{
    float segmentStart = Mathf.Max(trackRect.x, sweepX);
    float segmentEnd = Mathf.Min(trackRect.xMax, sweepX + sweepWidth);
    if (segmentEnd <= segmentStart)
    {
        return;
    }

    GUI.color = new Color(0.68f, 0.91f, 1f, 0.82f);
    GUI.DrawTexture(new Rect(segmentStart, trackRect.y, segmentEnd - segmentStart, trackRect.height), Dialog_DiplomacyDialogue.WhiteTexture);
    GUI.color = Color.white;
}



internal static void ResetTypingStatusStyle()
{
    Text.Anchor = TextAnchor.UpperLeft;
    Text.Font = GameFont.Small;
    GUI.color = Color.white;
}
}
