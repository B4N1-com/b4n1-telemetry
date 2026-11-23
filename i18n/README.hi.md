<p align="center">
  <img src="b4n1telemetry_presentation.png" alt="B4N1 Telemetry — Errors with frames, grouped, on your own disk." width="100%" />
</p>

<div align="center">

# 📊 b4n1-telemetry

**संप्रभु टेलीमेट्री। ईवेंट इन, स्थानीय डिस्क पर एक टिकाऊ कतार, एक रीड एपीआई और एक टर्मिनल यूआई आउट - कोई SaaS खाता नहीं, कोई एजेंट बेड़ा नहीं, मशीन को छोड़कर कुछ भी नहीं।**

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

## 📦 स्थापना - 6 रजिस्ट्रियां, 1 तार अनुबंध

| रजिस्ट्री | भाषा | पैकेट | आज्ञा |
|----------|----------|---------|---------|
| **पीईपीआई** | पायथन 3.10+ | `b4n1-telemetry` | `pip install b4n1-telemetry` |
| **एनपीएम** | नोड.जे.एस | `b4n1-telemetry` | `npm install b4n1-telemetry` |
| **न्यूगेट** | .नेट/सी# | `B4n1.Telemetry` | `dotnet add package B4n1.Telemetry` |
| **रूबीजेम्स** | रूबी | `b4n1-telemetry` | `gem install b4n1-telemetry` |
| **पैकेजिस्ट** | पीएचपी 8.1+ | `b4n1/b4n1-telemetry` | `composer require b4n1/b4n1-telemetry` |
| **जाना** | 1.21+ जाओ | `b4n1-telemetry/sdks/go` | `go get github.com/B4N1-com/b4n1-telemetry/sdks/go` |

प्रत्येक ग्राहक एक ही अनुबंध बोलता है - `POST /api/v1/events` लिफाफों की JSON श्रृंखला और एक वाहक टोकन के साथ - इसलिए कलेक्टर एक बार चलता है और छह ग्राहकों में से कोई भी उससे बात करता है।

---

## 🚀 त्वरित शुरुआत

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

## ⚡यह क्या करता है

| क्षमता | आपको क्या मिलता है |
|------------|--------------|
| **टिकाऊ कतार** | घटनाओं को स्वीकार करने से पहले स्थानीय स्टोर को लिखा जाता है; पुनरारंभ करने से वह बैच नहीं खोता जिसे आपने सोचा था कि आपने भेजा है। |
| **असली पृष्ठभूमि के निशान** | फ़्रेम को प्रक्रिया के दौरान कैप्चर किया जाता है, लॉग लाइनों से पुनर्निर्मित नहीं किया जाता है। |
| **निगलने पर पीआईआई स्क्रबिंग** | जमीन पर उतरने से पहले जो फ़िल्टर किया जाता है वह बाद में लीक नहीं हो सकता। |
| **कारण श्रृंखला** | एक सहसंबंध आईडी अनुरोध द्वारा लिए गए पूरे पथ को क्रमानुसार लौटाता है। |
| **समूहीकृत मुद्दे** | सामान्यीकरण प्लस एक मापा समानता मर्ज: लेबल किए गए कॉर्पस पर परिशुद्धता 1.00 / रिकॉल 0.72, परीक्षण सूट द्वारा पिन किया गया। |
| **सीमाबद्ध पदचिह्न** | स्टोर आपके द्वारा निर्धारित बाइट कैप से संकुचित हो जाता है; 10,000 आयोजनों की पूरी श्रृंखला में 17 एमआईबी निवासी को मापा गया। |
| **स्व-मेजबान पढ़ना** | क्वेरी, मुद्दे, ट्रेस और आँकड़े उसी एपीआई पर होते हैं जिस पर ग्राहक लिखते हैं। |

---

## 🖥 प्लेटफार्म समर्थन

| प्लैटफ़ॉर्म | वास्तुकला |
|----------|---------------|
| **लिनक्स** | x86_64 (स्थिर, स्थापित करने के लिए कोई रनटाइम नहीं) |

संग्राहक एक ऐसी प्रक्रिया है जिसमें कोई बाहरी निर्भरता नहीं है; क्लाइंट HTTP पर पतले आवरण हैं।

---

## 📄लाइसेंस

बिजनेस सोर्स लाइसेंस 1.1 - देखें [लाइसेंस](LICENSE) और [EULA.txt](EULA.txt).
