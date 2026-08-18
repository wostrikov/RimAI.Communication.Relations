using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Communication.Relations.UI
{
    /// <summary>/// Dependencies: portrait layout helpers, Verse widgets, and localized feedback text producers.
 /// Responsibility: queue and render portrait-anchored RPG floating subtitles with gentle rise/fade motion.
 ///</summary>
        internal sealed class RPGPawnDialogueFeedbackOverlay : Dialog_RPGPawnDialogueCollaborator
    {
        internal RPGPawnDialogueFeedbackOverlay(Dialog_RPGPawnDialogue owner) : base(owner)
        {
        }


        internal struct ActionFeedbackEntry
        {
            public string Text;
            public string MoodColoredText;
            public Color Color;
            public Color MoodColor;
            public float CreatedAt;
            public float Duration;
        }

        internal static Texture2D subtitleCornerTexture;
        internal static Texture2D SubtitleCornerTexture => subtitleCornerTexture ?? (subtitleCornerTexture = Dialog_RPGPawnDialogue.CreateSubtitleCornerTexture());

        internal readonly List<ActionFeedbackEntry> actionFeedbackEntries = new List<ActionFeedbackEntry>();
        internal static readonly Color ActionSuccessColor = new Color(0.45f, 0.9f, 0.55f, 1f);
        internal static readonly Color ActionFailureColor = new Color(0.95f, 0.6f, 0.45f, 1f);
        internal static readonly Color ActionErrorColor = new Color(0.95f, 0.4f, 0.4f, 1f);
        internal static readonly Color ActionInfoColor = new Color(0.55f, 0.78f, 0.98f, 1f);
        internal static readonly Color MoodPositiveColor = new Color(0.35f, 0.85f, 0.4f, 1f);
        internal static readonly Color MoodNegativeColor = new Color(0.95f, 0.45f, 0.4f, 1f);
        internal static readonly Vector2 ActionFeedbackShadowOffset = new Vector2(1.5f, 2f);
        internal const float ActionFeedbackDefaultDuration = 10f;
        internal const float ActionFeedbackFadeOutDuration = 1f;
        internal const float ActionFeedbackHorizontalOffset = 20f;
        internal const float ActionFeedbackVerticalInset = 60f;
        internal const float ActionFeedbackWidth = 300f;
        internal const float ActionFeedbackSpacing = 8f;
        internal const float ActionFeedbackStackRunway = 26f;
        internal const float ActionFeedbackBaseRiseDistance = 14f;
        internal const float ActionFeedbackFadeRiseDistance = 12f;
        internal const float ActionFeedbackHorizontalPadding = 16f;
        internal const float ActionFeedbackVerticalPadding = 9f;
        internal const float ActionFeedbackAccentWidth = 3f;
        internal const float ActionFeedbackCornerRadius = 8f;
        internal const float ActionFeedbackMinHeight = 34f;
        internal const float ActionFeedbackShadowVerticalOffset = 3f;
        internal const float ActionFeedbackShadowHorizontalOffset = 1f;
        internal const int ActionFeedbackMaxCount = 8;

        internal void AddActionFeedback(string text, Color color, float duration = ActionFeedbackDefaultDuration)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            actionFeedbackEntries.Add(new ActionFeedbackEntry
            {
                Text = text,
                Color = color,
                Duration = ActionFeedbackDefaultDuration,
                CreatedAt = Time.realtimeSinceStartup
            });

            if (actionFeedbackEntries.Count > ActionFeedbackMaxCount)
            {
                actionFeedbackEntries.RemoveAt(0);
            }
        }

        internal void AddActionFeedback(string text, string moodColoredText, Color color, Color moodColor, float duration = ActionFeedbackDefaultDuration)
        {
            if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(moodColoredText))
            {
                return;
            }

            actionFeedbackEntries.Add(new ActionFeedbackEntry
            {
                Text = text,
                MoodColoredText = moodColoredText,
                Color = color,
                MoodColor = moodColor,
                Duration = ActionFeedbackDefaultDuration,
                CreatedAt = Time.realtimeSinceStartup
            });

            if (actionFeedbackEntries.Count > ActionFeedbackMaxCount)
            {
                actionFeedbackEntries.RemoveAt(0);
            }
        }

        internal void AddSystemFeedback(string text, float duration = ActionFeedbackDefaultDuration)
        {
            Owner.AddActionFeedback(text, ActionInfoColor, duration);
        }

        internal void DrawActionFeedback(Rect inRect)
        {
            Owner.RemoveExpiredActionFeedback();
            if (actionFeedbackEntries.Count == 0 || !Owner.TryGetActionFeedbackAnchorRect(inRect, out Rect anchorRect))
            {
                return;
            }

            TextAnchor oldAnchor = Text.Anchor;
            GameFont oldFont = Text.Font;
            Color oldColor = GUI.color;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            Owner.DrawActionFeedbackEntries(anchorRect);
            Text.Anchor = oldAnchor;
            Text.Font = oldFont;
            GUI.color = oldColor;
        }

        internal bool TryGetActionFeedbackAnchorRect(Rect inRect, out Rect anchorRect)
        {
            float visibility = Owner.GetActionFeedbackVisibility();
            if (visibility <= 0.01f)
            {
                anchorRect = Rect.zero;
                return false;
            }

            Rect targetPortraitRect = Owner.GetTargetPortraitRect(inRect);
            anchorRect = new Rect(
                targetPortraitRect.xMax + ActionFeedbackHorizontalOffset,
                targetPortraitRect.y + ActionFeedbackVerticalInset,
                ActionFeedbackWidth,
                TargetPortraitHeight - ActionFeedbackVerticalInset * 2f);
            return true;
        }

        internal void DrawActionFeedbackEntries(Rect anchorRect)
        {
            float currentY = anchorRect.yMin;
            for (int index = actionFeedbackEntries.Count - 1; index >= 0; index--)
            {
                ActionFeedbackEntry entry = actionFeedbackEntries[index];
                float subtitleHeight = Owner.CalculateActionFeedbackPanelHeight(entry.Text);
                Rect subtitleRect = Owner.BuildActionFeedbackRect(anchorRect, currentY, subtitleHeight, entry);
                if (subtitleRect.yMax > anchorRect.yMax)
                {
                    break;
                }

                Owner.DrawActionFeedbackEntry(entry, subtitleRect);
                currentY += subtitleHeight + ActionFeedbackSpacing + ActionFeedbackStackRunway;
            }
        }

        internal Rect BuildActionFeedbackRect(Rect anchorRect, float baseY, float height, ActionFeedbackEntry entry)
        {
            float riseOffset = Owner.GetActionFeedbackRiseOffset(entry);
            return new Rect(anchorRect.x, baseY - riseOffset, anchorRect.width, height);
        }

        internal float CalculateActionFeedbackPanelHeight(string text)
        {
            float textWidth = Owner.GetActionFeedbackTextWidth();
            float textHeight = Text.CalcHeight(text ?? string.Empty, textWidth);
            return Mathf.Max(ActionFeedbackMinHeight, textHeight + ActionFeedbackVerticalPadding * 2f);
        }

        internal float GetActionFeedbackTextWidth()
        {
            return ActionFeedbackWidth - ActionFeedbackHorizontalPadding * 2f - ActionFeedbackAccentWidth - 8f;
        }

        internal void DrawActionFeedbackEntry(ActionFeedbackEntry entry, Rect subtitleRect)
        {
            float alpha = Owner.GetActionFeedbackAlpha(entry) * Owner.GetActionFeedbackVisibility();
            if (alpha <= 0.01f)
            {
                return;
            }

            Owner.DrawActionFeedbackBackground(subtitleRect, alpha);
            Owner.DrawActionFeedbackAccent(entry, subtitleRect, alpha);
            Owner.DrawActionFeedbackText(entry, subtitleRect, alpha);
        }

        internal void DrawActionFeedbackBackground(Rect subtitleRect, float alpha)
        {
            Rect shadowRect = new Rect(
                subtitleRect.x + ActionFeedbackShadowHorizontalOffset,
                subtitleRect.y + ActionFeedbackShadowVerticalOffset,
                subtitleRect.width,
                subtitleRect.height);
            Owner.DrawRoundedRect(shadowRect, new Color(0f, 0f, 0f, 0.12f * alpha), ActionFeedbackCornerRadius);
            Owner.DrawRoundedRect(subtitleRect, new Color(0.04f, 0.05f, 0.07f, 0.22f * alpha), ActionFeedbackCornerRadius);
        }

        internal void DrawActionFeedbackAccent(ActionFeedbackEntry entry, Rect subtitleRect, float alpha)
        {
            float glowHeight = Mathf.Max(12f, subtitleRect.height - 12f);
            float glowY = subtitleRect.y + (subtitleRect.height - glowHeight) * 0.5f;
            Rect glowRect = new Rect(subtitleRect.x + 3f, glowY, ActionFeedbackAccentWidth + 6f, glowHeight);
            Color accentGlow = new Color(entry.Color.r, entry.Color.g, entry.Color.b, 0.14f * alpha);
            Owner.DrawRoundedRect(glowRect, accentGlow, ActionFeedbackCornerRadius);

            Rect accentRect = new Rect(subtitleRect.x + 8f, glowY + 2f, ActionFeedbackAccentWidth, glowHeight - 4f);
            GUI.color = new Color(entry.Color.r, entry.Color.g, entry.Color.b, 0.72f * alpha);
            GUI.DrawTexture(accentRect, BaseContent.WhiteTex);
            GUI.color = Color.white;
        }

        internal void DrawActionFeedbackText(ActionFeedbackEntry entry, Rect subtitleRect, float alpha)
        {
            Rect textRect = Owner.GetActionFeedbackTextRect(subtitleRect);
            if (!string.IsNullOrEmpty(entry.MoodColoredText))
            {
                Owner.DrawBicolorFeedbackText(entry, textRect, alpha);
                return;
            }

            Rect shadowRect = new Rect(
                textRect.x + ActionFeedbackShadowOffset.x,
                textRect.y + ActionFeedbackShadowOffset.y,
                textRect.width,
                textRect.height);
            GUI.color = new Color(0f, 0f, 0f, 0.5f * alpha);
            Widgets.Label(shadowRect, entry.Text);
            GUI.color = Owner.GetActionFeedbackTextColor(entry.Color, alpha);
            Widgets.Label(textRect, entry.Text);
        }

        internal void DrawBicolorFeedbackText(ActionFeedbackEntry entry, Rect textRect, float alpha)
        {
            string prefix = entry.Text;
            string moodText = entry.MoodColoredText;
            float prefixWidth = Text.CalcSize(prefix).x;
            float moodWidth = Text.CalcSize(moodText).x;
            float totalWidth = prefixWidth + moodWidth;
            float startX = textRect.x + (textRect.width - totalWidth) * 0.5f;
            if (startX < textRect.x)
            {
                startX = textRect.x;
            }

            Rect shadowRect = new Rect(
                textRect.x + ActionFeedbackShadowOffset.x,
                textRect.y + ActionFeedbackShadowOffset.y,
                textRect.width,
                textRect.height);
            GUI.color = new Color(0f, 0f, 0f, 0.5f * alpha);
            Widgets.Label(shadowRect, prefix);
            float moodShadowX = startX + prefixWidth;
            Rect moodShadowRect = new Rect(
                moodShadowX + ActionFeedbackShadowOffset.x,
                textRect.y + ActionFeedbackShadowOffset.y,
                moodWidth,
                textRect.height);
            Widgets.Label(moodShadowRect, moodText);

            GUI.color = Owner.GetActionFeedbackTextColor(entry.Color, alpha);
            Rect prefixRenderRect = new Rect(startX, textRect.y, prefixWidth, textRect.height);
            Widgets.Label(prefixRenderRect, prefix);

            Rect moodRenderRect = new Rect(moodShadowX, textRect.y, moodWidth, textRect.height);
            GUI.color = Owner.GetActionFeedbackTextColor(entry.MoodColor, alpha);
            Widgets.Label(moodRenderRect, moodText);
        }

        internal Rect GetActionFeedbackTextRect(Rect subtitleRect)
        {
            float x = subtitleRect.x + ActionFeedbackHorizontalPadding + ActionFeedbackAccentWidth + 6f;
            float width = subtitleRect.width - (x - subtitleRect.x) - ActionFeedbackHorizontalPadding;
            return new Rect(x, subtitleRect.y + ActionFeedbackVerticalPadding, width, subtitleRect.height - ActionFeedbackVerticalPadding * 2f);
        }

        internal Color GetActionFeedbackTextColor(Color sourceColor, float alpha)
        {
            Color blendedColor = Color.Lerp(sourceColor, Color.white, 0.42f);
            blendedColor.a = 0.96f * alpha;
            return blendedColor;
        }

        internal float GetActionFeedbackRiseOffset(ActionFeedbackEntry entry)
        {
            float age = Time.realtimeSinceStartup - entry.CreatedAt;
            float sustainDuration = Mathf.Max(0.01f, entry.Duration - ActionFeedbackFadeOutDuration);
            float sustainProgress = Mathf.Clamp01(age / sustainDuration);
            float baseRise = Mathf.SmoothStep(0f, ActionFeedbackBaseRiseDistance, sustainProgress);
            if (age <= sustainDuration)
            {
                return baseRise;
            }

            float fadeProgress = Mathf.Clamp01((age - sustainDuration) / ActionFeedbackFadeOutDuration);
            return baseRise + Mathf.SmoothStep(0f, ActionFeedbackFadeRiseDistance, fadeProgress);
        }

        internal float GetActionFeedbackAlpha(ActionFeedbackEntry entry)
        {
            float age = Time.realtimeSinceStartup - entry.CreatedAt;
            float fadeStart = entry.Duration - ActionFeedbackFadeOutDuration;
            if (age <= fadeStart)
            {
                return 1f;
            }

            return Mathf.Clamp01((entry.Duration - age) / ActionFeedbackFadeOutDuration);
        }

        internal float GetActionFeedbackVisibility()
        {
            return Mathf.Clamp01(globalFadeAlpha * targetFadeAlpha);
        }

        internal void RemoveExpiredActionFeedback()
        {
            float now = Time.realtimeSinceStartup;
            actionFeedbackEntries.RemoveAll(entry => now - entry.CreatedAt > entry.Duration);
        }

        internal void DrawRoundedRect(Rect rect, Color color, float radius)
        {
            GUI.color = color;
            float cornerRadius = Mathf.Min(radius, rect.width / 2f, rect.height / 2f);
            GUI.DrawTexture(new Rect(rect.x + cornerRadius, rect.y, rect.width - cornerRadius * 2f, rect.height), BaseContent.WhiteTex);
            GUI.DrawTexture(new Rect(rect.x, rect.y + cornerRadius, rect.width, rect.height - cornerRadius * 2f), BaseContent.WhiteTex);
            GUI.DrawTextureWithTexCoords(new Rect(rect.x, rect.y, cornerRadius, cornerRadius), SubtitleCornerTexture, new Rect(0f, 0.5f, 0.5f, 0.5f));
            GUI.DrawTextureWithTexCoords(new Rect(rect.xMax - cornerRadius, rect.y, cornerRadius, cornerRadius), SubtitleCornerTexture, new Rect(0.5f, 0.5f, 0.5f, 0.5f));
            GUI.DrawTextureWithTexCoords(new Rect(rect.x, rect.yMax - cornerRadius, cornerRadius, cornerRadius), SubtitleCornerTexture, new Rect(0f, 0f, 0.5f, 0.5f));
            GUI.DrawTextureWithTexCoords(new Rect(rect.xMax - cornerRadius, rect.yMax - cornerRadius, cornerRadius, cornerRadius), SubtitleCornerTexture, new Rect(0.5f, 0f, 0.5f, 0.5f));
            GUI.color = Color.white;
        }

        internal static Texture2D CreateSubtitleCornerTexture()
        {
            const int radius = 32;
            int size = radius * 2;
            Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2(radius, radius);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
        }

}
