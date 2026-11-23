<p align="center">
  <img src="b4n1telemetry_presentation.png" alt="B4N1 Telemetry — Errors with frames, grouped, on your own disk." width="100%" />
</p>

<div align="center">

# 📊 b4n1-telemetry

**Télémétrie souveraine. Entrées d'événements, file d'attente durable sur le disque local, API de lecture et sortie de l'interface utilisateur du terminal : pas de compte SaaS, pas de flotte d'agents, rien ne quitte la machine.**

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

## 📦 Installation — 6 registres, 1 contrat filaire

| Enregistrement | Langue | Emballer | Commande |
|----------|----------|---------|---------|
| **PyPI** | Python3.10+ | `b4n1-telemetry` | `pip install b4n1-telemetry` |
| **npm** | Noeud.js | `b4n1-telemetry` | `npm install b4n1-telemetry` |
| **NuGet** | .NET/C# | `B4n1.Telemetry` | `dotnet add package B4n1.Telemetry` |
| **RubisGemmes** | Rubis | `b4n1-telemetry` | `gem install b4n1-telemetry` |
| **Emballeur** | PHP8.1+ | `b4n1/b4n1-telemetry` | `composer require b4n1/b4n1-telemetry` |
| **Aller** | Allez 1.21+ | `b4n1-telemetry/sdks/go` | `go get github.com/B4N1-com/b4n1-telemetry/sdks/go` |

Chaque client parle du même contrat — `POST /api/v1/events` avec un tableau JSON d'enveloppes et un jeton de porteur - de sorte que le collecteur s'exécute une fois et que l'un des six clients lui parle.

---

## 🚀 Démarrage rapide

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

## ⚡ Ce que ça fait

| Capacité | Ce que vous obtenez |
|------------|--------------|
| **File d'attente durable** | Les événements sont écrits dans le magasin local avant d'être reconnus ; un redémarrage ne perd pas le lot que vous pensiez avoir envoyé. |
| **De vraies traces** | Les images sont capturées en cours de processus et non reconstruites à partir des lignes de journal. |
| ** Lavage des informations personnelles lors de l'ingestion ** | Ce qui est filtré avant d’atterrir ne peut pas fuir plus tard. |
| **Chaîne causale** | Un identifiant de corrélation renvoie le chemin complet emprunté par une requête, dans l'ordre. |
| **Problèmes groupés** | Normalisation plus une fusion de similarité mesurée : précision 1,00 / rappel 0,72 sur le corpus étiqueté, épinglé par la suite de tests. |
| **Empreinte délimitée** | Le magasin se compacte en fonction d'une limite d'octets que vous avez définie ; une série complète de 10 000 événements a mesuré 17 Mio de résident. |
| **Lecture auto-hébergée** | Requête, problèmes, trace et statistiques sur la même API sur laquelle les clients écrivent. |

---

## 🖥 Prise en charge de la plateforme

| Plate-forme | Architectures |
|----------|---------------|
| **Linux** | x86_64 (statique, aucun runtime à installer) |

Le collecteur est un processus sans dépendances externes ; les clients sont des wrappers légers sur HTTP.

---

## 📄 Licence

Licence Business Source 1.1 — voir [LICENCE](LICENSE) et [CLUF.txt](EULA.txt).
