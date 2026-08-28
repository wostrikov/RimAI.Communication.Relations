using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Core.Storage;

namespace Ustas.RimAI.Communication.Relations.Config;

internal sealed class FactionPromptCatalogPersistence
{
    internal readonly FactionPromptManager Owner;

    internal FactionPromptCatalogPersistence(FactionPromptManager owner)
    {
        Owner = owner;
    }


        /// <summary>/// loadconfiguration
 ///</summary>
        internal void LoadConfigs()
        {
            string sourcePath = Owner.ConfigFilePath;
            if (!string.IsNullOrWhiteSpace(sourcePath) && LocalStorage.Current.FileExists(sourcePath))
            {
                try
                {
                    string json = LocalStorage.Current.ReadAllText(sourcePath);
                    Owner._configCollection = FactionPromptJsonUtility.FromJson(json);
                    Log.Message($"[RimAI.Relations] Loaded faction prompts from {sourcePath}");
                    
                    if (Owner._configCollection == null || Owner._configCollection.Configs.Count == 0)
                    {
                        Log.Warning($"[RimAI.Relations] Config file exists but contains no configs, loading defaults");
                        LoadDefaultConfigs();
                        SaveConfigs();
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[RimAI.Relations] Failed to load prompts from file: {ex}. Using defaults.");
                    LoadDefaultConfigs();
                }
            }
            else
            {
                Log.Message($"[RimAI.Relations] Prompt config file not found, loading defaults");
                LoadDefaultConfigs();
                SaveConfigs();
            }

            if (Owner._configCollection == null)
            {
                Owner._configCollection = new FactionPromptConfigCollection();
            }
        }

        /// <summary>/// saveconfiguration
 ///</summary>
        public void SaveConfigs()
        {
            try
            {
                if (Owner._configCollection == null) return;

                string directory = Path.GetDirectoryName(Owner.ConfigFilePath);
                if (!LocalStorage.Current.DirectoryExists(directory))
                {
                    LocalStorage.Current.CreateDirectory(directory);
                }

                string json = FactionPromptJsonUtility.ToJson(Owner._configCollection, true);
                LocalStorage.Current.WriteAllText(Owner.ConfigFilePath, json);
                Log.Message($"[RimAI.Relations] Saved faction prompts to {Owner.ConfigFilePath}");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAI.Relations] Failed to save prompts: {ex}");
            }
        }

        internal void LoadDefaultConfigs()
        {
            Owner._configCollection = new FactionPromptConfigCollection();
            foreach (var defName in Owner._defaultFactionDefNames.OrderBy(name => name))
            {
                if (Owner._defaultConfigLookup.TryGetValue(defName, out FactionPromptConfig config))
                {
                    Owner._configCollection.Configs.Add(config.Clone());
                }
            }

            if (Owner._configCollection.Configs.Count == 0)
            {
                Log.Warning("[RimAI.Relations] Default faction prompt catalog is empty. Using hardcoded fallback.");
                LoadHardcodedDefaultConfigs();
                BuildDefaultConfigCatalog();
            }
        }

        internal string GetDefaultConfigFilePath()
        {
            try
            {
                var mod = LoadedModManager.GetMod<RelationsMod>();
                if (mod?.Content != null)
                {
                    string defaultDir = Path.Combine(mod.Content.RootDir, FactionPromptManager.PromptFolderName, FactionPromptManager.DefaultSubFolderName);
                    string path = Path.Combine(defaultDir, FactionPromptManager.DefaultConfigFileName);
                    Log.Message($"[RimAI.Relations] Default config path from mod: {path}");
                    return path;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimAI.Relations] Failed to get mod path: {ex.Message}");
            }

            try
            {
                string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string assemblyDir = Path.GetDirectoryName(assemblyPath);
                string modDir = Directory.GetParent(assemblyDir)?.Parent?.FullName;
                if (!string.IsNullOrEmpty(modDir))
                {
                    string defaultDir = Path.Combine(modDir, FactionPromptManager.PromptFolderName, FactionPromptManager.DefaultSubFolderName);
                    string path = Path.Combine(defaultDir, FactionPromptManager.DefaultConfigFileName);
                    Log.Message($"[RimAI.Relations] Default config path from assembly parent: {path}");
                    return path;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimAI.Relations] Failed to get assembly path: {ex.Message}");
            }

            string fallbackPath = PromptDomainFileCatalog.GetDefaultPath(FactionPromptManager.DefaultConfigFileName);
            Log.Message($"[RimAI.Relations] Default config path from domain catalog: {fallbackPath}");
            return fallbackPath;
        }

        internal string GetCustomConfigFilePathInternal()
        {
            try
            {
                var mod = LoadedModManager.GetMod<RelationsMod>();
                if (mod?.Content != null)
                {
                    string customDir = Path.Combine(mod.Content.RootDir, FactionPromptManager.PromptFolderName, FactionPromptManager.CustomSubFolderName);
                    if (!LocalStorage.Current.DirectoryExists(customDir))
                    {
                        LocalStorage.Current.CreateDirectory(customDir);
                    }
                    return Path.Combine(customDir, FactionPromptManager.ConfigFileName);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimAI.Relations] Failed to get custom config path: {ex.Message}");
            }

            return Path.Combine(RelationsMod.Instance?.GetSettingsFolderPath() ?? "", FactionPromptManager.ConfigFileName);
        }

        internal void LoadHardcodedDefaultConfigs()
        {
            Owner._configCollection = new FactionPromptConfigCollection();

            foreach (var factionDef in GetSupportedFactionDefs())
            {
                var config = CreateDefaultConfig(factionDef);
                Owner._configCollection.Configs.Add(config);
            }
        }

        internal void EnsureAllFactionsHaveConfigs()
        {
            bool addedNew = false;

            foreach (var defName in Owner._defaultFactionDefNames)
            {
                if (Owner._configCollection.GetConfig(defName) != null)
                {
                    continue;
                }

                if (Owner._defaultConfigLookup.TryGetValue(defName, out FactionPromptConfig defaultConfig))
                {
                    Owner._configCollection.Configs.Add(defaultConfig.Clone());
                    addedNew = true;
                }
            }

            if (addedNew)
            {
                SaveConfigs();
            }
        }

        internal List<FactionDef> GetSupportedFactionDefs()
        {
            var supportedDefs = new List<FactionDef>();

            AddFactionDefIfExists(supportedDefs, "OutlanderCivil");
            AddFactionDefIfExists(supportedDefs, "OutlanderRough");
            AddFactionDefIfExists(supportedDefs, "TribeCivil");
            AddFactionDefIfExists(supportedDefs, "TribeRough");
            AddFactionDefIfExists(supportedDefs, "TribeSavage");
            AddFactionDefIfExists(supportedDefs, "Pirate");
            AddFactionDefIfExists(supportedDefs, "Mechanoid");
            AddFactionDefIfExists(supportedDefs, "Insect");
            AddFactionDefIfExists(supportedDefs, "HoraxCult");
            AddFactionDefIfExists(supportedDefs, "Entities");

            return supportedDefs;
        }

        internal void AddFactionDefIfExists(List<FactionDef> list, string defName)
        {
            var def = DefDatabase<FactionDef>.GetNamedSilentFail(defName);
            if (def != null)
            {
                list.Add(def);
            }
        }

        internal FactionPromptConfig CreateDefaultConfig(FactionDef factionDef)
        {
            var config = new FactionPromptConfig(factionDef.defName, factionDef.label);

            SetupDefaultTemplateFields(config, factionDef.defName);

            return config;
        }

        // Non-obvious edge case — read carefully before changing. (summary factionsettings template configuration FactionPrompts_Default.json file method file configuration summary)
        internal void SetupDefaultTemplateFields(FactionPromptConfig config, string factionDefName)
        {
            config.GetOrCreateField(FactionPromptFieldNames.CoreStyle, $"Завантаж типову конфігурацію {factionDefName} з файлу {FactionPromptManager.DefaultConfigFileName} або відредагуй цей шаблон вручну.", "Опиши основний стиль спілкування фракції");
            config.GetOrCreateField(FactionPromptFieldNames.Vocabulary, "Налаштуй особливості добору слів.", "Опиши звички й особливості добору слів");
            config.GetOrCreateField(FactionPromptFieldNames.Tone, "Налаштуй особливості тону.", "Опиши тон та емоційні риси");
            config.GetOrCreateField(FactionPromptFieldNames.Sentence, "Налаштуй особливості будови речень.", "Опиши особливості будови речень");
            config.GetOrCreateField(FactionPromptFieldNames.Taboos, "Налаштуй мовні табу.", "Опиши мовні табу й обмеження");
        }

        public bool ExportConfigs(string filePath)
        {
            try
            {
                if (Owner._configCollection == null) return false;

                string json = ExportConfigsToJson(prettyPrint: true);
                LocalStorage.Current.WriteAllText(filePath, json);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAI.Relations] Failed to export configs: {ex}");
                return false;
            }
        }

        public bool ImportConfigs(string filePath)
        {
            try
            {
                if (!LocalStorage.Current.FileExists(filePath)) return false;

                string json = LocalStorage.Current.ReadAllText(filePath);
                return ImportConfigsFromJson(json);
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAI.Relations] Failed to import configs: {ex}");
                return false;
            }
        }

        public string ExportConfigsToJson(bool prettyPrint = false)
        {
            if (!Owner._initialized) Owner.Initialize();
            if (Owner._configCollection == null)
            {
                return string.Empty;
            }

            return FactionPromptJsonUtility.ToJson(Owner._configCollection, prettyPrint);
        }

        public bool ImportConfigsFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            var imported = FactionPromptJsonUtility.FromJson(json);
            if (imported?.Configs == null || imported.Configs.Count == 0)
            {
                return false;
            }

            Owner._configCollection = imported;
            EnsureAllFactionsHaveConfigs();
            SaveConfigs();
            return true;
        }

        internal void BuildDefaultConfigCatalog()
        {
            Owner._defaultFactionDefNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Owner._defaultConfigLookup = new Dictionary<string, FactionPromptConfig>(StringComparer.OrdinalIgnoreCase);

            string defaultConfigPath = GetDefaultConfigFilePath();
            Log.Message($"[RimAI.Relations] Looking for default config at: {defaultConfigPath}");
            if (!string.IsNullOrWhiteSpace(defaultConfigPath) && LocalStorage.Current.FileExists(defaultConfigPath))
            {
                try
                {
                    string json = LocalStorage.Current.ReadAllText(defaultConfigPath);
                    FactionPromptConfigCollection collection = FactionPromptJsonUtility.FromJson(json);
                    if (TryPopulateDefaultCatalog(collection))
                    {
                        Log.Message($"[RimAI.Relations] Loaded default faction prompt catalog ({Owner._defaultFactionDefNames.Count})");
                        return;
                    }

                    Log.Warning("[RimAI.Relations] Default prompt file parsed but contains no valid configs.");
                }
                catch (Exception ex)
                {
                    Log.Warning($"[RimAI.Relations] Failed to parse default prompt file: {ex}");
                }
            }

            BuildHardcodedDefaultCatalog();
        }

        internal bool TryPopulateDefaultCatalog(FactionPromptConfigCollection collection)
        {
            if (collection?.Configs == null || collection.Configs.Count == 0)
            {
                return false;
            }

            foreach (FactionPromptConfig config in collection.Configs)
            {
                string defName = config?.FactionDefName?.Trim();
                if (string.IsNullOrWhiteSpace(defName))
                {
                    continue;
                }

                Owner._defaultFactionDefNames.Add(defName);
                Owner._defaultConfigLookup[defName] = config.Clone();
            }

            return Owner._defaultFactionDefNames.Count > 0;
        }

        internal void BuildHardcodedDefaultCatalog()
        {
            Log.Message("[RimAI.Relations] Using hardcoded fallback for faction prompt default catalog.");

            foreach (FactionDef factionDef in GetSupportedFactionDefs())
            {
                if (factionDef == null || string.IsNullOrWhiteSpace(factionDef.defName))
                {
                    continue;
                }

                FactionPromptConfig config = CreateDefaultConfig(factionDef);
                Owner._defaultFactionDefNames.Add(factionDef.defName);
                Owner._defaultConfigLookup[factionDef.defName] = config;
            }
        }
}
