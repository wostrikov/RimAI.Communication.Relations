using System;

namespace Ustas.RimAI.Communication.Relations.Config;

[Serializable]
internal class GoogleModelListResponse
{
    public GoogleModelInfo[] models = Array.Empty<GoogleModelInfo>();
}

[Serializable]
internal class GoogleModelInfo
{
    public string name = string.Empty;
    public string[] supportedGenerationMethods = Array.Empty<string>();
}
