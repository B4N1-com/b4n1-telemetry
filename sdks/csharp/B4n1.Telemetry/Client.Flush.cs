using System;
using System.Collections.Generic;
using System.Threading;

namespace B4n1.Telemetry
{
    /// <summary>
    /// The flush path, kept in its own file: a client that both queues and
    /// drains fits in one readable file only if each half has one.
    /// </summary>
    public sealed partial class Client
    {
        /// <summary>Wait before re-sending a request that never got an answer.</summary>
        private const int RetryPauseMilliseconds = 100;

        /// <summary>
        /// Sends everything queued. True when nothing is pending afterwards; an
        /// event already in flight counts as pending, because reporting
        /// "flushed" while the POST is open is how a caller loses data.
        /// </summary>
        public bool Flush(TimeSpan budget)
        {
            var deadline = DateTime.UtcNow + budget;
            while (true)
            {
                var batch = Take();
                if (batch.Count > 0)
                {
                    Settle(batch, Deliver(batch));
                    continue;
                }

                if (Pending() == 0)
                {
                    return true;
                }

                if (DateTime.UtcNow > deadline)
                {
                    return false;
                }

                Thread.Sleep(10);
            }
        }

        /// <summary>
        /// One POST, plus one retry when no answer arrived at all: a timeout
        /// against a healthy collector means the collector is fsyncing, and
        /// failing there loses the batch. An HTTP answer is never retried here
        /// (a 429 has its own requeue path, a 4xx would repeat the mistake).
        /// </summary>
        private Delivery Deliver(List<IDictionary<string, object>> batch)
        {
            var delivery = _transport.Send(batch);
            if (delivery.Status >= 0)
            {
                return delivery;
            }

            Thread.Sleep(RetryPauseMilliseconds);
            return _transport.Send(batch);
        }

        public int Pending()
        {
            lock (_gate)
            {
                return _queue.Count + _inFlight;
            }
        }

    }
}
