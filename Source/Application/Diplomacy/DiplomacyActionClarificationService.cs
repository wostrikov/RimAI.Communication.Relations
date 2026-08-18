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

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class DiplomacyActionClarificationService : DiplomacyDialogueCollaborator
{
    internal DiplomacyActionClarificationService(Dialog_DiplomacyDialogue owner) : base(owner) { }


internal const string SendInfoDirectiveStart = "[SendInfoDirective]";


internal const string SendInfoDirectiveEnd = "[/SendInfoDirective]";



internal static readonly Regex AirdropSingleAmountShorthandPattern = new Regex(
    @"^\s*(?<amount>\d{1,3}(?:,\d{3})*|\d{1,9})\s*(?:银|银币|silver|silvers)?\s*(?:[。.!！?？])?\s*$",
    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);



internal static void ApplyForcedSendInfoDirective(ParsedResponse response, string playerMessage)
{
    if (response == null || !TryParseSendInfoForcedActionDirective(playerMessage, out SendInfoForcedActionDirective directive))
    {
        return;
    }

    response.Actions ??= new List<AIAction>();
    response.Actions = response.Actions
        .Where(action => action != null && !IsConflictingForcedSendInfoAction(action.ActionType))
        .ToList();

    var parameters = new Dictionary<string, object>(StringComparer.Ordinal);
    if (directive.Waves.HasValue)
    {
        parameters["waves"] = directive.Waves.Value;
    }

    if (directive.ExplicitChallengeRequest)
    {
        parameters["explicit_challenge_request"] = true;
    }

    if (!string.IsNullOrWhiteSpace(directive.QuestDefName))
    {
        parameters["questDefName"] = directive.QuestDefName;
    }

    response.Actions.Add(new AIAction
    {
        ActionType = directive.ActionType,
        Parameters = parameters
    });
}



internal static bool IsConflictingForcedSendInfoAction(string actionType)
{
    if (string.IsNullOrWhiteSpace(actionType))
    {
        return false;
    }

    return string.Equals(actionType, AIActionNames.RequestRaid, StringComparison.Ordinal) ||
           string.Equals(actionType, AIActionNames.RequestRaidWaves, StringComparison.Ordinal) ||
           string.Equals(actionType, AIActionNames.RequestRaidCallEveryone, StringComparison.Ordinal) ||
           string.Equals(actionType, AIActionNames.RequestCaravan, StringComparison.Ordinal) ||
           string.Equals(actionType, AIActionNames.RequestVisitor, StringComparison.Ordinal);
}



internal static bool TryParseSendInfoForcedActionDirective(
    string playerMessage,
    out SendInfoForcedActionDirective directive)
{
    directive = null;
    if (string.IsNullOrWhiteSpace(playerMessage))
    {
        return false;
    }

    int start = playerMessage.IndexOf(SendInfoDirectiveStart, StringComparison.Ordinal);
    if (start < 0)
    {
        return false;
    }

    int end = playerMessage.IndexOf(SendInfoDirectiveEnd, start, StringComparison.Ordinal);
    if (end < 0)
    {
        return false;
    }

    string block = playerMessage.Substring(start + SendInfoDirectiveStart.Length, end - start - SendInfoDirectiveStart.Length);
    string actionType = ReadDirectiveValue(block, "force_action");
    if (string.IsNullOrWhiteSpace(actionType))
    {
        return false;
    }

    directive = new SendInfoForcedActionDirective
    {
        ActionType = actionType.Trim(),
        ExplicitChallengeRequest = string.Equals(ReadDirectiveValue(block, "explicit_challenge_request"), "true", StringComparison.OrdinalIgnoreCase),
        QuestDefName = ReadDirectiveValue(block, "questDefName")
    };

    if (int.TryParse(ReadDirectiveValue(block, "waves"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int waves))
    {
        directive.Waves = waves;
    }

    return true;
}



internal static string ReadDirectiveValue(string block, string key)
{
    if (string.IsNullOrWhiteSpace(block) || string.IsNullOrWhiteSpace(key))
    {
        return string.Empty;
    }

    string[] lines = block.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
    foreach (string rawLine in lines)
    {
        string line = rawLine?.Trim();
        if (string.IsNullOrWhiteSpace(line))
        {
            continue;
        }

        if (!line.StartsWith(key + ":", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        return line.Substring(key.Length + 1).Trim();
    }

    return string.Empty;
}



internal static bool TryMapAirdropAmountShorthandFollowup(
    ParsedResponse response,
    FactionDialogueSession currentSession,
    PendingDelayedActionIntent baseIntent,
    string playerMessage,
    int assistantRound)
{
    if (!CanMapAirdropAmountShorthand(response, currentSession, baseIntent, playerMessage, out int amount))
    {
        return false;
    }

    if (TryQueueMissingParameterClarification(response, currentSession, baseIntent, assistantRound))
    {
        return true;
    }

    AIAction mappedAction = BuildAirdropAmountShorthandAction(baseIntent, amount);
    if (response.Actions == null)
    {
        response.Actions = new List<AIAction>();
    }
    response.Actions.Add(mappedAction);

    if (string.IsNullOrWhiteSpace(response.DialogueText))
    {
        response.DialogueText = "RimChat_DiplomacyAirdropAmountMapped".Translate(amount, amount).ToString();
    }

    currentSession.pendingDelayedActionIntent = null;
    currentSession.lastDelayedActionIntent = DiplomacyActionPolicyService.CreatePendingDelayedIntent(mappedAction, assistantRound, false, string.Empty);
    return true;
}



internal static bool CanMapAirdropAmountShorthand(
    ParsedResponse response,
    FactionDialogueSession currentSession,
    PendingDelayedActionIntent baseIntent,
    string playerMessage,
    out int amount)
{
    amount = 0;
    if (response == null || currentSession == null || baseIntent == null)
    {
        return false;
    }

    if (!string.Equals(baseIntent.ActionType, AIActionNames.RequestItemAirdrop, StringComparison.Ordinal))
    {
        return false;
    }

    return TryParseSingleAirdropAmountShorthand(playerMessage, out amount);
}



internal static bool TryQueueMissingParameterClarification(
    ParsedResponse response,
    FactionDialogueSession currentSession,
    PendingDelayedActionIntent baseIntent,
    int assistantRound)
{
    string missingParameter = DiplomacyActionPolicyService.GetMissingRequiredParameter(baseIntent.ActionType, baseIntent.Parameters);
    if (string.IsNullOrWhiteSpace(missingParameter))
    {
        return false;
    }

    PendingDelayedActionIntent missingIntent = baseIntent.Clone();
    missingIntent.RequiredParameter = missingParameter;
    missingIntent.AwaitingConfirmation = true;
    missingIntent.UpdatedAssistantRound = assistantRound;
    currentSession.pendingDelayedActionIntent = missingIntent;
    if (string.IsNullOrWhiteSpace(response.DialogueText))
    {
        response.DialogueText = BuildMissingParameterClarification(
            missingIntent.ActionType,
            missingParameter,
            missingIntent.Parameters);
    }
    return true;
}



internal static AIAction BuildAirdropAmountShorthandAction(PendingDelayedActionIntent baseIntent, int amount)
{
    Dictionary<string, object> mappedParameters = DiplomacyActionPolicyService.CloneParameters(baseIntent.Parameters);
    mappedParameters.Remove("budget_silver");
    mappedParameters["payment_items"] = BuildDefaultSilverPaymentItems(amount);
    return new AIAction
    {
        ActionType = AIActionNames.RequestItemAirdrop,
        Parameters = mappedParameters,
        Reason = "intent_map_amount_shorthand"
    };
}



internal static bool TryParseSingleAirdropAmountShorthand(string playerMessage, out int amount)
{
    amount = 0;
    string rawText = (playerMessage ?? string.Empty).Trim();
    if (string.IsNullOrWhiteSpace(rawText))
    {
        return false;
    }

    Match amountMatch = AirdropSingleAmountShorthandPattern.Match(rawText);
    if (!amountMatch.Success)
    {
        return false;
    }

    string amountText = amountMatch.Groups["amount"].Value.Replace(",", string.Empty);
    if (!int.TryParse(amountText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedAmount) || parsedAmount <= 0)
    {
        return false;
    }

    amount = parsedAmount;
    return true;
}



internal static List<object> BuildDefaultSilverPaymentItems(int amount)
{
    var paymentLine = new Dictionary<string, object>(StringComparer.Ordinal)
    {
        ["item"] = "Silver",
        ["count"] = amount
    };
    return new List<object> { paymentLine };
}



internal static string BuildMissingParameterClarification(
    string actionType,
    string missingParameter,
    Dictionary<string, object> parameters)
{
    switch (actionType)
    {
        case AIActionNames.RequestItemAirdrop:
            return "你这次要我空投什么物资？你准备用哪些物资支付（或直接说一个银币金额）？";
        case AIActionNames.RequestAid:
            return "你要哪类援助：军事、医疗还是资源？";
        case AIActionNames.TriggerIncident:
            return "你要我触发哪个事件（defName）？";
        case AIActionNames.CreateQuest:
            return "你要发布哪一个任务模板（questDefName）？";
        default:
            return $"要继续这个请求，我还需要补充参数：{missingParameter}。";
    }
}



internal static string BuildResendConfirmationQuestion(PendingDelayedActionIntent intent)
{
    string summary = BuildIntentSummary(intent);
    return $"你是要我按这条请求再执行一次吗：{summary}？请回复“确认”或“取消”。";
}



internal static string BuildConfirmationAcceptedLine(PendingDelayedActionIntent intent)
{
    return $"明白，我按你确认的内容继续安排：{BuildIntentSummary(intent)}。";
}



internal static string BuildDedupeClarification(PendingDelayedActionIntent intent)
{
    return "这条请求刚刚处理过，为避免重复执行，我先不重复提交。";
}



internal static string BuildIntentSummary(PendingDelayedActionIntent intent)
{
    if (intent == null)
    {
        return "无可用请求";
    }

    Dictionary<string, object> parameters = intent.Parameters ?? new Dictionary<string, object>();
    switch (intent.ActionType)
    {
        case AIActionNames.RequestItemAirdrop:
            string need = GetParameterText(parameters, "need", "未指定物资");
            string payment = BuildPaymentIntentSummary(parameters);
            return $"空投 {need}（支付：{payment}）";
        case AIActionNames.RequestCaravan:
            return $"请求商队（goods={GetParameterText(parameters, "goods", "未指定")})";
        case AIActionNames.RequestAid:
            return $"请求援助（type={GetParameterText(parameters, "type", "未指定")})";
        case AIActionNames.RequestRaid:
            return $"请求袭击（strategy={GetParameterText(parameters, "strategy", "未指定")}）";
        case AIActionNames.TriggerIncident:
            return $"触发事件（defName={GetParameterText(parameters, "defName", "未指定")})";
        case AIActionNames.CreateQuest:
            return $"创建任务（questDefName={GetParameterText(parameters, "questDefName", "未指定")})";
        default:
            return intent.ActionType;
    }
}



internal static string GetParameterText(Dictionary<string, object> parameters, string key, string fallback)
{
    if (parameters != null && parameters.TryGetValue(key, out object value) && value != null)
    {
        string text = value.ToString();
        if (!string.IsNullOrWhiteSpace(text))
        {
            return text;
        }
    }

    return fallback;
}



internal static string BuildPaymentIntentSummary(Dictionary<string, object> parameters)
{
    if (parameters == null ||
        !parameters.TryGetValue("payment_items", out object raw) ||
        !(raw is IEnumerable<object> rows))
    {
        return "未指定";
    }

    List<string> items = rows
        .OfType<Dictionary<string, object>>()
        .Select(row =>
        {
            string item = GetDictionaryText(row, "item");
            string count = GetDictionaryText(row, "count");
            if (string.IsNullOrWhiteSpace(item) || string.IsNullOrWhiteSpace(count))
            {
                return string.Empty;
            }

            return $"{item}x{count}";
        })
        .Where(text => !string.IsNullOrWhiteSpace(text))
        .Take(2)
        .ToList();
    if (items.Count == 0)
    {
        return "未指定";
    }

    return string.Join(" + ", items);
}



internal static string GetDictionaryText(Dictionary<string, object> values, string key)
{
    if (values == null || !values.TryGetValue(key, out object raw) || raw == null)
    {
        return string.Empty;
    }

    return raw.ToString()?.Trim() ?? string.Empty;
}
}
