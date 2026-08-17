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
    {        internal void AppendMemoryData(StringBuilder sb, Faction faction)
        {
            if (sb == null)
            {
                return;
            }

            if (host.SnapshotService.TryGetScopedRuntimeSnapshotForFaction(faction, out DiplomacyPromptRuntimeSnapshot snapshot) &&
                !string.IsNullOrWhiteSpace(snapshot.MemoryDataBlock))
            {
                sb.AppendLine(snapshot.MemoryDataBlock);
                return;
            }

            try
            {
                var memoryManager = LeaderMemoryManager.Instance;
                if (memoryManager == null) return;

                var leaderMemory = memoryManager.GetMemory(faction);
                if (leaderMemory == null) return;

                sb.AppendLine();
                sb.AppendLine("=== 记忆与历史数据（动态注入）===");
                sb.AppendLine("以下是你对其他派系的记忆和交互历史，请基于这些信息形成你的态度和决策：");
                sb.AppendLine();

                if (leaderMemory.SignificantEvents != null && leaderMemory.SignificantEvents.Count > 0)
                {
                    sb.AppendLine("【重大事件记忆】");
                    sb.AppendLine("这些事件深刻影响了你对其他派系的看法：");

                    var recentEvents = leaderMemory.SignificantEvents
                        .OrderByDescending(e => e.OccurredTick)
                        .Take(5)
                        .ToList();

                    foreach (var evt in recentEvents)
                    {
                        string eventIcon = host.DiplomacyBuilder.GetEventIcon(evt.EventType);
                        sb.AppendLine($"  {eventIcon} [{host.DiplomacyBuilder.GetEventTypeName(evt.EventType)}] 对 {evt.InvolvedFactionName}: {evt.Description}");
                    }
                    sb.AppendLine();
                }

                if (leaderMemory.FactionMemories != null && leaderMemory.FactionMemories.Count > 0)
                {
                    sb.AppendLine("【派系关系认知】");
                    sb.AppendLine("基于长期交互，你对以下派系形成了印象：");

                    foreach (var memory in leaderMemory.FactionMemories)
                    {
                        if (memory.PositiveInteractions == 0 && memory.NegativeInteractions == 0) continue;

                        string impression = host.DiplomacyBuilder.GetRelationImpression(memory);
                        sb.AppendLine($"  • {memory.FactionName}: {impression}");
                        sb.AppendLine($"    交互记录：{memory.PositiveInteractions} 次正面，{memory.NegativeInteractions} 次负面");

                        if (memory.RelationHistory != null && memory.RelationHistory.Count > 0)
                        {
                            var trend = host.DiplomacyBuilder.GetRelationTrend(memory.RelationHistory);
                            if (!string.IsNullOrEmpty(trend))
                            {
                                sb.AppendLine($"    关系趋势：{trend}");
                            }
                        }
                    }
                    sb.AppendLine();
                }

                List<CrossChannelSummaryRecord> crossSummaries = new List<CrossChannelSummaryRecord>();
                if (leaderMemory.DiplomacySessionSummaries != null)
                {
                    crossSummaries.AddRange(leaderMemory.DiplomacySessionSummaries);
                }
                if (leaderMemory.RpgDepartSummaries != null)
                {
                    crossSummaries.AddRange(leaderMemory.RpgDepartSummaries);
                }

                if (crossSummaries.Count > 0)
                {
                    sb.AppendLine("【跨通道长期记忆】");
                    sb.AppendLine("来自外交会话与 RPG 离图事件的共享摘要：");

                    foreach (CrossChannelSummaryRecord summary in crossSummaries
                        .Where(x => x != null && !string.IsNullOrWhiteSpace(x.SummaryText))
                        .OrderByDescending(x => x.GameTick)
                        .Take(6))
                    {
                        string sourceLabel = summary.Source == CrossChannelSummarySource.DiplomacySession
                            ? "外交会话"
                            : "RPG离图";
                        sb.AppendLine($"  • [{sourceLabel}] {summary.SummaryText}");

                        if (summary.KeyFacts != null && summary.KeyFacts.Count > 0)
                        {
                            string facts = string.Join("；", summary.KeyFacts.Take(2));
                            if (!string.IsNullOrWhiteSpace(facts))
                            {
                                sb.AppendLine($"    关键点：{facts}");
                            }
                        }
                    }
                    sb.AppendLine();
                }

                sb.AppendLine("【记忆使用指导】");
                sb.AppendLine("- 对有过负面交互的派系保持警惕和怀疑");
                sb.AppendLine("- 对有过正面交互的派系更加友好和信任");
                sb.AppendLine("- 重大事件（如宣战、背叛）应该深刻影响你的态度");
                sb.AppendLine("- 基于历史形成连贯一致的外交策略");
                sb.AppendLine();
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimAI.Relations] 注入记忆数据失败：{ex.Message}");
            }
        }

        internal void AppendFactionInfo(StringBuilder sb, Faction faction)
        {
            if (sb == null || faction == null)
            {
                return;
            }

            if (host.SnapshotService.TryGetScopedRuntimeSnapshotForFaction(faction, out DiplomacyPromptRuntimeSnapshot snapshot) &&
                !string.IsNullOrWhiteSpace(snapshot.FactionInfoBlock))
            {
                sb.AppendLine(snapshot.FactionInfoBlock);
                return;
            }

            sb.AppendLine();
            sb.AppendLine($"=== FACTION INFO ===");
            sb.AppendLine($"Name: {faction.Name}");
            sb.AppendLine($"Type: {faction.def?.label ?? "Unknown"}");
            if (!faction.IsPlayer)
            {
                sb.AppendLine($"Current Goodwill: {faction.PlayerGoodwill}");
                sb.AppendLine($"Relation: {host.DiplomacyBuilder.GetRelationLabel(faction.PlayerGoodwill)}");
            }
            else
            {
                sb.AppendLine("Current Faction: Player Colony (Self)");
            }

            if (faction.leader != null)
            {
                sb.AppendLine($"Leader: {faction.leader.Name?.ToStringFull ?? "Unknown"}");

                if (faction.leader.story?.traits?.allTraits != null)
                {
                    var traits = faction.leader.story.traits.allTraits;
                    if (traits.Count > 0)
                    {
                        sb.AppendLine($"Leader Traits: {string.Join(", ", traits.Select(t => t.Label))}");
                    }
                }
            }

            if (faction.ideos?.PrimaryIdeo != null)
            {
                sb.AppendLine($"Ideology: {faction.ideos.PrimaryIdeo.name}");
            }
        }

        internal Pawn ResolveBestPlayerNegotiator(Pawn preferredNegotiator)
        {
            if (IsEligiblePlayerNegotiator(preferredNegotiator))
            {
                return preferredNegotiator;
            }

            var candidates = new List<Pawn>();
            IEnumerable<Map> maps = Find.Maps ?? Enumerable.Empty<Map>();
            foreach (Map map in maps.Where(m => m != null && m.IsPlayerHome))
            {
                if (map.mapPawns?.FreeColonistsSpawned == null)
                {
                    continue;
                }

                foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
                {
                    if (IsEligiblePlayerNegotiator(pawn) && !candidates.Contains(pawn))
                    {
                        candidates.Add(pawn);
                    }
                }
            }

            if (candidates.Count == 0)
            {
                foreach (Pawn pawn in PawnsFinder.AllMapsWorldAndTemporary_Alive)
                {
                    if (IsEligiblePlayerNegotiator(pawn) && !candidates.Contains(pawn))
                    {
                        candidates.Add(pawn);
                    }
                }
            }

            return candidates
                .OrderByDescending(GetPawnSocialSkillLevel)
                .ThenBy(pawn => pawn.Name?.ToStringShort ?? pawn.LabelShortCap)
                .FirstOrDefault();
        }

        internal string BuildPlayerPawnContextForPrompt(Faction faction, Pawn preferredNegotiator, int maxChars = 900)
        {
            if (host.SnapshotService.TryGetScopedRuntimeSnapshotForFaction(faction, out DiplomacyPromptRuntimeSnapshot snapshot))
            {
                return snapshot.PlayerPawnProfileBlock ?? string.Empty;
            }

            Pawn selected = ResolveBestPlayerNegotiator(preferredNegotiator);
            if (selected == null)
            {
                return string.Empty;
            }

            bool explicitNegotiator = ReferenceEquals(selected, preferredNegotiator);
            string source = explicitNegotiator ? "explicit_negotiator" : "fallback_highest_social_colonist";
            int social = GetPawnSocialSkillLevel(selected);
            string traits = selected.story?.traits?.allTraits == null
                ? "none"
                : string.Join(", ", selected.story.traits.allTraits.Select(t => t.Label).Take(6));

            var sb = new StringBuilder();
            sb.AppendLine("=== PLAYER PAWN PROFILE (REFERENCE ONLY) ===");
            sb.AppendLine($"Source: {source}");
            sb.AppendLine($"Name: {selected.Name?.ToStringFull ?? selected.LabelShort}");
            sb.AppendLine($"Kind: {selected.KindLabel}");
            sb.AppendLine($"Gender: {selected.gender}");
            sb.AppendLine($"Age: {selected.ageTracker?.AgeBiologicalYears}");
            sb.AppendLine($"SocialSkill: {social}");
            sb.AppendLine($"Traits: {traits}");
            if (faction != null && !faction.IsPlayer)
            {
                sb.AppendLine($"TargetFactionRelation: goodwill={faction.PlayerGoodwill} ({host.DiplomacyBuilder.GetRelationLabel(faction.PlayerGoodwill)})");
            }
            sb.AppendLine("Usage: treat this as player-side capability context only. Never switch identity.");
            return ClampPromptBlock(sb.ToString(), maxChars);
        }

        internal string BuildPlayerRoyaltySummaryForPrompt(Faction faction, Pawn preferredNegotiator, int maxChars = 1400)
        {
            if (host.SnapshotService.TryGetScopedRuntimeSnapshotForFaction(faction, out DiplomacyPromptRuntimeSnapshot snapshot))
            {
                return snapshot.PlayerRoyaltySummaryBlock ?? string.Empty;
            }

            if (!ModsConfig.RoyaltyActive || faction == null || faction.def != FactionDefOf.Empire)
            {
                return string.Empty;
            }

            Pawn selected = ResolveBestPlayerNegotiator(preferredNegotiator);
            if (selected == null)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            sb.AppendLine("=== PLAYER EMPIRE ROYALTY CONSTRAINTS ===");
            sb.AppendLine($"ReferencePawn: {selected.Name?.ToStringFull ?? selected.LabelShort}");

            if (selected.royalty == null)
            {
                sb.AppendLine("Honor: unavailable (no royalty tracker)");
                sb.AppendLine("HighestTitle: none");
                sb.AppendLine("Permits: none");
                sb.AppendLine("UnavailableReasons: no empire title context on the player side.");
                sb.AppendLine("EmpireActionGuidance: avoid empire-sensitive create_quest/request_aid; decline in-character and suggest goodwill-building alternatives.");
                return ClampPromptBlock(sb.ToString(), maxChars);
            }

            int honor = selected.royalty.GetFavor(faction);
            RoyalTitle highestTitle = selected.royalty.GetCurrentTitleInFaction(faction);
            if (highestTitle == null && selected.royalty.AllTitlesInEffectForReading != null)
            {
                highestTitle = selected.royalty.AllTitlesInEffectForReading
                    .Where(title => title != null && title.faction == faction)
                    .OrderByDescending(title => title.def?.seniority ?? int.MinValue)
                    .FirstOrDefault();
            }

            string titleLabel = highestTitle?.Label ?? "none";
            List<FactionPermit> permits = selected.royalty.AllFactionPermits == null
                ? new List<FactionPermit>()
                : selected.royalty.AllFactionPermits
                    .Where(permit => permit != null && permit.Faction == faction)
                    .ToList();
            int readyPermits = permits.Count(permit => !permit.OnCooldown);
            int cooldownPermits = permits.Count - readyPermits;

            var blockedReasons = new List<string>();
            if (titleLabel == "none")
            {
                blockedReasons.Add("no active empire title in the current faction");
            }
            if (readyPermits == 0)
            {
                blockedReasons.Add("no ready empire permit");
            }

            sb.AppendLine($"Honor: {honor}");
            sb.AppendLine($"HighestTitle: {titleLabel}");
            sb.AppendLine($"Permits: total={permits.Count}, ready={readyPermits}, cooldown={cooldownPermits}");
            sb.AppendLine($"PermitSamples: {BuildPermitSummaryText(permits)}");
            if (blockedReasons.Count > 0)
            {
                sb.AppendLine($"UnavailableReasons: {string.Join(" | ", blockedReasons)}");
            }
            sb.AppendLine("EmpireActionGuidance:");
            sb.AppendLine("- For empire-sensitive create_quest templates (bestowing/royal paths), require matching title and honor context.");
            sb.AppendLine("- If no suitable title/honor context exists, refuse create_quest in-character and offer alternatives.");
            sb.AppendLine("- If permits are missing or on cooldown, avoid request_aid and explain authority/logistics constraints.");
            sb.AppendLine("- Runtime eligibility remains authoritative; this block is a soft prompt policy.");
            return ClampPromptBlock(sb.ToString(), maxChars);
        }

        internal string BuildFactionSettlementSummaryForPrompt(Faction faction, int maxChars = 0)
        {
            if (host.SnapshotService.TryGetScopedRuntimeSnapshotForFaction(faction, out DiplomacyPromptRuntimeSnapshot snapshot))
            {
                return snapshot.FactionSettlementSummaryBlock ?? string.Empty;
            }

            if (faction == null)
            {
                return string.Empty;
            }

            List<WorldObject> bases = GetFactionBaseWorldObjects(faction);

            var sb = new StringBuilder();
            sb.AppendLine("=== FACTION SETTLEMENT SUMMARY ===");
            sb.AppendLine($"Faction: {faction.Name}");
            sb.AppendLine($"SettlementCount: {bases.Count}");

            if (bases.Count == 0)
            {
                sb.AppendLine("AllSettlements: none");
                sb.AppendLine("SettlementActionGuidance: avoid settlement-dependent trade/quest actions and explain constraints in-character.");
                return ClampPromptBlock(sb.ToString(), maxChars);
            }

            Map homeMap = Find.AnyPlayerHomeMap
                ?? Find.Maps?.FirstOrDefault(map => map != null && map.IsPlayerHome);
            IEnumerable<WorldObject> orderedBases = OrderFactionBasesByDistance(bases, homeMap);
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
            return ClampPromptBlock(sb.ToString(), maxChars);
        }

        internal string BuildFactionQuestStatusBlockForPrompt(Faction faction, int maxChars = 1600)
        {
            if (host.SnapshotService.TryGetScopedRuntimeSnapshotForFaction(faction, out DiplomacyPromptRuntimeSnapshot snapshot))
            {
                return snapshot.FactionQuestStatusBlock ?? string.Empty;
            }

            if (faction == null)
            {
                return string.Empty;
            }

            GameAIInterface.Instance.RefreshQuestTrackingState();
            var sb = new StringBuilder();
            AppendFactionQuestStatus(sb, faction);
            return ClampPromptBlock(sb.ToString(), maxChars);
        }

        internal void AppendFactionQuestStatus(StringBuilder sb, Faction faction)
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
                    string questName = ResolveQuestPromptName(quest);
                    string questDescription = ResolveQuestPromptDescription(quest);
                    sb.AppendLine($"- {questName}: {questDescription}");
                }
            }

            if (latestCompletion == null)
            {
                sb.AppendLine("LatestFinishedTask: none");
                return;
            }

            sb.AppendLine("LatestFinishedTask:");
            sb.AppendLine($"- Name: {NormalizePromptInlineText(latestCompletion.QuestName, latestCompletion.QuestDefName)}");
            sb.AppendLine($"- Detail: {NormalizePromptInlineText(latestCompletion.QuestDescription, "none")}");
            sb.AppendLine($"- Time: {FormatQuestTickForPrompt(latestCompletion.EndedTick)}");
            sb.AppendLine($"- Result: {(latestCompletion.Succeeded ? "success" : "failure")}");
        }

        internal string ResolveQuestPromptName(Quest quest)
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

        internal string ResolveQuestPromptDescription(Quest quest)
        {
            string description = quest?.description ?? string.Empty;
            return NormalizePromptInlineText(description, "none");
        }

        internal string NormalizePromptInlineText(string value, string fallback)
        {
            string normalized = (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
        }

        internal string FormatQuestTickForPrompt(int gameTick)
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

        internal List<WorldObject> GetFactionBaseWorldObjects(Faction faction)
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

        internal IEnumerable<WorldObject> OrderFactionBasesByDistance(List<WorldObject> bases, Map homeMap)
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

        internal bool IsEligiblePlayerNegotiator(Pawn pawn)
        {
            return pawn != null
                && pawn.Faction == Faction.OfPlayer
                && PawnDialogueRoutingPolicy.IsRpgDialogueEligibleRace(pawn)
                && !pawn.Dead
                && !pawn.Destroyed;
        }

        internal int GetPawnSocialSkillLevel(Pawn pawn)
        {
            return pawn?.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
        }

        internal string BuildPermitSummaryText(List<FactionPermit> permits)
        {
            if (permits == null || permits.Count == 0)
            {
                return "none";
            }

            return string.Join(", ", permits
                .Take(4)
                .Select(FormatPermitSummaryItem));
        }

        internal string FormatPermitSummaryItem(FactionPermit permit)
        {
            if (permit?.Permit == null)
            {
                return "unknown";
            }

            string state = permit.OnCooldown ? "cooldown" : "ready";
            string title = permit.Title?.label ?? "no_title";
            return $"{permit.Permit.LabelCap} [{state}] (title:{title})";
        }

        internal string ClampPromptBlock(string text, int maxChars)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string trimmed = text.Trim();
            if (maxChars <= 0 || trimmed.Length <= maxChars)
            {
                return trimmed;
            }

            return trimmed.Substring(0, maxChars).TrimEnd() + "...";
        }
    }
}
