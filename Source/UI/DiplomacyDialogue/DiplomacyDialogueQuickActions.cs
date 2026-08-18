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

internal sealed class DiplomacyDialogueQuickActions : DiplomacyDialogueCollaborator
{
    internal DiplomacyDialogueQuickActions(Dialog_DiplomacyDialogue owner) : base(owner) { }


internal void OpenHistoryWindow()
{
    if (faction == null)
    {
        Messages.Message("RimChat_DiplomacyHistoryNoFaction".Translate(), global::RimWorld.MessageTypeDefOf.RejectInput, false);
        return;
    }

    Find.WindowStack.Add(new Dialog_DiplomacyHistory(faction));
}


internal FloatMenuOption BuildQuickMakePeaceMenuOption()
{
    string label = "RimChat_QuickActionMakePeace".Translate().ToString();
    bool canPeace = faction != null && faction.HostileTo(Faction.OfPlayer);
    return new FloatMenuOption(label, canPeace ? (Action)HandleQuickMakePeace : null);
}



internal FloatMenuOption BuildQuickDeclareWarMenuOption()
{
    string label = "RimChat_QuickActionDeclareWar".Translate().ToString();
    bool canWar = faction != null && !faction.HostileTo(Faction.OfPlayer) && faction.PlayerGoodwill < -75;
    return new FloatMenuOption(label, canWar ? (Action)HandleQuickDeclareWar : null);
}



internal void HandleQuickMakePeace()
{
    if (WindowStackHasDialogOfType<Dialog_MessageBox>()) return;

    int peaceCost = Ustas.RimAI.Communication.Relations.Module.RelationsMod.Settings?.MaxPeaceCost ?? 500;
    var prepareResult = GameAIInterface.Instance.PrepareMakePeacePayment(faction, peaceCost, negotiator);
    if (!prepareResult.Success)
    {
        Messages.Message(prepareResult.Message ?? "Unknown error", MessageTypeDefOf.RejectInput, false);
        return;
    }

    if (prepareResult.Data is PreparedMakePeacePaymentData preparedData)
    {
        Owner.Parts.Peace.ShowMakePeaceConfirmationDialog(session, faction,
            new AIAction { ActionType = AIActionNames.MakePeace, Parameters = new System.Collections.Generic.Dictionary<string, object> { ["cost"] = peaceCost } },
            preparedData);
    }
}



internal void HandleQuickDeclareWar()
{
    if (WindowStackHasDialogOfType<Dialog_MessageBox>()) return;

    string title = "RimChat_DeclareWarConfirmTitle".Translate().ToString();
    string body = "RimChat_DeclareWarConfirmBody".Translate(faction.Name).ToString();
    string acceptLabel = "RimChat_DeclareWarConfirmAccept".Translate().ToString();
    string cancelLabel = "RimChat_DeclareWarConfirmCancel".Translate().ToString();

    Find.WindowStack.Add(new Dialog_MessageBox(body, acceptLabel,
        () => CommitQuickDeclareWar(), cancelLabel, null, title));
}



internal void CommitQuickDeclareWar()
{
    GameAIInterface.APIResult result = GameAIInterface.Instance.DeclareWar(faction, "Player quick action");
    if (result.Success)
    {
        string message = "RimChat_DeclareWarConfirmedSystem".Translate(faction.Name).ToString();
        session?.AddMessage("System", message, false, DialogueMessageType.System);
        Messages.Message(message, MessageTypeDefOf.ThreatBig, false);
    }
    else
    {
        string message = "RimChat_DeclareWarFailedSystem".Translate(result.Message ?? "Unknown error").ToString();
        session?.AddMessage("System", message, false, DialogueMessageType.System);
        Messages.Message(message, MessageTypeDefOf.RejectInput, false);
    }
}



internal static bool WindowStackHasDialogOfType<T>() where T : Window
{
    var windows = Find.WindowStack?.Windows;
    if (windows == null) return false;
    for (int i = 0; i < windows.Count; i++)
    {
        if (windows[i] is T) return true;
    }
    return false;
}
}
