using System;
using System.Collections.Generic;
using System.Text;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using RimWorld;
using UnityEngine;
using UnityEngine.Networking;
using Verse;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.UI;
using Ustas.RimAI.Core.UI;

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class RelationsImageSettingsPage
{
    readonly RelationsSettingsPages Pages;

    internal RelationsImageSettingsPage(RelationsSettingsPages pages)
    {
        Pages = pages;
    }

    RelationsSettings Settings => Pages.Settings;

    internal void Draw(Rect rect, SettingsSearchState search)
    {
        DrawTab_DiplomacyImageApi(rect);
    }

    internal void ResetToDefaults()
    {
        EnsureDiplomacyImageDefaults();
    }

        internal Vector2 _imageApiTabScroll = Vector2.zero;
        internal Vector2 _imageTemplateTextScroll = Vector2.zero;
        internal int _selectedImageTemplateIndex = 0;
        internal bool _isTestingImageConnection = false;
        internal string _imageConnectionTestStatus = string.Empty;

        internal void EnsureDiplomacyImageDefaults()
        {
            Settings.DiplomacyImageApi ??= new DiplomacyImageApiConfig();
            Settings.DiplomacyImagePromptTemplates ??= new List<DiplomacyImagePromptTemplate>();
            DiplomacyImageTemplateDefaults.EnsureDefaults(Settings.DiplomacyImagePromptTemplates);
            EnsureImageTemplateIds();
            if (_selectedImageTemplateIndex < 0 || _selectedImageTemplateIndex >= Settings.DiplomacyImagePromptTemplates.Count)
            {
                _selectedImageTemplateIndex = 0;
            }
        }

        internal void DrawTab_DiplomacyImageApi(Rect rect)
        {
            EnsureDiplomacyImageDefaults();
            float viewWidth = Mathf.Max(300f, rect.width - 16f);
            float viewHeight = CalculateImageApiContentHeight(viewWidth);
            Rect viewRect = new Rect(0f, 0f, viewWidth, viewHeight);
            Widgets.BeginScrollView(rect, ref _imageApiTabScroll, viewRect);

            var listing = new Listing_Standard();
            // Verse wraps a Listing into a second column, off the visible view, as soon as
            // content passes the rect height, and CurHeight then reports that new column.
            // A scrolling settings page never wants that; see validate_scrollable_listings.
            listing.maxOneColumn = true;
            listing.Begin(new Rect(0f, 0f, viewRect.width, viewRect.height));

            DrawImageApiConnectionSection(listing);

            listing.End();
            Widgets.EndScrollView();
        }

        internal float CalculateImageApiContentHeight(float width)
        {
            float estimatedHeight = Settings.DiplomacyImageApi != null && Settings.DiplomacyImageApi.IsNativeOpenAi()
                ? 980f
                : 760f;
            return estimatedHeight;
        }

        internal void DrawImageApiConnectionSection(Listing_Standard listing)
        {
            listing.CheckboxLabeled("RimAI.Relations.ImageApi.Enabled".Translate(), ref Settings.DiplomacyImageApi.IsEnabled);
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            listing.Label("RimAI.Relations.ImageApi.EnabledHint".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            DrawImageProviderPresetSelector(listing);
            if (Settings.DiplomacyImageApi.IsNativeOpenAi())
            {
                RelationsOpenAiImageSettingsUi.DrawNative(listing, Settings.DiplomacyImageApi, Pages);
                listing.Label("RimChat_ImageApiTimeout".Translate(Settings.DiplomacyImageApi.TimeoutSeconds));
                Settings.DiplomacyImageApi.TimeoutSeconds = Mathf.RoundToInt(listing.Slider(Settings.DiplomacyImageApi.TimeoutSeconds, 10f, 300f));
                DrawImageConnectionTestButton(listing);
                return;
            }

            DrawImageApiTextField(listing, "RimChat_ImageApiEndpoint", ref Settings.DiplomacyImageApi.Endpoint, "https://...");
            if (string.Equals(Settings.DiplomacyImageApi.AuthMode, DiplomacyImageApiConfig.AuthModeNone, StringComparison.OrdinalIgnoreCase))
            {
                Text.Font = GameFont.Tiny;
                GUI.color = Color.gray;
                listing.Label("RimChat_ImageApiNoAuthHint".Translate());
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }
            else
            {
                DrawImageApiTextField(listing, "RimChat_ImageApiKey", ref Settings.DiplomacyImageApi.ApiKey, "RimChat_Placeholder_ApiKey".Translate().ToString());
            }

            DrawImageApiTextField(listing, "RimChat_ImageApiModel", ref Settings.DiplomacyImageApi.Model, "model-id");

            listing.Label("RimChat_ImageApiDefaultSize".Translate());
            Settings.DiplomacyImageApi.DefaultSize = Pages.ProviderCloud.DrawTextFieldWithPlaceholder(listing.GetRect(26f), Settings.DiplomacyImageApi.DefaultSize ?? string.Empty, "2560x1440");

            listing.CheckboxLabeled("RimChat_ImageApiDefaultWatermark".Translate(), ref Settings.DiplomacyImageApi.DefaultWatermark);
            if (string.Equals(Settings.DiplomacyImageApi.ProviderPreset, DiplomacyImageApiConfig.ProviderPresetComfyUiLocal, StringComparison.OrdinalIgnoreCase))
            {
                listing.Label("RimChat_ImageApiComfyLoaderNode".Translate());
                Settings.DiplomacyImageApi.ComfyUiImageLoaderNode = Pages.ProviderCloud.DrawTextFieldWithPlaceholder(listing.GetRect(26f), Settings.DiplomacyImageApi.ComfyUiImageLoaderNode ?? string.Empty, "LoadImageBase64");
                Text.Font = GameFont.Tiny;
                GUI.color = Color.gray;
                listing.Label("RimChat_ImageApiComfyLoaderHint".Translate());
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }
            listing.Label("RimChat_ImageApiTimeout".Translate(Settings.DiplomacyImageApi.TimeoutSeconds));
            Settings.DiplomacyImageApi.TimeoutSeconds = Mathf.RoundToInt(listing.Slider(Settings.DiplomacyImageApi.TimeoutSeconds, 10f, 300f));
            DrawImageConnectionTestButton(listing);

            if (string.Equals(Settings.DiplomacyImageApi.ProviderPreset, DiplomacyImageApiConfig.ProviderPresetCustom, StringComparison.OrdinalIgnoreCase))
            {
                listing.CheckboxLabeled("RimChat_ImageApiAdvancedToggle".Translate(), ref Settings.DiplomacyImageApi.ShowAdvanced);
                if (Settings.DiplomacyImageApi.ShowAdvanced)
                {
                    DrawImageModeSelector(listing);
                    DrawImageSchemaPresetSelector(listing);
                    DrawImageAuthModeSelector(listing);
                    DrawImageApiTextField(listing, "RimChat_ImageApiAuthHeaderName", ref Settings.DiplomacyImageApi.ApiKeyHeaderName, "X-API-Key");
                    DrawImageApiTextField(listing, "RimChat_ImageApiAuthQueryName", ref Settings.DiplomacyImageApi.ApiKeyQueryName, "api_key");
                    DrawImageApiTextField(listing, "RimChat_ImageApiResponseUrlPath", ref Settings.DiplomacyImageApi.ResponseUrlPath, "url,data[0].url");
                    DrawImageApiTextField(listing, "RimChat_ImageApiResponseB64Path", ref Settings.DiplomacyImageApi.ResponseB64Path, "b64_json,data[0].b64_json");
                    DrawImageApiTextField(listing, "RimChat_ImageApiAsyncSubmitPath", ref Settings.DiplomacyImageApi.AsyncSubmitPath, "/prompt");
                    DrawImageApiTextField(listing, "RimChat_ImageApiAsyncStatusPath", ref Settings.DiplomacyImageApi.AsyncStatusPathTemplate, "/history/{job_id}");
                    DrawImageApiTextField(listing, "RimChat_ImageApiAsyncFetchPath", ref Settings.DiplomacyImageApi.AsyncImageFetchPath, "/view");
                    listing.Label("RimChat_ImageApiPollInterval".Translate(Settings.DiplomacyImageApi.PollIntervalMs));
                    Settings.DiplomacyImageApi.PollIntervalMs = Mathf.RoundToInt(listing.Slider(Settings.DiplomacyImageApi.PollIntervalMs, 250f, 10000f));
                    listing.Label("RimChat_ImageApiPollAttempts".Translate(Settings.DiplomacyImageApi.PollMaxAttempts));
                    Settings.DiplomacyImageApi.PollMaxAttempts = Mathf.RoundToInt(listing.Slider(Settings.DiplomacyImageApi.PollMaxAttempts, 1f, 600f));
                }
            }

        }

        internal void DrawImageApiTextField(Listing_Standard listing, string labelKey, ref string value, string placeholder)
        {
            listing.Label(labelKey.Translate());
            Rect rect = listing.GetRect(26f);
            value = Pages.ProviderCloud.DrawTextFieldWithPlaceholder(rect, value ?? string.Empty, placeholder ?? string.Empty);
        }

        internal void DrawImageProviderPresetSelector(Listing_Standard listing)
        {
            listing.Label("RimChat_ImageApiProviderPreset".Translate());
            Rect rect = listing.GetRect(24f);
            if (Widgets.ButtonText(rect, GetImageProviderPresetLabel(Settings.DiplomacyImageApi.ProviderPreset)))
            {
                var options = new List<FloatMenuOption>
                {
                    new FloatMenuOption("RimChat_ImageApiProviderOpenAI".Translate(), () => ApplyImageProviderPreset(DiplomacyImageApiConfig.ProviderPresetOpenAINative)),
                    new FloatMenuOption("RimChat_ImageApiProviderOpenAICompatible".Translate(), () => ApplyImageProviderPreset(DiplomacyImageApiConfig.ProviderPresetOpenAI)),
                    new FloatMenuOption("RimChat_ImageApiProviderArk".Translate(), () => ApplyImageProviderPreset(DiplomacyImageApiConfig.ProviderPresetArk)),
                    new FloatMenuOption("RimChat_ImageApiProviderSiliconFlow".Translate(), () => ApplyImageProviderPreset(DiplomacyImageApiConfig.ProviderPresetSiliconFlow)),
                    new FloatMenuOption("RimChat_ImageApiProviderComfyUiLocal".Translate(), () => ApplyImageProviderPreset(DiplomacyImageApiConfig.ProviderPresetComfyUiLocal)),
                    new FloatMenuOption("RimChat_ImageApiProviderCustom".Translate(), () => ApplyImageProviderPreset(DiplomacyImageApiConfig.ProviderPresetCustom))
                };
                Find.WindowStack.Add(new FloatMenu(options));
            }

            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            listing.Label("RimChat_ImageApiProviderPresetHint".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        internal void ApplyImageProviderPreset(string preset)
        {
            string normalized = DiplomacyImageApiConfig.NormalizeProviderPreset(preset);
            if (string.Equals(Settings.DiplomacyImageApi.ProviderPreset, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Settings.DiplomacyImageApi.ProviderPreset = normalized;
            if (!string.Equals(normalized, DiplomacyImageApiConfig.ProviderPresetCustom, StringComparison.OrdinalIgnoreCase))
            {
                Settings.DiplomacyImageApi.ShowAdvanced = false;
                Settings.DiplomacyImageApi.Endpoint = string.Empty;
                Settings.DiplomacyImageApi.Model = string.Empty;
            }

            if (string.Equals(normalized, DiplomacyImageApiConfig.ProviderPresetOpenAINative, StringComparison.OrdinalIgnoreCase))
            {
                Settings.DiplomacyImageApi.ApiKey = string.Empty;
                Settings.DiplomacyImageApi.DefaultSize = DiplomacyOpenAiImageContract.SizeAuto;
                Settings.DiplomacyImageApi.Quality = DiplomacyOpenAiImageContract.QualityAuto;
                Settings.DiplomacyImageApi.OutputFormat = DiplomacyOpenAiImageContract.FormatPng;
                Settings.DiplomacyImageApi.Background = DiplomacyOpenAiImageContract.BackgroundAuto;
            }

            Settings.DiplomacyImageApi.ApplyProviderPresetDefaults();
            Settings.DiplomacyImageApi.Normalize();
        }

        internal void DrawImageModeSelector(Listing_Standard listing)
        {
            listing.Label("RimChat_ImageApiMode".Translate());
            Rect rect = listing.GetRect(24f);
            if (Widgets.ButtonText(rect, GetImageModeLabel(Settings.DiplomacyImageApi.Mode)))
            {
                var options = new List<FloatMenuOption>
                {
                    new FloatMenuOption("RimChat_ImageApiModeSyncUrl".Translate(), () => Settings.DiplomacyImageApi.Mode = DiplomacyImageApiConfig.ModeSyncUrl),
                    new FloatMenuOption("RimChat_ImageApiModeSyncPayload".Translate(), () => Settings.DiplomacyImageApi.Mode = DiplomacyImageApiConfig.ModeSyncPayload),
                    new FloatMenuOption("RimChat_ImageApiModeAsyncJob".Translate(), () => Settings.DiplomacyImageApi.Mode = DiplomacyImageApiConfig.ModeAsyncJob)
                };
                Find.WindowStack.Add(new FloatMenu(options));
            }

            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            listing.Label("RimChat_ImageApiModeHint".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        internal void DrawImageSchemaPresetSelector(Listing_Standard listing)
        {
            listing.Label("RimChat_ImageApiSchemaPreset".Translate());
            Rect rect = listing.GetRect(24f);
            if (Widgets.ButtonText(rect, GetImageSchemaPresetLabel(Settings.DiplomacyImageApi.SchemaPreset)))
            {
                var options = new List<FloatMenuOption>
                {
                    new FloatMenuOption("RimChat_ImageApiSchemaArk".Translate(), () => Settings.DiplomacyImageApi.SchemaPreset = DiplomacyImageApiConfig.SchemaPresetArk),
                    new FloatMenuOption("RimChat_ImageApiSchemaOpenAI".Translate(), () => Settings.DiplomacyImageApi.SchemaPreset = DiplomacyImageApiConfig.SchemaPresetOpenAI),
                    new FloatMenuOption("RimChat_ImageApiSchemaComfyUI".Translate(), () => Settings.DiplomacyImageApi.SchemaPreset = DiplomacyImageApiConfig.SchemaPresetComfyUi),
                    new FloatMenuOption("RimChat_ImageApiSchemaCustom".Translate(), () => Settings.DiplomacyImageApi.SchemaPreset = DiplomacyImageApiConfig.SchemaPresetCustom)
                };
                Find.WindowStack.Add(new FloatMenu(options));
            }

            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            listing.Label("RimChat_ImageApiSchemaPresetHint".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        internal void DrawImageAuthModeSelector(Listing_Standard listing)
        {
            listing.Label("RimChat_ImageApiAuthMode".Translate());
            Rect rect = listing.GetRect(24f);
            if (Widgets.ButtonText(rect, GetImageAuthModeLabel(Settings.DiplomacyImageApi.AuthMode)))
            {
                var options = new List<FloatMenuOption>
                {
                    new FloatMenuOption("RimChat_ImageApiAuthBearer".Translate(), () => Settings.DiplomacyImageApi.AuthMode = DiplomacyImageApiConfig.AuthModeBearer),
                    new FloatMenuOption("RimChat_ImageApiAuthApiKeyHeader".Translate(), () => Settings.DiplomacyImageApi.AuthMode = DiplomacyImageApiConfig.AuthModeApiKeyHeader),
                    new FloatMenuOption("RimChat_ImageApiAuthQueryKey".Translate(), () => Settings.DiplomacyImageApi.AuthMode = DiplomacyImageApiConfig.AuthModeQueryKey),
                    new FloatMenuOption("RimChat_ImageApiAuthNone".Translate(), () => Settings.DiplomacyImageApi.AuthMode = DiplomacyImageApiConfig.AuthModeNone)
                };
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        internal void DrawImageConnectionTestButton(Listing_Standard listing)
        {
            Rect buttonRect = listing.GetRect(30f);
            string label = _isTestingImageConnection
                ? "RimChat_TestingConnection".Translate()
                : (Settings.DiplomacyImageApi.IsNativeOpenAi()
                    ? "RimAI.Relations.ImageApi.ProbeAuthCheck".Translate()
                    : "RimChat_TestConnectionButton".Translate());

            GUI.color = _isTestingImageConnection ? Color.gray : Color.white;
            bool clicked = Widgets.ButtonText(buttonRect, label, active: !_isTestingImageConnection);
            GUI.color = Color.white;
            if (clicked && !_isTestingImageConnection)
            {
                StartImageConnectionTest();
            }

            if (string.IsNullOrWhiteSpace(_imageConnectionTestStatus))
            {
                return;
            }

            GUI.color = GetImageConnectionStatusColor();
            listing.Label(_imageConnectionTestStatus);
            GUI.color = Color.white;
        }

        internal void StartImageConnectionTest()
        {
            _isTestingImageConnection = true;
            _imageConnectionTestStatus = "RimChat_ConnectionTesting".Translate();
            if (AIChatServiceAsync.Instance == null)
            {
                _imageConnectionTestStatus = "RimChat_ConnectionFailed".Translate("Coroutine host unavailable.");
                _isTestingImageConnection = false;
                return;
            }

            AIChatServiceAsync.Instance.StartCoroutine(TestImageConnectionCoroutine());
        }

        internal System.Collections.IEnumerator TestImageConnectionCoroutine()
        {
            Settings.DiplomacyImageApi.Normalize();
            if (Settings.DiplomacyImageApi.IsNativeOpenAi())
            {
                yield return ProbeOpenAiAuthCoroutine();
                yield break;
            }

            if (!Settings.DiplomacyImageApi.IsConfigured())
            {
                _imageConnectionTestStatus = "RimChat_ConnectionFailed".Translate("RimChat_SendImageConfigInvalid".Translate());
                _isTestingImageConnection = false;
                yield break;
            }
            DiplomacyImageGenerationRequest request = BuildImageProbeRequest();
            bool succeeded = false;
            string reason = string.Empty;
            yield return ProbeImageConnectionCoroutine(request, (ok, why) =>
            {
                succeeded = ok;
                reason = why ?? string.Empty;
            });

            _imageConnectionTestStatus = succeeded
                ? "RimChat_ConnectionSuccess".Translate()
                : "RimChat_ConnectionFailed".Translate(reason);
            _isTestingImageConnection = false;
        }

        internal System.Collections.IEnumerator ProbeOpenAiAuthCoroutine()
        {
            if (!OpenAIProviderAdapter.CredentialPresent)
            {
                _imageConnectionTestStatus = "RimChat_ConnectionFailed".Translate(
                    "RimAI.Relations.ImageApi.ErrorMissingCredential".Translate());
                _isTestingImageConnection = false;
                yield break;
            }

            string url = DiplomacyOpenAiImageContract.ResolveModelsProbeUrl(Settings.DiplomacyImageApi.Model);
            var probeRequest = new DiplomacyImageGenerationRequest
            {
                TimeoutSeconds = Mathf.Clamp(Settings.DiplomacyImageApi.TimeoutSeconds, 10, 60)
            };
            DiplomacyImageRequestBinder.Bind(Settings.DiplomacyImageApi, probeRequest);
            ProbeResult probe = default;
            yield return SendImageProbeRequestCoroutine(url, "GET", string.Empty, probeRequest, result => probe = result);
            OpenAiImageProbeOutcome outcome = DiplomacyOpenAiImageContract.ClassifyProbe(
                OpenAIProviderAdapter.CredentialPresent,
                probe.ResponseCode,
                probe.ResponseBody);
            string detail = RelationsOpenAiImageSettingsUi.ProbeOutcomeKey(outcome).Translate();
            _imageConnectionTestStatus = outcome == OpenAiImageProbeOutcome.Success
                ? "RimChat_ConnectionSuccess".Translate()
                : "RimChat_ConnectionFailed".Translate(detail);
            _isTestingImageConnection = false;
        }

        internal DiplomacyImageGenerationRequest BuildImageProbeRequest()
        {
            var request = new DiplomacyImageGenerationRequest
            {
                Prompt = "Connectivity test image. Keep it simple.",
                TimeoutSeconds = Mathf.Clamp(Settings.DiplomacyImageApi.TimeoutSeconds, 10, 60)
            };
            DiplomacyImageRequestBinder.Bind(Settings.DiplomacyImageApi, request);
            request.Prompt = "Connectivity test image. Keep it simple.";
            request.TimeoutSeconds = Mathf.Clamp(Settings.DiplomacyImageApi.TimeoutSeconds, 10, 60);
            return request;
        }

        internal System.Collections.IEnumerator ProbeImageConnectionCoroutine(
            DiplomacyImageGenerationRequest request,
            Action<bool, string> onFinished)
        {
            string mode = DiplomacyImageApiConfig.NormalizeMode(request.Mode);
            if (string.Equals(mode, DiplomacyImageApiConfig.ModeAsyncJob, StringComparison.OrdinalIgnoreCase))
            {
                yield return ProbeAsyncImageConnectionCoroutine(request, onFinished);
                yield break;
            }

            yield return ProbeSyncImageConnectionCoroutine(request, onFinished);
        }

        internal System.Collections.IEnumerator ProbeSyncImageConnectionCoroutine(
            DiplomacyImageGenerationRequest request,
            Action<bool, string> onFinished)
        {
            request.Normalize();
            string url = DiplomacyImageProviderCompat.BuildAuthAppliedUrl(request.Endpoint, request);
            string body = DiplomacyImageProviderCompat.BuildSchemaAwareRequestBody(request, true, BuildArkProbeBody, BuildArkProbeBodyWithoutSize);
            ProbeResult probe = default;
            yield return SendImageProbeRequestCoroutine(url, "POST", body, request, result => probe = result);
            if (probe.ResponseCode == 400 && IsSizeProbeFailure(probe.Error, probe.ResponseBody))
            {
                string fallbackBody = DiplomacyImageProviderCompat.BuildSchemaAwareRequestBody(request, false, BuildArkProbeBody, BuildArkProbeBodyWithoutSize);
                yield return SendImageProbeRequestCoroutine(url, "POST", fallbackBody, request, result => probe = result);
            }

            if (probe.IsAuthError)
            {
                onFinished?.Invoke(false, "RimChat_InvalidAPIKey".Translate());
                yield break;
            }

            onFinished?.Invoke(probe.IsReachable, probe.ToReason());
        }

        internal static bool IsSizeProbeFailure(string error, string responseBody)
        {
            string merged = $"{error} {responseBody}".ToLowerInvariant();
            return merged.Contains("\"size\"")
                || merged.Contains("parameter `size`")
                || merged.Contains("must be at least")
                || merged.Contains("invalid parameter");
        }

        internal System.Collections.IEnumerator ProbeAsyncImageConnectionCoroutine(
            DiplomacyImageGenerationRequest request,
            Action<bool, string> onFinished)
        {
            request.Normalize();
            string submitUrl = DiplomacyImageProviderCompat.ResolveAsyncSubmitUrl(request);
            string body = DiplomacyImageProviderCompat.BuildAsyncSubmitBody(
                request,
                req => DiplomacyImageProviderCompat.BuildSchemaAwareRequestBody(req, true, BuildArkProbeBody, BuildArkProbeBodyWithoutSize));
            ProbeResult probe = default;
            yield return SendImageProbeRequestCoroutine(submitUrl, "POST", body, request, result => probe = result);
            if (probe.IsAuthError)
            {
                onFinished?.Invoke(false, "RimChat_InvalidAPIKey".Translate());
                yield break;
            }

            if (probe.IsSuccess && DiplomacyImageProviderCompat.TryExtractPromptId(probe.ResponseBody, out _))
            {
                onFinished?.Invoke(true, string.Empty);
                yield break;
            }

            onFinished?.Invoke(probe.IsReachable, probe.ToReason());
        }

        internal System.Collections.IEnumerator SendImageProbeRequestCoroutine(
            string url,
            string method,
            string body,
            DiplomacyImageGenerationRequest request,
            Action<ProbeResult> onFinished)
        {
            string resolvedUrl = DiplomacyImageProviderCompat.BuildAuthAppliedUrl(url, request);
            using (var web = new UnityWebRequest(resolvedUrl, method))
            {
                web.downloadHandler = new DownloadHandlerBuffer();
                web.timeout = Mathf.Clamp(request.TimeoutSeconds, 5, 60);
                DiplomacyImageProviderCompat.ApplyAuth(web, request);
                if (!string.IsNullOrWhiteSpace(body))
                {
                    byte[] postData = Encoding.UTF8.GetBytes(body);
                    web.uploadHandler = new UploadHandlerRaw(postData);
                    web.SetRequestHeader("Content-Type", "application/json");
                }

                yield return web.SendWebRequest();
                onFinished?.Invoke(new ProbeResult(
                    web.result,
                    web.responseCode,
                    web.error ?? string.Empty,
                    web.downloadHandler?.text ?? string.Empty));
            }
        }

        internal static string BuildArkProbeBody(DiplomacyImageGenerationRequest request)
        {
            string size = DiplomacyImageApiConfig.NormalizeImageSize(request.Size, DiplomacyImageApiConfig.DefaultImageSize);
            string watermark = request.Watermark ? "true" : "false";
            return "{"
                + $"\"model\":\"{EscapeProbeJson(request.Model)}\","
                + $"\"prompt\":\"{EscapeProbeJson(request.Prompt)}\","
                + "\"sequential_image_generation\":\"disabled\","
                + "\"response_format\":\"url\","
                + "\"stream\":false,"
                + $"\"size\":\"{EscapeProbeJson(size)}\","
                + $"\"watermark\":{watermark}"
                + "}";
        }

        internal static string BuildArkProbeBodyWithoutSize(DiplomacyImageGenerationRequest request)
        {
            string watermark = request.Watermark ? "true" : "false";
            return "{"
                + $"\"model\":\"{EscapeProbeJson(request.Model)}\","
                + $"\"prompt\":\"{EscapeProbeJson(request.Prompt)}\","
                + "\"sequential_image_generation\":\"disabled\","
                + "\"response_format\":\"url\","
                + "\"stream\":false,"
                + $"\"watermark\":{watermark}"
                + "}";
        }

        internal static string EscapeProbeJson(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        internal static string GetImageModeLabel(string mode)
        {
            switch (DiplomacyImageApiConfig.NormalizeMode(mode))
            {
                case DiplomacyImageApiConfig.ModeSyncPayload:
                    return "RimChat_ImageApiModeSyncPayload".Translate();
                case DiplomacyImageApiConfig.ModeAsyncJob:
                    return "RimChat_ImageApiModeAsyncJob".Translate();
                default:
                    return "RimChat_ImageApiModeSyncUrl".Translate();
            }
        }

        internal static string GetImageSchemaPresetLabel(string preset)
        {
            string normalized = DiplomacyImageApiConfig.NormalizeSchemaPreset(preset, string.Empty);
            if (string.Equals(normalized, DiplomacyImageApiConfig.SchemaPresetOpenAI, StringComparison.OrdinalIgnoreCase))
            {
                return "RimChat_ImageApiSchemaOpenAI".Translate();
            }
            if (string.Equals(normalized, DiplomacyImageApiConfig.SchemaPresetComfyUi, StringComparison.OrdinalIgnoreCase))
            {
                return "RimChat_ImageApiSchemaComfyUI".Translate();
            }
            if (string.Equals(normalized, DiplomacyImageApiConfig.SchemaPresetCustom, StringComparison.OrdinalIgnoreCase))
            {
                return "RimChat_ImageApiSchemaCustom".Translate();
            }

            return "RimChat_ImageApiSchemaArk".Translate();
        }

        internal static string GetImageAuthModeLabel(string mode)
        {
            string normalized = DiplomacyImageApiConfig.NormalizeAuthMode(mode, DiplomacyImageApiConfig.SchemaPresetArk);
            if (string.Equals(normalized, DiplomacyImageApiConfig.AuthModeApiKeyHeader, StringComparison.OrdinalIgnoreCase))
            {
                return "RimChat_ImageApiAuthApiKeyHeader".Translate();
            }
            if (string.Equals(normalized, DiplomacyImageApiConfig.AuthModeQueryKey, StringComparison.OrdinalIgnoreCase))
            {
                return "RimChat_ImageApiAuthQueryKey".Translate();
            }
            if (string.Equals(normalized, DiplomacyImageApiConfig.AuthModeNone, StringComparison.OrdinalIgnoreCase))
            {
                return "RimChat_ImageApiAuthNone".Translate();
            }

            return "RimChat_ImageApiAuthBearer".Translate();
        }

        internal static string GetImageProviderPresetLabel(string preset)
        {
            string normalized = DiplomacyImageApiConfig.NormalizeProviderPreset(preset);
            if (string.Equals(normalized, DiplomacyImageApiConfig.ProviderPresetOpenAINative, StringComparison.OrdinalIgnoreCase))
            {
                return "RimChat_ImageApiProviderOpenAI".Translate();
            }
            if (string.Equals(normalized, DiplomacyImageApiConfig.ProviderPresetOpenAI, StringComparison.OrdinalIgnoreCase))
            {
                return "RimChat_ImageApiProviderOpenAICompatible".Translate();
            }
            if (string.Equals(normalized, DiplomacyImageApiConfig.ProviderPresetSiliconFlow, StringComparison.OrdinalIgnoreCase))
            {
                return "RimChat_ImageApiProviderSiliconFlow".Translate();
            }
            if (string.Equals(normalized, DiplomacyImageApiConfig.ProviderPresetComfyUiLocal, StringComparison.OrdinalIgnoreCase))
            {
                return "RimChat_ImageApiProviderComfyUiLocal".Translate();
            }
            if (string.Equals(normalized, DiplomacyImageApiConfig.ProviderPresetCustom, StringComparison.OrdinalIgnoreCase))
            {
                return "RimChat_ImageApiProviderCustom".Translate();
            }

            return "RimChat_ImageApiProviderArk".Translate();
        }

        internal Color GetImageConnectionStatusColor()
        {
            if (_imageConnectionTestStatus.Contains("RimChat_ConnectionSuccess".Translate().ToString()))
            {
                return Color.green;
            }
            if (_imageConnectionTestStatus.Contains("RimChat_ConnectionFailed".Translate().ToString()))
            {
                return Color.red;
            }
            return Color.yellow;
        }

        internal readonly struct ProbeResult
        {
            public readonly UnityWebRequest.Result Result;
            public readonly long ResponseCode;
            public readonly string Error;
            public readonly string ResponseBody;

            public ProbeResult(UnityWebRequest.Result result, long responseCode, string error, string responseBody)
            {
                Result = result;
                ResponseCode = responseCode;
                Error = error ?? string.Empty;
                ResponseBody = responseBody ?? string.Empty;
            }

            public bool IsSuccess => Result == UnityWebRequest.Result.Success || ResponseCode == 200;
            public bool IsAuthError => ResponseCode == 401 || ResponseCode == 403;
            public bool IsReachable => ResponseCode > 0 && ResponseCode != 404 && !IsAuthError;

            public string ToReason()
            {
                if (ResponseCode > 0)
                {
                    return $"HTTP {ResponseCode}";
                }
                return string.IsNullOrWhiteSpace(Error) ? "unknown error" : Error;
            }
        }

        internal void EnsureSendImageCaptionDefaults()
        {
        }

        internal string ResolveDefaultApiEndpointForImage()
        {
            ApiConfig config = ResolvePrimaryCloudApiConfig();
            string endpoint = config?.GetEffectiveEndpoint();
            return string.IsNullOrWhiteSpace(endpoint)
                ? DiplomacyImageApiConfig.DefaultVolcEngineImageEndpoint
                : endpoint;
        }

        internal string ResolveDefaultApiModelForImage()
        {
            ApiConfig config = ResolvePrimaryCloudApiConfig();
            string model = config?.GetEffectiveModelName();
            return string.IsNullOrWhiteSpace(model)
                ? DiplomacyImageApiConfig.DefaultVolcEngineImageModel
                : model;
        }

        internal ApiConfig ResolvePrimaryCloudApiConfig()
        {
            if (Settings.CloudConfigs == null || Settings.CloudConfigs.Count == 0)
            {
                return null;
            }

            for (int i = 0; i < Settings.CloudConfigs.Count; i++)
            {
                ApiConfig config = Settings.CloudConfigs[i];
                if (config != null && config.IsEnabled)
                {
                    return config;
                }
            }

            return Settings.CloudConfigs[0];
        }

        internal void EnsureImageTemplateIds()
        {
            if (Settings.DiplomacyImagePromptTemplates == null)
            {
                return;
            }

            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < Settings.DiplomacyImagePromptTemplates.Count; i++)
            {
                DiplomacyImagePromptTemplate template = Settings.DiplomacyImagePromptTemplates[i];
                if (template == null)
                {
                    continue;
                }

                string id = (template.Id ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(id))
                {
                    id = Guid.NewGuid().ToString("N");
                }

                if (used.Contains(id))
                {
                    id = $"{id}_{i + 1}";
                }

                template.Id = id;
                used.Add(id);
            }
        }
    
}
