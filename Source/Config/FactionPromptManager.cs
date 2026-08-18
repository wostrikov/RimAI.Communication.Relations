using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Persistence;

namespace Ustas.RimAI.Communication.Relations.Config
{
    /// <summary>/// factionPromptmanager
 /// 负责从外部fileload和管理各faction的Promptconfiguration
 ///</summary>
    public class FactionPromptManager
    {
        readonly FactionPromptCatalogPersistence Persistence;
        readonly FactionPromptTemplateOps Templates;

        internal FactionPromptManager()
        {
            Persistence = new FactionPromptCatalogPersistence(this);
            Templates = new FactionPromptTemplateOps(this);
        }
        #region 单例模式

        private static FactionPromptManager _instance;
        public static FactionPromptManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new FactionPromptManager();
                }
                return _instance;
            }
        }

        #endregion

        #region 常量

        /// <summary>/// Promptconfigurationfile名
 ///</summary>
        public const string ConfigFileName = "FactionPrompts_Custom.json";

        /// <summary>/// 默认Prompt资源path
 ///</summary>
        public const string DefaultPromptsResourcePath = "RimChat/DefaultFactionPrompts";

        /// <summary>/// 默认Promptconfigurationfile名 (在Mod目录中)
 ///</summary>
        public const string DefaultConfigFileName = "FactionPrompts_Default.json";

        #endregion

        #region 字段

        /// <summary>/// configuration集合
 ///</summary>
        internal FactionPromptConfigCollection _configCollection;

        /// <summary>/// whether已initialize
 ///</summary>
        internal bool _initialized;

        /// <summary>/// configurationfile完整path
 ///</summary>
        internal string _configFilePath;

        internal HashSet<string> _defaultFactionDefNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        internal Dictionary<string, FactionPromptConfig> _defaultConfigLookup =
            new Dictionary<string, FactionPromptConfig>(StringComparer.OrdinalIgnoreCase);
        #endregion

        #region 属性

        /// <summary>/// get所有configuration
 ///</summary>
        public List<FactionPromptConfig> AllConfigs
        {
            get
            {
                if (!_initialized) Initialize();
                return _configCollection?.Configs ?? new List<FactionPromptConfig>();
            }
        }

        /// <summary>/// configurationfilepath
 ///</summary>
        public string ConfigFilePath
        {
            get
            {
                if (string.IsNullOrEmpty(_configFilePath))
                {
                    _configFilePath = GetCustomConfigFilePathInternal();
                }
                return _configFilePath;
            }
        }

        #endregion

        #region 初始化

        /// <summary>/// initializemanager
 ///</summary>
        public void Initialize()
        {
            if (_initialized) return;

            try
            {
                BuildDefaultConfigCatalog();

                // Load或创建configuration
                LoadConfigs();

                // 确保所有faction都有configuration
                EnsureAllFactionsHaveConfigs();

                _initialized = true;
                Log.Message($"[RimAI.Relations] FactionPromptManager initialized with {_configCollection.Configs.Count} faction prompts");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAI.Relations] Failed to initialize FactionPromptManager: {ex}");
                // 创建空configuration集合作为后备
                _configCollection = new FactionPromptConfigCollection();
            }
        }

        #endregion

        #region 配置加载与保存

        /// <summary>/// loadconfiguration
 ///</summary>
        private void LoadConfigs()
        {
            Persistence.LoadConfigs();
        }


        /// <summary>/// saveconfiguration
 ///</summary>
        public void SaveConfigs()
        {
            Persistence.SaveConfigs();
        }


        /// <summary>/// load默认configuration (从 FactionPrompts_Default.json file)
 ///</summary>
        private void LoadDefaultConfigs()
        {
            Persistence.LoadDefaultConfigs();
        }


        /// <summary>/// Promptfoldername
 ///</summary>
        public const string PromptFolderName = "Prompt";

        /// <summary>/// 默认configuration子foldername
 ///</summary>
        public const string DefaultSubFolderName = "Default";

        /// <summary>/// 自定义configuration子foldername
 ///</summary>
        public const string CustomSubFolderName = "Custom";

        /// <summary>/// get默认configurationfilepath (Mod目录下的Prompt/Defaultfolder)
 ///</summary>
        private string GetDefaultConfigFilePath()
        {
            return Persistence.GetDefaultConfigFilePath();
        }


        /// <summary>/// get自定义configurationfilepath (Mod目录下的Prompt/Customfolder)
 ///</summary>
        public string GetCustomConfigFilePath()
        {
            return ConfigFilePath;
        }

        private string GetCustomConfigFilePathInternal()
        {
            return Persistence.GetCustomConfigFilePathInternal();
        }


        /// <summary>/// load硬编码默认configuration (后备方案)
 ///</summary>
        private void LoadHardcodedDefaultConfigs()
        {
            Persistence.LoadHardcodedDefaultConfigs();
        }


        /// <summary>/// 确保所有faction都有configuration
 ///</summary>
        private void EnsureAllFactionsHaveConfigs()
        {
            Persistence.EnsureAllFactionsHaveConfigs();
        }


        #endregion

        #region 默认配置创建

        /// <summary>/// get支持的factionDef列表
 ///</summary>
        private List<FactionDef> GetSupportedFactionDefs()
        {
            return Persistence.GetSupportedFactionDefs();
        }


        /// <summary>/// 如果presence则添加factionDef
 ///</summary>
        private void AddFactionDefIfExists(List<FactionDef> list, string defName)
        {
            Persistence.AddFactionDefIfExists(list, defName);
        }


        /// <summary>/// 创建默认configuration
 ///</summary>
        private FactionPromptConfig CreateDefaultConfig(FactionDef factionDef)
        {
            return Persistence.CreateDefaultConfig(factionDef);
        }


        /// <summary>/// 为factionsettings默认template字段
 /// 注意: 默认configuration应从 FactionPrompts_Default.json file读取
 /// 此method仅在file读取失败时作为后备使用, 创建最小化configuration
 ///</summary>
        internal void SetupDefaultTemplateFields(FactionPromptConfig config, string factionDefName)
        {
            Persistence.SetupDefaultTemplateFields(config, factionDefName);
        }


        #endregion

        #region 公共方法

        /// <summary>/// getfactionPromptconfiguration
 ///</summary>
        public FactionPromptConfig GetConfig(string factionDefName)
        {
            if (!_initialized) Initialize();
            return _configCollection?.GetConfig(factionDefName);
        }

        /// <summary>
        /// Get faction prompt configuration by faction instance first, then fallback to faction def template.
        /// </summary>
        public FactionPromptConfig GetConfig(Faction faction)
        {
            if (faction == null)
            {
                return null;
            }

            if (!_initialized) Initialize();

            string defName = faction.def?.defName;
            if (!string.IsNullOrWhiteSpace(defName) && faction.loadID > 0)
            {
                string instanceKey = BuildFactionInstanceConfigKey(defName, faction.loadID);
                FactionPromptConfig instanceConfig = _configCollection?.GetConfig(instanceKey);
                if (instanceConfig != null && instanceConfig.TemplateFields != null && instanceConfig.TemplateFields.Count > 0)
                {
                    return instanceConfig;
                }

                // Fail fast: once a concrete faction instance is seen, persist an isolated copy.
                FactionPromptConfig seeded = SeedInstanceConfigFromTemplate(faction, defName, instanceKey);
                if (seeded != null)
                {
                    _configCollection.SetConfig(seeded);
                    SaveConfigs();
                    return seeded;
                }
            }

            return _configCollection?.GetConfig(defName);
        }

        /// <summary>/// getfactionPromptcontents
 ///</summary>
        public string GetPrompt(string factionDefName)
        {
            var config = GetConfig(factionDefName);
            return config?.GetEffectivePrompt() ?? "";
        }

        /// <summary>
        /// Get faction prompt by faction instance first, then fallback to faction def template.
        /// </summary>
        public string GetPrompt(Faction faction)
        {
            FactionPromptConfig config = GetConfig(faction);
            return config?.GetEffectivePrompt() ?? string.Empty;
        }

        private static string BuildFactionInstanceConfigKey(string factionDefName, int factionLoadId)
        {
            string normalizedDefName = factionDefName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedDefName) || factionLoadId <= 0)
            {
                return string.Empty;
            }

            return $"{normalizedDefName}@{factionLoadId}";
        }

        private FactionPromptConfig SeedInstanceConfigFromTemplate(
            Faction faction,
            string factionDefName,
            string instanceKey)
        {
            return Templates.SeedInstanceConfigFromTemplate(faction, factionDefName, instanceKey);
        }


        /// <summary>/// 更新configuration
 ///</summary>
        public void UpdateConfig(FactionPromptConfig config)
        {
            if (!_initialized) Initialize();
            if (config == null) return;

            _configCollection.SetConfig(config);
            SaveConfigs();
        }

        public bool TryAddTemplateForFaction(string factionDefName, string displayName, out string status)
        {
            return Templates.TryAddTemplateForFaction(factionDefName, displayName, out status);
        }


        public bool TryRemoveTemplate(string factionDefName, out string reason)
        {
            return Templates.TryRemoveTemplate(factionDefName, out reason);
        }


        public bool IsDefaultTemplate(string factionDefName)
        {
            if (!_initialized) Initialize();
            if (string.IsNullOrWhiteSpace(factionDefName))
            {
                return false;
            }

            return _defaultFactionDefNames.Contains(factionDefName.Trim());
        }

        public bool IsFactionMissing(string factionDefName)
        {
            if (string.IsNullOrWhiteSpace(factionDefName))
            {
                return true;
            }

            return DefDatabase<FactionDef>.GetNamedSilentFail(factionDefName.Trim()) == null;
        }

        /// <summary>/// 重置configuration为默认
 ///</summary>
        public void ResetConfig(string factionDefName)
        {
            var config = GetConfig(factionDefName);
            if (config != null)
            {
                config.ResetToDefault();
                SaveConfigs();
            }
        }

        /// <summary>/// 重置所有configuration为默认
 ///</summary>
        public void ResetAllConfigs()
        {
            if (!_initialized) Initialize();

            foreach (var config in _configCollection.Configs)
            {
                config.ResetToDefault();
            }
            SaveConfigs();
        }

        /// <summary>/// apply自定义Prompt
 ///</summary>
        public void ApplyCustomPrompt(string factionDefName, string customPrompt)
        {
            var config = GetConfig(factionDefName);
            if (config != null)
            {
                config.ApplyCustomPrompt(customPrompt);
                SaveConfigs();
            }
        }

        /// <summary>/// 导出configuration到file
 ///</summary>
        public bool ExportConfigs(string filePath)
        {
            return Persistence.ExportConfigs(filePath);
        }


        /// <summary>/// 从file导入configuration
 ///</summary>
        public bool ImportConfigs(string filePath)
        {
            return Persistence.ImportConfigs(filePath);
        }


        public string ExportConfigsToJson(bool prettyPrint = false)
        {
            return Persistence.ExportConfigsToJson(prettyPrint);
        }


        public bool ImportConfigsFromJson(string json)
        {
            return Persistence.ImportConfigsFromJson(json);
        }


        private void BuildDefaultConfigCatalog()
        {
            Persistence.BuildDefaultConfigCatalog();
        }


        private bool TryPopulateDefaultCatalog(FactionPromptConfigCollection collection)
        {
            return Persistence.TryPopulateDefaultCatalog(collection);
        }


        private void BuildHardcodedDefaultCatalog()
        {
            Persistence.BuildHardcodedDefaultCatalog();
        }


        #endregion
    }

    /// <summary>/// 简单的JSON工具类 - 使用手动序列化
 ///</summary>
    public static class FactionPromptJsonUtility
    {
        public static string ToJson(FactionPromptConfigCollection collection, bool prettyPrint = false)
        {
            if (collection == null || collection.Configs == null)
                return "{\"Configs\":[]}";

            var sb = new StringBuilder();
            if (prettyPrint)
            {
                sb.AppendLine("{");
                sb.AppendLine("  \"Configs\": [");
            }
            else
            {
                sb.Append("{\"Configs\":[");
            }

            for (int i = 0; i < collection.Configs.Count; i++)
            {
                var config = collection.Configs[i];
                if (prettyPrint) sb.Append("    ");
                sb.Append("{");

                sb.Append($"\"FactionDefName\":\"{EscapeJson(config.FactionDefName)}\",");
                sb.Append($"\"DisplayName\":\"{EscapeJson(config.DisplayName)}\",");
                
                // 序列化template字段
                sb.Append("\"TemplateFields\":[");
                for (int j = 0; j < config.TemplateFields.Count; j++)
                {
                    var field = config.TemplateFields[j];
                    if (prettyPrint) sb.Append("\n      ");
                    sb.Append("{");
                    sb.Append($"\"FieldName\":\"{EscapeJson(field.FieldName)}\",");
                    sb.Append($"\"FieldValue\":\"{EscapeJson(field.FieldValue)}\",");
                    sb.Append($"\"FieldDescription\":\"{EscapeJson(field.FieldDescription)}\",");
                    sb.Append($"\"IsEnabled\":{field.IsEnabled.ToString().ToLower()}");
                    sb.Append("}");
                    if (j < config.TemplateFields.Count - 1)
                    {
                        sb.Append(",");
                    }
                }
                if (prettyPrint) sb.Append("\n    ");
                sb.Append("],");

                sb.Append($"\"UseCustomPrompt\":{config.UseCustomPrompt.ToString().ToLower()},");
                sb.Append($"\"CustomPrompt\":\"{EscapeJson(config.CustomPrompt)}\",");
                sb.Append($"\"LastModifiedTicks\":{config.LastModifiedTicks}");

                sb.Append("}");
                if (i < collection.Configs.Count - 1)
                {
                    sb.Append(",");
                }
                if (prettyPrint) sb.AppendLine();
            }

            if (prettyPrint)
            {
                sb.AppendLine("  ]");
                sb.Append("}");
            }
            else
            {
                sb.Append("]}");
            }

            return sb.ToString();
        }

        public static FactionPromptConfigCollection FromJson(string json)
        {
            var collection = new FactionPromptConfigCollection();

            if (string.IsNullOrEmpty(json))
                return collection;

            try
            {
                // 解析 Configs 数组
                int configsStart = json.IndexOf("\"Configs\":");
                if (configsStart < 0) return collection;

                int arrayStart = json.IndexOf("[", configsStart);
                if (arrayStart < 0) return collection;

                int arrayEnd = json.LastIndexOf("]");
                if (arrayEnd < 0) return collection;

                string arrayContent = json.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);

                // 分割对象
                var objects = SplitJsonObjects(arrayContent);

                foreach (var objStr in objects)
                {
                    var config = ParseConfig(objStr);
                    if (config != null)
                    {
                        collection.Configs.Add(config);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimAI.Relations] Failed to parse JSON: {ex.Message}");
            }

            return collection;
        }

        private static List<string> SplitJsonObjects(string arrayContent)
        {
            var objects = new List<string>();
            int depth = 0;
            int start = 0;

            for (int i = 0; i < arrayContent.Length; i++)
            {
                char c = arrayContent[i];

                if (c == '{')
                {
                    if (depth == 0) start = i;
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        objects.Add(arrayContent.Substring(start, i - start + 1));
                    }
                }
                else if (c == '"')
                {
                    // 跳过字符串contents
                    i++;
                    while (i < arrayContent.Length && arrayContent[i] != '"')
                    {
                        if (arrayContent[i] == '\\' && i + 1 < arrayContent.Length)
                        {
                            i += 2;
                        }
                        else
                        {
                            i++;
                        }
                    }
                }
            }

            return objects;
        }

        private static FactionPromptConfig ParseConfig(string json)
        {
            var config = new FactionPromptConfig();

            try
            {
                config.FactionDefName = ExtractString(json, "FactionDefName");
                config.DisplayName = ExtractString(json, "DisplayName");
                config.CustomPrompt = ExtractString(json, "CustomPrompt");

                ParseTemplateFields(json, config);

                string useCustomStr = ExtractValue(json, "UseCustomPrompt");
                if (bool.TryParse(useCustomStr, out bool useCustom))
                {
                    config.UseCustomPrompt = useCustom;
                }

                string ticksStr = ExtractValue(json, "LastModifiedTicks");
                if (long.TryParse(ticksStr, out long ticks))
                {
                    config.LastModifiedTicks = ticks;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimAI.Relations] Failed to parse config: {ex.Message}");
                return null;
            }

            return config;
        }

        private static void ParseTemplateFields(string json, FactionPromptConfig config)
        {
            int fieldsStart = json.IndexOf("\"TemplateFields\":");
            if (fieldsStart < 0) return;

            int arrayStart = json.IndexOf("[", fieldsStart);
            if (arrayStart < 0) return;

            int depth = 1;
            int arrayEnd = arrayStart + 1;
            while (arrayEnd < json.Length && depth > 0)
            {
                if (json[arrayEnd] == '[') depth++;
                else if (json[arrayEnd] == ']') depth--;
                arrayEnd++;
            }

            string arrayContent = json.Substring(arrayStart + 1, arrayEnd - arrayStart - 2);
            var fieldObjects = SplitJsonObjects(arrayContent);

            foreach (var fieldStr in fieldObjects)
            {
                var field = ParseTemplateField(fieldStr);
                if (field != null)
                {
                    config.TemplateFields.Add(field);
                }
            }
        }

        private static PromptTemplateField ParseTemplateField(string json)
        {
            var field = new PromptTemplateField();

            try
            {
                field.FieldName = ExtractString(json, "FieldName");
                field.FieldValue = ExtractString(json, "FieldValue");
                field.FieldDescription = ExtractString(json, "FieldDescription");

                string enabledStr = ExtractValue(json, "IsEnabled");
                if (bool.TryParse(enabledStr, out bool enabled))
                {
                    field.IsEnabled = enabled;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimAI.Relations] Failed to parse template field: {ex.Message}");
                return null;
            }

            return field;
        }

        private static string ExtractString(string json, string key)
        {
            string pattern = $"\"{key}\":";
            int index = json.IndexOf(pattern);
            if (index < 0) return "";

            int start = json.IndexOf("\"", index + pattern.Length);
            if (start < 0) return "";

            start++;
            var sb = new StringBuilder();

            for (int i = start; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"')
                {
                    break;
                }
                else if (c == '\\' && i + 1 < json.Length)
                {
                    char next = json[i + 1];
                    switch (next)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        default: sb.Append(next); break;
                    }
                    i++;
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        private static string ExtractValue(string json, string key)
        {
            string pattern = $"\"{key}\":";
            int index = json.IndexOf(pattern);
            if (index < 0) return "";

            int start = index + pattern.Length;
            int end = json.IndexOfAny(new[] { ',', '}' }, start);
            if (end < 0) end = json.Length;

            return json.Substring(start, end - start).Trim();
        }

        private static string EscapeJson(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            return str
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }
    }
}
