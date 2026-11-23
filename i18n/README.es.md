<p align="center">
  <img src="b4n1telemetry_presentation.png" alt="B4N1 Telemetry — Errors with frames, grouped, on your own disk." width="100%" />
</p>

<div align="center">

# 📊 b4n1-telemetry

**Telemetría soberana. Entrada de eventos, una cola duradera en el disco local, una API de lectura y una interfaz de usuario de terminal: sin cuenta SaaS, sin flota de agentes, nada sale de la máquina.**

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

## 📦 Instalación: 6 registros, 1 contrato electrónico

| Registro | Idioma | Paquete | Dominio |
|----------|----------|---------|---------|
| **PyPI** | Pitón 3.10+ | `b4n1-telemetry` | `pip install b4n1-telemetry` |
| **npm** | Nodo.js | `b4n1-telemetry` | `npm install b4n1-telemetry` |
| **NuGet** | .NET/C# | `B4n1.Telemetry` | `dotnet add package B4n1.Telemetry` |
| **RubíGemas** | Rubí | `b4n1-telemetry` | `gem install b4n1-telemetry` |
| **Empaquetador** | PHP 8.1+ | `b4n1/b4n1-telemetry` | `composer require b4n1/b4n1-telemetry` |
| **Ir** | Ir 1.21+ | `b4n1-telemetry/sdks/go` | `go get github.com/B4N1-com/b4n1-telemetry/sdks/go` |

Cada cliente habla el mismo contrato. `POST /api/v1/events` con una matriz JSON de sobres y un token al portador, de modo que el recopilador se ejecuta una vez y cualquiera de los seis clientes le habla.

---

## 🚀 Inicio rápido

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

## ⚡ Qué hace

| Capacidad | lo que obtienes |
|------------|--------------|
| **Cola duradera** | Los eventos se escriben en la tienda local antes de que se reconozcan; un reinicio no pierde el lote que creía haber enviado. |
| **Rastros reales** | Los fotogramas se capturan durante el proceso, no se reconstruyen a partir de líneas de registro. |
| **Lavado de PII al ingerir** | Lo que se filtra antes de aterrizar no puede filtrarse más tarde. |
| **Cadena causal** | Una identificación de correlación devuelve la ruta completa que tomó una solicitud, en orden. |
| **Temas agrupados** | Normalización más una combinación de similitud medida: precisión 1,00/recuperación 0,72 en el corpus etiquetado, fijado por el conjunto de pruebas. |
| **Huella delimitada** | La tienda se compacta hasta un límite de bytes que usted establezca; una serie completa de 10 000 eventos midió 17 MiB residentes. |
| **Lectura autohospedada** | Consultas, problemas, seguimiento y estadísticas a través de la misma API en la que escriben los clientes. |

---

## 🖥 Soporte de plataforma

| Plataforma | Arquitecturas |
|----------|---------------|
| **Linux** | x86_64 (estático, no hay tiempo de ejecución para instalar) |

El recopilador es un proceso sin dependencias externas; los clientes son contenedores ligeros sobre HTTP.

---

## 📄 Licencia

Licencia de origen empresarial 1.1: consulte [LICENCIA](LICENSE) y [CLUF.txt](EULA.txt).
