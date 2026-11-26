# b4n1-telemetry for Go

Drop-in error reporting to your own collector. Standard library only: the SDK
adds no dependency to your `go.mod`.

## Install

```bash
go get github.com/B4N1-com/b4n1-telemetry/sdks/go
```

```go
import telem "github.com/B4N1-com/b4n1-telemetry/sdks/go"

func main() {
    telem.Init(telem.Options{App: "api"})
    defer telem.Close()

    // ...
}
```

That is the whole integration. After it:

- `telem.Capture(level, message, module, data)` records an explicit event.
- `telem.CaptureError(err, module)` records a returned error.
- `telem.Recover(module)` in a `defer` turns a panic into a `fatal` event with
  its real stack, then re-panics so the process still dies as expected.
- `telem.Flush(deadline)` drains the queue; `telem.Close()` flushes and stops.

## Configuration

Zero configuration reads the environment:

| Variable | Meaning |
|---|---|
| `B4N1_TELEMETRY_URL` | Collector base URL (default `http://127.0.0.1:9450`) |
| `B4N1_TELEMETRY_TOKEN` | Bearer token, when the collector requires one |
| `B4N1_TELEMETRY_APP` | Application name (default `go`) |
| `B4N1_TELEMETRY_RELEASE` | Deploy id, stamped on every event |

The same values are available as `Options` fields when you need them in code.

## What it does not do

It never blocks your goroutine on the network, never panics, and never lies:
a full queue drops and counts (`Stats().Dropped`), a rejected batch counts as
`Failed`, and a `429` puts the batch back at the head of the queue and pauses
for `Retry-After` instead of dropping data to a quota you did not choose.

## License

Business Source License 1.1 — see `LICENSE`.
