using System;

namespace Ustas.RimAI.Communication.Relations.Prompting
{
    /// <summary>
    /// Dependencies: native RimTalk render diagnostics.
    /// Responsibility: signal native RimTalk render compatibility failures that must fail fast.
    /// </summary>
    internal sealed class PromptRenderCompatibilityException : Exception
    {
        public NativeRenderDiagnostic Diagnostic { get; }

        public PromptRenderCompatibilityException(string message, NativeRenderDiagnostic diagnostic)
            : base(message)
        {
            Diagnostic = diagnostic;
        }
    }
}
