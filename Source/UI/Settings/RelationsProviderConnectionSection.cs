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

internal sealed class RelationsProviderConnectionSection
{
    readonly RelationsSettingsPages Pages;

    internal RelationsProviderConnectionSection(RelationsSettingsPages pages)
    {
        Pages = pages;
    }

    RelationsSettings Settings => Pages.Settings;

        internal string connectionTestStatus = "";
        internal bool isTestingConnection = false;
        internal const int DialogueTokenLowThreshold = 1200;
        internal const int DialogueTokenMediumThreshold = 3000;

        internal void DrawConnectionTestButton(Listing_Standard listing)
        {
            Pages.ApiUsability.DrawApiTestButton(listing);
            listing.Gap(2f);
            Pages.ApiUsability.DrawUsabilityTestResult(listing);
        }

        internal void DrawLatestDialogueTokenUsage(Listing_Standard listing)
        {
            if (!AIChatServiceAsync.TryGetLatestDialogueTokenUsage(out DialogueTokenUsageSnapshot snapshot) || snapshot == null)
            {
                listing.Label("RimChat_LastDialogueTokenUsageNoData".Translate());
                return;
            }

            string level = GetDialogueTokenLevelLabel(snapshot.TotalTokens);
            string estimateSuffix = snapshot.IsEstimated
                ? " " + "RimChat_LastDialogueTokenUsageEstimated".Translate()
                : string.Empty;

            string text = "RimChat_LastDialogueTokenUsageLine".Translate(
                snapshot.TotalTokens.ToString(),
                level,
                estimateSuffix);
            listing.Label(text);
        }

        internal string GetDialogueTokenLevelLabel(int totalTokens)
        {
            if (totalTokens <= DialogueTokenLowThreshold)
            {
                return "RimChat_TokenLevelLow".Translate();
            }

            if (totalTokens <= DialogueTokenMediumThreshold)
            {
                return "RimChat_TokenLevelMedium".Translate();
            }

            return "RimChat_TokenLevelHigh".Translate();
        }

        internal Color GetStatusColor()
        {
            // Failure used to be detected by looking for the RimChat_ConnectionFailed
            // text, but that key is a format string: the probe compared against a
            // template still holding {0} while the real status had the reason
            // substituted in, so it never matched and a failed test rendered yellow.
            // Success and testing are the only two states with a fixed string, so
            // those are what we test for; anything else that has been set is a
            // failure by elimination.
            if (string.IsNullOrEmpty(connectionTestStatus))
                return Color.yellow;
            if (connectionTestStatus.Contains("RimChat_ConnectionSuccess".Translate().ToString()))
                return Color.green;
            if (connectionTestStatus.Contains("RimChat_ConnectionTesting".Translate().ToString()))
                return Color.yellow;
            return Color.red;
        }

        internal void TestConnection()
        {
            isTestingConnection = true;
            connectionTestStatus = "RimChat_ConnectionTesting".Translate();

            LongEventHandler.QueueLongEvent(() =>
            {
                TestConnectionSync();
            }, "RimChat_TestingConnection".Translate(), false, null);
        }

        internal void TestConnectionSync()
        {
            try
            {
                if (Settings.UseCloudProviders)
                {
                    ApiConfig config = ResolvePrimaryCloudConfigForConnectivity();
                    if (!TryValidateCloudConfigForConnectivity(config, out string validationKey))
                    {
                        connectionTestStatus = "RimChat_ConnectionFailed".Translate(validationKey.Translate());
                        return;
                    }

                    TestCloudConnection(config);
                }
                else
                {
                    TestLocalConnection();
                }
            }
            catch (Exception ex)
            {
                connectionTestStatus = "RimChat_ConnectionFailed".Translate(ex.Message);
            }
            finally
            {
                isTestingConnection = false;
            }
        }

        internal void TestCloudConnection(ApiConfig config)
        {
            string runtimeHint = string.Empty;
            string chatFallbackUrl = string.Empty;
            bool allowChatFallback = false;
            string url = ResolveCloudModelListTestUrl(config, out runtimeHint, out chatFallbackUrl, out allowChatFallback);

            // Player2 has no models endpoint; test connectivity via chat completions directly
            if (config.Provider == AIProvider.Player2)
            {
                string chatUrl = config.GetEffectiveEndpoint();
                CloudProbeResult p2Probe = ProbeCloudEndpoint(config, chatUrl, "POST", BuildConnectionTestChatBody(config));
                if (p2Probe.IsSuccess || p2Probe.IsChatFallbackReachable)
                {
                    connectionTestStatus = "RimChat_ConnectionSuccess".Translate();
                }
                else
                {
                    string p2Reason = p2Probe.HasResponseCode
                        ? $"HTTP {p2Probe.ResponseCode}"
                        : p2Probe.Error;
                    if (string.IsNullOrWhiteSpace(p2Reason)) p2Reason = "Unknown error";
                    connectionTestStatus = "RimChat_ConnectionFailed".Translate(p2Reason);
                }
                return;
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                string failed = "RimChat_ConnectionFailed".Translate("RimChat_ErrorEmptyUrl".Translate());
                connectionTestStatus = ComposeConnectionStatus(failed, runtimeHint, false);
                return;
            }

            CloudProbeResult modelsProbe = ProbeCloudEndpoint(config, url, "GET", null);
            if (modelsProbe.IsSuccess)
            {
                connectionTestStatus = ComposeConnectionStatus("RimChat_ConnectionSuccess".Translate(), runtimeHint, false);
                return;
            }

            if (modelsProbe.IsAuthError)
            {
                string failed = "RimChat_ConnectionFailed".Translate("RimChat_InvalidAPIKey".Translate());
                connectionTestStatus = ComposeConnectionStatus(failed, runtimeHint, false);
                return;
            }

            bool usedChatFallback = false;
            CloudProbeResult chatProbe = default(CloudProbeResult);
            if (allowChatFallback && !string.IsNullOrWhiteSpace(chatFallbackUrl))
            {
                chatProbe = ProbeCloudEndpoint(config, chatFallbackUrl, "POST", BuildConnectionTestChatBody(config));
                if (chatProbe.IsAuthError)
                {
                    string failed = "RimChat_ConnectionFailed".Translate("RimChat_InvalidAPIKey".Translate());
                    connectionTestStatus = ComposeConnectionStatus(failed, runtimeHint, false);
                    return;
                }

                if (chatProbe.IsChatFallbackReachable)
                {
                    usedChatFallback = true;
                    connectionTestStatus = ComposeConnectionStatus("RimChat_ConnectionSuccess".Translate(), runtimeHint, true);
                    return;
                }
            }

            CloudProbeResult failedProbe = chatProbe.HasResponseCode ? chatProbe : modelsProbe;
            string reason = failedProbe.HasResponseCode
                ? $"HTTP {failedProbe.ResponseCode}"
                : failedProbe.Error;
            if (string.IsNullOrWhiteSpace(reason))
            {
                reason = "Unknown error";
            }

            string status = "RimChat_ConnectionFailed".Translate(reason);
            connectionTestStatus = ComposeConnectionStatus(status, runtimeHint, usedChatFallback);
        }

        internal string ResolveCloudModelListTestUrl(
            ApiConfig config,
            out string runtimeHint,
            out string chatFallbackUrl,
            out bool allowChatFallback)
        {
            runtimeHint = string.Empty;
            chatFallbackUrl = string.Empty;
            allowChatFallback = false;

            string url = config.Provider.GetListModelsUrl();
            bool allowBaseUrlOverride = config.Provider != AIProvider.DeepSeek;
            bool hasBaseUrlOverride = allowBaseUrlOverride && !string.IsNullOrEmpty(config.BaseUrl);
            if (!hasBaseUrlOverride)
            {
                return Pages.ProviderCloudFetch.BuildModelListRequestUrl(config, url);
            }

            if (config.Provider == AIProvider.Custom && config.TryResolveCustomRuntimeEndpoints(out CustomUrlRuntimeResolution resolved))
            {
                runtimeHint = BuildCustomRuntimeHint(resolved);
                chatFallbackUrl = resolved.ChatEndpoint;
                allowChatFallback = config.CustomUrlMode == CustomUrlMode.FullEndpoint;
                return Pages.ProviderCloudFetch.BuildModelListRequestUrl(config, resolved.ModelsEndpoint);
            }

            return Pages.ProviderCloudFetch.BuildModelListRequestUrl(config, url);
        }

        internal static string BuildCustomRuntimeHint(CustomUrlRuntimeResolution resolution)
        {
            var segments = new List<string>();
            if (resolution.WasSiliconFlowHostMapped)
            {
                segments.Add("RimChat_CustomUrlMappedHint".Translate(resolution.ChatEndpoint));
            }

            if (resolution.HasSuspiciousBasePath)
            {
                segments.Add("RimChat_CustomUrlSuspiciousPathHint".Translate());
            }

            return string.Join(" ", segments.Where(segment => !string.IsNullOrWhiteSpace(segment)));
        }

        internal ApiConfig ResolvePrimaryCloudConfigForConnectivity()
        {
            if (Settings.CloudConfigs == null || Settings.CloudConfigs.Count == 0)
            {
                return null;
            }

            // Prefer enabled configs that are valid
            ApiConfig ready = Settings.CloudConfigs.FirstOrDefault(cfg =>
                cfg != null &&
                cfg.IsEnabled &&
                cfg.IsValid());
            if (ready != null)
            {
                return ready;
            }

            ApiConfig enabled = Settings.CloudConfigs.FirstOrDefault(cfg => cfg != null && cfg.IsEnabled);
            return enabled ?? Settings.CloudConfigs.FirstOrDefault(cfg => cfg != null);
        }

        internal static bool TryValidateCloudConfigForConnectivity(ApiConfig config, out string validationKey)
        {
            if (config == null)
            {
                validationKey = "RimChat_NoValidConfig";
                return false;
            }

            // All cloud providers require an API key
            if (string.IsNullOrWhiteSpace(config.ApiKey))
            {
                validationKey = "RimChat_EnterApiKey";
                return false;
            }

            validationKey = string.Empty;
            return true;
        }

        internal static string ComposeConnectionStatus(string status, string runtimeHint, bool usedChatFallback)
        {
            var segments = new List<string>();
            if (!string.IsNullOrWhiteSpace(status))
            {
                segments.Add(status);
            }

            if (usedChatFallback)
            {
                segments.Add("RimChat_CustomUrlChatFallbackHint".Translate());
            }

            if (!string.IsNullOrWhiteSpace(runtimeHint))
            {
                segments.Add(runtimeHint);
            }

            return string.Join(" ", segments);
        }

        internal CloudProbeResult ProbeCloudEndpoint(ApiConfig config, string url, string method, string body)
        {
            using (var request = new UnityWebRequest(url, method))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = 10;
                RelationsProviderCloudModelFetch.SetModelListAuthHeader(request, config.Provider, config.ApiKey);

                if (!string.IsNullOrEmpty(body))
                {
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.SetRequestHeader("Content-Type", "application/json");
                }

                var operation = request.SendWebRequest();
                while (!operation.isDone) { System.Threading.Thread.Sleep(100); }

                return new CloudProbeResult
                {
                    Result = request.result,
                    ResponseCode = request.responseCode,
                    Error = request.error ?? string.Empty
                };
            }
        }

        internal static string BuildConnectionTestChatBody(ApiConfig config)
        {
            // Player2 does not accept a model field; it selects the model server-side
            if (config.Provider == AIProvider.Player2)
            {
                return "{\"messages\":[{\"role\":\"user\",\"content\":\"ping\"}]}";
            }

            string model = config.GetEffectiveModelName();
            if (string.IsNullOrWhiteSpace(model))
            {
                model = "test";
            }

            string escapedModel = model.Replace("\\", "\\\\").Replace("\"", "\\\"");
            return $"{{\"model\":\"{escapedModel}\",\"messages\":[{{\"role\":\"user\",\"content\":\"ping\"}}]}}";
        }

        internal void TestLocalConnection()
        {
            string baseUrl = Settings.LocalConfig.GetNormalizedBaseUrl().TrimEnd('/');
            bool isPlayer2Local = Settings.LocalConfig.IsPlayer2Local();

            // Player2 local: test via health check then chat endpoint with game-key header
            if (isPlayer2Local)
            {
                string healthUrl = Player2Endpoints.Health(baseUrl);
                bool healthOk = TryTestUrl(healthUrl, "GET", null);
                if (!healthOk)
                {
                    connectionTestStatus = "RimChat_ConnectionFailed".Translate("Player2 local app not running");
                    return;
                }

                string chatUrl = Player2Endpoints.ChatCompletions(baseUrl);
                string chatBody = "{\"messages\":[{\"role\":\"user\",\"content\":\"ping\"}]}";
                bool chatOk = TryTestUrlWithHeaders(chatUrl, "POST", chatBody, AIProvider.Player2.GetExtraHeaders());
                connectionTestStatus = chatOk
                    ? "RimChat_ConnectionSuccess".Translate()
                    : "RimChat_ConnectionFailed".Translate("Player2 chat endpoint unreachable");
                return;
            }
            
            // Try Ollama endpoint first
            string testUrl = baseUrl + "/api/tags";
            bool success = TryTestUrl(testUrl, "GET", null);
            
            // If Ollama fails, try OpenAI-compatible models endpoint
            if (!success)
            {
                testUrl = baseUrl + "/v1/models";
                success = TryTestUrl(testUrl, "GET", null);
            }
            
            // If both fail, try a simple POST to chat completions endpoint
            if (!success)
            {
                testUrl = baseUrl + "/v1/chat/completions";
                success = TryTestUrl(testUrl, "POST", "{\"model\":\"test\",\"messages\":[]}");
            }
            
            if (success)
            {
                connectionTestStatus = "RimChat_ConnectionSuccess".Translate();
            }
            else
            {
                connectionTestStatus = "RimChat_ConnectionFailed".Translate("RimChat_LocalServiceNotFound".Translate());
            }
        }

        internal bool TryTestUrlWithHeaders(string url, string method, string body, Dictionary<string, string> extraHeaders)
        {
            try
            {
                using (var request = new UnityWebRequest(url, method))
                {
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.timeout = 5;

                    if (body != null)
                    {
                        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(body);
                        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                        request.SetRequestHeader("Content-Type", "application/json");
                    }

                    if (extraHeaders != null)
                    {
                        foreach (var header in extraHeaders)
                        {
                            request.SetRequestHeader(header.Key, header.Value);
                        }
                    }

                    var operation = request.SendWebRequest();
                    while (!operation.isDone) { System.Threading.Thread.Sleep(50); }

                    long responseCode = request.responseCode;
                    if (responseCode == 401 || responseCode == 403) return false;
                    return responseCode > 0 && responseCode != 404;
                }
            }
            catch
            {
                return false;
            }
        }

        internal bool TryTestUrl(string url, string method, string body)
        {
            try
            {
                using (var request = new UnityWebRequest(url, method))
                {
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.timeout = 5;
                    
                    if (body != null)
                    {
                        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(body);
                        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                        request.SetRequestHeader("Content-Type", "application/json");
                    }

                    var operation = request.SendWebRequest();
                    while (!operation.isDone) { System.Threading.Thread.Sleep(50); }

                    long responseCode = request.responseCode;
                    if (responseCode == 401 || responseCode == 403)
                    {
                        return false;
                    }

                    if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
                    {
                        return responseCode >= 200 && responseCode < 300;
                    }

                    if (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
                    {
                        return responseCode > 0 && responseCode != 404;
                    }

                    return responseCode > 0;
                }
            }
            catch
            {
                return false;
            }
        }
}
