using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using UnityEngine;
using UnityEngine.Networking;
using Verse;
using Verse.Sound;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.UI;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Core.Player2;
using Ustas.RimAI.Core.UI;

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class RelationsProviderFactionPrompts
{
    readonly RelationsSettingsPages Pages;

    internal RelationsProviderFactionPrompts(RelationsSettingsPages pages)
    {
        Pages = pages;
    }

    RelationsSettings Settings => Pages.Settings;

        internal UnityEngine.Vector2 factionListScrollPosition = UnityEngine.Vector2.zero;
        internal UnityEngine.Vector2 promptEditorScrollPosition = UnityEngine.Vector2.zero;
        internal bool showHiddenFactions = false;
        internal string selectedFactionDefName = null;
        internal string editingCustomPrompt = "";
        internal bool editingUseCustomPrompt = false;
        internal EnhancedTextArea factionPromptTextArea;

        internal void DrawFactionPromptSettingsSection(Listing_Standard listing)
        {
            listing.Label("RimChat_FactionPromptSettings".Translate());
            listing.GapLine();

            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            Rect descRect = listing.GetRect(Text.LineHeight);
            Widgets.Label(descRect, "RimChat_FactionPromptSettingsDesc".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            listing.Gap(5f);

            // 闁板秶鐤嗛弬鍥︽鐠侯垰绶為弰鍓с仛
            string configPath = FactionPromptManager.Instance.ConfigFilePath;
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            Rect pathRect = listing.GetRect(Text.LineHeight);
            Widgets.Label(pathRect, $"Config: {configPath}");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            listing.Gap(5f);

            // 閺勫墽銇氶梾鎰濞插墽閮撮柅澶愩€?
            Rect toggleRect = listing.GetRect(24f);
            Widgets.CheckboxLabeled(toggleRect, "RimChat_ShowHiddenFactions".Translate(), ref showHiddenFactions);
            listing.Gap(10f);

            float totalHeight = 420f;
            Rect mainRect = listing.GetRect(totalHeight);

            float leftWidth = mainRect.width * 0.38f;
            float rightWidth = mainRect.width * 0.6f - 10f;

            Rect leftRect = new Rect(mainRect.x, mainRect.y, leftWidth, totalHeight);
            Rect rightRect = new Rect(mainRect.x + leftWidth + 10f, mainRect.y, rightWidth, totalHeight);

            // 缂佹ê鍩楀ú鍓ч兇閸掓銆?
            DrawFactionPromptList(leftRect);

            DrawFactionPromptEditor(rightRect);

            listing.Gap(10f);

            // 鎼存洟鍎撮幙宥勭稊閹稿鎸?
            DrawFactionPromptActionButtons(listing);
        }

        internal void DrawFactionPromptList(Rect rect)
        {
            Rect innerRect = rect.ContractedBy(4f);

            // 閺嶅洭顣?
            Text.Font = GameFont.Small;
            Rect titleRect = new Rect(innerRect.x, innerRect.y, innerRect.width, 24f);
            Widgets.Label(titleRect, "RimChat_FactionList".Translate());

            float listY = innerRect.y + 28f;
            Rect listRect = new Rect(innerRect.x, listY, innerRect.width, innerRect.height - 28f);

            var configs = FactionPromptManager.Instance.AllConfigs;

            if (configs.Count == 0)
            {
                GUI.color = Color.gray;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(listRect, "RimChat_NoFactionConfigs".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                return;
            }

            float rowHeight = 30f;
            float totalListHeight = Mathf.Max(configs.Count * rowHeight, listRect.height);
            Rect viewRect = new Rect(0, 0, listRect.width - 16f, totalListHeight);

            Widgets.BeginScrollView(listRect, ref factionListScrollPosition, viewRect);

            float y = 0f;
            for (int i = 0; i < configs.Count; i++)
            {
                var config = configs[i];
                if (!showHiddenFactions && IsHiddenFaction(config.FactionDefName))
                {
                    continue;
                }

                Rect rowRect = new Rect(0, y, viewRect.width, rowHeight);

                // 闁鑵戞妯瑰瘨
                if (selectedFactionDefName == config.FactionDefName)
                {
                    Widgets.DrawHighlightSelected(rowRect);
                }
                else if (i % 2 == 0)
                {
                    Widgets.DrawLightHighlight(rowRect);
                }

                // 閻愮懓鍤柅澶嬪
                if (Widgets.ButtonInvisible(rowRect))
                {
                    selectedFactionDefName = config.FactionDefName;
                    editingCustomPrompt = config.CustomPrompt ?? "";
                    editingUseCustomPrompt = config.UseCustomPrompt;
                }

                float xOffset = 4f;

                // 閼奉亜鐣炬稊澶嬪瘹缁€鍝勬珤
                if (config.UseCustomPrompt)
                {
                    Rect customRect = new Rect(xOffset, y + 8f, 14f, 14f);
                    GUI.color = new Color(0.3f, 0.8f, 0.3f);
                    Widgets.DrawBoxSolid(customRect, GUI.color);
                    GUI.color = Color.white;
                    xOffset += 20f;
                }

                // 濞插墽閮撮崥宥囆?
                Rect nameRect = new Rect(xOffset, y, viewRect.width - xOffset - 10f, rowHeight);
                Text.Anchor = TextAnchor.MiddleLeft;
                string displayName = string.IsNullOrEmpty(config.DisplayName) ? config.FactionDefName : config.DisplayName;
                Widgets.Label(nameRect, displayName.Truncate(nameRect.width));
                Text.Anchor = TextAnchor.UpperLeft;

                y += rowHeight;
            }

            Widgets.EndScrollView();
        }

        internal bool IsHiddenFaction(string factionDefName)
        {
            var def = DefDatabase<FactionDef>.GetNamedSilentFail(factionDefName);
            if (def == null) return false;
            try
            {
                var hiddenField = typeof(FactionDef).GetField("hidden");
                if (hiddenField != null)
                {
                    return (bool)hiddenField.GetValue(def);
                }
            }
            catch { }
            return false;
        }

        internal void DrawFactionPromptEditor(Rect rect)
        {
            Rect innerRect = rect.ContractedBy(6f);

            if (string.IsNullOrEmpty(selectedFactionDefName))
            {
                GUI.color = Color.gray;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(innerRect, "RimChat_SelectFactionForPrompt".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                return;
            }

            var config = FactionPromptManager.Instance.GetConfig(selectedFactionDefName);
            if (config == null)
            {
                GUI.color = Color.gray;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(innerRect, "RimChat_FactionConfigNotFound".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                return;
            }

            float y = innerRect.y;

            // 濞插墽閮撮崥宥囆為弽鍥暯
            Text.Font = GameFont.Medium;
            Rect headerRect = new Rect(innerRect.x, y, innerRect.width, 28f);
            string displayName = string.IsNullOrEmpty(config.DisplayName) ? config.FactionDefName : config.DisplayName;
            Widgets.Label(headerRect, displayName);
            Text.Font = GameFont.Small;
            y += 32f;

            // 娴ｈ法鏁ら懛顏勭暰娑斿rompt闁銆?
            Rect checkboxRect = new Rect(innerRect.x, y, innerRect.width, 24f);
            bool prevUseCustom = editingUseCustomPrompt;
            Widgets.CheckboxLabeled(checkboxRect, "RimChat_UseCustomPrompt".Translate(), ref editingUseCustomPrompt);
            if (prevUseCustom != editingUseCustomPrompt)
            {
                config.UseCustomPrompt = editingUseCustomPrompt;
                FactionPromptManager.Instance.UpdateConfig(config);
            }
            y += 28f;

            Rect lineRect = new Rect(innerRect.x, y, innerRect.width, 2f);
            Widgets.DrawBoxSolid(lineRect, new Color(0.3f, 0.3f, 0.3f, 0.5f));
            y += 8f;

            if (editingUseCustomPrompt)
            {
                // 缂傛牞绶懛顏勭暰娑斿rompt
                DrawCustomPromptEditor(innerRect, ref y, config);
            }
            else
            {
                // 閺勫墽銇氭妯款吇Prompt鐠囷附鍎?
                DrawDefaultPromptViewer(innerRect, ref y, config);
            }
        }

        internal void DrawCustomPromptEditor(Rect innerRect, ref float y, FactionPromptConfig config)
        {
            if (factionPromptTextArea == null || factionPromptTextArea.Text != editingCustomPrompt)
            {
                factionPromptTextArea = new EnhancedTextArea($"FactionPrompt_{config.FactionDefName}", Settings.MaxFactionPromptLength);
                factionPromptTextArea.Text = editingCustomPrompt;
                factionPromptTextArea.OnTextChanged += (newText) => editingCustomPrompt = newText;
            }
            factionPromptTextArea.MaxLength = Settings.MaxFactionPromptLength;

            // 閺傚洦婀扮紓鏍帆閸栧搫鐓?
            float textHeight = innerRect.yMax - y - 70f;
            Rect textRect = new Rect(innerRect.x, y, innerRect.width, textHeight);
            factionPromptTextArea.Draw(textRect);
            editingCustomPrompt = factionPromptTextArea.Text;
            y += textHeight + 8f;

            float btnWidth = (innerRect.width - 20f) / 3;

            // 娣囨繂鐡ㄩ幐澶愭尦
            Rect saveRect = new Rect(innerRect.x, y, btnWidth, 28f);
            bool canSave = !factionPromptTextArea.HasExceededLimit;
            GUI.color = canSave ? new Color(0.3f, 0.8f, 0.3f) : Color.gray;
            if (Widgets.ButtonText(saveRect, "RimChat_SavePrompt".Translate()) && canSave)
            {
                config.ApplyCustomPrompt(editingCustomPrompt);
                FactionPromptManager.Instance.UpdateConfig(config);
                Messages.Message("RimChat_PromptSaved".Translate(), MessageTypeDefOf.NeutralEvent, false);
            }
            GUI.color = Color.white;

            Rect resetRect = new Rect(innerRect.x + btnWidth + 10f, y, btnWidth, 28f);
            if (Widgets.ButtonText(resetRect, "RimChat_ResetToDefault".Translate()))
            {
                ShowResetPromptConfirmation(config);
            }

            // 閺屻儳婀呮妯款吇閹稿鎸?
            Rect viewRect = new Rect(innerRect.x + btnWidth * 2 + 20f, y, btnWidth, 28f);
            if (Widgets.ButtonText(viewRect, "RimChat_ViewDefault".Translate()))
            {
                string defaultPrompt = config.BuildPromptFromTemplate();
                Find.WindowStack.Add(new Dialog_MessageBox(
                    defaultPrompt,
                    "OK".Translate(),
                    null,
                    null,
                    null,
                    null,
                    false,
                    null,
                    null,
                    WindowLayer.Dialog
                ));
            }
        }

        internal void DrawDefaultPromptViewer(Rect innerRect, ref float y, FactionPromptConfig config)
        {
            float sectionHeight = 60f;

            // 閺嶇绺炬搴㈢壐
            DrawPromptFeature(innerRect, ref y, "RimChat_CoreStyle".Translate(), config.GetFieldValue("閺嶇绺炬搴㈢壐"), sectionHeight);

            // 閻劏鐦濋悧鐟扮窙
            DrawPromptFeature(innerRect, ref y, "RimChat_VocabularyFeatures".Translate(), config.GetFieldValue("閻劏鐦濋悧鐟扮窙"), sectionHeight);

            // 鐠囶厽鐨甸悧鐟扮窙
            DrawPromptFeature(innerRect, ref y, "RimChat_ToneFeatures".Translate(), config.GetFieldValue("鐠囶厽鐨甸悧鐟扮窙"), sectionHeight);

            // 閸欍儱绱￠悧鐟扮窙
            DrawPromptFeature(innerRect, ref y, "RimChat_SentenceFeatures".Translate(), config.GetFieldValue("閸欍儱绱￠悧鐟扮窙"), sectionHeight);

            // 鐞涖劏鎻粋浣哥箟
            DrawPromptFeature(innerRect, ref y, "RimChat_Taboos".Translate(), config.GetFieldValue("鐞涖劏鎻粋浣哥箟"), sectionHeight);

            float btnWidth = (innerRect.width - 20f) / 2;
            float btnY = innerRect.yMax - 34f;

            // 缂傛牞绶Ο鈩冩緲閹稿鎸?
            Rect editTemplateRect = new Rect(innerRect.x, btnY, btnWidth, 28f);
            if (Widgets.ButtonText(editTemplateRect, "RimChat_EditTemplate".Translate()))
            {
                Find.WindowStack.Add(new Dialog_FactionPromptEditor(config));
            }

            // 妫板嫯顫嶉幐澶愭尦
            Rect previewRect = new Rect(innerRect.x + btnWidth + 10f, btnY, btnWidth, 28f);
            if (Widgets.ButtonText(previewRect, "RimChat_PreviewPrompt".Translate()))
            {
                string fullPrompt = config.GetEffectivePrompt();
                Find.WindowStack.Add(new Dialog_MessageBox(
                    fullPrompt,
                    "OK",
                    null,
                    null,
                    null,
                    null,
                    false,
                    null,
                    null,
                    WindowLayer.Dialog
                ));
            }
        }

        internal void DrawPromptFeature(Rect innerRect, ref float y, string label, string content, float height)
        {
            // 閺嶅洨顒?
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Rect labelRect = new Rect(innerRect.x, y, innerRect.width, Text.LineHeight);
            Widgets.Label(labelRect, label);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += Text.LineHeight + 2f;

            Rect contentRect = new Rect(innerRect.x, y, innerRect.width, height);
            Widgets.DrawBoxSolid(contentRect, new Color(0.1f, 0.1f, 0.1f, 0.3f));

            GUI.color = new Color(0.9f, 0.9f, 0.9f);
            Text.Font = GameFont.Tiny;
            Rect textRect = contentRect.ContractedBy(4f);
            Widgets.Label(textRect, content ?? "");
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            y += height + 6f;
        }

        internal void ShowResetPromptConfirmation(FactionPromptConfig config)
        {
            Dialog_MessageBox dialog = Dialog_MessageBox.CreateConfirmation(
                "RimChat_ResetPromptConfirm".Translate(config.DisplayName),
                () =>
                {
                    config.ResetToDefault();
                    editingCustomPrompt = "";
                    editingUseCustomPrompt = false;
                    FactionPromptManager.Instance.UpdateConfig(config);
                    Messages.Message("RimChat_PromptReset".Translate(), MessageTypeDefOf.NeutralEvent, false);
                },
                true,
                "RimChat_ResetConfirmTitle".Translate()
            );
            Find.WindowStack.Add(dialog);
        }

        internal void DrawFactionPromptActionButtons(Listing_Standard listing)
        {
            Rect buttonRowRect = listing.GetRect(28f);
            float btnWidth = (buttonRowRect.width - 20f) / 3;

            // 鐎电厧鍤柊宥囩枂閹稿鎸?
            Rect exportRect = new Rect(buttonRowRect.x, buttonRowRect.y, btnWidth, buttonRowRect.height);
            if (Widgets.ButtonText(exportRect, "RimChat_ExportPrompts".Translate()))
            {
                ShowExportPromptsDialog();
            }

            // 鐎电厧鍙嗛柊宥囩枂閹稿鎸?
            Rect importRect = new Rect(buttonRowRect.x + btnWidth + 10f, buttonRowRect.y, btnWidth, buttonRowRect.height);
            if (Widgets.ButtonText(importRect, "RimChat_ImportPrompts".Translate()))
            {
                ShowImportPromptsDialog();
            }

            Rect resetAllRect = new Rect(buttonRowRect.x + btnWidth * 2 + 20f, buttonRowRect.y, btnWidth, buttonRowRect.height);
            GUI.color = new Color(1f, 0.6f, 0.6f);
            if (Widgets.ButtonText(resetAllRect, "RimChat_ResetAllPrompts".Translate()))
            {
                ShowResetAllPromptsConfirmation();
            }
            GUI.color = Color.white;
        }

        internal void ShowExportPromptsDialog()
        {
            string defaultPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "RimChat_Prompts.json");
            Find.WindowStack.Add(new Dialog_SaveFile(defaultPath, (path) =>
            {
                if (FactionPromptManager.Instance.ExportConfigs(path))
                {
                    Messages.Message("RimChat_ExportSuccess".Translate(path), MessageTypeDefOf.NeutralEvent, false);
                }
                else
                {
                    Messages.Message("RimChat_ExportFailed".Translate(), MessageTypeDefOf.NegativeEvent, false);
                }
            }));
        }

        internal void ShowImportPromptsDialog()
        {
            string defaultPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "RimChat_Prompts.json");
            Find.WindowStack.Add(new Dialog_LoadFile(defaultPath, (path) =>
            {
                if (FactionPromptManager.Instance.ImportConfigs(path))
                {
                    if (!string.IsNullOrEmpty(selectedFactionDefName))
                    {
                        var config = FactionPromptManager.Instance.GetConfig(selectedFactionDefName);
                        if (config != null)
                        {
                            editingCustomPrompt = config.CustomPrompt ?? "";
                            editingUseCustomPrompt = config.UseCustomPrompt;
                        }
                    }
                    Messages.Message("RimChat_ImportSuccess".Translate(), MessageTypeDefOf.NeutralEvent, false);
                }
                else
                {
                    Messages.Message("RimChat_ImportFailed".Translate(), MessageTypeDefOf.NegativeEvent, false);
                }
            }));
        }

        internal void ShowResetAllPromptsConfirmation()
        {
            Dialog_MessageBox dialog = Dialog_MessageBox.CreateConfirmation(
                "RimChat_ResetAllPromptsConfirm".Translate(),
                () =>
                {
                    FactionPromptManager.Instance.ResetAllConfigs();
                    editingCustomPrompt = "";
                    editingUseCustomPrompt = false;
                    selectedFactionDefName = null;
                    Messages.Message("RimChat_AllPromptsReset".Translate(), MessageTypeDefOf.NeutralEvent, false);
                },
                true,
                "RimChat_ResetConfirmTitle".Translate()
            );
            Find.WindowStack.Add(dialog);
        }

        internal int EstimateTokenCount(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            // 缁鏆愭导鎵暬閿涙矮鑵戦懟杈ㄦ瀮濞ｅ嘲鎮庣痪?鐎涙顑?Token
            return text.Length / 4;
        }
}
