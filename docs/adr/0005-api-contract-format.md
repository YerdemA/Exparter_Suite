# ADR 0005: API sözleşmesinin biçimi ve denetimi

- **Durum:** Kabul edildi
- **Tarih:** 2026-10-07

## Bağlam

`contracts/openapi.yaml` tek doğruluk kaynağıdır: backend ona uymak zorundadır, Flutter (ve ileride web) istemci kodu ondan üretilir. Sözleşmenin biçimi bu yüzden kod üreticilerinin desteğine göre seçilmelidir ve sözleşme her değişiklikte otomatik denetlenmelidir.

## Karar

- **OpenAPI 3.0.3** kullanılır. 3.1, JSON Schema ile tam uyumlu olsa da Dart tarafındaki kod üreticilerinde desteği daha az olgundur; 3.0 hepsinde sorunsuz çalışır.
- Sözleşme tek dosyadır. Bölünmesi, dosya okunamayacak kadar büyürse ayrıca değerlendirilir.
- Sayfalı yanıtlar her kaynak için ayrı şema olarak tanımlanır (`SkillPage`, `MatchPage` ...). OpenAPI'de jenerik tip yoktur; ayrı şemalar üretilen istemcide düzgün tipler verir.
- Sözleşme **Redocly CLI** ile denetlenir; sürüm komutta sabitlenir (şu an 2.60.0). Kurallar `redocly.yaml` dosyasındadır: önerilen kural seti, ek olarak her operasyonda `operationId` ve en az bir 4xx yanıtı zorunludur, kullanılmayan bileşen hatadır.
- SignalR hub'ları OpenAPI ile tarif edilemediği için `docs/realtime.md` dosyasında belgelenir.

## Sonuçlar

- Boş olabilen alanlar 3.0'ın `nullable: true` sözdizimiyle yazılır. 3.1'e geçilirse bu alanlar dönüştürülmelidir.
- Backend OpenAPI belgesi üretmez; sözleşme elle yazılan dosyadır. Uyum, rota düzeyinde bir entegrasyon testiyle denetlenir (bkz. ADR 0006).
- Lint, backend CI'ında her değişiklikte çalışır.
