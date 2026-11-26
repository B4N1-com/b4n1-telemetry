'use strict';

/**
 * The POST itself: the only module that knows HTTP exists, and the only place
 * that retries.
 *
 * A missing answer is not a refusal. A timeout against a healthy collector
 * means the collector is fsyncing, and dropping the batch there is silent
 * loss, so the request is attempted once more. Any HTTP answer is returned to
 * the caller untouched: a 4xx would repeat the same mistake on a retry, and a
 * 429 is the collector asking for time.
 */

const http = require('node:http');
const https = require('node:https');
const { URL } = require('node:url');

const RETRY_PAUSE_MS = 100;

/**
 * POST one batch. Resolves `{ status, headers }` or `{ error }`; it never
 * rejects, because a sender that throws takes its host down with it.
 */
function postBatch({ url, token, timeoutMs, batch }) {
  const body = Buffer.from(JSON.stringify(batch), 'utf8');
  return attempt({ url, token, timeoutMs, body }).then((result) => {
    if (!result.error) return result;
    return new Promise((resolve) => setTimeout(resolve, RETRY_PAUSE_MS)).then(() =>
      attempt({ url, token, timeoutMs, body })
    );
  });
}

function attempt({ url, token, timeoutMs, body }) {
  const target = new URL('/api/v1/events', url);
  const headers = {
    'Content-Type': 'application/json',
    'Content-Length': body.length,
  };
  if (token) headers.Authorization = `Bearer ${token}`;
  const transport = target.protocol === 'https:' ? https : http;

  return new Promise((resolve) => {
    const request = transport.request(
      target,
      { method: 'POST', headers, timeout: timeoutMs },
      (response) => {
        response.resume();
        response.on('end', () =>
          resolve({ status: response.statusCode, headers: response.headers })
        );
      }
    );
    request.on('timeout', () => request.destroy(new Error('timeout')));
    request.on('error', (error) =>
      resolve({ error: `${error.name}: ${error.message}` })
    );
    request.end(body);
  });
}

module.exports = { postBatch };
