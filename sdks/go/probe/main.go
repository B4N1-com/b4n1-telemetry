// Command probe sends one event to a real collector, so a cross-language suite
// can prove the Go SDK speaks the same wire format as every other SDK.
//
//	go run ./probe --url http://127.0.0.1:9450 --app probe-go
package main

import (
	"fmt"
	"os"
	"time"

	telem "github.com/B4N1-com/b4n1-telemetry/sdks/go"
)

func main() {
	url := os.Getenv("B4N1_PROBE_URL")
	app := os.Getenv("B4N1_PROBE_APP")
	token := os.Getenv("B4N1_PROBE_TOKEN")
	if url == "" || app == "" {
		fmt.Fprintln(os.Stderr, "probe: B4N1_PROBE_URL and B4N1_PROBE_APP are required")
		os.Exit(2)
	}
	for _, arg := range os.Args[1:] {
		switch {
		case len(arg) > 6 && arg[:6] == "--url=":
			url = arg[6:]
		case len(arg) > 6 && arg[:6] == "--app=":
			app = arg[6:]
		case len(arg) > 8 && arg[:8] == "--token=":
			token = arg[8:]
		}
	}

	client := telem.Init(telem.Options{
		URL: url, App: app, Token: token,
		// 10s, not the 3s default: this probe verifies the wire format, and a
		// cold process writing to a collector mid-fsync must not read as a bug.
		Timeout: 10 * time.Second,
	})
	client.Capture("error", "hello-from-"+app, "probe", map[string]string{"lang": "go"})
	if !client.Flush(20 * time.Second) {
		fmt.Fprintln(os.Stderr, "probe: flush did not drain the queue")
		os.Exit(1)
	}
	stats := client.Stats()
	client.Close()
	fmt.Printf("sent=%d failed=%d dropped=%d\n", stats.Sent, stats.Failed, stats.Dropped)
	if stats.Sent == 0 {
		os.Exit(1)
	}
}
