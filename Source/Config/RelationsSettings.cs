using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Ustas.RimAI.Communication.Relations.UI;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Prompting;

namespace Ustas.RimAI.Communication.Relations.Config
{
    public enum TypewriterSpeedMode
    {
        Fast = 0,
        Standard = 1,
        Immersive = 2
    }

    public enum DialogueStyleMode
    {
        NaturalConcise = 0,
        Balanced = 1,
        Immersive = 2
    }

    public enum TerminalScale
    {
        Auto = 0,
        S100 = 1,
        S125 = 2,
        S150 = 3,
        S175 = 4,
        S200 = 5,
        S250 = 6
    }

    public enum ExpectedActionDenyLogLevel
    {
        Info = 0,
        Warning = 1
    }

    public enum NegotiatorSelectionMode
    {
        HighestSocial = 0,
        ProtagonistList = 1,
        LastUsed = 2,
        Designated = 3
    }

    public partial class RelationsSettings : ModSettings
    {
        // Provider Selection
        public bool UseCloudProviders = true;

        // Cloud API Configs
        public List<ApiConfig> CloudConfigs = new List<ApiConfig>();

        // Local Model Config
        public LocalModelConfig LocalConfig = new LocalModelConfig();

        // Diplomacy image API config (standalone from chat API)
        public DiplomacyImageApiConfig DiplomacyImageApi = new DiplomacyImageApiConfig();
        public List<DiplomacyImagePromptTemplate> DiplomacyImagePromptTemplates = new List<DiplomacyImagePromptTemplate>();
        public string SendImageCaptionStylePrompt = PromptTextConstants.SendImageCaptionStylePromptDefault;
        public string SendImageCaptionFallbackTemplate = PromptTextConstants.SendImageCaptionFallbackTemplateDefault;
        public string SelfieSelectedColonistThingId = string.Empty;
        public string SelfiePromptText = string.Empty;
        public string SelfieCaptionText = string.Empty;
        public string SelfieSizeText = DiplomacyImageApiConfig.DefaultImageSize;
        public bool SelfieWatermark;
        public bool SelfieIncludeAge = true;
        public bool SelfieIncludeGender = true;
        public bool SelfieIncludeFaction = true;
        public bool SelfieIncludeRole = true;
        public bool SelfieIncludeBodyType = true;
        public bool SelfieIncludeHair = true;
        public bool SelfieIncludeXenotype = true;
        public bool SelfieIncludeApparel = true;
        public bool SelfieIncludeHediffs = true;
        public bool SelfieIncludeHealth = true;
        public bool SelfieIncludeWeapon = true;
        public bool SelfieIncludeEquipment = true;
        public bool SelfieIncludePositivePrompt;
        public bool SelfieIncludeNegativePrompt;
        public string SelfiePositivePromptText = string.Empty;
        public string SelfieNegativePromptText = string.Empty;

        // Prompt output language settings
        public bool PromptLanguageFollowSystem = true;
        public string PromptLanguageOverride = "";
        public List<UserDefinedPromptVariableConfig> UserDefinedPromptVariables = new List<UserDefinedPromptVariableConfig>();
        public List<FactionPromptVariableRuleConfig> UserDefinedPromptVariableFactionRules = new List<FactionPromptVariableRuleConfig>();
        public List<PawnPromptVariableRuleConfig> UserDefinedPromptVariablePawnRules = new List<PawnPromptVariableRuleConfig>();
        public List<FactionScopedPromptVariableOverrideConfig> FactionScopedPromptVariableOverrides = new List<FactionScopedPromptVariableOverrideConfig>();


        // AI Behavior Limits
        public int MaxGoodwillAdjustmentPerCall = 15;
        public int MaxDailyGoodwillAdjustment = 30;
        public int GoodwillCooldownTicks = 0;
        public int MaxGiftSilverAmount = 1000;
        public int MaxGiftGoodwillGain = 10;
        public int GiftCooldownTicks = 60000;
        public int MinGoodwillForAid = 40;
        public int AidCooldownTicks = 120000;
        public int MaxGoodwillForWarDeclaration = -50;
        public int WarCooldownTicks = 60000;
        public int MaxPeaceCost = 5000;
        public int PeaceGoodwillReset = -20;
        public int PeaceCooldownTicks = 60000;
        public int CaravanCooldownTicks = 90000;
        public int AidDelayBaseTicks = 90000;
        public int CaravanDelayBaseTicks = 135000;
        public int RaidCooldownTicks = 180000;
        public float DialogueActionGoodwillCostMultiplier = 0.5f;
        public NegotiatorSelectionMode DiplomacyNegotiatorMode = NegotiatorSelectionMode.HighestSocial;
        public int DesignatedNegotiatorThingId = -1;

        public bool EnableNpcInitiatedDialogue = true;
        public bool EnablePawnRpgInitiatedDialogue = true;
        public NpcPushFrequencyMode NpcPushFrequencyMode = NpcPushFrequencyMode.Low;
        public int NpcQueueMaxPerFaction = 3;
        public float NpcQueueExpireHours = 12f;
        public float NpcGlobalDeliveryCooldownHours = 3f;
        public int NpcGlobalMaxMessagesPerWindow = 1;
        public float NpcGlobalWindowHours = 12f;
        public int NpcFactionCooldownMinDays = 3;
        public int NpcFactionCooldownMaxDays = 7;
        public bool EnableBusyByDrafted = true;
        public bool EnableBusyByHostiles = true;
        public bool EnableBusyByClickRate = true;
        public bool EnableNpcPushThrottleDebugLog = false;
        public int NpcPushThrottleProfileVersion = 1;
        public int PawnRpgProtagonistCap = 20;
        public bool EnableColonistToColonistDialogue = true;
        public int ColonistPairMinOpinion = 10;
        public NpcPushFrequencyMode ColonistPairFrequencyMode = NpcPushFrequencyMode.Low;
        public bool EnableAIGoodwillAdjustment = true;
        public bool EnableAIGiftSending = true;
        public bool EnableAIWarDeclaration = true;
        public bool EnableAIPeaceMaking = true;
        public bool EnableAITradeCaravan = true;
        public bool EnableAIRaidRequest = true;
        public bool EnableAIAidRequest = true;
        public bool EnableAIItemAirdrop = true;
        public bool EnablePrisonerRansom = true;
        public string RansomPaymentModeDefault = "silver";
        public int RansomReleaseTimeoutTicks = 30000;
        public float RansomValueDropMajorThreshold = 0.30f;
        public float RansomValueDropSevereThreshold = 0.60f;
        public int RansomLowGoodwillDiscountThreshold = 80;
        public float RansomLowGoodwillDiscountFactor = 0.8f;
        public int RansomPenaltyMajor = -15;
        public int RansomPenaltySevere = -25;
        public int RansomPenaltyTimeout = -35;

        public int ItemAirdropMinBudgetSilver = 200;
        public int ItemAirdropMaxBudgetSilver = 50000;
        public int ItemAirdropDefaultAIBudgetSilver = 2000;
        public float ItemAirdropRansomBudgetPercent = 0.01f;
        public int ItemAirdropMaxStacksPerDrop = 8;
        public int ItemAirdropMaxTotalItemsPerDrop = 200;
        public string ItemAirdropBlacklistDefNamesCsv = "VanometricPowerCell,PersonaCore,ArchotechArm,ArchotechLeg";
        public string FactionExclusionDefNamesCsv = "CASacrilegHunters";
        public int ItemAirdropSelectionCandidateLimit = 30;
        public int ItemAirdropSecondPassTimeoutSeconds = 25;
        public int ItemAirdropSecondPassQueueTimeoutSeconds = 15;
        public string ItemAirdropBlockedCategoriesCsv = "";
        public bool EnableAirdropAliasExpansion = true;
        public int ItemAirdropAliasExpansionMaxCount = 8;
        public int ItemAirdropAliasExpansionTimeoutSeconds = 4;
        public bool EnableAirdropSameFamilyRelaxedRetry = true;
        public int ItemAirdropCooldownTicks = 180000;
        public float ItemAirdropUntradeablePriceMultiplier = 6.0f;
        public float ItemAirdropUntradeableLowValuePriceMultiplier = 15.0f;
        public float ItemAirdropUntradeableMidValuePriceMultiplier = 8.0f;
        public float ItemAirdropNeedPriceMultiplier = 1.6f;
        public float ItemAirdropExoticMiscNeedPriceMultiplier = 5.0f;
        public float ItemAirdropOfferPriceMultiplier = 0.6f;
        public float ItemAirdropExoticMiscOfferPriceMultiplier = 0.9f;
        public float ItemAirdropUntradeableOfferPriceMultiplier = 1.0f;
        public float ItemAirdropSpecialItemDiscountMultiplier = 0.4f;
        public float ItemAirdropSpecialItemScarceMultiplier = 2.0f;
        public float ItemAirdropTradeLimitMultiplier = 2.0f;
        public float ItemAirdropCooldownMultiplier = 1.0f;

        // Quest Settings
        public int MinQuestCooldownDays = 7;
        public int MaxQuestCooldownDays = 12;

        // Raid Granular Settings
        public bool EnableRaidStrategy_ImmediateAttack = true;
        public bool EnableRaidStrategy_ImmediateAttackSmart = true;
        public bool EnableRaidStrategy_StageThenAttack = true;
        public bool EnableRaidStrategy_ImmediateAttackSappers = true;
        public bool EnableRaidStrategy_Siege = true;

        public bool EnableRaidArrival_EdgeWalkIn = true;
        public bool EnableRaidArrival_EdgeDrop = true;
        public bool EnableRaidArrival_EdgeWalkInGroups = true;
        public bool EnableRaidArrival_RandomDrop = false;
        public bool EnableRaidArrival_CenterDrop = false;
        public float RaidPointsMultiplier = 1f;
        public float MinRaidPoints = 35f;
        public List<RaidPointsFactionOverride> RaidPointsFactionOverrides = new List<RaidPointsFactionOverride>();

        public bool EnableAPICallLogging = true;
        public int MaxAPICallsPerHour = 0;


        // Debug Settings

        // UI Settings  
        public TypewriterSpeedMode TypewriterSpeedMode = TypewriterSpeedMode.Immersive;
        public DialogueStyleMode DialogueStyleMode = DialogueStyleMode.NaturalConcise;
        public ExpectedActionDenyLogLevel ExpectedActionDenyLogLevel = ExpectedActionDenyLogLevel.Info;
        public int ProactiveMessageHardLimit = 0;
        public bool EnableDiplomacyStrategyToggle = true;

        // Advanced API Parameters
        public bool ThinkingEnabled = false;
        public string ReasoningEffort = "medium";
        public float Temperature = 0.5f;
        public int MaxTokens = 2048;

        // Comms Console Settings
        public bool ReplaceCommsConsole = false;
        public TerminalScale TerminalScale = TerminalScale.Auto;
        public int ActiveBezelIndex = 0; // 0=Standard, 1=Spacer, 2=Fallout
        [Obsolete("Thought chain feature removed")]
        public bool EnableThoughtChainNode = true;
        [Obsolete("Thought chain feature removed")]
        public List<PromptChannelToggleConfig> ThoughtChainByChannel = new List<PromptChannelToggleConfig>();

        // Presence Settings
        public bool EnableFactionPresenceStatus = true;
        public float PresenceCacheHours = 2f;
        // Legacy compatibility field only. Runtime forced-offline duration is fixed in manager logic and this value is no longer exposed in UI.
        public float PresenceForcedOfflineHours = 24f;
        public bool PresenceNightBiasEnabled = true;
        public int PresenceNightStartHour = 0;
        public int PresenceNightEndHour = 5;
        public float PresenceNightOfflineBias = 0.85f;
        public bool PresenceUseAdvancedProfiles = true;
        public int PresenceOnlineStart_Default = 7;
        public int PresenceOnlineDuration_Default = 12;
        public int PresenceOnlineStart_Neolithic = 8;
        public int PresenceOnlineDuration_Neolithic = 8;
        public int PresenceOnlineStart_Medieval = 8;
        public int PresenceOnlineDuration_Medieval = 10;
        public int PresenceOnlineStart_Industrial = 7;
        public int PresenceOnlineDuration_Industrial = 14;
        public int PresenceOnlineStart_Spacer = 6;
        public int PresenceOnlineDuration_Spacer = 18;
        public int PresenceOnlineStart_Ultra = 4;
        public int PresenceOnlineDuration_Ultra = 20;
        public int PresenceOnlineStart_Archotech = 4;
        public int PresenceOnlineDuration_Archotech = 20;

        // Social Circle Settings
        public bool EnableSocialCircle = true;
        public ScheduledNewsFrequencyLevel ScheduledNewsFrequencyLevel = ScheduledNewsFrequencyLevel.High;
        // Legacy migration fields only. Current runtime scheduling uses ScheduledNewsFrequencyLevel; these values are kept for old save import.
        public int SocialPostIntervalMinDays = 5;
        public int SocialPostIntervalMaxDays = 7;
        public bool EnablePlayerInfluenceNews = true;
        public bool EnableAISimulationNews = true;
        public bool EnableSocialCircleAutoActions = false;

        // RPG Dialogue Settings
        public bool EnableRPGDialogue = true;
        public bool EnableRPGAPI = true;
        public bool EnableRPGNonVerbalPawnSpeech = true;

        // Connection Test State

        internal bool _promptWorkbenchExperimentalEnabled;

        // Model Cache
        internal static readonly Dictionary<string, List<string>> ModelCache = new();

        // Prompt Settings -  FactionPromptManager

        // Global Prompt Settings
        public string GlobalSystemPrompt = "";
        public string GlobalDialoguePrompt = "";
        public string RPGRoleSetting = "";
        public string RPGDialogueStyle = "";
        public string RPGApiGuidelines = "";
        public string RPGFormatConstraint = "";
        public string RPGRoleSettingFallbackTemplate = "";
        public string RPGFormatConstraintHeader = "";
        public string RPGCompactFormatFallback = "";
        public string RPGActionReliabilityFallback = "";
        public string RPGActionReliabilityMarker = "";
        internal RpgApiActionPromptConfig RPGApiActionPromptConfig = RpgApiActionPromptConfig.CreateFallback();
        [Obsolete("Use RPGRoleSetting instead")]
        public string RPGSystemPrompt = "";
        [Obsolete("Use RPGDialogueStyle instead")]
        public string RPGDialoguePrompt = "";
        [Obsolete("Use RPGApiGuidelines instead")]
        public string RPGApiFormatPrompt = "";

        public int MaxSystemPromptLength = 2000;
        public int MaxDialoguePromptLength = 2000;
        public int MaxFactionPromptLength = 4000;
        public bool EnableApiPromptEditing = false;

        // Prompt Scenario Tag Settings
        public string DiplomacyManualSceneTagsCsv = "scene:social";
        public string RpgManualSceneTagsCsv = "scene:daily";
        public bool PromptPreviewUseProactiveContext = false;
        public string PromptPreviewSceneTagsCsv = "scene:social";
        public bool RpgPromptPreviewUseProactiveContext = false;
        public string RpgPromptPreviewSceneTagsCsv = "scene:daily";

        // Dialogue Context Compression Settings
        public bool EnableDialogueContextCompression = true;
        public int DialogueCompressionKeepRecentTurns = 10;
        public int DialogueCompressionFirstPassChunkSize = 10;
        public int DialogueCompressionSecondaryTriggerTurns = 20;
        public int DialogueCompressionSecondaryWindowMinRecency = 21;
        public int DialogueCompressionSecondaryWindowMaxRecency = 25;
        public int DialogueCompressionSecondaryTierStart = 21;
        public int DialogueCompressionTertiaryTierStart = 26;
        public int DialogueCompressionMaxMark = 3;
        public int DialogueCompressionMaxEventsPerSegment = 3;
        public int DialogueCompressionSnippetMaxChars = 28;
        public int DialogueCompressionMaxSummaryLines = 3;
        public int DialogueCompressionMaxSecondaryRounds = 3;

        // RPG Dynamic Injection Settings
        public bool RPGInjectSelfStatus = true;
        public bool RPGInjectInterlocutorStatus = true;
        public bool RPGInjectFactionBackground = true;

        [Obsolete("Use RPGInjectSelfStatus instead")]
        public bool RPGInjectPawnInfo = true;
        [Obsolete("Use RPGInjectFactionBackground instead")]
        public bool RPGInjectFactionInfo = true;

        // Prompt editing state

        public override void ExposeData()
        {
            RelationsSettingsPersistence.Expose(this);
            base.ExposeData();
        }

        internal void FlushPromptWorkspaceEdits(bool persistToDisk = true)
        {
            RelationsSettingsPages.For(this).PromptWorkspaceBuffers.FlushPromptWorkspaceEdits(persistToDisk);
        }

        internal void DisposePromptWorkspaceRenderTextures()
        {
            RelationsSettingsPages.For(this).PromptWorkspaceBuffers.DisposePromptWorkspaceRenderTextures();
        }

        internal static string GetPromptWorkspaceQuickFactionLabel(Faction faction)
        {
            return RelationsPromptQuickActions.GetPromptWorkspaceQuickFactionLabel(faction);
        }

        internal static string GetPromptWorkspaceQuickPawnLabel(Pawn pawn)
        {
            return RelationsPromptQuickActions.GetPromptWorkspaceQuickPawnLabel(pawn);
        }

        internal string GetHelpDisplayContentForLanguage(string languageFolder)
        {
            return RelationsSettingsPages.For(this).ApiHeader.GetHelpDisplayContentForLanguage(languageFolder);
        }

        internal string GetVersionLogDisplayContentForLanguage(string languageFolder)
        {
            return RelationsSettingsPages.For(this).ApiHeader.GetVersionLogDisplayContentForLanguage(languageFolder);
        }

        internal string GetVersionDisplayVersion()
        {
            return RelationsSettingsPages.For(this).ApiHeader.GetVersionDisplayVersion();
        }

        internal void FlushPromptEditorsToStorageForPreset(bool persistToFiles = false)
        {
            RelationsSettingsPages.For(this).PromptWorkbench.FlushPromptEditorsToStorageForPreset(persistToFiles);
        }

        internal void RefreshPromptEditorStateFromStorage()
        {
            RelationsSettingsPages.For(this).PromptWorkbench.RefreshPromptEditorStateFromStorage();
        }

        internal void HandlePromptWorkspaceQuickPromptSaved(QuickPromptTargetKind kind, string targetLabel)
        {
            RelationsSettingsPages.For(this).PromptQuickActions.HandlePromptWorkspaceQuickPromptSaved(kind, targetLabel);
        }

        internal void SetPromptWorkbenchExperimentalEnabled(bool enabled)
        {
            _promptWorkbenchExperimentalEnabled = enabled;
        }

        internal static bool TryGetSharedTextConfig(out ApiConfig config)
        {
            return RelationsSettingsPromptLanguage.TryGetSharedTextConfig(out config);
        }

        internal static string GetMaxTokensLabel(int value)
        {
            return RelationsSettingsPromptLanguage.GetMaxTokensLabel(value);
        }

        internal static string GetReasoningEffortLabel(string value)
        {
            return RelationsSettingsPromptLanguage.GetReasoningEffortLabel(value);
        }

        public bool IsExpandMemoryCompatEnabled()
        {
            return RelationsPromptCatalogService.IsExpandMemoryCompatEnabled(this);
        }

        public bool IsExpandMemoryPawnMemoryEnabled()
        {
            return RelationsPromptCatalogService.IsExpandMemoryPawnMemoryEnabled(this);
        }

    }
}
