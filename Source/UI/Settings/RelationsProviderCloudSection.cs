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

internal sealed class RelationsProviderCloudSection
{
    internal readonly RelationsSettingsPages Pages;

    internal RelationsProviderCloudSection(RelationsSettingsPages pages)
    {
        Pages = pages;
    }

    internal RelationsSettings Settings => Pages.Settings;

        internal void DrawCloudProvidersSection(Listing_Standard listing)
        {
            Rect headerRect = listing.GetRect(24f);

            float addBtnSize = 24f;
            Rect addButtonRect = new Rect(headerRect.x + headerRect.width - addBtnSize, headerRect.y, addBtnSize, addBtnSize);
            headerRect.width -= (addBtnSize + 5f);

            Widgets.Label(headerRect, "RimChat_CloudApiConfigurations".Translate());

            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            Rect descRect = listing.GetRect(Text.LineHeight * 2);
            descRect.width -= 35f;
            Widgets.Label(descRect, "RimChat_CloudApiConfigurationsDesc".Translate());
            GUI.color = Color.white;

            Color prevColor = GUI.color;
            GUI.color = new Color(0.3f, 0.9f, 0.3f);
            if (Widgets.ButtonText(addButtonRect, "+"))
            {
                SoundDefOf.Click.PlayOneShotOnCamera(null);
                Settings.CloudConfigs.Add(new ApiConfig());
            }
            GUI.color = prevColor;

            Text.Font = GameFont.Small;
            listing.Gap(6f);

            // Table Headers
            Rect tableHeaderRect = listing.GetRect(20f);
            float x = tableHeaderRect.x;
            float y = tableHeaderRect.y;
            float height = tableHeaderRect.height;
            float totalWidth = tableHeaderRect.width;

            float providerWidth = 90f;
            float modelWidth = 180f;
            float controlsWidth = 100f;

            Rect providerHeaderRect = new Rect(x, y, providerWidth, height);
            Widgets.Label(providerHeaderRect, "RimChat_ProviderHeader".Translate());
            Pages.Tooltips.Register(providerHeaderRect, "RimChat_ApiProviderFieldTooltip");

            float middleStartX = x + providerWidth + 5f;
            Rect apiKeyHeaderRect = new Rect(middleStartX, y, 180f, height);
            Widgets.Label(apiKeyHeaderRect, "RimChat_ApiKeyHeader".Translate());
            Pages.Tooltips.Register(apiKeyHeaderRect, "RimChat_ApiKeyFieldTooltip");

            Rect modelHeaderRect = new Rect(totalWidth - controlsWidth - modelWidth - 5f, y, modelWidth, height);
            Widgets.Label(modelHeaderRect, "RimChat_ModelHeader".Translate());
            Pages.Tooltips.Register(modelHeaderRect, "RimChat_ApiModelFieldTooltip");

            Rect enabledHeaderRect = new Rect(totalWidth - controlsWidth + 5f, y, controlsWidth, height);
            Widgets.Label(enabledHeaderRect, "RimChat_EnabledHeader".Translate());

            listing.Gap(3f);

            for (int i = 0; i < Settings.CloudConfigs.Count; i++)
            {
                if (DrawCloudConfigRow(listing, Settings.CloudConfigs[i], i))
                {
                    Settings.CloudConfigs.RemoveAt(i);
                    i--;
                }
                listing.Gap(2f);
            }

            Text.Font = GameFont.Small;
        }

        internal bool DrawCloudConfigRow(Listing_Standard listing, ApiConfig config, int index)
        {
            Text.Font = GameFont.Tiny;

            Rect rowRect = listing.GetRect(24f);
            float x = rowRect.x;
            float y = rowRect.y;
            float height = rowRect.height;
            float totalWidth = rowRect.width;

            float providerWidth = 90f;
            float modelWidth = 180f;
            float controlsWidth = 100f;
            float gap = 5f;

            float middleZoneWidth = totalWidth - providerWidth - modelWidth - controlsWidth - (gap * 3);
            float middleStartX = x + providerWidth + gap;

            Color originalColor = GUI.color;
            if (!config.IsEnabled)
            {
                GUI.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
            }

            // 1. Provider Dropdown
            DrawProviderDropdown(x, y, height, providerWidth, config);

            // 2. Middle Zone (API Key or Custom URL)
            if (config.Provider == AIProvider.Custom)
            {
                float modeWidth = Mathf.Clamp(middleZoneWidth * 0.22f, 88f, 136f);
                float editableWidth = Mathf.Max(60f, middleZoneWidth - modeWidth - (gap * 2));
                float keyWidth = editableWidth * 0.38f;
                float urlWidth = editableWidth - keyWidth;

                DrawApiKeyInput(middleStartX, y, height, keyWidth, config);
                DrawBaseUrlInput(middleStartX + keyWidth + gap, y, height, urlWidth, config);
                DrawCustomUrlModeSelector(
                    middleStartX + keyWidth + gap + urlWidth + gap,
                    y,
                    height,
                    modeWidth,
                    config);
            }
            else
            {
                DrawApiKeyInput(middleStartX, y, height, middleZoneWidth, config);
            }

            // 3. Model
            float modelStartX = middleStartX + middleZoneWidth + gap;
            DrawModelSelector(modelStartX, y, height, modelWidth, config);

            GUI.color = originalColor;

            // 4. Controls (Enable + Reorder + Delete)
            float btnSize = 22f;
            float btnGap = 2f;

            float deleteX = totalWidth - btnSize;
            float downX = deleteX - btnGap - btnSize;
            float upX = downX - btnGap - btnSize;

            float controlsStartX = totalWidth - controlsWidth;
            float checkboxSpaceWidth = upX - controlsStartX;
            float checkboxX = controlsStartX + (checkboxSpaceWidth - 24f) / 2f;

            Rect toggleRect = new Rect(checkboxX, y, 24f, height);
            Widgets.Checkbox(new Vector2(toggleRect.x, toggleRect.y), ref config.IsEnabled, 20f);
            if (Mouse.IsOver(toggleRect)) TooltipHandler.TipRegion(toggleRect, "RimChat_EnableDisableTooltip".Translate());

            // Reorder buttons
            Rect upButtonRect = new Rect(upX, y, btnSize, height);
            if (Widgets.ButtonText(upButtonRect, "^") && index > 0)
            {
                SoundDefOf.Click.PlayOneShotOnCamera(null);
                (Settings.CloudConfigs[index], Settings.CloudConfigs[index - 1]) = (Settings.CloudConfigs[index - 1], Settings.CloudConfigs[index]);
            }

            Rect downButtonRect = new Rect(downX, y, btnSize, height);
            if (Widgets.ButtonText(downButtonRect, "v") && index < Settings.CloudConfigs.Count - 1)
            {
                SoundDefOf.Click.PlayOneShotOnCamera(null);
                (Settings.CloudConfigs[index], Settings.CloudConfigs[index + 1]) = (Settings.CloudConfigs[index + 1], Settings.CloudConfigs[index]);
            }

            // Delete button
            Rect deleteRect = new Rect(deleteX, y, btnSize, height);
            bool deleteClicked = false;
            bool canDelete = Settings.CloudConfigs.Count > 1;

            Color prevDeleteColor = GUI.color;
            if (canDelete)
            {
                GUI.color = new Color(1f, 0.4f, 0.4f);
            }
            else
            {
                GUI.color = Color.gray;
            }

            if (Widgets.ButtonText(deleteRect, "X", active: canDelete))
            {
                SoundDefOf.Click.PlayOneShotOnCamera(null);
                deleteClicked = true;
            }
            GUI.color = prevDeleteColor;

            Text.Font = GameFont.Tiny;
            return deleteClicked;
        }

        internal void DrawProviderDropdown(float x, float y, float height, float width, ApiConfig config)
        {
            Rect providerRect = new Rect(x, y, width, height);
            Pages.Tooltips.Register(providerRect, "RimChat_ApiProviderFieldTooltip");
            if (Widgets.ButtonText(providerRect, config.Provider.GetLabel()))
            {
                List<FloatMenuOption> providerOptions = new List<FloatMenuOption>();
                foreach (AIProvider provider in Enum.GetValues(typeof(AIProvider)))
                {
                    if (provider == AIProvider.None) continue;

                    if (provider == AIProvider.Player2)
                    {
                        // Player2 cloud redirects to local mode with guidance
                        providerOptions.Add(new FloatMenuOption(provider.GetLabel(), () =>
                        {
                            Settings.UseCloudProviders = false;
                            Settings.LocalConfig.BaseUrl = Player2Endpoints.LocalBaseUrl;
                            Find.WindowStack.Add(new Dialog_MessageBox(
                                "RimChat_Player2RedirectMessage".Translate(),
                                "OK".Translate()));
                        }));
                    }
                    else
                    {
                        providerOptions.Add(new FloatMenuOption(provider.GetLabel(), () =>
                        {
                            config.Provider = provider;
                            if (provider == AIProvider.Custom)
                            {
                                config.SelectedModel = "Custom";
                            }
                            else
                            {
                                config.SelectedModel = "";
                            }
                            Settings.NormalizeCloudConfigUrl(config);
                        }));
                    }
                }
                Find.WindowStack.Add(new FloatMenu(providerOptions));
            }
        }

        internal void DrawApiKeyInput(float x, float y, float height, float width, ApiConfig config)
        {
            Rect apiKeyRect = new Rect(x, y, width, height);
            if (config.Provider == AIProvider.OpenAI)
            {
                Widgets.Label(apiKeyRect, OpenAIProviderAdapter.CredentialDisplay);
                Pages.Tooltips.Register(apiKeyRect, "RimChat_OpenAICredentialTooltip");
                return;
            }
            config.ApiKey = DrawTextFieldWithPlaceholder(apiKeyRect, config.ApiKey, "RimChat_Placeholder_ApiKey".Translate());
            Pages.Tooltips.Register(apiKeyRect, "RimChat_ApiKeyFieldTooltip");
        }

        internal void DrawBaseUrlInput(float x, float y, float height, float width, ApiConfig config)
        {
            Rect baseUrlRect = new Rect(x, y, width, height);
            config.BaseUrl = ApiConfig.NormalizeUrl(DrawTextFieldWithPlaceholder(baseUrlRect, config.BaseUrl, "https:// ..."));
            Pages.Tooltips.Register(baseUrlRect, "RimChat_BaseUrlFieldTooltip");
        }

        internal void DrawCustomUrlModeSelector(float x, float y, float height, float width, ApiConfig config)
        {
            Rect modeRect = new Rect(x, y, width, height);
            Pages.Tooltips.Register(modeRect, "RimChat_CustomUrlModeFieldTooltip");

            string label = config.CustomUrlMode == CustomUrlMode.FullEndpoint
                ? "RimChat_CustomUrlModeFullEndpoint".Translate()
                : "RimChat_CustomUrlModeBase".Translate();
            if (!Widgets.ButtonText(modeRect, label))
            {
                return;
            }

            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("RimChat_CustomUrlModeBase".Translate(), () =>
                {
                    config.CustomUrlMode = CustomUrlMode.BaseUrl;
                    config.MarkCustomUrlModeInitialized();
                }),
                new FloatMenuOption("RimChat_CustomUrlModeFullEndpoint".Translate(), () =>
                {
                    config.CustomUrlMode = CustomUrlMode.FullEndpoint;
                    config.MarkCustomUrlModeInitialized();
                })
            };
            Find.WindowStack.Add(new FloatMenu(options));
        }

        internal void DrawModelSelector(float x, float y, float height, float width, ApiConfig config)
        {
            Rect modelRect = new Rect(x, y, width, height);
            Pages.Tooltips.Register(modelRect, "RimChat_ApiModelFieldTooltip");

            if (config.SelectedModel == "Custom")
            {
                float xButtonWidth = 22f;
                float textFieldWidth = width - xButtonWidth - 2f;

                Rect textFieldRect = new Rect(x, y, textFieldWidth, height);
                Rect backButtonRect = new Rect(x + textFieldWidth + 2f, y, xButtonWidth, height);

                config.CustomModelName = DrawTextFieldWithPlaceholder(textFieldRect, config.CustomModelName, "Model ID");
                Pages.Tooltips.Register(textFieldRect, "RimChat_ApiModelFieldTooltip");
                Pages.Tooltips.Register(backButtonRect, "RimChat_ApiModelFieldTooltip");

                if (Widgets.ButtonText(backButtonRect, "<"))
                {
                    SoundDefOf.Click.PlayOneShotOnCamera(null);
                    config.SelectedModel = "";
                }
            }
            else
            {
                string buttonLabel = string.IsNullOrEmpty(config.SelectedModel) ? "RimChat_ChooseModel".Translate() : config.SelectedModel;
                if (Widgets.ButtonText(modelRect, buttonLabel))
                {
                    ShowModelSelectionMenu(config);
                }
            }
        }

        internal string DrawTextFieldWithPlaceholder(Rect rect, string text, string placeholder)
        {
            string result = Widgets.TextField(rect, text);

            if (string.IsNullOrEmpty(result))
            {
                TextAnchor originalAnchor = Text.Anchor;
                Color originalColor = GUI.color;

                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = new Color(0.6f, 0.6f, 0.6f, 0.7f);

                Rect labelRect = new Rect(rect.x + 5f, rect.y, rect.width - 5f, rect.height);
                Widgets.Label(labelRect, placeholder);

                GUI.color = originalColor;
                Text.Anchor = originalAnchor;
            }

            return result;
        }

        internal void ShowModelSelectionMenu(ApiConfig config)
        {
            // Player2 does not support model listing; model is selected server-side
            if (config.Provider == AIProvider.Player2)
            {
                config.SelectedModel = "Default";
                return;
            }

            bool allowBaseUrlOverride = config.Provider != AIProvider.DeepSeek;
            bool hasBaseUrlOverride = allowBaseUrlOverride && !string.IsNullOrWhiteSpace(config.BaseUrl);
            bool requiresApiKey = config.Provider != AIProvider.Custom && config.Provider.RequiresApiKey() && !hasBaseUrlOverride;
            if (requiresApiKey && string.IsNullOrWhiteSpace(config.ApiKey))
            {
                Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                {
                    new FloatMenuOption("RimChat_EnterApiKey".Translate(), null)
                }));
                return;
            }

            string listModelsUrl = config.Provider.GetListModelsUrl();
            string providerFallbackUrl = Pages.ProviderCloudFetch.BuildProviderModelListRequestUrl(config);
            if (hasBaseUrlOverride)
            {
                if (config.Provider == AIProvider.Custom && config.TryResolveCustomRuntimeEndpoints(out CustomUrlRuntimeResolution customResolved))
                {
                    listModelsUrl = customResolved.ModelsEndpoint;
                    providerFallbackUrl = string.Empty;
                    RelationsProviderCloudModelFetch.LogCustomUrlResolutionHint(customResolved);
                }
            }

            if (string.IsNullOrEmpty(listModelsUrl))
            {
                Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                {
                    new FloatMenuOption("Custom", () => config.SelectedModel = "Custom")
                }));
                return;
            }
            
            string requestUrl = Pages.ProviderCloudFetch.BuildModelListRequestUrl(config, listModelsUrl);
            string cacheKey = Pages.ProviderCloudFetch.BuildModelCacheKey(config.Provider, listModelsUrl, config.ApiKey);

            void OpenMenu(List<string> models)
            {
                var options = new List<FloatMenuOption>();

                if (models != null && models.Any())
                {
                    options.AddRange(models.Select(model => new FloatMenuOption(model, () => config.SelectedModel = model)));
                }
                else
                {
                    options.Add(new FloatMenuOption("RimChat_ModelList_NoModels".Translate(), null));
                }

                options.Add(new FloatMenuOption("Custom", () => config.SelectedModel = "Custom"));
                Find.WindowStack.Add(new FloatMenu(options));
            }

            if (RelationsSettings.ModelCache.ContainsKey(cacheKey))
            {
                OpenMenu(RelationsSettings.ModelCache[cacheKey]);
            }
            else
            {
                Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                {
                    new FloatMenuOption("RimChat_ModelList_Loading".Translate(), null)
                }));
                
                Pages.ProviderCloudFetch.FetchModelsCoroutine(requestUrl, providerFallbackUrl, config.ApiKey, config.Provider, cacheKey, OpenMenu);
            }
        }


        internal void DrawLocalProviderSection(Listing_Standard listing)
        {
            listing.Label("RimChat_LocalProviderConfiguration".Translate());
            listing.Gap(6f);

            Rect rowRect = listing.GetRect(24f);
            float x = rowRect.x;
            float y = rowRect.y;
            float height = rowRect.height;

            Rect baseUrlLabelRect = new Rect(x, y, 80f, height);
            Widgets.Label(baseUrlLabelRect, "RimChat_BaseUrlLabel".Translate());
            x += 85f;

            Rect urlRect = new Rect(x, y, 250f, height);
            Settings.LocalConfig.BaseUrl = ApiConfig.NormalizeUrl(Widgets.TextField(urlRect, Settings.LocalConfig.BaseUrl));
            x += 285f;

            // Player2 local uses server-side model selection; no model name needed
            if (Settings.LocalConfig.IsPlayer2Local())
            {
                Rect modelLabelRect = new Rect(x, y, 70f, height);
                Widgets.Label(modelLabelRect, "RimChat_ModelLabel".Translate());
                x += 75f;

                Rect defaultLabelRect = new Rect(x, y, 200f, height);
                GUI.color = Color.gray;
                Widgets.Label(defaultLabelRect, "Default (Player2)");
                GUI.color = Color.white;
            }
            else
            {
                Rect modelLabelRect = new Rect(x, y, 70f, height);
                Widgets.Label(modelLabelRect, "RimChat_ModelLabel".Translate());
                x += 75f;

                Rect modelRect = new Rect(x, y, 200f, height);
                Settings.LocalConfig.ModelName = Widgets.TextField(modelRect, Settings.LocalConfig.ModelName);
            }
        }
}
