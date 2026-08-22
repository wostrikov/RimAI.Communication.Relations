using System.Collections.Generic;
using RimAI.Core.Application.Diplomacy;
using Ustas.RimAI.Communication.Relations.AI;

internal static class HallucinationGuardTests
{
    public static void Run(System.Action<bool, string> check)
    {
        UnknownDiplomacyDropped(check);
        MixedPayloadKeepsOnlyRecognized(check);
        MalformedAndEmptyHaveNoActions(check);
        OutcomeFamiliesAreCatalogued(check);
        RpgUnknownDropped(check);
    }

    static void UnknownDiplomacyDropped(System.Action<bool, string> check)
    {
        const string unknown = "{\"actions\":[{\"action\":\"summon_dragon\",\"parameters\":{\"amount\":99}}]}";
        check(DiplomacyActionJsonReader.AcceptedTypes(unknown).Count == 0, "unknown diplomacy action dropped");
        check(DiplomacyActionJsonReader.DroppedUnknownTypes(unknown).Contains("summon_dragon"), "unknown action recorded as dropped");

        const string retired = "{\"actions\":[{\"action\":\"trade\",\"parameters\":{}}]}";
        check(DiplomacyActionJsonReader.AcceptedTypes(retired).Count == 0, "retired trade alias is not a diplomacy action");

        const string image = "{\"actions\":[{\"action\":\"send_image\",\"parameters\":{}}]}";
        check(DiplomacyActionJsonReader.AcceptedTypes(image).Count == 0, "send_image stays outside the catalog");
        check(!DiplomacyActionCatalog.IsValidAction("send_image"), "send_image is not valid");
    }

    static void MixedPayloadKeepsOnlyRecognized(System.Action<bool, string> check)
    {
        const string mixed = "{\"actions\":[" +
            "{\"action\":\"adjust_goodwill\",\"parameters\":{\"amount\":3}}," +
            "{\"action\":\"summon_dragon\",\"parameters\":{}}," +
            "{\"action\":\"reject_request\",\"parameters\":{}}" +
            "]}";
        List<string> accepted = DiplomacyActionJsonReader.AcceptedTypes(mixed);
        List<string> dropped = DiplomacyActionJsonReader.DroppedUnknownTypes(mixed);
        check(accepted.Count == 2, "mixed payload keeps two recognized actions");
        check(accepted.Contains("adjust_goodwill"), "goodwill kept");
        check(accepted.Contains("reject_request"), "reject kept");
        check(dropped.Contains("summon_dragon"), "hallucinated action dropped");
    }

    static void MalformedAndEmptyHaveNoActions(System.Action<bool, string> check)
    {
        check(DiplomacyActionJsonReader.Read("{not json").Count == 0, "invalid json has no actions");
        check(DiplomacyActionJsonReader.Read("").Count == 0, "empty payload has no actions");
        check(DiplomacyActionJsonReader.Read("I will send a raid tomorrow.").Count == 0, "prose has no actions");
        check(DiplomacyActionJsonReader.Read("{\"actions\":[{\"reason\":\"x\"}]}").Count == 0, "missing discriminator dropped");
        check(DiplomacyActionJsonReader.Read("{\"visible_dialogue\":\"The gates stay closed.\"}").Count == 0, "dialogue-only envelope has no actions");
    }

    static void OutcomeFamiliesAreCatalogued(System.Action<bool, string> check)
    {
        foreach (string family in DiplomacyOutcomeFamilies.All)
        {
            string action = DiplomacyOutcomeFamilies.PrimaryAction(family);
            check(DiplomacyActionCatalog.IsValidAction(action), family + " primary action is catalogued");
        }

        check(DiplomacyActionCatalog.IsValidAction("request_raid_waves"), "waves stays recognized");
        check(DiplomacyActionCatalog.IsValidAction("request_raid_call_everyone"), "call_everyone stays recognized");
        check(DiplomacyRaidModes.IsClassifiedSpecialMode("waves"), "waves is a classified special mode");
        check(!DiplomacyRaidModes.IsAutoExecutable("waves"), "waves is not auto-executable");
    }

    static void RpgUnknownDropped(System.Action<bool, string> check)
    {
        check(!RpgActionCatalog.IsValidAction("SummonDragon"), "unknown RPG action rejected");
        check(!RpgActionCatalog.IsValidAction("summon_dragon"), "unknown RPG snake_case rejected");
        check(RpgActionCatalog.IsValidAction("RomanceAttempt"), "recognized RPG action kept");
        check(RpgActionCatalog.IsValidAction("recruit"), "rpg alias recruit");
        check(RpgActionCatalog.NormalizeActionName("romance") == "RomanceAttempt", "rpg romance alias");
    }
}
