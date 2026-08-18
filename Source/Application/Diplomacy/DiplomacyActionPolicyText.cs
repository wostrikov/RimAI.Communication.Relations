using System;

namespace Ustas.RimAI.Communication.Relations.UI;

internal static class DiplomacyActionPolicyText
{
    internal static readonly string[] AmbiguousFollowupHints =
    {
        "再发一次", "再发", "发送请求", "还是没收到", "没收到", "再来一次", "催一下",
        "send request", "resend", "still not received", "not received", "send it again"
    };

    internal static readonly string[] ConfirmationHints =
    {
        "确认", "是的", "是", "好", "行", "就这个", "下单", "发送吧", "发吧",
        "yes", "confirm", "do it", "go ahead", "place it", "submit it"
    };

    internal static readonly string[] CancellationHints =
    {
        "取消", "算了", "不用了", "不用", "不要", "别发", "不需要",
        "cancel", "stop", "no need", "never mind"
    };

    internal static readonly string[] AirdropSelectionRejectionHints =
    {
        "不是", "不对", "不是这个", "换一个", "换别的", "其他的", "另一种",
        "不是我想要", "不想要这个", "不要这个",
        "not this", "wrong item", "something else", "different item", "another one"
    };

    internal static bool ContainsAnyHint(string normalizedLowerText, string[] hints)
    {
        if (string.IsNullOrWhiteSpace(normalizedLowerText) || hints == null || hints.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < hints.Length; i++)
        {
            string hint = hints[i];
            if (string.IsNullOrWhiteSpace(hint))
            {
                continue;
            }

            if (normalizedLowerText.Contains(hint.ToLowerInvariant()))
            {
                return true;
            }
        }

        return false;
    }
}
