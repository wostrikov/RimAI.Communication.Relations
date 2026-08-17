namespace Ustas.RimAI.Communication.Relations.Settings;

/// <summary>Renderer-neutral snapshot of Relations settings that policy can validate and reset.</summary>
public sealed class RelationsSettingsModel
{
    public bool UseCloudProviders { get; set; } = true;
    public int CloudConfigCount { get; set; }
    public bool PromptLanguageFollowSystem { get; set; } = true;
    public string PromptLanguageOverride { get; set; } = "";

    public bool EnableAIGoodwillAdjustment { get; set; } = true;
    public bool EnableAIGiftSending { get; set; } = true;
    public bool EnableAIWarDeclaration { get; set; } = true;
    public bool EnableAIPeaceMaking { get; set; } = true;
    public bool EnableAITradeCaravan { get; set; } = true;
    public bool EnableAIAidRequest { get; set; } = true;
    public bool EnableAIRaidRequest { get; set; } = true;
    public bool EnableAIItemAirdrop { get; set; } = true;
    public bool EnablePrisonerRansom { get; set; } = true;

    public int MaxGoodwillAdjustmentPerCall { get; set; } = 15;
    public int MaxDailyGoodwillAdjustment { get; set; } = 30;
    public int GoodwillCooldownTicks { get; set; } = 2500;
    public float DialogueActionGoodwillCostMultiplier { get; set; } = 0.5f;

    public int MaxGiftSilverAmount { get; set; } = 1000;
    public int MaxGiftGoodwillGain { get; set; } = 10;
    public int GiftCooldownTicks { get; set; } = 60000;

    public int MinGoodwillForAid { get; set; } = 40;
    public int AidCooldownTicks { get; set; } = 120000;
    public int AidDelayBaseTicks { get; set; } = 90000;

    public int ItemAirdropMinBudgetSilver { get; set; } = 200;
    public int ItemAirdropMaxBudgetSilver { get; set; } = 50000;
    public int ItemAirdropDefaultAIBudgetSilver { get; set; } = 2000;
    public int ItemAirdropCooldownTicks { get; set; } = 180000;

    public int MinQuestCooldownDays { get; set; } = 7;
    public int MaxQuestCooldownDays { get; set; } = 12;

    public bool EnableSocialCircle { get; set; } = true;
    public bool EnablePlayerInfluenceNews { get; set; } = true;
    public bool EnableAISimulationNews { get; set; } = true;
    public bool EnableSocialCircleAutoActions { get; set; }

    public bool EnableRPGDialogue { get; set; } = true;
    public bool EnableRPGAPI { get; set; } = true;
    public bool EnableRPGNonVerbalPawnSpeech { get; set; } = true;

    public string ExpandMemoryCompatMode { get; set; } = "auto";
    public bool ExpandMemoryInjectPawnMemory { get; set; } = true;
    public int ExpandMemoryPawnMemoryMaxChars { get; set; } = 1200;
    public int ExpandMemoryPawnMemoryMaxEntries { get; set; } = 50;
    public string FactionExclusionDefNamesCsv { get; set; } = "CASacrilegHunters";

    public string DiplomacyImageProvider { get; set; } = "";
    public bool DiplomacyImageConfigured { get; set; }

    public static RelationsSettingsModel Default() => new();

    public RelationsSettingsModel Clone()
    {
        return new RelationsSettingsModel
        {
            UseCloudProviders = UseCloudProviders,
            CloudConfigCount = CloudConfigCount,
            PromptLanguageFollowSystem = PromptLanguageFollowSystem,
            PromptLanguageOverride = PromptLanguageOverride ?? "",
            EnableAIGoodwillAdjustment = EnableAIGoodwillAdjustment,
            EnableAIGiftSending = EnableAIGiftSending,
            EnableAIWarDeclaration = EnableAIWarDeclaration,
            EnableAIPeaceMaking = EnableAIPeaceMaking,
            EnableAITradeCaravan = EnableAITradeCaravan,
            EnableAIAidRequest = EnableAIAidRequest,
            EnableAIRaidRequest = EnableAIRaidRequest,
            EnableAIItemAirdrop = EnableAIItemAirdrop,
            EnablePrisonerRansom = EnablePrisonerRansom,
            MaxGoodwillAdjustmentPerCall = MaxGoodwillAdjustmentPerCall,
            MaxDailyGoodwillAdjustment = MaxDailyGoodwillAdjustment,
            GoodwillCooldownTicks = GoodwillCooldownTicks,
            DialogueActionGoodwillCostMultiplier = DialogueActionGoodwillCostMultiplier,
            MaxGiftSilverAmount = MaxGiftSilverAmount,
            MaxGiftGoodwillGain = MaxGiftGoodwillGain,
            GiftCooldownTicks = GiftCooldownTicks,
            MinGoodwillForAid = MinGoodwillForAid,
            AidCooldownTicks = AidCooldownTicks,
            AidDelayBaseTicks = AidDelayBaseTicks,
            ItemAirdropMinBudgetSilver = ItemAirdropMinBudgetSilver,
            ItemAirdropMaxBudgetSilver = ItemAirdropMaxBudgetSilver,
            ItemAirdropDefaultAIBudgetSilver = ItemAirdropDefaultAIBudgetSilver,
            ItemAirdropCooldownTicks = ItemAirdropCooldownTicks,
            MinQuestCooldownDays = MinQuestCooldownDays,
            MaxQuestCooldownDays = MaxQuestCooldownDays,
            EnableSocialCircle = EnableSocialCircle,
            EnablePlayerInfluenceNews = EnablePlayerInfluenceNews,
            EnableAISimulationNews = EnableAISimulationNews,
            EnableSocialCircleAutoActions = EnableSocialCircleAutoActions,
            EnableRPGDialogue = EnableRPGDialogue,
            EnableRPGAPI = EnableRPGAPI,
            EnableRPGNonVerbalPawnSpeech = EnableRPGNonVerbalPawnSpeech,
            ExpandMemoryCompatMode = ExpandMemoryCompatMode ?? "auto",
            ExpandMemoryInjectPawnMemory = ExpandMemoryInjectPawnMemory,
            ExpandMemoryPawnMemoryMaxChars = ExpandMemoryPawnMemoryMaxChars,
            ExpandMemoryPawnMemoryMaxEntries = ExpandMemoryPawnMemoryMaxEntries,
            FactionExclusionDefNamesCsv = FactionExclusionDefNamesCsv ?? "",
            DiplomacyImageProvider = DiplomacyImageProvider ?? "",
            DiplomacyImageConfigured = DiplomacyImageConfigured
        };
    }
}
