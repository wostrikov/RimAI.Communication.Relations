using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Context;
using Ustas.RimAI.Communication.Relations.Dialogue;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Communication.Relations.Guards;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.PawnRpgPush;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.UI;

namespace Ustas.RimAI.Communication.Relations.UI
{
    /// <summary>
    /// Faction speaker pawn discovery and generation helpers.
    /// </summary>
    internal static class DiplomacyDialogueSpeakerPawnOps
    {
internal static bool TryGetExistingFactionSpeakerPawn(Faction currentFaction, out Pawn speakerPawn)
{
    speakerPawn = null;
    if (currentFaction == null)
    {
        return false;
    }

    List<Pawn> candidates = PawnsFinder.AllMapsWorldAndTemporary_Alive
        .Where(pawn => IsEligibleSpeakerPawn(pawn, currentFaction))
        .ToList();
    if (candidates.Count == 0)
    {
        return false;
    }

    speakerPawn = candidates.RandomElement();
    return true;
}



internal static bool TryGenerateFactionSpeakerPawn(Faction currentFaction, out Pawn speakerPawn)
{
    speakerPawn = null;
    if (currentFaction == null || currentFaction.defeated)
    {
        return false;
    }

    PawnKindDef kindDef = currentFaction.def?.basicMemberKind ?? ResolveFallbackHumanlikeKind();
    if (kindDef == null)
    {
        return false;
    }

    try
    {
        speakerPawn = GenerateFactionSpeakerPawn(currentFaction, kindDef);
        if (speakerPawn == null)
        {
            return false;
        }
        return true;
    }
    catch (Exception ex)
    {
        Log.Warning($"[RimAI.Relations] Failed to generate fallback diplomacy speaker for faction '{currentFaction.Name}': {ex.Message}");
        return false;
    }
}



internal static Pawn GenerateFactionSpeakerPawn(Faction currentFaction, PawnKindDef kindDef)
{
    var request = new PawnGenerationRequest(kindDef, currentFaction, PawnGenerationContext.NonPlayer, -1, true);
    Pawn generated = PawnGenerator.GeneratePawn(request);
    if (generated == null)
    {
        return null;
    }

    if (generated.Faction != currentFaction)
    {
        generated.SetFaction(currentFaction);
    }

    Find.WorldPawns?.PassToWorld(generated);
    return generated;
}



internal static PawnKindDef ResolveFallbackHumanlikeKind()
{
    return DefDatabase<PawnKindDef>.AllDefsListForReading
        .FirstOrDefault(def => def?.RaceProps?.Humanlike == true);
}



internal static bool IsEligibleSpeakerPawn(Pawn pawn, Faction expectedFaction = null)
{
    if (pawn == null || pawn.Destroyed || pawn.Dead || pawn.RaceProps?.Humanlike != true)
    {
        return false;
    }

    return expectedFaction == null || pawn.Faction == expectedFaction;
}
    }
}
