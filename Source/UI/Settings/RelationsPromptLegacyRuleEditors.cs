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

internal sealed class RelationsPromptLegacyRuleEditors
{
    internal readonly RelationsPromptLegacyEditors Owner;

    internal RelationsPromptLegacyRuleEditors(RelationsPromptLegacyEditors owner)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    internal RelationsSettingsPages Pages => Owner.Pages;
    internal RelationsSettings Settings => Owner.Settings;
    internal SystemPromptConfig SystemPromptConfigData => Owner.SystemPromptConfigData;

        internal void DrawDecisionRulesEditorScrollable(Rect rect)
        {
            var rules = SystemPromptConfigData.DecisionRules;
            if (rules == null || rules.Count == 0)
            {
                Widgets.Label(rect, "RimChat_NoDecisionRules".Translate());
                return;
            }

            float listWidth = 220f;
            float buttonHeight = 32f;
            Rect listRect = new Rect(rect.x, rect.y, listWidth, rect.height - buttonHeight);
            
            Rect editRect = new Rect(rect.x + listWidth + 10f, rect.y, rect.width - listWidth - 10f, rect.height - buttonHeight);

            float itemHeight = 30f;
            float listContentHeight = rules.Count * itemHeight;
            Rect listContentRect = new Rect(0f, 0f, listWidth - 16f, Mathf.Max(listContentHeight, listRect.height));

            Owner._ruleContentScroll = GUI.BeginScrollView(listRect, Owner._ruleContentScroll, listContentRect);
            for (int i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                Rect rowRect = new Rect(0f, i * itemHeight, listContentRect.width, itemHeight - 1f);
                bool isSelected = i == Owner._selectedDecisionRuleIndex;

                if (isSelected)
                    Widgets.DrawBoxSolid(rowRect, new Color(0.25f, 0.35f, 0.55f, 0.8f));
                else if (Mouse.IsOver(rowRect))
                    Widgets.DrawBoxSolid(rowRect, new Color(0.2f, 0.22f, 0.28f, 0.6f));

                string label = $"{(rule.IsEnabled ? "[ON]" : "[OFF]")} {rule.RuleName}";
                GUI.color = rule.IsEnabled ? Color.white : Color.gray;
                TextAnchor oldAnchor = Text.Anchor;
                Text.Anchor = TextAnchor.MiddleLeft;
                Rect labelRect = new Rect(rowRect.x + 8f, rowRect.y + 2f, rowRect.width - 16f, rowRect.height - 4f);
                Widgets.Label(labelRect, label.Truncate(labelRect.width));
                Text.Anchor = oldAnchor;
                GUI.color = Color.white;

                if (Widgets.ButtonInvisible(rowRect))
                {
                    Owner._selectedDecisionRuleIndex = i;
                    Owner._editingRuleName = rule.RuleName ?? "";
                    Owner._editingRuleContent = rule.RuleContent ?? "";
                }
            }
            GUI.EndScrollView();

            Rect addBtnRect = new Rect(rect.x, rect.yMax - buttonHeight, listWidth, buttonHeight - 4f);
            if (Widgets.ButtonText(addBtnRect, "RimChat_AddNew".Translate()))
            {
                AddNewDecisionRule();
            }

            if (Owner._selectedDecisionRuleIndex >= 0 && Owner._selectedDecisionRuleIndex < rules.Count)
            {
                var rule = rules[Owner._selectedDecisionRuleIndex];
                float y = editRect.y;

                GUI.color = RelationsPromptLegacyEditors.SectionHeaderColor;
                Widgets.Label(new Rect(editRect.x, y, editRect.width, 24f), "RimChat_EditDecisionRule".Translate());
                GUI.color = Color.white;
                y += 28f;

                Widgets.Label(new Rect(editRect.x, y, editRect.width, 20f), "RimChat_RuleNameLabel".Translate());
                y += 22f;
                Owner._editingRuleName = Widgets.TextField(new Rect(editRect.x, y, editRect.width, 24f), Owner._editingRuleName);
                y += 28f;

                Widgets.Label(new Rect(editRect.x, y, editRect.width, 20f), "RimChat_RuleContentLabel".Translate());
                y += 22f;
                float contentHeight = editRect.yMax - y;
                Rect contentRect = new Rect(editRect.x, y, editRect.width - 16f, contentHeight);
                
                float ruleContentHeight = Mathf.Max(contentRect.height, Text.CalcHeight(Owner._editingRuleContent, contentRect.width - 16f) + 10f);
                Rect contentViewRect = new Rect(0f, 0f, contentRect.width - 16f, ruleContentHeight);
                Owner._jsonTemplateScroll = GUI.BeginScrollView(contentRect, Owner._jsonTemplateScroll, contentViewRect);
                Owner._editingRuleContent = GUI.TextArea(contentViewRect, Owner._editingRuleContent);
                GUI.EndScrollView();
                
                rule.RuleContent = Owner._editingRuleContent;
                rule.RuleName = Owner._editingRuleName;

                float btnWidth = 100f;
                float btnGap = 10f;
                float btnStartX = editRect.x;
                
                Rect enableBtnRect = new Rect(btnStartX, rect.yMax - buttonHeight, btnWidth, buttonHeight - 4f);
                if (Widgets.ButtonText(enableBtnRect, rule.IsEnabled ? "RimChat_Disable".Translate() : "RimChat_Enable".Translate()))
                    rule.IsEnabled = !rule.IsEnabled;
                
                Rect deleteBtnRect = new Rect(btnStartX + btnWidth + btnGap, rect.yMax - buttonHeight, btnWidth, buttonHeight - 4f);
                if (Widgets.ButtonText(deleteBtnRect, "RimChat_DeleteSelected".Translate()))
                {
                    ShowDeleteDecisionRuleConfirmation(rule);
                }
            }
            else
            {
                GUI.color = Color.gray;
                Text.Font = GameFont.Medium;
                Widgets.Label(editRect.ContractedBy(20f), "RimChat_SelectDecisionRule".Translate());
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
            }
        }

        internal void AddNewDecisionRule()
        {
            var newRule = new DecisionRuleConfig
            {
                RuleName = "NewRule",
                RuleContent = "",
                IsEnabled = true
            };
            SystemPromptConfigData.DecisionRules.Add(newRule);
            Owner._selectedDecisionRuleIndex = SystemPromptConfigData.DecisionRules.Count - 1;
            Owner._editingRuleName = "NewRule";
            Owner._editingRuleContent = "";
            Messages.Message("RimChat_ItemAdded".Translate("RimChat_DecisionRulesSection".Translate()), MessageTypeDefOf.NeutralEvent, false);
        }

        internal void ShowDeleteDecisionRuleConfirmation(DecisionRuleConfig rule)
        {
            Dialog_MessageBox dialog = Dialog_MessageBox.CreateConfirmation(
                "RimChat_DeleteConfirm".Translate("RimChat_DecisionRulesSection".Translate()),
                () =>
                {
                    int oldIndex = Owner._selectedDecisionRuleIndex;
                    SystemPromptConfigData.DecisionRules.Remove(rule);
                    if (SystemPromptConfigData.DecisionRules.Count == 0)
                    {
                        Owner._selectedDecisionRuleIndex = -1;
                        Owner._editingRuleName = "";
                        Owner._editingRuleContent = "";
                    }
                    else
                    {
                        Owner._selectedDecisionRuleIndex = Mathf.Min(oldIndex, SystemPromptConfigData.DecisionRules.Count - 1);
                        if (Owner._selectedDecisionRuleIndex >= 0 && Owner._selectedDecisionRuleIndex < SystemPromptConfigData.DecisionRules.Count)
                        {
                            var newRule = SystemPromptConfigData.DecisionRules[Owner._selectedDecisionRuleIndex];
                            Owner._editingRuleName = newRule.RuleName ?? "";
                            Owner._editingRuleContent = newRule.RuleContent ?? "";
                        }
                    }
                    Messages.Message("RimChat_ItemDeleted".Translate(rule.RuleName), MessageTypeDefOf.NeutralEvent, false);
                },
                true,
                "RimChat_DeleteConfirmTitle".Translate()
            );
            Find.WindowStack.Add(dialog);
        }

        internal void DrawFactionPromptsEditorScrollable(Rect rect)
        {
            FactionPromptManager manager = FactionPromptManager.Instance;
            var configs = manager.AllConfigs;
            if (configs == null || configs.Count == 0)
            {
                Widgets.Label(rect, "RimChat_NoFactionPrompts".Translate());
                return;
            }

            float listWidth = 200f;
            Rect listRect = new Rect(rect.x, rect.y, listWidth, rect.height);
            float listHeaderHeight = 30f;
            Rect listHeaderRect = new Rect(listRect.x, listRect.y, listRect.width, listHeaderHeight);
            Rect listScrollRect = new Rect(listRect.x, listRect.y + listHeaderHeight + 4f, listRect.width, listRect.height - listHeaderHeight - 4f);

            float addBtnWidth = 92f;
            Rect listTitleRect = new Rect(listHeaderRect.x, listHeaderRect.y, listHeaderRect.width - addBtnWidth - 6f, listHeaderRect.height);
            Rect addTemplateRect = new Rect(listHeaderRect.xMax - addBtnWidth, listHeaderRect.y, addBtnWidth, listHeaderRect.height - 2f);
            Text.Font = GameFont.Small;
            Widgets.Label(listTitleRect, "RimChat_FactionPromptsSection".Translate());
            if (Widgets.ButtonText(addTemplateRect, "RimChat_AddFactionTemplate".Translate()))
            {
                OpenFactionTemplateAddMenu();
            }

            Rect editRect = new Rect(rect.x + listWidth + 10f, rect.y, rect.width - listWidth - 10f, rect.height);

            float itemHeight = 32f;
            float listContentHeight = configs.Count * itemHeight;
            Rect listContentRect = new Rect(0f, 0f, listWidth - 16f, Mathf.Max(listContentHeight, listScrollRect.height));

            Owner._factionPromptScroll = GUI.BeginScrollView(listScrollRect, Owner._factionPromptScroll, listContentRect);
            for (int i = 0; i < configs.Count; i++)
            {
                var config = configs[i];
                Rect rowRect = new Rect(0f, i * itemHeight, listContentRect.width, itemHeight - 2f);
                bool isSelected = i == Owner._selectedFactionPromptIndex;

                if (isSelected)
                    Widgets.DrawBoxSolid(rowRect, new Color(0.25f, 0.35f, 0.55f, 0.8f));
                else if (Mouse.IsOver(rowRect))
                    Widgets.DrawBoxSolid(rowRect, new Color(0.2f, 0.22f, 0.28f, 0.6f));

                string stateTag = manager.IsDefaultTemplate(config.FactionDefName)
                    ? "RimChat_FactionTemplateTagDefault".Translate().ToString()
                    : "RimChat_FactionTemplateTagCustom".Translate().ToString();
                string missingTag = manager.IsFactionMissing(config.FactionDefName)
                    ? $" {"RimChat_FactionTemplateTagMissing".Translate()}"
                    : string.Empty;
                string label = $"{stateTag}{missingTag} {GetFactionTemplateDisplayName(config)}";
                GUI.color = manager.IsFactionMissing(config.FactionDefName)
                    ? new Color(1f, 0.7f, 0.7f)
                    : Color.white;
                Widgets.Label(rowRect.ContractedBy(4f), label.Truncate(rowRect.width - 8f));
                GUI.color = Color.white;

                if (Widgets.ButtonInvisible(rowRect))
                {
                    Owner._selectedFactionPromptIndex = i;
                }
            }
            GUI.EndScrollView();

            if (Owner._selectedFactionPromptIndex >= 0 && Owner._selectedFactionPromptIndex < configs.Count)
            {
                var selectedConfig = configs[Owner._selectedFactionPromptIndex];
                float y = editRect.y;

                GUI.color = RelationsPromptLegacyEditors.SectionHeaderColor;
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(editRect.x, y, editRect.width, 28f), GetFactionTemplateDisplayName(selectedConfig));
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
                y += 32f;

                Text.Font = GameFont.Tiny;
                GUI.color = Color.gray;
                string editorDesc = "RimChat_FactionPromptEditorDesc".Translate().ToString();
                if (manager.IsFactionMissing(selectedConfig.FactionDefName))
                {
                    editorDesc = $"{editorDesc} {"RimChat_FactionTemplateMissingDesc".Translate()}";
                }

                Widgets.Label(new Rect(editRect.x, y, editRect.width, 40f), editorDesc);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                y += 42f;

                Rect customCheckRect = new Rect(editRect.x, y, editRect.width, 24f);
                bool useCustom = selectedConfig.UseCustomPrompt;
                Widgets.CheckboxLabeled(customCheckRect, "RimChat_UseCustomPrompt".Translate(), ref useCustom);
                if (useCustom != selectedConfig.UseCustomPrompt)
                {
                    selectedConfig.UseCustomPrompt = useCustom;
                    manager.UpdateConfig(selectedConfig);
                }
                y += 28f;

                float btnWidth = (editRect.width - 16f) / 3f;
                float btnHeight = 28f;
                float btnGap = 8f;
                float buttonX = editRect.x;

                Rect editTemplateRect = new Rect(buttonX, y, btnWidth, btnHeight);
                if (Widgets.ButtonText(editTemplateRect, "RimChat_EditTemplate".Translate()))
                {
                    Find.WindowStack.Add(new Dialog_FactionPromptEditor(selectedConfig.Clone()));
                }
                buttonX += btnWidth + btnGap;

                Rect resetRect = new Rect(buttonX, y, btnWidth, btnHeight);
                if (Widgets.ButtonText(resetRect, "RimChat_Reset".Translate()))
                {
                    ShowResetFactionPromptConfirmation(selectedConfig);
                }
                buttonX += btnWidth + btnGap;

                Rect removeRect = new Rect(buttonX, y, btnWidth, btnHeight);
                bool canRemove = !manager.IsDefaultTemplate(selectedConfig.FactionDefName);
                if (!canRemove)
                {
                    GUI.color = Color.gray;
                    TooltipHandler.TipRegion(removeRect, "RimChat_FactionTemplateRemoveDefaultBlocked".Translate());
                }

                if (Widgets.ButtonText(removeRect, "RimChat_RemoveFactionTemplate".Translate()) && canRemove)
                {
                    ShowRemoveFactionPromptConfirmation(selectedConfig);
                }
                GUI.color = Color.white;

                y += btnHeight + 16f;
                Rect previewLabelRect = new Rect(editRect.x, y, editRect.width, 20f);
                GUI.color = new Color(0.5f, 0.8f, 0.5f);
                Widgets.Label(previewLabelRect, "RimChat_PreviewTitleShort".Translate());
                GUI.color = Color.white;
                y += 22f;

                Rect previewRect = new Rect(editRect.x, y, editRect.width, editRect.yMax - y);
                Widgets.DrawBoxSolid(previewRect, new Color(0.08f, 0.1f, 0.08f));
                Widgets.DrawBox(previewRect);

                string previewText = selectedConfig.GetEffectivePrompt();
                Rect innerPreviewRect = previewRect.ContractedBy(8f);

                float previewContentHeight = Text.CalcHeight(previewText, innerPreviewRect.width - 16f);
                Rect previewViewRect = new Rect(0f, 0f, innerPreviewRect.width - 16f, Mathf.Max(previewContentHeight, innerPreviewRect.height));

                Owner._previewScroll = GUI.BeginScrollView(innerPreviewRect, Owner._previewScroll, previewViewRect);
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.6f, 0.7f, 0.6f);
                Widgets.Label(previewViewRect, previewText);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                GUI.EndScrollView();
            }
            else
            {
                GUI.color = Color.gray;
                Text.Font = GameFont.Medium;
                Widgets.Label(editRect.ContractedBy(20f), "RimChat_SelectFactionPrompt".Translate());
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
            }
        }

        internal void ShowResetFactionPromptConfirmation(FactionPromptConfig config)
        {
            Dialog_MessageBox dialog = Dialog_MessageBox.CreateConfirmation(
                "RimChat_ResetFactionPromptConfirm".Translate(config.DisplayName),
                () =>
                {
                    FactionPromptManager.Instance.ResetConfig(config.FactionDefName);
                    Messages.Message("RimChat_FactionPromptReset".Translate(config.DisplayName), MessageTypeDefOf.NeutralEvent, false);
                },
                true,
                "RimChat_ResetConfirmTitle".Translate()
            );
            Find.WindowStack.Add(dialog);
        }

        internal void OpenFactionTemplateAddMenu()
        {
            List<FactionDef> defs = DefDatabase<FactionDef>.AllDefsListForReading
                .Where(def => def != null && !string.IsNullOrWhiteSpace(def.defName))
                .OrderBy(def => def.label ?? def.defName)
                .ToList();
            if (defs.Count == 0)
            {
                Messages.Message("RimChat_FactionTemplateNoFactionDefs".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            FactionPromptManager manager = FactionPromptManager.Instance;
            List<FloatMenuOption> options = defs.Select(def =>
            {
                string label = $"{(def.label ?? def.defName)} ({def.defName})";
                return new FloatMenuOption(label, () =>
                {
                    bool added = manager.TryAddTemplateForFaction(def.defName, def.label, out string status);
                    if (added)
                    {
                        List<FactionPromptConfig> refreshed = manager.AllConfigs;
                        Owner._selectedFactionPromptIndex = FindFactionPromptConfigIndex(refreshed, def.defName);
                        Messages.Message("RimChat_FactionTemplateAdded".Translate(label), MessageTypeDefOf.NeutralEvent, false);
                        return;
                    }

                    if (string.Equals(status, "existing", StringComparison.OrdinalIgnoreCase))
                    {
                        List<FactionPromptConfig> refreshed = manager.AllConfigs;
                        Owner._selectedFactionPromptIndex = FindFactionPromptConfigIndex(refreshed, def.defName);
                        Messages.Message("RimChat_FactionTemplateExistingSelected".Translate(label), MessageTypeDefOf.NeutralEvent, false);
                        return;
                    }

                    Messages.Message("RimChat_FactionTemplateAddFailed".Translate(label), MessageTypeDefOf.RejectInput, false);
                });
            }).ToList();

            Find.WindowStack.Add(new FloatMenu(options));
        }

        internal int FindFactionPromptConfigIndex(List<FactionPromptConfig> configs, string factionDefName)
        {
            if (configs == null || string.IsNullOrWhiteSpace(factionDefName))
            {
                return -1;
            }

            for (int i = 0; i < configs.Count; i++)
            {
                if (string.Equals(configs[i]?.FactionDefName, factionDefName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        internal string GetFactionTemplateDisplayName(FactionPromptConfig config)
        {
            if (config == null)
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(config.DisplayName)
                ? config.FactionDefName ?? string.Empty
                : config.DisplayName;
        }

        internal void ShowRemoveFactionPromptConfirmation(FactionPromptConfig config)
        {
            if (config == null)
            {
                return;
            }

            Dialog_MessageBox dialog = Dialog_MessageBox.CreateConfirmation(
                "RimChat_RemoveFactionTemplateConfirm".Translate(GetFactionTemplateDisplayName(config)),
                () =>
                {
                    bool removed = FactionPromptManager.Instance.TryRemoveTemplate(config.FactionDefName, out string reason);
                    if (removed)
                    {
                        Owner._selectedFactionPromptIndex = -1;
                        Owner._previewScroll = Vector2.zero;
                        Messages.Message("RimChat_FactionTemplateRemoved".Translate(GetFactionTemplateDisplayName(config)), MessageTypeDefOf.NeutralEvent, false);
                        return;
                    }

                    string key = string.Equals(reason, "default_protected", StringComparison.OrdinalIgnoreCase)
                        ? "RimChat_FactionTemplateRemoveDefaultBlocked"
                        : "RimChat_FactionTemplateRemoveFailed";
                    Messages.Message(key.Translate(GetFactionTemplateDisplayName(config)), MessageTypeDefOf.RejectInput, false);
                },
                true,
                "RimChat_DeleteConfirmTitle".Translate());
            Find.WindowStack.Add(dialog);
        }
}
