# b4n1-telemetry — sovereign execution telemetry

One binary. No Docker, no Postgres, no Kafka, no SaaS. Events are appended to
a local JSONL store, scrubbed before they leave the process, and read back
through a bounded cursor API.

## quickstart

```
bash cli.sh telemetry build
bash cli.sh telemetry start          # collector on :9450
bash cli.sh telemetry demo           # offline end-to-end demo
bash cli.sh telemetry query --limit 20
bash cli.sh telemetry issues
bash cli.sh telemetry trace <correlation_id>
```

## ingest

The collector accepts a JSON array of v1 envelopes on `POST /api/v1/events`:

```json
[{"v":1,"event":{"timestamp":"2026-01-01T00:00:00Z","app":"svc","level":"error","message":"db down","module":"db"}}]
```

Rejections are typed `4xx` with `urn:b4n1:error:{malformed,unsupported-version,not-an-object,missing-event,empty-batch}`.
A `500` is never a valid answer from this server.

Producers normally use the library (`B4N1Telemetry`) rather than raw HTTP; it
adds real backtraces, breadcrumbs, metrics, a durable queue and PII scrubbing.

## query

`GET /api/v1/events` (or `telemetry-server query`) returns one page of stored
events with a continuation cursor:

```
--app NAME --level debug|info|warning|error|fatal
--q TEXT --correlation-id ID --since RFC3339 --until RFC3339
--limit 1..1000 --cursor BYTES
```

`limit` bounds how many stored lines are scanned, not how many match; a page
may contain fewer rows than the limit. `next_cursor` is the byte offset to
pass back. `has_more` says whether the store continues after this page.

## issues

`GET /api/v1/issues` groups a bounded sample (200k events, 5000 groups) by
fingerprint: app, level, normalized message, module and top stack frame.
Volatile numbers and hex ids are collapsed, so `failed to open /tmp/x-1` and
`/tmp/x-2` are one issue. `truncated` and `overflow` are reported instead of
being hidden.

## trace

`GET /api/v1/trace/{correlation_id}` returns every event sharing a
correlation id, ordered by timestamp, with a per-app and per-level summary.
This is the whole chain of one task — ingest, span, finding, crash — not a
single stack trace.

## capture

`telemetry-server capture --app A --message TEXT [--level L] [--module M]
[--correlation-id ID] [--data k=v]... [--backtrace LINE]...`

One-shot ingest for anything that cannot link the crate: shell hooks, cron,
CI steps, foreign binaries. Exit `0` when the collector accepted the event,
`1` on transport/rejection, `2` on usage (missing `--app`/`--message`, unknown
level, malformed `--data`). Level defaults to `error` because that is what a
capture hook is for.

```bash
./deploy.sh || telemetry-server capture --app deploy --message "deploy failed" \
    --data step=migrate --correlation-id "deploy-$CI_PIPELINE_ID"
```

## python-sdk

The drop-in path lives in `sdks/python/` and is standard library only:
`b4n1_telemetry.init(app="musicogram")` wires ERROR logging, uncaught
exceptions (main thread and workers) and a Django middleware that correlates
every request. No per-call-site instrumentation.

```bash
PYTHONPATH=sdks/python python3 -m unittest discover -s sdks/python/tests
```

## retention

The store is capped by `B4N1_TELEMETRY_MAX_BYTES` (default 64 MiB). Retention
runs after each successful write and on `POST /api/v1/purge`; compaction always
cuts on a line boundary, so no event is ever truncated in half. A bad value
panics at startup instead of silently disabling the cap.

## alerts

Set `B4N1_TELEMETRY_WEBHOOK=http://host:port/path` to receive one JSON POST per
`fatal` event and per new fingerprint. Delivery failure is logged and never
blocks ingestion: losing an alert is bad, losing the event is worse.

## privacy

Every event is scrubbed **at ingest**, before bytes reach disk: message,
`data`, backtrace lines and breadcrumb messages. A client can be old, foreign
or simply forget to scrub; the collector cannot.

```
B4N1_TELEMETRY_SCRUB=on        default: structured secrets + provider tokens
B4N1_TELEMETRY_SCRUB=strict    also long digit runs (national IDs, accounts)
B4N1_TELEMETRY_SCRUB=off       store verbatim (explicit opt-out)
```

Redacted: emails, cards, `password=`, bearer tokens, API keys, AWS keys,
GitHub/Slack/provider tokens, JWTs, PEM private-key headers, `*_KEY=...`
assignments and high-entropy blobs. Placeholders are typed
(`[REDACTED_EMAIL]`, `[REDACTED_JWT]`, …) so a consumer knows what was removed.
Long lowercase hex digests (commit SHAs) and UUIDs are left readable on
purpose: redacting them would make every error line useless.

## Admin credential from a vault

The admin token is the one credential an operator may need to *read*, so it can
live in a `b4n1-secrets` vault instead of a `.env`:

```bash
telemetry-server 9450 /var/lib/b4n1-telemetry \
    --vault /srv/.secrets --secret-key telemetry/ADMIN_TOKEN
```

or the same through the environment: `B4N1_TELEMETRY_VAULT` +
`B4N1_TELEMETRY_SECRET_KEY`. The plaintext is resolved once at startup, in RAM,
and the token never appears in argv or in the child environment.

- **One source only.** Vault configured **and** `B4N1_TELEMETRY_TOKEN` set is
  refused (`urn:b4n1:error:conflicting-secret-sources`): ranking them silently
  would leave the ignored one looking enforced.
- **Half a vault is an error** (`incomplete-vault-config`), never a guess at
  the missing half.
- **Typed startup failures** — `secrets:locked`, `secrets:key-not-found`,
  `vault-empty`, `vault-unavailable` — all exit non-zero before binding.
- **Positional token refused.** `telemetry-server 9450 DIR TOKEN` used to work;
  argv is readable through `/proc/<pid>/cmdline`, so it now fails with
  `urn:b4n1:error:token-in-argv`.
- **A locked vault cannot restart unattended.** That is the honest trade: a
  service that must come back alone after a reboot should keep its credential
  in its own env file (`B4N1_TELEMETRY_TOKEN`), and use the vault for operator
  credentials. Pick per deployment, never both.
- Per-app tokens are **not** vault material: only their hash is persisted, and
  a hash is not a secret to retrieve.

## Issue grouping

`GET /api/v1/issues` groups events by fingerprint, then merges the groups that
are still similar:

- **Normalisation** replaces uuid, email, path, long hex, quoted values and
  numbers with placeholders, so one bug in ten tenants is one message.
- **Merge** joins groups with an overlap coefficient ≥ `0.7` **within the same
  app and level** — the same words from another service or another severity is
  a different bug, and merging it away would hide a regression behind a warning.
- The metric was chosen by measurement, not taste (see `RELEASE.md`): overlap at
  0.7 gives precision 1.00 / recall 0.72 on the labelled corpus; Jaccard at 0.8
  gave recall 0.24 because it only joined near-identical wording.
- Merging is **deterministic**: the input is sorted before pairing, so the same
  store yields the same groups after a restart.
- The response carries `groups`, `raw_groups`, `merged` and
  `merge.{threshold,pairs,truncated}`; each merged group carries `variants` and
  up to 10 `members`. A merge that swallows everything is therefore visible.

```bash
telemetry-server issues --json | jq '.groups, .raw_groups'
```

## Alert rules

Alerter v1 fired on "any fatal" and "any new fingerprint" — a rule nobody can
tune: one noisy deploy either floods the channel or teaches everybody to
ignore it. Rules are now data, in `<data-dir>/alerts.json`:

```json
{
  "cooldown_sec": 300,
  "channels": ["sink"],
  "rules": [
    { "name": "fatal", "on": "fatal" },
    { "name": "new-issue", "on": "new_issue" },
    { "name": "burst", "on": "threshold", "count": 50, "window_sec": 60 },
    { "name": "burn", "on": "rate", "count": 20, "ratio": 0.5, "window_sec": 60 }
  ]
}
```

- `fatal` — any event at level `fatal`.
- `new_issue` — the first time a fingerprint is seen.
- `threshold` — `count` events inside `window_sec`.
- `rate` — `count` events inside `window_sec` of which `ratio` are error/fatal.

**One page per issue per `cooldown`, across rules.** A brand-new fatal matches
both `fatal` and `new_issue`; that is one alert, not two, and ten thousand
identical requests are one alert, not ten thousand. A *different* fingerprint
is never silenced by the first.

**Channels:**
- `sink` (default) appends one JSON line to `<data-dir>/alerts.jsonl`, rotated
  at 1 MiB. It is inspectable and testable without a network, and it is what
  `telemetry-server alerts` reads.
- `webhook` POSTs the same payload. It is the paid channel: configuring
  `B4N1_TELEMETRY_WEBHOOK` without the `alerts` entitlement refuses to start.

```bash
telemetry-server alerts [--limit N] [--dir DIR] [--json]
```

A malformed `alerts.json` stops the collector naming the offending rule —
alerting on a threshold nobody declared is worse than not booting. A missing
file means the two defaults above, so adding the collector to an app needs no
config to alert at all.

## Per-app ingest quotas

One runaway service must not fill the store or starve the others. Quotas are
per app, both optional, and live in `<data-dir>/quotas.json`:

```json
{
  "default": { "events_per_sec": 200, "bytes_per_sec": 262144, "sample": 1.0 },
  "apps": {
    "noisy-frontend": { "events_per_sec": 50, "sample": 0.25 }
  }
}
```

Env defaults (for every app without an override): `B4N1_TELEMETRY_RATE_EVENTS`,
`B4N1_TELEMETRY_RATE_BYTES`, `B4N1_TELEMETRY_SAMPLE`. `0` means unmetered,
which is the default: adding the collector to a new app never starts by
dropping its events.

- **Over the limit → 429**, `Retry-After: <seconds>` and
  `urn:b4n1:error:rate-limited`. The whole batch is refused and **nothing is
  stored**, so a client never has to guess which half landed.
- **Buckets are per app, not per fleet.** A flooding app exhausts its own
  bucket and leaves a quiet one untouched — that is the property the suite
  actually asserts (`a quiet app keeps ingesting while another is limited`).
- **Sampling is a deterministic stride** (every 1/ratio-th event), not random:
  the same input gives the same decision twice, and the response body reports
  `sampled` so a client can tell "accepted" from "accepted and thinned".
- **A sampled-out event is not charged** to the bucket, so lowering the ratio
  raises throughput instead of hitting the same 429.
- **A malformed `quotas.json` stops the collector**, naming the field. Running
  unmetered because of a typo is the failure mode this avoids.
- `/api/v1/stats` reports `apps[] = {app, seen, kept, sampled, rejected,
  events_per_sec, bytes_per_sec, sample}`, so an operator sees who floods
  without grepping logs.

## Per-app tokens (read scoping)

One shared token makes every reporter a reader of everything. A per-app token
is bound to one app id: it may only report as that app, and its reads are
scoped to it.

```bash
export B4N1_TELEMETRY_DATA=/var/lib/b4n1-telemetry
telemetry-server tokens create svc-api --label ci      # plaintext printed ONCE
telemetry-server tokens list [--json]                  # ids + apps, hashes only
telemetry-server tokens revoke tok_1a2b3c4d            # dead immediately
```

The collector resolves one scope per request, before any handler runs:

| Caller | May do | Refusal |
|---|---|---|
| app token | `POST /events` for **its own app** | 403 `urn:b4n1:error:app-mismatch` |
| app token | `GET /events` (own app forced), `GET /issues`, `GET /releases`, `GET /trace/<id>` | 403 `urn:b4n1:error:cross-app-read` |
| app token | — | 403 `urn:b4n1:error:admin-required` for `stats`, `purge` |
| admin token (`B4N1_TELEMETRY_TOKEN`) | everything, whole fleet | — |

- **Only the SHA-256 is stored** (`tokens.json`, mode 0600). A stolen file
  hands over no working credential.
- **No anonymous admin:** once `tokens.json` has entries, a request without a
  valid token is a 401 — the old "no token = open" behaviour is gone.
- **Traces stay opaque:** another app's trace answers exactly like a missing one
  (404), so a 403 cannot confirm that a correlation id exists.
- **A broken token file stops the collector** instead of starting with every
  credential dropped (§5).
- The stream (`/api/v1/stream`) is also scoped: an app reader receives only its
  own app's events, and the heartbeat keeps flowing.

## Release tracking

Every envelope may carry a `release` — git sha, version tag, image tag. It is
omitted from the wire when unset, so older clients keep working.

```bash
telemetry-server capture --app svc --message boom --release v1.4.2
B4N1_TELEMETRY_RELEASE=v1.4.2 telemetry-server capture --app svc --message boom
telemetry-server releases            # events/issues per deploy, newest first
telemetry-server issues --json       # each group: first_release / last_release
```

The SDK picks it up the same way: `init(app="svc", release="v1.4.2")` or the
`B4N1_TELEMETRY_RELEASE` env var (what a container exports anyway).
`GET /api/v1/releases` reports events, distinct issues, apps and
first/last seen per release; events without one are reported as
`release: null` — an inventory gap worth seeing, never silently dropped.

## Optional index (`b4n1-db`)

```bash
cargo build --release --features tui,db-index
B4N1_TELEMETRY_INDEX=on telemetry-server 9450 /var/lib/telemetry
```

Exact lookups — a trace by `correlation_id`, events by `fingerprint` — are
answered from an embedded `b4n1-db` index instead of scanning the JSONL. The
dependency is pulled with `default-features = false`, so DataFusion and Arrow
stay out.

**It is opt-in because it is not free.** Measured here with 20 000 events
(load ≈16), same machine, `FSYNC=none`:

| | scan (default) | `db-index` on |
|---|---|---|
| trace latency | 224 ms | **9.4 ms** (24× faster) |
| ingest 20k events | 11.0 s | 47.5 s (4.3× slower) |
| RSS | 4.5 MiB | 56 MiB |
| binary (musl) | 2.6 MB | 3.6 MB |

Verdict: keep the scan as the default. Below ~1M events, 224 ms on a trace is
acceptable and the collector stays tiny; the index is there for stores where
that latency stops being acceptable, and the numbers above are what that
trade costs. `stats` reports `index.enabled` and `index.entries`.

## Scale: `bench`

```bash
telemetry-server bench [--events N] [--json]   # default 200 000, offline
```

Fills a throwaway store, then times the same functions the server uses:
`tail` (`page(0,50)`), a filtered `query` (full scan + parse + match) and
`issues` (full scan + fingerprint grouping). Prints store size, RSS, the host
load average and the split between generating events and appending them — a
slow number on a contended box is the box, and the output says so.

Measured on this machine, **100 000 events / 20.3 MiB** (load ≈16): tail
0.3 ms, query 35 ms, issues 0.6 s. Both `query` and `issues` scan the whole
store, so they grow linearly. At that size it is still fine; an index becomes
worth its complexity in the millions.

`B4N1_TELEMETRY_FSYNC=always|none` (default `always`): `none` keeps bytes in
the page cache (survives a process crash, not a power cut) and removes the
per-batch `fsync`. An invalid value aborts startup.

## Live stream (`/api/v1/stream`)

`tail --follow` no longer polls: it replays the last page, then keeps a
server-sent-events connection open and prints each event the collector stores.

```bash
bash cli.sh telemetry tail --follow          # live
bash cli.sh telemetry tail --follow --poll   # cursor polling (explicit)
curl -N http://127.0.0.1:9450/api/v1/stream  # raw SSE, same auth as the API
```

`telemetry-tui` consumes the same stream: a reader thread raises a flag and the
loop redraws on the next tick (60 ms), so an error appears in the panel as it
is stored rather than up to 2 s later. The thread never carries event data —
the panes still read over HTTP, one truth. If the stream dies it reconnects every
second and says so once; if the collector is older and has no stream, the
periodic refresh keeps the UI correct.

The stream carries **exactly what was stored** — scrubbed and capped — so it
cannot leak what the store would not keep. A `: ping` comment every 15 s proves
the reader is alive; a closed reader frees its slot on the next write. At most
16 readers are attached at once; beyond that the collector answers
`503 urn:b4n1:error:stream-full` instead of dropping the connection silently.
`stats` reports `stream_clients`.

**PII families (ported from the harness `pii_detect` rules):** emails, credit
cards (**Luhn-validated**, so a 16-digit order id survives), Spanish DNI/NIF,
IBANs and phone numbers. Overlapping matches keep the longest span, so a DNI
that also looks like a short phone is reported as a DNI. IPv4 addresses are
redacted **only in `strict`**: the peer address is the most useful signal in a
network error. Dates and `12:34:56` timestamps are never classified as phones.

**Size caps (a document is not an event):**

```bash
B4N1_TELEMETRY_MAX_EVENT_BYTES=65536   # default; larger envelope => 413
B4N1_TELEMETRY_MAX_FIELD_BYTES=8192    # default; larger message/data => truncated
```

An oversized envelope is rejected with `urn:b4n1:error:event-too-large` (the
body names the actual size and the cap); a field past the cap is cut and
marked `…[truncated]`. Both are reported by `stats`.

**What survives (debuggability contract):** the variable/field name, the
placeholder type, the value length, and a stable fingerprint of the value
(`DB_TOKEN=[REDACTED_CREDENTIAL fp=3f2a1c9d len=42]`). Same value → same
fingerprint, so two events can be compared without either carrying the secret;
the value itself never reaches disk. Redacting the name too would leave an
event that says something was wrong but not where — that is why it is kept.

An unknown mode is a startup error — never a silent downgrade to `off`.
`GET /api/v1/stats` reports the active mode (`standard`/`strict`/`off`).

## demo

`telemetry-server demo` starts an ephemeral collector, ingests five events,
reads them back, groups them, resolves a correlation trace and prints the
measured numbers plus this process's resident memory. It is offline and
self-contained.

## mcp

`telemetry-server mcp [--url URL] [--token TOKEN]` speaks JSON-RPC 2.0 over
stdio, one object per line, so an MCP client can query the collector as a tool
instead of shelling out and parsing text:

```json
{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
{"jsonrpc":"2.0","id":2,"method":"tools/list"}
{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"issues","arguments":{}}}
```

Tools: `issues` (grouped, with first/last release), `query` (raw events with the
collector's real filters and a byte cursor), `stats` (counters, caps, scrub
mode), `trace` (`correlation_id` required) and `releases` (events and issues per
deploy). Notifications get no answer, `initialize`/`ping` work offline, and a
call that fails is a **tool** error (`isError: true`) — the bridge keeps
answering, exactly like a JSON-RPC client expects. Nothing is cached here: every
call is the collector's answer.

Registration: `bash cli.sh mcp install` adds `b4n1-telemetry` to every detected
agent config (opencode, codebuff/freebuff, claude-desktop, kilo, cursor…), with
the release binary and `B4N1_TELEMETRY_URL`. Build it first with
`bash cli.sh telemetry build`; the install warns loudly if the binary is
missing instead of registering a dead path.

## tui

`telemetry-tui` is a separate binary and a pure client: it reads the same
HTTP API, never opens the store and never mutates anything. Build it with
`--features tui` (the collector does not carry ratatui/crossterm).

```
telemetry-tui [--url http://HOST:PORT] [--token TOKEN]
```

Three panes: issue groups on the left, the events of the selected issue on
the top right, and the causal chain of the selected event (`correlation_id`)
on the bottom right.

`j`/`k` or arrows move, `tab`/`shift-tab` switch pane, `enter` drills (issue →
events → trace), `r` reloads, `g`/`G` jump to first/last, `q`/`esc` quit.
The mouse works too: click a row to select it (and load its events/trace), click
a pane to focus it, wheel over a pane to move only that pane. Mouse capture is
enabled by the engine, so it also works inside a tmux/desk tab.
The issues pane refreshes every 2 s. It is read-only: `purge` stays a CLI
admin verb, never a key next to `q`. Piping it (no terminal) exits 3 and tells
you to use `query`/`issues`/`trace`.

## license

`telemetry-server license [--key KEY] [--json]` reports the current tier.

The observability core is **community** and needs no key: `serve`, `ingest`,
`query`, `issues`, `trace`, `stats`, `tail`, `purge`, `demo`, `docs` and the
TUI. Paid tiers add `alerts` (webhooks) and `export`.

A webhook configured on a community installation is a **startup error**, not a
silently ignored setting: alerts that are paid for but not licensed must be
visible, not quietly dropped.

```
B4N1_TELEMETRY_LICENSE_KEY     customer key (client side)
B4N1_TELEMETRY_LICENSE_SECRET  issuer signing secret (release/server side)
```

A key without a configured secret is rejected loudly — the binary never
pretends to have verified something. `--licenses` (third-party notices) stays
keyless: the legal surface is never gated by the commercial one.

## exit-codes

`0` ok · `1` runtime failure (transport, storage, bind) · `2` usage error
(unknown verb, missing argument, unknown docs section).
