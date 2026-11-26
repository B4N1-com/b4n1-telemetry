package b4n1telemetry

import "time"

// EnvelopeVersion is the wire version every SDK emits. The collector rejects
// anything else with a typed error instead of guessing at the shape.
const EnvelopeVersion = 1

// DefaultURL is the collector's default address (B4N1_TELEMETRY_URL wins).
const DefaultURL = "http://127.0.0.1:9450"

// Breadcrumb is one step recorded before an error, in order.
type Breadcrumb struct {
	Timestamp string `json:"timestamp"`
	Message   string `json:"message"`
	Category  string `json:"category"`
}

// Event is one telemetry event, field for field the collector's struct.
type Event struct {
	Timestamp     string            `json:"timestamp"`
	App           string            `json:"app"`
	Level         string            `json:"level"`
	Message       string            `json:"message"`
	Module        string            `json:"module"`
	Data          map[string]string `json:"data"`
	Backtrace     []string          `json:"backtrace"`
	CorrelationID *string           `json:"correlation_id"`
	Breadcrumbs   []Breadcrumb      `json:"breadcrumbs"`
	// Release answers "which deploy introduced this?". Omitted when unset so
	// an event from an older producer stays valid.
	Release string `json:"release,omitempty"`
}

// Envelope wraps an event with the wire version.
type Envelope struct {
	V     int   `json:"v"`
	Event Event `json:"event"`
}

// nowISO is the RFC 3339 UTC stamp the collector parses.
func nowISO() string {
	return time.Now().UTC().Format(time.RFC3339Nano)
}

// newEnvelope builds one event with the non-optional fields filled in. Level
// and message are the caller's; everything else has a defined zero value so a
// half-populated event never reaches the wire.
func newEnvelope(app, level, message, module string, data map[string]string, backtrace []string, correlationID *string, breadcrumbs []Breadcrumb, release string) Envelope {
	if level == "" {
		level = "error"
	}
	if data == nil {
		data = map[string]string{}
	}
	if backtrace == nil {
		backtrace = []string{}
	}
	if breadcrumbs == nil {
		breadcrumbs = []Breadcrumb{}
	}
	return Envelope{
		V: EnvelopeVersion,
		Event: Event{
			Timestamp:     nowISO(),
			App:           app,
			Level:         level,
			Message:       message,
			Module:        module,
			Data:          data,
			Backtrace:     backtrace,
			CorrelationID: correlationID,
			Breadcrumbs:   breadcrumbs,
			Release:       release,
		},
	}
}
