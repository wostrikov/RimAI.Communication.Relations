using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Core.Player2;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Config
{
    public class LocalModelConfig : IExposable
    {
        public string BaseUrl = "http://localhost:11434";
        public string ModelName = "";

        public void ExposeData()
        {
            Scribe_Values.Look(ref BaseUrl, "baseUrl", "http://localhost:11434");
            Scribe_Values.Look(ref ModelName, "modelName", "");
            BaseUrl = ApiConfig.NormalizeUrl(BaseUrl);
        }

        public bool IsValid()
        {
            if (!string.IsNullOrWhiteSpace(GetNormalizedBaseUrl()))
            {
                // Player2 local does not require a model name
                return IsPlayer2Local() || !string.IsNullOrWhiteSpace(ModelName);
            }
            return false;
        }

        public string GetNormalizedBaseUrl()
        {
            return ApiConfig.NormalizeUrl(BaseUrl);
        }

        /// <summary>
        /// Detect whether BaseUrl points to a local Player2 app (localhost:4315).
        /// </summary>
        public bool IsPlayer2Local()
        {
            return Player2Endpoints.IsLocalAppUrl(GetNormalizedBaseUrl());
        }
    }
}
