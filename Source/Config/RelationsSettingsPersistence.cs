using System;
using System.Collections.Generic;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.UI;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Config;

internal static class RelationsSettingsPersistence
{
        internal static void Expose(RelationsSettings s)
        {
            Scribe_Values.Look(ref s.UseCloudProviders, "UseCloudProviders", true);
            Scribe_Collections.Look(ref s.CloudConfigs, "CloudConfigs", LookMode.Deep);
            Scribe_Deep.Look(ref s.LocalConfig, "LocalConfig");
            Scribe_Deep.Look(ref s.DiplomacyImageApi, "DiplomacyImageApi");
            Scribe_Collections.Look(ref s.DiplomacyImagePromptTemplates, "DiplomacyImagePromptTemplates", LookMode.Deep);
            Scribe_Values.Look(ref s.SendImageCaptionStylePrompt, "SendImageCaptionStylePrompt", PromptTextConstants.SendImageCaptionStylePromptDefault);
            Scribe_Values.Look(ref s.SendImageCaptionFallbackTemplate, "SendImageCaptionFallbackTemplate", PromptTextConstants.SendImageCaptionFallbackTemplateDefault);
            Scribe_Values.Look(ref s.SelfieSelectedColonistThingId, "SelfieSelectedColonistThingId", string.Empty);
            Scribe_Values.Look(ref s.SelfiePromptText, "SelfiePromptText", string.Empty);
            Scribe_Values.Look(ref s.SelfieCaptionText, "SelfieCaptionText", string.Empty);
            Scribe_Values.Look(ref s.SelfieSizeText, "SelfieSizeText", DiplomacyImageApiConfig.DefaultImageSize);
            Scribe_Values.Look(ref s.SelfieWatermark, "SelfieWatermark", false);
            Scribe_Values.Look(ref s.SelfieIncludeAge, "SelfieIncludeAge", true);
            Scribe_Values.Look(ref s.SelfieIncludeGender, "SelfieIncludeGender", true);
            Scribe_Values.Look(ref s.SelfieIncludeFaction, "SelfieIncludeFaction", true);
            Scribe_Values.Look(ref s.SelfieIncludeRole, "SelfieIncludeRole", true);
            Scribe_Values.Look(ref s.SelfieIncludeBodyType, "SelfieIncludeBodyType", true);
            Scribe_Values.Look(ref s.SelfieIncludeHair, "SelfieIncludeHair", true);
            Scribe_Values.Look(ref s.SelfieIncludeXenotype, "SelfieIncludeXenotype", true);
            Scribe_Values.Look(ref s.SelfieIncludeApparel, "SelfieIncludeApparel", true);
            Scribe_Values.Look(ref s.SelfieIncludeHediffs, "SelfieIncludeHediffs", true);
            Scribe_Values.Look(ref s.SelfieIncludeHealth, "SelfieIncludeHealth", true);
            Scribe_Values.Look(ref s.SelfieIncludeWeapon, "SelfieIncludeWeapon", true);
            Scribe_Values.Look(ref s.SelfieIncludeEquipment, "SelfieIncludeEquipment", true);
            Scribe_Values.Look(ref s.SelfieIncludePositivePrompt, "SelfieIncludePositivePrompt", false);
            Scribe_Values.Look(ref s.SelfieIncludeNegativePrompt, "SelfieIncludeNegativePrompt", false);
            Scribe_Values.Look(ref s.SelfiePositivePromptText, "SelfiePositivePromptText", string.Empty);
            Scribe_Values.Look(ref s.SelfieNegativePromptText, "SelfieNegativePromptText", string.Empty);
            Scribe_Values.Look(ref s.PromptLanguageFollowSystem, "PromptLanguageFollowSystem", true);
            Scribe_Values.Look(ref s.PromptLanguageOverride, "PromptLanguageOverride", "");
            Scribe_Collections.Look(ref s.UserDefinedPromptVariables, "UserDefinedPromptVariables", LookMode.Deep);
            Scribe_Collections.Look(ref s.UserDefinedPromptVariableFactionRules, "UserDefinedPromptVariableFactionRules", LookMode.Deep);
            Scribe_Collections.Look(ref s.UserDefinedPromptVariablePawnRules, "UserDefinedPromptVariablePawnRules", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                Scribe_Collections.Look(ref s.FactionScopedPromptVariableOverrides, "FactionScopedPromptVariableOverrides", LookMode.Deep);
            }

            // Debug Settings

            // UI Settings
            Scribe_Values.Look(ref s.TypewriterSpeedMode, "TypewriterSpeedMode", TypewriterSpeedMode.Standard);
            Scribe_Values.Look(ref s.DialogueStyleMode, "DialogueStyleMode", DialogueStyleMode.NaturalConcise);
            Scribe_Values.Look(ref s.ExpectedActionDenyLogLevel, "ExpectedActionDenyLogLevel", ExpectedActionDenyLogLevel.Info);
            Scribe_Values.Look(ref s.ProactiveMessageHardLimit, "ProactiveMessageHardLimit", 0);
            Scribe_Values.Look(ref s.EnableDiplomacyStrategyToggle, "EnableDiplomacyStrategyToggle", true);
            Scribe_Values.Look(ref s.ThinkingEnabled, "ThinkingEnabled", false);
            Scribe_Values.Look(ref s.ReasoningEffort, "ReasoningEffort", "medium");
            Scribe_Values.Look(ref s.Temperature, "Temperature", 0.5f);
            Scribe_Values.Look(ref s.MaxTokens, "MaxTokens", 2048);

            // Comms Console Settings
            Scribe_Values.Look(ref s.ReplaceCommsConsole, "ReplaceCommsConsole", false);
            Scribe_Values.Look(ref s.TerminalScale, "TerminalScale", TerminalScale.Auto);
            Scribe_Values.Look(ref s.ActiveBezelIndex, "ActiveBezelIndex", 0);
#pragma warning disable CS0618 // Obsolete fields retained for save compatibility
            Scribe_Collections.Look(ref s.ThoughtChainByChannel, "ThoughtChainByChannel", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                bool legacyEnableThoughtChainNode = true;
                Scribe_Values.Look(ref legacyEnableThoughtChainNode, "EnableThoughtChainNode", true);
            }
#pragma warning restore CS0618

            // RPG Dialogue Settings
            Scribe_Values.Look(ref s.EnableRPGDialogue, "EnableRPGDialogue", true);
            Scribe_Values.Look(ref s.EnableRPGAPI, "EnableRPGAPI", true);
            Scribe_Values.Look(ref s.EnableRPGNonVerbalPawnSpeech, "EnableRPGNonVerbalPawnSpeech", true);
            
            // Refined RPG Prompt Settings
            // RPG prompt text persistence is handled by Prompt/Custom/PawnDialoguePrompt_Custom.json only.
            
            // Refined RPG Dynamic Injection Settings
            Scribe_Values.Look(ref s.RPGInjectSelfStatus, "RPGInjectSelfStatus", true);
            Scribe_Values.Look(ref s.RPGInjectInterlocutorStatus, "RPGInjectInterlocutorStatus", true);
            Scribe_Values.Look(ref s.RPGInjectFactionBackground, "RPGInjectFactionBackground", true);
            s.ExposeData_RimTalkCompat();

            // Migration from old fields
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                bool oldRPGInjectPawnInfo = true;
                bool oldRPGInjectFactionInfo = true;

                Scribe_Values.Look(ref oldRPGInjectPawnInfo, "RPGInjectPawnInfo", true);
                Scribe_Values.Look(ref oldRPGInjectFactionInfo, "RPGInjectFactionInfo", true);

                bool hasRpgCustomPromptFile = RpgPromptCustomStore.CustomConfigExists();
                s.LoadRpgPromptTextsFromCustom();
            }

            // Global Prompt Settings
            Scribe_Values.Look(ref s.MaxSystemPromptLength, "MaxSystemPromptLength", 2000);
            Scribe_Values.Look(ref s.MaxDialoguePromptLength, "MaxDialoguePromptLength", 2000);
            Scribe_Values.Look(ref s.MaxFactionPromptLength, "MaxFactionPromptLength", 4000);
            Scribe_Values.Look(ref s.EnableApiPromptEditing, "EnableApiPromptEditing", false);
            Scribe_Values.Look(ref s.DiplomacyManualSceneTagsCsv, "DiplomacyManualSceneTagsCsv", "scene:social");
            Scribe_Values.Look(ref s.RpgManualSceneTagsCsv, "RpgManualSceneTagsCsv", "scene:daily");
            Scribe_Values.Look(ref s.PromptPreviewUseProactiveContext, "PromptPreviewUseProactiveContext", false);
            Scribe_Values.Look(ref s.PromptPreviewSceneTagsCsv, "PromptPreviewSceneTagsCsv", "scene:social");
            Scribe_Values.Look(ref s.RpgPromptPreviewUseProactiveContext, "RpgPromptPreviewUseProactiveContext", false);
            Scribe_Values.Look(ref s.RpgPromptPreviewSceneTagsCsv, "RpgPromptPreviewSceneTagsCsv", "scene:daily");
            Scribe_Values.Look(ref s.EnableDialogueContextCompression, "EnableDialogueContextCompression", true);
            Scribe_Values.Look(ref s.DialogueCompressionKeepRecentTurns, "DialogueCompressionKeepRecentTurns", 10);
            Scribe_Values.Look(ref s.DialogueCompressionFirstPassChunkSize, "DialogueCompressionFirstPassChunkSize", 10);
            Scribe_Values.Look(ref s.DialogueCompressionSecondaryTriggerTurns, "DialogueCompressionSecondaryTriggerTurns", 20);
            Scribe_Values.Look(ref s.DialogueCompressionSecondaryWindowMinRecency, "DialogueCompressionSecondaryWindowMinRecency", 21);
            Scribe_Values.Look(ref s.DialogueCompressionSecondaryWindowMaxRecency, "DialogueCompressionSecondaryWindowMaxRecency", 25);
            Scribe_Values.Look(ref s.DialogueCompressionSecondaryTierStart, "DialogueCompressionSecondaryTierStart", 21);
            Scribe_Values.Look(ref s.DialogueCompressionTertiaryTierStart, "DialogueCompressionTertiaryTierStart", 26);
            Scribe_Values.Look(ref s.DialogueCompressionMaxMark, "DialogueCompressionMaxMark", 3);
            Scribe_Values.Look(ref s.DialogueCompressionMaxEventsPerSegment, "DialogueCompressionMaxEventsPerSegment", 3);
            Scribe_Values.Look(ref s.DialogueCompressionSnippetMaxChars, "DialogueCompressionSnippetMaxChars", 28);
            Scribe_Values.Look(ref s.DialogueCompressionMaxSummaryLines, "DialogueCompressionMaxSummaryLines", 3);
            Scribe_Values.Look(ref s.DialogueCompressionMaxSecondaryRounds, "DialogueCompressionMaxSecondaryRounds", 3);

            s.DialogueCompressionKeepRecentTurns = Math.Max(6, s.DialogueCompressionKeepRecentTurns);
            s.DialogueCompressionSecondaryTierStart = Math.Max(s.DialogueCompressionKeepRecentTurns + 1, s.DialogueCompressionSecondaryTierStart);
            s.DialogueCompressionTertiaryTierStart = Math.Max(s.DialogueCompressionSecondaryTierStart + 1, s.DialogueCompressionTertiaryTierStart);
            s.DialogueCompressionMaxMark = 3;
            s.DialogueCompressionMaxEventsPerSegment = Math.Max(1, Math.Min(3, s.DialogueCompressionMaxEventsPerSegment));
            s.DialogueCompressionMaxSummaryLines = Math.Max(1, Math.Min(3, s.DialogueCompressionMaxSummaryLines));

            // AI Control Settings
            RelationsSettingsAiPersistence.Expose(s);

            if (s.CloudConfigs == null) s.CloudConfigs = new List<ApiConfig>();
            if (s.LocalConfig == null) s.LocalConfig = new LocalModelConfig();
            if (s.DiplomacyImageApi == null) s.DiplomacyImageApi = new DiplomacyImageApiConfig();
            if (s.DiplomacyImagePromptTemplates == null) s.DiplomacyImagePromptTemplates = new List<DiplomacyImagePromptTemplate>();
            if (s.UserDefinedPromptVariables == null) s.UserDefinedPromptVariables = new List<UserDefinedPromptVariableConfig>();
            if (s.UserDefinedPromptVariableFactionRules == null) s.UserDefinedPromptVariableFactionRules = new List<FactionPromptVariableRuleConfig>();
            if (s.UserDefinedPromptVariablePawnRules == null) s.UserDefinedPromptVariablePawnRules = new List<PawnPromptVariableRuleConfig>();
            if (s.FactionScopedPromptVariableOverrides == null) s.FactionScopedPromptVariableOverrides = new List<FactionScopedPromptVariableOverrideConfig>();
            if (s.SendImageCaptionStylePrompt == null) s.SendImageCaptionStylePrompt = PromptTextConstants.SendImageCaptionStylePromptDefault;
            if (s.SendImageCaptionFallbackTemplate == null) s.SendImageCaptionFallbackTemplate = PromptTextConstants.SendImageCaptionFallbackTemplateDefault;
            if (s.SelfieSelectedColonistThingId == null) s.SelfieSelectedColonistThingId = string.Empty;
            if (string.IsNullOrWhiteSpace(s.SelfiePromptText)) s.SelfiePromptText = "Згенеруй зображення: переведи аніме-стилізованого персонажа в косплей-фотографію в приглушених кольорах, поза — селфі, композиція як у реального селфі в обличчя, збережи ключові елементи одягу, тло — типова сцена фракції, світло тепліше для атмосфери, на повний зріст. Співвідношення 4:3.";
            if (s.SelfieCaptionText == null) s.SelfieCaptionText = string.Empty;
            if (string.IsNullOrWhiteSpace(s.SelfieSizeText)) s.SelfieSizeText = DiplomacyImageApiConfig.DefaultImageSize;
            if (s.SelfiePositivePromptText == null) s.SelfiePositivePromptText = string.Empty;
            if (s.SelfieNegativePromptText == null) s.SelfieNegativePromptText = string.Empty;
            s.ProactiveMessageHardLimit = Math.Max(0, s.ProactiveMessageHardLimit);
            s.NormalizeCloudConfigUrls();
            RelationsSettingsPages.For(s).Image.EnsureDiplomacyImageDefaults();
            UserDefinedPromptVariableService.NormalizeSettingsCollections(s);

            
        }
}
