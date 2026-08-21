namespace Ustas.RimAI.Communication.Relations.Config
{
    /// <summary>
    /// Detects and rewrites only the persisted ModSettings root Class identity.
    /// Does not change settings values.
    /// </summary>
    public static class RelationsSettingsPersistedIdentity
    {
        public const string CurrentClrType = "Ustas.RimAI.Communication.Relations.Config.RelationsSettings";
        public const string LegacyClrType = "Ustas.RimAI.Communication.Relations.RelationsSettings";

        const string RootPrefix = "<ModSettings Class=\"";

        public static bool TryGetRootClass(string xml, out string className)
        {
            className = null;
            if (string.IsNullOrEmpty(xml))
                return false;

            int start = xml.IndexOf(RootPrefix, System.StringComparison.Ordinal);
            if (start < 0)
                return false;

            start += RootPrefix.Length;
            int end = xml.IndexOf('"', start);
            if (end <= start)
                return false;

            className = xml.Substring(start, end - start);
            return className.Length > 0;
        }

        public static bool NeedsMigration(string xml)
        {
            return TryGetRootClass(xml, out string className)
                && className == LegacyClrType;
        }

        public static string RewriteRootClass(string xml, string newClass)
        {
            if (string.IsNullOrEmpty(xml) || string.IsNullOrEmpty(newClass))
                return xml;
            if (!TryGetRootClass(xml, out string current) || current == newClass)
                return xml;

            string oldToken = RootPrefix + current + "\"";
            int index = xml.IndexOf(oldToken, System.StringComparison.Ordinal);
            if (index < 0)
                return xml;

            return xml.Substring(0, index) + RootPrefix + newClass + "\"" + xml.Substring(index + oldToken.Length);
        }
    }
}
