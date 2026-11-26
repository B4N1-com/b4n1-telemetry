using System;

namespace B4n1.Telemetry
{
    /// <summary>
    /// Client settings. The zero-configuration path reads the
    /// B4N1_TELEMETRY_URL / _TOKEN / _APP / _RELEASE environment variables.
    /// </summary>
    public sealed class Options
    {
        public string Url { get; set; }

        public string Token { get; set; }

        public string App { get; set; }

        public string Release { get; set; }

        /// <summary>Bound on queued events; a full queue drops and counts.</summary>
        public int QueueSize { get; set; } = 2048;

        public int BatchSize { get; set; } = 100;

        /// <summary>How often the background sender drains the queue.</summary>
        public TimeSpan FlushInterval { get; set; } = TimeSpan.FromMilliseconds(250);

        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(3);

        internal string ResolvedUrl()
        {
            var value = Url ?? Environment.GetEnvironmentVariable("B4N1_TELEMETRY_URL");
            return StripTrailingSlash(string.IsNullOrEmpty(value) ? Envelope.DefaultUrl : value);
        }

        internal string ResolvedToken()
        {
            var value = Token ?? Environment.GetEnvironmentVariable("B4N1_TELEMETRY_TOKEN");
            return string.IsNullOrEmpty(value) ? null : value;
        }

        internal string ResolvedApp()
        {
            var value = App ?? Environment.GetEnvironmentVariable("B4N1_TELEMETRY_APP");
            return string.IsNullOrEmpty(value) ? "dotnet" : value;
        }

        internal string ResolvedRelease()
        {
            var value = Release ?? Environment.GetEnvironmentVariable("B4N1_TELEMETRY_RELEASE");
            return string.IsNullOrEmpty(value) ? null : value;
        }

        private static string StripTrailingSlash(string value)
        {
            return value.TrimEnd('/');
        }
    }

    /// <summary>
    /// What the client did, so an application can see the SDK was working
    /// without reading logs. Failures are counted here, never thrown.
    /// </summary>
    public sealed class Stats
    {
        internal Stats(string url, int sent, int failed, int dropped, int throttled, int queued,
            string lastError)
        {
            Url = url;
            Sent = sent;
            Failed = failed;
            Dropped = dropped;
            Throttled = throttled;
            Queued = queued;
            LastError = lastError;
        }

        public string Url { get; }

        /// <summary>Batches accepted by the collector.</summary>
        public int Sent { get; }

        /// <summary>Batches rejected or that never arrived.</summary>
        public int Failed { get; }

        /// <summary>Events lost to a full queue — visible instead of silent.</summary>
        public int Dropped { get; }

        /// <summary>Batches the collector asked us to slow down.</summary>
        public int Throttled { get; }

        public int Queued { get; }

        /// <summary>The last failure, so "it did not work" is never all you get.</summary>
        public string LastError { get; }

        public override string ToString()
        {
            var summary = "url=" + Url + " sent=" + Sent + " failed=" + Failed + " dropped=" + Dropped
                          + " throttled=" + Throttled + " queued=" + Queued;
            return LastError == null ? summary : summary + " last_error=" + LastError;
        }
    }
}
