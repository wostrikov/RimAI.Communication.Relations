using System.IO;
using RimWorld;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Core.Handshake;
using Ustas.RimAI.Core.Modules;
using Ustas.RimAI.Core.Storage;

namespace Ustas.RimAI.Communication.Relations.Module
{
    public class RelationsMod : Mod
    {
        public const string HandshakeModuleVersion = "1.5.9";
        public static RelationsSettings Settings;
        public static RelationsMod Instance;
        public RelationsSettings InstanceSettings => Settings;

        public RelationsMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<RelationsSettings>();
            RimAiHandshake.TryActivate(
                RimAiHandshakeDescriptor.Current(RimAiModuleIds.Relations, HandshakeModuleVersion, isOptional: true),
                RelationsComposition.Current.Start);
        }

        public override string SettingsCategory()
        {
            return Content?.Name ?? "RimAI.Communication.Relations";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            RimAISettingsNavigation.Open("communication", "relations");
            bool workbenchActive = Ustas.RimAI.Communication.Relations.UI.RelationsSettingsWindow.IsPromptWorkbenchPage;
            ResizeSettingsWindowForWorkbench(workbenchActive);

            bool guiChanged = GUI.changed;
            Ustas.RimAI.Communication.Relations.UI.RelationsSettingsWindow.Draw(inRect, Settings);
            if (Ustas.RimAI.Communication.Relations.UI.RelationsSettingsWindow.IsPromptWorkbenchPage)
                GUI.changed = guiChanged;

            if (Ustas.RimAI.Communication.Relations.UI.RelationsSettingsWindow.IsPromptWorkbenchPage != workbenchActive)
                ResizeSettingsWindowForWorkbench(Ustas.RimAI.Communication.Relations.UI.RelationsSettingsWindow.IsPromptWorkbenchPage);
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
            if (!LocalStorage.Current.DirectoryExists(path))
            {
                LocalStorage.Current.CreateDirectory(path);
            }
            return path;
        }
    }
}
