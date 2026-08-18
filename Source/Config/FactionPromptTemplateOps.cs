using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Persistence;

namespace Ustas.RimAI.Communication.Relations.Config;

internal sealed class FactionPromptTemplateOps
{
    internal readonly FactionPromptManager Owner;

    internal FactionPromptTemplateOps(FactionPromptManager owner)
    {
        Owner = owner;
    }


        internal FactionPromptConfig SeedInstanceConfigFromTemplate(
            Faction faction,
            string factionDefName,
            string instanceKey)
        {
            if (faction == null || string.IsNullOrWhiteSpace(instanceKey))
            {
                return null;
            }

            FactionPromptConfig source = null;
            if (!string.IsNullOrWhiteSpace(factionDefName))
            {
                source = Owner._configCollection?.GetConfig(factionDefName);
                if (source == null)
                {
                    Owner._defaultConfigLookup.TryGetValue(factionDefName, out source);
                }
            }

            FactionPromptConfig clone = source?.Clone() ?? new FactionPromptConfig();
            clone.FactionDefName = instanceKey;
            if (string.IsNullOrWhiteSpace(clone.DisplayName))
            {
                clone.DisplayName = faction.Name ?? factionDefName ?? instanceKey;
            }

            if (clone.TemplateFields == null || clone.TemplateFields.Count == 0)
            {
                Owner.SetupDefaultTemplateFields(clone, factionDefName);
            }

            return clone;
        }

        public bool TryAddTemplateForFaction(string factionDefName, string displayName, out string status)
        {
            if (!Owner._initialized) Owner.Initialize();

            status = "invalid";
            string normalized = factionDefName?.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            if (Owner._configCollection.GetConfig(normalized) != null)
            {
                status = "existing";
                return false;
            }

            FactionPromptConfig config;
            if (Owner._defaultConfigLookup.TryGetValue(normalized, out FactionPromptConfig defaultConfig))
            {
                config = defaultConfig.Clone();
            }
            else
            {
                FactionDef factionDef = DefDatabase<FactionDef>.GetNamedSilentFail(normalized);
                string resolvedDisplayName = !string.IsNullOrWhiteSpace(displayName)
                    ? displayName
                    : factionDef?.label ?? normalized;
                config = new FactionPromptConfig(normalized, resolvedDisplayName);
                Owner.SetupDefaultTemplateFields(config, normalized);
            }

            Owner._configCollection.SetConfig(config);
            Owner.SaveConfigs();
            status = "created";
            return true;
        }

        public bool TryRemoveTemplate(string factionDefName, out string reason)
        {
            if (!Owner._initialized) Owner.Initialize();

            reason = "invalid";
            string normalized = factionDefName?.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            if (Owner.IsDefaultTemplate(normalized))
            {
                reason = "default_protected";
                return false;
            }

            FactionPromptConfig existing = Owner._configCollection.GetConfig(normalized);
            if (existing == null)
            {
                reason = "not_found";
                return false;
            }

            Owner._configCollection.Configs.Remove(existing);
            Owner.SaveConfigs();
            reason = "removed";
            return true;
        }
}
