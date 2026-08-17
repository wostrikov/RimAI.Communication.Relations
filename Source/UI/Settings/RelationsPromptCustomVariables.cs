using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.UI;
using RimWorld;
using Verse;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Core.UI;

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class RelationsPromptCustomVariables
{
    readonly RelationsSettingsPages Pages;

    internal RelationsPromptCustomVariables(RelationsSettingsPages pages)
    {
        Pages = pages;
    }

    RelationsSettings Settings => Pages.Settings;

        internal void InvalidatePromptVariableBrowserCache()
        {
            Pages.VariableBrowser._rimTalkVariableSnapshotReady = false;
            Pages.VariableBrowser._rimTalkVariableCacheRefreshAt = -1f;
            Pages.VariableBrowser._rimTalkVariableTooltipCache.Clear();
            Pages.VariableBrowser.InvalidatePromptVariableRowCache();
            Pages.PromptWorkspace.MarkWorkspaceDirty(RelationsPromptSectionWorkspace.WorkspaceDirtySidePanel);
        }

        internal void OpenUserDefinedPromptVariableEditor(string path = null)
        {
            UserDefinedPromptVariableConfig variable = UserDefinedPromptVariableService.FindVariableByPath(path, Settings)?.Clone();
            var model = new UserDefinedPromptVariableEditModel
            {
                Variable = variable ?? new UserDefinedPromptVariableConfig(),
                FactionRules = variable == null
                    ? new List<FactionPromptVariableRuleConfig>()
                    : UserDefinedPromptVariableService.GetFactionRulesForKey(variable.Key, Settings),
                PawnRules = variable == null
                    ? new List<PawnPromptVariableRuleConfig>()
                    : UserDefinedPromptVariableService.GetPawnRulesForKey(variable.Key, Settings)
            };
            Find.WindowStack.Add(new Dialog_UserDefinedPromptVariableEditor(Settings, model, variable, () =>
            {
                InvalidatePromptVariableBrowserCache();
                Pages.VariableBrowser._rimTalkSelectedVariableName = UserDefinedPromptVariableService.BuildPath(model.Variable.Key);
            }));
        }

        internal void TryDeleteUserDefinedPromptVariable(string path)
        {
            UserDefinedPromptVariableConfig config = UserDefinedPromptVariableService.FindVariableByPath(path, Settings);
            if (config == null)
            {
                return;
            }

            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "RimChat_CustomVariableDeleteConfirm".Translate(UserDefinedPromptVariableService.BuildPath(config.Key)),
                () =>
                {
                    if (UserDefinedPromptVariableService.TryDeleteVariable(Settings, path, out List<UserDefinedPromptVariableReferenceLocation> references))
                    {
                        InvalidatePromptVariableBrowserCache();
                        Pages.VariableBrowser._rimTalkSelectedVariableName = string.Empty;
                        Messages.Message("RimChat_CustomVariableDeleteSuccess".Translate(UserDefinedPromptVariableService.BuildPath(config.Key)), MessageTypeDefOf.NeutralEvent, false);
                        return;
                    }

                    string details = string.Join("\n", references.Select(item => "- " + item.DisplayText));
                    Messages.Message("RimChat_CustomVariableDeleteBlocked".Translate(details), MessageTypeDefOf.RejectInput, false);
                },
                true,
                "RimChat_DeleteConfirmTitle".Translate()));
        }
    
}
