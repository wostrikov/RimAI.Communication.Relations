using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.UI;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Core.UI;
using Ustas.RimAI.Core.Storage;

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class RelationsApiHeaderUx
{
    readonly RelationsSettingsPages Pages;

    internal RelationsApiHeaderUx(RelationsSettingsPages pages)
    {
        Pages = pages;
    }

    RelationsSettings Settings => Pages.Settings;

        internal const string EnglishLanguageFolder = "English";
        internal const string LanguagesRelativePath = "1.6\\Languages";
        internal const string VersionLogFileLocalizedDefault = "VersionLog.txt";
        internal const string VersionLogFileEnglish = "VersionLog_en.txt";
        internal const string VersionLogFileByLanguagePattern = "VersionLog_{0}.txt";
        internal const string HelpFileLocalizedDefault = "help.md";
        internal const string HelpFileEnglish = "help_en.md";
        internal const string RimChatGitHubUrl = "https://github.com/yancy22737-sudo/RimChat";
        internal const string DefaultVersionValue = "0.0.0";
        internal static readonly Dictionary<string, string> LanguageFolderAliasMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "english",
                ["enus"] = "english",
                ["eng"] = "english",
                ["zh"] = "chinesesimplified",
                ["zhcn"] = "chinesesimplified",
                ["zhhans"] = "chinesesimplified",
                ["chs"] = "chinesesimplified",
                ["simplifiedchinese"] = "chinesesimplified",
                ["zhtw"] = "chinesetraditional",
                ["zhhant"] = "chinesetraditional",
                ["cht"] = "chinesetraditional",
                ["traditionalchinese"] = "chinesetraditional"
            };

        internal string cachedVersionLanguage = string.Empty;
        internal string cachedVersionLogPath = string.Empty;
        internal string cachedVersionLogContent = string.Empty;
        internal string cachedVersionValue = DefaultVersionValue;
        internal bool versionLogCacheInitialized;
        internal bool cachedVersionReadFailed;
        internal string cachedVersionReadError = string.Empty;

        internal void DrawApiSettingsHeaderBar(Listing_Standard listing)
        {
            EnsureVersionLogCache();
            Rect rowRect = listing.GetRect(24f);

            string versionLabel = "RimChat_APIVersionButtonLabel".Translate(cachedVersionValue);
            const float githubWidth = 74f;
            float versionWidth = Mathf.Clamp(Text.CalcSize(versionLabel).x + 16f, 130f, 250f);
            const float spacing = 6f;

            Rect githubRect = new Rect(rowRect.xMax - githubWidth, rowRect.y, githubWidth, rowRect.height);
            Rect versionRect = new Rect(githubRect.x - spacing - versionWidth, rowRect.y, versionWidth, rowRect.height);
            Rect titleRect = new Rect(rowRect.x, rowRect.y, versionRect.x - spacing - rowRect.x, rowRect.height);

            Widgets.Label(titleRect, "RimChat_APISettings".Translate());
            DrawVersionButton(versionRect, versionLabel);
            DrawGitHubButton(githubRect);
        }

        internal void DrawVersionButton(Rect buttonRect, string label)
        {
            bool clicked = Widgets.ButtonText(buttonRect, label);
            Pages.Tooltips.Register(buttonRect, "RimChat_APIVersionButtonTooltip");
            if (!clicked)
            {
                return;
            }

            SoundDefOf.Click.PlayOneShotOnCamera(null);
            Find.WindowStack.Add(new Dialog_VersionLogViewer(
                "RimChat_VersionLogWindowTitle".Translate(),
                GetVersionLogDisplayContent()));
        }

        internal void DrawGitHubButton(Rect buttonRect)
        {
            Color previousColor = GUI.color;
            GUI.color = new Color(0.24f, 0.72f, 0.24f);
            bool clicked = Widgets.ButtonText(buttonRect, "RimChat_APIGitHubButton".Translate());
            GUI.color = previousColor;
            Pages.Tooltips.Register(buttonRect, "RimChat_APIGitHubButtonTooltip");

            if (!clicked)
            {
                return;
            }

            SoundDefOf.Click.PlayOneShotOnCamera(null);
            Application.OpenURL(RimChatGitHubUrl);
        }

        internal void EnsureVersionLogCache()
        {
            string language = LanguageDatabase.activeLanguage?.folderName ?? string.Empty;
            if (versionLogCacheInitialized
                && string.Equals(cachedVersionLanguage, language, StringComparison.Ordinal))
            {
                return;
            }

            versionLogCacheInitialized = true;
            cachedVersionLanguage = language;
            cachedVersionLogPath = ResolveVersionLogPath(language);
            cachedVersionReadFailed = false;
            cachedVersionReadError = string.Empty;
            cachedVersionLogContent = ReadVersionLogContent(cachedVersionLogPath);
            cachedVersionValue = ParseVersionFirstLine(cachedVersionLogContent);
            if (cachedVersionValue == DefaultVersionValue)
            {
                cachedVersionValue = ReadAboutVersion(ResolveModRootDir());
            }
        }

        internal static string ReadAboutVersion(string rootDir)
        {
            try
            {
                string path = System.IO.Path.Combine(rootDir ?? string.Empty, "About", "About.xml");
                var document = new System.Xml.XmlDocument();
                document.Load(path);
                string value = document.SelectSingleNode("/ModMetaData/modVersion")?.InnerText?.Trim();
                return string.IsNullOrWhiteSpace(value) ? DefaultVersionValue : value;
            }
            catch
            {
                return DefaultVersionValue;
            }
        }

        internal string ResolveVersionLogPath(string languageFolder)
        {
            return ResolveLocalizedDocumentPath(
                languageFolder,
                BuildVersionLogCandidates,
                VersionLogFileEnglish,
                "version log");
        }

        internal static string ResolveModRootDir()
        {
            return RelationsMod.Instance?.Content?.RootDir
                ?? LoadedModManager.GetMod<RelationsMod>()?.Content?.RootDir
                ?? string.Empty;
        }

        internal List<string> GetAvailableLanguages()
        {
            return GetAvailableLanguages(ResolveModRootDir());
        }

        internal static List<string> GetAvailableLanguages(string rootDir)
        {
            var languages = new List<string>();
            string languagesRoot = string.IsNullOrWhiteSpace(rootDir)
                ? LanguagesRelativePath
                : System.IO.Path.Combine(rootDir, LanguagesRelativePath);
            if (!LocalStorage.Current.DirectoryExists(languagesRoot))
            {
                return languages;
            }

            string[] dirs = LocalStorage.Current.GetDirectories(languagesRoot);
            for (int i = 0; i < dirs.Length; i++)
            {
                string folder = System.IO.Path.GetFileName(dirs[i])?.Trim();
                if (string.IsNullOrWhiteSpace(folder))
                {
                    continue;
                }

                if (languages.Exists(item => string.Equals(item, folder, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                languages.Add(folder);
            }

            languages.Sort(StringComparer.OrdinalIgnoreCase);
            return languages;
        }

        internal static string ResolveActiveLanguageFolder(string languageFolder, List<string> availableLanguages)
        {
            string direct = FindFolderByExactName(languageFolder, availableLanguages);
            if (!string.IsNullOrEmpty(direct))
            {
                return direct;
            }

            string normalized = NormalizeLanguageToken(languageFolder);
            string normalizedMatch = FindFolderByNormalizedName(normalized, availableLanguages);
            if (!string.IsNullOrEmpty(normalizedMatch))
            {
                return normalizedMatch;
            }

            if (LanguageFolderAliasMap.TryGetValue(normalized, out string aliasTarget))
            {
                string aliasMatch = FindFolderByNormalizedName(aliasTarget, availableLanguages);
                if (!string.IsNullOrEmpty(aliasMatch))
                {
                    return aliasMatch;
                }
            }

            return FindFolderByExactName(EnglishLanguageFolder, availableLanguages) ?? EnglishLanguageFolder;
        }

        internal static string FindFolderByExactName(string input, List<string> availableLanguages)
        {
            if (string.IsNullOrWhiteSpace(input) || availableLanguages == null)
            {
                return null;
            }

            for (int i = 0; i < availableLanguages.Count; i++)
            {
                if (string.Equals(availableLanguages[i], input.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return availableLanguages[i];
                }
            }

            return null;
        }

        internal static string FindFolderByNormalizedName(string normalizedTarget, List<string> availableLanguages)
        {
            if (string.IsNullOrWhiteSpace(normalizedTarget) || availableLanguages == null)
            {
                return null;
            }

            for (int i = 0; i < availableLanguages.Count; i++)
            {
                string normalizedCurrent = NormalizeLanguageToken(availableLanguages[i]);
                if (string.Equals(normalizedCurrent, normalizedTarget, StringComparison.Ordinal))
                {
                    return availableLanguages[i];
                }
            }

            return null;
        }

        internal static string NormalizeLanguageToken(string value)
        {
            string sanitized = TrimLanguageDisplaySuffix(value);
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder(sanitized.Length);
            for (int i = 0; i < sanitized.Length; i++)
            {
                char c = sanitized[i];
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(char.ToLowerInvariant(c));
                }
            }

            return sb.ToString();
        }

        internal static string TrimLanguageDisplaySuffix(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string trimmed = value.Trim();
            int suffixStart = trimmed.IndexOfAny(new[] { '(', '（' });
            if (suffixStart > 0)
            {
                return trimmed.Substring(0, suffixStart).Trim();
            }

            return trimmed;
        }

        internal static bool IsFolderMatched(string matchedFolder, string activeFolder)
        {
            if (string.IsNullOrWhiteSpace(activeFolder))
            {
                return false;
            }

            if (string.Equals(matchedFolder, activeFolder.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string normalizedMatched = NormalizeLanguageToken(matchedFolder);
            string normalizedActive = NormalizeLanguageToken(activeFolder);
            return !string.IsNullOrWhiteSpace(normalizedMatched)
                && string.Equals(normalizedMatched, normalizedActive, StringComparison.Ordinal);
        }

        internal static List<string> BuildVersionLogCandidates(string rootDir, string matchedFolder)
        {
            var candidates = new List<string>();
            bool isEnglish = string.Equals(
                NormalizeLanguageToken(matchedFolder),
                NormalizeLanguageToken(EnglishLanguageFolder),
                StringComparison.Ordinal);

            if (!isEnglish && !string.IsNullOrWhiteSpace(matchedFolder))
            {
                string languageSpecific = string.Format(
                    VersionLogFileByLanguagePattern,
                    matchedFolder.Trim());
                candidates.Add(CombineRootPath(rootDir, languageSpecific));
                candidates.Add(CombineRootPath(rootDir, VersionLogFileLocalizedDefault));
            }

            candidates.Add(CombineRootPath(rootDir, VersionLogFileEnglish));
            return candidates;
        }

        internal static List<string> BuildHelpCandidates(string rootDir, string matchedFolder)
        {
            var candidates = new List<string>();
            string normalizedFolder = NormalizeLanguageToken(matchedFolder);
            bool isChineseSimplified = string.Equals(normalizedFolder, "chinesesimplified", StringComparison.Ordinal);
            bool isEnglish = string.Equals(normalizedFolder, NormalizeLanguageToken(EnglishLanguageFolder), StringComparison.Ordinal);

            if (isChineseSimplified)
            {
                candidates.Add(CombineRootPath(rootDir, HelpFileLocalizedDefault));
                candidates.Add(CombineRootPath(rootDir, HelpFileEnglish));
                return candidates;
            }

            candidates.Add(CombineRootPath(rootDir, HelpFileEnglish));
            if (!isEnglish)
            {
                candidates.Add(CombineRootPath(rootDir, HelpFileLocalizedDefault));
            }

            return candidates;
        }

        internal static string ResolveLocalizedDocumentPath(
            string languageFolder,
            Func<string, string, List<string>> buildCandidates,
            string fallbackFileName,
            string logLabel)
        {
            string rootDir = ResolveModRootDir();
            List<string> availableLanguages = GetAvailableLanguages(rootDir);
            string matchedFolder = ResolveActiveLanguageFolder(languageFolder, availableLanguages);
            bool fallbackToEnglishFolder = !IsFolderMatched(matchedFolder, languageFolder);
            if (fallbackToEnglishFolder)
            {
                string fallbackPath = CombineRootPath(rootDir, fallbackFileName);
                string availableLabel = availableLanguages.Count == 0
                    ? "(none)"
                    : string.Join(", ", availableLanguages.ToArray());
                Log.Warning(
                    $"[RimAI.Relations] Active language folder '{languageFolder}' was not found in '{LanguagesRelativePath}'. " +
                    $"Available folders: {availableLabel}. Fail-fast fallback to '{EnglishLanguageFolder}' and '{fallbackPath}' for {logLabel}.");
            }

            List<string> candidates = buildCandidates(rootDir, matchedFolder);
            for (int i = 0; i < candidates.Count; i++)
            {
                string path = candidates[i];
                if (LocalStorage.Current.FileExists(path))
                {
                    if (i > 0)
                    {
                        Log.Warning(
                            $"[RimAI.Relations] {logLabel} file missing for language folder '{matchedFolder}'. " +
                            $"Tried '{candidates[0]}'. Fail-fast fallback to '{path}'.");
                    }

                    return path;
                }
            }

            string finalFallbackPath = CombineRootPath(rootDir, fallbackFileName);
            Log.Warning(
                $"[RimAI.Relations] No {logLabel} file exists for language folder '{matchedFolder}'. " +
                $"Tried: {string.Join(" | ", candidates.ToArray())}. Final fallback path: '{finalFallbackPath}'.");
            return finalFallbackPath;
        }

        internal static string CombineRootPath(string rootDir, string fileName)
        {
            return string.IsNullOrWhiteSpace(rootDir)
                ? fileName
                : System.IO.Path.Combine(rootDir, fileName);
        }

        internal string ReadVersionLogContent(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !LocalStorage.Current.FileExists(filePath))
            {
                return string.Empty;
            }

            try
            {
                return LocalStorage.Current.ReadAllText(filePath, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                cachedVersionReadFailed = true;
                cachedVersionReadError = ex.Message;
                Log.Warning($"[RimAI.Relations] Failed to read version log file: {filePath}. {ex.Message}");
                return string.Empty;
            }
        }

        internal string GetVersionLogDisplayContent()
        {
            if (!string.IsNullOrWhiteSpace(cachedVersionLogContent))
            {
                return cachedVersionLogContent;
            }

            if (cachedVersionReadFailed)
            {
                return "RimChat_VersionLogReadFailed".Translate(cachedVersionLogPath, cachedVersionReadError);
            }

            if (!LocalStorage.Current.FileExists(cachedVersionLogPath))
            {
                return "RimChat_VersionLogMissing".Translate(cachedVersionLogPath);
            }

            return "RimChat_VersionLogEmpty".Translate(cachedVersionLogPath);
        }

        internal string GetVersionDisplayVersion()
        {
            EnsureVersionLogCache();
            return cachedVersionValue;
        }

        internal string GetVersionLogDisplayContentForLanguage(string languageFolder)
        {
            string path = ResolveVersionLogPath(languageFolder);
            string content = ReadVersionLogContentFromPath(path, out string readError);
            if (!string.IsNullOrWhiteSpace(content))
            {
                return content;
            }

            if (!string.IsNullOrWhiteSpace(readError))
            {
                return "RimChat_VersionLogReadFailed".Translate(path, readError);
            }

            if (!LocalStorage.Current.FileExists(path))
            {
                return "RimChat_VersionLogMissing".Translate(path);
            }

            return "RimChat_VersionLogEmpty".Translate(path);
        }

        internal string GetHelpDisplayContentForLanguage(string languageFolder)
        {
            string path = ResolveLocalizedDocumentPath(
                languageFolder,
                BuildHelpCandidates,
                HelpFileEnglish,
                "help");
            string content = ReadVersionLogContentFromPath(path, out string readError);
            if (!string.IsNullOrWhiteSpace(content))
            {
                return content;
            }

            if (!string.IsNullOrWhiteSpace(readError))
            {
                return "RimChat_HelpReadFailed".Translate(path, readError);
            }

            if (!LocalStorage.Current.FileExists(path))
            {
                return "RimChat_HelpMissing".Translate(path);
            }

            return "RimChat_HelpEmpty".Translate(path);
        }

        internal static string ReadVersionLogContentFromPath(string filePath, out string readError)
        {
            readError = string.Empty;
            if (string.IsNullOrWhiteSpace(filePath) || !LocalStorage.Current.FileExists(filePath))
            {
                return string.Empty;
            }

            try
            {
                return LocalStorage.Current.ReadAllText(filePath, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                readError = ex.Message;
                Log.Warning($"[RimAI.Relations] Failed to read version log file: {filePath}. {ex.Message}");
                return string.Empty;
            }
        }

        internal static string ParseVersionFirstLine(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return DefaultVersionValue;
            }

            string[] lines = content.Replace("\r", string.Empty).Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i]?.Trim();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    return line;
                }
            }

            return DefaultVersionValue;
        }
    
}
