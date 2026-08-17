namespace Ustas.RimAI.Communication.Relations.Persistence
{
    /// <summary>
    /// Dependencies: none.
    /// Responsibility: normalize prompt-config schema versions and empty strings without mutating builders.
    /// </summary>
    internal static class PromptConfigDocumentNormalizer
    {
        public const int CurrentPromptSchemaVersion = 3;
        public const int CurrentPromptPolicySchemaVersion = 4;

        public static int NormalizeSchemaVersion(int loaded, int current)
        {
            return loaded <= 0 ? current : loaded;
        }

        public static string NullToEmpty(string value)
        {
            return value ?? string.Empty;
        }
    }
}
