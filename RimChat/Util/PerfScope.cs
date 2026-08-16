using System;
using System.Diagnostics;

namespace Ustas.RimAI.Communication.Relations.Util
{
    /// <summary>
    /// Lightweight disposable perf probe. Logs via DebugLogger when the
    /// measured block exceeds <see cref="ThresholdMs"/>.
    /// Usage: using (PerfScope.Measure("MyMethod")) { ... }
    /// </summary>
    internal struct PerfScope : IDisposable
    {
        private const double ThresholdMs = 2.0;

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
                DebugLogger.WarningGated($"[Perf] {label}: {ms:F1}ms");
            }
        }
    }
}
