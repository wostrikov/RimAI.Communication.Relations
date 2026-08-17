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
    /// <summary>Diplomacy action catalog, whitelist, airdrop and ransom parameter parse.</summary>
    public static class DiplomacyActionParser
    {
        public static bool IsValidAction(string action)
        {
            return DiplomacyActionCatalog.IsValidAction(action);
        }

        public static string NormalizeActionName(string action)
        {
            return DiplomacyActionCatalog.NormalizeActionName(action);
        }

        public static List<AIAction> ParseActionsFromJson(string json, string visibleDialogue = null)
        {
            var actions = new List<AIAction>();
            var keptRansomTargetIds = new List<int>();
            var droppedDuplicateRansomTargetIds = new List<int>();
            int keptRansomWithoutTargetCount = 0;

            string trimmedJson = (json ?? string.Empty).Trim();
            string actionsArray = trimmedJson.StartsWith("[", StringComparison.Ordinal)
                ? trimmedJson
                : JsonLooseObjectParser.ExtractJsonArray(trimmedJson, "actions");
            if (string.IsNullOrEmpty(actionsArray))
            {
                return actions;
            }

            foreach (string actionObj in JsonLooseObjectParser.SplitJsonObjects(actionsArray))
            {
                string actionType = JsonLooseObjectParser.ExtractJsonString(actionObj, "action");
                if (string.IsNullOrEmpty(actionType))
                {
                    continue;
                }
                string reason = JsonLooseObjectParser.ExtractJsonString(actionObj, "reason");
                string parametersJson = JsonLooseObjectParser.ExtractJsonObject(actionObj, "parameters");
                var parameters = string.IsNullOrEmpty(parametersJson)
                    ? new Dictionary<string, object>()
                    : JsonLooseObjectParser.ParseParameters(parametersJson);

                string normalizedAction = NormalizeActionName(actionType);
                if (string.Equals(normalizedAction, AIActionNames.CreateQuest, StringComparison.Ordinal))
                {
                    string questDefName = JsonLooseObjectParser.ExtractJsonString(actionObj, "questDefName");
                    if (string.IsNullOrEmpty(questDefName))
                    {
                        questDefName = JsonLooseObjectParser.ExtractJsonString(actionObj, "defName");
                    }
                    if (!string.IsNullOrEmpty(questDefName) && !parameters.ContainsKey("questDefName"))
                    {
                        parameters["questDefName"] = questDefName;
                    }
                    if (!parameters.ContainsKey("questDefName"))
                    {
                        DebugLogger.WarningGated($"create_quest action missing questDefName. Raw actionObj: {actionObj}");
                    }
                }

                AddActionIfValid(
                    actions,
                    actionType,
                    parameters,
                    reason,
                    keptRansomTargetIds,
                    droppedDuplicateRansomTargetIds,
                    ref keptRansomWithoutTargetCount,
                    visibleDialogue);
            }

            LogRansomParseSummary(keptRansomTargetIds, droppedDuplicateRansomTargetIds, keptRansomWithoutTargetCount);
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
            string normalizedAction = NormalizeActionName(actionType);
            if (string.IsNullOrEmpty(normalizedAction) || normalizedAction == "none")
            {
                return;
            }

            if (!IsValidAction(normalizedAction))
            {
                DebugLogger.WarningGated($"Unknown AI action: {normalizedAction}");
                return;
            }

            if (parameters == null)
            {
                parameters = new Dictionary<string, object>();
            }

            if (string.Equals(normalizedAction, AIActionNames.RequestItemAirdrop, StringComparison.Ordinal) &&
                !HasValidAirdropBarterParameters(parameters, visibleDialogue))
            {
                bool needPresent = HasNonEmptyText(parameters, "need", requireString: true);
                string paymentItemsType = DescribeAirdropParameterType(parameters, "payment_items");
                string paymentItemsCount = DescribeAirdropPaymentItemsCount(parameters);
                string paymentItem0Type = DescribeAirdropPaymentItem0Type(parameters);
                string paymentItem0Keys = DescribeAirdropPaymentItem0Keys(parameters);
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
                if (!HasValidPrisonerRansomParameters(
                        parameters,
                        out string invalidParameter,
                        out string paymentModeRaw,
                        out string paymentModeNormalized,
                        out bool paymentModePassthrough))
                {
                    Log.Warning(
                        $"[RimAI.Relations] pay_prisoner_ransom parameters unresolved: missing_or_invalid={invalidParameter ?? "unknown"}, " +
                        $"payment_mode_raw={FormatRansomLogValue(paymentModeRaw)}, " +
                        $"payment_mode_normalized={FormatRansomLogValue(paymentModeNormalized)}, " +
                        $"passthrough_to_execution={paymentModePassthrough}. " +
                        "Dropping action in parser for fail-fast validation.");
                    return;
                }

                parameters.Remove("__ransom_missing_parameter");
                if (TryGetRansomTargetPawnLoadId(parameters, out int targetPawnLoadId))
                {
                    if (IsDuplicateRansomActionForTarget(actions, targetPawnLoadId))
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
                    $"payment_mode_raw={FormatRansomLogValue(paymentModeRaw)}, " +
                    $"payment_mode_normalized={FormatRansomLogValue(paymentModeNormalized)}, " +
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
                TryGetRansomTargetPawnLoadId(action.Parameters, out int existingTargetPawnLoadId) &&
                existingTargetPawnLoadId == targetPawnLoadId);
        }

        public static bool TryGetRansomTargetPawnLoadId(Dictionary<string, object> parameters, out int targetPawnLoadId)
        {
            targetPawnLoadId = 0;
            return TryReadLoosePositiveIntegerParameter(parameters, "target_pawn_load_id", out targetPawnLoadId);
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
            NormalizeAirdropBarterParameters(parameters, visibleDialogue);

            if (!HasNonEmptyText(parameters, "need", requireString: true))
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
                    !HasNonEmptyText(item, "item") ||
                    !HasPositiveInteger(item, "count"))
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
            if (TryReadStringByAliases(
                    parameters,
                    out string need,
                    "need",
                    "need_def",
                    "needDef",
                    "__airdrop_bound_need_def"))
            {
                string normalizedNeed = need.Trim();
                SetCanonicalParameter(parameters, "need", normalizedNeed);
                int explicitNeedCount = ExtractSingleExplicitAirdropNeedCount(normalizedNeed);
                if (explicitNeedCount > 0)
                {
                    SetCanonicalParameter(parameters, "count", explicitNeedCount);
                    parameters["__airdrop_explicit_need_count"] = explicitNeedCount;
                }
            }
            else if (TryReadParameterByAliases(parameters, out object rawNeed, "need") &&
                     rawNeed is Dictionary<string, object> needDict)
            {
                // AI sent need as a JSON object — try to salvage a text description.
                string extracted = ExtractNeedTextFromDictionary(needDict);
                if (!string.IsNullOrWhiteSpace(extracted))
                {
                    SetCanonicalParameter(parameters, "need", extracted);
                }
            }

            if (!TryReadParameterByAliases(parameters, out object rawPaymentItems, "payment_items") ||
                !(rawPaymentItems is IEnumerable<object> paymentItems))
            {
                return;
            }

            var normalizedItems = new List<object>();
            foreach (object row in paymentItems)
            {
                if (row is Dictionary<string, object> item)
                {
                    NormalizeAirdropPaymentItem(item);
                    // If count is still missing, try to infer from visible_dialogue.
                    if (!item.ContainsKey("count") && !string.IsNullOrWhiteSpace(visibleDialogue))
                    {
                        int inferred = TryInferSilverCountFromDialogue(visibleDialogue);
                        if (inferred > 0)
                        {
                            SetCanonicalParameter(item, "count", inferred);
                            DebugLogger.Debug($"Inferred airdrop payment count={inferred} from visible_dialogue.");
                        }
                    }
                    normalizedItems.Add(item);
                    continue;
                }

                normalizedItems.Add(row);
            }

            SetCanonicalParameter(parameters, "payment_items", normalizedItems);
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

            if (TryReadStringByAliases(item, out string value, "item", "defName", "def_name", "thingDef", "thing_def"))
            {
                SetCanonicalParameter(item, "item", value.Trim());
            }

            // Normalise count from common aliases the AI may use.
            if (TryReadParameterByAliases(item, out object rawCount, "count", "amount", "quantity", "qty", "price", "value", "silver"))
            {
                if (TryReadLoosePositiveInteger(rawCount, out int count))
                {
                    SetCanonicalParameter(item, "count", count);
                }
            }
        }

        public static int TryInferSilverCountFromDialogue(string visibleDialogue)
        {
            if (string.IsNullOrWhiteSpace(visibleDialogue))
            {
                return 0;
            }

            // Match patterns like "收你220银币", "220银", "一共370银币".
            // Take the last match — it's typically the final quoted price.
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

        public static string DescribeAirdropPaymentItemsCount(Dictionary<string, object> parameters)
        {
            if (parameters == null ||
                !parameters.TryGetValue("payment_items", out object rawItems) ||
                rawItems == null)
            {
                return "<missing>";
            }

            if (!(rawItems is IEnumerable<object> paymentItems))
            {
                return "<not-enumerable>";
            }

            return paymentItems.Count().ToString();
        }

        public static string DescribeAirdropPaymentItem0Type(Dictionary<string, object> parameters)
        {
            if (parameters == null ||
                !parameters.TryGetValue("payment_items", out object rawItems) ||
                rawItems == null)
            {
                return "<missing>";
            }

            if (!(rawItems is IEnumerable<object> paymentItems))
            {
                return "<not-enumerable>";
            }

            object first = paymentItems.FirstOrDefault();
            if (first == null)
            {
                return "<empty>";
            }

            return first.GetType().FullName ?? first.GetType().Name;
        }

        public static string DescribeAirdropPaymentItem0Keys(Dictionary<string, object> parameters)
        {
            if (parameters == null ||
                !parameters.TryGetValue("payment_items", out object rawItems) ||
                rawItems == null)
            {
                return "<missing>";
            }

            if (!(rawItems is IEnumerable<object> paymentItems))
            {
                return "<not-enumerable>";
            }

            object first = paymentItems.FirstOrDefault();
            if (!(first is Dictionary<string, object> item))
            {
                return "<not-dictionary>";
            }

            return item.Count <= 0
                ? "<no-keys>"
                : string.Join(",", item.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase));
        }

        public static bool HasValidPrisonerRansomParameters(
            Dictionary<string, object> parameters,
            out string invalidParameter,
            out string paymentModeRaw,
            out string paymentModeNormalized,
            out bool paymentModePassthrough)
        {
            invalidParameter = string.Empty;
            paymentModeRaw = string.Empty;
            paymentModeNormalized = string.Empty;
            paymentModePassthrough = false;
            TryReadStringByAliases(
                parameters,
                out paymentModeRaw,
                "payment_mode",
                "paymentMode",
                "pay_mode",
                "payMode",
                "mode");

            NormalizePrisonerRansomParameters(parameters);

            // target_pawn_load_id is optional at parse stage; execution layer can bind from session state.
            if (TryReadLoosePositiveIntegerParameter(parameters, "target_pawn_load_id", out int targetPawnLoadId))
            {
                parameters["target_pawn_load_id"] = targetPawnLoadId;
            }

            if (!TryReadLoosePositiveIntegerParameter(parameters, "offer_silver", out int offerSilver))
            {
                invalidParameter = "offer_silver";
                return false;
            }

            parameters["offer_silver"] = offerSilver;
            if (parameters == null || !parameters.TryGetValue("payment_mode", out object modeObj) || modeObj == null)
            {
                paymentModeNormalized = "(omitted)";
                return true;
            }

            string mode = modeObj.ToString()?.Trim().ToLowerInvariant() ?? string.Empty;
            if (TryNormalizePrisonerRansomPaymentMode(mode, out string normalizedMode, out bool passthroughToExecution))
            {
                paymentModeNormalized = normalizedMode;
                paymentModePassthrough = passthroughToExecution;
                if (!string.IsNullOrWhiteSpace(normalizedMode))
                {
                    parameters["payment_mode"] = normalizedMode;
                }

                return true;
            }

            paymentModeNormalized = "(omitted)";
            return true;
        }

        public static bool TryNormalizePrisonerRansomPaymentMode(
            string rawMode,
            out string normalizedMode,
            out bool passthroughToExecution)
        {
            normalizedMode = string.Empty;
            passthroughToExecution = false;
            if (string.IsNullOrWhiteSpace(rawMode))
            {
                return false;
            }

            string mode = rawMode.Trim().ToLowerInvariant();
            switch (mode)
            {
                case "silver":
                case "银币":
                case "银":
                case "coin":
                case "coins":
                case "cash":
                    normalizedMode = "silver";
                    return true;
                default:
                    normalizedMode = mode;
                    passthroughToExecution = true;
                    return true;
            }
        }

        public static string FormatRansomLogValue(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "(omitted)"
                : value.Trim();
        }

        public static void NormalizePrisonerRansomParameters(Dictionary<string, object> parameters)
        {
            if (parameters == null || parameters.Count == 0)
            {
                return;
            }

            if (TryReadLoosePositiveIntegerByAliases(
                    parameters,
                    out int targetPawnLoadId,
                    "target_pawn_load_id",
                    "targetPawnLoadId",
                    "target_pawn_id",
                    "targetPawnId",
                    "prisoner_load_id",
                    "prisonerLoadId",
                    "pawn_load_id",
                    "pawnLoadId",
                    "pawn_id",
                    "target_id"))
            {
                SetCanonicalParameter(parameters, "target_pawn_load_id", targetPawnLoadId);
            }

            if (TryReadLoosePositiveIntegerByAliases(
                    parameters,
                    out int offerSilver,
                    "offer_silver",
                    "offerSilver",
                    "offered_silver",
                    "offeredSilver",
                    "silver",
                    "amount",
                    "ransom_silver",
                    "ransomSilver"))
            {
                SetCanonicalParameter(parameters, "offer_silver", offerSilver);
            }

            if (TryReadStringByAliases(
                    parameters,
                    out string paymentMode,
                    "payment_mode",
                    "paymentMode",
                    "pay_mode",
                    "payMode",
                    "mode"))
            {
                SetCanonicalParameter(parameters, "payment_mode", paymentMode.Trim().ToLowerInvariant());
            }
        }

        public static bool TryReadLoosePositiveIntegerByAliases(
            Dictionary<string, object> values,
            out int parsed,
            params string[] aliases)
        {
            parsed = 0;
            if (!TryReadParameterByAliases(values, out object raw, aliases))
            {
                return false;
            }

            return TryReadLoosePositiveInteger(raw, out parsed);
        }

        public static bool TryReadStringByAliases(
            Dictionary<string, object> values,
            out string text,
            params string[] aliases)
        {
            text = string.Empty;
            if (!TryReadParameterByAliases(values, out object raw, aliases) || raw == null)
            {
                return false;
            }

            if (raw is string str)
            {
                text = str;
                return !string.IsNullOrWhiteSpace(text);
            }

            // Reject complex objects — calling ToString() on a Dictionary or List
            // produces a useless type name that pollutes downstream tokenization.
            return false;
        }

        public static bool TryReadParameterByAliases(
            Dictionary<string, object> values,
            out object raw,
            params string[] aliases)
        {
            raw = null;
            if (values == null || values.Count == 0 || aliases == null || aliases.Length == 0)
            {
                return false;
            }

            foreach (string alias in aliases)
            {
                string key = FindDictionaryKey(values, alias);
                if (string.IsNullOrWhiteSpace(key) || !values.TryGetValue(key, out object value) || value == null)
                {
                    continue;
                }

                raw = value;
                return true;
            }

            return false;
        }

        public static string FindDictionaryKey(Dictionary<string, object> values, string expected)
        {
            if (values == null || string.IsNullOrWhiteSpace(expected))
            {
                return string.Empty;
            }

            foreach (string key in values.Keys)
            {
                if (string.Equals(key, expected, StringComparison.OrdinalIgnoreCase))
                {
                    return key;
                }
            }

            return string.Empty;
        }

        public static void SetCanonicalParameter(Dictionary<string, object> values, string canonicalKey, object value)
        {
            if (values == null || string.IsNullOrWhiteSpace(canonicalKey))
            {
                return;
            }

            string existing = FindDictionaryKey(values, canonicalKey);
            if (!string.IsNullOrWhiteSpace(existing) && !string.Equals(existing, canonicalKey, StringComparison.Ordinal))
            {
                values.Remove(existing);
            }

            values[canonicalKey] = value;
        }

        public static bool TryReadLoosePositiveIntegerParameter(Dictionary<string, object> values, string key, out int parsed)
        {
            parsed = 0;
            if (values == null || string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            string actualKey = FindDictionaryKey(values, key);
            if (string.IsNullOrWhiteSpace(actualKey) || !values.TryGetValue(actualKey, out object raw))
            {
                return false;
            }

            return TryReadLoosePositiveInteger(raw, out parsed);
        }

        public static bool TryReadLoosePositiveInteger(object raw, out int parsed)
        {
            parsed = 0;
            if (raw == null)
            {
                return false;
            }

            if (raw is int intValue)
            {
                parsed = intValue;
                return parsed > 0;
            }

            if (raw is long longValue)
            {
                if (longValue <= 0 || longValue > int.MaxValue)
                {
                    return false;
                }

                parsed = (int)longValue;
                return true;
            }

            if (raw is double doubleValue)
            {
                int rounded = (int)Math.Round(doubleValue);
                if (rounded <= 0 || Math.Abs(doubleValue - rounded) > 0.001d)
                {
                    return false;
                }

                parsed = rounded;
                return true;
            }

            string source = NormalizeNumberishText(raw.ToString());
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            if (int.TryParse(source, NumberStyles.Integer, CultureInfo.InvariantCulture, out int directParsed) && directParsed > 0)
            {
                parsed = directParsed;
                return true;
            }

            string digitsOnly = ExtractDigits(source);
            if (string.IsNullOrWhiteSpace(digitsOnly))
            {
                return false;
            }

            if (int.TryParse(digitsOnly, NumberStyles.Integer, CultureInfo.InvariantCulture, out int recovered) && recovered > 0)
            {
                parsed = recovered;
                return true;
            }

            return false;
        }

        public static string NormalizeNumberishText(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(raw.Length);
            foreach (char c in raw.Trim())
            {
                if (c >= '０' && c <= '９')
                {
                    sb.Append((char)('0' + (c - '０')));
                    continue;
                }

                if (c == '，' || c == ',')
                {
                    continue;
                }

                sb.Append(c);
            }

            return sb.ToString().Trim();
        }

        public static string ExtractDigits(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(source.Length);
            foreach (char c in source)
            {
                if (char.IsDigit(c))
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        public static bool HasNonEmptyText(Dictionary<string, object> values, string key, bool requireString = false)
        {
            if (values == null || !values.TryGetValue(key, out object raw) || raw == null)
            {
                return false;
            }

            if (requireString && !(raw is string))
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(raw.ToString());
        }

        public static bool HasPositiveInteger(Dictionary<string, object> values, string key)
        {
            if (values == null || !values.TryGetValue(key, out object raw) || raw == null)
            {
                return false;
            }

            if (raw is int intValue)
            {
                return intValue > 0;
            }

            if (raw is long longValue)
            {
                return longValue > 0 && longValue <= int.MaxValue;
            }

            string text = raw.ToString();
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed > 0;
        }

        /// <summary>/// 从JSON中提取字符串values
 ///</summary>
    }
}
