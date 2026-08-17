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

internal sealed class RelationsPromptWorkspacePreviewUi
{
    internal readonly RelationsPromptSectionWorkspace Owner;

    internal RelationsPromptWorkspacePreviewUi(RelationsPromptSectionWorkspace owner)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    internal RelationsSettingsPages Pages => Owner.Pages;
    internal RelationsSettings Settings => Owner.Settings;

        internal string DrawPromptWorkspaceEditor(Rect rect, string text)
        {
            Widgets.DrawBoxSolid(rect, RelationsPromptSectionWorkspace.ModuleListBg);
            Rect inner = rect.ContractedBy(6f);
            if (Owner._promptWorkspaceChipEditorDisabledForSession || RelationsRimTalkTabPage.ExceedsChipEditorSoftLimits(text))
            {
                return Owner.DrawPromptWorkspaceLegacyTextArea(inner, text);
            }

            try
            {
                Owner._promptWorkspaceChipEditor ??= new PromptWorkbenchChipEditor(RelationsPromptSectionWorkspace.PromptWorkspaceEditorControlName);
                return Owner._promptWorkspaceChipEditor.Draw(inner, text, ref Owner._promptWorkspaceEditorScroll);
            }
            catch (Exception ex)
            {
                Owner._promptWorkspaceChipEditorDisabledForSession = true;
                Log.Warning($"[RimAI.Relations] Prompt workspace chip editor fallback activated: {ex.GetType().Name}: {ex.Message}");
                return Owner.DrawPromptWorkspaceLegacyTextArea(inner, text);
            }
        }

        internal void DrawPromptWorkspaceSidePanel(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, RelationsPromptSectionWorkspace.PresetPanelBg);
            Rect inner = rect.ContractedBy(8f);
            float buttonWidth = (inner.width - 12f) / 3f;
            Rect previewRect = new Rect(inner.x, inner.y, buttonWidth, 24f);
            Rect fullPreviewRect = new Rect(previewRect.xMax + 6f, inner.y, buttonWidth, 24f);
            Rect varsRect = new Rect(fullPreviewRect.xMax + 6f, inner.y, buttonWidth, 24f);

            Pages.PromptWorkbench.DrawWorkbenchSideButton(previewRect, PromptWorkbenchInfoPanel.Preview, "RimChat_PreviewTitleShort");
            Pages.PromptWorkbench.DrawWorkbenchSideButton(fullPreviewRect, PromptWorkbenchInfoPanel.FullPreview, "RimChat_PromptWorkbench_FullPreviewTab");
            Pages.PromptWorkbench.DrawWorkbenchSideButton(varsRect, PromptWorkbenchInfoPanel.Variables, "RimChat_PromptWorkbench_VariablesTab");

            Rect contentRect = new Rect(inner.x, previewRect.yMax + 6f, inner.width, inner.height - 30f);

            // Variables tab has interactive elements — use direct IMGUI rendering.
            // Preview and FullPreview tabs are read-only — use RenderTexture cache (1 DrawCall).
            if (Pages.PromptWorkbench._workbenchSidePanelTab == PromptWorkbenchInfoPanel.Variables)
            {
                DrawPromptWorkspaceVariables(contentRect);
                return;
            }

            // Preview/FullPreview: read-only content — skip during Layout (only Repaint renders blocks).
            // Must capture BeginScrollView return value so scroll input is preserved across events.
            if (Event.current.type != EventType.Repaint)
            {
                float contentH = Owner._promptWorkspacePreviewRenderer?.CachedContentHeight ?? 200f;
                Owner._promptWorkspacePreviewScroll = GUI.BeginScrollView(contentRect, Owner._promptWorkspacePreviewScroll,
                    new Rect(0f, 0f, contentRect.width - 16f, Mathf.Max(1f, contentH)), false, true);
                GUI.EndScrollView();
                return;
            }

            switch (Pages.PromptWorkbench._workbenchSidePanelTab)
            {
                case PromptWorkbenchInfoPanel.FullPreview:
                    DrawPromptWorkspaceFullPreview(contentRect);
                    break;
                default:
                    DrawPromptWorkspacePreview(contentRect);
                    break;
            }
        }

        internal string BuildSidePanelRenderSignature()
        {
            PromptWorkspaceStructuredPreview preview = Owner.GetPromptWorkspaceStructuredPreview();
            string previewSig = preview?.Signature ?? "null";
            string editMode = Owner._promptWorkspaceEditNodeMode ? "node" : "section";
            string selectedSection = Owner._promptWorkspaceSelectedSectionId ?? string.Empty;
            string selectedNode = Owner._promptWorkspaceSelectedNodeId ?? string.Empty;
            string tab = Pages.PromptWorkbench._workbenchSidePanelTab.ToString();
            // Only include stable state — scroll and editor version change too frequently for pixel cache
            return $"{tab}|{previewSig}|{editMode}|{selectedSection}|{selectedNode}";
        }

        internal void DrawPromptWorkspaceFullPreview(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, RelationsPromptSectionWorkspace.ModuleListBg);
            Rect inner = rect.ContractedBy(6f);
            PromptWorkspaceStructuredPreview preview = Owner.GetPromptWorkspaceStructuredPreview();
            Owner.DrawPromptWorkspaceStructuredPreview(inner, preview);
        }

        internal void DrawPromptWorkspacePreview(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, RelationsPromptSectionWorkspace.ModuleListBg);
            Rect inner = rect.ContractedBy(6f);

            // Single-node preview: show only the selected module's content
            if (Owner._promptWorkspaceEditNodeMode && !string.IsNullOrWhiteSpace(Owner._promptWorkspaceSelectedNodeId))
            {
                DrawPromptWorkspaceSingleNodePreview(inner);
            }
            else if (!Owner._promptWorkspaceEditNodeMode && !string.IsNullOrWhiteSpace(Owner._promptWorkspaceSelectedSectionId))
            {
                DrawPromptWorkspaceSingleSectionPreview(inner);
            }
            else
            {
                PromptWorkspaceStructuredPreview preview = Owner.GetPromptWorkspaceStructuredPreview();
                Owner.DrawPromptWorkspaceStructuredPreview(inner, preview);
            }
        }

        internal void DrawPromptWorkspaceSingleNodePreview(Rect rect)
        {
            string label = PromptUnifiedNodeSchemaCatalog.GetDisplayLabel(Owner._promptWorkspaceSelectedNodeId);
            string text = ResolvePromptWorkspaceNodePreviewContent(Owner._promptWorkspaceSelectedNodeId);

            Color oldColor = GUI.color;
            TextAnchor oldAnchor = Text.Anchor;
            GameFont oldFont = Text.Font;

            // Header
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = RelationsPromptSectionWorkspace.NodeInfoText;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 20f),
                "RimChat_PromptWorkspaceKind_Node".Translate() + ": " + label);
            GUI.color = oldColor;
            Text.Font = oldFont;
            Text.Anchor = oldAnchor;

            // Content
            Rect contentRect = new Rect(rect.x, rect.y + 24f, rect.width, Mathf.Max(24f, rect.height - 24f));
            DrawPromptWorkspacePreviewContentScroll(contentRect, text);
        }

        internal void DrawPromptWorkspaceSingleSectionPreview(Rect rect)
        {
            string label = PromptSectionSchemaCatalog.TryGetSection(Owner._promptWorkspaceSelectedSectionId, out PromptSectionSchemaItem section)
                ? section.GetDisplayLabel()
                : Owner._promptWorkspaceSelectedSectionId;
            string text = ResolvePromptWorkspaceSectionPreviewContent(Owner._promptWorkspaceSelectedSectionId);

            Color oldColor = GUI.color;
            TextAnchor oldAnchor = Text.Anchor;
            GameFont oldFont = Text.Font;

            // Header
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = RelationsPromptSectionWorkspace.WorkspaceAccentBrightGold;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 20f),
                "RimChat_PromptWorkspaceKind_Section".Translate() + ": " + label);
            GUI.color = oldColor;
            Text.Font = oldFont;
            Text.Anchor = oldAnchor;

            // Content
            Rect contentRect = new Rect(rect.x, rect.y + 24f, rect.width, Mathf.Max(24f, rect.height - 24f));
            DrawPromptWorkspacePreviewContentScroll(contentRect, text);
        }

        /// <summary>
        /// Extract rendered node content from the already-built StructuredPreview blocks.
        /// Falls back to raw template text only if the preview has no matching block yet.
        /// </summary>
        internal string ResolvePromptWorkspaceNodePreviewContent(string nodeId)
        {
            PromptWorkspaceStructuredPreview preview = Owner.GetPromptWorkspaceStructuredPreview();
            if (preview?.Blocks != null)
            {
                string normalizedId = PromptUnifiedNodeSchemaCatalog.NormalizeId(nodeId);
                foreach (PromptWorkspacePreviewBlock block in preview.Blocks)
                {
                    if (block?.Kind != PromptWorkspacePreviewBlockKind.Node)
                    {
                        continue;
                    }

                    if (string.Equals(
                        PromptUnifiedNodeSchemaCatalog.NormalizeId(block.NodeId),
                        normalizedId,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return block.Content ?? string.Empty;
                    }
                }
            }

            // Fallback: preview not yet built for this node — return raw template
            return Pages.PromptWorkspaceBuffers.GetPromptWorkspaceNodeText(Pages.PromptWorkbench._workbenchPromptChannel, nodeId);
        }

        /// <summary>
        /// Extract rendered section content from the already-built StructuredPreview subsections.
        /// Falls back to raw template text only if the preview has no matching subsection yet.
        /// </summary>
        internal string ResolvePromptWorkspaceSectionPreviewContent(string sectionId)
        {
            PromptWorkspaceStructuredPreview preview = Owner.GetPromptWorkspaceStructuredPreview();
            if (preview?.Blocks != null)
            {
                string normalizedId = PromptSectionSchemaCatalog.NormalizeSectionId(sectionId);
                foreach (PromptWorkspacePreviewBlock block in preview.Blocks)
                {
                    // Only SectionAggregate blocks carry Subsections with section-level content
                    if (block?.Kind != PromptWorkspacePreviewBlockKind.SectionAggregate)
                    {
                        continue;
                    }

                    if (block.Subsections == null)
                    {
                        continue;
                    }

                    foreach (PromptWorkspacePreviewSubsection subsection in block.Subsections)
                    {
                        if (subsection == null)
                        {
                            continue;
                        }

                        if (string.Equals(
                            PromptSectionSchemaCatalog.NormalizeSectionId(subsection.SectionId),
                            normalizedId,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            return subsection.Content ?? string.Empty;
                        }
                    }
                }
            }

            // Fallback: preview not yet built for this section — return raw template
            return Pages.PromptWorkspaceBuffers.GetPromptWorkspaceSectionText(Pages.PromptWorkbench._workbenchPromptChannel, sectionId);
        }

        internal void DrawPromptWorkspacePreviewContentScroll(Rect rect, string text)
        {
            string source = text ?? string.Empty;
            if (Owner._previewContentScrollStyle == null)
            {
                Owner._previewContentScrollStyle = new GUIStyle(GUI.skin.label)
                {
                    wordWrap = true,
                    richText = false
                };
            }

            float contentWidth = Mathf.Max(1f, rect.width - 16f);
            float contentHeight;
            if (string.Equals(source, Owner._previewContentScrollCachedText, StringComparison.Ordinal) &&
                Mathf.Abs(contentWidth - Owner._previewContentScrollCachedWidth) < 0.5f)
            {
                contentHeight = Owner._previewContentScrollCachedHeight;
            }
            else
            {
                contentHeight = Mathf.Max(rect.height, Owner._previewContentScrollStyle.CalcHeight(new GUIContent(source), contentWidth) + 4f);
                Owner._previewContentScrollCachedText = source;
                Owner._previewContentScrollCachedWidth = contentWidth;
                Owner._previewContentScrollCachedHeight = contentHeight;
            }

            Rect viewRect = new Rect(0f, 0f, contentWidth, contentHeight);
            Owner._promptWorkspacePreviewScroll = new Vector2(
                0f,
                Mathf.Clamp(Owner._promptWorkspacePreviewScroll.y, 0f, Mathf.Max(0f, viewRect.height - rect.height)));
            Owner._promptWorkspacePreviewScroll = GUI.BeginScrollView(rect, Owner._promptWorkspacePreviewScroll, viewRect, false, true);

            Color oldColor = GUI.color;
            GUI.color = RelationsPromptSectionWorkspace.DimmedText;
            Widgets.Label(new Rect(0f, 0f, contentWidth, contentHeight), source);
            GUI.color = oldColor;

            GUI.EndScrollView();
        }

        internal void DrawPromptWorkspaceVariables(Rect rect)
        {
            Pages.VariableBrowser.DrawPromptVariableBrowser(
                rect,
                Owner._promptWorkspaceEditorBuffer,
                entry =>
                {
                    string token = "{{ " + (entry?.Path ?? string.Empty).Trim() + " }}";
                    return Pages.PromptWorkspaceBuffers.TryInsertVariableTokenToPromptWorkspace(token);
                },
                showCustomCrud: true);
        }
}
