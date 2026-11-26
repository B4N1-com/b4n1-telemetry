# b4n1-telemetry — Node SDK

Drop-in error reporting to your own collector. **Zero dependencies**: core
modules only, so `npm install` never compiles anything and the SDK cannot
change an application's dependency tree.

## Install

```bash
npm install ./sdks/node
```

## Wire it once

```js
require('b4n1-telemetry').init({ app: 'api' });
```

That single call installs:

- `uncaughtException` and `unhandledRejection` hooks → level `fatal`, with the
  real stack, **before** the process dies (the SDK flushes, then reproduces
  Node's own fatal behavior — installing a listener without that would leave a
  half-dead process where the app used to crash).
- a bounded queue (2048 events) drained in batches by a background sender.
- correlation ids that survive `await`, via `AsyncLocalStorage`.

Sending is best-effort, exactly like the Python SDK: a collector that is down
never throws inside your application. `stats` is the honest ledger of what was
sent, failed, dropped or throttled.

## Capture explicitly

```js
const telemetry = require('b4n1-telemetry');

telemetry.capture('checkout failed', { level: 'error', module: 'cart', data: { cartId } });
telemetry.captureException(error, { module: 'payments', orderId });

// One id for every event of a request, no argument threading:
telemetry.withCorrelation('req-42', async () => {
  await handler();          // anything captured here carries req-42
});
```

## HTTP frameworks

Two middlewares with the Connect signature every Node framework speaks
(Express, Koa via koa-connect, Fastify via middie, plain `http`) — no framework
dependency and no framework-specific build:

```js
const telemetry = require('b4n1-telemetry');
app.use(telemetry.middleware());               // one correlation id per request
app.use(telemetry.errorMiddleware());          // report, then delegate
```

`middleware()` reuses an incoming `x-correlation-id` / `x-request-id` and
echoes it back, so one id spans the browser, the API and the collector.

## Configuration

```bash
B4N1_TELEMETRY_URL=http://127.0.0.1:9450
B4N1_TELEMETRY_TOKEN=...          # optional bearer token
B4N1_TELEMETRY_APP=api            # optional default app name
B4N1_TELEMETRY_RELEASE=sha-abc    # deploy identifier, rides every event
```

Anything can also be passed to `init()` or to `new Client({...})`
(`queueSize`, `batchSize`, `flushMs`, `timeoutMs`, `onError`).

## Quotas are respected, not fought

If the collector answers **429**, the batch is put back at the head of the
queue and sending pauses for `Retry-After`. The events are not lost and the
client does not hammer a limit it did not choose. `stats.throttled` counts it.

## Verify

```bash
npm test    # node --test: batching, 429 pause, correlation, real crash process
```

The suite runs offline against a local collector double that speaks the same
protocol, including the 429 path.
