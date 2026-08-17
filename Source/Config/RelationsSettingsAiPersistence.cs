using System.Xml;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.UI;

namespace Ustas.RimAI.Communication.Relations.Config;

internal static class RelationsSettingsAiPersistence
{
        internal static void Expose(RelationsSettings Settings)
        {
            Scribe_Values.Look(ref Settings.MaxGoodwillAdjustmentPerCall, "MaxGoodwillAdjustmentPerCall", 15);
            Scribe_Values.Look(ref Settings.MaxDailyGoodwillAdjustment, "MaxDailyGoodwillAdjustment", 30);
            Scribe_Values.Look(ref Settings.GoodwillCooldownTicks, "GoodwillCooldownTicks", 2500);

            Scribe_Values.Look(ref Settings.MaxGiftSilverAmount, "MaxGiftSilverAmount", 1000);
            Scribe_Values.Look(ref Settings.MaxGiftGoodwillGain, "MaxGiftGoodwillGain", 10);
            Scribe_Values.Look(ref Settings.GiftCooldownTicks, "GiftCooldownTicks", 60000);

            Scribe_Values.Look(ref Settings.MinGoodwillForAid, "MinGoodwillForAid", 40);
            Scribe_Values.Look(ref Settings.AidCooldownTicks, "AidCooldownTicks", 120000);

            Scribe_Values.Look(ref Settings.MaxGoodwillForWarDeclaration, "MaxGoodwillForWarDeclaration", -50);
            Scribe_Values.Look(ref Settings.WarCooldownTicks, "WarCooldownTicks", 60000);

            Scribe_Values.Look(ref Settings.MaxPeaceCost, "MaxPeaceCost", 5000);
            Scribe_Values.Look(ref Settings.PeaceGoodwillReset, "PeaceGoodwillReset", -20);
            Scribe_Values.Look(ref Settings.PeaceCooldownTicks, "PeaceCooldownTicks", 60000);

            Scribe_Values.Look(ref Settings.CaravanCooldownTicks, "CaravanCooldownTicks", 90000);
            Scribe_Values.Look(ref Settings.AidDelayBaseTicks, "AidDelayBaseTicks", 90000);
            Scribe_Values.Look(ref Settings.CaravanDelayBaseTicks, "CaravanDelayBaseTicks", 135000);
            Scribe_Values.Look(ref Settings.DialogueActionGoodwillCostMultiplier, "DialogueActionGoodwillCostMultiplier", 0.5f);
            Scribe_Values.Look(ref Settings.DiplomacyNegotiatorMode, "DiplomacyNegotiatorMode", NegotiatorSelectionMode.HighestSocial);
            Scribe_Values.Look(ref Settings.DesignatedNegotiatorThingId, "DesignatedNegotiatorThingId", -1);

            Scribe_Values.Look(ref Settings.MinQuestCooldownDays, "MinQuestCooldownDays", 7);
            Scribe_Values.Look(ref Settings.MaxQuestCooldownDays, "MaxQuestCooldownDays", 12);

            Scribe_Values.Look(ref Settings.EnableAIGoodwillAdjustment, "EnableAIGoodwillAdjustment", true);
            Scribe_Values.Look(ref Settings.EnableAIGiftSending, "EnableAIGiftSending", true);
            Scribe_Values.Look(ref Settings.EnableAIWarDeclaration, "EnableAIWarDeclaration", true);
            Scribe_Values.Look(ref Settings.EnableAIPeaceMaking, "EnableAIPeaceMaking", true);
            Scribe_Values.Look(ref Settings.EnableAITradeCaravan, "EnableAITradeCaravan", true);
            Scribe_Values.Look(ref Settings.EnableAIAidRequest, "EnableAIAidRequest", true);
            Scribe_Values.Look(ref Settings.EnableAIRaidRequest, "EnableAIRaidRequest", true);
            Scribe_Values.Look(ref Settings.EnableAIItemAirdrop, "EnableAIItemAirdrop", true);
            Scribe_Values.Look(ref Settings.EnablePrisonerRansom, "EnablePrisonerRansom", true);
            Scribe_Values.Look(ref Settings.RansomPaymentModeDefault, "RansomPaymentModeDefault", "silver");
            Scribe_Values.Look(ref Settings.RansomReleaseTimeoutTicks, "RansomReleaseTimeoutTicks", 30000);
            Scribe_Values.Look(ref Settings.RansomValueDropMajorThreshold, "RansomValueDropMajorThreshold", 0.30f);
            Scribe_Values.Look(ref Settings.RansomValueDropSevereThreshold, "RansomValueDropSevereThreshold", 0.60f);
            Scribe_Values.Look(ref Settings.RansomLowGoodwillDiscountThreshold, "RansomLowGoodwillDiscountThreshold", 80);
            Scribe_Values.Look(ref Settings.RansomLowGoodwillDiscountFactor, "RansomLowGoodwillDiscountFactor", 0.8f);
            Scribe_Values.Look(ref Settings.RansomPenaltyMajor, "RansomPenaltyMajor", -15);
            Scribe_Values.Look(ref Settings.RansomPenaltySevere, "RansomPenaltySevere", -25);
            Scribe_Values.Look(ref Settings.RansomPenaltyTimeout, "RansomPenaltyTimeout", -35);
            Scribe_Values.Look(ref Settings.ItemAirdropMinBudgetSilver, "ItemAirdropMinBudgetSilver", 200);
            Scribe_Values.Look(ref Settings.ItemAirdropMaxBudgetSilver, "ItemAirdropMaxBudgetSilver", 50000);
            Scribe_Values.Look(ref Settings.ItemAirdropDefaultAIBudgetSilver, "ItemAirdropDefaultAIBudgetSilver", 2000);
            Scribe_Values.Look(ref Settings.ItemAirdropRansomBudgetPercent, "ItemAirdropRansomBudgetPercent", 0.01f);
            Scribe_Values.Look(ref Settings.ItemAirdropMaxStacksPerDrop, "ItemAirdropMaxStacksPerDrop", 8);
            Scribe_Values.Look(ref Settings.ItemAirdropMaxTotalItemsPerDrop, "ItemAirdropMaxTotalItemsPerDrop", 200);
            Scribe_Values.Look(ref Settings.ItemAirdropBlacklistDefNamesCsv, "ItemAirdropBlacklistDefNamesCsv", "VanometricPowerCell,PersonaCore,ArchotechArm,ArchotechLeg");
            Scribe_Values.Look(ref Settings.FactionExclusionDefNamesCsv, "FactionExclusionDefNamesCsv", "CASacrilegHunters");
            Scribe_Values.Look(ref Settings.ItemAirdropSelectionCandidateLimit, "ItemAirdropSelectionCandidateLimit", 30);
            Scribe_Values.Look(ref Settings.ItemAirdropSecondPassTimeoutSeconds, "ItemAirdropSecondPassTimeoutSeconds", 25);
            Scribe_Values.Look(ref Settings.ItemAirdropSecondPassQueueTimeoutSeconds, "ItemAirdropSecondPassQueueTimeoutSeconds", 15);
            Scribe_Values.Look(ref Settings.ItemAirdropBlockedCategoriesCsv, "ItemAirdropBlockedCategoriesCsv", "");
            Scribe_Values.Look(ref Settings.EnableAirdropAliasExpansion, "EnableAirdropAliasExpansion", true);
            Scribe_Values.Look(ref Settings.ItemAirdropAliasExpansionMaxCount, "ItemAirdropAliasExpansionMaxCount", 8);
            Scribe_Values.Look(ref Settings.ItemAirdropAliasExpansionTimeoutSeconds, "ItemAirdropAliasExpansionTimeoutSeconds", 4);
            Scribe_Values.Look(ref Settings.EnableAirdropSameFamilyRelaxedRetry, "EnableAirdropSameFamilyRelaxedRetry", true);
            Scribe_Values.Look(ref Settings.ItemAirdropCooldownTicks, "ItemAirdropCooldownTicks", 180000);
            Scribe_Values.Look(ref Settings.ItemAirdropUntradeablePriceMultiplier, "ItemAirdropUntradeablePriceMultiplier", 6.0f);
            Scribe_Values.Look(ref Settings.ItemAirdropUntradeableLowValuePriceMultiplier, "ItemAirdropUntradeableLowValuePriceMultiplier", 15.0f);
            Scribe_Values.Look(ref Settings.ItemAirdropUntradeableMidValuePriceMultiplier, "ItemAirdropUntradeableMidValuePriceMultiplier", 8.0f);
            Scribe_Values.Look(ref Settings.ItemAirdropNeedPriceMultiplier, "ItemAirdropNeedPriceMultiplier", 1.6f);
            Scribe_Values.Look(ref Settings.ItemAirdropExoticMiscNeedPriceMultiplier, "ItemAirdropExoticMiscNeedPriceMultiplier", 5.0f);
            Scribe_Values.Look(ref Settings.ItemAirdropOfferPriceMultiplier, "ItemAirdropOfferPriceMultiplier", 0.6f);
            Scribe_Values.Look(ref Settings.ItemAirdropExoticMiscOfferPriceMultiplier, "ItemAirdropExoticMiscOfferPriceMultiplier", 0.9f);
            Scribe_Values.Look(ref Settings.ItemAirdropUntradeableOfferPriceMultiplier, "ItemAirdropUntradeableOfferPriceMultiplier", 1.0f);
            Scribe_Values.Look(ref Settings.ItemAirdropSpecialItemDiscountMultiplier, "ItemAirdropSpecialItemDiscountMultiplier", 0.4f);
            Scribe_Values.Look(ref Settings.ItemAirdropSpecialItemScarceMultiplier, "ItemAirdropSpecialItemScarceMultiplier", 2.0f);
            Scribe_Values.Look(ref Settings.ItemAirdropTradeLimitMultiplier, "ItemAirdropTradeLimitMultiplier", 2.0f);
            Scribe_Values.Look(ref Settings.ItemAirdropCooldownMultiplier, "ItemAirdropCooldownMultiplier", 1.0f);

            // Raid Granular Settings
            Scribe_Values.Look(ref Settings.EnableRaidStrategy_ImmediateAttack, "EnableRaidStrategy_ImmediateAttack", true);
            Scribe_Values.Look(ref Settings.EnableRaidStrategy_ImmediateAttackSmart, "EnableRaidStrategy_ImmediateAttackSmart", true);
            Scribe_Values.Look(ref Settings.EnableRaidStrategy_StageThenAttack, "EnableRaidStrategy_StageThenAttack", true);
            Scribe_Values.Look(ref Settings.EnableRaidStrategy_ImmediateAttackSappers, "EnableRaidStrategy_ImmediateAttackSappers", true);
            Scribe_Values.Look(ref Settings.EnableRaidStrategy_Siege, "EnableRaidStrategy_Siege", true);

            Scribe_Values.Look(ref Settings.EnableRaidArrival_EdgeWalkIn, "EnableRaidArrival_EdgeWalkIn", true);
            Scribe_Values.Look(ref Settings.EnableRaidArrival_EdgeDrop, "EnableRaidArrival_EdgeDrop", true);
            Scribe_Values.Look(ref Settings.EnableRaidArrival_EdgeWalkInGroups, "EnableRaidArrival_EdgeWalkInGroups", true);
            Scribe_Values.Look(ref Settings.EnableRaidArrival_RandomDrop, "EnableRaidArrival_RandomDrop", false);
            Scribe_Values.Look(ref Settings.EnableRaidArrival_CenterDrop, "EnableRaidArrival_CenterDrop", false);
            Scribe_Values.Look(ref Settings.RaidPointsMultiplier, "RaidPointsMultiplier", 1f);
            Scribe_Values.Look(ref Settings.MinRaidPoints, "MinRaidPoints", 35f);
            Scribe_Collections.Look(ref Settings.RaidPointsFactionOverrides, "RaidPointsFactionOverrides", LookMode.Deep);

            Scribe_Values.Look(ref Settings.EnableAPICallLogging, "EnableAPICallLogging", true);
            Scribe_Values.Look(ref Settings.MaxAPICallsPerHour, "MaxAPICallsPerHour", 0);

            Scribe_Values.Look(ref Settings.EnableFactionPresenceStatus, "EnableFactionPresenceStatus", true);
            Scribe_Values.Look(ref Settings.PresenceCacheHours, "PresenceCacheHours", 2f);
            Scribe_Values.Look(ref Settings.PresenceForcedOfflineHours, "PresenceForcedOfflineHours", 24f);
            Scribe_Values.Look(ref Settings.PresenceNightBiasEnabled, "PresenceNightBiasEnabled", true);
            Scribe_Values.Look(ref Settings.PresenceNightStartHour, "PresenceNightStartHour", 22);
            Scribe_Values.Look(ref Settings.PresenceNightEndHour, "PresenceNightEndHour", 6);
            Scribe_Values.Look(ref Settings.PresenceNightOfflineBias, "PresenceNightOfflineBias", 0.65f);
            Scribe_Values.Look(ref Settings.PresenceUseAdvancedProfiles, "PresenceUseAdvancedProfiles", true);
            Scribe_Values.Look(ref Settings.PresenceOnlineStart_Default, "PresenceOnlineStart_Default", 7);
            Scribe_Values.Look(ref Settings.PresenceOnlineDuration_Default, "PresenceOnlineDuration_Default", 12);
            Scribe_Values.Look(ref Settings.PresenceOnlineStart_Neolithic, "PresenceOnlineStart_Neolithic", 8);
            Scribe_Values.Look(ref Settings.PresenceOnlineDuration_Neolithic, "PresenceOnlineDuration_Neolithic", 8);
            Scribe_Values.Look(ref Settings.PresenceOnlineStart_Medieval, "PresenceOnlineStart_Medieval", 8);
            Scribe_Values.Look(ref Settings.PresenceOnlineDuration_Medieval, "PresenceOnlineDuration_Medieval", 10);
            Scribe_Values.Look(ref Settings.PresenceOnlineStart_Industrial, "PresenceOnlineStart_Industrial", 7);
            Scribe_Values.Look(ref Settings.PresenceOnlineDuration_Industrial, "PresenceOnlineDuration_Industrial", 14);
            Scribe_Values.Look(ref Settings.PresenceOnlineStart_Spacer, "PresenceOnlineStart_Spacer", 6);
            Scribe_Values.Look(ref Settings.PresenceOnlineDuration_Spacer, "PresenceOnlineDuration_Spacer", 18);
            Scribe_Values.Look(ref Settings.PresenceOnlineStart_Ultra, "PresenceOnlineStart_Ultra", 4);
            Scribe_Values.Look(ref Settings.PresenceOnlineDuration_Ultra, "PresenceOnlineDuration_Ultra", 20);
            Scribe_Values.Look(ref Settings.PresenceOnlineStart_Archotech, "PresenceOnlineStart_Archotech", 4);
            Scribe_Values.Look(ref Settings.PresenceOnlineDuration_Archotech, "PresenceOnlineDuration_Archotech", 20);

            Scribe_Values.Look(ref Settings.EnableSocialCircle, "EnableSocialCircle", true);
            Scribe_Values.Look(ref Settings.ScheduledNewsFrequencyLevel, "ScheduledNewsFrequencyLevel", global::Ustas.RimAI.Communication.Relations.Config.ScheduledNewsFrequencyLevel.High);
            Scribe_Values.Look(ref Settings.SocialPostIntervalMinDays, "SocialPostIntervalMinDays", 5);
            Scribe_Values.Look(ref Settings.SocialPostIntervalMaxDays, "SocialPostIntervalMaxDays", 7);
            Scribe_Values.Look(ref Settings.EnablePlayerInfluenceNews, "EnablePlayerInfluenceNews", true);
            Scribe_Values.Look(ref Settings.EnableAISimulationNews, "EnableAISimulationNews", true);
            Scribe_Values.Look(ref Settings.EnableSocialCircleAutoActions, "EnableSocialCircleAutoActions", false);

            Scribe_Values.Look(ref Settings.EnableNpcInitiatedDialogue, "EnableNpcInitiatedDialogue", true);
            Scribe_Values.Look(ref Settings.EnablePawnRpgInitiatedDialogue, "EnablePawnRpgInitiatedDialogue", true);
            Scribe_Values.Look(
                ref Settings.NpcPushFrequencyMode,
                "NpcPushFrequencyMode",
                global::Ustas.RimAI.Communication.Relations.Config.NpcPushFrequencyMode.Low);
            Scribe_Values.Look(ref Settings.NpcQueueMaxPerFaction, "NpcQueueMaxPerFaction", 3);
            Scribe_Values.Look(ref Settings.NpcQueueExpireHours, "NpcQueueExpireHours", 12f);
            Scribe_Values.Look(ref Settings.NpcGlobalDeliveryCooldownHours, "NpcGlobalDeliveryCooldownHours", 6f);
            Scribe_Values.Look(ref Settings.NpcGlobalMaxMessagesPerWindow, "NpcGlobalMaxMessagesPerWindow", 1);
            Scribe_Values.Look(ref Settings.NpcGlobalWindowHours, "NpcGlobalWindowHours", 12f);
            Scribe_Values.Look(ref Settings.NpcFactionCooldownMinDays, "NpcFactionCooldownMinDays", 3);
            Scribe_Values.Look(ref Settings.NpcFactionCooldownMaxDays, "NpcFactionCooldownMaxDays", 7);
            Scribe_Values.Look(ref Settings.EnableBusyByDrafted, "EnableBusyByDrafted", true);
            Scribe_Values.Look(ref Settings.EnableBusyByHostiles, "EnableBusyByHostiles", true);
            Scribe_Values.Look(ref Settings.EnableBusyByClickRate, "EnableBusyByClickRate", true);
            Scribe_Values.Look(ref Settings.EnableNpcPushThrottleDebugLog, "EnableNpcPushThrottleDebugLog", false);
            Scribe_Values.Look(ref Settings.NpcPushThrottleProfileVersion, "NpcPushThrottleProfileVersion", 1);
            Scribe_Values.Look(ref Settings.PawnRpgProtagonistCap, "PawnRpgProtagonistCap", 20);
            Scribe_Values.Look(ref Settings.EnableColonistToColonistDialogue, "EnableColonistToColonistDialogue", true);
            Scribe_Values.Look(ref Settings.ColonistPairMinOpinion, "ColonistPairMinOpinion", 10);
            Scribe_Values.Look(ref Settings.ColonistPairFrequencyMode, "ColonistPairFrequencyMode", NpcPushFrequencyMode.Low);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                XmlNode currentNode = Scribe.loader?.curXmlParent;
                if (currentNode != null && currentNode["EnablePawnRpgInitiatedDialogue"] == null)
                {
                    Settings.EnablePawnRpgInitiatedDialogue = Settings.EnableNpcInitiatedDialogue;
                }

                if (currentNode != null && currentNode["ScheduledNewsFrequencyLevel"] == null)
                {
                    Settings.ScheduledNewsFrequencyLevel = InferFrequencyLevelFromLegacyRange(
                        Settings.SocialPostIntervalMinDays,
                        Settings.SocialPostIntervalMaxDays);
                }

                if (Settings.NpcPushThrottleProfileVersion < 1)
                {
                    Settings.NpcQueueMaxPerFaction = 3;
                    Settings.NpcQueueExpireHours = 12f;
                    Settings.NpcGlobalDeliveryCooldownHours = 6f;
                    Settings.NpcFactionCooldownMinDays = 3;
                    Settings.NpcFactionCooldownMaxDays = 7;
                    Settings.EnableNpcPushThrottleDebugLog = false;
                    Settings.NpcPushThrottleProfileVersion = 1;
                }
            }

            Settings.MaxAPICallsPerHour = Mathf.Max(0, Settings.MaxAPICallsPerHour);
            Settings.ItemAirdropMinBudgetSilver = Mathf.Max(1, Settings.ItemAirdropMinBudgetSilver);
            Settings.ItemAirdropMaxBudgetSilver = Mathf.Max(Settings.ItemAirdropMinBudgetSilver, Settings.ItemAirdropMaxBudgetSilver);
            Settings.ItemAirdropDefaultAIBudgetSilver = Mathf.Clamp(Settings.ItemAirdropDefaultAIBudgetSilver, Settings.ItemAirdropMinBudgetSilver, Settings.ItemAirdropMaxBudgetSilver);
            Settings.ItemAirdropRansomBudgetPercent = Mathf.Clamp(Settings.ItemAirdropRansomBudgetPercent, 0.001f, 0.20f);
            Settings.ItemAirdropMaxStacksPerDrop = Mathf.Clamp(Settings.ItemAirdropMaxStacksPerDrop, 1, 100);
            Settings.ItemAirdropMaxTotalItemsPerDrop = Mathf.Clamp(Settings.ItemAirdropMaxTotalItemsPerDrop, 1, 5000);
            Settings.ItemAirdropSelectionCandidateLimit = Mathf.Clamp(Settings.ItemAirdropSelectionCandidateLimit, 1, 100);
            Settings.ItemAirdropSecondPassTimeoutSeconds = Mathf.Clamp(Settings.ItemAirdropSecondPassTimeoutSeconds, 3, 30);
            Settings.ItemAirdropSecondPassQueueTimeoutSeconds = Mathf.Clamp(Settings.ItemAirdropSecondPassQueueTimeoutSeconds, 3, 120);
            Settings.ItemAirdropAliasExpansionMaxCount = Mathf.Clamp(Settings.ItemAirdropAliasExpansionMaxCount, 2, 12);
            Settings.ItemAirdropAliasExpansionTimeoutSeconds = Mathf.Clamp(Settings.ItemAirdropAliasExpansionTimeoutSeconds, 2, 10);
            Settings.RansomPaymentModeDefault = "silver";
            Settings.RansomReleaseTimeoutTicks = Mathf.Clamp(Settings.RansomReleaseTimeoutTicks, 2500, 600000);
            Settings.RansomValueDropMajorThreshold = Mathf.Clamp(Settings.RansomValueDropMajorThreshold, 0.01f, 0.95f);
            Settings.RansomValueDropSevereThreshold = Mathf.Clamp(Settings.RansomValueDropSevereThreshold, Settings.RansomValueDropMajorThreshold, 0.99f);
            Settings.RansomLowGoodwillDiscountThreshold = Mathf.Clamp(Settings.RansomLowGoodwillDiscountThreshold, -100, 100);
            Settings.RansomLowGoodwillDiscountFactor = Mathf.Clamp(Settings.RansomLowGoodwillDiscountFactor, 0.10f, 1f);
            Settings.RansomPenaltyMajor = -Mathf.Clamp(Mathf.Abs(Settings.RansomPenaltyMajor), 0, 100);
            Settings.RansomPenaltySevere = -Mathf.Clamp(Mathf.Abs(Settings.RansomPenaltySevere), 0, 100);
            Settings.RansomPenaltyTimeout = -Mathf.Clamp(Mathf.Abs(Settings.RansomPenaltyTimeout), 0, 100);
            Settings.NpcQueueMaxPerFaction = Mathf.Clamp(Settings.NpcQueueMaxPerFaction, 1, 10);
            Settings.NpcQueueExpireHours = Mathf.Clamp(Settings.NpcQueueExpireHours, 1f, 48f);
            Settings.NpcGlobalDeliveryCooldownHours = Mathf.Clamp(Settings.NpcGlobalDeliveryCooldownHours, 1f, 24f);
            Settings.NpcGlobalMaxMessagesPerWindow = Mathf.Clamp(Settings.NpcGlobalMaxMessagesPerWindow, 1, 10);
            Settings.NpcGlobalWindowHours = Mathf.Clamp(Settings.NpcGlobalWindowHours, 6f, 72f);
            Settings.NpcFactionCooldownMinDays = Mathf.Clamp(Settings.NpcFactionCooldownMinDays, 1, 30);
            Settings.NpcFactionCooldownMaxDays = Mathf.Clamp(Settings.NpcFactionCooldownMaxDays, Settings.NpcFactionCooldownMinDays, 30);
            Settings.PawnRpgProtagonistCap = Mathf.Clamp(Settings.PawnRpgProtagonistCap, 1, 100);
            Settings.DialogueActionGoodwillCostMultiplier = Mathf.Clamp(Settings.DialogueActionGoodwillCostMultiplier, 0f, 1f);
            if (Settings.FactionExclusionDefNamesCsv == null) Settings.FactionExclusionDefNamesCsv = "CASacrilegHunters";
            RelationsSettingsPages.For(Settings).Gameplay.NormalizeRaidPointSettings();
        }

        internal static global::Ustas.RimAI.Communication.Relations.Config.ScheduledNewsFrequencyLevel InferFrequencyLevelFromLegacyRange(int minDays, int maxDays)
        {
            int min = Mathf.Max(1, minDays);
            int max = Mathf.Max(min, maxDays);
            if (max <= 1)
            {
                return global::Ustas.RimAI.Communication.Relations.Config.ScheduledNewsFrequencyLevel.High;
            }

            if (min <= 1 && max <= 2)
            {
                return global::Ustas.RimAI.Communication.Relations.Config.ScheduledNewsFrequencyLevel.Medium;
            }

            return global::Ustas.RimAI.Communication.Relations.Config.ScheduledNewsFrequencyLevel.Low;
        }
}
