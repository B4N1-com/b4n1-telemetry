'use strict';

/**
 * Transport: a bounded queue drained by a background sender.
 *
 * Sending is asynchronous and best-effort by design. An observability client
 * that blocks or throws inside the application turns a bug report into an
 * outage, so every failure is counted in `stats` instead of raised.
 *
 * The one failure that is NOT just counted is the collector's 429: the batch
 * is put back at the head of the queue and sending pauses for `Retry-After`.
 * Retrying immediately would earn another 429, and dropping the batch would
 * make a quota the producer never chose look like lost telemetry.
 */

const { DEFAULT_URL, envelope } = require('./envelope');
const { postBatch } = require('./transport');

class Client {
  constructor({
    url = process.env.B4N1_TELEMETRY_URL || DEFAULT_URL,
    token = process.env.B4N1_TELEMETRY_TOKEN || null,
    app = process.env.B4N1_TELEMETRY_APP || 'node',
    timeoutMs = 3000,
    queueSize = 2048,
    batchSize = 100,
    flushMs = 250,
    onError = null,
    release = process.env.B4N1_TELEMETRY_RELEASE || null,
  } = {}) {
    this.url = String(url).replace(/\/+$/, '');
    this.token = token;
    this.app = app;
    this.release = release || null;
    this.timeoutMs = timeoutMs;
    this.queueSize = queueSize;
    this.batchSize = Math.max(1, batchSize);
    this.flushMs = Math.max(10, flushMs);
    this.onError = onError;

    this._queue = [];
    this._sent = 0;
    this._failed = 0;
    this._dropped = 0;
    this._throttled = 0;
    this._lastError = null;
    this._pausedUntil = 0;
    this._closed = false;
    this._inFlight = null;
    this._timer = setInterval(() => this._tick(), this.flushMs);
    this._timer.unref();
  }

  /** Enqueue one event. Returns false when the queue is full (counted). */
  send(fields = {}) {
    return this.sendEnvelope(
      envelope({
        app: fields.app || this.app,
        release: this.release,
        ...fields,
      })
    );
  }

  sendEnvelope(env) {
    if (this._closed) return false;
    if (this._queue.length >= this.queueSize) {
      this._dropped += 1;
      return false;
    }
    this._queue.push(env);
    if (this._queue.length >= this.batchSize) this._flushNow();
    return true;
  }

  /**
   * Resolves true when everything queued has been sent (or dropped) within
   * `timeoutMs`. A batch already in flight counts as pending — reporting
   * "flushed" while the POST is still open is how a caller loses events.
   */
  async flush(timeoutMs = 2000) {
    const deadline = Date.now() + timeoutMs;
    const pending = () => this._queue.length + (this._inFlight ? 1 : 0);
    while (pending() > 0 && Date.now() < deadline) {
      if (this._inFlight) {
        await this._inFlight.catch(() => {});
      } else if (this._queue.length) {
        this._flushNow();
      } else {
        await new Promise((resolve) => setTimeout(resolve, 5));
      }
      if (pending() > 0) await new Promise((resolve) => setTimeout(resolve, 5));
    }
    return pending() === 0;
  }

  async shutdown(timeoutMs = 2000) {
    await this.flush(timeoutMs);
    this._closed = true;
    clearInterval(this._timer);
  }

  get stats() {
    return {
      url: this.url,
      sent: this._sent,
      failed: this._failed,
      dropped: this._dropped,
      throttled: this._throttled,
      queued: this._queue.length,
      last_error: this._lastError,
    };
  }

  _tick() {
    if (this._closed || this._queue.length === 0 || this._inFlight) return;
    if (Date.now() < this._pausedUntil) return;
    this._flushNow();
  }

  _flushNow() {
    if (this._inFlight || this._closed || this._queue.length === 0) return;
    if (Date.now() < this._pausedUntil) return;
    const batch = this._queue.splice(0, this.batchSize);
    this._inFlight = this._post(batch).finally(() => {
      this._inFlight = null;
    });
  }

  _post(batch) {
    return postBatch({
      url: this.url,
      token: this.token,
      timeoutMs: this.timeoutMs,
      batch,
    }).then((result) => {
      if (result.error) {
        this._fail(result.error);
        return;
      }
      this._settle(batch, result.status, result.headers);
    });
  }

  _settle(batch, status, headers) {
    if (status >= 200 && status < 300) {
      this._sent += 1;
      return;
    }
    if (status === 429) {
      // Not a bug in the payload: the collector is asking us to wait.
      this._throttled += 1;
      const retryAfter = Number(headers['retry-after']);
      const seconds = Number.isFinite(retryAfter) && retryAfter > 0 ? retryAfter : 1;
      this._pausedUntil = Date.now() + seconds * 1000;
      // The failed batch goes back FIRST (it is the oldest), then whatever
      // still fits. What does not fit is counted, never hidden.
      const room = Math.max(0, this.queueSize - batch.length);
      const kept = this._queue.splice(0, room);
      this._dropped += this._queue.length;
      this._queue.length = 0;
      this._queue.push(...batch, ...kept);
      this._lastError = `HTTP 429 (retry in ${seconds}s)`;
      return;
    }
    this._fail(`HTTP ${status}`);
  }

  _fail(message) {
    this._failed += 1;
    this._lastError = message;
    if (this.onError) {
      try {
        this.onError(message);
      } catch (_ignored) {
        // An error hook must never break the sender.
      }
    }
  }
}

module.exports = { Client };
