using System;
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.Guards;
using Ustas.RimAI.Communication.Relations.WorldState;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Ustas.RimAI.Communication.Relations.DiplomacySystem
{
    internal static class SocialNewsSeedSlice3
    {
internal static void AddSummarySeeds(List<SocialNewsSeed> seeds)
        {
            foreach (Faction faction in SocialNewsSeedFactory.GetEligibleSourceFactions())
            {
                FactionLeaderMemory memory = LeaderMemoryManager.Instance?.GetMemory(faction);
                IEnumerable<CrossChannelSummaryRecord> records = memory?.DiplomacySessionSummaries ?? Enumerable.Empty<CrossChannelSummaryRecord>();
                foreach (CrossChannelSummaryRecord record in records)
                {
                    if (record == null || string.IsNullOrWhiteSpace(record.SummaryText) || record.GameTick <= 0)
                    {
                        continue;
                    }

                    seeds.Add(SocialNewsSeedFactory.CreateSummarySeed(faction, record));
                }
            }
        }

internal static SocialNewsSeed CreateSummarySeed(Faction faction, CrossChannelSummaryRecord record)
        {
            SocialPostCategory category = SocialCircleService.InferCategory(record?.SummaryText, record?.Source.ToString());
            int sentiment = SocialCircleService.InferSentiment(record?.SummaryText);
            return new SocialNewsSeed
            {
                OriginType = SocialNewsOriginType.DiplomacySummary,
                OriginKey = $"summary:{record?.Source}:{record?.GameTick}:{record?.ContentHash}",
                SourceFaction = faction,
                TargetFaction = null,
                Category = category,
                Sentiment = sentiment,
                OccurredTick = record?.GameTick ?? 0,
                Summary = record?.SummaryText ?? string.Empty,
                SourceLabel = "RimChat_SocialSourceDiplomacyArchive",
                CredibilityLabel = record?.IsLlmFallback == true
                    ? "RimChat_SocialCredibilityArchiveFallback"
                    : "RimChat_SocialCredibilityArchiveSummary",
                CredibilityValue = record?.IsLlmFallback == true ? 0.55f : 0.68f,
                Facts = SocialNewsSeedFactory.BuildSummaryFacts(record),
                RawText = record?.SummaryText ?? string.Empty
            };
        }

internal static List<string> BuildSummaryFacts(CrossChannelSummaryRecord record)
        {
            Faction sourceFaction = SocialNewsSeedFactory.ResolveFaction(record?.FactionId);
            string location = SocialNewsSeedFactory.ResolveFactionStrongholdLabel(sourceFaction, null);
            return new List<string>
            {
                record?.SummaryText ?? string.Empty,
                $"Source faction: {SocialNewsSeedFactory.BuildFactionFactValue(sourceFaction)}",
                SocialNewsSeedFactory.BuildLocationFact(location),
                SocialNewsSeedFactory.BuildSettlementContextFact(location, sourceFaction, null),
                $"Source pool: {record?.Source.ToString() ?? "Unknown"}",
                $"Confidence: {(record?.Confidence ?? 0f):F2}",
                $"Key facts: {string.Join(" | ", record?.KeyFacts ?? new List<string>())}",
                $"raw_text: {record?.SummaryText ?? string.Empty}"
            };
        }

internal static void AddScheduledEventSeeds(List<SocialNewsSeed> seeds)
        {
            List<ScheduledSocialEventRecord> events = GameComponent_DiplomacyManager.Instance
                ?.GetRecentScheduledSocialEvents(SocialNewsSeedFactory.SeedWindowDays) ?? new List<ScheduledSocialEventRecord>();
            for (int index = 0; index < events.Count; index++)
            {
                SocialNewsSeed seed = SocialNewsSeedFactory.CreateScheduledEventSeed(events[index]);
                if (seed != null)
                {
                    seeds.Add(seed);
                }
            }
        }

internal static SocialNewsSeed CreateScheduledEventSeed(ScheduledSocialEventRecord record)
        {
            if (record == null || record.EventType == ScheduledSocialEventType.Unknown)
            {
                return null;
            }

            return record.EventType switch
            {
                ScheduledSocialEventType.QuestResult => SocialNewsSeedFactory.CreateQuestResultSeed(record),
                ScheduledSocialEventType.TradeDeal => SocialNewsSeedFactory.CreateTradeDealSeed(record),
                ScheduledSocialEventType.GoodwillShift => SocialNewsSeedFactory.CreateGoodwillShiftSeed(record),
                ScheduledSocialEventType.RelationShift => SocialNewsSeedFactory.CreateRelationShiftSeed(record),
                ScheduledSocialEventType.AidArrival => SocialNewsSeedFactory.CreateAidArrivalSeed(record),
                _ => null
            };
        }

internal static SocialNewsSeed CreateQuestResultSeed(ScheduledSocialEventRecord record)
        {
            return SocialNewsSeedFactory.CreateScheduledSeed(
                record,
                SocialNewsOriginType.QuestResult,
                SocialPostCategory.Diplomatic,
                record.Value >= 0 ? 1 : -1,
                "RimChat_SocialSourceQuestResult",
                "RimChat_SocialCredibilityPublicReport",
                0.86f);
        }

internal static SocialNewsSeed CreateTradeDealSeed(ScheduledSocialEventRecord record)
        {
            int sentiment = record.Value >= 0 ? 1 : -1;
            return SocialNewsSeedFactory.CreateScheduledSeed(
                record,
                SocialNewsOriginType.TradeDeal,
                SocialPostCategory.Economic,
                sentiment,
                "RimChat_SocialSourceTradeDeal",
                "RimChat_SocialCredibilityPublicReport",
                0.82f);
        }

internal static SocialNewsSeed CreateGoodwillShiftSeed(ScheduledSocialEventRecord record)
        {
            int sentiment = record.Value > 0 ? 2 : -2;
            return SocialNewsSeedFactory.CreateScheduledSeed(
                record,
                SocialNewsOriginType.GoodwillShift,
                SocialPostCategory.Diplomatic,
                sentiment,
                "RimChat_SocialSourceGoodwillShift",
                "RimChat_SocialCredibilityObserverNote",
                0.78f);
        }

internal static SocialNewsSeed CreateRelationShiftSeed(ScheduledSocialEventRecord record)
        {
            bool hostile = record.Value < 0 || (record.Detail?.Contains("Hostile") ?? false);
            return SocialNewsSeedFactory.CreateScheduledSeed(
                record,
                SocialNewsOriginType.RelationShift,
                hostile ? SocialPostCategory.Military : SocialPostCategory.Diplomatic,
                hostile ? -2 : 2,
                "RimChat_SocialSourceRelationShift",
                "RimChat_SocialCredibilityObserverNote",
                0.84f);
        }

internal static SocialNewsSeed CreateAidArrivalSeed(ScheduledSocialEventRecord record)
        {
            return SocialNewsSeedFactory.CreateScheduledSeed(
                record,
                SocialNewsOriginType.AidArrival,
                SocialPostCategory.Economic,
                2,
                "RimChat_SocialSourceAidArrival",
                "RimChat_SocialCredibilityPublicReport",
                0.88f);
        }

internal static SocialNewsSeed CreateScheduledSeed(
            ScheduledSocialEventRecord record,
            SocialNewsOriginType originType,
            SocialPostCategory category,
            int sentiment,
            string sourceLabel,
            string credibilityLabel,
            float credibility)
        {
            return new SocialNewsSeed
            {
                OriginType = originType,
                OriginKey = $"scheduled:{record.EventType}:{record.SourceKey}",
                SourceFaction = record.SourceFaction,
                TargetFaction = record.TargetFaction,
                Category = category,
                Sentiment = sentiment,
                OccurredTick = record.OccurredTick,
                Summary = record.Summary ?? string.Empty,
                SourceLabel = sourceLabel,
                CredibilityLabel = credibilityLabel,
                CredibilityValue = credibility,
                Facts = SocialNewsSeedFactory.BuildScheduledFacts(record),
                RawText = (record?.Summary ?? string.Empty) +
                    (string.IsNullOrWhiteSpace(record?.Detail) ? string.Empty : " — " + record.Detail)
            };
        }

internal static List<string> BuildScheduledFacts(ScheduledSocialEventRecord record)
        {
            return new List<string>
            {
                record?.Summary ?? string.Empty,
                $"Event type: {record?.EventType.ToString() ?? "Unknown"}",
                $"Source faction: {record?.SourceFaction?.Name ?? "Unknown"}",
                $"Target faction: {record?.TargetFaction?.Name ?? "None"}",
                $"Detail: {record?.Detail ?? string.Empty}",
                $"Value: {record?.Value ?? 0}",
                $"raw_text: {record?.Summary ?? string.Empty} {(string.IsNullOrWhiteSpace(record?.Detail) ? string.Empty : "— " + record.Detail)}"
            };
        }

internal static string BuildLocationFact(string location)
        {
            return string.IsNullOrWhiteSpace(location)
                ? string.Empty
                : $"Stronghold/settlement explicitly tied to this event: {location}";
        }

internal static string BuildSettlementContextFact(string location, Faction primaryFaction, Faction secondaryFaction)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return string.Empty;
            }

            Faction owner = primaryFaction != null && !primaryFaction.IsPlayer
                ? primaryFaction
                : secondaryFaction != null && !secondaryFaction.IsPlayer
                    ? secondaryFaction
                    : primaryFaction ?? secondaryFaction;
            if (owner == null)
            {
                return $"This location is a concrete settlement name and should be referenced naturally in the article: {location}";
            }

            return $"Settlement context: {location} is a concrete stronghold/settlement associated with {owner.Name}; weave it naturally into the article body instead of leaving it as metadata.";
        }

internal static string BuildFactionFactValue(Faction faction, string fallbackName = null)
        {
            string displayName = string.IsNullOrWhiteSpace(faction?.Name)
                ? (string.IsNullOrWhiteSpace(fallbackName) ? "Unknown" : fallbackName.Trim())
                : faction.Name;
            if (faction?.def == null)
            {
                return displayName;
            }

            string tech = faction.def.techLevel.ToString();
            string kind = faction.def.label ?? faction.def.defName ?? string.Empty;
            string relation = Faction.OfPlayer == null || faction.IsPlayer
                ? string.Empty
                : $", relation to player: {faction.RelationKindWith(Faction.OfPlayer)}";
            return string.IsNullOrWhiteSpace(kind)
                ? $"{displayName} (tech level: {tech}{relation})"
                : $"{displayName} ({kind}, tech level: {tech}{relation})";
        }

internal static string ResolveFactionStrongholdLabel(Faction primaryFaction, Faction secondaryFaction)
        {
            Faction resolvedFaction = primaryFaction != null && !primaryFaction.IsPlayer
                ? primaryFaction
                : secondaryFaction != null && !secondaryFaction.IsPlayer
                    ? secondaryFaction
                    : null;
            if (resolvedFaction == null)
            {
                return string.Empty;
            }

            int homeTile = Find.AnyPlayerHomeMap?.Tile ?? -1;
            if (!WorldTileGuard.IsValidTile(homeTile))
            {
                homeTile = -1;
            }
            IEnumerable<Settlement> candidateSettlements = Find.WorldObjects?.Settlements?
                .Where(settlement => settlement?.Faction == resolvedFaction
                                     && WorldTileGuard.IsValidTile(settlement.Tile))
                ?? Enumerable.Empty<Settlement>();
            List<Settlement> settlements = Enumerable.OrderBy<Settlement, int>(
                    candidateSettlements,
                    settlement => homeTile < 0
                        ? settlement.Tile
                        : Find.WorldGrid.TraversalDistanceBetween(homeTile, settlement.Tile))
                .ThenBy(settlement => settlement.ID)
                .Take(3)
                .ToList();
            if (settlements.Count == 0)
            {
                return string.Empty;
            }

            Settlement selected = settlements.RandomElement();
            return selected?.LabelCap ?? string.Empty;
        }

internal static IEnumerable<Faction> GetEligibleSourceFactions()
        {
            return Find.FactionManager.AllFactions
                .Where(faction => faction != null && !faction.IsPlayer && !faction.defeated && !faction.def.hidden);
        }

internal static Faction ResolveKnownFaction(IEnumerable<string> ids, bool preferPlayer)
        {
            List<Faction> factions = (ids ?? Enumerable.Empty<string>())
                .Select(ResolveFaction)
                .Where(faction => faction != null)
                .Distinct()
                .ToList();
            if (preferPlayer)
            {
                return factions.FirstOrDefault(faction => faction.IsPlayer);
            }

            return factions.FirstOrDefault(faction => !faction.IsPlayer);
        }

internal static Faction ResolveFaction(string factionId)
        {
            if (string.IsNullOrWhiteSpace(factionId))
            {
                return null;
            }

            return Find.FactionManager.AllFactions.FirstOrDefault(faction =>
                string.Equals(faction?.GetUniqueLoadID(), factionId, StringComparison.Ordinal)
                || string.Equals(SocialNewsSeedFactory.BuildMemoryFactionId(faction), factionId, StringComparison.Ordinal));
        }

internal static string BuildMemoryFactionId(Faction faction)
        {
            if (faction?.def != null && !string.IsNullOrWhiteSpace(faction.def.defName))
            {
                return $"{faction.def.defName}_{faction.loadID}";
            }

            return faction == null ? string.Empty : $"custom_{faction.loadID}";
        }

internal static string BuildDialogueOriginKey(
            Faction sourceFaction,
            Faction targetFaction,
            int currentTick,
            string summary,
            string intentHint,
            bool isKeyword)
        {
            string sourceId = sourceFaction?.GetUniqueLoadID() ?? "none";
            string targetId = targetFaction?.GetUniqueLoadID() ?? "none";
            return $"{(isKeyword ? "keyword" : "explicit")}:{sourceId}:{targetId}:{currentTick}:{summary}:{intentHint}";
        }
    }
}
