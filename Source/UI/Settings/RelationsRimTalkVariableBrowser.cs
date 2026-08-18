using System;
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.UI;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Core.UI;

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class RelationsRimTalkVariableBrowser
{
    readonly RelationsSettingsPages Pages;

    internal RelationsRimTalkVariableBrowser(RelationsSettingsPages pages)
    {
        Pages = pages;
    }

    RelationsSettings Settings => Pages.Settings;

        internal const float RimTalkVariableCacheRefreshSeconds = 1.2f;
        internal const float VariableListRowStep = 24f;

        internal string _rimTalkVariableSearch = string.Empty;
        internal string _rimTalkSelectedVariableName = string.Empty;
        internal readonly List<PromptVariableDisplayEntry> _rimTalkVariableSnapshotCache = new List<PromptVariableDisplayEntry>();
        internal readonly List<PromptVariableDisplayEntry> _rimTalkVariableDisplayCache = new List<PromptVariableDisplayEntry>();
        internal readonly List<VariableListRow> _rimTalkVariableRowCache = new List<VariableListRow>();
        internal readonly Dictionary<string, string> _rimTalkVariableTooltipCache = new Dictionary<string, string>(StringComparer.Ordinal);
        internal float _rimTalkVariableCacheRefreshAt = -1f;
        internal bool _rimTalkVariableSnapshotReady;
        internal int _rimTalkVariableSnapshotVersion;
        internal int _rimTalkVariableDisplayVersion = -1;
        internal int _rimTalkVariableRowVersion = -1;
        internal string _rimTalkVariableDisplaySearch = string.Empty;
        internal string _rimTalkVariableRowSearch = string.Empty;
        internal string _rimTalkVariableLastClickedPath = string.Empty;
        internal float _rimTalkVariableLastClickAt = -10f;
        internal const float VariableRepeatClickSeconds = 0.7f;

        internal void DrawRimTalkWorkbenchVariableBrowser(Rect rect, string currentEntryContent)
        {
            DrawPromptVariableBrowser(rect, currentEntryContent, entry =>
            {
                string variableName = ResolveDefaultInsertVariableName(entry);
                if (string.IsNullOrWhiteSpace(variableName))
                {
                    return false;
                }

                Pages.RimTalkTemplates.AppendVariableToCurrentRimTalkTemplate(variableName);
                return true;
            }, showCustomCrud: true);
        }

        internal void DrawPromptVariableBrowser(
            Rect rect,
            string currentContent,
            Func<PromptVariableDisplayEntry, bool> onInsert,
            bool showCustomCrud = false)
        {
            float topY = rect.y;
            if (showCustomCrud)
            {
                float buttonWidth = Mathf.Min(110f, Mathf.Max(74f, (rect.width - 12f) / 3f));
                Rect createRect = new Rect(rect.x, topY, buttonWidth, 24f);
                bool selectedEditable = TryGetSelectedEditableVariable(out PromptVariableDisplayEntry selectedVariable);
                Rect editRect = new Rect(createRect.xMax + 6f, topY, buttonWidth, 24f);
                Rect deleteRect = new Rect(editRect.xMax + 6f, topY, buttonWidth, 24f);

                if (Widgets.ButtonText(createRect, "RimChat_CustomVariableCreate".Translate()))
                {
                    OpenUserDefinedPromptVariableCreateMenu();
                }

                GUI.color = selectedEditable ? Color.white : Color.gray;
                if (Widgets.ButtonText(editRect, "RimChat_EditTemplate".Translate()) && selectedEditable)
                {
                    Pages.CustomVariables.OpenUserDefinedPromptVariableEditor(selectedVariable.Path);
                }

                if (Widgets.ButtonText(deleteRect, "RimChat_CustomVariableDelete".Translate()) && selectedEditable)
                {
                    Pages.CustomVariables.TryDeleteUserDefinedPromptVariable(selectedVariable.Path);
                }
                GUI.color = Color.white;

                topY += 28f;
            }

            Rect searchRect = new Rect(rect.x, topY, rect.width, 24f);
            string before = _rimTalkVariableSearch ?? string.Empty;
            _rimTalkVariableSearch = Widgets.TextField(searchRect, before);
            if (!string.Equals(before, _rimTalkVariableSearch, StringComparison.Ordinal))
            {
                Pages.RpgEditors._rimTalkCompatVariableScroll = Vector2.zero;
            }

            if (string.IsNullOrWhiteSpace(_rimTalkVariableSearch))
            {
                GUI.color = new Color(1f, 1f, 1f, 0.3f);
                Widgets.Label(searchRect.ContractedBy(2f, 0f), "RimChat_RimTalkVariableSearch".Translate());
                GUI.color = Color.white;
            }

            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(rect.x, topY + 26f, rect.width, 20f), "RimChat_RimTalkVariableBrowserHint".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            float listTop = topY + 45f;
            Rect listRect = new Rect(rect.x, listTop, rect.width, Mathf.Max(1f, rect.height - (listTop - rect.y)));
            List<PromptVariableDisplayEntry> variables = GetFilteredPromptVariables(_rimTalkVariableSearch);
            DrawPromptVariableList(listRect, variables, selectable: false, currentContent, onInsert);
        }

        internal void DrawPromptVariableList(
            Rect rect,
            List<PromptVariableDisplayEntry> variables,
            bool selectable,
            string currentContent,
            Func<PromptVariableDisplayEntry, bool> onInsert)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.03f, 0.03f, 0.05f));
            Rect inner = rect.ContractedBy(2f);
            EnsurePromptVariableRows(variables);
            int totalRows = _rimTalkVariableRowCache.Count;
            Rect viewRect = new Rect(0f, 0f, inner.width - 16f, Mathf.Max(inner.height, totalRows * VariableListRowStep + 6f));
            Widgets.BeginScrollView(inner, ref Pages.RpgEditors._rimTalkCompatVariableScroll, viewRect);

            if (totalRows == 0)
            {
                Widgets.Label(new Rect(2f, 0f, viewRect.width - 4f, 20f), "RimChat_RimTalkVariableBrowserHint".Translate());
                Widgets.EndScrollView();
                return;
            }

            ResolveVisibleRowRange(Pages.RpgEditors._rimTalkCompatVariableScroll.y, inner.height, totalRows, out int firstRow, out int lastRow);
            for (int rowIndex = firstRow; rowIndex <= lastRow; rowIndex++)
            {
                VariableListRow row = _rimTalkVariableRowCache[rowIndex];
                float y = rowIndex * VariableListRowStep;
                if (row.IsHeader)
                {
                    DrawVariableGroupHeaderRow(new Rect(2f, y, viewRect.width - 4f, 20f), row.HeaderText);
                    continue;
                }

                PromptVariableDisplayEntry variable = row.Variable;
                Rect rowRect = new Rect(2f, y, viewRect.width - 4f, 22f);
                DrawVariableEntryRow(rowRect, variable, selectable, currentContent, onInsert);
            }

            Widgets.EndScrollView();
        }

        internal void DrawPromptVariableRow(Rect rect, PromptVariableDisplayEntry variable, string currentContent)
        {
            if (variable == null)
            {
                return;
            }

            Text.Font = GameFont.Tiny;
            bool oldWordWrap = Text.WordWrap;
            Text.WordWrap = false;
            string token = BuildVariableRowTokenLabel(variable);
            float tokenWidth = Mathf.Min(Text.CalcSize(token).x + 6f, Mathf.Max(1f, rect.width - 8f));
            Rect tokenRect = new Rect(rect.x + 2f, rect.y + 1f, tokenWidth, rect.height - 2f);
            Rect infoRect = new Rect(tokenRect.xMax + 6f, rect.y + 1f, Mathf.Max(1f, rect.xMax - tokenRect.xMax - 8f), rect.height - 2f);

            GUI.color = new Color(0.8f, 1f, 0.8f);
            Widgets.Label(tokenRect, token.Truncate(tokenRect.width));

            string info = BuildVariableInlineInfo(variable, currentContent);
            if (!string.IsNullOrWhiteSpace(info))
            {
                GUI.color = Color.gray;
                Widgets.Label(infoRect, info.Truncate(infoRect.width));
            }

            Text.WordWrap = oldWordWrap;
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        internal void DrawPromptVariableDetails(
            Rect rect,
            PromptVariableDisplayEntry variable,
            Func<PromptVariableDisplayEntry, bool> onInsert)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.08f, 0.09f, 0.11f));
            Rect inner = rect.ContractedBy(6f);
            if (variable == null)
            {
                GUI.color = Color.gray;
                Widgets.Label(inner, "RimChat_RimTalkVariableBrowserHint".Translate());
                GUI.color = Color.white;
                return;
            }

            string insertLabel = "RimChat_InsertVariable".Translate();
            float buttonWidth = onInsert == null ? 0f : Mathf.Clamp(Text.CalcSize(insertLabel).x + 20f, 72f, 118f);
            float trailingWidth = buttonWidth;
            Rect insertRect = new Rect(inner.xMax - buttonWidth, inner.y, buttonWidth, 24f);
            Rect tokenRect = new Rect(inner.x, inner.y + 2f, inner.width - trailingWidth - 8f, 20f);
            Rect detailRect = new Rect(inner.x, tokenRect.yMax + 2f, inner.width, inner.height - 24f);

            bool oldWordWrap = Text.WordWrap;
            Text.WordWrap = false;
            string label = BuildVariableDetailsTokenLabel(variable);
            if (!variable.IsAvailable)
            {
                label += " " + "RimChat_PromptVariableDependencyMissingShort".Translate();
            }

            Widgets.Label(tokenRect, label.Truncate(tokenRect.width));
            Text.WordWrap = oldWordWrap;

            GUI.color = Color.gray;
            string summary = string.IsNullOrWhiteSpace(variable.DetailSummary) ? variable.Description ?? string.Empty : variable.DetailSummary;
            string details = BuildVariableGroupKey(variable) + "\n" +
                             BuildAvailabilityLabel(variable) + "\n" +
                             summary;
            Widgets.Label(detailRect, details);
            GUI.color = Color.white;
            if (onInsert != null && Widgets.ButtonText(insertRect, insertLabel))
            {
                onInsert?.Invoke(variable);
            }
        }

        internal bool TryGetSelectedEditableVariable(out PromptVariableDisplayEntry variable)
        {
            variable = ResolveSelectedPromptVariable(_rimTalkVariableDisplayCache);
            return variable != null && variable.IsEditable;
        }

        internal void OpenUserDefinedPromptVariableCreateMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>
            {
                new FloatMenuOption("RimChat_CustomVariableCreateBlank".Translate(), () => Pages.CustomVariables.OpenUserDefinedPromptVariableEditor())
            };

            foreach (string key in UserDefinedPromptVariableService.GetSuggestedKeys())
            {
                string normalized = UserDefinedPromptVariableService.NormalizeKey(key);
                string path = UserDefinedPromptVariableService.BuildPath(normalized);
                options.Add(new FloatMenuOption(path, () =>
                {
                    UserDefinedPromptVariableEditModel model = UserDefinedPromptVariableService.CreateSuggestedModel(normalized);
                    Find.WindowStack.Add(new Dialog_UserDefinedPromptVariableEditor(Settings, model, null, () =>
                    {
                        Pages.CustomVariables.InvalidatePromptVariableBrowserCache();
                        _rimTalkSelectedVariableName = UserDefinedPromptVariableService.BuildPath(model.Variable.Key);
                    }));
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        internal List<PromptVariableDisplayEntry> GetFilteredPromptVariables(string searchText)
        {
            EnsurePromptVariableSnapshotCacheFresh();
            string normalizedSearch = string.IsNullOrWhiteSpace(searchText) ? string.Empty : searchText.Trim();
            bool unchanged = _rimTalkVariableDisplayVersion == _rimTalkVariableSnapshotVersion &&
                             string.Equals(_rimTalkVariableDisplaySearch, normalizedSearch, StringComparison.Ordinal);
            if (unchanged)
            {
                return _rimTalkVariableDisplayCache;
            }

            _rimTalkVariableDisplaySearch = normalizedSearch;
            _rimTalkVariableDisplayVersion = _rimTalkVariableSnapshotVersion;
            RebuildPromptVariableDisplayCache(normalizedSearch);
            return _rimTalkVariableDisplayCache;
        }

        internal void EnsurePromptVariableSnapshotCacheFresh()
        {
            float now = Time.realtimeSinceStartup;
            if (_rimTalkVariableSnapshotReady && now < _rimTalkVariableCacheRefreshAt)
            {
                return;
            }

            _rimTalkVariableCacheRefreshAt = now + RimTalkVariableCacheRefreshSeconds;
            PromptRuntimeVariableBridge.RefreshRimTalkCustomVariableSnapshot();
            List<PromptVariableDisplayEntry> snapshot = PromptVariableCatalog.GetDisplayEntries().ToList();
            _rimTalkVariableSnapshotCache.Clear();
            _rimTalkVariableSnapshotCache.AddRange(snapshot.Where(item => item != null));
            _rimTalkVariableSnapshotReady = true;
            _rimTalkVariableSnapshotVersion++;
            Pages.VariableBrowser._rimTalkVariableTooltipCache.Clear();
            InvalidatePromptVariableRowCache();
        }

        internal void RebuildPromptVariableDisplayCache(string term)
        {
            _rimTalkVariableDisplayCache.Clear();
            foreach (PromptVariableDisplayEntry entry in _rimTalkVariableSnapshotCache)
            {
                bool matches = string.IsNullOrEmpty(term) ||
                               ContainsTerm(entry?.Path, term) ||
                               ContainsTerm(entry?.RawToken, term) ||
                               ContainsTerm(entry?.NamespacedToken, term) ||
                               ContainsTerm(entry?.DefaultInsertToken, term) ||
                               ContainsTerm(entry?.Scope, term) ||
                               ContainsTerm(entry?.SourceId, term) ||
                               ContainsTerm(entry?.SourceLabel, term) ||
                               ContainsTerm(entry?.Description, term) ||
                               ContainsTerm(entry?.DetailSummary, term);
                if (matches)
                {
                    _rimTalkVariableDisplayCache.Add(entry);
                }
            }

            _rimTalkVariableDisplayCache.Sort(ComparePromptVariables);
            InvalidatePromptVariableRowCache();
        }

        internal static bool ContainsTerm(string value, string term) => RelationsRimTalkVariableLabelOps.ContainsTerm(value, term);
        internal static int ComparePromptVariables(PromptVariableDisplayEntry left, PromptVariableDisplayEntry right) => RelationsRimTalkVariableLabelOps.ComparePromptVariables(left, right);
        internal PromptVariableDisplayEntry ResolveSelectedPromptVariable(IReadOnlyList<PromptVariableDisplayEntry> variables)
        {
            if (variables == null || variables.Count == 0)
            {
                return null;
            }

            PromptVariableDisplayEntry selected = variables.FirstOrDefault(variable =>
                variable != null && string.Equals(variable.Path, _rimTalkSelectedVariableName, StringComparison.Ordinal));
            if (selected != null)
            {
                return selected;
            }

            _rimTalkSelectedVariableName = variables[0]?.Path ?? string.Empty;
            return variables[0];
        }

        internal static string BuildVariableTooltipText(PromptVariableDisplayEntry variable) => RelationsRimTalkVariableLabelOps.BuildVariableTooltipText(variable);
        internal static string BuildTypicalValuesText(IReadOnlyList<string> values) => RelationsRimTalkVariableLabelOps.BuildTypicalValuesText(values);
        internal static string BuildVariableGroupKey(PromptVariableDisplayEntry variable) => RelationsRimTalkVariableLabelOps.BuildVariableGroupKey(variable);
        internal static string ResolveGroupedSourceLabel(PromptVariableDisplayEntry variable) => RelationsRimTalkVariableLabelOps.ResolveGroupedSourceLabel(variable);
        internal static string BuildVariableToken(string variableName) => RelationsRimTalkVariableLabelOps.BuildVariableToken(variableName);
        internal static string BuildVariableRowTokenLabel(PromptVariableDisplayEntry variable) => RelationsRimTalkVariableLabelOps.BuildVariableRowTokenLabel(variable);
        internal static string BuildVariableDetailsTokenLabel(PromptVariableDisplayEntry variable) => RelationsRimTalkVariableLabelOps.BuildVariableDetailsTokenLabel(variable);
        internal static string ResolveTokenFallback(string token, string variablePath) => RelationsRimTalkVariableLabelOps.ResolveTokenFallback(token, variablePath);
        internal static string ResolveDefaultInsertVariableName(PromptVariableDisplayEntry entry) => RelationsRimTalkVariableLabelOps.ResolveDefaultInsertVariableName(entry);
        internal static string BuildVariableInlineInfo(PromptVariableDisplayEntry variable, string currentContent) => RelationsRimTalkVariableLabelOps.BuildVariableInlineInfo(variable, currentContent);
        internal static string BuildAvailabilityLabel(PromptVariableDisplayEntry variable) => RelationsRimTalkVariableLabelOps.BuildAvailabilityLabel(variable);
        internal void EnsurePromptVariableRows(List<PromptVariableDisplayEntry> variables)
        {
            if (_rimTalkVariableRowVersion == _rimTalkVariableDisplayVersion &&
                string.Equals(_rimTalkVariableRowSearch, _rimTalkVariableDisplaySearch, StringComparison.Ordinal))
            {
                return;
            }

            RebuildPromptVariableRows(variables);
            _rimTalkVariableRowVersion = _rimTalkVariableDisplayVersion;
            _rimTalkVariableRowSearch = _rimTalkVariableDisplaySearch;
        }

        internal void RebuildPromptVariableRows(List<PromptVariableDisplayEntry> variables)
        {
            _rimTalkVariableRowCache.Clear();
            string previousGroup = null;
            for (int i = 0; i < variables.Count; i++)
            {
                PromptVariableDisplayEntry variable = variables[i];
                if (variable == null)
                {
                    continue;
                }

                string group = BuildVariableGroupKey(variable);
                if (!string.Equals(previousGroup, group, StringComparison.Ordinal))
                {
                    _rimTalkVariableRowCache.Add(VariableListRow.CreateHeader(group));
                    previousGroup = group;
                }

                _rimTalkVariableRowCache.Add(VariableListRow.CreateVariable(variable));
            }
        }

        internal void InvalidatePromptVariableRowCache()
        {
            _rimTalkVariableRowVersion = -1;
            _rimTalkVariableRowSearch = string.Empty;
            _rimTalkVariableRowCache.Clear();
        }

        internal static void ResolveVisibleRowRange(float scrollY,
            float viewportHeight,
            int rowCount,
            out int firstRow,
            out int lastRow) => RelationsRimTalkVariableLabelOps.ResolveVisibleRowRange(scrollY, viewportHeight, rowCount, out firstRow, out lastRow);
        internal static void DrawVariableGroupHeaderRow(Rect rect, string header) => RelationsRimTalkVariableLabelOps.DrawVariableGroupHeaderRow(rect, header);
        internal void DrawVariableEntryRow(
            Rect rowRect,
            PromptVariableDisplayEntry variable,
            bool selectable,
            string currentContent,
            Func<PromptVariableDisplayEntry, bool> onInsert)
        {
            if (variable == null)
            {
                return;
            }

            if (Mouse.IsOver(rowRect))
            {
                Widgets.DrawHighlight(rowRect);
            }

            bool isSelected = string.Equals(_rimTalkSelectedVariableName, variable.Path ?? string.Empty, StringComparison.Ordinal);
            if (isSelected)
            {
                Widgets.DrawBoxSolid(rowRect, new Color(0.25f, 0.35f, 0.55f, 0.45f));
            }

            if (Widgets.ButtonInvisible(rowRect))
            {
                string path = variable.Path ?? string.Empty;
                bool shouldInsert = !selectable &&
                                    onInsert != null &&
                                    string.Equals(_rimTalkVariableLastClickedPath, path, StringComparison.Ordinal) &&
                                    Time.realtimeSinceStartup - _rimTalkVariableLastClickAt <= VariableRepeatClickSeconds;

                _rimTalkSelectedVariableName = path;
                _rimTalkVariableLastClickedPath = path;
                _rimTalkVariableLastClickAt = Time.realtimeSinceStartup;

                if (shouldInsert)
                {
                    onInsert(variable);
                }
            }

            DrawPromptVariableRow(rowRect, variable, currentContent);
            TooltipHandler.TipRegion(rowRect, GetVariableTooltipTextCached(variable));
        }

        internal string GetVariableTooltipTextCached(PromptVariableDisplayEntry variable)
        {
            string path = variable?.Path ?? string.Empty;
            if (!Pages.VariableBrowser._rimTalkVariableTooltipCache.TryGetValue(path, out string tooltip))
            {
                tooltip = BuildVariableTooltipText(variable);
                Pages.VariableBrowser._rimTalkVariableTooltipCache[path] = tooltip;
            }

            return tooltip;
        }

        internal sealed class VariableListRow
        {
            public bool IsHeader { get; private set; }
            public string HeaderText { get; private set; }
            public PromptVariableDisplayEntry Variable { get; private set; }

            public static VariableListRow CreateHeader(string headerText)
            {
                return new VariableListRow
                {
                    IsHeader = true,
                    HeaderText = headerText ?? string.Empty
                };
            }

            public static VariableListRow CreateVariable(PromptVariableDisplayEntry variable)
            {
                return new VariableListRow
                {
                    IsHeader = false,
                    Variable = variable
                };
            }
        }
    
}
