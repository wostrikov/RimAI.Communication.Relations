using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.QuestGen;
using Verse;
using Ustas.RimAI.Communication.Relations.Diagnostics;

namespace Ustas.RimAI.Communication.Relations.DiplomacySystem
{
    public class QuestPart_AICallback : QuestPart
    {
        public string callbackId;
        public Faction faction;
        public string inSignalSuccess;
        public string inSignalFailed;

        public override void Notify_QuestSignalReceived(Signal signal)
        {
            base.Notify_QuestSignalReceived(signal);

            if (!inSignalSuccess.NullOrEmpty() && signal.tag == inSignalSuccess)
            {
                OnQuestStateChanged("Success");
            }
            else if (!inSignalFailed.NullOrEmpty() && signal.tag == inSignalFailed)
            {
                OnQuestStateChanged("Failed");
            }
        }

        private void OnQuestStateChanged(string state)
        {
            ModuleLog.Message($"[RimAI.Relations] Quest {quest.name} state changed to {state}. CallbackId: {callbackId}");
            
            if (faction != null)
            {
                Messages.Message($"Завдання '{quest.name}' {state} (фракція: {faction.Name})", MessageTypeDefOf.NeutralEvent);
                int value = string.Equals(state, "Success", StringComparison.Ordinal) ? 1 : -1;
                string questId = quest != null ? quest.id.ToString() : (quest?.name ?? "UnknownQuest");
                string key = $"quest:{callbackId}:{questId}:{state}";
                GameComponent_DiplomacyManager.Instance?.RecordScheduledSocialEvent(
                    ScheduledSocialEventType.QuestResult,
                    faction,
                    Faction.OfPlayer,
                    $"Quest result from {faction.Name}: {quest?.name ?? "UnknownQuest"} is {state}.",
                    $"callback={callbackId}, state={state}",
                    value,
                    key);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref callbackId, "callbackId");
            string factionId = faction?.GetUniqueLoadID() ?? string.Empty;
            Scribe_Values.Look(ref factionId, "factionId", string.Empty);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (!string.IsNullOrEmpty(factionId))
                {
                    faction = Find.FactionManager.AllFactions.FirstOrDefault(f => f.GetUniqueLoadID() == factionId);
                }
                // If factionId is empty, faction remains null.
            }
            Scribe_Values.Look(ref inSignalSuccess, "inSignalSuccess");
            Scribe_Values.Look(ref inSignalFailed, "inSignalFailed");
        }
    }

    public class QuestNode_AddAICallback : QuestNode
    {
        public SlateRef<string> callbackId;
        public SlateRef<string> inSignalSuccess;
        public SlateRef<string> inSignalFailed;

        protected override bool TestRunInt(Slate slate)
        {
            return true;
        }

        protected override void RunInt()
        {
            Slate slate = QuestGen.slate;
            QuestPart_AICallback questPart = new QuestPart_AICallback();
            questPart.callbackId = callbackId.GetValue(slate);
            questPart.faction = slate.Get<Faction>("askerFaction");
            
            // Resolve signals
            string successSignal = inSignalSuccess.GetValue(slate);
            string failedSignal = inSignalFailed.GetValue(slate);
            
            questPart.inSignalSuccess = QuestGenUtility.HardcodedSignalWithQuestID(successSignal);
            questPart.inSignalFailed = QuestGenUtility.HardcodedSignalWithQuestID(failedSignal);

            QuestGen.quest.AddPart(questPart);
        }
    }
}
