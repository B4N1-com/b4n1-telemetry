<p align="center">
  <img src="b4n1telemetry_presentation.png" alt="B4N1 Telemetry — Errors with frames, grouped, on your own disk." width="100%" />
</p>

<div align="center">

# 📊 b4n1-telemetry

**Souveräne Telemetrie. Eingehende Ereignisse, eine dauerhafte Warteschlange auf der lokalen Festplatte, eine Lese-API und eine Terminal-Benutzeroberfläche – kein SaaS-Konto, keine Agentenflotte, nichts verlässt die Maschine.**

[English](README.md) · [Español](README.es.md) · [Deutsch](README.de.md) · [Français](README.fr.md) · [Italiano](README.it.md) · [Português (BR)](README.pt-BR.md) · [Русский](README.ru.md) · [العربية](README.ar.md) · [हिन्दी](README.hi.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [中文](README.zh-CN.md)

[English](../README.md) · [Español](../README.es.md) · [Deutsch](../README.de.md) · [Français](../README.fr.md) · [Italiano](../README.it.md) · [Português (BR)](../README.pt-BR.md) · [Русский](../README.ru.md) · [العربية](../README.ar.md) · [हिन्दी](../README.hi.md) · [日本語](../README.ja.md) · [한국어](../README.ko.md) · [中文](../README.zh-CN.md)

[![License](https://img.shields.io/badge/license-BUSL--1.1-blue.svg)](LICENSE)
[![PyPI](https://badge.fury.io/py/b4n1-telemetry.svg)](https://pypi.org/project/b4n1-telemetry/)
[![npm](https://img.shields.io/npm/v/b4n1-telemetry.svg)](https://www.npmjs.com/package/b4n1-telemetry)
[![NuGet](https://img.shields.io/nuget/v/B4n1.Telemetry.svg)](https://www.nuget.org/packages/B4n1.Telemetry)
[![RubyGems](https://img.shields.io/gem/v/b4n1-telemetry.svg)](https://rubygems.org/gems/b4n1-telemetry)
[![Packagist](https://img.shields.io/packagist/v/b4n1/b4n1-telemetry.svg)](https://packagist.org/packages/b4n1/b4n1-telemetry)
[![Go Reference](https://pkg.go.dev/badge/github.com/B4N1-com/b4n1-telemetry/sdks/go.svg)](https://pkg.go.dev/github.com/B4N1-com/b4n1-telemetry/sdks/go)
[![Tests](https://img.shields.io/badge/tests-passing-brightgreen)](https://github.com/B4N1-com/b4n1-telemetry)

</div>

---

## 📦 Installation – 6 Register, 1 Kabelvertrag

| Registrierung | Sprache | Paket | Befehl |
|----------|----------|---------|---------|
| **PyPI** | Python 3.10+ | `b4n1-telemetry` | `pip install b4n1-telemetry` |
| **npm** | Node.js | `b4n1-telemetry` | `npm install b4n1-telemetry` |
| **NuGet** | .NET / C# | `B4n1.Telemetry` | `dotnet add package B4n1.Telemetry` |
| **RubyGems** | Rubin | `b4n1-telemetry` | `gem install b4n1-telemetry` |
| **Verpacker** | PHP 8.1+ | `b4n1/b4n1-telemetry` | `composer require b4n1/b4n1-telemetry` |
| **Gehen** | Gehen Sie 1.21+ | `b4n1-telemetry/sdks/go` | `go get github.com/B4N1-com/b4n1-telemetry/sdks/go` |

Jeder Kunde spricht den gleichen Vertrag – `POST /api/v1/events` mit einem JSON-Array von Umschlägen und einem Inhabertoken – der Collector wird also einmal ausgeführt und jeder der sechs Clients kommuniziert mit ihm.

---

## 🚀 Schnellstart

```python
# Python
import b4n1_telemetry

b4n1_telemetry.init(app="checkout")
b4n1_telemetry.capture("payment failed", level="error", module="payments",
                       correlation_id="req-42")
b4n1_telemetry.flush()
```

```javascript
// Node.js
const telemetry = require('b4n1-telemetry');

telemetry.init({ app: 'checkout' });
telemetry.capture('payment failed', { level: 'error', module: 'payments', correlationId: 'req-42' });
await telemetry.flush();
```

```csharp
// .NET / C#
using B4n1.Telemetry;

Telemetry.Init(new Options { App = "checkout" });
Telemetry.InstallUncaughtHandlers();
Telemetry.Capture("payment failed", "error", "payments");
```

```ruby
# Ruby
require "b4n1_telemetry"

B4N1Telemetry.init(app: "checkout")
B4N1Telemetry.correlation_id = "req-42"
B4N1Telemetry.capture("payment failed", level: "error", component: "payments")
B4N1Telemetry.flush(2.0)
```

```php
// PHP
\B4n1\Telemetry\Telemetry::init(['app' => 'checkout']);
\B4n1\Telemetry\Telemetry::installErrorHandlers();
\B4n1\Telemetry\Telemetry::capture('payment failed', component: 'payments');
```

```go
// Go
import telem "github.com/B4N1-com/b4n1-telemetry/sdks/go"

telem.Init(telem.Options{App: "checkout"})
defer telem.Close()
telem.Capture("error", "payment failed", "payments", nil)
```

---

## ⚡ Was es bewirkt

| Fähigkeit | Was Sie bekommen |
|------------|--------------|
| **Dauerhafte Warteschlange** | Ereignisse werden vor der Bestätigung an den lokalen Store geschrieben; Bei einem Neustart geht der Stapel, von dem Sie dachten, dass er gesendet wurde, nicht verloren. |
| **Echte Backtraces** | Frames werden im Prozess erfasst und nicht aus Protokollzeilen rekonstruiert. |
| **PII-Scrubbing bei der Aufnahme** | Was vor der Landung gefiltert wird, kann später nicht mehr auslaufen. |
| **Kausalkette** | Eine Korrelations-ID gibt den gesamten Pfad zurück, den eine Anfrage der Reihe nach genommen hat. |
| **Gruppierte Probleme** | Normalisierung plus gemessene Ähnlichkeitszusammenführung: Präzision 1,00 / Rückruf 0,72 auf dem beschrifteten Korpus, gepinnt von der Testsuite. |
| **Begrenzter Footprint** | Der Speicher wird auf eine von Ihnen festgelegte Byte-Obergrenze komprimiert. Ein vollständiger Durchlauf von 10.000 Ereignissen hatte eine Einwohnerzahl von 17 MiB. |
| **Selbst gehostete Lektüre** | Abfragen, Probleme, Ablaufverfolgung und Statistiken über dieselbe API, an die die Clients schreiben. |

---

## 🖥 Plattformunterstützung

| Plattform | Architekturen |
|----------|---------------|
| **Linux** | x86_64 (statisch, keine Runtime zu installieren) |

Der Kollektor ist ein Prozess ohne externe Abhängigkeiten. Die Clients sind Thin Wrapper über HTTP.

---

## 📄 Lizenz

Business Source-Lizenz 1.1 – siehe [LIZENZ](LICENSE) und [EULA.txt](EULA.txt).
