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

internal sealed class RelationsProviderCloudModelFetch
{
    internal readonly RelationsProviderCloudSection Owner;

    internal RelationsProviderCloudModelFetch(RelationsProviderCloudSection owner)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    internal RelationsSettingsPages Pages => Owner.Pages;
    internal RelationsSettings Settings => Owner.Settings;

        internal static void LogCustomUrlResolutionHint(CustomUrlRuntimeResolution resolution)
        {
            if (resolution.WasSiliconFlowHostMapped)
            {
                Log.Message($"[RimAI.Relations] Custom URL host mapped to API domain: {resolution.ChatEndpoint}");
            }

            if (resolution.HasSuspiciousBasePath)
            {
                Log.Warning("[RimAI.Relations] Custom BaseUrl path looks non-standard for Base URL mode. The value was kept unchanged.");
            }
        }

        internal string BuildModelListRequestUrl(ApiConfig config, string baseUrl)
        {
            if (config.Provider == AIProvider.Google)
            {
                return AppendQueryParameter(baseUrl, "key", config.ApiKey);
            }

            return baseUrl;
        }

        internal string BuildProviderModelListRequestUrl(ApiConfig config)
        {
            string providerUrl = config.Provider.GetListModelsUrl();
            if (string.IsNullOrWhiteSpace(providerUrl))
            {
                return string.Empty;
            }

            return BuildModelListRequestUrl(config, providerUrl);
        }

        internal string BuildModelCacheKey(AIProvider provider, string baseUrl, string apiKey)
        {
            string keyFingerprint = ComputeApiKeyFingerprint(apiKey);
            return $"{provider}:{baseUrl}:{keyFingerprint}";
        }

        internal static string ComputeApiKeyFingerprint(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return "nokey";
            }

            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(apiKey.Trim());
                byte[] hash = sha256.ComputeHash(bytes);
                return BitConverter.ToString(hash, 0, 6).Replace("-", string.Empty);
            }
        }

        internal static string AppendQueryParameter(string url, string name, string value)
        {
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value))
            {
                return url;
            }

            if (url.IndexOf($"{name}=", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return url;
            }

            char separator = url.Contains("?") ? '&' : '?';
            return $"{url}{separator}{name}={Uri.EscapeDataString(value)}";
        }

        internal static void SetModelListAuthHeader(UnityWebRequest request, AIProvider provider, string apiKey)
        {
            string trimmedKey = apiKey?.Trim();

            // Player2 requires both Bearer token and game-key header
            if (provider == AIProvider.Player2)
            {
                if (!string.IsNullOrWhiteSpace(trimmedKey))
                {
                    request.SetRequestHeader("Authorization", $"Bearer {trimmedKey}");
                }
                var extraHeaders = provider.GetExtraHeaders();
                if (extraHeaders != null)
                {
                    foreach (var header in extraHeaders)
                    {
                        request.SetRequestHeader(header.Key, header.Value);
                    }
                }
                return;
            }

            if (string.IsNullOrWhiteSpace(trimmedKey))
            {
                return;
            }

            if (provider == AIProvider.Google)
            {
                request.SetRequestHeader("x-goog-api-key", trimmedKey);
                return;
            }

            request.SetRequestHeader("Authorization", $"Bearer {trimmedKey}");

            // Add provider-specific extra headers
            var extraHdrs = provider.GetExtraHeaders();
            if (extraHdrs != null)
            {
                foreach (var header in extraHdrs)
                {
                    request.SetRequestHeader(header.Key, header.Value);
                }
            }
        }

        internal List<string> BuildModelListRequestCandidates(string requestUrl, string providerFallbackUrl, AIProvider provider)
        {
            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(requestUrl))
            {
                candidates.Add(requestUrl);
            }

            if (!string.IsNullOrWhiteSpace(providerFallbackUrl))
            {
                candidates.Add(providerFallbackUrl);
            }

            const string v1ModelsSuffix = "/v1/models";
            if (provider != AIProvider.Google
                && requestUrl != null
                && requestUrl.EndsWith(v1ModelsSuffix, StringComparison.OrdinalIgnoreCase))
            {
                string fallback = requestUrl.Substring(0, requestUrl.Length - v1ModelsSuffix.Length) + "/models";
                if (!string.Equals(fallback, requestUrl, StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Add(fallback);
                }
            }

            return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        internal void FetchModelsCoroutine(string url, string providerFallbackUrl, string apiKey, AIProvider provider, string cacheKey, Action<List<string>> callback)
        {
            // 绾喕绻欰IChatServiceAsync鐎圭偘绶ョ€涙ê婀?
            var service = AIChatServiceAsync.Instance;
            List<string> candidateUrls = BuildModelListRequestCandidates(url, providerFallbackUrl, provider);
            Log.Message($"[RimAI.Relations] FetchModelsCoroutine: provider={provider}, candidateUrls={string.Join(" | ", candidateUrls)}");

            Task.Run(() =>
            {
                List<string> models = null;
                try
                {
                    foreach (string candidateUrl in candidateUrls)
                    {
                        Log.Message($"[RimAI.Relations] FetchModelsCoroutine: trying url={candidateUrl}");
                        using (var request = new UnityWebRequest(candidateUrl, "GET"))
                        {
                            request.downloadHandler = new DownloadHandlerBuffer();
                            SetModelListAuthHeader(request, provider, apiKey);
                            request.timeout = 10;

                            var operation = request.SendWebRequest();
                        
                            while (!operation.isDone)
                            {
                                System.Threading.Thread.Sleep(50);
                            }

                            if (request.result == UnityWebRequest.Result.Success)
                            {
                                Log.Message($"[RimAI.Relations] FetchModelsCoroutine: url={candidateUrl}, result={request.result}, responseCode={request.responseCode}");
                                models = ParseModelsFromResponse(request.downloadHandler.text, provider);
                                RelationsSettings.ModelCache[cacheKey] = models;
                                Log.Message($"[RimAI.Relations] FetchModelsCoroutine: success, parsed {models?.Count ?? 0} models");
                                break;
                            }

                            string body = request.downloadHandler?.text ?? string.Empty;
                            if (body.Length > 240)
                            {
                                body = body.Substring(0, 240) + "...";
                            }

                            Log.Warning($"[RimAI.Relations] Failed to fetch models: url={candidateUrl}, HTTP {request.responseCode}, error={request.error}, body={body}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[RimAI.Relations] Failed to fetch models: {ex.Message}");
                }

                // Marshal callback back to Unity main thread before touching UI.
                service.ExecuteOnMainThread(() => callback(models));
            });
        }

        internal List<string> ParseModelsFromResponse(string json, AIProvider provider)
        {
            try
            {
                if (provider == AIProvider.Google)
                {
                    return ParseGoogleModelsFromResponse(json);
                }

                return ParseOpenAIModelsFromResponse(json);
            }
            catch
            {
                return new List<string>();
            }
        }

        internal List<string> ParseOpenAIModelsFromResponse(string json)
        {
            var response = JsonUtility.FromJson<OpenAIModelListResponse>(json);
            List<string> models = response?.data
                ?.Select(model => model?.id)
                .Where(modelId => !string.IsNullOrWhiteSpace(modelId))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(modelId => modelId, StringComparer.OrdinalIgnoreCase)
                .ToList()
                ?? new List<string>();

            if (models.Count == 0)
            {
                models = ExtractModelIdsFromJson(json);
            }

            return models;
        }

        internal List<string> ParseGoogleModelsFromResponse(string json)
        {
            var response = JsonUtility.FromJson<GoogleModelListResponse>(json);
            List<string> models = response?.models?
                .Where(SupportsGenerateContent)
                .Select(model => NormalizeGoogleModelName(model.name))
                .Where(modelName => !string.IsNullOrWhiteSpace(modelName))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(modelName => modelName, StringComparer.OrdinalIgnoreCase)
                .ToList()
                ?? new List<string>();

            if (models.Count > 0)
            {
                return models;
            }

            return ExtractGoogleModelNamesFromJson(json);
        }

        internal static bool SupportsGenerateContent(GoogleModelInfo model)
        {
            if (model?.supportedGenerationMethods == null || model.supportedGenerationMethods.Length == 0)
            {
                return true;
            }

            return model.supportedGenerationMethods.Any(method =>
                string.Equals(method, "generateContent", StringComparison.OrdinalIgnoreCase));
        }

        internal static string NormalizeGoogleModelName(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return string.Empty;
            }

            string normalized = modelName.Trim();
            const string prefix = "models/";
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(prefix.Length);
            }

            normalized = normalized.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            return normalized;
        }

        internal static List<string> ExtractGoogleModelNamesFromJson(string json)
        {
            return ExtractQuotedValuesFromJson(json, "\"name\"")
                .Select(NormalizeGoogleModelName)
                .Where(modelName => !string.IsNullOrWhiteSpace(modelName))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(modelName => modelName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        internal static List<string> ExtractModelIdsFromJson(string json)
        {
            return ExtractQuotedValuesFromJson(json, "\"id\"")
                .Distinct(StringComparer.Ordinal)
                .OrderBy(modelId => modelId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        internal static List<string> ExtractQuotedValuesFromJson(string json, string token)
        {
            var results = new List<string>();
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(token))
            {
                return results;
            }

            int index = 0;
            while ((index = json.IndexOf(token, index, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                index = json.IndexOf(':', index);
                if (index < 0)
                {
                    break;
                }

                index++;
                while (index < json.Length && char.IsWhiteSpace(json[index]))
                {
                    index++;
                }

                if (index >= json.Length || json[index] != '\"')
                {
                    continue;
                }

                int start = ++index;
                int end = json.IndexOf('\"', start);
                if (end < 0)
                {
                    break;
                }

                string value = json.Substring(start, end - start);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    results.Add(value);
                }

                index = end + 1;
            }

            return results;
        }
}
