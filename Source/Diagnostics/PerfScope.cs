using System;
using System.Diagnostics;

namespace Ustas.RimAI.Communication.Relations.Diagnostics
{
    /// <summary>
    /// Lightweight disposable perf probe, reporting a block that took longer
    /// than <see cref="ThresholdMs"/>.
    /// Usage: using (PerfScope.Measure("MyMethod")) { ... }
    ///
    /// A timing is not a warning. This used to report through WarningGated, so
    /// "FactionIntel.RaidScan: 2.4ms" arrived in yellow and RimWorld popped the
    /// debug log open for it - an alarm whose text said nothing was wrong. The
    /// other WarningGated callers are real problems and keep that level.
    /// </summary>
    internal struct PerfScope : IDisposable
    {
        /// <summary>
        /// A tick at normal speed has about 16.6ms for the whole game, so a
        /// single scan of ours passing 25 has overrun a frame on its own and is
        /// worth looking at. The old bar was 2ms, which every ordinary scan
        /// clears, which is why this reported constantly.
        /// </summary>
        private const double ThresholdMs = 25.0;

        private readonly string label;
        private readonly Stopwatch sw;

        private PerfScope(string label)
        {
            this.label = label;
            this.sw = Stopwatch.StartNew();
        }

        public static PerfScope Measure(string label) => new PerfScope(label);

        public void Dispose()
        {
            sw.Stop();
            double ms = sw.Elapsed.TotalMilliseconds;
            if (ms >= ThresholdMs)
            {
                ModuleLog.Message($"[RimAI.Relations] [Perf] {label}: {ms:F1}ms");
            }
        }
    }
}
