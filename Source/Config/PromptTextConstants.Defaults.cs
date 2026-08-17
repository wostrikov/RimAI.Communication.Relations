using Ustas.RimAI.Communication.Relations.Persistence;

namespace Ustas.RimAI.Communication.Relations.Config
{
    /// <summary>
    /// Dependencies: RPG and Social Circle default providers.
    /// Responsibility: expose loaded default prompt text that is not hardcoded in PromptTextConstants.
    /// </summary>
    internal static partial class PromptTextConstants
    {
        private static RpgPromptDefaultsConfig _cachedRpgDefaults;
        private static SocialCirclePromptDomainConfig _cachedSocialDefaults;

        private static RpgPromptDefaultsConfig RpgDefaults =>
            _cachedRpgDefaults ?? (_cachedRpgDefaults = RpgPromptDefaultsProvider.GetDefaults());

        private static SocialCirclePromptDomainConfig SocialDefaults =>
            _cachedSocialDefaults ?? (_cachedSocialDefaults = SocialCirclePromptDefaultsProvider.GetDefaults());

        public static string RpgRoleSettingDefault =>
            RpgDefaults.RoleSetting;

        public static string RpgDialogueStyleDefault =>
            RpgDefaults.DialogueStyle;

        public static string RpgFormatConstraintDefault =>
            RpgDefaults.FormatConstraint;

        public static string PublishPublicPostActionDescription =>
            SocialDefaults.PublishPublicPostAction?.Description ?? string.Empty;

        public static string PublishPublicPostActionParameters =>
            SocialDefaults.PublishPublicPostAction?.Parameters ?? string.Empty;

        public static string PublishPublicPostActionRequirement =>
            SocialDefaults.PublishPublicPostAction?.Requirement ?? string.Empty;

        public static string SocialCircleNewsStyleTemplateDefault =>
            SocialDefaults.SocialCircleNewsStyleTemplate ?? string.Empty;

        public static string SocialCircleNewsJsonContractTemplateDefault =>
            SocialDefaults.SocialCircleNewsJsonContractTemplate ?? string.Empty;

        public static string SocialCircleNewsFactTemplateDefault =>
            SocialDefaults.SocialCircleNewsFactTemplate ?? string.Empty;
    }
}
