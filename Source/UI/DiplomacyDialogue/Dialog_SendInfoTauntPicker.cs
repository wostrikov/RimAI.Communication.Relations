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



internal sealed class Dialog_SendInfoTauntPicker : Window
{
    private readonly Dialog_DiplomacyDialogue owner;

    public Dialog_SendInfoTauntPicker(Dialog_DiplomacyDialogue owner)
    {
        this.owner = owner;
        absorbInputAroundWindow = true;
        closeOnClickedOutside = true;
        doCloseButton = false;
        doCloseX = true;
        draggable = true;
        forcePause = true;
    }

    public override Vector2 InitialSize => new Vector2(560f, 300f);

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 34f), "RimChat_SendInfoTauntTitle".Translate());
        Text.Font = GameFont.Small;

        float y = inRect.y + 44f;
        foreach (TauntSendInfoOption option in DiplomacySendInfoWorkflow.TauntSendInfoOptions)
        {
            Rect cardRect = new Rect(inRect.x, y, inRect.width, 64f);
            Widgets.DrawMenuSection(cardRect);

            Rect buttonRect = new Rect(cardRect.x + 12f, cardRect.y + 10f, 160f, 34f);
            if (Widgets.ButtonText(buttonRect, option.LabelKey.Translate()))
            {
                owner.Parts.SendInfo.SubmitTauntSendInfo(option);
                Close();
                return;
            }

            Rect descRect = new Rect(buttonRect.xMax + 12f, cardRect.y + 8f, cardRect.width - buttonRect.width - 36f, 44f);
            Widgets.Label(descRect, option.DescriptionKey.Translate());
            y += cardRect.height + 10f;
        }
    }
}

