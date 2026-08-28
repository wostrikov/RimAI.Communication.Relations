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
    internal static class SocialNewsSeedSlice1
    {
public static SocialNewsSeed CreateDialogueSeed(
            Faction sourceFaction,
            Faction targetFaction,
            SocialPostCategory category,
            int sentiment,
            string summary,
            bool isKeyword,
            string intentHint,
            DebugGenerateReason reason)
        {
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            string trimmedSummary = (summary ?? string.Empty).Trim();
            string trimmedIntent = (intentHint ?? string.Empty).Trim();
            string publicClaim = SocialNewsSeedFactory.TryBuildFactionDialoguePublicClaim(
                sourceFaction, category, sentiment, trimmedSummary, trimmedIntent, targetFaction);
            if (string.IsNullOrWhiteSpace(publicClaim))
            {
                string targetName = targetFaction?.Name?.Trim();
                publicClaim = string.IsNullOrWhiteSpace(targetName)
                    ? $"{sourceFaction.Name} зробила публічну заяву щодо поточної ситуації."
                    : $"{sourceFaction.Name} зробила публічну заяву щодо відносин з {targetName}.";
            }

            return new SocialNewsSeed
            {
                OriginType = isKeyword ? SocialNewsOriginType.DialogueKeyword : SocialNewsOriginType.DialogueExplicit,
                OriginKey = SocialNewsSeedFactory.BuildDialogueOriginKey(sourceFaction, targetFaction, currentTick, summary, intentHint, isKeyword),
                SourceFaction = sourceFaction,
                TargetFaction = targetFaction,
                Category = category,
                Sentiment = sentiment,
                OccurredTick = currentTick,
                Summary = string.IsNullOrWhiteSpace(summary)
                    ? "RimChat_SocialPostSummaryFromDialogue".Translate().ToString()
                    : summary.Trim(),
                IntentHint = intentHint ?? string.Empty,
                SourceLabel = "RimChat_SocialSourceOfficialStatement",
                CredibilityLabel = isKeyword
                    ? "RimChat_SocialCredibilityMonitoredChannel"
                    : "RimChat_SocialCredibilityOfficialStatement",
                CredibilityValue = isKeyword ? 0.72f : 0.88f,
                IsFromPlayerDialogue = true,
                ApplyDiplomaticImpact = true,
                DebugReason = reason,
                PrimaryClaim = publicClaim,
                QuoteAttributionHint = SocialNewsSeedFactory.BuildDialogueQuoteAttributionHint(sourceFaction),
                Facts = SocialNewsSeedFactory.BuildDialogueFacts(sourceFaction, targetFaction, category, sentiment, trimmedSummary, trimmedIntent, isKeyword, publicClaim),
                RawText = string.IsNullOrWhiteSpace(publicClaim) ? trimmedSummary : publicClaim
            };
        }

public static string TryBuildFactionDialoguePublicClaim(
            Faction sourceFaction,
            SocialPostCategory category,
            int sentiment,
            string summary,
            string intentHint,
            Faction targetFaction = null)
        {
            string factionName = sourceFaction?.Name?.Trim();
            if (string.IsNullOrWhiteSpace(factionName))
            {
                return string.Empty;
            }

            string normalizedSummary = SocialNewsSeedFactory.NormalizeDialogueClaimCandidate(summary);
            string normalizedIntent = SocialNewsSeedFactory.NormalizeDialogueClaimCandidate(intentHint);
            if (SocialNewsSeedFactory.TryBuildStructuredClaimFromIntent(factionName, category, sentiment, normalizedIntent, targetFaction, out string structuredFromIntent))
            {
                return structuredFromIntent;
            }

            if (SocialNewsSeedFactory.TryBuildStructuredClaimFromIntent(factionName, category, sentiment, normalizedSummary, targetFaction, out string structuredFromSummary))
            {
                return structuredFromSummary;
            }

            return string.Empty;
        }

public static List<SocialNewsSeed> CollectScheduledSeeds()
        {
            var seeds = new List<SocialNewsSeed>();
            SocialNewsSeedFactory.AddRaidSeeds(seeds);
            SocialNewsSeedFactory.AddWorldEventSeeds(seeds);
            SocialNewsSeedFactory.AddAidArrivalSeeds(seeds);
            SocialNewsSeedFactory.AddLeaderMemorySeeds(seeds);
            SocialNewsSeedFactory.AddSummarySeeds(seeds);
            SocialNewsSeedFactory.AddScheduledEventSeeds(seeds);
            return seeds
                .Where(seed => seed != null && seed.IsValid())
                .OrderByDescending(seed => seed.OccurredTick)
                .ToList();
        }

internal static List<string> BuildDialogueFacts(
            Faction sourceFaction,
            Faction targetFaction,
            SocialPostCategory category,
            int sentiment,
            string summary,
            string intentHint,
            bool isKeyword,
            string publicClaim)
        {
            string trimmedSummary = (summary ?? string.Empty).Trim();
            string trimmedIntent = (intentHint ?? string.Empty).Trim();
            string sourceName = sourceFaction?.Name ?? "Unknown";
            string targetName = targetFaction?.Name ?? "None";
            string channel = isKeyword ? "keyword-detected public signal" : "explicit official public statement";
            string location = SocialNewsSeedFactory.ResolveFactionStrongholdLabel(sourceFaction, targetFaction);
            var facts = new List<string>
            {
                $"Source faction: {SocialNewsSeedFactory.BuildFactionFactValue(sourceFaction, sourceName)}",
                $"Target faction: {SocialNewsSeedFactory.BuildFactionFactValue(targetFaction, targetName)}",
                $"News category: {SocialCircleService.GetCategoryLabel(category)}",
                $"Public channel: {channel}",
                SocialNewsSeedFactory.BuildLocationFact(location),
                SocialNewsSeedFactory.BuildSettlementContextFact(location, sourceFaction, targetFaction)
            };

            if (!string.IsNullOrWhiteSpace(publicClaim))
            {
                facts.Add($"Public claim: {publicClaim}");
            }

            string background = SocialNewsSeedFactory.BuildDialogueBackground(category, sentiment, targetName, trimmedIntent);
            if (!string.IsNullOrWhiteSpace(background))
            {
                facts.Add($"Background tension: {background}");
            }

            string observedReaction = SocialNewsSeedFactory.BuildDialogueObservedReaction(category, sentiment, sourceName, targetName, trimmedIntent);
            if (!string.IsNullOrWhiteSpace(observedReaction))
            {
                facts.Add($"Observed reaction: {observedReaction}");
            }

            string implication = SocialNewsSeedFactory.BuildDialogueGameplayImplication(category, sentiment, targetFaction, trimmedIntent);
            if (!string.IsNullOrWhiteSpace(implication))
            {
                facts.Add($"Gameplay implication: {implication}");
            }

            if (!string.IsNullOrWhiteSpace(trimmedIntent))
            {
                facts.Add($"Intent hint: {trimmedIntent}");
            }

            facts.Add($"raw_text: {(string.IsNullOrWhiteSpace(publicClaim) ? trimmedSummary : publicClaim)}");
            return facts;
        }

internal static bool TryBuildStructuredClaimFromIntent(
            string factionName,
            SocialPostCategory category,
            int sentiment,
            string candidate,
            Faction targetFaction,
            out string claim)
        {
            claim = string.Empty;
            string targetName = targetFaction?.Name?.Trim();
            string text = (candidate ?? string.Empty).Trim();

            if (string.Equals(text, SocialIntentType.Raid.ToString(), StringComparison.OrdinalIgnoreCase)
                || (string.Equals(text, "request_raid", StringComparison.OrdinalIgnoreCase))
                || (category == SocialPostCategory.Military && sentiment <= -1 && string.IsNullOrWhiteSpace(text)))
            {
                claim = string.IsNullOrWhiteSpace(targetName)
                    ? $"{factionName} попереджає: ще одна провокація — і буде звичайний напад."
                    : $"{factionName} попереджає {targetName}: ще одна провокація — і буде звичайний напад.";
                return true;
            }

            if (string.Equals(text, SocialIntentType.Aid.ToString(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "request_aid", StringComparison.OrdinalIgnoreCase))
            {
                claim = string.IsNullOrWhiteSpace(targetName)
                    ? $"{factionName} готова й надалі надавати допомогу."
                    : $"{factionName} готова й надалі надавати допомогу {targetName}.";
                return true;
            }

            if (string.Equals(text, SocialIntentType.Caravan.ToString(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "request_caravan", StringComparison.OrdinalIgnoreCase))
            {
                claim = string.IsNullOrWhiteSpace(targetName)
                    ? $"{factionName} готова відновити торгівлю."
                    : $"{factionName} готова відновити торгівлю з {targetName}.";
                return true;
            }

            if (!SocialNewsSeedFactory.IsConcreteDialogueFact(text))
            {
                return false;
            }

            claim = text.IndexOf(factionName, StringComparison.Ordinal) >= 0
                ? text
                : $"{factionName} заявляє: {text}";
            return true;
        }

internal static string BuildDialogueQuoteAttributionHint(Faction sourceFaction)
        {
            if (!string.IsNullOrWhiteSpace(sourceFaction?.Name))
            {
                return sourceFaction.Name.Trim();
            }

            return "Public statement";
        }

internal static string BuildDialogueBackground(
            SocialPostCategory category,
            int sentiment,
            string targetName,
            string intentHint)
        {
            if (category == SocialPostCategory.Military || sentiment <= -1)
            {
                return targetName == "None"
                    ? "The statement appeared while local security concerns were rising."
                    : $"The statement appeared while tensions around {targetName} were rising.";
            }

            if (category == SocialPostCategory.Economic)
            {
                return "The statement centered on trade expectations, supply movement, or exchange terms.";
            }

            if (category == SocialPostCategory.Anomaly)
            {
                return "The statement followed an unusual incident that people were already trying to explain.";
            }

            if (!string.IsNullOrWhiteSpace(intentHint))
            {
                return "The statement was read as a signal about the faction's next diplomatic move.";
            }

            return "The statement was treated as a public position rather than casual talk.";
        }

internal static string BuildDialogueObservedReaction(
            SocialPostCategory category,
            int sentiment,
            string sourceName,
            string targetName,
            string intentHint)
        {
            if (category == SocialPostCategory.Military || sentiment <= -1)
            {
                return targetName == "None"
                    ? "Guards and caravan crews started talking as if route risk might rise again."
                    : $"Traders and guards started weighing whether contact with {targetName} was becoming more dangerous.";
            }

            if (category == SocialPostCategory.Economic)
            {
                return "Merchants and haulers began comparing whether future deals would tighten, loosen, or change price expectations.";
            }

            if (category == SocialPostCategory.Anomaly)
            {
                return "Witnesses repeated the story as a warning, and nearby settlements treated it as a sign to watch for similar incidents.";
            }

            if (!string.IsNullOrWhiteSpace(intentHint))
            {
                return $"Listeners treated the wording as a deliberate signal of {sourceName}'s next public posture.";
            }

            return "Listeners treated the wording as a public line that others would now have to answer or test.";
        }

internal static string BuildDialogueGameplayImplication(
            SocialPostCategory category,
            int sentiment,
            Faction targetFaction,
            string intentHint)
        {
            if (category == SocialPostCategory.Military || sentiment <= -1)
            {
                return "Possible pressure on security expectations, hostile intent, or future raid risk.";
            }

            if (category == SocialPostCategory.Economic)
            {
                return "Possible pressure on trade expectations, caravan tone, or future aid and exchange terms.";
            }

            if (category == SocialPostCategory.Anomaly)
            {
                return "Possible pressure on regional safety expectations and how outsiders approach the area.";
            }

            if (targetFaction == Faction.OfPlayer || !string.IsNullOrWhiteSpace(intentHint))
            {
                return "Possible pressure on diplomatic attitude, public goodwill, or future cooperation tone.";
            }

            return string.Empty;
        }
    }
}
