using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.UI;
using Ustas.RimAI.Core.UI;

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class RelationsRpgPromptFieldEditors
{
    readonly RelationsSettingsPages Pages;

    internal RelationsRpgPromptFieldEditors(RelationsSettingsPages pages)
    {
        Pages = pages;
    }

    RelationsSettings Settings => Pages.Settings;

        internal Vector2 _rpgFallbackEditorScroll = Vector2.zero;
        internal Vector2 _rpgApiPromptEditorScroll = Vector2.zero;

        internal void DrawRpgFallbackTemplateEditor(Rect rect)
        {
            float viewWidth = rect.width - 16f;
            float contentHeight = 430f;
            Rect viewRect = new Rect(0f, 0f, viewWidth, contentHeight);
            _rpgFallbackEditorScroll = GUI.BeginScrollView(rect, _rpgFallbackEditorScroll, viewRect);

            float y = 0f;
            bool changed = false;
            changed |= DrawRpgTextField(viewRect.width, ref y, "RimChat_RPGRoleFallbackTemplateLabel", ref Settings.RPGRoleSettingFallbackTemplate, 54f);
            changed |= DrawRpgTextField(viewRect.width, ref y, "RimChat_RPGFormatConstraintHeaderLabel", ref Settings.RPGFormatConstraintHeader, 38f);
            changed |= DrawRpgTextField(viewRect.width, ref y, "RimChat_RPGCompactFormatFallbackLabel", ref Settings.RPGCompactFormatFallback, 54f);
            changed |= DrawRpgTextField(viewRect.width, ref y, "RimChat_RPGActionReliabilityFallbackLabel", ref Settings.RPGActionReliabilityFallback, 54f);
            changed |= DrawRpgTextField(viewRect.width, ref y, "RimChat_RPGActionReliabilityMarkerLabel", ref Settings.RPGActionReliabilityMarker, 38f);

            if (changed)
            {
                Pages.RpgEditors._rpgPreviewUpdateCooldown = 0;
            }

            GUI.EndScrollView();
        }

        internal void DrawRpgApiPromptTemplateEditor(Rect rect)
        {
            EnsureRpgApiPromptConfig();
            RpgApiActionPromptConfig config = Settings.RPGApiActionPromptConfig;

            float viewWidth = rect.width - 16f;
            float contentHeight = 1280f;
            Rect viewRect = new Rect(0f, 0f, viewWidth, contentHeight);
            _rpgApiPromptEditorScroll = GUI.BeginScrollView(rect, _rpgApiPromptEditorScroll, viewRect);

            float y = 0f;
            bool changed = false;
            changed |= DrawRpgTextField(viewRect.width, ref y, "RimChat_RPGApiFullHeaderLabel", ref config.FullHeader, 38f);
            changed |= DrawRpgTextField(viewRect.width, ref y, "RimChat_RPGApiFullIntroLabel", ref config.FullIntro, 54f);
            changed |= DrawRpgTextField(viewRect.width, ref y, "RimChat_RPGApiFullActionObjectHintLabel", ref config.FullActionObjectHint, 54f);
            changed |= DrawRpgTextField(viewRect.width, ref y, "RimChat_RPGApiFullActionReliabilityGuidanceLabel", ref config.FullActionReliabilityGuidance, 54f);
            changed |= DrawRpgTextField(viewRect.width, ref y, "RimChat_RPGApiFullClosureReliabilityGuidanceLabel", ref config.FullClosureReliabilityGuidance, 54f);
            changed |= DrawRpgTextField(viewRect.width, ref y, "RimChat_RPGApiFullTryGainMemoryLineTemplateLabel", ref config.FullTryGainMemoryLineTemplate, 70f);

            string sharedLines = string.Join("\n", config.SharedActionLines ?? new List<string>());
            bool sharedChanged = DrawRpgTextField(viewRect.width, ref y, "RimChat_RPGApiSharedActionLinesLabel", ref sharedLines, 180f);
            if (sharedChanged)
            {
                config.SharedActionLines = ParseLineList(sharedLines);
                changed = true;
            }

            changed |= DrawRpgTextField(viewRect.width, ref y, "RimChat_RPGApiCompactHeaderLabel", ref config.CompactHeader, 38f);
            changed |= DrawRpgTextField(viewRect.width, ref y, "RimChat_RPGApiCompactIntroLabel", ref config.CompactIntro, 54f);
            changed |= DrawRpgTextField(viewRect.width, ref y, "RimChat_RPGApiCompactAllowedActionsTemplateLabel", ref config.CompactAllowedActionsTemplate, 54f);
            changed |= DrawRpgTextField(viewRect.width, ref y, "RimChat_RPGApiCompactTryGainMemoryTemplateLabel", ref config.CompactTryGainMemoryTemplate, 54f);
            changed |= DrawRpgTextField(viewRect.width, ref y, "RimChat_RPGApiCompactActionFieldsHintLabel", ref config.CompactActionFieldsHint, 54f);
            changed |= DrawRpgTextField(viewRect.width, ref y, "RimChat_RPGApiCompactClosureGuidanceLabel", ref config.CompactClosureGuidance, 54f);

            string compactNames = string.Join("\n", config.CompactActionNames ?? new List<string>());
            bool compactNamesChanged = DrawRpgTextField(viewRect.width, ref y, "RimChat_RPGApiCompactActionNamesLabel", ref compactNames, 140f);
            if (compactNamesChanged)
            {
                config.CompactActionNames = ParseLineList(compactNames);
                changed = true;
            }

            if (changed)
            {
                Pages.RpgEditors._rpgPreviewUpdateCooldown = 0;
            }

            GUI.EndScrollView();
        }

        internal static bool DrawRpgTextField(float width, ref float y, string labelKey, ref string value, float height)
        {
            Widgets.Label(new Rect(0f, y, width, 20f), labelKey.Translate());
            y += 22f;
            string current = value ?? string.Empty;
            string edited = Widgets.TextArea(new Rect(0f, y, width, height), current);
            y += height + 8f;

            if (string.Equals(edited, current, StringComparison.Ordinal))
            {
                return false;
            }

            value = edited;
            return true;
        }

        internal void EnsureRpgApiPromptConfig()
        {
            Settings.RPGApiActionPromptConfig ??= RpgApiActionPromptConfig.CreateFallback();
            Settings.RPGApiActionPromptConfig.SharedActionLines ??= new List<string>();
            Settings.RPGApiActionPromptConfig.CompactActionNames ??= new List<string>();
        }

        internal static List<string> ParseLineList(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new List<string>();
            }

            return text
                .Replace("\r", string.Empty)
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();
        }
    
}
