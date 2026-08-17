using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Comp;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Core.Modules;
using Ustas.RimAI.Core.Relations;

namespace Ustas.RimAI.Communication.Relations.Module
{
    public class RelationsMod : Mod
    {
        public static RelationsSettings Settings;
        public static RelationsMod Instance;
        public RelationsSettings InstanceSettings => Settings;

        public RelationsMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<RelationsSettings>();
            Settings?.EnsureRpgPromptTextsLoaded();
            Settings?.EnsurePawnPersonalityTokenForRpgChannelsSafe();
            RefreshDefaultPresetSnapshotOnStartup();

            // Initialize FactionPromptManager
            FactionPromptManager.Instance.Initialize();

            // Apply Harmony patches
            var harmony = new Harmony("ustas.rimai.communication.relations");
            Ustas.RimAI.Communication.Relations.Patches.HarmonyPatchStartupSelfCheck.Run();
            harmony.PatchAll();

            // Initialize custom patches that require dynamic method lookup
            Ustas.RimAI.Communication.Relations.Patches.CommsConsolePatch.Initialize(harmony);
            Ustas.RimAI.Communication.Relations.Patches.QuestGenPatch.Initialize(harmony);

            // Inject CompPawnDialogue to all eligible pawn ThingDefs after all defs are loaded
            LongEventHandler.ExecuteWhenFinished(PawnDialogueCompDefInjector.EnsureInjected);

            RelationsApplicationAccess.Register(new RelationsApplication());
            RimAIModuleRegistry.Current.Register(new RimAIModuleDescriptor(
                "relations",
                "RimAI.Communication.Relations",
                "RimAI.Communication.Relations",
                "Communication",
                "RimAI.Communication"));
            RimAISettingsContributionRegistry.Current.Register(new DelegateSettingsContributor(
                "relations",
                "Relations",
                RimAISettingsSection.Module,
                20,
                listing => DrawCoreRelationsSummary((Listing_Standard)listing),
                "communication",
                "relations"));
            Log.Message("[RimAI.Relations] Mod initialized successfully.");
        }

        static void DrawCoreRelationsSummary(Listing_Standard listing)
        {
            listing.Label("RimAI.Settings.TextAiOwnedByCore".Translate());
            listing.Label(AI.OpenAIProviderAdapter.CredentialDisplay);
            listing.Gap(6f);
            listing.Label("RimAI.Settings.RelationsModuleHint".Translate());
        }

        private static void RefreshDefaultPresetSnapshotOnStartup()
        {
            if (Settings == null)
            {
                return;
            }

            try
            {
                // Force-refresh immutable default preset payload from Prompt/Default files on every startup.
                IPromptPresetService presetService = new PromptPresetService();
                PromptPresetStoreConfig store = presetService.LoadAll(Settings);
                presetService.SaveAll(store);
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimAI.Relations] Default preset refresh on startup failed: {ex.Message}");
            }
        }

        public override string SettingsCategory()
        {
            return Content?.Name ?? "RimAI.Communication.Relations";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            RimAISettingsNavigation.Open("communication", "relations");
            bool workbenchActive = Settings.selectedTab == 2;
            ResizeSettingsWindowForWorkbench(workbenchActive);

            if (workbenchActive)
            {
                // Escape hatch: a small "back to settings" button so the user is never trapped.
                // selectedTab is a sticky instance field — without this, closing & reopening
                // the dialog while on this tab leaves the user with no way to navigate away.
                Rect backRect = new Rect(inRect.x, inRect.y, 140f, 24f);
                if (Widgets.ButtonText(backRect, "RimChat_ReturnToSettings".Translate()))
                {
                    Settings.selectedTab = 0;
                    return;
                }

                Rect contentRect = new Rect(inRect.x, inRect.y + 28f, inRect.width, inRect.height - 28f);
                // Block GUI.changed from propagating to parent Dialog_ModSettings,
                // which would otherwise trigger WriteSettings() → ExposeData() (80+ Scribe fields) every Repaint.
                bool guiChanged = GUI.changed;
                Settings.DrawTab_PromptSettingsDirect(contentRect);
                GUI.changed = guiChanged;
            }
            else
            {
                Settings.DoWindowContents(inRect);
            }
        }

        private static void ResizeSettingsWindowForWorkbench(bool workbenchActive)
        {
            Dialog_ModSettings settingsWindow = Find.WindowStack.WindowOfType<Dialog_ModSettings>();
            if (settingsWindow == null) return;

            settingsWindow.doCloseX = true;
            settingsWindow.draggable = true;
            settingsWindow.closeOnAccept = false;
            settingsWindow.absorbInputAroundWindow = false;
            settingsWindow.preventCameraMotion = false;
            settingsWindow.closeOnClickedOutside = false;

            float targetWidth = workbenchActive
                ? Mathf.Min(Verse.UI.screenWidth * 0.9f, 1580f)
                : 900f;
            float targetHeight = workbenchActive
                ? Mathf.Min(Verse.UI.screenHeight * 0.9f, 960f)
                : 700f;

            if (Mathf.Abs(settingsWindow.windowRect.width - targetWidth) > 1f ||
                Mathf.Abs(settingsWindow.windowRect.height - targetHeight) > 1f)
            {
                settingsWindow.windowRect.width = targetWidth;
                settingsWindow.windowRect.height = targetHeight;
                settingsWindow.windowRect.x = (Verse.UI.screenWidth - targetWidth) / 2f;
                settingsWindow.windowRect.y = (Verse.UI.screenHeight - targetHeight) / 2f;
            }
        }

        /// <summary>
        /// Get mod settings folder path
        /// </summary>
        public string GetSettingsFolderPath()
        {
            string path = Path.Combine(GenFilePaths.ConfigFolderPath, "Ustas.RimAI.Communication.Relations");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return path;
        }
    }
}
