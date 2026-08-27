using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using Ustas.RimAI.Communication.Relations.AI;
using System.Xml;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.UI;
using Ustas.RimAI.Core.UI;

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class RelationsGameplaySettingsPage
{
    readonly RelationsGameplaySettingsSections Sections;
    internal readonly RelationsSettingsPages Pages;

    internal RelationsGameplaySettingsPage(RelationsSettingsPages pages)
    {
        Pages = pages;
        Sections = new RelationsGameplaySettingsSections(this);
    }

    internal RelationsSettings Settings => Pages.Settings;

    internal void Draw(Rect rect, SettingsSearchState search)
    {
        DrawTab_AIControl(rect);
    }

    internal void ResetAllSections()
    {
        ResetGameplaySectionsToDefault();
    }

        #region 闂佺懓鍚嬪娆戞崲閹版澘鍨傛い蹇撶墕缁€?- AI 闂備胶顢婇惌鍥礃閵娧冨箑闂佽崵濮崇粈浣规櫠娴犲鍋?


        #endregion

        #region UI缂傚倸鍊烽悞锕傛晪闂?- AI闂備胶顢婇惌鍥礃閵娧冨箑闂傚倷绶￠崑鍕囬悽绋课ョ€广儱顦涵鈧?
        internal Vector2 aiSettingsScrollPosition = Vector2.zero;
        internal string raidOverrideSelectedFactionDefName = string.Empty;
        internal float raidOverrideSelectedMultiplier = 1f;
        internal float raidOverrideSelectedMinPoints = 35f;
        internal AIControlSection expandedAIControlSection = AIControlSection.UISettings;

        internal void DrawTab_AIControl(Rect rect)
        {
            float viewHeight = CalculateAIControlContentHeight(rect.width - 16f);
            Rect viewRect = new Rect(0, 0, rect.width - 16f, viewHeight);
            
            Widgets.BeginScrollView(rect, ref aiSettingsScrollPosition, viewRect);
            
            Listing_Standard listing = new Listing_Standard();
            // Verse wraps a Listing into a second column, off the visible view, as soon as
            // content passes the rect height, and CurHeight then reports that new column.
            // A scrolling settings page never wants that; see validate_scrollable_listings.
            listing.maxOneColumn = true;
            listing.Begin(new Rect(0, 0, viewRect.width, viewRect.height));
            Pages.GameplayUx.DrawAIControlHeaderBar(listing);

            DrawAccordionSection(listing, AIControlSection.UISettings, "RimChat_UISettings".Translate(), ResetUISettingsToDefault, DrawUISettings, new Color(0.9f, 0.9f, 1f));
            DrawAccordionSection(listing, AIControlSection.PresenceSettings, "RimChat_PresenceSettings".Translate(), ResetPresenceSettingsToDefault, DrawPresenceSettings, new Color(0.85f, 1f, 0.85f));
            DrawAccordionSection(listing, AIControlSection.NpcPushSettings, "RimChat_NpcPushSettings".Translate(), Pages.NpcPush.ResetNpcInitiatedDialogueSettings, Pages.NpcPush.DrawNpcInitiatedDialogueSettings, new Color(0.85f, 0.9f, 1f));
            DrawAccordionSection(listing, AIControlSection.RpgDialogueSettings, "RimChat_RpgDialogueSettingsModOptions".Translate(), Pages.RpgDialogue.ResetRpgNonPromptSettingsToDefault, Pages.RpgDialogue.DrawRpgNonPromptSettings, new Color(0.95f, 0.85f, 1f));
            DrawAccordionSection(listing, AIControlSection.RaidSettings, "RimChat_RaidSettings".Translate(), ResetRaidSettingsToDefault, Pages.GameplayActions.DrawRaidSettings, new Color(1f, 0.6f, 0.6f));
            DrawAccordionSection(listing, AIControlSection.GoodwillSettings, "RimChat_GoodwillSettings".Translate(), ResetGoodwillSettingsToDefault, Pages.GameplayActions.DrawGoodwillSettings, new Color(0.8f, 0.9f, 1f));
            DrawAccordionSection(listing, AIControlSection.AidRequestSettings, "RimChat_AidRequestSettings".Translate(), ResetAidRequestSettingsToDefault, Pages.GameplayActions.DrawAidRequestSettings, new Color(0.7f, 1f, 0.8f));
            DrawAccordionSection(listing, AIControlSection.AirdropTradeSettings, "RimChat_AirdropTradeSettings".Translate(), ResetAirdropTradeSettingsToDefault, Pages.GameplayActions.DrawAirdropTradeSettings, new Color(0.6f, 0.95f, 0.8f));
            DrawAccordionSection(listing, AIControlSection.PrisonerRansomSettings, "RimChat_PrisonerRansomSettings".Translate(), ResetPrisonerRansomSettingsToDefault, Pages.GameplayActions.DrawPrisonerRansomSettings, new Color(0.85f, 0.75f, 0.9f));
            DrawAccordionSection(listing, AIControlSection.WarPeaceSettings, "RimChat_WarPeaceSettings".Translate(), ResetWarPeaceSettingsToDefault, Pages.GameplayActions.DrawWarPeaceSettings, new Color(1f, 0.7f, 0.7f));
            DrawAccordionSection(listing, AIControlSection.CaravanSettings, "RimChat_CaravanSettings".Translate(), ResetCaravanSettingsToDefault, Pages.GameplayActions.DrawCaravanSettings, new Color(0.9f, 0.8f, 1f));
            DrawAccordionSection(listing, AIControlSection.QuestSettings, "RimChat_QuestSettings".Translate(), ResetQuestSettingsToDefault, Pages.GameplayActions.DrawQuestSettings, new Color(0.8f, 0.8f, 1f));
            DrawAccordionSection(listing, AIControlSection.SocialCircleSettings, "RimChat_SocialCircleSettings".Translate(), Pages.SocialCircle.ResetSocialCircleSettingsToDefault, Pages.SocialCircle.DrawSocialCircleSettings, new Color(0.8f, 1f, 0.95f));
            DrawAccordionSection(listing, AIControlSection.SecuritySettings, "RimChat_SecuritySettings".Translate(), ResetSecuritySettingsToDefault, Pages.GameplayActions.DrawSecuritySettings, new Color(1f, 0.9f, 0.5f));
            DrawAccordionSection(listing, AIControlSection.ModCompatSettings, "RimChat_ModCompatSettings".Translate(), ResetModCompatSettingsToDefault, DrawModCompatSettings, new Color(0.8f, 1f, 0.9f));

            listing.End();
            Widgets.EndScrollView();
        }

        internal float CalculateAIControlContentHeight(float width)
        {
            float headerHeight = 34f * 14f + 120f;
            float expandedContentHeight = GetExpandedSectionBodyHeight();
            float viewHeight = headerHeight + expandedContentHeight + 40f;
            float minHeight = Mathf.Max(260f, width * 0.6f);
            return Mathf.Max(viewHeight, minHeight);
        }

        internal float GetExpandedSectionBodyHeight()
        {
            return expandedAIControlSection switch
            {
                AIControlSection.None => 0f,
                AIControlSection.UISettings => 280f,
                AIControlSection.PresenceSettings => 860f,
                AIControlSection.NpcPushSettings => 620f,
                AIControlSection.RpgDialogueSettings => 460f,
                AIControlSection.RaidSettings => 860f,
                AIControlSection.GoodwillSettings => 320f,
                AIControlSection.GiftSettings => 100f,
                AIControlSection.AidRequestSettings => 300f,
                AIControlSection.AirdropTradeSettings => 1000f,
                AIControlSection.PrisonerRansomSettings => 450f,
                AIControlSection.WarPeaceSettings => 400f,
                AIControlSection.CaravanSettings => 280f,
                AIControlSection.QuestSettings => 260f,
                AIControlSection.SocialCircleSettings => 420f,
                AIControlSection.SecuritySettings => 270f,
                AIControlSection.ModCompatSettings => 300f,
                _ => 360f
            };
        }

        internal void ToggleAIControlSection(AIControlSection section)
        {
            expandedAIControlSection = expandedAIControlSection == section
                ? AIControlSection.None
                : section;
        }

        internal void DrawAccordionSection(
            Listing_Standard listing,
            AIControlSection section,
            string title,
            System.Action resetAction,
            System.Action<Listing_Standard> drawContent,
            Color? titleColor = null)
        {
            Sections.DrawAccordionSection(listing, section, title, resetAction, drawContent, titleColor);
        }


        internal void DrawUISettings(Listing_Standard listing)
        {
            Sections.DrawUISettings(listing);
        }


        internal void DrawPresenceSettings(Listing_Standard listing)
        {
            Sections.DrawPresenceSettings(listing);
        }


        internal void DrawPresenceProfileSliders(Listing_Standard listing, string profileLabel, ref int startHour, ref int durationHours)
        {
            Sections.DrawPresenceProfileSliders(listing, profileLabel, ref startHour, ref durationHours);
        }


        internal void DrawSpeedOption(Rect rect, string label, bool isActive, System.Action onClick)
        {
            Sections.DrawSpeedOption(rect, label, isActive, onClick);
        }


        internal void DrawAIBehaviorToggles(Listing_Standard listing)
        {
            Sections.DrawAIBehaviorToggles(listing);
        }


        internal void NormalizeRaidPointSettings()
        {
            Sections.NormalizeRaidPointSettings();
        }


        internal void DrawSectionHeader(Listing_Standard listing, string title, System.Action resetAction, Color? titleColor = null)
        {
            Sections.DrawSectionHeader(listing, title, resetAction, titleColor);
        }


        internal void ShowResetConfirmationDialog(string sectionName, System.Action resetAction)
        {
            Dialog_MessageBox dialog = Dialog_MessageBox.CreateConfirmation(
                "RimChat_ResetSectionConfirm".Translate(sectionName),
                () =>
                {
                    resetAction?.Invoke();
                    SoundDefOf.Tick_High.PlayOneShotOnCamera(null);
                },
                true,
                "RimChat_ResetConfirmTitle".Translate()
            );
            Find.WindowStack.Add(dialog);
        }

        #region 闂備礁鎲￠懝鍓р偓姘煎墴瀹曡鎯旈妸銉ь槺闂佺粯鍨剁湁闁告帗甯掗…璺ㄦ崉閾忓墣褏绱掗鍛仯闁瑰嘲顑夋俊鍫曞幢濡厧骞嶆繝?
        internal void ResetAIBehaviorToDefault()
        {
            Settings.EnableAIGoodwillAdjustment = true;
            Settings.EnableAIGiftSending = true;
            Settings.EnableAIWarDeclaration = true;
            Settings.EnableAIPeaceMaking = true;
            Settings.EnableAITradeCaravan = true;
            Settings.EnableAIAidRequest = true;
            Settings.EnableAIRaidRequest = true;
            Settings.EnableAIItemAirdrop = true;
            Settings.EnablePrisonerRansom = true;
            Settings.DialogueStyleMode = DialogueStyleMode.NaturalConcise;
            Settings.ExpectedActionDenyLogLevel = ExpectedActionDenyLogLevel.Info;
        }

        internal void ResetRaidSettingsToDefault()
        {
            Settings.EnableRaidStrategy_ImmediateAttack = true;
            Settings.EnableRaidStrategy_ImmediateAttackSmart = true;
            Settings.EnableRaidStrategy_StageThenAttack = true;
            Settings.EnableRaidStrategy_ImmediateAttackSappers = true;
            Settings.EnableRaidStrategy_Siege = true;

            Settings.EnableRaidArrival_EdgeWalkIn = true;
            Settings.EnableRaidArrival_EdgeDrop = true;
            Settings.EnableRaidArrival_EdgeWalkInGroups = true;
            Settings.EnableRaidArrival_RandomDrop = false;
            Settings.EnableRaidArrival_CenterDrop = false;

            Settings.RaidPointsMultiplier = 1f;
            Settings.MinRaidPoints = 35f;
            Settings.RaidPointsFactionOverrides?.Clear();
            raidOverrideSelectedFactionDefName = string.Empty;
            raidOverrideSelectedMultiplier = 1f;
            raidOverrideSelectedMinPoints = 35f;
        }

        internal void ResetGoodwillSettingsToDefault()
        {
            Settings.DialogueActionGoodwillCostMultiplier = 0.5f;
            Settings.MaxGoodwillAdjustmentPerCall = 15;
            Settings.MaxDailyGoodwillAdjustment = 30;
            Settings.GoodwillCooldownTicks = 2500;
        }

        internal void ResetGiftSettingsToDefault()
        {
            Settings.MaxGiftSilverAmount = 1000;
            Settings.MaxGiftGoodwillGain = 10;
            Settings.GiftCooldownTicks = 60000;
        }

        internal void ResetAidRequestSettingsToDefault()
        {
            Settings.MinGoodwillForAid = 40;
            Settings.AidCooldownTicks = 120000;
            Settings.AidDelayBaseTicks = 90000;
        }

        internal void ResetAirdropTradeSettingsToDefault()
        {
            Settings.ItemAirdropMinBudgetSilver = 200;
            Settings.ItemAirdropMaxBudgetSilver = 50000;
            Settings.ItemAirdropDefaultAIBudgetSilver = 2000;
            Settings.ItemAirdropRansomBudgetPercent = 0.01f;
            Settings.ItemAirdropMaxStacksPerDrop = 8;
            Settings.ItemAirdropMaxTotalItemsPerDrop = 200;
            Settings.ItemAirdropBlacklistDefNamesCsv = "VanometricPowerCell,PersonaCore,ArchotechArm,ArchotechLeg";
            Settings.ItemAirdropSelectionCandidateLimit = 30;
            Settings.ItemAirdropSecondPassTimeoutSeconds = 25;
            Settings.ItemAirdropSecondPassQueueTimeoutSeconds = 15;
            Settings.ItemAirdropBlockedCategoriesCsv = "";
            Settings.EnableAirdropAliasExpansion = true;
            Settings.ItemAirdropAliasExpansionMaxCount = 8;
            Settings.ItemAirdropAliasExpansionTimeoutSeconds = 4;
            Settings.EnableAirdropSameFamilyRelaxedRetry = true;
            Settings.ItemAirdropCooldownTicks = 180000;
            Settings.ItemAirdropUntradeablePriceMultiplier = 6.0f;
            Settings.ItemAirdropUntradeableLowValuePriceMultiplier = 15.0f;
            Settings.ItemAirdropUntradeableMidValuePriceMultiplier = 8.0f;
            Settings.ItemAirdropNeedPriceMultiplier = 1.6f;
            Settings.ItemAirdropExoticMiscNeedPriceMultiplier = 5.0f;
            Settings.ItemAirdropOfferPriceMultiplier = 0.6f;
            Settings.ItemAirdropExoticMiscOfferPriceMultiplier = 0.9f;
            Settings.ItemAirdropUntradeableOfferPriceMultiplier = 1.0f;
            Settings.ItemAirdropSpecialItemDiscountMultiplier = 0.4f;
            Settings.ItemAirdropSpecialItemScarceMultiplier = 2.0f;
            Settings.ItemAirdropTradeLimitMultiplier = 2.0f;
            Settings.ItemAirdropCooldownMultiplier = 1.0f;
        }

        internal void ResetPrisonerRansomSettingsToDefault()
        {
            Settings.RansomPaymentModeDefault = "silver";
            Settings.RansomReleaseTimeoutTicks = 30000;
            Settings.RansomValueDropMajorThreshold = 0.30f;
            Settings.RansomValueDropSevereThreshold = 0.60f;
            Settings.RansomLowGoodwillDiscountThreshold = 80;
            Settings.RansomLowGoodwillDiscountFactor = 0.8f;
            Settings.RansomPenaltyMajor = -15;
            Settings.RansomPenaltySevere = -25;
            Settings.RansomPenaltyTimeout = -35;
        }

        internal void ResetWarPeaceSettingsToDefault()
        {
            Settings.MaxGoodwillForWarDeclaration = -50;
            Settings.WarCooldownTicks = 60000;
            Settings.MaxPeaceCost = 5000;
            Settings.PeaceGoodwillReset = -20;
            Settings.PeaceCooldownTicks = 60000;
        }

        internal void ResetCaravanSettingsToDefault()
        {
            Settings.CaravanCooldownTicks = 90000;
            Settings.CaravanDelayBaseTicks = 135000;
        }

        internal void ResetQuestSettingsToDefault()
        {
            Settings.MinQuestCooldownDays = 7;
            Settings.MaxQuestCooldownDays = 12;
        }

        internal void ResetSecuritySettingsToDefault()
        {
            Settings.EnableAPICallLogging = true;
            Settings.MaxAPICallsPerHour = 0;
        }

        internal void ResetUISettingsToDefault()
        {
            Settings.TypewriterSpeedMode = TypewriterSpeedMode.Standard;
            Settings.ReplaceCommsConsole = false;
            Settings.DialogueStyleMode = DialogueStyleMode.NaturalConcise;
        }

        internal void ResetPresenceSettingsToDefault()
        {
            Settings.EnableFactionPresenceStatus = true;
            Settings.PresenceCacheHours = 2f;
            Settings.PresenceForcedOfflineHours = 24f;
            Settings.PresenceNightBiasEnabled = true;
            Settings.PresenceNightStartHour = 22;
            Settings.PresenceNightEndHour = 6;
            Settings.PresenceNightOfflineBias = 0.65f;
            Settings.PresenceUseAdvancedProfiles = true;
            Settings.PresenceOnlineStart_Default = 7;
            Settings.PresenceOnlineDuration_Default = 12;
            Settings.PresenceOnlineStart_Neolithic = 8;
            Settings.PresenceOnlineDuration_Neolithic = 8;
            Settings.PresenceOnlineStart_Medieval = 8;
            Settings.PresenceOnlineDuration_Medieval = 10;
            Settings.PresenceOnlineStart_Industrial = 7;
            Settings.PresenceOnlineDuration_Industrial = 14;
            Settings.PresenceOnlineStart_Spacer = 6;
            Settings.PresenceOnlineDuration_Spacer = 18;
            Settings.PresenceOnlineStart_Ultra = 4;
            Settings.PresenceOnlineDuration_Ultra = 20;
            Settings.PresenceOnlineStart_Archotech = 4;
            Settings.PresenceOnlineDuration_Archotech = 20;
        }

        internal void ResetGameplaySectionsToDefault()
        {
            ResetAILimitsToDefault();
        }

        internal void ResetAILimitsToDefault()
        {
            ResetGoodwillSettingsToDefault();
            ResetGiftSettingsToDefault();
            ResetAidRequestSettingsToDefault();
            ResetAirdropTradeSettingsToDefault();
            ResetPrisonerRansomSettingsToDefault();
            ResetWarPeaceSettingsToDefault();
            ResetCaravanSettingsToDefault();
            ResetQuestSettingsToDefault();
            Pages.SocialCircle.ResetSocialCircleSettingsToDefault();
            ResetSecuritySettingsToDefault();
            ResetPresenceSettingsToDefault();
            Pages.NpcPush.ResetNpcInitiatedDialogueSettings();
            ResetModCompatSettingsToDefault();
        }

        #endregion

        #region Mod Compatibility Settings

        internal void DrawModCompatSettings(Listing_Standard listing)
        {
            Sections.DrawModCompatSettings(listing);
        }


        internal void ResetModCompatSettingsToDefault()
        {
            Settings.ExpandMemoryCompatMode = "auto";
            Settings.ExpandMemoryInjectPawnMemory = true;
            Settings.ExpandMemoryPawnMemoryMaxChars = 1200;
            Settings.ExpandMemoryPawnMemoryMaxEntries = 50;
            Settings.FactionExclusionDefNamesCsv = "CASacrilegHunters";
        }

        #endregion

        #endregion
    
}
