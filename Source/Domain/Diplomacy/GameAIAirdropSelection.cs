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
    /// <summary>Item airdrop AI selection and validation.</summary>
    internal sealed class GameAIAirdropSelection : GameAIInterfaceCollaborator
    {
        internal GameAIAirdropSelection(GameAIInterface owner) : base(owner)
        {
        }

internal APIResult ExecuteItemAirdropSelection(
            ItemAirdropIntent intent,
            ItemAirdropCandidatePack candidatePack,
            int budget,
            RelationsSettings settings,
            Dictionary<string, object> parameters,
            string forcedSelectedDefName = "")
        {
            string boundNeedDefName = GameAIAirdropDrop.ReadString(parameters, ItemAirdropParameterKeys.BoundNeedDefName);
            string effectiveForcedSelectedDefName = forcedSelectedDefName;
            bool hasBoundNeed = !string.IsNullOrWhiteSpace(boundNeedDefName);
            bool hadForcedSelectionConflict = false;
            if (hasBoundNeed)
            {
                if (string.IsNullOrWhiteSpace(effectiveForcedSelectedDefName))
                {
                    effectiveForcedSelectedDefName = boundNeedDefName;
                }
                else if (!string.Equals(effectiveForcedSelectedDefName, boundNeedDefName, StringComparison.OrdinalIgnoreCase))
                {
                    effectiveForcedSelectedDefName = boundNeedDefName;
                    hadForcedSelectionConflict = true;
                }
            }

            RequestedCountExtraction requestedCount = ExtractRequestedCount(intent?.NeedText);
            requestedCount = MergeRequestedCountWithParameters(requestedCount, parameters);
            if (requestedCount.HasMultipleCounts)
            {
                return Owner.Parts.AirdropDrop.BuildSelectionFailure(
                    "need_count_ambiguous",
                    "need contains multiple explicit counts; request_item_airdrop supports single-item count only.");
            }

            if (!string.IsNullOrWhiteSpace(effectiveForcedSelectedDefName))
            {
                APIResult forcedResult = Owner.Parts.AirdropPending.TryBuildForcedSelection(
                    effectiveForcedSelectedDefName,
                    intent,
                    candidatePack,
                    budget,
                    settings,
                    requestedCount,
                    out ItemAirdropSelection forcedSelection,
                    out string forcedCountSource,
                    out int forcedHardMax,
                    out int forcedMaxByBudget);
                if (!forcedResult.Success || forcedSelection == null)
                {
                    return forcedResult;
                }

                if (hasBoundNeed)
                {
                    forcedSelection.Reason = hadForcedSelectionConflict
                        ? "bound_need_conflict_rebuilt"
                        : "bound_need_selected";
                }

                string forcedDetails = Owner.Parts.AirdropDrop.BuildSelectionAuditDetails(
                    forcedSelection,
                    candidatePack,
                    budget,
                    settings,
                    forcedCountSource,
                    forcedMaxByBudget,
                    forcedHardMax);
                Owner.Parts.AirdropDrop.RecordStageAudit("selection", null, null, forcedDetails);
                return APIResult.SuccessResult("Selection resolved from bound need / selected_def.", forcedSelection);
            }

            const string pendingReason = "Second-pass LLM selection moved to async pipeline.";
            APIResult pendingResult = Owner.Parts.AirdropPending.BuildTimeoutPendingSelection(intent, candidatePack, budget, settings, "selection_timeout", pendingReason);
            if (pendingResult.Data is ItemAirdropPendingSelectionData pendingData)
            {
                Owner.Parts.AirdropDrop.RecordStageAudit("selection", null, null, GameAIAirdropPending.BuildPendingSelectionAuditDetails(pendingData));
            }

            return pendingResult;
        }

internal static RequestedCountExtraction ExtractRequestedCount(string needText)
        {
            var result = new RequestedCountExtraction();
            if (string.IsNullOrWhiteSpace(needText))
            {
                return result;
            }

            string trimmed = needText.Trim();
            // Only extract a leading number as count when followed by:
            // - non-ASCII text (CJK): "1000原木" → count=1000
            // - whitespace then text: "5 医疗包" → count=5
            // - end of string (pure number): "10" → count=10
            // NOT when followed by ASCII letters/digits: "75x350mmR" → no count (item name)
            Match match = Regex.Match(trimmed, @"^(\d+)(?=[^\x00-\x7F]|\s+\S|\s*$)");
            if (!match.Success)
            {
                return result;
            }

            if (!int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                return result;
            }

            result.HasExplicitCount = true;
            result.RequestedCount = Math.Max(1, parsed);
            return result;
        }

internal APIResult ValidateAirdropSelection(
            ItemAirdropSelection selection,
            ItemAirdropCandidatePack candidatePack,
            int budget,
            RelationsSettings settings,
            RequestedCountExtraction requestedCount,
            string defaultCountSource,
            out ThingDefRecord selectedRecord,
            out int validatedCount,
            out string resolvedCountSource,
            out int requestedOriginalCount,
            out int maxByBudget,
            out int maxBySystem,
            out int hardMax)
        {
            selectedRecord = null;
            validatedCount = 0;
            resolvedCountSource = string.IsNullOrWhiteSpace(defaultCountSource) ? "llm" : defaultCountSource;
            requestedOriginalCount = 0;
            maxByBudget = 0;
            maxBySystem = 0;
            hardMax = 0;
            if (selection == null)
            {
                return Owner.Parts.AirdropDrop.BuildSelectionFailure("selection_null", "Selection payload is null.");
            }

            selectedRecord = candidatePack.Candidates
                .Select(c => c.Record)
                .FirstOrDefault(r => string.Equals(r.DefName, selection.SelectedDefName, StringComparison.OrdinalIgnoreCase));
            if (selectedRecord?.Def == null)
            {
                return Owner.Parts.AirdropDrop.BuildSelectionFailure("selection_out_of_candidates", $"selected_def '{selection.SelectedDefName}' is not in candidate list.");
            }

            if (requestedCount.HasMultipleCounts)
            {
                return Owner.Parts.AirdropDrop.BuildSelectionFailure(
                    "need_count_ambiguous",
                    "need contains multiple explicit counts; request_item_airdrop supports single-item count only.");
            }

            int targetCount = selection.Count;
            if (requestedCount.HasExplicitCount)
            {
                targetCount = requestedCount.RequestedCount;
                resolvedCountSource = "fallback_explicit";
            }
            else if (requestedCount.HasParameterCount)
            {
                targetCount = requestedCount.ParameterCount;
                resolvedCountSource = "fallback_parameter";
            }

            if (requestedCount.HasExplicitCount && requestedCount.HasParameterCount)
            {
                int needCount = requestedCount.RequestedCount;
                int parameterCount = requestedCount.ParameterCount;
                targetCount = Math.Max(needCount, parameterCount);
                resolvedCountSource = needCount == parameterCount
                    ? "fallback_explicit_parameter_consistent"
                    : "fallback_max_conflict";
            }

            if (targetCount <= 0)
            {
                return Owner.Parts.AirdropDrop.BuildSelectionFailure("selection_count_invalid", "count must be greater than 0.");
            }

            requestedOriginalCount = targetCount;
            GameAIAirdropDrop.ComputeLegalCountWindow(budget, selectedRecord, candidatePack, settings, out maxByBudget, out maxBySystem, out hardMax);

            // Relax budget for bound-need (negotiated) items: use the lower of
            // market price and implied negotiated price so the budget allows
            // the agreed-upon quantity rather than capping at market rate.
            if (candidatePack.BoundNeedInjectedIntoCandidates
                && string.Equals(candidatePack.BoundNeedDefName, selectedRecord.DefName, StringComparison.OrdinalIgnoreCase)
                && budget > 0
                && targetCount > 0)
            {
                float marketPrice = candidatePack.ResolveUnitPrice(selectedRecord);
                float negotiatedPrice = (float)budget / targetCount;
                float effectivePrice = Math.Min(marketPrice, Math.Max(0.01f, negotiatedPrice));
                maxByBudget = Mathf.FloorToInt(Math.Max(0, budget) / effectivePrice);
                hardMax = Math.Max(0, Math.Min(maxByBudget, maxBySystem));
            }

            validatedCount = targetCount;
            return APIResult.SuccessResult("Selection validated.");
        }

internal static RequestedCountExtraction MergeRequestedCountWithParameters(
            RequestedCountExtraction requestedCount,
            Dictionary<string, object> parameters)
        {
            int explicitNeedCount = 0;
            bool hasExplicitNeedCount = GameAIAirdropDrop.TryReadIntParameter(parameters, "__airdrop_explicit_need_count", out explicitNeedCount);
            if (hasExplicitNeedCount && explicitNeedCount > 0)
            {
                requestedCount.HasExplicitCount = true;
                requestedCount.RequestedCount = Math.Max(1, explicitNeedCount);
            }

            int parameterCount = 0;
            bool hasCount = GameAIAirdropDrop.TryReadIntParameter(parameters, "count", out parameterCount);
            if (!hasCount)
            {
                hasCount = GameAIAirdropDrop.TryReadIntParameter(parameters, "quantity", out parameterCount);
            }

            if (!hasCount || parameterCount <= 0)
            {
                return requestedCount;
            }

            requestedCount.HasParameterCount = true;
            requestedCount.ParameterCount = Math.Max(1, parameterCount);
            return requestedCount;
        }

    }
}
