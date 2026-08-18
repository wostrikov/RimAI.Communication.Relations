using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Ustas.RimAI.Communication.Relations.UI;

internal static class DiplomacyParameterParse
{
    internal static bool TryReadPositiveInt(Dictionary<string, object> values, string key, out int parsed)
    {
        parsed = 0;
        if (values == null || string.IsNullOrWhiteSpace(key) || !values.TryGetValue(key, out object raw) || raw == null)
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

        string normalized = (raw.ToString() ?? string.Empty)
            .Trim()
            .Replace(",", string.Empty)
            .Replace("，", string.Empty);

        if (int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out int directParsed) && directParsed > 0)
        {
            parsed = directParsed;
            return true;
        }

        string digits = new string(normalized.Where(char.IsDigit).ToArray());
        if (int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out int recovered) && recovered > 0)
        {
            parsed = recovered;
            return true;
        }

        return false;
    }
}
