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

internal sealed class RelationsGameplaySettingsSections
{
    internal readonly RelationsGameplaySettingsPage Owner;

    internal RelationsGameplaySettingsSections(RelationsGameplaySettingsPage owner)
    {
        Owner = owner;
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
            bool expanded = Owner.expandedAIControlSection == section;
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
            Owner.Pages.Tooltips.Register(clickableRect, RelationsSettingsTooltips.GetAISectionTooltipKey(section));

            if (Widgets.ButtonInvisible(clickableRect))
            {
                Owner.ToggleAIControlSection(section);
                SoundDefOf.Click.PlayOneShotOnCamera(null);
            }

            if (expanded)
            {
                Color prevButtonColor = GUI.color;
                GUI.color = new Color(0.85f, 0.85f, 0.85f);
                if (Widgets.ButtonText(buttonRect, "RimChat_ResetToDefault".Translate()))
                {
                    Owner.ShowResetConfirmationDialog(title, resetAction);
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
            listing.CheckboxLabeled("RimChat_ReplaceCommsConsole".Translate(), ref Owner.Settings.ReplaceCommsConsole);
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
            DrawSpeedOption(new Rect(scaleRowRect.x, scaleRowRect.y, scaleColW, 32f), "RimChat_ScaleAuto".Translate(), Owner.Settings.TerminalScale == TerminalScale.Auto, () => Owner.Settings.TerminalScale = TerminalScale.Auto);
            DrawSpeedOption(new Rect(scaleRowRect.x + (scaleColW + scaleGap), scaleRowRect.y, scaleColW, 32f), "RimChat_Scale100".Translate(), Owner.Settings.TerminalScale == TerminalScale.S100, () => Owner.Settings.TerminalScale = TerminalScale.S100);
            DrawSpeedOption(new Rect(scaleRowRect.x + (scaleColW + scaleGap) * 2, scaleRowRect.y, scaleColW, 32f), "RimChat_Scale125".Translate(), Owner.Settings.TerminalScale == TerminalScale.S125, () => Owner.Settings.TerminalScale = TerminalScale.S125);
            DrawSpeedOption(new Rect(scaleRowRect.x + (scaleColW + scaleGap) * 3, scaleRowRect.y, scaleColW, 32f), "RimChat_Scale150".Translate(), Owner.Settings.TerminalScale == TerminalScale.S150, () => Owner.Settings.TerminalScale = TerminalScale.S150);
            DrawSpeedOption(new Rect(scaleRowRect.x + (scaleColW + scaleGap) * 4, scaleRowRect.y, scaleColW, 32f), "RimChat_Scale175".Translate(), Owner.Settings.TerminalScale == TerminalScale.S175, () => Owner.Settings.TerminalScale = TerminalScale.S175);
            DrawSpeedOption(new Rect(scaleRowRect.x + (scaleColW + scaleGap) * 5, scaleRowRect.y, scaleColW, 32f), "RimChat_Scale200".Translate(), Owner.Settings.TerminalScale == TerminalScale.S200, () => Owner.Settings.TerminalScale = TerminalScale.S200);
            DrawSpeedOption(new Rect(scaleRowRect.x + (scaleColW + scaleGap) * 6, scaleRowRect.y, scaleColW, 32f), "RimChat_Scale250".Translate(), Owner.Settings.TerminalScale == TerminalScale.S250, () => Owner.Settings.TerminalScale = TerminalScale.S250);
            listing.Gap(8f);

            listing.Label("RimChat_TypewriterSpeed".Translate());
            listing.Gap(6f);

            Rect speedRowRect = listing.GetRect(32f);
            float columnWidth = (speedRowRect.width - 20f) / 3f;
            float spacing = 10f;

            Rect fastRect = new Rect(speedRowRect.x, speedRowRect.y, columnWidth, 32f);
            Rect standardRect = new Rect(speedRowRect.x + columnWidth + spacing, speedRowRect.y, columnWidth, 32f);
            Rect immersiveRect = new Rect(speedRowRect.x + (columnWidth + spacing) * 2, speedRowRect.y, columnWidth, 32f);

            DrawSpeedOption(fastRect, "RimChat_SpeedFast".Translate(), Owner.Settings.TypewriterSpeedMode == TypewriterSpeedMode.Fast, () => Owner.Settings.TypewriterSpeedMode = TypewriterSpeedMode.Fast);
            DrawSpeedOption(standardRect, "RimChat_SpeedStandard".Translate(), Owner.Settings.TypewriterSpeedMode == TypewriterSpeedMode.Standard, () => Owner.Settings.TypewriterSpeedMode = TypewriterSpeedMode.Standard);
            DrawSpeedOption(immersiveRect, "RimChat_SpeedImmersive".Translate(), Owner.Settings.TypewriterSpeedMode == TypewriterSpeedMode.Immersive, () => Owner.Settings.TypewriterSpeedMode = TypewriterSpeedMode.Immersive);

            listing.Gap(6f);

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.65f, 0.65f, 0.65f);
            string speedDesc = Owner.Settings.TypewriterSpeedMode switch
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
            int selectedIndex = System.Array.IndexOf(modes, (Owner.Settings.ExpandMemoryCompatMode ?? "auto").ToLowerInvariant());
            if (selectedIndex < 0) selectedIndex = 0;

            Rect autoRect = listing.GetRect(28f);
            Rect onRect = listing.GetRect(28f);
            Rect offRect = listing.GetRect(28f);
            if (Widgets.RadioButtonLabeled(autoRect, "RimChat_ExpandMemoryCompatAuto".Translate(), selectedIndex == 0))
                Owner.Settings.ExpandMemoryCompatMode = modes[0];
            if (Widgets.RadioButtonLabeled(onRect, "RimChat_ExpandMemoryCompatOn".Translate(), selectedIndex == 1))
                Owner.Settings.ExpandMemoryCompatMode = modes[1];
            if (Widgets.RadioButtonLabeled(offRect, "RimChat_ExpandMemoryCompatOff".Translate(), selectedIndex == 2))
                Owner.Settings.ExpandMemoryCompatMode = modes[2];

            listing.Gap();
            listing.CheckboxLabeled("RimChat_ExpandMemoryInjectPawnMemory".Translate(), ref Owner.Settings.ExpandMemoryInjectPawnMemory);

            listing.Gap();
            listing.Label("RimChat_ExpandMemoryPawnMemoryMaxChars".Translate(Owner.Settings.ExpandMemoryPawnMemoryMaxChars));
            Owner.Settings.ExpandMemoryPawnMemoryMaxChars = (int)listing.Slider(
                Owner.Settings.ExpandMemoryPawnMemoryMaxChars,
                Persistence.PromptPersistenceService.ExpandMemoryPawnMemoryMaxCharsMin,
                Persistence.PromptPersistenceService.ExpandMemoryPawnMemoryMaxCharsMax);

            listing.Gap();
            listing.Label("RimChat_ExpandMemoryPawnMemoryMaxEntries".Translate(Owner.Settings.ExpandMemoryPawnMemoryMaxEntries));
            Owner.Settings.ExpandMemoryPawnMemoryMaxEntries = (int)listing.Slider(
                Owner.Settings.ExpandMemoryPawnMemoryMaxEntries,
                Persistence.PromptPersistenceService.ExpandMemoryPawnMemoryMaxEntriesMin,
                Persistence.PromptPersistenceService.ExpandMemoryPawnMemoryMaxEntriesMax);

            listing.GapLine();
            listing.Label("RimChat_FactionExclusionCsv".Translate());
            Owner.Settings.FactionExclusionDefNamesCsv = listing.TextEntry(Owner.Settings.FactionExclusionDefNamesCsv ?? string.Empty);
        }






















        internal void NormalizeRaidPointSettings()
        {
            Owner.Settings.RaidPointsMultiplier = RaidPointsFactionOverride.ClampMultiplier(Owner.Settings.RaidPointsMultiplier);
            Owner.Settings.MinRaidPoints = RaidPointsFactionOverride.ClampMinPoints(Owner.Settings.MinRaidPoints);

            if (Owner.Settings.RaidPointsFactionOverrides == null)
            {
                Owner.Settings.RaidPointsFactionOverrides = new List<RaidPointsFactionOverride>();
                return;
            }

            List<RaidPointsFactionOverride> normalized = new List<RaidPointsFactionOverride>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (RaidPointsFactionOverride entry in Owner.Settings.RaidPointsFactionOverrides)
            {
                if (entry == null) continue;
                entry.Normalize();
                if (string.IsNullOrWhiteSpace(entry.FactionDefName)) continue;
                if (!seen.Add(entry.FactionDefName)) continue;
                normalized.Add(entry);
            }

            Owner.Settings.RaidPointsFactionOverrides = normalized;
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
                Owner.ShowResetConfirmationDialog(title, resetAction);
            }

            GUI.color = prevColor;
        }

        internal void DrawPresenceSettings(Listing_Standard listing)
        {
            listing.CheckboxLabeled("RimChat_EnablePresenceSystem".Translate(), ref Owner.Settings.EnableFactionPresenceStatus);

            listing.Label("RimChat_PresenceCacheHours".Translate(Owner.Settings.PresenceCacheHours.ToString("F1")));
            Owner.Settings.PresenceCacheHours = listing.Slider(Owner.Settings.PresenceCacheHours, 1f, 48f);

            listing.CheckboxLabeled("RimChat_PresenceNightBiasEnabled".Translate(), ref Owner.Settings.PresenceNightBiasEnabled);
            if (Owner.Settings.PresenceNightBiasEnabled)
            {
                listing.Label("RimChat_PresenceNightStartHour".Translate(Owner.Settings.PresenceNightStartHour));
                Owner.Settings.PresenceNightStartHour = Mathf.RoundToInt(listing.Slider(Owner.Settings.PresenceNightStartHour, 0f, 23f));

                listing.Label("RimChat_PresenceNightEndHour".Translate(Owner.Settings.PresenceNightEndHour));
                Owner.Settings.PresenceNightEndHour = Mathf.RoundToInt(listing.Slider(Owner.Settings.PresenceNightEndHour, 0f, 23f));

                listing.Label("RimChat_PresenceNightOfflineBias".Translate((Owner.Settings.PresenceNightOfflineBias * 100f).ToString("F0")));
                Owner.Settings.PresenceNightOfflineBias = listing.Slider(Owner.Settings.PresenceNightOfflineBias, 0f, 1f);
            }

            listing.CheckboxLabeled("RimChat_PresenceAdvancedProfiles".Translate(), ref Owner.Settings.PresenceUseAdvancedProfiles);
            if (Owner.Settings.PresenceUseAdvancedProfiles)
            {
                DrawPresenceProfileSliders(listing, "RimChat_PresenceProfileDefault".Translate(), ref Owner.Settings.PresenceOnlineStart_Default, ref Owner.Settings.PresenceOnlineDuration_Default);
                DrawPresenceProfileSliders(listing, "RimChat_PresenceProfileNeolithic".Translate(), ref Owner.Settings.PresenceOnlineStart_Neolithic, ref Owner.Settings.PresenceOnlineDuration_Neolithic);
                DrawPresenceProfileSliders(listing, "RimChat_PresenceProfileMedieval".Translate(), ref Owner.Settings.PresenceOnlineStart_Medieval, ref Owner.Settings.PresenceOnlineDuration_Medieval);
                DrawPresenceProfileSliders(listing, "RimChat_PresenceProfileIndustrial".Translate(), ref Owner.Settings.PresenceOnlineStart_Industrial, ref Owner.Settings.PresenceOnlineDuration_Industrial);
                DrawPresenceProfileSliders(listing, "RimChat_PresenceProfileSpacer".Translate(), ref Owner.Settings.PresenceOnlineStart_Spacer, ref Owner.Settings.PresenceOnlineDuration_Spacer);
                DrawPresenceProfileSliders(listing, "RimChat_PresenceProfileUltra".Translate(), ref Owner.Settings.PresenceOnlineStart_Ultra, ref Owner.Settings.PresenceOnlineDuration_Ultra);
                DrawPresenceProfileSliders(listing, "RimChat_PresenceProfileArchotech".Translate(), ref Owner.Settings.PresenceOnlineStart_Archotech, ref Owner.Settings.PresenceOnlineDuration_Archotech);
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

        /// <summary>/// AI 闂佽崵鍋炵粙鎴炵附閺冨倹瀚婚柣鏃傚帶缁犳垿鎮归崶顏勭毢缁炬儳顭烽弻? ///</summary>
        internal void DrawAIBehaviorToggles(Listing_Standard listing)
        {
            listing.CheckboxLabeled("RimChat_EnableAIGoodwillAdjustment".Translate(), ref Owner.Settings.EnableAIGoodwillAdjustment);
            listing.CheckboxLabeled("RimChat_EnableAIGiftSending".Translate(), ref Owner.Settings.EnableAIGiftSending);
            listing.CheckboxLabeled("RimChat_EnableAIWarDeclaration".Translate(), ref Owner.Settings.EnableAIWarDeclaration);
            listing.CheckboxLabeled("RimChat_EnableAIPeaceMaking".Translate(), ref Owner.Settings.EnableAIPeaceMaking);
            listing.CheckboxLabeled("RimChat_EnableAITradeCaravan".Translate(), ref Owner.Settings.EnableAITradeCaravan);
            listing.CheckboxLabeled("RimChat_EnableAIAidRequest".Translate(), ref Owner.Settings.EnableAIAidRequest);
            listing.CheckboxLabeled("RimChat_EnableAIRaidRequest".Translate(), ref Owner.Settings.EnableAIRaidRequest);
            listing.CheckboxLabeled("RimChat_EnableAIItemAirdrop".Translate(), ref Owner.Settings.EnableAIItemAirdrop);
            listing.CheckboxLabeled("RimChat_EnablePrisonerRansom".Translate(), ref Owner.Settings.EnablePrisonerRansom);
        }
}
