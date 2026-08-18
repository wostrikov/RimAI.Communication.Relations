using System;
using System.Collections.Generic;
using Ustas.RimAI.Communication.Relations.UI;

internal static class DiplomacyDialoguePolicyTests
{
    public static void Run(Action<bool, string> check)
    {
        Hints(check);
        AirdropParse(check);
        ParameterParse(check);
    }

    static void Hints(Action<bool, string> check)
    {
        check(DiplomacyActionPolicyText.ContainsAnyHint("please confirm the order", DiplomacyActionPolicyText.ConfirmationHints), "confirm hint matches english");
        check(DiplomacyActionPolicyText.ContainsAnyHint("确认发送", DiplomacyActionPolicyText.ConfirmationHints), "confirm hint matches chinese");
        check(DiplomacyActionPolicyText.ContainsAnyHint("cancel this", DiplomacyActionPolicyText.CancellationHints), "cancel hint matches");
        check(!DiplomacyActionPolicyText.ContainsAnyHint("hello there", DiplomacyActionPolicyText.ConfirmationHints), "unrelated text is not a confirm hint");
        check(DiplomacyActionPolicyText.ContainsAnyHint("not this item", DiplomacyActionPolicyText.AirdropSelectionRejectionHints), "airdrop rejection hint matches");
        check(!DiplomacyActionPolicyText.ContainsAnyHint("   ", DiplomacyActionPolicyText.AmbiguousFollowupHints), "whitespace is not a hint");
        check(!DiplomacyActionPolicyText.ContainsAnyHint("yes", null), "null hint array is false");
    }

    static void AirdropParse(Action<bool, string> check)
    {
        check(DiplomacyAirdropPendingParse.TryExtractAirdropRequestedCount("need steel x 12", out int structured) && structured == 12, "structured need count");
        check(DiplomacyAirdropPendingParse.TryExtractAirdropRequestedCount("需求 组件 x 8", out int chineseNeed) && chineseNeed == 8, "chinese structured need count");
        check(DiplomacyAirdropPendingParse.TryExtractAirdropRequestedCount("给我20个", out int quantifier) && quantifier == 20, "chinese quantifier count");
        check(!DiplomacyAirdropPendingParse.TryExtractAirdropRequestedCount("1", out _), "option index 1 is not a requested count");
        check(DiplomacyAirdropPendingParse.TryExtractAirdropRequestedCount("send 40", out int loose) && loose == 40, "loose count above option range");
        check(DiplomacyAirdropPendingParse.TryParseChineseChoiceIndex("选二") == 2, "chinese choice index two");
        check(DiplomacyAirdropPendingParse.TryParseChineseChoiceIndex("两") == 2, "chinese choice index liang");
        check(DiplomacyAirdropPendingParse.TryParseChineseChoiceIndex("") == 0, "empty chinese choice is zero");
    }

    static void ParameterParse(Action<bool, string> check)
    {
        var values = new Dictionary<string, object>
        {
            ["offer_silver"] = "1,250",
            ["target_pawn_load_id"] = 42L,
            ["zero"] = 0,
            ["junk"] = "abc"
        };
        check(DiplomacyParameterParse.TryReadPositiveInt(values, "offer_silver", out int silver) && silver == 1250, "comma grouped silver");
        check(DiplomacyParameterParse.TryReadPositiveInt(values, "target_pawn_load_id", out int pawnId) && pawnId == 42, "long pawn id");
        check(!DiplomacyParameterParse.TryReadPositiveInt(values, "zero", out _), "zero is not positive");
        check(!DiplomacyParameterParse.TryReadPositiveInt(values, "junk", out _), "non-numeric fails");
        check(!DiplomacyParameterParse.TryReadPositiveInt(null, "offer_silver", out _), "null map fails");
    }
}
