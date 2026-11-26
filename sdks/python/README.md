# b4n1-telemetry — Python SDK

Drop-in error reporting to your own collector. **Standard library only**: the
SDK adds no dependency to the application it instruments.

## Install

```bash
pip install b4n1-telemetry
```

## Wire it once

```python
import b4n1_telemetry

b4n1_telemetry.init(app="musicogram")
```

That single call installs:

- a root logging handler for **ERROR and above** (with the real traceback),
- `sys.excepthook` and `threading.excepthook` (uncaught exceptions, level `fatal`),
- a flush on interpreter exit.

Configuration comes from the call or the environment:

```bash
B4N1_TELEMETRY_URL=http://127.0.0.1:9450
B4N1_TELEMETRY_TOKEN=...        # optional bearer token
B4N1_TELEMETRY_APP=musicogram   # optional default app name
```

## Django

```python
# settings.py
import b4n1_telemetry
b4n1_telemetry.init(app="musicogram")

MIDDLEWARE = [
    "b4n1_telemetry.django.TelemetryMiddleware",
    ...,
]
```

The middleware gives every request a correlation id (honouring an incoming
`X-Correlation-Id` / `X-Request-Id`), returns it in the response, and records
5xx responses. Uncaught exceptions are captured by the logging handler Django
itself uses, so nothing is counted twice.

## Explicit capture

```python
b4n1_telemetry.capture("payment retried", level="warning",
                       module="payments", data={"attempt": 2})
try:
    charge()
except Exception as exc:
    b4n1_telemetry.capture_exception(exc, module="payments")
    raise
```

`b4n1_telemetry.flush()` waits for the queue to drain (tests, short-lived
scripts).

## Contract

- Sending is asynchronous and bounded (2048 events); overflow is counted, never
  blocks the application.
- A collector that is down **never** raises into the application: failures are
  counted in `client.stats` (`sent`, `failed`, `dropped`, `last_error`).
- `init()` is idempotent; `shutdown()` removes the handler and restores the
  previous exception hooks.

After a request fails, the whole chain is one lookup:

```bash
telemetry-server trace <correlation-id>
```
