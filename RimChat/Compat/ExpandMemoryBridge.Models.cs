namespace RimChat.Compat
{
    /// <summary>
    /// Dependencies: none.
    /// Responsibility: mirror data model for ExpandMemory CommonKnowledgeEntry fields used by RimChat.
    /// </summary>
    internal sealed class CommonKnowledgeMirrorEntry
    {
        public string Tag;
        public string Content;
        public float Importance;
        public string Category;
    }
}
