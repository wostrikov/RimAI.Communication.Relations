using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Dialogue;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Communication.Relations.Relation;
using Ustas.RimAI.Communication.Relations.Config;

namespace Ustas.RimAI.Communication.Relations.UI
{
    public partial class MainTabWindow_Relations : MainTabWindow
    {
        public override Vector2 InitialSize => new Vector2(1000f, 750f);

        internal Vector2 factionListScrollPosition = Vector2.zero;
        internal Vector2 detailScrollPosition = Vector2.zero;
        internal Faction selectedFaction;
        internal List<Faction> allFactions = new List<Faction>();

        // 颜色主题
        internal static readonly Color BackgroundColor = new Color(0.08f, 0.08f, 0.10f);
        internal static readonly Color PanelColor = new Color(0.12f, 0.12f, 0.15f);
        internal static readonly Color HeaderColor = new Color(0.15f, 0.15f, 0.18f);
        internal static readonly Color AccentColor = new Color(0.25f, 0.55f, 0.95f);
        internal static readonly Color TextPrimary = new Color(0.95f, 0.95f, 0.97f);
        internal static readonly Color TextSecondary = new Color(0.65f, 0.65f, 0.70f);
        internal static readonly Color BorderColor = new Color(0.20f, 0.20f, 0.25f);

        // Faction位置映射 (used for动画定位)
        internal readonly Dictionary<Faction, Rect> factionRowRects = new Dictionary<Faction, Rect>();
        internal bool goodwillEventSubscribed;
        internal MainTabWindow_RelationsParts Parts;

        public MainTabWindow_Relations()
        {
            Parts = new MainTabWindow_RelationsParts(this);
            closeOnClickedOutside = false;
            absorbInputAroundWindow = false;
            draggable = true;
            EnsureGoodwillEventSubscription();
        }

        public override void PreClose()
        {
            base.PreClose();
            ClearGoodwillEventSubscription();
        }

        /// <summary>/// goodwill变化eventprocessing
 ///</summary>
        

        public override void PreOpen()
        {
            base.PreOpen();
            EnsureGoodwillEventSubscription();
            RefreshFactionList();
        }

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        internal void ShowFallbackFactionInfo(Faction faction)
        {
            Log.Message($"[RimAI.Relations] Faction Info: {faction.def?.label ?? "Unknown"} - Tech: {faction.def?.techLevel}");
        }

        

        

        

        

        

        
    
        #region Cluster forwards
        internal void OnGoodwillChanged(Faction faction, int changeAmount) => Parts.Slice1.OnGoodwillChanged(faction, changeAmount);
        internal void EnsureGoodwillEventSubscription() => Parts.Slice1.EnsureGoodwillEventSubscription();
        internal void ClearGoodwillEventSubscription() => Parts.Slice1.ClearGoodwillEventSubscription();
        internal void RefreshFactionList() => Parts.Slice1.RefreshFactionList();
        public override void DoWindowContents(Rect inRect) => Parts.Slice1.DoWindowContents(inRect);
        internal void DrawHeader(Rect rect) => Parts.Slice1.DrawHeader(rect);
        internal void DrawFactionList(Rect rect) => Parts.Slice1.DrawFactionList(rect);
        internal void DrawModernFactionListItem(Faction faction, Rect rect) => Parts.Slice1.DrawModernFactionListItem(faction, rect);
        internal void DrawFactionDetail(Rect rect) => Parts.Slice1.DrawFactionDetail(rect);
        internal void DrawEmptyState(Rect rect) => Parts.Slice1.DrawEmptyState(rect);
        internal void DrawModernFactionHeader(Faction faction, Rect rect) => Parts.Slice1.DrawModernFactionHeader(faction, rect);
        internal void DrawRelationCard(Faction faction, Rect rect) => Parts.Slice2.DrawRelationCard(faction, rect);
        internal void DrawInfoGrid(Faction faction, Rect rect) => Parts.Slice2.DrawInfoGrid(faction, rect);
        internal void DrawInfoCard(Rect rect, string label, string value, string subtext) => Parts.Slice2.DrawInfoCard(rect, label, value, subtext);
        internal void DrawModernActionButtons(Rect rect) => Parts.Slice2.DrawModernActionButtons(rect);
        internal void DrawModernAIStatus(Faction faction, Rect rect) => Parts.Slice2.DrawModernAIStatus(faction, rect);
        internal void DrawModernButton(Rect rect, string label, Action onClick, Color? color = null, bool enabled = true) => Parts.Slice2.DrawModernButton(rect, label, onClick, color, enabled);
        internal void DrawInfoButton(Rect rect, Action onClick) => Parts.Slice2.DrawInfoButton(rect, onClick);
        internal void OpenFactionDefInfoCard(Faction faction) => Parts.Slice2.OpenFactionDefInfoCard(faction);
        internal void DrawDefaultFactionIcon(Rect rect, Faction faction) => Parts.Slice2.DrawDefaultFactionIcon(rect, faction);
        internal Color GetFactionColor(Faction faction) => Parts.Slice2.GetFactionColor(faction);
        internal string GetRelationLabel(int goodwill) => Parts.Slice2.GetRelationLabel(goodwill);
        internal string GetRelationLabelShort(int goodwill) => Parts.Slice2.GetRelationLabelShort(goodwill);
        internal Color GetGoodwillColor(int goodwill) => Parts.Slice2.GetGoodwillColor(goodwill);
        internal void OpenDialogueWindow() => Parts.Slice2.OpenDialogueWindow();
        #endregion
}
    internal sealed class RelationsMainTabSlice2 : MainTabWindow_RelationsCollaborator
    {
        internal RelationsMainTabSlice2(MainTabWindow_Relations owner) : base(owner)
        {
        }

internal void DrawRelationCard(Faction faction, Rect rect)
        {
            // 卡片背景
            int goodwill = faction.PlayerGoodwill;
            Color relationColor = Owner.GetGoodwillColor(goodwill);
            
            Widgets.DrawBoxSolid(rect, new Color(0.10f, 0.10f, 0.13f));
            GUI.color = new Color(relationColor.r, relationColor.g, relationColor.b, 0.5f);
            Widgets.DrawBox(rect);
            GUI.color = Color.white;

            float x = rect.x + 15f;
            float y = rect.y + 15f;

            // Relationlabel
            string relationLabel = Owner.GetRelationLabel(goodwill);
            Text.Font = GameFont.Medium;
            GUI.color = relationColor;
            Widgets.Label(new Rect(x, y, 200f, 28f), relationLabel);

            // Goodwill大数字
            Text.Font = GameFont.Medium;
            GUI.color = relationColor;
            string goodwillText = $"{goodwill}";
            float goodwillWidth = Text.CalcSize(goodwillText).x;
            Widgets.Label(new Rect(rect.xMax - goodwillWidth - 20f, y, goodwillWidth + 10f, 28f), goodwillText);

            y += 35f;

            // Goodwill条
            Rect barBgRect = new Rect(x, y, rect.width - 30f, 10f);
            Widgets.DrawBoxSolid(barBgRect, new Color(0.08f, 0.08f, 0.10f));
            
            float goodwillPercent = Mathf.InverseLerp(-100f, 100f, goodwill);
            Rect barFillRect = new Rect(barBgRect.x, barBgRect.y, barBgRect.width * goodwillPercent, barBgRect.height);
            Widgets.DrawBoxSolid(barFillRect, relationColor);

            // 刻度标记
            GUI.color = new Color(0.3f, 0.3f, 0.35f);
            for (int i = -100; i <= 100; i += 50)
            {
                float markX = barBgRect.x + barBgRect.width * Mathf.InverseLerp(-100f, 100f, i);
                Widgets.DrawBoxSolid(new Rect(markX, barBgRect.y - 3f, 1f, 16f), new Color(0.3f, 0.3f, 0.35f));
            }

            Text.Font = GameFont.Small;
            GUI.color = Color.white;
        }

internal void DrawInfoGrid(Faction faction, Rect rect)
        {
            float cardWidth = (rect.width - 15f) / 2f;
            float cardHeight = 90f;

            // Leader信息卡片
            Rect leaderRect = new Rect(rect.x, rect.y, cardWidth, cardHeight);
            string leaderName = faction.leader?.Name?.ToStringFull ?? "RimChat_None".Translate();
            string leaderTraits = faction.leader?.story?.traits?.allTraits?.Count > 0
                ? string.Join(", ", faction.leader.story.traits.allTraits.Select(t => t.Label))
                : "RimChat_NoTraits".Translate();
            Owner.DrawInfoCard(leaderRect, "RimChat_LeaderCard".Translate(), leaderName, leaderTraits);

            // 科技等级卡片
            Rect techRect = new Rect(rect.x + cardWidth + 15f, rect.y, cardWidth, cardHeight);
            Owner.DrawInfoCard(techRect, "RimChat_TechLevelCard".Translate(),
                faction.def?.techLevel.ToString() ?? "RimChat_Unknown".Translate(),
                "RimChat_TechLevelDesc".Translate());

            // 据点数量卡片
            int settlementCount = 0;
            if (Find.WorldObjects?.SettlementBases != null)
            {
                settlementCount = Find.WorldObjects.SettlementBases.Count(s => s.Faction == faction);
            }
            Rect settlementRect = new Rect(rect.x, rect.y + cardHeight + 10f, cardWidth, cardHeight);
            Owner.DrawInfoCard(settlementRect, "RimChat_SettlementsCard".Translate(), settlementCount.ToString(),
                "RimChat_SettlementsDesc".Translate());

            // 意识形态卡片
            Rect ideoRect = new Rect(rect.x + cardWidth + 15f, rect.y + cardHeight + 10f, cardWidth, cardHeight);
            string ideoName = faction.ideos?.PrimaryIdeo?.name ?? "RimChat_None".Translate();
            Owner.DrawInfoCard(ideoRect, "RimChat_IdeologyCard".Translate(), ideoName,
                "RimChat_IdeologyDesc".Translate());
        }

internal void DrawInfoCard(Rect rect, string label, string value, string subtext)
        {
            // 卡片背景
            Widgets.DrawBoxSolid(rect, new Color(0.10f, 0.10f, 0.13f));
            GUI.color = BorderColor;
            Widgets.DrawBox(rect);
            GUI.color = Color.white;

            float x = rect.x + 12f;
            float y = rect.y + 10f;

            // Label
            Text.Font = GameFont.Tiny;
            GUI.color = TextSecondary;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(x, y - 1f, rect.width - 20f, 20f), label.Translate().RawText.ToUpper());

            // 数values
            y += 18f;
            Text.Font = GameFont.Small;
            GUI.color = TextPrimary;
            Widgets.Label(new Rect(x, y, rect.width - 20f, 22f), value);

            // 副text
            y += 24f;
            Text.Font = GameFont.Tiny;
            GUI.color = TextSecondary * 0.8f;
            Widgets.Label(new Rect(x, y - 1f, rect.width - 20f, 20f), subtext);
            Text.Anchor = TextAnchor.UpperLeft;

            Text.Font = GameFont.Small;
            GUI.color = Color.white;
        }

internal void DrawModernActionButtons(Rect rect)
        {
            float buttonWidth = 140f;
            float x = rect.x;

            // Dialoguebutton
            Rect dialogueRect = new Rect(x, rect.y + 10f, buttonWidth, 40f);
            Owner.DrawModernButton(dialogueRect, "RimChat_DialogueButton".Translate(), () => Owner.OpenDialogueWindow(), AccentColor);
        }

internal void DrawModernAIStatus(Faction faction, Rect rect)
        {
            bool isAIControlled = GameComponent_DiplomacyManager.Instance?.IsAIControlled(faction) ?? false;
            
            // 背景
            Color statusColor = isAIControlled 
                ? new Color(0.2f, 0.6f, 0.9f, 0.15f)
                : new Color(0.3f, 0.3f, 0.35f, 0.15f);
            Widgets.DrawBoxSolid(rect, statusColor);
            GUI.color = isAIControlled 
                ? new Color(0.2f, 0.6f, 0.9f, 0.5f)
                : BorderColor;
            Widgets.DrawBox(rect);
            GUI.color = Color.white;

            float x = rect.x + 15f;
            float y = rect.y + 12f;

            // State图标
            string icon = isAIControlled ? "[AI]" : "[Std]";
            Text.Font = GameFont.Medium;
            GUI.color = isAIControlled ? new Color(0.4f, 0.8f, 1f) : TextSecondary;
            Widgets.Label(new Rect(x, y, 30f, 30f), icon);

            x += 40f;

            // State标题
            Text.Font = GameFont.Small;
            GUI.color = TextPrimary;
            string statusTitle = isAIControlled ? "RimChat_AIControlledStatus".Translate() : "RimChat_StandardBehaviorStatus".Translate();
            Widgets.Label(new Rect(x, y, rect.width - x + rect.x - 20f, 22f), statusTitle);

            // State描述
            y += 22f;
            Text.Font = GameFont.Tiny;
            GUI.color = TextSecondary;
            string statusDesc = isAIControlled
                ? "RimChat_AIControlledDesc".Translate()
                : "RimChat_StandardBehaviorDesc".Translate();
            Widgets.Label(new Rect(x, y, rect.width - x + rect.x - 20f, 20f), statusDesc);

            Text.Font = GameFont.Small;
            GUI.color = Color.white;
        }

internal void DrawModernButton(Rect rect, string label, Action onClick, Color? color = null, bool enabled = true)
        {
            Color buttonColor = color ?? AccentColor;
            
            if (!enabled)
            {
                buttonColor = new Color(0.2f, 0.2f, 0.25f);
                GUI.color = new Color(0.5f, 0.5f, 0.55f);
            }

            // Button背景
            Widgets.DrawBoxSolid(rect, buttonColor * (Mouse.IsOver(rect) && enabled ? 1.2f : 1f));
            
            // Button文字
            TextAnchor oldAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = enabled ? Color.white : new Color(0.5f, 0.5f, 0.55f);
            Widgets.Label(rect, label);
            Text.Anchor = oldAnchor;
            GUI.color = Color.white;

            // 点击processing
            if (enabled && Widgets.ButtonInvisible(rect))
            {
                onClick?.Invoke();
            }
        }

internal void DrawInfoButton(Rect rect, Action onClick)
        {
            Color buttonColor = new Color(0.35f, 0.58f, 0.92f);
            bool isMouseOver = Mouse.IsOver(rect);
            
            if (isMouseOver)
            {
                Widgets.DrawBoxSolid(rect, buttonColor * 1.3f);
            }
            else
            {
                Widgets.DrawBoxSolid(rect, buttonColor);
            }
            
            GUI.color = BorderColor;
            Widgets.DrawBox(rect);
            GUI.color = Color.white;

            TextAnchor oldAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = Color.white;
            Text.Font = GameFont.Medium;
            Widgets.Label(rect, "i");
            Text.Anchor = oldAnchor;
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            if (Widgets.ButtonInvisible(rect))
            {
                onClick?.Invoke();
            }
        }

internal void OpenFactionDefInfoCard(Faction faction)
        {
            if (faction?.def == null) return;

            try
            {
                Type dialogInfoCardType = typeof(Window).Assembly.GetType("RimWorld.Dialog_InfoCard");
                if (dialogInfoCardType != null)
                {
                    ConstructorInfo constructor = dialogInfoCardType.GetConstructor(new Type[] { typeof(object) });
                    if (constructor != null)
                    {
                        object dialog = constructor.Invoke(new object[] { faction.def });
                        Find.WindowStack.Add((Window)dialog);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimAI.Relations] Failed to open faction info card: {ex.Message}");
            }

            Owner.ShowFallbackFactionInfo(faction);
        }

internal void DrawDefaultFactionIcon(Rect rect, Faction faction)
        {
            Color factionColor = Owner.GetFactionColor(faction);
            Widgets.DrawBoxSolid(rect, factionColor * 0.3f);
            
            Text.Font = GameFont.Medium;
            GUI.color = factionColor;
            
            string initial = faction.Name?.Length > 0 ? faction.Name[0].ToString().ToUpper() : "?";
            
            TextAnchor oldAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, initial);
            Text.Anchor = oldAnchor;
            
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
        }

internal Color GetFactionColor(Faction faction)
        {
            if (faction?.def == null) return new Color(0.5f, 0.5f, 0.5f);
            
            if (faction.PlayerRelationKind == FactionRelationKind.Hostile)
                return new Color(0.95f, 0.35f, 0.35f);
            if (faction.PlayerRelationKind == FactionRelationKind.Neutral)
                return new Color(0.95f, 0.85f, 0.3f);
            if (faction.PlayerRelationKind == FactionRelationKind.Ally)
                return new Color(0.3f, 0.85f, 0.4f);
            
            return new Color(0.4f, 0.6f, 0.9f);
        }

internal string GetRelationLabel(int goodwill)
        {
            if (goodwill >= 80) return "RimChat_RelationAlly".Translate();
            if (goodwill >= 40) return "RimChat_RelationFriend".Translate();
            if (goodwill >= 0) return "RimChat_RelationNeutral".Translate();
            if (goodwill >= -40) return "RimChat_RelationHostile".Translate();
            return "RimChat_RelationEnemy".Translate();
        }

internal string GetRelationLabelShort(int goodwill)
        {
            if (goodwill >= 80) return "RimChat_RelationAllyShort";
            if (goodwill >= 40) return "RimChat_RelationFriendShort";
            if (goodwill >= 0) return "RimChat_RelationNeutralShort";
            if (goodwill >= -40) return "RimChat_RelationHostileShort";
            return "RimChat_RelationEnemyShort";
        }

internal Color GetGoodwillColor(int goodwill)
        {
            if (goodwill >= 80) return new Color(0.3f, 0.85f, 0.4f);   // 绿色
            if (goodwill >= 40) return new Color(0.7f, 0.9f, 0.3f);    // 黄绿
            if (goodwill >= 0) return new Color(0.95f, 0.85f, 0.3f);   // 黄色
            if (goodwill >= -40) return new Color(0.95f, 0.6f, 0.25f); // 橙色
            return new Color(0.95f, 0.35f, 0.35f);                      // 红色
        }

internal void OpenDialogueWindow()
        {
            if (selectedFaction != null)
            {
                Close();
                if (DialogueWindowCoordinator.TryOpen(
                    DialogueOpenIntent.CreateDiplomacy(selectedFaction, null, null, false),
                    out string reason))
                {
                    return;
                }

                Log.Warning($"[RimAI.Relations] MainTab dialogue open rejected: faction={selectedFaction.Name}, reason={reason ?? "unknown"}");
                Log.Warning($"[RimAI.Relations] Applying direct diplomacy open fallback: source=main_tab, faction={selectedFaction.Name}");
                Find.WindowStack?.Add(new Dialog_DiplomacyDialogue(selectedFaction, null));
            }
        }
    }

    internal sealed class MainTabWindow_RelationsParts
    {
        internal readonly MainTabWindow_Relations Owner;
        internal readonly RelationsMainTabSlice1 Slice1;
        internal readonly RelationsMainTabSlice2 Slice2;
        internal MainTabWindow_RelationsParts(MainTabWindow_Relations owner)
        {
            Owner = owner;
            Slice1 = new RelationsMainTabSlice1(owner);
            Slice2 = new RelationsMainTabSlice2(owner);
        }
    }

}


