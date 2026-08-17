using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.UI;
using Ustas.RimAI.Core.UI;

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class RelationsSocialCircleSettingsSection
{
    readonly RelationsSettingsPages Pages;

    internal RelationsSocialCircleSettingsSection(RelationsSettingsPages pages)
    {
        Pages = pages;
    }

    RelationsSettings Settings => Pages.Settings;

        internal void DrawSocialCircleSettings(Listing_Standard listing)
        {
            listing.CheckboxLabeled("RimChat_EnableSocialCircle".Translate(), ref Settings.EnableSocialCircle);
            listing.CheckboxLabeled("RimChat_EnablePlayerInfluenceNews".Translate(), ref Settings.EnablePlayerInfluenceNews);
            listing.CheckboxLabeled("RimChat_EnableAISimulationNews".Translate(), ref Settings.EnableAISimulationNews);
            listing.CheckboxLabeled("RimChat_EnableSocialCircleAutoActions".Translate(), ref Settings.EnableSocialCircleAutoActions);
            DrawScheduledNewsFrequencySelector(listing);

            Rect buttonRect = listing.GetRect(30f);
            bool canForceGenerate = Settings.EnableSocialCircle && Current.ProgramState == ProgramState.Playing && Current.Game != null;
            if (Widgets.ButtonText(buttonRect, "RimChat_SocialForceGenerateButton".Translate(), active: canForceGenerate))
            {
                SocialForceGenerateFailureReason failureReason = SocialForceGenerateFailureReason.Unknown;
                bool success = GameComponent_DiplomacyManager.Instance?.TryForceGeneratePublicPost(
                    DebugGenerateReason.ManualButton,
                    out failureReason) ?? false;

                MessageTypeDef messageType = success ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.RejectInput;
                string message;
                if (success)
                {
                    message = "RimChat_SocialForceGenerateSuccess".Translate();
                }
                else
                {
                    message = GetFailureMessage(failureReason);
                }

                Messages.Message(message, messageType, false);
                SoundDefOf.Click.PlayOneShotOnCamera(null);
            }

            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            if (!Settings.EnableSocialCircle)
            {
                listing.Label("RimChat_SocialForceGenerateDisabledHint".Translate());
            }
            else if (!canForceGenerate)
            {
                listing.Label("RimChat_SocialForceGenerateGameHint".Translate());
            }
            else
            {
                listing.Label("RimChat_SocialForceGenerateHint".Translate());
            }
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        internal string GetFailureMessage(SocialForceGenerateFailureReason reason)
        {
            switch (reason)
            {
                case SocialForceGenerateFailureReason.Disabled:
                    return "RimChat_SocialForceGenerateFailedDisabled".Translate();
                case SocialForceGenerateFailureReason.AiUnavailable:
                    return "RimChat_SocialForceGenerateFailedAiUnavailable".Translate();
                case SocialForceGenerateFailureReason.QueueFull:
                    return "RimChat_SocialForceGenerateFailedQueueFull".Translate();
                case SocialForceGenerateFailureReason.NoAvailableSeed:
                    return "RimChat_SocialForceGenerateFailedNoSeed".Translate();
                default:
                    return "RimChat_SocialForceGenerateFailed".Translate();
            }
        }

        internal void ResetSocialCircleSettingsToDefault()
        {
            Settings.EnableSocialCircle = true;
            Settings.ScheduledNewsFrequencyLevel = global::Ustas.RimAI.Communication.Relations.Config.ScheduledNewsFrequencyLevel.High;
            Settings.EnablePlayerInfluenceNews = true;
            Settings.EnableAISimulationNews = true;
            Settings.EnableSocialCircleAutoActions = false;
        }

        internal void DrawScheduledNewsFrequencySelector(Listing_Standard listing)
        {
            listing.Label("RimChat_ScheduledNewsFrequency".Translate());
            Rect rowRect = listing.GetRect(30f);
            float buttonWidth = (rowRect.width - 30f) / 4f;
            DrawScheduledNewsFrequencyButton(
                new Rect(rowRect.x, rowRect.y, buttonWidth, 30f),
                global::Ustas.RimAI.Communication.Relations.Config.ScheduledNewsFrequencyLevel.Low,
                "RimChat_ScheduledNewsFrequencyLow".Translate());
            DrawScheduledNewsFrequencyButton(
                new Rect(rowRect.x + buttonWidth + 10f, rowRect.y, buttonWidth, 30f),
                global::Ustas.RimAI.Communication.Relations.Config.ScheduledNewsFrequencyLevel.Medium,
                "RimChat_ScheduledNewsFrequencyMedium".Translate());
            DrawScheduledNewsFrequencyButton(
                new Rect(rowRect.x + (buttonWidth + 10f) * 2f, rowRect.y, buttonWidth, 30f),
                global::Ustas.RimAI.Communication.Relations.Config.ScheduledNewsFrequencyLevel.High,
                "RimChat_ScheduledNewsFrequencyHigh".Translate());
            DrawScheduledNewsFrequencyButton(
                new Rect(rowRect.x + (buttonWidth + 10f) * 3f, rowRect.y, buttonWidth, 30f),
                global::Ustas.RimAI.Communication.Relations.Config.ScheduledNewsFrequencyLevel.VeryHigh,
                "RimChat_ScheduledNewsFrequencyVeryHigh".Translate());
        }

        internal void DrawScheduledNewsFrequencyButton(
            Rect rect,
            global::Ustas.RimAI.Communication.Relations.Config.ScheduledNewsFrequencyLevel mode,
            string label)
        {
            Color oldColor = GUI.color;
            if (Settings.ScheduledNewsFrequencyLevel == mode)
            {
                GUI.color = new Color(0.35f, 0.55f, 0.85f, 0.9f);
            }

            if (Widgets.ButtonText(rect, label))
            {
                Settings.ScheduledNewsFrequencyLevel = mode;
            }

            GUI.color = oldColor;
        }
    
}
