using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.UI;
using Ustas.RimAI.Core.UI;

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class RelationsPromptWorkspaceModuleLayout
{
    internal readonly RelationsPromptWorkspaceNodeLayout Owner;

    internal RelationsPromptWorkspaceModuleLayout(RelationsPromptWorkspaceNodeLayout owner)
    {
        Owner = owner;
    }


        internal void DrawPromptWorkspaceModuleList(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, RelationsPromptSectionWorkspace.ModuleListBg);
            Rect inner = rect.ContractedBy(6f);

            List<PromptWorkbenchModuleItem> modules = GetCachedPromptWorkspaceModules();
            List<PromptSectionLayoutConfig> sectionLayouts = Owner.GetPromptWorkspaceSectionLayouts();
            List<PromptUnifiedNodeLayoutConfig> nodeLayouts = Owner.GetPromptWorkspaceNodeLayouts();

            const float rowHeight = 25f;
            const float rowStep = 26f;
            const float toolbarH = 22f;

            // Toolbar above the scroll view — reorder buttons for selected module
            if (modules.Count > 0)
            {
                int selIdx = -1;
                for (int i = 0; i < modules.Count; i++)
                {
                    bool s = modules[i].Kind == ModuleKind.Section
                        ? (!Owner.Pages.PromptWorkspace._promptWorkspaceEditNodeMode && string.Equals(Owner.Pages.PromptWorkspace._promptWorkspaceSelectedSectionId, modules[i].Id, StringComparison.OrdinalIgnoreCase))
                        : (Owner.Pages.PromptWorkspace._promptWorkspaceEditNodeMode && string.Equals(Owner.Pages.PromptWorkspace._promptWorkspaceSelectedNodeId, modules[i].Id, StringComparison.OrdinalIgnoreCase));
                    if (s) { selIdx = i; break; }
                }

                Rect toolbarRect = new Rect(inner.x, inner.y, inner.width, toolbarH);
                DrawPromptWorkspaceModuleReorderButtons(toolbarRect, selIdx, modules, sectionLayouts, nodeLayouts);
            }

            // Scroll view — render all modules (no virtual scrolling; avoids index math bugs)
            float totalHeight = modules.Count * rowStep;
            Rect scrollRect = new Rect(inner.x, inner.y + toolbarH + 2f, inner.width, Mathf.Max(1f, inner.height - toolbarH - 2f));
            Rect viewRect = new Rect(0f, 0f, inner.width - 16f, Mathf.Max(scrollRect.height, totalHeight));
            Widgets.BeginScrollView(scrollRect, ref Owner.Pages.PromptWorkspace._promptWorkspaceModuleScroll, viewRect);

            string sectionTag = "RimChat_PromptWorkspaceKind_Section".Translate().ToString();
            string nodeTag = "RimChat_PromptWorkspaceKind_Node".Translate().ToString();

            for (int i = 0; i < modules.Count; i++)
            {
                PromptWorkbenchModuleItem module = modules[i];
                float rowY = i * rowStep;
                float rowWidth = viewRect.width;

                bool selected = module.Kind == ModuleKind.Section
                    ? (!Owner.Pages.PromptWorkspace._promptWorkspaceEditNodeMode && string.Equals(Owner.Pages.PromptWorkspace._promptWorkspaceSelectedSectionId, module.Id, StringComparison.OrdinalIgnoreCase))
                    : (Owner.Pages.PromptWorkspace._promptWorkspaceEditNodeMode && string.Equals(Owner.Pages.PromptWorkspace._promptWorkspaceSelectedNodeId, module.Id, StringComparison.OrdinalIgnoreCase));

                // Selection highlight
                if (selected)
                {
                    Widgets.DrawBoxSolid(new Rect(0f, rowY, rowWidth, rowHeight), RelationsPromptSectionWorkspace.RowSelectedBg);
                }

                // Row layout: [checkbox] [label]
                float checkW = 24f;
                float gap = 3f;
                float labelW = rowWidth - checkW - gap;
                Rect checkRect = new Rect(0f, rowY + 2f, checkW, checkW);
                Rect labelRect = new Rect(checkRect.xMax + gap, rowY, labelW, rowHeight);

                // Enable checkbox
                bool enabled = module.Enabled;
                Widgets.Checkbox(checkRect.position, ref enabled);
                if (enabled != module.Enabled)
                {
                    TogglePromptWorkspaceModuleEnabled(module, enabled, sectionLayouts, nodeLayouts);
                }

                string kindTag = module.Kind == ModuleKind.Section ? sectionTag : nodeTag;
                string displayText = $"{module.Label} [{kindTag}]";
                if (Widgets.ButtonText(labelRect, displayText.Truncate(labelRect.width), false))
                {
                    if (module.Kind == ModuleKind.Section)
                        Owner.Pages.PromptWorkspace.SchedulePromptWorkspaceNavigation(() => Owner.Pages.PromptWorkspaceBuffers.SelectPromptWorkspaceSection(module.Id));
                    else
                        Owner.Pages.PromptWorkspace.SchedulePromptWorkspaceNavigation(() =>
                        {
                            if (!Owner.Pages.PromptWorkspaceBuffers.PersistPromptWorkspaceBufferNow(force: true)) return;
                            Owner.Pages.PromptWorkspace._promptWorkspaceEditNodeMode = true;
                            Owner.Pages.PromptWorkspace._promptWorkspaceSelectedNodeId = module.Id;
                            Owner.Pages.PromptWorkspaceBuffers.EnsurePromptWorkspaceBuffer();
                            Owner.Pages.PromptWorkspace.InvalidatePromptWorkspacePreviewCache();
                        });
                }
            }

            Widgets.EndScrollView();
        }

        internal void DrawPromptWorkspaceModuleReorderButtons(
            Rect rowRect,
            int selectedIndex,
            List<PromptWorkbenchModuleItem> modules,
            List<PromptSectionLayoutConfig> sectionLayouts,
            List<PromptUnifiedNodeLayoutConfig> nodeLayouts)
        {
            if (selectedIndex < 0 || selectedIndex >= modules.Count) return;

            float btnW = 22f;
            float btnH = 22f;
            PromptWorkbenchModuleItem selected = modules[selectedIndex];
            bool isCustom = selected.Kind == ModuleKind.Node &&
                PromptUnifiedNodeSchemaCatalog.IsCustomNode(selected.Id);
            float deleteW = isCustom ? btnW + 4f : 0f;
            float x = rowRect.xMax - btnW * 2f - deleteW - 8f;
            float y = rowRect.y + 2f;

            // Delete button for custom/imported nodes
            if (isCustom)
            {
                Rect deleteRect = new Rect(rowRect.xMax - btnW - 4f, y, btnW, btnH);
                GUI.color = new Color(0.9f, 0.4f, 0.4f);
                if (Widgets.ButtonText(deleteRect, "×"))
                {
                    Owner.Pages.PromptModuleTransfer.DeleteCustomModule(selected.Id);
                    return;
                }
                GUI.color = Color.white;
                TooltipHandler.TipRegion(deleteRect, "RimChat_ModuleDeleteTip".Translate());
            }

            Rect upRect = new Rect(x, y, btnW, btnH);
            Rect downRect = new Rect(x + btnW + 4f, y, btnW, btnH);

            // Label hint
            GUI.color = Color.gray;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(rowRect.x + 4f, rowRect.y + 4f, x - rowRect.x - 12f, rowRect.height - 4f),
                "RimChat_PromptWorkspace_ModuleReorderHint".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            bool canMoveUp = selectedIndex > 0;
            bool canMoveDown = selectedIndex < modules.Count - 1;

            if (!canMoveUp) GUI.enabled = false;
            if (Widgets.ButtonText(upRect, "▲"))
            {
                MovePromptWorkspaceModule(modules, selectedIndex, -1, sectionLayouts, nodeLayouts);
            }
            GUI.enabled = true;

            if (!canMoveDown) GUI.enabled = false;
            if (Widgets.ButtonText(downRect, "▼"))
            {
                MovePromptWorkspaceModule(modules, selectedIndex, 1, sectionLayouts, nodeLayouts);
            }
            GUI.enabled = true;

            TooltipHandler.TipRegion(upRect, "RimChat_PromptWorkspace_ModuleReorderUpTip".Translate());
            TooltipHandler.TipRegion(downRect, "RimChat_PromptWorkspace_ModuleReorderDownTip".Translate());
        }

        internal void MovePromptWorkspaceModule(
            List<PromptWorkbenchModuleItem> modules,
            int selectedIndex,
            int direction,
            List<PromptSectionLayoutConfig> sectionLayouts,
            List<PromptUnifiedNodeLayoutConfig> nodeLayouts)
        {
            if (!Owner.Pages.PromptPresetsUi.EnsurePromptWorkspaceEditablePresetForMutation("workspace.module_order"))
            {
                return;
            }

            PromptWorkbenchModuleItem current = modules[selectedIndex];

            // Scan for the nearest same-kind neighbor in the desired direction
            int targetIdx = selectedIndex + direction;
            while (targetIdx >= 0 && targetIdx < modules.Count)
            {
                PromptWorkbenchModuleItem neighbor = modules[targetIdx];
                if (current.Kind == ModuleKind.Section && neighbor.Kind == ModuleKind.Section)
                {
                    // Swap section orders
                    Owner.SwapSectionOrder(sectionLayouts, current.Id, neighbor.Id);
                    return;
                }
                if (current.Kind == ModuleKind.Node && neighbor.Kind == ModuleKind.Node)
                {
                    // Swap node orders
                    SwapNodeOrder(nodeLayouts, current.Id, neighbor.Id, current.Slot == neighbor.Slot);
                    return;
                }
                targetIdx += direction;
            }
            // No same-kind neighbor in that direction — nothing to do
        }

        internal void TogglePromptWorkspaceModuleEnabled(
            PromptWorkbenchModuleItem module,
            bool enabled,
            List<PromptSectionLayoutConfig> sectionLayouts,
            List<PromptUnifiedNodeLayoutConfig> nodeLayouts)
        {
            if (!Owner.Pages.PromptPresetsUi.EnsurePromptWorkspaceEditablePresetForMutation("workspace.module_toggle"))
            {
                return;
            }

            if (module.Kind == ModuleKind.Section)
            {
                PromptSectionLayoutConfig target = sectionLayouts.FirstOrDefault(s =>
                    string.Equals(s.SectionId, module.Id, StringComparison.OrdinalIgnoreCase));
                if (target == null) return;
                target.Enabled = enabled;
                Owner.SavePromptWorkspaceSectionLayouts(sectionLayouts);
            }
            else
            {
                PromptUnifiedNodeLayoutConfig target = nodeLayouts.FirstOrDefault(n =>
                    string.Equals(n.NodeId, module.Id, StringComparison.OrdinalIgnoreCase));
                if (target == null) return;
                target.Enabled = enabled;
                Owner.SavePromptWorkspaceNodeLayouts(nodeLayouts);
            }

            Owner.Pages.PromptWorkbench.InvalidateWorkbenchEditingChannelConfig();
            Owner.Pages.PromptWorkspace.InvalidatePromptWorkspacePreviewCache();
        }

        internal List<PromptWorkbenchModuleItem> GetCachedPromptWorkspaceModules()
        {
            string channel = string.IsNullOrWhiteSpace(Owner.Pages.PromptWorkbench._workbenchPromptChannel)
                ? Owner.Pages.PromptWorkspaceBuffers.EnsurePromptWorkspaceSelection()
                : Owner.Pages.PromptWorkbench._workbenchPromptChannel;

            if (string.Equals(Owner.Pages.PromptWorkspace._promptWorkspaceModuleCacheChannel, channel, StringComparison.Ordinal) &&
                Owner.Pages.PromptWorkspace._promptWorkspaceModuleCache != null &&
                Owner.Pages.PromptWorkspace._promptWorkspaceModuleCache.Count >= 0)
            {
                return Owner.Pages.PromptWorkspace._promptWorkspaceModuleCache;
            }

            List<PromptSectionLayoutConfig> sectionLayouts = Owner.GetPromptWorkspaceSectionLayouts();
            List<PromptUnifiedNodeLayoutConfig> nodeLayouts = Owner.GetPromptWorkspaceNodeLayouts();
            Owner.Pages.PromptWorkspace._promptWorkspaceModuleCacheChannel = channel;
            Owner.Pages.PromptWorkspace._promptWorkspaceModuleCache = PromptWorkbenchModuleProjection.BuildModules(channel, sectionLayouts, nodeLayouts);
            return Owner.Pages.PromptWorkspace._promptWorkspaceModuleCache;
        }

        internal void SwapNodeOrder(List<PromptUnifiedNodeLayoutConfig> layouts, string idA, string idB, bool sameSlot)
        {
            var a = layouts.FirstOrDefault(n => string.Equals(n.NodeId, idA, StringComparison.OrdinalIgnoreCase));
            var b = layouts.FirstOrDefault(n => string.Equals(n.NodeId, idB, StringComparison.OrdinalIgnoreCase));
            if (a == null || b == null) return;

            if (sameSlot)
            {
                int tmp = a.Order;
                a.Order = b.Order;
                b.Order = tmp;
            }
            else
            {
                string tmpSlot = a.Slot;
                a.Slot = b.Slot;
                b.Slot = tmpSlot;
                int tmpOrder = a.Order;
                a.Order = b.Order;
                b.Order = tmpOrder;
            }
            Owner.SavePromptWorkspaceNodeLayouts(layouts);
            Owner.Pages.PromptWorkspace.InvalidatePromptWorkspaceNodeUiCaches();
            Owner.Pages.PromptWorkspace.InvalidatePromptWorkspacePreviewCache();
        }

        internal List<PromptUnifiedNodeSchemaItem> GetPromptWorkspaceEditableNodes()
        {
            string channel = string.IsNullOrWhiteSpace(Owner.Pages.PromptWorkbench._workbenchPromptChannel)
                ? Owner.Pages.PromptWorkspaceBuffers.EnsurePromptWorkspaceSelection()
                : Owner.Pages.PromptWorkbench._workbenchPromptChannel;
            if (string.IsNullOrWhiteSpace(channel))
            {
                return new List<PromptUnifiedNodeSchemaItem>();
            }

            if (string.Equals(Owner.Pages.PromptWorkspace._promptWorkspaceNodeListCacheChannel, channel, StringComparison.Ordinal) &&
                Owner.Pages.PromptWorkspace._promptWorkspaceNodeListCache != null &&
                Owner.Pages.PromptWorkspace._promptWorkspaceNodeListCache.Count > 0)
            {
                return Owner.Pages.PromptWorkspace._promptWorkspaceNodeListCache;
            }

            List<PromptUnifiedNodeSchemaItem> allowedNodes = PromptUnifiedNodeSchemaCatalog.GetAllowedNodes(channel).ToList();
            List<PromptUnifiedNodeLayoutConfig> layouts = Owner.GetPromptWorkspaceNodeLayouts();
            if (layouts.Count > 0)
            {
                var byId = allowedNodes.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
                var ordered = new List<PromptUnifiedNodeSchemaItem>();
                foreach (PromptUnifiedNodeLayoutConfig layout in layouts)
                {
                    if (layout == null || string.IsNullOrWhiteSpace(layout.NodeId))
                    {
                        continue;
                    }

                    if (byId.TryGetValue(layout.NodeId, out PromptUnifiedNodeSchemaItem matched))
                    {
                        ordered.Add(matched);
                    }
                }

                if (ordered.Count > 0)
                {
                    allowedNodes = ordered;
                }
            }

            Owner.Pages.PromptWorkspace._promptWorkspaceNodeListCacheChannel = channel;
            Owner.Pages.PromptWorkspace._promptWorkspaceNodeListCache = allowedNodes;
            return Owner.Pages.PromptWorkspace._promptWorkspaceNodeListCache;
        }

        internal void HandleNodeLayoutDrag(
            List<PromptUnifiedNodeLayoutConfig> layouts,
            PromptUnifiedNodeLayoutConfig item,
            Rect rowRect)
        {
            Event evt = Event.current;
            if (evt == null)
            {
                return;
            }

            if (evt.type == EventType.MouseDown && evt.button == 0 && rowRect.Contains(evt.mousePosition))
            {
                Owner.Pages.PromptWorkspace._promptWorkspaceDraggingNodeId = item.NodeId;
            }

            if (evt.type == EventType.MouseDrag &&
                !string.IsNullOrWhiteSpace(Owner.Pages.PromptWorkspace._promptWorkspaceDraggingNodeId) &&
                rowRect.Contains(evt.mousePosition))
            {
                Owner.Pages.PromptWorkspace._promptWorkspaceDropTargetNodeId = item.NodeId;
            }

            if (evt.type == EventType.MouseUp && evt.button == 0)
            {
                if (!string.IsNullOrWhiteSpace(Owner.Pages.PromptWorkspace._promptWorkspaceDraggingNodeId) &&
                    !string.IsNullOrWhiteSpace(Owner.Pages.PromptWorkspace._promptWorkspaceDropTargetNodeId) &&
                    !string.Equals(Owner.Pages.PromptWorkspace._promptWorkspaceDraggingNodeId, Owner.Pages.PromptWorkspace._promptWorkspaceDropTargetNodeId, StringComparison.OrdinalIgnoreCase))
                {
                    Owner.MovePromptNodeLayoutToTarget(layouts, Owner.Pages.PromptWorkspace._promptWorkspaceDraggingNodeId, Owner.Pages.PromptWorkspace._promptWorkspaceDropTargetNodeId);
                }

                Owner.Pages.PromptWorkspace._promptWorkspaceDraggingNodeId = string.Empty;
                Owner.Pages.PromptWorkspace._promptWorkspaceDropTargetNodeId = string.Empty;
            }
        }
}
