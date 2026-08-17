using System;
using System.Collections.Generic;

namespace Ustas.RimAI.Communication.Relations.Prompting
{
    internal sealed class RelationsCoreVariableProvider : IPromptRuntimeVariableProvider
    {
        private static readonly IReadOnlyList<PromptRuntimeVariableDefinition> Definitions = BuildDefinitions();
        private readonly Func<string, PromptRuntimeVariableContext, object> _resolver;

        public RelationsCoreVariableProvider(Func<string, PromptRuntimeVariableContext, object> resolver)
        {
            _resolver = resolver;
        }

        public string SourceId => PromptCanonicalVariablePaths.CoreSourceId;
        public string SourceLabel => PromptCanonicalVariablePaths.CoreSourceLabel;
        public bool IsAvailable(PromptRuntimeVariableContext context) => true;
        public IReadOnlyList<PromptRuntimeVariableDefinition> GetDefinitions() => Definitions;
        public bool TryMapLegacyToken(string token, out string namespacedPath)
        {
            return PromptLegacyVariableMap.TryMap(token, out namespacedPath);
        }

        public void PopulateValues(IDictionary<string, object> values, PromptRuntimeVariableContext context)
        {
            if (values == null || _resolver == null)
            {
                return;
            }

            for (int i = 0; i < Definitions.Count; i++)
            {
                PromptRuntimeVariableDefinition definition = Definitions[i];
                object value = _resolver(definition.Path, context);
                if (value != null)
                {
                    values[definition.Path] = value;
                }
            }
        }

        private static IReadOnlyList<PromptRuntimeVariableDefinition> BuildDefinitions()
        {
            string[] paths = PromptCanonicalVariablePaths.All;
            var items = new List<PromptRuntimeVariableDefinition>(paths.Length);
            for (int i = 0; i < paths.Length; i++)
            {
                string key = PromptRuntimeVariableBridge.GetDescriptionKey(paths[i]);
                items.Add(new PromptRuntimeVariableDefinition(
                    paths[i],
                    PromptCanonicalVariablePaths.CoreSourceId,
                    PromptCanonicalVariablePaths.CoreSourceLabel,
                    key,
                    true));
            }

            return items;
        }
    }
}
