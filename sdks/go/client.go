package b4n1telemetry

import (
	"net/http"
	"os"
	"strings"
	"sync"
	"time"
)

// Options configures a Client. The zero value is usable: it reads the
// B4N1_TELEMETRY_* environment variables and sends to DefaultURL.
type Options struct {
	URL           string
	Token         string
	App           string
	Release       string
	QueueSize     int
	BatchSize     int
	FlushInterval time.Duration
	Timeout       time.Duration
	// HTTP is the transport. Left nil it is a normal client; set it to route
	// through a proxy or a custom root CA.
	HTTP *http.Client
}

func envOr(key, fallback string) string {
	if value := os.Getenv(key); value != "" {
		return value
	}
	return fallback
}

// Stats is what the client did, so an application can see the SDK was working
// without reading logs. Failures are counted here, never raised.
type Stats struct {
	URL       string
	Sent      int
	Failed    int
	Dropped   int
	Throttled int
	Queued    int
	LastErr   string
}

// Client batches events and sends them in the background.
//
// Sending never blocks the application and never panics: an observability
// client that breaks its host turns a bug report into an outage. A full queue
// drops and counts; only a 429 pauses the sender, because the collector is
// asking for time, not reporting a bad payload.
type Client struct {
	url     string
	token   string
	app     string
	release string
	batch   int
	timeout time.Duration
	http    *http.Client

	mu        sync.Mutex
	queue     []Envelope
	capacity  int
	inFlight  int
	sent      int
	failed    int
	dropped   int
	throttled int
	lastErr   string
	pausedTo  time.Time
	closed    bool

	ticker *time.Ticker
	done   chan struct{}
	wg     sync.WaitGroup
}

// New builds a client and starts its background sender.
func New(opts Options) *Client {
	url := strings.TrimRight(opts.URL, "/")
	if url == "" {
		url = strings.TrimRight(envOr("B4N1_TELEMETRY_URL", DefaultURL), "/")
	}
	app := opts.App
	if app == "" {
		app = envOr("B4N1_TELEMETRY_APP", "go")
	}
	release := opts.Release
	if release == "" {
		release = os.Getenv("B4N1_TELEMETRY_RELEASE")
	}
	token := opts.Token
	if token == "" {
		token = os.Getenv("B4N1_TELEMETRY_TOKEN")
	}
	c := &Client{
		url:      url,
		token:    token,
		app:      app,
		release:  release,
		batch:    opts.BatchSize,
		timeout:  opts.Timeout,
		capacity: opts.QueueSize,
	}
	if c.batch <= 0 {
		c.batch = 100
	}
	if c.capacity <= 0 {
		c.capacity = 2048
	}
	if c.timeout <= 0 {
		c.timeout = 3 * time.Second
	}
	interval := opts.FlushInterval
	if interval <= 0 {
		interval = 250 * time.Millisecond
	}
	c.http = opts.HTTP
	if c.http == nil {
		c.http = &http.Client{Timeout: c.timeout}
	}
	c.ticker = time.NewTicker(interval)
	c.done = make(chan struct{})
	c.wg.Add(1)
	go c.loop()
	return c
}

// Capture enqueues one event. Returns false when the queue is full; the drop
// is counted in Stats so it is visible rather than silent.
func (c *Client) Capture(level, message, module string, data map[string]string) bool {
	return c.send(newEnvelope(c.app, level, message, module, data, nil, nil, nil, c.release))
}

// CaptureError enqueues an error at level "error", with its stack when the
// error carries one.
func (c *Client) CaptureError(err error, module string) bool {
	if err == nil {
		return false
	}
	return c.send(newEnvelope(c.app, "error", err.Error(), module,
		map[string]string{"error_type": errorType(err)}, nil, nil, nil, c.release))
}

func (c *Client) send(env Envelope) bool {
	c.mu.Lock()
	defer c.mu.Unlock()
	if c.closed {
		return false
	}
	if len(c.queue) >= c.capacity {
		c.dropped++
		return false
	}
	c.queue = append(c.queue, env)
	if len(c.queue) >= c.batch {
		go c.Flush(2 * time.Second)
	}
	return true
}

// Stats reports the counters. Safe to call while the sender is running.
func (c *Client) Stats() Stats {
	c.mu.Lock()
	defer c.mu.Unlock()
	return Stats{
		URL: c.url, Sent: c.sent, Failed: c.failed, Dropped: c.dropped,
		Throttled: c.throttled, Queued: len(c.queue), LastErr: c.lastErr,
	}
}

// Queued reports how many events are waiting to be sent.
func (c *Client) Queued() int {
	c.mu.Lock()
	defer c.mu.Unlock()
	return len(c.queue)
}

// Close flushes, stops the sender and refuses further events. Idempotent.
func (c *Client) Close() {
	c.Flush(2 * time.Second)
	c.mu.Lock()
	if c.closed {
		c.mu.Unlock()
		return
	}
	c.closed = true
	c.mu.Unlock()
	c.ticker.Stop()
	close(c.done)
	c.wg.Wait()
}
