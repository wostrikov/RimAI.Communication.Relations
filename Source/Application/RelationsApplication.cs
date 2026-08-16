using System;
using System.Collections.Generic;
using RimAI.Core.Application.Diplomacy;
using RimWorld;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Core.Relations;
using Verse;

namespace Ustas.RimAI.Communication.Relations;

public sealed class RelationsApplication : IRelationsApplication
{
    public void AttachHostExecutor(RelationsHostExecute executor)
    {
        RelationsInteractionAdapter.HostExecute = (action, faction, applyFixedCost) =>
        {
            var typed = action as AIAction;
            var parameters = typed?.Parameters ?? new Dictionary<string, object>(StringComparer.Ordinal);
            return executor(typed?.ActionType ?? string.Empty, faction, parameters, applyFixedCost);
        };
    }

    public RelationsActionOutcome ExecuteAction(
        string actionType,
        object faction,
        IReadOnlyDictionary<string, object> parameters,
        bool applyFixedCost)
    {
        if (faction is not Faction resolved)
            return Fail("Faction is unavailable.");

        var action = new AIAction
        {
            ActionType = actionType,
            Parameters = ToMutable(parameters)
        };
        var result = RelationsInteractionAdapter.Execute(action, resolved, applyFixedCost);
        return new RelationsActionOutcome
        {
            Success = result != null && result.IsSuccess,
            Message = result?.Message ?? string.Empty,
            Raw = result
        };
    }

    public bool IsFeatureEnabled(DiplomacyKind kind)
    {
        var settings = RelationsMod.Instance?.InstanceSettings;
        if (settings == null)
            return true;

        return kind switch
        {
            DiplomacyKind.AdjustGoodwill => settings.EnableAIGoodwillAdjustment,
            DiplomacyKind.SendGift => settings.EnableAIGiftSending,
            DiplomacyKind.RequestAid => settings.EnableAIAidRequest,
            DiplomacyKind.DeclareWar => settings.EnableAIWarDeclaration,
            DiplomacyKind.MakePeace => settings.EnableAIPeaceMaking,
            DiplomacyKind.RequestCaravan or DiplomacyKind.RequestVisitor => settings.EnableAITradeCaravan,
            DiplomacyKind.RequestRaid or DiplomacyKind.RequestRaidWaves or DiplomacyKind.RequestRaidCallEveryone =>
                settings.EnableAIRaidRequest,
            DiplomacyKind.RequestItemAirdrop => settings.EnableAIItemAirdrop,
            DiplomacyKind.RequestInformation or DiplomacyKind.PayPrisonerRansom => settings.EnablePrisonerRansom,
            _ => true
        };
    }

    public RelationsActionOutcome InvokeGameInterface(
        string methodName,
        object faction,
        IReadOnlyDictionary<string, object> parameters)
    {
        if (faction is not Faction resolved)
            return Fail("Faction is unavailable.");

        var instance = GameAIInterface.Instance;
        if (instance == null)
            return Fail("GameAIInterface is unavailable.");

        GameAIInterface.APIResult raw = methodName switch
        {
            "PayPrisonerRansom" => instance.PayPrisonerRansom(resolved, ToMutable(parameters)),
            "RequestItemAirdrop" => instance.RequestItemAirdrop(resolved, ToMutable(parameters)),
            _ => null
        };
        if (raw == null)
            return Fail("Unsupported GameAIInterface method.");

        return new RelationsActionOutcome
        {
            Success = raw.Success,
            Message = raw.Message ?? string.Empty,
            Raw = raw
        };
    }

    public RelationsActionOutcome CreateQuest(
        string templateId,
        IReadOnlyDictionary<string, object> parameters)
    {
        var instance = GameAIInterface.Instance;
        if (instance == null)
            return Fail("GameAIInterface is unavailable.");

        var raw = instance.CreateQuest(templateId, ToMutable(parameters));
        return new RelationsActionOutcome
        {
            Success = raw != null && raw.Success,
            Message = raw?.Message ?? string.Empty,
            Raw = raw
        };
    }

    static RelationsActionOutcome Fail(string message) =>
        new() { Success = false, Message = message };

    static Dictionary<string, object> ToMutable(IReadOnlyDictionary<string, object> parameters)
    {
        if (parameters is Dictionary<string, object> existing)
            return existing;
        var copy = new Dictionary<string, object>(StringComparer.Ordinal);
        if (parameters == null)
            return copy;
        foreach (var pair in parameters)
            copy[pair.Key] = pair.Value;
        return copy;
    }
}
