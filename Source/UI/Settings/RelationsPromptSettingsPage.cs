using System;
using UnityEngine;
using Ustas.RimAI.Core.UI;
using Verse;

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class RelationsPromptSettingsPage
{
    readonly RelationsSettingsPages _pages;

    internal RelationsPromptSettingsPage(RelationsSettingsPages pages)
    {
        _pages = pages;
    }

    internal void Draw(Rect rect) => Draw(rect, SettingsSearchState.Empty);

    internal void Draw(Rect rect, SettingsSearchState search)
    {
        // BOUNDARY_REQUIRED: Verse settings render callback. Escaping would destabilize Dialog_ModSettings.
        try
        {
            _pages.PromptWorkspace.DrawPromptSectionWorkspace(rect);
        }
        catch (Exception ex)
        {
            Log.Error($"[RimAI.Relations] Prompt settings page render failed: {ex}");
            Widgets.Label(rect, "RimChat_PromptRenderFailed".Translate());
        }
    }

    internal void ResetToDefaults()
    {
        _pages.Settings.PromptLanguageFollowSystem = true;
        _pages.Settings.PromptLanguageOverride = "";
    }
}
