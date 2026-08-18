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

internal sealed class DiplomacyDialogueChrome : DiplomacyDialogueCollaborator
{
    internal DiplomacyDialogueChrome(Dialog_DiplomacyDialogue owner) : base(owner) { }



internal void DrawTitleBar(Rect inRect)
{
    Widgets.DrawBoxSolid(new Rect(inRect.x, inRect.y, inRect.width, Dialog_DiplomacyDialogue.LayoutTitleBarHeight), new Color(0.15f, 0.15f, 0.18f));

    Text.Font = GameFont.Small;
    GUI.color = new Color(0.9f, 0.9f, 0.95f);
    string title = "RimChat_TerminalTitle".Translate();
    float titleY = inRect.y + (Dialog_DiplomacyDialogue.LayoutTitleBarHeight - 20f) / 2f;
    Widgets.Label(new Rect(inRect.x + Dialog_DiplomacyDialogue.LayoutTitleLeftPadding, titleY, 250f, 20f), title);

    Text.Font = GameFont.Small;
    GUI.color = new Color(0.7f, 0.7f, 0.75f);
    string factionTitle = faction.Name ?? "Unknown";
    float factionTitleWidth = Text.CalcSize(factionTitle).x;
    float centerX = inRect.x + (inRect.width - factionTitleWidth) / 2f;
    float factionLineY = inRect.y + Dialog_DiplomacyDialogue.LayoutTitleFactionLineTopPadding;
    Widgets.Label(new Rect(centerX, factionLineY, factionTitleWidth + 10f, 25f), factionTitle);
    Owner.Parts.Presence.DrawCurrentFactionPresenceStatus(new Rect(centerX + factionTitleWidth + 14f, factionLineY - 1f, 132f, 24f));

    DrawVersionLine(inRect, centerX, factionTitleWidth);

    Text.Font = GameFont.Small;
    GUI.color = Color.white;
}



internal void DrawVersionLine(Rect inRect, float centerX, float factionTitleWidth)
{
    string versionText = GetDialogueHeaderVersionText();
    string helpLabel = "RimChat_DiplomacyHelpButton".Translate();
    Text.Font = GameFont.Tiny;
    GUI.color = new Color(0.72f, 0.86f, 0.96f);
    float versionWidth = Text.CalcSize(versionText).x;
    float helpWidth = Text.CalcSize(helpLabel).x;
    float closeButtonX = inRect.xMax - (Dialog_DiplomacyDialogue.LayoutCloseButtonSize + 5f);
    float helpX = closeButtonX - helpWidth - 10f;
    float versionX = helpX - versionWidth - 16f;
    Rect versionRect = new Rect(versionX, inRect.y + Dialog_DiplomacyDialogue.LayoutTitleVersionLineTopPadding, versionWidth + 10f, Dialog_DiplomacyDialogue.LayoutTitleVersionLineHeight);
    Widgets.Label(versionRect, versionText);
    TooltipHandler.TipRegion(versionRect, "RimChat_DiplomacyVersionTooltip".Translate());
    if (Widgets.ButtonInvisible(versionRect))
    {
        OpenVersionLogLanguageMenu();
    }

    Rect helpRect = new Rect(helpX, inRect.y + Dialog_DiplomacyDialogue.LayoutTitleVersionLineTopPadding, helpWidth + 8f, Dialog_DiplomacyDialogue.LayoutTitleVersionLineHeight);
    GUI.color = new Color(0.72f, 0.86f, 0.96f);
    Widgets.Label(helpRect, helpLabel);
    if (Widgets.ButtonInvisible(helpRect))
    {
        OpenHelpLanguageMenu();
    }
    TooltipHandler.TipRegion(helpRect, "RimChat_DiplomacyHelpTooltip".Translate());

    GUI.color = Color.white;
    Text.Font = GameFont.Small;
}



internal string GetDialogueHeaderVersionText()
{
    var settings = RelationsMod.Instance?.InstanceSettings;
    string version = settings?.GetVersionDisplayVersion();
    if (string.IsNullOrWhiteSpace(version))
    {
        return "0.0.0";
    }

    return version.Trim();
}



internal void OpenVersionLogLanguageMenu()
{
    var options = new List<FloatMenuOption>
    {
        new FloatMenuOption("RimChat_VersionLogLanguageChinese".Translate(), OpenChineseVersionLog),
        new FloatMenuOption("RimChat_VersionLogLanguageEnglish".Translate(), OpenEnglishVersionLog)
    };
    Find.WindowStack.Add(new FloatMenu(options));
}



internal void OpenChineseVersionLog()
{
    OpenVersionLogForLanguage("ChineseSimplified", "RimChat_VersionLogLanguageChinese");
}



internal void OpenEnglishVersionLog()
{
    OpenVersionLogForLanguage("English", "RimChat_VersionLogLanguageEnglish");
}



internal void OpenHelpLanguageMenu()
{
    var options = new List<FloatMenuOption>
    {
        new FloatMenuOption("RimChat_VersionLogLanguageChinese".Translate(), OpenChineseHelp),
        new FloatMenuOption("RimChat_VersionLogLanguageEnglish".Translate(), OpenEnglishHelp)
    };
    Find.WindowStack.Add(new FloatMenu(options));
}



internal void OpenChineseHelp()
{
    OpenHelpForLanguage("ChineseSimplified", "RimChat_VersionLogLanguageChinese");
}



internal void OpenEnglishHelp()
{
    OpenHelpForLanguage("English", "RimChat_VersionLogLanguageEnglish");
}



internal void OpenHelpForLanguage(string languageFolder, string languageKey)
{
    var settings = RelationsMod.Instance?.InstanceSettings;
    if (settings == null)
    {
        return;
    }

    string title = "RimChat_HelpWindowTitleByLanguage".Translate(languageKey.Translate());
    string content = settings.GetHelpDisplayContentForLanguage(languageFolder);
    Find.WindowStack.Add(new Dialog_VersionLogViewer(title, content));
}



internal void OpenVersionLogForLanguage(string languageFolder, string languageKey)
{
    var settings = RelationsMod.Instance?.InstanceSettings;
    if (settings == null)
    {
        return;
    }

    string title = "RimChat_VersionLogWindowTitleByLanguage".Translate(languageKey.Translate());
    string content = settings.GetVersionLogDisplayContentForLanguage(languageFolder);
    Find.WindowStack.Add(new Dialog_VersionLogViewer(title, content));
}



internal string GetWeatherAndTimeText()
{
    var map = Find.CurrentMap;
    if (map == null) return "";

    // Get温度
    float temperature = map.mapTemperature?.OutdoorTemp ?? 0f;
    string tempText = $"{temperature:F0}°C";

    // Get游戏时间
    int hour = GenLocalDate.HourOfDay(map);
    int minute = (int)((GenLocalDate.DayPercent(map) * 24f - hour) * 60f);
    string timeText = $"{hour:D2}:{minute:D2}";

    return $"{tempText}  {timeText}";
}


// Bezel frame insets — asymmetric, matching the actual transparent region.
// Original texture: 1402x1122, transparent X(232..1287) Y(167..994)
// Scale factor: 960/1402 ≈ 0.685, 720/1122 ≈ 0.642
internal const float BezelInsetLeft = 159f;

// 232 * 0.685
    internal const float BezelInsetRight = 79f;

// (1402-1287) * 0.685
    internal const float BezelInsetTop = 107f;

// 167 * 0.642
   internal const float BezelInsetBottom = 82f;

// (1122-994) * 0.642

     // Note: doWindowBackground = false is set in the constructor
     // to disable the default semi-transparent black background.
     // The bezel texture provides its own background.

     // CRT bezel textures (standard + spacer + fallout variants)
     internal static Texture2D TexCRTBezel;


internal static Texture2D TexCRTBezelSpacer;


internal static Texture2D TexCRTBezelFallout;



// Active bezel index: 0=Standard, 1=Spacer, 2=Fallout
internal static int ActiveBezelIndex
{
    get => RelationsMod.Settings?.ActiveBezelIndex ?? 0;
    set
    {
        var settings = RelationsMod.Settings;
        if (settings != null) settings.ActiveBezelIndex = value;
    }
}


internal const int BezelIndexStandard = 0;


internal const int BezelIndexSpacer = 1;


internal const int BezelIndexFallout = 2;



// Texture switch hotspot — rect in original texture coords (1402x1122): (40,665)→(148,783)
// Scaled to window 960x720: (27,404)→(101,480)
internal static readonly Rect SwitchHotspotWindow = new Rect(27f, 404f, 74f, 76f);



// Close button hotspot — rect in original texture coords (1402x1122): (1099,1036)→(1285,1110)
// Scaled to window 960x720: (753,665)→(880,712)
internal static readonly Rect CloseHotspotWindow = new Rect(724f, 632f, 127f, 47f);


internal static bool _closeHotspotWasHovering;



// Terminal UI scale override
internal float _originalUIScale;


internal bool _scaleOverridden;


internal static bool _switchHotspotWasHovering;



// CRT overlay material (barrel distortion + scanlines + green tint + vignette)
internal static Material MatCRT;



// Procedural scanline texture fallback (no shader needed)
internal static Texture2D _scanlineOverlay;


internal static Texture2D ScanlineOverlay
{
    get
    {
        if (_scanlineOverlay == null)
            _scanlineOverlay = CreateScanlineTexture();
        return _scanlineOverlay;
    }
}



/// <summary>
/// Called from the main static constructor to load CRT resources.
/// </summary>
internal static void InitTerminalTheme()
{
    TexCRTBezel = ContentFinder<Texture2D>.Get("UI/RimChat/Terminal/terminal", false);
    TexCRTBezelSpacer = ContentFinder<Texture2D>.Get("UI/RimChat/Terminal/terminal_spacer", false);
    TexCRTBezelFallout = ContentFinder<Texture2D>.Get("UI/RimChat/Terminal/terminal_fallout", false);

    Shader crtShader = Shader.Find("RimChat/CRT");
    if (crtShader != null)
    {
        MatCRT = new Material(crtShader);
    }
}



/// <summary>
/// Shrink a rect inward by the bezel frame insets (asymmetric).
/// Content should be drawn inside this rect to avoid being covered by the bezel.
/// </summary>
internal static Rect ShrinkForBezel(Rect rect)
{
    return new Rect(
        rect.x + BezelInsetLeft,
        rect.y + BezelInsetTop,
        rect.width - BezelInsetLeft - BezelInsetRight,
        rect.height - BezelInsetTop - BezelInsetBottom);
}



/// <summary>
/// Draw CRT overlay (green tint + scanlines + vignette) on the content area.
/// Drawn AFTER content but BEFORE hover cards.
/// </summary>
internal static void DrawCRTOverlay(Rect contentRect)
{
    if (MatCRT != null)
    {
        DrawCRTWithShader(contentRect);
    }
    else
    {
        DrawCRTProcedural(contentRect);
    }
}



/// <summary>
/// Draw the bezel frame as the OUTERMOST background layer.
/// Call this BEFORE drawing any content.
/// </summary>
internal void DrawCRTBezelBackground(Rect windowRect)
{
    Texture2D tex = GetActiveBezelTexture();
    if (tex == null) return;
    GUI.DrawTexture(windowRect, tex);

    // Texture switch hotspot — always visible (Fallout always unlocked, Spacer unlocks later)
    bool hasSpacer = IsSpacerTechLevel() && TexCRTBezelSpacer != null;
    bool hasFallout = TexCRTBezelFallout != null;
    bool hasAnyAlternative = hasSpacer || hasFallout;
    if (hasAnyAlternative)
    {
        Rect hotspot = new Rect(
            windowRect.x + SwitchHotspotWindow.x,
            windowRect.y + SwitchHotspotWindow.y,
            SwitchHotspotWindow.width,
            SwitchHotspotWindow.height);

        bool hovering = Mouse.IsOver(hotspot);

        if (hovering)
        {
            // Hover highlight
            Color prev = GUI.color;
            GUI.color = new Color(0.3f, 0.8f, 0.5f, 0.18f);
            GUI.DrawTexture(hotspot, BaseContent.WhiteTex);
            GUI.color = prev;

            // Play hover sound once
            if (!_switchHotspotWasHovering)
            {
                SoundDefOf.Mouseover_ButtonToggle.PlayOneShotOnCamera();
            }

            // Click to cycle to next available theme
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                CycleToNextBezel(hasSpacer, hasFallout);
                SoundDefOf.Click.PlayOneShotOnCamera();
                Event.current.Use();
            }

            _switchHotspotWasHovering = true;
        }
        else
        {
            _switchHotspotWasHovering = false;
        }
    }

    // Close button hotspot
    {
        Rect closeBtn = new Rect(
            windowRect.x + CloseHotspotWindow.x,
            windowRect.y + CloseHotspotWindow.y,
            CloseHotspotWindow.width,
            CloseHotspotWindow.height);

        bool hovering = Mouse.IsOver(closeBtn);

        if (hovering)
        {
            Color prev = GUI.color;
            GUI.color = new Color(0.8f, 0.3f, 0.3f, 0.2f);
            GUI.DrawTexture(closeBtn, BaseContent.WhiteTex);
            GUI.color = prev;

            if (!_closeHotspotWasHovering)
            {
                SoundDefOf.Mouseover_ButtonToggle.PlayOneShotOnCamera();
            }

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                Close();
                Event.current.Use();
            }

            _closeHotspotWasHovering = true;
        }
        else
        {
            _closeHotspotWasHovering = false;
        }
    }
}



internal static Texture2D GetActiveBezelTexture()
{
    switch (ActiveBezelIndex)
    {
        case BezelIndexSpacer:
            return TexCRTBezelSpacer ?? TexCRTBezel;
        case BezelIndexFallout:
            return TexCRTBezelFallout ?? TexCRTBezel;
        default:
            return TexCRTBezel;
    }
}



internal static void CycleToNextBezel(bool hasSpacer, bool hasFallout)
{
    if (ActiveBezelIndex == BezelIndexStandard)
    {
        ActiveBezelIndex = hasSpacer ? BezelIndexSpacer : BezelIndexFallout;
    }
    else if (ActiveBezelIndex == BezelIndexSpacer)
    {
        ActiveBezelIndex = hasFallout ? BezelIndexFallout : BezelIndexStandard;
    }
    else // Fallout
    {
        ActiveBezelIndex = BezelIndexStandard;
    }
}



internal static bool IsSpacerTechLevel()
{
    return DefDatabase<ResearchProjectDef>.AllDefsListForReading
        .Any(r => r.techLevel >= TechLevel.Spacer && r.IsFinished);
}



internal static float GetDesiredScale()
{
    var settings = LoadedModManager.GetMod<RelationsMod>()?.GetSettings<RelationsSettings>();
    switch (settings?.TerminalScale ?? TerminalScale.Auto)
    {
        case TerminalScale.S100: return 1.0f;
        case TerminalScale.S125: return 1.25f;
        case TerminalScale.S150: return 1.5f;
        case TerminalScale.S175: return 1.75f;
        case TerminalScale.S200: return 2.0f;
        case TerminalScale.S250: return 2.5f;
        default: return -1f; // Auto: don't override
    }
}



internal void ApplyTerminalScale()
{
    float desired = GetDesiredScale();
    if (desired < 0f) return; // Auto mode, no override
    _originalUIScale = Prefs.UIScale;
    _scaleOverridden = true;
    Prefs.UIScale = desired;
}



internal void RestoreTerminalScale()
{
    if (_scaleOverridden)
    {
        Prefs.UIScale = _originalUIScale;
        _scaleOverridden = false;
    }
}



/// <summary>
/// Shader-based CRT effect (full fidelity with barrel distortion).
/// </summary>
internal static void DrawCRTWithShader(Rect rect)
{
    MatCRT.SetFloat("_Distortion", 0.18f);
    MatCRT.SetFloat("_ScanlineIntensity", 0.10f);
    MatCRT.SetFloat("_ScanlineCount", 600f);
    MatCRT.SetFloat("_VignetteIntensity", 0.35f);
    MatCRT.SetFloat("_GreenTint", 0.65f);
    MatCRT.SetFloat("_ChromaticAberration", 1.5f);
    MatCRT.SetFloat("_NoiseIntensity", 0.05f);

    Graphics.DrawTexture(rect, BaseContent.WhiteTex, new Rect(0, 0, 1, 1), 0, 0, 0, 0, MatCRT);
}



/// <summary>
/// Procedural CRT effect (no shader, no barrel distortion).
/// Green tint + scanlines + vignette using draw calls.
/// </summary>
internal static void DrawCRTProcedural(Rect rect)
{
    Color prevColor = GUI.color;

    // Green phosphor tint overlay (subtle, preserves readability)
    GUI.color = new Color(0.05f, 0.2f, 0.08f, 0.12f);
    GUI.DrawTexture(rect, BaseContent.WhiteTex);

    // Scanline overlay (very subtle)
    GUI.color = new Color(0, 0, 0, 0.04f);
    float uScale = rect.width / 4f;
    float vScale = rect.height / 4f;
    GUI.DrawTextureWithTexCoords(rect, ScanlineOverlay, new Rect(0, 0, uScale, vScale));

    GUI.color = prevColor;
}



/// <summary>
/// Create a 4x4 scanline texture (2 transparent rows + 2 dark rows).
/// </summary>
internal static Texture2D CreateScanlineTexture()
{
    var tex = new Texture2D(4, 4, TextureFormat.ARGB32, false);
    tex.filterMode = FilterMode.Point;
    Color clear = new Color(0, 0, 0, 0);
    Color dark = new Color(0, 0, 0, 1f);

    for (int y = 0; y < 4; y++)
    {
        for (int x = 0; x < 4; x++)
        {
            tex.SetPixel(x, y, (y % 2 == 0) ? clear : dark);
        }
    }

    tex.Apply();
    return tex;
}
}
