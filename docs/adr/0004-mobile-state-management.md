# ADR 0004: Mobil state yönetimi

- **Durum:** Kabul edildi
- **Tarih:** 2026-10-06

## Bağlam

Mobil uygulama çoğunlukla sunucu verisi gösterir: listeler, ayrıntılar, gerçek zamanlı güncellenen sohbetler. Her ekranda yükleniyor, boş ve hata durumları ele alınmalı; widget'lar iş mantığı içermemeli. Ayrıca oturum durumu (giriş yapılmış mı, token yenileniyor mu) yönlendirmeyi ve tüm ağ katmanını etkiler.

## Karar

State yönetimi için Riverpod, kod üretimiyle (`@riverpod` anotasyonu) kullanılır.

- **Katmanlar:** Her feature `data/` (API client sarmalayıcıları, repository), `domain/` (modeller) ve `presentation/` (notifier'lar ve widget'lar) içerir. Mantık notifier'da, widget yalnızca durumu çizer.
- **Asenkron durum:** Sunucu verisi `AsyncValue` ile taşınır; yükleniyor, veri ve hata durumları tek tipte ve derleyici zorlamasıyla ele alınır.
- **Bağımlılık enjeksiyonu:** Ayrı bir DI kütüphanesi kullanılmaz; repository ve client'lar provider olarak tanımlanır ve testlerde override edilir.
- **Gerçek zamanlı veri:** SignalR bağlantısı bir provider'dır; hub olayları ilgili notifier'ların durumunu günceller.
- **Yönlendirme:** `go_router`, oturum durumu provider'ını dinleyerek yönlendirme kararı verir.

Değerlendirilen alternatif Bloc'tu. Bloc daha katı bir olay/durum ayrımı sunar, ama her özellik için daha fazla şablon kod gerektirir ve DI için ek bir çözüm ister. Tek geliştiricili bir projede Riverpod'un daha az kodla aynı test edilebilirliği sağlaması tercih sebebidir.

## Sonuçlar

- Kod üretimi `build_runner` adımı gerektirir; üretilen dosyalar (`*.g.dart`) repoya girmez ve CI'da üretilir. (ADR 0008 ile kesinleşti.)
- Provider'lar global erişilebilir olduğu için feature sınırları kuralla korunur: bir feature başka bir feature'ın `data/` katmanına doğrudan erişmez.
- Notifier'lar widget ağacı olmadan, `ProviderContainer` ile birim test edilebilir.
