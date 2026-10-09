# Yol Haritası

Fazların ayrıntısı için `docs/PROJECT_BRIEF.md` Bölüm 9'a bak.

## Faz durumu

| Faz | Konu | Durum |
|---|---|---|
| 0 | Kurulum | Tamamlandı (2026-10-06) |
| 1 | Domain ve veri tasarımı | Tamamlandı (2026-10-07) |
| 2 | API sözleşmesi | Tamamlandı (2026-10-07) |
| 3 | Backend iskeleti | Tamamlandı (2026-10-07) |
| 4 | Mobil iskelet | Tamamlandı (2026-10-09) |
| 5 | Dikey dilimler | Sürüyor: dilim 0 tamamlandı; dilim 1 onay bekliyor |
| 6 | Sertleştirme ve MVP yayını | Başlamadı |
| 7 | Web istemcisi | Başlamadı |

## Faz 0 — Kurulum

- Repo yapısı, `.gitignore`, `.gitattributes`, `.editorconfig`, `CLAUDE.md` eklendi.
- `docker-compose.yml` yalnızca PostgreSQL içerir. Host portu `.env` içindeki `POSTGRES_PORT` ile ayarlanır (varsayılan 5432).
- MinIO kapsamdan çıkarıldı. S3 uyumlu nesne depolama ürünü Faz 5.1'de (avatar yükleme) ADR ile seçilecek.
- Kabul kriteri doğrulandı (2026-10-06): `docker compose up -d --wait` ile PostgreSQL `healthy` duruma geldi ve sorgu yanıtladı.

## Faz 1 — Domain ve veri tasarımı

- `docs/domain/`: sözlük, domain modeli, iş kuralları, kararlar ve açık sorular.
- `docs/erd/erd.md`: Mermaid ERD, tablo sahipliği, benzersizlik kısıtları.
- `docs/adr/`: 0001 modüler monolit, 0002 PostgreSQL, 0003 auth stratejisi, 0004 mobil state yönetimi.
- Domain modeli ve ERD 2026-10-07'de onaylandı.

## Faz 2 — API sözleşmesi

- `contracts/openapi.yaml`: MVP'nin tamamı; 74 operasyon, 87 şema (OpenAPI 3.0.3).
- `docs/api-conventions.md`: ortak standartlar, sayfalama, hata biçimi ve hata kodları.
- `docs/realtime.md`: SignalR hub'ı, olaylar ve push kuralları.
- `docs/adr/0005-api-contract-format.md`: sözleşme biçimi ve lint aracı.
- Lint: `npx --yes @redocly/cli@2.60.0 lint` hatasız geçiyor (2026-10-07); iki bulgu bilinçli olarak yok sayılıyor.

## Faz 3 — Backend iskeleti

- `backend/Exparter.sln`: dört katman projesi ve üç test projesi; modüller katmanların içinde klasör (ADR 0006).
- Altyapı: Serilog, ProblemDetails ve hata kodları, FluentValidation hattı, JWT doğrulama, `/health`.
- Veritabanı: EF Core + Npgsql, `identity` şeması, ilk migration (`InitialIdentity`).
- Identity modülü: sözleşmedeki Auth ve Account endpoint'lerinin tamamı (11 operasyon). Kayıtta kullanıcı adı alınır; giriş e-posta veya kullanıcı adıyla yapılır; kullanıcı adı değiştirilebilir.
- E-posta: SMTP ile gönderim, geliştirmede Mailpit (ADR 0007).
- Testler (2026-10-07): 41 domain, 55 uygulama, 21 entegrasyon testi geçiyor. Entegrasyon testleri Testcontainers ile gerçek PostgreSQL kullanır.
- CI: `.github/workflows/backend-ci.yml` sözleşme lint'i, build ve testleri çalıştırır.
- Faz 5'e bırakılanlar: hesabın 30 gün sonunda kalıcı silinmesi ve e-posta için 3 aylık bekleme süresi (`Deleted` durumu, `DeletedAt`, `DeletedEmailHash` alanları henüz yok); e-posta doğrulaması gerektiren yetkilendirme kuralı (ilk kullanan endpoint ile gelir).
- Bilinen sınır: hesabı askıya alacak bir endpoint yok; `Suspended` durumu şimdilik yalnızca veritabanından elle verilebilir.

### Lokal çalıştırma

```powershell
docker compose up -d --wait
cd backend
dotnet user-secrets set --project src/Exparter.Api "ConnectionStrings:Database" "Host=localhost;Port=5432;Database=exparter;Username=exparter;Password=<.env içindeki parola>"
dotnet user-secrets set --project src/Exparter.Api "Jwt:SigningKey" "<en az 32 karakterlik rastgele değer>"
dotnet run --project src/Exparter.Api
```

API `http://localhost:5244` adresinde açılır ve Development ortamında migration'ları kendisi uygular. Gönderilen e-postalar `http://localhost:8025` adresinde görünür. Testler: `dotnet test` (Docker çalışıyor olmalı).

## Faz 4 — Mobil iskelet

- `mobile/`: Flutter projesi (`com.exparter.app`), feature-first yapı; tema token'ları (`core/theme/`), `l10n` (Türkçe), go_router, Riverpod (kod üretimiyle).
- API client sözleşmeden üretilir (swagger_parser, ADR 0008). Üretilen kod repoya girmez.
- Oturum: token'lar ve hesap güvenli depolamada; süresi dolan access token tek seferde yenilenir, bekleyen istekler yeniden denenir; refresh token reddedilirse kullanıcı giriş ekranına döner.
- Ekranlar: kayıt (kullanıcı adı ve 18 yaş onayıyla), giriş (e-posta veya kullanıcı adı), çıkışlı geçici ana ekran. Hata kodları Türkçe mesajlara eşlenir.
- Testler: 32 birim ve widget testi geçiyor. Uçtan uca test (`integration_test/auth_flow_test.dart`) 2026-10-09'da Pixel 6 emülatöründe gerçek backend'e karşı geçti: kayıt, yeniden açılışta oturumun korunması, çıkış, hatalı parola mesajı ve kullanıcı adıyla giriş.
- CI: `.github/workflows/mobile-ci.yml` kodu üretir, analiz eder ve testleri çalıştırır.
- Faz 5'in ilk dilimine bırakılanlar: e-posta doğrulama ve parola sıfırlama ekranları ile derin bağlantı.
- Identity'ye bu fazda eklenen: kullanıcı adı 7 günde bir kez değiştirilebilir.

### Mobil uygulamayı lokal çalıştırma

```powershell
cd mobile
flutter pub get
dart run swagger_parser
dart run build_runner build --delete-conflicting-outputs
flutter run                                   # backend http://localhost:5244 adresinde çalışıyor olmalı
flutter test integration_test -d emulator-5554   # uçtan uca test; emülatör ve backend açık olmalı
```

Android emülatörü bilgisayarın `localhost` adresine `10.0.2.2` üzerinden ulaşır; varsayılan API adresi budur. Başka bir adres için `--dart-define=API_BASE_URL=...` verilir.

## Faz 5 — Dikey dilimler

Her dilim kendi branch'inde (`phase-5/<n>-<konu>`) yapılır ve onayla `main`'e merge edilir.

| Dilim | Konu | Durum |
|---|---|---|
| 0 | E-posta doğrulama ve parola sıfırlama | Tamamlandı (2026-10-09) |
| 1 | Profil ve avatar | Tamamlandı, onay bekliyor |
| 2 | Yetenek kataloğu ve kullanıcı yetenekleri | Başlamadı |
| 3 | Keşfet | Başlamadı |
| 4 | Takas isteği ve eşleşme | Başlamadı |
| 5 | Gerçek zamanlı mesajlaşma | Başlamadı |
| 6 | Takas oturumları | Başlamadı |
| 7 | Değerlendirmeler | Başlamadı |
| 8 | Bildirimler | Başlamadı |

### Dilim 0 — E-posta doğrulama ve parola sıfırlama

- Backend endpoint'leri Faz 3'te hazırdı; bu dilim mobil taraftır. Backend'de yalnızca e-postadaki bağlantı adresleri değişti (`exparter://app/...`, ADR 0009).
- Ekranlar: e-posta doğrulama (bağlantıyla açılır), "Şifremi unuttum", yeni parola belirleme; ana ekranda doğrulama e-postasını yeniden gönderme.
- Parola değişince uygulama yerel oturumu da kapatır (sunucu tüm oturumları iptal eder).
- Testler (2026-10-09): 46 mobil birim ve widget testi geçiyor. Uçtan uca test emülatörde gerçek backend ve Mailpit ile geçti: kayıt, e-postadaki token ile doğrulama, parola sıfırlama, eski parolanın reddi, yeni parolayla giriş. Gerçek bağlantının kapalı uygulamayı açtığı ayrıca Android intent'i ile doğrulandı.
- Sonraya bırakılan: e-posta doğrulaması gerektiren yetki kuralı, onu kullanan ilk endpoint ile dilim 2'de gelir. O zaman doğrulamadan sonra access token'ın yenilenmesi de eklenir.

### Dilim 1 — Profil ve avatar

- Nesne depolama: kod S3 API'sine yazıldı, lokalde Garage (ADR 0010). `docker-compose.yml` içine eklendi; ayarları `.env` dosyasında.
- Backend: Profiles modülü (profil okuma/güncelleme, avatar yükleme adresi, avatar ayarlama/silme, başkasının profili), 81 il listesi. Kayıtta boş profil hesapla aynı transaction'da oluşur.
- Görünürlük kuralı: profili tamamlanmamış veya e-postası doğrulanmamış kullanıcı başkalarına görünmez (profili 404 döner). Etkileşim denemesindeki uyarı ve "en az bir sunulan yetenek" koşulu ilgili dilimlerde (2 ve 4) uygulanacak.
- Mobil: profil görüntüleme ve düzenleme, galeriden veya kameradan avatar, alt gezinme çubuğu (Keşfet, Eşleşmeler, Mesajlar "yakında" ekranı gösterir). Zorunlu profil ekranı yok: yeni kullanıcı doğrudan uygulamaya girer; Profil sekmesi etkileşim için eksik olanları (profil bilgileri, e-posta doğrulaması) gösterir.
- Modüller arası sınır testi eklendi: modüller birbirine yalnızca yayınlanmış sözleşme tipleri üzerinden başvurabilir.
- Testler (2026-10-09): backend 175 (60 domain, 81 uygulama, 34 entegrasyon; 4'ü gerçek Garage'a karşı), mobil 62. Uçtan uca test emülatörde zorunlu profil ekranlı ilk sürümle geçti (düzenleme, gerçek depolamaya avatar yükleme ve silme, doğrulama, parola sıfırlama); zorunlu ekran kaldırıldıktan sonra **yeniden çalıştırılmadı**.
- Android derlemesi için iki ayar: Kotlin artımlı derlemesi kapatıldı (pub önbelleği ile proje farklı sürücülerdeyken derleme hata veriyor) ve Gradle belleği 4 GB'a indirildi.
- Bilinen sınırlar: fotoğraf kırpma ekranı yok (ortadan kare gösterilir); avatar görselleri önbelleğe alınmıyor.

#### Depolama için ek lokal ayar

```powershell
dotnet user-secrets set --project src/Exparter.Api "Storage:AccessKey" "<.env içindeki STORAGE_ACCESS_KEY>"
dotnet user-secrets set --project src/Exparter.Api "Storage:SecretKey" "<.env içindeki STORAGE_SECRET_KEY>"
```

## Geliştirme ortamı

2026-10-06 tarihinde ölçülen sürümler:

| Araç | Sürüm |
|---|---|
| İşletim sistemi | Windows 11 Pro 10.0.26300 |
| .NET SDK | 10.0.301 (ayrıca 10.0.103 ve 9.0.312 kurulu) |
| Flutter | 3.47.6 (stable), Dart 3.13.5 |
| Android SDK | 36.1.0 |
| Docker Engine | 29.7.2 (Docker Desktop) |
| Docker Compose | v5.4.0 |
| Git | 2.55.0.windows.3 |
| PostgreSQL imajı | `postgres:18.6-alpine` |
| Mailpit imajı | `axllent/mailpit:v1.31.4` |
| Garage imajı | `dxflrs/garage:v2.4.1` |
| Node.js (lint için) | 24.14.0 |

Notlar:

- Bu makinede 5432 portunu yerel PostgreSQL 17 servisi (`postgresql-x64-17`) kullanıyor. Bu yüzden lokal `.env` dosyasında `POSTGRES_PORT=5433` ayarlı.
- `flutter doctor` tek uyarı veriyor: Visual Studio'nun "Desktop development with C++" bileşenleri eksik. Bu yalnızca Windows masaüstü derlemesini etkiler; Android ve web hedefleri sorunsuz.
