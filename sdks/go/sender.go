package b4n1telemetry

import (
	"bytes"
	"encoding/json"
	"net/http"
	"strconv"
	"time"
)

// loop is the background sender: it flushes on the ticker until Close.
func (c *Client) loop() {
	defer c.wg.Done()
	for {
		select {
		case <-c.ticker.C:
			c.Flush(2 * time.Second)
		case <-c.done:
			return
		}
	}
}

// retryPause is the wait before re-sending a batch that never got an answer.
const retryPause = 100 * time.Millisecond

// attempt performs one POST. A returned error means no HTTP answer at all.
func (c *Client) attempt(body []byte) (*http.Response, error) {
	req, err := http.NewRequest(http.MethodPost, c.url+"/api/v1/events", bytes.NewReader(body))
	if err != nil {
		return nil, err
	}
	req.Header.Set("Content-Type", "application/json")
	if c.token != "" {
		req.Header.Set("Authorization", "Bearer "+c.token)
	}
	return c.http.Do(req)
}

func (c *Client) post(batch []Envelope) {
	defer c.settle(len(batch))
	body, err := json.Marshal(batch)
	if err != nil {
		c.fail("marshal: " + err.Error())
		return
	}
	resp, err := c.attempt(body)
	if err != nil {
		// One retry, and only for a missing answer: a timeout against a healthy
		// collector means the collector is fsyncing, and dropping the batch
		// there is silent loss. An HTTP answer is never retried — a 4xx would
		// just repeat, and a 429 has its own requeue path.
		time.Sleep(retryPause)
		resp, err = c.attempt(body)
	}
	if err != nil {
		c.fail(err.Error())
		return
	}
	defer resp.Body.Close()

	c.mu.Lock()
	defer c.mu.Unlock()
	switch {
	case resp.StatusCode >= 200 && resp.StatusCode < 300:
		c.sent++
	case resp.StatusCode == http.StatusTooManyRequests:
		// Put the batch back at the head: it is the oldest and must not be
		// lost to a quota the producer never chose.
		c.throttled++
		seconds := 1
		if value, err := strconv.Atoi(resp.Header.Get("Retry-After")); err == nil && value > 0 {
			seconds = value
		}
		c.pausedTo = time.Now().Add(time.Duration(seconds) * time.Second)
		c.queue = append(batch, c.queue...)
		c.lastErr = "HTTP 429 (retry in " + strconv.Itoa(seconds) + "s)"
	default:
		c.failed++
		c.lastErr = "HTTP " + resp.Status
	}
}

func (c *Client) fail(message string) {
	c.mu.Lock()
	defer c.mu.Unlock()
	c.failed++
	c.lastErr = message
}

// settle marks a batch as no longer in flight.
func (c *Client) settle(n int) {
	c.mu.Lock()
	defer c.mu.Unlock()
	c.inFlight -= n
}
