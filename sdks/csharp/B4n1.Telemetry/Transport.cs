using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace B4n1.Telemetry
{
    /// <summary>Outcome of one POST: the status, and the 429 wait when there is one.</summary>
    internal struct Delivery
    {
        public int Status;
        public int RetryAfterSeconds;
        public string Error;
    }

    /// <summary>
    /// The POST itself, isolated so the queue and the retry policy stay
    /// readable on their own. This is the only place that knows HTTP exists.
    /// </summary>
    internal sealed class Transport : IDisposable
    {
        private readonly string _url;
        private readonly HttpClient _http;

        public Transport(string url, string token, TimeSpan timeout)
        {
            _url = url;
            _http = new HttpClient { Timeout = timeout };
            if (token != null)
            {
                _http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer " + token);
            }
        }

        public Delivery Send(IList<IDictionary<string, object>> batch)
        {
            var delivery = new Delivery();
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, _url + "/api/v1/events"))
                {
                    request.Content = new StringContent(
                        Json.EncodeBatch(batch), Encoding.UTF8, "application/json");
                    using (var response = _http.SendAsync(request).GetAwaiter().GetResult())
                    {
                        delivery.Status = (int)response.StatusCode;
                        delivery.RetryAfterSeconds = RetryAfter(response);
                    }
                }
            }
            catch (Exception failure)
            {
                delivery.Status = -1;
                delivery.Error = failure.GetType().Name + ": " + failure.Message;
            }

            return delivery;
        }

        public void Dispose()
        {
            _http.Dispose();
        }

        private static int RetryAfter(HttpResponseMessage response)
        {
            var header = response.Headers.RetryAfter;
            if (header == null)
            {
                return 0;
            }

            if (header.Delta.HasValue && header.Delta.Value.TotalSeconds > 0)
            {
                return (int)Math.Ceiling(header.Delta.Value.TotalSeconds);
            }

            if (header.Date.HasValue)
            {
                var seconds = (header.Date.Value - DateTimeOffset.UtcNow).TotalSeconds;
                return seconds > 0 ? (int)Math.Ceiling(seconds) : 0;
            }

            return 0;
        }
    }
}
