# b4n1-telemetry for PHP

Drop-in error reporting to your own collector. No extensions required: it uses
PHP's stream wrapper, so a host without `curl` still reports.

## Install

```bash
composer require b4n1/b4n1-telemetry
```

```php
\B4n1\Telemetry\Telemetry::init(['app' => 'api']);
\B4n1\Telemetry\Telemetry::installErrorHandlers();
```

That is the whole integration. After it:

- `Telemetry::capture($message, component: 'billing', data: ['order' => '42'])`
  records an explicit event.
- `Telemetry::captureException($e, 'billing')` records a caught exception with
  its real backtrace, including the frame that created it.
- `installErrorHandlers()` turns uncaught exceptions, fatal errors and
  `E_USER_ERROR` into events — the errors a request never survives to report.
- The queue is flushed when the request ends.

## Configuration

| Variable | Meaning |
|---|---|
| `B4N1_TELEMETRY_URL` | Collector base URL (default `http://127.0.0.1:9450`) |
| `B4N1_TELEMETRY_TOKEN` | Bearer token, when the collector requires one |
| `B4N1_TELEMETRY_APP` | Application name (default `php`) |
| `B4N1_TELEMETRY_RELEASE` | Deploy id, stamped on every event |

## What it does not do

PHP's request model has no background thread, so events are sent at the end of
the request; the queue is bounded, so a flood is dropped and counted instead of
turning into unbounded memory. Nothing here throws into your application.

## License

Business Source License 1.1 — see `LICENSE`.
