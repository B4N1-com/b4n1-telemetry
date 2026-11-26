# b4n1-telemetry for .NET

Drop-in error reporting to your own collector. Zero package references: it
targets `netstandard2.0`, so the same library works on .NET Framework, .NET
Core and .NET 5+, and adds no JSON package to your project.

```csharp
using B4n1.Telemetry;

Telemetry.Init(new Options { App = "api" });
Telemetry.InstallUncaughtHandlers();
```

That is the whole integration. After it:

- `Telemetry.Capture(message, level, component, data)` records an explicit
  event.
- `Telemetry.CaptureException(error, component)` records an exception with its
  real stack.
- `InstallUncaughtHandlers()` reports unhandled exceptions as `fatal` and
  unobserved task faults as `error` — the two errors an application never gets
  to log itself.
- `Telemetry.CorrelationId` is an `AsyncLocal`, so it follows one request
  through `await` without leaking into another.
- `Telemetry.Flush(TimeSpan)` drains the queue.

## Configuration

| Variable | Meaning |
|---|---|
| `B4N1_TELEMETRY_URL` | Collector base URL (default `http://127.0.0.1:9450`) |
| `B4N1_TELEMETRY_TOKEN` | Bearer token, when the collector requires one |
| `B4N1_TELEMETRY_APP` | Application name (default `dotnet`) |
| `B4N1_TELEMETRY_RELEASE` | Deploy id, stamped on every event |

`Options` exposes the same values, plus queue size, batch size, timeout and
flush interval.

## What it does not do

Sending never blocks the application thread and never throws. A full queue
drops and counts (`Stats.Dropped`); a `429` puts the batch back at the head and
pauses for `Retry-After` instead of dropping data to a quota the producer never
chose. `Stats.LastError` says why the last attempt failed, so "it did not work"
is never all you get.

## License

Business Source License 1.1 — see `LICENSE`.
