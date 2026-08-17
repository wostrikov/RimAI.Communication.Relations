using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.UI;
using RimWorld;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Core.UI;

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class RelationsPromptSectionWorkspace
{
    internal readonly RelationsSettingsPages Pages;

    internal RelationsPromptSectionWorkspace(RelationsSettingsPages pages)
    {
        Pages = pages;
    }

    internal RelationsSettings Settings => Pages.Settings;

        internal string _promptWorkspaceSelectedSectionId = "system_rules";
        internal string _promptWorkspaceSelectedNodeId = "fact_grounding";
        internal bool _promptWorkspaceEditNodeMode;
        internal string _promptWorkspaceBufferedChannel = string.Empty;
        internal string _promptWorkspaceBufferedSectionId = string.Empty;
        internal string _promptWorkspaceBufferedNodeId = string.Empty;
        internal bool _promptWorkspaceBufferedNodeMode;
        internal string _promptWorkspaceEditorBuffer = string.Empty;
        internal string _promptWorkspaceLastRenderedEditorTarget = string.Empty;
        internal string _promptWorkspaceLastRenderedEditorText = string.Empty;
        internal Vector2 _promptWorkspaceSectionScroll = Vector2.zero;
        internal Vector2 _promptWorkspaceModuleScroll = Vector2.zero;
        internal Vector2 _promptWorkspaceNodeScroll = Vector2.zero;
        internal Vector2 _promptWorkspaceEditorScroll = Vector2.zero;
        internal Vector2 _promptWorkspacePreviewScroll = Vector2.zero;
        internal Vector2 _promptWorkspaceReportScroll = Vector2.zero;
        internal PromptWorkbenchChannel _promptWorkspacePreviewCachedRoot;
        internal string _promptWorkspacePreviewCachedChannel = string.Empty;
        internal string _promptWorkspacePreviewCachedSignature = string.Empty;
        internal PromptWorkspaceStructuredPreview _promptWorkspacePreviewCachedData;
        internal bool _promptWorkspacePreviewCacheValid;
        internal PromptWorkspaceIncrementalPreviewBuildState _promptWorkspacePreviewBuildState;
        internal bool _promptWorkspaceHasPendingPersist;
        internal bool _promptWorkspaceLastPersistHadMaterialChange;
        internal bool _promptWorkspacePreviewFrozen;
        internal DateTime _promptWorkspaceLastEditUtc = DateTime.MinValue;
        internal TemplateVariableValidationResult _promptWorkspaceValidationResult = new TemplateVariableValidationResult();
        internal readonly PromptWorkspacePerformanceState _promptWorkspacePerformance = new PromptWorkspacePerformanceState();
        internal const double PromptWorkspaceValidationDebounceSeconds = 0.30d;
        internal const int PromptWorkspacePreviewStartDelayFrames = 1;
        internal const float PromptWorkspacePreviewFrameBudgetSeconds = 0.004f;
        internal const string PromptWorkspaceEditorControlName = "RimChat_PromptWorkspaceSectionEditor";
        internal PromptWorkbenchChipEditor _promptWorkspaceChipEditor;
        internal PromptWorkspaceStructuredPreviewRenderer _promptWorkspacePreviewRenderer;
        internal bool _promptWorkspaceChipEditorDisabledForSession;
        // RenderTexture caches for offscreen rendering
        internal CachedRenderTexture _sidePanelContentRtCache;
        internal string _promptWorkspaceDraggingNodeId = string.Empty;
        internal string _promptWorkspaceDropTargetNodeId = string.Empty;
        internal string _promptWorkspaceNodeListCacheChannel = string.Empty;
        internal List<PromptUnifiedNodeSchemaItem> _promptWorkspaceNodeListCache = new List<PromptUnifiedNodeSchemaItem>();
        internal string _promptWorkspaceNodeLayoutCacheChannel = string.Empty;
        internal List<PromptUnifiedNodeLayoutConfig> _promptWorkspaceNodeLayoutCache = new List<PromptUnifiedNodeLayoutConfig>();
        internal string _promptWorkspaceSectionLayoutCacheChannel = string.Empty;
        internal List<PromptSectionLayoutConfig> _promptWorkspaceSectionLayoutCache = new List<PromptSectionLayoutConfig>();
        internal Action _promptWorkspaceDeferredNavigationAction;

        // Dirty-flag infrastructure for cache-invalidation signaling across partial classes.
        // Used by InvalidatePromptWorkspaceNodeUiCaches, InvalidatePromptWorkspacePreviewCache, etc.
        // NOT used for frame-throttle (IMGUI requires rendering every frame to avoid flicker).
        internal int _workspaceDirtyFlags;
        internal const int WorkspaceDirtyPresetPanel = 1 << 0;
        internal const int WorkspaceDirtyModuleList   = 1 << 1;
        internal const int WorkspaceDirtySidePanel    = 1 << 2;
        internal const int WorkspaceDirtyHeader       = 1 << 3;
        internal const int WorkspaceDirtyAll = WorkspaceDirtyPresetPanel | WorkspaceDirtyModuleList | WorkspaceDirtySidePanel | WorkspaceDirtyHeader;

        internal void MarkWorkspaceDirty(int flags)
        {
            _workspaceDirtyFlags |= flags;
            if (flags == 0)
            {
                return;
            }

            _promptWorkspacePerformance.LayoutVersion++;
        }
        internal bool IsWorkspaceDirty(int flag)      { return (_workspaceDirtyFlags & flag) != 0; }
        internal void ClearWorkspaceDirty(int flags)  { _workspaceDirtyFlags &= ~flags; }
        internal void MarkWorkspaceAllDirty()
        {
            _workspaceDirtyFlags = WorkspaceDirtyAll;
            _promptWorkspacePerformance.LayoutVersion++;
        }

        // Cached module list to avoid per-frame rebuilds (A+B+C optimization)
        internal string _promptWorkspaceModuleCacheChannel = string.Empty;
        internal List<PromptWorkbenchModuleItem> _promptWorkspaceModuleCache = new List<PromptWorkbenchModuleItem>();

        // Static color constants to avoid per-frame allocations (D optimization)
        internal static readonly Color WorkspaceBackground = new Color(0.08f, 0.09f, 0.11f);

        // Frame skipping for preview build to reduce CPU usage (A optimization)
        internal int _promptWorkspacePreviewFrameCounter;
        internal const int PromptWorkspacePreviewFrameSkip = 2; // Build every 3rd frame (20fps at 60fps game)
        internal static readonly Color WorkspaceHeaderBg = new Color(0.07f, 0.08f, 0.10f);
        internal static readonly Color WorkspaceAccentGold = new Color(0.95f, 0.74f, 0.26f);
        internal static readonly Color WorkspaceAccentLightGold = new Color(1f, 0.88f, 0.55f);
        internal static readonly Color WorkspaceAccentBrightGold = new Color(0.95f, 0.88f, 0.55f);
        internal static readonly Color ModuleListBg = new Color(0.03f, 0.03f, 0.04f);
        internal static readonly Color EditorPanelBg = new Color(0.06f, 0.07f, 0.09f);
        internal static readonly Color RowHoverBg = new Color(0.18f, 0.18f, 0.20f);
        internal static readonly Color RowSelectedBg = new Color(0.24f, 0.35f, 0.55f);
        internal static readonly Color ModeSelectedActiveBg = new Color(0.24f, 0.35f, 0.55f);
        internal static readonly Color ModeSelectedInactiveBg = new Color(0.16f, 0.16f, 0.16f);
        internal static readonly Color ModeNormalActiveBg = new Color(0.13f, 0.15f, 0.18f);
        internal static readonly Color ModeNormalInactiveBg = new Color(0.10f, 0.10f, 0.10f);
        internal static readonly Color InactiveText = new Color(0.60f, 0.60f, 0.60f);
        internal static readonly Color MetadataTagText = new Color(0.70f, 0.70f, 0.70f);
        internal static readonly Color DropdownBg = new Color(0.25f, 0.18f, 0.08f);
        internal static readonly Color ButtonSelectedBg = new Color(0.45f, 0.33f, 0.15f);
        internal static readonly Color ButtonNormalBg = new Color(0.19f, 0.15f, 0.10f);
        internal static readonly Color PresetPanelBg = new Color(0.09f, 0.10f, 0.12f);
        internal static readonly Color VariablePanelBg = new Color(0.12f, 0.14f, 0.18f);
        internal static readonly Color NodeInfoText = new Color(0.70f, 0.80f, 0.95f);
        internal static readonly Color DimmedText = new Color(0.75f, 0.80f, 0.85f);

        internal bool _pwDiagOnce;

        internal void DrawPromptSectionWorkspace(Rect root)
        {
            if (!_pwDiagOnce) { _pwDiagOnce = true; PreRasterizeWorkbenchFontGlyphs(); }

            Pages.PromptWorkbenchPresets.EnsurePresetStoreReady();
            Pages.PromptWorkspaceBuffers.EnsurePromptWorkspaceSelection();
            TryRunDeferredPromptWorkspaceNavigation();
            Pages.PromptWorkspaceBuffers.TryAutoSavePromptWorkspaceBuffer();

            bool editorHasFocus = GUI.GetNameOfFocusedControl() == PromptWorkspaceEditorControlName;
            if (editorHasFocus && !_promptWorkspacePreviewFrozen) _promptWorkspacePreviewFrozen = true;
            else if (!editorHasFocus && _promptWorkspacePreviewFrozen)
            {
                _promptWorkspacePreviewFrozen = false;
                _promptWorkspacePreviewCacheValid = false;
                _sidePanelContentRtCache?.MarkDirty();
                _promptWorkspacePreviewRenderer?.MarkDirty();
            }

            if (!_promptWorkspacePreviewFrozen || Pages.PromptWorkbench._workbenchSidePanelTab == PromptWorkbenchInfoPanel.Preview)
            {
                bool canBuild = Pages.PromptWorkspaceBuffers.TryRunDeferredPreviewBuild();
                _promptWorkspacePreviewFrameCounter++;
                if (_promptWorkspacePreviewFrameCounter >= PromptWorkspacePreviewFrameSkip)
                    _promptWorkspacePreviewFrameCounter = 0;
                if (canBuild && (_promptWorkspacePreviewFrameCounter == 0 || _promptWorkspacePreviewBuildState != null)
                    && (!_promptWorkspacePreviewCacheValid || _promptWorkspacePreviewBuildState != null))
                    TickPromptWorkspacePreviewBuild(PromptWorkspacePreviewFrameBudgetSeconds);
            }

            Widgets.DrawBoxSolid(root, WorkspaceBackground);
            Rect frame = root.ContractedBy(8f);
            Pages.PromptWorkspaceChrome.DrawPromptWorkspaceHeader(new Rect(frame.x, frame.y, frame.width, 74f));
            Rect bodyRect = new Rect(frame.x, frame.y + 80f, frame.width, frame.height - 80f);
            Pages.PromptWorkspaceChrome.DrawPromptWorkspaceBody(bodyRect);
        }

        internal void SchedulePromptWorkspaceNavigation(Action action)
        {
            if (action == null)
            {
                return;
            }

            _promptWorkspaceDeferredNavigationAction = action;
            GUI.FocusControl(string.Empty);
        }

        internal void TryRunDeferredPromptWorkspaceNavigation()
        {
            if (_promptWorkspaceDeferredNavigationAction == null)
            {
                return;
            }

            Action action = _promptWorkspaceDeferredNavigationAction;
            _promptWorkspaceDeferredNavigationAction = null;
            action.Invoke();
        }

        internal void PreRasterizeWorkbenchFontGlyphs()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                // Module list labels + kind tags
                foreach (var m in Pages.PromptNodeLayout.GetCachedPromptWorkspaceModules())
                    sb.Append(m.Label).Append(' ');
                string[] glyphKeys =
                {
                    "RimChat_PromptWorkspaceKind_Section",
                    "RimChat_PromptWorkspaceKind_Node",
                    "RimChat_Tab_PromptWorkbench",
                    "RimChat_PromptWorkbench_PresetHeader",
                    "RimChat_PromptWorkbench_ModuleHeader",
                    "RimChat_PromptPreset_Create",
                    "RimChat_PromptPreset_Duplicate",
                    "RimChat_Import",
                    "RimChat_Export",
                    "RimChat_PreviewTitleShort",
                    "RimChat_PromptWorkbench_FullPreviewTab",
                    "RimChat_PromptWorkbench_VariablesTab",
                    "RimChat_RimTalkCompatEnable",
                    "RimChat_PromptWorkbench_ChannelDiplomacy",
                    "RimChat_PromptWorkbench_ChannelRpg",
                };
                foreach (string glyphKey in glyphKeys)
                    sb.Append(glyphKey.Translate()).Append(' ');
                // Pre-render all CJK glyphs into the font texture to avoid per-frame rasterization
                GUI.skin.font.RequestCharactersInTexture(sb.ToString());
                Log.Message($"[RimAI.Relations] Font pre-rasterized, {sb.Length} chars");
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimAI.Relations] Font pre-rasterize failed (non-fatal): {ex.Message}");
            }
        }



























        internal GUIStyle _previewContentScrollStyle;
        internal string _previewContentScrollCachedText = string.Empty;
        internal float _previewContentScrollCachedWidth = -1f;
        internal float _previewContentScrollCachedHeight;




























        internal float ResolvePromptWorkspacePresetListHeight(float startY, float bottomY, float panelHeight)
        {
            float available = Mathf.Max(96f, bottomY - startY - 170f);
            float preferred = Mathf.Clamp(panelHeight * 0.32f, 96f, 280f);
            return Mathf.Clamp(preferred, 96f, available);
        }

        internal GUIStyle _legacyTextAreaStyle;
        internal string _legacyTextAreaCachedText = string.Empty;
        internal float _legacyTextAreaCachedWidth = -1f;
        internal float _legacyTextAreaCachedHeight;

        internal string DrawPromptWorkspaceLegacyTextArea(Rect rect, string text)
        {
            string source = text ?? string.Empty;
            if (_legacyTextAreaStyle == null)
            {
                _legacyTextAreaStyle = new GUIStyle(GUI.skin.textArea)
                {
                    wordWrap = true,
                    richText = false
                };
            }

            float contentWidth = Mathf.Max(1f, rect.width - 16f);
            float contentHeight;
            if (string.Equals(source, _legacyTextAreaCachedText, StringComparison.Ordinal) &&
                Mathf.Abs(contentWidth - _legacyTextAreaCachedWidth) < 0.5f)
            {
                contentHeight = _legacyTextAreaCachedHeight;
            }
            else
            {
                contentHeight = Mathf.Max(rect.height, _legacyTextAreaStyle.CalcHeight(new GUIContent(source), contentWidth) + 4f);
                _legacyTextAreaCachedText = source;
                _legacyTextAreaCachedWidth = contentWidth;
                _legacyTextAreaCachedHeight = contentHeight;
            }

            Rect viewRect = new Rect(0f, 0f, contentWidth, contentHeight);
            _promptWorkspaceEditorScroll = new Vector2(
                0f,
                Mathf.Clamp(_promptWorkspaceEditorScroll.y, 0f, Mathf.Max(0f, viewRect.height - rect.height)));
            _promptWorkspaceEditorScroll = GUI.BeginScrollView(rect, _promptWorkspaceEditorScroll, viewRect, false, true);
            GUI.SetNextControlName(PromptWorkspaceEditorControlName);
            string edited = GUI.TextArea(new Rect(0f, 0f, contentWidth, contentHeight), source, _legacyTextAreaStyle);
            GUI.EndScrollView();
            return edited;
        }

        internal void DrawPromptWorkspaceStructuredPreview(Rect rect, PromptWorkspaceStructuredPreview preview)
        {
            _promptWorkspacePreviewRenderer ??= new PromptWorkspaceStructuredPreviewRenderer();
            _promptWorkspacePreviewRenderer.Draw(rect, preview, ref _promptWorkspacePreviewScroll);
        }

        internal PromptWorkspaceStructuredPreview GetPromptWorkspaceStructuredPreview()
        {
            // Pure cache read — Tick is already called once per frame in DrawPromptSectionWorkspace.
            // If channel changed, invalidate so next Tick creates a fresh build state.
            if (_promptWorkspacePreviewCachedRoot != Pages.PromptWorkbench._workbenchChannel ||
                !string.Equals(_promptWorkspacePreviewCachedChannel, Pages.PromptWorkbench._workbenchPromptChannel ?? string.Empty, StringComparison.Ordinal))
            {
                InvalidatePromptWorkspacePreviewCache();
            }

            return _promptWorkspacePreviewCachedData ?? new PromptWorkspaceStructuredPreview();
        }

        internal void TickPromptWorkspacePreviewBuild(float frameBudgetSeconds)
        {
            Pages.PromptWorkspaceBuffers.EnsurePromptWorkspaceSelection();
            EnsurePromptWorkspacePreviewBuildState();
            if (_promptWorkspacePreviewBuildState == null)
            {
                return;
            }

            float start = Time.realtimeSinceStartup;
            do
            {
                PromptPersistenceService.Instance.StepPromptWorkspaceIncrementalPreviewBuild(_promptWorkspacePreviewBuildState);
                SyncPromptWorkspacePreviewCacheFromBuildState();
                if (_promptWorkspacePreviewBuildState == null)
                {
                    return;
                }
            }
            while (Time.realtimeSinceStartup - start < frameBudgetSeconds);
        }

        internal void EnsurePromptWorkspacePreviewBuildState()
        {
            if (_promptWorkspacePreviewCacheValid)
            {
                return;
            }

            if (_promptWorkspacePreviewBuildState != null)
            {
                return;
            }

            _promptWorkspacePreviewBuildState = PromptPersistenceService.Instance.CreatePromptWorkspaceIncrementalPreviewBuild(
                Pages.PromptWorkspaceBuffers.GetPromptWorkspaceRootChannel(),
                Pages.PromptWorkbench._workbenchPromptChannel);
            _promptWorkspacePerformance.PreviewStartPending = false;
            _promptWorkspacePerformance.PreviewStartDelayFrames = 0;
            _promptWorkspacePreviewCachedRoot = Pages.PromptWorkbench._workbenchChannel;
            _promptWorkspacePreviewCachedChannel = Pages.PromptWorkbench._workbenchPromptChannel ?? string.Empty;
            _promptWorkspacePreviewCachedData = _promptWorkspacePreviewBuildState?.Preview ?? new PromptWorkspaceStructuredPreview();
            _promptWorkspacePreviewCachedSignature = _promptWorkspacePreviewCachedData?.Signature ?? string.Empty;
        }

        internal void SyncPromptWorkspacePreviewCacheFromBuildState()
        {
            if (_promptWorkspacePreviewBuildState == null)
            {
                return;
            }

            PromptWorkspaceStructuredPreview preview = _promptWorkspacePreviewBuildState.Preview ?? new PromptWorkspaceStructuredPreview();
            _promptWorkspacePreviewCachedRoot = Pages.PromptWorkbench._workbenchChannel;
            _promptWorkspacePreviewCachedChannel = Pages.PromptWorkbench._workbenchPromptChannel ?? string.Empty;
            _promptWorkspacePreviewCachedData = preview;
            _promptWorkspacePreviewCachedSignature = preview.Signature ?? string.Empty;
            if (preview.Stage == PromptWorkspacePreviewBuildStage.Completed ||
                preview.Stage == PromptWorkspacePreviewBuildStage.Failed)
            {
                _promptWorkspacePreviewCacheValid = true;
                _promptWorkspacePreviewBuildState = null;
            }
        }

        internal void InvalidatePromptWorkspacePreviewCache()
        {
            _promptWorkspacePreviewCacheValid = false;
            _promptWorkspacePreviewCachedChannel = string.Empty;
            _promptWorkspacePreviewCachedSignature = string.Empty;
            _promptWorkspacePreviewCachedData = null;
            _promptWorkspacePreviewBuildState = null;
            _promptWorkspacePerformance.PreviewStartPending = true;
            _promptWorkspacePerformance.PreviewStartDelayFrames = PromptWorkspacePreviewStartDelayFrames;
            MarkWorkspaceDirty(WorkspaceDirtySidePanel);
            _sidePanelContentRtCache?.MarkDirty();
            _promptWorkspacePreviewRenderer?.MarkDirty();
        }

        internal void InvalidatePromptWorkspaceNodeUiCaches()
        {
            _promptWorkspaceNodeListCacheChannel = string.Empty;
            _promptWorkspaceNodeListCache.Clear();
            _promptWorkspaceNodeLayoutCacheChannel = string.Empty;
            _promptWorkspaceNodeLayoutCache.Clear();
            _promptWorkspaceSectionLayoutCacheChannel = string.Empty;
            _promptWorkspaceSectionLayoutCache.Clear();
            _promptWorkspaceModuleCacheChannel = string.Empty;
            _promptWorkspaceModuleCache.Clear();
            MarkWorkspaceDirty(WorkspaceDirtyPresetPanel | WorkspaceDirtyModuleList | WorkspaceDirtySidePanel);
            _sidePanelContentRtCache?.MarkDirty();
        }


    
}
