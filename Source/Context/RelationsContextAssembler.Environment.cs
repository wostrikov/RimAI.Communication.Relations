using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Dialogue;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Relation;
using Ustas.RimAI.Communication.Relations.Guards;
using Ustas.RimAI.Communication.Relations.WorldState;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.Prompting.Builders;
using Ustas.RimAI.Communication.Relations.Context;
using Ustas.RimAI.Communication.Relations.Prompting.Transfer;
using Ustas.RimAI.Communication.Relations.Serialization;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Prompting.Diplomacy;

namespace Ustas.RimAI.Communication.Relations.Context
{
    internal sealed partial class RelationsContextAssembler
    {        internal string BuildEnvironmentPromptBlocks(SystemPromptConfig config, DialogueScenarioContext context)
        {
            return BuildEnvironmentPromptBlocksInternal(config, context, null);
        }

        internal string BuildEnvironmentPromptBlocksWithDiagnostics(
            SystemPromptConfig config,
            DialogueScenarioContext context,
            out EnvironmentPromptBuildDiagnostics diagnostics)
        {
            diagnostics = new EnvironmentPromptBuildDiagnostics();
            return BuildEnvironmentPromptBlocksInternal(config, context, diagnostics);
        }

        internal string BuildEnvironmentPromptBlocksInternal(
            SystemPromptConfig config,
            DialogueScenarioContext context,
            EnvironmentPromptBuildDiagnostics diagnostics)
        {
            if (config?.EnvironmentPrompt == null || context == null)
            {
                return string.Empty;
            }

            if (host.SnapshotService.TryGetScopedRuntimeSnapshotForFaction(context.Faction, out DiplomacyPromptRuntimeSnapshot snapshot) &&
                !string.IsNullOrWhiteSpace(snapshot.EnvironmentPromptBlock))
            {
                return snapshot.EnvironmentPromptBlock;
            }

            var env = config.EnvironmentPrompt;
            var sb = new StringBuilder();

            if (env.Worldview?.Enabled == true && !string.IsNullOrWhiteSpace(env.Worldview.Content))
            {
                sb.AppendLine(env.Worldview.Content.Trim());
                sb.AppendLine();
            }

            AppendEnvironmentContextBlock(sb, env, context);
            AppendRecentWorldEventIntel(sb, env, context);

            if (!(env.SceneSystem?.Enabled ?? false) || env.SceneEntries == null || env.SceneEntries.Count == 0)
            {
                return sb.ToString();
            }

            HashSet<string> tags = BuildScenarioTags(context, env.SceneSystem.PresetTagsEnabled);
            if (diagnostics != null)
            {
                diagnostics.ScenarioTags.AddRange(tags.OrderBy(tag => tag));
            }

            int maxPerScene = env.SceneSystem.MaxSceneChars > 0 ? env.SceneSystem.MaxSceneChars : int.MaxValue;
            int maxTotalSceneChars = env.SceneSystem.MaxTotalChars > 0 ? env.SceneSystem.MaxTotalChars : int.MaxValue;
            int totalSceneChars = 0;
            int appendedCount = 0;

            var orderedEntries = env.SceneEntries
                .Where(entry => entry != null)
                .OrderByDescending(entry => entry.Priority)
                .ThenBy(entry => entry.Name ?? string.Empty)
                .ToList();

            foreach (ScenePromptEntryConfig entry in orderedEntries)
            {
                EnvironmentSceneEntryDiagnostic sceneDiag = null;
                if (diagnostics != null)
                {
                    sceneDiag = new EnvironmentSceneEntryDiagnostic
                    {
                        Id = entry.Id ?? string.Empty,
                        Name = string.IsNullOrWhiteSpace(entry.Name) ? "UnnamedScene" : entry.Name.Trim(),
                        Priority = entry.Priority
                    };
                    diagnostics.SceneEntries.Add(sceneDiag);
                }

                if (!entry.Enabled)
                {
                    if (sceneDiag != null)
                    {
                        sceneDiag.SkipReason = "disabled";
                    }
                    continue;
                }

                bool channelMatched = context.IsRpg ? entry.ApplyToRPG : entry.ApplyToDiplomacy;
                if (sceneDiag != null)
                {
                    sceneDiag.ChannelMatched = channelMatched;
                }

                if (!channelMatched)
                {
                    if (sceneDiag != null)
                    {
                        sceneDiag.SkipReason = "channel_filtered";
                    }
                    continue;
                }

                bool tagsMatched = EntryMatchesTags(entry, tags);
                if (sceneDiag != null)
                {
                    sceneDiag.TagsMatched = tagsMatched;
                }

                if (!tagsMatched)
                {
                    if (sceneDiag != null)
                    {
                        sceneDiag.SkipReason = "tag_filtered";
                    }
                    continue;
                }

                string content = entry.Content?.Trim() ?? string.Empty;
                if (content.Length == 0)
                {
                    if (sceneDiag != null)
                    {
                        sceneDiag.SkipReason = "empty";
                    }
                    continue;
                }

                content = host.TemplateVariables.RenderTemplateVariables(content, context, env, out List<string> usedVariables, out List<string> unknownVariables);
                if (sceneDiag != null)
                {
                    sceneDiag.UsedVariables.AddRange(usedVariables);
                    sceneDiag.UnknownVariables.AddRange(unknownVariables);
                }

                if (content.Length == 0)
                {
                    if (sceneDiag != null)
                    {
                        sceneDiag.SkipReason = "empty_after_render";
                    }
                    continue;
                }

                int originalChars = content.Length;
                if (content.Length > maxPerScene)
                {
                    content = content.Substring(0, maxPerScene);
                    if (sceneDiag != null)
                    {
                        sceneDiag.TruncatedByPerSceneLimit = true;
                    }
                }

                int remain = maxTotalSceneChars - totalSceneChars;
                if (remain <= 0)
                {
                    if (sceneDiag != null)
                    {
                        sceneDiag.SkipReason = "total_limit_exceeded";
                    }
                    if (diagnostics == null)
                    {
                        break;
                    }
                    continue;
                }

                if (content.Length > remain)
                {
                    content = content.Substring(0, remain);
                    if (sceneDiag != null)
                    {
                        sceneDiag.TruncatedByTotalLimit = true;
                    }
                }

                if (content.Length == 0)
                {
                    if (sceneDiag != null)
                    {
                        sceneDiag.SkipReason = "empty_after_limit";
                    }
                    continue;
                }

                if (appendedCount == 0)
                {
                    sb.AppendLine("=== SCENE PROMPT LAYERS ===");
                }

                string name = string.IsNullOrWhiteSpace(entry.Name) ? "UnnamedScene" : entry.Name.Trim();
                sb.AppendLine($"[{name}]");
                sb.AppendLine(content);
                sb.AppendLine();
                appendedCount++;
                totalSceneChars += content.Length;

                if (sceneDiag != null)
                {
                    sceneDiag.Included = true;
                    sceneDiag.OriginalChars = originalChars;
                    sceneDiag.AppliedChars = content.Length;
                    sceneDiag.SkipReason = string.Empty;
                }
            }

            return sb.ToString();
        }

        internal void AppendEnvironmentContextBlock(StringBuilder sb, EnvironmentPromptConfig env, DialogueScenarioContext context)
        {
            EnvironmentContextSwitchesConfig switches = env?.EnvironmentContextSwitches;
            if (!(switches?.Enabled ?? false))
            {
                return;
            }

            Map map = ResolveEnvironmentMap(context);
            if (map == null)
            {
                return;
            }

            if (!TryResolveFocusCell(map, context, out IntVec3 focusCell))
            {
                return;
            }

            List<string> lines = BuildEnvironmentContextLines(map, focusCell, context, switches);
            if (lines.Count == 0)
            {
                return;
            }

            sb.AppendLine("=== ENVIRONMENT PARAMETERS ===");
            foreach (string line in lines)
            {
                sb.AppendLine(line);
            }
            sb.AppendLine();
        }

        internal void AppendRecentWorldEventIntel(StringBuilder sb, EnvironmentPromptConfig env, DialogueScenarioContext context)
        {
            if (sb == null)
            {
                return;
            }

            if (!TryCollectRecentEventIntelItems(env, context, out List<RecentEventIntelItem> items))
            {
                return;
            }

            EventIntelPromptConfig intel = env?.EventIntelPrompt ?? new EventIntelPromptConfig();
            int maxItems = Mathf.Clamp(intel.MaxInjectedItems, 1, 50);
            int maxChars = Mathf.Clamp(intel.MaxInjectedChars, 200, 12000);
            RecentEventSelectionResult selection = SelectRecentEventIntelLines(items, maxItems, maxChars);
            if (selection.SelectedLines.Count == 0)
            {
                return;
            }

            sb.AppendLine("=== RECENT WORLD EVENTS & BATTLE INTEL ===");
            for (int i = 0; i < selection.SelectedLines.Count; i++)
            {
                sb.AppendLine(selection.SelectedLines[i]);
            }

            if (selection.OmittedCount > 0)
            {
                List<string> digestLines = BuildRecentEventDigestLines(items, selection);
                for (int i = 0; i < digestLines.Count; i++)
                {
                    sb.AppendLine(digestLines[i]);
                }
            }

            sb.AppendLine();
        }

        internal string BuildRecentWorldEventIntelCompactDigest(
            EnvironmentPromptConfig env,
            DialogueScenarioContext context,
            int maxItems = 2,
            int maxChars = 260)
        {
            if (!TryCollectRecentEventIntelItems(env, context, out List<RecentEventIntelItem> items))
            {
                return string.Empty;
            }

            RecentEventSelectionResult selection = SelectRecentEventIntelLines(
                items,
                Mathf.Clamp(maxItems, 1, 6),
                Mathf.Clamp(maxChars, 120, 1200));
            if (selection.SelectedLines.Count == 0)
            {
                return string.Empty;
            }

            string latestDigest = string.Join(" | ", selection.SelectedLines
                .Take(2)
                .Select(BuildCompactDigestEntry)
                .Where(line => !string.IsNullOrWhiteSpace(line)));
            string typeDigest = BuildTypeDigest(items);
            string topicDigest = BuildTopicDigest(items);
            string trendDigest = BuildTrendDigest(items);

            var sb = new StringBuilder();
            sb.Append("See <environment> for full event details. ");
            sb.Append("Digest: ");
            if (!string.IsNullOrWhiteSpace(latestDigest))
            {
                sb.Append("latest=");
                sb.Append(latestDigest);
                sb.Append("; ");
            }

            sb.Append("total=");
            sb.Append(items.Count);
            if (selection.OmittedCount > 0)
            {
                sb.Append(", omitted=");
                sb.Append(selection.OmittedCount);
            }

            sb.Append("; types=");
            sb.Append(typeDigest);
            sb.Append("; topics=");
            sb.Append(topicDigest);
            sb.Append("; trend=");
            sb.Append(trendDigest);
            return ClampPromptBlock(sb.ToString().Trim(), 420);
        }

        internal bool TryCollectRecentEventIntelItems(
            EnvironmentPromptConfig env,
            DialogueScenarioContext context,
            out List<RecentEventIntelItem> items)
        {
            items = new List<RecentEventIntelItem>();
            EventIntelPromptConfig intel = env?.EventIntelPrompt;
            if (intel == null || !intel.Enabled || context == null)
            {
                return false;
            }

            if (context.IsRpg && !intel.ApplyToRpg)
            {
                return false;
            }

            if (!context.IsRpg && !intel.ApplyToDiplomacy)
            {
                return false;
            }

            WorldEventLedgerComponent ledger = WorldEventLedgerComponent.Instance;
            if (ledger == null)
            {
                return false;
            }

            Faction observer = context.Faction ?? context.Target?.Faction ?? context.Initiator?.Faction;
            if (intel.IncludeMapEvents)
            {
                List<WorldEventRecord> mapEvents = ledger.GetRecentWorldEvents(observer, intel.DaysWindow, includePublic: true, includeDirect: true);
                for (int i = 0; i < mapEvents.Count; i++)
                {
                    WorldEventRecord record = mapEvents[i];
                    if (record == null || string.IsNullOrWhiteSpace(record.Summary))
                    {
                        continue;
                    }

                    items.Add(new RecentEventIntelItem
                    {
                        Tick = record.OccurredTick,
                        Category = "MapEvent",
                        Summary = record.Summary.Trim(),
                        EventType = string.IsNullOrWhiteSpace(record.EventType) ? "map_event" : record.EventType
                    });
                }
            }

            if (intel.IncludeRaidBattleReports)
            {
                List<RaidBattleReportRecord> reports = ledger.GetRecentRaidBattleReports(observer, intel.DaysWindow, includeDirect: true);
                for (int i = 0; i < reports.Count; i++)
                {
                    RaidBattleReportRecord report = reports[i];
                    if (report == null || string.IsNullOrWhiteSpace(report.Summary))
                    {
                        continue;
                    }

                    items.Add(new RecentEventIntelItem
                    {
                        Tick = report.BattleEndTick,
                        Category = "BattleIntel",
                        Summary = report.Summary.Trim(),
                        EventType = "battle_intel"
                    });
                }
            }

            items = items
                .OrderByDescending(item => item.Tick)
                .ToList();
            return items.Count > 0;
        }

        internal RecentEventSelectionResult SelectRecentEventIntelLines(
            List<RecentEventIntelItem> items,
            int maxItems,
            int maxChars)
        {
            var result = new RecentEventSelectionResult();
            if (items == null || items.Count == 0)
            {
                return result;
            }

            int usedChars = 0;
            int cappedItems = Mathf.Clamp(maxItems, 1, 100);
            int cappedChars = Mathf.Clamp(maxChars, 80, 16000);
            for (int i = 0; i < items.Count; i++)
            {
                RecentEventIntelItem item = items[i];
                string line = BuildRecentEventIntelLine(item);
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (result.SelectedLines.Count >= cappedItems)
                {
                    result.OmittedCount++;
                    continue;
                }

                int remainingChars = cappedChars - usedChars;
                if (remainingChars < 16)
                {
                    result.OmittedCount += items.Count - i;
                    break;
                }

                if (line.Length > remainingChars)
                {
                    line = line.Substring(0, remainingChars).TrimEnd() + "...";
                }

                result.SelectedLines.Add(line);
                usedChars += line.Length;
            }

            result.OmittedCount += Math.Max(0, items.Count - result.SelectedLines.Count - result.OmittedCount);
            return result;
        }

        internal string BuildRecentEventIntelLine(RecentEventIntelItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Summary))
            {
                return string.Empty;
            }

            string category = string.IsNullOrWhiteSpace(item.Category) ? "MapEvent" : item.Category.Trim();
            return $"- [{category}] {item.Summary} ({BuildRelativeTickText(item.Tick)})";
        }

        internal List<string> BuildRecentEventDigestLines(
            List<RecentEventIntelItem> items,
            RecentEventSelectionResult selection)
        {
            var lines = new List<string>();
            if (items == null || items.Count == 0 || selection == null || selection.OmittedCount <= 0)
            {
                return lines;
            }

            lines.Add($"- [EventDigest] omitted={selection.OmittedCount}; total={items.Count}; types={BuildTypeDigest(items)}");
            lines.Add($"- [EventDigest] topics={BuildTopicDigest(items)}; trend={BuildTrendDigest(items)}");
            return lines;
        }

        internal string BuildCompactDigestEntry(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return string.Empty;
            }

            string text = line.Trim();
            if (text.StartsWith("- ", StringComparison.Ordinal))
            {
                text = text.Substring(2).TrimStart();
            }

            int maxLen = text.Length > 100 ? 100 : 80;
            if (text.Length > maxLen)
            {
                text = text.Substring(0, maxLen).TrimEnd() + "...";
            }

            return text;
        }

        internal string BuildTypeDigest(IEnumerable<RecentEventIntelItem> items)
        {
            int mapEvents = 0;
            int battleIntel = 0;
            foreach (RecentEventIntelItem item in items ?? Enumerable.Empty<RecentEventIntelItem>())
            {
                if (item == null)
                {
                    continue;
                }

                if (string.Equals(item.Category, "BattleIntel", StringComparison.OrdinalIgnoreCase))
                {
                    battleIntel++;
                }
                else
                {
                    mapEvents++;
                }
            }

            return $"MapEvent:{mapEvents},BattleIntel:{battleIntel}";
        }

        internal string BuildTopicDigest(IEnumerable<RecentEventIntelItem> items)
        {
            var topicCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (RecentEventIntelItem item in items ?? Enumerable.Empty<RecentEventIntelItem>())
            {
                string topic = ResolveRecentEventTopic(item);
                if (topicCounts.ContainsKey(topic))
                {
                    topicCounts[topic]++;
                }
                else
                {
                    topicCounts[topic] = 1;
                }
            }

            if (topicCounts.Count == 0)
            {
                return "general:0";
            }

            return string.Join(",",
                topicCounts
                    .OrderByDescending(pair => pair.Value)
                    .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                    .Take(3)
                    .Select(pair => $"{pair.Key}:{pair.Value}"));
        }

        internal string ResolveRecentEventTopic(RecentEventIntelItem item)
        {
            string text = (item?.Summary ?? string.Empty).ToLowerInvariant();
            if (ContainsAnyKeyword(text, "raid", "attack", "battle", "siege", "袭击", "战斗", "围攻"))
            {
                return "conflict";
            }

            if (ContainsAnyKeyword(text, "died", "death", "killed", "死亡", "阵亡", "葬礼"))
            {
                return "casualty";
            }

            if (ContainsAnyKeyword(text, "trade", "caravan", "merchant", "交易", "商队", "商船"))
            {
                return "trade";
            }

            if (ContainsAnyKeyword(text, "quest", "mission", "任务", "委托"))
            {
                return "quest";
            }

            return "general";
        }

        internal bool ContainsAnyKeyword(string text, params string[] tokens)
        {
            if (string.IsNullOrEmpty(text) || tokens == null)
            {
                return false;
            }

            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (!string.IsNullOrEmpty(token) && text.Contains(token))
                {
                    return true;
                }
            }

            return false;
        }

        internal string BuildTrendDigest(IEnumerable<RecentEventIntelItem> items)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            int recent1d = 0;
            int recent3d = 0;
            int older = 0;
            foreach (RecentEventIntelItem item in items ?? Enumerable.Empty<RecentEventIntelItem>())
            {
                if (item == null)
                {
                    continue;
                }

                int delta = Math.Max(0, now - Math.Max(0, item.Tick));
                if (delta <= GenDate.TicksPerDay)
                {
                    recent1d++;
                }
                else if (delta <= GenDate.TicksPerDay * 3)
                {
                    recent3d++;
                }
                else
                {
                    older++;
                }
            }

            return $"24h:{recent1d},3d:{recent3d},older:{older}";
        }

        internal sealed class RecentEventIntelItem
        {
            public int Tick;
            public string Category;
            public string Summary;
            public string EventType;
        }

        internal sealed class RecentEventSelectionResult
        {
            public readonly List<string> SelectedLines = new List<string>();
            public int OmittedCount;
        }

        internal string BuildRelativeTickText(int tick)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            int delta = Math.Max(0, now - Math.Max(0, tick));
            if (delta >= GenDate.TicksPerDay)
            {
                float days = delta / (float)GenDate.TicksPerDay;
                return $"{days:F1}d ago";
            }

            float hours = delta / 2500f;
            return $"{hours:F1}h ago";
        }

        internal List<string> BuildEnvironmentContextLines(
            Map map,
            IntVec3 focusCell,
            DialogueScenarioContext context,
            EnvironmentContextSwitchesConfig switches)
        {
            var lines = new List<string>();
            if (switches.IncludeTime)
            {
                lines.Add($"Time: {BuildLocalTimeText(map)}");
            }

            if (switches.IncludeDate)
            {
                lines.Add($"Date: {BuildLocalDateText(map)}");
            }

            if (switches.IncludeSeason)
            {
                lines.Add($"Season: {GenLocalDate.Season(map)}");
            }

            if (switches.IncludeWeather)
            {
                lines.Add($"Weather: {map.weatherManager?.curWeather?.LabelCap ?? "Unknown"}");
            }

            if (switches.IncludeLocationAndTemperature)
            {
                lines.Add(BuildLocationAndTemperatureText(map, focusCell, context));
            }

            if (switches.IncludeTerrain)
            {
                TerrainDef terrain = map.terrainGrid?.TerrainAt(focusCell);
                if (terrain != null)
                {
                    lines.Add($"Terrain: {terrain.LabelCap}");
                }
            }

            if (switches.IncludeBeauty)
            {
                lines.Add($"Beauty: {BuildBeautyText(map, focusCell)}");
            }

            if (switches.IncludeCleanliness)
            {
                string cleanliness = BuildCleanlinessText(map, focusCell);
                if (!string.IsNullOrWhiteSpace(cleanliness))
                {
                    lines.Add($"Cleanliness: {cleanliness}");
                }
            }

            if (switches.IncludeSurroundings)
            {
                string surroundings = BuildSurroundingsText(map, focusCell, context);
                if (!string.IsNullOrWhiteSpace(surroundings))
                {
                    lines.Add($"Surroundings: {surroundings}");
                }
            }

            if (switches.IncludeWealth)
            {
                lines.Add($"MapWealth: {(int)(map.wealthWatcher?.WealthTotal ?? 0f)}");
            }

            return lines;
        }

        internal string BuildLocalTimeText(Map map)
        {
            int hour = GenLocalDate.HourOfDay(map);
            float dayPercent = GenLocalDate.DayPercent(map);
            int minute = (int)((dayPercent * 24f - hour) * 60f);
            if (minute < 0) minute = 0;
            if (minute > 59) minute = 59;
            return $"{hour:00}:{minute:00}";
        }

        internal string BuildLocalDateText(Map map)
        {
            int absTicks = Find.TickManager?.TicksAbs ?? 0;
            if (!WorldTileGuard.IsValidTile(map?.Tile ?? -1))
            {
                return $"Unknown Date, Year {GenDate.Year(absTicks, 0f) + 1}";
            }
            Vector2 longLat = Find.WorldGrid.LongLatOf(map.Tile);
            int dayOfQuadrum = GenDate.DayOfQuadrum(absTicks, longLat.x) + 1;
            string quadrum = GenDate.Quadrum(absTicks, longLat.x).Label();
            int year = GenDate.Year(absTicks, longLat.x) + 1;
            return $"{quadrum} {dayOfQuadrum}, Year {year}";
        }

        internal string BuildLocationAndTemperatureText(Map map, IntVec3 focusCell, DialogueScenarioContext context)
        {
            float temperature = GenTemperature.GetTemperatureForCell(focusCell, map);
            string location = BuildLocationText(context, map, focusCell);
            return $"Location: {location}; Temperature: {temperature:F0}C";
        }

        internal string BuildLocationText(DialogueScenarioContext context, Map map, IntVec3 focusCell)
        {
            Pawn target = context?.Target;
            if (target != null && target.Spawned && target.Map == map)
            {
                Room room = target.GetRoom();
                string roomLabel = room is { PsychologicallyOutdoors: false }
                    ? room.Role?.label ?? "Room"
                    : "Outdoors";
                return $"{target.LabelShortCap} @ {roomLabel} / {map.Parent?.LabelCap ?? map.Biome?.LabelCap}";
            }

            Pawn initiator = context?.Initiator;
            if (initiator != null && initiator.Spawned && initiator.Map == map)
            {
                Room room = initiator.GetRoom();
                string roomLabel = room is { PsychologicallyOutdoors: false }
                    ? room.Role?.label ?? "Room"
                    : "Outdoors";
                return $"{initiator.LabelShortCap} @ {roomLabel} / {map.Parent?.LabelCap ?? map.Biome?.LabelCap}";
            }

            TerrainDef terrain = map.terrainGrid?.TerrainAt(focusCell);
            string terrainLabel = terrain?.LabelCap ?? "UnknownTerrain";
            return $"{map.Parent?.LabelCap ?? map.Biome?.LabelCap} ({terrainLabel})";
        }

        internal string BuildBeautyText(Map map, IntVec3 focusCell)
        {
            CellRect cellRect = CellRect.CenteredOn(focusCell, 2).ClipInsideMap(map);
            float total = 0f;
            int count = 0;
            foreach (IntVec3 cell in cellRect.Cells)
            {
                total += BeautyUtility.CellBeauty(cell, map);
                count++;
            }

            if (count == 0)
            {
                return "Unknown";
            }

            float avg = total / count;
            return avg.ToString("F1");
        }

        internal string BuildCleanlinessText(Map map, IntVec3 focusCell)
        {
            Room room = focusCell.GetRoom(map);
            if (room == null || room.PsychologicallyOutdoors)
            {
                return "Outdoors";
            }

            float cleanliness = room.GetStat(RoomStatDefOf.Cleanliness);
            return cleanliness.ToString("F2");
        }

        internal string BuildSurroundingsText(Map map, IntVec3 focusCell, DialogueScenarioContext context)
        {
            CellRect area = CellRect.CenteredOn(focusCell, 6).ClipInsideMap(map);
            if (area.Area == 0)
            {
                return string.Empty;
            }

            int humanlikes = 0;
            int hostiles = 0;
            int buildings = 0;
            int fires = 0;
            Faction referenceFaction = context?.Target?.Faction ?? context?.Initiator?.Faction ?? Faction.OfPlayer;

            foreach (IntVec3 cell in area.Cells)
            {
                List<Thing> things = cell.GetThingList(map);
                if (things == null)
                {
                    continue;
                }

                for (int i = 0; i < things.Count; i++)
                {
                    Thing thing = things[i];
                    if (thing is Pawn pawn)
                    {
                        if (pawn.RaceProps?.Humanlike == true)
                        {
                            humanlikes++;
                        }

                        if (referenceFaction != null && pawn.Faction != null && pawn.Faction.HostileTo(referenceFaction))
                        {
                            hostiles++;
                        }
                        continue;
                    }

                    if (thing.def?.category == ThingCategory.Building)
                    {
                        buildings++;
                    }

                    if (thing.def == ThingDefOf.Fire)
                    {
                        fires++;
                    }
                }
            }

            var parts = new List<string>
            {
                $"humanlike={humanlikes}",
                $"hostile={hostiles}",
                $"buildings={buildings}"
            };
            if (fires > 0)
            {
                parts.Add($"fires={fires}");
            }
            return string.Join(", ", parts);
        }

        internal Map ResolveEnvironmentMap(DialogueScenarioContext context)
        {
            if (context?.Target?.Map != null)
            {
                return context.Target.Map;
            }

            if (context?.Initiator?.Map != null)
            {
                return context.Initiator.Map;
            }

            if (Find.CurrentMap != null)
            {
                return Find.CurrentMap;
            }

            return Find.Maps?.FirstOrDefault(m => m != null && m.IsPlayerHome)
                ?? Find.Maps?.FirstOrDefault();
        }

        internal bool TryResolveFocusCell(Map map, DialogueScenarioContext context, out IntVec3 focusCell)
        {
            focusCell = IntVec3.Invalid;
            if (map == null)
            {
                return false;
            }

            if (context?.Target != null && context.Target.Spawned && context.Target.Map == map)
            {
                focusCell = context.Target.Position;
                return true;
            }

            if (context?.Initiator != null && context.Initiator.Spawned && context.Initiator.Map == map)
            {
                focusCell = context.Initiator.Position;
                return true;
            }

            focusCell = map.Center;
            return focusCell.IsValid && focusCell.InBounds(map);
        }

        internal HashSet<string> BuildScenarioTags(DialogueScenarioContext context, bool includePresetTags)
        {
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (context?.Tags != null)
            {
                foreach (string tag in context.Tags)
                {
                    AddNormalizedTag(tags, tag);
                }
            }

            if (!includePresetTags || context == null)
            {
                return tags;
            }

            if (context.IsRpg)
            {
                AppendRpgScenarioTags(context, tags);
            }
            else
            {
                AppendDiplomacyScenarioTags(context, tags);
            }

            return tags;
        }

        internal void AppendDiplomacyScenarioTags(DialogueScenarioContext context, HashSet<string> tags)
        {
            Faction faction = context?.Faction;
            if (faction == null)
            {
                return;
            }

            AddNormalizedTag(tags, $"faction:{faction.def?.defName}");
            AddNormalizedTag(tags, $"tech:{faction.def?.techLevel}");

            int goodwill = faction.PlayerGoodwill;
            if (goodwill >= 60)
            {
                AddNormalizedTag(tags, "relation:friendly");
                AddNormalizedTag(tags, "scene:social");
            }
            else if (goodwill <= -40 || faction.HostileTo(Faction.OfPlayer))
            {
                AddNormalizedTag(tags, "relation:hostile");
                AddNormalizedTag(tags, "scene:threat");
            }
            else
            {
                AddNormalizedTag(tags, "relation:neutral");
                AddNormalizedTag(tags, "scene:social");
            }

            bool hasQuestWithFaction = Find.QuestManager?.QuestsListForReading?.Any(q =>
                q != null &&
                q.State == QuestState.Ongoing &&
                QuestInvolvedFactionsGuard.HasInvolvedFaction(q, faction)) == true;
            if (hasQuestWithFaction)
            {
                AddNormalizedTag(tags, "scene:task");
            }
        }

        internal void AppendRpgScenarioTags(DialogueScenarioContext context, HashSet<string> tags)
        {
            Pawn initiator = context?.Initiator;
            Pawn target = context?.Target;
            if (target == null)
            {
                return;
            }

            AddNormalizedTag(tags, $"faction:{target.Faction?.def?.defName}");

            if (TryGetMoodTag(target, out string moodTag))
            {
                AddNormalizedTag(tags, moodTag);
            }

            float health = target.health?.summaryHealth?.SummaryHealthPercent ?? 1f;
            if (health <= 0.6f)
            {
                AddNormalizedTag(tags, "health:injured");
                AddNormalizedTag(tags, "scene:conflict");
            }

            if (HasIntimateRelation(target, initiator))
            {
                AddNormalizedTag(tags, "relation:intimate");
                AddNormalizedTag(tags, "scene:intimacy");
            }

            if (!tags.Contains("scene:intimacy") && !tags.Contains("scene:conflict"))
            {
                AddNormalizedTag(tags, "scene:daily");
            }
        }

        internal bool TryGetMoodTag(Pawn pawn, out string moodTag)
        {
            moodTag = null;
            if (pawn?.needs?.mood == null)
            {
                return false;
            }

            float mood = pawn.needs.mood.CurLevelPercentage;
            if (mood <= 0.3f)
            {
                moodTag = "mood:low";
            }
            else if (mood >= 0.75f)
            {
                moodTag = "mood:high";
            }
            else
            {
                moodTag = "mood:normal";
            }

            return true;
        }

        internal bool HasIntimateRelation(Pawn first, Pawn second)
        {
            if (first == null || second == null || first.relations == null)
            {
                return false;
            }

            return first.relations.DirectRelationExists(PawnRelationDefOf.Spouse, second)
                || first.relations.DirectRelationExists(PawnRelationDefOf.Fiance, second)
                || first.relations.DirectRelationExists(PawnRelationDefOf.Lover, second);
        }

        internal bool EntryMatchesTags(ScenePromptEntryConfig entry, HashSet<string> normalizedTags)
        {
            if (entry?.MatchTags == null || entry.MatchTags.Count == 0)
            {
                return true;
            }

            foreach (string rawTag in entry.MatchTags)
            {
                string normalized = NormalizeTag(rawTag);
                if (normalized.Length == 0)
                {
                    continue;
                }

                if (!normalizedTags.Contains(normalized))
                {
                    return false;
                }
            }

            return true;
        }

        internal void AddNormalizedTag(HashSet<string> tags, string tag)
        {
            if (tags == null)
            {
                return;
            }

            string normalized = NormalizeTag(tag);
            if (normalized.Length > 0)
            {
                tags.Add(normalized);
            }
        }

        internal string NormalizeTag(string tag)
        {
            return string.IsNullOrWhiteSpace(tag) ? string.Empty : tag.Trim().ToLowerInvariant();
        }


        internal string ResolveRpgPawnPersonaPrompt(Pawn target)
        {
            if (target == null)
            {
                return string.Empty;
            }

            // Workbench priority: if user wrote pure text in character_persona (no template variables),
            // use it directly. Template content (e.g. {{ pawn.personality }}) is skipped to avoid
            // circular dependency where rendering would read back from GameComponent_RPGManager.
            string promptChannel = RimTalkPromptEntryChannelCatalog.RpgDialogue;
            string personaSection = RelationsMod.Settings?.ResolvePromptSectionText(promptChannel, "character_persona");
            if (!string.IsNullOrWhiteSpace(personaSection)
                && personaSection.IndexOf("{{", StringComparison.Ordinal) < 0)
            {
                return personaSection.Trim();
            }

            // Fallback: GameComponent_RPGManager per-pawn persona.
            var rpgManager = GameComponent_RPGManager.Instance ?? Current.Game?.GetComponent<GameComponent_RPGManager>();
            return rpgManager?.GetPawnPersonaPrompt(target) ?? string.Empty;
        }
    }
}
