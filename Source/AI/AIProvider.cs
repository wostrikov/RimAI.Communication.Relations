using System.Collections.Generic;
using System.Text;
using Ustas.RimAI.Core.Configuration;
using Ustas.RimAI.Core.Player2;

namespace Ustas.RimAI.Communication.Relations.AI
{
    public enum AIProvider
    {
        OpenAI,
        Google,
        DeepSeek,
        OpenRouter,
        GLM,
        Kimi,
        Mistral,
        Grok,
        Player2,
        Custom,
        None
    }

    public static class AIProviderRegistry
    {
        static readonly Dictionary<string, string> Player2SessionHeaders = new()
        {
            { Player2GameKeys.HeaderName, Player2GameKeys.Relations }
        };

        public static string GetLabel(this AIProvider p) => GameplayTextAiProviderCatalog.Label(p.ToString());

        public static string GetEndpointUrl(this AIProvider p)
        {
            return NormalizeProviderUrl(GameplayTextAiProviderCatalog.ResolveChatEndpoint(p.ToString(), null));
        }

        public static string GetListModelsUrl(this AIProvider p)
        {
            return NormalizeProviderUrl(GameplayTextAiProviderCatalog.ListModelsUrl(p.ToString()));
        }

        public static bool SupportsModelListing(this AIProvider p) =>
            GameplayTextAiProviderCatalog.SupportsModelListing(p.ToString());

        public static Dictionary<string, string> GetExtraHeaders(this AIProvider p)
        {
            if (p == AIProvider.Player2)
                return new Dictionary<string, string>(Player2SessionHeaders);

            var headers = GameplayTextAiProviderCatalog.ExtraHeaders(p.ToString());
            if (headers.Count == 0)
                return null;
            var copy = new Dictionary<string, string>();
            foreach (var pair in headers)
                copy[pair.Key] = pair.Value;
            return copy;
        }

        public static bool RequiresApiKey(this AIProvider p)
        {
            return p != AIProvider.None;
        }

        private static string NormalizeProviderUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(url.Length);
            for (int i = 0; i < url.Length; i++)
            {
                char current = url[i];
                if (!char.IsWhiteSpace(current))
                {
                    builder.Append(current);
                }
            }

            return builder.ToString().Trim();
        }
    }
}
