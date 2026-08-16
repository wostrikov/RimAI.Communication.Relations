using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;

namespace Ustas.RimAI.Communication.Relations.Prompting
{
    /// <summary>
    /// Dependencies: RimChat settings prompt section catalog.
    /// Responsibility: resolve long-lived prompt section text from the native section catalog with safe fallbacks.
    /// </summary>
    internal static class PromptEntryStaticTextCatalog
    {
        internal static class DiplomacyDialogueRequest
        {
            public static string SystemRules => Resolve("system_rules");

            public static string CharacterPersona => Resolve("character_persona");

            public static string MemorySystem => Resolve("memory_system");

            public static string EnvironmentPerception => Resolve("environment_perception");

            public static string Context => Resolve("context");

            public static string ActionRules => Resolve("action_rules");

            public static string RepetitionReinforcement => Resolve("repetition_reinforcement");

            public static string OutputSpecification => Resolve("output_specification");

            private static string Resolve(string sectionId)
            {
                RimTalkPromptEntryDefaultsConfig catalog = RelationsMod.Settings?.GetPromptSectionCatalogClone();
                string configured = PromptSectionAggregateBuilder.ResolveTemplate(
                    catalog ?? RimTalkPromptEntryDefaultsProvider.GetDefaultsSnapshot(),
                    RimTalkPromptEntryChannelCatalog.DiplomacyDialogue,
                    sectionId);
                if (!string.IsNullOrWhiteSpace(configured))
                {
                    return configured;
                }

                return RimTalkPromptEntryDefaultsProvider.ResolveContent(
                    RimTalkPromptEntryChannelCatalog.DiplomacyDialogue,
                    sectionId);
            }
        }
    }
}
