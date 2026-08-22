using System;
using System.IO;

internal static class QuestEligibilityInitTests
{
    public static void Run(Action<bool, string> check)
    {
        string source = Path.Combine(AppContext.BaseDirectory, "ApiActionEligibilityService.cs.src");
        check(File.Exists(source), "eligibility service source is copied beside the test binary");
        if (!File.Exists(source))
            return;

        string text = File.ReadAllText(source);
        check(text.Contains("Parts = new ApiActionEligibilityServiceParts(this)"),
            "constructor assigns the eligibility slice cluster");
        check(text.Contains("=> Parts.Slice1.ValidateCreateQuest("),
            "ValidateCreateQuest goes through the wired cluster, not a null Parts field");
        check(!IsEmptyConstructor(text),
            "empty constructor that left Parts null is gone");
    }

    static bool IsEmptyConstructor(string text)
    {
        const string signature = "internal ApiActionEligibilityService()";
        int start = text.IndexOf(signature, StringComparison.Ordinal);
        if (start < 0)
            return true;
        int open = text.IndexOf('{', start);
        int close = text.IndexOf('}', open + 1);
        if (open < 0 || close < 0)
            return true;
        string body = text.Substring(open + 1, close - open - 1).Trim();
        return body.Length == 0;
    }
}
