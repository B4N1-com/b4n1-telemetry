<p align="center">
  <img src="b4n1telemetry_presentation.png" alt="B4N1 Telemetry — Errors with frames, grouped, on your own disk." width="100%" />
</p>

<div align="center">

# 📊 b4n1-telemetry

**主权遥测。事件输入、本地磁盘上的持久队列、读取 API 和终端 UI 输出 — 没有 SaaS 帐户，没有代理队列，没有任何东西离开机器。**

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

## 📦 安装 — 6 个注册表，1 个电汇合同

| 登记处 | 语言 | 包裹 | 命令 |
|----------|----------|---------|---------|
| **PyPI** | Python 3.10+ | `b4n1-telemetry` | `pip install b4n1-telemetry` |
| **npm** | Node.js | `b4n1-telemetry` | `npm install b4n1-telemetry` |
| **NuGet** | .NET / C# | `B4n1.Telemetry` | `dotnet add package B4n1.Telemetry` |
| **红宝石** | 红宝石 | `b4n1-telemetry` | `gem install b4n1-telemetry` |
| **包装师** | PHP 8.1+ | `b4n1/b4n1-telemetry` | `composer require b4n1/b4n1-telemetry` |
| **去** | 去1.21+ | `b4n1-telemetry/sdks/go` | `go get github.com/B4N1-com/b4n1-telemetry/sdks/go` |

每个客户都讲同一份合同—— `POST /api/v1/events` 带有信封和不记名令牌的 JSON 数组 - 因此收集器运行一次，六个客户端中的任何一个都可以与它对话。

---

## 🚀 快速入门

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

## ⚡ 它的作用

| 能力 | 你得到什么 |
|------------|--------------|
| **持久队列** | 事件在被确认之前被写入本地存储；重新启动不会丢失您认为已发送的批次。 |
| **真实的回溯** | 帧是在进程中捕获的，而不是从日志行重建的。 |
| **摄取时清除 PII** | 降落前经过过滤的物质以后不会泄漏。 |
| **因果链** | 一个相关 ID 按顺序返回请求所采用的整个路径。 |
| **分组问题** | 标准化加上测量的相似性合并：标记语料库上的精度 1.00 / 召回率 0.72，由测试套件固定。 |
| **有界足迹** | 存储压缩到您设置的字节上限；完整运行 10 000 个事件测量了 17 MiB 驻留。 |
| **自托管阅读** | 通过客户端写入的同一 API 进行查询、问题、跟踪和统计。 |

---

## 🖥 平台支持

| 平台 | 架构 |
|----------|---------------|
| **Linux** | x86_64（静态，无需安装运行时） |

收集器是一个没有外部依赖的进程；客户端是 HTTP 上的瘦包装器。

---

## 📄 许可证

商业源许可证 1.1 — 请参阅 [许可证](LICENSE) 和 [EULA.txt](EULA.txt)。
