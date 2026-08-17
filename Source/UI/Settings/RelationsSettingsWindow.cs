using UnityEngine;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Settings;
using Ustas.RimAI.Core.UI;
using Ustas.RimAI.RimWorld.UI;
using Verse;

namespace Ustas.RimAI.Communication.Relations.UI;

/// <summary>Relations settings renderer. Persistent data stays on RelationsSettings.</summary>
public static class RelationsSettingsWindow
{
    static SettingsUiState _state;
    static Vector2 _scrollPosition;
    static RelationsSettingsPages _pages;

    public static bool IsPromptWorkbenchPage =>
        _state != null && _state.Navigation.CurrentPage.Equals(RelationsSettingsCatalog.PromptPage);

    public static void Draw(Rect inRect, RelationsSettings settings)
    {
        if (settings == null)
            return;

        _pages = RelationsSettingsPages.For(settings);
        EnsureState(settings);
        var content = SettingsShellRenderer.DrawChrome(
            inRect,
            _state,
            SettingsShellLabels.FromKeyedTranslations(),
            out _state,
            out var resetRequest);
        if (resetRequest != null)
        {
            ApplyReset(settings, resetRequest);
            _state = _state.WithValidation(RelationsSettingsPolicy.Validate(RelationsSettingsMapping.ToModel(settings)));
        }

        _state = _state.WithValidation(RelationsSettingsPolicy.Validate(RelationsSettingsMapping.ToModel(settings)));
        var page = _state.Navigation.CurrentPage;
        if (page.Equals(RelationsSettingsCatalog.PromptPage))
        {
            _pages.Prompt.Draw(content, _state.Search);
            return;
        }

        if (page.Equals(RelationsSettingsCatalog.GameplayPage))
        {
            _pages.Gameplay.Draw(content, _state.Search);
            return;
        }

        if (page.Equals(RelationsSettingsCatalog.ImagePage))
        {
            _pages.Image.Draw(content, _state.Search);
            return;
        }

        SettingsShellRenderer.BeginScroll(content, ref _scrollPosition, EstimateHeight(page, content.width));
        var listing = new Listing_Standard();
        listing.Begin(new Rect(0f, 0f, content.width - 16f, EstimateHeight(page, content.width)));
        _pages.Provider.Draw(listing, _state.Search);
        listing.End();
        SettingsShellRenderer.EndScroll();
    }

    public static void DrawPromptWorkbench(Rect inRect, RelationsSettings settings)
    {
        if (settings == null)
            return;
        _pages = RelationsSettingsPages.For(settings);
        _pages.Prompt.Draw(inRect);
    }

    public static void SelectProviderPage()
    {
        if (_state == null)
            return;
        _state = _state.SelectPage(RelationsSettingsCatalog.ProviderPage);
    }

    static void EnsureState(RelationsSettings settings)
    {
        var pages = RelationsSettingsCatalog.CreatePages(
            "RimChat_Tab_API".Translate(),
            "RimChat_Tab_ModOptions".Translate(),
            "RimChat_Tab_PromptWorkbench".Translate(),
            "RimChat_Tab_ImageApi".Translate());
        if (_state == null)
        {
            _state = new SettingsUiState(
                SettingsNavigationState.Create(pages),
                SettingsSearchState.Empty,
                RelationsSettingsPolicy.Validate(RelationsSettingsMapping.ToModel(settings)));
            return;
        }

        _state = _state.WithNavigation(SettingsNavigationState.Create(pages, _state.Navigation.CurrentPage));
    }

    static void ApplyReset(RelationsSettings settings, SettingsResetRequest request)
    {
        var next = RelationsSettingsPolicy.ApplyReset(RelationsSettingsMapping.ToModel(settings), request);
        RelationsSettingsMapping.CopyFrom(settings, next);
        if (RelationsSettingsCatalog.IsAll(request) ||
            RelationsSettingsCatalog.IsPage(request, RelationsSettingsCatalog.GameplayPage))
            _pages.Gameplay.ResetAllSections();
        if (RelationsSettingsCatalog.IsAll(request) ||
            RelationsSettingsCatalog.IsPage(request, RelationsSettingsCatalog.ImagePage))
            _pages.Image.ResetToDefaults();
        if (RelationsSettingsCatalog.IsAll(request) ||
            RelationsSettingsCatalog.IsPage(request, RelationsSettingsCatalog.PromptPage))
            _pages.Prompt.ResetToDefaults();
    }

    static float EstimateHeight(SettingsPageId page, float width)
    {
        if (page.Equals(RelationsSettingsCatalog.GameplayPage))
            return 2200f;
        if (page.Equals(RelationsSettingsCatalog.ImagePage))
            return 1400f;
        return Mathf.Max(720f, width);
    }
}
