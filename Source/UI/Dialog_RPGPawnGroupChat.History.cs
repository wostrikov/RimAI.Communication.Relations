using System;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Communication.Relations.UI
{
    public partial class Dialog_RPGPawnGroupChat
    {
        private bool isHistoryPanelOpen;
        private Vector2 historyScrollPos = Vector2.zero;
        private const float HistoryPanelMinW = 600f;
        private const float HistoryPanelMaxW = 860f;
        private const float HistoryPanelMinH = 200f;
        private const float HistoryPanelMaxH = 560f;

        private void DrawHistoryToggleButton(Rect boxRect)
        {
            Rect btnRect = new Rect(boxRect.x + 14f, boxRect.yMax - 30f, 100f, 25f);
            if (Widgets.ButtonText(btnRect, "RimChat_RPGHistoryButton".Translate()))
            {
                isHistoryPanelOpen = !isHistoryPanelOpen;
                if (isHistoryPanelOpen)
                {
                    isViewingHistory = false;
                    currentTextPageIndex = 0;
                    currentTextPages.Clear();
                }
            }
        }

        private void DrawHistoryPanel(Rect inRect)
        {
            if (!isHistoryPanelOpen || dialogPages.Count == 0) return;

            float sidePad = Mathf.Clamp(inRect.width * 0.15f, 160f, 300f);
            float panelW = Mathf.Clamp(inRect.width - sidePad * 2f, HistoryPanelMinW, HistoryPanelMaxW);
            float panelH = Mathf.Clamp(inRect.height * 0.7f, HistoryPanelMinH, HistoryPanelMaxH);
            float panelX = (inRect.width - panelW) / 2f;
            float panelY = (inRect.height - panelH) / 2f - 30f;
            Rect panelRect = new Rect(panelX, panelY, panelW, panelH);

            Widgets.DrawBoxSolid(panelRect, new Color(0.07f, 0.07f, 0.09f, 0.95f));
            GUI.color = new Color(0.25f, 0.25f, 0.3f, 1f);
            Widgets.DrawBox(panelRect, 2);
            GUI.color = Color.white;

            // Header
            Rect titleRect = new Rect(panelRect.x + 16f, panelRect.y + 10f, panelRect.width - 72f, 30f);
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, "RimChat_RPGHistoryPanelTitle".Translate());
            Text.Anchor = TextAnchor.UpperLeft;

            Rect closeRect = new Rect(panelRect.xMax - 44f, panelRect.y + 10f, 28f, 28f);
            if (Widgets.ButtonText(closeRect, "×"))
            {
                isHistoryPanelOpen = false;
                return;
            }

            // Body — scrollable records, dynamic row height (mirrors 1v1 history pattern)
            Rect bodyRect = new Rect(panelRect.x + 12f, panelRect.y + 44f, panelRect.width - 24f, panelRect.height - 54f);
            float viewWidth = bodyRect.width - 18f;
            float totalH = 4f;
            GameFont prevFont = Text.Font;
            Text.Font = GameFont.Tiny;
            for (int i = 0; i < dialogPages.Count; i++)
            {
                string entry = dialogPages[i].speakerName + ": " + dialogPages[i].text;
                totalH += MeasureGroupHistoryEntryHeight(entry, viewWidth) + 2f;
            }
            Text.Font = prevFont;
            Rect viewRect = new Rect(0f, 0f, viewWidth, Mathf.Max(totalH, 40f));
            historyScrollPos = GUI.BeginScrollView(bodyRect, historyScrollPos, viewRect);

            float y = 1f;
            for (int i = 0; i < dialogPages.Count; i++)
            {
                string entry = dialogPages[i].speakerName + ": " + dialogPages[i].text;
                float entryH = MeasureGroupHistoryEntryHeight(entry, viewWidth);
                Rect rowRect = new Rect(5f, y, viewWidth - 10f, entryH);
                Widgets.DrawBoxSolid(rowRect, i % 2 == 0 ? new Color(1f, 1f, 1f, 0.025f) : new Color(1f, 1f, 1f, 0.055f));
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(rowRect, entry);
                y += entryH + 2f;
            }
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.EndScrollView();
        }

        private bool TryHandleHistoryPanelClick()
        {
            if (!isHistoryPanelOpen) return false;
            // Click outside the panel closes it — handled in DrawHistoryPanel via close button,
            // or you can click outside. We handle the outside click in DoWindowContents.
            return false;
        }

        private static float MeasureGroupHistoryEntryHeight(string text, float width)
        {
            if (string.IsNullOrWhiteSpace(text)) return 20f;
            GameFont prev = Text.Font;
            Text.Font = GameFont.Tiny;
            float h = Text.CalcHeight(text, width);
            Text.Font = prev;
            return Mathf.Max(20f, h + 4f);
        }
    }
}
