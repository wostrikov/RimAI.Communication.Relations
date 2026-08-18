using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Communication.Relations.Guards;
using Ustas.RimAI.Communication.Relations.Module;

namespace Ustas.RimAI.Communication.Relations.Context
{
    /// <summary>
    /// Faction settlement/quest prompt block builders for world context assembly.
    /// </summary>
    internal static class RelationsContextAssemblerQuestOps
    {
        internal static string BuildFactionSettlementSummaryForPrompt(RelationsContextAssemblerWorld owner, Faction faction, int maxChars = 0)
        {
            if (owner.Owner.host.SnapshotService.TryGetScopedRuntimeSnapshotForFaction(faction, out DiplomacyPromptRuntimeSnapshot snapshot))
            {
                return snapshot.FactionSettlementSummaryBlock ?? string.Empty;
            }

            if (faction == null)
            {
                return string.Empty;
            }

            List<WorldObject> bases = GetFactionBaseWorldObjects(owner, faction);

            var sb = new StringBuilder();
            sb.AppendLine("=== FACTION SETTLEMENT SUMMARY ===");
            sb.AppendLine($"Faction: {faction.Name}");
            sb.AppendLine($"SettlementCount: {bases.Count}");

            if (bases.Count == 0)
            {
                sb.AppendLine("AllSettlements: none");
                sb.AppendLine("SettlementActionGuidance: avoid settlement-dependent trade/quest actions and explain constraints in-character.");
                return owner.ClampPromptBlock(sb.ToString(), maxChars);
            }

            Map homeMap = Find.AnyPlayerHomeMap
                ?? Find.Maps?.FirstOrDefault(map => map != null && map.IsPlayerHome);
            IEnumerable<WorldObject> orderedBases = OrderFactionBasesByDistance(owner, bases, homeMap);
            WorldObject nearest = null;

            if (homeMap != null && WorldTileGuard.IsValidTile(homeMap.Tile))
            {
                nearest = orderedBases.FirstOrDefault();
                if (nearest != null)
                {
                    int distance = Mathf.RoundToInt(
                        Find.WorldGrid.ApproxDistanceInTiles(homeMap.Tile, nearest.Tile));
                    sb.AppendLine($"NearestToPlayerHome: {nearest.LabelCap} ({distance} tiles)");
                }
            }

            string names = string.Join(", ", orderedBases.Select(obj => obj.LabelCap));
            sb.AppendLine($"AllSettlements: {names}");
            sb.AppendLine("SettlementActionGuidance: settlement-backed actions are allowed only when this summary indicates viable settlement presence.");
            return owner.ClampPromptBlock(sb.ToString(), maxChars);
        }

        internal static string BuildFactionQuestStatusBlockForPrompt(RelationsContextAssemblerWorld owner, Faction faction, int maxChars = 1600)
        {
            if (owner.Owner.host.SnapshotService.TryGetScopedRuntimeSnapshotForFaction(faction, out DiplomacyPromptRuntimeSnapshot snapshot))
            {
                return snapshot.FactionQuestStatusBlock ?? string.Empty;
            }

            if (faction == null)
            {
                return string.Empty;
            }

            GameAIInterface.Instance.RefreshQuestTrackingState();
            var sb = new StringBuilder();
            AppendFactionQuestStatus(owner, sb, faction);
            return owner.ClampPromptBlock(sb.ToString(), maxChars);
        }

        internal static void AppendFactionQuestStatus(RelationsContextAssemblerWorld owner, StringBuilder sb, Faction faction)
        {
            if (sb == null || faction == null)
            {
                return;
            }

            FactionQuestAvailabilityReport report = ApiActionEligibilityService.Instance.GetFactionQuestAvailabilityReport(faction, null);
            List<string> availableQuestNames = report?.AllowedQuestDefs?
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            List<Quest> ongoingQuests = (Find.QuestManager?.QuestsListForReading ?? new List<Quest>())
                .Where(quest => quest != null && quest.State == QuestState.Ongoing)
                .Where(quest => QuestInvolvedFactionsGuard.HasInvolvedFaction(quest, faction))
                .OrderByDescending(quest => quest.id)
                .ToList();

            RelationsFactionQuestCompletionRecord latestCompletion = GameAIInterface.Instance.GetLatestCompletedQuestForFaction(faction);
            sb.AppendLine("=== FACTION QUEST STATUS ===");
            sb.AppendLine($"Faction: {faction.Name}");
            sb.AppendLine($"AvailableTasks: {(availableQuestNames.Count == 0 ? "none" : string.Join(", ", availableQuestNames))}");

            if (ongoingQuests.Count == 0)
            {
                sb.AppendLine("OngoingTasks: none");
            }
            else
            {
                sb.AppendLine("OngoingTasks:");
                foreach (Quest quest in ongoingQuests.Take(6))
                {
                    string questName = ResolveQuestPromptName(owner, quest);
                    string questDescription = ResolveQuestPromptDescription(owner, quest);
                    sb.AppendLine($"- {questName}: {questDescription}");
                }
            }

            if (latestCompletion == null)
            {
                sb.AppendLine("LatestFinishedTask: none");
                return;
            }

            sb.AppendLine("LatestFinishedTask:");
            sb.AppendLine($"- Name: {NormalizePromptInlineText(owner, latestCompletion.QuestName, latestCompletion.QuestDefName)}");
            sb.AppendLine($"- Detail: {NormalizePromptInlineText(owner, latestCompletion.QuestDescription, "none")}");
            sb.AppendLine($"- Time: {FormatQuestTickForPrompt(owner, latestCompletion.EndedTick)}");
            sb.AppendLine($"- Result: {(latestCompletion.Succeeded ? "success" : "failure")}");
        }

        internal static string ResolveQuestPromptName(RelationsContextAssemblerWorld owner, Quest quest)
        {
            if (!string.IsNullOrWhiteSpace(quest?.name))
            {
                return quest.name.Trim();
            }

            string rootTypeName = quest?.root?.GetType().Name;
            if (!string.IsNullOrWhiteSpace(rootTypeName))
            {
                return rootTypeName.Trim();
            }

            return "UnknownQuest";
        }

        internal static string ResolveQuestPromptDescription(RelationsContextAssemblerWorld owner, Quest quest)
        {
            string description = quest?.description ?? string.Empty;
            return NormalizePromptInlineText(owner, description, "none");
        }

        internal static string NormalizePromptInlineText(RelationsContextAssemblerWorld owner, string value, string fallback)
        {
            string normalized = (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
        }

        internal static string FormatQuestTickForPrompt(RelationsContextAssemblerWorld owner, int gameTick)
        {
            if (gameTick <= 0)
            {
                return "unknown";
            }

            int ticksAbs = Find.TickManager?.TicksAbs ?? gameTick;
            int homeTile = Find.AnyPlayerHomeMap?.Tile ?? -1;
            if (!WorldTileGuard.IsValidTile(homeTile))
            {
                return $"Year {GenDate.Year(ticksAbs, 0f) + 1}";
            }
            return GenDate.DateFullStringAt(ticksAbs, Find.WorldGrid.LongLatOf(homeTile));
        }

        internal static List<WorldObject> GetFactionBaseWorldObjects(RelationsContextAssemblerWorld owner, Faction faction)
        {
            var result = new List<WorldObject>();
            if (faction == null)
            {
                return result;
            }

            WorldObjectsHolder holder = Find.WorldObjects;
            if (holder == null)
            {
                return result;
            }

            List<WorldObject> allObjects = holder.AllWorldObjects;
            if (allObjects == null || allObjects.Count == 0)
            {
                return result;
            }

            foreach (WorldObject obj in allObjects)
            {
                if (obj == null || obj.Destroyed || obj.Faction != faction
                    || !WorldTileGuard.IsValidTile(obj.Tile))
                {
                    continue;
                }

                MapParent parent = obj as MapParent;
                if (parent == null)
                {
                    if (obj is Settlement)
                    {
                        result.Add(obj);
                    }

                    continue;
                }

                WorldObjectDef def = parent.def;
                if (def == null)
                {
                    continue;
                }

                if (def.worldObjectClass != null &&
                    def.worldObjectClass.Name.IndexOf("Incident", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                // RimWorld 1.6: canGenerateSourceRect was removed; approximate with settlement-like objects.
                if (parent is Settlement || def.worldObjectClass == typeof(Settlement))
                {
                    result.Add(obj);
                }
            }

            return result;
        }

        internal static IEnumerable<WorldObject> OrderFactionBasesByDistance(RelationsContextAssemblerWorld owner, List<WorldObject> bases, Map homeMap)
        {
            if (bases == null || bases.Count == 0)
            {
                return Enumerable.Empty<WorldObject>();
            }

            bool hasDistance = homeMap != null && WorldTileGuard.IsValidTile(homeMap.Tile);
            if (!hasDistance)
            {
                return bases
                    .OrderBy(b => b?.LabelCap)
                    .ToList();
            }

            return bases
                .OrderBy(b => Find.WorldGrid.ApproxDistanceInTiles(homeMap.Tile, b.Tile))
                .ThenBy(b => b?.LabelCap)
                .ToList();
        }
    }
}
