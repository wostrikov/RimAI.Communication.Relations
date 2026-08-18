using System;
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Persistence;
using Verse;
using Ustas.RimAI.Communication.Relations.Prompting.Transfer;

namespace Ustas.RimAI.Communication.Relations.Config
{
        internal abstract class PromptUnifiedChannelConfigCollaborator
    {
        internal readonly PromptUnifiedChannelConfig Owner;

        protected PromptUnifiedChannelConfigCollaborator(PromptUnifiedChannelConfig owner)
        {
            Owner = owner;
        }

        protected PromptUnifiedChannelConfigParts Parts => Owner.Parts;
        protected string PromptChannel
        {
            get => Owner.PromptChannel;
            set => Owner.PromptChannel = value;
        }
        protected List<PromptUnifiedSectionContent> Sections
        {
            get => Owner.Sections;
            set => Owner.Sections = value;
        }
        protected List<PromptUnifiedNodeContent> Nodes
        {
            get => Owner.Nodes;
            set => Owner.Nodes = value;
        }
        protected List<PromptUnifiedNodeLayoutConfig> NodeLayout
        {
            get => Owner.NodeLayout;
            set => Owner.NodeLayout = value;
        }
        protected List<PromptSectionLayoutConfig> SectionLayout
        {
            get => Owner.SectionLayout;
            set => Owner.SectionLayout = value;
        }
        protected List<PromptUnifiedTemplateAliasConfig> TemplateAliases
        {
            get => Owner.TemplateAliases;
            set => Owner.TemplateAliases = value;
        }
        protected List<PromptUnifiedNodeRegistration> CustomNodes
        {
            get => Owner.CustomNodes;
            set => Owner.CustomNodes = value;
        }
    }

}
