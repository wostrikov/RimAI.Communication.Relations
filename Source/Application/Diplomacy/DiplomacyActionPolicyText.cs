using System;

namespace Ustas.RimAI.Communication.Relations.UI;

internal static class DiplomacyActionPolicyText
{
    internal static readonly string[] AmbiguousFollowupHints =
    {
        "надішли ще раз", "надіслати ще", "надсилаю запит", "досі не отримав", "не отримав", "ще раз", "підганяю",
        "send request", "resend", "still not received", "not received", "send it again"
    };

    internal static readonly string[] ConfirmationHints =
    {
        "підтвердити", "так", "є", "добре", "чинити", "саме це", "оформити", "надсилай", "надсилай",
        "yes", "confirm", "do it", "go ahead", "place it", "submit it"
    };

    internal static readonly string[] CancellationHints =
    {
        "скасувати", "облишмо", "не треба", "не треба", "не хочу", "не надсилай", "не потрібно",
        "cancel", "stop", "no need", "never mind"
    };

    internal static readonly string[] AirdropSelectionRejectionHints =
    {
        "не те", "неправильно", "не це", "заміни", "дай інше", "щось інше", "інший вид",
        "я не це хотів", "не хочу цього", "не треба цього",
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
