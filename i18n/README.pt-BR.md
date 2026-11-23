<p align="center">
  <img src="b4n1telemetry_presentation.png" alt="B4N1 Telemetry — Errors with frames, grouped, on your own disk." width="100%" />
</p>

<div align="center">

# 📊 b4n1-telemetry

**Telemetria soberana. Entrada de eventos, uma fila durável no disco local, uma API de leitura e uma saída de UI de terminal — sem conta SaaS, sem frota de agentes, nada saindo da máquina.**

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

## 📦 Instalação — 6 registros, 1 contrato de transferência

| Registro | Linguagem | Pacote | Comando |
|----------|----------|---------|---------|
| **PyPI** | Python 3.10+ | `b4n1-telemetry` | `pip install b4n1-telemetry` |
| **npm** | Node.js | `b4n1-telemetry` | `npm install b4n1-telemetry` |
| **NuGet** | .NET/C# | `B4n1.Telemetry` | `dotnet add package B4n1.Telemetry` |
| **RubyGems** | Rubi | `b4n1-telemetry` | `gem install b4n1-telemetry` |
| **Embalador** | PHP 8.1+ | `b4n1/b4n1-telemetry` | `composer require b4n1/b4n1-telemetry` |
| **Ir** | Vá para 1,21+ | `b4n1-telemetry/sdks/go` | `go get github.com/B4N1-com/b4n1-telemetry/sdks/go` |

Cada cliente fala o mesmo contrato - `POST /api/v1/events` com uma matriz JSON de envelopes e um token de portador — para que o coletor seja executado uma vez e qualquer um dos seis clientes fale com ele.

---

## 🚀 Início rápido

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

## ⚡ O que faz

| Capacidade | O que você ganha |
|------------|--------------|
| **Fila durável** | Os eventos são gravados na loja local antes de serem reconhecidos; uma reinicialização não perde o lote que você pensou ter enviado. |
| **Traços reais** | Os quadros são capturados em processo, não reconstruídos a partir de linhas de registro. |
| **Esfregação de PII na ingestão** | O que é filtrado antes de pousar não pode vazar depois. |
| **Cadeia causal** | Um ID de correlação retorna todo o caminho percorrido por uma solicitação, em ordem. |
| **Problemas agrupados** | Normalização mais uma mesclagem de similaridade medida: precisão 1,00 / recall 0,72 no corpus rotulado, fixado pelo conjunto de testes. |
| **Pegada limitada** | A loja é compactada para um limite de bytes que você define; uma série completa de 10.000 eventos mediu 17 MiB residentes. |
| **Leitura auto-hospedada** | Consultas, problemas, rastreamento e estatísticas na mesma API na qual os clientes escrevem. |

---

## 🖥 Suporte de plataforma

| Plataforma | Arquiteturas |
|----------|---------------|
| **Linux** | x86_64 (estático, sem tempo de execução para instalar) |

O coletor é um processo sem dependências externas; os clientes são thin wrappers sobre HTTP.

---

## 📄 Licença

Licença de fonte comercial 1.1 — consulte [LICENÇA](LICENSE) e [EULA.txt](EULA.txt).
