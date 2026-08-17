using System;

namespace Ustas.RimAI.Communication.Relations.Config;

internal sealed class PromptWorkbenchSectionDefinition
{
    public readonly string Id;
    public readonly string EnglishName;
    public readonly string[] Aliases;

    public PromptWorkbenchSectionDefinition(string id, string englishName, params string[] aliases)
    {
        Id = id ?? string.Empty;
        EnglishName = englishName ?? "Entry";
        Aliases = aliases ?? Array.Empty<string>();
    }
}
