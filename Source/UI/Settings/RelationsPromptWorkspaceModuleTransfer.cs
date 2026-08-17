using System;
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.UI;
using RimWorld;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Prompting.Transfer;
using Ustas.RimAI.Communication.Relations.Serialization;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Core.UI;

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class RelationsPromptWorkspaceModuleTransfer
{
    readonly RelationsSettingsPages Pages;

    internal RelationsPromptWorkspaceModuleTransfer(RelationsSettingsPages pages)
    {
        Pages = pages;
    }

    RelationsSettings Settings => Pages.Settings;

        internal void DrawPromptWorkspaceModuleHeaderActions(Rect rect)
        {
            float btnW = 60f;
            float gap = 4f;
            float plusW = 26f;

            Rect exportRect = new Rect(rect.xMax - btnW, rect.y, btnW, rect.height);
            Rect importRect = new Rect(exportRect.xMin - gap - btnW, rect.y, btnW, rect.height);
            Rect newRect = new Rect(importRect.xMin - gap - plusW, rect.y, plusW, rect.height);

            if (Widgets.ButtonText(newRect, "+"))
            {
                Find.WindowStack.Add(new Dialog_PromptModuleCreate(HandleModuleCreate));
            }

            if (Widgets.ButtonText(importRect, "RimChat_ModuleImportBtn".Translate()))
            {
                ShowModuleImportDialog();
            }

            if (Widgets.ButtonText(exportRect, "RimChat_ModuleExportBtn".Translate()))
            {
                ShowModuleExportDialog();
            }
        }

        internal void HandleModuleCreate(string nodeId, string displayName, PromptUnifiedNodeSlot slot)
        {
            if (!Pages.PromptPresetsUi.EnsurePromptWorkspaceEditablePresetForMutation("workspace.module_create"))
            {
                return;
            }

            PromptUnifiedNodeSchemaCatalog.RegisterCustomNode(nodeId, displayName);
            AddCustomNodeRegistrationToCatalog(nodeId, displayName);

            List<PromptUnifiedNodeLayoutConfig> layouts = Pages.PromptNodeLayout.GetPromptWorkspaceNodeLayouts();
            int maxOrder = layouts
                .Where(l => l != null && l.GetSlot() == slot)
                .Select(l => l.Order)
                .DefaultIfEmpty(0)
                .Max();
            layouts.Add(PromptUnifiedNodeLayoutConfig.Create(nodeId, slot, maxOrder + 1, true));
            Pages.PromptNodeLayout.SavePromptWorkspaceNodeLayouts(layouts);

            Settings.SetPromptNodeText(Pages.PromptWorkbench._workbenchPromptChannel, nodeId, string.Empty, persistToFiles: false);
            Settings.ApplyUnifiedCatalogPersistence(persistToFiles: true);

            Pages.PromptWorkspace.SchedulePromptWorkspaceNavigation(() =>
            {
                if (!Pages.PromptWorkspaceBuffers.PersistPromptWorkspaceBufferNow(force: true)) return;
                Pages.PromptWorkspace._promptWorkspaceEditNodeMode = true;
                Pages.PromptWorkspace._promptWorkspaceSelectedNodeId = nodeId;
                Pages.PromptWorkspaceBuffers.EnsurePromptWorkspaceBuffer();
                Pages.PromptWorkspace.InvalidatePromptWorkspacePreviewCache();
            });

            Messages.Message("RimChat_ModuleCreateSuccess".Translate(displayName), MessageTypeDefOf.PositiveEvent, false);
        }

        internal void ShowModuleExportDialog()
        {
            Pages.PromptWorkspaceBuffers.PersistPromptWorkspaceBufferNow(force: true);
            List<PromptWorkbenchModuleItem> modules = Pages.PromptNodeLayout.GetCachedPromptWorkspaceModules();
            if (modules.Count == 0)
            {
                Messages.Message("RimChat_ModuleExportNoSelection".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            Find.WindowStack.Add(new Dialog_PromptModuleMultiExport(modules, HandleMultiExport));
        }

        internal void HandleMultiExport(string filePath, List<PromptWorkbenchModuleItem> selectedModules)
        {
            string channel = Pages.PromptWorkbench._workbenchPromptChannel;
            List<PromptUnifiedNodeLayoutConfig> layouts = Pages.PromptNodeLayout.GetPromptWorkspaceNodeLayouts();
            var bundle = new PromptModuleExportBundle();

            foreach (PromptWorkbenchModuleItem module in selectedModules)
            {
                string content = module.Kind == ModuleKind.Node
                    ? Settings.UnifiedPromptCatalog.ResolveNode(channel, module.Id)
                    : string.Empty;

                PromptUnifiedNodeLayoutConfig layout = layouts.FirstOrDefault(l =>
                    l != null && string.Equals(l.NodeId, module.Id, StringComparison.OrdinalIgnoreCase));

                bundle.Modules.Add(new PromptModuleExportPayload
                {
                    FormatVersion = 1,
                    NodeId = module.Id,
                    DisplayName = module.Label,
                    Slot = layout?.Slot ?? PromptUnifiedNodeSlot.MainChainAfter.ToSerializedValue(),
                    Order = layout?.Order ?? module.DisplayOrder,
                    Enabled = module.Enabled,
                    Content = content
                });
            }

            PromptDomainJsonUtility.WriteToFile(filePath, bundle, prettyPrint: true);
            Messages.Message("RimChat_ModuleExportSuccess".Translate(filePath), MessageTypeDefOf.PositiveEvent, false);
        }

        internal void ShowModuleImportDialog()
        {
            string defaultDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            Find.WindowStack.Add(new Dialog_LoadFile(defaultDir, path =>
            {
                // Try multi-module bundle first
                PromptModuleExportBundle bundle = PromptDomainJsonUtility.LoadSingle<PromptModuleExportBundle>(path);
                if (bundle?.Modules != null && bundle.Modules.Count > 0 &&
                    bundle.Modules.Any(m => m != null && !string.IsNullOrWhiteSpace(m.NodeId)))
                {
                    Find.WindowStack.Add(new Dialog_PromptModuleImportPreview(bundle.Modules, HandleMultiImport));
                    return;
                }

                // Fallback: try single-module payload (backward compatibility)
                PromptModuleExportPayload single = PromptDomainJsonUtility.LoadSingle<PromptModuleExportPayload>(path);
                if (single != null && !string.IsNullOrWhiteSpace(single.NodeId))
                {
                    Find.WindowStack.Add(new Dialog_PromptModuleImportPreview(
                        new List<PromptModuleExportPayload> { single }, HandleMultiImport));
                    return;
                }

                Messages.Message("RimChat_ModuleImportInvalidFile".Translate(), MessageTypeDefOf.RejectInput, false);
            }));
        }

        internal void HandleMultiImport(List<PromptModuleExportPayload> modules)
        {
            if (!Pages.PromptPresetsUi.EnsurePromptWorkspaceEditablePresetForMutation("workspace.module_import"))
            {
                return;
            }

            int count = 0;
            foreach (PromptModuleExportPayload payload in modules)
            {
                if (payload == null || string.IsNullOrWhiteSpace(payload.NodeId))
                {
                    continue;
                }

                ImportSingleModule(payload);
                count++;
            }

            Settings.ApplyUnifiedCatalogPersistence(persistToFiles: true);

            Pages.PromptWorkspace.InvalidatePromptWorkspaceNodeUiCaches();
            Pages.PromptWorkspace.InvalidatePromptWorkspacePreviewCache();
            Pages.PromptWorkspaceBuffers.EnsurePromptWorkspaceBuffer();

            Messages.Message("RimChat_ModuleImportMultiSuccess".Translate(count), MessageTypeDefOf.PositiveEvent, false);
        }

        internal void ImportSingleModule(PromptModuleExportPayload payload)
        {
            string nodeId = payload.NodeId;
            PromptUnifiedNodeSchemaCatalog.RegisterCustomNode(nodeId, payload.DisplayName);
            AddCustomNodeRegistrationToCatalog(nodeId, payload.DisplayName);

            PromptUnifiedNodeSlot slot = PromptUnifiedNodeSlotExtensions.ToPromptUnifiedNodeSlot(payload.Slot);
            List<PromptUnifiedNodeLayoutConfig> layouts = Pages.PromptNodeLayout.GetPromptWorkspaceNodeLayouts();
            PromptUnifiedNodeLayoutConfig existing = layouts.FirstOrDefault(l =>
                l != null && string.Equals(l.NodeId, nodeId, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Slot = slot.ToSerializedValue();
                existing.Order = payload.Order;
                existing.Enabled = payload.Enabled;
            }
            else
            {
                layouts.Add(PromptUnifiedNodeLayoutConfig.Create(nodeId, slot, payload.Order, payload.Enabled));
            }

            Pages.PromptNodeLayout.SavePromptWorkspaceNodeLayouts(layouts);
            Settings.SetPromptNodeText(Pages.PromptWorkbench._workbenchPromptChannel, nodeId, payload.Content ?? string.Empty, persistToFiles: false);
        }

        internal void AddCustomNodeRegistrationToCatalog(string nodeId, string displayName)
        {
            PromptUnifiedChannelConfig channelConfig = Settings.UnifiedPromptCatalog
                .Channels?.FirstOrDefault(c =>
                    c != null && string.Equals(c.PromptChannel, Pages.PromptWorkbench._workbenchPromptChannel, StringComparison.OrdinalIgnoreCase));
            if (channelConfig == null)
            {
                return;
            }

            channelConfig.CustomNodes ??= new List<PromptUnifiedNodeRegistration>();
            PromptUnifiedNodeRegistration existing = channelConfig.CustomNodes.FirstOrDefault(r =>
                r != null && string.Equals(r.NodeId, nodeId, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.DisplayName = displayName;
            }
            else
            {
                channelConfig.CustomNodes.Add(new PromptUnifiedNodeRegistration
                {
                    NodeId = nodeId,
                    DisplayName = displayName
                });
            }
        }

        internal void DeleteCustomModule(string nodeId)
        {
            if (!Pages.PromptPresetsUi.EnsurePromptWorkspaceEditablePresetForMutation("workspace.module_delete"))
            {
                return;
            }

            string channel = Pages.PromptWorkbench._workbenchPromptChannel;
            string displayName = PromptUnifiedNodeSchemaCatalog.GetDisplayLabel(nodeId);

            // Remove node layout
            List<PromptUnifiedNodeLayoutConfig> layouts = Pages.PromptNodeLayout.GetPromptWorkspaceNodeLayouts();
            layouts.RemoveAll(l => l != null && string.Equals(l.NodeId, nodeId, StringComparison.OrdinalIgnoreCase));
            Pages.PromptNodeLayout.SavePromptWorkspaceNodeLayouts(layouts);

            // Remove node content
            Settings.UnifiedPromptCatalog.SetNode(channel, nodeId, string.Empty);

            // Remove custom node registration from catalog
            PromptUnifiedChannelConfig channelConfig = Settings.UnifiedPromptCatalog
                .Channels?.FirstOrDefault(c =>
                    c != null && string.Equals(c.PromptChannel, channel, StringComparison.OrdinalIgnoreCase));
            if (channelConfig?.CustomNodes != null)
            {
                channelConfig.CustomNodes.RemoveAll(r =>
                    r != null && string.Equals(r.NodeId, nodeId, StringComparison.OrdinalIgnoreCase));
            }

            // Unregister from schema catalog
            PromptUnifiedNodeSchemaCatalog.UnregisterCustomNode(nodeId);

            Settings.ApplyUnifiedCatalogPersistence(persistToFiles: true);

            // Reset selection to first section
            Pages.PromptWorkspace._promptWorkspaceEditNodeMode = false;
            Pages.PromptWorkspace._promptWorkspaceSelectedNodeId = string.Empty;
            Pages.PromptWorkspaceBuffers.EnsurePromptWorkspaceBuffer();
            Pages.PromptWorkspace.InvalidatePromptWorkspaceNodeUiCaches();
            Pages.PromptWorkspace.InvalidatePromptWorkspacePreviewCache();

            Messages.Message("RimChat_ModuleDeleteSuccess".Translate(displayName), MessageTypeDefOf.NeutralEvent, false);
        }
    
}
