# b4n1-telemetry SDK

[![version](https://img.shields.io/badge/version-0.1.1-blue)](https://github.com/B4N1-com/b4n1-telemetry)
[![license](https://img.shields.io/badge/license-BUSL--1.1-orange)](LICENSE)
[![registry](https://img.shields.io/badge/registry-py%20%7C%20npm%20%7C%20nuget%20%7C%20gem%20%7C%20composer%20%7C%20go-brightgreen)](#install)
[![tests](https://img.shields.io/badge/tests-7%20languages-green)](tests)

Telemetry you own: events go to your collector on your machine, not to a SaaS.
One wire format, seven thin wrappers, a collector that reads back.

## Install

| Language | Registry | Command |
|---|---|---|
| Python | PyPI | `pip install b4n1-telemetry` |
| JavaScript | npm | `npm install b4n1-telemetry` |
| .NET | NuGet | `dotnet add package B4n1.Telemetry` |
| Ruby | RubyGems | `gem install b4n1_telemetry` |
| PHP | Packagist | `composer require b4n1/b4n1-telemetry` |
| Go | module | `go get github.com/B4N1-com/b4n1-telemetry/sdks/go` |
| Java | JAR | built from this tree; distributed as a release artifact |

## Quickstart

```python
from b4n1_telemetry import init

telemetry = init(url="http://127.0.0.1:9450", app="demo")
telemetry.capture("error", "boom", module="api")
telemetry.flush()
```

The collector answers with the same events, grouped by fingerprint:

```bash
bash cli.sh telemetry issues
```

## What it gives you

- **A queue that survives a slow collector:** bounded, counted drops, one retry
  when no response arrived (a 429 has its own path with `Retry-After`).
- **Grouping by similarity,** not by exact string, so the same failure written
  two ways is one issue.
- **A correlation id that travels,** so a trace is a real chain and not a guess.
- **Read-back:** query, issues, stats, releases and trace over the same HTTP
  surface the SDKs write to.

## Documentation

- `docs/` — manual (quickstart, wire format, operations).
- `examples/README.md` — runnable examples.
- `pitch.md` — what it is and who it is for.
