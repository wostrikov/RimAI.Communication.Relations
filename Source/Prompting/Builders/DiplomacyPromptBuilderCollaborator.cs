using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Context;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Prompting;

namespace Ustas.RimAI.Communication.Relations.Prompting.Builders
{
        internal abstract class DiplomacyPromptBuilderCollaborator
    {
        internal readonly DiplomacyPromptBuilder Owner;

        protected DiplomacyPromptBuilderCollaborator(DiplomacyPromptBuilder owner)
        {
            Owner = owner;
        }

        protected DiplomacyPromptBuilderParts Parts => Owner.Parts;
        protected PromptPersistenceService promptService => Owner.promptService;
    }

}
