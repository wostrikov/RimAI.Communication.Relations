using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Ustas.RimAI.Communication.Relations.AI;

namespace Ustas.RimAI.Communication.Relations.Config
{
    /// <summary>
    /// GPT Image 2 / direct OpenAI Images API contract. No Verse or Unity dependency.
    /// </summary>
    public static class DiplomacyOpenAiImageContract
    {
        public const string ProviderPresetNative = "openai";
        public const string ProviderPresetCompatible = "openai_compatible";
        public const string CanonicalEndpoint = "https://api.openai.com/v1/images/generations";
        public const string RecommendedModel = "gpt-image-2";
        public const string LegacyDefaultModel = "gpt-image-1";
        public const string SizeAuto = "auto";
        public const string QualityAuto = "auto";
        public const string FormatPng = "png";
        public const string FormatJpeg = "jpeg";
        public const string FormatWebp = "webp";
        public const string BackgroundAuto = "auto";
        public const string BackgroundOpaque = "opaque";
        public const string BackgroundTransparent = "transparent";

        public static readonly string[] SizePresets =
        {
            SizeAuto,
            "1024x1024",
            "1536x1024",
            "1024x1536",
            "2048x1152",
            "2048x2048",
            "2560x1440",
            "3840x2160",
            "2160x3840"
        };

        public static readonly string[] QualityValues = { "auto", "low", "medium", "high" };
        public static readonly string[] OutputFormats = { FormatPng, FormatJpeg, FormatWebp };
        public static readonly string[] BackgroundValues = { BackgroundAuto, BackgroundOpaque, BackgroundTransparent };

        private const int MaxEdge = 3840;
        private const int MinPixels = 655360;
        private const int MaxPixels = 8294400;
        private const int SizeMultiple = 16;
        private const double MaxAspect = 3.0;

        private static readonly Regex B64Field = new Regex(
            "\"b64_json\"\\s*:\\s*\"(?<value>(?:[^\"\\\\]|\\\\.)*)\"",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex UrlField = new Regex(
            "\"url\"\\s*:\\s*\"(?<value>(?:[^\"\\\\]|\\\\.)*)\"",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static bool IsNativeProvider(string providerPreset)
        {
            return string.Equals(NormalizeText(providerPreset), ProviderPresetNative, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsCompatibleProvider(string providerPreset)
        {
            return string.Equals(NormalizeText(providerPreset), ProviderPresetCompatible, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsCanonicalEndpoint(string endpoint)
        {
            return string.Equals(NormalizeEndpoint(endpoint), CanonicalEndpoint, StringComparison.OrdinalIgnoreCase);
        }

        public static string NormalizeEndpoint(string endpoint)
        {
            return NormalizeText(endpoint).TrimEnd('/');
        }

        public static bool ShouldMigrateCompatibleToNative(string providerPreset, string endpoint)
        {
            return IsCompatibleProvider(providerPreset) && IsCanonicalEndpoint(endpoint);
        }

        public static string MigrateLegacyDefaultModel(string model)
        {
            string normalized = NormalizeText(model);
            if (string.IsNullOrWhiteSpace(normalized) ||
                string.Equals(normalized, LegacyDefaultModel, StringComparison.OrdinalIgnoreCase))
            {
                return RecommendedModel;
            }

            return normalized;
        }

        public static string NormalizeQuality(string quality)
        {
            string normalized = NormalizeText(quality).ToLowerInvariant();
            for (int i = 0; i < QualityValues.Length; i++)
            {
                if (string.Equals(normalized, QualityValues[i], StringComparison.OrdinalIgnoreCase))
                {
                    return QualityValues[i];
                }
            }

            return QualityAuto;
        }

        public static string NormalizeOutputFormat(string format)
        {
            string normalized = NormalizeText(format).ToLowerInvariant();
            if (normalized == "jpg")
            {
                normalized = FormatJpeg;
            }

            for (int i = 0; i < OutputFormats.Length; i++)
            {
                if (string.Equals(normalized, OutputFormats[i], StringComparison.OrdinalIgnoreCase))
                {
                    return OutputFormats[i];
                }
            }

            return FormatPng;
        }

        public static string NormalizeBackground(string background, string outputFormat)
        {
            string normalized = NormalizeText(background).ToLowerInvariant();
            string format = NormalizeOutputFormat(outputFormat);
            if (string.Equals(normalized, BackgroundTransparent, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(format, FormatJpeg, StringComparison.OrdinalIgnoreCase))
            {
                return BackgroundOpaque;
            }

            for (int i = 0; i < BackgroundValues.Length; i++)
            {
                if (string.Equals(normalized, BackgroundValues[i], StringComparison.OrdinalIgnoreCase))
                {
                    return BackgroundValues[i];
                }
            }

            return BackgroundAuto;
        }

        public static string CanonicalizeSizeToken(string rawSize)
        {
            string normalized = NormalizeText(rawSize).Replace('X', 'x');
            if ((normalized.StartsWith("\"", StringComparison.Ordinal) && normalized.EndsWith("\"", StringComparison.Ordinal)) ||
                (normalized.StartsWith("'", StringComparison.Ordinal) && normalized.EndsWith("'", StringComparison.Ordinal)))
            {
                normalized = normalized.Substring(1, normalized.Length - 2).Trim();
            }

            return normalized;
        }

        public static bool IsSizePreset(string size)
        {
            string normalized = CanonicalizeSizeToken(size);
            for (int i = 0; i < SizePresets.Length; i++)
            {
                if (string.Equals(normalized, SizePresets[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool TryNormalizeSize(string rawSize, out string normalized)
        {
            normalized = CanonicalizeSizeToken(rawSize);
            if (string.Equals(normalized, SizeAuto, StringComparison.OrdinalIgnoreCase))
            {
                normalized = SizeAuto;
                return true;
            }

            return TryValidateCustomSize(normalized, out normalized);
        }

        public static bool TryValidateCustomSize(string rawSize, out string normalized)
        {
            normalized = CanonicalizeSizeToken(rawSize);
            int sep = normalized.IndexOf('x');
            if (sep <= 0 || sep >= normalized.Length - 1)
            {
                return false;
            }

            if (!int.TryParse(normalized.Substring(0, sep), NumberStyles.Integer, CultureInfo.InvariantCulture, out int width) ||
                !int.TryParse(normalized.Substring(sep + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int height))
            {
                return false;
            }

            if (width <= 0 || height <= 0)
            {
                return false;
            }

            if (width % SizeMultiple != 0 || height % SizeMultiple != 0)
            {
                return false;
            }

            int longEdge = Math.Max(width, height);
            int shortEdge = Math.Min(width, height);
            if (longEdge > MaxEdge)
            {
                return false;
            }

            if (shortEdge <= 0 || (double)longEdge / shortEdge > MaxAspect + 0.0001)
            {
                return false;
            }

            long pixels = (long)width * height;
            if (pixels < MinPixels || pixels > MaxPixels)
            {
                return false;
            }

            normalized = width.ToString(CultureInfo.InvariantCulture) + "x" + height.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        public static string BuildGenerationRequestJson(
            string model,
            string prompt,
            string size,
            string quality,
            string outputFormat,
            string background)
        {
            if (string.IsNullOrWhiteSpace(model))
            {
                throw new ArgumentException("OpenAI image model is required.", nameof(model));
            }

            if (string.IsNullOrWhiteSpace(prompt))
            {
                throw new ArgumentException("OpenAI image prompt is required.", nameof(prompt));
            }

            if (!TryNormalizeSize(size, out string normalizedSize))
            {
                throw new ArgumentException("GPT Image 2 size is invalid.", nameof(size));
            }

            string format = NormalizeOutputFormat(outputFormat);
            return "{"
                + "\"model\":\"" + EscapeJson(model) + "\","
                + "\"prompt\":\"" + EscapeJson(prompt) + "\","
                + "\"size\":\"" + EscapeJson(normalizedSize) + "\","
                + "\"quality\":\"" + EscapeJson(NormalizeQuality(quality)) + "\","
                + "\"output_format\":\"" + EscapeJson(format) + "\","
                + "\"background\":\"" + EscapeJson(NormalizeBackground(background, format)) + "\""
                + "}";
        }

        public static bool RequestJsonContainsUnsupportedOpenAiFields(string json)
        {
            string body = json ?? string.Empty;
            return body.IndexOf("\"watermark\"", StringComparison.OrdinalIgnoreCase) >= 0
                || body.IndexOf("sequential_image_generation", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool TryParseGenerationResponse(string json, out string b64Json, out string imageUrl)
        {
            b64Json = string.Empty;
            imageUrl = string.Empty;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            Match b64 = B64Field.Match(json);
            if (b64.Success)
            {
                b64Json = UnescapeJson(b64.Groups["value"].Value);
                if (!string.IsNullOrWhiteSpace(b64Json))
                {
                    return true;
                }
            }

            Match url = UrlField.Match(json);
            if (url.Success)
            {
                imageUrl = UnescapeJson(url.Groups["value"].Value);
                return imageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                    || imageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        public static string ResolveModelsProbeUrl(string model)
        {
            string id = NormalizeText(model);
            if (string.IsNullOrWhiteSpace(id))
            {
                id = RecommendedModel;
            }

            return OpenAIProviderAdapter.ModelsEndpoint.TrimEnd('/') + "/" + Uri.EscapeDataString(id);
        }

        public static OpenAiImageProbeOutcome ClassifyProbe(
            bool credentialPresent,
            long httpStatus,
            string responseBody)
        {
            if (!credentialPresent)
            {
                return OpenAiImageProbeOutcome.MissingCredential;
            }

            if (httpStatus == 200)
            {
                return OpenAiImageProbeOutcome.Success;
            }

            OpenAIError error = OpenAIProviderAdapter.ParseError(httpStatus, responseBody);
            if (error.Category == OpenAIErrorCategory.AuthenticationError ||
                error.Category == OpenAIErrorCategory.PermissionError)
            {
                return OpenAiImageProbeOutcome.Unauthorized;
            }

            if (error.Category == OpenAIErrorCategory.ModelNotFound || httpStatus == 404)
            {
                return OpenAiImageProbeOutcome.ModelUnavailable;
            }

            if (error.Category == OpenAIErrorCategory.RateLimit)
            {
                return OpenAiImageProbeOutcome.RateLimited;
            }

            string merged = ((error.Code ?? string.Empty) + " " + (error.Type ?? string.Empty) + " " + (error.Message ?? string.Empty) + " " + (responseBody ?? string.Empty)).ToLowerInvariant();
            if (merged.Contains("moderation") || merged.Contains("content_policy") || merged.Contains("safety"))
            {
                return OpenAiImageProbeOutcome.ModerationBlocked;
            }

            return OpenAiImageProbeOutcome.TransportFailure;
        }

        public static DiplomacyOpenAiImageFields CreateNewNativeDefaults()
        {
            return new DiplomacyOpenAiImageFields
            {
                IsEnabled = false,
                ProviderPreset = ProviderPresetNative,
                Endpoint = CanonicalEndpoint,
                ApiKey = string.Empty,
                Model = RecommendedModel,
                Size = SizeAuto,
                Quality = QualityAuto,
                OutputFormat = FormatPng,
                Background = BackgroundAuto,
                SchemaPreset = "openai",
                Mode = "sync_payload",
                AuthMode = "bearer"
            };
        }

        public static void NormalizeNativeFields(DiplomacyOpenAiImageFields fields)
        {
            if (fields == null)
            {
                return;
            }

            if (ShouldMigrateCompatibleToNative(fields.ProviderPreset, fields.Endpoint))
            {
                fields.ProviderPreset = ProviderPresetNative;
            }

            if (!IsNativeProvider(fields.ProviderPreset))
            {
                return;
            }

            fields.ProviderPreset = ProviderPresetNative;
            fields.Endpoint = CanonicalEndpoint;
            fields.ApiKey = string.Empty;
            fields.Model = MigrateLegacyDefaultModel(fields.Model);
            fields.Quality = NormalizeQuality(fields.Quality);
            fields.OutputFormat = NormalizeOutputFormat(fields.OutputFormat);
            fields.Background = NormalizeBackground(fields.Background, fields.OutputFormat);
            fields.SchemaPreset = "openai";
            fields.Mode = "sync_payload";
            fields.AuthMode = "bearer";
            string size = CanonicalizeSizeToken(fields.Size);
            fields.Size = string.IsNullOrWhiteSpace(size) ? SizeAuto : size;
        }

        public static string NormalizeText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!char.IsControl(c))
                {
                    builder.Append(c);
                }
            }

            return builder.ToString().Trim();
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        private static string UnescapeJson(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Replace("\\/", "/").Replace("\\\\", "\\").Replace("\\\"", "\"").Trim();
        }
    }

    public enum OpenAiImageProbeOutcome
    {
        Success,
        MissingCredential,
        Unauthorized,
        ModelUnavailable,
        RateLimited,
        ModerationBlocked,
        TransportFailure
    }

    public sealed class DiplomacyOpenAiImageFields
    {
        public bool IsEnabled;
        public string ProviderPreset = string.Empty;
        public string Endpoint = string.Empty;
        public string ApiKey = string.Empty;
        public string Model = string.Empty;
        public string Size = string.Empty;
        public string Quality = string.Empty;
        public string OutputFormat = string.Empty;
        public string Background = string.Empty;
        public string SchemaPreset = string.Empty;
        public string Mode = string.Empty;
        public string AuthMode = string.Empty;
    }
}
