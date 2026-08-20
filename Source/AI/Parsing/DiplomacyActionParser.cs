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

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

    
        #region Cluster forwards
        public static List<AIAction> ParseActionsFromJson(string json, string visibleDialogue = null) => DiplomacyActionParserSlice1.ParseActionsFromJson(json, visibleDialogue);
        public static void AddActionIfValid(List<AIAction> actions, string actionType, Dictionary<string, object> parameters, string reason, List<int> keptRansomTargetIds, List<int> droppedDuplicateRansomTargetIds, ref int keptRansomWithoutTargetCount, string visibleDialogue = null) => DiplomacyActionParserSlice1.AddActionIfValid(actions, actionType, parameters, reason, keptRansomTargetIds, droppedDuplicateRansomTargetIds, ref keptRansomWithoutTargetCount, visibleDialogue);
        public static bool IsDuplicateRansomActionForTarget(List<AIAction> actions, int targetPawnLoadId) => DiplomacyActionParserSlice1.IsDuplicateRansomActionForTarget(actions, targetPawnLoadId);
        public static bool TryGetRansomTargetPawnLoadId(Dictionary<string, object> parameters, out int targetPawnLoadId) => DiplomacyActionParserSlice1.TryGetRansomTargetPawnLoadId(parameters, out targetPawnLoadId);
        public static void LogRansomParseSummary(List<int> keptRansomTargetIds, List<int> droppedDuplicateRansomTargetIds, int keptRansomWithoutTargetCount) => DiplomacyActionParserSlice1.LogRansomParseSummary(keptRansomTargetIds, droppedDuplicateRansomTargetIds, keptRansomWithoutTargetCount);
        public static bool HasValidAirdropBarterParameters(Dictionary<string, object> parameters, string visibleDialogue = null) => DiplomacyActionParserSlice1.HasValidAirdropBarterParameters(parameters, visibleDialogue);
        public static void NormalizeAirdropBarterParameters(Dictionary<string, object> parameters, string visibleDialogue = null) => DiplomacyActionParserSlice1.NormalizeAirdropBarterParameters(parameters, visibleDialogue);
        public static int ExtractSingleExplicitAirdropNeedCount(string need) => DiplomacyActionParserSlice1.ExtractSingleExplicitAirdropNeedCount(need);
        public static void NormalizeAirdropPaymentItem(Dictionary<string, object> item) => DiplomacyActionParserSlice1.NormalizeAirdropPaymentItem(item);
        public static int TryInferSilverCountFromDialogue(string visibleDialogue) => DiplomacyActionParserSlice1.TryInferSilverCountFromDialogue(visibleDialogue);
        public static string ExtractNeedTextFromDictionary(Dictionary<string, object> needDict) => DiplomacyActionParserSlice1.ExtractNeedTextFromDictionary(needDict);
        public static string DescribeAirdropParameterType(Dictionary<string, object> parameters, string key) => DiplomacyActionParserSlice1.DescribeAirdropParameterType(parameters, key);
        public static string DescribeAirdropPaymentItemsCount(Dictionary<string, object> parameters) => DiplomacyActionParserSlice2.DescribeAirdropPaymentItemsCount(parameters);
        public static string DescribeAirdropPaymentItem0Type(Dictionary<string, object> parameters) => DiplomacyActionParserSlice2.DescribeAirdropPaymentItem0Type(parameters);
        public static string DescribeAirdropPaymentItem0Keys(Dictionary<string, object> parameters) => DiplomacyActionParserSlice2.DescribeAirdropPaymentItem0Keys(parameters);
        public static bool HasValidPrisonerRansomParameters(Dictionary<string, object> parameters, out string invalidParameter, out string paymentModeRaw, out string paymentModeNormalized, out bool paymentModePassthrough) => DiplomacyActionParserSlice2.HasValidPrisonerRansomParameters(parameters, out invalidParameter, out paymentModeRaw, out paymentModeNormalized, out paymentModePassthrough);
        public static bool TryNormalizePrisonerRansomPaymentMode(string rawMode, out string normalizedMode, out bool passthroughToExecution) => DiplomacyActionParserSlice2.TryNormalizePrisonerRansomPaymentMode(rawMode, out normalizedMode, out passthroughToExecution);
        public static string FormatRansomLogValue(string value) => DiplomacyActionParserSlice2.FormatRansomLogValue(value);
        public static void NormalizePrisonerRansomParameters(Dictionary<string, object> parameters) => DiplomacyActionParserSlice2.NormalizePrisonerRansomParameters(parameters);
        public static bool TryReadLoosePositiveIntegerByAliases(Dictionary<string, object> values, out int parsed, params string[] aliases) => DiplomacyActionParserSlice2.TryReadLoosePositiveIntegerByAliases(values, out parsed, aliases);
        public static bool TryReadStringByAliases(Dictionary<string, object> values, out string text, params string[] aliases) => DiplomacyActionParserSlice2.TryReadStringByAliases(values, out text, aliases);
        public static bool TryReadParameterByAliases(Dictionary<string, object> values, out object raw, params string[] aliases) => DiplomacyActionParserSlice2.TryReadParameterByAliases(values, out raw, aliases);
        public static string FindDictionaryKey(Dictionary<string, object> values, string expected) => DiplomacyActionParserSlice2.FindDictionaryKey(values, expected);
        public static void SetCanonicalParameter(Dictionary<string, object> values, string canonicalKey, object value) => DiplomacyActionParserSlice2.SetCanonicalParameter(values, canonicalKey, value);
        public static bool TryReadLoosePositiveIntegerParameter(Dictionary<string, object> values, string key, out int parsed) => DiplomacyActionParserSlice2.TryReadLoosePositiveIntegerParameter(values, key, out parsed);
        public static bool TryReadLoosePositiveInteger(object raw, out int parsed) => DiplomacyActionParserSlice2.TryReadLoosePositiveInteger(raw, out parsed);
        public static string NormalizeNumberishText(string raw) => DiplomacyActionParserSlice2.NormalizeNumberishText(raw);
        public static string ExtractDigits(string source) => DiplomacyActionParserSlice2.ExtractDigits(source);
        public static bool HasNonEmptyText(Dictionary<string, object> values, string key, bool requireString = false) => DiplomacyActionParserSlice2.HasNonEmptyText(values, key, requireString);
        public static bool HasPositiveInteger(Dictionary<string, object> values, string key) => DiplomacyActionParserSlice2.HasPositiveInteger(values, key);
        #endregion
}

}
