'use strict';

/**
 * Wire envelope construction. Mirrors the Python SDK byte for byte, so one
 * collector sees the same shape whatever the language of the producer.
 */

const ENVELOPE_VERSION = 1;
const DEFAULT_URL = 'http://127.0.0.1:9450';

/** UTC timestamp in the RFC 3339 form the collector parses. */
function nowIso() {
  return new Date().toISOString();
}

/**
 * Build one wire envelope (`{v:1, event:{...}}`).
 *
 * `release` is the deploy identifier (git sha, version, image tag) and is
 * omitted when unset, so the wire stays compatible with older collectors.
 */
function envelope({
  app,
  level = 'error',
  message = '',
  module = '',
  data = undefined,
  backtrace = undefined,
  correlationId = null,
  breadcrumbs = undefined,
  timestamp = undefined,
  release = undefined,
}) {
  const plain = {};
  for (const [key, value] of Object.entries(data || {})) {
    plain[String(key)] = typeof value === 'string' ? value : String(value);
  }
  const event = {
    timestamp: timestamp || nowIso(),
    app,
    level,
    message,
    module,
    data: plain,
    backtrace: Array.from(backtrace || []),
    correlation_id: correlationId || null,
    breadcrumbs: Array.from(breadcrumbs || []),
  };
  if (release) event.release = release;
  return { v: ENVELOPE_VERSION, event };
}

/** Node's `Error.stack` as one string per line, like a Python traceback. */
function stackLines(error) {
  if (!error || typeof error.stack !== 'string') return [];
  return error.stack.split('\n');
}

/** `"TypeError: boom"` — the message every other SDK uses for the same error. */
function describe(error) {
  if (error instanceof Error) return `${error.name}: ${error.message}`;
  return `Error: ${String(error)}`;
}

module.exports = { ENVELOPE_VERSION, DEFAULT_URL, nowIso, envelope, stackLines, describe };
