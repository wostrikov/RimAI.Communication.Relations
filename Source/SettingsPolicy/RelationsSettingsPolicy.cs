using System;
using System.Collections.Generic;
using Ustas.RimAI.Core.UI;

namespace Ustas.RimAI.Communication.Relations.Settings;

/// <summary>Defaults, reset, and validation for Relations settings. No Verse types.</summary>
public static class RelationsSettingsPolicy
{
    public static SettingsValidationResult Validate(RelationsSettingsModel model)
    {
        if (model == null)
            return SettingsValidationResult.Valid;

        var issues = new List<SettingsValidationIssue>();
        if (model.ItemAirdropMinBudgetSilver > model.ItemAirdropMaxBudgetSilver)
        {
            issues.Add(new SettingsValidationIssue(
                SettingsValidationSeverity.Error,
                RelationsSettingsCatalog.AirdropBudgetError,
                RelationsSettingsCatalog.FieldAirdropBudget,
                RelationsSettingsCatalog.GameplayPage));
        }

        if (model.MinQuestCooldownDays > model.MaxQuestCooldownDays)
        {
            issues.Add(new SettingsValidationIssue(
                SettingsValidationSeverity.Error,
                RelationsSettingsCatalog.QuestCooldownError,
                RelationsSettingsCatalog.FieldQuestCooldown,
                RelationsSettingsCatalog.GameplayPage));
        }

        if (!model.PromptLanguageFollowSystem && string.IsNullOrWhiteSpace(model.PromptLanguageOverride))
        {
            issues.Add(new SettingsValidationIssue(
                SettingsValidationSeverity.Warning,
                RelationsSettingsCatalog.PromptLanguageWarning,
                RelationsSettingsCatalog.FieldLanguage,
                RelationsSettingsCatalog.ProviderPage));
        }

        return SettingsValidationResult.FromIssues(issues);
    }

    public static RelationsSettingsModel ApplyReset(RelationsSettingsModel current, SettingsResetRequest request)
    {
        var next = current?.Clone() ?? RelationsSettingsModel.Default();
        if (request == null)
            return next;

        var defaults = RelationsSettingsModel.Default();
        var resetProvider = RelationsSettingsCatalog.IsAll(request) ||
                            RelationsSettingsCatalog.IsPage(request, RelationsSettingsCatalog.ProviderPage);
        var resetGameplay = RelationsSettingsCatalog.IsAll(request) ||
                            RelationsSettingsCatalog.IsPage(request, RelationsSettingsCatalog.GameplayPage);
        var resetPrompt = RelationsSettingsCatalog.IsAll(request) ||
                          RelationsSettingsCatalog.IsPage(request, RelationsSettingsCatalog.PromptPage);
        var resetImage = RelationsSettingsCatalog.IsAll(request) ||
                         RelationsSettingsCatalog.IsPage(request, RelationsSettingsCatalog.ImagePage);

        if (resetProvider)
        {
            next.UseCloudProviders = defaults.UseCloudProviders;
            next.PromptLanguageFollowSystem = defaults.PromptLanguageFollowSystem;
            next.PromptLanguageOverride = defaults.PromptLanguageOverride;
        }

        if (resetGameplay)
            CopyGameplay(defaults, next);

        if (resetPrompt)
        {
            next.EnableRPGDialogue = defaults.EnableRPGDialogue;
            next.EnableRPGAPI = defaults.EnableRPGAPI;
            next.EnableRPGNonVerbalPawnSpeech = defaults.EnableRPGNonVerbalPawnSpeech;
        }

        if (resetImage)
        {
            next.DiplomacyImageProvider = defaults.DiplomacyImageProvider;
            next.DiplomacyImageConfigured = defaults.DiplomacyImageConfigured;
        }

        return next;
    }

    public static string NormalizeCompatMode(string mode)
    {
        if (string.Equals(mode, "on", StringComparison.OrdinalIgnoreCase))
            return "on";
        if (string.Equals(mode, "off", StringComparison.OrdinalIgnoreCase))
            return "off";
        return "auto";
    }

    public static bool MatchesSearch(SettingsSearchState search, string fieldId, params string[] extraKeywords)
    {
        if (search == null || search.IsEmpty)
            return true;
        if (RelationsSettingsCatalog.IsFieldVisible(fieldId, search))
            return true;
        return extraKeywords != null && extraKeywords.Length > 0 && search.MatchesAny(extraKeywords);
    }

    static void CopyGameplay(RelationsSettingsModel from, RelationsSettingsModel to)
    {
        to.EnableAIGoodwillAdjustment = from.EnableAIGoodwillAdjustment;
        to.EnableAIGiftSending = from.EnableAIGiftSending;
        to.EnableAIWarDeclaration = from.EnableAIWarDeclaration;
        to.EnableAIPeaceMaking = from.EnableAIPeaceMaking;
        to.EnableAITradeCaravan = from.EnableAITradeCaravan;
        to.EnableAIAidRequest = from.EnableAIAidRequest;
        to.EnableAIRaidRequest = from.EnableAIRaidRequest;
        to.EnableAIItemAirdrop = from.EnableAIItemAirdrop;
        to.EnablePrisonerRansom = from.EnablePrisonerRansom;
        to.MaxGoodwillAdjustmentPerCall = from.MaxGoodwillAdjustmentPerCall;
        to.MaxDailyGoodwillAdjustment = from.MaxDailyGoodwillAdjustment;
        to.GoodwillCooldownTicks = from.GoodwillCooldownTicks;
        to.DialogueActionGoodwillCostMultiplier = from.DialogueActionGoodwillCostMultiplier;
        to.MaxGiftSilverAmount = from.MaxGiftSilverAmount;
        to.MaxGiftGoodwillGain = from.MaxGiftGoodwillGain;
        to.GiftCooldownTicks = from.GiftCooldownTicks;
        to.MinGoodwillForAid = from.MinGoodwillForAid;
        to.AidCooldownTicks = from.AidCooldownTicks;
        to.AidDelayBaseTicks = from.AidDelayBaseTicks;
        to.ItemAirdropMinBudgetSilver = from.ItemAirdropMinBudgetSilver;
        to.ItemAirdropMaxBudgetSilver = from.ItemAirdropMaxBudgetSilver;
        to.ItemAirdropDefaultAIBudgetSilver = from.ItemAirdropDefaultAIBudgetSilver;
        to.ItemAirdropCooldownTicks = from.ItemAirdropCooldownTicks;
        to.MinQuestCooldownDays = from.MinQuestCooldownDays;
        to.MaxQuestCooldownDays = from.MaxQuestCooldownDays;
        to.EnableSocialCircle = from.EnableSocialCircle;
        to.EnablePlayerInfluenceNews = from.EnablePlayerInfluenceNews;
        to.EnableAISimulationNews = from.EnableAISimulationNews;
        to.EnableSocialCircleAutoActions = from.EnableSocialCircleAutoActions;
        to.ExpandMemoryCompatMode = from.ExpandMemoryCompatMode;
        to.ExpandMemoryInjectPawnMemory = from.ExpandMemoryInjectPawnMemory;
        to.ExpandMemoryPawnMemoryMaxChars = from.ExpandMemoryPawnMemoryMaxChars;
        to.ExpandMemoryPawnMemoryMaxEntries = from.ExpandMemoryPawnMemoryMaxEntries;
        to.FactionExclusionDefNamesCsv = from.FactionExclusionDefNamesCsv;
    }
}
