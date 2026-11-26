using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace B4n1.Telemetry
{
    /// <summary>
    /// Drop-in integration for .NET.
    ///
    /// <code>
    /// Telemetry.Init(new Options { App = "api" });
    /// Telemetry.InstallUncaughtHandlers();
    /// </code>
    ///
    /// <para>After that every captured error, every unhandled exception and
    /// every unobserved task fault is reported with no per-call-site code.</para>
    /// </summary>
    public static class Telemetry
    {
        private static readonly AsyncLocal<string> Correlation = new AsyncLocal<string>();
        private static readonly object Gate = new object();
        private static Client _client;
        private static bool _handlersInstalled;

        /// <summary>Install the package-level client. A second call keeps the first.</summary>
        public static Client Init(Options options = null)
        {
            lock (Gate)
            {
                if (_client == null)
                {
                    _client = new Client(options);
                }

                return _client;
            }
        }

        public static Client Instance
        {
            get { return _client; }
        }

        public static bool Capture(string message, string level = "error", string component = "",
            IDictionary<string, string> data = null)
        {
            var client = _client;
            return client != null && client.Capture(level, message, component, data);
        }

        public static bool CaptureException(Exception error, string component = "")
        {
            var client = _client;
            return client != null && client.CaptureException(error, component);
        }

        public static bool Flush(TimeSpan budget)
        {
            var client = _client;
            return client == null || client.Flush(budget);
        }

        public static void Shutdown()
        {
            lock (Gate)
            {
                if (_client != null)
                {
                    _client.Dispose();
                    _client = null;
                }
            }
        }

        /// <summary>
        /// Correlation id for the work in progress. <see cref="AsyncLocal{T}"/>
        /// is what keeps it attached across awaits without leaking one request's
        /// id into another.
        /// </summary>
        public static string CorrelationId
        {
            get { return Correlation.Value; }
            set { Correlation.Value = value; }
        }

        /// <summary>
        /// Report unhandled exceptions and unobserved task faults. Without this
        /// the errors that matter most — the ones that killed the process —
        /// are the only ones the collector never hears about.
        /// </summary>
        public static void InstallUncaughtHandlers()
        {
            lock (Gate)
            {
                if (_handlersInstalled)
                {
                    return;
                }

                _handlersInstalled = true;
            }

            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var error = args.ExceptionObject as Exception;
                if (error == null)
                {
                    return;
                }

                var client = _client;
                if (client == null)
                {
                    return;
                }

                var data = new Dictionary<string, string> { ["error_type"] = error.GetType().Name };
                if (Correlation.Value != null)
                {
                    data["correlation_id"] = Correlation.Value;
                }

                client.Enqueue(Envelope.Build(AppName(), "fatal", Envelope.Describe(error), "uncaught",
                    data, Envelope.StackLines(error), Correlation.Value, null, null));
                // The process is going down: this is the last chance to send.
                client.Flush(TimeSpan.FromSeconds(2));
            };

            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                var client = _client;
                if (client != null)
                {
                    client.Enqueue(Envelope.Build(AppName(), "error", Envelope.Describe(args.Exception),
                        "unobserved_task",
                        new Dictionary<string, string> { ["error_type"] = args.Exception.GetType().Name },
                        Envelope.StackLines(args.Exception), Correlation.Value, null, null));
                }

                args.SetObserved();
            };
        }

        private static string AppName()
        {
            var configured = Environment.GetEnvironmentVariable("B4N1_TELEMETRY_APP");
            return string.IsNullOrEmpty(configured) ? "dotnet" : configured;
        }
    }
}
