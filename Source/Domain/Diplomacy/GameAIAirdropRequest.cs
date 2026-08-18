using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using RimWorld;
using UnityEngine;
using Verse;
using APIResult = Ustas.RimAI.Communication.Relations.DiplomacySystem.GameAIInterface.APIResult;
using APICallRecord = Ustas.RimAI.Communication.Relations.DiplomacySystem.GameAIInterface.APICallRecord;
using DialogueApiGoodwillCostResult = Ustas.RimAI.Communication.Relations.DiplomacySystem.GameAIInterface.DialogueApiGoodwillCostResult;
using FactionCooldownEntry = Ustas.RimAI.Communication.Relations.DiplomacySystem.GameAIInterface.FactionCooldownEntry;
using RaidWaveState = Ustas.RimAI.Communication.Relations.DiplomacySystem.GameAIInterface.RaidWaveState;

namespace Ustas.RimAI.Communication.Relations.DiplomacySystem
{
    /// <summary>Item airdrop request and candidate preparation.</summary>
    internal sealed class GameAIAirdropRequest : GameAIInterfaceCollaborator
    {
        internal GameAIAirdropRequest(GameAIInterface owner) : base(owner)
        {
        }

public APIResult RequestItemAirdrop(Faction faction, Dictionary<string, object> parameters)
        {
            GameAIAirdropBoundNeed.ClearStaleBoundNeedParameters(parameters);

            Map map = Find.AnyPlayerHomeMap;
            Pawn negotiator = ItemAirdropTradePolicy.ResolveBestNegotiator(null);
            if (negotiator == null)
            {
                return Owner.Parts.AirdropDrop.FailFastAirdrop(
                    "player_negotiator_required",
                    "Preparing a barter airdrop requires a valid player negotiator on a map.",
                    faction,
                    parameters,
                    sendLetter: false);
            }

            APIResult prepareResult = Owner.Parts.AirdropBarter.PrepareItemAirdropTradeForMap(faction, parameters, map, false, negotiator);
            if (!prepareResult.Success)
            {
                return prepareResult;
            }

            if (prepareResult.Data is ItemAirdropPendingSelectionData)
            {
                return prepareResult;
            }

            if (!(prepareResult.Data is ItemAirdropPreparedTradeData preparedTrade))
            {
                return Owner.Parts.AirdropDrop.FailFastAirdrop("prepare_trade_failed", "Airdrop trade payload is missing.", faction, parameters, sendLetter: false);
            }

            return Owner.Parts.AirdropBarter.CommitPreparedItemAirdropTrade(faction, preparedTrade);
        }

internal APIResult PrepareItemAirdropCandidates(
            ItemAirdropIntent intent,
            int budget,
            RelationsSettings settings,
            out ItemAirdropCandidatePack candidatePack)
        {
            candidatePack = null;
            HashSet<string> blacklist = GameAIAirdropDrop.ParseCsv(settings.ItemAirdropBlacklistDefNamesCsv);
            HashSet<string> blockedCategories = ItemAirdropSafetyPolicy.ParseBlockedCategories(settings.ItemAirdropBlockedCategoriesCsv);
            int topN = Mathf.Clamp(settings.ItemAirdropSelectionCandidateLimit, 1, 100);
            ItemAirdropCandidatePack strictPack = ThingDefResolver.BuildCandidates(intent, topN, blacklist, blockedCategories);
            if (strictPack.Candidates.Count > 0 ||
                intent == null ||
                intent.Family == ItemAirdropNeedFamily.Unknown ||
                !settings.EnableAirdropSameFamilyRelaxedRetry)
            {
                candidatePack = strictPack;
                return APIResult.SuccessResult("Candidate market prices resolved.");
            }

            // Relax blocked-category filtering once, while keeping the same family boundary.
            HashSet<string> relaxedCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ItemAirdropCandidatePack retryPack = ThingDefResolver.BuildCandidates(intent, topN, blacklist, relaxedCategories);
            retryPack.UsedFallbackPool = true;
            candidatePack = retryPack;
            return APIResult.SuccessResult("Candidate market prices resolved.");
        }

internal static string BuildPrepareAuditSummary(
            ItemAirdropIntent intent,
            int budget,
            ItemAirdropCandidatePack candidatePack,
            List<string> localAliases,
            List<string> aiAliases,
            string needType = "missing",
            string needRawPreview = "none")
        {
            string tokenSummary = intent?.Tokens == null || intent.Tokens.Count == 0
                ? "none"
                : string.Join("|", intent.Tokens.Take(8));
            string localAliasSummary = localAliases == null || localAliases.Count == 0
                ? "none"
                : string.Join("|", localAliases.Take(6));
            string aiAliasSummary = aiAliases == null || aiAliases.Count == 0
                ? "none"
                : string.Join("|", aiAliases.Take(6));
            string diagnostics = candidatePack?.BuildDiagnosticsSummary() ?? "records=0,blacklist=0,blockedCategory=0,familyReject=0,matchReject=0,nearMiss=none";
            string topSummary = candidatePack?.BuildSummary() ?? "none";
            string boundNeedDetails = string.IsNullOrWhiteSpace(candidatePack?.BoundNeedConflictDetails)
                ? "none"
                : candidatePack.BoundNeedConflictDetails;
            return $"budget={budget},family={intent?.Family ?? ItemAirdropNeedFamily.Unknown},needType={needType},needRawPreview={needRawPreview},tokens={tokenSummary},localAliases={localAliasSummary},aiAliases={aiAliasSummary},candidates={candidatePack?.Candidates?.Count ?? 0},fallback={candidatePack?.UsedFallbackPool ?? false},{diagnostics},boundNeedDetails={boundNeedDetails},top={topSummary}";
        }

internal static bool ShouldRequireNeedClarification(ItemAirdropIntent intent, ItemAirdropCandidatePack candidatePack)
        {
            return intent?.Family == ItemAirdropNeedFamily.Unknown &&
                   !ThingDefResolver.HasStrongNeedRelevance(intent, candidatePack, 5);
        }

internal static string BuildNeedClarificationReason()
        {
            return "need_relevance_insufficient";
        }

internal List<string> ExpandNeedAliasesWithAi(string need, string constraints, RelationsSettings settings)
        {
            _ = need;
            _ = constraints;
            _ = settings;
            // AI alias expansion is handled by BeginPrepareItemAirdropTradeAsync.
            return new List<string>();
        }

internal static string BuildAliasExpansionPrompt(string need, string constraints, int maxCount)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Need: {(need ?? string.Empty).Trim()}");
            sb.AppendLine($"Constraints: {(constraints ?? string.Empty).Trim()}");
            sb.AppendLine($"MaxAliases: {maxCount}");
            sb.AppendLine("Output strictly JSON with field aliases only.");
            return sb.ToString().Trim();
        }

internal static List<string> ParseAliases(string rawText, int maxCount)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(rawText))
            {
                return result;
            }

            MatchCollection matches = Regex.Matches(rawText, "\"([^\"]+)\"");
            for (int i = 0; i < matches.Count; i++)
            {
                string value = matches[i].Groups[1].Value?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(value) ||
                    string.Equals(value, "aliases", StringComparison.OrdinalIgnoreCase) ||
                    value.Length > 36)
                {
                    continue;
                }

                result.Add(value);
                if (result.Count >= maxCount)
                {
                    break;
                }
            }

            return result
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

    }
}
