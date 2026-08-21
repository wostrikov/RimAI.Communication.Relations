using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Config;

namespace Ustas.RimAI.Communication.Relations.DiplomacySystem
{
    internal static class DiplomacyImageRequestBinder
    {
        public static void Bind(DiplomacyImageApiConfig config, DiplomacyImageGenerationRequest request)
        {
            if (config == null || request == null)
            {
                return;
            }

            request.Endpoint = config.Endpoint;
            request.ApiKey = config.ApiKey;
            request.Model = config.Model;
            request.Size = config.DefaultSize;
            request.Quality = config.Quality;
            request.OutputFormat = config.OutputFormat;
            request.Background = config.Background;
            request.Watermark = config.DefaultWatermark;
            request.TimeoutSeconds = config.TimeoutSeconds;
            request.Mode = config.Mode;
            request.SchemaPreset = config.SchemaPreset;
            request.ProviderPreset = config.ProviderPreset;
            request.AuthMode = config.AuthMode;
            request.ApiKeyHeaderName = config.ApiKeyHeaderName;
            request.ApiKeyQueryName = config.ApiKeyQueryName;
            request.ResponseUrlPath = config.ResponseUrlPath;
            request.ResponseB64Path = config.ResponseB64Path;
            request.AsyncSubmitPath = config.AsyncSubmitPath;
            request.AsyncStatusPathTemplate = config.AsyncStatusPathTemplate;
            request.AsyncImageFetchPath = config.AsyncImageFetchPath;
            request.ComfyUiImageLoaderNode = config.ComfyUiImageLoaderNode;
            request.PollIntervalMs = config.PollIntervalMs;
            request.PollMaxAttempts = config.PollMaxAttempts;
            if (config.IsNativeOpenAi())
            {
                request.Endpoint = DiplomacyOpenAiImageContract.CanonicalEndpoint;
                request.ApiKey = OpenAIProviderAdapter.ResolveCredential();
                request.Watermark = false;
                request.ProviderPreset = DiplomacyOpenAiImageContract.ProviderPresetNative;
            }
        }
    }
}
