# Quickstart

## 1. Start the collector

```bash
bash cli.sh telemetry start
bash cli.sh telemetry status
```

The collector listens on `127.0.0.1:9450` and stores events on disk with a
retention cap. It reads back what it stored: a write-only pipeline answers
nothing when something breaks.

## 2. Send an event from your app

```python
from b4n1_telemetry import init

telemetry = init(url="http://127.0.0.1:9450", app="demo")
telemetry.capture("error", "boom", module="api")
telemetry.flush()      # counts what is still in flight; never claims success early
```

## 3. Read it back

```bash
bash cli.sh telemetry query --app demo --limit 10
bash cli.sh telemetry issues
bash cli.sh telemetry trace <correlation_id>
```

## 4. Point a deployment at it

Set `B4N1_TELEMETRY_URL` and `B4N1_TELEMETRY_TOKEN` in the app's environment.
With tokens configured, a token writes only for its own app and reads only its
own events: a cross-app read is a `403`, not a silent leak.
