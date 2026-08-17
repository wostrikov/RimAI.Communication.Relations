using System;
using System.Collections.Generic;
using Ustas.RimAI.Core.UI;

namespace Ustas.RimAI.Communication.Relations.Settings;

/// <summary>Stable Relations settings page and field identities for the shared UI shell.</summary>
public static class RelationsSettingsCatalog
{
    public static readonly SettingsPageId ProviderPage = SettingsPageId.From("provider");
    public static readonly SettingsPageId GameplayPage = SettingsPageId.From("gameplay");
    public static readonly SettingsPageId PromptPage = SettingsPageId.From("prompt");
    public static readonly SettingsPageId ImagePage = SettingsPageId.From("image");

    public const string FieldProvider = "provider";
    public const string FieldLanguage = "prompt-language";
    public const string FieldDebug = "debug";
    public const string FieldActions = "actions";
    public const string FieldAirdropBudget = "airdrop-budget";
    public const string FieldQuestCooldown = "quest-cooldown";
    public const string FieldSocial = "social";
    public const string FieldMemory = "memory-integration";
    public const string FieldPromptWorkbench = "prompt-workbench";
    public const string FieldImageApi = "image-api";

    public const string AirdropBudgetError = "Ustas.RimAI.Relations.Settings.Validation.AirdropBudget";
    public const string QuestCooldownError = "Ustas.RimAI.Relations.Settings.Validation.QuestCooldown";
    public const string PromptLanguageWarning = "Ustas.RimAI.Relations.Settings.Validation.PromptLanguage";

    public static IReadOnlyList<SettingsPageDescriptor> CreatePages(
        string providerTitle,
        string gameplayTitle,
        string promptTitle,
        string imageTitle)
    {
        return new[]
        {
            new SettingsPageDescriptor(ProviderPage, providerTitle, ProviderKeywords),
            new SettingsPageDescriptor(GameplayPage, gameplayTitle, GameplayKeywords),
            new SettingsPageDescriptor(PromptPage, promptTitle, PromptKeywords),
            new SettingsPageDescriptor(ImagePage, imageTitle, ImageKeywords)
        };
    }

    public static IReadOnlyList<string> ProviderKeywords { get; } =
        new[] { "provider", "api", "cloud", "local", "model", "openai", "debug", "language" };

    public static IReadOnlyList<string> GameplayKeywords { get; } =
        new[] { "gameplay", "actions", "raid", "gift", "aid", "airdrop", "ransom", "social", "npc", "rpg", "quest" };

    public static IReadOnlyList<string> PromptKeywords { get; } =
        new[] { "prompt", "workbench", "template", "persona", "memory", "variable", "rimtalk" };

    public static IReadOnlyList<string> ImageKeywords { get; } =
        new[] { "image", "selfie", "caption", "diplomacy", "picture" };

    public static IReadOnlyList<string> KeywordsFor(string fieldId)
    {
        switch (fieldId)
        {
            case FieldProvider:
                return new[] { "provider", "api", "cloud", "local", "model" };
            case FieldLanguage:
                return new[] { "language", "prompt", "locale" };
            case FieldDebug:
                return new[] { "debug", "log", "logging" };
            case FieldActions:
                return new[] { "actions", "gift", "war", "peace", "raid", "caravan" };
            case FieldAirdropBudget:
                return new[] { "airdrop", "budget", "trade" };
            case FieldQuestCooldown:
                return new[] { "quest", "cooldown" };
            case FieldSocial:
                return new[] { "social", "news", "circle" };
            case FieldMemory:
                return new[] { "memory", "expandmemory", "compat" };
            case FieldPromptWorkbench:
                return new[] { "prompt", "workbench", "template", "persona" };
            case FieldImageApi:
                return new[] { "image", "selfie", "caption" };
            default:
                return Array.Empty<string>();
        }
    }

    public static bool IsFieldVisible(string fieldId, SettingsSearchState search)
    {
        if (search == null || search.IsEmpty)
            return true;
        return search.MatchesAny(KeywordsFor(fieldId)) || search.Matches(fieldId);
    }

    public static bool IsPage(SettingsResetRequest request, SettingsPageId pageId) =>
        request != null &&
        request.Scope == SettingsResetScope.Page &&
        request.PageId.HasValue &&
        request.PageId.Value.Equals(pageId);

    public static bool IsAll(SettingsResetRequest request) =>
        request != null && request.Scope == SettingsResetScope.All;
}
