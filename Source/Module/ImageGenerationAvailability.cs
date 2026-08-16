using Ustas.RimAI.Communication.Relations.Config;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Module
{
    /// <summary>
    /// Dependencies: Verse translation service and RimChat settings.
    /// Responsibility: centralize image-generation availability policy and user-facing message text.
    /// </summary>
    internal static class ImageGenerationAvailability
    {
        internal const string InDevelopmentKey = "RimChat_ImageGenerationInDevelopment";

        internal static bool IsBlocked()
        {
            RelationsSettings settings = RelationsMod.Settings;
            return settings?.DiplomacyImageApi == null || !settings.DiplomacyImageApi.IsEnabled;
        }

        internal static string GetBlockedMessage()
        {
            if (RelationsMod.Settings?.DiplomacyImageApi == null || !RelationsMod.Settings.DiplomacyImageApi.IsEnabled)
            {
                return "RimChat_SelfieConfigInvalid".Translate().ToString();
            }

            return InDevelopmentKey.Translate().ToString();
        }
    }
}
