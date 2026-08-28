using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Verse;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Module;
using UnityEngine;
using Ustas.RimAI.Core.Storage;

namespace Ustas.RimAI.Communication.Relations.Config
{
    [Serializable]
    public class ApiActionConfig : IExposable
    {
        public string ActionName;
        public string Description;
        public string Parameters;
        public string Requirement;
        public bool IsEnabled;

        public ApiActionConfig()
        {
            IsEnabled = true;
        }

        public ApiActionConfig(string actionName, string description, string parameters = "", string requirement = "")
        {
            ActionName = actionName;
            Description = description;
            Parameters = parameters;
            Requirement = requirement;
            IsEnabled = true;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref ActionName, "actionName", "");
            Scribe_Values.Look(ref Description, "description", "");
            Scribe_Values.Look(ref Parameters, "parameters", "");
            Scribe_Values.Look(ref Requirement, "requirement", "");
            Scribe_Values.Look(ref IsEnabled, "isEnabled", true);
        }

        public ApiActionConfig Clone()
        {
            return new ApiActionConfig
            {
                ActionName = this.ActionName,
                Description = this.Description,
                Parameters = this.Parameters,
                Requirement = this.Requirement,
                IsEnabled = this.IsEnabled
            };
        }
    }

    [Serializable]
    public class ResponseFormatConfig : IExposable
    {
        public string JsonTemplate;
        public string ImportantRules;

        public void ExposeData()
        {
            Scribe_Values.Look(ref JsonTemplate, "jsonTemplate", "");
            Scribe_Values.Look(ref ImportantRules, "importantRules", "");
        }

        public ResponseFormatConfig Clone()
        {
            return new ResponseFormatConfig
            {
                JsonTemplate = this.JsonTemplate,
                ImportantRules = this.ImportantRules
            };
        }
    }

    [Serializable]
    public class DecisionRuleConfig : IExposable
    {
        public string RuleName;
        public string RuleContent;
        public bool IsEnabled;

        public DecisionRuleConfig()
        {
            IsEnabled = true;
        }

        public DecisionRuleConfig(string ruleName, string ruleContent)
        {
            RuleName = ruleName;
            RuleContent = ruleContent;
            IsEnabled = true;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref RuleName, "ruleName", "");
            Scribe_Values.Look(ref RuleContent, "ruleContent", "");
            Scribe_Values.Look(ref IsEnabled, "isEnabled", true);
        }

        public DecisionRuleConfig Clone()
        {
            return new DecisionRuleConfig
            {
                RuleName = this.RuleName,
                RuleContent = this.RuleContent,
                IsEnabled = this.IsEnabled
            };
        }
    }

    [Serializable]
    public class DynamicDataInjectionConfig : IExposable
    {
        public bool InjectMemoryData;
        public bool InjectFactionInfo;
        public string CustomInjectionHeader;

        public DynamicDataInjectionConfig()
        {
            InjectMemoryData = true;
            InjectFactionInfo = true;
            CustomInjectionHeader = "";
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref InjectMemoryData, "injectMemoryData", true);
            Scribe_Values.Look(ref InjectFactionInfo, "injectFactionInfo", true);
            Scribe_Values.Look(ref CustomInjectionHeader, "customInjectionHeader", "");
        }

        public DynamicDataInjectionConfig Clone()
        {
            return new DynamicDataInjectionConfig
            {
                InjectMemoryData = this.InjectMemoryData,
                InjectFactionInfo = this.InjectFactionInfo,
                CustomInjectionHeader = this.CustomInjectionHeader
            };
        }
    }

    [Serializable]
    public class WorldviewPromptConfig : IExposable
    {
        public bool Enabled;
        public string Content;

        public WorldviewPromptConfig()
        {
            Enabled = true;
            Content = string.Empty;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Enabled, "enabled", true);
            Scribe_Values.Look(ref Content, "content", string.Empty);
        }

        public WorldviewPromptConfig Clone()
        {
            return new WorldviewPromptConfig
            {
                Enabled = this.Enabled,
                Content = this.Content
            };
        }
    }

    [Serializable]
    public class SceneSystemPromptConfig : IExposable
    {
        public bool Enabled;
        public int MaxSceneChars;
        public int MaxTotalChars;
        public bool PresetTagsEnabled;

        public SceneSystemPromptConfig()
        {
            Enabled = true;
            MaxSceneChars = 1200;
            MaxTotalChars = 4000;
            PresetTagsEnabled = true;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Enabled, "enabled", true);
            Scribe_Values.Look(ref MaxSceneChars, "maxSceneChars", 1200);
            Scribe_Values.Look(ref MaxTotalChars, "maxTotalChars", 4000);
            Scribe_Values.Look(ref PresetTagsEnabled, "presetTagsEnabled", true);
        }

        public SceneSystemPromptConfig Clone()
        {
            return new SceneSystemPromptConfig
            {
                Enabled = this.Enabled,
                MaxSceneChars = this.MaxSceneChars,
                MaxTotalChars = this.MaxTotalChars,
                PresetTagsEnabled = this.PresetTagsEnabled
            };
        }
    }

    [Serializable]
    public class ScenePromptEntryConfig : IExposable
    {
        public string Id;
        public string Name;
        public bool Enabled;
        public bool ApplyToDiplomacy;
        public bool ApplyToRPG;
        public int Priority;
        public List<string> MatchTags;
        public string Content;

        public ScenePromptEntryConfig()
        {
            Id = Guid.NewGuid().ToString("N");
            Name = string.Empty;
            Enabled = true;
            ApplyToDiplomacy = true;
            ApplyToRPG = true;
            Priority = 0;
            MatchTags = new List<string>();
            Content = string.Empty;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Id, "id", string.Empty);
            Scribe_Values.Look(ref Name, "name", string.Empty);
            Scribe_Values.Look(ref Enabled, "enabled", true);
            Scribe_Values.Look(ref ApplyToDiplomacy, "applyToDiplomacy", true);
            Scribe_Values.Look(ref ApplyToRPG, "applyToRPG", true);
            Scribe_Values.Look(ref Priority, "priority", 0);
            Scribe_Collections.Look(ref MatchTags, "matchTags", LookMode.Value);
            Scribe_Values.Look(ref Content, "content", string.Empty);
            if (MatchTags == null)
            {
                MatchTags = new List<string>();
            }
            if (string.IsNullOrEmpty(Id))
            {
                Id = Guid.NewGuid().ToString("N");
            }
        }

        public ScenePromptEntryConfig Clone()
        {
            return new ScenePromptEntryConfig
            {
                Id = this.Id,
                Name = this.Name,
                Enabled = this.Enabled,
                ApplyToDiplomacy = this.ApplyToDiplomacy,
                ApplyToRPG = this.ApplyToRPG,
                Priority = this.Priority,
                MatchTags = this.MatchTags != null ? new List<string>(this.MatchTags) : new List<string>(),
                Content = this.Content
            };
        }
    }

    [Serializable]
    public class RpgSceneParamSwitchesConfig : IExposable
    {
        public bool IncludeSkills;
        public bool IncludeEquipment;
        public bool IncludeGenes;
        public bool IncludeNeeds;
        public bool IncludeHediffs;
        public bool IncludeRecentEvents;
        public bool IncludeColonyInventorySummary;
        public bool IncludeHomeAlerts;
        public bool IncludeRecentJobState;
        public bool IncludeAttributeLevels;

        public RpgSceneParamSwitchesConfig()
        {
            IncludeSkills = true;
            IncludeEquipment = true;
            IncludeGenes = true;
            IncludeNeeds = true;
            IncludeHediffs = true;
            IncludeRecentEvents = true;
            IncludeColonyInventorySummary = true;
            IncludeHomeAlerts = true;
            IncludeRecentJobState = true;
            IncludeAttributeLevels = true;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref IncludeSkills, "includeSkills", true);
            Scribe_Values.Look(ref IncludeEquipment, "includeEquipment", true);
            Scribe_Values.Look(ref IncludeGenes, "includeGenes", true);
            Scribe_Values.Look(ref IncludeNeeds, "includeNeeds", true);
            Scribe_Values.Look(ref IncludeHediffs, "includeHediffs", true);
            Scribe_Values.Look(ref IncludeRecentEvents, "includeRecentEvents", true);
            Scribe_Values.Look(ref IncludeColonyInventorySummary, "includeColonyInventorySummary", true);
            Scribe_Values.Look(ref IncludeHomeAlerts, "includeHomeAlerts", true);
            Scribe_Values.Look(ref IncludeRecentJobState, "includeRecentJobState", true);
            Scribe_Values.Look(ref IncludeAttributeLevels, "includeAttributeLevels", true);
        }

        public RpgSceneParamSwitchesConfig Clone()
        {
            return new RpgSceneParamSwitchesConfig
            {
                IncludeSkills = this.IncludeSkills,
                IncludeEquipment = this.IncludeEquipment,
                IncludeGenes = this.IncludeGenes,
                IncludeNeeds = this.IncludeNeeds,
                IncludeHediffs = this.IncludeHediffs,
                IncludeRecentEvents = this.IncludeRecentEvents,
                IncludeColonyInventorySummary = this.IncludeColonyInventorySummary,
                IncludeHomeAlerts = this.IncludeHomeAlerts,
                IncludeRecentJobState = this.IncludeRecentJobState,
                IncludeAttributeLevels = this.IncludeAttributeLevels
            };
        }
    }

    [Serializable]
    public class EnvironmentContextSwitchesConfig : IExposable
    {
        public bool Enabled;
        public bool IncludeTime;
        public bool IncludeDate;
        public bool IncludeSeason;
        public bool IncludeWeather;
        public bool IncludeLocationAndTemperature;
        public bool IncludeTerrain;
        public bool IncludeBeauty;
        public bool IncludeCleanliness;
        public bool IncludeSurroundings;
        public bool IncludeWealth;

        public EnvironmentContextSwitchesConfig()
        {
            Enabled = true;
            IncludeTime = true;
            IncludeDate = false;
            IncludeSeason = true;
            IncludeWeather = true;
            IncludeLocationAndTemperature = true;
            IncludeTerrain = false;
            IncludeBeauty = false;
            IncludeCleanliness = false;
            IncludeSurroundings = false;
            IncludeWealth = false;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Enabled, "enabled", true);
            Scribe_Values.Look(ref IncludeTime, "includeTime", true);
            Scribe_Values.Look(ref IncludeDate, "includeDate", false);
            Scribe_Values.Look(ref IncludeSeason, "includeSeason", true);
            Scribe_Values.Look(ref IncludeWeather, "includeWeather", true);
            Scribe_Values.Look(ref IncludeLocationAndTemperature, "includeLocationAndTemperature", true);
            Scribe_Values.Look(ref IncludeTerrain, "includeTerrain", false);
            Scribe_Values.Look(ref IncludeBeauty, "includeBeauty", false);
            Scribe_Values.Look(ref IncludeCleanliness, "includeCleanliness", false);
            Scribe_Values.Look(ref IncludeSurroundings, "includeSurroundings", false);
            Scribe_Values.Look(ref IncludeWealth, "includeWealth", false);
        }

        public EnvironmentContextSwitchesConfig Clone()
        {
            return new EnvironmentContextSwitchesConfig
            {
                Enabled = this.Enabled,
                IncludeTime = this.IncludeTime,
                IncludeDate = this.IncludeDate,
                IncludeSeason = this.IncludeSeason,
                IncludeWeather = this.IncludeWeather,
                IncludeLocationAndTemperature = this.IncludeLocationAndTemperature,
                IncludeTerrain = this.IncludeTerrain,
                IncludeBeauty = this.IncludeBeauty,
                IncludeCleanliness = this.IncludeCleanliness,
                IncludeSurroundings = this.IncludeSurroundings,
                IncludeWealth = this.IncludeWealth
            };
        }
    }

    [Serializable]
    public class EnvironmentPromptConfig : IExposable
    {
        public WorldviewPromptConfig Worldview;
        public SceneSystemPromptConfig SceneSystem;
        public List<ScenePromptEntryConfig> SceneEntries;
        public EnvironmentContextSwitchesConfig EnvironmentContextSwitches;
        public RpgSceneParamSwitchesConfig RpgSceneParamSwitches;
        public EventIntelPromptConfig EventIntelPrompt;

        public EnvironmentPromptConfig()
        {
            Worldview = new WorldviewPromptConfig();
            SceneSystem = new SceneSystemPromptConfig();
            SceneEntries = new List<ScenePromptEntryConfig>();
            EnvironmentContextSwitches = new EnvironmentContextSwitchesConfig();
            RpgSceneParamSwitches = new RpgSceneParamSwitchesConfig();
            EventIntelPrompt = new EventIntelPromptConfig();
        }

        public void ExposeData()
        {
            Scribe_Deep.Look(ref Worldview, "worldview");
            Scribe_Deep.Look(ref SceneSystem, "sceneSystem");
            Scribe_Collections.Look(ref SceneEntries, "sceneEntries", LookMode.Deep);
            Scribe_Deep.Look(ref EnvironmentContextSwitches, "environmentContextSwitches");
            Scribe_Deep.Look(ref RpgSceneParamSwitches, "rpgSceneParamSwitches");
            Scribe_Deep.Look(ref EventIntelPrompt, "eventIntelPrompt");

            if (Worldview == null) Worldview = new WorldviewPromptConfig();
            if (SceneSystem == null) SceneSystem = new SceneSystemPromptConfig();
            if (SceneEntries == null) SceneEntries = new List<ScenePromptEntryConfig>();
            if (EnvironmentContextSwitches == null) EnvironmentContextSwitches = new EnvironmentContextSwitchesConfig();
            if (RpgSceneParamSwitches == null) RpgSceneParamSwitches = new RpgSceneParamSwitchesConfig();
            if (EventIntelPrompt == null) EventIntelPrompt = new EventIntelPromptConfig();
        }

        public EnvironmentPromptConfig Clone()
        {
            var clone = new EnvironmentPromptConfig
            {
                Worldview = this.Worldview?.Clone() ?? new WorldviewPromptConfig(),
                SceneSystem = this.SceneSystem?.Clone() ?? new SceneSystemPromptConfig(),
                EnvironmentContextSwitches = this.EnvironmentContextSwitches?.Clone() ?? new EnvironmentContextSwitchesConfig(),
                RpgSceneParamSwitches = this.RpgSceneParamSwitches?.Clone() ?? new RpgSceneParamSwitchesConfig(),
                EventIntelPrompt = this.EventIntelPrompt?.Clone() ?? new EventIntelPromptConfig(),
                SceneEntries = new List<ScenePromptEntryConfig>()
            };

            if (this.SceneEntries != null)
            {
                foreach (var entry in this.SceneEntries)
                {
                    if (entry != null)
                    {
                        clone.SceneEntries.Add(entry.Clone());
                    }
                }
            }

            return clone;
        }

        public static EnvironmentPromptConfig CreateDefaultSeed()
        {
            var config = new EnvironmentPromptConfig();
            config.Worldview.Enabled = true;
            config.Worldview.Content = string.Empty;
            config.SceneSystem.Enabled = true;
            config.SceneSystem.MaxSceneChars = 1200;
            config.SceneSystem.MaxTotalChars = 4000;
            config.SceneSystem.PresetTagsEnabled = true;
            config.EventIntelPrompt = new EventIntelPromptConfig
            {
                Enabled = true,
                ApplyToDiplomacy = true,
                ApplyToRpg = true,
                IncludeMapEvents = true,
                IncludeRaidBattleReports = true,
                DaysWindow = 15,
                MaxStoredRecords = 50,
                MaxInjectedItems = 8,
                MaxInjectedChars = 1200
            };

            config.SceneEntries = new List<ScenePromptEntryConfig>
            {
                CreateSeedEntry(
                    "Дипломатія — соціальний контакт",
                    30,
                    true,
                    false,
                    "Перший контакт із фракцією або звичайне вітання: наголос на ввічливості, обміні відомостями та промацуванні меж.",
                    "channel:diplomacy",
                    "scene:social"),
                CreateSeedEntry(
                    "Дипломатія — узгодження завдань",
                    60,
                    true,
                    false,
                    "Зосередься на меті завдання, умовах, ризиках і винагороді; уникай порожніх обіцянок.",
                    "channel:diplomacy",
                    "scene:task"),
                CreateSeedEntry(
                    "Дипломатія — погрози й протистояння",
                    90,
                    true,
                    false,
                    "Перехід до жорстких переговорів і гри на залякування: мова твердіша, позиція послідовна.",
                    "channel:diplomacy",
                    "scene:threat"),
                CreateSeedEntry(
                    "RPG — щоденна взаємодія",
                    30,
                    false,
                    true,
                    "Веди побутову, рольову розмову; тримай розмовний тон та індивідуальність.",
                    "channel:rpg",
                    "scene:daily"),
                CreateSeedEntry(
                    "RPG — близькі стосунки",
                    70,
                    false,
                    true,
                    "Наголос на емоційній напрузі, довірі та змінах у прив'язаності; уникай механічних формулювань.",
                    "channel:rpg",
                    "scene:intimacy"),
                CreateSeedEntry(
                    "RPG — конфліктна розмова",
                    85,
                    false,
                    true,
                    "Опрацювання суперечки, провокації чи відмови: мотиви персонажа й наслідки лишаються послідовними.",
                    "channel:rpg",
                    "scene:conflict")
            };

            return config;
        }

        private static ScenePromptEntryConfig CreateSeedEntry(
            string name,
            int priority,
            bool diplomacy,
            bool rpg,
            string content,
            params string[] tags)
        {
            return new ScenePromptEntryConfig
            {
                Name = name,
                Priority = priority,
                ApplyToDiplomacy = diplomacy,
                ApplyToRPG = rpg,
                Content = content,
                MatchTags = tags != null ? new List<string>(tags) : new List<string>()
            };
        }
    }

    [Serializable]
    public class SystemPromptConfig : IExposable
    {
        public const int CurrentPromptSchemaVersion = 3;
        public const int CurrentPromptPolicySchemaVersion = 4;
        public const string PlaceholderGlobalSystemPrompt =
            "Завантаж типову конфігурацію системного промпту з файлу SystemPrompt_Default.json.";

        [Serializable]
        private sealed class DefaultPromptHeaderPayload
        {
            public string GlobalSystemPrompt = string.Empty;
        }

        public string ConfigName;
        public string GlobalSystemPrompt;
        public string GlobalDialoguePrompt;
        public bool UseAdvancedMode;
        public bool UseHierarchicalPromptFormat;

        public List<ApiActionConfig> ApiActions;
        public ResponseFormatConfig ResponseFormat;
        public List<DecisionRuleConfig> DecisionRules;
        public EnvironmentPromptConfig EnvironmentPrompt;
        public DynamicDataInjectionConfig DynamicDataInjection;
        public PromptTemplateTextConfig PromptTemplates;
        public int PromptSchemaVersion;
        public int PromptPolicySchemaVersion;
        public PromptPolicyConfig PromptPolicy;

        public bool Enabled;

        public SystemPromptConfig()
        {
            ConfigName = "Default";
            GlobalSystemPrompt = "";
            GlobalDialoguePrompt = "";
            UseAdvancedMode = false;
            UseHierarchicalPromptFormat = true;
            Enabled = true;
            ApiActions = new List<ApiActionConfig>();
            ResponseFormat = new ResponseFormatConfig();
            DecisionRules = new List<DecisionRuleConfig>();
            EnvironmentPrompt = new EnvironmentPromptConfig();
            DynamicDataInjection = new DynamicDataInjectionConfig();
            PromptTemplates = new PromptTemplateTextConfig();
            PromptSchemaVersion = CurrentPromptSchemaVersion;
            PromptPolicySchemaVersion = CurrentPromptPolicySchemaVersion;
            PromptPolicy = PromptPolicyConfig.CreateDefault();
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref ConfigName, "configName", "Default");
            Scribe_Values.Look(ref GlobalSystemPrompt, "globalSystemPrompt", "");
            Scribe_Values.Look(ref GlobalDialoguePrompt, "globalDialoguePrompt", "");
            Scribe_Values.Look(ref UseAdvancedMode, "useAdvancedMode", false);
            Scribe_Values.Look(ref UseHierarchicalPromptFormat, "useHierarchicalPromptFormat", true);
            Scribe_Values.Look(ref Enabled, "enabled", true);
            Scribe_Collections.Look(ref ApiActions, "apiActions", LookMode.Deep);
            Scribe_Deep.Look(ref ResponseFormat, "responseFormat");
            Scribe_Collections.Look(ref DecisionRules, "decisionRules", LookMode.Deep);
            Scribe_Deep.Look(ref EnvironmentPrompt, "environmentPrompt");
            Scribe_Deep.Look(ref DynamicDataInjection, "dynamicDataInjection");
            Scribe_Deep.Look(ref PromptTemplates, "promptTemplates");
            Scribe_Values.Look(ref PromptSchemaVersion, "promptSchemaVersion", CurrentPromptSchemaVersion);
            Scribe_Values.Look(ref PromptPolicySchemaVersion, "promptPolicySchemaVersion", CurrentPromptPolicySchemaVersion);
            Scribe_Deep.Look(ref PromptPolicy, "promptPolicy");
            if (EnvironmentPrompt == null)
            {
                EnvironmentPrompt = new EnvironmentPromptConfig();
            }

            if (PromptTemplates == null)
            {
                PromptTemplates = new PromptTemplateTextConfig();
            }

            if (PromptPolicy == null)
            {
                PromptPolicy = PromptPolicyConfig.CreateDefault();
            }

            if (PromptPolicySchemaVersion <= 0)
            {
                PromptPolicySchemaVersion = CurrentPromptPolicySchemaVersion;
            }

            if (PromptSchemaVersion <= 0)
            {
                PromptSchemaVersion = CurrentPromptSchemaVersion;
            }
        }

        public SystemPromptConfig Clone()
        {
            var clone = new SystemPromptConfig
            {
                ConfigName = this.ConfigName,
                GlobalSystemPrompt = this.GlobalSystemPrompt,
                GlobalDialoguePrompt = this.GlobalDialoguePrompt,
                UseAdvancedMode = this.UseAdvancedMode,
                UseHierarchicalPromptFormat = this.UseHierarchicalPromptFormat,
                Enabled = this.Enabled,
                ResponseFormat = this.ResponseFormat?.Clone() ?? new ResponseFormatConfig(),
                EnvironmentPrompt = this.EnvironmentPrompt?.Clone() ?? new EnvironmentPromptConfig(),
                DynamicDataInjection = this.DynamicDataInjection?.Clone() ?? new DynamicDataInjectionConfig(),
                PromptTemplates = this.PromptTemplates?.Clone() ?? new PromptTemplateTextConfig(),
                PromptSchemaVersion = this.PromptSchemaVersion,
                PromptPolicySchemaVersion = this.PromptPolicySchemaVersion,
                PromptPolicy = this.PromptPolicy?.Clone() ?? PromptPolicyConfig.CreateDefault()
            };

            foreach (var action in ApiActions)
            {
                clone.ApiActions.Add(action.Clone());
            }

            foreach (var rule in DecisionRules)
            {
                clone.DecisionRules.Add(rule.Clone());
            }

            return clone;
        }

        public void InitializeDefaults()
        {
            var defaultConfig = LoadDefaultConfigFromFile();
            if (IsDefaultConfigUsable(defaultConfig))
            {
                CopyFrom(defaultConfig);
                return;
            }

            if (defaultConfig != null)
            {
                Log.Warning("[RimAI.Relations] Default system prompt file parsed but critical sections are missing; fallback to minimal defaults.");
            }

            InitializeMinimalDefaults();
        }

        private SystemPromptConfig LoadDefaultConfigFromFile()
        {
            try
            {
                string defaultConfigPath = GetDefaultConfigPath();
                if (LocalStorage.Current.FileExists(defaultConfigPath))
                {
                    string json = LocalStorage.Current.ReadAllText(defaultConfigPath);
                    var config = PromptPersistenceService.Instance?.ParseJsonToConfigInternal(
                        json,
                        $"default_system_prompt_file:{defaultConfigPath}");
                    if (config != null)
                    {
                        Log.Message($"[RimAI.Relations] Loaded default system prompt from {defaultConfigPath}");
                        return config;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimAI.Relations] Failed to load default system prompt from file: {ex.Message}");
            }
            return null;
        }

        private static bool IsDefaultConfigUsable(SystemPromptConfig config)
        {
            if (config == null)
            {
                return false;
            }

            bool hasActions = config.ApiActions != null && config.ApiActions.Count > 0;
            bool hasDecisionRules = config.DecisionRules != null && config.DecisionRules.Count > 0;
            bool hasResponseFormat = config.ResponseFormat != null;
            bool hasJsonTemplate = !string.IsNullOrWhiteSpace(config.ResponseFormat?.JsonTemplate);
            bool hasImportantRules = !string.IsNullOrWhiteSpace(config.ResponseFormat?.ImportantRules);
            bool hasPromptTemplates = config.PromptTemplates != null;
            bool hasPromptPolicy = config.PromptPolicy != null;
            return hasActions && hasDecisionRules && hasResponseFormat && hasJsonTemplate && hasImportantRules && hasPromptTemplates && hasPromptPolicy;
        }

        /// <summary>/// Promptfoldername
 ///</summary>
        public const string PromptFolderName = "Prompt";

        public const string DefaultSubFolderName = "Default";

        public const string CustomSubFolderName = "Custom";

        public const string DefaultConfigFileName = "SystemPrompt_Default.json";
        private const string DefaultDiplomacyPromptFileName = "DiplomacyDialoguePrompt_Default.json";

        private string GetDefaultConfigPath()
        {
            return GetDefaultPromptResourcePath(DefaultConfigFileName);
        }

        private string GetDefaultPromptResourcePath(string fileName)
        {
            try
            {
                var mod = LoadedModManager.GetMod<RelationsMod>();
                if (mod?.Content != null)
                {
                    string defaultDir = System.IO.Path.Combine(mod.Content.RootDir, PromptFolderName, DefaultSubFolderName);
                    return System.IO.Path.Combine(defaultDir, fileName);
                }
            }
            catch { }

            try
            {
                string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string assemblyDir = System.IO.Path.GetDirectoryName(assemblyPath);
                string modDir = System.IO.Directory.GetParent(assemblyDir)?.Parent?.FullName;
                if (!string.IsNullOrEmpty(modDir))
                {
                    string defaultDir = System.IO.Path.Combine(modDir, PromptFolderName, DefaultSubFolderName);
                    return System.IO.Path.Combine(defaultDir, fileName);
                }
            }
            catch { }

            return System.IO.Path.Combine(PromptFolderName, DefaultSubFolderName, fileName);
        }

        private void CopyFrom(SystemPromptConfig source)
        {
            ConfigName = source.ConfigName;
            GlobalSystemPrompt = source.GlobalSystemPrompt;
            GlobalDialoguePrompt = source.GlobalDialoguePrompt;
            UseAdvancedMode = source.UseAdvancedMode;
            UseHierarchicalPromptFormat = source.UseHierarchicalPromptFormat;
            Enabled = source.Enabled;
            PromptSchemaVersion = source.PromptSchemaVersion;
            PromptPolicySchemaVersion = source.PromptPolicySchemaVersion;

            ApiActions.Clear();
            foreach (var action in source.ApiActions)
            {
                ApiActions.Add(action.Clone());
            }

            ResponseFormat = source.ResponseFormat?.Clone() ?? new ResponseFormatConfig();
            EnvironmentPrompt = source.EnvironmentPrompt?.Clone() ?? new EnvironmentPromptConfig();

            DecisionRules.Clear();
            foreach (var rule in source.DecisionRules)
            {
                DecisionRules.Add(rule.Clone());
            }

            DynamicDataInjection = source.DynamicDataInjection?.Clone() ?? new DynamicDataInjectionConfig();
            PromptTemplates = source.PromptTemplates?.Clone() ?? new PromptTemplateTextConfig();
            PromptPolicy = source.PromptPolicy?.Clone() ?? PromptPolicyConfig.CreateDefault();
        }

        private void InitializeMinimalDefaults()
        {
            if (!TryLoadDefaultGlobalSystemPromptText(out string defaultGlobalSystemPrompt))
            {
                GlobalSystemPrompt = PlaceholderGlobalSystemPrompt;
            }
            else
            {
                GlobalSystemPrompt = defaultGlobalSystemPrompt;
            }

            if (TryLoadDefaultDiplomacyPromptSections(out SystemPromptConfig diplomacyDefaults))
            {
                ApiActions = new List<ApiActionConfig>();
                foreach (ApiActionConfig action in diplomacyDefaults.ApiActions)
                {
                    ApiActions.Add(action.Clone());
                }

                ResponseFormat = diplomacyDefaults.ResponseFormat?.Clone() ?? new ResponseFormatConfig();
                DecisionRules = new List<DecisionRuleConfig>();
                foreach (DecisionRuleConfig rule in diplomacyDefaults.DecisionRules)
                {
                    DecisionRules.Add(rule.Clone());
                }

                EnvironmentPrompt = diplomacyDefaults.EnvironmentPrompt?.Clone() ?? EnvironmentPromptConfig.CreateDefaultSeed();
                DynamicDataInjection = diplomacyDefaults.DynamicDataInjection?.Clone() ?? new DynamicDataInjectionConfig();
                PromptTemplates = diplomacyDefaults.PromptTemplates?.Clone() ?? new PromptTemplateTextConfig();
                PromptSchemaVersion = CurrentPromptSchemaVersion;
                PromptPolicySchemaVersion = CurrentPromptPolicySchemaVersion;
                PromptPolicy = diplomacyDefaults.PromptPolicy?.Clone() ?? PromptPolicyConfig.CreateDefault();
                return;
            }

            ApiActions = new List<ApiActionConfig>
            {
                new ApiActionConfig("adjust_goodwill", "Змінити відносини з фракцією", "amount (int), reason (string)", ""),
                new ApiActionConfig("request_aid", "Запросити військову/медичну допомогу", "type (string)", "Лише коли відносини достатньо міцні для допомоги, поточна прихильність відповідає порогу допомоги з обмежень API, а час відновлення RimChat-запиту допомоги для цієї фракції готовий (15 днів на фракцію). Початкові запити через консоль зв'язку не входять до цього часу відновлення."),
                new ApiActionConfig("declare_war", "Оголосити війну", "reason (string)", "Лише коли відносини вже достатньо ворожі для оголошення війни."),
                new ApiActionConfig("make_peace", "Запропонувати мирний договір (потрібен стан війни). Оціни щирість гравця й переходь до дії лише за дуже високої щирості.", "cost (int, silver)", "Лише коли вже триває війна і щирість гравця дуже висока."),
                new ApiActionConfig("request_caravan", "Запросити торговий караван до колонії гравця. Роль: ти (AI) є ПРОДАВЦЕМ, гравець є ПОКУПЦЕМ. Товари каравану походять зі складів твоєї фракції; не можна обіцяти конкретні предмети. Використовуй лише дозволені типи караванів: General / BulkGoods / CombatSupplier / Exotic / Slaver.", "goods (string, optional)", "Лише коли відносини не ворожі й час відновлення RimChat-запиту каравану для цієї фракції готовий (7 днів на фракцію). Початкові запити через консоль зв'язку не входять до цього часу відновлення."),
                new ApiActionConfig("request_visitor", "Запросити групу відвідувачів", "", "Лише коли відносини не ворожі й час відновлення RimChat-запиту відвідувачів для цієї фракції готовий (7 днів на фракцію). Відправлення використовує затримане прибуття та подію VisitorGroup."),
                new ApiActionConfig("request_raid", PromptTextConstants.RequestRaidActionDescription, PromptTextConstants.RequestRaidActionParameters, PromptTextConstants.RequestRaidActionRequirement),
                new ApiActionConfig("request_raid_call_everyone", PromptTextConstants.RequestRaidCallEveryoneActionDescription, "", PromptTextConstants.RequestRaidCallEveryoneActionRequirement),
                new ApiActionConfig("request_raid_waves", PromptTextConstants.RequestRaidWavesActionDescription, PromptTextConstants.RequestRaidWavesActionParameters, PromptTextConstants.RequestRaidWavesActionRequirement),
                new ApiActionConfig("request_item_airdrop", "Знайди один реальний ігровий ThingDef з тексту потреби й після підтвердження гравцем надішли його стандартною десантною капсулою. Якщо торговельна картка вже прив'язала точний need_def, виконання має лишатися на цьому предметі, доки гравець явно не вибере інший.", "need (string, required; MUST include player-specified quantity when present, e.g. '1000 колод', '50 сталі'), payment_items (array<object>, required; each object MUST contain: item (string, prefer defName) AND count (int>0). Example: [{\"item\":\"Silver\",\"count\":1200}]", "Need і payment_items обов'язкові. need MUST точно відображати запит гравця: коли гравець указує кількість (наприклад, '1000 колод', '50 сталі'), поле need MUST містити цю кількість; ігнорувати вказану гравцем кількість заборонено. Бюджет під час виконання обчислюється з повної ринкової вартості payment_items через Floor. Кожен елемент payment_items MUST має містити і 'item', і 'count'; відсутній 'count' спричинить помилку виконання. payment_items мають бути дійсними товарами для торгівлі через маяк; item слід задавати спершу як defName, а label лише коли відповідність однозначна. Якщо торговельна картка вже прив'язала точний need_def, не підміняй його іншим предметом без явного повторного вибору. За будь-якої помилки валідації негайно зупинись."),
                new ApiActionConfig("request_info", "Запросити інформацію часу виконання, потрібну перед дією.", "info_type (string, REQUIRED; currently prisoner only)", "Використовуй лише коли в ланцюжку викупу бракує інформації про вибір полоненого (наприклад, відсутній дійсний target_pawn_load_id). Негайно зупиняйся для непідтримуваного info_type."),
                new ApiActionConfig("pay_prisoner_ransom", "Подати виплату викупу сріблом за полоненого, якого утримує гравець, і зареєструвати контракт викупу; звільнення полоненого виконується гравцем вручну. Стратегія ціни: нижчий викуп вигідніший фракції; вищий викуп відображає цінність полоненого або апетит гравця; якщо гравець готовий звільнити без умов, запропонуй низьку ціну й оціни його добру волю.", "target_pawn_load_id (int, REQUIRED), offer_silver (int>0, REQUIRED), payment_mode (string, optional; omit or set exactly silver)", "Лише для полонених, що належать поточній фракції й утримуються гравцем. Якщо target_pawn_load_id відсутній або недійсний, виклич request_info(info_type=prisoner) для вибору; інакше pay_prisoner_ransom можна викликати напряму. offer_silver має спиратися на поточне вікно пропозиції із системних повідомлень; виконання притисне значення поза межами до найближчої межі перед поданням. Принцип ціни: як платник, фракція виграє від нижчого викупу; вища ціна лише для цінних полонених або коли гравець вимагає багато; якщо гравець готовий звільнити без умов, запропонуй низьку ціну (наприклад 10%-30% від довідкової) і подякуй за добру волю. payment_mode можна опустити; якщо вказано, воно MUST бути точно silver. Виконуй одне подання платежу за хід. MUST: якщо природна мова стверджує, що викуп подано/сплачено/врегульовано або полоненого звільнено, та сама відповідь MUST містити дію pay_prisoner_ransom."),
                new ApiActionConfig("trigger_incident", "Запустити ігрову подію (incident)", "defName (string), amount (int, optional points)", ""),
                new ApiActionConfig("create_quest", "Створити місію/завдання для гравця за допомогою вбудованого шаблону. Напрям має значення: [гравець→фракція] = гравець надає (PawnLend, TradeRequest); [фракція→гравець] = фракція надає (Hospitality_Refugee, ThreatReward_Raid_MiscReward). Узгоджуй напрям із контекстом розмови.", "questDefName (string, REQUIRED: exact name from the dynamic list provided below - this parameter is MANDATORY and cannot be omitted), askerFaction (string, optional: defaults to current faction), points (int, optional: threat points for the mission)", "CRITICAL: questDefName є MANDATORY. Дія завершиться помилкою, якщо questDefName відсутній або не входить до дозволеного списку. MUST перевірити доступний список questDefName у контексті перед викликом цієї дії. Якщо немає дійсного questDefName, НЕ викликай create_quest. Власні завдання НЕ дозволені."),
                new ApiActionConfig("send_image", PromptTextConstants.SendImageActionDescription, PromptTextConstants.SendImageActionParameters, PromptTextConstants.SendImageActionRequirement),
                new ApiActionConfig("exit_dialogue", "Завершити поточний сеанс розмови, зберігши поточний статус присутності", "reason (string, optional)", ""),
                new ApiActionConfig("go_offline", PromptTextConstants.GoOfflineActionDescription, "reason (string, optional)", ""),
                new ApiActionConfig("set_dnd", PromptTextConstants.SetDndActionDescription, "reason (string, optional)", ""),
                new ApiActionConfig("publish_public_post", PromptTextConstants.PublishPublicPostActionDescription, PromptTextConstants.PublishPublicPostActionParameters, PromptTextConstants.PublishPublicPostActionRequirement),
                new ApiActionConfig("reject_request", "Відхилити запит гравця", "reason (string)", "Використовуй, коли явно відхиляєш конкретний запит гравця, який слід записати як відмову. Не використовуй для звичайної незгоди.")
            };

            ApiActionConfig requestAidAction = ApiActions.Find(action => action.ActionName == "request_aid");
            if (requestAidAction != null)
            {
                requestAidAction.Requirement = "Лише коли відносини достатньо міцні для допомоги, поточна прихильність відповідає порогу допомоги з обмежень API, а час відновлення RimChat-запиту допомоги для цієї фракції готовий (15 днів на фракцію). Початкові запити через консоль зв'язку не входять до цього часу відновлення.";
            }

            ResponseFormat = new ResponseFormatConfig
            {
                JsonTemplate = "{\n  \"visible_dialogue\": \"видима репліка в ролі\",\n  \"actions\": [\n    {\n      \"action\": \"snake_case_action\",\n      \"parameters\": {\n        \"param1\": \"value\"\n      }\n    }\n  ]\n}",
                ImportantRules = "1. Відповідай мовою гри користувача, зберігаючи JSON-ключі та назви дій без змін.\n2. Для діалогових ходів поверни рівно один JSON-об'єкт верхнього рівня.\n3. Основне видиме поле — visible_dialogue; воно має містити лише репліку в ролі без міркувань, нотаток, заголовків або системних коментарів.\n4. Використовуй лише ввімкнені дії й дотримуйся вимог, часу відновлення та обмежень.\n5. Коли доречно, віддзеркалюй стислість гравця, але зберігай достатню ясність і послідовний тон.\n6. Діалог відбувається через комунікаційний термінал, а не на особистій/offline зустрічі. Не описуй прибуття на місце, передачу з рук у руки або фізичне забирання полоненого в цьому каналі.\n7. Якщо ігровий ефект не потрібен, повністю опусти actions; сам JSON-об'єкт не опускай.\n8. Використовуй request_info(info_type=prisoner) лише коли бракує інформації про ціль викупу.\n9. Якщо target_pawn_load_id уже відомий і дійсний, pay_prisoner_ransom можна викликати напряму.\n10. Для pay_prisoner_ransom ніколи не стверджуй оплату/подання, доки target_pawn_load_id і offer_silver не є дійсними.\n11. Для pay_prisoner_ransom payment_mode можна опустити; якщо вказано, використовуй точно silver.\n12. Для pay_prisoner_ransom offer_silver має спиратися на поточне вікно пропозиції із системних повідомлень; виконання притискає значення поза межами до найближчої межі перед поданням.\n13. Для стратегії ціни pay_prisoner_ransom: як платник, фракція виграє від нижчого викупу; вища ціна лише для цінних полонених або коли гравець вимагає багато; якщо гравець готовий звільнити без умов, запропонуй низьку ціну й подякуй за добру волю.\n14. Для pay_prisoner_ransom звичайний потік використовує одне подання; коли існує [RansomBatchSelection] і ти виводиш pay_prisoner_ransom, виведи по одній дії для кожної переліченої цілі рівно один раз у тій самій відповіді.\n15. MUST: якщо visible_dialogue стверджує, що викуп сплачено/подано/врегульовано або полоненого звільнено, та сама відповідь має містити дію pay_prisoner_ransom; інакше перепиши текст як очікування або уточнення.\n16. MUST читати [RansomState] як джерело істини щодо статусу paid/unpaid/pending-release перед текстом про перебіг викупу.\n17. Якщо [RansomState] current_request_paid=false, НЕ стверджуй, що платіж подано/врегульовано або полоненого звільнено; спершу попроси уточнення або виконай дію платежу."
            };

            DecisionRules = new List<DecisionRuleConfig>
            {
                new DecisionRuleConfig("GoodwillGuideline", "Під час ухвалення рішень враховуй поточний рівень прихильності."),
                new DecisionRuleConfig("LeaderTraits", "Під час ухвалення рішень враховуй риси свого лідера."),
                new DecisionRuleConfig("RansomPricingStrategy", "Стратегія ціни викупу: як платник, фракція виграє від нижчого викупу. Вища ціна лише для: 1) цінних полонених (високі навички, міцне здоров'я); 2) гравця з великим апетитом (вимагає високої ціни або займає жорстку позицію). Якщо гравець готовий звільнити без умов, запропонуй низьку ціну (10%-30% від довідкової) і подякуй за добру волю, показуючи щирість і вдячність фракції.")
            };

            EnvironmentPrompt = EnvironmentPromptConfig.CreateDefaultSeed();
            PromptTemplates = new PromptTemplateTextConfig();
            PromptSchemaVersion = CurrentPromptSchemaVersion;
            PromptPolicySchemaVersion = CurrentPromptPolicySchemaVersion;
            PromptPolicy = PromptPolicyConfig.CreateDefault();
        }

        private bool TryLoadDefaultGlobalSystemPromptText(out string prompt)
        {
            prompt = string.Empty;

            try
            {
                string defaultConfigPath = GetDefaultConfigPath();
                if (string.IsNullOrWhiteSpace(defaultConfigPath) || !LocalStorage.Current.FileExists(defaultConfigPath))
                {
                    return false;
                }

                string json = LocalStorage.Current.ReadAllText(defaultConfigPath);
                var payload = JsonUtility.FromJson<DefaultPromptHeaderPayload>(json);
                if (!string.IsNullOrWhiteSpace(payload?.GlobalSystemPrompt))
                {
                    prompt = payload.GlobalSystemPrompt;
                    return true;
                }

                Match match = Regex.Match(
                    json ?? string.Empty,
                    "\"GlobalSystemPrompt\"\\s*:\\s*\"((?:\\\\.|[^\"])*)\"",
                    RegexOptions.Singleline);
                if (!match.Success || match.Groups.Count < 2)
                {
                    return false;
                }

                prompt = match.Groups[1].Value
                    .Replace("\\n", "\n")
                    .Replace("\\r", "\r")
                    .Replace("\\t", "\t")
                    .Replace("\\\"", "\"")
                    .Replace("\\\\", "\\");
                return !string.IsNullOrWhiteSpace(prompt);
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimAI.Relations] Failed to extract GlobalSystemPrompt from default file for minimal defaults: {ex.Message}");
                prompt = string.Empty;
                return false;
            }
        }

        private bool TryLoadDefaultDiplomacyPromptSections(out SystemPromptConfig config)
        {
            config = null;

            try
            {
                string defaultPath = GetDefaultPromptResourcePath(DefaultDiplomacyPromptFileName);
                if (string.IsNullOrWhiteSpace(defaultPath) || !LocalStorage.Current.FileExists(defaultPath))
                {
                    return false;
                }

                string json = LocalStorage.Current.ReadAllText(defaultPath);
                config = PromptPersistenceService.Instance?.ParseJsonToConfigInternal(
                    json,
                    $"default_diplomacy_prompt_file:{defaultPath}");

                bool usable = config?.ApiActions != null &&
                    config.ApiActions.Count > 0 &&
                    config.ResponseFormat != null &&
                    !string.IsNullOrWhiteSpace(config.ResponseFormat.ImportantRules) &&
                    !string.IsNullOrWhiteSpace(config.ResponseFormat.JsonTemplate) &&
                    config.DecisionRules != null &&
                    config.DecisionRules.Count > 0;

                if (usable)
                {
                    Log.Message($"[RimAI.Relations] Loaded default diplomacy prompt fallback from {defaultPath}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimAI.Relations] Failed to load default diplomacy prompt fallback: {ex.Message}");
            }

            config = null;
            return false;
        }
    }
}

