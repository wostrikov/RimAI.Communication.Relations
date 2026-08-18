using System;
using System.IO;
using Ustas.RimAI.Communication.Relations.Persistence;
using Verse;
using Ustas.RimAI.Communication.Relations.Serialization;

namespace Ustas.RimAI.Communication.Relations.Config
{
    /// <summary>
    /// Dependencies: prompt domain file catalog and legacy migration report DTOs.
    /// Responsibility: publish the latest legacy prompt import report to memory, disk, and Player.log.
    /// </summary>
        internal static class PromptLegacyCompatMigrationReporting
    {

        internal const string ReportFolderName = "Reports";
        internal const string ReportFileName = "LegacyPromptMigrationReport.json";
        internal static LegacyPromptMigrationReport latestReport = new LegacyPromptMigrationReport();

        public static LegacyPromptMigrationReport GetLatestReport()
        {
            return latestReport?.Clone() ?? new LegacyPromptMigrationReport();
        }

        internal static LegacyPromptMigrationReport CreateReport(string sourceId)
        {
            return new LegacyPromptMigrationReport
            {
                GeneratedAtUtc = DateTime.UtcNow.ToString("o"),
                SourceId = sourceId ?? string.Empty
            };
        }

        internal static void RecordImported(
            LegacyPromptMigrationReport report,
            string sourceId,
            string promptChannel,
            string sectionId,
            bool rewritten)
        {
            if (report == null)
            {
                return;
            }

            report.ImportedCount++;
            if (rewritten)
            {
                report.RewrittenCount++;
            }

            report.Entries.Add(new LegacyPromptMigrationEntry
            {
                SourceId = sourceId ?? string.Empty,
                PromptChannel = promptChannel ?? string.Empty,
                SectionId = sectionId ?? string.Empty,
                Status = rewritten ? "rewritten" : "imported",
                Detail = rewritten ? "Legacy template was rewritten to supported runtime variables." : "Legacy section was imported without rewrite.",
                FallbackApplied = false
            });
        }

        internal static void RecordRejected(
            LegacyPromptMigrationReport report,
            string sourceId,
            string promptChannel,
            string sectionId,
            string detail,
            bool fallbackApplied)
        {
            if (report == null)
            {
                return;
            }

            report.RejectedCount++;
            if (fallbackApplied)
            {
                report.DefaultedCount++;
            }

            report.Entries.Add(new LegacyPromptMigrationEntry
            {
                SourceId = sourceId ?? string.Empty,
                PromptChannel = promptChannel ?? string.Empty,
                SectionId = sectionId ?? string.Empty,
                Status = "rejected",
                Detail = detail ?? string.Empty,
                FallbackApplied = fallbackApplied
            });
        }

        internal static void PublishReport(LegacyPromptMigrationReport report)
        {
            if (report == null)
            {
                return;
            }

            latestReport = report.Clone();
            if (report.Entries.Count == 0)
            {
                return;
            }

            string path = PromptLegacyCompatMigration.GetReportPath();
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = PromptDomainJsonUtility.Serialize(report, prettyPrint: true) ?? "{}";
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimAI.Relations] Failed to write legacy prompt migration report: {ex.Message}");
            }

            Log.Message(
                $"[RimAI.Relations] Legacy prompt migration report updated: source={report.SourceId}, imported={report.ImportedCount}, rewritten={report.RewrittenCount}, rejected={report.RejectedCount}, defaulted={report.DefaultedCount}.");
        }

        internal static string GetReportPath()
        {
            string root = PromptDomainFileCatalog.GetCustomPath("noop.txt");
            string customDirectory = Path.GetDirectoryName(root) ?? string.Empty;
            string promptDirectory = Directory.GetParent(customDirectory)?.FullName ?? customDirectory;
            return Path.Combine(promptDirectory, ReportFolderName, ReportFileName);
        }
        }

}
