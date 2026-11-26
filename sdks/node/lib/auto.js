'use strict';

/**
 * Drop-in instrumentation: process hooks + an optional console hook.
 *
 * `require('b4n1-telemetry').init({app: 'api'})` is the whole integration:
 * every uncaught exception and unhandled rejection becomes an event, already
 * correlated, with no per-call-site code.
 *
 * Correlation uses `AsyncLocalStorage`, the only Node primitive that keeps an
 * id across awaits. A plain module variable would leak one request's id into
 * every concurrent request, which is worse than no correlation at all.
 */

const { AsyncLocalStorage } = require('node:async_hooks');
const { Client } = require('./client');
const { describe, envelope, stackLines } = require('./envelope');

const storage = new AsyncLocalStorage();
const installed = { client: null, uncaught: null, rejection: null };

function currentClient() {
  return installed.client;
}

function requireClient() {
  if (!installed.client) {
    throw new Error('b4n1-telemetry init({app}) must be called before capture()');
  }
  return installed.client;
}

/** Node's default fatal behaviour, reproduced: report, then die with status 1. */
function fatal(error) {
  process.stderr.write(`${(error && error.stack) || String(error)}\n`);
  process.exit(1);
}

function init({
  app = process.env.B4N1_TELEMETRY_APP || 'node',
  url = process.env.B4N1_TELEMETRY_URL,
  token = process.env.B4N1_TELEMETRY_TOKEN,
  captureUncaught = true,
  client = null,
  release = process.env.B4N1_TELEMETRY_RELEASE,
  ...options
} = {}) {
  if (installed.client) return installed.client;
  const active = client || new Client({ url, token, app, release, ...options });
  installed.client = active;

  if (captureUncaught) {
    // Installing a listener SUPPRESSES Node's default fatal behaviour, so the
    // handler must put it back: otherwise a crash that used to kill the process
    // leaves a half-dead one behind. Flushing first is the only chance the
    // event has to leave before the process is gone.
    installed.uncaught = (error) => {
      captureException(error, { module: 'uncaught', level: 'fatal', thread: 'main' });
      installed.client.flush(2000).then(() => {
        if (installed.previousUncaught) installed.previousUncaught(error);
        else fatal(error);
      });
    };
    installed.rejection = (reason) => {
      captureException(reason, { module: 'unhandledRejection', level: 'fatal' });
      installed.client.flush(2000).then(() => {
        if (installed.previousRejection) installed.previousRejection(reason);
        else fatal(reason);
      });
    };
    installed.previousUncaught = process.listeners('uncaughtException').pop() || null;
    installed.previousRejection = process.listeners('unhandledRejection').pop() || null;
    process.on('uncaughtException', installed.uncaught);
    process.on('unhandledRejection', installed.rejection);
  }
  return active;
}

function shutdown() {
  if (!installed.client) return Promise.resolve();
  if (installed.uncaught) process.removeListener('uncaughtException', installed.uncaught);
  if (installed.rejection) process.removeListener('unhandledRejection', installed.rejection);
  const client = installed.client;
  installed.client = null;
  installed.uncaught = null;
  installed.rejection = null;
  return client.shutdown();
}

/** Record one event explicitly. */
function capture(message, { level = 'error', module = '', data, correlationId } = {}) {
  return requireClient().send({
    level,
    message,
    module,
    data,
    correlationId: correlationId || storage.getStore() || null,
  });
}

/** Record a caught exception with its real stack. */
function captureException(error, { module = '', level = 'error', ...data } = {}) {
  return requireClient().send({
    level,
    message: describe(error),
    module: module || 'js',
    data,
    backtrace: stackLines(error),
    correlationId: storage.getStore() || null,
  });
}

/**
 * Run `fn` with a correlation id. The id rides every event emitted from inside,
 * including across awaits, without being passed down as an argument.
 */
function withCorrelation(correlationId, fn) {
  return storage.run(correlationId, fn);
}

function correlationId() {
  return storage.getStore() || null;
}

/**
 * Connect-style middleware: one correlation id per request, and an error
 * handler that reports before delegating. No framework dependency — the
 * signature is the one every Node framework already speaks.
 */
function middleware() {
  return function telemetryMiddleware(req, res, next) {
    const id = String(req.headers['x-correlation-id'] || req.headers['x-request-id'] || '');
    const generated = id || `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
    res.setHeader('x-correlation-id', generated);
    withCorrelation(generated, () => next());
  };
}

function errorMiddleware() {
  return function telemetryErrorMiddleware(error, req, res, next) {
    captureException(error, { module: 'http', path: req && req.url ? req.url : '' });
    if (typeof next === 'function') next(error);
  };
}

/** Flush explicitly (before a crash handler exits, or in a test). */
function flush(timeoutMs) {
  return installed.client ? installed.client.flush(timeoutMs) : Promise.resolve(true);
}

module.exports = {
  Client,
  capture,
  captureException,
  correlationId,
  currentClient,
  envelope,
  errorMiddleware,
  flush,
  init,
  middleware,
  shutdown,
  withCorrelation,
};
