using System.Collections.Generic;
using Ustas.RimAI.Communication.Relations.Persistence;
using Verse;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Context;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Prompting;

namespace Ustas.RimAI.Communication.Relations.Prompting.Builders
{
        internal abstract class RpgPromptBuilderCollaborator
    {
        internal readonly RpgPromptBuilder Owner;

        protected RpgPromptBuilderCollaborator(RpgPromptBuilder owner)
        {
            Owner = owner;
        }

        protected RpgPromptBuilderParts Parts => Owner.Parts;
        protected PromptPersistenceService promptService => Owner.promptService;
    }

}
