using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using UnityEngine;
using UnityEngine.Networking;
using Verse;
using Verse.Sound;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.UI;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Core.Player2;
using Ustas.RimAI.Core.UI;

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class RelationsProviderSettingsPage
{
    readonly RelationsSettingsPages Pages;

    internal RelationsProviderSettingsPage(RelationsSettingsPages pages)
    {
        Pages = pages;
    }

    RelationsSettings Settings => Pages.Settings;

        internal bool showPromptLanguageSettings;

        internal void DrawTab_APISettings(Listing_Standard listing)
        {
            listing.Label("RimAI.Settings.TextAiOwnedByCore".Translate());
            listing.Label(OpenAIProviderAdapter.CredentialDisplay);
            if (RelationsSettings.TryGetSharedTextConfig(out ApiConfig shared))
            {
                listing.Gap(6f);
                listing.Label(shared.Provider + " / " + shared.GetEffectiveModelName());
            }
            listing.GapLine();
            listing.Label("RimAI.Settings.RelationsModuleHint".Translate());
            listing.Gap();
            DrawDebugSettingsSection(listing);

            listing.Gap();
            Pages.ProviderConnection.DrawLatestDialogueTokenUsage(listing);
            listing.Gap(6f);
            DrawPromptLanguageSettings(listing);
        }

        internal void Draw(Listing_Standard listing, SettingsSearchState search)
        {
            DrawTab_APISettings(listing);
        }

        internal void DrawActionButtonRow(Listing_Standard listing)
        {
            Rect rowRect = listing.GetRect(30f);
            float gap = 6f;
            float btnWidth = (rowRect.width - gap * 2f) / 3f;

            // Test Usability
            bool disableTest = Pages.ApiUsability.IsAnyApiTestRunning();
            Rect testRect = new Rect(rowRect.x, rowRect.y, btnWidth, rowRect.height);
            string testLabel = Pages.ApiUsability._isTestingUsability
                ? "RimChat_UsabilityTesting".Translate()
                : "RimChat_TestUsabilityButton".Translate();
            GUI.color = disableTest ? Color.gray : Color.white;
            if (Widgets.ButtonText(testRect, testLabel, active: !disableTest))
            {
                Pages.ApiUsability.StartUsabilityTest();
            }
            GUI.color = Color.white;

            // Advanced Parameters
            Rect advRect = new Rect(rowRect.x + btnWidth + gap, rowRect.y, btnWidth, rowRect.height);
            if (Widgets.ButtonText(advRect, "RimChat_AdvancedApiParameters".Translate()))
            {
                Find.WindowStack.Add(new Dialog_AdvancedApiParameters());
            }
            Pages.Tooltips.Register(advRect, "RimChat_AdvancedApiParametersTooltip");

            // Debug Window
            Rect debugRect = new Rect(rowRect.x + (btnWidth + gap) * 2f, rowRect.y, btnWidth, rowRect.height);
            if (Widgets.ButtonText(debugRect, "RimChat_OpenApiDebugWindowButton".Translate()))
            {
                Find.WindowStack.Add(new Dialog_ApiDebugObservability());
            }
            Pages.Tooltips.Register(debugRect, "RimChat_OpenApiDebugWindowButtonTooltip");
        }

        internal void DrawDebugSettingsSection(Listing_Standard listing)
        {
            Rect headerRect = listing.GetRect(28f);
            Widgets.Label(headerRect, "RimChat_DebugSettings".Translate());
            listing.GapLine();

            listing.CheckboxLabeled("RimChat_EnableDebugLogging".Translate(), ref Settings.EnableDebugLogging);
            if (Settings.EnableDebugLogging)
            {
                listing.CheckboxLabeled("RimChat_LogAIRequests".Translate(), ref Settings.LogAIRequests);
                listing.CheckboxLabeled("RimChat_LogAIResponses".Translate(), ref Settings.LogAIResponses);
                listing.CheckboxLabeled("RimChat_LogInternals".Translate(), ref Settings.LogInternals);
                listing.CheckboxLabeled("RimChat_LogFullMessages".Translate(), ref Settings.LogFullMessages);
                listing.CheckboxLabeled("RimChat_LogWarnings".Translate(), ref Settings.LogWarnings);
            }
        }

        internal void DrawProviderSelection(Listing_Standard listing)
        {
            Rect radioRect1 = listing.GetRect(24f);
            if (Widgets.RadioButtonLabeled(radioRect1, "RimChat_CloudProviders".Translate(), Settings.UseCloudProviders))
            {
                Settings.UseCloudProviders = true;
            }
            Pages.Tooltips.Register(radioRect1, "RimChat_CloudProvidersDesc");

            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            Rect cloudDescRect = listing.GetRect(Text.LineHeight);
            Widgets.Label(cloudDescRect, "RimChat_CloudProvidersDesc".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            listing.Gap(3f);

            Rect radioRect2 = listing.GetRect(24f);
            if (Widgets.RadioButtonLabeled(radioRect2, "RimChat_LocalProvider".Translate(), !Settings.UseCloudProviders))
            {
                Settings.UseCloudProviders = false;
            }
            Pages.Tooltips.Register(radioRect2, "RimChat_LocalProviderDesc");

            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            Rect localDescRect = listing.GetRect(Text.LineHeight);
            Widgets.Label(localDescRect, "RimChat_LocalProviderDesc".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        internal void DrawPromptLanguageSettings(Listing_Standard listing)
        {
            string systemLanguage = RelationsSettingsPromptLanguage.ResolveSystemPromptLanguage();
            string modeText = Settings.PromptLanguageFollowSystem
                ? "RimChat_OutputLanguageFollowSystem".Translate(systemLanguage)
                : "RimChat_OutputLanguageCustom".Translate();
            string effectiveLanguage = Settings.GetEffectivePromptLanguage();

            Rect compactRow = listing.GetRect(24f);
            Rect toggleRect = new Rect(compactRow.x + compactRow.width - 24f, compactRow.y, 24f, compactRow.height);
            Rect labelRect = new Rect(compactRow.x, compactRow.y, compactRow.width - 30f, compactRow.height);
            string summaryText = "RimChat_OutputLanguage".Translate() + ": " + modeText;
            if (!Settings.PromptLanguageFollowSystem)
            {
                summaryText += " (" + effectiveLanguage + ")";
            }
            Widgets.Label(labelRect, summaryText);
            Pages.Tooltips.Register(labelRect, "RimChat_OutputLanguageTooltip");
            if (Widgets.ButtonText(toggleRect, showPromptLanguageSettings ? "^" : "v"))
            {
                showPromptLanguageSettings = !showPromptLanguageSettings;
                SoundDefOf.Click.PlayOneShotOnCamera(null);
            }

            if (!showPromptLanguageSettings)
            {
                return;
            }

            listing.Gap(2f);
            Rect followRect = listing.GetRect(24f);
            if (Widgets.RadioButtonLabeled(followRect, "RimChat_OutputLanguageFollowSystem".Translate(systemLanguage), Settings.PromptLanguageFollowSystem))
            {
                Settings.PromptLanguageFollowSystem = true;
            }
            Pages.Tooltips.Register(followRect, "RimChat_OutputLanguageFollowSystemTooltip");
            Rect customRect = listing.GetRect(24f);
            if (Widgets.RadioButtonLabeled(customRect, "RimChat_OutputLanguageCustom".Translate(), !Settings.PromptLanguageFollowSystem))
            {
                Settings.PromptLanguageFollowSystem = false;
            }
            Pages.Tooltips.Register(customRect, "RimChat_OutputLanguageCustomTooltip");
            if (!Settings.PromptLanguageFollowSystem)
            {
                Rect customLangRect = listing.GetRect(24f);
                Settings.PromptLanguageOverride = Pages.ProviderCloud.DrawTextFieldWithPlaceholder(customLangRect, Settings.PromptLanguageOverride, "RimChat_OutputLanguageCustomPlaceholder".Translate());
                Pages.Tooltips.Register(customLangRect, "RimChat_OutputLanguageCustomTooltip");
            }
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            Rect hintRect = listing.GetRect(Text.LineHeight * 2f);
            Widgets.Label(hintRect, "RimChat_OutputLanguageHint".Translate());
            Pages.Tooltips.Register(hintRect, "RimChat_OutputLanguageTooltip");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }
}
