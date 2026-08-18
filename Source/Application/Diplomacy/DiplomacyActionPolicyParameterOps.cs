using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Context;
using Ustas.RimAI.Communication.Relations.Dialogue;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Communication.Relations.Guards;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.PawnRpgPush;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.UI;

namespace Ustas.RimAI.Communication.Relations.UI
{
    /// <summary>
    /// Parameter clone/signature helpers for delayed diplomacy actions.
    /// </summary>
    internal static class DiplomacyActionPolicyParameterOps
    {
internal static Dictionary<string, object> CloneParameters(Dictionary<string, object> source)
{
    var clone = new Dictionary<string, object>();
    if (source == null)
    {
        return clone;
    }

    foreach (KeyValuePair<string, object> entry in source)
    {
        clone[entry.Key] = entry.Value;
    }
    return clone;
}



internal static string BuildActionSignature(string actionType, Dictionary<string, object> parameters)
{
    if (string.IsNullOrWhiteSpace(actionType))
    {
        return string.Empty;
    }

    var sb = new StringBuilder();
    sb.Append(actionType.Trim().ToLowerInvariant());
    if (parameters == null || parameters.Count == 0)
    {
        return sb.ToString();
    }

    foreach (KeyValuePair<string, object> entry in parameters.OrderBy(item => item.Key, StringComparer.Ordinal))
    {
        sb.Append('|');
        sb.Append((entry.Key ?? string.Empty).Trim().ToLowerInvariant());
        sb.Append('=');
        sb.Append(NormalizeParameterValue(entry.Value));
    }

    return sb.ToString();
}



internal static string NormalizeParameterValue(object value)
{
    if (value == null)
    {
        return string.Empty;
    }

    if (value is float floatValue)
    {
        return floatValue.ToString(CultureInfo.InvariantCulture);
    }
    if (value is double doubleValue)
    {
        return doubleValue.ToString(CultureInfo.InvariantCulture);
    }
    if (value is decimal decimalValue)
    {
        return decimalValue.ToString(CultureInfo.InvariantCulture);
    }

    return value.ToString()?.Trim().ToLowerInvariant() ?? string.Empty;
}



internal static string GetMissingRequiredParameter(string actionType, Dictionary<string, object> parameters)
{
    switch (actionType)
    {
        case AIActionNames.RequestItemAirdrop:
            return HasNonEmptyParameter(parameters, "need") ? string.Empty : "need";
        case AIActionNames.RequestAid:
            return HasNonEmptyParameter(parameters, "type") ? string.Empty : "type";
        case AIActionNames.TriggerIncident:
            return HasNonEmptyParameter(parameters, "defName") ? string.Empty : "defName";
        case AIActionNames.CreateQuest:
            return HasNonEmptyParameter(parameters, "questDefName") ? string.Empty : "questDefName";
        default:
            return string.Empty;
    }
}



internal static bool HasNonEmptyParameter(Dictionary<string, object> parameters, string key)
{
    if (parameters == null || string.IsNullOrWhiteSpace(key))
    {
        return false;
    }

    if (!parameters.TryGetValue(key, out object value) || value == null)
    {
        return false;
    }

    return !string.IsNullOrWhiteSpace(value.ToString());
}
    }
}
