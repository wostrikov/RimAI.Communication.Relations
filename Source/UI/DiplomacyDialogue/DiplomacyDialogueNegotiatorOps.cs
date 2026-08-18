using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Ustas.RimAI.Communication.Relations.Config;
using Verse;
using Ustas.RimAI.Communication.Relations.PawnRpgPush;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Communication.Relations.Module;

namespace Ustas.RimAI.Communication.Relations.UI
{
    /// <summary>
    /// Negotiator resolution helpers for diplomacy faction list.
    /// </summary>
    internal static class DiplomacyDialogueNegotiatorOps
    {
internal static bool TryOpenDiplomacyDirectFallback(Faction faction, Pawn negotiator, bool muteOpenSound, string source)
{
    if (Find.WindowStack == null || faction == null || faction.defeated)
    {
        return false;
    }

    Log.Warning($"[RimAI.Relations] Applying direct diplomacy open fallback: source={source}, faction={faction.Name}");
    Find.WindowStack.Add(new Dialog_DiplomacyDialogue(faction, negotiator, muteOpenSound));
    return true;
}



internal static Pawn ResolveAutoNegotiator(Pawn preferredNegotiator)
{
    if (IsValidNegotiator(preferredNegotiator))
    {
        return preferredNegotiator;
    }

    var settings = RelationsMod.Instance?.InstanceSettings;
    var mode = settings?.DiplomacyNegotiatorMode ?? NegotiatorSelectionMode.HighestSocial;

    return mode switch
    {
        NegotiatorSelectionMode.ProtagonistList => ResolveNegotiatorFromProtagonistList(),
        NegotiatorSelectionMode.LastUsed => ResolveLastUsedNegotiator(),
        NegotiatorSelectionMode.Designated => ResolveDesignatedNegotiator(settings),
        _ => ResolveHighestSocialNegotiator()
    };
}



internal static Pawn ResolveHighestSocialNegotiator()
{
    IEnumerable<Map> maps = Find.Maps ?? Enumerable.Empty<Map>();
    foreach (Map map in maps.Where(m => m != null && m.IsPlayerHome))
    {
        if (map.mapPawns?.FreeColonistsSpawned == null)
        {
            continue;
        }

        Pawn best = map.mapPawns.FreeColonistsSpawned
            .Where(IsValidNegotiator)
            .OrderByDescending(p => GetNegotiatorScore(p))
            .FirstOrDefault();
        if (best != null)
        {
            return best;
        }
    }

    foreach (Pawn pawn in PawnsFinder.AllMapsWorldAndTemporary_Alive)
    {
        if (IsValidNegotiator(pawn) && pawn.Faction == Faction.OfPlayer)
        {
            return pawn;
        }
    }

    return null;
}



internal static Pawn ResolveNegotiatorFromProtagonistList()
{
    var manager = GameComponent_PawnRpgDialoguePushManager.Instance;
    if (manager == null) return ResolveHighestSocialNegotiator();

    List<Pawn> protagonists = manager.GetRpgProactiveProtagonists();
    if (protagonists == null || protagonists.Count == 0) return ResolveHighestSocialNegotiator();

    Pawn best = protagonists
        .Where(IsValidNegotiator)
        .OrderByDescending(p => GetNegotiatorScore(p))
        .FirstOrDefault();
    return best ?? ResolveHighestSocialNegotiator();
}



internal static Pawn ResolveLastUsedNegotiator()
{
    var diplomacyManager = GameComponent_DiplomacyManager.Instance;
    if (diplomacyManager == null) return ResolveHighestSocialNegotiator();

    int thingId = diplomacyManager.GetLastNegotiatorThingId();
    if (thingId <= 0) return ResolveHighestSocialNegotiator();

    Pawn pawn = PawnsFinder.AllMapsWorldAndTemporary_Alive
        .FirstOrDefault(p => p != null && p.thingIDNumber == thingId);
    return IsValidNegotiator(pawn) ? pawn : ResolveHighestSocialNegotiator();
}



internal static Pawn ResolveDesignatedNegotiator(RelationsSettings settings)
{
    if (settings == null || settings.DesignatedNegotiatorThingId <= 0)
        return ResolveHighestSocialNegotiator();

    Pawn pawn = PawnsFinder.AllMapsWorldAndTemporary_Alive
        .FirstOrDefault(p => p != null && p.thingIDNumber == settings.DesignatedNegotiatorThingId);
    return IsValidNegotiator(pawn) ? pawn : ResolveHighestSocialNegotiator();
}



internal static bool IsValidNegotiator(Pawn pawn)
{
    return pawn != null
        && !pawn.Dead
        && !pawn.Destroyed
        && pawn.RaceProps?.Humanlike == true
        && pawn.Map != null;
}



internal static int GetNegotiatorScore(Pawn pawn)
{
    int score = 0;
    if (pawn.skills?.GetSkill(SkillDefOf.Social) != null)
    {
        score += pawn.skills.GetSkill(SkillDefOf.Social).Level * 100;
    }

    if (pawn.Drafted)
    {
        score += 50;
    }

    if (pawn.HostileTo(Faction.OfPlayer))
    {
        score -= 1000;
    }

    return score;
}
    }
}
