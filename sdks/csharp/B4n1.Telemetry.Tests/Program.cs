using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using B4n1.Telemetry;

namespace B4n1.Telemetry.Tests
{
    /// <summary>
    /// Offline suite: a real local HTTP server, real envelopes, no network and
    /// no test framework (a package reference would need a feed).
    /// </summary>
    internal static class Program
    {
        private static int _failures;

        private static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "probe")
            {
                return Probe(args);
            }

            Console.WriteLine("csharp sdk");
            EnvelopeShape();
            CaptureAndFlush();
            Batching();
            Throttled();
            RetriedOnTransportFailure();
            BoundedQueue();
            ExceptionKeepsStack();
            UncaughtHandler();
            Console.WriteLine(_failures == 0 ? "csharp sdk: PASS" : "csharp sdk: " + _failures + " FAILED");
            return _failures == 0 ? 0 : 1;
        }

        /// <summary>
        /// Sends one event to a real collector, so the cross-language suite can
        /// prove this SDK speaks the same wire format as every other one.
        /// </summary>
        private static int Probe(string[] args)
        {
            var url = Environment.GetEnvironmentVariable("B4N1_PROBE_URL");
            var app = Environment.GetEnvironmentVariable("B4N1_PROBE_APP");
            var token = Environment.GetEnvironmentVariable("B4N1_PROBE_TOKEN");
            foreach (var arg in args)
            {
                if (arg.StartsWith("--url="))
                {
                    url = arg.Substring(6);
                }
                else if (arg.StartsWith("--app="))
                {
                    app = arg.Substring(6);
                }
                else if (arg.StartsWith("--token="))
                {
                    token = arg.Substring(8);
                }
            }

            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(app))
            {
                Console.Error.WriteLine("probe: B4N1_PROBE_URL and B4N1_PROBE_APP are required");
                return 2;
            }

            using (var client = new Client(new Options
            {
                Url = url, App = app, Token = token,
                // 10s, not the 3s default: a cold .NET process measured a
                // POST timeout while the collector was fsyncing a backlog,
                // and this probe is about the wire format, not the budget.
                Timeout = TimeSpan.FromSeconds(10),
                FlushInterval = TimeSpan.FromMilliseconds(20),
            }))
            {
                client.Capture("error", "hello-from-" + app, "probe",
                    new Dictionary<string, string> { ["lang"] = "csharp" });
                var flushed = client.Flush(TimeSpan.FromSeconds(20));
                var stats = client.Stats();
                Console.WriteLine("sent=" + stats.Sent + " failed=" + stats.Failed +
                                  " dropped=" + stats.Dropped +
                                  (stats.LastError == null ? "" : " last_error=" + stats.LastError));
                return flushed && stats.Sent > 0 ? 0 : 1;
            }
        }

        /// <summary>Local double that speaks the collector's protocol and can answer 429.</summary>
        private sealed class Collector : IDisposable
        {
            private readonly HttpListener _listener = new HttpListener();
            private readonly List<string> _bodies = new List<string>();
            private readonly List<string> _auth = new List<string>();
            private readonly Queue<int> _statuses = new Queue<int>();
            private readonly Thread _thread;

            public Collector(params int[] codes)
            {
                foreach (var code in codes)
                {
                    _statuses.Enqueue(code);
                }

                var port = FreePort();
                Url = "http://127.0.0.1:" + port;
                _listener.Prefixes.Add(Url + "/");
                _listener.Start();
                _thread = new Thread(Serve) { IsBackground = true };
                _thread.Start();
            }

            public string Url { get; }

            public List<string> Bodies()
            {
                lock (_bodies)
                {
                    return new List<string>(_bodies);
                }
            }

            public List<string> Auth()
            {
                lock (_bodies)
                {
                    return new List<string>(_auth);
                }
            }

            public void Dispose()
            {
                _listener.Stop();
                _listener.Close();
            }

            private static int FreePort()
            {
                var probe = new TcpListener(IPAddress.Loopback, 0);
                probe.Start();
                var port = ((IPEndPoint)probe.LocalEndpoint).Port;
                probe.Stop();
                return port;
            }

            private void Serve()
            {
                while (_listener.IsListening)
                {
                    HttpListenerContext context;
                    try
                    {
                        context = _listener.GetContext();
                    }
                    catch (Exception)
                    {
                        return;
                    }

                    var buffer = new byte[context.Request.ContentLength64 > 0
                        ? context.Request.ContentLength64
                        : 0];
                    var read = 0;
                    while (read < buffer.Length)
                    {
                        var chunk = context.Request.InputStream.Read(buffer, read, buffer.Length - read);
                        if (chunk <= 0)
                        {
                            break;
                        }

                        read += chunk;
                    }

                    var body = Encoding.UTF8.GetString(buffer, 0, read);
                    var status = _statuses.Count > 0 ? _statuses.Dequeue() : 202;
                    if (status < 300)
                    {
                        // A refused batch stores nothing: recording it would
                        // fake a duplicate production never creates.
                        lock (_bodies)
                        {
                            _bodies.Add(body);
                            _auth.Add(context.Request.Headers["Authorization"]);
                        }
                    }

                    if (status == 429)
                    {
                        context.Response.AddHeader("Retry-After", "1");
                    }

                    context.Response.StatusCode = status;
                    context.Response.Close();
                }
            }
        }

        private static void Check(string label, bool condition, string detail = "")
        {
            if (condition)
            {
                Console.WriteLine("  ok   " + label);
                return;
            }

            _failures++;
            Console.WriteLine("  FAIL " + label + (detail.Length == 0 ? "" : " — " + detail));
        }

        private static void EnvelopeShape()
        {
            var envelope = Envelope.Build("api", "error", "boom", "db",
                new Dictionary<string, string> { ["attempt"] = "2" }, null, null, null, "sha-1");
            Check("envelope is v1", (int)envelope["v"] == 1);
            var body = (IDictionary<string, object>)envelope["event"];
            Check("component maps to module", (string)body["module"] == "db");
            Check("release carried", (string)body["release"] == "sha-1");
            Check("timestamp is RFC3339 Z",
                System.Text.RegularExpressions.Regex.IsMatch((string)body["timestamp"],
                    @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$"));
            var withoutRelease = Envelope.Build("api", "info", "x");
            Check("release omitted when unset", !((IDictionary<string, object>)withoutRelease["event"]).ContainsKey("release"));
            Check("json escapes a quote", Json.Encode("a\"b") == "\"a\\\"b\"");
            Check("json encodes null correlation",
                Json.Encode(envelope).Contains("\"correlation_id\":null"));
            Check("json encodes empty backtrace", Json.Encode(envelope).Contains("\"backtrace\":[]"));
        }

        private static void CaptureAndFlush()
        {
            using (var fake = new Collector())
            {
                using (var client = new Client(new Options { Url = fake.Url, App = "api", Token = "tkn" }))
                {
                    client.Capture("error", "boom", "db", new Dictionary<string, string> { ["attempt"] = "2" });
                    Check("flush reports empty", client.Flush(TimeSpan.FromSeconds(2)));
                    Check("one request", fake.Bodies().Count == 1, "requests=" + fake.Bodies().Count);
                    Check("bearer token sent", fake.Auth().Count == 1 && fake.Auth()[0] == "Bearer tkn");
                    var body = fake.Bodies().Count == 0 ? "" : fake.Bodies()[0];
                    Check("body is a JSON array", body.StartsWith("[{") && body.EndsWith("}]"));
                    Check("message on the wire", body.Contains("\"message\":\"boom\""), body);
                    Check("app on the wire", body.Contains("\"app\":\"api\""));
                    Check("data on the wire", body.Contains("\"attempt\":\"2\""));
                    Check("stats sent = 1", client.Stats().Sent == 1);
                }
            }
        }

        private static void Batching()
        {
            using (var fake = new Collector())
            {
                using (var client = new Client(new Options
                {
                    Url = fake.Url, App = "batch", BatchSize = 50,
                    // A long interval keeps the sender out of the way: this
                    // test is about one flush carrying everything queued, and
                    // a timer firing mid-test would make requests a coin toss.
                    FlushInterval = TimeSpan.FromMinutes(1),
                }))
                {
                    for (var i = 0; i < 5; i++)
                    {
                        client.Capture("info", "m" + i);
                    }

                    Check("flush after batching", client.Flush(TimeSpan.FromSeconds(3)));
                    var body = fake.Bodies().Count == 0 ? "" : fake.Bodies()[0];
                    Check("five events, one request",
                        fake.Bodies().Count == 1 && body.Split(new[] { "\"v\":1" }, StringSplitOptions.None).Length == 6,
                        "requests=" + fake.Bodies().Count);
                }
            }
        }

        private static void Throttled()
        {
            using (var fake = new Collector(429))
            {
                using (var client = new Client(new Options
                {
                    Url = fake.Url, App = "limited", FlushInterval = TimeSpan.FromMinutes(1),
                }))
                {
                    client.Capture("error", "kept");
                    Check("429 retried to success", client.Flush(TimeSpan.FromSeconds(20)));
                    var stats = client.Stats();
                    Check("throttled counted", stats.Throttled == 1, stats.ToString());
                    Check("nothing dropped", stats.Dropped == 0, stats.ToString());
                    Check("event landed", fake.Bodies().Count == 1
                        && fake.Bodies()[0].Contains("\"message\":\"kept\""));
                }
            }
        }

        private static void RetriedOnTransportFailure()
        {
            using (var fake = new DroppingListener(1))
            using (var client = new Client(new Options
            {
                Url = fake.Url,
                App = "retry",
                Timeout = TimeSpan.FromSeconds(2),
                FlushInterval = TimeSpan.FromMinutes(1),
            }))
            {
                client.Capture("error", "kept", "db");
                Check("dropped connection retried to success",
                    client.Flush(TimeSpan.FromSeconds(10)), client.Stats().ToString());
                var stats = client.Stats();
                Check("missing answer is not counted as failure", stats.Failed == 0, stats.ToString());
                var bodies = fake.Bodies();
                Check("the retry delivered the batch",
                    bodies.Count == 1 && bodies[0].Contains("\"message\":\"kept\""),
                    "bodies=" + bodies.Count + " " + stats);
            }
        }

        /// <summary>
        /// Raw socket double: the first connection is closed without one byte of
        /// answer, the next one gets a canned 202.
        ///
        /// HttpListener cannot simulate this. Abort() still flushes a default
        /// 200, and a client that treated that as a delivered batch would pass a
        /// fake test — measured, which is why this double speaks sockets.
        /// </summary>
        private sealed class DroppingListener : IDisposable
        {
            private readonly TcpListener _listener;
            private readonly Thread _thread;
            private readonly List<string> _bodies = new List<string>();
            private int _drops;

            public DroppingListener(int drops)
            {
                _drops = drops;
                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start();
                Url = "http://127.0.0.1:" + ((IPEndPoint)_listener.LocalEndpoint).Port;
                _thread = new Thread(Serve) { IsBackground = true };
                _thread.Start();
            }

            public string Url { get; }

            public List<string> Bodies()
            {
                lock (_bodies)
                {
                    return new List<string>(_bodies);
                }
            }

            public void Dispose()
            {
                _listener.Stop();
            }

            private void Serve()
            {
                while (true)
                {
                    TcpClient accepted;
                    try
                    {
                        accepted = _listener.AcceptTcpClient();
                    }
                    catch (Exception)
                    {
                        return;
                    }

                    using (accepted)
                    using (var stream = accepted.GetStream())
                    {
                        if (Interlocked.Decrement(ref _drops) >= 0)
                        {
                            continue; // closed without answering: a stall
                        }

                        lock (_bodies)
                        {
                            _bodies.Add(ReadBody(stream));
                        }

                        var reply = Encoding.ASCII.GetBytes(
                            "HTTP/1.1 202 Accepted\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                        stream.Write(reply, 0, reply.Length);
                    }
                }
            }

            private static string ReadBody(Stream stream)
            {
                var head = new StringBuilder();
                var one = new byte[1];
                while (!head.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
                {
                    if (stream.Read(one, 0, 1) <= 0)
                    {
                        return head.ToString();
                    }

                    head.Append((char)one[0]);
                }

                var length = 0;
                foreach (var line in head.ToString().Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    {
                        int.TryParse(line.Substring(15).Trim(), out length);
                    }
                }

                var body = new byte[length];
                var read = 0;
                while (read < length)
                {
                    var chunk = stream.Read(body, read, length - read);
                    if (chunk <= 0)
                    {
                        break;
                    }

                    read += chunk;
                }

                return Encoding.UTF8.GetString(body, 0, read);
            }
        }

        private static void BoundedQueue()
        {
            var options = new Options
            {
                Url = "http://127.0.0.1:9", QueueSize = 3,
                FlushInterval = TimeSpan.FromMinutes(10),
            };
            // The sender thread is not started before the first interval, so the
            // queue keeps exactly what Capture enqueues.
            using (var client = new Client(options))
            {
                for (var i = 0; i < 6; i++)
                {
                    client.Capture("info", "m" + i);
                }

                var stats = client.Stats();
                Check("queue bounded", stats.Dropped == 3 && stats.Queued == 3, stats.ToString());
            }
        }

        private static void ExceptionKeepsStack()
        {
            using (var fake = new Collector())
            {
                using (var client = new Client(new Options { Url = fake.Url, App = "api" }))
                {
                    // Thrown on purpose: .NET only fills StackTrace when an
                    // exception is thrown, so a never-thrown instance would
                    // test a case no caller reports.
                    InvalidOperationException thrown;
                    try
                    {
                        throw new InvalidOperationException("db down");
                    }
                    catch (InvalidOperationException caught)
                    {
                        thrown = caught;
                    }

                    client.CaptureException(thrown, "store");
                    client.Flush(TimeSpan.FromSeconds(2));
                    var body = fake.Bodies().Count == 0 ? "" : fake.Bodies()[0];
                    Check("exception message", body.Contains("\"message\":\"InvalidOperationException: db down\""), body);
                    Check("error_type recorded", body.Contains("\"error_type\":\"InvalidOperationException\""));
                    Check("stack carried", body.Contains("at B4n1.Telemetry.Tests.Program.ExceptionKeepsStack"), body);
                    Check("never-thrown keeps the message line",
                        Envelope.StackLines(new ArgumentException("cold")).Count == 1);
                }
            }
        }

        private static void UncaughtHandler()
        {
            using (var fake = new Collector())
            {
                Telemetry.Init(new Options { Url = fake.Url, App = "worker" });
                var client = Telemetry.Instance;
                Check("init keeps the first client",
                    ReferenceEquals(Telemetry.Init(new Options { Url = fake.Url }), client));
                Telemetry.InstallUncaughtHandlers();
                Telemetry.CorrelationId = "corr-1";
                Check("correlation id set", Telemetry.CorrelationId == "corr-1");
                Telemetry.CaptureException(new ArgumentException("captured-through-facade"), "facade");
                client.Flush(TimeSpan.FromSeconds(2));
                var body = fake.Bodies().Count == 0 ? "" : fake.Bodies()[0];
                Check("facade reaches the client", body.Contains("\"message\":\"ArgumentException: captured-through-facade\""), body);
                Check("app from options", body.Contains("\"app\":\"worker\""));
                Telemetry.Shutdown();
            }
        }
    }
}
