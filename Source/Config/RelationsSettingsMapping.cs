using Ustas.RimAI.Communication.Relations.Settings;

namespace Ustas.RimAI.Communication.Relations.Config;

/// <summary>Maps persisted RelationsSettings fields to the renderer-neutral policy model.</summary>
public static class RelationsSettingsMapping
{
    public static RelationsSettingsModel ToModel(RelationsSettings settings)
    {
        if (settings == null)
            return RelationsSettingsModel.Default();
        return new RelationsSettingsModel
        {
            UseCloudProviders = settings.UseCloudProviders,
            CloudConfigCount = settings.CloudConfigs?.Count ?? 0,
            PromptLanguageFollowSystem = settings.PromptLanguageFollowSystem,
            PromptLanguageOverride = settings.PromptLanguageOverride ?? "",
            EnableAIGoodwillAdjustment = settings.EnableAIGoodwillAdjustment,
            EnableAIGiftSending = settings.EnableAIGiftSending,
            EnableAIWarDeclaration = settings.EnableAIWarDeclaration,
            EnableAIPeaceMaking = settings.EnableAIPeaceMaking,
            EnableAITradeCaravan = settings.EnableAITradeCaravan,
            EnableAIAidRequest = settings.EnableAIAidRequest,
            EnableAIRaidRequest = settings.EnableAIRaidRequest,
            EnableAIItemAirdrop = settings.EnableAIItemAirdrop,
            EnablePrisonerRansom = settings.EnablePrisonerRansom,
            MaxGoodwillAdjustmentPerCall = settings.MaxGoodwillAdjustmentPerCall,
            MaxDailyGoodwillAdjustment = settings.MaxDailyGoodwillAdjustment,
            GoodwillCooldownTicks = settings.GoodwillCooldownTicks,
            DialogueActionGoodwillCostMultiplier = settings.DialogueActionGoodwillCostMultiplier,
            MaxGiftSilverAmount = settings.MaxGiftSilverAmount,
            MaxGiftGoodwillGain = settings.MaxGiftGoodwillGain,
            GiftCooldownTicks = settings.GiftCooldownTicks,
            MinGoodwillForAid = settings.MinGoodwillForAid,
            AidCooldownTicks = settings.AidCooldownTicks,
            AidDelayBaseTicks = settings.AidDelayBaseTicks,
            ItemAirdropMinBudgetSilver = settings.ItemAirdropMinBudgetSilver,
            ItemAirdropMaxBudgetSilver = settings.ItemAirdropMaxBudgetSilver,
            ItemAirdropDefaultAIBudgetSilver = settings.ItemAirdropDefaultAIBudgetSilver,
            ItemAirdropCooldownTicks = settings.ItemAirdropCooldownTicks,
            MinQuestCooldownDays = settings.MinQuestCooldownDays,
            MaxQuestCooldownDays = settings.MaxQuestCooldownDays,
            EnableSocialCircle = settings.EnableSocialCircle,
            EnablePlayerInfluenceNews = settings.EnablePlayerInfluenceNews,
            EnableAISimulationNews = settings.EnableAISimulationNews,
            EnableSocialCircleAutoActions = settings.EnableSocialCircleAutoActions,
            EnableRPGDialogue = settings.EnableRPGDialogue,
            EnableRPGAPI = settings.EnableRPGAPI,
            EnableRPGNonVerbalPawnSpeech = settings.EnableRPGNonVerbalPawnSpeech,
            ExpandMemoryCompatMode = RelationsSettingsPolicy.NormalizeCompatMode(settings.ExpandMemoryCompatMode),
            ExpandMemoryInjectPawnMemory = settings.ExpandMemoryInjectPawnMemory,
            ExpandMemoryPawnMemoryMaxChars = settings.ExpandMemoryPawnMemoryMaxChars,
            ExpandMemoryPawnMemoryMaxEntries = settings.ExpandMemoryPawnMemoryMaxEntries,
            FactionExclusionDefNamesCsv = settings.FactionExclusionDefNamesCsv ?? "",
            DiplomacyImageProvider = settings.DiplomacyImageApi?.ProviderPreset ?? "",
            DiplomacyImageConfigured = settings.DiplomacyImageApi != null
        };
    }

    public static void CopyFrom(RelationsSettings settings, RelationsSettingsModel model)
    {
        if (settings == null || model == null)
            return;
        settings.UseCloudProviders = model.UseCloudProviders;
        settings.PromptLanguageFollowSystem = model.PromptLanguageFollowSystem;
        settings.PromptLanguageOverride = model.PromptLanguageOverride ?? "";
        settings.EnableAIGoodwillAdjustment = model.EnableAIGoodwillAdjustment;
        settings.EnableAIGiftSending = model.EnableAIGiftSending;
        settings.EnableAIWarDeclaration = model.EnableAIWarDeclaration;
        settings.EnableAIPeaceMaking = model.EnableAIPeaceMaking;
        settings.EnableAITradeCaravan = model.EnableAITradeCaravan;
        settings.EnableAIAidRequest = model.EnableAIAidRequest;
        settings.EnableAIRaidRequest = model.EnableAIRaidRequest;
        settings.EnableAIItemAirdrop = model.EnableAIItemAirdrop;
        settings.EnablePrisonerRansom = model.EnablePrisonerRansom;
        settings.MaxGoodwillAdjustmentPerCall = model.MaxGoodwillAdjustmentPerCall;
        settings.MaxDailyGoodwillAdjustment = model.MaxDailyGoodwillAdjustment;
        settings.GoodwillCooldownTicks = model.GoodwillCooldownTicks;
        settings.DialogueActionGoodwillCostMultiplier = model.DialogueActionGoodwillCostMultiplier;
        settings.MaxGiftSilverAmount = model.MaxGiftSilverAmount;
        settings.MaxGiftGoodwillGain = model.MaxGiftGoodwillGain;
        settings.GiftCooldownTicks = model.GiftCooldownTicks;
        settings.MinGoodwillForAid = model.MinGoodwillForAid;
        settings.AidCooldownTicks = model.AidCooldownTicks;
        settings.AidDelayBaseTicks = model.AidDelayBaseTicks;
        settings.ItemAirdropMinBudgetSilver = model.ItemAirdropMinBudgetSilver;
        settings.ItemAirdropMaxBudgetSilver = model.ItemAirdropMaxBudgetSilver;
        settings.ItemAirdropDefaultAIBudgetSilver = model.ItemAirdropDefaultAIBudgetSilver;
        settings.ItemAirdropCooldownTicks = model.ItemAirdropCooldownTicks;
        settings.MinQuestCooldownDays = model.MinQuestCooldownDays;
        settings.MaxQuestCooldownDays = model.MaxQuestCooldownDays;
        settings.EnableSocialCircle = model.EnableSocialCircle;
        settings.EnablePlayerInfluenceNews = model.EnablePlayerInfluenceNews;
        settings.EnableAISimulationNews = model.EnableAISimulationNews;
        settings.EnableSocialCircleAutoActions = model.EnableSocialCircleAutoActions;
        settings.EnableRPGDialogue = model.EnableRPGDialogue;
        settings.EnableRPGAPI = model.EnableRPGAPI;
        settings.EnableRPGNonVerbalPawnSpeech = model.EnableRPGNonVerbalPawnSpeech;
        settings.ExpandMemoryCompatMode = RelationsSettingsPolicy.NormalizeCompatMode(model.ExpandMemoryCompatMode);
        settings.ExpandMemoryInjectPawnMemory = model.ExpandMemoryInjectPawnMemory;
        settings.ExpandMemoryPawnMemoryMaxChars = model.ExpandMemoryPawnMemoryMaxChars;
        settings.ExpandMemoryPawnMemoryMaxEntries = model.ExpandMemoryPawnMemoryMaxEntries;
        settings.FactionExclusionDefNamesCsv = model.FactionExclusionDefNamesCsv ?? "";
    }
}
