<p align="center">
  <img src="b4n1telemetry_presentation.png" alt="B4N1 Telemetry — Errors with frames, grouped, on your own disk." width="100%" />
</p>

<div align="center">

# 📊 b4n1-telemetry

**소버린 원격 측정. 이벤트 수신, 로컬 디스크의 내구성 있는 대기열, 읽기 API 및 터미널 UI 출력 — SaaS 계정 없음, 에이전트 플릿 없음, 시스템에서 나가는 것은 아무것도 없습니다.**

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

## 📦 설치 — 6개 레지스트리, 1개 유선 계약

| 기재 | 언어 | 패키지 | 명령 |
|----------|----------|---------|---------|
| **PyPI** | 파이썬 3.10+ | `b4n1-telemetry` | `pip install b4n1-telemetry` |
| **npm** | Node.js | `b4n1-telemetry` | `npm install b4n1-telemetry` |
| **누겟** | .NET/C# | `B4n1.Telemetry` | `dotnet add package B4n1.Telemetry` |
| **RubyGems** | 루비 | `b4n1-telemetry` | `gem install b4n1-telemetry` |
| **패키지 담당자** | PHP 8.1+ | `b4n1/b4n1-telemetry` | `composer require b4n1/b4n1-telemetry` |
| **가다** | 1.21+로 이동 | `b4n1-telemetry/sdks/go` | `go get github.com/B4N1-com/b4n1-telemetry/sdks/go` |

모든 고객은 동일한 계약을 체결합니다. `POST /api/v1/events` JSON 배열의 봉투와 전달자 토큰을 사용합니다. 따라서 수집기는 한 번 실행되고 6개의 클라이언트 중 하나가 이에 대해 이야기합니다.

---

## 🚀 빠른 시작

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

## ⚡ 그것이 하는 일

| 능력 | 당신이 얻는 것 |
|------------|--------------|
| **지속성 대기열** | 이벤트는 승인되기 전에 로컬 저장소에 기록됩니다. 다시 시작해도 전송했다고 생각한 배치가 손실되지 않습니다. |
| **실제 역추적** | 프레임은 로그 라인에서 재구성되지 않고 프로세스 중에 캡처됩니다. |
| **수집 시 PII 스크러빙** | 착륙하기 전에 여과된 것은 나중에 누출될 수 없습니다. |
| **인과관계** | 하나의 상관 관계 ID는 요청이 수행한 전체 경로를 순서대로 반환합니다. |
| **그룹화된 문제** | 정규화와 측정된 유사성 병합: 테스트 모음에 의해 고정된 라벨이 붙은 말뭉치에 대한 정밀도 1.00 / 재현율 0.72입니다. |
| **제한된 공간** | 저장소는 사용자가 설정한 바이트 한도로 압축됩니다. 10,000개의 이벤트 전체 실행에서 17MiB의 상주가 측정되었습니다. |
| **자체 호스팅 읽기** | 클라이언트가 작성하는 동일한 API를 통해 쿼리, 문제, 추적 및 통계를 수행합니다. |

---

## 🖥 플랫폼 지원

| 플랫폼 | 아키텍처 |
|----------|---------------|
| **리눅스** | x86_64(정적, 설치할 런타임 없음) |

수집기는 외부 종속성이 없는 하나의 프로세스입니다. 클라이언트는 HTTP를 통한 씬 래퍼입니다.

---

## 📄 라이선스

비즈니스 소스 라이선스 1.1 - [LICENSE] 참조](LICENSE) 그리고 [EULA.txt](EULA.txt).
