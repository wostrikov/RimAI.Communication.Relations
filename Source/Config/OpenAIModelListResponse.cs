using System;
using UnityEngine.Networking;

namespace Ustas.RimAI.Communication.Relations.Config;

[Serializable]
internal class OpenAIModelListResponse
{
    public OpenAIModelInfo[] data = Array.Empty<OpenAIModelInfo>();
}

[Serializable]
internal class OpenAIModelInfo
{
    public string id = string.Empty;
}
