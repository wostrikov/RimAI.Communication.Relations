using System;
using System.IO;
using System.Text.RegularExpressions;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Config;

internal static class DiplomacyOpenAiImageTests
{
    public static void Run(Action<bool, string> check)
    {
        DiplomacyOpenAiImageFields fresh = DiplomacyOpenAiImageContract.CreateNewNativeDefaults();
        check(fresh.Model == DiplomacyOpenAiImageContract.RecommendedModel, "openai image default model is gpt-image-2");
        check(fresh.Endpoint == DiplomacyOpenAiImageContract.CanonicalEndpoint, "openai image endpoint is canonical");
        check(fresh.Size == DiplomacyOpenAiImageContract.SizeAuto, "new openai size default is auto");
        check(fresh.Quality == "auto", "new openai quality default is auto");
        check(string.IsNullOrEmpty(fresh.ApiKey), "new openai config has no persisted secret");
        check(!fresh.IsEnabled, "new openai config stays disabled");

        DiplomacyOpenAiImageFields migrated = new DiplomacyOpenAiImageFields
        {
            IsEnabled = false,
            ProviderPreset = DiplomacyOpenAiImageContract.ProviderPresetCompatible,
            Endpoint = DiplomacyOpenAiImageContract.CanonicalEndpoint,
            Model = DiplomacyOpenAiImageContract.LegacyDefaultModel,
            ApiKey = "sk-live-secret",
            Size = "2560x1440",
            Quality = "high",
            OutputFormat = "png",
            Background = "auto"
        };
        DiplomacyOpenAiImageContract.NormalizeNativeFields(migrated);
        check(migrated.ProviderPreset == DiplomacyOpenAiImageContract.ProviderPresetNative, "canonical compatible endpoint migrates to OpenAI");
        check(migrated.Model == DiplomacyOpenAiImageContract.RecommendedModel, "untouched gpt-image-1 migrates to gpt-image-2");
        check(migrated.ApiKey == string.Empty, "native OpenAI does not persist api key");
        check(migrated.Size == "2560x1440", "existing size is preserved");
        check(migrated.Quality == "high", "existing quality is preserved");
        check(!migrated.IsEnabled, "migration does not enable generation");

        DiplomacyOpenAiImageFields customModel = new DiplomacyOpenAiImageFields
        {
            ProviderPreset = DiplomacyOpenAiImageContract.ProviderPresetNative,
            Endpoint = DiplomacyOpenAiImageContract.CanonicalEndpoint,
            Model = "my-custom-image-model"
        };
        DiplomacyOpenAiImageContract.NormalizeNativeFields(customModel);
        check(customModel.Model == "my-custom-image-model", "explicit custom model is preserved");

        check(DiplomacyOpenAiImageContract.TryNormalizeSize("2048x1152", out string preset) && preset == "2048x1152", "size preset 2048x1152 serializes");
        check(DiplomacyOpenAiImageContract.TryNormalizeSize("auto", out string autoSize) && autoSize == "auto", "size auto serializes");
        check(!DiplomacyOpenAiImageContract.TryValidateCustomSize("100x100", out _), "too-small custom size is rejected");
        check(!DiplomacyOpenAiImageContract.TryValidateCustomSize("3841x2160", out _), "edge above 3840 is rejected");
        check(!DiplomacyOpenAiImageContract.TryValidateCustomSize("1025x1024", out _), "non-multiple-of-16 size is rejected");

        check(DiplomacyOpenAiImageContract.NormalizeQuality("HIGH") == "high", "quality high maps");
        check(DiplomacyOpenAiImageContract.NormalizeQuality("medium") == "medium", "quality medium maps");
        check(DiplomacyOpenAiImageContract.NormalizeBackground("transparent", "jpeg") == "opaque", "jpeg plus transparent normalizes to opaque");

        string json = DiplomacyOpenAiImageContract.BuildGenerationRequestJson(
            DiplomacyOpenAiImageContract.RecommendedModel,
            "a rimworld pawn portrait",
            "auto",
            "auto",
            "png",
            "auto");
        check(json.Contains("\"model\":\"gpt-image-2\""), "gpt-image-2 request contains model");
        check(json.Contains("\"prompt\":\"a rimworld pawn portrait\""), "gpt-image-2 request contains prompt");
        check(json.Contains("\"size\":\"auto\""), "gpt-image-2 request contains size");
        check(json.Contains("\"quality\":\"auto\""), "gpt-image-2 request contains quality");
        check(json.Contains("\"output_format\":\"png\""), "gpt-image-2 request contains output_format");
        check(json.Contains("\"background\":\"auto\""), "gpt-image-2 request contains background");
        check(!DiplomacyOpenAiImageContract.RequestJsonContainsUnsupportedOpenAiFields(json), "openai request omits watermark");

        bool invalidSizeThrown = false;
        try
        {
            DiplomacyOpenAiImageContract.BuildGenerationRequestJson("gpt-image-2", "prompt", "99x99", "auto", "png", "auto");
        }
        catch (ArgumentException)
        {
            invalidSizeThrown = true;
        }
        check(invalidSizeThrown, "invalid custom GPT Image 2 size is rejected before request");

        DiplomacyOpenAiImageFields compatible = new DiplomacyOpenAiImageFields
        {
            ProviderPreset = DiplomacyOpenAiImageContract.ProviderPresetCompatible,
            Endpoint = "https://example.test/v1/images/generations",
            Model = "local-image",
            ApiKey = "custom-provider-key"
        };
        DiplomacyOpenAiImageContract.NormalizeNativeFields(compatible);
        check(compatible.ProviderPreset == DiplomacyOpenAiImageContract.ProviderPresetCompatible, "custom compatible provider is preserved");
        check(compatible.Endpoint == "https://example.test/v1/images/generations", "custom compatible endpoint is preserved");
        check(compatible.ApiKey == "custom-provider-key", "custom compatible key is preserved");

        const string responseJson = "{\"created\":1730000000,\"data\":[{\"b64_json\":\"aGVsbG8=\",\"revised_prompt\":\"ok\"}]}";
        check(DiplomacyOpenAiImageContract.TryParseGenerationResponse(responseJson, out string b64, out string imageUrl)
            && b64 == "aGVsbG8="
            && string.IsNullOrEmpty(imageUrl), "representative gpt-image-2 b64_json response parses");

        check(OpenAIProviderAdapter.CredentialVariable == "OPENAI_RIMAI", "image credential uses OPENAI_RIMAI");
        check(DiplomacyOpenAiImageContract.ResolveModelsProbeUrl("gpt-image-2").EndsWith("/v1/models/gpt-image-2"), "auth probe uses models endpoint");
        check(DiplomacyOpenAiImageContract.ClassifyProbe(false, 0, "") == OpenAiImageProbeOutcome.MissingCredential, "probe missing credential");
        check(DiplomacyOpenAiImageContract.ClassifyProbe(true, 401, "{\"error\":{\"type\":\"authentication_error\",\"message\":\"no\"}}") == OpenAiImageProbeOutcome.Unauthorized, "probe unauthorized");
        check(DiplomacyOpenAiImageContract.ClassifyProbe(true, 404, "{\"error\":{\"code\":\"model_not_found\",\"message\":\"no\"}}") == OpenAiImageProbeOutcome.ModelUnavailable, "probe model unavailable");
        check(DiplomacyOpenAiImageContract.ClassifyProbe(true, 429, "{\"error\":{\"type\":\"rate_limit_error\",\"message\":\"no\"}}") == OpenAiImageProbeOutcome.RateLimited, "probe rate limited");

        string roundtrip = Roundtrip(fresh);
        DiplomacyOpenAiImageFields restored = ParseRoundtrip(roundtrip);
        check(restored.ProviderPreset == fresh.ProviderPreset, "roundtrip provider");
        check(restored.Model == fresh.Model, "roundtrip model");
        check(restored.Size == fresh.Size, "roundtrip size");
        check(restored.Quality == fresh.Quality, "roundtrip quality");
        check(restored.OutputFormat == fresh.OutputFormat, "roundtrip format");
        check(restored.Background == fresh.Background, "roundtrip background");
        check(restored.IsEnabled == fresh.IsEnabled, "roundtrip enable");
        check(string.IsNullOrEmpty(restored.ApiKey), "roundtrip does not restore openai secret");

        check(!SourceContainsCjk("Source/UI/Settings/RelationsImageSettingsPage.cs"), "image settings page has no CJK helper text");
        check(!SourceContainsCjk("Source/UI/Settings/RelationsOpenAiImageSettingsUi.cs"), "openai image settings ui has no CJK helper text");
    }

    static string Roundtrip(DiplomacyOpenAiImageFields fields)
    {
        return string.Join("|",
            fields.IsEnabled ? "1" : "0",
            fields.ProviderPreset,
            fields.Endpoint,
            fields.Model,
            fields.Size,
            fields.Quality,
            fields.OutputFormat,
            fields.Background,
            fields.ApiKey ?? string.Empty);
    }

    static DiplomacyOpenAiImageFields ParseRoundtrip(string packed)
    {
        string[] parts = packed.Split('|');
        return new DiplomacyOpenAiImageFields
        {
            IsEnabled = parts[0] == "1",
            ProviderPreset = parts[1],
            Endpoint = parts[2],
            Model = parts[3],
            Size = parts[4],
            Quality = parts[5],
            OutputFormat = parts[6],
            Background = parts[7],
            ApiKey = parts.Length > 8 ? parts[8] : string.Empty
        };
    }

    static bool SourceContainsCjk(string relativePath)
    {
        string path = ResolveSource(relativePath);
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return true;
        }

        string text = File.ReadAllText(path);
        return Regex.IsMatch(text, @"[\u4e00-\u9fff]");
    }

    static string ResolveSource(string relativePath)
    {
        var probe = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (int depth = 0; depth < 8 && probe != null; depth++)
        {
            string[] candidates =
            {
                Path.Combine(probe.FullName, relativePath),
                Path.Combine(probe.FullName, "sources", "RimAI.Communication.Relations", relativePath)
            };
            for (int i = 0; i < candidates.Length; i++)
            {
                string full = Path.GetFullPath(candidates[i]);
                if (File.Exists(full))
                {
                    return full;
                }
            }

            probe = probe.Parent;
        }

        string fromBin = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", relativePath));
        return File.Exists(fromBin) ? fromBin : string.Empty;
    }
}
