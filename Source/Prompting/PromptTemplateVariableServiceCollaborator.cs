using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.Guards;
using RimWorld;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Context;

using Ustas.RimAI.Communication.Relations.Persistence;

namespace Ustas.RimAI.Communication.Relations.Prompting
{
        internal abstract class PromptTemplateVariableServiceCollaborator
    {
        internal readonly PromptTemplateVariableService Owner;

        protected PromptTemplateVariableServiceCollaborator(PromptTemplateVariableService owner)
        {
            Owner = owner;
        }

        protected PromptTemplateVariableServiceParts Parts => Owner.Parts;
        protected PromptPersistenceService host => Owner.host;
        protected static Regex TemplateVariableRegex => PromptTemplateVariableService.TemplateVariableRegex;
        protected static HashSet<string> AllowedTemplateVariableNamespaces => PromptTemplateVariableService.AllowedTemplateVariableNamespaces;
    }

}
