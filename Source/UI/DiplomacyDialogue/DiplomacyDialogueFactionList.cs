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

internal sealed class DiplomacyDialogueFactionList : DiplomacyDialogueCollaborator
{
    internal DiplomacyDialogueFactionList(Dialog_DiplomacyDialogue owner) : base(owner) { }


internal void DrawFactionList(Rect rect)
{
    factionRowRects.Clear();

    Widgets.DrawBoxSolid(rect, new Color(0.085f, 0.085f, 0.11f, 0.98f));
    GUI.color = new Color(0.26f, 0.26f, 0.32f, 0.95f);
    Widgets.DrawBox(rect);
    GUI.color = Color.white;

    Rect innerRect = rect.ContractedBy(Dialog_DiplomacyDialogue.LayoutFactionInnerPadding);

    Text.Font = GameFont.Small;
    GUI.color = new Color(0.82f, 0.86f, 0.92f);
    Rect headerLabelRect = new Rect(
        innerRect.x,
        innerRect.y,
        innerRect.width - Dialog_DiplomacyDialogue.LayoutFactionHeaderButtonSize - 4f,
        Dialog_DiplomacyDialogue.LayoutFactionHeaderButtonSize);
    Widgets.Label(headerLabelRect, "RimChat_FactionsTitle".Translate());

    // Edit link for Faction Editor (bottom-aligned Tiny text matching title baseline)
    bool feInstalled = ModDependencyProbe.IsLoaded("yancy.factiongearcustomizer");
    string editLabel = feInstalled
        ? "RimChat_FactionEditorLink".Translate().ToString()
        : "*" + "RimChat_FactionEditorLink".Translate().ToString();
    float editLabelWidth = Text.CalcSize(editLabel).x;
    float titleTextWidth = Text.CalcSize("RimChat_FactionsTitle".Translate().ToString()).x;
    Rect editLinkRect = new Rect(headerLabelRect.x + titleTextWidth + 6f, headerLabelRect.y, editLabelWidth + 4f, headerLabelRect.height);
    bool isOverEdit = Mouse.IsOver(editLinkRect);
    Text.Font = GameFont.Tiny;
    Text.Anchor = TextAnchor.LowerLeft;
    GUI.color = isOverEdit ? new Color(0.6f, 0.75f, 1f) : new Color(0.45f, 0.52f, 0.65f);
    Widgets.Label(editLinkRect, editLabel);
    Text.Anchor = TextAnchor.UpperLeft;
    if (isOverEdit)
    {
        TooltipHandler.TipRegion(editLinkRect,
            feInstalled
                ? "RimChat_FactionEditorLinkTooltip_Installed".Translate()
                : "RimChat_FactionEditorLinkTooltip_NotInstalled".Translate());
    }
    if (isOverEdit && Event.current.type == EventType.MouseDown && Event.current.button == 0)
    {
        Event.current.Use();
        HandleFactionEditorLink();
    }
    Text.Font = GameFont.Small;
    GUI.color = Color.white;

    Rect hiddenFactionSettingsRect = new Rect(
        innerRect.xMax - Dialog_DiplomacyDialogue.LayoutFactionHeaderButtonSize,
        innerRect.y,
        Dialog_DiplomacyDialogue.LayoutFactionHeaderButtonSize,
        Dialog_DiplomacyDialogue.LayoutFactionHeaderButtonSize);
    if (Widgets.ButtonText(hiddenFactionSettingsRect, "+"))
    {
        OpenHiddenFactionVisibilitySelector();
    }
    TooltipHandler.TipRegion(hiddenFactionSettingsRect, "RimChat_HiddenFactionSelectorTooltip".Translate());
    GUI.color = Color.white;

    GUI.color = new Color(0.42f, 0.45f, 0.52f, 0.45f);
    Widgets.DrawLineHorizontal(innerRect.x, innerRect.y + Dialog_DiplomacyDialogue.LayoutFactionVerticalLineY, innerRect.width);
    GUI.color = Color.white;

    var allFactions = GetAvailableFactions(false);
    CleanupGoodwillHoverAlpha(allFactions);
    Owner.Parts.HoverCard.CleanupHoverCardAlpha(allFactions.Select(f => $"faction:{f.loadID}"));

    float rowHeight = Dialog_DiplomacyDialogue.LayoutFactionRowHeight;
    float contentHeight = allFactions.Count * (rowHeight + Dialog_DiplomacyDialogue.LayoutFactionRowSpacing);

    Rect viewRect = new Rect(0f, 0f, innerRect.width - 16f, Mathf.Max(contentHeight, innerRect.height - (Dialog_DiplomacyDialogue.LayoutFactionHeaderHeight + 4f)));

    Rect scrollRect = new Rect(innerRect.x, innerRect.y + Dialog_DiplomacyDialogue.LayoutFactionHeaderHeight, innerRect.width, innerRect.height - Dialog_DiplomacyDialogue.LayoutFactionHeaderHeight);
    factionScrollPosition = GUI.BeginScrollView(scrollRect, factionScrollPosition, viewRect);

    float curY = 0f;
    foreach (var f in allFactions)
    {
        Rect rowRect = new Rect(5f, curY, viewRect.width - 10f, rowHeight);
        DrawFactionListItem(f, rowRect);

        Rect screenRect = new Rect(
            rect.x + 8f + rowRect.x,
            rect.y + 8f + 31f + rowRect.y - factionScrollPosition.y,
            rowRect.width,
            rowRect.height
        );
        factionRowRects[f] = screenRect;

        curY += rowHeight + Dialog_DiplomacyDialogue.LayoutFactionRowSpacing;

        // Show quests below the selected faction
        if (f == faction)
        {
            float questH = DrawFactionQuests(new Rect(5f, curY, viewRect.width - 10f, 200f), f);
            if (questH > 0f)
            {
                curY += questH + 2f;
            }
        }
    }

    GUI.EndScrollView();

    GoodwillChangeAnimator.CheckGoodwillChanges(allFactions);
}


internal List<Faction> GetAvailableFactions(bool refreshPresence = false)
{
    int currentTick = Find.TickManager.TicksGame;
    if (_cachedFactionList != null && _cachedFactionListTick == currentTick && !refreshPresence)
    {
        return _cachedFactionList;
    }

    var list = new List<Faction>();
    var manager = GameComponent_DiplomacyManager.Instance;
    var manuallyVisibleHiddenFactions = manager?.GetManuallyVisibleHiddenFactions() ?? new List<Faction>();
    if (Find.FactionManager?.AllFactions != null)
    {
        foreach (var f in Find.FactionManager.AllFactions)
        {
            if (!IsFactionEligibleForDialogueList(f))
            {
                continue;
            }

            if (!f.Hidden || manuallyVisibleHiddenFactions.Contains(f))
            {
                list.Add(f);
            }
        }
    }
    if (refreshPresence)
    {
        GameComponent_DiplomacyManager.Instance?.RefreshPresenceForFactions(list);
    }
    _cachedFactionList = list
        .OrderBy(GetPresenceSortWeight)
        .ThenByDescending(f => f.PlayerGoodwill)
        .ToList();
    _cachedFactionListTick = currentTick;
    return _cachedFactionList;
}


internal static bool IsFactionEligibleForDialogueList(Faction factionEntry)
{
    return factionEntry != null &&
           !factionEntry.IsPlayer &&
           !factionEntry.defeated;
}


internal void HandleFactionEditorLink()
{
    if (ModDependencyProbe.IsLoaded("yancy.factiongearcustomizer"))
    {
        var def = DefDatabase<MainButtonDef>.GetNamed("FactionGear_MainButton");
        if (def?.workerClass != null)
        {
            var worker = (MainButtonWorker)Activator.CreateInstance(def.workerClass);
            worker.def = def;
            worker.Activate();
        }
    }
    else
    {
        Application.OpenURL("https://steamcommunity.com/sharedfiles/filedetails/?id=3670833973");
    }
}


internal void OpenHiddenFactionVisibilitySelector()
{
    var manager = GameComponent_DiplomacyManager.Instance;
    if (manager == null)
    {
        return;
    }

    var candidates = Find.FactionManager?.AllFactions?
        .Where(IsSelectableHiddenFactionCandidate)
        .ToList() ?? new List<Faction>();
    var preselected = manager.GetManuallyVisibleHiddenFactions();

    Find.WindowStack.Add(new Dialog_HiddenFactionVisibilitySelector(
        candidates,
        preselected,
        OnHiddenFactionSelectionConfirmed));
}


internal static bool IsSelectableHiddenFactionCandidate(Faction factionEntry)
{
    return IsFactionEligibleForDialogueList(factionEntry) &&
           factionEntry.Hidden;
}


internal static void OnHiddenFactionSelectionConfirmed(List<Faction> selectedFactions)
{
    GameComponent_DiplomacyManager.Instance?.SetManuallyVisibleHiddenFactions(selectedFactions);
}


internal int GetPresenceSortWeight(Faction factionToSort)
{
    var status = GameComponent_DiplomacyManager.Instance?.GetPresenceStatus(factionToSort) ?? FactionPresenceStatus.Online;
    switch (status)
    {
        case FactionPresenceStatus.Online:
            return 0;
        case FactionPresenceStatus.DoNotDisturb:
            return 1;
        default:
            return 2;
    }
}


internal void DrawFactionListItem(Faction f, Rect rect)
{
    bool isSelected = f == faction;
    bool hasUnread = GameComponent_DiplomacyManager.Instance?.HasUnreadMessages(f) ?? false;
    bool isHovering = Mouse.IsOver(rect);
    int goodwill = f.PlayerGoodwill;
    Color goodwillColor = GetGoodwillColor(goodwill);
    float hoverAlpha = UpdateGoodwillHoverAlpha(f, isHovering);
    bool showGoodwillValue = hoverAlpha > 0.01f;

    Color rowBase = new Color(0.11f, 0.12f, 0.16f, 0.78f);
    Color rowHover = new Color(0.25f, 0.28f, 0.38f, 0.95f);
    Color rowColor = isSelected
        ? new Color(0.18f, 0.4f, 0.66f, 0.72f)
        : Color.Lerp(rowBase, rowHover, hoverAlpha);
    Widgets.DrawBoxSolid(rect, rowColor);
    GUI.color = isSelected ? new Color(0.42f, 0.58f, 0.85f, 0.95f) : new Color(0.25f, 0.28f, 0.35f, 0.95f);
    Widgets.DrawBox(rect);
    GUI.color = Color.white;

    if (hasUnread && !isSelected)
    {
        Widgets.DrawBoxSolid(new Rect(rect.x + 2f, rect.y + 6f, 3f, rect.height - 12f), new Color(0.24f, 0.82f, 0.96f));
    }

    float x = rect.x + 6f + (hasUnread && !isSelected ? 4f : 0f);
    float y = rect.y + 4f;

    Rect iconFrame = new Rect(x, y, 32f, 32f);
    Widgets.DrawBoxSolid(iconFrame, new Color(0.18f, 0.2f, 0.25f, 0.95f));
    GUI.color = new Color(0.34f, 0.38f, 0.46f, 0.9f);
    Widgets.DrawBox(iconFrame);
    GUI.color = Color.white;

    Rect iconRect = iconFrame.ContractedBy(2f);
    Texture2D factionIcon = f.def?.FactionIcon;
    if (factionIcon != null && factionIcon != BaseContent.BadTex)
    {
        GUI.DrawTexture(iconRect, factionIcon);
    }
    x += 38f;

    float rightReserved = Mathf.Lerp(4f, 40f, hoverAlpha);
    float contentWidth = Mathf.Max(40f, rect.xMax - x - rightReserved);
    Rect nameRect = new Rect(x, y + 1f, contentWidth, 20f);
    GUI.color = isSelected ? Color.white : new Color(0.9f, 0.93f, 0.98f);
    bool previousWordWrap = Text.WordWrap;
    Text.WordWrap = false;
    Widgets.Label(nameRect, (f.Name ?? "Unknown").Truncate(nameRect.width));
    Text.WordWrap = previousWordWrap;

    Rect presenceRect = new Rect(x, y + 21f, contentWidth, 14f);
    Text.Font = GameFont.Tiny;
    Owner.Parts.Presence.DrawFactionPresenceStatus(f, presenceRect, false);

    string goodwillText = goodwill >= 0 ? $"+{goodwill}" : goodwill.ToString();
    Rect goodwillRect = new Rect(rect.xMax - 44f, y + 1f, 38f, 16f);
    if (showGoodwillValue)
    {
        GUI.color = new Color(goodwillColor.r, goodwillColor.g, goodwillColor.b, hoverAlpha);
        Text.Anchor = TextAnchor.MiddleRight;
        Widgets.Label(goodwillRect, goodwillText);
        Text.Anchor = TextAnchor.UpperLeft;
    }

    Text.Font = GameFont.Small;
    GUI.color = Color.white;

    Owner.Parts.HoverCard.DrawFactionHoverCard(f, rect);

    if (!isSelected && Widgets.ButtonInvisible(rect))
    {
        Owner.Parts.Presenter.SwitchFactionInPlace(f);
    }
}


        internal static bool TryOpenDiplomacyDirectFallback(Faction faction, Pawn negotiator, bool muteOpenSound, string source) => DiplomacyDialogueNegotiatorOps.TryOpenDiplomacyDirectFallback(faction, negotiator, muteOpenSound, source);
        internal static Pawn ResolveAutoNegotiator(Pawn preferredNegotiator) => DiplomacyDialogueNegotiatorOps.ResolveAutoNegotiator(preferredNegotiator);
        internal static Pawn ResolveHighestSocialNegotiator() => DiplomacyDialogueNegotiatorOps.ResolveHighestSocialNegotiator();
        internal static Pawn ResolveNegotiatorFromProtagonistList() => DiplomacyDialogueNegotiatorOps.ResolveNegotiatorFromProtagonistList();
        internal static Pawn ResolveLastUsedNegotiator() => DiplomacyDialogueNegotiatorOps.ResolveLastUsedNegotiator();
        internal static Pawn ResolveDesignatedNegotiator(RelationsSettings settings) => DiplomacyDialogueNegotiatorOps.ResolveDesignatedNegotiator(settings);
        internal static bool IsValidNegotiator(Pawn pawn) => DiplomacyDialogueNegotiatorOps.IsValidNegotiator(pawn);
        internal static int GetNegotiatorScore(Pawn pawn) => DiplomacyDialogueNegotiatorOps.GetNegotiatorScore(pawn);
internal float UpdateGoodwillHoverAlpha(Faction faction, bool isHovering)
{
    if (faction == null) return 0f;
    float current = goodwillHoverAlpha.TryGetValue(faction, out float alpha) ? alpha : 0f;
    float target = isHovering ? 1f : 0f;
    float next = Mathf.MoveTowards(current, target, 0.04f);
    goodwillHoverAlpha[faction] = next;
    return next;
}


internal void CleanupGoodwillHoverAlpha(List<Faction> activeFactions)
{
    if (activeFactions == null) return;
    for (int i = goodwillHoverAlpha.Count - 1; i >= 0; i--)
    {
        Faction key = goodwillHoverAlpha.Keys.ElementAt(i);
        if (!activeFactions.Contains(key))
        {
            goodwillHoverAlpha.Remove(key);
        }
    }
}


internal TradeShip GetTradeShip()
{
    if (faction == null || Find.CurrentMap == null) return null;
    return Find.CurrentMap.passingShipManager?.passingShips
        .FirstOrDefault(x => x.Faction == faction && x is TradeShip) as TradeShip;
}


internal void DrawOrbitalTraderCard(Rect rect, TradeShip tradeShip)
{
    Widgets.DrawBoxSolid(rect, new Color(0.15f, 0.2f, 0.25f, 0.8f));
    Widgets.DrawBox(rect);

    Rect innerRect = rect.ContractedBy(6f);

    // Button on the right
    Rect btnRect = new Rect(innerRect.xMax - 90f, innerRect.y + (innerRect.height - 26f) / 2f, 90f, 26f);

    // Text area - left of button
    float textWidth = innerRect.width - 100f;

    Text.Font = GameFont.Small;
    GUI.color = new Color(0.9f, 0.9f, 1f);
    string shipName = tradeShip.name;
    string traderKind = tradeShip.def.LabelCap;
    Rect labelRect = new Rect(innerRect.x, innerRect.y, textWidth, 18f);
    Widgets.Label(labelRect, "RimChat_OrbitalTraderAvailable".Translate(shipName, traderKind));

    Text.Font = GameFont.Tiny;
    GUI.color = Color.gray;
    Rect descRect = new Rect(innerRect.x, innerRect.y + 18f, textWidth, 14f);
    Widgets.Label(descRect, "RimChat_ClickToTrade".Translate());
    GUI.color = Color.white;
    Text.Font = GameFont.Small;
    bool canTrade = negotiator != null && negotiator.Map == Find.CurrentMap && !negotiator.Downed && !negotiator.InMentalState;
    
    if (canTrade)
    {
        if (Widgets.ButtonText(btnRect, "RimChat_TradeButton".Translate()))
        {
            Find.WindowStack.Add(new Dialog_Trade(negotiator, tradeShip, false));
            Close();
        }
    }
    else
    {
        GUI.color = Color.gray;
        Widgets.DrawBoxSolid(btnRect, new Color(0.3f, 0.3f, 0.3f));
        Widgets.Label(btnRect, "RimChat_TradeButton".Translate());
        GUI.color = Color.white;
        
        if (Mouse.IsOver(btnRect))
        {
            TooltipHandler.TipRegion(btnRect, "RimChat_NegotiatorUnavailable".Translate());
        }
    }
}


internal float DrawFactionQuests(Rect rect, Faction targetFaction)
{
    int currentTick = Find.TickManager.TicksGame;
    if (_cachedQuestFaction != targetFaction || _cachedQuestsTick != currentTick)
    {
        _cachedQuestFaction = targetFaction;
        _cachedQuestsTick = currentTick;
        _cachedQuests = Find.QuestManager.QuestsListForReading
            .Where(q => q.State == QuestState.Ongoing && !q.hidden
                && QuestInvolvedFactionsGuard.HasInvolvedFaction(q, targetFaction))
            .ToList();
    }

    if (_cachedQuests == null || _cachedQuests.Count == 0)
    {
        return 0f;
    }

    float x = rect.x + 6f;
    float rowH = 16f;
    float curY = rect.y;

    Text.Font = GameFont.Tiny;
    Color prev = GUI.color;
    GUI.color = new Color(0.5f, 0.7f, 0.9f, 0.8f);
    Widgets.Label(new Rect(x, curY, rect.width - 12f, rowH), "RimChat_QuestActions".Translate());
    GUI.color = prev;
    curY += rowH + 2f;

    foreach (Quest quest in _cachedQuests)
    {
        Rect itemRect = new Rect(x, curY, rect.width - 12f, rowH);
        Widgets.DrawHighlightIfMouseover(itemRect);
        GUI.color = new Color(0.72f, 0.86f, 1f, 0.9f);
        Widgets.Label(itemRect, (quest?.name ?? string.Empty).Truncate(itemRect.width));
        GUI.color = prev;

        if (Widgets.ButtonInvisible(itemRect))
        {
            MainTabWindow_Quests questsWindow = (MainTabWindow_Quests)MainButtonDefOf.Quests.TabWindow;
            questsWindow.Select(quest);
            Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Quests, true);
        }

        curY += rowH + 2f;
    }

    Text.Font = GameFont.Small;
    return curY - rect.y;
}


internal Color GetGoodwillColor(int goodwill)
{
    if (goodwill >= 80) return new Color(0.3f, 0.9f, 0.3f);
    if (goodwill >= 40) return new Color(0.6f, 0.9f, 0.3f);
    if (goodwill >= 0) return new Color(0.9f, 0.9f, 0.3f);
    if (goodwill >= -40) return new Color(0.9f, 0.6f, 0.2f);
    return new Color(0.9f, 0.3f, 0.3f);
}


internal string GetRelationLabelShort(int goodwill)
{
    if (goodwill >= 80) return "RimChat_RelationAllyShort".Translate();
    if (goodwill >= 40) return "RimChat_RelationFriendShort".Translate();
    if (goodwill >= 0) return "RimChat_RelationNeutralShort".Translate();
    if (goodwill >= -40) return "RimChat_RelationHostileShort".Translate();
    return "RimChat_RelationEnemyShort".Translate();
}
}
