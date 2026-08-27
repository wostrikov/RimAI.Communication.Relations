using System.Collections.Generic;

namespace Ustas.RimAI.Communication.Relations.AI.Runtime
{
    /// <summary>
    /// Relations' own reloadable decision surface.
    ///
    /// This interface lives in the stable assembly, which is what makes the
    /// candidate work at all: the candidate references this assembly, the CLR
    /// binds it to the copy already loaded, and the cast in the gateway holds on
    /// type identity. It is deliberately coarse — three operations over strings
    /// and plain data — because changing its shape is a restart, while changing
    /// what the implementation does is a reload. A fine-grained contract would
    /// put every fix back on the restart path and buy nothing.
    ///
    /// Everything here is a pure function of its arguments: no Verse, no Unity,
    /// no IO, nothing the host holds a reference to across a swap.
    /// </summary>
    public interface IRelationsRuntime
    {
        /// <summary>Diagnostic identity of whoever answered. Never empty.</summary>
        string PolicyMarker { get; }

        string BuildResponsesRequest(RelationsProviderRequest request);

        PrimaryTextExtractionResult ExtractProviderText(string body, AIProvider provider);

        bool IsRetryableEmptyPrimaryText(string reasonTag);
    }

    /// <summary>
    /// One outgoing provider request, already normalized by the host. Plain data
    /// so it survives the assembly boundary without dragging anything with it.
    /// </summary>
    public sealed class RelationsProviderRequest
    {
        public string Model { get; set; } = string.Empty;

        public IList<ChatMessageData> Messages { get; set; } = new List<ChatMessageData>();

        public int MaxOutputTokens { get; set; }
    }
}
