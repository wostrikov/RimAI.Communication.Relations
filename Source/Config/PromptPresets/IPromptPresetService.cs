using System.Collections.Generic;

namespace Ustas.RimAI.Communication.Relations.Config
{
    internal interface IPromptPresetService
    {
        PromptPresetStoreConfig LoadAll(RelationsSettings settings);
        void SaveAll(PromptPresetStoreConfig store);
        PromptPresetConfig CreateFromLegacy(RelationsSettings settings, string name);
        PromptPresetConfig Duplicate(RelationsSettings settings, PromptPresetConfig source, string name);
        bool Activate(RelationsSettings settings, PromptPresetStoreConfig store, string presetId, out string error);
        bool IsDefaultPreset(PromptPresetStoreConfig store, string presetId);
        bool EnsureEditablePresetForMutation(
            RelationsSettings settings,
            PromptPresetStoreConfig store,
            string selectedPresetId,
            string forkNamePrefix,
            out PromptPresetConfig editablePreset,
            out bool forked,
            out string error);
        bool SyncPresetPayloadFromSettings(
            RelationsSettings settings,
            PromptPresetStoreConfig store,
            string presetId,
            out string error);
        void ApplyPayloadToSettings(RelationsSettings settings, PromptPresetChannelPayloads payload, bool persistToFiles);
        bool ExportPreset(string filePath, PromptPresetConfig preset, out string error);
        bool ImportPreset(string filePath, PromptPresetStoreConfig store, out PromptPresetConfig imported, out string error);
        List<PromptPresetSummary> BuildSummaries(PromptPresetStoreConfig store);
    }
}
