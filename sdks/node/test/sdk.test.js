'use strict';

/**
 * Offline SDK suite: a real local collector double, real envelopes, no network.
 *
 * The double speaks the collector's protocol and can answer 429 on demand, so
 * the quota path is exercised the same way production does — not mocked away.
 */

const assert = require('node:assert/strict');
const http = require('node:http');
const path = require('node:path');
const { spawn } = require('node:child_process');
const { test, before, after } = require('node:test');

const telemetry = require('..');
const { Client } = require('../lib/client');

function collector({ statuses = [] } = {}) {
  const state = { batches: [], headers: [], statuses: [...statuses] };
  const server = http.createServer((req, res) => {
    const chunks = [];
    req.on('data', (chunk) => chunks.push(chunk));
    req.on('end', () => {
      const status = state.statuses.length > 0 ? state.statuses.shift() : 202;
      if (status === 0) {
        // No answer at all: what a stalled collector looks like on the wire.
        req.socket.destroy();
        return;
      }
      // Like the real collector: a refused batch stores nothing, so recording
      // it here would fake a duplicate that production never creates.
      if (status < 300) {
        state.batches.push(JSON.parse(Buffer.concat(chunks).toString('utf8')));
        state.headers.push(req.headers);
      }
      res.writeHead(status, status === 429 ? { 'Retry-After': '1' } : {});
      res.end('');
    });
  });
  return new Promise((resolve) => {
    server.listen(0, '127.0.0.1', () => {
      state.url = `http://127.0.0.1:${server.address().port}`;
      state.events = () => state.batches.flat().map((env) => env.event);
      state.close = () => new Promise((done) => server.close(done));
      resolve(state);
    });
  });
}

test('a dropped connection is retried once instead of losing the batch', async () => {
  const fake = await collector({ statuses: [0] });
  const client = new Client({ url: fake.url, app: 'retry' });
  client.send({ level: 'error', message: 'kept', module: 'db' });

  assert.equal(await client.flush(), true, 'batch lost after a dropped connection');
  const stats = client.stats;
  assert.equal(stats.failed, 0, 'a missing answer must be retried, not counted');
  assert.equal(stats.sent, 1);
  assert.deepEqual(fake.events().map((event) => event.message), ['kept']);
  await client.shutdown();
  await fake.close();
});

test('capture sends an envelope the collector can parse', async () => {
  const fake = await collector();
  const client = new Client({ url: fake.url, app: 'api', token: 'tkn', release: 'sha-1' });
  client.send({ level: 'error', message: 'boom', module: 'db', data: { attempt: 2 } });
  assert.equal(await client.flush(), true);
  await client.shutdown();
  const [event] = fake.events();
  assert.equal(event.app, 'api');
  assert.equal(event.level, 'error');
  assert.equal(event.message, 'boom');
  assert.equal(event.module, 'db');
  assert.equal(event.release, 'sha-1');
  assert.equal(event.data.attempt, '2');
  assert.match(event.timestamp, /^\d{4}-\d{2}-\d{2}T.*Z$/);
  assert.equal(fake.headers[0].authorization, 'Bearer tkn');
  assert.equal(client.stats.sent, 1);
  await fake.close();
});

test('events are batched into one request', async () => {
  const fake = await collector();
  const client = new Client({ url: fake.url, app: 'batch', batchSize: 50, flushMs: 20 });
  for (let i = 0; i < 5; i += 1) client.send({ message: `m${i}` });
  await new Promise((resolve) => setTimeout(resolve, 120));
  assert.equal(fake.batches.length, 1, 'five events must not mean five requests');
  assert.equal(fake.events().length, 5);
  await client.shutdown();
  await fake.close();
});

test('a 429 pauses the sender and keeps the events', async () => {
  const fake = await collector({ statuses: [429] });
  const client = new Client({ url: fake.url, app: 'limited', flushMs: 10 });
  client.send({ message: 'kept' });
  // flush() waits out the pause and retries: the event must land, not vanish.
  assert.equal(await client.flush(4000), true);
  assert.equal(client.stats.throttled, 1);
  assert.equal(client.stats.sent, 1, 'the retry after the pause succeeded');
  assert.equal(client.stats.dropped, 0, 'a quota is not a reason to lose data');
  assert.equal(fake.events().length, 1);
  assert.equal(fake.events()[0].message, 'kept');
  await client.shutdown();
  await fake.close();
});

test('a full queue drops and counts instead of growing without bound', async () => {
  const client = new Client({ url: 'http://127.0.0.1:9', queueSize: 3, flushMs: 10_000 });
  for (let i = 0; i < 6; i += 1) client.send({ message: `m${i}` });
  assert.equal(client.stats.dropped, 3);
  assert.equal(client.stats.queued, 3);
  await client.shutdown(50);
});

test('the middleware correlates every event of a request', async () => {
  const fake = await collector();
  telemetry.init({ app: 'web', url: fake.url, client: null });
  const middleware = telemetry.middleware();
  const headers = {};
  const res = { setHeader: (name, value) => { headers[name] = value; } };
  await new Promise((resolve) => {
    middleware({ headers: { 'x-request-id': 'req-42' } }, res, () => {
      telemetry.capture('inside the request');
      resolve();
    });
  });
  await telemetry.flush();
  await telemetry.shutdown();
  assert.equal(headers['x-correlation-id'], 'req-42');
  assert.equal(fake.events()[0].correlation_id, 'req-42');
  await fake.close();
});

test('an uncaught exception is reported as fatal from a real process', async () => {
  const fake = await collector();
  const script = path.join(__dirname, 'crash.js');
  // Async on purpose: spawnSync would block this process's event loop, and the
  // collector double lives here — the child's POST would never be answered.
  const child = spawn(process.execPath, [script, fake.url], { stdio: 'ignore' });
  const code = await new Promise((resolve) => child.on('exit', resolve));
  assert.equal(code, 1, 'the process must still die like an uncaught throw');
  const [event] = fake.events();
  assert.equal(event.level, 'fatal');
  assert.equal(event.module, 'uncaught');
  assert.match(event.message, /Error: uncaught-boom/);
  assert.ok(event.backtrace.length > 1, 'the real stack must travel with the event');
  assert.equal(event.app, 'crash-app');
  await fake.close();
});
