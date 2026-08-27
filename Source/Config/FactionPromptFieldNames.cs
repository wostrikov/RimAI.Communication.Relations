namespace Ustas.RimAI.Communication.Relations.Config
{
    /// <summary>
    /// The five template field names, in one place because they are lookup keys.
    ///
    /// They are stored verbatim inside every persisted faction prompt config and
    /// matched by string equality, so they are machine-readable identity rather
    /// than prose: they are not translated, and they cannot be retyped. They read
    /// as Chinese only because the donor wrote them that way and the saved data
    /// now carries them.
    ///
    /// The faction prompt editor used to spell them out again at each call site,
    /// and at some point those copies were mangled by an encoding round-trip into
    /// sequences like the one below. Nothing failed: Find returned null,
    /// GetFieldValue returned an empty string, and all five feature boxes simply
    /// rendered blank. Referencing these constants is what makes that class of
    /// silence impossible.
    /// </summary>
    internal static class FactionPromptFieldNames
    {
        internal const string CoreStyle = "核心风格";
        internal const string Vocabulary = "用词特征";
        internal const string Tone = "语气特征";
        internal const string Sentence = "句式特征";
        internal const string Taboos = "表达禁忌";
    }
}
