package b4n1telemetry

import (
	"fmt"
	"os"
	"reflect"
	"runtime/debug"
	"strings"
	"time"
)

// Package-level integration: the whole setup is Init().
//
//	import telem "github.com/B4N1-com/b4n1-telemetry/sdks/go"
//	telem.Init(telem.Options{App: "api"})
//	defer telem.Close()
//
// After that Capture/CaptureError/Recover report to the collector without any
// per-call-site configuration.

var defaultClient *Client

// Init installs the package-level client. Calling it twice replaces the client
// instead of flowing events into two senders, which would double-count.
func Init(opts Options) *Client {
	if defaultClient != nil {
		defaultClient.Close()
	}
	defaultClient = New(opts)
	return defaultClient
}

func current() *Client { return defaultClient }

// Capture records one event through the package-level client.
func Capture(level, message, module string, data map[string]string) bool {
	if current() == nil {
		return false
	}
	return current().Capture(level, message, module, data)
}

// CaptureError records an error through the package-level client.
func CaptureError(err error, module string) bool {
	if current() == nil {
		return false
	}
	return current().CaptureError(err, module)
}

// Flush drains the package-level queue. Returns true when everything landed.
func Flush(deadline time.Duration) bool {
	if current() == nil {
		return true
	}
	return current().Flush(deadline)
}

// Close flushes and shuts the package-level client down.
func Close() {
	if current() == nil {
		return
	}
	current().Close()
	defaultClient = nil
}

// Recover runs fn and reports a panic as a fatal event with the real stack,
// then re-panics so the process still dies the way its caller expects.
//
//	defer telem.Recover(telem.Safe("worker"))
//	work()
func Recover(module string) {
	if recovered := recover(); recovered != nil {
		if current() != nil {
			message := fmt.Sprintf("panic: %v", recovered)
			stack := strings.Split(strings.TrimRight(string(debug.Stack()), "\n"), "\n")
			current().send(newEnvelope(current().app, "fatal", message, module,
				map[string]string{"error_type": errorType(recovered)}, stack,
				nil, nil, current().release))
			current().Flush(2 * time.Second)
		}
		panic(recovered)
	}
}

// errorType is the value's type name, so a dashboard can group
// "*url.Error" separately from "ValueError"-style classes in other languages.
func errorType(value any) string {
	switch typed := value.(type) {
	case nil:
		return "nil"
	case *os.PathError:
		return "*os.PathError"
	case error:
		return reflect.TypeOf(typed).String()
	default:
		return reflect.TypeOf(typed).String()
	}
}
