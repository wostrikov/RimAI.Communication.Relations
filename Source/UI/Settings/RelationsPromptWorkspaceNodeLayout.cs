using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.UI;
using Ustas.RimAI.Core.UI;

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class RelationsPromptWorkspaceNodeLayout
{
    readonly RelationsPromptWorkspaceModuleLayout Modules;
    internal readonly RelationsSettingsPages Pages;

    internal RelationsPromptWorkspaceNodeLayout(RelationsSettingsPages pages)
    {
        Pages = pages;
        Modules = new RelationsPromptWorkspaceModuleLayout(this);
    }

    internal RelationsSettings Settings => Pages.Settings;

        internal void DrawPromptWorkspaceSectionList(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, RelationsPromptSectionWorkspace.ModuleListBg);
            Rect inner = rect.ContractedBy(6f);
            IReadOnlyList<PromptSectionSchemaItem> sections = PromptSectionSchemaCatalog.GetMainChainSections();
            float rowHeight = 30f;
            Rect viewRect = new Rect(0f, 0f, inner.width - 16f, Mathf.Max(inner.height, sections.Count * rowHeight));
            Widgets.BeginScrollView(inner, ref Pages.PromptWorkspace._promptWorkspaceSectionScroll, viewRect);

            for (int i = 0; i < sections.Count; i++)
            {
                PromptSectionSchemaItem section = sections[i];
                Rect rowRect = new Rect(0f, i * rowHeight, viewRect.width, rowHeight - 2f);
                bool selected = string.Equals(Pages.PromptWorkspace._promptWorkspaceSelectedSectionId, section.Id, StringComparison.OrdinalIgnoreCase);
                if (selected)
                {
                    Widgets.DrawBoxSolid(rowRect, RelationsPromptSectionWorkspace.RowSelectedBg);
                }
                else if (Mouse.IsOver(rowRect))
                {
                    Widgets.DrawBoxSolid(rowRect, RelationsPromptSectionWorkspace.RowHoverBg);
                }

                if (Widgets.ButtonInvisible(rowRect))
                {
                    Pages.PromptWorkspace.SchedulePromptWorkspaceNavigation(() => Pages.PromptWorkspaceBuffers.SelectPromptWorkspaceSection(section.Id));
                }

                Widgets.Label(new Rect(rowRect.x + 8f, rowRect.y + 4f, rowRect.width - 16f, rowRect.height - 8f), section.GetDisplayLabel());
            }

            Widgets.EndScrollView();
        }

        internal void DrawPromptWorkspaceNodeLayoutList(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, RelationsPromptSectionWorkspace.ModuleListBg);
            Rect inner = rect.ContractedBy(6f);
            List<PromptUnifiedNodeLayoutConfig> layouts = GetPromptWorkspaceNodeLayouts();
            float rowHeight = 28f;
            float totalRows = layouts.Count;
            Rect viewRect = new Rect(0f, 0f, inner.width - 16f, Mathf.Max(inner.height, totalRows * rowHeight));
            Widgets.BeginScrollView(inner, ref Pages.PromptWorkspace._promptWorkspaceNodeScroll, viewRect);

            float y = 0f;

            List<PromptUnifiedNodeLayoutConfig> orderedItems = layouts
                .OrderBy(item => item.Order)
                .ThenBy(item => item.NodeId, StringComparer.OrdinalIgnoreCase)
                .ToList();
            for (int i = 0; i < orderedItems.Count; i++)
            {
                PromptUnifiedNodeLayoutConfig item = orderedItems[i];
                Rect rowRect = new Rect(0f, y, viewRect.width, rowHeight - 2f);
                bool selected = string.Equals(Pages.PromptWorkspace._promptWorkspaceSelectedNodeId, item.NodeId, StringComparison.OrdinalIgnoreCase);
                if (selected)
                {
                    Widgets.DrawBoxSolid(rowRect, RelationsPromptSectionWorkspace.RowSelectedBg);
                }
                else if (Mouse.IsOver(rowRect))
                {
                    Widgets.DrawBoxSolid(rowRect, RelationsPromptSectionWorkspace.RowHoverBg);
                }

                Rect toggleRect = new Rect(rowRect.x + 4f, rowRect.y + 4f, 18f, rowRect.height - 8f);
                bool enabled = item.Enabled;
                Widgets.Checkbox(toggleRect.position, ref enabled, rowRect.height - 8f, false);
                if (enabled != item.Enabled)
                {
                    if (!Pages.PromptPresetsUi.EnsurePromptWorkspaceEditablePresetForMutation("workspace.node_toggle"))
                    {
                        enabled = item.Enabled;
                        continue;
                    }

                    item.Enabled = enabled;
                    SavePromptWorkspaceNodeLayouts(layouts);
                }

                Rect labelRect = new Rect(toggleRect.xMax + 4f, rowRect.y + 4f, rowRect.width - 106f, rowRect.height - 8f);
                Widgets.Label(labelRect, PromptUnifiedNodeSchemaCatalog.GetDisplayLabel(item.NodeId));
                if (Widgets.ButtonInvisible(labelRect))
                {
                    Pages.PromptWorkspace.SchedulePromptWorkspaceNavigation(() =>
                    {
                        if (!Pages.PromptWorkspaceBuffers.PersistPromptWorkspaceBufferNow(force: true))
                        {
                            return;
                        }

                        Pages.PromptWorkspace._promptWorkspaceSelectedNodeId = item.NodeId;
                        Pages.PromptWorkspaceBuffers.EnsurePromptWorkspaceBuffer();
                        Pages.PromptWorkspace.InvalidatePromptWorkspacePreviewCache();
                    });
                }

                DrawNodeLayoutRowButtons(layouts, item, rowRect);
                HandleNodeLayoutDrag(layouts, item, rowRect);
                y += rowHeight;
            }

            Widgets.EndScrollView();
        }

        internal void DrawNodeLayoutRowButtons(
            List<PromptUnifiedNodeLayoutConfig> layouts,
            PromptUnifiedNodeLayoutConfig item,
            Rect rowRect)
        {
            Rect upRect = new Rect(rowRect.xMax - 50f, rowRect.y + 3f, 20f, rowRect.height - 6f);
            Rect downRect = new Rect(rowRect.xMax - 28f, rowRect.y + 3f, 20f, rowRect.height - 6f);
            if (Widgets.ButtonText(upRect, "▲"))
            {
                MovePromptNodeLayout(layouts, item.NodeId, -1);
            }

            if (Widgets.ButtonText(downRect, "▼"))
            {
                MovePromptNodeLayout(layouts, item.NodeId, 1);
            }
        }

        internal void HandleNodeLayoutDrag(
            List<PromptUnifiedNodeLayoutConfig> layouts,
            PromptUnifiedNodeLayoutConfig item,
            Rect rowRect)

        {

            Modules.HandleNodeLayoutDrag(layouts, item, rowRect);

        }


        internal List<PromptUnifiedNodeLayoutConfig> GetPromptWorkspaceNodeLayouts()
        {
            string channel = string.IsNullOrWhiteSpace(Pages.PromptWorkbench._workbenchPromptChannel)
                ? Pages.PromptWorkspaceBuffers.EnsurePromptWorkspaceSelection()
                : Pages.PromptWorkbench._workbenchPromptChannel;
            if (string.IsNullOrWhiteSpace(channel))
            {
                return new List<PromptUnifiedNodeLayoutConfig>();
            }

            if (string.Equals(Pages.PromptWorkspace._promptWorkspaceNodeLayoutCacheChannel, channel, StringComparison.Ordinal) &&
                Pages.PromptWorkspace._promptWorkspaceNodeLayoutCache != null &&
                Pages.PromptWorkspace._promptWorkspaceNodeLayoutCache.Count > 0)
            {
                return Pages.PromptWorkspace._promptWorkspaceNodeLayoutCache;
            }

            Pages.PromptWorkspace._promptWorkspaceNodeLayoutCacheChannel = channel;
            Pages.PromptWorkspace._promptWorkspaceNodeLayoutCache = Settings.GetPromptNodeLayouts(channel)
                .Select(item => item.Clone())
                .OrderBy(item => item.Order)
                .ThenBy(item => item.NodeId, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return Pages.PromptWorkspace._promptWorkspaceNodeLayoutCache;
        }

        internal List<PromptUnifiedNodeSchemaItem> GetPromptWorkspaceEditableNodes()

        {

            return Modules.GetPromptWorkspaceEditableNodes();

        }


        internal void SavePromptWorkspaceNodeLayouts(List<PromptUnifiedNodeLayoutConfig> layouts)
        {
            Settings.SavePromptNodeLayouts(Pages.PromptWorkbench._workbenchPromptChannel, layouts, persistToFiles: false);
            Pages.PromptWorkspace.InvalidatePromptWorkspaceNodeUiCaches();
            Pages.PromptWorkspace.InvalidatePromptWorkspacePreviewCache();
            Pages.PromptWorkspaceBuffers.EnsurePromptWorkspaceBuffer();
        }

        internal void MovePromptNodeLayout(List<PromptUnifiedNodeLayoutConfig> layouts, string nodeId, int direction)
        {
            if (!Pages.PromptPresetsUi.EnsurePromptWorkspaceEditablePresetForMutation("workspace.node_order"))
            {
                return;
            }

            PromptUnifiedNodeLayoutConfig current = layouts.FirstOrDefault(item =>
                string.Equals(item.NodeId, nodeId, StringComparison.OrdinalIgnoreCase));
            if (current == null)
            {
                return;
            }

            PromptUnifiedNodeSlot slot = current.GetSlot();
            List<PromptUnifiedNodeLayoutConfig> slotItems = layouts
                .Where(item => item.GetSlot() == slot)
                .OrderBy(item => item.Order)
                .ThenBy(item => item.NodeId, StringComparer.OrdinalIgnoreCase)
                .ToList();
            int index = slotItems.FindIndex(item => string.Equals(item.NodeId, nodeId, StringComparison.OrdinalIgnoreCase));
            int target = index + direction;
            if (index < 0 || target < 0 || target >= slotItems.Count)
            {
                return;
            }

            PromptUnifiedNodeLayoutConfig source = slotItems[index];
            PromptUnifiedNodeLayoutConfig destination = slotItems[target];
            int tempOrder = source.Order;
            source.Order = destination.Order;
            destination.Order = tempOrder;
            SavePromptWorkspaceNodeLayouts(layouts);
        }

        internal void MovePromptNodeLayoutToTarget(List<PromptUnifiedNodeLayoutConfig> layouts, string dragNodeId, string targetNodeId)
        {
            if (!Pages.PromptPresetsUi.EnsurePromptWorkspaceEditablePresetForMutation("workspace.node_drag"))
            {
                return;
            }

            PromptUnifiedNodeLayoutConfig drag = layouts.FirstOrDefault(item =>
                string.Equals(item.NodeId, dragNodeId, StringComparison.OrdinalIgnoreCase));
            PromptUnifiedNodeLayoutConfig target = layouts.FirstOrDefault(item =>
                string.Equals(item.NodeId, targetNodeId, StringComparison.OrdinalIgnoreCase));
            if (drag == null || target == null)
            {
                return;
            }

            drag.Slot = target.Slot;
            drag.Order = target.Order;
            MovePromptNodeLayout(layouts, drag.NodeId, 1);
        }

        internal void ShowPromptNodeSlotMenu(List<PromptUnifiedNodeLayoutConfig> layouts, PromptUnifiedNodeLayoutConfig node)
        {
            List<FloatMenuOption> options = Enum.GetValues(typeof(PromptUnifiedNodeSlot))
                .Cast<PromptUnifiedNodeSlot>()
                .Select(slot => new FloatMenuOption(GetPromptNodeSlotLabel(slot), () =>
                {
                    if (!Pages.PromptPresetsUi.EnsurePromptWorkspaceEditablePresetForMutation("workspace.node_slot"))
                    {
                        return;
                    }

                    node.Slot = slot.ToSerializedValue();
                    SavePromptWorkspaceNodeLayouts(layouts);
                }))
                .ToList();
            Find.WindowStack.Add(new FloatMenu(options));
        }

        internal static string GetPromptNodeSlotLabel(PromptUnifiedNodeSlot slot)
        {
            switch (slot)
            {
                case PromptUnifiedNodeSlot.MetadataAfter:
                    return "RimChat_PromptNodeSlot_MetadataAfter".Translate().ToString();
                case PromptUnifiedNodeSlot.MainChainBefore:
                    return "RimChat_PromptNodeSlot_MainChainBefore".Translate().ToString();
                case PromptUnifiedNodeSlot.MainChainAfter:
                    return "RimChat_PromptNodeSlot_MainChainAfter".Translate().ToString();
                case PromptUnifiedNodeSlot.DynamicDataAfter:
                    return "RimChat_PromptNodeSlot_DynamicDataAfter".Translate().ToString();
                case PromptUnifiedNodeSlot.ContractBeforeEnd:
                    return "RimChat_PromptNodeSlot_ContractBeforeEnd".Translate().ToString();
                default:
                    return slot.ToSerializedValue();
            }
        }

        internal void RestorePromptWorkspaceCurrentEntry()
        {
            Pages.PromptEditorActions.TryResetPromptWorkspaceCurrentEntry();
        }

        internal void RestorePromptWorkspaceCurrentChannel()
        {
            if (!Pages.PromptPresetsUi.EnsurePromptWorkspaceEditablePresetForMutation("workspace.reset_channel"))
            {
                return;
            }

            foreach (PromptSectionSchemaItem section in PromptSectionSchemaCatalog.GetMainChainSections())
            {
                Settings.SetPromptSectionText(
                    Pages.PromptWorkbench._workbenchPromptChannel,
                    section.Id,
                    RimTalkPromptEntryDefaultsProvider.ResolveContent(Pages.PromptWorkbench._workbenchPromptChannel, section.Id),
                    persistToFiles: false);
            }
            if (Pages.PromptWorkspace._promptWorkspaceEditNodeMode)
            {
                PromptUnifiedCatalog fallback = PromptUnifiedCatalog.CreateFallback();
                var resetLayouts = new List<PromptUnifiedNodeLayoutConfig>();
                foreach (PromptUnifiedNodeSchemaItem node in PromptUnifiedNodeSchemaCatalog.GetAllowedNodes(Pages.PromptWorkbench._workbenchPromptChannel))
                {
                    Settings.SetPromptNodeText(Pages.PromptWorkbench._workbenchPromptChannel, node.Id, fallback.ResolveNode(Pages.PromptWorkbench._workbenchPromptChannel, node.Id), persistToFiles: false);
                    resetLayouts.Add(PromptUnifiedNodeLayoutDefaults.BuildDefaultLayout(Pages.PromptWorkbench._workbenchPromptChannel, node.Id));
                }

                Settings.SavePromptNodeLayouts(Pages.PromptWorkbench._workbenchPromptChannel, resetLayouts, persistToFiles: false);
            }

            Pages.PromptWorkspaceBuffers.EnsurePromptWorkspaceBuffer();
            Pages.PromptWorkspace.InvalidatePromptWorkspaceNodeUiCaches();
            Pages.PromptWorkspace.InvalidatePromptWorkspacePreviewCache();
        }

        internal List<PromptWorkbenchModuleItem> GetCachedPromptWorkspaceModules()

        {

            return Modules.GetCachedPromptWorkspaceModules();

        }


        internal void DrawPromptWorkspaceModuleList(Rect rect)

        {

            Modules.DrawPromptWorkspaceModuleList(rect);

        }


        internal void MovePromptWorkspaceModule(
            List<PromptWorkbenchModuleItem> modules,
            int selectedIndex,
            int direction,
            List<PromptSectionLayoutConfig> sectionLayouts,
            List<PromptUnifiedNodeLayoutConfig> nodeLayouts)

        {

            Modules.MovePromptWorkspaceModule(modules, selectedIndex, direction, sectionLayouts, nodeLayouts);

        }


        internal void SwapSectionOrder(List<PromptSectionLayoutConfig> layouts, string idA, string idB)
        {
            var a = layouts.FirstOrDefault(s => string.Equals(s.SectionId, idA, StringComparison.OrdinalIgnoreCase));
            var b = layouts.FirstOrDefault(s => string.Equals(s.SectionId, idB, StringComparison.OrdinalIgnoreCase));
            if (a == null || b == null) return;
            int tmp = a.Order;
            a.Order = b.Order;
            b.Order = tmp;
            SavePromptWorkspaceSectionLayouts(layouts);
            Pages.PromptWorkspace.InvalidatePromptWorkspaceNodeUiCaches();
            Pages.PromptWorkspace.InvalidatePromptWorkspacePreviewCache();
        }

        internal void SwapNodeOrder(List<PromptUnifiedNodeLayoutConfig> layouts, string idA, string idB, bool sameSlot)

        {

            Modules.SwapNodeOrder(layouts, idA, idB, sameSlot);

        }


        internal void TogglePromptWorkspaceModuleEnabled(
            PromptWorkbenchModuleItem module,
            bool enabled,
            List<PromptSectionLayoutConfig> sectionLayouts,
            List<PromptUnifiedNodeLayoutConfig> nodeLayouts)

        {

            Modules.TogglePromptWorkspaceModuleEnabled(module, enabled, sectionLayouts, nodeLayouts);

        }


        internal void DrawPromptWorkspaceModuleReorderButtons(
            Rect rowRect,
            int selectedIndex,
            List<PromptWorkbenchModuleItem> modules,
            List<PromptSectionLayoutConfig> sectionLayouts,
            List<PromptUnifiedNodeLayoutConfig> nodeLayouts)

        {

            Modules.DrawPromptWorkspaceModuleReorderButtons(rowRect, selectedIndex, modules, sectionLayouts, nodeLayouts);

        }


        internal List<PromptSectionLayoutConfig> GetPromptWorkspaceSectionLayouts()
        {
            string channel = string.IsNullOrWhiteSpace(Pages.PromptWorkbench._workbenchPromptChannel)
                ? Pages.PromptWorkspaceBuffers.EnsurePromptWorkspaceSelection()
                : Pages.PromptWorkbench._workbenchPromptChannel;
            if (string.IsNullOrWhiteSpace(channel))
            {
                return new List<PromptSectionLayoutConfig>();
            }

            if (string.Equals(Pages.PromptWorkspace._promptWorkspaceSectionLayoutCacheChannel, channel, StringComparison.Ordinal) &&
                Pages.PromptWorkspace._promptWorkspaceSectionLayoutCache != null &&
                Pages.PromptWorkspace._promptWorkspaceSectionLayoutCache.Count > 0)
            {
                return Pages.PromptWorkspace._promptWorkspaceSectionLayoutCache;
            }

            Pages.PromptWorkspace._promptWorkspaceSectionLayoutCacheChannel = channel;
            Pages.PromptWorkspace._promptWorkspaceSectionLayoutCache = Settings.GetPromptSectionLayouts(channel)
                .Select(item => item.Clone())
                .ToList();
            return Pages.PromptWorkspace._promptWorkspaceSectionLayoutCache;
        }

        internal void SavePromptWorkspaceSectionLayouts(List<PromptSectionLayoutConfig> layouts)
        {
            Settings.SavePromptSectionLayouts(Pages.PromptWorkbench._workbenchPromptChannel, layouts, persistToFiles: false);
            Pages.PromptWorkspace.InvalidatePromptWorkspaceNodeUiCaches();
            Pages.PromptWorkspace.InvalidatePromptWorkspacePreviewCache();
            Pages.PromptWorkspaceBuffers.EnsurePromptWorkspaceBuffer();
        }

        internal void DrawSectionLayoutRowButtons(
            List<PromptSectionLayoutConfig> layouts,
            string sectionId,
            Rect rowRect)
        {
            Rect upRect = new Rect(rowRect.xMax - 50f, rowRect.y + 3f, 20f, rowRect.height - 6f);
            Rect downRect = new Rect(rowRect.xMax - 28f, rowRect.y + 3f, 20f, rowRect.height - 6f);
            if (Widgets.ButtonText(upRect, "▲"))
            {
                MovePromptSectionLayout(layouts, sectionId, -1);
            }

            if (Widgets.ButtonText(downRect, "▼"))
            {
                MovePromptSectionLayout(layouts, sectionId, 1);
            }
        }

        internal void MovePromptSectionLayout(List<PromptSectionLayoutConfig> layouts, string sectionId, int direction)
        {
            if (!Pages.PromptPresetsUi.EnsurePromptWorkspaceEditablePresetForMutation("workspace.section_order"))
            {
                return;
            }

            PromptSectionLayoutConfig current = layouts.FirstOrDefault(item =>
                string.Equals(item.SectionId, sectionId, StringComparison.OrdinalIgnoreCase));
            if (current == null)
            {
                return;
            }

            List<PromptSectionLayoutConfig> ordered = layouts
                .OrderBy(item => item.Order)
                .ThenBy(item => item.SectionId, StringComparer.OrdinalIgnoreCase)
                .ToList();
            int index = ordered.FindIndex(item => string.Equals(item.SectionId, sectionId, StringComparison.OrdinalIgnoreCase));
            int target = index + direction;
            if (index < 0 || target < 0 || target >= ordered.Count)
            {
                return;
            }

            PromptSectionLayoutConfig source = ordered[index];
            PromptSectionLayoutConfig destination = ordered[target];
            int tempOrder = source.Order;
            source.Order = destination.Order;
            destination.Order = tempOrder;
            SavePromptWorkspaceSectionLayouts(layouts);
        }
    
}
