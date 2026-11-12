# Exparter Suite

Yetenek takası platformu: kullanıcılar sunabildikleri ve öğrenmek istedikleri yetenekleri ekler, karşılıklı uyumlu kişilerle eşleşir, mesajlaşır, oturum planlar ve birbirini değerlendirir.

**Tek referans belge `docs/PROJECT_BRIEF.md` dosyasıdır.** Her oturumun başında onu oku. Bu dosya yalnızca özettir; çelişki olursa brief geçerlidir. Brief ile çelişen bir talimat gelirse uygulamadan önce bildir.

Güncel faz durumu ve geliştirme ortamı: `docs/roadmap.md`.

## Çalışma kuralları

- Önce plan, sonra kod. Her fazın başında planı yaz ve onay bekle; onay almadan faz atlama.
- Her fazın sonunda dur, özetle, `docs/roadmap.md` dosyasını güncelle ve onay bekle.
- Varsayım yapma, sor. Brief'te "AÇIK SORU" olarak işaretli konularda kendi başına karar verme.
- Aşırı mühendislik yok: kullanılmayan paket, katman veya "ileride lazım olur" kodu ekleme.
- Küçük adımlarla ilerle; her adımın sonunda proje derlenmeli ve testler geçmeli.
- Önemli teknik kararları `docs/adr/` altına ADR olarak kaydet.
- Mevcut bir dosyayı bütünüyle yeniden yazma, yalnızca gereken kısmı değiştir.
- Paket ve imaj sürümlerini ezberden yazma, kurmadan önce güncel kararlı sürümü kontrol et.

## Mimari (değiştirmek için ADR ve onay gerekir)

- **API-first:** `contracts/openapi.yaml` tek doğruluk kaynağıdır. Endpoint önce sözleşmede tanımlanır; istemci API kodu sözleşmeden üretilir, elle yazılmaz.
- **Modüler monolit:** `Identity`, `Profiles`, `Skills`, `Matching`, `Messaging`, `Sessions`, `Reviews`, `Notifications`, `Moderation`. Modüller birbirinin tablolarına erişmez; uygulama servisleri veya domain event'leriyle konuşur. Mikroservis ve event sourcing yok.
- **Gerçek zamanlılık:** SignalR hub'ları istemci tipinden bağımsızdır; arka planda FCM.
- **Dikey dilimler:** Her özellik sözleşme → backend → test → mobil ekran sırasıyla uçtan uca bitirilir.
- Backend, web istemcisini engelleyecek mobile özel varsayım içermez.

## Teknoloji

- **Backend:** .NET 10, ASP.NET Core Web API, Clean Architecture, PostgreSQL + EF Core, `Mediator` (martinothamar; **MediatR kullanma**), FluentValidation, ProblemDetails (RFC 9457), JWT + rotasyonlu refresh token, Serilog, xUnit + Testcontainers.
- **Mobil:** Flutter, feature-first (`data/`, `domain/`, `presentation/`), Riverpod (codegen), go_router, dio + üretilmiş client, freezed, flutter_secure_storage.
- **Nesne depolama:** S3 API'si (`IObjectStorage`); lokalde Garage, canlıda Cloudflare R2 (ADR 0010, 0014).
- **Görüntülü görüşme:** kendi sunucumuzda LiveKit; backend yalnızca görüşme anahtarı verir (ADR 0015).
- **Canlı ortam:** Oracle Cloud'da Docker Compose (API, PostgreSQL, Caddy, LiveKit); `main`'e merge edilen backend ve `deploy/` değişiklikleri otomatik dağıtılır (ADR 0014, `docs/deployment.md`).

## Kodlama kuralları

- Arayüz dili Türkçe ve "siz" hitabıyla (e-postalar dahil); düğme etiketleri kısa emir kipinde ("Giriş yap", "Kaydet"). Kod, adlandırma, yorumlar ve commit mesajları İngilizce.
- Conventional Commits. Her faz ayrı branch'te (`phase-N/...`), tamamlanınca `main`'e merge.
- Gizli bilgiler repoya girmez: lokalde `.env` veya user-secrets, repoda yalnızca `.env.example`.
- Domain katmanının dış bağımlılığı olmaz. Entity'ler zengin modeldir, setter'lar kapalıdır. Validation Application'da, iş kuralları Domain'de.
- Liste endpoint'lerinde sayfalama zorunlu (keşfet ve mesajlarda cursor tabanlı). Versiyonlama: `/api/v1/...`.
- Her command handler için birim testi, her endpoint için en az bir entegrasyon testi. Testsiz backend kodu bırakma.
- Yorumlar "ne"yi değil "neden"i anlatır.
- Mobil: widget'larda iş mantığı yok; metinler `l10n`'den; renk/tipografi/boşluk yalnızca `core/theme/` token'larından; her ekranda yükleniyor, boş ve hata durumları; ham hata metni gösterilmez.

## Ortam

- Windows 11, PowerShell. Altyapı Docker Desktop üzerinde.
- Altyapıyı başlatma: `.env.example` dosyasını `.env` olarak kopyala, sonra `docker compose up -d --wait` (PostgreSQL, Mailpit, Garage ve LiveKit).
- Backend komutları `backend/` klasöründen: `dotnet build`, `dotnet test` (Docker gerekir), `dotnet run --project src/Exparter.Api`. Gizli ayarlar user-secrets ile verilir; ayrıntı `docs/roadmap.md` içinde.
- Migration: `dotnet ef migrations add <Ad> --project src/Exparter.Infrastructure --startup-project src/Exparter.Api --output-dir Persistence/Migrations`.
- Sözleşme lint'i (kökten): `npx --yes @redocly/cli@2.60.0 lint`.
- Mobil komutları `mobile/` klasöründen. Üretilen kod repoda yok; önce üret: `flutter pub get`, `dart run swagger_parser`, `dart run build_runner build --delete-conflicting-outputs`. Sonra `flutter analyze`, `flutter test`, `flutter run`. Sözleşme değişince üretimi yeniden çalıştır.
- Hot reload (Flutter eklentisinin kuralı; eklenti bunu kendiliğinden yüklemiyor): `mobile/lib/` altında bir `.dart` dosyasını değiştirdikten sonra, çalışan uygulama varsa Dart MCP sunucusuyla yenile. Önce `dtd` aracıyla çalışan uygulamayı bul ve bağlan. Widget (`build` dahil) veya basit metot değişikliğinde `hot_reload`; `initState`, global/statik durum, `main()` veya temel mantık değişikliğinde `hot_restart`. `lib/` dışındaki dosyalarda (`test/`, `integration_test/` vb.) ve yalnızca yorum ya da boşluk değişikliklerinde tetikleme.
