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
    /// <summary>/// Dependencies: world-event ledger, leader memory, faction manager.
 /// Responsibility: translate real world-state records into fact-grounded social-news seeds.
 ///</summary>
    internal static class SocialNewsSeedFactory
    {
        internal const int SeedWindowDays = 60;

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        
    
        #region Cluster forwards
        public static SocialNewsSeed CreateDialogueSeed(Faction sourceFaction, Faction targetFaction, SocialPostCategory category, int sentiment, string summary, bool isKeyword, string intentHint, DebugGenerateReason reason) => SocialNewsSeedSlice1.CreateDialogueSeed(sourceFaction, targetFaction, category, sentiment, summary, isKeyword, intentHint, reason);
        public static string TryBuildFactionDialoguePublicClaim(Faction sourceFaction, SocialPostCategory category, int sentiment, string summary, string intentHint, Faction targetFaction = null) => SocialNewsSeedSlice1.TryBuildFactionDialoguePublicClaim(sourceFaction, category, sentiment, summary, intentHint, targetFaction);
        public static List<SocialNewsSeed> CollectScheduledSeeds() => SocialNewsSeedSlice1.CollectScheduledSeeds();
        internal static List<string> BuildDialogueFacts(Faction sourceFaction, Faction targetFaction, SocialPostCategory category, int sentiment, string summary, string intentHint, bool isKeyword, string publicClaim) => SocialNewsSeedSlice1.BuildDialogueFacts(sourceFaction, targetFaction, category, sentiment, summary, intentHint, isKeyword, publicClaim);
        internal static bool TryBuildStructuredClaimFromIntent(string factionName, SocialPostCategory category, int sentiment, string candidate, Faction targetFaction, out string claim) => SocialNewsSeedSlice1.TryBuildStructuredClaimFromIntent(factionName, category, sentiment, candidate, targetFaction, out claim);
        internal static string BuildDialogueQuoteAttributionHint(Faction sourceFaction) => SocialNewsSeedSlice1.BuildDialogueQuoteAttributionHint(sourceFaction);
        internal static string BuildDialogueBackground(SocialPostCategory category, int sentiment, string targetName, string intentHint) => SocialNewsSeedSlice1.BuildDialogueBackground(category, sentiment, targetName, intentHint);
        internal static string BuildDialogueObservedReaction(SocialPostCategory category, int sentiment, string sourceName, string targetName, string intentHint) => SocialNewsSeedSlice1.BuildDialogueObservedReaction(category, sentiment, sourceName, targetName, intentHint);
        internal static string BuildDialogueGameplayImplication(SocialPostCategory category, int sentiment, Faction targetFaction, string intentHint) => SocialNewsSeedSlice1.BuildDialogueGameplayImplication(category, sentiment, targetFaction, intentHint);
        internal static bool IsConcreteDialogueFact(string value) => SocialNewsSeedSlice2.IsConcreteDialogueFact(value);
        internal static string NormalizeDialogueClaimCandidate(string value) => SocialNewsSeedSlice2.NormalizeDialogueClaimCandidate(value);
        internal static void AddRaidSeeds(List<SocialNewsSeed> seeds) => SocialNewsSeedSlice2.AddRaidSeeds(seeds);
        internal static SocialNewsSeed CreateRaidSeed(RaidBattleReportRecord report) => SocialNewsSeedSlice2.CreateRaidSeed(report);
        internal static List<string> BuildRaidFacts(RaidBattleReportRecord report) => SocialNewsSeedSlice2.BuildRaidFacts(report);
        internal static int CalculateBattleSentiment(RaidBattleReportRecord report) => SocialNewsSeedSlice2.CalculateBattleSentiment(report);
        internal static void AddWorldEventSeeds(List<SocialNewsSeed> seeds) => SocialNewsSeedSlice2.AddWorldEventSeeds(seeds);
        internal static SocialNewsSeed CreateWorldEventSeed(WorldEventRecord record) => SocialNewsSeedSlice2.CreateWorldEventSeed(record);
        internal static List<string> BuildWorldEventFacts(WorldEventRecord record) => SocialNewsSeedSlice2.BuildWorldEventFacts(record);
        internal static void AddAidArrivalSeeds(List<SocialNewsSeed> seeds) => SocialNewsSeedSlice2.AddAidArrivalSeeds(seeds);
        internal static bool ShouldTreatAsAidArrival(WorldEventRecord record) => SocialNewsSeedSlice2.ShouldTreatAsAidArrival(record);
        internal static SocialNewsSeed CreateAidArrivalWorldEventSeed(WorldEventRecord record) => SocialNewsSeedSlice2.CreateAidArrivalWorldEventSeed(record);
        internal static void AddLeaderMemorySeeds(List<SocialNewsSeed> seeds) => SocialNewsSeedSlice2.AddLeaderMemorySeeds(seeds);
        internal static bool ShouldSkipMemoryEvent(SignificantEventMemory evt) => SocialNewsSeedSlice2.ShouldSkipMemoryEvent(evt);
        internal static SocialNewsSeed CreateLeaderMemorySeed(Faction ownerFaction, SignificantEventMemory evt) => SocialNewsSeedSlice2.CreateLeaderMemorySeed(ownerFaction, evt);
        internal static List<string> BuildLeaderMemoryFacts(SignificantEventMemory evt) => SocialNewsSeedSlice2.BuildLeaderMemoryFacts(evt);
        internal static void AddSummarySeeds(List<SocialNewsSeed> seeds) => SocialNewsSeedSlice3.AddSummarySeeds(seeds);
        internal static SocialNewsSeed CreateSummarySeed(Faction faction, CrossChannelSummaryRecord record) => SocialNewsSeedSlice3.CreateSummarySeed(faction, record);
        internal static List<string> BuildSummaryFacts(CrossChannelSummaryRecord record) => SocialNewsSeedSlice3.BuildSummaryFacts(record);
        internal static void AddScheduledEventSeeds(List<SocialNewsSeed> seeds) => SocialNewsSeedSlice3.AddScheduledEventSeeds(seeds);
        internal static SocialNewsSeed CreateScheduledEventSeed(ScheduledSocialEventRecord record) => SocialNewsSeedSlice3.CreateScheduledEventSeed(record);
        internal static SocialNewsSeed CreateQuestResultSeed(ScheduledSocialEventRecord record) => SocialNewsSeedSlice3.CreateQuestResultSeed(record);
        internal static SocialNewsSeed CreateTradeDealSeed(ScheduledSocialEventRecord record) => SocialNewsSeedSlice3.CreateTradeDealSeed(record);
        internal static SocialNewsSeed CreateGoodwillShiftSeed(ScheduledSocialEventRecord record) => SocialNewsSeedSlice3.CreateGoodwillShiftSeed(record);
        internal static SocialNewsSeed CreateRelationShiftSeed(ScheduledSocialEventRecord record) => SocialNewsSeedSlice3.CreateRelationShiftSeed(record);
        internal static SocialNewsSeed CreateAidArrivalSeed(ScheduledSocialEventRecord record) => SocialNewsSeedSlice3.CreateAidArrivalSeed(record);
        internal static SocialNewsSeed CreateScheduledSeed(ScheduledSocialEventRecord record, SocialNewsOriginType originType, SocialPostCategory category, int sentiment, string sourceLabel, string credibilityLabel, float credibility) => SocialNewsSeedSlice3.CreateScheduledSeed(record, originType, category, sentiment, sourceLabel, credibilityLabel, credibility);
        internal static List<string> BuildScheduledFacts(ScheduledSocialEventRecord record) => SocialNewsSeedSlice3.BuildScheduledFacts(record);
        internal static string BuildLocationFact(string location) => SocialNewsSeedSlice3.BuildLocationFact(location);
        internal static string BuildSettlementContextFact(string location, Faction primaryFaction, Faction secondaryFaction) => SocialNewsSeedSlice3.BuildSettlementContextFact(location, primaryFaction, secondaryFaction);
        internal static string BuildFactionFactValue(Faction faction, string fallbackName = null) => SocialNewsSeedSlice3.BuildFactionFactValue(faction, fallbackName);
        internal static string ResolveFactionStrongholdLabel(Faction primaryFaction, Faction secondaryFaction) => SocialNewsSeedSlice3.ResolveFactionStrongholdLabel(primaryFaction, secondaryFaction);
        internal static IEnumerable<Faction> GetEligibleSourceFactions() => SocialNewsSeedSlice3.GetEligibleSourceFactions();
        internal static Faction ResolveKnownFaction(IEnumerable<string> ids, bool preferPlayer) => SocialNewsSeedSlice3.ResolveKnownFaction(ids, preferPlayer);
        internal static Faction ResolveFaction(string factionId) => SocialNewsSeedSlice3.ResolveFaction(factionId);
        internal static string BuildMemoryFactionId(Faction faction) => SocialNewsSeedSlice3.BuildMemoryFactionId(faction);
        internal static string BuildDialogueOriginKey(Faction sourceFaction, Faction targetFaction, int currentTick, string summary, string intentHint, bool isKeyword) => SocialNewsSeedSlice3.BuildDialogueOriginKey(sourceFaction, targetFaction, currentTick, summary, intentHint, isKeyword);
        #endregion
}

}
