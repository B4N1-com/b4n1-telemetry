using System;
using System.Collections.Generic;
using System.Threading;

namespace B4n1.Telemetry
{
    /// <summary>
    /// Bounded queue drained by a background sender.
    ///
    /// <para>Sending never blocks the application and never throws: a telemetry
    /// client that breaks its host turns a bug report into an outage. Failures
    /// are counted in <see cref="Stats"/>; the one exception is a 429, where the
    /// batch goes back to the head of the queue and sending pauses for
    /// Retry-After — retrying at once earns another 429, and dropping it would
    /// lose data to a quota the producer never chose.</para>
    /// </summary>
    public sealed partial class Client : IDisposable
    {
        private readonly string _url;
        private readonly string _app;
        private readonly string _release;
        private readonly int _queueSize;
        private readonly int _batchSize;
        private readonly Transport _transport;
        private readonly Queue<IDictionary<string, object>> _queue =
            new Queue<IDictionary<string, object>>();
        private readonly object _gate = new object();
        private readonly Timer _sender;

        private int _sent;
        private int _failed;
        private int _dropped;
        private int _throttled;
        private int _inFlight;
        private string _lastError;
        private DateTime _pausedUntil = DateTime.MinValue;
        private bool _closed;

        public Client(Options options)
        {
            var settings = options ?? new Options();
            _url = settings.ResolvedUrl();
            _app = settings.ResolvedApp();
            _release = settings.ResolvedRelease();
            _queueSize = settings.QueueSize <= 0 ? 2048 : settings.QueueSize;
            _batchSize = settings.BatchSize <= 0 ? 100 : settings.BatchSize;
            _transport = new Transport(_url, settings.ResolvedToken(), settings.Timeout);

            var interval = settings.FlushInterval <= TimeSpan.Zero
                ? TimeSpan.FromMilliseconds(250)
                : settings.FlushInterval;
            _sender = new Timer(_ => Flush(TimeSpan.FromSeconds(2)), null, interval, interval);
        }

        /// <summary>Enqueue one event. False when the queue is full (counted).</summary>
        public bool Capture(string level, string message, string component = "",
            IDictionary<string, string> data = null)
        {
            return Enqueue(Envelope.Build(_app, level, message, component, data, null, null, null, _release));
        }

        /// <summary>Report an exception at level "error", with its real stack.</summary>
        public bool CaptureException(Exception error, string component = "")
        {
            if (error == null)
            {
                return false;
            }

            var data = new Dictionary<string, string> { ["error_type"] = error.GetType().Name };
            return Enqueue(Envelope.Build(_app, "error", Envelope.Describe(error), component, data,
                Envelope.StackLines(error), null, null, _release));
        }

        public bool Enqueue(IDictionary<string, object> envelope)
        {
            lock (_gate)
            {
                if (_closed)
                {
                    return false;
                }

                if (_queue.Count >= _queueSize)
                {
                    _dropped++;
                    return false;
                }

                _queue.Enqueue(envelope);
                return true;
            }
        }

        public Stats Stats()
        {
            lock (_gate)
            {
                return new Stats(_url, _sent, _failed, _dropped, _throttled, _queue.Count, _lastError);
            }
        }

        public void Dispose()
        {
            Flush(TimeSpan.FromSeconds(2));
            lock (_gate)
            {
                _closed = true;
            }

            _sender.Dispose();
            _transport.Dispose();
        }

        private List<IDictionary<string, object>> Take()
        {
            lock (_gate)
            {
                var batch = new List<IDictionary<string, object>>();
                if (_closed || _queue.Count == 0 || DateTime.UtcNow < _pausedUntil)
                {
                    return batch;
                }

                while (batch.Count < _batchSize && _queue.Count > 0)
                {
                    batch.Add(_queue.Dequeue());
                }

                _inFlight += batch.Count;
                return batch;
            }
        }

        private void Settle(List<IDictionary<string, object>> batch, Delivery delivery)
        {
            lock (_gate)
            {
                _inFlight -= batch.Count;
                if (delivery.Status >= 200 && delivery.Status < 300)
                {
                    _sent++;
                }
                else if (delivery.Status == 429)
                {
                    _throttled++;
                    var seconds = delivery.RetryAfterSeconds > 0 ? delivery.RetryAfterSeconds : 1;
                    _pausedUntil = DateTime.UtcNow.AddSeconds(seconds);
                    _lastError = "HTTP 429 (retry in " + seconds + "s)";
                    Requeue(batch);
                }
                else if (delivery.Status > 0)
                {
                    _failed++;
                    _lastError = "HTTP " + delivery.Status;
                }
                else
                {
                    _failed++;
                    _lastError = delivery.Error;
                }
            }
        }

        /// <summary>The refused batch is the oldest: it goes back first, then what fits.</summary>
        private void Requeue(List<IDictionary<string, object>> batch)
        {
            var kept = new List<IDictionary<string, object>>();
            while (_queue.Count > 0 && batch.Count + kept.Count < _queueSize)
            {
                kept.Add(_queue.Dequeue());
            }

            _dropped += _queue.Count;
            _queue.Clear();
            foreach (var envelope in batch)
            {
                _queue.Enqueue(envelope);
            }

            foreach (var envelope in kept)
            {
                _queue.Enqueue(envelope);
            }
        }
    }
}
