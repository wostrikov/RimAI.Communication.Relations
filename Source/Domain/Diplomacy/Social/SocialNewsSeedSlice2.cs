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
    internal static class SocialNewsSeedSlice2
    {
internal static bool IsConcreteDialogueFact(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string text = value.Trim();
            if (text.Length < 8)
            {
                return false;
            }

            string lowered = text.ToLowerInvariant();
            string[] concreteFragments =
            {
                "要求",
                "主张",
                "表示",
                "宣布",
                "拒绝",
                "支持",
                "反对",
                "停止",
                "继续",
                "允许",
                "禁止",
                "开放",
                "封锁",
                "停火",
                "谈判",
                "贸易",
                "援助",
                "袭击",
                "进攻",
                "威胁",
                "撤军",
                "增兵",
                "赔偿",
                "合作",
                "结盟",
                "归还",
                "交付",
                "释放",
                "警告",
                "trade",
                "truce",
                "aid",
                "raid",
                "attack",
                "threaten",
                "withdraw",
                "deploy",
                "compensation",
                "cooperate",
                "alliance",
                "return",
                "deliver",
                "release",
                "ban",
                "allow",
                "refuse",
                "reject",
                "support",
                "oppose",
                "demand",
                "claim",
                "announce",
                "warn"
            };
            if (concreteFragments.Any(fragment => lowered.Contains(fragment)))
            {
                return true;
            }

            string[] blockedFragments =
            {
                "引发讨论",
                "引起讨论",
                "公开社交圈",
                "社交圈",
                "发酵",
                "波澜",
                "关注",
                "热议",
                "议论",
                "讨论",
                "风声",
                "信号",
                "口径",
                "态度",
                "立场",
                "局势",
                "传闻",
                "rumor",
                "discussion",
                "debate",
                "signal",
                "stance",
                "attitude",
                "position",
                "public circle",
                "social circle"
            };
            if (blockedFragments.Any(fragment => lowered.Contains(fragment)))
            {
                return false;
            }

            return text.Contains("：")
                || text.Contains(":")
                || text.Contains("“")
                || text.Contains("”")
                || text.Contains("将")
                || text.Contains("会")
                || text.Contains("必须")
                || text.Contains("不得")
                || text.Contains("would")
                || text.Contains("will ")
                || text.Contains("must ")
                || text.Contains("should ");
        }

internal static string NormalizeDialogueClaimCandidate(string value)
        {
            string text = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            int firstSentenceIndex = text.IndexOfAny(new[] { '。', '！', '？', '.', '!', '?', ';', '；' });
            if (firstSentenceIndex > 0)
            {
                text = text.Substring(0, firstSentenceIndex).Trim();
            }

            string[] prefixes =
            {
                "对话内容",
                "公开对话",
                "公开声明",
                "公开表态",
                "消息称",
                "据称",
                "报道称",
                "有声音称"
            };
            foreach (string prefix in prefixes)
            {
                if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    text = text.Substring(prefix.Length).TrimStart('：', ':', '，', ',', ' ');
                }
            }

            return text.Trim().Trim('"', '“', '”');
        }

internal static void AddRaidSeeds(List<SocialNewsSeed> seeds)
        {
            List<RaidBattleReportRecord> reports = WorldEventLedgerComponent.Instance
                ?.GetRecentRaidBattleReports(Faction.OfPlayer, SocialNewsSeedFactory.SeedWindowDays, true) ?? new List<RaidBattleReportRecord>();
            foreach (RaidBattleReportRecord report in reports)
            {
                seeds.Add(SocialNewsSeedFactory.CreateRaidSeed(report));
            }
        }

internal static SocialNewsSeed CreateRaidSeed(RaidBattleReportRecord report)
        {
            Faction attacker = SocialNewsSeedFactory.ResolveFaction(report?.AttackerFactionId);
            Faction defender = SocialNewsSeedFactory.ResolveFaction(report?.DefenderFactionId);
            int sentiment = SocialNewsSeedFactory.CalculateBattleSentiment(report);
            return new SocialNewsSeed
            {
                OriginType = SocialNewsOriginType.RaidBattleReport,
                OriginKey = $"raid:{report?.AttackerFactionId}:{report?.DefenderFactionId}:{report?.BattleEndTick}:{report?.MapId}",
                SourceFaction = attacker,
                TargetFaction = defender,
                Category = SocialPostCategory.Military,
                Sentiment = sentiment,
                OccurredTick = report?.BattleEndTick ?? 0,
                Summary = report?.Summary ?? string.Empty,
                SourceLabel = "RimChat_SocialSourceBattleReport",
                CredibilityLabel = "RimChat_SocialCredibilityBattleReport",
                CredibilityValue = 0.85f,
                Facts = SocialNewsSeedFactory.BuildRaidFacts(report),
                RawText = report?.Summary ?? string.Empty
            };
        }

internal static List<string> BuildRaidFacts(RaidBattleReportRecord report)
        {
            Faction attacker = SocialNewsSeedFactory.ResolveFaction(report?.AttackerFactionId);
            Faction defender = SocialNewsSeedFactory.ResolveFaction(report?.DefenderFactionId);
            string location = SocialNewsSeedFactory.ResolveFactionStrongholdLabel(attacker, defender);
            return new List<string>
            {
                report?.Summary ?? string.Empty,
                $"Attacker: {SocialNewsSeedFactory.BuildFactionFactValue(attacker, report?.AttackerFactionName)}",
                $"Defender: {SocialNewsSeedFactory.BuildFactionFactValue(defender, report?.DefenderFactionName)}",
                SocialNewsSeedFactory.BuildLocationFact(location),
                SocialNewsSeedFactory.BuildSettlementContextFact(location, attacker, defender),
                $"Attacker deaths: {report?.AttackerDeaths ?? 0}",
                $"Defender deaths: {report?.DefenderDeaths ?? 0}",
                $"Defender downed: {report?.DefenderDowned ?? 0}",
                $"raw_text: {report?.AttackerFactionName} raided {report?.MapLabel}. Attacker lost {report?.AttackerDeaths ?? 0}, defender lost {report?.DefenderDeaths ?? 0}, {report?.DefenderDowned ?? 0} defenders were downed. Battle from tick {report?.BattleStartTick} to {report?.BattleEndTick}."
            };
        }

internal static int CalculateBattleSentiment(RaidBattleReportRecord report)
        {
            if (report == null)
            {
                return 0;
            }

            int score = report.DefenderDeaths - report.AttackerDeaths;
            if (score >= 4) return 2;
            if (score > 0) return 1;
            if (score <= -4) return -2;
            return score < 0 ? -1 : 0;
        }

internal static void AddWorldEventSeeds(List<SocialNewsSeed> seeds)
        {
            List<WorldEventRecord> events = WorldEventLedgerComponent.Instance
                ?.GetRecentWorldEvents(Faction.OfPlayer, SocialNewsSeedFactory.SeedWindowDays, true, true) ?? new List<WorldEventRecord>();
            foreach (WorldEventRecord record in events)
            {
                if (SocialNewsSeedFactory.ShouldTreatAsAidArrival(record))
                {
                    continue;
                }

                seeds.Add(SocialNewsSeedFactory.CreateWorldEventSeed(record));
            }
        }

internal static SocialNewsSeed CreateWorldEventSeed(WorldEventRecord record)
        {
            Faction sourceFaction = SocialNewsSeedFactory.ResolveKnownFaction(record?.KnownFactionIds, preferPlayer: false);
            Faction targetFaction = SocialNewsSeedFactory.ResolveKnownFaction(record?.KnownFactionIds, preferPlayer: true);
            SocialPostCategory category = SocialCircleService.InferCategory(record?.Summary, record?.EventType);
            int sentiment = SocialCircleService.InferSentiment(record?.Summary);
            return new SocialNewsSeed
            {
                OriginType = SocialNewsOriginType.WorldEvent,
                OriginKey = string.IsNullOrWhiteSpace(record?.SourceKey)
                    ? $"world:{record?.OccurredTick}:{record?.EventType}:{record?.Summary}"
                    : record.SourceKey,
                SourceFaction = sourceFaction,
                TargetFaction = targetFaction,
                Category = category,
                Sentiment = sentiment,
                OccurredTick = record?.OccurredTick ?? 0,
                Summary = record?.Summary ?? string.Empty,
                SourceLabel = "RimChat_SocialSourceWorldLedger",
                CredibilityLabel = record?.IsPublic == true
                    ? "RimChat_SocialCredibilityPublicReport"
                    : "RimChat_SocialCredibilityObserverNote",
                CredibilityValue = record?.IsPublic == true ? 0.74f : 0.62f,
                Facts = SocialNewsSeedFactory.BuildWorldEventFacts(record),
                RawText = record?.OriginalFullText ?? record?.Summary ?? string.Empty
            };
        }

internal static List<string> BuildWorldEventFacts(WorldEventRecord record)
        {
            Faction sourceFaction = SocialNewsSeedFactory.ResolveKnownFaction(record?.KnownFactionIds, preferPlayer: false);
            Faction targetFaction = SocialNewsSeedFactory.ResolveKnownFaction(record?.KnownFactionIds, preferPlayer: true);
            string location = SocialNewsSeedFactory.ResolveFactionStrongholdLabel(sourceFaction, targetFaction);
            return new List<string>
            {
                record?.Summary ?? string.Empty,
                $"Event type: {record?.EventType ?? "unknown"}",
                $"Source faction: {SocialNewsSeedFactory.BuildFactionFactValue(sourceFaction)}",
                $"Target faction: {SocialNewsSeedFactory.BuildFactionFactValue(targetFaction, Faction.OfPlayer?.Name)}",
                SocialNewsSeedFactory.BuildLocationFact(location),
                SocialNewsSeedFactory.BuildSettlementContextFact(location, sourceFaction, targetFaction),
                $"Visibility: {(record?.IsPublic == true ? "public" : "direct/limited")}",
                $"Known factions: {string.Join(", ", record?.KnownFactionIds ?? new List<string>())}",
                $"raw_text: {record?.OriginalFullText ?? record?.Summary ?? string.Empty}"
            };
        }

internal static void AddAidArrivalSeeds(List<SocialNewsSeed> seeds)
        {
            List<WorldEventRecord> events = WorldEventLedgerComponent.Instance
                ?.GetRecentWorldEvents(Faction.OfPlayer, SocialNewsSeedFactory.SeedWindowDays, true, true) ?? new List<WorldEventRecord>();
            foreach (WorldEventRecord record in events)
            {
                if (!SocialNewsSeedFactory.ShouldTreatAsAidArrival(record))
                {
                    continue;
                }

                seeds.Add(SocialNewsSeedFactory.CreateAidArrivalWorldEventSeed(record));
            }
        }

internal static bool ShouldTreatAsAidArrival(WorldEventRecord record)
        {
            string merged = $"{record?.EventType} {record?.Summary}".ToLowerInvariant();
            return merged.Contains("aid")
                   || merged.Contains("援助")
                   || merged.Contains("救援")
                   || merged.Contains("support");
        }

internal static SocialNewsSeed CreateAidArrivalWorldEventSeed(WorldEventRecord record)
        {
            Faction sourceFaction = SocialNewsSeedFactory.ResolveKnownFaction(record?.KnownFactionIds, preferPlayer: false);
            return new SocialNewsSeed
            {
                OriginType = SocialNewsOriginType.AidArrival,
                OriginKey = string.IsNullOrWhiteSpace(record?.SourceKey)
                    ? $"aid-arrival:{record?.OccurredTick}:{record?.Summary}"
                    : $"aid-arrival:{record.SourceKey}",
                SourceFaction = sourceFaction,
                TargetFaction = Faction.OfPlayer,
                Category = SocialPostCategory.Economic,
                Sentiment = 2,
                OccurredTick = record?.OccurredTick ?? 0,
                Summary = record?.Summary ?? string.Empty,
                SourceLabel = "RimChat_SocialSourceAidArrival",
                CredibilityLabel = "RimChat_SocialCredibilityPublicReport",
                CredibilityValue = 0.88f,
                Facts = SocialNewsSeedFactory.BuildWorldEventFacts(record),
                RawText = record?.OriginalFullText ?? record?.Summary ?? string.Empty
            };
        }

internal static void AddLeaderMemorySeeds(List<SocialNewsSeed> seeds)
        {
            foreach (Faction faction in SocialNewsSeedFactory.GetEligibleSourceFactions())
            {
                FactionLeaderMemory memory = LeaderMemoryManager.Instance?.GetMemory(faction);
                if (memory?.SignificantEvents == null)
                {
                    continue;
                }

                foreach (SignificantEventMemory evt in memory.SignificantEvents)
                {
                    if (SocialNewsSeedFactory.ShouldSkipMemoryEvent(evt))
                    {
                        continue;
                    }

                    seeds.Add(SocialNewsSeedFactory.CreateLeaderMemorySeed(faction, evt));
                }
            }
        }

internal static bool ShouldSkipMemoryEvent(SignificantEventMemory evt)
        {
            return evt == null
                || evt.OccurredTick <= 0
                || string.IsNullOrWhiteSpace(evt.Description)
                || evt.Description.StartsWith("[init-snapshot]", StringComparison.Ordinal);
        }

internal static SocialNewsSeed CreateLeaderMemorySeed(Faction ownerFaction, SignificantEventMemory evt)
        {
            Faction targetFaction = SocialNewsSeedFactory.ResolveFaction(evt?.InvolvedFactionId);
            SocialPostCategory category = SocialCircleService.InferCategory(evt?.Description, evt?.EventType.ToString());
            int sentiment = SocialCircleService.InferSentiment(evt?.Description);
            return new SocialNewsSeed
            {
                OriginType = SocialNewsOriginType.LeaderMemory,
                OriginKey = $"memory:{ownerFaction?.GetUniqueLoadID()}:{evt?.OccurredTick}:{evt?.Timestamp}:{evt?.EventType}",
                SourceFaction = ownerFaction,
                TargetFaction = targetFaction,
                Category = category,
                Sentiment = sentiment,
                OccurredTick = evt?.OccurredTick ?? 0,
                Summary = evt?.Description ?? string.Empty,
                SourceLabel = "RimChat_SocialSourceLeaderMemory",
                CredibilityLabel = "RimChat_SocialCredibilityLeaderMemory",
                CredibilityValue = 0.58f,
                Facts = SocialNewsSeedFactory.BuildLeaderMemoryFacts(evt),
                RawText = evt?.Description ?? string.Empty
            };
        }

internal static List<string> BuildLeaderMemoryFacts(SignificantEventMemory evt)
        {
            Faction involvedFaction = SocialNewsSeedFactory.ResolveFaction(evt?.InvolvedFactionId);
            string location = SocialNewsSeedFactory.ResolveFactionStrongholdLabel(involvedFaction, null);
            return new List<string>
            {
                evt?.Description ?? string.Empty,
                $"Event type: {evt?.EventType.ToString() ?? "Unknown"}",
                $"Involved faction: {SocialNewsSeedFactory.BuildFactionFactValue(involvedFaction, evt?.InvolvedFactionName)}",
                SocialNewsSeedFactory.BuildLocationFact(location),
                SocialNewsSeedFactory.BuildSettlementContextFact(location, involvedFaction, null),
                $"Occurred tick: {evt?.OccurredTick ?? 0}",
                $"raw_text: {evt?.Description ?? string.Empty}"
            };
        }
    }
}
