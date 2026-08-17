using System;
using Ustas.RimAI.Communication.Relations.Settings;
using Ustas.RimAI.Core.UI;

internal static class RelationsSettingsPolicyTests
{
    public static void Run(Action<bool, string> check)
    {
        Defaults(check);
        Validation(check);
        Reset(check);
        Search(check);
        Catalog(check);
        CompatMode(check);
    }

    static void Defaults(Action<bool, string> check)
    {
        var model = RelationsSettingsModel.Default();
        check(model.UseCloudProviders, "default uses cloud providers");
        check(model.EnableAIGiftSending, "default gift sending enabled");
        check(model.ItemAirdropMinBudgetSilver == 200, "default airdrop min budget");
        check(model.ItemAirdropMaxBudgetSilver == 50000, "default airdrop max budget");
        check(model.MinQuestCooldownDays == 7 && model.MaxQuestCooldownDays == 12, "default quest cooldown");
        check(model.EnableSocialCircle, "default social circle enabled");
        check(model.ExpandMemoryCompatMode == "auto", "default expand-memory compat");
        check(model.PromptLanguageFollowSystem, "default prompt language follows system");
        var clone = model.Clone();
        clone.EnableAIGiftSending = false;
        check(model.EnableAIGiftSending, "clone does not alias gameplay flags");
    }

    static void Validation(Action<bool, string> check)
    {
        var ok = RelationsSettingsPolicy.Validate(RelationsSettingsModel.Default());
        check(ok.IsValid && !ok.HasWarnings, "defaults are valid without warnings");

        var budget = RelationsSettingsModel.Default();
        budget.ItemAirdropMinBudgetSilver = 900;
        budget.ItemAirdropMaxBudgetSilver = 100;
        var budgetResult = RelationsSettingsPolicy.Validate(budget);
        check(!budgetResult.IsValid, "inverted airdrop budget is an error");
        check(budgetResult.Issues.Count == 1 && budgetResult.Issues[0].Severity == SettingsValidationSeverity.Error, "airdrop budget error only");

        var language = RelationsSettingsModel.Default();
        language.PromptLanguageFollowSystem = false;
        language.PromptLanguageOverride = " ";
        var languageResult = RelationsSettingsPolicy.Validate(language);
        check(languageResult.IsValid && languageResult.HasWarnings, "empty custom language is a warning");

        var quest = RelationsSettingsModel.Default();
        quest.MinQuestCooldownDays = 20;
        quest.MaxQuestCooldownDays = 4;
        var questResult = RelationsSettingsPolicy.Validate(quest);
        check(!questResult.IsValid, "inverted quest cooldown is an error");
    }

    static void Reset(Action<bool, string> check)
    {
        var current = RelationsSettingsModel.Default();
        current.EnableAIGiftSending = false;
        current.PromptLanguageFollowSystem = false;
        current.PromptLanguageOverride = "uk";
        current.DiplomacyImageConfigured = true;

        var gameplay = RelationsSettingsPolicy.ApplyReset(
            current,
            SettingsResetRequest.Page(RelationsSettingsCatalog.GameplayPage));
        check(gameplay.EnableAIGiftSending, "gameplay page reset restores gift toggle");
        check(!gameplay.PromptLanguageFollowSystem && gameplay.PromptLanguageOverride == "uk", "gameplay reset leaves provider language");
        check(gameplay.DiplomacyImageConfigured, "gameplay reset leaves image flag");

        var all = RelationsSettingsPolicy.ApplyReset(current, SettingsResetRequest.All());
        check(all.EnableAIGiftSending && all.PromptLanguageFollowSystem, "all reset restores provider and gameplay");
        check(!all.DiplomacyImageConfigured, "all reset restores image flag");
    }

    static void Search(Action<bool, string> check)
    {
        var empty = SettingsSearchState.Empty;
        check(RelationsSettingsCatalog.IsFieldVisible(RelationsSettingsCatalog.FieldActions, empty), "empty search shows actions");
        var raid = SettingsSearchState.FromQuery("raid");
        check(RelationsSettingsCatalog.IsFieldVisible(RelationsSettingsCatalog.FieldActions, raid), "raid matches actions keywords");
        check(!RelationsSettingsCatalog.IsFieldVisible(RelationsSettingsCatalog.FieldImageApi, raid), "raid does not match image");
        check(RelationsSettingsPolicy.MatchesSearch(raid, RelationsSettingsCatalog.FieldActions), "policy search helper matches actions");
        var pages = RelationsSettingsCatalog.CreatePages("API", "Gameplay", "Prompt", "Image");
        var visible = raid.FilterPages(pages);
        check(visible.Count == 1 && visible[0].Id.Equals(RelationsSettingsCatalog.GameplayPage), "raid search keeps gameplay page");
    }

    static void Catalog(Action<bool, string> check)
    {
        var pages = RelationsSettingsCatalog.CreatePages("API", "Gameplay", "Prompt", "Image");
        check(pages.Count == 4, "catalog has four real pages");
        check(pages[0].Id.Equals(RelationsSettingsCatalog.ProviderPage), "first page is provider");
        check(pages[2].Id.Equals(RelationsSettingsCatalog.PromptPage), "third page is prompt workbench");
    }

    static void CompatMode(Action<bool, string> check)
    {
        check(RelationsSettingsPolicy.NormalizeCompatMode("ON") == "on", "compat mode on");
        check(RelationsSettingsPolicy.NormalizeCompatMode("Off") == "off", "compat mode off");
        check(RelationsSettingsPolicy.NormalizeCompatMode("nope") == "auto", "compat mode fallback auto");
    }
}
