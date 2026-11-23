<p align="center">
  <img src="b4n1telemetry_presentation.png" alt="B4N1 Telemetry — Errors with frames, grouped, on your own disk." width="100%" />
</p>

<div align="center">

# 📊 b4n1-telemetry

**Суверенная телеметрия. Входящие события, устойчивая очередь на локальном диске, API чтения и выходной пользовательский интерфейс терминала — ни учетной записи SaaS, ни парка агентов, ничего не покидает машину.**

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

## 📦 Установка — 6 реестров, 1 проводной договор

| Реестр | Язык | Упаковка | Команда |
|----------|----------|---------|---------|
| **ПиПИ** | Питон 3.10+ | `b4n1-telemetry` | `pip install b4n1-telemetry` |
| **нпм** | Node.js | `b4n1-telemetry` | `npm install b4n1-telemetry` |
| **НюГет** | .NET/С# | `B4n1.Telemetry` | `dotnet add package B4n1.Telemetry` |
| **РубинДжемс** | Руби | `b4n1-telemetry` | `gem install b4n1-telemetry` |
| **Упаковщик** | PHP 8.1+ | `b4n1/b4n1-telemetry` | `composer require b4n1/b4n1-telemetry` |
| **Идти** | Перейти 1.21+ | `b4n1-telemetry/sdks/go` | `go get github.com/B4N1-com/b4n1-telemetry/sdks/go` |

Каждый клиент говорит об одном и том же контракте — `POST /api/v1/events` с массивом конвертов JSON и токеном-носителем — поэтому сборщик запускается один раз, и с ним обращается любой из шести клиентов.

---

## 🚀 Быстрый старт

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

## ⚡ Что он делает

| Возможность | Что вы получаете |
|------------|--------------|
| **Постоянная очередь** | События записываются в локальное хранилище до их подтверждения; перезапуск не потеряет пакет, который, как вы думали, вы отправили. |
| **Настоящие следы** | Кадры захватываются в процессе, а не восстанавливаются из строк журнала. |
| **Очистка личных данных при приеме пищи** | То, что отфильтровано до того, как попадет на землю, не может просочиться позже. |
| **Причинно-следственная цепочка** | Один идентификатор корреляции возвращает весь путь, пройденный запросом, по порядку. |
| **Сгруппированные проблемы** | Нормализация плюс измеренное слияние сходства: точность 1,00 / полнота 0,72 для помеченного корпуса, закрепленного набором тестов. |
| **Ограниченный след** | Хранилище сжимается до установленного вами ограничения в байтах; полный цикл из 10 000 событий имел размер 17 МБ. |
| **Самостоятельное чтение** | Запросы, проблемы, отслеживание и статистика через тот же API, в который пишут клиенты. |

---

## 🖥 Поддержка платформы

| Платформа | Архитектуры |
|----------|---------------|
| **Линукс** | x86_64 (статический, нет среды выполнения для установки) |

Сборщик — это один процесс без внешних зависимостей; клиенты представляют собой тонкие оболочки HTTP.

---

## 📄 Лицензия

Лицензия Business Source 1.1 — см. [ЛИЦЕНЗИЯ](LICENSE) и [EULA.txt](EULA.txt).
