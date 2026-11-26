# Wire format

One contract for the seven wrappers: `POST /api/v1/events` with a JSON **array**
of v1 envelopes, `Authorization: Bearer <token>`.

```json
[
  {
    "v": 1,
    "app": "demo",
    "event": {
      "level": "error",
      "message": "boom",
      "module": "api",
      "timestamp": "2026-01-01T00:00:00.000Z",
      "correlation_id": null,
      "release": "sha-1",
      "data": { "attempt": "2" },
      "backtrace": []
    }
  }
]
```

## Rules the collector enforces

- **A batch is all or nothing:** a rejected batch writes nothing, so a `429`
  never leaves half a request behind.
- **Typed rejections:** a malformed body answers `400` with
  `urn:b4n1:error:*`, never a generic failure.
- **`429` carries `Retry-After`,** and the SDK returns the batch to the front of
  the queue and pauses for that long.
- **`release` is optional** and omitted when unknown: events without a release
  are grouped as `null`, never discarded.

## SDK behaviour that the suite pins

- A bounded queue with **counted** drops — a drop is a number, not silence.
- **One retry, only when no response arrived** (timeout, cut connection, EOF).
  A response is never retried: a `4xx` would repeat the same error, and a `429`
  has its own path.
- `flush` counts in-flight work as pending; saying "flushed" with an open POST
  is losing data.
