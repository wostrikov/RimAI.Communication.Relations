using RimWorld;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.UI;
using Ustas.RimAI.Core.UI;

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class RelationsGameplayUxSection
{
    readonly RelationsSettingsPages Pages;

    internal RelationsGameplayUxSection(RelationsSettingsPages pages)
    {
        Pages = pages;
    }

    RelationsSettings Settings => Pages.Settings;

        internal void DrawAIControlHeaderBar(Listing_Standard listing)
        {
            if (listing == null)
            {
                return;
            }

            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            Color oldColor = GUI.color;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            DrawAIQuickSummary(listing);

            Text.Font = oldFont;
            Text.Anchor = oldAnchor;
            GUI.color = oldColor;
        }

        internal void DrawAIQuickSummary(Listing_Standard listing)
        {
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            Color oldColor = GUI.color;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            Rect cardRect = listing.GetRect(52f);
            Widgets.DrawBoxSolid(cardRect, new Color(0.10f, 0.14f, 0.20f, 0.55f));

            Rect labelRect = new Rect(cardRect.x + 6f, cardRect.y + 2f, cardRect.width - 12f, 22f);
            GUI.color = new Color(0.75f, 0.9f, 1f);
            Widgets.Label(labelRect, "RimChat_AIQuickTools".Translate());

            int enabledActions = GetEnabledAIBehaviorActionCount();
            GUI.color = Color.white;
            Rect infoRect = new Rect(cardRect.x + 6f, cardRect.y + 24f, cardRect.width - 12f, 22f);
            Widgets.Label(
                infoRect,
                "RimChat_AIQuickSummary".Translate(
                    "RimChat_AIQuickEnabledActions".Translate(enabledActions, 7),
                    "RimChat_AIQuickApiLimit".Translate(FormatApiLimitForQuickSummary())));

            Text.Font = oldFont;
            Text.Anchor = oldAnchor;
            GUI.color = oldColor;
        }

        internal int GetEnabledAIBehaviorActionCount()
        {
            int count = 0;
            if (Settings.EnableAIGoodwillAdjustment) count++;
            if (Settings.EnableAIGiftSending) count++;
            if (Settings.EnableAIWarDeclaration) count++;
            if (Settings.EnableAIPeaceMaking) count++;
            if (Settings.EnableAITradeCaravan) count++;
            if (Settings.EnableAIAidRequest) count++;
            if (Settings.EnableAIRaidRequest) count++;
            return count;
        }

        internal string FormatApiLimitForQuickSummary()
        {
            int limit = Mathf.Max(0, Settings.MaxAPICallsPerHour);
            if (limit <= 0)
            {
                return "RimChat_Unlimited".Translate().ToString();
            }

            return "RimChat_ApiPerHourValue".Translate(limit).ToString();
        }
    
}
