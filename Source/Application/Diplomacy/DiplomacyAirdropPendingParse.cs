using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Ustas.RimAI.Communication.Relations.UI;

internal static class DiplomacyAirdropPendingParse
{
    internal static readonly Regex AirdropPendingChoicePattern = new Regex(
        @"(?<!\d)(?<index>[1-9]\d?)(?!\d)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static readonly Regex AirdropPendingCountPattern = new Regex(
        @"(?<!\d)(?<count>\d{1,5})(?!\d)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static readonly Regex AirdropTradeCardNeedCountPattern = new Regex(
        @"(?:потрібно|need)\s+[^\r\n,.]*?(?:x|×)\s*(?<count>\d{1,5})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static readonly Regex AirdropPendingChineseQuantifierCountPattern = new Regex(
        @"(?<!\d)(?<count>\d{1,5})(?:\s*)(?:шт|штук|штуки|одиниць|одиниці|ящик|ящика|ящиків|комплект|комплекти|комплектів|пар|пари|набір|набори|наборів|пляшок|мисок|тарілок|чашок|бочок|мішків|коробок|звʼязок|стос)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static bool TryExtractAirdropRequestedCount(string playerMessage, out int requestedCount)
    {
        requestedCount = 0;
        if (string.IsNullOrWhiteSpace(playerMessage))
        {
            return false;
        }

        Match structuredNeedCountMatch = AirdropTradeCardNeedCountPattern.Match(playerMessage);
        if (structuredNeedCountMatch.Success &&
            int.TryParse(structuredNeedCountMatch.Groups["count"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int structuredNeedCount) &&
            structuredNeedCount > 0)
        {
            requestedCount = Math.Min(structuredNeedCount, 5000);
            return true;
        }

        Match quantifierMatch = AirdropPendingChineseQuantifierCountPattern.Match(playerMessage);
        if (quantifierMatch.Success &&
            int.TryParse(quantifierMatch.Groups["count"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int quantifierCount) &&
            quantifierCount > 0)
        {
            requestedCount = Math.Min(quantifierCount, 5000);
            return true;
        }

        MatchCollection matches = AirdropPendingCountPattern.Matches(playerMessage);
        if (matches == null || matches.Count <= 0)
        {
            return false;
        }

        int maxValue = 0;
        for (int i = 0; i < matches.Count; i++)
        {
            if (!int.TryParse(matches[i].Groups["count"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                continue;
            }

            maxValue = Math.Max(maxValue, value);
        }

        if (maxValue <= 5)
        {
            return false;
        }

        requestedCount = Math.Min(maxValue, 5000);
        return requestedCount > 0;
    }

    internal static int TryParseChineseChoiceIndex(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        if (text.Contains("один"))
        {
            return 1;
        }

        if (text.Contains("два") || text.Contains("пара"))
        {
            return 2;
        }

        if (text.Contains("три"))
        {
            return 3;
        }

        if (text.Contains("чотири"))
        {
            return 4;
        }

        if (text.Contains("пʼять"))
        {
            return 5;
        }

        return 0;
    }
}
