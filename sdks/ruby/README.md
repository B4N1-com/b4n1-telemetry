# b4n1-telemetry for Ruby

Drop-in error reporting to your own collector. No gems, no native extensions:
the SDK is Ruby's standard library.

## Install

```bash
gem install b4n1-telemetry
```

```ruby
require "b4n1_telemetry"

B4N1Telemetry.init(app: "api")
```

That is the whole integration — an `at_exit` hook flushes whatever is still
queued, so a process that ends normally does not lose its last events.

```ruby
B4N1Telemetry.capture("payment failed", level: "error", component: "billing", data: { "order" => "42" })
B4N1Telemetry.capture_exception(error, component: "billing")
B4N1Telemetry.correlation_id = "req-7"   # stamped on every event of the request
B4N1Telemetry.flush(2.0)
```

`component:` is the wire's `module` field. Ruby's `module` is a keyword, so the
option cannot be called that without losing the ability to pass it as a hash
key — the mapping is done in `envelope.rb`.

## Configuration

| Variable | Meaning |
|---|---|
| `B4N1_TELEMETRY_URL` | Collector base URL (default `http://127.0.0.1:9450`) |
| `B4N1_TELEMETRY_TOKEN` | Bearer token, when the collector requires one |
| `B4N1_TELEMETRY_APP` | Application name (default `ruby`) |
| `B4N1_TELEMETRY_RELEASE` | Deploy id, stamped on every event |

## What it does not do

It never raises into your application, never blocks a request on the network
for longer than the given timeout, and never hides a loss: a full queue drops
and counts, a `429` pauses for `Retry-After` and keeps the batch.

## License

Business Source License 1.1 — see `LICENSE`.
