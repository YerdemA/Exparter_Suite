# Exparter

Yetenek takası platformu. Kullanıcılar sunabildikleri ve öğrenmek istedikleri yetenekleri ekler, karşılıklı uyumlu kişilerle eşleşir, mesajlaşır, çevrimiçi oturumları uygulamanın içinden görüntülü görüşmeyle yapar ve birbirini değerlendirir.

- Durum: Android uygulaması Google Play'de kapalı testte; backend canlıda (`api.exparter.app`). Faz durumu: [docs/roadmap.md](docs/roadmap.md).
- Tek referans belge: [docs/PROJECT_BRIEF.md](docs/PROJECT_BRIEF.md). Çalışma kuralları: [CLAUDE.md](CLAUDE.md).

## Yapı

| Klasör | İçerik |
|---|---|
| `contracts/openapi.yaml` | API sözleşmesi; tek doğruluk kaynağı. İstemci kodu buradan üretilir. |
| `backend/` | .NET 10 modüler monolit (Clean Architecture), PostgreSQL, SignalR. |
| `mobile/` | Flutter uygulaması (Riverpod, go_router, üretilmiş API client). |
| `deploy/` | Canlı ortam: Docker Compose, Caddy, LiveKit, yedek betiği, herkese açık sayfalar. |
| `infra/` | Lokal servislerin ayarları (Garage, LiveKit). |
| `scripts/` | Admin betikleri ve mağaza görüntüsü verisi. |
| `docs/` | Brief, ADR'ler, domain modeli, ERD, API kuralları, dağıtım ve mağaza rehberleri. |
| `web/` | Web istemcisi (Faz 7, henüz başlamadı). |

## Lokal çalıştırma

Gerekenler: Docker Desktop, .NET 10 SDK, Flutter (stable), Android emülatörü, sözleşme lint'i için Node.js. Komutlar PowerShell içindir.

**1. Altyapı** (PostgreSQL, Mailpit, Garage, LiveKit), repo kökünden:

```powershell
Copy-Item .env.example .env   # içindeki açıklamalara göre gizli değerleri doldurun
docker compose up -d --wait
```

**2. Backend**, `backend/` klasöründen. Gizli ayarlar user-secrets ile verilir:

```powershell
dotnet user-secrets set --project src/Exparter.Api "ConnectionStrings:Database" "Host=localhost;Port=<POSTGRES_PORT>;Database=exparter;Username=exparter;Password=<POSTGRES_PASSWORD>"
dotnet user-secrets set --project src/Exparter.Api "Jwt:SigningKey" "<en az 32 karakterlik rastgele değer>"
dotnet user-secrets set --project src/Exparter.Api "Storage:AccessKey" "<STORAGE_ACCESS_KEY>"
dotnet user-secrets set --project src/Exparter.Api "Storage:SecretKey" "<STORAGE_SECRET_KEY>"
dotnet user-secrets set --project src/Exparter.Api "LiveKit:ApiKey" "<LIVEKIT_API_KEY>"
dotnet user-secrets set --project src/Exparter.Api "LiveKit:ApiSecret" "<LIVEKIT_API_SECRET>"
dotnet run --project src/Exparter.Api
```

API `http://localhost:5244` adresinde açılır ve geliştirmede migration'ları kendisi uygular. Gönderilen e-postalar `http://localhost:8025` adresinde (Mailpit) görünür. Telefon bildirimi (`Firebase:CredentialsPath`) ve hata izleme (`Sentry:Dsn`) isteğe bağlıdır; ayrıntı [docs/roadmap.md](docs/roadmap.md).

**3. Mobil**, `mobile/` klasöründen. Üretilen kod repoda yoktur, önce üretilir; Android derlemesi için Firebase'den indirilen `google-services.json` `mobile/android/app/` altına konur:

```powershell
flutter pub get
dart run swagger_parser
dart run build_runner build --delete-conflicting-outputs
flutter run
```

Emülatör bilgisayara `10.0.2.2` üzerinden ulaşır; varsayılan API adresi budur. Başka bir adres için `--dart-define=API_BASE_URL=...`.

## Testler

```powershell
cd backend; dotnet test          # Docker çalışıyor olmalı (Testcontainers)
cd mobile; flutter analyze; flutter test
npx --yes @redocly/cli@2.60.0 lint   # repo kökünden, sözleşme lint'i
```

CI, `main` ve `phase-*` branch'lerine yapılan push'larda ve pull request'lerde ilgili klasör değiştiyse çalışır: backend iş akışı sözleşme lint'ini, derlemeyi ve testleri, mobil iş akışı kod üretimini, analizi ve testleri (`.github/workflows/`). Backend iş akışı paketlerdeki güvenlik açıkları için haftada bir de çalışır.

## Belgeler

| Belge | Konu |
|---|---|
| [docs/adr/](docs/adr/) | Mimari kararlar ve gerekçeleri |
| [docs/domain/](docs/domain/) | Domain modeli, iş kuralları, sözlük, kararlar |
| [docs/erd/erd.md](docs/erd/erd.md) | Veritabanı şeması |
| [docs/api-conventions.md](docs/api-conventions.md) | Sayfalama, hata biçimi, hata kodları, sınırlar |
| [docs/realtime.md](docs/realtime.md) | SignalR hub'ı ve telefon bildirimleri |
| [docs/deployment.md](docs/deployment.md) | Canlı ortamın kurulumu, dağıtım, yedek, günlük işler |
| [docs/store/listing.md](docs/store/listing.md) | Google Play kaydı, formlar, kapalı test |
| [docs/legal/](docs/legal/) | KVKK ve mevzuat uyum araştırması |

## Dağıtım

`main`'e merge edilen backend veya `deploy/` değişiklikleri otomatik olarak canlıya dağıtılır: imaj GitHub Container Registry'ye yayımlanır, sunucu hazırlanır, migration çalışır ve yeni sürüm başlar ([ADR 0014](docs/adr/0014-deployment.md)). Mobil sürümler elle derlenip Play Console'a yüklenir ([docs/deployment.md](docs/deployment.md), 9. bölüm).
