using System;
using System.IO;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Settings;

internal static class RelationsProactiveTokenTests
{
    public static void Run(Action<bool, string> check)
    {
        check(RelationsTokenAccountingPolicy.ShouldTrack(DialogueUsageChannel.Diplomacy), "token-diplomacy");
        check(RelationsTokenAccountingPolicy.ShouldTrack(DialogueUsageChannel.Rpg), "token-rpg");
        check(!RelationsTokenAccountingPolicy.ShouldTrack(DialogueUsageChannel.Unknown), "token-unknown");
        check(!RelationsTokenAccountingPolicy.ShouldTrack(DialogueUsageChannel.Proactive), "token-proactive");
        check(DialogueTokenUsageTracker.ShouldTrack(DialogueUsageChannel.Diplomacy), "tracker-delegates-player");
        check(!DialogueTokenUsageTracker.ShouldTrack(DialogueUsageChannel.Proactive), "tracker-skips-proactive");

        check(RelationsGoodwillGatePolicy.AllowAid(40, 40), "aid-at-floor");
        check(!RelationsGoodwillGatePolicy.AllowAid(39, 40), "aid-below-floor");
        check(RelationsGoodwillGatePolicy.AllowWarDeclaration(-50, -50), "war-at-ceiling");
        check(!RelationsGoodwillGatePolicy.AllowWarDeclaration(-49, -50), "war-above-ceiling");

        check(!RelationsProactiveEmitPolicy.AllowRegularSweep(false), "proactive-off");
        check(RelationsProactiveEmitPolicy.ClassifyRegular(-40) == RelationsProactiveKind.Skip, "proactive-hostile-skip");
        check(RelationsProactiveEmitPolicy.ClassifyRegular(40) == RelationsProactiveKind.FriendlyDiplomacy, "proactive-friendly");
        check(RelationsProactiveEmitPolicy.ClassifyRegular(0) == RelationsProactiveKind.AmbientSocial, "proactive-ambient");
        check(!RelationsProactiveEmitPolicy.AllowCausalGoodwillShift(9), "causal-below");
        check(RelationsProactiveEmitPolicy.AllowCausalGoodwillShift(10), "causal-at");
        check(RelationsProactiveEmitPolicy.ShouldEmit(true, false, false, true), "emit-yes");
        check(!RelationsProactiveEmitPolicy.ShouldEmit(true, true, false, true), "emit-pending");

        string tracker = Read("DialogueTokenUsageTracker.cs.src");
        string slice1 = Read("NpcDialoguePushSlice1.cs.src");
        string slice2 = Read("NpcDialoguePushSlice2.cs.src");
        string rpg = Read("PawnRpgDialoguePushManagerGeneration.cs.src");
        string rpgUi1 = Read("RPGPawnDialogueSlice1.cs.src");
        string rpgUi2 = Read("RPGPawnDialogueSlice2.cs.src");
        string eligibility = Read("ApiActionEligibilitySlice1.cs.src");
        check(tracker.Contains("RelationsTokenAccountingPolicy.ShouldTrack"), "host-token");
        check(slice1.Contains("RelationsProactiveEmitPolicy.AllowRegularSweep"), "host-regular");
        check(slice1.Contains("RelationsProactiveEmitPolicy.ClassifyRegular"), "host-classify");
        check(slice1.Contains("RelationsProactiveEmitPolicy.AllowCausalGoodwillShift"), "host-causal");
        check(slice2.Contains("DialogueUsageChannel.Proactive"), "host-npc-proactive-channel");
        check(rpg.Contains("DialogueUsageChannel.Proactive"), "host-rpg-proactive-channel");
        check(rpg.Contains("[RIMAI_PAWNRPG_PROACTIVE] outcome=Failed"), "host-rpg-proactive-structured-failure");
        check(!rpgUi1.Contains("currentDialogueText = \"Error: \" + error"), "host-rpg-initial-hides-transport-error");
        check(!rpgUi2.Contains("aiResponseText = \"Error: \" + error"), "host-rpg-reply-hides-transport-error");
        check(rpgUi1.Contains("RimChat_DialogueRequestUnavailable"), "host-rpg-initial-localized-error");
        check(rpgUi2.Contains("RimChat_DialogueRequestUnavailable"), "host-rpg-reply-localized-error");
        check(eligibility.Contains("RelationsGoodwillGatePolicy.AllowAid"), "host-aid-gate");
        check(eligibility.Contains("RelationsGoodwillGatePolicy.AllowWarDeclaration"), "host-war-gate");
    }

    static string Read(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, name);
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }
}
