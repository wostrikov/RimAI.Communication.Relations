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
        internal sealed class RelationsContextAssemblerWorld : RelationsContextAssemblerCollaborator
    {
        internal RelationsContextAssemblerWorld(RelationsContextAssembler owner) : base(owner)
        {
        }

        internal void AppendMemoryData(StringBuilder sb, Faction faction)
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
                sb.AppendLine("=== Памʼять та історичні дані (динамічне вставлення) ===");
                sb.AppendLine("Нижче — твоя памʼять і історія взаємодій з іншими фракціями; формуй своє ставлення й рішення на цій основі:");
                sb.AppendLine();

                if (leaderMemory.SignificantEvents != null && leaderMemory.SignificantEvents.Count > 0)
                {
                    sb.AppendLine("[Памʼять про великі події]");
                    sb.AppendLine("Ці події глибоко вплинули на твій погляд на інші фракції:");

                    var recentEvents = leaderMemory.SignificantEvents
                        .OrderByDescending(e => e.OccurredTick)
                        .Take(5)
                        .ToList();

                    foreach (var evt in recentEvents)
                    {
                        string eventIcon = host.DiplomacyBuilder.GetEventIcon(evt.EventType);
                        sb.AppendLine($"  {eventIcon} [{host.DiplomacyBuilder.GetEventTypeName(evt.EventType)}] щодо {evt.InvolvedFactionName}: {evt.Description}");
                    }
                    sb.AppendLine();
                }

                if (leaderMemory.FactionMemories != null && leaderMemory.FactionMemories.Count > 0)
                {
                    sb.AppendLine("[Уявлення про відносини фракцій]");
                    sb.AppendLine("За тривалою взаємодією в тебе склалося враження про такі фракції:");

                    foreach (var memory in leaderMemory.FactionMemories)
                    {
                        if (memory.PositiveInteractions == 0 && memory.NegativeInteractions == 0) continue;

                        string impression = host.DiplomacyBuilder.GetRelationImpression(memory);
                        sb.AppendLine($"  • {memory.FactionName}: {impression}");
                        sb.AppendLine($"    Записи взаємодій: {memory.PositiveInteractions} позитивних, {memory.NegativeInteractions} негативних");

                        if (memory.RelationHistory != null && memory.RelationHistory.Count > 0)
                        {
                            var trend = host.DiplomacyBuilder.GetRelationTrend(memory.RelationHistory);
                            if (!string.IsNullOrEmpty(trend))
                            {
                                sb.AppendLine($"    Тенденція відносин: {trend}");
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
                    sb.AppendLine("[Довготривала памʼять між каналами]");
                    sb.AppendLine("Спільний переказ з дипломатичних сесій і подій RPG поза мапою:");

                    foreach (CrossChannelSummaryRecord summary in crossSummaries
                        .Where(x => x != null && !string.IsNullOrWhiteSpace(x.SummaryText))
                        .OrderByDescending(x => x.GameTick)
                        .Take(6))
                    {
                        string sourceLabel = summary.Source == CrossChannelSummarySource.DiplomacySession
                            ? "Дипломатична сесія"
                            : "RPG поза мапою";
                        sb.AppendLine($"  • [{sourceLabel}] {summary.SummaryText}");

                        if (summary.KeyFacts != null && summary.KeyFacts.Count > 0)
                        {
                            string facts = string.Join("；", summary.KeyFacts.Take(2));
                            if (!string.IsNullOrWhiteSpace(facts))
                            {
                                sb.AppendLine($"    Ключове: {facts}");
                            }
                        }
                    }
                    sb.AppendLine();
                }

                sb.AppendLine("[Настанови щодо використання памʼяті]");
                sb.AppendLine("- До фракцій з негативним досвідом взаємодії лишайся насторожі й з підозрою");
                sb.AppendLine("- До фракцій з позитивним досвідом взаємодії стався привітніше й довірливіше");
                sb.AppendLine("- Великі події (оголошення війни, зрада) мають глибоко впливати на твоє ставлення");
                sb.AppendLine("- Формуй звʼязну й послідовну дипломатичну лінію на основі історії");
                sb.AppendLine();
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimAI.Relations] Не вдалося вставити дані памʼяті: {ex.Message}");
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
            if (Owner.IsEligiblePlayerNegotiator(preferredNegotiator))
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
                    if (Owner.IsEligiblePlayerNegotiator(pawn) && !candidates.Contains(pawn))
                    {
                        candidates.Add(pawn);
                    }
                }
            }

            if (candidates.Count == 0)
            {
                foreach (Pawn pawn in PawnsFinder.AllMapsWorldAndTemporary_Alive)
                {
                    if (Owner.IsEligiblePlayerNegotiator(pawn) && !candidates.Contains(pawn))
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

            Pawn selected = Owner.ResolveBestPlayerNegotiator(preferredNegotiator);
            if (selected == null)
            {
                return string.Empty;
            }

            bool explicitNegotiator = ReferenceEquals(selected, preferredNegotiator);
            string source = explicitNegotiator ? "explicit_negotiator" : "fallback_highest_social_colonist";
            int social = Owner.GetPawnSocialSkillLevel(selected);
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
            return Owner.ClampPromptBlock(sb.ToString(), maxChars);
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

            Pawn selected = Owner.ResolveBestPlayerNegotiator(preferredNegotiator);
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
                return Owner.ClampPromptBlock(sb.ToString(), maxChars);
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
            sb.AppendLine($"PermitSamples: {Owner.BuildPermitSummaryText(permits)}");
            if (blockedReasons.Count > 0)
            {
                sb.AppendLine($"UnavailableReasons: {string.Join(" | ", blockedReasons)}");
            }
            sb.AppendLine("EmpireActionGuidance:");
            sb.AppendLine("- For empire-sensitive create_quest templates (bestowing/royal paths), require matching title and honor context.");
            sb.AppendLine("- If no suitable title/honor context exists, refuse create_quest in-character and offer alternatives.");
            sb.AppendLine("- If permits are missing or on cooldown, avoid request_aid and explain authority/logistics constraints.");
            sb.AppendLine("- Runtime eligibility remains authoritative; this block is a soft prompt policy.");
            return Owner.ClampPromptBlock(sb.ToString(), maxChars);
        }

        internal string BuildFactionSettlementSummaryForPrompt(Faction faction, int maxChars = 0) => RelationsContextAssemblerQuestOps.BuildFactionSettlementSummaryForPrompt(this, faction, 0);
        internal string BuildFactionQuestStatusBlockForPrompt(Faction faction, int maxChars = 1600) => RelationsContextAssemblerQuestOps.BuildFactionQuestStatusBlockForPrompt(this, faction, 1600);
        internal void AppendFactionQuestStatus(StringBuilder sb, Faction faction) => RelationsContextAssemblerQuestOps.AppendFactionQuestStatus(this, sb, faction);
        internal string ResolveQuestPromptName(Quest quest) => RelationsContextAssemblerQuestOps.ResolveQuestPromptName(this, quest);
        internal string ResolveQuestPromptDescription(Quest quest) => RelationsContextAssemblerQuestOps.ResolveQuestPromptDescription(this, quest);
        internal string NormalizePromptInlineText(string value, string fallback) => RelationsContextAssemblerQuestOps.NormalizePromptInlineText(this, value, fallback);
        internal string FormatQuestTickForPrompt(int gameTick) => RelationsContextAssemblerQuestOps.FormatQuestTickForPrompt(this, gameTick);
        internal List<WorldObject> GetFactionBaseWorldObjects(Faction faction) => RelationsContextAssemblerQuestOps.GetFactionBaseWorldObjects(this, faction);
        internal IEnumerable<WorldObject> OrderFactionBasesByDistance(List<WorldObject> bases, Map homeMap) => RelationsContextAssemblerQuestOps.OrderFactionBasesByDistance(this, bases, homeMap);
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
