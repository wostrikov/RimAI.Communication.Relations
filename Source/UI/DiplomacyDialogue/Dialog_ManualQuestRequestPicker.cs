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



internal sealed class Dialog_ManualQuestRequestPicker : Window
{
    private readonly Dialog_DiplomacyDialogue owner;
    private readonly List<ManualQuestRequestOption> options;
    private ManualQuestRequestOption selectedOption;
    private Vector2 scrollPosition = Vector2.zero;
    private bool committed;

    public Dialog_ManualQuestRequestPicker(Dialog_DiplomacyDialogue owner, List<ManualQuestRequestOption> options)
    {
        this.owner = owner;
        this.options = options ?? new List<ManualQuestRequestOption>();

        doCloseX = true;
        closeOnCancel = true;
        closeOnClickedOutside = true;
        absorbInputAroundWindow = true;
        forcePause = true;
        onlyOneOfTypeAllowed = true;
        draggable = true;
    }

    public override Vector2 InitialSize => new Vector2(620f, 520f);

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "RimChat_SendInfoQuestPickerTitle".Translate());

        Text.Font = GameFont.Small;
        Widgets.Label(
            new Rect(inRect.x, inRect.y + 34f, inRect.width, 24f),
            "RimChat_SendInfoQuestPickerSubtitle".Translate(owner?.faction?.Name ?? "Unknown"));

        Rect listRect = new Rect(inRect.x, inRect.y + 64f, inRect.width, inRect.height - 112f);
        DrawList(listRect);

        bool hasSelection = selectedOption != null;
        Rect confirmRect = new Rect(inRect.x + inRect.width - 326f, inRect.yMax - 38f, 160f, 32f);
        GUI.color = hasSelection ? Color.white : Color.gray;
        if (Widgets.ButtonText(confirmRect, "RimChat_SendInfoQuestPickerConfirm".Translate()))
        {
            if (!CommitSelection())
            {
                Messages.Message("RimChat_SendInfoQuestPickerEmptySelection".Translate(), MessageTypeDefOf.RejectInput, false);
            }
        }

        GUI.color = Color.white;
        Rect cancelRect = new Rect(inRect.x + inRect.width - 160f, inRect.yMax - 38f, 160f, 32f);
        if (Widgets.ButtonText(cancelRect, "RimChat_SendInfoQuestPickerCancel".Translate()))
        {
            Close();
        }
    }

    private void DrawList(Rect rect)
    {
        Widgets.DrawMenuSection(rect);
        Rect inner = rect.ContractedBy(6f);
        if (options == null || options.Count == 0)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = Color.gray;
            Widgets.Label(inner, "RimChat_SendInfoQuestPickerNoOptions".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            return;
        }

        const float rowHeight = 42f;
        float totalHeight = Mathf.Max(inner.height, options.Count * rowHeight);
        Rect viewRect = new Rect(0f, 0f, inner.width - 16f, totalHeight);
        Widgets.BeginScrollView(inner, ref scrollPosition, viewRect);
        float y = 0f;
        foreach (ManualQuestRequestOption option in options)
        {
            DrawOptionRow(new Rect(0f, y, viewRect.width, rowHeight - 2f), option);
            y += rowHeight;
        }

        Widgets.EndScrollView();
    }

    private void DrawOptionRow(Rect rect, ManualQuestRequestOption option)
    {
        Widgets.DrawHighlightIfMouseover(rect);
        Widgets.DrawBox(rect, 1);
        if (option == null)
        {
            return;
        }

        bool selected = selectedOption != null && string.Equals(selectedOption.QuestDefName, option.QuestDefName, StringComparison.Ordinal);
        Widgets.RadioButton(new Vector2(rect.x + 10f, rect.y + 11f), selected);
        Widgets.Label(new Rect(rect.x + 38f, rect.y + 10f, rect.width - 46f, 22f), option.Label);

        if (!Widgets.ButtonInvisible(rect))
        {
            return;
        }

        selectedOption = option;
    }

    private bool CommitSelection()
    {
        if (selectedOption == null)
        {
            return false;
        }

        committed = true;
        owner?.Parts.SendInfo.SubmitManualQuestRequest(selectedOption);
        Close();
        return true;
    }

    public override void PreClose()
    {
        base.PreClose();
        if (!committed)
        {
            owner?.Parts.SendInfo.HandleManualQuestRequestPickerClosedWithoutSelection();
        }
    }
}

