using System.Linq;
using RimChat.Config;
using RimChat.Core;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimChat.UI
{
    public partial class Dialog_DiplomacyDialogue
    {
        // Bezel frame insets — asymmetric, matching the actual transparent region.
        // Original texture: 1402x1122, transparent X(232..1287) Y(167..994)
        // Scale factor: 960/1402 ≈ 0.685, 720/1122 ≈ 0.642
        private const float BezelInsetLeft = 159f;    // 232 * 0.685
        private const float BezelInsetRight = 79f;    // (1402-1287) * 0.685
        private const float BezelInsetTop = 107f;     // 167 * 0.642
        private const float BezelInsetBottom = 82f;   // (1122-994) * 0.642

        // Note: doWindowBackground = false is set in the constructor
        // to disable the default semi-transparent black background.
        // The bezel texture provides its own background.

        // CRT bezel textures (standard + spacer + fallout variants)
        private static Texture2D TexCRTBezel;
        private static Texture2D TexCRTBezelSpacer;
        private static Texture2D TexCRTBezelFallout;

        // Active bezel index: 0=Standard, 1=Spacer, 2=Fallout
        private static int ActiveBezelIndex
        {
            get => RimChatMod.Settings?.ActiveBezelIndex ?? 0;
            set
            {
                var settings = RimChatMod.Settings;
                if (settings != null) settings.ActiveBezelIndex = value;
            }
        }
        private const int BezelIndexStandard = 0;
        private const int BezelIndexSpacer = 1;
        private const int BezelIndexFallout = 2;

        // Texture switch hotspot — rect in original texture coords (1402x1122): (40,665)→(148,783)
        // Scaled to window 960x720: (27,404)→(101,480)
        private static readonly Rect SwitchHotspotWindow = new Rect(27f, 404f, 74f, 76f);

        // Close button hotspot — rect in original texture coords (1402x1122): (1099,1036)→(1285,1110)
        // Scaled to window 960x720: (753,665)→(880,712)
        private static readonly Rect CloseHotspotWindow = new Rect(724f, 632f, 127f, 47f);
        private static bool _closeHotspotWasHovering;

        // Terminal UI scale override
        private float _originalUIScale;
        private bool _scaleOverridden;
        private static bool _switchHotspotWasHovering;

        // CRT overlay material (barrel distortion + scanlines + green tint + vignette)
        private static Material MatCRT;

        // Procedural scanline texture fallback (no shader needed)
        private static Texture2D _scanlineOverlay;
        private static Texture2D ScanlineOverlay
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
        private static void InitTerminalTheme()
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
        private static Rect ShrinkForBezel(Rect rect)
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
        private static void DrawCRTOverlay(Rect contentRect)
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
        private void DrawCRTBezelBackground(Rect windowRect)
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

        private static Texture2D GetActiveBezelTexture()
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

        private static void CycleToNextBezel(bool hasSpacer, bool hasFallout)
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

        private static bool IsSpacerTechLevel()
        {
            return DefDatabase<ResearchProjectDef>.AllDefsListForReading
                .Any(r => r.techLevel >= TechLevel.Spacer && r.IsFinished);
        }

        private static float GetDesiredScale()
        {
            var settings = LoadedModManager.GetMod<RimChatMod>()?.GetSettings<RimChatSettings>();
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

        private void ApplyTerminalScale()
        {
            float desired = GetDesiredScale();
            if (desired < 0f) return; // Auto mode, no override
            _originalUIScale = Prefs.UIScale;
            _scaleOverridden = true;
            Prefs.UIScale = desired;
        }

        private void RestoreTerminalScale()
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
        private static void DrawCRTWithShader(Rect rect)
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
        private static void DrawCRTProcedural(Rect rect)
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
        private static Texture2D CreateScanlineTexture()
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
}
