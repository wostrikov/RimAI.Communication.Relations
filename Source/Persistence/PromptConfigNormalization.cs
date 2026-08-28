using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Dialogue;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Relation;
using Ustas.RimAI.Communication.Relations.Guards;
using Ustas.RimAI.Communication.Relations.WorldState;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.Prompting.Builders;
using Ustas.RimAI.Communication.Relations.Context;
using Ustas.RimAI.Communication.Relations.Prompting.Transfer;
using Ustas.RimAI.Communication.Relations.Serialization;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Prompting.Diplomacy;

namespace Ustas.RimAI.Communication.Relations.Persistence
{
    internal sealed class PromptConfigNormalization
    {
        internal PromptConfigNormalizationParts Parts;

        internal readonly PromptPersistenceService host;

        internal PromptTemplateAutoRewriteResult _lastSchemaRewriteResult;
        internal PromptTemplateAutoRewriteResult LastSchemaRewriteResult => _lastSchemaRewriteResult;

        internal static readonly string[] PresenceBehaviorSectionTitles =
        {
            "[Політика статусу присутності]",
            "Online Status Strategy:",
            "Online Status Strategy"
        };

        internal static readonly string[] PresenceBehaviorActionAnchors =
        {
            "[exit_dialogue]",
            "[go_offline",
            "[set_dnd]"
        };

        internal PromptConfigNormalization(PromptPersistenceService host)
        {
            Parts = new PromptConfigNormalizationParts(this);
            this.host = host ?? throw new System.ArgumentNullException(nameof(host));
        }
        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

        

    
        #region Cluster forwards
        internal bool MigratePresenceBehaviorGuidance(SystemPromptConfig config) => Parts.Slice1.MigratePresenceBehaviorGuidance(config);
        internal string LoadPresenceBehaviorGuidanceSection() => Parts.Slice1.LoadPresenceBehaviorGuidanceSection();
        internal bool ContainsPresenceBehaviorGuidance(string promptText) => Parts.Slice1.ContainsPresenceBehaviorGuidance(promptText);
        internal int FindPresenceBehaviorInsertIndex(string promptText) => Parts.Slice1.FindPresenceBehaviorInsertIndex(promptText);
        internal string ExtractPresenceBehaviorSection(string promptText) => Parts.Slice1.ExtractPresenceBehaviorSection(promptText);
        internal int FindPresenceBehaviorSectionStart(IReadOnlyList<string> lines) => Parts.Slice1.FindPresenceBehaviorSectionStart(lines);
        internal string NormalizePresenceBehaviorSectionTitle(string titleLine) => Parts.Slice1.NormalizePresenceBehaviorSectionTitle(titleLine);
        internal bool IsPresenceBehaviorBoundary(string line) => Parts.Slice1.IsPresenceBehaviorBoundary(line);
        internal string BuildPresenceBehaviorFallbackSection() => Parts.Slice1.BuildPresenceBehaviorFallbackSection();
        internal bool TryApplyPromptSchemaUpgrade(SystemPromptConfig config) => Parts.Slice1.TryApplyPromptSchemaUpgrade(config);
        internal bool TryApplyPromptPolicySchemaUpgrade(ref SystemPromptConfig config) => Parts.Slice1.TryApplyPromptPolicySchemaUpgrade(ref config);
        internal bool EnsurePresenceActionExists(SystemPromptConfig config, string actionName, string description, string parameters, string requirement) => Parts.Slice1.EnsurePresenceActionExists(config, actionName, description, parameters, requirement);
        internal bool EnsureConfigDefaults(SystemPromptConfig config) => Parts.Slice1.EnsureConfigDefaults(config);
        internal bool EnsureApiActionDefaults(SystemPromptConfig config, SystemPromptConfig defaults) => Parts.Slice1.EnsureApiActionDefaults(config, defaults);
        internal bool TryUpgradeLegacyMakePeaceAction(ApiActionConfig target, ApiActionConfig defAction) => Parts.Slice1.TryUpgradeLegacyMakePeaceAction(target, defAction);
        internal bool TryUpgradeRansomActionContract(ApiActionConfig target, ApiActionConfig defAction) => Parts.Slice1.TryUpgradeRansomActionContract(target, defAction);
        internal bool EnsureRansomImportantRules(ResponseFormatConfig format) => Parts.Slice2.EnsureRansomImportantRules(format);
        internal string AppendRuleLine(string rules, string line) => Parts.Slice2.AppendRuleLine(rules, line);
        internal string RemoveRuleLine(string rules, string line) => Parts.Slice2.RemoveRuleLine(rules, line);
        internal bool IsLegacyMakePeaceDescription(string description) => Parts.Slice2.IsLegacyMakePeaceDescription(description);
        internal bool IsLegacyMakePeaceRequirement(string requirement) => Parts.Slice2.IsLegacyMakePeaceRequirement(requirement);
        internal bool RemoveDeprecatedPromptAction(SystemPromptConfig config, string actionName) => Parts.Slice2.RemoveDeprecatedPromptAction(config, actionName);
        internal bool EnsureResponseFormatDefaults(SystemPromptConfig config, SystemPromptConfig defaults) => Parts.Slice2.EnsureResponseFormatDefaults(config, defaults);
        internal bool EnsureDecisionRuleDefaults(SystemPromptConfig config, SystemPromptConfig defaults) => Parts.Slice2.EnsureDecisionRuleDefaults(config, defaults);
        internal bool EnsureEnvironmentPromptDefaults(SystemPromptConfig config, SystemPromptConfig defaults) => Parts.Slice2.EnsureEnvironmentPromptDefaults(config, defaults);
        internal bool EnsureWorldviewDefaults(EnvironmentPromptConfig target, EnvironmentPromptConfig defaults) => Parts.Slice2.EnsureWorldviewDefaults(target, defaults);
        internal bool EnsureSceneDefaults(EnvironmentPromptConfig target, EnvironmentPromptConfig defaults) => Parts.Slice2.EnsureSceneDefaults(target, defaults);
        internal bool EnsureEnvSwitchDefaults(EnvironmentPromptConfig target, EnvironmentPromptConfig defaults) => Parts.Slice2.EnsureEnvSwitchDefaults(target, defaults);
        internal bool EnsureRpgSwitchDefaults(EnvironmentPromptConfig target, EnvironmentPromptConfig defaults) => Parts.Slice2.EnsureRpgSwitchDefaults(target, defaults);
        internal bool TryUpgradeLegacyRpgSwitchDefaults(EnvironmentPromptConfig target) => Parts.Slice2.TryUpgradeLegacyRpgSwitchDefaults(target);
        internal bool IsLegacyRpgSwitchSignature(RpgSceneParamSwitchesConfig switches) => Parts.Slice2.IsLegacyRpgSwitchSignature(switches);
        internal bool EnsureEventIntelDefaults(EnvironmentPromptConfig target, EnvironmentPromptConfig defaults) => Parts.Slice2.EnsureEventIntelDefaults(target, defaults);
        internal bool EnsureDynamicInjectionDefaults(SystemPromptConfig config, SystemPromptConfig defaults) => Parts.Slice2.EnsureDynamicInjectionDefaults(config, defaults);
        internal bool EnsurePromptTemplateDefaults(SystemPromptConfig config, SystemPromptConfig defaults) => Parts.Slice3.EnsurePromptTemplateDefaults(config, defaults);
        internal bool ForceRefreshRpgPromptTemplates(PromptTemplateTextConfig target) => Parts.Slice3.ForceRefreshRpgPromptTemplates(target);
        internal bool EnsurePromptPolicyDefaults(SystemPromptConfig config, SystemPromptConfig defaults) => Parts.Slice3.EnsurePromptPolicyDefaults(config, defaults);
        internal bool TryMigrateLegacyNodeBodyLiteralTemplates(PromptTemplateTextConfig templates) => Parts.Slice3.TryMigrateLegacyNodeBodyLiteralTemplates(templates);
        internal bool TryRewriteLegacyNodeTemplate(ref string template, string rewrittenTemplate, string requiredMarkerA, string requiredMarkerB) => Parts.Slice3.TryRewriteLegacyNodeTemplate(ref template, rewrittenTemplate, requiredMarkerA, requiredMarkerB);
        internal bool AssignIfMissing(ref string target, string fallback) => Parts.Slice3.AssignIfMissing(ref target, fallback);
        internal bool AssignIfLessOrEqualZero(ref int target, int fallback) => Parts.Slice3.AssignIfLessOrEqualZero(ref target, fallback);
        #endregion
}
    internal sealed class PromptConfigNormalizationParts
    {
        internal readonly PromptConfigNormalization Owner;
        internal readonly PromptConfigNormalizationSlice1 Slice1;
        internal readonly PromptConfigNormalizationSlice2 Slice2;
        internal readonly PromptConfigNormalizationSlice3 Slice3;
        internal PromptConfigNormalizationParts(PromptConfigNormalization owner)
        {
            Owner = owner;
            Slice1 = new PromptConfigNormalizationSlice1(owner);
            Slice2 = new PromptConfigNormalizationSlice2(owner);
            Slice3 = new PromptConfigNormalizationSlice3(owner);
        }
    }

}
