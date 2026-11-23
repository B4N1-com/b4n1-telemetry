<p align="center">
  <img src="b4n1telemetry_presentation.png" alt="B4N1 Telemetry — Errors with frames, grouped, on your own disk." width="100%" />
</p>

<div align="center">

# 📊 b4n1-telemetry

**ソブリンテレメトリ。イベントの入力、ローカル ディスク上の永続キュー、読み取り API、およびターミナル UI の出力 — SaaS アカウントもエージェント フリートも、マシンから何も出力されません。**

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

## 📦 インストール — 6 つのレジストリ、1 つの通信契約

| レジストリ | 言語 | パッケージ | 指示 |
|----------|----------|---------|---------|
| **PyPI** | Python 3.10+ | `b4n1-telemetry` | `pip install b4n1-telemetry` |
| **npm** | Node.js | `b4n1-telemetry` | `npm install b4n1-telemetry` |
| **NuGet** | .NET / C# | `B4n1.Telemetry` | `dotnet add package B4n1.Telemetry` |
| **ルビージェム** | ルビー | `b4n1-telemetry` | `gem install b4n1-telemetry` |
| **パッケージニスト** | PHP 8.1+ | `b4n1/b4n1-telemetry` | `composer require b4n1/b4n1-telemetry` |
| **行く** | ゴー 1.21+ | `b4n1-telemetry/sdks/go` | `go get github.com/B4N1-com/b4n1-telemetry/sdks/go` |

すべてのクライアントは同じ契約を話します — `POST /api/v1/events` エンベロープの JSON 配列とベアラー トークンを使用するため、コレクターは 1 回実行され、6 つのクライアントのいずれかがコレクターと通信します。

---

## 🚀 クイックスタート

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

## ⚡ 機能

| 能力 | 得られるもの |
|------------|--------------|
| **耐久性のあるキュー** | イベントは、確認される前にローカル ストアに書き込まれます。再起動しても、送信したはずのバッチは失われません。 |
| **実際のバックトレース** | フレームはログ行から再構築されるのではなく、プロセス中にキャプチャされます。 |
| **取り込み時の PII スクラブ** | 着地前にろ過されたものは、後で漏れることはありません。 |
| **因果連鎖** | 1 つの相関 ID は、リクエストがたどったパス全体を順番に返します。 |
| **グループ化された問題** | 正規化と測定された類似性のマージ: テスト スイートによって固定されたラベル付きコーパスの精度 1.00 / 再現率 0.72。 |
| **境界のあるフットプリント** | ストアは、設定したバイト キャップに圧縮されます。 10,000 イベントのフル実行では、17 MiB の常駐が測定されました。 |
| **自己ホスト型の読み取り** | クライアントが書き込む同じ API を介してクエリ、問題、トレース、統計を実行します。 |

---

## 🖥 プラットフォームのサポート

| プラットフォーム | アーキテクチャ |
|----------|---------------|
| **Linux** | x86_64 (静的、ランタイムのインストールなし) |

コレクターは外部依存関係のない 1 つのプロセスです。クライアントは HTTP 上のシン ラッパーです。

---

## 📄ライセンス

ビジネス ソース ライセンス 1.1 — [ライセンス] を参照](LICENSE) および [EULA.txt]](EULA.txt)。
