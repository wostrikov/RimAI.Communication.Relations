using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Dialogue;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Relation;
using Ustas.RimAI.Communication.Relations.Guards;
using Ustas.RimAI.Communication.Relations.WorldState;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.Prompting.Builders;
using Ustas.RimAI.Communication.Relations.Context;
using Ustas.RimAI.Communication.Relations.Prompting.Transfer;
using Ustas.RimAI.Communication.Relations.Serialization;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Prompting.Diplomacy;

namespace Ustas.RimAI.Communication.Relations.Persistence
{
        internal abstract class PromptConfigNormalizationCollaborator
    {
        internal readonly PromptConfigNormalization Owner;

        protected PromptConfigNormalizationCollaborator(PromptConfigNormalization owner)
        {
            Owner = owner;
        }

        protected PromptConfigNormalizationParts Parts => Owner.Parts;
        protected PromptPersistenceService host => Owner.host;
        protected PromptTemplateAutoRewriteResult _lastSchemaRewriteResult
        {
            get => Owner._lastSchemaRewriteResult;
            set => Owner._lastSchemaRewriteResult = value;
        }
        protected static string[] PresenceBehaviorSectionTitles => PromptConfigNormalization.PresenceBehaviorSectionTitles;
        protected static string[] PresenceBehaviorActionAnchors => PromptConfigNormalization.PresenceBehaviorActionAnchors;
    }

}
