using System.Collections.Generic;
using Ustas.RimAI.Communication.Relations.Serialization;

namespace Ustas.RimAI.Communication.Relations.Prompting.Transfer
{
    /// <summary>
    /// Dependencies: none.
    /// Responsibility: classify prompt-bundle import failures without touching live config.
    /// </summary>
    internal enum PromptBundleImportFailure
    {
        None = 0,
        EmptyPath,
        FileNotFound,
        EmptyFile,
        InvalidJson,
        PresetFileDetected,
        NotPromptBundle,
        InvalidBundlePayload,
        NoModuleOverlap,
        UnexpectedException
    }

    internal static class PromptBundleImportErrorCodes
    {
        public const string EmptyPath = "PBIMP_001_EMPTY_PATH";
        public const string FileNotFound = "PBIMP_002_FILE_NOT_FOUND";
        public const string EmptyFile = "PBIMP_003_EMPTY_FILE";
        public const string InvalidJson = "PBIMP_004_INVALID_JSON";
        public const string PresetFileDetected = "PBIMP_005_PRESET_FILE";
        public const string NotPromptBundle = "PBIMP_006_NOT_BUNDLE";
        public const string InvalidBundlePayload = "PBIMP_007_INVALID_BUNDLE_PAYLOAD";
        public const string NoModuleOverlap = "PBIMP_008_NO_MODULE_OVERLAP";
        public const string UnexpectedException = "PBIMP_999_UNEXPECTED";
    }

    /// <summary>
    /// Dependencies: PromptJsonText.
    /// Responsibility: validate prompt-bundle transfer envelope before any config mutation.
    /// </summary>
    internal static class PromptBundleEnvelope
    {
        public static readonly string[] PayloadMarkers =
        {
            "SystemPrompt",
            "SystemPromptJson",
            "DiplomacyDialoguePrompt",
            "DiplomacyDialoguePromptJson",
            "PawnDialoguePrompt",
            "PawnDialoguePromptJson",
            "SocialCirclePrompt",
            "SocialCirclePromptJson",
            "FactionPromptsJson",
            "PromptSectionCatalog",
            "PromptSectionCatalogJson",
            "UnifiedPromptCatalog",
            "UnifiedPromptCatalogJson"
        };

        public static readonly string[] PresetFeatureKeys =
        {
            "Presets",
            "ChannelPayloads",
            "UnifiedPromptCatalog"
        };

        public static bool TryValidate(
            string json,
            out PromptBundleImportFailure failure,
            out string errorCode)
        {
            failure = PromptBundleImportFailure.None;
            errorCode = string.Empty;
            if (string.IsNullOrWhiteSpace(json))
            {
                failure = PromptBundleImportFailure.EmptyFile;
                errorCode = PromptBundleImportErrorCodes.EmptyFile;
                return false;
            }

            if (!PromptJsonText.LooksLikeJsonObject(json))
            {
                failure = PromptBundleImportFailure.InvalidJson;
                errorCode = PromptBundleImportErrorCodes.InvalidJson;
                return false;
            }

            if (PromptJsonText.ContainsAnyJsonKey(json, PresetFeatureKeys))
            {
                failure = PromptBundleImportFailure.PresetFileDetected;
                errorCode = PromptBundleImportErrorCodes.PresetFileDetected;
                return false;
            }

            bool hasVersion = PromptJsonText.ContainsJsonKey(json, "BundleVersion");
            bool hasModules = PromptJsonText.ContainsJsonKey(json, "IncludedModules");
            bool hasPayload = PromptJsonText.ContainsAnyJsonKey(json, PayloadMarkers);
            if (!hasVersion || !hasModules || !hasPayload)
            {
                failure = PromptBundleImportFailure.NotPromptBundle;
                errorCode = PromptBundleImportErrorCodes.NotPromptBundle;
                return false;
            }

            return true;
        }
    }
}
