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
    internal sealed class RelationsMainTabSlice1 : MainTabWindow_RelationsCollaborator
    {
        internal RelationsMainTabSlice1(MainTabWindow_Relations owner) : base(owner)
        {
        }

internal void OnGoodwillChanged(Faction faction, int changeAmount)
        {
            if (faction == null) return;

            if (factionRowRects.TryGetValue(faction, out Rect rowRect))
            {
                Vector2 startPos = new Vector2(
                    rowRect.x + 82f,
                    rowRect.y + 36f
                );

                GoodwillChangeAnimator.CreateAnimation(faction, changeAmount, startPos);
            }
        }

internal void EnsureGoodwillEventSubscription()
        {
            if (goodwillEventSubscribed)
            {
                return;
            }

            GoodwillChangeAnimator.OnGoodwillChanged -= OnGoodwillChanged;
            GoodwillChangeAnimator.OnGoodwillChanged += OnGoodwillChanged;
            goodwillEventSubscribed = true;
        }

internal void ClearGoodwillEventSubscription()
        {
            if (!goodwillEventSubscribed)
            {
                return;
            }

            GoodwillChangeAnimator.OnGoodwillChanged -= OnGoodwillChanged;
            goodwillEventSubscribed = false;
        }

internal void RefreshFactionList()
        {
            allFactions.Clear();
            
            if (Find.FactionManager?.AllFactions != null)
            {
                foreach (var faction in Find.FactionManager.AllFactions)
                {
                    if (faction != null && !faction.IsPlayer && !faction.defeated && !faction.Hidden)
                    {
                        allFactions.Add(faction);
                    }
                }
            }

            allFactions = allFactions.OrderByDescending(f => f.PlayerGoodwill).ToList();

            if (selectedFaction == null || !allFactions.Contains(selectedFaction))
            {
                selectedFaction = allFactions.FirstOrDefault();
            }
        }

public void DoWindowContents(Rect inRect)
        {
            Widgets.DrawBoxSolid(inRect, BackgroundColor);

            Owner.DrawHeader(new Rect(inRect.x, inRect.y, inRect.width, 74f));

            float contentY = inRect.y + 79f;
            float contentHeight = inRect.height - 84f;

            float listWidth = 280f;
            Rect listRect = new Rect(inRect.x + 5f, contentY, listWidth, contentHeight);
            Owner.DrawFactionList(listRect);

            Rect detailRect = new Rect(inRect.x + listWidth + 15f, contentY,
                inRect.width - listWidth - 25f, contentHeight);
            Owner.DrawFactionDetail(detailRect);

            GoodwillChangeAnimator.UpdateAndDrawAnimations();
        }

internal void DrawHeader(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, HeaderColor);
            
            Text.Font = GameFont.Medium;
            GUI.color = TextPrimary;
            string title = "RimChat_WindowTitle".Translate();
            Widgets.Label(new Rect(rect.x + 15f, rect.y + 12f, rect.width - 200f, 30f), title);
            
            Text.Font = GameFont.Tiny;
            GUI.color = TextSecondary;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x + 15f, rect.y + 30f, rect.width - 200f, 20f),
                "RimChat_FactionsAvailable".Translate(allFactions.Count));
            Text.Anchor = TextAnchor.UpperLeft;

            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            Rect refreshRect = new Rect(rect.xMax - 100f, rect.y + 10f, 85f, 30f);
            Owner.DrawModernButton(refreshRect, "RimChat_Refresh".Translate(), () => Owner.RefreshFactionList());
        }

internal void DrawFactionList(Rect rect)
        {
            factionRowRects.Clear();

            Widgets.DrawBoxSolid(rect, PanelColor);
            GUI.color = BorderColor;
            Widgets.DrawBox(rect);
            GUI.color = Color.white;

            Rect headerRect = new Rect(rect.x, rect.y, rect.width, 35f);
            Widgets.DrawBoxSolid(headerRect, new Color(0.14f, 0.14f, 0.17f));
            
            Text.Font = GameFont.Small;
            GUI.color = TextSecondary;
            Widgets.Label(new Rect(headerRect.x + 12f, headerRect.y + 8f, headerRect.width - 20f, 20f),
                "RimChat_FactionsHeader".Translate());
            GUI.color = Color.white;

            Rect innerRect = new Rect(rect.x, rect.y + 35f, rect.width, rect.height - 35f);

            float rowHeight = 75f;
            float contentHeight = allFactions.Count * (rowHeight + 4f);
            
            Rect viewRect = new Rect(0f, 0f, innerRect.width - 16f, Mathf.Max(contentHeight, innerRect.height));
            
            factionListScrollPosition = GUI.BeginScrollView(innerRect, factionListScrollPosition, viewRect);

            float curY = 5f;
            foreach (var faction in allFactions)
            {
                Rect rowRect = new Rect(8f, curY, viewRect.width - 16f, rowHeight);
                Owner.DrawModernFactionListItem(faction, rowRect);

                Rect screenRect = new Rect(
                    rect.x + rowRect.x,
                    rect.y + 35f + rowRect.y - factionListScrollPosition.y,
                    rowRect.width,
                    rowRect.height
                );
                factionRowRects[faction] = screenRect;

                curY += rowHeight + 4f;
            }

            GUI.EndScrollView();

            GoodwillChangeAnimator.CheckGoodwillChanges(allFactions);
        }

internal void DrawModernFactionListItem(Faction faction, Rect rect)
        {
            bool isSelected = faction == selectedFaction;
            bool hasDialogue = GameComponent_DiplomacyManager.Instance?.GetSession(faction)?.messages.Count > 0;
            bool hasUnread = GameComponent_DiplomacyManager.Instance?.HasUnreadMessages(faction) ?? false;
            
            if (isSelected)
            {
                Widgets.DrawBoxSolid(rect, new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.25f));
                GUI.color = AccentColor;
                Widgets.DrawBox(rect);
                GUI.color = Color.white;
            }
            else
            {
                Widgets.DrawBoxSolid(rect, new Color(0.10f, 0.10f, 0.13f));
                if (Mouse.IsOver(rect))
                {
                    Widgets.DrawBoxSolid(rect, new Color(0.14f, 0.14f, 0.18f));
                }
            }

            float x = rect.x + 12f;
            float y = rect.y + 10f;

            Rect iconBgRect = new Rect(x, y, 55f, 55f);
            Widgets.DrawBoxSolid(iconBgRect, new Color(0.08f, 0.08f, 0.10f));
            GUI.color = BorderColor;
            Widgets.DrawBox(iconBgRect);
            GUI.color = Color.white;
            
            Rect iconRect = new Rect(x + 2f, y + 2f, 51f, 51f);
            if (faction.def != null)
            {
                Texture2D factionIcon = faction.def.FactionIcon;
                if (factionIcon != null && factionIcon != BaseContent.BadTex)
                {
                    GUI.DrawTexture(iconRect, factionIcon);
                }
                else
                {
                    Owner.DrawDefaultFactionIcon(iconRect, faction);
                }
            }
            x += 70f;

            bool isAIControlled = GameComponent_DiplomacyManager.Instance?.IsAIControlled(faction) ?? false;
            if (isAIControlled)
            {
                Rect aiBadgeRect = new Rect(rect.xMax - 40f, rect.y + 7f, 32f, 20f);
                Widgets.DrawBoxSolid(aiBadgeRect, new Color(0.2f, 0.6f, 0.9f, 0.8f));
                Text.Font = GameFont.Tiny;
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(aiBadgeRect, "RimChat_AIBadge".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
            }

            // Factionname
            Text.Font = GameFont.Small;
            GUI.color = isSelected ? Color.white : TextPrimary;
            Rect nameRect = new Rect(x, y, rect.width - x + rect.x - 45f, 22f);
            Widgets.Label(nameRect, faction.Name ?? "RimChat_Unknown".Translate());

            if (hasUnread && !isSelected)
            {
                Rect unreadRect = new Rect(rect.xMax - 12f, rect.y + 28f, 8f, 8f);
                Widgets.DrawBoxSolid(unreadRect, new Color(0.3f, 0.8f, 1f));
            }

            y += 26f;

            int goodwill = faction.PlayerGoodwill;
            Color goodwillColor = Owner.GetGoodwillColor(goodwill);
            
            GUI.color = goodwillColor;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(x, y, 45f, 20f), goodwill.ToString());

            Rect barBgRect = new Rect(x + 50f, y + 4f, 100f, 12f);
            Widgets.DrawBoxSolid(barBgRect, new Color(0.08f, 0.08f, 0.10f));
            
            float goodwillPercent = Mathf.InverseLerp(-100f, 100f, goodwill);
            Rect barFillRect = new Rect(barBgRect.x, barBgRect.y, barBgRect.width * goodwillPercent, barBgRect.height);
            Widgets.DrawBoxSolid(barFillRect, goodwillColor);

            // Relationlabel
            string relationLabel = Owner.GetRelationLabelShort(goodwill);
            GUI.color = goodwillColor * 0.9f;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(x + 155f, y - 1f, 76f, 22f), relationLabel.Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            if (Widgets.ButtonInvisible(rect))
            {
                selectedFaction = faction;
                var session = GameComponent_DiplomacyManager.Instance?.GetSession(faction);
                session?.MarkAsRead();
                Owner.OpenDialogueWindow();
            }
        }

internal void DrawFactionDetail(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, PanelColor);
            GUI.color = BorderColor;
            Widgets.DrawBox(rect);
            GUI.color = Color.white;

            if (selectedFaction == null)
            {
                Owner.DrawEmptyState(rect);
                return;
            }

            Rect innerRect = rect.ContractedBy(15f);

            float contentHeight = 800f;
            Rect viewRect = new Rect(0f, 0f, innerRect.width - 16f, contentHeight);
            
            detailScrollPosition = GUI.BeginScrollView(innerRect, detailScrollPosition, viewRect);

            float curY = 0f;
            float width = viewRect.width;

            Rect headerRect = new Rect(0f, curY, width, 100f);
            Owner.DrawModernFactionHeader(selectedFaction, headerRect);
            curY += 115f;

            Rect relationRect = new Rect(0f, curY, width, 80f);
            Owner.DrawRelationCard(selectedFaction, relationRect);
            curY += 95f;

            Rect infoRect = new Rect(0f, curY, width, 200f);
            Owner.DrawInfoGrid(selectedFaction, infoRect);
            curY += 215f;

            Rect actionRect = new Rect(0f, curY, width, 60f);
            Owner.DrawModernActionButtons(actionRect);
            curY += 75f;

            Rect aiRect = new Rect(0f, curY, width, 70f);
            Owner.DrawModernAIStatus(selectedFaction, aiRect);

            GUI.EndScrollView();
        }

internal void DrawEmptyState(Rect rect)
        {
            GUI.color = new Color(0.3f, 0.3f, 0.35f);
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Medium;
            Widgets.Label(rect, "RimChat_SelectFactionPrompt".Translate());
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

internal void DrawModernFactionHeader(Faction faction, Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.10f, 0.10f, 0.13f));
            GUI.color = BorderColor;
            Widgets.DrawBox(rect);
            GUI.color = Color.white;

            float x = rect.x + 15f;
            float y = rect.y + 15f;

            Rect iconRect = new Rect(x, y, 70f, 70f);
            Widgets.DrawBoxSolid(iconRect, new Color(0.08f, 0.08f, 0.10f));
            GUI.color = BorderColor;
            Widgets.DrawBox(iconRect);
            GUI.color = Color.white;
            
            Rect iconInnerRect = new Rect(x + 3f, y + 3f, 64f, 64f);
            if (faction.def != null)
            {
                Texture2D factionIcon = faction.def.FactionIcon;
                if (factionIcon != null && factionIcon != BaseContent.BadTex)
                {
                    GUI.DrawTexture(iconInnerRect, factionIcon);
                }
                else
                {
                    Owner.DrawDefaultFactionIcon(iconInnerRect, faction);
                }
            }
            x += 90f;

            // Factionname
            Text.Font = GameFont.Medium;
            GUI.color = TextPrimary;
            Widgets.Label(new Rect(x, y, rect.width - x + rect.x - 20f, 30f),
                faction.Name ?? "RimChat_Unknown".Translate());

            Rect infoButtonRect = new Rect(rect.xMax - 40f, y, 28f, 28f);
            Owner.DrawInfoButton(infoButtonRect, () => Owner.OpenFactionDefInfoCard(faction));

            y += 32f;
            Rect typeBadgeRect = new Rect(x, y, 120f, 22f);
            Widgets.DrawBoxSolid(typeBadgeRect, new Color(0.20f, 0.20f, 0.25f));
            Text.Font = GameFont.Tiny;
            GUI.color = TextSecondary;
            Widgets.Label(typeBadgeRect, faction.def?.label?.CapitalizeFirst() ?? "RimChat_Unknown".Translate());

            bool isAIControlled = GameComponent_DiplomacyManager.Instance?.IsAIControlled(faction) ?? false;
            if (isAIControlled)
            {
                Rect aiBadgeRect = new Rect(x + 130f, y, 60f, 22f);
                Widgets.DrawBoxSolid(aiBadgeRect, new Color(0.2f, 0.6f, 0.9f, 0.6f));
                GUI.color = Color.white;
                Widgets.Label(aiBadgeRect, "RimChat_AIControl".Translate());
            }

            Text.Font = GameFont.Small;
            GUI.color = Color.white;
        }
    }
}
