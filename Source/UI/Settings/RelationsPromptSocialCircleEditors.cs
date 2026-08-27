using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.UI;
using Ustas.RimAI.Core.UI;

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class RelationsPromptSocialCircleEditors
{
    readonly RelationsSettingsPages Pages;

    internal RelationsPromptSocialCircleEditors(RelationsSettingsPages pages)
    {
        Pages = pages;
    }

    RelationsSettings Settings => Pages.Settings;

        internal const string SocialCirclePublishActionName = "publish_public_post";

        internal Vector2 _socialCirclePromptScroll = Vector2.zero;
        internal Vector2 _socialCircleRuleTemplateScroll = Vector2.zero;
        internal Vector2 _socialCircleNewsStyleScroll = Vector2.zero;
        internal Vector2 _socialCircleNewsContractScroll = Vector2.zero;
        internal Vector2 _socialCircleNewsFactScroll = Vector2.zero;
        internal Vector2 _socialCircleActionDescScroll = Vector2.zero;
        internal Vector2 _socialCircleActionParamsScroll = Vector2.zero;
        internal Vector2 _socialCircleActionReqScroll = Vector2.zero;

        internal void DrawSocialCirclePromptEditorScrollable(Rect rect)
        {
            ApiActionConfig action = GetOrCreateSocialCirclePublishAction();
            PromptTemplateTextConfig templates = Pages.PromptTemplates.EnsurePromptTemplateConfig();
            float contentHeight = Mathf.Max(rect.height, 1060f);
            Rect viewRect = new Rect(0f, 0f, rect.width - 16f, contentHeight);

            _socialCirclePromptScroll = GUI.BeginScrollView(rect, _socialCirclePromptScroll, viewRect);
            var listing = new Listing_Standard();
            // Verse wraps a Listing into a second column, off the visible view, as soon as
            // content passes the rect height, and CurHeight then reports that new column.
            // A scrolling settings page never wants that; see validate_scrollable_listings.
            listing.maxOneColumn = true;
            listing.Begin(new Rect(0f, 0f, viewRect.width, viewRect.height));
            DrawSocialCirclePromptHeader(listing, action);
            DrawSocialCircleTemplateEditor(listing, "RimChat_SocialCirclePromptTemplateLabel", templates.SocialCircleActionRuleTemplate, ref _socialCircleRuleTemplateScroll, value => templates.SocialCircleActionRuleTemplate = value);
            DrawNewsPromptHint(listing);
            DrawSocialCircleTemplateEditor(listing, "RimChat_SocialCircleNewsStyleLabel", templates.SocialCircleNewsStyleTemplate, ref _socialCircleNewsStyleScroll, value => templates.SocialCircleNewsStyleTemplate = value);
            DrawSocialCircleTemplateEditor(listing, "RimChat_SocialCircleNewsContractLabel", templates.SocialCircleNewsJsonContractTemplate, ref _socialCircleNewsContractScroll, value => templates.SocialCircleNewsJsonContractTemplate = value);
            DrawSocialCircleTemplateEditor(listing, "RimChat_SocialCircleNewsFactLabel", templates.SocialCircleNewsFactTemplate, ref _socialCircleNewsFactScroll, value => templates.SocialCircleNewsFactTemplate = value);
            DrawSocialCircleActionEditor(listing, action);
            listing.End();
            GUI.EndScrollView();
        }

        internal void DrawSocialCirclePromptHeader(Listing_Standard listing, ApiActionConfig action)
        {
            listing.Label("RimChat_SocialCirclePromptFlow".Translate());
            GUI.color = Color.gray;
            listing.Label("RimChat_SocialCirclePromptPersistenceHint".Translate());
            GUI.color = Color.white;

            bool enabled = action.IsEnabled;
            listing.CheckboxLabeled("RimChat_SocialCircleActionToggle".Translate(), ref enabled);
            if (enabled != action.IsEnabled)
            {
                action.IsEnabled = enabled;
                Pages.PromptLegacy._previewUpdateCooldown = 0;
            }
        }

        internal void DrawNewsPromptHint(Listing_Standard listing)
        {
            listing.Gap(4f);
            GUI.color = Color.gray;
            listing.Label("RimChat_SocialCircleNewsVariablesHint".Translate());
            GUI.color = Color.white;
        }

        internal void DrawSocialCircleTemplateEditor(
            Listing_Standard listing,
            string labelKey,
            string value,
            ref Vector2 scroll,
            Action<string> assign)
        {
            listing.GapLine();
            listing.Label(labelKey.Translate());
            Rect areaRect = listing.GetRect(140f);
            string edited = DrawPromptTextArea(areaRect, value, ref scroll);
            if (string.Equals(edited, value, StringComparison.Ordinal))
            {
                return;
            }

            assign?.Invoke(edited);
            Pages.PromptLegacy._previewUpdateCooldown = 0;
        }

        internal void DrawSocialCircleActionEditor(Listing_Standard listing, ApiActionConfig action)
        {
            listing.GapLine();
            listing.Label("RimChat_SocialCircleActionFieldsLabel".Translate(SocialCirclePublishActionName));
            DrawSocialCircleActionField(listing, "RimChat_SocialCircleActionDescriptionLabel", action.Description, ref _socialCircleActionDescScroll, edited => action.Description = edited);
            DrawSocialCircleActionField(listing, "RimChat_SocialCircleActionParametersLabel", action.Parameters, ref _socialCircleActionParamsScroll, edited => action.Parameters = edited);
            DrawSocialCircleActionField(listing, "RimChat_SocialCircleActionRequirementLabel", action.Requirement, ref _socialCircleActionReqScroll, edited => action.Requirement = edited);
        }

        internal void DrawSocialCircleActionField(
            Listing_Standard listing,
            string labelKey,
            string value,
            ref Vector2 scroll,
            Action<string> assign)
        {
            listing.Gap(4f);
            listing.Label(labelKey.Translate());
            Rect areaRect = listing.GetRect(82f);
            string edited = DrawPromptTextArea(areaRect, value, ref scroll);
            if (string.Equals(edited, value, StringComparison.Ordinal))
            {
                return;
            }

            assign?.Invoke(edited);
            Pages.PromptLegacy._previewUpdateCooldown = 0;
        }

        internal static string DrawPromptTextArea(Rect rect, string value, ref Vector2 scroll)
        {
            string normalized = value ?? string.Empty;
            float contentHeight = Mathf.Max(rect.height, Text.CalcHeight(normalized, rect.width - 16f) + 10f);
            Rect viewRect = new Rect(0f, 0f, rect.width - 16f, contentHeight);
            scroll = GUI.BeginScrollView(rect, scroll, viewRect);
            string edited = GUI.TextArea(viewRect, normalized);
            GUI.EndScrollView();
            return edited;
        }

        internal ApiActionConfig GetOrCreateSocialCirclePublishAction()
        {
            List<ApiActionConfig> actions = Pages.PromptLegacy.SystemPromptConfigData.ApiActions ??= new List<ApiActionConfig>();
            ApiActionConfig action = actions.FirstOrDefault(item =>
                string.Equals(item?.ActionName, SocialCirclePublishActionName, StringComparison.OrdinalIgnoreCase));
            if (action != null)
            {
                return action;
            }

            action = new ApiActionConfig(
                SocialCirclePublishActionName,
                PromptTextConstants.PublishPublicPostActionDescription,
                PromptTextConstants.PublishPublicPostActionParameters,
                PromptTextConstants.PublishPublicPostActionRequirement)
            {
                IsEnabled = true
            };
            int rejectIndex = actions.FindIndex(item => string.Equals(item?.ActionName, "reject_request", StringComparison.OrdinalIgnoreCase));
            if (rejectIndex >= 0)
            {
                actions.Insert(rejectIndex, action);
            }
            else
            {
                actions.Add(action);
            }

            return action;
        }

        internal bool TryAppendVariableToSocialCircleSection(string token)
        {
            PromptTemplateTextConfig templates = Pages.PromptTemplates.EnsurePromptTemplateConfig();
            templates.SocialCircleActionRuleTemplate = (templates.SocialCircleActionRuleTemplate ?? string.Empty) + token;
            Pages.PromptLegacy._previewUpdateCooldown = 0;
            return true;
        }

        internal string GetSocialCircleEditableText()
        {
            PromptTemplateTextConfig templates = Pages.PromptTemplates.EnsurePromptTemplateConfig();
            return string.Join(
                "\n\n",
                new[]
                {
                    templates.SocialCircleActionRuleTemplate ?? string.Empty,
                    templates.SocialCircleNewsStyleTemplate ?? string.Empty,
                    templates.SocialCircleNewsJsonContractTemplate ?? string.Empty,
                    templates.SocialCircleNewsFactTemplate ?? string.Empty
                }.Where(text => !string.IsNullOrWhiteSpace(text)));
        }
    
}
