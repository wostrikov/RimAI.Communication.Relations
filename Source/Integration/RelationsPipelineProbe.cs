using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimAI.Core.Application.Diplomacy;
using RimWorld;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Core.Relations;
using Ustas.RimAI.Core.TestDriver;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Integration;

/// <summary>
/// Deterministic TestDriver fixture for the Relations post-provider path.
/// Never calls a paid provider. Execution is opt-in and still goes through
/// the catalog, then <see cref="IRelationsApplication.ExecuteAction"/>.
/// </summary>
public static class RelationsPipelineProbe
{
    public static void Register()
    {
        TestDriverModuleOperations.Register(
            TestDriverCommandNames.ProbeRelations,
            (request, _) => new TestDriverDelegateOperation(() => Run(request)));
    }

    static TestDriverProgress Run(TestDriverRequest request)
    {
        if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null)
            return TestDriverProgress.Failed("probe_relations requires a loaded game");

        var mode = request.Arguments.GetString("mode");
        var correlationId = request.Arguments.GetString("correlationId", request.RequestId);
        if (string.Equals(mode, "provider_response", StringComparison.OrdinalIgnoreCase))
            return ProbeProviderResponse(request, correlationId);
        if (string.Equals(mode, "execute", StringComparison.OrdinalIgnoreCase))
            return ProbeExecute(request, correlationId);
        return TestDriverProgress.Failed("mode must be provider_response or execute");
    }

    static TestDriverProgress ProbeProviderResponse(TestDriverRequest request, string correlationId)
    {
        var raw = request.Arguments.GetString("raw");
        var channelName = request.Arguments.GetString("channel", "diplomacy");
        var execute = request.Arguments.GetBool("execute", false);
        var channel = string.Equals(channelName, "rpg", StringComparison.OrdinalIgnoreCase)
            ? DialogueUsageChannel.Rpg
            : DialogueUsageChannel.Diplomacy;
        var debugSource = channel == DialogueUsageChannel.Rpg
            ? AIRequestDebugSource.RpgDialogue
            : AIRequestDebugSource.DiplomacyDialogue;

        var before = WorldFingerprint.Capture();
        var processed = ProcessProviderFixture(raw, debugSource, channel, out var contractStatus, out var contractReason);
        var accepted = new List<string>();
        var droppedUnknown = new List<string>();
        if (channel == DialogueUsageChannel.Diplomacy)
        {
            CollectDiplomacyActions(raw, processed, accepted, droppedUnknown);
        }
        else
        {
            CollectRpgActions(raw, processed, accepted, droppedUnknown);
        }

        var executorInvoked = false;
        string outcome = null;
        string resultCode = null;
        if (execute && channel == DialogueUsageChannel.Diplomacy && accepted.Count > 0)
        {
            var first = DiplomacyActionParser.ParseActionsFromJson(processed);
            if (first.Count == 0)
                first = DiplomacyActionParser.ParseActionsFromJson(raw);
            var executed = ExecuteAccepted(first, correlationId, out outcome, out resultCode);
            executorInvoked = executed;
        }

        var after = WorldFingerprint.Capture();
        var mutated = !string.Equals(before, after, StringComparison.Ordinal);
        return TestDriverProgress.Completed(new TestDriverJsonWriter()
            .Text("mode", "provider_response")
            .Text("correlationId", correlationId)
            .Text("channel", channel == DialogueUsageChannel.Rpg ? "rpg" : "diplomacy")
            .Text("contractStatus", contractStatus)
            .Text("contractReason", contractReason)
            .Integer("acceptedCount", accepted.Count)
            .Integer("droppedUnknownCount", droppedUnknown.Count)
            .TextArray("accepted", accepted)
            .TextArray("droppedUnknown", droppedUnknown)
            .Flag("mayExecute", accepted.Count > 0)
            .Flag("executorInvoked", executorInvoked)
            .Flag("worldMutated", mutated)
            .Text("outcome", outcome)
            .Text("resultCode", resultCode)
            .Text("fingerprintBefore", before)
            .Text("fingerprintAfter", after)
            .Flag("paused", Find.TickManager?.Paused ?? true)
            .Integer("ticksGame", Find.TickManager?.TicksGame ?? 0));
    }

    static TestDriverProgress ProbeExecute(TestDriverRequest request, string correlationId)
    {
        var action = request.Arguments.GetString("action");
        if (string.IsNullOrWhiteSpace(action))
            return TestDriverProgress.Failed("execute mode requires action");

        var normalized = DiplomacyActionCatalog.NormalizeActionName(action);
        var family = DiplomacyOutcomeFamilies.FamilyForAction(normalized);
        var recognized = DiplomacyActionCatalog.IsValidAction(normalized);
        var before = WorldFingerprint.Capture();
        var executorInvoked = false;
        string outcome = "rejected";
        string resultCode = recognized ? null : "UnknownAction";

        if (recognized)
        {
            var parameters = new Dictionary<string, object>(StringComparer.Ordinal);
            CopyKnownParameters(request, normalized, parameters);
            var executed = ExecuteAccepted(
                new List<AIAction>
                {
                    new AIAction
                    {
                        ActionType = normalized,
                        Parameters = parameters
                    }
                },
                correlationId,
                out outcome,
                out resultCode);
            executorInvoked = executed;
        }

        var after = WorldFingerprint.Capture();
        return TestDriverProgress.Completed(new TestDriverJsonWriter()
            .Text("mode", "execute")
            .Text("correlationId", correlationId)
            .Text("action", normalized)
            .Text("family", family)
            .Flag("recognized", recognized)
            .Flag("mayExecute", recognized)
            .Flag("executorInvoked", executorInvoked)
            .Flag("worldMutated", !string.Equals(before, after, StringComparison.Ordinal))
            .Text("outcome", outcome)
            .Text("resultCode", resultCode)
            .Text("fingerprintBefore", before)
            .Text("fingerprintAfter", after)
            .Flag("paused", Find.TickManager?.Paused ?? true)
            .Integer("ticksGame", Find.TickManager?.TicksGame ?? 0));
    }

    static string ProcessProviderFixture(
        string raw,
        AIRequestDebugSource debugSource,
        DialogueUsageChannel channel,
        out string contractStatus,
        out string contractReason)
    {
        var attemptMessages = new List<ChatMessageData>();
        var parseRetry = RelationsSemanticRetry.MaxParseRetryCount;
        var immersionRetry = RelationsSemanticRetry.MaxImmersionRetryCount;
        var integrityRetry = RelationsSemanticRetry.MaxTextIntegrityRetryCount;
        var contractRetry = channel == DialogueUsageChannel.Rpg
            ? RelationsSemanticRetry.MaxRpgContractRetryCount
            : RelationsSemanticRetry.MaxDiplomacyContractRetryCount;
        contractStatus = string.Empty;
        contractReason = string.Empty;
        RelationsDomainSuccessPipeline.Process(
            raw ?? string.Empty,
            debugSource,
            channel,
            ref attemptMessages,
            ref parseRetry,
            ref immersionRetry,
            ref integrityRetry,
            ref contractRetry,
            ref contractStatus,
            ref contractReason,
            out var processed);
        return processed ?? string.Empty;
    }

    static void CollectDiplomacyActions(string raw, string processed, List<string> accepted, List<string> droppedUnknown)
    {
        CollectDiplomacyFrom(processed, accepted, droppedUnknown);
        if (accepted.Count == 0 && droppedUnknown.Count == 0)
            CollectDiplomacyFrom(raw, accepted, droppedUnknown);
    }

    static void CollectDiplomacyFrom(string payload, List<string> accepted, List<string> droppedUnknown)
    {
        var parsed = DiplomacyActionParser.ParseActionsFromJson(payload);
        foreach (var action in parsed)
        {
            if (action?.ActionType == null)
                continue;
            if (!accepted.Contains(action.ActionType))
                accepted.Add(action.ActionType);
        }

        foreach (var candidate in ExtractActionNames(payload))
        {
            var normalized = DiplomacyActionCatalog.NormalizeActionName(candidate);
            if (DiplomacyActionCatalog.IsValidAction(normalized))
                continue;
            if (string.IsNullOrWhiteSpace(normalized) || normalized == "none")
                continue;
            if (!droppedUnknown.Contains(normalized))
                droppedUnknown.Add(normalized);
        }
    }

    static void CollectRpgActions(string raw, string processed, List<string> accepted, List<string> droppedUnknown)
    {
        var parsed = RpgActionParser.ParseActionsFromJson(ExtractActionsArray(processed) ?? ExtractActionsArray(raw) ?? processed);
        foreach (var action in parsed)
        {
            if (string.IsNullOrWhiteSpace(action?.action) || accepted.Contains(action.action))
                continue;
            accepted.Add(action.action);
        }

        foreach (var candidate in ExtractActionNames(processed) .Concat(ExtractActionNames(raw)))
        {
            if (RpgActionCatalog.IsValidAction(candidate))
                continue;
            var normalized = RpgActionParser.NormalizeActionName(candidate);
            if (string.IsNullOrWhiteSpace(normalized) || droppedUnknown.Contains(normalized))
                continue;
            droppedUnknown.Add(normalized);
        }
    }

    static bool ExecuteAccepted(List<AIAction> actions, string correlationId, out string outcome, out string resultCode)
    {
        outcome = "no_action";
        resultCode = null;
        var relations = RelationsApplicationAccess.Current;
        if (relations == null)
        {
            outcome = "adapter_missing";
            resultCode = "AdapterMissing";
            return false;
        }

        var invoked = false;
        foreach (var action in actions)
        {
            if (action == null || !DiplomacyActionCatalog.IsValidAction(action.ActionType))
                continue;
            var faction = PickFaction(action.ActionType);
            if (faction == null)
            {
                outcome = "no_faction";
                resultCode = "FactionUnavailable";
                return invoked;
            }

            var parameters = action.Parameters ?? new Dictionary<string, object>(StringComparer.Ordinal);
            if (!parameters.ContainsKey("reason"))
                parameters["reason"] = "probe_relations:" + correlationId;
            var result = relations.ExecuteAction(action.ActionType, faction, parameters, false);
            invoked = true;
            outcome = result.Success ? "success" : "denied";
            resultCode = result.Success ? "Ok" : result.Message;
        }

        return invoked;
    }

    static void CopyKnownParameters(TestDriverRequest request, string action, Dictionary<string, object> parameters)
    {
        var amount = request.Arguments.GetString("amount");
        if (!string.IsNullOrWhiteSpace(amount))
            parameters["amount"] = amount;
        var reason = request.Arguments.GetString("reason");
        if (!string.IsNullOrWhiteSpace(reason))
            parameters["reason"] = reason;
        var infoType = request.Arguments.GetString("info_type");
        if (!string.IsNullOrWhiteSpace(infoType))
            parameters["info_type"] = infoType;
        var quest = request.Arguments.GetString("questDefName");
        if (!string.IsNullOrWhiteSpace(quest))
            parameters["questDefName"] = quest;
        var aidType = request.Arguments.GetString("type");
        if (!string.IsNullOrWhiteSpace(aidType))
            parameters["type"] = aidType;
        if (string.Equals(action, AIActionNames.AdjustGoodwill, StringComparison.Ordinal) && !parameters.ContainsKey("amount"))
            parameters["amount"] = "1";
        if (string.Equals(action, AIActionNames.RequestAid, StringComparison.Ordinal) && !parameters.ContainsKey("type"))
            parameters["type"] = "military";
        if (string.Equals(action, AIActionNames.CreateQuest, StringComparison.Ordinal) && !parameters.ContainsKey("questDefName"))
            parameters["questDefName"] = "TradeRequest";
        if (string.Equals(action, AIActionNames.RequestInfo, StringComparison.Ordinal) && !parameters.ContainsKey("info_type"))
            parameters["info_type"] = "prisoner";
    }

    static Faction PickFaction(string action)
    {
        var factions = Find.FactionManager?.AllFactionsListForReading;
        if (factions == null)
            return null;

        var family = DiplomacyOutcomeFamilies.FamilyForAction(action);
        foreach (var faction in factions)
        {
            if (faction == null || faction.IsPlayer || faction.defeated || faction.def is { hidden: true })
                continue;
            if (!faction.HasGoodwill && family != null)
                continue;
            var relation = Faction.OfPlayer == null ? FactionRelationKind.Neutral : faction.RelationKindWith(Faction.OfPlayer);
            if (family == DiplomacyOutcomeFamilies.Raid || family == DiplomacyOutcomeFamilies.Peace)
            {
                if (relation == FactionRelationKind.Hostile)
                    return faction;
                continue;
            }

            if (family == DiplomacyOutcomeFamilies.Aid || family == DiplomacyOutcomeFamilies.Alliance)
            {
                if (relation == FactionRelationKind.Ally)
                    return faction;
                continue;
            }

            if (family == DiplomacyOutcomeFamilies.War && relation == FactionRelationKind.Hostile)
                continue;
            return faction;
        }

        return factions.FirstOrDefault(item => item is { IsPlayer: false, defeated: false });
    }

    static IEnumerable<string> ExtractActionNames(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            yield break;
        var cursor = 0;
        while (cursor < payload.Length)
        {
            var key = payload.IndexOf("\"action\"", cursor, StringComparison.OrdinalIgnoreCase);
            if (key < 0)
                yield break;
            var colon = payload.IndexOf(':', key + 8);
            if (colon < 0)
                yield break;
            var start = payload.IndexOf('"', colon + 1);
            if (start < 0)
                yield break;
            var end = payload.IndexOf('"', start + 1);
            if (end < 0)
                yield break;
            yield return payload.Substring(start + 1, end - start - 1);
            cursor = end + 1;
        }
    }

    static string ExtractActionsArray(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return null;
        return JsonLooseObjectParser.ExtractJsonArray(payload, "actions");
    }

    static class WorldFingerprint
    {
        public static string Capture()
        {
            var builder = new StringBuilder();
            var factions = Find.FactionManager?.AllFactionsListForReading;
            if (factions != null)
            {
                foreach (var faction in factions.OrderBy(item => item?.loadID ?? 0))
                {
                    if (faction == null || faction.IsPlayer)
                        continue;
                    builder.Append(faction.loadID)
                        .Append(':')
                        .Append(faction.PlayerGoodwill)
                        .Append(':')
                        .Append(Faction.OfPlayer == null ? "none" : faction.RelationKindWith(Faction.OfPlayer))
                        .Append('|');
                }
            }

            builder.Append("letters=").Append(Find.LetterStack?.LettersListForReading?.Count ?? 0);
            return builder.ToString();
        }
    }
}
