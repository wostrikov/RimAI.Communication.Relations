using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Ustas.RimAI.Communication.Relations.Persistence;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Context;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.UI;
using Ustas.RimAI.Core.UI;

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class RelationsPromptEnvironmentSceneEditors
{
    internal readonly RelationsPromptEnvironmentEditors Owner;

    internal RelationsPromptEnvironmentSceneEditors(RelationsPromptEnvironmentEditors owner)
    {
        Owner = owner;
    }


        internal bool DrawEnvironmentWorldviewCard(Rect rect, EnvironmentPromptConfig envConfig)
        {
            Rect contentRect = Owner.DrawEnvironmentCard(rect, "RimChat_EnvironmentWorldviewLabel");
            bool changed = false;
            bool oldEnabled = envConfig.Worldview.Enabled;

            Widgets.CheckboxLabeled(
                new Rect(contentRect.x, contentRect.y, contentRect.width, 24f),
                "RimChat_EnvironmentWorldviewEnabled".Translate(),
                ref envConfig.Worldview.Enabled);
            changed |= oldEnabled != envConfig.Worldview.Enabled;

            int limit = Mathf.Max(Owner.Settings.MaxSystemPromptLength, 500);
            int length = envConfig.Worldview.Content?.Length ?? 0;
            GUI.color = length > limit * 0.9f ? Color.yellow : Color.gray;
            Widgets.Label(
                new Rect(contentRect.x, contentRect.y + 28f, contentRect.width, 18f),
                "RimChat_EnvironmentCharCount".Translate(length, limit));
            GUI.color = Color.white;

            if (!envConfig.Worldview.Enabled)
            {
                return changed;
            }

            Rect textRect = new Rect(contentRect.x, contentRect.y + 48f, contentRect.width, contentRect.height - 52f);
            Widgets.DrawBoxSolid(textRect, new Color(0.05f, 0.05f, 0.07f));
            string oldText = envConfig.Worldview.Content ?? string.Empty;
            string newText = Widgets.TextArea(textRect.ContractedBy(4f), oldText);
            if (newText.Length > limit)
            {
                newText = newText.Substring(0, limit);
            }

            if (!string.Equals(oldText, newText, StringComparison.Ordinal))
            {
                envConfig.Worldview.Content = newText;
                changed = true;
            }

            return changed;
        }

        internal bool DrawEnvironmentSceneSystemCard(Rect rect, EnvironmentPromptConfig envConfig)
        {
            Rect contentRect = Owner.DrawEnvironmentCard(rect, "RimChat_EnvironmentSceneSystemLabel");
            bool changed = false;
            float y = contentRect.y;

            bool oldEnabled = envConfig.SceneSystem.Enabled;
            Widgets.CheckboxLabeled(
                new Rect(contentRect.x, y, contentRect.width, 24f),
                "RimChat_EnvironmentSceneSystemEnabled".Translate(),
                ref envConfig.SceneSystem.Enabled);
            changed |= oldEnabled != envConfig.SceneSystem.Enabled;
            y += 26f;

            if (!envConfig.SceneSystem.Enabled)
            {
                return changed;
            }

            bool oldPreset = envConfig.SceneSystem.PresetTagsEnabled;
            Widgets.CheckboxLabeled(
                new Rect(contentRect.x, y, contentRect.width, 24f),
                "RimChat_EnvironmentPresetTagsEnabled".Translate(),
                ref envConfig.SceneSystem.PresetTagsEnabled);
            changed |= oldPreset != envConfig.SceneSystem.PresetTagsEnabled;
            y += 28f;

            changed |= Owner.DrawEnvironmentIntSlider(
                contentRect,
                ref y,
                ref envConfig.SceneSystem.MaxSceneChars,
                200,
                4000,
                "RimChat_EnvironmentMaxSceneChars");

            changed |= Owner.DrawEnvironmentIntSlider(
                contentRect,
                ref y,
                ref envConfig.SceneSystem.MaxTotalChars,
                500,
                12000,
                "RimChat_EnvironmentMaxTotalChars");

            return changed;
        }

        internal bool DrawEnvironmentEventIntelCard(Rect rect, EnvironmentPromptConfig envConfig)
        {
            Rect contentRect = Owner.DrawEnvironmentCard(rect, "RimChat_EnvironmentEventIntelLabel");
            EventIntelPromptConfig intel = envConfig.EventIntelPrompt;
            bool changed = false;
            float y = contentRect.y;

            bool oldEnabled = intel.Enabled;
            Widgets.CheckboxLabeled(
                new Rect(contentRect.x, y, contentRect.width, 24f),
                "RimChat_EnvironmentEventIntelEnabled".Translate(),
                ref intel.Enabled);
            changed |= oldEnabled != intel.Enabled;
            y += 26f;

            if (!intel.Enabled)
            {
                return changed;
            }

            changed |= Owner.DrawEnvironmentCheckbox(
                new Rect(contentRect.x, y, contentRect.width * 0.5f, 24f),
                "RimChat_EnvironmentEventIntelApplyDiplomacy",
                ref intel.ApplyToDiplomacy);
            changed |= Owner.DrawEnvironmentCheckbox(
                new Rect(contentRect.x + contentRect.width * 0.5f, y, contentRect.width * 0.5f, 24f),
                "RimChat_EnvironmentEventIntelApplyRpg",
                ref intel.ApplyToRpg);
            y += 24f;

            changed |= Owner.DrawEnvironmentCheckbox(
                new Rect(contentRect.x, y, contentRect.width * 0.5f, 24f),
                "RimChat_EnvironmentEventIntelIncludeMapEvents",
                ref intel.IncludeMapEvents);
            changed |= Owner.DrawEnvironmentCheckbox(
                new Rect(contentRect.x + contentRect.width * 0.5f, y, contentRect.width * 0.5f, 24f),
                "RimChat_EnvironmentEventIntelIncludeRaidReports",
                ref intel.IncludeRaidBattleReports);
            y += 28f;

            changed |= Owner.DrawEnvironmentIntSlider(
                contentRect,
                ref y,
                ref intel.DaysWindow,
                1,
                30,
                "RimChat_EnvironmentEventIntelDaysWindow");

            changed |= Owner.DrawEnvironmentIntSlider(
                contentRect,
                ref y,
                ref intel.MaxStoredRecords,
                20,
                200,
                "RimChat_EnvironmentEventIntelMaxStored");

            changed |= Owner.DrawEnvironmentIntSlider(
                contentRect,
                ref y,
                ref intel.MaxInjectedItems,
                1,
                20,
                "RimChat_EnvironmentEventIntelMaxItems");

            changed |= Owner.DrawEnvironmentIntSlider(
                contentRect,
                ref y,
                ref intel.MaxInjectedChars,
                200,
                4000,
                "RimChat_EnvironmentEventIntelMaxChars");

            return changed;
        }

        internal bool DrawEnvironmentSceneEntriesCard(Rect rect, EnvironmentPromptConfig envConfig)
        {
            Rect contentRect = Owner.DrawEnvironmentCard(rect, "RimChat_EnvironmentSceneEntriesLabel");
            float listWidth = Mathf.Min(250f, contentRect.width * 0.38f);
            Rect listRect = new Rect(contentRect.x, contentRect.y, listWidth, contentRect.height);
            Rect editorRect = new Rect(listRect.xMax + 10f, contentRect.y, contentRect.width - listWidth - 10f, contentRect.height);

            bool changed = false;
            changed |= DrawEnvironmentSceneList(listRect, envConfig);
            changed |= DrawEnvironmentSceneEditor(editorRect, envConfig);
            return changed;
        }

        internal bool DrawEnvironmentSceneList(Rect rect, EnvironmentPromptConfig envConfig)
        {
            bool changed = false;
            float buttonWidth = (rect.width - 6f) / 2f;
            Rect addRect = new Rect(rect.x, rect.y, buttonWidth, 24f);
            if (Widgets.ButtonText(addRect, "RimChat_EnvironmentAddScene".Translate()))
            {
                envConfig.SceneEntries.Add(new ScenePromptEntryConfig
                {
                    Name = "RimChat_EnvironmentNewSceneName".Translate().ToString(),
                    Priority = 10,
                    MatchTags = new List<string> { "channel:diplomacy", "scene:social" }
                });
                Owner._selectedEnvironmentSceneIndex = envConfig.SceneEntries.Count - 1;
                Owner.SyncEnvironmentSelection(envConfig);
                changed = true;
            }

            bool hasSelection = Owner._selectedEnvironmentSceneIndex >= 0 && Owner._selectedEnvironmentSceneIndex < envConfig.SceneEntries.Count;
            Rect removeRect = new Rect(addRect.xMax + 6f, rect.y, buttonWidth, 24f);
            if (Widgets.ButtonText(removeRect, "RimChat_EnvironmentRemoveScene".Translate(), active: hasSelection))
            {
                envConfig.SceneEntries.RemoveAt(Owner._selectedEnvironmentSceneIndex);
                Owner._selectedEnvironmentSceneIndex = Mathf.Clamp(Owner._selectedEnvironmentSceneIndex - 1, -1, envConfig.SceneEntries.Count - 1);
                Owner.SyncEnvironmentSelection(envConfig);
                changed = true;
            }

            Rect listRect = new Rect(rect.x, rect.y + 28f, rect.width, rect.height - 28f);
            float rowStride = RelationsPromptEnvironmentEditors.EnvSceneRowHeight + RelationsPromptEnvironmentEditors.EnvSceneRowGap;
            float contentHeight = Mathf.Max(listRect.height, envConfig.SceneEntries.Count * rowStride);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, contentHeight);
            Owner._envSceneListScroll = GUI.BeginScrollView(listRect, Owner._envSceneListScroll, viewRect);

            for (int i = 0; i < envConfig.SceneEntries.Count; i++)
            {
                ScenePromptEntryConfig entry = envConfig.SceneEntries[i];
                Rect rowRect = new Rect(0f, i * rowStride, viewRect.width, RelationsPromptEnvironmentEditors.EnvSceneRowHeight);
                bool selected = i == Owner._selectedEnvironmentSceneIndex;
                DrawEnvironmentSceneRow(rowRect, entry, selected);

                if (Widgets.ButtonInvisible(rowRect))
                {
                    Owner._selectedEnvironmentSceneIndex = i;
                    Owner.SyncEnvironmentSelection(envConfig);
                }
            }

            GUI.EndScrollView();
            return changed;
        }

        internal void DrawEnvironmentSceneRow(Rect rowRect, ScenePromptEntryConfig entry, bool selected)
        {
            bool hovered = Mouse.IsOver(rowRect);
            Color background = selected
                ? new Color(0.23f, 0.34f, 0.55f, 0.9f)
                : hovered
                    ? new Color(0.16f, 0.18f, 0.22f, 0.95f)
                    : new Color(0.10f, 0.11f, 0.14f, 0.92f);
            Widgets.DrawBoxSolid(rowRect, background);
            GUI.color = selected ? new Color(0.70f, 0.82f, 1f, 0.95f) : new Color(0.24f, 0.26f, 0.31f, 0.95f);
            Widgets.DrawBox(rowRect);

            if (entry?.Enabled == true)
            {
                Widgets.DrawBoxSolid(new Rect(rowRect.x + 1f, rowRect.y + 1f, 3f, rowRect.height - 2f), new Color(0.24f, 0.78f, 0.44f, 0.95f));
            }

            Rect nameRect = new Rect(rowRect.x + 8f, rowRect.y + 4f, rowRect.width - 16f, 20f);
            Rect metaRect = new Rect(rowRect.x + 8f, rowRect.y + 24f, rowRect.width - 16f, 18f);
            string name = string.IsNullOrWhiteSpace(entry?.Name)
                ? "RimChat_EnvironmentNewSceneName".Translate().ToString()
                : entry.Name.Trim();

            GUI.color = entry?.Enabled == false
                ? new Color(0.72f, 0.74f, 0.78f, 0.55f)
                : Color.white;
            Widgets.Label(nameRect, name.Truncate(nameRect.width));

            Text.Font = GameFont.Tiny;
            GUI.color = entry?.Enabled == false
                ? new Color(0.62f, 0.66f, 0.72f, 0.45f)
                : new Color(0.74f, 0.78f, 0.84f, 0.92f);
            Widgets.Label(metaRect, BuildEnvironmentSceneMeta(entry).Truncate(metaRect.width));
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
        }

        internal string BuildEnvironmentSceneMeta(ScenePromptEntryConfig entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            var channels = new List<string>();
            if (entry.ApplyToDiplomacy)
            {
                channels.Add("RimChat_EnvironmentApplyDiplomacy".Translate().ToString());
            }

            if (entry.ApplyToRPG)
            {
                channels.Add("RimChat_EnvironmentApplyRPG".Translate().ToString());
            }

            string channelText = channels.Count > 0 ? string.Join(" / ", channels) : "-";
            string priorityLabel = "RimChat_EnvironmentPriorityLabel".Translate().ToString();
            return $"{channelText}   {priorityLabel}: {entry.Priority}";
        }

        internal bool DrawEnvironmentSceneEditor(Rect rect, EnvironmentPromptConfig envConfig)
        {
            if (Owner._selectedEnvironmentSceneIndex < 0 || Owner._selectedEnvironmentSceneIndex >= envConfig.SceneEntries.Count)
            {
                GUI.color = Color.gray;
                Widgets.Label(rect, "RimChat_EnvironmentSelectSceneHint".Translate());
                GUI.color = Color.white;
                return false;
            }

            ScenePromptEntryConfig entry = envConfig.SceneEntries[Owner._selectedEnvironmentSceneIndex];
            if (entry == null)
            {
                return false;
            }

            if (!string.Equals(Owner._selectedEnvironmentSceneId, entry.Id, StringComparison.Ordinal))
            {
                Owner.SyncEnvironmentSelection(envConfig);
            }

            bool changed = false;
            float y = rect.y;

            Widgets.Label(new Rect(rect.x, y, rect.width, 18f), "RimChat_EnvironmentSceneNameLabel".Translate());
            y += 18f;
            string oldName = entry.Name ?? string.Empty;
            string newName = Widgets.TextField(new Rect(rect.x, y, rect.width, 24f), oldName);
            if (!string.Equals(oldName, newName, StringComparison.Ordinal))
            {
                entry.Name = newName;
                changed = true;
            }
            y += 28f;

            Widgets.Label(new Rect(rect.x, y, 90f, 18f), "RimChat_EnvironmentPriorityLabel".Translate());
            y += 18f;
            string oldPriorityBuffer = Owner._scenePriorityBuffer ?? "0";
            Owner._scenePriorityBuffer = Widgets.TextField(new Rect(rect.x, y, 90f, 24f), oldPriorityBuffer);
            if (int.TryParse(Owner._scenePriorityBuffer, out int parsedPriority))
            {
                parsedPriority = Mathf.Clamp(parsedPriority, -999, 999);
                if (entry.Priority != parsedPriority)
                {
                    entry.Priority = parsedPriority;
                    changed = true;
                }
            }

            float rightX = rect.x + 110f;
            changed |= Owner.DrawEnvironmentCheckbox(
                new Rect(rightX, y, rect.width - 110f, 24f),
                "RimChat_EnvironmentEntryEnabled",
                ref entry.Enabled);
            y += 24f;
            changed |= Owner.DrawEnvironmentCheckbox(
                new Rect(rightX, y, rect.width - 110f, 24f),
                "RimChat_EnvironmentApplyDiplomacy",
                ref entry.ApplyToDiplomacy);
            y += 24f;
            changed |= Owner.DrawEnvironmentCheckbox(
                new Rect(rightX, y, rect.width - 110f, 24f),
                "RimChat_EnvironmentApplyRPG",
                ref entry.ApplyToRPG);
            y += 28f;

            Widgets.Label(new Rect(rect.x, y, rect.width, 18f), "RimChat_EnvironmentSceneTagsLabel".Translate());
            y += 18f;
            string oldTagsBuffer = Owner._sceneTagsBuffer ?? string.Empty;
            Owner._sceneTagsBuffer = Widgets.TextField(new Rect(rect.x, y, rect.width, 24f), oldTagsBuffer);
            if (!string.Equals(oldTagsBuffer, Owner._sceneTagsBuffer, StringComparison.Ordinal))
            {
                entry.MatchTags = Owner.ParseTagCsv(Owner._sceneTagsBuffer);
                changed = true;
            }
            y += 28f;

            Widgets.Label(new Rect(rect.x, y, rect.width, 18f), "RimChat_EnvironmentSceneContentLabel".Translate());
            y += 20f;

            Rect textAreaRect = new Rect(rect.x, y, rect.width, rect.yMax - y);
            float contentHeight = Mathf.Max(textAreaRect.height, Text.CalcHeight(entry.Content ?? string.Empty, textAreaRect.width - 16f) + 10f);
            Rect viewRect = new Rect(0f, 0f, textAreaRect.width - 16f, contentHeight);
            Owner._envSceneContentScroll = GUI.BeginScrollView(textAreaRect, Owner._envSceneContentScroll, viewRect);
            string oldContent = entry.Content ?? string.Empty;
            string newContent = GUI.TextArea(viewRect, oldContent);
            GUI.EndScrollView();
            if (!string.Equals(oldContent, newContent, StringComparison.Ordinal))
            {
                entry.Content = newContent;
                changed = true;
            }

            return changed;
        }

        internal bool DrawEnvironmentRpgSwitchesCard(Rect rect, EnvironmentPromptConfig envConfig)
        {
            Rect contentRect = Owner.DrawEnvironmentCard(rect, "RimChat_EnvironmentRpgParamsLabel");
            RpgSceneParamSwitchesConfig switches = envConfig.RpgSceneParamSwitches;
            bool changed = false;

            float colGap = 14f;
            float colWidth = (contentRect.width - colGap) / 2f;
            Rect leftCol = new Rect(contentRect.x, contentRect.y, colWidth, contentRect.height);
            Rect rightCol = new Rect(leftCol.xMax + colGap, contentRect.y, colWidth, contentRect.height);
            float leftY = leftCol.y;
            float rightY = rightCol.y;

            changed |= Owner.DrawEnvironmentCheckbox(new Rect(leftCol.x, leftY, leftCol.width, 24f), "RimChat_EnvironmentRpgParamSkills", ref switches.IncludeSkills); leftY += 24f;
            changed |= Owner.DrawEnvironmentCheckbox(new Rect(leftCol.x, leftY, leftCol.width, 24f), "RimChat_EnvironmentRpgParamEquipment", ref switches.IncludeEquipment); leftY += 24f;
            changed |= Owner.DrawEnvironmentCheckbox(new Rect(leftCol.x, leftY, leftCol.width, 24f), "RimChat_EnvironmentRpgParamGenes", ref switches.IncludeGenes); leftY += 24f;
            changed |= Owner.DrawEnvironmentCheckbox(new Rect(leftCol.x, leftY, leftCol.width, 24f), "RimChat_EnvironmentRpgParamColonyInventory", ref switches.IncludeColonyInventorySummary); leftY += 24f;
            changed |= Owner.DrawEnvironmentCheckbox(new Rect(leftCol.x, leftY, leftCol.width, 24f), "RimChat_EnvironmentRpgParamRecentJobState", ref switches.IncludeRecentJobState);

            changed |= Owner.DrawEnvironmentCheckbox(new Rect(rightCol.x, rightY, rightCol.width, 24f), "RimChat_EnvironmentRpgParamNeeds", ref switches.IncludeNeeds); rightY += 24f;
            changed |= Owner.DrawEnvironmentCheckbox(new Rect(rightCol.x, rightY, rightCol.width, 24f), "RimChat_EnvironmentRpgParamHediffs", ref switches.IncludeHediffs); rightY += 24f;
            changed |= Owner.DrawEnvironmentCheckbox(new Rect(rightCol.x, rightY, rightCol.width, 24f), "RimChat_EnvironmentRpgParamRecentEvents", ref switches.IncludeRecentEvents); rightY += 24f;
            changed |= Owner.DrawEnvironmentCheckbox(new Rect(rightCol.x, rightY, rightCol.width, 24f), "RimChat_EnvironmentRpgParamHomeAlerts", ref switches.IncludeHomeAlerts); rightY += 24f;
            changed |= Owner.DrawEnvironmentCheckbox(new Rect(rightCol.x, rightY, rightCol.width, 24f), "RimChat_EnvironmentRpgParamAttributeLevels", ref switches.IncludeAttributeLevels);

            return changed;
        }
}
