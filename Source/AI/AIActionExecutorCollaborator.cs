using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RimWorld;
using Verse;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Relation;

namespace Ustas.RimAI.Communication.Relations.AI
{
        internal abstract class AIActionExecutorCollaborator
    {
        internal readonly AIActionExecutor Owner;

        protected AIActionExecutorCollaborator(AIActionExecutor owner)
        {
            Owner = owner;
        }

        protected AIActionExecutorParts Parts => Owner.Parts;
        protected Faction faction => Owner.faction;
        protected GameAIInterface gameInterface => Owner.gameInterface;
        protected bool applyDialogueApiGoodwillCost => Owner.applyDialogueApiGoodwillCost;
    }

}
