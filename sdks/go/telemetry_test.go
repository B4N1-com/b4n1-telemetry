package b4n1telemetry

import (
	"encoding/json"
	"errors"
	"net/http"
	"net/http/httptest"
	"sync"
	"testing"
	"time"
)

// collector is a local double that speaks the collector's protocol and can
// answer 429 on demand, so the quota path is exercised, not mocked away.
type collector struct {
	mu       sync.Mutex
	batches  [][]Envelope
	requests []*http.Request
	statuses []int
}

// drop asks the double to close the connection without answering at all: the
// transport failure the single retry exists for.
const drop = -1

func newCollector(statuses ...int) (*collector, *httptest.Server) {
	c := &collector{statuses: statuses}
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		c.mu.Lock()
		defer c.mu.Unlock()
		status := http.StatusAccepted
		if len(c.statuses) > 0 {
			status = c.statuses[0]
			c.statuses = c.statuses[1:]
		}
		if status == drop {
			if hijacker, ok := w.(http.Hijacker); ok {
				if conn, _, err := hijacker.Hijack(); err == nil {
					_ = conn.Close()
				}
			}
			return
		}
		var batch []Envelope
		_ = json.NewDecoder(r.Body).Decode(&batch)
		c.requests = append(c.requests, r.Clone(r.Context()))
		// A refused batch stores nothing: recording it would fake a duplicate
		// that production never creates.
		if status < 300 {
			c.batches = append(c.batches, batch)
		}
		if status == http.StatusTooManyRequests {
			w.Header().Set("Retry-After", "1")
		}
		w.WriteHeader(status)
	}))
	return c, server
}

func (c *collector) events() []Event {
	c.mu.Lock()
	defer c.mu.Unlock()
	var out []Event
	for _, batch := range c.batches {
		for _, env := range batch {
			out = append(out, env.Event)
		}
	}
	return out
}

func TestCaptureSendsV1Envelope(t *testing.T) {
	fake, server := newCollector()
	defer server.Close()

	client := New(Options{URL: server.URL, App: "api", Token: "tkn", Release: "sha-1"})
	defer client.Close()

	if !client.Capture("error", "boom", "db", map[string]string{"attempt": "2"}) {
		t.Fatal("capture refused an empty queue")
	}
	if !client.Flush(2 * time.Second) {
		t.Fatal("flush reported pending events")
	}

	events := fake.events()
	if len(events) != 1 {
		t.Fatalf("want 1 event, got %d", len(events))
	}
	event := events[0]
	if event.App != "api" || event.Level != "error" || event.Message != "boom" {
		t.Fatalf("unexpected event: %+v", event)
	}
	if event.Data["attempt"] != "2" {
		t.Fatalf("data not carried: %+v", event.Data)
	}
	if event.Release != "sha-1" {
		t.Fatalf("release not carried: %q", event.Release)
	}
	if event.Backtrace == nil || event.Breadcrumbs == nil {
		t.Fatal("empty lists must serialize as [], never null")
	}
	fake.mu.Lock()
	auth := fake.requests[0].Header.Get("Authorization")
	fake.mu.Unlock()
	if auth != "Bearer tkn" {
		t.Fatalf("auth header = %q", auth)
	}
	if client.Stats().Sent != 1 {
		t.Fatalf("sent = %d", client.Stats().Sent)
	}
}

func TestBatchingSendsOneRequest(t *testing.T) {
	fake, server := newCollector()
	defer server.Close()

	client := New(Options{URL: server.URL, App: "batch", BatchSize: 50, FlushInterval: 20 * time.Millisecond})
	for i := 0; i < 5; i++ {
		client.Capture("info", "m", "", nil)
	}
	if !client.Flush(2 * time.Second) {
		t.Fatal("flush incomplete")
	}
	client.Close()

	if len(fake.batches) != 1 {
		t.Fatalf("five events must not mean five requests, got %d", len(fake.batches))
	}
	if len(fake.events()) != 5 {
		t.Fatalf("want 5 events, got %d", len(fake.events()))
	}
}

func TestThrottledBatchIsRetriedNotDropped(t *testing.T) {
	fake, server := newCollector(http.StatusTooManyRequests)
	defer server.Close()

	client := New(Options{URL: server.URL, App: "limited", FlushInterval: 10 * time.Millisecond})
	client.Capture("error", "kept", "", nil)
	// flush() waits out the pause and retries.
	if !client.Flush(5 * time.Second) {
		t.Fatal("event lost after 429")
	}
	stats := client.Stats()
	if stats.Throttled != 1 || stats.Sent != 1 || stats.Dropped != 0 {
		t.Fatalf("unexpected stats after 429: %+v", stats)
	}
	if len(fake.events()) != 1 || fake.events()[0].Message != "kept" {
		t.Fatalf("throttled event did not land: %+v", fake.events())
	}
	client.Close()
}

func TestFullQueueDropsAndCounts(t *testing.T) {
	client := New(Options{URL: "http://127.0.0.1:9", QueueSize: 3, FlushInterval: time.Hour})
	defer client.Close()

	for i := 0; i < 6; i++ {
		client.Capture("info", "m", "", nil)
	}
	stats := client.Stats()
	if stats.Dropped != 3 || stats.Queued != 3 {
		t.Fatalf("unbounded growth: %+v", stats)
	}
}

func TestRecoverReportsFatalAndRepanics(t *testing.T) {
	fake, server := newCollector()
	defer server.Close()
	client := Init(Options{URL: server.URL, App: "worker"})
	defer Close()

	func() {
		defer func() {
			if recovered := recover(); recovered == nil {
				t.Fatal("Recover must not swallow the panic")
			}
		}()
		func() {
			defer Recover("job")
			panic("kaboom")
		}()
	}()

	events := fake.events()
	if len(events) != 1 {
		t.Fatalf("want 1 fatal event, got %d", len(events))
	}
	if events[0].Level != "fatal" || events[0].Module != "job" {
		t.Fatalf("unexpected event: %+v", events[0])
	}
	if len(events[0].Backtrace) < 2 {
		t.Fatalf("the real stack must travel with the event: %+v", events[0].Backtrace)
	}
	if client.Stats().Sent != 1 {
		t.Fatalf("sent = %d", client.Stats().Sent)
	}
}

func TestDroppedConnectionIsRetriedNotLost(t *testing.T) {
	fake, server := newCollector(drop)
	defer server.Close()

	client := New(Options{URL: server.URL, App: "retry", FlushInterval: 10 * time.Millisecond})
	defer client.Close()
	client.Capture("error", "kept", "db", nil)

	if !client.Flush(5 * time.Second) {
		t.Fatal("batch lost after a dropped connection")
	}
	stats := client.Stats()
	if stats.Failed != 0 || stats.Sent != 1 {
		t.Fatalf("a missing answer must be retried, not counted: %+v", stats)
	}
	if len(fake.events()) != 1 || fake.events()[0].Message != "kept" {
		t.Fatalf("the batch did not land: %+v", fake.events())
	}
}

func TestCaptureErrorKeepsTheType(t *testing.T) {
	fake, server := newCollector()
	defer server.Close()
	client := New(Options{URL: server.URL, App: "api"})
	defer client.Close()

	client.CaptureError(errors.New("db down"), "store")
	client.Flush(2 * time.Second)

	events := fake.events()
	if len(events) != 1 || events[0].Message != "db down" {
		t.Fatalf("unexpected events: %+v", events)
	}
	if events[0].Data["error_type"] != "*errors.errorString" {
		t.Fatalf("error_type = %q", events[0].Data["error_type"])
	}
}
