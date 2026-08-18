using Ustas.RimAI.Communication.Relations.WorldState;
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

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class DiplomacyDialogueStrategyContext : DiplomacyDialogueCollaborator
{
    internal DiplomacyDialogueStrategyContext(Dialog_DiplomacyDialogue owner) : base(owner) { }

internal string BuildStrategyFactPackForPrompt(FactionDialogueSession currentSession, Faction currentFaction)
{
    int social = Owner.Parts.StrategyUi.GetNegotiatorSocialLevel();
    int useLimit = Owner.Parts.StrategyUi.GetStrategyUseLimitBySocial(social);
    int remaining = Math.Max(0, useLimit - (currentSession?.strategyUsesConsumed ?? 0));
    string trait = negotiator?.story?.traits?.allTraits?.FirstOrDefault()?.Label ?? "none";
    float wealth = Find.Maps == null
        ? 0f
        : Find.Maps.Where(m => m.IsPlayerHome).Sum(m => m.wealthWatcher?.WealthTotal ?? 0f);
    string wealthTier = wealth >= 250000f ? "very_high"
        : wealth >= 120000f ? "high"
        : wealth >= 50000f ? "mid"
        : wealth >= 15000f ? "low"
        : "very_low";
    Map map = Find.AnyPlayerHomeMap ?? Find.CurrentMap;
    string mapLabel = map?.Parent?.LabelCap ?? map?.Biome?.LabelCap ?? "Unknown";
    string season = map == null ? "Unknown" : GenLocalDate.Season(map).ToString();
    string weather = map?.weatherManager?.curWeather?.LabelCap ?? "Unknown";
    float outdoorTemp = map?.mapTemperature?.OutdoorTemp ?? 0f;
    int colonists = map?.mapPawns?.FreeColonistsSpawnedCount ?? 0;
    int drafted = map?.mapPawns?.FreeColonistsSpawned?.Count(p => p != null && p.Drafted) ?? 0;
    int hostilesOnMap = map?.mapPawns?.AllPawnsSpawned?.Count(p => p != null && p.HostileTo(Faction.OfPlayer)) ?? 0;
    string relationKind = currentFaction?.RelationKindWith(Faction.OfPlayer).ToString() ?? "Unknown";
    int goodwill = currentFaction?.PlayerGoodwill ?? 0;
    string lastPlayerMessage = currentSession?.messages?
        .LastOrDefault(m => m != null && m.isPlayer && !string.IsNullOrWhiteSpace(m.message))?
        .message ?? string.Empty;

    int aggressiveCount = 0;
    if (currentSession?.messages != null)
    {
        aggressiveCount = currentSession.messages
            .Where(m => m != null && m.isPlayer && !string.IsNullOrWhiteSpace(m.message))
            .Reverse()
            .Take(4)
            .Count(m => ContainsAnyStrategyToken((m.message ?? string.Empty).ToLowerInvariant(),
                "war", "attack", "threat", "kill", "侮辱", "威胁", "进攻", "开战", "袭击"));
    }
    string recentPlayerTone = aggressiveCount > 0 ? "aggressive" : "non_aggressive";
    string playerIntentDigest = TrimPrompt(lastPlayerMessage, 80);

    var sb = new StringBuilder();
    sb.AppendLine("PLAYER-SIDE FACT PACK (use these IDs in reason):");
    sb.AppendLine($"[F1] DiplomaticState goodwill_to_current_counterpart={goodwill}, relation_kind={relationKind}");
    sb.AppendLine($"[F2] NegotiatorSocial={social}, Trait={trait}");
    sb.AppendLine($"[F3] StrategyUses remaining={DiplomacyDialogueStrategyUi.FormatStrategyUseLimit(remaining)}/{DiplomacyDialogueStrategyUi.FormatStrategyUseLimit(useLimit)}");
    sb.AppendLine($"[F4] ColonyWealth={wealth:F0}, Tier={wealthTier}");
    sb.AppendLine($"[F5] RecentPlayerAggressiveTurns(last4)={aggressiveCount}");
    sb.AppendLine($"[F6] Map={mapLabel}, Season={season}, Weather={weather}, TempC={outdoorTemp:F0}");
    sb.AppendLine($"[F7] ColonyStatus colonists={colonists}, drafted={drafted}, hostiles_on_map={hostilesOnMap}");
    sb.AppendLine($"[F8] PlayerRecentTone={recentPlayerTone}");
    sb.AppendLine($"[F9] PlayerLatestIntent={playerIntentDigest}");
    sb.AppendLine("[F10] Constraint: strategy suggestions must stay player-side; do not use counterpart leader profile details.");
    sb.AppendLine("Reason quality bar: reference concrete player-side facts and explain causality.");
    return sb.ToString();
}



internal string BuildStrategyScenarioDossierPrompt(FactionDialogueSession currentSession, Faction currentFaction)
{
    var sb = new StringBuilder();
    sb.AppendLine("=== PLAYER-SIDE STRATEGY SCENARIO DOSSIER ===");
    AppendFactionIdentityContext(sb, currentFaction);
    AppendEnvironmentBackgroundContext(sb);
    AppendRecentSessionBackgroundContext(sb, currentSession);
    AppendMemoryBackgroundContext(sb, currentFaction);
    sb.AppendLine("Hard scope: dossier must remain player-side; exclude counterpart leader identity and faction profile details.");
    sb.AppendLine("Use this dossier to write concrete strategy reasons, not generic descriptions.");
    return sb.ToString();
}



internal void AppendFactionIdentityContext(StringBuilder sb, Faction currentFaction)
{
    if (sb == null)
    {
        return;
    }

    if (currentFaction == null)
    {
        sb.AppendLine("DiplomacyState: counterpart unavailable");
        return;
    }

    string relation = currentFaction.RelationKindWith(Faction.OfPlayer).ToString();
    int goodwill = currentFaction.PlayerGoodwill;
    sb.AppendLine($"DiplomacyState: goodwill_to_current_counterpart={goodwill}, relation_kind={relation}");
    sb.AppendLine("CounterpartIdentity: redacted in strategy materials (player-side context only)");
}



internal void AppendEnvironmentBackgroundContext(StringBuilder sb)
{
    if (sb == null)
    {
        return;
    }

    Map map = Find.AnyPlayerHomeMap ?? Find.CurrentMap;
    if (map == null)
    {
        sb.AppendLine("Environment: map unavailable");
        return;
    }

    string label = map.Parent?.LabelCap ?? map.Biome?.LabelCap ?? $"Map#{map.uniqueID}";
    string season = GenLocalDate.Season(map).ToString();
    int hour = GenLocalDate.HourOfDay(map);
    string weather = map.weatherManager?.curWeather?.LabelCap ?? "Unknown";
    float temp = map.mapTemperature?.OutdoorTemp ?? 0f;
    int colonists = map.mapPawns?.FreeColonistsSpawnedCount ?? 0;
    int hostiles = map.mapPawns?.AllPawnsSpawned?.Count(p => p != null && p.HostileTo(Faction.OfPlayer)) ?? 0;
    sb.AppendLine($"Environment: map={label}, season={season}, hour={hour}, weather={weather}, tempC={temp:F0}");
    sb.AppendLine($"MapTacticalState: colonists={colonists}, hostiles_on_map={hostiles}");
}



internal void AppendRecentSessionBackgroundContext(StringBuilder sb, FactionDialogueSession currentSession)
{
    if (sb == null)
    {
        return;
    }

    if (currentSession?.messages == null || currentSession.messages.Count == 0)
    {
        sb.AppendLine("SessionBackground: no previous messages");
        return;
    }

    int totalTurns = currentSession.messages.Count;
    int playerTurns = currentSession.messages.Count(m => m != null && m.isPlayer);
    int aiTurns = currentSession.messages.Count(m => m != null && !m.isPlayer);
    string lastPlayer = currentSession.messages.LastOrDefault(m => m != null && m.isPlayer)?.message ?? string.Empty;
    sb.AppendLine($"SessionBackground: total_turns={totalTurns}, player_turns={playerTurns}, ai_turns={aiTurns}");
    sb.AppendLine($"LastPlayerMessage: {TrimPrompt(lastPlayer, 120)}");
}



internal void AppendMemoryBackgroundContext(StringBuilder sb, Faction currentFaction)
{
    if (sb == null)
    {
        return;
    }

    sb.AppendLine("MemoryBackground: player-side only (counterpart memory profile excluded)");
    sb.AppendLine("WorldEventBackground: player-side only (counterpart profile excluded)");
}



internal string BuildStrategyMemoryDigest(Faction currentFaction)
{
    if (currentFaction == null)
    {
        return "none";
    }

    FactionLeaderMemory memory = LeaderMemoryManager.Instance?.GetMemory(currentFaction);
    if (memory == null)
    {
        return "none";
    }

    List<string> parts = new List<string>();
    List<SignificantEventMemory> events = (memory.SignificantEvents ?? new List<SignificantEventMemory>())
        .Where(evt => evt != null)
        .OrderByDescending(evt => evt.OccurredTick)
        .Take(2)
        .ToList();
    for (int i = 0; i < events.Count; i++)
    {
        SignificantEventMemory evt = events[i];
        parts.Add($"{evt.EventType}:{TrimPrompt(evt.Description, 40)}");
    }

    CrossChannelSummaryRecord latestSummary = (memory.DiplomacySessionSummaries ?? new List<CrossChannelSummaryRecord>())
        .Where(item => item != null)
        .OrderByDescending(item => item.GameTick)
        .FirstOrDefault();
    if (latestSummary != null && !string.IsNullOrWhiteSpace(latestSummary.SummaryText))
    {
        parts.Add($"summary:{TrimPrompt(latestSummary.SummaryText, 60)}");
    }

    return parts.Count == 0 ? "none" : string.Join(" | ", parts);
}



internal string BuildStrategyWorldEventDigest(Faction currentFaction)
{
    WorldEventLedgerComponent ledger = WorldEventLedgerComponent.Instance;
    if (ledger == null || currentFaction == null)
    {
        return "none";
    }

    List<string> parts = new List<string>();
    List<WorldEventRecord> events = ledger.GetRecentWorldEvents(currentFaction, 2, true, true)
        .Where(record => record != null)
        .Take(2)
        .ToList();
    for (int i = 0; i < events.Count; i++)
    {
        parts.Add(TrimPrompt(events[i].Summary, 48));
    }

    RaidBattleReportRecord raid = ledger.GetRecentRaidBattleReports(currentFaction, 3, true)
        .FirstOrDefault(record => record != null);
    if (raid != null && !string.IsNullOrWhiteSpace(raid.Summary))
    {
        parts.Add($"raid:{TrimPrompt(raid.Summary, 48)}");
    }

    return parts.Count == 0 ? "none" : string.Join(" | ", parts);
}



internal string TrimPrompt(string text, int maxChars)
{
    string value = (text ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
    if (value.Length <= maxChars)
    {
        return value;
    }

    if (maxChars <= 3)
    {
        return value.Substring(0, Math.Max(0, maxChars));
    }

    return value.Substring(0, maxChars - 3) + "...";
}



internal List<string> BuildAttributeBasisPool()
{
    var list = new List<string>();
    int social = Owner.Parts.StrategyUi.GetNegotiatorSocialLevel();
    if (social >= 3)
    {
        list.Add("RimChat_StrategyBasisSocial".Translate());
    }

    if (negotiator?.story?.traits?.allTraits != null && negotiator.story.traits.allTraits.Count > 0)
    {
        list.Add("RimChat_StrategyBasisTrait".Translate(negotiator.story.traits.allTraits[0].Label));
    }

    float wealth = 0f;
    if (Find.Maps != null)
    {
        wealth = Find.Maps.Where(m => m.IsPlayerHome).Sum(m => m.wealthWatcher?.WealthTotal ?? 0f);
    }
    list.Add(wealth >= 120000f
        ? "RimChat_StrategyBasisWealthHigh".Translate()
        : "RimChat_StrategyBasisWealth".Translate());
    list.Add("RimChat_StrategyBasisRecentTone".Translate());
    return list;
}



internal bool IsGenericBasis(string basis)
{
    if (string.IsNullOrWhiteSpace(basis))
    {
        return true;
    }

    string normalized = basis.Trim().ToLowerInvariant();
    return normalized == "综合判断" ||
           normalized == "综合" ||
           normalized == "general" ||
           normalized == "generic" ||
           normalized.Contains("unknown");
}



internal bool HasFactReference(string reason)
{
    if (string.IsNullOrWhiteSpace(reason))
    {
        return false;
    }

    string normalized = reason.ToLowerInvariant();
    for (int i = 1; i <= 11; i++)
    {
        if (normalized.Contains($"[f{i}]"))
        {
            return true;
        }
    }

    return false;
}



internal string BuildStrategyPlayerContextPrompt()
{
    var sb = new StringBuilder();
    sb.AppendLine("=== PLAYER NEGOTIATOR CONTEXT (NOT YOUR IDENTITY) ===");
    sb.AppendLine("Identity guard: You are the target faction representative, not the player negotiator.");
    sb.AppendLine("Never claim you are the negotiator, colony assistant, or any player-colony pawn.");
    AppendNegotiatorContext(sb);
    AppendNegotiatorRoyaltyConstraintContext(sb);
    AppendColonyWealthContext(sb);
    AppendRecentInteractionContext(sb);
    AppendStrategyAvailabilityContext(sb);
    sb.AppendLine("Use the context above as soft hints only; do not treat them as hard thresholds.");
    return sb.ToString();
}



internal void AppendNegotiatorRoyaltyConstraintContext(StringBuilder sb)
{
    var promptService = Ustas.RimAI.Communication.Relations.Persistence.PromptPersistenceService.Instance;
    string pawnProfile = promptService.BuildPlayerPawnContextForPrompt(faction, negotiator);
    if (!string.IsNullOrWhiteSpace(pawnProfile))
    {
        sb.AppendLine(pawnProfile);
    }

    string royaltySummary = promptService.BuildPlayerRoyaltySummaryForPrompt(faction, negotiator);
    if (!string.IsNullOrWhiteSpace(royaltySummary))
    {
        sb.AppendLine(royaltySummary);
    }
}



internal void AppendStrategyAvailabilityContext(StringBuilder sb)
{
    if (session == null)
    {
        return;
    }

    int social = Owner.Parts.StrategyUi.GetNegotiatorSocialLevel();
    int useLimit = Owner.Parts.StrategyUi.GetStrategyUseLimitBySocial(social);
    int remaining = Math.Max(0, useLimit - session.strategyUsesConsumed);
    sb.AppendLine($"Strategy Ability: social={social}, max_uses={DiplomacyDialogueStrategyUi.FormatStrategyUseLimit(useLimit)}, remaining_uses={DiplomacyDialogueStrategyUi.FormatStrategyUseLimit(remaining)}");
    sb.AppendLine("If remaining_uses <= 0, do not include strategy_suggestions.");
    sb.AppendLine("If remaining_uses > 0, prefer compact, attribute-grounded strategy suggestions.");
}



internal void AppendNegotiatorContext(StringBuilder sb)
{
    if (negotiator == null)
    {
        sb.AppendLine("PlayerNegotiator (not you): unavailable");
        return;
    }

    int social = negotiator.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
    string traits = negotiator.story?.traits?.allTraits == null
        ? "none"
        : string.Join(", ", negotiator.story.traits.allTraits.Select(t => t.Label).Take(6));

    sb.AppendLine($"PlayerNegotiator (not you): {negotiator.LabelShort} | Social: {social}");
    sb.AppendLine($"PlayerNegotiator Traits: {traits}");
}



internal void AppendColonyWealthContext(StringBuilder sb)
{
    float wealth = 0f;
    if (Find.Maps != null)
    {
        wealth = Find.Maps.Where(m => m.IsPlayerHome).Sum(m => m.wealthWatcher?.WealthTotal ?? 0f);
    }

    string tier = wealth switch
    {
        >= 250000f => "顶级",
        >= 120000f => "高",
        >= 50000f => "中",
        >= 15000f => "低",
        _ => "极低"
    };
    sb.AppendLine($"Colony Wealth: {wealth:F0} (Tier: {tier})");
}



internal void AppendRecentInteractionContext(StringBuilder sb)
{
    if (session?.messages == null || session.messages.Count == 0)
    {
        sb.AppendLine("Recent Player Interaction: none");
        return;
    }

    var recentPlayers = session.messages
        .Where(m => m != null && m.isPlayer && !string.IsNullOrWhiteSpace(m.message))
        .Reverse()
        .Take(4)
        .Select(m => m.message.Replace("\n", " ").Trim())
        .ToList();

    if (recentPlayers.Count == 0)
    {
        sb.AppendLine("Recent Player Interaction: none");
        return;
    }

    int aggressiveCount = recentPlayers.Count(m => ContainsAnyStrategyToken(m.ToLowerInvariant(),
        "war", "attack", "threat", "kill", "侮辱", "威胁", "进攻", "开战", "袭击"));

    sb.AppendLine($"Recent Player Interaction: {recentPlayers.Count} turns, aggressive={aggressiveCount}");
    sb.AppendLine($"Recent Snippets: {string.Join(" || ", recentPlayers.Select(m => m.Length > 60 ? m.Substring(0, 60) : m))}");
}



internal bool ContainsAnyStrategyToken(string source, params string[] tokens)
{
    if (string.IsNullOrEmpty(source) || tokens == null)
    {
        return false;
    }

    for (int i = 0; i < tokens.Length; i++)
    {
        if (!string.IsNullOrWhiteSpace(tokens[i]) && source.Contains(tokens[i]))
        {
            return true;
        }
    }

    return false;
}



internal bool IsStrategyRequestContextValid(FactionDialogueSession currentSession, Faction currentFaction, int snapshotMessageCount)
{
    if (currentSession == null || currentFaction == null || currentFaction.defeated)
    {
        return false;
    }

    if (currentSession.isWaitingForResponse)
    {
        return false;
    }

    if ((currentSession.messages?.Count ?? 0) != snapshotMessageCount)
    {
        return false;
    }

    FactionDialogueSession liveSession = GameComponent_DiplomacyManager.Instance?.GetSession(currentFaction);
    return ReferenceEquals(liveSession, currentSession);
}
}
