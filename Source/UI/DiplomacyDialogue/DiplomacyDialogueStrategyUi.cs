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

internal sealed class DiplomacyDialogueStrategyUi : DiplomacyDialogueCollaborator
{
    internal DiplomacyDialogueStrategyUi(Dialog_DiplomacyDialogue owner) : base(owner) { }


internal const int StrategySuggestionRequiredCount = 3;


internal const float StrategyButtonSpacing = 6f;


internal const float StrategyIconSlotWidth = 34f;


internal const float StrategyAnimSpeed = 10f;


internal const float StrategyIntroOffset = 5f;


internal const float StrategyStatusCollapsedHeight = 20f;


internal const float StrategyStatusExpandedHeight = Dialog_DiplomacyDialogue.STRATEGY_BAR_HEIGHT;


internal const int StrategyLabelDisplayMaxChars = 6;


internal const int StrategyBasisDisplayMaxChars = 8;


internal const int StrategyTooltipReplyMaxChars = 72;


internal const string StrategyFollowupUserInstruction =
    "Generate exactly 3 strategy suggestions for the current diplomacy context and latest turns. " +
    "Return exactly one JSON object only with key strategy_suggestions, and each item must include " +
    "strategy_name, reason, and content.";


internal float strategyBarAnimProgress = 0f;


internal float strategyStatusExpandProgress = 1f;


internal bool strategySuggestionRequestPending = false;


internal string strategySuggestionRequestId = null;


internal int strategyFxSignature = 0;


internal float strategyFxStartRealtime = -99f;


internal bool strategyStatusAnimInitialized = false;



internal void DrawControlsRow(Rect rect)
{
    Widgets.DrawBoxSolid(rect, new Color(0.1f, 0.1f, 0.13f));
    if (ShouldShowStrategySuggestionBar())
    {
        DrawStrategySuggestionBar(rect);
        return;
    }

    DrawStrategyStatusHint(rect);
}



internal void DrawStrategySuggestionBar(Rect rect)
{
    bool shouldShow = ShouldShowStrategySuggestionBar();
    float target = shouldShow ? 1f : 0f;
    strategyBarAnimProgress = Mathf.MoveTowards(strategyBarAnimProgress, target, Time.deltaTime * StrategyAnimSpeed);

    if (strategyBarAnimProgress <= 0.01f)
    {
        return;
    }

    float barAlpha = Mathf.SmoothStep(0f, 1f, strategyBarAnimProgress);
    GUI.color = new Color(1f, 1f, 1f, barAlpha);
    var list = session.pendingStrategySuggestions?.ToList();
    if (list == null || list.Count == 0)
    {
        GUI.color = Color.white;
        return;
    }
    int count = Mathf.Min(StrategySuggestionRequiredCount, list.Count);
    if (count <= 0)
    {
        GUI.color = Color.white;
        return;
    }
    float buttonWidth = (rect.width - (count - 1) * StrategyButtonSpacing) / count;
    float buttonHeight = rect.height - 4f;
    Rect barBgRect = new Rect(rect.x, rect.y + 2f, rect.width, buttonHeight);
    Widgets.DrawBoxSolid(barBgRect, new Color(0.13f, 0.16f, 0.2f, 0.52f * barAlpha));
    DrawStrategyAppearFx(rect, count, buttonWidth, buttonHeight, barAlpha, list);

    for (int i = 0; i < count; i++)
    {
        float itemProgress = Mathf.Clamp01((barAlpha - i * 0.06f) / 0.72f);
        if (itemProgress <= 0.01f)
        {
            continue;
        }

        float easedProgress = Mathf.SmoothStep(0f, 1f, itemProgress);
        float yOffset = (1f - easedProgress) * StrategyIntroOffset;
        Rect btnRect = new Rect(rect.x + i * (buttonWidth + StrategyButtonSpacing), rect.y + 2f + yOffset, buttonWidth, buttonHeight);
        var suggestion = list[i];

        Color old = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, easedProgress);
        if (Widgets.ButtonText(btnRect, BuildStrategyButtonLabel(suggestion)))
        {
            TrySendStrategySuggestion(suggestion);
            GUI.color = Color.white;
            return;
        }
        GUI.color = old;

        AddStrategyTooltip(btnRect, suggestion);
    }
    GUI.color = Color.white;
}



internal void DrawStrategyAppearFx(Rect rect, int count, float buttonWidth, float buttonHeight, float alpha, List<PendingStrategySuggestion> list)
{
    int signature = 17;
    for (int i = 0; i < count; i++)
    {
        signature = signature * 31 + ((list[i]?.StrategyName ?? string.Empty).GetHashCode());
    }
    if (signature != strategyFxSignature)
    {
        strategyFxSignature = signature;
        strategyFxStartRealtime = Time.realtimeSinceStartup;
    }

    float elapsed = Time.realtimeSinceStartup - strategyFxStartRealtime;
    if (elapsed < 0f || elapsed > 0.75f)
    {
        return;
    }

    float progress = Mathf.Clamp01(elapsed / 0.75f);
    float glowAlpha = (1f - progress) * 0.28f * alpha;
    for (int i = 0; i < count; i++)
    {
        Rect baseRect = new Rect(rect.x + i * (buttonWidth + StrategyButtonSpacing), rect.y + 2f, buttonWidth, buttonHeight);
        Rect fxRect = baseRect.ExpandedBy(8f * (1f - progress));
        Widgets.DrawBoxSolid(fxRect, new Color(0.35f, 0.72f, 1f, glowAlpha));
    }
}



internal bool ShouldShowStrategySuggestionBar()
{
    if (!IsStrategyUiEnabled())
    {
        return false;
    }

    if (session == null || session.isWaitingForResponse)
    {
        return false;
    }

    if (session.pendingStrategySuggestions == null || session.pendingStrategySuggestions.Count != StrategySuggestionRequiredCount)
    {
        return false;
    }

    bool blocked = Owner.Parts.Presence.IsInputBlockedByPresence(out _, out _);
    if (blocked || !Owner.Parts.Presence.CanSendMessageNow())
    {
        return false;
    }

    return HasStrategyUsesRemaining(session);
}



internal bool IsStrategyUiEnabled()
{
    return RelationsMod.Settings?.EnableDiplomacyStrategyToggle ?? true;
}



internal string BuildStrategyButtonLabel(PendingStrategySuggestion suggestion)
{
    string label = suggestion?.StrategyName ?? string.Empty;
    if (string.IsNullOrWhiteSpace(label))
    {
        label = "RimChat_StrategyFallbackLabel".Translate();
    }
    label = label.Replace("\r", string.Empty).Replace("\n", " ").Trim();
    if (Owner.Parts.StrategyPrompt.IsGenericStrategyLabel(label))
    {
        label = Owner.Parts.StrategyPrompt.BuildStrategyLabelFromReply(suggestion?.Content ?? string.Empty);
    }
    if (label.Length > StrategyLabelDisplayMaxChars)
    {
        label = label.Substring(0, StrategyLabelDisplayMaxChars);
    }

    return label;
}



internal string CompactStrategyReasonForDisplay(string reason)
{
    string compact = (reason ?? string.Empty).Replace("\r", string.Empty).Replace("\n", " ").Trim();
    compact = System.Text.RegularExpressions.Regex.Replace(
        compact,
        "\\[\\s*F\\d+\\s*\\]",
        string.Empty,
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    compact = compact.Replace("факт", string.Empty)
                     .Replace("причина", string.Empty)
                     .Replace("because", string.Empty)
                     .Replace("Because", string.Empty)
                     .Trim(' ', ':', '：', '-', '|', ';', '；');

    var parts = new List<string>();
    string wealth = Owner.Parts.StrategyPrompt.ExtractWealthTier(compact);
    if (!string.IsNullOrWhiteSpace(wealth))
    {
        parts.Add($"статки {wealth}");
    }

    int? social = Owner.Parts.StrategyPrompt.ExtractIntNearKeyword(compact, "спілкування", "social");
    if (social.HasValue)
    {
        parts.Add($"соціальність {social.Value}");
    }

    int? population = Owner.Parts.StrategyPrompt.ExtractIntNearKeyword(compact, "колоніст", "населення", "colonists");
    if (population.HasValue)
    {
        parts.Add($"населення {population.Value}");
    }

    if (parts.Count > 0)
    {
        return string.Join("·", parts.Take(2));
    }

    string head = compact;
    int separator = head.IndexOfAny(new[] { '，', ',', '。', ';', '；', '|' });
    if (separator > 0)
    {
        head = head.Substring(0, separator).Trim();
    }
    if (head.Length <= StrategyBasisDisplayMaxChars)
    {
        return head;
    }
    return head.Substring(0, StrategyBasisDisplayMaxChars);
}



internal void DrawStrategyStatusHint(Rect rect)
{
    string collapsedHint = BuildCollapsedStrategyStatusHint();
    string expandedHint = BuildExpandedStrategyStatusHint();
    if (string.IsNullOrWhiteSpace(collapsedHint) && string.IsNullOrWhiteSpace(expandedHint))
    {
        return;
    }

    float expandProgress = GetStrategyStatusExpandProgress();
    bool hovered = Mouse.IsOver(rect);
    float hoverBoost = hovered ? 0.14f : 0f;

    Text.Font = GameFont.Tiny;
    Text.Anchor = TextAnchor.MiddleLeft;

    Rect textRect = new Rect(rect.x + 8f, rect.y, rect.width - 12f, rect.height);
    if (!string.IsNullOrWhiteSpace(collapsedHint))
    {
        float collapsedAlpha = Mathf.Clamp01((1f - expandProgress) * (0.54f + hoverBoost));
        if (collapsedAlpha > 0.01f)
        {
            GUI.color = new Color(0.62f, 0.68f, 0.76f, collapsedAlpha);
            Widgets.Label(textRect, collapsedHint);
        }
    }

    if (!string.IsNullOrWhiteSpace(expandedHint))
    {
        float expandedAlpha = Mathf.Clamp01(expandProgress * (0.82f + hoverBoost));
        if (expandedAlpha > 0.01f)
        {
            GUI.color = new Color(0.72f, 0.78f, 0.86f, expandedAlpha);
            Widgets.Label(textRect, expandedHint);
        }
    }

    if (Widgets.ButtonInvisible(rect))
    {
        ToggleStrategyUiEnabled();
    }

    Text.Anchor = TextAnchor.UpperLeft;
    Text.Font = GameFont.Small;
    GUI.color = Color.white;
}



internal string BuildCollapsedStrategyStatusHint()
{
    return "RimChat_StrategyCollapsedEntry".Translate();
}



internal string BuildExpandedStrategyStatusHint()
{
    if (session == null)
    {
        return string.Empty;
    }

    int social = GetNegotiatorSocialLevel();
    int useLimit = GetStrategyUseLimitBySocial(social);
    int remaining = Math.Max(0, useLimit - session.strategyUsesConsumed);
    string useLimitDisplay = FormatStrategyUseLimit(useLimit);
    string remainingDisplay = FormatStrategyUseLimit(remaining);
    string statusText;
    if (social < 3)
    {
        statusText = "RimChat_StrategyNeedSocialHint".Translate(social);
    }
    else if (strategySuggestionRequestPending && remaining > 0)
    {
        statusText = "RimChat_StrategyGeneratingHint".Translate(remainingDisplay, useLimitDisplay);
    }
    else if (remaining <= 0)
    {
        statusText = "RimChat_StrategyUsesExhaustedHint".Translate(useLimitDisplay);
    }
    else if (session.pendingStrategySuggestions != null && session.pendingStrategySuggestions.Count == StrategySuggestionRequiredCount)
    {
        statusText = "RimChat_StrategyReadyHint".Translate(remainingDisplay, useLimitDisplay);
    }
    else
    {
        statusText = "RimChat_StrategyRemainingHint".Translate(remainingDisplay, useLimitDisplay);
    }

    string toggleText = IsStrategyUiEnabled()
        ? "RimChat_StrategyToggleDisable".Translate()
        : "RimChat_StrategyToggleEnable".Translate();
    return $"{statusText}  {toggleText}";
}



internal float GetStrategyControlsHeight()
{
    EnsureStrategyStatusAnimationInitialized();
    if (ShouldShowStrategySuggestionBar())
    {
        return StrategyStatusExpandedHeight;
    }

    UpdateStrategyStatusExpandProgress();
    float easedProgress = Mathf.SmoothStep(0f, 1f, strategyStatusExpandProgress);
    return Mathf.Lerp(StrategyStatusCollapsedHeight, StrategyStatusExpandedHeight, easedProgress);
}



internal float GetStrategyStatusExpandProgress()
{
    EnsureStrategyStatusAnimationInitialized();
    return Mathf.SmoothStep(0f, 1f, strategyStatusExpandProgress);
}



internal void EnsureStrategyStatusAnimationInitialized()
{
    if (strategyStatusAnimInitialized)
    {
        return;
    }

    strategyStatusAnimInitialized = true;
    strategyStatusExpandProgress = IsStrategyUiEnabled() ? 1f : 0f;
}



internal void UpdateStrategyStatusExpandProgress()
{
    float target = IsStrategyUiEnabled() ? 1f : 0f;
    strategyStatusExpandProgress = Mathf.MoveTowards(
        strategyStatusExpandProgress,
        target,
        Time.deltaTime * StrategyAnimSpeed);
}



internal void AddStrategyTooltip(Rect rect, PendingStrategySuggestion suggestion)
{
    if (suggestion == null)
    {
        return;
    }

    string reason = string.IsNullOrWhiteSpace(suggestion.FactReason)
        ? "RimChat_StrategyFallbackBasis".Translate()
        : suggestion.FactReason;

    string contentPreview = (suggestion.Content ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
    if (contentPreview.Length > StrategyTooltipReplyMaxChars)
    {
        contentPreview = contentPreview.Substring(0, StrategyTooltipReplyMaxChars) + "...";
    }

    string tip = string.IsNullOrWhiteSpace(contentPreview)
        ? reason
        : $"{reason}\n{contentPreview}";

    if (!string.IsNullOrWhiteSpace(tip))
    {
        TooltipHandler.TipRegion(rect, tip);
    }
}



internal void TrySendStrategySuggestion(PendingStrategySuggestion suggestion)
{
    if (suggestion == null || string.IsNullOrWhiteSpace(suggestion.Content))
    {
        return;
    }

    if (!IsStrategyUiEnabled())
    {
        return;
    }

    if (!HasStrategyUsesRemaining(session))
    {
        return;
    }

    if (!Owner.Parts.Presence.CanSendMessageNow())
    {
        return;
    }

    session.strategyUsesConsumed++;
    inputText = string.Empty;
    Owner.Parts.Session.SendPreparedMessage(suggestion.Content.Trim(), true);
}



internal bool HasStrategyUsesRemaining(FactionDialogueSession currentSession)
{
    if (currentSession == null)
    {
        return false;
    }

    int social = GetNegotiatorSocialLevel();
    int useLimit = GetStrategyUseLimitBySocial(social);
    return social >= 3 && currentSession.strategyUsesConsumed < useLimit;
}



internal int GetNegotiatorSocialLevel()
{
    return negotiator?.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
}



internal int GetStrategyUseLimitBySocial(int socialLevel)
{
    if (socialLevel < 3)
    {
        return 0;
    }

    if (socialLevel >= 20)
    {
        return int.MaxValue;
    }

    return socialLevel / 2;
}



internal static string FormatStrategyUseLimit(int useLimit)
{
    return useLimit >= 999 ? "∞" : useLimit.ToString();
}



internal void ClearPendingStrategySuggestions(FactionDialogueSession currentSession)
{
    if (currentSession == null)
    {
        return;
    }

    currentSession.pendingStrategySuggestions?.Clear();
}



internal void ToggleStrategyUiEnabled()
{
    var settings = RelationsMod.Settings ?? RelationsMod.Instance?.InstanceSettings;
    if (settings == null)
    {
        return;
    }

    settings.EnableDiplomacyStrategyToggle = !settings.EnableDiplomacyStrategyToggle;
    if (!settings.EnableDiplomacyStrategyToggle)
    {
        strategySuggestionRequestPending = false;
        strategySuggestionRequestId = null;
        ClearPendingStrategySuggestions(session);
    }

    RelationsMod.Instance?.WriteSettings();
}
}
