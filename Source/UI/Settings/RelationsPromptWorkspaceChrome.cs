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

internal sealed class RelationsPromptWorkspaceChrome
{
    internal readonly RelationsPromptSectionWorkspace Owner;

    internal RelationsPromptWorkspaceChrome(RelationsPromptSectionWorkspace owner)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    internal RelationsSettingsPages Pages => Owner.Pages;
    internal RelationsSettings Settings => Owner.Settings;

        internal void DrawPromptWorkspaceHeader(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, RelationsPromptSectionWorkspace.WorkspaceHeaderBg);
            Rect inner = rect.ContractedBy(8f);

            TextAnchor oldAnchor = Text.Anchor;
            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = RelationsPromptSectionWorkspace.WorkspaceAccentGold;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width * 0.42f, 28f), "RimChat_Tab_PromptWorkbench".Translate() + " Beta");
            GUI.color = Color.white;
            Text.Font = oldFont;
            Text.Anchor = oldAnchor;

            float top = inner.y + 34f;
            DrawPromptWorkspaceRootButtons(new Rect(inner.x, top, 250f, 30f));
            DrawPromptWorkspaceChannelDropdown(new Rect(inner.x + 260f, top, 300f, 30f));
            Pages.PromptQuickActions.DrawPromptWorkspaceQuickActions(new Rect(inner.x + 570f, top, Mathf.Max(220f, inner.xMax - (inner.x + 570f) - 196f), 30f));

            Rect importRect = new Rect(inner.xMax - 180f, top, 84f, 30f);
            Rect exportRect = new Rect(inner.xMax - 90f, top, 84f, 30f);
            if (Widgets.ButtonText(importRect, "RimChat_Import".Translate()))
            {
                if (!Pages.PromptWorkspaceBuffers.PersistPromptWorkspaceBufferNow(force: true))
                {
                    return;
                }

                Pages.PromptWorkbenchPresets.ShowImportPresetDialog();
            }

            if (Widgets.ButtonText(exportRect, "RimChat_Export".Translate()))
            {
                if (!Pages.PromptWorkspaceBuffers.PersistPromptWorkspaceBufferNow(force: true))
                {
                    return;
                }

                Pages.PromptWorkbenchPresets.ShowExportPresetDialog();
            }
        }

        internal void DrawPromptWorkspaceRootButtons(Rect rect)
        {
            float width = (rect.width - 6f) * 0.5f;
            Rect diplomacyRect = new Rect(rect.x, rect.y, width, rect.height);
            Rect rpgRect = new Rect(diplomacyRect.xMax + 6f, rect.y, width, rect.height);
            DrawPromptWorkspaceRootButton(diplomacyRect, PromptWorkbenchChannel.Diplomacy, "RimChat_PromptWorkbench_ChannelDiplomacy");
            DrawPromptWorkspaceRootButton(rpgRect, PromptWorkbenchChannel.Rpg, "RimChat_PromptWorkbench_ChannelRpg");
        }

        internal void DrawPromptWorkspaceRootButton(Rect rect, PromptWorkbenchChannel channel, string key)
        {
            bool selected = Pages.PromptWorkbench._workbenchChannel == channel;
            Widgets.DrawBoxSolid(rect, selected ? RelationsPromptSectionWorkspace.ButtonSelectedBg : RelationsPromptSectionWorkspace.ButtonNormalBg);
            Widgets.DrawBox(rect, 1);
            TextAnchor oldAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = selected ? RelationsPromptSectionWorkspace.WorkspaceAccentLightGold : Color.white;
            Widgets.Label(rect, key.Translate());
            GUI.color = Color.white;
            Text.Anchor = oldAnchor;
            if (Widgets.ButtonInvisible(rect))
            {
                Owner.SchedulePromptWorkspaceNavigation(() => Pages.PromptWorkspaceBuffers.SetPromptWorkspaceRoot(channel));
            }
        }

        internal void DrawPromptWorkspaceChannelDropdown(Rect rect)
        {
            string currentChannel = Pages.PromptWorkspaceBuffers.EnsurePromptWorkspaceSelection();
            Widgets.DrawBoxSolid(rect, RelationsPromptSectionWorkspace.DropdownBg);
            Widgets.DrawBox(rect, 1);
            TextAnchor oldAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = RelationsPromptSectionWorkspace.WorkspaceAccentLightGold;
            Widgets.Label(new Rect(rect.x + 8f, rect.y, rect.width - 30f, rect.height), RimTalkPromptEntryChannelCatalog.GetLabel(currentChannel));
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(rect.xMax - 22f, rect.y, 18f, rect.height), "▼");
            GUI.color = Color.white;
            Text.Anchor = oldAnchor;
            if (Widgets.ButtonInvisible(rect))
            {
                List<FloatMenuOption> options = Pages.PromptWorkspaceBuffers.GetPromptWorkspaceChannels()
                    .Select(channelId => new FloatMenuOption(
                        RimTalkPromptEntryChannelCatalog.GetLabel(channelId),
                        () => Owner.SchedulePromptWorkspaceNavigation(() => Pages.PromptWorkspaceBuffers.SetPromptWorkspaceChannel(channelId))))
                    .ToList();
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        internal void DrawPromptWorkspaceBody(Rect rect)
        {
            float gap = 8f;
            float leftWidth = Mathf.Clamp(rect.width * 0.24f, 240f, 300f);
            float remainingWidth = Mathf.Max(1f, rect.width - leftWidth - gap * 2f);
            float middleWidth = Mathf.Clamp(remainingWidth * 0.60f, 360f, Mathf.Max(360f, remainingWidth - 300f));
            float rightWidth = Mathf.Max(260f, remainingWidth - middleWidth);
            if (middleWidth + rightWidth > remainingWidth)
            {
                rightWidth = Mathf.Max(220f, remainingWidth - middleWidth);
            }

            Rect leftRect = new Rect(rect.x, rect.y, leftWidth, rect.height);
            Rect middleRect = new Rect(leftRect.xMax + gap, rect.y, middleWidth, rect.height);
            Rect rightRect = new Rect(middleRect.xMax + gap, rect.y, Mathf.Max(1f, rect.xMax - (middleRect.xMax + gap)), rect.height);

            DrawPromptWorkspacePresetPanel(leftRect);
            DrawPromptWorkspaceEditorPanel(middleRect);
            Pages.PromptWorkspacePreviewUi.DrawPromptWorkspaceSidePanel(rightRect);
        }

        internal void DrawPromptWorkspacePresetPanel(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, RelationsPromptSectionWorkspace.PresetPanelBg);
            Rect inner = rect.ContractedBy(8f);
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inner);

            Rect headerRect = listing.GetRect(22f);
            Widgets.Label(headerRect, "RimChat_PromptWorkbench_PresetHeader".Translate());
            listing.Gap(2f);

            Rect actionRect = listing.GetRect(24f);
            Pages.PromptPresetsUi.DrawPromptWorkspacePresetActions(actionRect);
            listing.Gap(4f);

            float consumed = 52f;
            float presetListHeight = Owner.ResolvePromptWorkspacePresetListHeight(inner.y + consumed, inner.yMax, inner.height);
            Rect presetListRect = listing.GetRect(presetListHeight);
            Pages.PromptPresetsUi.DrawPromptWorkspacePresetList(presetListRect);
            listing.Gap(8f);
            consumed += presetListHeight + 8f;

            Rect moduleHeaderRect = listing.GetRect(22f);
            float labelWidth = moduleHeaderRect.width - 150f;
            Widgets.Label(new Rect(moduleHeaderRect.x, moduleHeaderRect.y, labelWidth, moduleHeaderRect.height),
                "RimChat_PromptWorkspaceModuleHeader".Translate());
            Pages.PromptModuleTransfer.DrawPromptWorkspaceModuleHeaderActions(new Rect(moduleHeaderRect.x + labelWidth, moduleHeaderRect.y, 150f, moduleHeaderRect.height));
            listing.Gap(2f);
            consumed += 24f;

            float moduleListHeight = Mathf.Max(72f, inner.height - consumed - 6f);
            Rect moduleListRect = listing.GetRect(moduleListHeight);
            Pages.PromptNodeLayout.DrawPromptWorkspaceModuleList(moduleListRect);

            listing.End();
        }

        internal void DrawPromptWorkspaceEditorPanel(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, RelationsPromptSectionWorkspace.EditorPanelBg);
            Rect inner = rect.ContractedBy(8f);
            float y = inner.y;
            const float validationHeight = 24f;

            Pages.PromptWorkbench.DrawWorkbenchPresetNameRow(inner, ref y);
            Pages.PromptEditorActions.HandlePromptWorkspaceKeyboardShortcuts();
            Rect toolbarRect = new Rect(inner.x, y, inner.width, 26f);
            y += 32f;

            // Metadata row removed — redundant with module list (selection highlight + per-row checkbox)

            // Second metadata row for nodes: slot selector
            if (Owner._promptWorkspaceEditNodeMode)
            {
                DrawPromptWorkspaceNodeSlotRow(new Rect(inner.x, y, inner.width, 24f));
                y += 28f;
            }

            float editorHeight = Mathf.Max(24f, inner.yMax - y - validationHeight - 4f);
            Rect editorRect = new Rect(inner.x, y, inner.width, editorHeight);
            string sourceText = Pages.PromptWorkspaceBuffers.GetPromptWorkspaceCurrentEditorText();

            string edited = Pages.PromptWorkspacePreviewUi.DrawPromptWorkspaceEditor(editorRect, sourceText);
            Pages.PromptWorkspaceBuffers.CachePromptWorkspaceRenderedEditorText(edited);
            Pages.PromptEditorActions.CapturePromptWorkspaceLiveEditorText();
            DrawPromptWorkspaceValidationStatus(
                new Rect(inner.x, editorRect.yMax + 4f, inner.width, validationHeight),
                edited);

            if (!string.Equals(edited, Owner._promptWorkspaceEditorBuffer, StringComparison.Ordinal))
            {
                Pages.PromptWorkspaceBuffers.SetPromptWorkspaceCurrentEditorText(edited);
            }

            Pages.PromptEditorActions.DrawPromptWorkspaceToolbar(toolbarRect);
        }

        internal void DrawPromptWorkspaceModuleMetadataRow(Rect rect)
        {
            string kindTag = Owner._promptWorkspaceEditNodeMode
                ? "RimChat_PromptWorkspaceKind_Node".Translate().ToString()
                : "RimChat_PromptWorkspaceKind_Section".Translate().ToString();
            string label = Owner._promptWorkspaceEditNodeMode
                ? PromptUnifiedNodeSchemaCatalog.GetDisplayLabel(Owner._promptWorkspaceSelectedNodeId)
                : (PromptSectionSchemaCatalog.TryGetSection(Owner._promptWorkspaceSelectedSectionId, out PromptSectionSchemaItem section)
                    ? section.GetDisplayLabel()
                    : Owner._promptWorkspaceSelectedSectionId);

            bool oldWrap = Text.WordWrap;
            Text.WordWrap = false;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width - 190f, rect.height),
                $"[{kindTag}] {label}".Truncate(rect.width - 190f));
            Text.WordWrap = oldWrap;

            // Enabled checkbox (RimTalk-style)
            List<PromptSectionLayoutConfig> sectionLayouts = Pages.PromptNodeLayout.GetPromptWorkspaceSectionLayouts();
            List<PromptUnifiedNodeLayoutConfig> nodeLayouts = Pages.PromptNodeLayout.GetPromptWorkspaceNodeLayouts();
            bool isEnabled;
            if (Owner._promptWorkspaceEditNodeMode)
            {
                PromptUnifiedNodeLayoutConfig layout = nodeLayouts.FirstOrDefault(item =>
                    string.Equals(item.NodeId, Owner._promptWorkspaceSelectedNodeId, StringComparison.OrdinalIgnoreCase));
                isEnabled = layout?.Enabled ?? true;
            }
            else
            {
                PromptSectionLayoutConfig layout = sectionLayouts.FirstOrDefault(item =>
                    string.Equals(item.SectionId, Owner._promptWorkspaceSelectedSectionId, StringComparison.OrdinalIgnoreCase));
                isEnabled = layout?.Enabled ?? true;
            }

            float enabledWidth = Mathf.Clamp(rect.width * 0.28f, 100f, 180f);
            Rect enabledRect = new Rect(rect.xMax - enabledWidth, rect.y, enabledWidth, rect.height);
            bool toggled = isEnabled;
            Widgets.CheckboxLabeled(enabledRect, "RimChat_RimTalkCompatEnable".Translate(), ref toggled);
            if (toggled != isEnabled)
            {
                if (Pages.PromptPresetsUi.EnsurePromptWorkspaceEditablePresetForMutation("workspace.module_toggle"))
                {
                    if (Owner._promptWorkspaceEditNodeMode)
                    {
                        PromptUnifiedNodeLayoutConfig layout = nodeLayouts.FirstOrDefault(item =>
                            string.Equals(item.NodeId, Owner._promptWorkspaceSelectedNodeId, StringComparison.OrdinalIgnoreCase));
                        if (layout != null)
                        {
                            layout.Enabled = toggled;
                            Pages.PromptNodeLayout.SavePromptWorkspaceNodeLayouts(nodeLayouts);
                        }
                    }
                    else
                    {
                        PromptSectionLayoutConfig layout = sectionLayouts.FirstOrDefault(item =>
                            string.Equals(item.SectionId, Owner._promptWorkspaceSelectedSectionId, StringComparison.OrdinalIgnoreCase));
                        if (layout != null)
                        {
                            layout.Enabled = toggled;
                            Pages.PromptNodeLayout.SavePromptWorkspaceSectionLayouts(sectionLayouts);
                        }
                    }
                }
            }
        }

        internal void DrawPromptWorkspaceNodeSlotRow(Rect rect)
        {
            List<PromptUnifiedNodeLayoutConfig> nodeLayouts = Pages.PromptNodeLayout.GetPromptWorkspaceNodeLayouts();
            PromptUnifiedNodeLayoutConfig layout = nodeLayouts.FirstOrDefault(item =>
                string.Equals(item.NodeId, Owner._promptWorkspaceSelectedNodeId, StringComparison.OrdinalIgnoreCase));
            if (layout == null)
            {
                return;
            }

            PromptUnifiedNodeSlot currentSlot = layout.GetSlot();
            string slotLabel = RelationsPromptWorkspaceNodeLayout.GetPromptNodeSlotLabel(currentSlot);

            float slotButtonWidth = Mathf.Clamp(rect.width * 0.45f, 140f, 240f);
            Rect slotRect = new Rect(rect.x, rect.y, slotButtonWidth, rect.height);
            if (Widgets.ButtonText(slotRect, "RimChat_PromptNodeSlot".Translate() + ": " + slotLabel))
            {
                Pages.PromptNodeLayout.ShowPromptNodeSlotMenu(nodeLayouts, layout);
            }

            // Order display
            int order = layout.Order;
            float orderLabelWidth = Mathf.Clamp(rect.width * 0.30f, 100f, 160f);
            Rect orderRect = new Rect(slotRect.xMax + 8f, rect.y, orderLabelWidth, rect.height);
            Widgets.Label(orderRect, "RimChat_PromptNodeOrder".Translate() + ": " + order);
        }

        internal void DrawPromptWorkspaceCurrentModuleLabel(Rect rect)
        {
            string label = Owner._promptWorkspaceEditNodeMode
                ? PromptUnifiedNodeSchemaCatalog.GetDisplayLabel(Owner._promptWorkspaceSelectedNodeId)
                : (PromptSectionSchemaCatalog.TryGetSection(Owner._promptWorkspaceSelectedSectionId, out PromptSectionSchemaItem section)
                    ? section.GetDisplayLabel()
                    : Owner._promptWorkspaceSelectedSectionId);

            string kindTag = Owner._promptWorkspaceEditNodeMode
                ? "RimChat_PromptWorkspaceKind_Node".Translate().ToString()
                : "RimChat_PromptWorkspaceKind_Section".Translate().ToString();

            Color oldColor = GUI.color;
            GUI.color = RelationsPromptSectionWorkspace.MetadataTagText;
            TextAnchor oldAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x, rect.y, 60f, rect.height), $"[{kindTag}]");
            GUI.color = RelationsPromptSectionWorkspace.WorkspaceAccentBrightGold;
            Widgets.Label(new Rect(rect.x + 62f, rect.y, rect.width - 62f, rect.height), label);
            GUI.color = oldColor;
            Text.Anchor = oldAnchor;
        }

        internal void DrawPromptWorkspaceValidationStatus(Rect rect, string templateText)
        {
            TemplateVariableValidationContext validationContext = BuildPromptWorkspaceValidationContext();
            UpdatePromptWorkspaceValidationState(templateText, validationContext);
            string statusText = Pages.PromptLegacyValidation.BuildLiveValidationStatusText(Owner._promptWorkspaceValidationResult, templateText);
            Color oldColor = GUI.color;
            GUI.color = Pages.PromptLegacyValidation.ResolveLiveValidationStatusColor(Owner._promptWorkspaceValidationResult, templateText);
            Widgets.Label(rect, statusText);
            GUI.color = oldColor;
        }

        internal TemplateVariableValidationContext BuildPromptWorkspaceValidationContext()
        {
            return Owner._promptWorkspaceEditNodeMode
                ? TemplateVariableValidationContext.ForPromptWorkspaceNode(
                    Pages.PromptWorkspaceBuffers.GetPromptWorkspaceRootChannel(),
                    Pages.PromptWorkbench._workbenchPromptChannel,
                    Owner._promptWorkspaceSelectedNodeId)
                : TemplateVariableValidationContext.ForPromptWorkspaceSection(
                    Pages.PromptWorkspaceBuffers.GetPromptWorkspaceRootChannel(),
                    Pages.PromptWorkbench._workbenchPromptChannel,
                    Owner._promptWorkspaceSelectedSectionId);
        }

        internal void ForcePromptWorkspaceValidationNow()
        {
            string editorText = Owner._promptWorkspaceEditorBuffer ?? string.Empty;
            TemplateVariableValidationContext validationContext = BuildPromptWorkspaceValidationContext();
            UpdatePromptWorkspaceValidationState(editorText, validationContext, force: true);
        }

        internal void UpdatePromptWorkspaceValidationState(
            string templateText,
            TemplateVariableValidationContext validationContext,
            bool force = false)
        {
            string contextSignature = validationContext?.Signature ?? "runtime.default";
            long textVersion = Owner._promptWorkspacePerformance.EditorTextVersion;
            bool contextChanged = !string.Equals(
                contextSignature,
                Owner._promptWorkspacePerformance.LastValidatedContextSignature,
                StringComparison.Ordinal);
            bool textChanged = Owner._promptWorkspacePerformance.LastValidatedTextVersion != textVersion;
            bool hasPendingRequest = Owner._promptWorkspacePerformance.ValidationPending;

            if (!force && !contextChanged && !textChanged && !hasPendingRequest)
            {
                return;
            }

            DateTime now = DateTime.UtcNow;
            bool needsDebounce = (textChanged || hasPendingRequest) && !contextChanged && !force;
            if (needsDebounce && now < Owner._promptWorkspacePerformance.ValidationEarliestRunUtc)
            {
                return;
            }

            Owner._promptWorkspaceValidationResult = string.IsNullOrWhiteSpace(templateText)
                ? new TemplateVariableValidationResult()
                : PromptPersistenceService.Instance.ValidateTemplateVariables(templateText, validationContext);
            Owner._promptWorkspacePerformance.LastValidatedContextSignature = contextSignature;
            Owner._promptWorkspacePerformance.LastValidatedTextVersion = textVersion;
            Owner._promptWorkspacePerformance.ValidationPending = false;
            Owner._promptWorkspacePerformance.ValidationResultVersion++;
        }

        internal void DrawPromptWorkspaceEditModeSwitch(Rect rect)
        {
            float buttonWidth = (rect.width - 6f) * 0.5f;
            Rect sectionRect = new Rect(rect.x, rect.y, buttonWidth, rect.height);
            Rect nodeRect = new Rect(sectionRect.xMax + 6f, rect.y, buttonWidth, rect.height);
            bool hasEditableNodes = Pages.PromptNodeLayout.GetPromptWorkspaceEditableNodes().Count > 0;
            DrawPromptWorkspaceModeButton(sectionRect, false, "RimChat_PromptWorkspaceMode_Sections".Translate().ToString(), true);
            DrawPromptWorkspaceModeButton(nodeRect, true, "RimChat_PromptWorkspaceMode_Nodes".Translate().ToString(), hasEditableNodes);
        }

        internal void DrawPromptWorkspaceModeButton(Rect rect, bool nodeMode, string label, bool active)
        {
            bool selected = Owner._promptWorkspaceEditNodeMode == nodeMode;
            Color selectedColor = active ? RelationsPromptSectionWorkspace.ModeSelectedActiveBg : RelationsPromptSectionWorkspace.ModeSelectedInactiveBg;
            Color normalColor = active ? RelationsPromptSectionWorkspace.ModeNormalActiveBg : RelationsPromptSectionWorkspace.ModeNormalInactiveBg;
            Widgets.DrawBoxSolid(rect, selected ? selectedColor : normalColor);
            Widgets.DrawBox(rect, 1);
            TextAnchor old = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleCenter;
            Color oldColor = GUI.color;
            GUI.color = active ? Color.white : RelationsPromptSectionWorkspace.InactiveText;
            Widgets.Label(rect, label);
            GUI.color = oldColor;
            Text.Anchor = old;
            if (active && Widgets.ButtonInvisible(rect) && Owner._promptWorkspaceEditNodeMode != nodeMode)
            {
                Owner.SchedulePromptWorkspaceNavigation(() =>
                {
                    if (!Pages.PromptWorkspaceBuffers.PersistPromptWorkspaceBufferNow(force: true))
                    {
                        return;
                    }

                    Owner._promptWorkspaceEditNodeMode = nodeMode;
                    if (Owner._promptWorkspaceEditNodeMode)
                    {
                        Pages.PromptWorkspaceBuffers.EnsurePromptWorkspaceNodeLayoutCoverage(Pages.PromptWorkbench._workbenchPromptChannel, Pages.PromptNodeLayout.GetPromptWorkspaceEditableNodes());
                    }

                    Pages.PromptWorkspaceBuffers.EnsurePromptWorkspaceBuffer();
                    Owner.MarkWorkspaceDirty(RelationsPromptSectionWorkspace.WorkspaceDirtyModuleList | RelationsPromptSectionWorkspace.WorkspaceDirtySidePanel);
                    // Note: no need to InvalidatePromptWorkspacePreviewCache here.
                    // Switching edit mode only changes which module is shown in the
                    // editor; the full preview (all sections + nodes) is unchanged.
                });
            }
        }

        internal void DrawPromptWorkspaceNodeSelector(Rect rect)
        {
            List<PromptUnifiedNodeSchemaItem> editableNodes = Pages.PromptNodeLayout.GetPromptWorkspaceEditableNodes();
            if (editableNodes.Count == 0)
            {
                Owner._promptWorkspaceEditNodeMode = false;
                Pages.PromptWorkspaceBuffers.EnsurePromptWorkspaceBuffer();
                return;
            }

            string current = PromptUnifiedNodeSchemaCatalog.GetDisplayLabel(Owner._promptWorkspaceSelectedNodeId);
            Widgets.DrawBoxSolid(rect, RelationsPromptSectionWorkspace.VariablePanelBg);
            Widgets.DrawBox(rect, 1);
            Widgets.Label(new Rect(rect.x + 8f, rect.y, rect.width - 28f, rect.height), current);
            TextAnchor old = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(rect.xMax - 22f, rect.y, 18f, rect.height), "▼");
            Text.Anchor = old;
            if (!Widgets.ButtonInvisible(rect))
            {
                return;
            }

            List<FloatMenuOption> options = editableNodes
                .Select(node => new FloatMenuOption(PromptUnifiedNodeSchemaCatalog.GetDisplayLabel(node.Id), () =>
                {
                    Owner.SchedulePromptWorkspaceNavigation(() =>
                    {
                        if (!Pages.PromptWorkspaceBuffers.PersistPromptWorkspaceBufferNow(force: true))
                        {
                            return;
                        }

                        Owner._promptWorkspaceSelectedNodeId = node.Id;
                        Pages.PromptWorkspaceBuffers.EnsurePromptWorkspaceBuffer();
                        Owner.MarkWorkspaceDirty(RelationsPromptSectionWorkspace.WorkspaceDirtyModuleList | RelationsPromptSectionWorkspace.WorkspaceDirtySidePanel);
                        // Note: no need to InvalidatePromptWorkspacePreviewCache here.
                        // Selecting a different node only changes which module is shown
                        // in the editor; the full preview (all sections + nodes) is unchanged.
                    });
                }))
                .ToList();
            Find.WindowStack.Add(new FloatMenu(options));
        }
}
