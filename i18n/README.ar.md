<p align="center">
  <img src="b4n1telemetry_presentation.png" alt="B4N1 Telemetry — Errors with frames, grouped, on your own disk." width="100%" />
</p>

<div align="center">

# 📊 b4n1-telemetry

** القياس عن بعد السيادي. الأحداث الواردة، وقائمة انتظار دائمة على القرص المحلي، وواجهة برمجة تطبيقات للقراءة، وواجهة مستخدم طرفية للخارج - لا يوجد حساب SaaS، ولا يوجد أسطول من الوكلاء، ولا شيء يغادر الجهاز.**

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

## 📦 التثبيت — 6 سجلات، 1 عقد سلكي

| التسجيل | لغة | طَرد | يأمر |
|----------|----------|---------|---------|
| **بايبي** | بايثون 3.10+ | `b4n1-telemetry` | `pip install b4n1-telemetry` |
| **نبم** | Node.js | `b4n1-telemetry` | `npm install b4n1-telemetry` |
| ** نوجيت ** | صافي / سي # | `B4n1.Telemetry` | `dotnet add package B4n1.Telemetry` |
| **جواهر روبي** | روبي | `b4n1-telemetry` | `gem install b4n1-telemetry` |
| **باكاجيست** | بي إتش بي 8.1+ | `b4n1/b4n1-telemetry` | `composer require b4n1/b4n1-telemetry` |
| **يذهب** | الذهاب 1.21+ | `b4n1-telemetry/sdks/go` | `go get github.com/B4N1-com/b4n1-telemetry/sdks/go` |

كل عميل يتحدث نفس العقد - `POST /api/v1/events` مع مجموعة JSON من الأظرف والرمز المميز لحاملها - بحيث يعمل المجمع مرة واحدة ويتحدث إليه أي من العملاء الستة.

---

## 🚀 بداية سريعة

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

## ⚡ ماذا يفعل

| القدرة | ما تحصل عليه |
|------------|--------------|
| ** طابور دائم ** | تتم كتابة الأحداث إلى المتجر المحلي قبل أن يتم الإقرار بها؛ لن تؤدي إعادة التشغيل إلى فقدان الدفعة التي اعتقدت أنك أرسلتها. |
| **الخلفيات الحقيقية** | يتم التقاط الإطارات أثناء العملية، ولا يتم إعادة بنائها من سطور السجل. |
| **تنقية معلومات تحديد الهوية الشخصية عند الابتلاع** | ما يتم تصفيته قبل أن يهبط لا يمكن أن يتسرب لاحقًا. |
| **السلسلة السببية** | يقوم أحد معرفات الارتباط بإرجاع المسار الكامل الذي اتخذه الطلب بالترتيب. |
| **قضايا جماعية** | التطبيع بالإضافة إلى دمج التشابه المقاس: الدقة 1.00 / الاستدعاء 0.72 على المجموعة المسمى، المثبتة بواسطة مجموعة الاختبار. |
| **بصمة محدودة** | يضغط المتجر إلى الحد الأقصى للبايت الذي قمت بتعيينه؛ مجموعة كاملة من 10000 حدث بقياس 17 ميجابايت مقيم. |
| ** القراءة الذاتية ** | الاستعلام والمشكلات والتتبع والإحصائيات عبر نفس واجهة برمجة التطبيقات التي يكتب إليها العملاء. |

---

## 🖥 دعم المنصة

| منصة | أبنية |
|----------|---------------|
| **لينكس** | x86_64 (ثابت، لا يوجد وقت تشغيل للتثبيت) |

المجمع عبارة عن عملية واحدة بدون أي تبعيات خارجية؛ العملاء عبارة عن أغلفة رقيقة عبر HTTP.

---

## 📄 الترخيص

ترخيص مصدر الأعمال 1.1 - راجع [الترخيص](LICENSE) و[EULA.txt](EULA.txt).
