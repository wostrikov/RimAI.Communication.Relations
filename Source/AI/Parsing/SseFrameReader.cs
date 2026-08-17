using System;
using System.Collections.Generic;

namespace Ustas.RimAI.Communication.Relations.AI
{
    /// <summary>
    /// Protocol-only SSE framing. Does not know provider envelopes or Relations contracts.
    /// </summary>
    public readonly struct SseFrame
    {
        public SseFrame(string eventName, string id, string data, bool isDone)
        {
            EventName = eventName ?? string.Empty;
            Id = id ?? string.Empty;
            Data = data ?? string.Empty;
            IsDone = isDone;
        }

        public string EventName { get; }
        public string Id { get; }
        public string Data { get; }
        public bool IsDone { get; }
    }

    public static class SseFrameReader
    {
        public static bool LooksLikeSse(string payload)
        {
            return !string.IsNullOrEmpty(payload) &&
                   payload.IndexOf("data:", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Current Relations production path: each <c>data:</c> line is an independent payload.
        /// Empty data and <c>[DONE]</c> are skipped. Other fields are ignored.
        /// </summary>
        public static List<string> EnumerateDataPayloads(string payload)
        {
            var payloads = new List<string>();
            if (string.IsNullOrWhiteSpace(payload))
            {
                return payloads;
            }

            string[] lines = payload.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i] ?? string.Empty;
                if (!TryReadField(line, out string name, out string value))
                {
                    continue;
                }

                if (!string.Equals(name, "data", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(value) ||
                    string.Equals(value, "[DONE]", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                payloads.Add(value);
            }

            return payloads;
        }

        /// <summary>
        /// Spec-oriented event assembly: blank-line boundaries, concatenated data lines,
        /// comments ignored, <c>[DONE]</c> marked. Used for tests and future stream owners.
        /// </summary>
        public static List<SseFrame> ReadEvents(string payload)
        {
            var events = new List<SseFrame>();
            if (string.IsNullOrEmpty(payload))
            {
                return events;
            }

            string[] lines = payload.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            string eventName = string.Empty;
            string id = string.Empty;
            var dataLines = new List<string>();
            bool sawField = false;

            void Flush(bool trailingIncomplete)
            {
                if (!sawField && dataLines.Count == 0)
                {
                    eventName = string.Empty;
                    id = string.Empty;
                    return;
                }

                string data = string.Join("\n", dataLines);
                bool isDone = string.Equals(data.Trim(), "[DONE]", StringComparison.OrdinalIgnoreCase);
                events.Add(new SseFrame(eventName, id, data, isDone));
                eventName = string.Empty;
                id = string.Empty;
                dataLines.Clear();
                sawField = false;
            }

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i] ?? string.Empty;
                if (line.Length == 0)
                {
                    Flush(false);
                    continue;
                }

                if (line[0] == ':')
                {
                    continue;
                }

                if (!TryReadField(line, out string name, out string value))
                {
                    continue;
                }

                sawField = true;
                if (string.Equals(name, "event", StringComparison.OrdinalIgnoreCase))
                {
                    eventName = value;
                }
                else if (string.Equals(name, "id", StringComparison.OrdinalIgnoreCase))
                {
                    id = value;
                }
                else if (string.Equals(name, "data", StringComparison.OrdinalIgnoreCase))
                {
                    dataLines.Add(value);
                }
            }

            if (sawField || dataLines.Count > 0)
            {
                Flush(true);
            }

            return events;
        }

        static bool TryReadField(string line, out string name, out string value)
        {
            name = string.Empty;
            value = string.Empty;
            if (string.IsNullOrEmpty(line))
            {
                return false;
            }

            string trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed[0] == ':')
            {
                return false;
            }

            int colon = trimmed.IndexOf(':');
            if (colon < 0)
            {
                return false;
            }

            name = trimmed.Substring(0, colon).Trim();
            value = colon + 1 < trimmed.Length ? trimmed.Substring(colon + 1).Trim() : string.Empty;
            return !string.IsNullOrEmpty(name);
        }
    }
}
