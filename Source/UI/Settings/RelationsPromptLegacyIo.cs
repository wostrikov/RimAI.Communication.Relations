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

internal sealed class RelationsPromptLegacyIo
{
    internal readonly RelationsPromptLegacyEditors Owner;

    internal RelationsPromptLegacyIo(RelationsPromptLegacyEditors owner)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    internal RelationsSettingsPages Pages => Owner.Pages;
    internal RelationsSettings Settings => Owner.Settings;
    internal SystemPromptConfig SystemPromptConfigData => Owner.SystemPromptConfigData;

        internal void DrawPromptActionButtonsNative(Listing_Standard listing)
        {
            Rect buttonRowRect = listing.GetRect(28f);
            float btnWidth = (buttonRowRect.width - 30f) / 4;

            Rect saveRect = new Rect(buttonRowRect.x, buttonRowRect.y, btnWidth, buttonRowRect.height);
            if (Widgets.ButtonText(saveRect, "RimChat_SavePrompt".Translate()))
            {
                SaveSystemPromptConfig();
                Messages.Message("RimChat_PromptSaved".Translate(), MessageTypeDefOf.NeutralEvent, false);
            }

            Rect resetRect = new Rect(buttonRowRect.x + btnWidth + 10f, buttonRowRect.y, btnWidth, buttonRowRect.height);
            if (Widgets.ButtonText(resetRect, "RimChat_ResetToDefault".Translate()))
            {
                ShowResetPromptConfigConfirmation();
            }

            Rect exportRect = new Rect(buttonRowRect.x + (btnWidth + 10f) * 2, buttonRowRect.y, btnWidth, buttonRowRect.height);
            if (Widgets.ButtonText(exportRect, "RimChat_ExportPrompts".Translate()))
            {
                ShowExportSystemPromptDialog();
            }

            Rect importRect = new Rect(buttonRowRect.x + (btnWidth + 10f) * 3, buttonRowRect.y, btnWidth, buttonRowRect.height);
            if (Widgets.ButtonText(importRect, "RimChat_ImportPrompts".Translate()))
            {
                ShowImportSystemPromptDialog();
            }
        }

        internal void SaveSystemPromptConfig()
        {
            PromptPersistenceService.Instance.SaveConfig(SystemPromptConfigData);
            Settings.SaveRpgPromptTextsToCustom();
            Owner._previewUpdateCooldown = 0;
        }

        internal void ShowResetPromptConfigConfirmation()
        {
            Dialog_MessageBox dialog = Dialog_MessageBox.CreateConfirmation(
                "RimChat_ResetPromptConfigConfirm".Translate(),
                () =>
                {
                    PromptPersistenceService.Instance.ResetToDefault();
                    Owner._systemPromptConfig = PromptPersistenceService.Instance.LoadConfigReadOnly();
                    Settings.ReloadPromptUnifiedCatalogFromStorage();
                    Owner._selectedApiActionIndex = -1;
                    Owner._selectedDecisionRuleIndex = -1;
                    Owner._previewUpdateCooldown = 0;
                    Pages.PromptLegacyPreview.SyncBuffersToData();
                    Messages.Message("RimChat_PromptConfigReset".Translate(), MessageTypeDefOf.NeutralEvent, false);
                },
                true,
                "RimChat_ResetConfirmTitle".Translate()
            );
            Find.WindowStack.Add(dialog);
        }

        internal void ShowExportSystemPromptDialog()
        {
            string defaultPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "RimChat_PromptBundle.json");
            Messages.Message("RimChat_PromptBundleExportFormatHint".Translate(), MessageTypeDefOf.NeutralEvent, false);
            Find.WindowStack.Add(new Dialog_PromptBundleExport(defaultPath, (path, modules) =>
            {
                try
                {
                    // Export should include the latest in-editor changes, not only last saved files.
                    Pages.PromptLegacyPreview.SyncBuffersToData();
                    SaveSystemPromptConfig();
                    Settings.SaveRpgPromptTextsToCustom();
                }
                catch (Exception ex)
                {
                    Log.Warning($"[RimAI.Relations] Failed to flush latest prompt edits before export: {ex.Message}");
                }

                bool exported = modules == null
                    ? PromptPersistenceService.Instance.ExportConfig(path)
                    : PromptPersistenceService.Instance.ExportConfig(path, modules);
                if (exported)
                {
                    Messages.Message("RimChat_ExportSuccess".Translate(path), MessageTypeDefOf.NeutralEvent, false);
                }
                else
                {
                    Messages.Message("RimChat_ExportFailed".Translate(), MessageTypeDefOf.NegativeEvent, false);
                }
            }));
        }

        internal void ShowImportSystemPromptDialog()
        {
            string defaultPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "RimChat_PromptBundle.json");
            Find.WindowStack.Add(new Dialog_LoadFile(defaultPath, (path) =>
            {
                try
                {
                    if (PromptPersistenceService.Instance.TryGetImportPreview(path, out PromptBundleImportPreview preview))
                    {
                        Find.WindowStack.Add(new Dialog_PromptBundleImportPreview(preview, modules =>
                        {
                            if (PromptPersistenceService.Instance.ImportConfig(path, modules))
                            {
                                RefreshPromptEditorStateAfterImport();
                                Messages.Message("RimChat_ImportSuccess".Translate(), MessageTypeDefOf.NeutralEvent, false);
                                return;
                            }

                            ShowPromptBundleImportFailureMessage();
                        }));
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[RimAI.Relations] Import preview failed unexpectedly: {ex.Message}");
                }

                ShowPromptBundleImportFailureMessage();
            }));
        }

        internal void ShowPromptBundleImportFailureMessage()
        {
            PromptPersistenceService service = PromptPersistenceService.Instance;
            PromptBundleImportFailure failure = service.GetLastPromptBundleImportFailure();
            string code = service.GetLastPromptBundleImportErrorCode();

            string reason;
            switch (failure)
            {
                case PromptBundleImportFailure.PresetFileDetected:
                    reason = "RimChat_PromptBundleImportFail_PresetFile".Translate().ToString();
                    break;
                case PromptBundleImportFailure.NotPromptBundle:
                    reason = "RimChat_PromptBundleImportFail_NotBundle".Translate().ToString();
                    break;
                case PromptBundleImportFailure.InvalidJson:
                    reason = "RimChat_PromptBundleImportFail_InvalidJson".Translate().ToString();
                    break;
                case PromptBundleImportFailure.EmptyFile:
                    reason = "RimChat_PromptBundleImportFail_EmptyFile".Translate().ToString();
                    break;
                case PromptBundleImportFailure.FileNotFound:
                    reason = "RimChat_PromptBundleImportFail_FileNotFound".Translate().ToString();
                    break;
                case PromptBundleImportFailure.InvalidBundlePayload:
                    reason = "RimChat_PromptBundleImportFail_InvalidBundlePayload".Translate().ToString();
                    break;
                default:
                    reason = "RimChat_ImportFailed".Translate().ToString();
                    break;
            }

            if (!string.IsNullOrWhiteSpace(code))
            {
                reason = "RimChat_PromptBundleImportFail_WithCode".Translate(reason, code).ToString();
            }

            Messages.Message(reason, MessageTypeDefOf.NegativeEvent, false);
        }

        internal void RefreshPromptEditorStateAfterImport()
        {
            Owner._systemPromptConfig = PromptPersistenceService.Instance.LoadConfigReadOnly();
            Settings.ReloadPromptUnifiedCatalogFromStorage();
            Settings.LoadRpgPromptTextsFromCustom();
            Owner._selectedApiActionIndex = -1;
            Owner._selectedDecisionRuleIndex = -1;
            Owner._previewUpdateCooldown = 0;
            Pages.PromptLegacyPreview.SyncBuffersToData();
        }
}
