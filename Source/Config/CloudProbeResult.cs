using UnityEngine.Networking;

namespace Ustas.RimAI.Communication.Relations.Config;

internal struct CloudProbeResult
{
    public UnityWebRequest.Result Result;
    public long ResponseCode;
    public string Error;

    public bool IsSuccess => Result == UnityWebRequest.Result.Success || ResponseCode == 200;
    public bool HasResponseCode => ResponseCode > 0;
    public bool IsAuthError => ResponseCode == 401 || ResponseCode == 403;
    public bool IsChatFallbackReachable => HasResponseCode && !IsAuthError && ResponseCode != 404;
}
