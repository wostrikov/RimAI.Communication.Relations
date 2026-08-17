using Verse;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.UI;
using Ustas.RimAI.Core.UI;

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class RelationsRpgRimTalkCompatUi
{
    readonly RelationsSettingsPages Pages;

    internal RelationsRpgRimTalkCompatUi(RelationsSettingsPages pages)
    {
        Pages = pages;
    }

    RelationsSettings Settings => Pages.Settings;

        internal static string FormatUnlimitedAwareLimit(int value)
        {
            return value <= RelationsSettings.RimTalkPresetInjectionLimitUnlimited
                ? "RimChat_Unlimited".Translate().ToString()
                : value.ToString();
        }
    
}
