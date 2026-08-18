using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Persistence;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Serialization;

namespace Ustas.RimAI.Communication.Relations.Config
{
        internal abstract class PromptPresetServiceCollaborator
    {
        internal readonly PromptPresetService Owner;

        protected PromptPresetServiceCollaborator(PromptPresetService owner)
        {
            Owner = owner;
        }
        protected PromptPresetServiceParts Parts => Owner.Parts;


        protected const int CurrentSchemaVersion = 2;
        protected const int LegacyRpgNodeMigrationVersion = 2;
        protected const int LegacySocialNewsNodeMigrationVersion = 3;
        protected const string ImmutableDefaultPresetId = "rimchat_default_preset";
        protected const string ImmutableDefaultPresetName = "Default";
        protected const string PresetStoreFileName = "PromptPresets_Custom.json";
        protected const string CorruptStoreFileSuffix = ".corrupt";
    }

}
