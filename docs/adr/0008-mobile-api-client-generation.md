# ADR 0008: Mobil API client üretimi

- **Durum:** Kabul edildi
- **Tarih:** 2026-10-07

## Bağlam

Brief gereği API çağrıları elle yazılmaz; istemci kodu `contracts/openapi.yaml` dosyasından üretilir. Üretilen kod projenin seçili yığınıyla (dio, freezed, json_serializable) uyumlu olmalı ve üretim adımı her geliştirici makinesinde ve CI'da kolayca çalışmalıdır.

## Karar

- Üretici olarak **swagger_parser** kullanılır. Saf Dart'tır (Java veya Docker gerektirmez), `dart run swagger_parser` ile çalışır ve retrofit tabanlı dio client'ları ile freezed modelleri üretir.
- Çıktı `mobile/lib/core/api/generated/` altına yazılır; ayarlar `mobile/swagger_parser.yaml` dosyasındadır.
- **Üretilen kod repoya girmez** (API client, `*.g.dart`, `*.freezed.dart`, `app_localizations*.dart`). Geliştirici ve CI aynı komutlarla üretir. Böylece üretilen kod sözleşmeden sapamaz ve sözleşmede uygulamayı bozan bir değişiklik CI'da derleme hatası olarak görünür.
- Bilinmeyen enum değerleri `$unknown` olarak okunur; backend'e yeni bir değer eklenmesi eski uygulama sürümünü çökertmez.
- Üretilen modeller uygulamada doğrudan kullanılır. Aynı alanları taşıyan ikinci bir "domain modeli" katmanı, bir ekran sözleşmedekinden farklı bir şekle ihtiyaç duyana kadar eklenmez.

Değerlendirilen alternatif OpenAPI Generator'dı (`dart-dio`). Java gerektirir ve çıktısı freezed yerine `built_value` kullanır; yığınımıza uymaz.

## Sonuçlar

- Sözleşme, üreticinin anlayabildiği biçimde yazılmalıdır. İlk üretimde bir kısıt çıktı: yol düzeyinde tanımlanan `parameters` desteklenmiyor; parametreler operasyon düzeyinde yazılır. Sözleşme buna göre düzeltildi.
- `mobile/` altında çalışmaya başlamadan önce üretim komutları çalıştırılmalıdır (bkz. `docs/roadmap.md`).
- ADR 0004'teki "üretilen dosyalar repoya girmez ve CI'da üretilir" notu bu kararla kesinleşti.
- OpenAPI 3.0.3 seçimi (ADR 0005) bu üreticiyle doğrulandı: 92 model ve 13 client hatasız üretiliyor.
