<p align="center">
  <img src="b4n1telemetry_presentation.png" alt="B4N1 Telemetry — Errors with frames, grouped, on your own disk." width="100%" />
</p>

<div align="center">

# 📊 b4n1-telemetry

**Telemetria sovrana. Eventi in ingresso, una coda duratura sul disco locale, un'API di lettura e un'interfaccia utente del terminale in uscita: nessun account SaaS, nessun parco agenti, niente in uscita dalla macchina.**

[English](README.md) · [Español](README.es.md) · [Deutsch](README.de.md) · [Français](README.fr.md) · [Italiano](README.it.md) · [Português (BR)](README.pt-BR.md) · [Русский](README.ru.md) · [العربية](README.ar.md) · [हिन्दी](README.hi.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [中文](README.zh-CN.md)

[English](README.md) · [Español](README.es.md) · [Deutsch](README.de.md) · [Français](README.fr.md) · [Italiano](README.it.md) · [Português (BR)](README.pt-BR.md) · [Русский](README.ru.md) · [العربية](README.ar.md) · [हिन्दी](README.hi.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [中文](README.zh-CN.md)

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

## 📦 Installazione — 6 registri, 1 contratto via cavo

| Registro | Lingua | Pacchetto | Comando |
|----------|----------|---------|---------|
| **PyPI** | Python 3.10+ | `b4n1-telemetry` | `pip install b4n1-telemetry` |
| **npm** | Node.js | `b4n1-telemetry` | `npm install b4n1-telemetry` |
| **NuGet** | .NET/C# | `B4n1.Telemetry` | `dotnet add package B4n1.Telemetry` |
| **GemmeRubino** | Rubino | `b4n1-telemetry` | `gem install b4n1-telemetry` |
| **Packagista** | PHP 8.1+ | `b4n1/b4n1-telemetry` | `composer require b4n1/b4n1-telemetry` |
| **Andare** | Vai 1.21+ | `b4n1-telemetry/sdks/go` | `go get github.com/B4N1-com/b4n1-telemetry/sdks/go` |

Ogni cliente parla lo stesso contratto — `POST /api/v1/events` con un array JSON di buste e un token al portatore, quindi il raccoglitore viene eseguito una volta e uno qualsiasi dei sei client gli parla.

---

## 🚀Avvio rapido

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

## ⚡ Cosa fa

| Capacità | Cosa ottieni |
|------------|--------------|
| **Coda duratura** | Gli eventi vengono scritti nell'archivio locale prima di essere riconosciuti; un riavvio non perde il batch che pensavi di aver inviato. |
| **Retrotracce reali** | I frame vengono acquisiti durante il processo, non ricostruiti dalle linee di registro. |
| **Cancellazione PII durante l'acquisizione** | Ciò che viene filtrato prima di atterrare non può fuoriuscire successivamente. |
| **Catena causale** | Un ID di correlazione restituisce l'intero percorso intrapreso da una richiesta, in ordine. |
| **Problemi raggruppati** | Normalizzazione più fusione di similarità misurata: precisione 1,00 / richiamo 0,72 sul corpus etichettato, fissato dalla suite di test. |
| **Impronta delimitata** | Il negozio si compatta su un limite di byte impostato; una serie completa di 10.000 eventi ha misurato 17 MiB residenti. |
| **Lettura ospitata autonomamente** | Esegui query, problemi, traccia e statistiche sulla stessa API su cui scrivono i client. |

---

## 🖥 Supporto piattaforma

| Piattaforma | Architetture |
|----------|---------------|
| **Linux** | x86_64 (statico, nessun runtime da installare) |

Il raccoglitore è un processo senza dipendenze esterne; i client sono wrapper sottili su HTTP.

---

## 📄 Licenza

Licenza Business Source 1.1: vedere [LICENZA](LICENSE) e [EULA.txt](EULA.txt).
