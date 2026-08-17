using System.Text;
using Ustas.RimAI.Communication.Relations.Config;

namespace Ustas.RimAI.Communication.Relations.Prompting.Builders
{
    /// <summary>
    /// Dependencies: PromptTextConstants contract literals.
    /// Responsibility: compose the static Diplomacy output-contract hint block.
    /// </summary>
    internal static class DiplomacyPromptContractComposer
    {
        public static string ComposeStaticContractHints()
        {
            var sb = new StringBuilder();
            AppendStaticContractHints(sb);
            return sb.ToString();
        }

        public static void AppendStaticContractHints(StringBuilder sb)
        {
            if (sb == null)
            {
                return;
            }

            sb.AppendLine(PromptTextConstants.OutputSpecificationAuthorityHeader);
            sb.AppendLine(PromptTextConstants.OutputSpecificationAuthorityReference);
            sb.AppendLine();
            sb.AppendLine(PromptTextConstants.ResponseFormatHeader);
            sb.AppendLine(PromptTextConstants.ResponseFormatReference);
            sb.AppendLine();
            sb.AppendLine(PromptTextConstants.CriticalActionRulesHeader);
            sb.AppendLine(PromptTextConstants.CriticalActionRulesReference);
            sb.AppendLine();
            sb.AppendLine(PromptTextConstants.NoActionResponseHint);
        }
    }
}
