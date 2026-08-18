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
    internal sealed class RelationsContextAssembler
    {
        internal RelationsContextAssemblerParts Parts;
        internal const int ExpandMemoryPawnMemoryMaxCharsDefault = RelationsContextAssemblerExpandMemory.ExpandMemoryPawnMemoryMaxCharsDefault;
        internal const int ExpandMemoryPawnMemoryMaxCharsMin = RelationsContextAssemblerExpandMemory.ExpandMemoryPawnMemoryMaxCharsMin;
        internal const int ExpandMemoryPawnMemoryMaxCharsMax = RelationsContextAssemblerExpandMemory.ExpandMemoryPawnMemoryMaxCharsMax;
        internal const int ExpandMemoryPawnMemoryMaxEntriesDefault = RelationsContextAssemblerExpandMemory.ExpandMemoryPawnMemoryMaxEntriesDefault;
        internal const int ExpandMemoryPawnMemoryMaxEntriesMin = RelationsContextAssemblerExpandMemory.ExpandMemoryPawnMemoryMaxEntriesMin;
        internal const int ExpandMemoryPawnMemoryMaxEntriesMax = RelationsContextAssemblerExpandMemory.ExpandMemoryPawnMemoryMaxEntriesMax;
        internal const int ExpandMemoryPawnMemoryMaxEntriesPerLayer = RelationsContextAssemblerExpandMemory.ExpandMemoryPawnMemoryMaxEntriesPerLayer;
        internal readonly PromptPersistenceService host;

        internal RelationsContextAssembler(PromptPersistenceService host)
        {
            this.host = host ?? throw new System.ArgumentNullException(nameof(host));
            Parts = new RelationsContextAssemblerParts(this);
        }
        internal string BuildEnvironmentPromptBlocks(SystemPromptConfig config, DialogueScenarioContext context)
        {
            return BuildEnvironmentPromptBlocksInternal(config, context, null);
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

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        internal string NormalizeTag(string tag)
        {
            return string.IsNullOrWhiteSpace(tag) ? string.Empty : tag.Trim().ToLowerInvariant();
        }


        

        #region Facade forwards
        internal string BuildCommonKnowledgeBlock(string playerMessage) => Parts.ExpandMemory.BuildCommonKnowledgeBlock(playerMessage);
        internal string BuildExpandMemoryPawnBlock(Pawn pawn) => Parts.ExpandMemory.BuildExpandMemoryPawnBlock(pawn);
        internal string BuildExpandMemoryPawnBlock(Pawn pawn, int maxChars, int maxTotalEntries) => Parts.ExpandMemory.BuildExpandMemoryPawnBlock(pawn, maxChars, maxTotalEntries);
        internal string TruncateAtNaturalBoundary(string text, int maxChars) => Parts.ExpandMemory.TruncateAtNaturalBoundary(text, maxChars);
        internal string InjectExpandMemoryIntoPrompt(string prompt, Pawn target) => Parts.ExpandMemory.InjectExpandMemoryIntoPrompt(prompt, target);
        internal void AppendMemoryData(StringBuilder sb, Faction faction) => Parts.World.AppendMemoryData(sb, faction);
        internal void AppendFactionInfo(StringBuilder sb, Faction faction) => Parts.World.AppendFactionInfo(sb, faction);
        internal Pawn ResolveBestPlayerNegotiator(Pawn preferredNegotiator) => Parts.World.ResolveBestPlayerNegotiator(preferredNegotiator);
        internal string BuildPlayerPawnContextForPrompt(Faction faction, Pawn preferredNegotiator, int maxChars = 900) => Parts.World.BuildPlayerPawnContextForPrompt(faction, preferredNegotiator, maxChars);
        internal string BuildPlayerRoyaltySummaryForPrompt(Faction faction, Pawn preferredNegotiator, int maxChars = 1400) => Parts.World.BuildPlayerRoyaltySummaryForPrompt(faction, preferredNegotiator, maxChars);
        internal string BuildFactionSettlementSummaryForPrompt(Faction faction, int maxChars = 0) => Parts.World.BuildFactionSettlementSummaryForPrompt(faction, maxChars);
        internal string BuildFactionQuestStatusBlockForPrompt(Faction faction, int maxChars = 1600) => Parts.World.BuildFactionQuestStatusBlockForPrompt(faction, maxChars);
        internal void AppendFactionQuestStatus(StringBuilder sb, Faction faction) => Parts.World.AppendFactionQuestStatus(sb, faction);
        internal string ResolveQuestPromptName(Quest quest) => Parts.World.ResolveQuestPromptName(quest);
        internal string ResolveQuestPromptDescription(Quest quest) => Parts.World.ResolveQuestPromptDescription(quest);
        internal string NormalizePromptInlineText(string value, string fallback) => Parts.World.NormalizePromptInlineText(value, fallback);
        internal string FormatQuestTickForPrompt(int gameTick) => Parts.World.FormatQuestTickForPrompt(gameTick);
        internal List<WorldObject> GetFactionBaseWorldObjects(Faction faction) => Parts.World.GetFactionBaseWorldObjects(faction);
        internal IEnumerable<WorldObject> OrderFactionBasesByDistance(List<WorldObject> bases, Map homeMap) => Parts.World.OrderFactionBasesByDistance(bases, homeMap);
        internal bool IsEligiblePlayerNegotiator(Pawn pawn) => Parts.World.IsEligiblePlayerNegotiator(pawn);
        internal int GetPawnSocialSkillLevel(Pawn pawn) => Parts.World.GetPawnSocialSkillLevel(pawn);
        internal string BuildPermitSummaryText(List<FactionPermit> permits) => Parts.World.BuildPermitSummaryText(permits);
        internal string FormatPermitSummaryItem(FactionPermit permit) => Parts.World.FormatPermitSummaryItem(permit);
        internal string ClampPromptBlock(string text, int maxChars) => Parts.World.ClampPromptBlock(text, maxChars);
        #endregion
    
        #region Cluster forwards
        internal string BuildEnvironmentPromptBlocksWithDiagnostics(SystemPromptConfig config, DialogueScenarioContext context, out EnvironmentPromptBuildDiagnostics diagnostics) => Parts.Slice1.BuildEnvironmentPromptBlocksWithDiagnostics(config, context, out diagnostics);
        internal string BuildEnvironmentPromptBlocksInternal(SystemPromptConfig config, DialogueScenarioContext context, EnvironmentPromptBuildDiagnostics diagnostics) => Parts.Slice1.BuildEnvironmentPromptBlocksInternal(config, context, diagnostics);
        internal void AppendEnvironmentContextBlock(StringBuilder sb, EnvironmentPromptConfig env, DialogueScenarioContext context) => Parts.Slice1.AppendEnvironmentContextBlock(sb, env, context);
        internal void AppendRecentWorldEventIntel(StringBuilder sb, EnvironmentPromptConfig env, DialogueScenarioContext context) => Parts.Slice1.AppendRecentWorldEventIntel(sb, env, context);
        internal string BuildRecentWorldEventIntelCompactDigest(EnvironmentPromptConfig env, DialogueScenarioContext context, int maxItems = 2, int maxChars = 260) => Parts.Slice1.BuildRecentWorldEventIntelCompactDigest(env, context, maxItems, maxChars);
        internal bool TryCollectRecentEventIntelItems(EnvironmentPromptConfig env, DialogueScenarioContext context, out List<RecentEventIntelItem> items) => Parts.Slice1.TryCollectRecentEventIntelItems(env, context, out items);
        internal RecentEventSelectionResult SelectRecentEventIntelLines(List<RecentEventIntelItem> items, int maxItems, int maxChars) => Parts.Slice2.SelectRecentEventIntelLines(items, maxItems, maxChars);
        internal string BuildRecentEventIntelLine(RecentEventIntelItem item) => Parts.Slice2.BuildRecentEventIntelLine(item);
        internal List<string> BuildRecentEventDigestLines(List<RecentEventIntelItem> items, RecentEventSelectionResult selection) => Parts.Slice2.BuildRecentEventDigestLines(items, selection);
        internal string BuildCompactDigestEntry(string line) => Parts.Slice2.BuildCompactDigestEntry(line);
        internal string BuildTypeDigest(IEnumerable<RecentEventIntelItem> items) => Parts.Slice2.BuildTypeDigest(items);
        internal string BuildTopicDigest(IEnumerable<RecentEventIntelItem> items) => Parts.Slice2.BuildTopicDigest(items);
        internal string ResolveRecentEventTopic(RecentEventIntelItem item) => Parts.Slice2.ResolveRecentEventTopic(item);
        internal bool ContainsAnyKeyword(string text, params string[] tokens) => Parts.Slice2.ContainsAnyKeyword(text, tokens);
        internal string BuildTrendDigest(IEnumerable<RecentEventIntelItem> items) => Parts.Slice2.BuildTrendDigest(items);
        internal string BuildRelativeTickText(int tick) => Parts.Slice2.BuildRelativeTickText(tick);
        internal List<string> BuildEnvironmentContextLines(Map map, IntVec3 focusCell, DialogueScenarioContext context, EnvironmentContextSwitchesConfig switches) => Parts.Slice2.BuildEnvironmentContextLines(map, focusCell, context, switches);
        internal string BuildLocalTimeText(Map map) => Parts.Slice2.BuildLocalTimeText(map);
        internal string BuildLocalDateText(Map map) => Parts.Slice2.BuildLocalDateText(map);
        internal string BuildLocationAndTemperatureText(Map map, IntVec3 focusCell, DialogueScenarioContext context) => Parts.Slice2.BuildLocationAndTemperatureText(map, focusCell, context);
        internal string BuildLocationText(DialogueScenarioContext context, Map map, IntVec3 focusCell) => Parts.Slice2.BuildLocationText(context, map, focusCell);
        internal string BuildBeautyText(Map map, IntVec3 focusCell) => Parts.Slice2.BuildBeautyText(map, focusCell);
        internal string BuildCleanlinessText(Map map, IntVec3 focusCell) => Parts.Slice2.BuildCleanlinessText(map, focusCell);
        internal string BuildSurroundingsText(Map map, IntVec3 focusCell, DialogueScenarioContext context) => Parts.Slice3.BuildSurroundingsText(map, focusCell, context);
        internal Map ResolveEnvironmentMap(DialogueScenarioContext context) => Parts.Slice3.ResolveEnvironmentMap(context);
        internal bool TryResolveFocusCell(Map map, DialogueScenarioContext context, out IntVec3 focusCell) => Parts.Slice3.TryResolveFocusCell(map, context, out focusCell);
        internal HashSet<string> BuildScenarioTags(DialogueScenarioContext context, bool includePresetTags) => Parts.Slice3.BuildScenarioTags(context, includePresetTags);
        internal void AppendDiplomacyScenarioTags(DialogueScenarioContext context, HashSet<string> tags) => Parts.Slice3.AppendDiplomacyScenarioTags(context, tags);
        internal void AppendRpgScenarioTags(DialogueScenarioContext context, HashSet<string> tags) => Parts.Slice3.AppendRpgScenarioTags(context, tags);
        internal bool TryGetMoodTag(Pawn pawn, out string moodTag) => Parts.Slice3.TryGetMoodTag(pawn, out moodTag);
        internal bool HasIntimateRelation(Pawn first, Pawn second) => Parts.Slice3.HasIntimateRelation(first, second);
        internal bool EntryMatchesTags(ScenePromptEntryConfig entry, HashSet<string> normalizedTags) => Parts.Slice3.EntryMatchesTags(entry, normalizedTags);
        internal void AddNormalizedTag(HashSet<string> tags, string tag) => Parts.Slice3.AddNormalizedTag(tags, tag);
        internal string ResolveRpgPawnPersonaPrompt(Pawn target) => Parts.Slice3.ResolveRpgPawnPersonaPrompt(target);
        #endregion
}
    internal sealed class RelationsContextAssemblerParts
    {
        internal readonly RelationsContextAssembler Owner;
        internal readonly RelationsContextAssemblerExpandMemory ExpandMemory;
        internal readonly RelationsContextAssemblerWorld World;
        internal readonly RelationsContextSlice1 Slice1;
        internal readonly RelationsContextSlice2 Slice2;
        internal readonly RelationsContextSlice3 Slice3;
        internal RelationsContextAssemblerParts(RelationsContextAssembler owner)
        {
            Owner = owner;
            ExpandMemory = new RelationsContextAssemblerExpandMemory(owner);
            World = new RelationsContextAssemblerWorld(owner);
            Slice1 = new RelationsContextSlice1(owner);
            Slice2 = new RelationsContextSlice2(owner);
            Slice3 = new RelationsContextSlice3(owner);
        }
    }


}
