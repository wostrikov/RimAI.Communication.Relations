using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using RimWorld;
using UnityEngine;
using UnityEngine.Networking;
using Verse;
using Verse.Sound;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Context;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.Prompting.Transfer;
using Ustas.RimAI.Core.Player2;
using Ustas.RimAI.Core.UI;

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class RelationsGameplayActionSettings
{
    internal readonly RelationsGameplaySettingsPage Owner;

    internal RelationsGameplayActionSettings(RelationsGameplaySettingsPage owner)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    internal RelationsSettingsPages Pages => Owner.Pages;
    internal RelationsSettings Settings => Owner.Settings;

        /// <summary>/// 闂佽崵鍋為崙褰掑储婵傜鍚规い鏃傚亾婵ジ鏌涢幘妤€鎳忛悗? ///</summary>
        internal void DrawRaidSettings(Listing_Standard listing)
        {
            listing.CheckboxLabeled("RimChat_EnableRaidStrategy_ImmediateAttack".Translate(), ref Settings.EnableRaidStrategy_ImmediateAttack);
            listing.CheckboxLabeled("RimChat_EnableRaidStrategy_ImmediateAttackSmart".Translate(), ref Settings.EnableRaidStrategy_ImmediateAttackSmart);
            listing.CheckboxLabeled("RimChat_EnableRaidStrategy_StageThenAttack".Translate(), ref Settings.EnableRaidStrategy_StageThenAttack);
            listing.CheckboxLabeled("RimChat_EnableRaidStrategy_ImmediateAttackSappers".Translate(), ref Settings.EnableRaidStrategy_ImmediateAttackSappers);
            listing.CheckboxLabeled("RimChat_EnableRaidStrategy_Siege".Translate(), ref Settings.EnableRaidStrategy_Siege);

            // 闂備礁鎲＄敮妤佸垔娴犲绠垫い蹇撶墕濡﹢鏌ｉ悢绋款棆缁绢厸鍋?
            listing.CheckboxLabeled("RimChat_EnableRaidArrival_EdgeWalkIn".Translate(), ref Settings.EnableRaidArrival_EdgeWalkIn);
            listing.CheckboxLabeled("RimChat_EnableRaidArrival_EdgeDrop".Translate(), ref Settings.EnableRaidArrival_EdgeDrop);
            listing.CheckboxLabeled("RimChat_EnableRaidArrival_EdgeWalkInGroups".Translate(), ref Settings.EnableRaidArrival_EdgeWalkInGroups);
            listing.CheckboxLabeled("RimChat_EnableRaidArrival_RandomDrop".Translate(), ref Settings.EnableRaidArrival_RandomDrop);
            listing.CheckboxLabeled("RimChat_EnableRaidArrival_CenterDrop".Translate(), ref Settings.EnableRaidArrival_CenterDrop);
            if (Settings.EnableRaidArrival_CenterDrop || Settings.EnableRaidArrival_RandomDrop)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = Color.yellow;
                listing.Label("RimChat_CenterDropWarning".Translate());
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }

            listing.Gap();
            listing.Label("RimChat_RaidPointTuningTitle".Translate());
            listing.Label("RimChat_GlobalRaidPointsMultiplier".Translate(Settings.RaidPointsMultiplier.ToString("F2")));
            Settings.RaidPointsMultiplier = listing.Slider(Settings.RaidPointsMultiplier, 0.1f, 5f);

            listing.Label("RimChat_GlobalMinRaidPoints".Translate(Mathf.RoundToInt(Settings.MinRaidPoints)));
            Settings.MinRaidPoints = listing.Slider(Settings.MinRaidPoints, 0f, 1000f);

            DrawRaidFactionOverrideEditor(listing);
        }

        internal void DrawRaidFactionOverrideEditor(Listing_Standard listing)
        {
            listing.Gap(4f);
            listing.Label("RimChat_RaidFactionOverridesTitle".Translate());

            string buttonLabel = "RimChat_RaidSelectFactionOverride".Translate(GetRaidOverrideSelectionLabel());
            Rect selectorRect = listing.GetRect(28f);
            if (Widgets.ButtonText(selectorRect, buttonLabel))
            {
                OpenRaidOverrideFactionMenu();
            }

            if (string.IsNullOrWhiteSpace(Owner.raidOverrideSelectedFactionDefName))
            {
                DrawRaidOverrideSummary(listing);
                return;
            }

            listing.Label("RimChat_RaidOverrideTargetFaction".Translate(Owner.raidOverrideSelectedFactionDefName));
            listing.Label("RimChat_RaidOverrideMultiplier".Translate(Owner.raidOverrideSelectedMultiplier.ToString("F2")));
            Owner.raidOverrideSelectedMultiplier = listing.Slider(Owner.raidOverrideSelectedMultiplier, 0.1f, 5f);

            listing.Label("RimChat_RaidOverrideMinPoints".Translate(Mathf.RoundToInt(Owner.raidOverrideSelectedMinPoints)));
            Owner.raidOverrideSelectedMinPoints = listing.Slider(Owner.raidOverrideSelectedMinPoints, 0f, 1000f);

            DrawRaidOverrideActionButtons(listing);
            DrawRaidOverrideSummary(listing);
        }

        internal void DrawRaidOverrideActionButtons(Listing_Standard listing)
        {
            Rect rowRect = listing.GetRect(28f);
            float halfWidth = (rowRect.width - 6f) / 2f;
            Rect applyRect = new Rect(rowRect.x, rowRect.y, halfWidth, rowRect.height);
            Rect removeRect = new Rect(rowRect.x + halfWidth + 6f, rowRect.y, halfWidth, rowRect.height);

            if (Widgets.ButtonText(applyRect, "RimChat_RaidOverrideApply".Translate()))
            {
                ApplyRaidOverrideSelection();
            }

            if (Widgets.ButtonText(removeRect, "RimChat_RaidOverrideRemove".Translate()))
            {
                RemoveRaidOverride(Owner.raidOverrideSelectedFactionDefName);
            }
        }

        internal void DrawRaidOverrideSummary(Listing_Standard listing)
        {
            if (Settings.RaidPointsFactionOverrides == null || Settings.RaidPointsFactionOverrides.Count == 0)
            {
                Text.Font = GameFont.Tiny;
                listing.Label("RimChat_RaidOverrideListEmpty".Translate());
                Text.Font = GameFont.Small;
                return;
            }

            Text.Font = GameFont.Tiny;
            foreach (RaidPointsFactionOverride entry in Settings.RaidPointsFactionOverrides.OrderBy(e => e.FactionDefName))
            {
                listing.Label("RimChat_RaidOverrideEntry".Translate(entry.FactionDefName, entry.RaidPointsMultiplier.ToString("F2"), Mathf.RoundToInt(entry.MinRaidPoints)));
            }
            Text.Font = GameFont.Small;
        }

        internal void OpenRaidOverrideFactionMenu()
        {
            List<string> factionDefs = GetRaidOverrideCandidateFactionDefs();
            if (factionDefs.Count == 0)
            {
                Messages.Message("RimChat_RaidOverrideNoFactionsFound".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            List<FloatMenuOption> options = factionDefs
                .Select(defName => new FloatMenuOption(defName, () => LoadRaidOverrideEditor(defName)))
                .ToList();
            Find.WindowStack.Add(new FloatMenu(options));
        }

        internal List<string> GetRaidOverrideCandidateFactionDefs()
        {
            HashSet<string> candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (Find.FactionManager?.AllFactions != null)
            {
                foreach (Faction faction in Find.FactionManager.AllFactions)
                {
                    string defName = faction?.def?.defName;
                    if (faction == null || faction.IsPlayer || string.IsNullOrWhiteSpace(defName)) continue;
                    candidates.Add(defName.Trim());
                }
            }

            if (Settings.RaidPointsFactionOverrides != null)
            {
                foreach (RaidPointsFactionOverride entry in Settings.RaidPointsFactionOverrides)
                {
                    if (!string.IsNullOrWhiteSpace(entry?.FactionDefName))
                    {
                        candidates.Add(entry.FactionDefName.Trim());
                    }
                }
            }

            return candidates.OrderBy(name => name).ToList();
        }

        internal void LoadRaidOverrideEditor(string factionDefName)
        {
            Owner.raidOverrideSelectedFactionDefName = factionDefName?.Trim() ?? string.Empty;
            Owner.raidOverrideSelectedMultiplier = Settings.RaidPointsMultiplier;
            Owner.raidOverrideSelectedMinPoints = Settings.MinRaidPoints;

            RaidPointsFactionOverride existing = FindRaidOverride(Owner.raidOverrideSelectedFactionDefName);
            if (existing == null) return;

            Owner.raidOverrideSelectedMultiplier = existing.RaidPointsMultiplier;
            Owner.raidOverrideSelectedMinPoints = existing.MinRaidPoints;
        }

        internal RaidPointsFactionOverride FindRaidOverride(string factionDefName)
        {
            if (string.IsNullOrWhiteSpace(factionDefName) || Settings.RaidPointsFactionOverrides == null)
            {
                return null;
            }

            return Settings.RaidPointsFactionOverrides.FirstOrDefault(entry => entry?.MatchesFactionDef(factionDefName) == true);
        }

        internal string GetRaidOverrideSelectionLabel()
        {
            return string.IsNullOrWhiteSpace(Owner.raidOverrideSelectedFactionDefName)
                ? "RimChat_RaidOverrideNoSelection".Translate().ToString()
                : Owner.raidOverrideSelectedFactionDefName;
        }

        internal void ApplyRaidOverrideSelection()
        {
            if (string.IsNullOrWhiteSpace(Owner.raidOverrideSelectedFactionDefName))
            {
                return;
            }

            if (Settings.RaidPointsFactionOverrides == null)
            {
                Settings.RaidPointsFactionOverrides = new List<RaidPointsFactionOverride>();
            }

            RaidPointsFactionOverride entry = FindRaidOverride(Owner.raidOverrideSelectedFactionDefName);
            if (entry == null)
            {
                entry = new RaidPointsFactionOverride { FactionDefName = Owner.raidOverrideSelectedFactionDefName };
                Settings.RaidPointsFactionOverrides.Add(entry);
            }

            entry.RaidPointsMultiplier = Owner.raidOverrideSelectedMultiplier;
            entry.MinRaidPoints = Owner.raidOverrideSelectedMinPoints;
            Owner.NormalizeRaidPointSettings();
        }

        internal void RemoveRaidOverride(string factionDefName)
        {
            if (string.IsNullOrWhiteSpace(factionDefName) || Settings.RaidPointsFactionOverrides == null)
            {
                return;
            }

            Settings.RaidPointsFactionOverrides.RemoveAll(entry => entry?.MatchesFactionDef(factionDefName) == true);
            if (string.Equals(Owner.raidOverrideSelectedFactionDefName, factionDefName, StringComparison.OrdinalIgnoreCase))
            {
                Owner.raidOverrideSelectedFactionDefName = string.Empty;
            }
        }

        /// <summary>/// 濠电娀娼ч崐鑺ユ叏閵堝绀夐柛娑卞枟閸庣喖鏌ㄩ弴姘冲厡婵炲牆鐖奸弻鈩冩媴娓氼垱顥撳銈嗘⒐濞叉粎妲? ///</summary>
        internal void DrawGoodwillSettings(Listing_Standard listing)
        {
            listing.Label("RimChat_DialogueActionGoodwillCostMultiplier".Translate(Settings.DialogueActionGoodwillCostMultiplier.ToString("F2")));
            Settings.DialogueActionGoodwillCostMultiplier = listing.Slider(Settings.DialogueActionGoodwillCostMultiplier, 0f, 1f);
            listing.Label("RimChat_DialogueActionGoodwillCostMultiplierHint".Translate());
            listing.Gap(6f);

            listing.Label($"RimChat_MaxGoodwillAdjustmentPerCall".Translate(Settings.MaxGoodwillAdjustmentPerCall));
            Settings.MaxGoodwillAdjustmentPerCall = (int)listing.Slider(Settings.MaxGoodwillAdjustmentPerCall, 0, 50);

            listing.Label($"RimChat_MaxDailyGoodwillAdjustment".Translate(Settings.MaxDailyGoodwillAdjustment));
            Settings.MaxDailyGoodwillAdjustment = (int)listing.Slider(Settings.MaxDailyGoodwillAdjustment, 0, 100);

            float cooldownHours = Settings.GoodwillCooldownTicks / 2500f;
            listing.Label($"RimChat_GoodwillCooldown".Translate(cooldownHours.ToString("F1")));
            cooldownHours = listing.Slider(cooldownHours, 0f, 24f);
            Settings.GoodwillCooldownTicks = (int)(cooldownHours * 2500);

            if (Settings.MaxGoodwillAdjustmentPerCall > Settings.MaxDailyGoodwillAdjustment / 2)
            {
                GUI.color = Color.yellow;
                Text.Font = GameFont.Tiny;
                listing.Label("RimChat_GoodwillWarning".Translate());
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
            }
        }

        /// <summary>/// 缂傚倷璁查崑鎾绘煠濞村娅呴柍閿嬬墵閹鎷呯粙搴撴寖闂? ///</summary>
        internal void DrawGiftSettings(Listing_Standard listing)
        {
            listing.Label($"RimChat_MaxGiftSilverAmount".Translate(Settings.MaxGiftSilverAmount));
            Settings.MaxGiftSilverAmount = (int)listing.Slider(Settings.MaxGiftSilverAmount, 100, 5000);

            // 闂備礁鎼悧鍐磻閹炬剚鐔嗛柛顐㈡閸熴劑宕戦妸鈺傜厵闁规鍠栭弸搴ㄦ倵鐟欏嫬鈻曠€殿喓鍔戝畷婊勬媴鐟欏嫬巍
            listing.Label($"RimChat_MaxGiftGoodwillGain".Translate(Settings.MaxGiftGoodwillGain));
            Settings.MaxGiftGoodwillGain = (int)listing.Slider(Settings.MaxGiftGoodwillGain, 1, 25);

            float cooldownDays = Settings.GiftCooldownTicks / 60000f;
            listing.Label($"RimChat_GiftCooldown".Translate(cooldownDays.ToString("F1")));
            cooldownDays = listing.Slider(cooldownDays, 0.5f, 5f);
            Settings.GiftCooldownTicks = (int)(cooldownDays * 60000);
        }

        /// <summary>/// 闂備礁婀辩划顖氼焽濞嗘劖鍙忔い蹇撴婵ジ鏌涢幘妤€鎳忛悗? ///</summary>
        internal void DrawAidRequestSettings(Listing_Standard listing)
        {
            listing.Label($"RimChat_MinGoodwillForAid".Translate(Settings.MinGoodwillForAid));
            Settings.MinGoodwillForAid = (int)listing.Slider(Settings.MinGoodwillForAid, 0, 100);

            float cooldownDays = Settings.AidCooldownTicks / 60000f;
            listing.Label($"RimChat_AidCooldown".Translate(cooldownDays.ToString("F1")));
            cooldownDays = listing.Slider(cooldownDays, 1f, 7f);
            Settings.AidCooldownTicks = (int)(cooldownDays * 60000);

            float delayDays = Settings.AidDelayBaseTicks / 60000f;
            listing.Label($"RimChat_AidDelay".Translate(delayDays.ToString("F1")));
            delayDays = listing.Slider(delayDays, 0.0f, 5f);
            Settings.AidDelayBaseTicks = (int)(delayDays * 60000);
        }

        internal void DrawAirdropTradeSettings(Listing_Standard listing)
        {
            listing.Label("RimChat_ItemAirdropSettingsTitle".Translate());

            listing.Label("RimChat_AirdropMinBudgetSilver".Translate(Settings.ItemAirdropMinBudgetSilver));
            Settings.ItemAirdropMinBudgetSilver = (int)listing.Slider(Settings.ItemAirdropMinBudgetSilver, 100f, 5000f);

            listing.Label("RimChat_AirdropMaxBudgetSilver".Translate(Settings.ItemAirdropMaxBudgetSilver));
            Settings.ItemAirdropMaxBudgetSilver = (int)listing.Slider(Settings.ItemAirdropMaxBudgetSilver, 5000f, 200000f);

            listing.Label("RimChat_AirdropDefaultAIBudgetSilver".Translate(Settings.ItemAirdropDefaultAIBudgetSilver));
            Settings.ItemAirdropDefaultAIBudgetSilver = (int)listing.Slider(Settings.ItemAirdropDefaultAIBudgetSilver, (float)Settings.ItemAirdropMinBudgetSilver, (float)Settings.ItemAirdropMaxBudgetSilver);

            listing.CheckboxLabeled("RimChat_EnableAirdropAliasExpansion".Translate(), ref Settings.EnableAirdropAliasExpansion);
            listing.Label("RimChat_ItemAirdropAliasExpansionMaxCount".Translate(Settings.ItemAirdropAliasExpansionMaxCount));
            Settings.ItemAirdropAliasExpansionMaxCount = (int)listing.Slider(Settings.ItemAirdropAliasExpansionMaxCount, 2, 12);

            listing.Label("RimChat_ItemAirdropAliasExpansionTimeoutSeconds".Translate(Settings.ItemAirdropAliasExpansionTimeoutSeconds));
            Settings.ItemAirdropAliasExpansionTimeoutSeconds = (int)listing.Slider(Settings.ItemAirdropAliasExpansionTimeoutSeconds, 2, 10);

            listing.CheckboxLabeled("RimChat_EnableAirdropSameFamilyRelaxedRetry".Translate(), ref Settings.EnableAirdropSameFamilyRelaxedRetry);

            int airdropDays = Settings.ItemAirdropCooldownTicks / 60000;
            listing.Label("RimChat_ItemAirdropCooldownDays".Translate(airdropDays));
            airdropDays = (int)listing.Slider(airdropDays, 1f, 7f);
            Settings.ItemAirdropCooldownTicks = airdropDays * 60000;

            listing.Label("RimChat_AirdropTradeLimitMultiplier".Translate(Settings.ItemAirdropTradeLimitMultiplier.ToString("F1")));
            Settings.ItemAirdropTradeLimitMultiplier = listing.Slider(Settings.ItemAirdropTradeLimitMultiplier, 0.5f, 10.0f);

            listing.Label("RimChat_AirdropCooldownMultiplier".Translate(Settings.ItemAirdropCooldownMultiplier.ToString("F2")));
            Settings.ItemAirdropCooldownMultiplier = listing.Slider(Settings.ItemAirdropCooldownMultiplier, 0.1f, 5.0f);

            listing.Label("RimChat_ItemAirdropBlockedCategories".Translate());
            Settings.ItemAirdropBlockedCategoriesCsv = listing.TextEntry(Settings.ItemAirdropBlockedCategoriesCsv ?? string.Empty);

            listing.Label("RimChat_ItemAirdropBlacklist".Translate());
            Settings.ItemAirdropBlacklistDefNamesCsv = listing.TextEntry(Settings.ItemAirdropBlacklistDefNamesCsv ?? string.Empty);

            listing.Gap(8f);
            listing.Label("RimChat_AirdropPriceSettingsTitle".Translate());

            listing.Label("RimChat_AirdropNeedPriceMultiplier".Translate(Settings.ItemAirdropNeedPriceMultiplier.ToString("F2")));
            Settings.ItemAirdropNeedPriceMultiplier = listing.Slider(Settings.ItemAirdropNeedPriceMultiplier, 0.10f, 10.0f);

            listing.Label("RimChat_AirdropExoticMiscNeedPriceMultiplier".Translate(Settings.ItemAirdropExoticMiscNeedPriceMultiplier.ToString("F2")));
            Settings.ItemAirdropExoticMiscNeedPriceMultiplier = listing.Slider(Settings.ItemAirdropExoticMiscNeedPriceMultiplier, 0.10f, 10.0f);

            listing.Label("RimChat_AirdropOfferPriceMultiplier".Translate(Settings.ItemAirdropOfferPriceMultiplier.ToString("F2")));
            Settings.ItemAirdropOfferPriceMultiplier = listing.Slider(Settings.ItemAirdropOfferPriceMultiplier, 0.10f, 5.0f);

            listing.Label("RimChat_AirdropExoticMiscOfferPriceMultiplier".Translate(Settings.ItemAirdropExoticMiscOfferPriceMultiplier.ToString("F2")));
            Settings.ItemAirdropExoticMiscOfferPriceMultiplier = listing.Slider(Settings.ItemAirdropExoticMiscOfferPriceMultiplier, 0.10f, 5.0f);

            listing.Label("RimChat_AirdropUntradeableOfferPriceMultiplier".Translate(Settings.ItemAirdropUntradeableOfferPriceMultiplier.ToString("F2")));
            Settings.ItemAirdropUntradeableOfferPriceMultiplier = listing.Slider(Settings.ItemAirdropUntradeableOfferPriceMultiplier, 0.10f, 5.0f);

            listing.Label("RimChat_AirdropSpecialItemDiscountMultiplier".Translate(Settings.ItemAirdropSpecialItemDiscountMultiplier.ToString("F2")));
            Settings.ItemAirdropSpecialItemDiscountMultiplier = listing.Slider(Settings.ItemAirdropSpecialItemDiscountMultiplier, 0.01f, 2.0f);

            listing.Label("RimChat_AirdropSpecialItemScarceMultiplier".Translate(Settings.ItemAirdropSpecialItemScarceMultiplier.ToString("F2")));
            Settings.ItemAirdropSpecialItemScarceMultiplier = listing.Slider(Settings.ItemAirdropSpecialItemScarceMultiplier, 0.10f, 10.0f);

            listing.Gap(4f);
            listing.Label("RimChat_AirdropUntradeableTieredTitle".Translate());

            listing.Label("RimChat_AirdropUntradeableLowValueMultiplier".Translate(Settings.ItemAirdropUntradeableLowValuePriceMultiplier.ToString("F2")));
            Settings.ItemAirdropUntradeableLowValuePriceMultiplier = listing.Slider(Settings.ItemAirdropUntradeableLowValuePriceMultiplier, 0.10f, 50.0f);

            listing.Label("RimChat_AirdropUntradeableMidValueMultiplier".Translate(Settings.ItemAirdropUntradeableMidValuePriceMultiplier.ToString("F2")));
            Settings.ItemAirdropUntradeableMidValuePriceMultiplier = listing.Slider(Settings.ItemAirdropUntradeableMidValuePriceMultiplier, 0.10f, 50.0f);

            listing.Label("RimChat_AirdropUntradeableHighValueMultiplier".Translate(Settings.ItemAirdropUntradeablePriceMultiplier.ToString("F2")));
            Settings.ItemAirdropUntradeablePriceMultiplier = listing.Slider(Settings.ItemAirdropUntradeablePriceMultiplier, 0.10f, 50.0f);
        }

        internal void DrawPrisonerRansomSettings(Listing_Standard listing)
        {
            listing.Label("RimChat_PrisonerRansomSettingsTitle".Translate());
            listing.Label("RimChat_RansomReleaseTimeoutTicks".Translate(Settings.RansomReleaseTimeoutTicks));
            Settings.RansomReleaseTimeoutTicks = (int)listing.Slider(Settings.RansomReleaseTimeoutTicks, 2500, 600000);

            listing.Label("RimChat_RansomValueDropMajorThreshold".Translate((Settings.RansomValueDropMajorThreshold * 100f).ToString("F0")));
            Settings.RansomValueDropMajorThreshold = listing.Slider(Settings.RansomValueDropMajorThreshold, 0.05f, 0.90f);

            listing.Label("RimChat_RansomValueDropSevereThreshold".Translate((Settings.RansomValueDropSevereThreshold * 100f).ToString("F0")));
            Settings.RansomValueDropSevereThreshold = listing.Slider(Settings.RansomValueDropSevereThreshold, Settings.RansomValueDropMajorThreshold, 0.98f);

            listing.Label("RimChat_RansomLowGoodwillDiscountThreshold".Translate(Settings.RansomLowGoodwillDiscountThreshold));
            Settings.RansomLowGoodwillDiscountThreshold = (int)listing.Slider(Settings.RansomLowGoodwillDiscountThreshold, -100, 100);

            listing.Label("RimChat_RansomLowGoodwillDiscountFactor".Translate((Settings.RansomLowGoodwillDiscountFactor * 100f).ToString("F0")));
            Settings.RansomLowGoodwillDiscountFactor = listing.Slider(Settings.RansomLowGoodwillDiscountFactor, 0.10f, 1f);

            listing.Label("RimChat_RansomPenaltyMajor".Translate(Settings.RansomPenaltyMajor));
            Settings.RansomPenaltyMajor = -(int)listing.Slider(Mathf.Abs(Settings.RansomPenaltyMajor), 0, 100);

            listing.Label("RimChat_RansomPenaltySevere".Translate(Settings.RansomPenaltySevere));
            Settings.RansomPenaltySevere = -(int)listing.Slider(Mathf.Abs(Settings.RansomPenaltySevere), 0, 100);

            listing.Label("RimChat_RansomPenaltyTimeout".Translate(Settings.RansomPenaltyTimeout));
            Settings.RansomPenaltyTimeout = -(int)listing.Slider(Mathf.Abs(Settings.RansomPenaltyTimeout), 0, 100);
        }

        /// <summary>/// 闂備胶鎳撻悺銊ф箒缂備降鍔婇崐鏍矙婢跺鍎熼柍鈺佸暙椤忣垰螖閻橀潧浠滈柣銈呮喘椤㈡瑩寮撮悩鐢碉紴? ///</summary>
        internal void DrawWarPeaceSettings(Listing_Standard listing)
        {
            listing.Label($"RimChat_MaxGoodwillForWar".Translate(Settings.MaxGoodwillForWarDeclaration));
            Settings.MaxGoodwillForWarDeclaration = (int)listing.Slider(Settings.MaxGoodwillForWarDeclaration, -100, 0);

            float warCooldownDays = Settings.WarCooldownTicks / 60000f;
            listing.Label($"RimChat_WarCooldown".Translate(warCooldownDays.ToString("F1")));
            warCooldownDays = listing.Slider(warCooldownDays, 1f, 7f);
            Settings.WarCooldownTicks = (int)(warCooldownDays * 60000);

            listing.Gap();

            listing.Label($"RimChat_MaxPeaceCost".Translate(Settings.MaxPeaceCost));
            Settings.MaxPeaceCost = (int)listing.Slider(Settings.MaxPeaceCost, 0, 10000);

            listing.Label($"RimChat_PeaceGoodwillReset".Translate(Settings.PeaceGoodwillReset));
            Settings.PeaceGoodwillReset = (int)listing.Slider(Settings.PeaceGoodwillReset, -100, 0);

            float peaceCooldownDays = Settings.PeaceCooldownTicks / 60000f;
            listing.Label($"RimChat_PeaceCooldown".Translate(peaceCooldownDays.ToString("F1")));
            peaceCooldownDays = listing.Slider(peaceCooldownDays, 1f, 7f);
            Settings.PeaceCooldownTicks = (int)(peaceCooldownDays * 60000);
        }

        /// <summary>/// 闂備礁鎽滈崰搴∥涘┑鍠綁鏁傞悙顒€顎涢梺鍛婃寙閸涱喚鈧? ///</summary>
        internal void DrawCaravanSettings(Listing_Standard listing)
        {
            float cooldownDays = Settings.CaravanCooldownTicks / 60000f;
            listing.Label($"RimChat_CaravanCooldown".Translate(cooldownDays.ToString("F1")));
            cooldownDays = listing.Slider(cooldownDays, 0.5f, 5f);
            Settings.CaravanCooldownTicks = (int)(cooldownDays * 60000);

            float delayDays = Settings.CaravanDelayBaseTicks / 60000f;
            listing.Label($"RimChat_CaravanDelay".Translate(delayDays.ToString("F1")));
            delayDays = listing.Slider(delayDays, 0.0f, 7f);
            Settings.CaravanDelayBaseTicks = (int)(delayDays * 60000);
        }

        /// <summary>/// 濠电偛顕慨楣冾敋瑜庨幈銊╂偄閻戞ê顎涢梺鍛婃寙閸涱喚鈧? ///</summary>
        internal void DrawQuestSettings(Listing_Standard listing)
        {
            listing.Label($"RimChat_MinQuestCooldown".Translate(Settings.MinQuestCooldownDays));
            Settings.MinQuestCooldownDays = (int)listing.Slider(Settings.MinQuestCooldownDays, 1, 30);

            listing.Label($"RimChat_MaxQuestCooldown".Translate(Settings.MaxQuestCooldownDays));
            Settings.MaxQuestCooldownDays = (int)listing.Slider(Settings.MaxQuestCooldownDays, Math.Max(Settings.MinQuestCooldownDays, 1), 60);
        }

        /// <summary>/// 闂佽娴烽幊鎾凰囬鐐茬煑闊洦娲樻刊濂告煕閹炬鎳忛悗? ///</summary>
        internal void DrawSecuritySettings(Listing_Standard listing)
        {
            listing.CheckboxLabeled("RimChat_EnableAPICallLogging".Translate(), ref Settings.EnableAPICallLogging);

            listing.Label("RimChat_MaxAPICallsPerHour".Translate(GetApiCallLimitLabelValue()));
            int clampedLimit = Mathf.Clamp(Settings.MaxAPICallsPerHour, 0, 100);
            Settings.MaxAPICallsPerHour = Mathf.RoundToInt(listing.Slider(clampedLimit, 0f, 100f));
        }

        internal string GetApiCallLimitLabelValue()
        {
            int limit = Mathf.Max(0, Settings.MaxAPICallsPerHour);
            if (limit <= 0)
            {
                return "RimChat_Unlimited".Translate().ToString();
            }

            return limit.ToString();
        }
}
