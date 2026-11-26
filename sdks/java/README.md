# b4n1-telemetry for Java

Drop-in error reporting to your own collector. Zero dependencies: it is
`java.net.http`, in the JDK since 11, and the SDK targets Java 11 so an
application that has not moved to a newer JDK can still adopt it.

```java
Telemetry.init(Client.builder().app("api").build());
Telemetry.installUncaughtHandler();
```

That is the whole integration. After it:

- `Telemetry.capture(level, message, component, data)` records an explicit
  event.
- `Telemetry.captureException(error, component)` records an exception with its
  real stack.
- `installUncaughtHandler()` reports every uncaught exception as `fatal` and
  then hands it to the JVM's previous handler, so the process still dies the
  way its caller expects.
- `Telemetry.withCorrelation(id)` stamps one request's events; the id is a
  `ThreadLocal`, so request frameworks running on a pool do not mix ids.
- `Telemetry.flush(Duration)` drains the queue.

## Configuration

| Variable | Meaning |
|---|---|
| `B4N1_TELEMETRY_URL` | Collector base URL (default `http://127.0.0.1:9450`) |
| `B4N1_TELEMETRY_TOKEN` | Bearer token, when the collector requires one |
| `B4N1_TELEMETRY_APP` | Application name (default `java`) |
| `B4N1_TELEMETRY_RELEASE` | Deploy id, stamped on every event |

`Client.builder()` exposes the same values, plus queue size, batch size,
timeout and flush interval.

## What it does not do

Sending never blocks the application thread and never throws. A full queue
drops and counts (`Stats.dropped()`); a `429` puts the batch back at the head and
pauses for `Retry-After` instead of dropping data to a quota the producer never
chose.

## License

Business Source License 1.1 — see `LICENSE`.
