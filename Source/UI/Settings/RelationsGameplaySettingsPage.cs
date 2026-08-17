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
    internal readonly RelationsSettingsPages Pages;

    internal RelationsGameplaySettingsPage(RelationsSettingsPages pages)
    {
        Pages = pages;
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

        /// <summary>/// 闂佽崵濮崇欢銈囨閺囥垺鍋╃紒顐㈠殬闂備胶顢婇惌鍥礃閵娧冨箑闂傚倷绶￠崑鍕囬悽绋课ョ€广儱顦涵鈧梺鐐藉劚閸熷潡寮崼鏇熷€电痪顓炴媼濞兼劙鏌嶈閸撴瑩鈥﹂悜鑺ュ仧妞ゆ棁濮ら崕?
 ///</summary>
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
            Rect headerRect = listing.GetRect(30f);
            bool expanded = expandedAIControlSection == section;
            float buttonWidth = expanded ? 80f : 0f;
            float rightPadding = expanded ? 10f : 0f;
            Rect clickableRect = new Rect(headerRect.x, headerRect.y, headerRect.width - buttonWidth - rightPadding, headerRect.height);
            Rect titleRect = new Rect(clickableRect.x + 6f, clickableRect.y, clickableRect.width - 6f, clickableRect.height);
            Rect buttonRect = new Rect(headerRect.x + headerRect.width - 80f, headerRect.y + 2f, 80f, 24f);
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;

            Color headerBackground = expanded
                ? new Color(0.20f, 0.28f, 0.42f, 0.35f)
                : (Mouse.IsOver(clickableRect) ? new Color(0.16f, 0.18f, 0.22f, 0.45f) : new Color(0.12f, 0.12f, 0.14f, 0.30f));
            Widgets.DrawBoxSolid(headerRect, headerBackground);
            if (expanded)
            {
                Color accent = titleColor ?? new Color(0.45f, 0.75f, 1f, 0.9f);
                Widgets.DrawBoxSolid(new Rect(headerRect.x, headerRect.y, 3f, headerRect.height), accent);
            }

            Color original = GUI.color;
            Text.Font = GameFont.Small;
            if (titleColor.HasValue) GUI.color = titleColor.Value;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(titleRect, title);
            Text.Anchor = oldAnchor;
            Text.Font = oldFont;
            GUI.color = original;
            Pages.Tooltips.Register(clickableRect, RelationsSettingsTooltips.GetAISectionTooltipKey(section));

            if (Widgets.ButtonInvisible(clickableRect))
            {
                ToggleAIControlSection(section);
                SoundDefOf.Click.PlayOneShotOnCamera(null);
            }

            if (expanded)
            {
                Color prevButtonColor = GUI.color;
                GUI.color = new Color(0.85f, 0.85f, 0.85f);
                if (Widgets.ButtonText(buttonRect, "RimChat_ResetToDefault".Translate()))
                {
                    ShowResetConfirmationDialog(title, resetAction);
                }
                GUI.color = prevButtonColor;
            }

            if (expanded)
            {
                listing.Gap(2f);
                drawContent?.Invoke(listing);
                listing.Gap(8f);
            }
            else
            {
                listing.Gap(4f);
            }

            Text.Font = oldFont;
            Text.Anchor = oldAnchor;
        }

        internal void DrawUISettings(Listing_Standard listing)
        {
            listing.CheckboxLabeled("RimChat_ReplaceCommsConsole".Translate(), ref Settings.ReplaceCommsConsole);
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            Rect commsDescRect = listing.GetRect(Text.LineHeight * 2f);
            Widgets.Label(commsDescRect, "RimChat_ReplaceCommsConsoleDesc".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            listing.Gap(6f);

            listing.Label("RimChat_TerminalScale".Translate());
            listing.Gap(6f);
            Rect scaleRowRect = listing.GetRect(32f);
            float scaleGap = 4f;
            float scaleColW = (scaleRowRect.width - scaleGap * 6) / 7f;
            DrawSpeedOption(new Rect(scaleRowRect.x, scaleRowRect.y, scaleColW, 32f), "RimChat_ScaleAuto".Translate(), Settings.TerminalScale == TerminalScale.Auto, () => Settings.TerminalScale = TerminalScale.Auto);
            DrawSpeedOption(new Rect(scaleRowRect.x + (scaleColW + scaleGap), scaleRowRect.y, scaleColW, 32f), "RimChat_Scale100".Translate(), Settings.TerminalScale == TerminalScale.S100, () => Settings.TerminalScale = TerminalScale.S100);
            DrawSpeedOption(new Rect(scaleRowRect.x + (scaleColW + scaleGap) * 2, scaleRowRect.y, scaleColW, 32f), "RimChat_Scale125".Translate(), Settings.TerminalScale == TerminalScale.S125, () => Settings.TerminalScale = TerminalScale.S125);
            DrawSpeedOption(new Rect(scaleRowRect.x + (scaleColW + scaleGap) * 3, scaleRowRect.y, scaleColW, 32f), "RimChat_Scale150".Translate(), Settings.TerminalScale == TerminalScale.S150, () => Settings.TerminalScale = TerminalScale.S150);
            DrawSpeedOption(new Rect(scaleRowRect.x + (scaleColW + scaleGap) * 4, scaleRowRect.y, scaleColW, 32f), "RimChat_Scale175".Translate(), Settings.TerminalScale == TerminalScale.S175, () => Settings.TerminalScale = TerminalScale.S175);
            DrawSpeedOption(new Rect(scaleRowRect.x + (scaleColW + scaleGap) * 5, scaleRowRect.y, scaleColW, 32f), "RimChat_Scale200".Translate(), Settings.TerminalScale == TerminalScale.S200, () => Settings.TerminalScale = TerminalScale.S200);
            DrawSpeedOption(new Rect(scaleRowRect.x + (scaleColW + scaleGap) * 6, scaleRowRect.y, scaleColW, 32f), "RimChat_Scale250".Translate(), Settings.TerminalScale == TerminalScale.S250, () => Settings.TerminalScale = TerminalScale.S250);
            listing.Gap(8f);

            listing.Label("RimChat_TypewriterSpeed".Translate());
            listing.Gap(6f);

            Rect speedRowRect = listing.GetRect(32f);
            float columnWidth = (speedRowRect.width - 20f) / 3f;
            float spacing = 10f;

            Rect fastRect = new Rect(speedRowRect.x, speedRowRect.y, columnWidth, 32f);
            Rect standardRect = new Rect(speedRowRect.x + columnWidth + spacing, speedRowRect.y, columnWidth, 32f);
            Rect immersiveRect = new Rect(speedRowRect.x + (columnWidth + spacing) * 2, speedRowRect.y, columnWidth, 32f);

            DrawSpeedOption(fastRect, "RimChat_SpeedFast".Translate(), Settings.TypewriterSpeedMode == TypewriterSpeedMode.Fast, () => Settings.TypewriterSpeedMode = TypewriterSpeedMode.Fast);
            DrawSpeedOption(standardRect, "RimChat_SpeedStandard".Translate(), Settings.TypewriterSpeedMode == TypewriterSpeedMode.Standard, () => Settings.TypewriterSpeedMode = TypewriterSpeedMode.Standard);
            DrawSpeedOption(immersiveRect, "RimChat_SpeedImmersive".Translate(), Settings.TypewriterSpeedMode == TypewriterSpeedMode.Immersive, () => Settings.TypewriterSpeedMode = TypewriterSpeedMode.Immersive);

            listing.Gap(6f);

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.65f, 0.65f, 0.65f);
            string speedDesc = Settings.TypewriterSpeedMode switch
            {
                TypewriterSpeedMode.Fast => "RimChat_SpeedFastDesc".Translate(),
                TypewriterSpeedMode.Standard => "RimChat_SpeedStandardDesc".Translate(),
                TypewriterSpeedMode.Immersive => "RimChat_SpeedImmersiveDesc".Translate(),
                _ => ""
            };
            Rect descRect = listing.GetRect(Text.LineHeight * 2f);
            Widgets.Label(descRect, speedDesc);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            listing.Gap(8f);
        }

        internal void DrawPresenceSettings(Listing_Standard listing)
        {
            listing.CheckboxLabeled("RimChat_EnablePresenceSystem".Translate(), ref Settings.EnableFactionPresenceStatus);

            listing.Label("RimChat_PresenceCacheHours".Translate(Settings.PresenceCacheHours.ToString("F1")));
            Settings.PresenceCacheHours = listing.Slider(Settings.PresenceCacheHours, 1f, 48f);

            listing.CheckboxLabeled("RimChat_PresenceNightBiasEnabled".Translate(), ref Settings.PresenceNightBiasEnabled);
            if (Settings.PresenceNightBiasEnabled)
            {
                listing.Label("RimChat_PresenceNightStartHour".Translate(Settings.PresenceNightStartHour));
                Settings.PresenceNightStartHour = Mathf.RoundToInt(listing.Slider(Settings.PresenceNightStartHour, 0f, 23f));

                listing.Label("RimChat_PresenceNightEndHour".Translate(Settings.PresenceNightEndHour));
                Settings.PresenceNightEndHour = Mathf.RoundToInt(listing.Slider(Settings.PresenceNightEndHour, 0f, 23f));

                listing.Label("RimChat_PresenceNightOfflineBias".Translate((Settings.PresenceNightOfflineBias * 100f).ToString("F0")));
                Settings.PresenceNightOfflineBias = listing.Slider(Settings.PresenceNightOfflineBias, 0f, 1f);
            }

            listing.CheckboxLabeled("RimChat_PresenceAdvancedProfiles".Translate(), ref Settings.PresenceUseAdvancedProfiles);
            if (Settings.PresenceUseAdvancedProfiles)
            {
                DrawPresenceProfileSliders(listing, "RimChat_PresenceProfileDefault".Translate(), ref Settings.PresenceOnlineStart_Default, ref Settings.PresenceOnlineDuration_Default);
                DrawPresenceProfileSliders(listing, "RimChat_PresenceProfileNeolithic".Translate(), ref Settings.PresenceOnlineStart_Neolithic, ref Settings.PresenceOnlineDuration_Neolithic);
                DrawPresenceProfileSliders(listing, "RimChat_PresenceProfileMedieval".Translate(), ref Settings.PresenceOnlineStart_Medieval, ref Settings.PresenceOnlineDuration_Medieval);
                DrawPresenceProfileSliders(listing, "RimChat_PresenceProfileIndustrial".Translate(), ref Settings.PresenceOnlineStart_Industrial, ref Settings.PresenceOnlineDuration_Industrial);
                DrawPresenceProfileSliders(listing, "RimChat_PresenceProfileSpacer".Translate(), ref Settings.PresenceOnlineStart_Spacer, ref Settings.PresenceOnlineDuration_Spacer);
                DrawPresenceProfileSliders(listing, "RimChat_PresenceProfileUltra".Translate(), ref Settings.PresenceOnlineStart_Ultra, ref Settings.PresenceOnlineDuration_Ultra);
                DrawPresenceProfileSliders(listing, "RimChat_PresenceProfileArchotech".Translate(), ref Settings.PresenceOnlineStart_Archotech, ref Settings.PresenceOnlineDuration_Archotech);
            }
        }

        internal void DrawPresenceProfileSliders(Listing_Standard listing, string profileLabel, ref int startHour, ref int durationHours)
        {
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.75f, 0.95f, 0.75f);
            listing.Label(profileLabel);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            listing.Label("RimChat_PresenceProfileStartHour".Translate(startHour));
            startHour = Mathf.RoundToInt(listing.Slider(startHour, 0f, 23f));

            listing.Label("RimChat_PresenceProfileDurationHours".Translate(durationHours));
            durationHours = Mathf.RoundToInt(listing.Slider(durationHours, 1f, 24f));
        }

        /// <summary>/// 缂傚倸鍊烽悞锕傛晪闂佺硶鏅滈〃濠囧蓟閸涘瓨鍋勭€瑰嫰鍋婇崬娲⒒娓氬洤浜滄い锔炬暬婵℃潙顓兼径瀣珫闂佸壊鍋呯换鍌滅矆鐎ｎ喗鈷戞い鎰╁焺濡插綊鎮楅崹顐ょ煉闁?+ 闂備礁鎼崐绋棵洪敃鈧敃銏″鐎涙ɑ娅? ///</summary>
        internal void DrawSpeedOption(Rect rect, string label, bool isActive, System.Action onClick)
        {
            // 缂傚倸鍊烽悞锕傛晪闂佺硶鏅滈〃濠囧箠濡ゅ啩娌柣鎰靛墰瑜版煡姊洪幐搴ｂ槈闁绘妫濋妴鍛邦樄鐎殿喚顭堥…銊╁醇濮橆兛澹曟繝銏ｆ硾椤︽娊宕㈤鍕厵閻庢稒顭囨晶顒勬煕鐎ｎ偅宕岀€规洘鍨甸…銊╁箛椤旂虎妲?
            if (isActive)
            {
                Widgets.DrawBoxSolid(rect, new Color(0.25f, 0.45f, 0.7f, 0.3f));
            }
            else if (Mouse.IsOver(rect))
            {
                Widgets.DrawBoxSolid(rect, new Color(0.15f, 0.15f, 0.2f, 0.5f));
            }

            float radioSize = 20f;
            float radioX = rect.x + 10f;
            float radioY = rect.y + (rect.height - radioSize) / 2f;
            Rect radioRect = new Rect(radioX, radioY, radioSize, radioSize);
            
            Color outerColor = isActive ? new Color(0.3f, 0.7f, 1f) : new Color(0.5f, 0.5f, 0.55f);
            GUI.color = outerColor;
            GUI.DrawTexture(radioRect, BaseContent.WhiteTex);
            
            if (isActive)
            {
                float innerSize = radioSize * 0.5f;
                float innerX = radioX + (radioSize - innerSize) / 2f;
                float innerY = radioY + (radioSize - innerSize) / 2f;
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(innerX, innerY, innerSize, innerSize), BaseContent.WhiteTex);
            }
            
            GUI.color = Color.white;

            Text.Font = GameFont.Small;
            GUI.color = isActive ? Color.white : new Color(0.85f, 0.85f, 0.9f);
            Rect textRect = new Rect(radioX + radioSize + 8f, rect.y + (rect.height - Text.LineHeight) / 2f, rect.width - radioSize - 16f, Text.LineHeight);
            Widgets.Label(textRect, label);
            GUI.color = Color.white;

            if (Widgets.ButtonInvisible(rect))
            {
                onClick();
                SoundDefOf.Click.PlayOneShotOnCamera(null);
            }
        }

        /// <summary>/// AI 闂佽崵鍋炵粙鎴炵附閺冨倹瀚婚柣鏃傚帶缁犳垿鎮归崶顏勭毢缁炬儳顭烽弻? ///</summary>
        internal void DrawAIBehaviorToggles(Listing_Standard listing)
        {
            listing.CheckboxLabeled("RimChat_EnableAIGoodwillAdjustment".Translate(), ref Settings.EnableAIGoodwillAdjustment);
            listing.CheckboxLabeled("RimChat_EnableAIGiftSending".Translate(), ref Settings.EnableAIGiftSending);
            listing.CheckboxLabeled("RimChat_EnableAIWarDeclaration".Translate(), ref Settings.EnableAIWarDeclaration);
            listing.CheckboxLabeled("RimChat_EnableAIPeaceMaking".Translate(), ref Settings.EnableAIPeaceMaking);
            listing.CheckboxLabeled("RimChat_EnableAITradeCaravan".Translate(), ref Settings.EnableAITradeCaravan);
            listing.CheckboxLabeled("RimChat_EnableAIAidRequest".Translate(), ref Settings.EnableAIAidRequest);
            listing.CheckboxLabeled("RimChat_EnableAIRaidRequest".Translate(), ref Settings.EnableAIRaidRequest);
            listing.CheckboxLabeled("RimChat_EnableAIItemAirdrop".Translate(), ref Settings.EnableAIItemAirdrop);
            listing.CheckboxLabeled("RimChat_EnablePrisonerRansom".Translate(), ref Settings.EnablePrisonerRansom);
        }






















        internal void NormalizeRaidPointSettings()
        {
            Settings.RaidPointsMultiplier = RaidPointsFactionOverride.ClampMultiplier(Settings.RaidPointsMultiplier);
            Settings.MinRaidPoints = RaidPointsFactionOverride.ClampMinPoints(Settings.MinRaidPoints);

            if (Settings.RaidPointsFactionOverrides == null)
            {
                Settings.RaidPointsFactionOverrides = new List<RaidPointsFactionOverride>();
                return;
            }

            List<RaidPointsFactionOverride> normalized = new List<RaidPointsFactionOverride>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (RaidPointsFactionOverride entry in Settings.RaidPointsFactionOverrides)
            {
                if (entry == null) continue;
                entry.Normalize();
                if (string.IsNullOrWhiteSpace(entry.FactionDefName)) continue;
                if (!seen.Add(entry.FactionDefName)) continue;
                normalized.Add(entry);
            }

            Settings.RaidPointsFactionOverrides = normalized;
        }

        /// <summary>/// 缂傚倸鍊烽悞锕傛晪闂佺硶鏅滈〃濠傜暦濮樿泛骞㈡俊銈傚亾闂傚懏锕㈤弻鈥愁吋閸涱喖鏋犲銈忕导缁瑥顕ｉ崐鐔虹杸闁靛／鍜佹Х闂備礁鎲￠悧鏇㈠箠鎼淬劌绠氶柛顐犲劚閸愨偓闂佹悶鍎洪崜锕傚汲椤栫偞鐓曟繝濠傚暞濠€鏉棵归悪鈧崰妤€顕ラ崟顐悑濠㈣泛鑻粭锟犳煟閻橀亶妾烽柛濠冪墱閳ь剙鐏氱划鎾诲蓟? ///</summary>
        internal void DrawSectionHeader(Listing_Standard listing, string title, System.Action resetAction, Color? titleColor = null)
        {
            Rect headerRect = listing.GetRect(28f);
            float buttonWidth = 80f;
            float buttonHeight = 24f;

            Rect titleRect = new Rect(headerRect.x, headerRect.y, headerRect.width - buttonWidth - 10f, headerRect.height);

            Color originalColor = GUI.color;
            if (titleColor.HasValue)
            {
                GUI.color = titleColor.Value;
            }

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(titleRect, title);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = originalColor;

            Rect lineRect = new Rect(headerRect.x, headerRect.y + headerRect.height - 2f, headerRect.width - buttonWidth - 10f, 2f);
            Widgets.DrawBoxSolid(lineRect, new Color(0.3f, 0.3f, 0.3f, 0.5f));

            Rect buttonRect = new Rect(headerRect.x + headerRect.width - buttonWidth, headerRect.y + 2f, buttonWidth, buttonHeight);
            Color prevColor = GUI.color;
            GUI.color = new Color(0.85f, 0.85f, 0.85f);

            if (Widgets.ButtonText(buttonRect, "RimChat_ResetToDefault".Translate()))
            {
                ShowResetConfirmationDialog(title, resetAction);
            }

            GUI.color = prevColor;
        }

        /// <summary>/// 闂備礁鎼€氼剚鏅舵禒瀣︽慨妯挎硾缁犳帡鏌曡箛鏇烆€屾俊鑼额嚙椤鈽夊▎妯煎姼濡炪倖鎹佸畷闈涒槈閻㈠壊鏁婃繛鍡樺劤閹鏌ｆ惔锝嗘毄妞ゃ垹锕幆渚€鎸婃径妯荤? ///</summary>
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
        /// <summary>/// 闂備浇顕栭崢褰掑垂瑜版崵鍥儓濠甸亶鏌ｉ悙瀵糕槈濠靛倹姊婚幏褰掓偄閻戞ê顎涢梺鍛婃寙閸涱喚鈧厽绻涢幋鐐村鞍婵＄偟鏅崚鎺楊敍濠婂嫬顎涢梺闈涚墕閹冲宕? ///</summary>
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

        /// <summary>/// 闂備浇顕栭崢褰掑垂瑜版崵鍥嚑椤掍礁浠忓銈嗘尵閸嬫稑袙婵犲洦鍋ｅù锝囶焾閳锋棃鏌ｉ妶鍛棡缂佸顦叅妞ゅ繐妫楃粭锟犳煟閻橀亶妾烽柛濠冩礋閸┾偓? ///</summary>
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

        /// <summary>/// 闂備浇顕栭崢褰掑垂瑜版崵鍥矗婢跺矂妾梺鍏间航閸庤鲸淇婇幎钘夌閺夊牆澧介悾铏亜閺冣偓濞叉粎妲愰弮鍫晩闁哄嫬绻掗ˇ鐗堟叏閹烘挾鈯曟い顓炵墦椤㈡ɑ绻濆顒傦紮? ///</summary>
        internal void ResetGoodwillSettingsToDefault()
        {
            Settings.DialogueActionGoodwillCostMultiplier = 0.5f;
            Settings.MaxGoodwillAdjustmentPerCall = 15;
            Settings.MaxDailyGoodwillAdjustment = 30;
            Settings.GoodwillCooldownTicks = 2500;
        }

        /// <summary>/// 闂備浇顕栭崢褰掑垂瑜版崵鍥嚑椤掑倻鐒奸梺鍏肩ゴ閺呮盯鍩涢弽顓熷仯濞达絿顭堥埛鏃堟煟閵堝懏顥炵紒瀣槸鐓ゆい蹇撴缁楋繝鏌ｉ悩閬嶆闁稿﹥娲熼崺鈧? ///</summary>
        internal void ResetGiftSettingsToDefault()
        {
            Settings.MaxGiftSilverAmount = 1000;
            Settings.MaxGiftGoodwillGain = 10;
            Settings.GiftCooldownTicks = 60000;
        }

        /// <summary>/// 闂備浇顕栭崢褰掑垂瑜版崵鍥蓟閵夈儳顔岄梺鍝勵槹閸ㄤ絻顤呴梺鑽ゅС缁€浣规櫠娴犲鍋柛鈩冾焽閳绘梹绻涘顔荤敖閻㈩垱鐩幃瑙勬媴闂堟稈鍋撻弴銏犵劦? ///</summary>
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

        /// <summary>/// 闂備浇顕栭崢褰掑垂瑜版崵鍥蓟閵夈儳顓哄┑鈽嗗灠濠€閬嶅箰閵娿儮妲堥柟鐐▕椤庢鏌熼摎鍌氬祮闁绘侗鍠氶埀顒€婀辨刊顓㈠疮鎼达絿纾介柛鎰劤閺嬫瑩鎮归幇顔兼瀾妞ゎ亖鍋撳┑鈽嗗灡椤戞瑩宕ラ崶顒佺厱? ///</summary>
        internal void ResetWarPeaceSettingsToDefault()
        {
            Settings.MaxGoodwillForWarDeclaration = -50;
            Settings.WarCooldownTicks = 60000;
            Settings.MaxPeaceCost = 5000;
            Settings.PeaceGoodwillReset = -20;
            Settings.PeaceCooldownTicks = 60000;
        }

        /// <summary>/// 闂備浇顕栭崢褰掑垂瑜版崵鍥蓟閵夈儳鏌堥梺绯曞墲缁嬫帟顤傞梺鑽ゅС缁€浣规櫠娴犲鍋柛鈩冾焽閳绘梹绻涘顔荤敖閻㈩垱鐩幃瑙勬媴闂堟稈鍋撻弴銏犵劦? ///</summary>
        internal void ResetCaravanSettingsToDefault()
        {
            Settings.CaravanCooldownTicks = 90000;
            Settings.CaravanDelayBaseTicks = 135000;
        }

        /// <summary>/// 闂備浇顕栭崢褰掑垂瑜版崵鍥矗婢跺矈娴勯柣鐘叉处瑜板啴锝為妶澶嬪仯濞达絿顭堥埛鏃堟煟閵堝懏顥炵紒瀣槸鐓ゆい蹇撴缁楋繝鏌ｉ悩閬嶆闁稿﹥娲熼崺鈧? ///</summary>
        internal void ResetQuestSettingsToDefault()
        {
            Settings.MinQuestCooldownDays = 7;
            Settings.MaxQuestCooldownDays = 12;
        }

        /// <summary>/// 闂備浇顕栭崢褰掑垂瑜版崵鍥嚑椤戣棄浜鹃柣鐔煎亰濡叉悂鏌涘▎蹇曠闁瑰嘲顑夊畷婊嗩槾闁哄鍊搁埥澶愬箻鐎涙ǜ浠㈢紓渚囧櫘閸ㄦ娊骞忕€ｎ喖围闁告侗浜滄禍? ///</summary>
        internal void ResetSecuritySettingsToDefault()
        {
            Settings.EnableAPICallLogging = true;
            Settings.MaxAPICallsPerHour = 0;
        }

        /// <summary>/// 闂備浇顕栭崢褰掑垂瑜版崵?UI 闂佽崵濮崇粈浣规櫠娴犲鍋柛鈩冾焽閳绘梹绻涘顔荤敖閻㈩垱鐩幃瑙勬媴闂堟稈鍋撻弴銏犵劦? ///</summary>
        internal void ResetUISettingsToDefault()
        {
            Settings.TypewriterSpeedMode = TypewriterSpeedMode.Standard;
            Settings.ReplaceCommsConsole = false;
            Settings.DialogueStyleMode = DialogueStyleMode.NaturalConcise;
        }

        /// <summary>/// 闂備浇顕栭崢褰掑垂瑜版崵鍥蓟閵夈儲宓嶉梺闈浤涢崘鈺冩瀮闂備胶绮…鍫ュ春閺嶎厼鐒垫い鎴炲缁佺増銇勯弮鈧ú婊呮閺冨牜鏁婇柡鍕箳椤︾増鎱ㄩ幒鎾垛姇妞ゎ厼鐗撻, 妯荤節濮橆剛锛? ///</summary>
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

        /// <summary>/// 闂傚倷鐒﹁ぐ鍐矓閸洘鍋柛鈩冪☉缁犮儵鏌嶈閸撶喎顕ｉ悽绋块唶缂佸搫瀚板濠氬礋椤掆偓婵洭鏌涢埡鍌ゆ畷缂佸顦叅妞ゅ繐妫楃粭锟犳煟閻橀亶妾烽柛濠冩礋閸┾偓妞ゆ帒鍊堕埀顒€顑囧Σ鎰枎閹邦喒鏀冲┑鐘绘涧閻楀﹤鈻撳畝鍕厽妞ゎ偒鍓欐俊铏圭磼椤垵澧寸€规洘顨婇幃鈩冩償椤斿吋娅嶉梻? ///</summary>
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
            listing.Label("RimChat_ModCompatRimTalkExpandMemory".Translate());
            listing.GapLine();

            bool detected = Prompting.PromptRuntimeVariableBridge.IsDependencyAvailable("expandmemory");
            string status = detected
                ? "RimChat_PromptVariableReady".Translate().ToString()
                : "RimChat_PromptVariableDependencyMissing".Translate().ToString();
            listing.Label(status);
            listing.Gap();

            string[] modes = { "auto", "on", "off" };
            int selectedIndex = System.Array.IndexOf(modes, (Settings.ExpandMemoryCompatMode ?? "auto").ToLowerInvariant());
            if (selectedIndex < 0) selectedIndex = 0;

            Rect autoRect = listing.GetRect(28f);
            Rect onRect = listing.GetRect(28f);
            Rect offRect = listing.GetRect(28f);
            if (Widgets.RadioButtonLabeled(autoRect, "RimChat_ExpandMemoryCompatAuto".Translate(), selectedIndex == 0))
                Settings.ExpandMemoryCompatMode = modes[0];
            if (Widgets.RadioButtonLabeled(onRect, "RimChat_ExpandMemoryCompatOn".Translate(), selectedIndex == 1))
                Settings.ExpandMemoryCompatMode = modes[1];
            if (Widgets.RadioButtonLabeled(offRect, "RimChat_ExpandMemoryCompatOff".Translate(), selectedIndex == 2))
                Settings.ExpandMemoryCompatMode = modes[2];

            listing.Gap();
            listing.CheckboxLabeled("RimChat_ExpandMemoryInjectPawnMemory".Translate(), ref Settings.ExpandMemoryInjectPawnMemory);

            listing.Gap();
            listing.Label("RimChat_ExpandMemoryPawnMemoryMaxChars".Translate(Settings.ExpandMemoryPawnMemoryMaxChars));
            Settings.ExpandMemoryPawnMemoryMaxChars = (int)listing.Slider(
                Settings.ExpandMemoryPawnMemoryMaxChars,
                Persistence.PromptPersistenceService.ExpandMemoryPawnMemoryMaxCharsMin,
                Persistence.PromptPersistenceService.ExpandMemoryPawnMemoryMaxCharsMax);

            listing.Gap();
            listing.Label("RimChat_ExpandMemoryPawnMemoryMaxEntries".Translate(Settings.ExpandMemoryPawnMemoryMaxEntries));
            Settings.ExpandMemoryPawnMemoryMaxEntries = (int)listing.Slider(
                Settings.ExpandMemoryPawnMemoryMaxEntries,
                Persistence.PromptPersistenceService.ExpandMemoryPawnMemoryMaxEntriesMin,
                Persistence.PromptPersistenceService.ExpandMemoryPawnMemoryMaxEntriesMax);

            listing.GapLine();
            listing.Label("RimChat_FactionExclusionCsv".Translate());
            Settings.FactionExclusionDefNamesCsv = listing.TextEntry(Settings.FactionExclusionDefNamesCsv ?? string.Empty);
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
