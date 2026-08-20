using System.Collections.Generic;
using RimWorld.QuestGen;
using Verse;
using Verse.Grammar;

namespace Ustas.RimAI.Communication.Relations.DiplomacySystem
{
    public class QuestNode_InjectSlateToGrammar : QuestNode
    {
        public SlateRef<string> prefix;

        protected override bool TestRunInt(Slate slate)
        {
            return true;
        }

        protected override void RunInt()
        {
            Slate slate = QuestGen.slate;
            string p = prefix.GetValue(slate) ?? "";

            if (slate.Exists("title"))
            {
                string val = slate.Get<string>("title");
                QuestGen.AddQuestNameRules(new List<Rule> { new Rule_String(p + "title", val) });
            }

            if (slate.Exists("description"))
            {
                string val = slate.Get<string>("description");
                QuestGen.AddQuestDescriptionRules(new List<Rule> { new Rule_String(p + "description", val) });
            }
            
            if (slate.Exists("rewardDescription"))
            {
                string val = slate.Get<string>("rewardDescription");
                QuestGen.AddQuestDescriptionRules(new List<Rule> { new Rule_String(p + "rewardDescription", val) });
            }
        }
    }
}
