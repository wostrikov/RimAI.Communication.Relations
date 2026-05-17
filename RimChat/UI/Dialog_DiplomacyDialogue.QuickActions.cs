using System;
using RimChat.AI;
using RimChat.DiplomacySystem;
using RimChat.Memory;
using RimWorld;
using Verse;

namespace RimChat.UI
{
    /// <summary>
    /// Dependencies: GameAIInterface, RimWorld widgets.
    /// Responsibility: quick-action Make Peace / Declare War handlers injected into the send-info menu.
    /// </summary>
    public partial class Dialog_DiplomacyDialogue
    {
        private FloatMenuOption BuildQuickMakePeaceMenuOption()
        {
            string label = "RimChat_QuickActionMakePeace".Translate().ToString();
            bool canPeace = faction != null && faction.HostileTo(Faction.OfPlayer);
            return new FloatMenuOption(label, canPeace ? (Action)HandleQuickMakePeace : null);
        }

        private FloatMenuOption BuildQuickDeclareWarMenuOption()
        {
            string label = "RimChat_QuickActionDeclareWar".Translate().ToString();
            bool canWar = faction != null && !faction.HostileTo(Faction.OfPlayer) && faction.PlayerGoodwill < -75;
            return new FloatMenuOption(label, canWar ? (Action)HandleQuickDeclareWar : null);
        }

        private void HandleQuickMakePeace()
        {
            if (WindowStackHasDialogOfType<Dialog_MessageBox>()) return;

            int peaceCost = RimChat.Core.RimChatMod.Settings?.MaxPeaceCost ?? 500;
            var prepareResult = GameAIInterface.Instance.PrepareMakePeacePayment(faction, peaceCost, negotiator);
            if (!prepareResult.Success)
            {
                Messages.Message(prepareResult.Message ?? "Unknown error", MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (prepareResult.Data is PreparedMakePeacePaymentData preparedData)
            {
                ShowMakePeaceConfirmationDialog(session, faction,
                    new AIAction { ActionType = AIActionNames.MakePeace, Parameters = new System.Collections.Generic.Dictionary<string, object> { ["cost"] = peaceCost } },
                    preparedData);
            }
        }

        private void HandleQuickDeclareWar()
        {
            if (WindowStackHasDialogOfType<Dialog_MessageBox>()) return;

            string title = "RimChat_DeclareWarConfirmTitle".Translate().ToString();
            string body = "RimChat_DeclareWarConfirmBody".Translate(faction.Name).ToString();
            string acceptLabel = "RimChat_DeclareWarConfirmAccept".Translate().ToString();
            string cancelLabel = "RimChat_DeclareWarConfirmCancel".Translate().ToString();

            Find.WindowStack.Add(new Dialog_MessageBox(body, acceptLabel,
                () => CommitQuickDeclareWar(), cancelLabel, null, title));
        }

        private void CommitQuickDeclareWar()
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

        private static bool WindowStackHasDialogOfType<T>() where T : Window
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
}
