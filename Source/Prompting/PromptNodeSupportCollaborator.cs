using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Dialogue;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.Prompting;
using RimWorld;
using Verse;
using Ustas.RimAI.Communication.Relations.Context;
using Ustas.RimAI.Communication.Relations.Prompting.Diplomacy;

using Ustas.RimAI.Communication.Relations.Persistence;

namespace Ustas.RimAI.Communication.Relations.Prompting
{
    internal abstract class PromptNodeSupportCollaborator
    {
        internal readonly PromptNodeSupport Owner;

        protected PromptNodeSupportCollaborator(PromptNodeSupport owner)
        {
            Owner = owner;
        }
        protected PromptNodeSupportParts Parts => Owner.Parts;


        protected PromptPersistenceService host => Owner.host;
        protected const string DefaultDiplomacyFallbackRoleTemplate =
            PromptNodeSupport.DefaultDiplomacyFallbackRoleTemplate;
    }
}
