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
| 5 | Dikey dilimler | Tamamlandı (2026-10-14) |
| 6 | Sertleştirme ve MVP yayını | Sürüyor: dilim 1–5 tamamlandı |
| 7 | Web istemcisi | Başlamadı |
| 8 | MVP sonrası modüller | Sürüyor: dilim 1 (görüntülü görüşme) canlıda, cihaz denemesi bekliyor |

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
| 7 | Değerlendirmeler | Tamamlandı (2026-10-13) |
| 8 | Bildirimler | Tamamlandı (2026-10-14) |

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

### Dilim 7 — Değerlendirmeler

- Değerlendirme: 1–10 puan ve isteğe bağlı, en fazla 500 karakterlik yorum. Aralarında tamamlanmış en az bir oturum olan iki kullanıcı, eşleşme sürerken de birbirini bir kez değerlendirir. Anında herkese görünür, düzenlenemez; yazarı silebilir, silinenin yerine yenisi yazılamaz. Değerlendirilen kişi cevap yazamaz.
- Sözleşme değişikliği: `createReview` isteği oturum yerine değerlendirilen kullanıcıyı (`revieweeId`) alır; hakkı doğuran tamamlanmış oturumu sunucu bulur ve kayda yazar. Yeni `GET /api/v1/users/{userId}/review-eligibility` istemciye formu sunup sunmayacağını söyler.
- Puan: ortalama ve değerlendirme sayısı silinmemiş değerlendirmelerden her okumada hesaplanır (özet tablosu yok); profilde, kendi profilinde ve keşfet kartlarında görünür.
- Keşfet sıralaması: uyum ve tercih eşitse ortalaması yüksek olan önce gelir; hiç değerlendirmesi olmayanlar değerlendirilmişlerden sonra. Değerlendirme sayısı sıralamayı etkilemez.
- Mobil: profillerde "Değerlendirmeler" bölümü (ortalama, liste, kendi değerlendirmeni silme), değerlendirme yazma ekranı, eşleşme ayrıntısında ve karşı tarafın profilinde yalnızca değerlendirme hakkı varken görünen "Değerlendir" düğmesi.
- Testler (2026-10-13): backend 437 (152 domain, 196 uygulama, 89 entegrasyon), mobil 175. Uçtan uca test emülatörde geçti: tamamlanan buluşmadan sonra eşleşmeden değerlendirme yazma, karşı tarafın profilinde ortalamanın görünmesi ve kendi profilinde alınan değerlendirme.
- Sonraya bırakılan: değerlendirme bildirimi (dilim 8); değerlendirmeyi şikâyet etme (moderasyon); silinen hesabın yazdığı değerlendirmelerin "Silinmiş kullanıcı" adıyla kalması ve aldıklarının silinmesi (dilim 8, kalıcı hesap silme); profilde yalnızca ilk 50 değerlendirme listelenir.

### Dilim 8A — Uygulama içi bildirimler ve hesap silme

Dilim 8 iki parçaya bölündü (kullanıcı onayı, 2026-10-13): 8A Firebase gerektirmeyen her şey, 8B telefon bildirimleri (FCM).

- Bildirimler: bir kullanıcının işlemi karşı tarafı ilgilendiriyorsa bildirim üretilir (istek, sonlandırma isteği, eşleşmenin bitmesi, oturum adımları ve iptal sebebi, değerlendirme, yetenek önerisinin sonucu). Bildirim, onu doğuran işlemle aynı işlemde kaydedilir; açık bağlantılara `NotificationReceived` olayıyla anında gider. Metni istemci üretir. Kurallar: NT-01–NT-10.
- Hatırlatmalar: cevaplanmamış sonlandırma isteği ve onaylanmamış tamamlandı işareti için son 24 saatte bir kez; planlanmış çevrimiçi oturum için 24 saat ve 1 saat önce iki tarafa. Kendiliğinden sonuçlanan süreler (süresi dolan istek, cevaplanmayan öneri, kendiliğinden biten eşleşme ve tamamlanan oturum) de bildirilir.
- Sözleşme: kullanılmayan `MatchCreated` türü çıkarıldı (kabul bildirimi eşleşmenin kimliğini taşır), `SessionStartingSoon` eklendi. Cihaz kaydı uçları da yazıldı; gönderim 8B'de.
- Saklama: bildirimler 90 gün sonra silinir.
- Mobil: Keşfet ekranının üst köşesinde okunmamış sayısını gösteren zil, bildirimler ekranı (okundu işaretleme, tümünü okundu sayma, dokununca ilgili ekrana gitme). Gelen bildirim, açık ekrandaki istek, eşleşme, oturum ve değerlendirme listelerini de yeniler.
- Hesap silme isteği: bekleyen istekler iptal edilir, eşleşme `AccountDeleted` sebebiyle biter (gerçekleşmemiş oturumlar da iptal olur), sohbetler kapanır; karşı taraflara bildirim gider. Giriş yapıp silmeyi geri alan kullanıcıya kapananlar geri gelmez.
- Kalıcı silme: 30 gün dolunca arka plan işi profili, avatarı, yetenekleri, bildirimleri, cihazları, token'ları ve kullanıcının aldığı değerlendirmeleri siler. Hesap satırı kimliksiz olarak kalır; yazdığı mesaj ve değerlendirmeler "Silinmiş kullanıcı" adıyla görünür. E-postanın hash'i 3 ay tutulur ve bu sürede aynı adresle kayıt `email_recently_deleted` ile reddedilir; kullanıcı adı hemen serbest kalır.
- Mobil: profil ekranında parola isteyen "Hesabımı sil" akışı (önceden yoktu).
- Testler (2026-10-13): backend 476 (166 domain, 214 uygulama, 96 entegrasyon), mobil 190.
- Sonraya bırakılan: telefon bildirimleri ve yeni mesaj bildirimi (8B); bildirim izni akışı (8B); hesap silme isteğinde açık hub bağlantılarının kapatılması; silinen hesap hakkındaki açık şikâyetlerin saklanması (moderasyon); saklama sürelerinin hukuki doğrulaması (yayından önce).
- Kullanıcı onayı: 2026-10-14.

### Dilim 8B — Telefon bildirimleri (FCM)

- Firebase projesi: `exparter-5d834`, Android uygulaması `com.exparter.app`. Karar ve gerekçe: ADR 0012.
- Backend: açık uygulaması olmayan kullanıcıya her bildirim ve her yeni mesaj FCM ile gider. Metni sunucu üretir (kullanıcı kararı, 2026-10-14); mesajda gönderenin adı ve mesajın başı görünür. Gönderim arka plandaki kuyruktan yapılır, isteği bekletmez. FCM'in tanımadığı token'lar silinir; hesap silme isteği cihaz kayıtlarını siler. Kurallar: NT-11, NT-12.
- Mobil: ilk girişten sonra kısa bir açıklama ve ardından sistemin izin penceresi; profilde bildirim durumu ve "Bildirimleri aç". Cihaz kaydı girişte ve token yenilenince yapılır, çıkışta silinir. Dokunulan bildirim ilgili ekranı açar ve bildirimi okundu sayar. Uygulama arka plana geçince gerçek zamanlı bağlantıyı kapatır, öne gelince yeniden açıp listeleri yeniler.
- Paketler: FirebaseAdmin 3.7.0 (getirdiği güvenlik açıklı `System.Security.Cryptography.Pkcs` 10.0.13'e sabitlendi), firebase_core 4.15.0, firebase_messaging 16.7.0, Google services Gradle eklentisi 4.5.0. Ayrıca yeni yayımlanan bir SignalR güvenlik bildirimi yüzünden Microsoft paketleri 10.0.12'den 10.0.13'e yükseltildi.
- Testler (2026-10-14): backend 484 (166 domain, 221 uygulama, 97 entegrasyon), mobil 199. Emülatörde gerçek Firebase ile doğrulandı: açıklama ve izin penceresi, cihaz kaydı, uygulama arka plandayken mesaj ve oturum önerisi bildirimi, dokununca eşleşme ekranının açılması.
- Bilinen sınırlar: bildirim simgesi şimdilik uygulama simgesi (marka simgesiyle birlikte değişecek); Android birden çok bildirimi gruplar, grubun kendisine dokunmak yalnızca uygulamayı açar. Uçtan uca test Firebase'siz çalışır; push elle doğrulandı.

#### Firebase için lokal ayar

Firebase konsolundan indirilen iki dosya repoya girmez:

- `google-services.json` → `mobile/android/app/` (Android derlemesi için gerekir).
- Servis hesabı anahtarı (Proje ayarları → Hizmet hesapları → Yeni özel anahtar) → repo dışında bir klasör:

```powershell
dotnet user-secrets set --project src/Exparter.Api "Firebase:CredentialsPath" "<anahtar dosyasının tam yolu>"
```

Ayar yoksa backend push göndermez, geri kalan her şey çalışır.

## Faz 6 — Sertleştirme ve MVP yayını

Plan 2026-10-14'te onaylandı. Her dilim kendi branch'inde (`phase-6/<n>-<konu>`) yapılır ve onayla `main`'e merge edilir.

| Dilim | Konu | Durum |
|---|---|---|
| 1 | Yeni renk paleti, açık ve koyu tema, logo | Tamamlandı (2026-10-14) |
| 2 | Moderasyon: engelleme, şikâyet, admin uçları (şikâyetler, askıya alma, iptal sebepleri, yetenek önerilerinin listelenmesi ve kabul/reddi) | Tamamlandı (2026-10-14) |
| 3 | Güvenlik: istek sınırlama, girdi sınırları, güvenlik başlıkları, bağımlılık denetimi | Tamamlandı (2026-10-14) |
| 4 | Performans: N+1 sorgular, index'ler, keşfet sorgusu, yük denemesi | Tamamlandı (2026-10-14) |
| 5 | Hata izleme ve kayıtlar | Tamamlandı (2026-10-14) |
| 6 | Canlı ortam ve Google Play kapalı test sürümü | Sürüyor |

Kararlar (kullanıcı, 2026-10-14):

- Moderasyon brief'in Faz 5 listesinde yoktu ama MVP kapsamında (2026-10-06 kararı); yayından önce bitmesi için Faz 6'ya dilim olarak eklendi.
- Palet: "İndigo ve turkuaz". Açık temada ana renk `#4F46E5`, ikincil `#0EA5A4`, vurgu `#F59E0B`, zemin `#F8F9FC`, metin `#1E1B3A`; koyu temada ana renk `#A5B4FC`, ikincil `#5EEAD4`, zemin `#0F1020`, yüzey `#1A1B2E`, metin `#E6E6F0`.
- Tema varsayılan olarak telefonun ayarını izler; profilde Sistem, Açık veya Koyu seçilebilir.
- Logo yok; sade bir logo bu dilimde hazırlanır.
- Yetenek önerilerinin listelenmesi ve kabul/reddi moderasyon dilimine eklendi (şu an yalnızca PowerShell betiğiyle yapılıyor).

### Dilim 1 — Renk paleti, açık ve koyu tema, logo

- Palet `core/theme/` token'larında değişti; widget'larda sabit renk yoktu, ekranlara dokunulmadı. Kartlar gölge yerine ince kenarlıkla ayrılıyor (koyu temada gölge görünmüyor).
- Turkuazın üstünde beyaz yazı okunmadığı için ikincil rengin üstündeki yazı koyu. Giriş ekranı başlığının geçişi indigo → turkuaz; beyaz başlık her noktada okunur kalacak şekilde koyulaştırıldı.
- Kontrast testleri iki tema için de metin/zemin çiftlerini (en az 4.5:1) ve başlık geçişini (en az 3:1) denetler.
- Tema: varsayılan olarak telefonun ayarı; profilde "Görünüm" altında Sistem / Açık / Koyu. Seçim telefonda saklanır. Durum çubuğu simgeleri temaya göre açık veya koyu.
- Logo: iki ok bir kıvılcımın çevresinde birbirine dönüşüyor (yeteneklerin el değiştirmesi). Tek kaynak `core/theme/app_logo.dart`; Android simgeleri (eski tip, uyarlanabilir ve tek renkli, bildirim simgesi) aynı çizimden `flutter test tool/generate_icons.dart` ile üretilir. Giriş ekranında adın üstünde de görünür.
- Android açılış ekranı ve pencere zemini iki temada da paletle aynı.
- Testler (2026-10-14): mobil 208. Emülatörde iki temada Keşfet, Profil ve giriş ekranı, uygulama simgesi ve bildirim simgesi ekran görüntüsüyle kontrol edildi.
- Sonraya bırakılan: Play Store için 512 px simge ve mağaza görselleri (dilim 6).

### Dilim 2 — Moderasyon

Kararlar (kullanıcı, 2026-10-14): engellenen kişi engellendiğini görmez; askıya alınan hesap giriş yapamaz ve görünmez olur, paylaştıkları olduğu gibi kalır; şikâyet sebepleri sözleşmedeki beş sebep, şikâyet kapanınca şikâyet edene bildirim gider; admin işleri API ve betiklerle yapılır.

- Backend: `Moderation` modülü (`moderation` şeması, `blocks` ve `reports` tabloları). Engelleme bekleyen isteği iptal eder, eşleşmeyi bitirir, oturumları iptal eder ve sohbeti kapatır; bunlar engellemeyle aynı işlemde olur. Engel sürerken taraflar keşfette, aramada ve profilde birbirini görmez, istek ve değerlendirme gönderemez. Kurallar: MD-01–MD-11.
- Eşleşmeyi kimin engellediği eşleşmede saklanır (`ended_by_user_id`); `Blocked` sebebini yalnızca engelleyen görür.
- Şikâyet, şikâyet edilen metni o anki haliyle saklar; admin listesi hedefin sahibini ve bu metni gösterir. Silinen hesaplar hakkındaki şikâyetler silinmez.
- Admin: şikâyetleri listeleme ve kapatma, oturum iptal sebepleri, hesabı askıya alma ve askıyı kaldırma (sözleşmeye yeni uç eklendi). Yetenek önerisi uçları dilim 2'den beri vardı. Betikler: `scripts/admin-skill-proposals.ps1` ve yeni `scripts/admin-moderation.ps1` (Windows PowerShell'de Türkçe karakterler bozulmasın diye ikisi de BOM'lu UTF-8).
- Askıya alınan veya silme isteği veren kullanıcının açık canlı bağlantıları kapatılır ve yeniden bağlanamaz (8A'dan kalan iş).
- Mobil: başka bir kullanıcının profilinde ve sohbet menüsünde "Şikâyet et" ve "Engelle"; karşı tarafın mesajına basılı tutunca ve değerlendirme kartının menüsünden "Şikâyet et"; profilde "Engellenen kullanıcılar" ve engeli kaldırma; "Şikâyetiniz incelendi" bildirimi.
- Paket: `dotnet-ef` aracı 10.0.13'e yükseltildi (çalışma zamanıyla aynı sürüm).
- Testler (2026-10-14): backend 502 (169 domain, 232 uygulama, 101 entegrasyon), mobil 216.
- Bilinen sınırlar (dilim 2): askıya alınan kullanıcının elindeki access token süresi dolana kadar (en fazla 15 dakika) REST isteklerinde geçerli kalır; bağlantısı ve oturum yenilemesi ise hemen kapanır. Engellenen kullanıcının yetenek ve değerlendirme listesi uçları kimliğiyle çağrılırsa yanıt verir (profil 404 döndüğü için uygulamada görünmez).

### Dilim 3 — Güvenlik

- İstek sınırlama (ASP.NET Core'un yerleşik sınırlayıcısı, ek paket yok): giriş yapmış kullanıcı dakikada 300, giriş yapmamış adres dakikada 120 istek; giriş, token yenileme ve token onayları adres başına dakikada 30; e-posta gönderen uçlar adres başına 15 dakikada 10. Aşılınca `429 rate_limited` ve `Retry-After`. Sınırlar `RateLimits:*` ayarlarıyla değişir; ayrıntı `docs/api-conventions.md`.
- Girdi sınırları: istek gövdesi en fazla 64 KB (`413 payload_too_large`), hub mesajı en fazla 4 KB. Metin alanlarının hepsinin zaten doğrulama sınırı vardı.
- Güvenlik başlıkları: `no-store`, `nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, kısıtlayıcı CSP; geliştirme dışında HSTS. CORS kapalı (tarayıcı istemcisi yok).
- Bağımlılık denetimi: NuGet denetimi doğrudan ve dolaylı tüm paketleri kapsar ve uyarı hata sayılır; backend CI haftada bir de çalışır. Flutter paketleri için otomatik bir güvenlik denetimi aracı yok.
- Yerleşik güvenlik incelemesi bu dilimin değişikliklerinde yüksek veya orta düzeyde bulgu çıkarmadı.
- Testler (2026-10-14): backend 507 (169 domain, 232 uygulama, 106 entegrasyon), mobil 216.
- Sonraya bırakılan (dilim 6): ters vekil arkasında gerçek istemci adresinin alınması (forwarded headers), `AllowedHosts` kısıtı, HTTPS. Hesap başına giriş denemesi sınırı (şu an adres başına).

### Dilim 4 — Performans

- N+1 taraması: entegrasyon testlerine EF Core komut sayacı eklendi. Dokuz liste uç noktası (eşleşmeler, sohbetler, bildirimler, alınan değerlendirmeler, oturumlar, engellenenler, keşfet, istatistik, gelen istekler) 1 ve 4 kayıtla aynı sayıda sorgu çalıştırıyor; N+1 yok. Test kalıcı (`QueryCountTests`).
- Index'ler: sorguların hepsi index kullanıyor; tek eksik, kalıcı silme işinin her dakika aradığı silinmeyi bekleyen hesaplardı (kısmi index eklendi).
- Keşfet: 10 000 kullanıcıyla ölçüldü. Sorgu her çağrıda PostgreSQL'in JIT derlemesini tetikliyordu (yaklaşık 750 ms derleme, 150 ms iş). API'nin bağlantılarında JIT kapatıldı; kısa ve sık sorgular JIT'ten kazanç sağlamaz.
- Yük denemesi (2026-10-14, bu geliştirme makinesi, 10 eşzamanlı istemci, uç başına 300 istek; 10 000 kullanıcı, 40 000 kullanıcı yeteneği, 30 000 değerlendirme):

| Uç | p50 (ms) | p95 (ms) |
|---|---|---|
| Keşfet, ilk sayfa (önce / sonra) | 1354 / 270 | 1591 / 369 |
| Keşfet, ikinci sayfa | 313 | 369 |
| Keşfet, şehir ve yetenek süzgeciyle | 22 | 28 |
| Keşfet, ad araması | 65 | 76 |
| Profil, değerlendirmeler, yetenekler, istatistik | 16–19 | 20–25 |
| Yetenek araması, bildirimler | 11–12 | 15 |

- Mobil: avatarlar önbelleğe alınıyor (`cached_network_image` 4.0.4). İmzalı adres her yanıtta değiştiği için önbellek anahtarı adresin imzasız halidir; yeni yükleme yeni nesne anahtarı aldığından eski fotoğraf gösterilmez.
- Testler (2026-10-14): backend 509 (169 domain, 232 uygulama, 108 entegrasyon), mobil 217.
- Bilinen sınırlar: süzgeçsiz keşfet tüm adayları sıraladığı için kullanıcı sayısıyla doğrusal büyür (10 000 kullanıcıda tek başına yaklaşık 145 ms). Kullanıcı sayısı onlarca bine çıktığında sıralamanın önceden hesaplanması gerekir (ADR 0011). Ad araması index kullanmaz (`pg_trgm` aynı ADR'de not edildi). Avatar önbelleği telefonda bir fotoğrafla elle denenmedi; davranışı birim testiyle doğrulandı.

### Dilim 5 — Hata izleme ve kayıtlar

Karar (kullanıcı, 2026-10-14): Sentry, AB veri bölgesi. Gerekçe ve kurallar: ADR 0013.

- Backend: hata seviyesindeki loglar (yakalanmayan istisnalar, arka plan işlerinin, e-posta ve push gönderiminin hataları) Sentry olayı olur; öncesindeki loglar iz olarak eklenir. Kullanıcıdan yalnızca kimliği gider; IP, başlık ve gövde gitmez. Performans izleme kapalı.
- Kayıtlar: geliştirmede metin, diğer ortamlarda satır başına JSON. Hata yanıtındaki `traceId` kayıtlardaki iz kimliğiyle aynıdır.
- Mobil: yakalanmayan hatalar ve çökmeler Sentry'ye gider; kullanıcıdan yalnızca kimliği.
- DSN yokken Sentry kapalıdır (CI, testler, DSN'siz lokal çalıştırma).
- Paketler: Sentry.AspNetCore 6.12.1, Sentry.Serilog 6.12.1, Serilog.Formatting.Compact 3.0.0, sentry_flutter 9.30.1 (10.x Android 8 ve üstünü istiyor; uygulama Android 7.0 ve üstünü desteklediği için 9.x seçildi).
- Testler (2026-10-14): backend 510 (169 domain, 232 uygulama, 109 entegrasyon), mobil 218.
- Sentry: `exparter` kuruluşu (AB bölgesi, `de.sentry.io`), projeler `exparter-api` (ASP.NET Core) ve `exparter-mobile` (Flutter). Uçtan uca doğrulandı (2026-10-14): Mailpit durdurulunca e-posta gönderim hatası `EXPARTER-API-1` olarak, `adb shell am crash` ile çökertilen uygulama `EXPARTER-MOBILE-1` olarak geldi; ikisinde de kullanıcıdan yalnızca kimliği vardı. İki deneme kaydı çözüldü olarak işaretlendi.
- IP saklama kuruluş ayarlarında kapatıldı (kullanıcı, 2026-10-14).
- Kalan (dilim 6): Release derlemesinde Android eşleme dosyalarının (R8) Sentry'ye yüklenmesi; ortam ve sürüm etiketlerinin canlı ortamda ayarlanması.

#### Sentry için lokal ayar

```powershell
dotnet user-secrets set --project src/Exparter.Api "Sentry:Dsn" "<exparter-api projesinin DSN'i>"
flutter run --dart-define=SENTRY_DSN=<exparter-mobile projesinin DSN'i>
```

DSN'ler Sentry'de proje ayarlarında, "Client Keys (DSN)" altında. Mobil DSN uygulamaya gömülür ve Sentry'ye göre gizli değildir; backend DSN'i yine de user-secrets'ta tutulur.

IP saklamayı kapatmak için: Sentry → Settings → Organization (`exparter`) → Security & Privacy → "Prevent Storing of IP Addresses" açık.

### Dilim 6 — Canlı ortam ve kapalı test

Kararlar (kullanıcı, 2026-10-14): sunucu Oracle Cloud Always Free (ARM, Frankfurt), e-posta Brevo, alan adı alınacak, kişisel Play Console hesabı açılacak. Önerilen ve plana alınan: DNS ve nesne depolama Cloudflare (R2). Gerekçe: ADR 0014; ADR 0007, 0010 ve 0013 güncellendi.

- Backend imajı: `backend/Dockerfile`, arm64 için çapraz derleme. `--migrate` argümanıyla migration uygulanır ve süreç çıkar. Backend CI imajı her push'ta derler.
- Sunucu: `deploy/` (Compose ile API, PostgreSQL ve Caddy; Faz 8'den beri LiveKit de; `.env.example`; gece yedeği `backup.sh`; herkese açık sayfalar `site/`). Kurulum ve günlük işler: `docs/deployment.md`.
- Dağıtım: `Deploy` iş akışı `main`'den imajı GHCR'ye yayımlar. `DEPLOY_HOST` değişkeni tanımlanınca migration'ı ve yeni sürümü SSH ile sunucuda çalıştırır.
- Android: yayın derlemesi repo dışındaki yükleme anahtarıyla imzalanır (`mobile/android/key.properties`); dosya yoksa debug anahtarı kullanılır.
- Mağaza: metinler, form yanıtları ve kapalı test adımları `docs/store/listing.md`; simge ve öne çıkan görsel `docs/store/` (üretici: `mobile/tool/generate_store_assets.dart`).
- Yasal metinler `deploy/site/` altında yayında (`exparter.app`). Kalan hukuki işler `docs/store/listing.md` 7. bölümde.
- Bekleyen (kullanıcı): 12'den fazla test kullanıcısı.
- Canlı ortam kuruldu (2026-10-27): Oracle A1 sunucusu, `exparter.app` ve `api.exparter.app` (Caddy sertifikaları), 13 migration, R2 ve Brevo doğrulandı (doğrulama e-postası gelen kutusuna düştü), gece yedeği R2'de, otomatik dağıtım ilk denemede çalıştı.
- Hukuki uyum (karar, kullanıcı, 2026-10-27): hukukçu yerine araştırma raporu (`docs/legal/compliance-research.md`). Uygulananlar: kayıtta kullanım koşullarının kabulü (kabul zamanı ve sürümü saklanır, ID-09), aydınlatma metnine bağlantı (açık rıza yok), profilde yasal metin bağlantıları, kimseyi adlandırmayan telefon bildirimleri (NT-11), kapanan şikâyetlerin 2 yıl sonra silinmesi (MD-12), 1 yıl tutulan trafik kayıtları (5651, ADR 0014). Metinler son haliyle `deploy/site/` altında; sağlayıcılara standart sözleşme talepleri `docs/legal/scc-requests.md`.
- Paketler: url_launcher 6.3.3.
- Testler (2026-10-27): backend 514 (170 domain, 234 uygulama, 110 entegrasyon), mobil 221.
- Mağaza ekran görüntüleri (2026-10-27): 6 adet, `docs/store/screenshots/`. Kurgusal kişilerle yerel ortamda alındı; canlıya örnek hesap açılmadı. Veriler `scripts/seed-store-screenshots.py` ile yeniden üretilir.
- İlk yayın derlemesi (2026-10-27): 1.0.0 (1), canlı API ve Sentry anahtarıyla; imzanın yükleme anahtarıyla eşleştiği ve yayın APK'sının canlı API'ye bağlandığı (giriş, bildirim izni ve Firebase kaydı, yasal sayfalar) emülatörde denendi.
- E-posta bağlantıları (2026-10-27, ADR 0009): canlıda `https://exparter.app/verify-email` ve `/reset-password`. Uygulama kuruluysa Android App Links ile uygulama açılır, yoksa sitedeki sayfa işlemi tamamlar. Yerelde Caddy ile uçtan uca, emülatörde uygulamayla denendi. Play'in imzalama anahtarı yüklemeden sonra `assetlinks.json` dosyasına eklenecek.
- Site ve e-postalar (2026-10-27): `exparter.app` ana sayfası markaya uygun yeniden tasarlandı (tanıtım, nasıl çalışır, özellikler, ekranlar, güvenlik, kapalı teste katılım); yasal sayfalar ve e-posta bağlantısı sayfaları aynı başlık ve kart düzenini kullanıyor. Doğrulama ve parola sıfırlama e-postaları markalı HTML ve düz metin olarak gidiyor (ADR 0007); Mailpit'e gerçek gönderimle test edildi.
- Testler (2026-10-27): backend 516 (170 domain, 234 uygulama, 112 entegrasyon), mobil 221.
- Play Console (2026-10-28): uygulama `com.exparter.app` olarak oluşturuldu, formlar dolduruldu, 1.0.0 (1) kapalı test kanalında incelemeye gönderildi. İnceleme hesabı `exparter_inceleme`, `deneme_yusuf` ile aktif bir eşleşmesi var.
- Kapalı test (2026-11-02): 1.0.0 (1) incelemeden geçti, 10 test kullanıcısının cihazında kurulu. Play'in imzalama anahtarı `assetlinks.json` dosyasına eklendi; bağlantıların cihazlarda doğrulanması için 1.0.0 (2) derlendi ve incelemeye gönderildi.
- Sentry incelemesi (2026-11-02): uygulama dışında doğrulanan e-posta artık uygulama açılınca, öne gelince ve "zaten doğrulanmış" yanıtında fark ediliyor (1.0.0 (3)). JSON kabul etmeyen istekler boş 500 yerine doğru sorun yanıtını alıyor. İstemci hataları Sentry'ye gitmiyor. Backend 520, mobil 227 test.
- Test geri bildirimi (2026-11-03, 1.0.0 (4)): sohbet başlığında profil fotoğrafı; profilde devam eden eşleşme veya bekleyen istek varsa yeni istek yerine durum ve ilgili ekrana geçiş (`otherUserId` filtresi); kullanıcı adına boşluk girilemiyor; "Çevrimiçi takas" / "Yüz yüze takas" adları; profildeki çark simgesinden açılan Ayarlar ekranı (görünüm, bildirim, engellenenler, yasal, çıkış, hesap silme). Backend 521, mobil 234 test; uçtan uca test emülatörde geçti.
- Test geri bildirimi (2026-11-03, 1.0.0 (5)): kayıtta kullanıcı adındaki büyük harfler yazılırken küçük harfe dönüyor; keşfette sunulan ve istenen yetenekler ayrı çiplerde; bir kullanıcı aynı anda iki oturumda olamıyor. Kabul edilmiş oturumlar ve kişinin kendi açık teklifleri, aynı veya farklı eşleşmelerde, o zamanı tutuyor. Çakışan teklif veya kabul `session_time_taken` (kendi zamanı) ya da `other_user_busy` (karşı tarafın zamanı) ile reddediliyor. Eşzamanlı iki istek nadiren ikisi de geçebilir; veritabanı kısıtı eklenmedi. Eski uygulama sürümleri bu iki kod için genel hata mesajı gösterir. Backend 534 (178 domain, 238 uygulama, 118 entegrasyon), mobil 236 test.
- Kapalı testteki sürüm (2026-11-03): 1.0.0 (6), görüntülü görüşmeyle birlikte Play incelemesinde (Faz 8 dilim 1).
- Kalan: e-posta bağlantılarının test cihazında doğrulanması, test kullanıcılarının 12 kişiye tamamlanması (14 gün), Android eşleme dosyalarının Sentry'ye yüklenmesi ve sürüm etiketleri, standart sözleşme taleplerinin yanıtları, genel yayından önce yer sağlayıcılık bildirimi.

## Faz 8 — MVP sonrası modüller

Kapalı test sürerken MVP sonrasına bırakılan modüller geliştirilir (kullanıcı kararı, 2026-11-03). Her modül bir dilimdir, kendi branch'inde (`phase-8/<n>-<konu>`) yapılır ve onayla `main`'e merge edilir. `main`'e yapılan backend değişiklikleri otomatik olarak canlıya çıktığı için yalnızca eski uygulama sürümlerini bozmayan değişiklikler merge edilir; kapalı testten gelen düzeltmeler önceliklidir. Brief'te Faz 7 (web istemcisi) olarak duran iş bu fazdan sonra ele alınır.

| Dilim | Konu | Durum |
|---|---|---|
| 1 | Görüntülü görüşme | Canlıda (2026-11-03); iki cihazla deneme bekliyor |

### Dilim 1 — Görüntülü görüşme

Kararlar (kullanıcı, 2026-11-03; ADR 0015): altyapı kendi sunucumuzdaki LiveKit; görüşmeye planlanan başlangıçtan 10 dakika önce katılınabilir, süre bittikten 15 dakika sonra görüşme kapanır, 5 dakika kala uyarı gösterilir; katılım kaydı tutulmaz; uygulama arka plandayken ses sürer, kamera durur. Ekran paylaşımı da bu dilimde (kullanıcı kararı, 2026-11-03): iki taraf da ekranını paylaşabilir, paylaşırken kamera açık kalır, cihazın sesi paylaşılmaz.

Plan: sözleşme (`POST /sessions/{id}/call-token`, oturumda pencere alanları), backend (pencere kuralı, anahtar, süresi dolan odaların kapatılması), yerel ve canlı LiveKit servisi, mobil görüşme ekranı, gizlilik metni ve Play beyanları, iki cihaz arasında uçtan uca deneme.

- Sözleşme: `POST /api/v1/sessions/{sessionId}/call-token`; oturumda `callOpensAt` ve `callClosesAt`; yeni hata kodları `call_not_open_yet` ve `call_window_closed`.
- Backend: pencere kuralı `SwapSession` içinde; anahtar LiveKit'e kamera, mikrofon ve ekran yayını izni verir, veri kanalı ve cihaz sesi yoktur; pencere kapanan, iptal edilen veya eşleşmesi biten oturumların odası arka plan işiyle kapatılır.
- Mobil: oturum kartında "Görüşmeye katıl" (pencere açılınca), görüşme ekranı (mikrofon, kamera, kamera çevirme, ekran paylaşımı, bitirme; 5 dakika kala uyarı), arka planda "Görüşme sürüyor" bildirimiyle süren ses. Ekran paylaşımı sistemce durdurulunca (durum çubuğu, ekran kilidi) `MainActivity.kt` bunu yakalar.
- Paketler: LiveKit sunucusu v1.13.11, livekit_client 2.13.2.
- Testler (2026-11-03, `main` ile birleştikten sonra): backend 552 (188 domain, 241 uygulama, 123 entegrasyon), mobil 252. Emülatörde canlı olmayan ortamda uçtan uca denendi: iki taraflı görüntü, arka planda ses, ekran paylaşımı ve sistemce durdurulması, iptalde odanın kapanması.
- Bilinen sınırlar: karşıdan gelen paylaşılan ekranın gösterimi cihazda denenmedi (ikinci emülatör belleğe sığmadı). Bağlantı tamamen yeniden kurulursa SDK ekran paylaşımını yeniden yayımlayamıyor; paylaşım kapalı görünür, kullanıcı yeniden başlatabilir (emülatör ağır yük altındayken görüldü). Yalnızca 443/TCP'ye izin veren ağlarda görüşme kurulamaz (ADR 0015).

#### Görüşme için ek lokal ayar

`.env` dosyasına `LIVEKIT_API_SECRET` değerini yazın (`openssl rand -hex 32`); `docker compose up -d --wait` LiveKit'i de başlatır. API'ye aynı anahtarı verin:

```powershell
dotnet user-secrets set --project src/Exparter.Api "LiveKit:ApiKey" "<.env içindeki LIVEKIT_API_KEY>"
dotnet user-secrets set --project src/Exparter.Api "LiveKit:ApiSecret" "<.env içindeki LIVEKIT_API_SECRET>"
```

Uygulama emülatörde `ws://10.0.2.2:7880` adresine bağlanır. Medya UDP 7882'den (engelliyse TCP 7881'den) gider; `LIVEKIT_NODE_IP` sunucunun istemcilere bildirdiği adrestir.

#### Canlıya alma (kullanıcı, 2026-11-03; tamamlandı)

Sunucuya SSH yurt ağından kapalı olduğu için sunucudaki işleri dağıtım üstlenir: `deploy/prepare-server.sh` her dağıtımda yeni sürüm başlamadan çalışır; `.env`'de eksikse `RTC_HOST`, `LIVEKIT_API_KEY`, sunucuda üretilen `LIVEKIT_API_SECRET` ve sunucunun genel IP'si olarak `LIVEKIT_NODE_IP` ekler, `iptables`'ta TCP 7881 ve UDP 7882'yi açar. Hazır sunucuda hiçbir şey değiştirmez. Gizli anahtar sunucudan çıkmaz; yerel `exparter-prod.env` yedeğinde bu değerler yoktur.

1. Cloudflare DNS (kullanıcı): `rtc` için sunucu IP'sine A kaydı, proxy kapalı (gri bulut); Caddy sertifikayı kendisi alır. Merge'den önce.
2. Oracle Security List (kullanıcı): TCP 7881 ve UDP 7882 giriş kuralları, kaynak `0.0.0.0/0`. Merge'den önce.
3. `main`'e merge (2026-11-03): dağıtım sunucuyu hazırladı; `rtc.exparter.app` sertifikayla yanıt veriyor, TCP 7881 dışarıdan açık.
4. Play Console: Data safety'de video (kamera görüntüsü ve paylaşılan ekran) ve ses kaydı, ön plan servisi beyanında "Arka planda ses girişi" (`microphone`) ve "Medya ve içerik projeksiyonu, yayın" (`mediaProjection`); beyan videosu emülatörde çekildi (`marketing/08-play-beyanlari/`, repo dışında). Ayrıntı `docs/store/listing.md`.
5. 1.0.0 (6) derlendi ve kapalı teste yüklendi (2026-11-03).

Kalan: iki gerçek cihazla görüşme ve iki yönde ekran paylaşımı denemesi; ardından dilim kapanır.

## Geliştirme ortamı

2026-10-06 tarihinde ölçülen sürümler (2026-11-03'te yeniden kontrol edildi, değişmedi):

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
| LiveKit imajı (2026-11-03) | `livekit/livekit-server:v1.13.11` |
| Node.js (lint için) | 24.14.0 |

Notlar:

- Bu makinede 5432 portunu yerel PostgreSQL 17 servisi (`postgresql-x64-17`) kullanıyor. Bu yüzden lokal `.env` dosyasında `POSTGRES_PORT=5433` ayarlı.
- `flutter doctor` tek uyarı veriyor: Visual Studio'nun "Desktop development with C++" bileşenleri eksik. Bu yalnızca Windows masaüstü derlemesini etkiler; Android ve web hedefleri sorunsuz.
