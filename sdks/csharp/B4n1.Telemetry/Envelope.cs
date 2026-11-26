using System;
using System.Collections.Generic;
using System.Globalization;

namespace B4n1.Telemetry
{
    /// <summary>
    /// Wire envelope, field for field the collector's TelemetryEvent.
    ///
    /// <para>Every optional field gets its defined zero value (null correlation
    /// id, empty lists) so a half-populated event never reaches the wire.</para>
    /// </summary>
    public static class Envelope
    {
        public const int Version = 1;
        public const string DefaultUrl = "http://127.0.0.1:9450";

        private static readonly string[] Levels = { "debug", "info", "warning", "error", "fatal" };

        /// <summary>RFC 3339 UTC with milliseconds — the form the collector parses.</summary>
        public static string NowIso()
        {
            return DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        }

        /// <summary>Build one envelope as a plain dictionary, ready for <see cref="Json"/>.</summary>
        public static IDictionary<string, object> Build(
            string app,
            string level = "error",
            string message = "",
            string component = "",
            IDictionary<string, string> data = null,
            IEnumerable<string> backtrace = null,
            string correlationId = null,
            IEnumerable<IDictionary<string, string>> breadcrumbs = null,
            string release = null)
        {
            if (Array.IndexOf(Levels, level) < 0)
            {
                throw new ArgumentException("unknown level " + level, nameof(level));
            }

            var eventBody = new Dictionary<string, object>
            {
                ["timestamp"] = NowIso(),
                ["app"] = app,
                ["level"] = level,
                ["message"] = message,
                ["module"] = component,
                ["data"] = Stringify(data),
                ["backtrace"] = new List<string>(backtrace ?? new string[0]),
                ["correlation_id"] = correlationId,
                ["breadcrumbs"] = new List<IDictionary<string, string>>(
                    breadcrumbs ?? new IDictionary<string, string>[0]),
            };

            // Omitted when unset so an event from an older producer stays valid.
            if (!string.IsNullOrEmpty(release))
            {
                eventBody["release"] = release;
            }

            return new Dictionary<string, object>
            {
                ["v"] = Version,
                ["event"] = eventBody,
            };
        }

        /// <summary>
        /// The wire carries strings only: 2 and "2" must group together on the
        /// dashboard instead of splitting one issue in two.
        /// </summary>
        public static IDictionary<string, string> Stringify(IDictionary<string, string> data)
        {
            var output = new Dictionary<string, string>();
            if (data == null)
            {
                return output;
            }

            foreach (var pair in data)
            {
                output[pair.Key] = pair.Value ?? string.Empty;
            }

            return output;
        }

        /// <summary><c>"InvalidOperationException: boom"</c> — the message every SDK uses.</summary>
        public static string Describe(Exception error)
        {
            return error.GetType().Name + ": " + (error.Message ?? string.Empty);
        }

        /// <summary>The real stack as one string per frame, like a Python traceback.</summary>
        public static List<string> StackLines(Exception error)
        {
            var lines = new List<string>();
            if (error == null)
            {
                return lines;
            }

            lines.Add(Describe(error));
            var stack = error.StackTrace ?? string.Empty;
            foreach (var line in stack.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.Length > 0)
                {
                    lines.Add(trimmed);
                }
            }

            return lines;
        }
    }
}
