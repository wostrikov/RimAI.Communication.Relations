using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.Diagnostics;
using RimWorld;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Context;

using Ustas.RimAI.Communication.Relations.Persistence;

namespace Ustas.RimAI.Communication.Relations.Prompting
{
        internal abstract class PromptWorkspaceComposerCollaborator
    {
        internal readonly PromptWorkspaceComposer Owner;

        protected PromptWorkspaceComposerCollaborator(PromptWorkspaceComposer owner)
        {
            Owner = owner;
        }
        protected PromptWorkspaceComposerParts Parts => Owner.Parts;


        protected PromptPersistenceService host => Owner.host;
    }

}
