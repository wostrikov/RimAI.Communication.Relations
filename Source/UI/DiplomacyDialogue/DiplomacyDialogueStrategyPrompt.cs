using Ustas.RimAI.Communication.Relations.WorldState;
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

internal sealed class DiplomacyDialogueStrategyPrompt : DiplomacyDialogueCollaborator
{
    internal DiplomacyDialogueStrategyPrompt(Dialog_DiplomacyDialogue owner) : base(owner) { }



internal List<ChatMessageData> BuildStrategySuggestionRequestMessages(FactionDialogueSession currentSession, Faction currentFaction)
{
    var messages = new List<ChatMessageData>();
    string systemPrompt = BuildStrategySystemPrompt(currentFaction, currentSession);
    if (!string.IsNullOrWhiteSpace(systemPrompt))
    {
        messages.Add(new ChatMessageData { role = "system", content = systemPrompt });
    }

    AppendRecentDialogueForStrategy(messages, currentSession);
    messages.Add(new ChatMessageData
    {
        role = "user",
        content = DiplomacyDialogueStrategyUi.StrategyFollowupUserInstruction
    });
    return messages;
}



internal string BuildStrategySystemPrompt(Faction currentFaction, FactionDialogueSession currentSession)
{
    PromptPersistenceService.Instance.Initialize();
    var settings = RelationsMod.Settings;
    var tags = DiplomacySessionPromptBuilder.ParseSceneTagsCsv(settings?.DiplomacyManualSceneTagsCsv);
    var strategyContext = new DiplomacyStrategyPromptContext
    {
        NegotiatorContextText = Owner.Parts.StrategyContext.BuildStrategyPlayerContextPrompt(),
        StrategyFactPackText = Owner.Parts.StrategyContext.BuildStrategyFactPackForPrompt(currentSession, currentFaction),
        ScenarioDossierText = Owner.Parts.StrategyContext.BuildStrategyScenarioDossierPrompt(currentSession, currentFaction)
    };

    return PromptPersistenceService.Instance.BuildDiplomacyStrategySystemPrompt(
        currentFaction,
        PromptPersistenceService.Instance.LoadConfig(),
        tags,
        strategyContext);
}



internal void AppendRecentDialogueForStrategy(List<ChatMessageData> messages, FactionDialogueSession currentSession)
{
    if (messages == null || currentSession?.messages == null || currentSession.messages.Count == 0)
    {
        return;
    }

    List<ChatMessageData> compressedHistory =
        DialogueContextCompressionService.BuildFromDialogueMessages(currentSession.messages);
    for (int i = 0; i < compressedHistory.Count; i++)
    {
        ChatMessageData msg = compressedHistory[i];
        if (msg == null || string.IsNullOrWhiteSpace(msg.content))
        {
            continue;
        }

        messages.Add(new ChatMessageData
        {
            role = msg.role,
            content = msg.content.Trim()
        });
    }
}



internal PendingStrategySuggestion MapStrategySuggestion(StrategySuggestion source)
{
    if (source == null)
    {
        return null;
    }

    string strategyName = (source.StrategyName ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
    if (string.IsNullOrWhiteSpace(strategyName) || IsCodeLikeStrategyName(strategyName))
    {
        string labelSeed = $"{source.Content} {source.Reason}".Trim();
        strategyName = BuildStrategyLabelFromReply(labelSeed);
    }
    if (IsGenericStrategyLabel(strategyName))
    {
        strategyName = BuildStrategyLabelFromReply(source.Content ?? source.Reason ?? string.Empty);
    }
    if (strategyName.Length > 6)
    {
        strategyName = strategyName.Substring(0, 6);
    }

    string reason = (source.Reason ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
    if (string.IsNullOrWhiteSpace(reason))
    {
        reason = source.Content ?? string.Empty;
    }
    if (reason.Length > 80)
    {
        reason = reason.Substring(0, 80);
    }

    return new PendingStrategySuggestion
    {
        StrategyName = strategyName,
        FactReason = reason,
        StrategyKeywords = source.StrategyKeywords?.Take(5).ToList() ?? new List<string>(),
        Content = source.Content ?? string.Empty
    };
}



internal List<PendingStrategySuggestion> EnsureStrategySuggestionCount(List<PendingStrategySuggestion> suggestions)
{
    var result = (suggestions ?? new List<PendingStrategySuggestion>())
        .Where(s => s != null && !string.IsNullOrWhiteSpace(s.Content))
        .Take(DiplomacyDialogueStrategyUi.StrategySuggestionRequiredCount)
        .ToList();

    var basisPool = Owner.Parts.StrategyContext.BuildAttributeBasisPool();
    if (basisPool.Count == 0)
    {
        basisPool.Add("RimChat_StrategyFallbackBasis".Translate());
    }

    while (result.Count < DiplomacyDialogueStrategyUi.StrategySuggestionRequiredCount)
    {
        int index = result.Count;
        string reply = BuildDefaultStrategyReplyByIndex(index);
        string label = BuildDefaultStrategyNameByIndex(index);
        string basis = basisPool[index % basisPool.Count];
        result.Add(new PendingStrategySuggestion
        {
            StrategyName = label,
            FactReason = basis,
            StrategyKeywords = new List<string> { label },
            Content = reply
        });
    }

    ApplyAttributeBasisFallback(result);
    return result;
}



internal string BuildDefaultStrategyReplyByIndex(int index)
{
    return index switch
    {
        0 => "RimChat_StrategyFallbackReply1".Translate(),
        1 => "RimChat_StrategyFallbackReply2".Translate(),
        _ => "RimChat_StrategyFallbackReply3".Translate()
    };
}



internal string BuildDefaultStrategyNameByIndex(int index)
{
    return index switch
    {
        0 => "RimChat_StrategyLabelSocialLeverage".Translate(),
        1 => "RimChat_StrategyLabelResourceTransfer".Translate(),
        _ => "RimChat_StrategyLabelRiskBuffer".Translate()
    };
}



internal string BuildStrategyLabelFromReply(string reply)
{
    string cleaned = (reply ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
    if (cleaned.Length == 0)
    {
        return "RimChat_StrategyFallbackLabel".Translate();
    }

    string lower = cleaned.ToLowerInvariant();
    if (Owner.Parts.StrategyContext.ContainsAnyStrategyToken(lower, "social", "口才", "谈判", "交涉", "说服"))
    {
        return "RimChat_StrategyLabelSocialLeverage".Translate();
    }
    if (Owner.Parts.StrategyContext.ContainsAnyStrategyToken(lower, "trade", "贸易", "资源", "组件", "物资", "代工"))
    {
        return "RimChat_StrategyLabelResourceTransfer".Translate();
    }
    if (Owner.Parts.StrategyContext.ContainsAnyStrategyToken(lower, "weak", "示弱", "弱势"))
    {
        return "RimChat_StrategyLabelWeakPosture".Translate();
    }
    if (Owner.Parts.StrategyContext.ContainsAnyStrategyToken(lower, "risk", "风险", "防御", "人口", "缓冲"))
    {
        return "RimChat_StrategyLabelRiskBuffer".Translate();
    }
    if (Owner.Parts.StrategyContext.ContainsAnyStrategyToken(lower, "respect", "trust", "goodwill", "关系", "信任", "亲密", "尊重"))
    {
        return "RimChat_StrategyLabelRelationRepair".Translate();
    }
    if (Owner.Parts.StrategyContext.ContainsAnyStrategyToken(lower, "emotion", "情绪", "共鸣", "安抚"))
    {
        return "RimChat_StrategyLabelEmotionalResonance".Translate();
    }

    string label = cleaned.TrimStart('-', '*', '#').Trim();
    if (label.Length > 6)
    {
        label = label.Substring(0, 6);
    }
    return label;
}



internal bool IsCodeLikeStrategyName(string text)
{
    if (string.IsNullOrWhiteSpace(text))
    {
        return false;
    }

    string value = text.Trim();
    if (value.Contains("_"))
    {
        return true;
    }

    for (int i = 0; i < value.Length; i++)
    {
        char ch = value[i];
        bool asciiWord = (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9') || ch == '-';
        if (!asciiWord)
        {
            return false;
        }
    }
    return true;
}



internal bool IsGenericStrategyLabel(string label)
{
    if (string.IsNullOrWhiteSpace(label))
    {
        return true;
    }

    string normalized = label.Trim().ToLowerInvariant();
    return normalized == "策略建议" ||
           normalized == "建议" ||
           normalized == "strategy" ||
           normalized == "proposal";
}



internal int? ExtractIntNearKeyword(string text, params string[] keywords)
{
    if (string.IsNullOrWhiteSpace(text) || keywords == null || keywords.Length == 0)
    {
        return null;
    }

    foreach (string keyword in keywords)
    {
        string pattern = $"{keyword}[^0-9]{{0,8}}(\\d{{1,3}})";
        var match = System.Text.RegularExpressions.Regex.Match(
            text,
            pattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out int parsed))
        {
            return parsed;
        }
    }

    return null;
}



internal string ExtractWealthTier(string text)
{
    if (string.IsNullOrWhiteSpace(text))
    {
        return string.Empty;
    }

    string value = text.ToLowerInvariant();
    if (Owner.Parts.StrategyContext.ContainsAnyStrategyToken(value, "very_low", "极低", "贫困"))
    {
        return "极低";
    }
    if (Owner.Parts.StrategyContext.ContainsAnyStrategyToken(value, "low", "较低", "低"))
    {
        return "低";
    }
    if (Owner.Parts.StrategyContext.ContainsAnyStrategyToken(value, "very_high", "极高"))
    {
        return "极高";
    }
    if (Owner.Parts.StrategyContext.ContainsAnyStrategyToken(value, "high", "较高", "高"))
    {
        return "高";
    }
    if (Owner.Parts.StrategyContext.ContainsAnyStrategyToken(value, "mid", "medium", "中"))
    {
        return "中";
    }

    var match = System.Text.RegularExpressions.Regex.Match(value, "wealth[^0-9]{0,8}(\\d{4,7})");
    if (match.Success && int.TryParse(match.Groups[1].Value, out int wealth))
    {
        if (wealth >= 250000) return "极高";
        if (wealth >= 120000) return "高";
        if (wealth >= 50000) return "中";
        if (wealth >= 15000) return "低";
        return "极低";
    }

    return string.Empty;
}



internal void ApplyAttributeBasisFallback(List<PendingStrategySuggestion> suggestions)
{
    if (suggestions == null || suggestions.Count == 0)
    {
        return;
    }

    var basisPool = Owner.Parts.StrategyContext.BuildAttributeBasisPool();
    if (basisPool.Count == 0)
    {
        basisPool.Add("RimChat_StrategyFallbackBasis".Translate());
    }

    for (int i = 0; i < suggestions.Count; i++)
    {
        var item = suggestions[i];
        if (item == null)
        {
            continue;
        }

        if (Owner.Parts.StrategyContext.IsGenericBasis(item.FactReason))
        {
            item.FactReason = basisPool[i % basisPool.Count];
            continue;
        }

        if (!Owner.Parts.StrategyContext.HasFactReference(item.FactReason))
        {
            item.FactReason = $"{basisPool[i % basisPool.Count]} | {item.FactReason}";
        }
    }
}
}

