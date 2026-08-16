using System;
using System.Collections.Generic;
using System.IO;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Diagnostics;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Persistence
{
    /// <summary>
    /// Responsibility: cache prompt file last-write timestamps to avoid per-tick disk IO.
    /// Uses FileSystemWatcher for instant invalidation + periodic polling as fallback.
    /// </summary>
    public sealed class PromptFileStampCache : IDisposable
    {
        private const int CacheValidityTicks = 1500; // ~25 seconds at 60fps

        private long cachedStamp = -1;
        private int cachedAtTick = -1;
        private readonly object syncRoot = new object();
        private FileSystemWatcher watcher;
        private HashSet<string> trackedFilePaths;

        public PromptFileStampCache()
        {
            BuildTrackedPathSet();
            TryInitializeWatcher();
        }

        public long GetStamp(int currentTick)
        {
            lock (syncRoot)
            {
                if (cachedAtTick > 0 && currentTick - cachedAtTick < CacheValidityTicks)
                {
                    return cachedStamp;
                }

                using (PerfScope.Measure("PromptFileStamp.Compute"))
                    cachedStamp = ComputePromptFilesStampUtcTicks();
                cachedAtTick = currentTick;
                return cachedStamp;
            }
        }

        public void Prime(int currentTick)
        {
            lock (syncRoot)
            {
                if (cachedAtTick > 0)
                {
                    return;
                }

                cachedStamp = ComputePromptFilesStampUtcTicks();
                cachedAtTick = currentTick;
            }
        }

        public void Invalidate()
        {
            lock (syncRoot)
            {
                cachedAtTick = -1;
            }
        }

        public void Dispose()
        {
            if (watcher != null)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
                watcher = null;
            }
        }

        private void BuildTrackedPathSet()
        {
            trackedFilePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in EnumeratePromptFilePaths())
            {
                if (!string.IsNullOrWhiteSpace(path))
                {
                    trackedFilePaths.Add(Path.GetFullPath(path));
                }
            }
        }

        private void TryInitializeWatcher()
        {
            try
            {
                string promptDir = ResolvePromptDirectory();
                if (string.IsNullOrWhiteSpace(promptDir) || !Directory.Exists(promptDir))
                {
                    return;
                }

                watcher = new FileSystemWatcher(promptDir)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true
                };

                watcher.Changed += OnPromptFileChanged;
                watcher.Created += OnPromptFileChanged;
                watcher.Deleted += OnPromptFileChanged;
                watcher.Renamed += OnPromptFileChanged;
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimAI.Relations] PromptFileStampCache: FileSystemWatcher init failed, falling back to polling. {ex.Message}");
                watcher = null;
            }
        }

        private void OnPromptFileChanged(object sender, FileSystemEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.FullPath))
            {
                return;
            }

            string fullPath = Path.GetFullPath(e.FullPath);
            if (trackedFilePaths != null && trackedFilePaths.Contains(fullPath))
            {
                Invalidate();
            }
        }

        private static string ResolvePromptDirectory()
        {
            string samplePath = PromptDomainFileCatalog.GetDefaultPath(
                PromptDomainFileCatalog.SystemPromptDefaultFileName);
            string dir = Path.GetDirectoryName(samplePath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                dir = Path.GetDirectoryName(dir); // go up from Default/ to Prompt/
            }

            return dir;
        }

        private static long ComputePromptFilesStampUtcTicks()
        {
            long maxTicks = 0L;
            foreach (string path in EnumeratePromptFilePaths())
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    continue;
                }

                long ticks = File.GetLastWriteTimeUtc(path).Ticks;
                if (ticks > maxTicks)
                {
                    maxTicks = ticks;
                }
            }

            return maxTicks;
        }

        private static IEnumerable<string> EnumeratePromptFilePaths()
        {
            yield return PromptDomainFileCatalog.GetDefaultPath(PromptDomainFileCatalog.SystemPromptDefaultFileName);
            yield return PromptDomainFileCatalog.GetDefaultPath(PromptDomainFileCatalog.DiplomacyPromptDefaultFileName);
            yield return PromptDomainFileCatalog.GetDefaultPath(PromptDomainFileCatalog.SocialCirclePromptDefaultFileName);
            yield return PromptDomainFileCatalog.GetDefaultPath(PromptDomainFileCatalog.FactionPromptDefaultFileName);
            yield return PromptDomainFileCatalog.GetDefaultPath(PromptDomainFileCatalog.PromptUnifiedDefaultFileName);
            yield return PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.SystemPromptCustomFileName);
            yield return PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.DiplomacyPromptCustomFileName);
            yield return PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.SocialCirclePromptCustomFileName);
            yield return PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.FactionPromptCustomFileName);
            yield return PromptDomainFileCatalog.GetCustomPath(PromptDomainFileCatalog.PromptUnifiedCustomFileName);
        }
    }
}
