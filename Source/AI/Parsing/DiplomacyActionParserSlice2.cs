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
    internal static class DiplomacyActionParserSlice2
    {
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
            DiplomacyActionParser.TryReadStringByAliases(
                parameters,
                out paymentModeRaw,
                "payment_mode",
                "paymentMode",
                "pay_mode",
                "payMode",
                "mode");

            DiplomacyActionParser.NormalizePrisonerRansomParameters(parameters);

            // target_pawn_load_id is optional at parse stage; execution layer can bind from session state.
            if (DiplomacyActionParser.TryReadLoosePositiveIntegerParameter(parameters, "target_pawn_load_id", out int targetPawnLoadId))
            {
                parameters["target_pawn_load_id"] = targetPawnLoadId;
            }

            if (!DiplomacyActionParser.TryReadLoosePositiveIntegerParameter(parameters, "offer_silver", out int offerSilver))
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
            if (DiplomacyActionParser.TryNormalizePrisonerRansomPaymentMode(mode, out string normalizedMode, out bool passthroughToExecution))
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
                case "срібло":
                case "срібняк":
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

            if (DiplomacyActionParser.TryReadLoosePositiveIntegerByAliases(
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
                DiplomacyActionParser.SetCanonicalParameter(parameters, "target_pawn_load_id", targetPawnLoadId);
            }

            if (DiplomacyActionParser.TryReadLoosePositiveIntegerByAliases(
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
                DiplomacyActionParser.SetCanonicalParameter(parameters, "offer_silver", offerSilver);
            }

            if (DiplomacyActionParser.TryReadStringByAliases(
                    parameters,
                    out string paymentMode,
                    "payment_mode",
                    "paymentMode",
                    "pay_mode",
                    "payMode",
                    "mode"))
            {
                DiplomacyActionParser.SetCanonicalParameter(parameters, "payment_mode", paymentMode.Trim().ToLowerInvariant());
            }
        }

public static bool TryReadLoosePositiveIntegerByAliases(
            Dictionary<string, object> values,
            out int parsed,
            params string[] aliases)
        {
            parsed = 0;
            if (!DiplomacyActionParser.TryReadParameterByAliases(values, out object raw, aliases))
            {
                return false;
            }

            return DiplomacyActionParser.TryReadLoosePositiveInteger(raw, out parsed);
        }

public static bool TryReadStringByAliases(
            Dictionary<string, object> values,
            out string text,
            params string[] aliases)
        {
            text = string.Empty;
            if (!DiplomacyActionParser.TryReadParameterByAliases(values, out object raw, aliases) || raw == null)
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
                string key = DiplomacyActionParser.FindDictionaryKey(values, alias);
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

            string existing = DiplomacyActionParser.FindDictionaryKey(values, canonicalKey);
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

            string actualKey = DiplomacyActionParser.FindDictionaryKey(values, key);
            if (string.IsNullOrWhiteSpace(actualKey) || !values.TryGetValue(actualKey, out object raw))
            {
                return false;
            }

            return DiplomacyActionParser.TryReadLoosePositiveInteger(raw, out parsed);
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

            string source = DiplomacyActionParser.NormalizeNumberishText(raw.ToString());
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            if (int.TryParse(source, NumberStyles.Integer, CultureInfo.InvariantCulture, out int directParsed) && directParsed > 0)
            {
                parsed = directParsed;
                return true;
            }

            string digitsOnly = DiplomacyActionParser.ExtractDigits(source);
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
    }
}
