using Ustas.RimAI.Communication.Relations.Config;

namespace Ustas.RimAI.Communication.Relations.Persistence
{
    /// <summary>
    /// Storage-only contract for the Relations prompt configuration domain files.
    /// Prompt composition lives behind <see cref="Prompting.IRelationsPromptBuilder"/>.
    /// </summary>
    public interface IPromptConfigStore
    {
        SystemPromptConfig LoadConfig();
        SystemPromptConfig LoadConfigReadOnly();
        bool RepairAndRewritePromptDomains();
        void SaveConfig(SystemPromptConfig config);
        bool ConfigExists();
        void ResetToDefault();
        string GetConfigFilePath();
        bool ExportConfig(string filePath);
        bool ImportConfig(string filePath);
    }
}
