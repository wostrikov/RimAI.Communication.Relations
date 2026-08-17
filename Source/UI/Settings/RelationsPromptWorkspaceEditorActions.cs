using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.UI;
using Ustas.RimAI.Core.UI;

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class RelationsPromptWorkspaceEditorActions
{
    readonly RelationsSettingsPages Pages;

    internal RelationsPromptWorkspaceEditorActions(RelationsSettingsPages pages)
    {
        Pages = pages;
    }

    RelationsSettings Settings => Pages.Settings;

        internal const int PromptWorkspaceHistoryLimit = 64;

        internal sealed class PromptWorkspaceTextHistory
        {
            public readonly List<string> Undo = new List<string>();
            public readonly List<string> Redo = new List<string>();
        }

        internal readonly Dictionary<string, PromptWorkspaceTextHistory> _promptWorkspaceTextHistories =
            new Dictionary<string, PromptWorkspaceTextHistory>(StringComparer.Ordinal);

        internal void DrawPromptWorkspaceToolbar(Rect rect)
        {
            float width = (rect.width - 18f) / 4f;
            Rect undoRect = new Rect(rect.x, rect.y, width, rect.height);
            Rect redoRect = new Rect(undoRect.xMax + 6f, rect.y, width, rect.height);
            Rect saveRect = new Rect(redoRect.xMax + 6f, rect.y, width, rect.height);
            Rect resetRect = new Rect(saveRect.xMax + 6f, rect.y, width, rect.height);

            DrawPromptWorkspaceToolbarButton(
                undoRect,
                "RimChat_PromptWorkspaceToolbar_Undo",
                CanUndoPromptWorkspaceText(),
                TryUndoPromptWorkspaceText);
            DrawPromptWorkspaceToolbarButton(
                redoRect,
                "RimChat_PromptWorkspaceToolbar_Redo",
                CanRedoPromptWorkspaceText(),
                TryRedoPromptWorkspaceText);
            DrawPromptWorkspaceToolbarButton(
                saveRect,
                "RimChat_PromptWorkspaceToolbar_Save",
                true,
                TrySavePromptWorkspaceNow);
            DrawPromptWorkspaceToolbarButton(
                resetRect,
                "RimChat_PromptWorkspaceToolbar_Reset",
                true,
                TryResetPromptWorkspaceCurrentEntry);
        }

        internal static void DrawPromptWorkspaceToolbarButton(Rect rect, string key, bool enabled, Action action)
        {
            bool oldEnabled = GUI.enabled;
            if (!enabled)
            {
                GUI.enabled = false;
            }

            if (Widgets.ButtonText(rect, key.Translate()))
            {
                action?.Invoke();
            }

            GUI.enabled = oldEnabled;
        }

        internal bool CanUndoPromptWorkspaceText()
        {
            string key = BuildPromptWorkspaceHistoryKey();
            return !string.IsNullOrWhiteSpace(key) &&
                   _promptWorkspaceTextHistories.TryGetValue(key, out PromptWorkspaceTextHistory history) &&
                   history.Undo.Count > 0;
        }

        internal bool CanRedoPromptWorkspaceText()
        {
            string key = BuildPromptWorkspaceHistoryKey();
            return !string.IsNullOrWhiteSpace(key) &&
                   _promptWorkspaceTextHistories.TryGetValue(key, out PromptWorkspaceTextHistory history) &&
                   history.Redo.Count > 0;
        }

        internal void TryUndoPromptWorkspaceText()
        {
            if (!Pages.PromptPresetsUi.EnsurePromptWorkspaceEditablePresetForMutation("workspace.undo"))
            {
                return;
            }

            string key = BuildPromptWorkspaceHistoryKey();
            if (string.IsNullOrWhiteSpace(key) ||
                !_promptWorkspaceTextHistories.TryGetValue(key, out PromptWorkspaceTextHistory history) ||
                history.Undo.Count == 0)
            {
                return;
            }

            string current = Pages.PromptWorkspace._promptWorkspaceEditorBuffer ?? string.Empty;
            string previous = PopFromHistory(history.Undo);
            PushToHistory(history.Redo, current);
            SetPromptWorkspaceCurrentEditorTextWithoutHistory(previous);
        }

        internal void TryRedoPromptWorkspaceText()
        {
            if (!Pages.PromptPresetsUi.EnsurePromptWorkspaceEditablePresetForMutation("workspace.redo"))
            {
                return;
            }

            string key = BuildPromptWorkspaceHistoryKey();
            if (string.IsNullOrWhiteSpace(key) ||
                !_promptWorkspaceTextHistories.TryGetValue(key, out PromptWorkspaceTextHistory history) ||
                history.Redo.Count == 0)
            {
                return;
            }

            string current = Pages.PromptWorkspace._promptWorkspaceEditorBuffer ?? string.Empty;
            string next = PopFromHistory(history.Redo);
            PushToHistory(history.Undo, current);
            SetPromptWorkspaceCurrentEditorTextWithoutHistory(next);
        }

        internal void TrySavePromptWorkspaceNow()
        {
            Pages.PromptWorkspaceBuffers.TryScheduleValidation(immediate: true);
            Pages.PromptWorkspaceChrome.ForcePromptWorkspaceValidationNow();
            if (Pages.PromptWorkspaceBuffers.PersistPromptWorkspaceBufferNow(force: true, persistToDisk: true))
            {
                if (Pages.PromptWorkspace._promptWorkspaceLastPersistHadMaterialChange)
                {
                    Messages.Message("RimChat_PromptWorkspace_SaveDone".Translate(), MessageTypeDefOf.NeutralEvent, false);
                }
            }
        }

        internal void TryResetPromptWorkspaceCurrentEntry()
        {
            if (!Pages.PromptPresetsUi.EnsurePromptWorkspaceEditablePresetForMutation("workspace.reset_entry"))
            {
                return;
            }

            string current = Pages.PromptWorkspaceBuffers.GetPromptWorkspaceCurrentEditorText();
            string fallback = Pages.PromptWorkspace._promptWorkspaceEditNodeMode
                ? PromptUnifiedCatalog.CreateFallback().ResolveNode(Pages.PromptWorkbench._workbenchPromptChannel, Pages.PromptWorkspace._promptWorkspaceSelectedNodeId)
                : RimTalkPromptEntryDefaultsProvider.ResolveContent(Pages.PromptWorkbench._workbenchPromptChannel, Pages.PromptWorkspace._promptWorkspaceSelectedSectionId);
            string next = fallback ?? string.Empty;
            if (string.Equals(current ?? string.Empty, next, StringComparison.Ordinal))
            {
                return;
            }

            RecordPromptWorkspaceTextHistoryBeforeMutation(current ?? string.Empty);
            SetPromptWorkspaceCurrentEditorTextWithoutHistory(next);
            Pages.PromptWorkspaceBuffers.PersistPromptWorkspaceBufferNow();
        }

        internal void HandlePromptWorkspaceKeyboardShortcuts()
        {
            Event evt = Event.current;
            if (evt == null || evt.type != EventType.KeyDown || !evt.control)
            {
                return;
            }

            if (evt.keyCode == KeyCode.S)
            {
                TrySavePromptWorkspaceNow();
                evt.Use();
                return;
            }

            if (evt.keyCode == KeyCode.R)
            {
                TryResetPromptWorkspaceCurrentEntry();
                evt.Use();
                return;
            }

            if (evt.keyCode == KeyCode.Z && !evt.shift)
            {
                TryUndoPromptWorkspaceText();
                evt.Use();
                return;
            }

            if (evt.keyCode == KeyCode.Y || (evt.keyCode == KeyCode.Z && evt.shift))
            {
                TryRedoPromptWorkspaceText();
                evt.Use();
            }
        }

        internal void CapturePromptWorkspaceLiveEditorText()
        {
            if (!string.Equals(GUI.GetNameOfFocusedControl(), RelationsPromptSectionWorkspace.PromptWorkspaceEditorControlName, StringComparison.Ordinal))
            {
                return;
            }

            TextEditor editor = GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl) as TextEditor;
            if (editor == null)
            {
                return;
            }

            string liveText = editor.text ?? string.Empty;
            if (string.Equals(liveText, Pages.PromptWorkspace._promptWorkspaceEditorBuffer ?? string.Empty, StringComparison.Ordinal))
            {
                return;
            }

            Pages.PromptWorkspace._promptWorkspaceEditorBuffer = liveText;
            Pages.PromptWorkspace._promptWorkspaceBufferedChannel = Pages.PromptWorkbench._workbenchPromptChannel ?? string.Empty;
            Pages.PromptWorkspace._promptWorkspaceBufferedNodeMode = Pages.PromptWorkspace._promptWorkspaceEditNodeMode;
            Pages.PromptWorkspace._promptWorkspaceBufferedSectionId = Pages.PromptWorkspace._promptWorkspaceSelectedSectionId ?? string.Empty;
            Pages.PromptWorkspace._promptWorkspaceBufferedNodeId = Pages.PromptWorkspace._promptWorkspaceSelectedNodeId ?? string.Empty;
            Pages.PromptWorkspace._promptWorkspaceHasPendingPersist = true;
            Pages.PromptWorkspaceBuffers.NotifyPromptWorkspaceEditorTextChanged();
        }

        internal void SetPromptWorkspaceCurrentEditorTextWithoutHistory(string text)
        {
            string next = text ?? string.Empty;
            if (string.Equals(next, Pages.PromptWorkspace._promptWorkspaceEditorBuffer ?? string.Empty, StringComparison.Ordinal))
            {
                return;
            }

            Pages.PromptWorkspace._promptWorkspaceEditorBuffer = next;
            Pages.PromptWorkspace._promptWorkspaceBufferedChannel = Pages.PromptWorkbench._workbenchPromptChannel ?? string.Empty;
            Pages.PromptWorkspace._promptWorkspaceBufferedNodeMode = Pages.PromptWorkspace._promptWorkspaceEditNodeMode;
            Pages.PromptWorkspace._promptWorkspaceBufferedSectionId = Pages.PromptWorkspace._promptWorkspaceSelectedSectionId ?? string.Empty;
            Pages.PromptWorkspace._promptWorkspaceBufferedNodeId = Pages.PromptWorkspace._promptWorkspaceSelectedNodeId ?? string.Empty;
            Pages.PromptWorkspaceBuffers.MarkPromptWorkspaceDirty();
        }

        internal void RecordPromptWorkspaceTextHistoryBeforeMutation(string oldText)
        {
            string key = BuildPromptWorkspaceHistoryKey();
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            PromptWorkspaceTextHistory history = GetOrCreatePromptWorkspaceTextHistory(key);
            PushToHistory(history.Undo, oldText ?? string.Empty);
            history.Redo.Clear();
        }

        internal string BuildPromptWorkspaceHistoryKey()
        {
            string presetId = Pages.PromptWorkbench._selectedPromptPresetId ?? string.Empty;
            string channel = string.IsNullOrWhiteSpace(Pages.PromptWorkbench._workbenchPromptChannel)
                ? Pages.PromptWorkspaceBuffers.EnsurePromptWorkspaceSelection()
                : Pages.PromptWorkbench._workbenchPromptChannel;
            string mode = Pages.PromptWorkspace._promptWorkspaceEditNodeMode ? "node" : "section";
            string target = Pages.PromptWorkspace._promptWorkspaceEditNodeMode
                ? (Pages.PromptWorkspace._promptWorkspaceSelectedNodeId ?? string.Empty)
                : (Pages.PromptWorkspace._promptWorkspaceSelectedSectionId ?? string.Empty);
            if (string.IsNullOrWhiteSpace(presetId) ||
                string.IsNullOrWhiteSpace(channel) ||
                string.IsNullOrWhiteSpace(target))
            {
                return string.Empty;
            }

            return $"{presetId}|{channel}|{mode}|{target}";
        }

        internal PromptWorkspaceTextHistory GetOrCreatePromptWorkspaceTextHistory(string key)
        {
            if (!_promptWorkspaceTextHistories.TryGetValue(key, out PromptWorkspaceTextHistory history))
            {
                history = new PromptWorkspaceTextHistory();
                _promptWorkspaceTextHistories[key] = history;
            }

            return history;
        }

        internal static void PushToHistory(List<string> stack, string value)
        {
            if (stack == null)
            {
                return;
            }

            string normalized = value ?? string.Empty;
            if (stack.Count > 0 && string.Equals(stack[stack.Count - 1], normalized, StringComparison.Ordinal))
            {
                return;
            }

            stack.Add(normalized);
            if (stack.Count <= PromptWorkspaceHistoryLimit)
            {
                return;
            }

            stack.RemoveAt(0);
        }

        internal static string PopFromHistory(List<string> stack)
        {
            if (stack == null || stack.Count == 0)
            {
                return string.Empty;
            }

            int index = stack.Count - 1;
            string value = stack[index];
            stack.RemoveAt(index);
            return value ?? string.Empty;
        }
    
}
