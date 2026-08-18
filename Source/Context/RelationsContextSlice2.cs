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

using RecentEventIntelItem = Ustas.RimAI.Communication.Relations.Context.RelationsContextAssembler.RecentEventIntelItem;
using RecentEventSelectionResult = Ustas.RimAI.Communication.Relations.Context.RelationsContextAssembler.RecentEventSelectionResult;

namespace Ustas.RimAI.Communication.Relations.Context
{
    internal sealed class RelationsContextSlice2 : RelationsContextAssemblerCollaborator
    {
        internal RelationsContextSlice2(RelationsContextAssembler owner) : base(owner)
        {
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
                string line = Owner.BuildRecentEventIntelLine(item);
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
            return $"- [{category}] {item.Summary} ({Owner.BuildRelativeTickText(item.Tick)})";
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

            lines.Add($"- [EventDigest] omitted={selection.OmittedCount}; total={items.Count}; types={Owner.BuildTypeDigest(items)}");
            lines.Add($"- [EventDigest] topics={Owner.BuildTopicDigest(items)}; trend={Owner.BuildTrendDigest(items)}");
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
                string topic = Owner.ResolveRecentEventTopic(item);
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
            if (Owner.ContainsAnyKeyword(text, "raid", "attack", "battle", "siege", "袭击", "战斗", "围攻"))
            {
                return "conflict";
            }

            if (Owner.ContainsAnyKeyword(text, "died", "death", "killed", "死亡", "阵亡", "葬礼"))
            {
                return "casualty";
            }

            if (Owner.ContainsAnyKeyword(text, "trade", "caravan", "merchant", "交易", "商队", "商船"))
            {
                return "trade";
            }

            if (Owner.ContainsAnyKeyword(text, "quest", "mission", "任务", "委托"))
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
                lines.Add($"Time: {Owner.BuildLocalTimeText(map)}");
            }

            if (switches.IncludeDate)
            {
                lines.Add($"Date: {Owner.BuildLocalDateText(map)}");
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
                lines.Add(Owner.BuildLocationAndTemperatureText(map, focusCell, context));
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
                lines.Add($"Beauty: {Owner.BuildBeautyText(map, focusCell)}");
            }

            if (switches.IncludeCleanliness)
            {
                string cleanliness = Owner.BuildCleanlinessText(map, focusCell);
                if (!string.IsNullOrWhiteSpace(cleanliness))
                {
                    lines.Add($"Cleanliness: {cleanliness}");
                }
            }

            if (switches.IncludeSurroundings)
            {
                string surroundings = Owner.BuildSurroundingsText(map, focusCell, context);
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
            string location = Owner.BuildLocationText(context, map, focusCell);
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
    }
}
