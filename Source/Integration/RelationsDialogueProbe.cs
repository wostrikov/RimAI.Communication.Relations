using System;
using System.Collections.Generic;
using RimWorld;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Dialogue;
using Ustas.RimAI.Communication.Relations.UI;
using Ustas.RimAI.Core.Diagnostics;
using Ustas.RimAI.Core.TestDriver;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Integration;

/// <summary>
/// Live dialogue observation through the real window coordinator and
/// RPG/diplomacy session objects. Never calls a paid provider.
/// </summary>
public static class RelationsDialogueProbe
{
    public static TestDriverProgress Run(TestDriverRequest request, string correlationId)
    {
        var mode = request.Arguments.GetString("mode");
        if (string.Equals(mode, "dialogue_rpg", StringComparison.OrdinalIgnoreCase))
            return ProbeRpg(correlationId);
        if (string.Equals(mode, "dialogue_diplomacy", StringComparison.OrdinalIgnoreCase))
            return ProbeDiplomacy(correlationId);
        return TestDriverProgress.Failed("mode must be dialogue_rpg or dialogue_diplomacy");
    }

    static TestDriverProgress ProbeRpg(string correlationId)
    {
        if (!TryPickColonyPair(out var initiator, out var target, out var spawnedTemp, out var pickReason))
            return TestDriverProgress.Failed(pickReason);

        var before = CapturePawn(initiator, target);
        string openReason = null;
        bool opened = false;
        bool moodApplied = false;
        bool romanceInvoked = false;
        bool romanceOk = false;
        bool recruitInvoked = false;
        bool recruitOk = false;
        string firstError = null;
        int exceptionCount = 0;
        string displayed = null;
        Dialog_RPGPawnDialogue window = null;

        try
        {
            opened = DialogueWindowCoordinator.TryOpen(
                DialogueOpenIntent.CreateRpg(
                    initiator,
                    target,
                    initiator.Map,
                    "Probe opening. No provider call."),
                out openReason);
            if (opened)
                window = FindWindow<Dialog_RPGPawnDialogue>();
            if (window == null)
                return CompletedRpg(
                    correlationId, initiator, target, before, CapturePawn(initiator, target),
                    opened, openReason, false, false, false, false, false,
                    null, 1, "window_missing", "RPG window did not appear");

            displayed = window.currentDialogueText;
            moodApplied = window.ExecuteRpgAction(new LLMRpgApiResponse.ApiAction
            {
                action = "TryGainMemory",
                defName = "KindWordsMood",
                reason = "probe_relations:" + correlationId
            });
            romanceInvoked = true;
            romanceOk = window.ExecuteRpgAction(new LLMRpgApiResponse.ApiAction
            {
                action = "RomanceAttempt",
                reason = "probe_relations:" + correlationId
            });
            recruitInvoked = true;
            recruitOk = window.ExecuteRpgAction(new LLMRpgApiResponse.ApiAction
            {
                action = "Recruit",
                reason = "probe_relations:" + correlationId
            });
        }
        catch (NullReferenceException ex)
        {
            exceptionCount++;
            firstError = ex.GetType().Name + ": " + ex.Message;
            RimAiLog.Error(RimAiLogCategory.Relations, "[RimAI.Relations] dialogue_rpg NRE: " + ex);
        }
        catch (InvalidOperationException ex)
        {
            exceptionCount++;
            firstError = ex.GetType().Name + ": " + ex.Message;
            RimAiLog.Error(RimAiLogCategory.Relations, "[RimAI.Relations] dialogue_rpg invalid: " + ex);
        }
        catch (ArgumentException ex)
        {
            exceptionCount++;
            firstError = ex.GetType().Name + ": " + ex.Message;
            RimAiLog.Error(RimAiLogCategory.Relations, "[RimAI.Relations] dialogue_rpg argument: " + ex);
        }
        finally
        {
            window?.Close(doCloseSound: false);
            if (spawnedTemp != null && !spawnedTemp.Destroyed)
                spawnedTemp.Destroy();
        }

        return CompletedRpg(
            correlationId, initiator, target, before, CapturePawn(initiator, target),
            opened, openReason, moodApplied, romanceInvoked, romanceOk, recruitInvoked, recruitOk,
            displayed, exceptionCount, firstError, null);
    }

    static TestDriverProgress ProbeDiplomacy(string correlationId)
    {
        if (!TryPickDiplomacyPair(out var faction, out var negotiator, out var pickReason))
            return TestDriverProgress.Failed(pickReason);

        string openReason = null;
        bool opened = false;
        string firstError = null;
        int exceptionCount = 0;
        Dialog_DiplomacyDialogue window = null;
        var context = new TestDriverJsonWriter();

        try
        {
            opened = DialogueWindowCoordinator.TryOpen(
                DialogueOpenIntent.CreateDiplomacy(faction, negotiator, negotiator?.Map, muteOpenSound: true),
                out openReason);
            if (opened)
                window = FindWindow<Dialog_DiplomacyDialogue>();
            if (window == null)
            {
                return TestDriverProgress.Completed(BaseDiplomacy(correlationId, faction, negotiator, opened, openReason)
                    .Integer("exceptionCount", 1)
                    .Flag("EXCEPTION_PRESENT", true)
                    .Text("firstError", "diplomacy window did not appear")
                    .Flag("windowPresent", false)
                    .Object("leaderContext", context));
            }

            var leader = faction.leader;
            context
                .Text("factionName", faction.Name)
                .Text("leaderName", leader?.Name?.ToStringFull ?? leader?.LabelShortCap)
                .Flag("leaderPresent", leader != null)
                .Text("leaderTitle", faction.def?.leaderTitle)
                .Integer("goodwill", faction.PlayerGoodwill)
                .Text("relationKind", Faction.OfPlayer == null ? "none" : faction.RelationKindWith(Faction.OfPlayer).ToString())
                .Text("techLevel", faction.def?.techLevel.ToString())
                .Text("ideo", faction.ideos?.PrimaryIdeo?.name)
                .Text("negotiator", negotiator?.LabelShort)
                .Integer("sessionMessages", window.session?.messages?.Count ?? 0)
                .Text("windowType", window.GetType().Name);
        }
        catch (NullReferenceException ex)
        {
            exceptionCount++;
            firstError = ex.GetType().Name + ": " + ex.Message;
            RimAiLog.Error(RimAiLogCategory.Relations, "[RimAI.Relations] dialogue_diplomacy NRE: " + ex);
        }
        catch (InvalidOperationException ex)
        {
            exceptionCount++;
            firstError = ex.GetType().Name + ": " + ex.Message;
            RimAiLog.Error(RimAiLogCategory.Relations, "[RimAI.Relations] dialogue_diplomacy invalid: " + ex);
        }
        catch (ArgumentException ex)
        {
            exceptionCount++;
            firstError = ex.GetType().Name + ": " + ex.Message;
            RimAiLog.Error(RimAiLogCategory.Relations, "[RimAI.Relations] dialogue_diplomacy argument: " + ex);
        }
        finally
        {
            window?.Close(doCloseSound: false);
        }

        return TestDriverProgress.Completed(BaseDiplomacy(correlationId, faction, negotiator, opened, openReason)
            .Integer("exceptionCount", exceptionCount)
            .Flag("EXCEPTION_PRESENT", exceptionCount > 0)
            .Text("firstError", firstError)
            .Flag("windowPresent", window != null)
            .Object("leaderContext", context));
    }

    static TestDriverProgress CompletedRpg(
        string correlationId,
        Pawn initiator,
        Pawn target,
        TestDriverJsonWriter before,
        TestDriverJsonWriter after,
        bool opened,
        string openReason,
        bool moodApplied,
        bool romanceInvoked,
        bool romanceOk,
        bool recruitInvoked,
        bool recruitOk,
        string displayed,
        int exceptionCount,
        string firstError,
        string failHint)
    {
        return TestDriverProgress.Completed(new TestDriverJsonWriter()
            .Text("mode", "dialogue_rpg")
            .Text("correlationId", correlationId)
            .Text("initiator", initiator?.LabelShort)
            .Text("target", target?.LabelShort)
            .Flag("windowOpened", opened)
            .Text("openReason", openReason)
            .Text("displayedText", displayed)
            .Flag("moodApplied", moodApplied)
            .Flag("romanceInvoked", romanceInvoked)
            .Flag("romanceApplied", romanceOk)
            .Flag("recruitInvoked", recruitInvoked)
            .Flag("recruitApplied", recruitOk)
            .Flag("consequencesObserved", moodApplied || romanceOk || recruitOk || romanceInvoked || recruitInvoked)
            .Integer("exceptionCount", exceptionCount)
            .Flag("EXCEPTION_PRESENT", exceptionCount > 0)
            .Text("firstError", firstError ?? failHint)
            .Flag("paused", Find.TickManager?.Paused ?? true)
            .Integer("ticksGame", Find.TickManager?.TicksGame ?? 0)
            .Object("before", before)
            .Object("after", after));
    }

    static TestDriverJsonWriter BaseDiplomacy(
        string correlationId,
        Faction faction,
        Pawn negotiator,
        bool opened,
        string openReason)
    {
        return new TestDriverJsonWriter()
            .Text("mode", "dialogue_diplomacy")
            .Text("correlationId", correlationId)
            .Text("faction", faction?.Name)
            .Text("negotiator", negotiator?.LabelShort)
            .Flag("windowOpened", opened)
            .Text("openReason", openReason)
            .Flag("paused", Find.TickManager?.Paused ?? true)
            .Integer("ticksGame", Find.TickManager?.TicksGame ?? 0);
    }

    static TestDriverJsonWriter CapturePawn(Pawn initiator, Pawn target)
    {
        var memories = target?.needs?.mood?.thoughts?.memories?.Memories;
        bool lovers = initiator != null && target != null &&
                      initiator.relations != null &&
                      initiator.relations.DirectRelationExists(PawnRelationDefOf.Lover, target);
        return new TestDriverJsonWriter()
            .Integer("moodMillis", (int)Math.Round((target?.needs?.mood?.CurLevel ?? 0f) * 1000f))
            .Integer("thoughtCount", memories?.Count ?? 0)
            .Flag("lovers", lovers)
            .Flag("recruitable", target != null && target.guest != null && !target.IsColonist);
    }

    static bool TryPickColonyPair(out Pawn initiator, out Pawn target, out Pawn spawnedTemp, out string reason)
    {
        initiator = null;
        target = null;
        spawnedTemp = null;
        reason = null;
        var map = Find.CurrentMap;
        var colonists = map?.mapPawns?.FreeColonistsSpawned;
        if (colonists == null || colonists.Count == 0)
        {
            reason = "need a spawned free colonist initiator";
            return false;
        }

        for (int i = 0; i < colonists.Count; i++)
        {
            var first = colonists[i];
            if (first == null || first.Dead || first.needs?.mood == null)
                continue;

            for (int j = 0; j < colonists.Count; j++)
            {
                var second = colonists[j];
                if (second == null || second == first || second.Dead || second.needs?.mood == null)
                    continue;
                initiator = first;
                target = second;
                return true;
            }

            var all = map.mapPawns?.AllPawnsSpawned;
            if (all == null)
                continue;
            for (int k = 0; k < all.Count; k++)
            {
                var other = all[k];
                if (other == null || other == first || other.Dead)
                    continue;
                if (other.RaceProps?.Humanlike != true || other.needs?.mood == null)
                    continue;
                initiator = first;
                target = other;
                return true;
            }

            var generated = TrySpawnTemporaryPartner(first);
            if (generated != null)
            {
                initiator = first;
                target = generated;
                spawnedTemp = generated;
                return true;
            }
        }

        reason = "no mood-capable humanlike dialogue pair";
        return false;
    }

    static Pawn TrySpawnTemporaryPartner(Pawn initiator)
    {
        var map = initiator?.Map;
        var kind = PawnKindDefOf.Colonist ?? PawnKindDefOf.Villager;
        if (map == null || kind == null || Faction.OfPlayer == null)
            return null;

        var pawn = PawnGenerator.GeneratePawn(
            new PawnGenerationRequest(kind, Faction.OfPlayer, PawnGenerationContext.NonPlayer, -1, true));
        if (pawn == null)
            return null;

        var cell = initiator.Position;
        if (CellFinder.TryFindRandomCellNear(initiator.Position, map, 6, candidate => candidate.Standable(map), out var near))
            cell = near;
        GenSpawn.Spawn(pawn, cell, map);
        return pawn;
    }

    static bool TryPickDiplomacyPair(out Faction faction, out Pawn negotiator, out string reason)
    {
        faction = null;
        negotiator = null;
        reason = null;
        var map = Find.CurrentMap;
        negotiator = map?.mapPawns?.FreeColonistsSpawned?.Find(pawn => pawn is { Dead: false });
        if (negotiator == null)
        {
            reason = "no spawned free colonist negotiator";
            return false;
        }

        var factions = Find.FactionManager?.AllFactionsListForReading;
        if (factions == null)
        {
            reason = "no factions";
            return false;
        }

        foreach (var item in factions)
        {
            if (item == null || item.IsPlayer || item.defeated || item.def is { hidden: true })
                continue;
            if (item.leader == null || !item.HasGoodwill)
                continue;
            faction = item;
            return true;
        }

        reason = "no faction with a leader and goodwill";
        return false;
    }

    static T FindWindow<T>() where T : Window
    {
        var windows = Find.WindowStack?.Windows;
        if (windows == null)
            return null;
        for (int i = 0; i < windows.Count; i++)
        {
            if (windows[i] is T typed)
                return typed;
        }

        return null;
    }
}
