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

internal sealed class RelationsRimTalkTemplateEditors
{
    internal readonly RelationsRimTalkTabPage Owner;

    internal RelationsRimTalkTemplateEditors(RelationsRimTalkTabPage owner)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    internal RelationsSettingsPages Pages => Owner.Pages;
    internal RelationsSettings Settings => Owner.Settings;

        internal void DrawRimTalkChannelTemplateTextArea(Rect rect, RimTalkChannelCompatConfig config)
        {
            string current = config?.CompatTemplate ?? string.Empty;
            const float validationStatusHeight = 24f;
            const float validationGap = 2f;
            Rect contentRect = new Rect(rect.x, rect.y, rect.width, Mathf.Max(24f, rect.height - validationStatusHeight - validationGap));
            float contentHeight = Mathf.Max(contentRect.height, Text.CalcHeight(current, contentRect.width - 16f) + 10f);
            Rect viewRect = new Rect(0f, 0f, contentRect.width - 16f, contentHeight);
            Pages.RpgEditors._rimTalkCompatTemplateScroll = GUI.BeginScrollView(contentRect, Pages.RpgEditors._rimTalkCompatTemplateScroll, viewRect);
            string edited = GUI.TextArea(viewRect, current);
            GUI.EndScrollView();
            Rect validationRect = new Rect(rect.x, contentRect.yMax + validationGap, rect.width, validationStatusHeight);
            DrawRimTalkTemplateValidationStatus(validationRect, edited);

            if (!string.Equals(edited, current, StringComparison.Ordinal))
            {
                RimTalkChannelCompatConfig changed = config?.Clone() ?? RimTalkChannelCompatConfig.CreateDefault();
                changed.CompatTemplate = edited;
                Settings.SetRimTalkChannelConfig(Owner._rimTalkEditorChannel, changed);
            }
        }

        internal void DrawRimTalkPersonaCopyTemplateEditor(Listing_Standard listing)
        {
            listing.Gap(4f);
            listing.Label("RimChat_RimTalkPersonaCopyTemplate".Translate());
            string current = Settings.RimTalkPersonaCopyTemplate ?? RelationsSettings.DefaultRimTalkPersonaCopyTemplate;
            const float validationStatusHeight = 24f;
            const float validationGap = 2f;
            Rect rect = listing.GetRect(116f);
            Rect contentRect = new Rect(rect.x, rect.y, rect.width, Mathf.Max(24f, rect.height - validationStatusHeight - validationGap));
            float contentHeight = Mathf.Max(contentRect.height, Text.CalcHeight(current, contentRect.width - 16f) + 10f);
            Rect viewRect = new Rect(0f, 0f, contentRect.width - 16f, contentHeight);
            Owner._rimTalkPersonaCopyTemplateScroll = GUI.BeginScrollView(contentRect, Owner._rimTalkPersonaCopyTemplateScroll, viewRect);
            string edited = GUI.TextArea(viewRect, current);
            GUI.EndScrollView();
            Rect validationRect = new Rect(rect.x, contentRect.yMax + validationGap, rect.width, validationStatusHeight);
            DrawRimTalkTemplateValidationStatus(validationRect, edited);

            if (!string.Equals(edited, current, StringComparison.Ordinal))
            {
                Settings.RimTalkPersonaCopyTemplate = edited;
            }

            DrawRimTalkManualPersonaCopyButton(listing);
        }

        internal void DrawRimTalkManualPersonaCopyButton(Listing_Standard listing)
        {
            listing.Gap(4f);
            Rect buttonRect = listing.GetRect(28f);
            if (!Widgets.ButtonText(buttonRect, "RimChat_RimTalkPersonaManualCopyButton".Translate()))
            {
                return;
            }

            GameComponent_RPGManager manager = Current.Game?.GetComponent<GameComponent_RPGManager>();
            if (manager == null)
            {
                Messages.Message("RimChat_RPGPawnPersonaNeedGame".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            bool changed = manager.TrySyncAllColonyPawnPersonasFromRimTalk(
                out int updated,
                out int cleared,
                out int unchanged,
                out int skipped);
            MessageTypeDef messageType = changed ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent;
            Messages.Message(
                "RimChat_RimTalkPersonaManualCopySummary".Translate(updated, cleared, unchanged, skipped),
                messageType,
                false);
        }

        internal void DrawRimTalkTemplateValidationStatus(
            Rect rect,
            string templateText,
            TemplateVariableValidationContext validationContext = null)
        {
            TemplateVariableValidationResult result = string.IsNullOrWhiteSpace(templateText)
                ? new TemplateVariableValidationResult()
                : PromptPersistenceService.Instance.ValidateTemplateVariables(templateText, validationContext);
            string statusText = Pages.PromptLegacyValidation.BuildLiveValidationStatusText(result, templateText);
            Color oldColor = GUI.color;
            GUI.color = Pages.PromptLegacyValidation.ResolveLiveValidationStatusColor(result, templateText);
            Widgets.Label(rect, statusText);
            GUI.color = oldColor;
        }

        internal void AppendVariableToCurrentRimTalkTemplate(string variableName)
        {
            string normalizedName = variableName?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return;
            }

            if (Pages.PromptWorkspaceBuffers.TryInsertVariableTokenToPromptWorkspace("{{ " + normalizedName + " }}"))
            {
                return;
            }

            RimTalkChannelCompatConfig config = Pages.PromptWorkbench.IsEntryDrivenWorkbenchChannelActive()
                ? Pages.PromptWorkbench.GetWorkbenchEditingChannelConfig()
                : Settings.GetRimTalkChannelConfigClone(Owner._rimTalkEditorChannel);
            string token = "{{ " + normalizedName + " }}";
            RimTalkPromptEntryConfig entry = Owner.GetSelectedRimTalkPromptEntry(config);
            if (entry != null)
            {
                string currentEntry = entry.Content ?? string.Empty;
                if (ContainsVariableToken(currentEntry, normalizedName))
                {
                    Messages.Message("RimChat_RimTalkVariableAlreadyInTemplate".Translate(), MessageTypeDefOf.NeutralEvent, false);
                    return;
                }

                if (!TryInsertVariableIntoFocusedEditor(ref currentEntry, normalizedName))
                {
                    currentEntry = string.IsNullOrWhiteSpace(currentEntry)
                        ? token
                        : currentEntry.TrimEnd() + "\n" + token;
                }

                entry.Content = currentEntry;
                Settings.SetRimTalkChannelConfig(Owner._rimTalkEditorChannel, config);
                Owner._rimTalkEntryContentBufferEntryId = entry.Id ?? string.Empty;
                Owner._rimTalkEntryContentBuffer = currentEntry;
                Owner._rimTalkEntryContentSnapshot = currentEntry;
                Messages.Message("RimChat_RimTalkVariableInserted".Translate(token), MessageTypeDefOf.NeutralEvent, false);
                return;
            }

            string current = config.CompatTemplate ?? string.Empty;
            if (ContainsVariableToken(current, normalizedName))
            {
                Messages.Message("RimChat_RimTalkVariableAlreadyInTemplate".Translate(), MessageTypeDefOf.NeutralEvent, false);
                return;
            }

            config.CompatTemplate = string.IsNullOrWhiteSpace(current)
                ? token
                : current.TrimEnd() + "\n" + token;
            Settings.SetRimTalkChannelConfig(Owner._rimTalkEditorChannel, config);
            Messages.Message("RimChat_RimTalkVariableInserted".Translate(token), MessageTypeDefOf.NeutralEvent, false);
        }

        internal static bool ContainsVariableToken(string text, string variableName)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(variableName))
            {
                return false;
            }

            string normalized = variableName.Trim();
            string[] patterns =
            {
                "{{" + normalized + "}}",
                "{{ " + normalized + "}}",
                "{{" + normalized + " }}",
                "{{ " + normalized + " }}"
            };

            for (int i = 0; i < patterns.Length; i++)
            {
                if (text.IndexOf(patterns[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        internal bool TryInsertVariableIntoFocusedEditor(ref string content, string variableName)
        {
            TextEditor editor = GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl) as TextEditor;
            string text = content ?? string.Empty;
            if (editor == null || editor.cursorIndex < 0 || editor.cursorIndex > text.Length)
            {
                return false;
            }

            int cursor = editor.cursorIndex;
            int prefixStart = cursor - 1;
            while (prefixStart >= 0 && (char.IsLetterOrDigit(text[prefixStart]) || text[prefixStart] == '.' || text[prefixStart] == '_'))
            {
                prefixStart--;
            }

            prefixStart++;
            if (prefixStart < cursor)
            {
                string prefix = text.Substring(prefixStart, cursor - prefixStart);
                if (variableName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    text = text.Remove(prefixStart, cursor - prefixStart);
                    cursor = prefixStart;
                }
            }

            int left = cursor - 1;
            while (left >= 0 && char.IsWhiteSpace(text[left]))
            {
                left--;
            }

            bool insideOpenToken = left >= 1 && text[left] == '{' && text[left - 1] == '{';
            string insert = insideOpenToken ? variableName : "{{ " + variableName + " }}";
            if (insideOpenToken)
            {
                if (cursor > 0 && text[cursor - 1] == '{')
                {
                    insert = " " + insert;
                }

                int right = cursor;
                while (right < text.Length && char.IsWhiteSpace(text[right]))
                {
                    right++;
                }

                bool hasClosing = right < text.Length - 1 && text[right] == '}' && text[right + 1] == '}';
                if (!hasClosing)
                {
                    insert += " }}";
                }
            }
            else
            {
                insert = AddTokenSpacing(text, cursor, insert);
            }

            text = text.Insert(cursor, insert);
            int newCursor = cursor + insert.Length;
            if (insideOpenToken)
            {
                int close = text.IndexOf("}}", cursor, StringComparison.Ordinal);
                newCursor = close >= 0 ? close + 2 : newCursor;
            }

            editor.text = text;
            editor.cursorIndex = newCursor;
            editor.selectIndex = newCursor;
            content = text;
            return true;
        }

        internal string AddTokenSpacing(string text, int cursor, string token)
        {
            string insert = token ?? string.Empty;
            if (NeedsLeadingSpace(text, cursor))
            {
                insert = " " + insert;
            }

            if (NeedsTrailingSpace(text, cursor))
            {
                insert += " ";
            }

            return insert;
        }

        internal bool NeedsLeadingSpace(string text, int cursor)
        {
            return cursor > 0 && !char.IsWhiteSpace(text[cursor - 1]);
        }

        internal bool NeedsTrailingSpace(string text, int cursor)
        {
            return cursor < (text?.Length ?? 0) && !char.IsWhiteSpace(text[cursor]);
        }

        internal string GetCurrentChannelToken()
        {
            return Owner._rimTalkEditorChannel == RimTalkPromptChannel.Diplomacy ? "diplomacy" : "rpg";
        }
}
