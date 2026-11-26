package b4n1telemetry

import "time"

// Flush sends everything queued, retrying once after a 429 pause. It reports
// whether the queue is empty within the deadline: "flushed" while a batch is
// still open is how a caller loses events.
func (c *Client) Flush(deadline time.Duration) bool {
	stop := time.Now().Add(deadline)
	for {
		batch, wait := c.take()
		if len(batch) > 0 {
			c.post(batch)
			continue
		}
		// Nothing left and nothing in flight: the flush is done now, not at the
		// deadline. An event another goroutine is POSTing still counts as pending
		// — reporting "flushed" while the request is open is how a caller loses
		// events.
		if c.pending() == 0 {
			return true
		}
		if time.Now().After(stop) {
			return false
		}
		sleep := wait
		if sleep <= 0 || time.Now().Add(sleep).After(stop) {
			sleep = 10 * time.Millisecond
		}
		time.Sleep(sleep)
	}
}

func (c *Client) take() ([]Envelope, time.Duration) {
	c.mu.Lock()
	defer c.mu.Unlock()
	if len(c.queue) == 0 || c.closed {
		return nil, 0
	}
	if pause := time.Until(c.pausedTo); pause > 0 {
		return nil, pause
	}
	n := c.batch
	if n > len(c.queue) {
		n = len(c.queue)
	}
	batch := make([]Envelope, n)
	copy(batch, c.queue[:n])
	c.queue = c.queue[n:]
	c.inFlight += n
	return batch, 0
}

func (c *Client) pending() int {
	c.mu.Lock()
	defer c.mu.Unlock()
	return len(c.queue) + c.inFlight
}
