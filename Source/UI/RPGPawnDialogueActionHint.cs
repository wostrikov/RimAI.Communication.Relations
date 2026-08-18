using System.Text;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Communication.Relations.UI
{
    /// <summary>/// 依赖: RPG Action label映射 (GetRpgActionLabel) .
 /// 职责: 发送button旁问号提示与 RPG Actions Tooltip 渲染.
 ///</summary>
        internal sealed class RPGPawnDialogueActionHint : Dialog_RPGPawnDialogueCollaborator
    {
        internal RPGPawnDialogueActionHint(Dialog_RPGPawnDialogue owner) : base(owner)
        {
        }


        internal static readonly string[] RpgActionHintOrder =
        {
            "ExitDialogue",
            "ExitDialogueCooldown",
            "RomanceAttempt",
            "MarriageProposal",
            "Breakup",
            "Divorce",
            "Date",
            "TryGainMemory",
            "TryAffectSocialGoodwill",
            "ReduceResistance",
            "ReduceWill",
            "Recruit",
            "ConvertIdeology",
            "AdjustCertainty",
            "TryTakeOrderedJob",
            "TriggerIncident",
            "GrantInspiration"
        };

        internal string rpgActionHintTooltipCache = string.Empty;

        internal void DrawRpgPotentialActionsHint(Rect sendRect, float uiAlpha)
        {
            float visibleAlpha = Mathf.Clamp01(uiAlpha);
            if (visibleAlpha <= 0.01f)
            {
                return;
            }

            Rect hintRect = new Rect(sendRect.xMax - 16f, sendRect.yMax + 2f, 24f, 18f);
            bool hovered = Mouse.IsOver(hintRect);
            float targetAlpha = hovered ? Mathf.Max(visibleAlpha, 0.84f) : Mathf.Max(visibleAlpha * 0.65f, 0.3f);

            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            Color oldColor = GUI.color;

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = new Color(0.9f, 0.92f, 1f, targetAlpha);
            Widgets.Label(hintRect, "[?]");

            Text.Font = oldFont;
            Text.Anchor = oldAnchor;
            GUI.color = oldColor;

            TooltipHandler.TipRegion(hintRect, Owner.GetRpgPotentialActionsTooltipText());
        }

        internal string GetRpgPotentialActionsTooltipText()
        {
            if (!string.IsNullOrEmpty(rpgActionHintTooltipCache))
            {
                return rpgActionHintTooltipCache;
            }

            var sb = new StringBuilder();
            sb.AppendLine("RimChat_ActionsHint_RpgTitle".Translate());
            foreach (string actionName in RpgActionHintOrder)
            {
                sb.AppendLine("- " + Owner.GetRpgActionLabel(actionName));
            }

            rpgActionHintTooltipCache = sb.ToString().TrimEnd();
            return rpgActionHintTooltipCache;
        }
        }

}
