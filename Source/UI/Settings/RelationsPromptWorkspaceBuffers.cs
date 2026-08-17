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

internal sealed class RelationsPromptWorkspaceBuffers
{
    internal readonly RelationsPromptSectionWorkspace Owner;

    internal RelationsPromptWorkspaceBuffers(RelationsPromptSectionWorkspace owner)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    internal RelationsSettingsPages Pages => Owner.Pages;
    internal RelationsSettings Settings => Owner.Settings;

        internal void SetPromptWorkspaceRoot(PromptWorkbenchChannel root)
        {
            if (!PersistPromptWorkspaceBufferNow(force: true))
            {
                return;
            }

            Pages.PromptWorkbench._workbenchChannel = root;
            Pages.PromptWorkbench._workbenchPromptChannel = string.Empty;
            Owner.InvalidatePromptWorkspaceNodeUiCaches();
            Owner.InvalidatePromptWorkspacePreviewCache();
            EnsurePromptWorkspaceSelection();
            EnsurePromptWorkspaceNodeLayoutCoverage(Pages.PromptWorkbench._workbenchPromptChannel, Pages.PromptNodeLayout.GetPromptWorkspaceEditableNodes());
        }

        internal RimTalkPromptChannel GetPromptWorkspaceRootChannel()
        {
            return Pages.PromptWorkbench._workbenchChannel == PromptWorkbenchChannel.Rpg
                ? RimTalkPromptChannel.Rpg
                : RimTalkPromptChannel.Diplomacy;
        }

        internal IReadOnlyList<string> GetPromptWorkspaceChannels()
        {
            return PromptSectionSchemaCatalog.GetWorkspaceChannels(GetPromptWorkspaceRootChannel());
        }

        internal string EnsurePromptWorkspaceSelection()
        {
            IReadOnlyList<string> channels = GetPromptWorkspaceChannels();
            string normalizedChannel = RimTalkPromptEntryChannelCatalog.NormalizeLoose(Pages.PromptWorkbench._workbenchPromptChannel);
            if (!channels.Contains(normalizedChannel, StringComparer.Ordinal))
            {
                normalizedChannel = PromptSectionSchemaCatalog.GetDefaultWorkspaceChannel(GetPromptWorkspaceRootChannel());
            }

            Pages.PromptWorkbench._workbenchPromptChannel = normalizedChannel;
            if (!PromptSectionSchemaCatalog.TryGetSection(Owner._promptWorkspaceSelectedSectionId, out PromptSectionSchemaItem _))
            {
                Owner._promptWorkspaceSelectedSectionId = PromptSectionSchemaCatalog.GetMainChainSections()[0].Id;
            }

            List<PromptUnifiedNodeSchemaItem> allowedNodes = PromptUnifiedNodeSchemaCatalog
                .GetAllowedNodes(Pages.PromptWorkbench._workbenchPromptChannel)
                .ToList();
            if (allowedNodes.Count == 0)
            {
                Owner._promptWorkspaceSelectedNodeId = string.Empty;
                Owner._promptWorkspaceEditNodeMode = false;
            }
            else if (!allowedNodes.Any(item =>
                         string.Equals(item.Id, Owner._promptWorkspaceSelectedNodeId, StringComparison.OrdinalIgnoreCase)))
            {
                Owner._promptWorkspaceSelectedNodeId = allowedNodes[0].Id;
            }

            EnsurePromptWorkspaceBuffer();
            return Pages.PromptWorkbench._workbenchPromptChannel;
        }

        internal void EnsurePromptWorkspaceNodeLayoutCoverage(
            string channel,
            IReadOnlyList<PromptUnifiedNodeSchemaItem> allowedNodes)
        {
            if (string.IsNullOrWhiteSpace(channel) || allowedNodes == null || allowedNodes.Count == 0)
            {
                return;
            }

            List<PromptUnifiedNodeLayoutConfig> layouts = Settings.GetPromptNodeLayouts(channel)
                .Select(item => item.Clone())
                .ToList();
            var allowedSet = new HashSet<string>(
                allowedNodes.Select(item => item.Id),
                StringComparer.OrdinalIgnoreCase);
            var existingSet = new HashSet<string>(
                layouts.Where(item => item != null && !string.IsNullOrWhiteSpace(item.NodeId))
                    .Select(item => item.NodeId),
                StringComparer.OrdinalIgnoreCase);
            bool changed = false;
            foreach (PromptUnifiedNodeSchemaItem node in allowedNodes)
            {
                if (existingSet.Contains(node.Id))
                {
                    continue;
                }

                layouts.Add(PromptUnifiedNodeLayoutDefaults.BuildDefaultLayout(channel, node.Id));
                existingSet.Add(node.Id);
                changed = true;
            }

            for (int i = layouts.Count - 1; i >= 0; i--)
            {
                PromptUnifiedNodeLayoutConfig layout = layouts[i];
                if (layout == null || string.IsNullOrWhiteSpace(layout.NodeId) || !allowedSet.Contains(layout.NodeId))
                {
                    layouts.RemoveAt(i);
                    changed = true;
                }
            }

            if (!changed)
            {
                return;
            }

            Settings.SavePromptNodeLayouts(channel, layouts, persistToFiles: false);
            Owner.InvalidatePromptWorkspaceNodeUiCaches();
            Owner.InvalidatePromptWorkspacePreviewCache();
        }

        internal void SetPromptWorkspaceChannel(string channelId)
        {
            if (!PersistPromptWorkspaceBufferNow(force: true))
            {
                return;
            }

            Pages.PromptWorkbench._workbenchPromptChannel = RimTalkPromptEntryChannelCatalog.NormalizeForRoot(channelId, GetPromptWorkspaceRootChannel());
            Owner.InvalidatePromptWorkspaceNodeUiCaches();
            Owner.InvalidatePromptWorkspacePreviewCache();
            EnsurePromptWorkspaceSelection();
            EnsurePromptWorkspaceNodeLayoutCoverage(Pages.PromptWorkbench._workbenchPromptChannel, Pages.PromptNodeLayout.GetPromptWorkspaceEditableNodes());
        }

        internal void SelectPromptWorkspaceSection(string sectionId)
        {
            if (!PersistPromptWorkspaceBufferNow(force: true))
            {
                return;
            }

            Owner._promptWorkspaceEditNodeMode = false;
            Owner._promptWorkspaceSelectedSectionId = PromptSectionSchemaCatalog.NormalizeSectionId(sectionId);
            EnsurePromptWorkspaceBuffer();
            Owner.MarkWorkspaceDirty(RelationsPromptSectionWorkspace.WorkspaceDirtyModuleList);
        }

        internal void EnsurePromptWorkspaceBuffer()
        {
            string targetId = Owner._promptWorkspaceEditNodeMode ? Owner._promptWorkspaceSelectedNodeId : Owner._promptWorkspaceSelectedSectionId;
            if (string.Equals(Owner._promptWorkspaceBufferedChannel, Pages.PromptWorkbench._workbenchPromptChannel, StringComparison.Ordinal) &&
                Owner._promptWorkspaceBufferedNodeMode == Owner._promptWorkspaceEditNodeMode &&
                string.Equals(Owner._promptWorkspaceEditNodeMode ? Owner._promptWorkspaceBufferedNodeId : Owner._promptWorkspaceBufferedSectionId, targetId, StringComparison.Ordinal))
            {
                return;
            }

            Owner._promptWorkspaceBufferedChannel = Pages.PromptWorkbench._workbenchPromptChannel ?? string.Empty;
            Owner._promptWorkspaceBufferedNodeMode = Owner._promptWorkspaceEditNodeMode;
            Owner._promptWorkspaceBufferedSectionId = Owner._promptWorkspaceSelectedSectionId ?? string.Empty;
            Owner._promptWorkspaceBufferedNodeId = Owner._promptWorkspaceSelectedNodeId ?? string.Empty;
            string nextBuffer = Owner._promptWorkspaceEditNodeMode
                ? GetPromptWorkspaceNodeText(Owner._promptWorkspaceBufferedChannel, Owner._promptWorkspaceBufferedNodeId)
                : GetPromptWorkspaceSectionText(Owner._promptWorkspaceBufferedChannel, Owner._promptWorkspaceBufferedSectionId);
            if (!string.Equals(nextBuffer ?? string.Empty, Owner._promptWorkspaceEditorBuffer ?? string.Empty, StringComparison.Ordinal))
            {
                Owner._promptWorkspaceEditorBuffer = nextBuffer ?? string.Empty;
                NotifyPromptWorkspaceEditorBufferRebound();
            }
            else
            {
                Owner._promptWorkspaceEditorBuffer = nextBuffer ?? string.Empty;
            }
        }

        internal string GetPromptWorkspaceCurrentEditorText()
        {
            EnsurePromptWorkspaceBuffer();
            return Owner._promptWorkspaceEditorBuffer ?? string.Empty;
        }

        internal string GetPromptWorkspaceSectionText(string promptChannel, string sectionId)
        {
            RimTalkPromptEntryDefaultsConfig catalog = Settings.GetPromptSectionCatalogClone();
            return catalog.ResolveContent(promptChannel, sectionId) ?? string.Empty;
        }

        internal string GetPromptWorkspaceNodeText(string promptChannel, string nodeId)
        {
            return Settings.ResolvePromptNodeText(promptChannel, nodeId);
        }

        internal void SetPromptWorkspaceCurrentEditorText(string text)
        {
            string next = text ?? string.Empty;
            if (string.Equals(next, Owner._promptWorkspaceEditorBuffer ?? string.Empty, StringComparison.Ordinal))
            {
                return;
            }

            Pages.PromptEditorActions.RecordPromptWorkspaceTextHistoryBeforeMutation(Owner._promptWorkspaceEditorBuffer ?? string.Empty);
            Pages.PromptEditorActions.SetPromptWorkspaceCurrentEditorTextWithoutHistory(next);
        }

        internal void MarkPromptWorkspaceDirty()
        {
            Owner._promptWorkspaceHasPendingPersist = true;
            NotifyPromptWorkspaceEditorTextChanged();
        }

        internal void NotifyPromptWorkspaceEditorTextChanged()
        {
            DateTime now = DateTime.UtcNow;
            Owner._promptWorkspaceLastEditUtc = now;
            Owner._promptWorkspacePerformance.LastEditorTextChangedUtc = now;
            Owner._promptWorkspacePerformance.EditorTextVersion++;
            TryScheduleValidation(immediate: false);
        }

        internal void NotifyPromptWorkspaceEditorBufferRebound()
        {
            DateTime now = DateTime.UtcNow;
            Owner._promptWorkspacePerformance.LastEditorTextChangedUtc = now;
            Owner._promptWorkspacePerformance.EditorTextVersion++;
            TryScheduleValidation(immediate: true);
        }

        internal void TryScheduleValidation(bool immediate)
        {
            Owner._promptWorkspacePerformance.ValidationPending = true;
            Owner._promptWorkspacePerformance.ValidationEarliestRunUtc = immediate
                ? DateTime.UtcNow
                : DateTime.UtcNow.AddSeconds(RelationsPromptSectionWorkspace.PromptWorkspaceValidationDebounceSeconds);
        }

        internal bool TryRunDeferredPreviewBuild()
        {
            if (Owner._promptWorkspacePreviewBuildState != null)
            {
                return true;
            }

            if (Owner._promptWorkspacePreviewCacheValid)
            {
                Owner._promptWorkspacePerformance.PreviewStartPending = false;
                Owner._promptWorkspacePerformance.PreviewStartDelayFrames = 0;
                return false;
            }

            if (!Owner._promptWorkspacePerformance.PreviewStartPending)
            {
                return true;
            }

            if (Owner._promptWorkspacePerformance.PreviewStartDelayFrames > 0)
            {
                Owner._promptWorkspacePerformance.PreviewStartDelayFrames--;
                return false;
            }

            Owner._promptWorkspacePerformance.PreviewStartPending = false;
            return true;
        }

        internal void TryAutoSavePromptWorkspaceBuffer()
        {
            // Unified-only workspace persists to disk only via explicit Save.
        }

        internal bool PersistPromptWorkspaceBufferNow(bool force = false, bool persistToDisk = false)
        {
            if (force)
            {
                EnsurePromptWorkspaceSelection();
                TryScheduleValidation(immediate: true);
            }

            string targetChannel = force ? (Pages.PromptWorkbench._workbenchPromptChannel ?? string.Empty) : (Owner._promptWorkspaceBufferedChannel ?? string.Empty);
            bool targetNodeMode = force ? Owner._promptWorkspaceEditNodeMode : Owner._promptWorkspaceBufferedNodeMode;
            string targetSectionId = force ? (Owner._promptWorkspaceSelectedSectionId ?? string.Empty) : (Owner._promptWorkspaceBufferedSectionId ?? string.Empty);
            string targetNodeId = force ? (Owner._promptWorkspaceSelectedNodeId ?? string.Empty) : (Owner._promptWorkspaceBufferedNodeId ?? string.Empty);
            ApplyRenderedEditorSnapshotToPromptWorkspaceBuffer(
                targetChannel,
                targetNodeMode,
                targetSectionId,
                targetNodeId);
            Pages.PromptEditorActions.CapturePromptWorkspaceLiveEditorText();
            Owner._promptWorkspaceLastPersistHadMaterialChange = false;

            if (!Owner._promptWorkspaceHasPendingPersist)
            {
                if (persistToDisk && Settings.HasPendingUnifiedPromptCatalogChanges())
                {
                    Settings.PersistUnifiedPromptCatalogToCustom();
                }

                return true;
            }

            if (string.IsNullOrWhiteSpace(targetChannel))
            {
                Owner._promptWorkspaceHasPendingPersist = false;
                Owner._promptWorkspaceLastEditUtc = DateTime.MinValue;
                return false;
            }

            string bufferedText = Owner._promptWorkspaceEditorBuffer ?? string.Empty;
            bool changed = false;
            if (targetNodeMode)
            {
                if (!string.IsNullOrWhiteSpace(targetNodeId))
                {
                    string current = GetPromptWorkspaceNodeText(targetChannel, targetNodeId);
                    if (!string.Equals(current ?? string.Empty, bufferedText, StringComparison.Ordinal))
                    {
                        if (!Pages.PromptPresetsUi.EnsurePromptWorkspaceEditablePresetForMutation("workspace.persist_node"))
                        {
                            return false;
                        }

                        Settings.SetPromptNodeText(targetChannel, targetNodeId, bufferedText, persistToDisk);
                        changed = true;
                    }
                }
            }
            else if (!string.IsNullOrWhiteSpace(targetSectionId))
            {
                string current = GetPromptWorkspaceSectionText(targetChannel, targetSectionId);
                if (!string.Equals(current ?? string.Empty, bufferedText, StringComparison.Ordinal))
                {
                    if (!Pages.PromptPresetsUi.EnsurePromptWorkspaceEditablePresetForMutation("workspace.persist_section"))
                    {
                        return false;
                    }

                    Settings.SetPromptSectionText(targetChannel, targetSectionId, bufferedText, persistToDisk);
                    changed = true;
                }

                Owner._promptWorkspaceBufferedChannel = targetChannel;
                Owner._promptWorkspaceBufferedNodeMode = false;
                Owner._promptWorkspaceBufferedSectionId = targetSectionId;
                Owner._promptWorkspaceBufferedNodeId = targetNodeId;
            }

            Owner._promptWorkspaceHasPendingPersist = false;
            Owner._promptWorkspaceLastEditUtc = DateTime.MinValue;
            if (changed)
            {
                if (Pages.PromptWorkbench._promptPresetService != null && Pages.PromptWorkbench._promptPresetStore != null)
                {
                    string syncError = string.Empty;
                    bool syncOk = Pages.PromptWorkbench._promptPresetService.SyncPresetPayloadFromSettings(
                        Settings,
                        Pages.PromptWorkbench._promptPresetStore,
                        Pages.PromptWorkbench._selectedPromptPresetId,
                        out syncError);
                    if (syncOk)
                    {
                        if (persistToDisk)
                        {
                            Pages.PromptWorkbench._promptPresetService.SaveAll(Pages.PromptWorkbench._promptPresetStore);
                        }
                    }
                    else
                    {
                        Log.Warning($"[RimAI.Relations] Prompt workspace preset payload sync failed: {syncError}");
                        Messages.Message(
                            "RimChat_PromptPreset_AutoForkFailed".Translate(syncError ?? "workspace.sync_payload"),
                            MessageTypeDefOf.RejectInput,
                            false);
                        Owner._promptWorkspaceLastPersistHadMaterialChange = false;
                        Owner._promptWorkspaceHasPendingPersist = true;
                        Owner._promptWorkspaceLastEditUtc = DateTime.MinValue;
                        return false;
                    }
                }

                Owner._promptWorkspaceLastPersistHadMaterialChange = true;
                Owner.InvalidatePromptWorkspaceNodeUiCaches();
                Owner.InvalidatePromptWorkspacePreviewCache();
            }

            if (persistToDisk && Settings.HasPendingUnifiedPromptCatalogChanges())
            {
                Settings.PersistUnifiedPromptCatalogToCustom();
            }

            return true;
        }

        internal void CachePromptWorkspaceRenderedEditorText(string text)
        {
            Owner._promptWorkspaceLastRenderedEditorTarget = BuildPromptWorkspaceEditorTargetSignature(
                Pages.PromptWorkbench._workbenchPromptChannel,
                Owner._promptWorkspaceEditNodeMode,
                Owner._promptWorkspaceSelectedSectionId,
                Owner._promptWorkspaceSelectedNodeId);
            Owner._promptWorkspaceLastRenderedEditorText = text ?? string.Empty;
        }

        internal void ApplyRenderedEditorSnapshotToPromptWorkspaceBuffer(
            string promptChannel,
            bool nodeMode,
            string sectionId,
            string nodeId)
        {
            string target = BuildPromptWorkspaceEditorTargetSignature(promptChannel, nodeMode, sectionId, nodeId);
            if (!string.Equals(target, Owner._promptWorkspaceLastRenderedEditorTarget, StringComparison.Ordinal))
            {
                return;
            }

            string renderedText = Owner._promptWorkspaceLastRenderedEditorText ?? string.Empty;
            if (string.Equals(renderedText, Owner._promptWorkspaceEditorBuffer ?? string.Empty, StringComparison.Ordinal))
            {
                return;
            }

            Owner._promptWorkspaceEditorBuffer = renderedText;
            Owner._promptWorkspaceBufferedChannel = promptChannel ?? string.Empty;
            Owner._promptWorkspaceBufferedNodeMode = nodeMode;
            Owner._promptWorkspaceBufferedSectionId = sectionId ?? string.Empty;
            Owner._promptWorkspaceBufferedNodeId = nodeId ?? string.Empty;
            Owner._promptWorkspaceHasPendingPersist = true;
            NotifyPromptWorkspaceEditorTextChanged();
        }

        internal string BuildPromptWorkspaceEditorTargetSignature(
            string promptChannel,
            bool nodeMode,
            string sectionId,
            string nodeId)
        {
            string targetId = nodeMode ? (nodeId ?? string.Empty) : (sectionId ?? string.Empty);
            return $"{promptChannel ?? string.Empty}|{(nodeMode ? "node" : "section")}|{targetId}";
        }

        internal void FlushPromptWorkspaceEdits(bool persistToDisk = false)
        {
            PersistPromptWorkspaceBufferNow(force: false, persistToDisk: persistToDisk);
        }

        /// <summary>
        /// Release all RenderTexture GPU resources used by the prompt workspace panels.
        /// Called when the workbench window closes to prevent memory leaks.
        /// </summary>
        internal void DisposePromptWorkspaceRenderTextures()
        {
            Owner._sidePanelContentRtCache?.Dispose();
            Owner._sidePanelContentRtCache = null;
            Owner._promptWorkspacePreviewRenderer?.Dispose();
        }

        internal bool TryInsertVariableTokenToPromptWorkspace(string token)
        {
            if (!CanInsertVariableTokenToPromptWorkspace())
            {
                return false;
            }

            string normalized = RelationsPromptWorkbenchFramework.NormalizeVariableNameToken(token);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            string current = GetPromptWorkspaceCurrentEditorText();
            if (RelationsRimTalkTemplateEditors.ContainsVariableToken(current, normalized))
            {
                Messages.Message("RimChat_RimTalkVariableAlreadyInTemplate".Translate(), MessageTypeDefOf.NeutralEvent, false);
                return true;
            }

            string wrapped = "{{ " + normalized + " }}";
            string updated = string.IsNullOrWhiteSpace(current)
                ? wrapped
                : current.TrimEnd() + "\n" + wrapped;
            SetPromptWorkspaceCurrentEditorText(updated);
            Messages.Message("RimChat_RimTalkVariableInserted".Translate(wrapped), MessageTypeDefOf.NeutralEvent, false);
            return true;
        }

        internal bool CanInsertVariableTokenToPromptWorkspace()
        {
            EnsurePromptWorkspaceSelection();
            if (string.IsNullOrWhiteSpace(Pages.PromptWorkbench._workbenchPromptChannel))
            {
                return false;
            }

            if (Owner._promptWorkspaceEditNodeMode)
            {
                return PromptUnifiedNodeSchemaCatalog.TryGet(Owner._promptWorkspaceSelectedNodeId, out PromptUnifiedNodeSchemaItem _);
            }

            return PromptSectionSchemaCatalog.TryGetSection(Owner._promptWorkspaceSelectedSectionId, out PromptSectionSchemaItem _);
        }
}
