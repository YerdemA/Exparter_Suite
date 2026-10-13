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
| 5 | Dikey dilimler | Sürüyor: dilim 0–6 tamamlandı |
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
| 1 | Profil ve avatar | Tamamlandı (2026-10-13) |
| 2 | Yetenek kataloğu ve kullanıcı yetenekleri | Tamamlandı (2026-10-13) |
| 3 | Keşfet | Tamamlandı (2026-10-13) |
| 4 | Takas isteği ve eşleşme | Tamamlandı (2026-10-13) |
| 5 | Gerçek zamanlı mesajlaşma | Tamamlandı (2026-10-13) |
| 6 | Takas oturumları | Tamamlandı (2026-10-13) |
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
- Testler (2026-10-09): backend 175 (60 domain, 81 uygulama, 34 entegrasyon; 4'ü gerçek Garage'a karşı), mobil 62. Uçtan uca test emülatörde geçti: kayıttan sonra doğrudan uygulamaya giriş, Profil sekmesinde eksikler kartı, profili doldurma ve düzenleme, gerçek depolamaya avatar yükleme ve silme, e-posta doğrulama, parola sıfırlama.
- Android derlemesi için iki ayar: Kotlin artımlı derlemesi kapatıldı (pub önbelleği ile proje farklı sürücülerdeyken derleme hata veriyor) ve Gradle belleği 4 GB'a indirildi.
- Bilinen sınırlar: fotoğraf kırpma ekranı yok (ortadan kare gösterilir); avatar görselleri önbelleğe alınmıyor.

#### Depolama için ek lokal ayar

```powershell
dotnet user-secrets set --project src/Exparter.Api "Storage:AccessKey" "<.env içindeki STORAGE_ACCESS_KEY>"
dotnet user-secrets set --project src/Exparter.Api "Storage:SecretKey" "<.env içindeki STORAGE_SECRET_KEY>"
```

### Dilim 2 — Yetenek kataloğu ve kullanıcı yetenekleri

- Katalog: 10 kategori ve 68 yetenek, migration ile gelen başlangıç verisi. Arama büyük/küçük harf ve Türkçe karakter ayrımı yapmaz ("ingilizce" → "İngilizce").
- Kullanıcı yetenekleri: en fazla 5 sunulan ve 5 istenen; aynı yetenek iki tipte de olabilir, istenen seviye sunulandan yüksekse.
- Yetenek önerisi: doğrulanmış e-posta yeterli; admin onaylayana kadar katalogda görünmez ve kullanılamaz.
- Etkileşim koşulları: doğrulanmış e-posta, tamamlanmış profil ve en az bir sunulan yetenek tek bir yerde denetlenir (`IInteractionRequirements`) ve `GET /me/interaction-requirements` ile istemciye verilir. Koşulları sağlamayan kullanıcının profili ve yetenekleri başkalarına görünmez.
- Mobil: Profil sekmesinde sunulan ve istenen yetenekler; katalogdan arama ve kategoriyle seçme, seviye belirleme, seviye değiştirme, kaldırma; yetenek önerme ve önerilerin durumu; eksikler kartında "En az bir sunulan yetenek" satırı; etkileşim reddedildiğinde uyarı penceresi.
- Admin: `scripts/admin-skill-proposals.ps1` bekleyen önerileri listeler ve onaylar/reddeder. Hesabı admin yapmak için betiğin başındaki komut kullanılır.
- Testler (2026-10-13): backend 246 (91 domain, 107 uygulama, 48 entegrasyon), mobil 80. Uçtan uca test emülatörde geçti: katalogdan arayıp sunulan ve istenen yetenek ekleme, seviye seçme, eksikler kartının güncellenmesi, yeniden açılışta yeteneklerin korunması; ardından önceki dilimlerin akışı.
- Sonraya bırakılan: eşleşmede veya bekleyen istekte kullanılan sunulan yeteneğin kaldırılamaması (SK-06), eşleşmelerin geldiği dilim 4'te.

### Dilim 3 — Keşfet

- `GET /api/v1/discovery/users`: karşılıklı uyum, tek yönlü uyum, tercih uyumu ve yenilik sırasıyla kullanıcı listesi; cursor ile sayfalanır. Sunulan seviye istenen en düşük seviyenin altındaysa uyum sayılmaz. Uyumu olmayan kullanıcılar listenin sonunda gösterilir.
- Filtreler: kategori, tek bir sunulan yetenek (katalogdan aranarak seçilir), şehir, çevrimiçi / yüz yüze. Ad veya kullanıcı adıyla arama büyük/küçük harf ve Türkçe karakter ayrımı yapmaz.
- Görünürlük değişti: etkileşim koşulunu sağlamayan kullanıcı öneri olarak listelenmez, ama aranınca bulunur ve profili ile yetenekleri açılabilir. Askıya alınmış ve silinmekte olan hesaplar hiçbir yerde görünmez.
- Keşfet tek bir SQL sorgusudur ve üç modülün tablosunu birlikte okur; modül sınırı kuralının bilinçli tek istisnası (ADR 0011).
- Mobil: Keşfet sekmesi (arama, şehir / mod / kategori filtreleri, sonsuz kaydırma, aşağı çekerek yenileme) ve başka bir kullanıcının salt okunur profil ekranı.
- Testler (2026-10-13): backend 270 (91 domain, 123 uygulama, 56 entegrasyon), mobil 94. Uçtan uca test emülatörde geçti: koşulu sağlamayan bir kullanıcı önerilerde çıkmıyor, adıyla aranınca bulunuyor ve profili açılıyor.
- Sonraya bırakılan: "İstek gönder" düğmesi dilim 4'te; puana göre sıralama dilim 7'de; engellenen kullanıcıların gizlenmesi moderasyonla birlikte.

### Dilim 4 — Takas isteği ve eşleşme

- İstek: gönderen kendi sunduğu bir yeteneği ve alıcının sunduğu bir yeteneği seçer, isteğe bağlı not (en fazla 300 karakter) ekler. Gönderen etkileşim koşullarını sağlamalıdır; alıcı kabul ederken sağlamalıdır.
- Kurallar: bir çift arasında tek bekleyen istek ve tek sona ermemiş eşleşme (veritabanı dizinleriyle de korunur); istek 3 günde sona erer; reddedilen veya cevaplanmayan gönderen aynı kişiye 7 gün yeniden istek gönderemez; geri çekilen istek bekleme başlatmaz.
- Eşleşme: kabul edilen istek eşleşme oluşturur. Bir taraf sonlandırmayı ister, diğeri onaylar veya reddeder; isteyen geri alabilir; 7 gün cevapsız kalan istek eşleşmeyi sonlandırır.
- Süreler: okumalar ve işlemler süreyi saate göre değerlendirir; dakikada bir çalışan arka plan işi (`MatchingDeadlineWorker`) kayıtlı durumu günceller.
- Bekleyen istekte veya sona ermemiş eşleşmede kullanılan sunulan yetenek kaldırılamaz (SK-06).
- İstatistik için veri: her istek ve eşleşmenin nasıl, ne zaman ve kimin isteğiyle bittiği saklanır. Sayılar profile dilim 6'da eklenecek.
- Mobil: başka kullanıcının profilinde "İstek gönder"; istek formu; Eşleşmeler sekmesinde eşleşmeler, gelen ve gönderilen istekler (kabul, ret, geri çekme); eşleşme ayrıntısı ve sonlandırma akışı.
- Testler (2026-10-13): backend 331 (110 domain, 154 uygulama, 67 entegrasyon), mobil 118. Uçtan uca test emülatörde geçti: istek gönderme, karşı tarafın kabulü, eşleşmenin görünmesi ve sonlandırma isteği.
- Sonraya bırakılan: eşleşme kabulünde sohbetin açılması ve `conversationId` (dilim 5); yeni istek ve sonlandırma hatırlatması bildirimleri (dilim 8); engelleme ile eşleşmenin sonlanması (moderasyon); listelerde yalnızca ilk 50 kayıt gösterilir.

### Dilim 5 — Mesajlaşma

- Sohbet: bir çiftin ilk eşleşmesiyle açılır, çift başına tektir; yeniden eşleşince aynı sohbet geçmişiyle sürer. Eşleşme artık sohbetin kimliğini taşır. Daha önce oluşmuş eşleşmelerin sohbetleri migration ile açıldı.
- Mesaj: yalnızca metin, en fazla 2000 karakter; düzenlenemez ve silinemez. Aynı `clientMessageId` ile yinelenen gönderim tek mesaj olarak saklanır.
- Okundu bilgisi: her taraf için son okuma anı saklanır; okunmamış sayısı ve "Görüldü" bundan türetilir.
- Kapatma: sona ermemiş eşleşme yokken taraflardan biri sohbeti kapatabilir; kapalı sohbet salt okunurdur.
- Gerçek zamanlı: `/hubs/realtime` SignalR hub'ı; `MessageReceived`, `ConversationRead`, `ConversationUpdated` olayları (docs/realtime.md). İstemci yalnızca dinler, yazma REST'ten yapılır.
- Mobil: Mesajlar sekmesi (son mesaj, okunmamış sayısı), sohbet ekranı (iyimser gönderim, başarısız mesajı yeniden deneme, eskiye doğru sayfalama, "Görüldü", sohbeti kapatma), alt çubukta okunmamış rozeti, eşleşme ayrıntısında "Mesaj gönder". Bağlantı koparsa yeniden kurulur ve ekrandaki veriler yenilenir.
- Testler (2026-10-13): backend 366 (119 domain, 171 uygulama, 76 entegrasyon; hub gerçek SignalR istemcisiyle sınanır), mobil 143. Uçtan uca test emülatörde geçti: eşleşmeden sohbete geçiş, mesaj gönderme ve karşı tarafın cevabının gerçek bağlantı üzerinden anında gelmesi.
- Sonraya bırakılan: uygulama kapalıyken telefon bildirimi (dilim 8, FCM); engelleme ve hesap silmenin sohbeti kapatması (moderasyon ve dilim 8); sohbet listesinde yalnızca ilk 50 sohbet.

### Dilim 6 — Oturumlar ve profil istatistikleri

- Çevrimiçi oturum: taraflardan biri tarih, saat, süre (30/45/60/90/120 dk) ve isteğe bağlı notla önerir; diğeri kabul veya reddeder. Başlangıç saatine kadar cevaplanmayan öneri reddedilmiş sayılır. Başlangıçtan sonra bir taraf tamamlandı işaretler, diğeri onaylar; 72 saat cevapsız kalırsa kendiliğinden tamamlanır.
- Yüz yüze buluşma: uygulamada planlanmaz; kullanıcılar sohbette ayarlar, tarih ve yer tutulmaz. Buluşmadan sonra taraflardan biri "Yüz yüze buluştuk" diye bildirir, diğeri onaylar veya sebep yazarak itiraz eder (kullanıcı kararı, 2026-10-13). Sahteciliğe karşı bir kullanıcı çifti, bütün eşleşmeleri boyunca yalnızca bir yüz yüze buluşma bildirebilir; iptal edilen (itiraz edilen) bildirim bu hakkı tüketmez.
- İptal: taraflardan biri sebep yazarak tek başına iptal eder; sebebi yalnızca iki taraf görür.
- Eşleşmeyle ilişki: sonlandırma isteği bekleyen veya sona ermiş eşleşmeye oturum eklenemez; eşleşme bitince henüz gerçekleşmemiş oturumlar iptal edilir, onay bekleyen işaret sonradan da onaylanabilir.
- İstatistikler (`GET /api/v1/users/{userId}/stats`): toplam eşleşme, tamamlanan eşleşme, iptal edilen (tamamlanmamış) eşleşme ve tamamlanan oturum; kayıtlardan hesaplanır, ayrı sayaç tutulmaz. Devam eden eşleşme hiçbir sayaca girmez: toplam = tamamlanan + iptal edilen.
- Mobil: eşleşme ayrıntısında oturum listesi, "Çevrimiçi oturum öner" formu, "Yüz yüze buluştuk" bildirimi ve oturum kartlarındaki işlemler; kendi profilinde ve başka kullanıcıların profilinde istatistik kartı.
- Testler (2026-10-13): backend 413 (140 domain, 188 uygulama, 85 entegrasyon), mobil 162. Uçtan uca test emülatörde geçti: yüz yüze buluşma bildirimi, karşı tarafın onayı, çevrimiçi oturum önerisi ve profildeki istatistik.
- Sonraya bırakılan: uygulamaya gömülü görüntülü görüşme modülü (çevrimiçi oturumlar yalnızca planlanan saatte bu modülle yapılacak; zamanlaması kararlaştırılacak, o zamana kadar taraflar bağlantıyı nota yazar); iptal sebebinin ve hatırlatmaların bildirim olarak gitmesi (dilim 8); iptal sebeplerinin admin tarafından görülmesi (moderasyon); bütün eşleşmelerdeki oturumları gösteren takvim ekranı (API hazır: `GET /api/v1/sessions`).

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
