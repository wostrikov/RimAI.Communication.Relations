using System.Text;
using Ustas.RimAI.Communication.Relations.Config;

namespace Ustas.RimAI.Communication.Relations.Prompting.Builders
{
    /// <summary>
    /// Dependencies: PromptTextConstants format literals.
    /// Responsibility: compose static RPG output-contract hints without colony/runtime state.
    /// </summary>
    internal static class RpgPromptComposition
    {
        public static string ResolveChannel(bool isProactive)
        {
            return PromptRuntimeChannels.ResolveRpg(isProactive);
        }

        public static string ComposeStaticFormatHints()
        {
            var sb = new StringBuilder();
            sb.AppendLine(PromptTextConstants.StrictJsonFormatHeader);
            sb.AppendLine(PromptTextConstants.StrictJsonFormatRequirement);
            return sb.ToString();
        }
    }
}
