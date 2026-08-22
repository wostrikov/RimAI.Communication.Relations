using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Ustas.RimAI.Communication.Relations.Diagnostics;
using Verse;

namespace Ustas.RimAI.Communication.Relations.AI
{
    internal static class DiplomacyActionParserSlice1
    {
public static List<AIAction> ParseActionsFromJson(string json, string visibleDialogue = null)
        {
            var actions = new List<AIAction>();
            var keptRansomTargetIds = new List<int>();
            var droppedDuplicateRansomTargetIds = new List<int>();
            int keptRansomWithoutTargetCount = 0;

            foreach (DiplomacyActionJsonReader.Candidate candidate in DiplomacyActionJsonReader.Read(json))
            {
                if (string.Equals(candidate.NormalizedActionType, AIActionNames.CreateQuest, StringComparison.Ordinal) &&
                    candidate.Parameters != null &&
                    !candidate.Parameters.ContainsKey("questDefName"))
                {
                    DebugLogger.WarningGated($"create_quest action missing questDefName. Raw actionObj: {candidate.RawObject}");
                }

                DiplomacyActionParser.AddActionIfValid(
                    actions,
                    candidate.RawActionType,
                    candidate.Parameters,
                    candidate.Reason,
                    keptRansomTargetIds,
                    droppedDuplicateRansomTargetIds,
                    ref keptRansomWithoutTargetCount,
                    visibleDialogue);
            }

            DiplomacyActionParser.LogRansomParseSummary(keptRansomTargetIds, droppedDuplicateRansomTargetIds, keptRansomWithoutTargetCount);
            return actions;
        }

public static void AddActionIfValid(
            List<AIAction> actions,
            string actionType,
            Dictionary<string, object> parameters,
            string reason,
            List<int> keptRansomTargetIds,
            List<int> droppedDuplicateRansomTargetIds,
            ref int keptRansomWithoutTargetCount,
            string visibleDialogue = null)
        {
            string normalizedAction = DiplomacyActionParser.NormalizeActionName(actionType);
            if (string.IsNullOrEmpty(normalizedAction) || normalizedAction == "none")
            {
                return;
            }

            if (!DiplomacyActionParser.IsValidAction(normalizedAction))
            {
                DebugLogger.WarningGated($"Unknown AI action: {normalizedAction}");
                return;
            }

            if (parameters == null)
            {
                parameters = new Dictionary<string, object>();
            }

            if (string.Equals(normalizedAction, AIActionNames.RequestItemAirdrop, StringComparison.Ordinal) &&
                !DiplomacyActionParser.HasValidAirdropBarterParameters(parameters, visibleDialogue))
            {
                bool needPresent = DiplomacyActionParser.HasNonEmptyText(parameters, "need", requireString: true);
                string paymentItemsType = DiplomacyActionParser.DescribeAirdropParameterType(parameters, "payment_items");
                string paymentItemsCount = DiplomacyActionParser.DescribeAirdropPaymentItemsCount(parameters);
                string paymentItem0Type = DiplomacyActionParser.DescribeAirdropPaymentItem0Type(parameters);
                string paymentItem0Keys = DiplomacyActionParser.DescribeAirdropPaymentItem0Keys(parameters);
                Log.Warning(
                    $"[RimAI.Relations] Dropped request_item_airdrop action because required parameters are missing or invalid (need, payment_items). " +
                    $"need_present={needPresent}, " +
                    $"payment_items_type={paymentItemsType}, " +
                    $"payment_items_count={paymentItemsCount}, " +
                    $"payment_item0_type={paymentItem0Type}, " +
                    $"payment_item0_keys={paymentItem0Keys}");
                return;
            }

            if (string.Equals(normalizedAction, AIActionNames.PayPrisonerRansom, StringComparison.Ordinal))
            {
                if (!DiplomacyActionParser.HasValidPrisonerRansomParameters(
                        parameters,
                        out string invalidParameter,
                        out string paymentModeRaw,
                        out string paymentModeNormalized,
                        out bool paymentModePassthrough))
                {
                    Log.Warning(
                        $"[RimAI.Relations] pay_prisoner_ransom parameters unresolved: missing_or_invalid={invalidParameter ?? "unknown"}, " +
                        $"payment_mode_raw={DiplomacyActionParser.FormatRansomLogValue(paymentModeRaw)}, " +
                        $"payment_mode_normalized={DiplomacyActionParser.FormatRansomLogValue(paymentModeNormalized)}, " +
                        $"passthrough_to_execution={paymentModePassthrough}. " +
                        "Dropping action in parser for fail-fast validation.");
                    return;
                }

                parameters.Remove("__ransom_missing_parameter");
                if (DiplomacyActionParser.TryGetRansomTargetPawnLoadId(parameters, out int targetPawnLoadId))
                {
                    if (DiplomacyActionParser.IsDuplicateRansomActionForTarget(actions, targetPawnLoadId))
                    {
                        droppedDuplicateRansomTargetIds?.Add(targetPawnLoadId);
                        DebugLogger.Debug($"pay_prisoner_ransom parser dropped duplicate target action. target_pawn_load_id={targetPawnLoadId}");
                        return;
                    }

                    keptRansomTargetIds?.Add(targetPawnLoadId);
                }
                else
                {
                    keptRansomWithoutTargetCount++;
                }

                Log.Message(
                    $"[RimAI.Relations] pay_prisoner_ransom parser accepted: " +
                    $"payment_mode_raw={DiplomacyActionParser.FormatRansomLogValue(paymentModeRaw)}, " +
                    $"payment_mode_normalized={DiplomacyActionParser.FormatRansomLogValue(paymentModeNormalized)}, " +
                    $"passthrough_to_execution={paymentModePassthrough}.");
                if (string.IsNullOrWhiteSpace(reason) &&
                    parameters.TryGetValue("reason", out object reasonObj) &&
                    reasonObj != null)
                {
                    reason = reasonObj.ToString();
                }

                actions.Add(new AIAction
                {
                    ActionType = normalizedAction,
                    Parameters = parameters,
                    Reason = reason
                });
                return;
            }

            if (string.IsNullOrWhiteSpace(reason) &&
                parameters.TryGetValue("reason", out object reasonObjNonRansom) &&
                reasonObjNonRansom != null)
            {
                reason = reasonObjNonRansom.ToString();
            }

            if (actions.Exists(a =>
                string.Equals(a.ActionType, normalizedAction, StringComparison.Ordinal) &&
                string.Equals(a.Reason ?? string.Empty, reason ?? string.Empty, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            actions.Add(new AIAction
            {
                ActionType = normalizedAction,
                Parameters = parameters,
                Reason = reason
            });
        }

public static bool IsDuplicateRansomActionForTarget(List<AIAction> actions, int targetPawnLoadId)
        {
            if (actions == null || targetPawnLoadId <= 0)
            {
                return false;
            }

            return actions.Any(action =>
                action != null &&
                string.Equals(action.ActionType, AIActionNames.PayPrisonerRansom, StringComparison.Ordinal) &&
                DiplomacyActionParser.TryGetRansomTargetPawnLoadId(action.Parameters, out int existingTargetPawnLoadId) &&
                existingTargetPawnLoadId == targetPawnLoadId);
        }

public static bool TryGetRansomTargetPawnLoadId(Dictionary<string, object> parameters, out int targetPawnLoadId)
        {
            targetPawnLoadId = 0;
            return DiplomacyActionParser.TryReadLoosePositiveIntegerParameter(parameters, "target_pawn_load_id", out targetPawnLoadId);
        }

public static void LogRansomParseSummary(
            List<int> keptRansomTargetIds,
            List<int> droppedDuplicateRansomTargetIds,
            int keptRansomWithoutTargetCount)
        {
            int keptTargetCount = keptRansomTargetIds?.Count ?? 0;
            int droppedTargetCount = droppedDuplicateRansomTargetIds?.Count ?? 0;
            if (keptTargetCount <= 0 && droppedTargetCount <= 0 && keptRansomWithoutTargetCount <= 0)
            {
                return;
            }

            string keptTargets = keptTargetCount > 0
                ? string.Join(",", keptRansomTargetIds.Distinct().OrderBy(id => id))
                : "none";
            string droppedTargets = droppedTargetCount > 0
                ? string.Join(",", droppedDuplicateRansomTargetIds.Distinct().OrderBy(id => id))
                : "none";
            Log.Message(
                $"[RimAI.Relations] pay_prisoner_ransom parser summary: kept_targets={keptTargets}, " +
                $"dropped_duplicate_targets={droppedTargets}, kept_without_target={Math.Max(0, keptRansomWithoutTargetCount)}.");
        }

public static bool HasValidAirdropBarterParameters(Dictionary<string, object> parameters, string visibleDialogue = null)
        {
            DiplomacyActionParser.NormalizeAirdropBarterParameters(parameters, visibleDialogue);

            if (!DiplomacyActionParser.HasNonEmptyText(parameters, "need", requireString: true))
            {
                return false;
            }

            if (parameters == null ||
                !parameters.TryGetValue("payment_items", out object rawItems) ||
                rawItems == null ||
                !(rawItems is IEnumerable<object> paymentItems))
            {
                return false;
            }

            bool hasAny = false;
            foreach (object row in paymentItems)
            {
                if (!(row is Dictionary<string, object> item) ||
                    !DiplomacyActionParser.HasNonEmptyText(item, "item") ||
                    !DiplomacyActionParser.HasPositiveInteger(item, "count"))
                {
                    return false;
                }

                hasAny = true;
            }

            return hasAny;
        }

public static void NormalizeAirdropBarterParameters(Dictionary<string, object> parameters, string visibleDialogue = null)
        {
            if (parameters == null || parameters.Count == 0)
            {
                return;
            }

            // Try to read need as a plain string first.
            if (DiplomacyActionParser.TryReadStringByAliases(
                    parameters,
                    out string need,
                    "need",
                    "need_def",
                    "needDef",
                    "__airdrop_bound_need_def"))
            {
                string normalizedNeed = need.Trim();
                DiplomacyActionParser.SetCanonicalParameter(parameters, "need", normalizedNeed);
                int explicitNeedCount = DiplomacyActionParser.ExtractSingleExplicitAirdropNeedCount(normalizedNeed);
                if (explicitNeedCount > 0)
                {
                    DiplomacyActionParser.SetCanonicalParameter(parameters, "count", explicitNeedCount);
                    parameters["__airdrop_explicit_need_count"] = explicitNeedCount;
                }
            }
            else if (DiplomacyActionParser.TryReadParameterByAliases(parameters, out object rawNeed, "need") &&
                     rawNeed is Dictionary<string, object> needDict)
            {
                // AI sent need as a JSON object — try to salvage a text description.
                string extracted = DiplomacyActionParser.ExtractNeedTextFromDictionary(needDict);
                if (!string.IsNullOrWhiteSpace(extracted))
                {
                    DiplomacyActionParser.SetCanonicalParameter(parameters, "need", extracted);
                }
            }

            if (!DiplomacyActionParser.TryReadParameterByAliases(parameters, out object rawPaymentItems, "payment_items") ||
                !(rawPaymentItems is IEnumerable<object> paymentItems))
            {
                return;
            }

            var normalizedItems = new List<object>();
            foreach (object row in paymentItems)
            {
                if (row is Dictionary<string, object> item)
                {
                    DiplomacyActionParser.NormalizeAirdropPaymentItem(item);
                    // If count is still missing, try to infer from visible_dialogue.
                    if (!item.ContainsKey("count") && !string.IsNullOrWhiteSpace(visibleDialogue))
                    {
                        int inferred = DiplomacyActionParser.TryInferSilverCountFromDialogue(visibleDialogue);
                        if (inferred > 0)
                        {
                            DiplomacyActionParser.SetCanonicalParameter(item, "count", inferred);
                            DebugLogger.Debug($"Inferred airdrop payment count={inferred} from visible_dialogue.");
                        }
                    }
                    normalizedItems.Add(item);
                    continue;
                }

                normalizedItems.Add(row);
            }

            DiplomacyActionParser.SetCanonicalParameter(parameters, "payment_items", normalizedItems);
        }

public static int ExtractSingleExplicitAirdropNeedCount(string need)
        {
            if (string.IsNullOrWhiteSpace(need))
            {
                return 0;
            }

            MatchCollection matches = System.Text.RegularExpressions.Regex.Matches(need, "\\d+");
            if (matches.Count != 1)
            {
                return 0;
            }

            return int.TryParse(matches[0].Value, out int parsed)
                ? Math.Max(0, parsed)
                : 0;
        }

public static void NormalizeAirdropPaymentItem(Dictionary<string, object> item)
        {
            if (item == null || item.Count == 0)
            {
                return;
            }

            if (DiplomacyActionParser.TryReadStringByAliases(item, out string value, "item", "defName", "def_name", "thingDef", "thing_def"))
            {
                DiplomacyActionParser.SetCanonicalParameter(item, "item", value.Trim());
            }

            // Normalise count from common aliases the AI may use.
            if (DiplomacyActionParser.TryReadParameterByAliases(item, out object rawCount, "count", "amount", "quantity", "qty", "price", "value", "silver"))
            {
                if (DiplomacyActionParser.TryReadLoosePositiveInteger(rawCount, out int count))
                {
                    DiplomacyActionParser.SetCanonicalParameter(item, "count", count);
                }
            }
        }

public static int TryInferSilverCountFromDialogue(string visibleDialogue)
        {
            if (string.IsNullOrWhiteSpace(visibleDialogue))
            {
                return 0;
            }

            var matches = System.Text.RegularExpressions.Regex.Matches(
                visibleDialogue,
                @"(?:(?:收你|算你|收|仅收|只需|只要|一共|合计|总计|总共|抹零|折后|实付|应付|给你|作价|报价|要价|开价)\s*)?(\d{1,9})\s*(?:银|银币|块)");
            if (matches.Count == 0)
            {
                return 0;
            }

            Match last = matches[matches.Count - 1];
            return int.TryParse(last.Groups[1].Value, out int silver) ? silver : 0;
        }

public static string ExtractNeedTextFromDictionary(Dictionary<string, object> needDict)
        {
            if (needDict == null || needDict.Count == 0)
            {
                return string.Empty;
            }

            // Try keys in priority order — first match wins.
            string[] textKeys = { "text", "name", "item", "item_name", "label", "defName", "def_name", "description" };
            for (int i = 0; i < textKeys.Length; i++)
            {
                if (needDict.TryGetValue(textKeys[i], out object raw) && raw is string s && !string.IsNullOrWhiteSpace(s))
                {
                    return s.Trim();
                }
            }

            return string.Empty;
        }

public static string DescribeAirdropParameterType(Dictionary<string, object> parameters, string key)
        {
            if (parameters == null)
            {
                return "<parameters-null>";
            }

            if (string.IsNullOrWhiteSpace(key) || !parameters.TryGetValue(key, out object raw) || raw == null)
            {
                return "<missing>";
            }

            return raw.GetType().FullName ?? raw.GetType().Name;
        }
    }
}
