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

internal sealed class RelationsRimTalkEntryList
{
    internal readonly RelationsRimTalkTabPage Owner;

    internal RelationsRimTalkEntryList(RelationsRimTalkTabPage owner)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    internal RelationsSettingsPages Pages => Owner.Pages;
    internal RelationsSettings Settings => Owner.Settings;

        internal void DrawRimTalkPromptEntryList(Rect rect, RimTalkChannelCompatConfig config)
        {
            const float buttonSize = 22f;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width - (buttonSize * 3f + 12f), 22f), "RimChat_RimTalkEntryListTitle".Translate());
            Rect duplicateRect = new Rect(rect.xMax - buttonSize, rect.y, buttonSize, buttonSize);
            Rect addRect = new Rect(duplicateRect.x - buttonSize - 4f, rect.y, buttonSize, buttonSize);
            Rect restoreRect = new Rect(addRect.x - buttonSize - 4f, rect.y, buttonSize, buttonSize);
            string scopedPromptChannel = GetScopedPromptChannelOrEmpty();
            List<int> visibleIndices = CollectVisiblePromptEntryIndices(config, scopedPromptChannel);
            EnsureSelectedEntryInVisibleScope(config, visibleIndices);
            bool dirty = false;
            if (Widgets.ButtonText(restoreRect, "↺"))
            {
                if (Owner.TryRestoreDefaultEntriesForScopedChannel(config, scopedPromptChannel))
                {
                    visibleIndices = CollectVisiblePromptEntryIndices(config, scopedPromptChannel);
                    EnsureSelectedEntryInVisibleScope(config, visibleIndices);
                    dirty = true;
                }
            }

            TooltipHandler.TipRegion(restoreRect, "RimChat_RimTalkEntryRestoreDefaultsTooltip".Translate());
            if (Widgets.ButtonText(addRect, "+"))
            {
                var created = new RimTalkPromptEntryConfig
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = "RimChat_RimTalkEntryDefaultName".Translate(),
                    Role = "System",
                    CustomRole = string.Empty,
                    Position = "Relative",
                    InChatDepth = 0,
                    Enabled = true,
                    PromptChannel = ResolveEntryCreationPromptChannel(scopedPromptChannel),
                    Content = string.Empty
                };

                int insertIndex = config.PromptEntries.Count;
                if (!string.IsNullOrWhiteSpace(Owner._rimTalkSelectedEntryId))
                {
                    int currentIndex = config.PromptEntries.FindIndex(entry =>
                        entry != null && string.Equals(entry.Id, Owner._rimTalkSelectedEntryId, StringComparison.Ordinal));
                    if (currentIndex >= 0)
                    {
                        insertIndex = currentIndex + 1;
                    }
                }
                else if (visibleIndices.Count > 0)
                {
                    insertIndex = visibleIndices[visibleIndices.Count - 1] + 1;
                }

                config.PromptEntries.Insert(insertIndex, created);
                Owner._rimTalkSelectedEntryId = created.Id;
                Owner._rimTalkDepthBuffer = created.InChatDepth.ToString();
                visibleIndices = CollectVisiblePromptEntryIndices(config, scopedPromptChannel);
                int visibleInsertIndex = FindVisibleIndexByEntryId(config, visibleIndices, created.Id);
                Owner._rimTalkEntryListScroll = new Vector2(0f, Mathf.Max(0f, visibleInsertIndex * 25f - 40f));
                dirty = true;
            }

            TooltipHandler.TipRegion(addRect, "RimChat_RimTalkEntryAddTooltip".Translate());
            int selectedFullIndex = config.PromptEntries.FindIndex(entry =>
                entry != null && string.Equals(entry.Id, Owner._rimTalkSelectedEntryId, StringComparison.Ordinal));
            bool hasVisibleSelection = selectedFullIndex >= 0 && visibleIndices.Contains(selectedFullIndex);
            RimTalkPromptEntryConfig selectedForDuplicate = hasVisibleSelection ? config.PromptEntries[selectedFullIndex] : null;
            if (hasVisibleSelection && selectedForDuplicate != null && Widgets.ButtonText(duplicateRect, "C"))
            {
                RimTalkPromptEntryConfig duplicated = selectedForDuplicate.Clone();
                duplicated.Id = Guid.NewGuid().ToString("N");
                duplicated.SectionId = string.Empty;
                duplicated.Name = RelationsRimTalkTabPage.NextPromptEntryName(config, selectedForDuplicate.Name);
                duplicated.PromptChannel = ResolveEntryCreationPromptChannel(scopedPromptChannel);
                int selectedEntryIndex = selectedFullIndex;
                int insertIndex = selectedEntryIndex >= 0 ? selectedEntryIndex + 1 : config.PromptEntries.Count;
                config.PromptEntries.Insert(insertIndex, duplicated);
                Owner._rimTalkSelectedEntryId = duplicated.Id;
                Owner._rimTalkDepthBuffer = duplicated.InChatDepth.ToString();
                visibleIndices = CollectVisiblePromptEntryIndices(config, scopedPromptChannel);
                int visibleInsertIndex = FindVisibleIndexByEntryId(config, visibleIndices, duplicated.Id);
                Owner._rimTalkEntryListScroll = new Vector2(0f, Mathf.Max(0f, visibleInsertIndex * 25f - 40f));
                dirty = true;
            }

            TooltipHandler.TipRegion(duplicateRect, "RimChat_RimTalkEntryDuplicateTooltip".Translate());
            const float rowHeight = 25f;
            const float rowStep = 26f;
            Rect listRect = new Rect(rect.x, rect.y + 24f, rect.width, rect.height - 52f);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, Mathf.Max(listRect.height, visibleIndices.Count * rowStep));
            float rowButtonX = viewRect.width - buttonSize - 2f;
            Widgets.BeginScrollView(listRect, ref Owner._rimTalkEntryListScroll, viewRect);
            float rowY = 0f;
            for (int i = 0; i < visibleIndices.Count; i++)
            {
                int entryIndex = visibleIndices[i];
                RimTalkPromptEntryConfig entry = config.PromptEntries[entryIndex];
                if (entry == null)
                {
                    continue;
                }

                Rect rowRect = new Rect(0f, rowY, viewRect.width, rowHeight);
                bool isSelected = string.Equals(entry.Id, Owner._rimTalkSelectedEntryId, StringComparison.Ordinal);
                if (isSelected)
                {
                    Widgets.DrawHighlight(rowRect);
                }
                else if (Mouse.IsOver(rowRect))
                {
                    Widgets.DrawBoxSolid(rowRect, new Color(0.18f, 0.18f, 0.2f));
                }

                bool enabled = entry.Enabled;
                Widgets.Checkbox(new Vector2(4f, rowY + 4f), ref enabled, 16f);
                if (enabled != entry.Enabled)
                {
                    entry.Enabled = enabled;
                    dirty = true;
                }

                Rect selectRect = new Rect(24f, rowY, viewRect.width - 24f - buttonSize - 6f, rowHeight);
                if (Widgets.ButtonInvisible(selectRect))
                {
                    Owner._rimTalkSelectedEntryId = entry.Id;
                    Owner._rimTalkDepthBuffer = entry.InChatDepth.ToString();
                }

                string title = string.IsNullOrWhiteSpace(entry.Name)
                    ? "RimChat_RimTalkEntryDefaultName".Translate().ToString()
                    : entry.Name;
                string channelLabel = RelationsRimTalkTabPage.GetRimTalkPromptChannelLabel(entry.PromptChannel);
                string rowText = $"{title} [{channelLabel}]";
                bool oldWordWrap = Text.WordWrap;
                Text.WordWrap = false;
                Rect titleRect = new Rect(24f, rowY + 1f, viewRect.width - 24f - buttonSize - 8f, rowHeight - 2f);
                Widgets.Label(titleRect, rowText.Truncate(titleRect.width));
                Text.WordWrap = oldWordWrap;

                Rect deleteRect = new Rect(rowButtonX, rowY + 2f, buttonSize, buttonSize);
                bool canDeleteEntry = RelationsRimTalkTabPage.IsDefaultPromptEntry(entry);
                if (canDeleteEntry)
                {
                    GUI.color = new Color(1f, 0.4f, 0.4f);
                    if (Widgets.ButtonText(deleteRect, "×"))
                    {
                        bool deletingSelected = string.Equals(Owner._rimTalkSelectedEntryId, entry.Id, StringComparison.Ordinal);
                        config.PromptEntries.RemoveAt(entryIndex);
                        visibleIndices = CollectVisiblePromptEntryIndices(config, scopedPromptChannel);
                        if (deletingSelected)
                        {
                            EnsureSelectedEntryInVisibleScope(config, visibleIndices);
                        }

                        dirty = true;
                        GUI.color = Color.white;
                        continue;
                    }
                }

                GUI.color = Color.white;
                string tip = title + "\n" + channelLabel;
                TooltipHandler.TipRegion(rowRect, tip);
                rowY += rowStep;
            }

            Widgets.EndScrollView();

            selectedFullIndex = config.PromptEntries.FindIndex(entry =>
                entry != null && string.Equals(entry.Id, Owner._rimTalkSelectedEntryId, StringComparison.Ordinal));
            int selectedVisibleIndex = selectedFullIndex >= 0 ? visibleIndices.IndexOf(selectedFullIndex) : -1;
            float buttonWidth = (rect.width - 4f) * 0.5f;
            Rect upRect = new Rect(rect.x, rect.yMax - 24f, buttonWidth, 24f);
            Rect downRect = new Rect(upRect.xMax + 4f, rect.yMax - 24f, buttonWidth, 24f);
            if (selectedVisibleIndex > 0)
            {
                if (Widgets.ButtonText(upRect, "▲"))
                {
                    int currentIndex = visibleIndices[selectedVisibleIndex];
                    int targetIndex = visibleIndices[selectedVisibleIndex - 1];
                    RimTalkPromptEntryConfig item = config.PromptEntries[currentIndex];
                    config.PromptEntries.RemoveAt(currentIndex);
                    if (targetIndex > currentIndex)
                    {
                        targetIndex--;
                    }

                    config.PromptEntries.Insert(targetIndex, item);
                    dirty = true;
                }
            }
            else
            {
                GUI.enabled = false;
                Widgets.ButtonText(upRect, "▲");
                GUI.enabled = true;
            }

            if (selectedVisibleIndex >= 0 && selectedVisibleIndex < visibleIndices.Count - 1)
            {
                if (Widgets.ButtonText(downRect, "▼"))
                {
                    int currentIndex = visibleIndices[selectedVisibleIndex];
                    int targetIndex = visibleIndices[selectedVisibleIndex + 1];
                    RimTalkPromptEntryConfig item = config.PromptEntries[currentIndex];
                    config.PromptEntries.RemoveAt(currentIndex);
                    if (targetIndex > currentIndex)
                    {
                        targetIndex--;
                    }

                    int insertIndex = Mathf.Min(config.PromptEntries.Count, targetIndex + 1);
                    config.PromptEntries.Insert(insertIndex, item);
                    dirty = true;
                }
            }
            else
            {
                GUI.enabled = false;
                Widgets.ButtonText(downRect, "▼");
                GUI.enabled = true;
            }

            if (dirty)
            {
                Settings.SetRimTalkChannelConfig(Owner._rimTalkEditorChannel, config);
                Owner.EnsureRimTalkEntrySelection(config);
            }
        }

        internal int FindVisibleIndexByEntryId(
            RimTalkChannelCompatConfig config,
            IReadOnlyList<int> visibleIndices,
            string entryId)
        {
            if (config?.PromptEntries == null || visibleIndices == null || string.IsNullOrWhiteSpace(entryId))
            {
                return 0;
            }

            for (int i = 0; i < visibleIndices.Count; i++)
            {
                int index = visibleIndices[i];
                RimTalkPromptEntryConfig entry = config.PromptEntries[index];
                if (entry != null && string.Equals(entry.Id, entryId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return 0;
        }

        internal string GetScopedPromptChannelOrEmpty()
        {
            if (!Pages.PromptWorkbench.IsEntryDrivenWorkbenchChannelActive())
            {
                return string.Empty;
            }

            string selected = Pages.PromptWorkbench.EnsureWorkbenchPromptChannelSelection();
            return RimTalkPromptEntryChannelCatalog.NormalizeForRoot(selected, Owner._rimTalkEditorChannel);
        }

        internal List<int> CollectVisiblePromptEntryIndices(RimTalkChannelCompatConfig config, string scopedPromptChannel)
        {
            var result = new List<int>();
            if (config?.PromptEntries == null)
            {
                return result;
            }

            bool scoped = !string.IsNullOrWhiteSpace(scopedPromptChannel);
            for (int i = 0; i < config.PromptEntries.Count; i++)
            {
                RimTalkPromptEntryConfig entry = config.PromptEntries[i];
                if (entry == null)
                {
                    continue;
                }

                if (scoped)
                {
                    string normalizedEntryChannel = RimTalkPromptEntryChannelCatalog.NormalizeForRoot(entry.PromptChannel, Owner._rimTalkEditorChannel);
                    if (!string.Equals(normalizedEntryChannel, scopedPromptChannel, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                result.Add(i);
            }

            return result;
        }

        internal void EnsureSelectedEntryInVisibleScope(RimTalkChannelCompatConfig config, IReadOnlyList<int> visibleIndices)
        {
            if (config?.PromptEntries == null)
            {
                Owner._rimTalkSelectedEntryId = string.Empty;
                Owner._rimTalkDepthBuffer = string.Empty;
                return;
            }

            if (!string.IsNullOrWhiteSpace(Owner._rimTalkSelectedEntryId))
            {
                for (int i = 0; i < visibleIndices.Count; i++)
                {
                    RimTalkPromptEntryConfig current = config.PromptEntries[visibleIndices[i]];
                    if (current != null && string.Equals(current.Id, Owner._rimTalkSelectedEntryId, StringComparison.Ordinal))
                    {
                        return;
                    }
                }
            }

            if (visibleIndices.Count == 0)
            {
                Owner._rimTalkSelectedEntryId = string.Empty;
                Owner._rimTalkDepthBuffer = string.Empty;
                return;
            }

            RimTalkPromptEntryConfig first = config.PromptEntries[visibleIndices[0]];
            Owner._rimTalkSelectedEntryId = first?.Id ?? string.Empty;
            Owner._rimTalkDepthBuffer = first?.InChatDepth.ToString() ?? string.Empty;
        }

        internal string ResolveEntryCreationPromptChannel(string scopedPromptChannel)
        {
            if (!string.IsNullOrWhiteSpace(scopedPromptChannel))
            {
                return scopedPromptChannel;
            }

            return RimTalkPromptEntryChannelCatalog.GetDefaultChannel(Owner._rimTalkEditorChannel);
        }
}
