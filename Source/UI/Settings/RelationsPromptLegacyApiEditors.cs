using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using RimWorld;
using UnityEngine;
using UnityEngine.Networking;
using Verse;
using Verse.Sound;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Context;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.Prompting.Transfer;
using Ustas.RimAI.Core.Player2;
using Ustas.RimAI.Core.UI;

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class RelationsPromptLegacyApiEditors
{
    internal readonly RelationsPromptLegacyEditors Owner;

    internal RelationsPromptLegacyApiEditors(RelationsPromptLegacyEditors owner)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    internal RelationsSettingsPages Pages => Owner.Pages;
    internal RelationsSettings Settings => Owner.Settings;
    internal SystemPromptConfig SystemPromptConfigData => Owner.SystemPromptConfigData;

        internal void DrawApiActionsEditorScrollable(Rect rect)
        {
            bool editingEnabledBefore = Settings.EnableApiPromptEditing;
            Rect toggleRect = new Rect(rect.x, rect.y, rect.width, 24f);
            Widgets.CheckboxLabeled(toggleRect, "RimChat_EnableApiPromptEditing".Translate(), ref Settings.EnableApiPromptEditing);
            if (!editingEnabledBefore && Settings.EnableApiPromptEditing)
            {
                ShowApiPromptEditingWarningDialog();
            }

            if (!Settings.EnableApiPromptEditing)
            {
                GUI.color = Color.yellow;
                Widgets.Label(new Rect(rect.x, rect.y + 28f, rect.width, 24f), "RimChat_ApiPromptEditingLocked".Translate());
                GUI.color = Color.white;
                return;
            }

            float contentTop = rect.y + 30f;
            Rect contentRect = new Rect(rect.x, contentTop, rect.width, rect.height - 30f);

            var actions = GetEditableApiActions();
            if (actions == null || actions.Count == 0)
            {
                Widgets.Label(contentRect, "RimChat_NoApiActions".Translate());
                return;
            }

            // 宸︿晶鍒楄〃鍖哄煙锛堝浐瀹氬搴︼級
            float listWidth = 220f;
            float buttonHeight = 32f;
            Rect listRect = new Rect(contentRect.x, contentRect.y, listWidth, contentRect.height - buttonHeight);
            
            // 鍙充晶缂栬緫鍖哄煙
            Rect editRect = new Rect(contentRect.x + listWidth + 10f, contentRect.y, contentRect.width - listWidth - 10f, contentRect.height - buttonHeight);

            // 缁樺埗鍔ㄤ綔鍒楄〃锛堝甫婊氬姩锛?
            float itemHeight = 30f;
            float listContentHeight = actions.Count * itemHeight;
            Rect listContentRect = new Rect(0f, 0f, listWidth - 16f, Mathf.Max(listContentHeight, listRect.height));

            Owner._apiActionListScroll = GUI.BeginScrollView(listRect, Owner._apiActionListScroll, listContentRect);
            for (int i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                Rect rowRect = new Rect(0f, i * itemHeight, listContentRect.width, itemHeight - 1f);
                bool isSelected = i == Owner._selectedApiActionIndex;

                if (isSelected)
                    Widgets.DrawBoxSolid(rowRect, new Color(0.25f, 0.35f, 0.55f, 0.8f));
                else if (Mouse.IsOver(rowRect))
                    Widgets.DrawBoxSolid(rowRect, new Color(0.2f, 0.22f, 0.28f, 0.6f));

                string label = $"{(action.IsEnabled ? "[ON]" : "[OFF]")} {action.ActionName}";
                GUI.color = action.IsEnabled ? Color.white : Color.gray;
                TextAnchor oldAnchor = Text.Anchor;
                Text.Anchor = TextAnchor.MiddleLeft;
                Rect labelRect = new Rect(rowRect.x + 8f, rowRect.y + 2f, rowRect.width - 16f, rowRect.height - 4f);
                Widgets.Label(labelRect, label.Truncate(labelRect.width));
                Text.Anchor = oldAnchor;
                GUI.color = Color.white;

                if (Widgets.ButtonInvisible(rowRect))
                {
                    Owner._selectedApiActionIndex = i;
                    Owner._editingApiActionName = action.ActionName ?? "";
                    Owner._editingApiActionDesc = action.Description ?? "";
                    Owner._editingApiActionParams = action.Parameters ?? "";
                    Owner._editingApiActionReq = action.Requirement ?? "";
                }
            }
            GUI.EndScrollView();

            // 鏂板鎸夐挳锛堝垪琛ㄥ簳閮級
            Rect addBtnRect = new Rect(contentRect.x, contentRect.yMax - buttonHeight, listWidth, buttonHeight - 4f);
            if (Widgets.ButtonText(addBtnRect, "RimChat_AddNew".Translate()))
            {
                AddNewApiAction();
            }

            // 缁樺埗鍙充晶缂栬緫鍖哄煙
            if (Owner._selectedApiActionIndex >= 0 && Owner._selectedApiActionIndex < actions.Count)
            {
                var action = actions[Owner._selectedApiActionIndex];
                float y = editRect.y;

                // 鏍囬
                GUI.color = RelationsPromptLegacyEditors.SectionHeaderColor;
                Widgets.Label(new Rect(editRect.x, y, editRect.width, 24f), "RimChat_EditApiAction".Translate());
                GUI.color = Color.white;
                y += 28f;

                // 鍚嶇О
                Widgets.Label(new Rect(editRect.x, y, editRect.width, 20f), "RimChat_ActionName".Translate());
                y += 22f;
                Owner._editingApiActionName = Widgets.TextField(new Rect(editRect.x, y, editRect.width, 24f), Owner._editingApiActionName);
                y += 28f;

                // 鍙傛暟
                Widgets.Label(new Rect(editRect.x, y, editRect.width, 20f), "RimChat_ParametersLabel".Translate());
                y += 22f;
                Owner._editingApiActionParams = Widgets.TextField(new Rect(editRect.x, y, editRect.width, 24f), Owner._editingApiActionParams);
                y += 28f;

                // 闄愬埗鏉′欢
                Widgets.Label(new Rect(editRect.x, y, editRect.width, 20f), "RimChat_RequirementLabel".Translate());
                y += 22f;
                Owner._editingApiActionReq = Widgets.TextField(new Rect(editRect.x, y, editRect.width, 24f), Owner._editingApiActionReq);
                y += 28f;

                // 鎻忚堪锛堝ぇ鏂囨湰妗嗭紝甯︽粴鍔紝濉弧鍓╀綑绌洪棿锛?
                Widgets.Label(new Rect(editRect.x, y, editRect.width, 20f), "RimChat_DescriptionLabel".Translate());
                y += 22f;
                float descHeight = editRect.yMax - y - 40f; // 涓哄簳閮ㄦ寜閽暀鍑虹┖闂?
                Rect descRect = new Rect(editRect.x, y, editRect.width - 16f, descHeight);
                
                // 璁＄畻瀹為檯鍐呭楂樺害锛岀‘淇濆畬鏁存樉绀?
                float descContentHeight = Mathf.Max(descRect.height, Text.CalcHeight(Owner._editingApiActionDesc, descRect.width - 16f) + 10f);
                Rect descViewRect = new Rect(0f, 0f, descRect.width - 16f, descContentHeight);
                Owner._apiActionDescScroll = GUI.BeginScrollView(descRect, Owner._apiActionDescScroll, descViewRect);
                Owner._editingApiActionDesc = GUI.TextArea(descViewRect, Owner._editingApiActionDesc);
                GUI.EndScrollView();
                
                action.Description = Owner._editingApiActionDesc;
                action.ActionName = Owner._editingApiActionName;
                action.Parameters = Owner._editingApiActionParams;
                action.Requirement = Owner._editingApiActionReq;

                // 鎸夐挳鍖哄煙锛堝浐瀹氬湪搴曢儴锛?
                float btnWidth = 100f;
                float btnGap = 10f;
                float btnStartX = editRect.x;
                
                // 鍚敤/绂佺敤鎸夐挳
                Rect enableBtnRect = new Rect(btnStartX, contentRect.yMax - buttonHeight, btnWidth, buttonHeight - 4f);
                if (Widgets.ButtonText(enableBtnRect, action.IsEnabled ? "RimChat_Disable".Translate() : "RimChat_Enable".Translate()))
                    action.IsEnabled = !action.IsEnabled;
                
                // 鍒犻櫎鎸夐挳
                Rect deleteBtnRect = new Rect(btnStartX + btnWidth + btnGap, contentRect.yMax - buttonHeight, btnWidth, buttonHeight - 4f);
                if (Widgets.ButtonText(deleteBtnRect, "RimChat_DeleteSelected".Translate()))
                {
                    ShowDeleteApiActionConfirmation(action);
                }
            }
            else
            {
                // 鏈€変腑浠讳綍椤规椂鏄剧ず鎻愮ず
                GUI.color = Color.gray;
                Text.Font = GameFont.Medium;
                Widgets.Label(editRect.ContractedBy(20f), "RimChat_SelectApiAction".Translate());
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
            }
        }

        internal void ShowApiPromptEditingWarningDialog()
        {
            Find.WindowStack.Add(new Dialog_MessageBox(
                $"[{"RimChat_WarningTitle".Translate()}]\n\n{"RimChat_ApiPromptEditingWarning".Translate()}",
                "OK".Translate(),
                null,
                null,
                null,
                null,
                false,
                null,
                null,
                WindowLayer.Dialog));
        }

        internal void AddNewApiAction()
        {
            SystemPromptConfigData.ApiActions ??= new List<ApiActionConfig>();
            var newAction = new ApiActionConfig
            {
                ActionName = "NewAction",
                Description = "",
                Parameters = "",
                Requirement = "",
                IsEnabled = true
            };
            int insertIndex = SystemPromptConfigData.ApiActions.FindIndex(item =>
                string.Equals(item?.ActionName, "publish_public_post", StringComparison.OrdinalIgnoreCase));
            if (insertIndex < 0)
            {
                insertIndex = SystemPromptConfigData.ApiActions.Count;
            }

            SystemPromptConfigData.ApiActions.Insert(insertIndex, newAction);
            Owner._selectedApiActionIndex = GetEditableApiActions().Count - 1;
            Owner._editingApiActionName = "NewAction";
            Owner._editingApiActionDesc = "";
            Owner._editingApiActionParams = "";
            Owner._editingApiActionReq = "";
            Messages.Message("RimChat_ItemAdded".Translate("RimChat_ApiActionsSection".Translate()), MessageTypeDefOf.NeutralEvent, false);
        }

        internal void ShowDeleteApiActionConfirmation(ApiActionConfig action)
        {
            Dialog_MessageBox dialog = Dialog_MessageBox.CreateConfirmation(
                "RimChat_DeleteConfirm".Translate("RimChat_ApiActionsSection".Translate()),
                () =>
                {
                    int oldIndex = Owner._selectedApiActionIndex;
                    SystemPromptConfigData.ApiActions.Remove(action);
                    List<ApiActionConfig> editableActions = GetEditableApiActions();
                    if (editableActions.Count == 0)
                    {
                        Owner._selectedApiActionIndex = -1;
                        Owner._editingApiActionName = "";
                        Owner._editingApiActionDesc = "";
                        Owner._editingApiActionParams = "";
                        Owner._editingApiActionReq = "";
                    }
                    else
                    {
                        Owner._selectedApiActionIndex = Mathf.Min(oldIndex, editableActions.Count - 1);
                        if (Owner._selectedApiActionIndex >= 0 && Owner._selectedApiActionIndex < editableActions.Count)
                        {
                            var newAction = editableActions[Owner._selectedApiActionIndex];
                            Owner._editingApiActionName = newAction.ActionName ?? "";
                            Owner._editingApiActionDesc = newAction.Description ?? "";
                            Owner._editingApiActionParams = newAction.Parameters ?? "";
                            Owner._editingApiActionReq = newAction.Requirement ?? "";
                        }
                    }
                    Messages.Message("RimChat_ItemDeleted".Translate(action.ActionName), MessageTypeDefOf.NeutralEvent, false);
                },
                true,
                "RimChat_DeleteConfirmTitle".Translate()
            );
            Find.WindowStack.Add(dialog);
        }

        internal List<ApiActionConfig> GetEditableApiActions()
        {
            SystemPromptConfigData.ApiActions ??= new List<ApiActionConfig>();
            return SystemPromptConfigData.ApiActions
                .Where(action => !string.Equals(action?.ActionName, "publish_public_post", StringComparison.OrdinalIgnoreCase))
                .Where(action => !string.Equals(action?.ActionName, "send_image", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
}
