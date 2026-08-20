using System;
using System.Collections.Generic;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Config
{
    public class PromptTemplateField
    {
        public string FieldName;

        public string FieldValue;

        public string FieldDescription;

        public bool IsEnabled;

        public PromptTemplateField()
        {
            IsEnabled = true;
        }

        public PromptTemplateField(string fieldName, string fieldValue, string fieldDescription = "")
        {
            FieldName = fieldName;
            FieldValue = fieldValue;
            FieldDescription = fieldDescription;
            IsEnabled = true;
        }

        public PromptTemplateField Clone()
        {
            return new PromptTemplateField
            {
                FieldName = this.FieldName,
                FieldValue = this.FieldValue,
                FieldDescription = this.FieldDescription,
                IsEnabled = this.IsEnabled
            };
        }
    }

    [Obsolete("Use PromptTemplatePreset with flat PromptTemplateEntry list instead. Retained only for in-memory reads of legacy preset shapes.")]
    public class FactionPromptConfig : IExposable
    {
        /// <summary>/// faction defName
 ///</summary>
        public string FactionDefName;

        /// <summary>/// factiondisplayname
 ///</summary>
        public string DisplayName;

        public List<PromptTemplateField> TemplateFields;

        public bool UseCustomPrompt;

        public string CustomPrompt;

        public long LastModifiedTicks;

        public FactionPromptConfig()
        {
            TemplateFields = new List<PromptTemplateField>();
        }

        public FactionPromptConfig(string factionDefName, string displayName)
        {
            FactionDefName = factionDefName;
            DisplayName = displayName;
            UseCustomPrompt = false;
            TemplateFields = new List<PromptTemplateField>();
        }

        public string GetEffectivePrompt()
        {
            if (UseCustomPrompt && !string.IsNullOrEmpty(CustomPrompt))
            {
                return CustomPrompt;
            }
            return BuildPromptFromTemplate();
        }

        public string BuildPromptFromTemplate()
        {
            var parts = new List<string>();

            foreach (var field in TemplateFields)
            {
                if (field.IsEnabled && !string.IsNullOrEmpty(field.FieldValue))
                {
                    parts.Add($"{field.FieldName}: {field.FieldValue}");
                }
            }

            return string.Join("\n\n", parts);
        }

        public PromptTemplateField GetOrCreateField(string fieldName, string defaultValue = "", string description = "")
        {
            var field = TemplateFields.Find(f => f.FieldName == fieldName);
            if (field == null)
            {
                field = new PromptTemplateField(fieldName, defaultValue, description);
                TemplateFields.Add(field);
            }
            return field;
        }

        public void SetFieldValue(string fieldName, string value)
        {
            var field = GetOrCreateField(fieldName);
            field.FieldValue = value;
            field.IsEnabled = !string.IsNullOrEmpty(value);
            LastModifiedTicks = DateTime.Now.Ticks;
        }

        public string GetFieldValue(string fieldName)
        {
            var field = TemplateFields.Find(f => f.FieldName == fieldName);
            return field?.FieldValue ?? "";
        }

        public void ResetToDefault()
        {
            UseCustomPrompt = false;
            CustomPrompt = "";
            LastModifiedTicks = DateTime.Now.Ticks;
        }

        public void ApplyCustomPrompt(string customPrompt)
        {
            CustomPrompt = customPrompt;
            UseCustomPrompt = true;
            LastModifiedTicks = DateTime.Now.Ticks;
        }

        // Serialization / save-load constraint — keep field identity stable. (summary summary)
        public void ExposeData()
        {
            Scribe_Values.Look(ref FactionDefName, "factionDefName", "");
            Scribe_Values.Look(ref DisplayName, "displayName", "");
            Scribe_Collections.Look(ref TemplateFields, "templateFields", LookMode.Deep);
            Scribe_Values.Look(ref UseCustomPrompt, "useCustomPrompt", false);
            Scribe_Values.Look(ref CustomPrompt, "customPrompt", "");
            Scribe_Values.Look(ref LastModifiedTicks, "lastModifiedTicks", 0);
        }

        public FactionPromptConfig Clone()
        {
            var clone = new FactionPromptConfig
            {
                FactionDefName = this.FactionDefName,
                DisplayName = this.DisplayName,
                UseCustomPrompt = this.UseCustomPrompt,
                CustomPrompt = this.CustomPrompt,
                LastModifiedTicks = this.LastModifiedTicks,
                TemplateFields = new List<PromptTemplateField>()
            };

            foreach (var field in TemplateFields)
            {
                clone.TemplateFields.Add(field.Clone());
            }

            return clone;
        }
    }

    public class FactionPromptConfigCollection : IExposable
    {
        public List<FactionPromptConfig> Configs = new List<FactionPromptConfig>();

        public void ExposeData()
        {
            Scribe_Collections.Look(ref Configs, "configs", LookMode.Deep);
        }

        public FactionPromptConfig GetConfig(string factionDefName)
        {
            return Configs.Find(c => c.FactionDefName == factionDefName);
        }

        public void SetConfig(FactionPromptConfig config)
        {
            var existing = GetConfig(config.FactionDefName);
            if (existing != null)
            {
                Configs.Remove(existing);
            }
            Configs.Add(config);
        }
    }
}
