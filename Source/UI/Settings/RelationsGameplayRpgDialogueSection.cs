using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.UI;
using Ustas.RimAI.Core.UI;

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class RelationsGameplayRpgDialogueSection
{
    readonly RelationsSettingsPages Pages;

    internal RelationsGameplayRpgDialogueSection(RelationsSettingsPages pages)
    {
        Pages = pages;
    }

    RelationsSettings Settings => Pages.Settings;

        internal void DrawRpgNonPromptSettings(Listing_Standard listing)
        {
            Rect enableDialogueRect = listing.GetRect(24f);
            Widgets.CheckboxLabeled(enableDialogueRect, "RimChat_EnableRPGDialogue".Translate(), ref Settings.EnableRPGDialogue);
            Pages.Tooltips.Register(enableDialogueRect, "RimChat_EnableRPGDialogueTooltip");

            Rect enableApiRect = listing.GetRect(24f);
            Widgets.CheckboxLabeled(enableApiRect, "RimChat_EnableRPGAPI".Translate(), ref Settings.EnableRPGAPI);
            Pages.Tooltips.Register(enableApiRect, "RimChat_EnableRPGAPITooltip");

            Rect selfStatusRect = listing.GetRect(24f);
            Widgets.CheckboxLabeled(selfStatusRect, "RimChat_RPGInjectSelfStatus".Translate(), ref Settings.RPGInjectSelfStatus);
            Pages.Tooltips.Register(selfStatusRect, "RimChat_RPGInjectSelfStatusTooltip");

            Rect interlocutorStatusRect = listing.GetRect(24f);
            Widgets.CheckboxLabeled(interlocutorStatusRect, "RimChat_RPGInjectInterlocutorStatus".Translate(), ref Settings.RPGInjectInterlocutorStatus);
            Pages.Tooltips.Register(interlocutorStatusRect, "RimChat_RPGInjectInterlocutorStatusTooltip");

            Rect factionBackgroundRect = listing.GetRect(24f);
            Widgets.CheckboxLabeled(factionBackgroundRect, "RimChat_RPGInjectFactionBackground".Translate(), ref Settings.RPGInjectFactionBackground);
            Pages.Tooltips.Register(factionBackgroundRect, "RimChat_RPGInjectFactionBackgroundTooltip");

            Rect nonVerbalRect = listing.GetRect(24f);
            Widgets.CheckboxLabeled(nonVerbalRect, "RimChat_EnableRPGNonVerbalPawnSpeech".Translate(), ref Settings.EnableRPGNonVerbalPawnSpeech);
            Pages.Tooltips.Register(nonVerbalRect, "RimChat_EnableRPGNonVerbalPawnSpeechTooltip");

            Rect sceneTagsRow = listing.GetRect(24f);
            Rect sceneTagsLabel = new Rect(sceneTagsRow.x, sceneTagsRow.y, 150f, sceneTagsRow.height);
            Rect sceneTagsInput = new Rect(sceneTagsLabel.xMax + 6f, sceneTagsRow.y, sceneTagsRow.width - sceneTagsLabel.width - 6f, sceneTagsRow.height);
            Widgets.Label(sceneTagsLabel, "RimChat_RpgSceneTags".Translate());
            Pages.Tooltips.Register(sceneTagsLabel, "RimChat_RpgSceneTagsTooltip");
            string sceneTags = Settings.RpgManualSceneTagsCsv ?? string.Empty;
            string editedTags = Widgets.TextField(sceneTagsInput, sceneTags);
            Pages.Tooltips.Register(sceneTagsInput, "RimChat_RpgSceneTagsTooltip");
            if (!string.Equals(editedTags, sceneTags, System.StringComparison.Ordinal))
            {
                Settings.RpgManualSceneTagsCsv = editedTags;
                Pages.RpgEditors._rpgPreviewUpdateCooldown = 0;
            }

            listing.Gap(4f);
            Rect openWorkbenchRect = listing.GetRect(28f);
            if (Widgets.ButtonText(openWorkbenchRect, "RimChat_RpgOpenPromptWorkbench".Translate()))
            {
                Pages.PromptWorkbench.OpenPromptWorkbenchWindowForRpg();
            }

            Pages.Tooltips.Register(openWorkbenchRect, "RimChat_RpgOpenPromptWorkbenchTooltip");
        }

        internal void ResetRpgNonPromptSettingsToDefault()
        {
            Settings.EnableRPGDialogue = true;
            Settings.EnableRPGAPI = true;
            Settings.RPGInjectSelfStatus = true;
            Settings.RPGInjectInterlocutorStatus = true;
            Settings.RPGInjectFactionBackground = true;
            Settings.EnableRPGNonVerbalPawnSpeech = true;
            Settings.RpgManualSceneTagsCsv = "scene:daily";
        }
    
}
