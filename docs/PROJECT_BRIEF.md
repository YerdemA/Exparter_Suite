# Exparter Suite — Geliştirme Brief'i (AI Kodlama Ajanı İçin)

> Bu dosya projenin tek referans belgesidir. Her oturumun başında bu dosyayı oku ve buradaki kurallara uy.
> Bu belgeyle çelişen bir talimat alırsan, uygulamadan önce çelişkiyi bana bildir.

---

## 1. Rolün

Kıdemli bir yazılım mimarı ve full-stack geliştirici gibi çalışacaksın. Uzmanlık alanların .NET backend, Flutter mobil ve sistem tasarımı.

Çalışma tarzın şu kurallara dayanır:

- **Önce plan, sonra kod.** Her fazın başında ne yapacağını kısaca yaz ve onayımı bekle.
- **Varsayım yapma, sor.** Bir iş kuralı belirsizse kodlamadan önce sor. Belgede "AÇIK SORU" olarak işaretlenen konularda kendi başına karar verme.
- **Aşırı mühendislikten kaçın.** Bir deseni yalnızca somut bir ihtiyacı çözüyorsa kullan. Gereksiz soyutlama, gereksiz katman ve "ileride lazım olur" kodu yazma.
- **Küçük, doğrulanabilir adımlarla ilerle.** Her adımın sonunda proje derlenmeli ve testler geçmeli.
- **Kararları belgele.** Önemli her teknik kararı `docs/adr/` altına bir ADR olarak kaydet.

---

## 2. Proje Tanımı

**Exparter**, kullanıcıların yeteneklerini karşılıklı takas ettiği bir platformdur. Örneğin gitar bilen biri, İngilizce öğretebilecek biriyle eşleşir ve iki taraf birbirine bildiğini öğretir.

**Temel akış:**

1. Kullanıcı kayıt olur ve profilini oluşturur.
2. **Sunabildiği** yetenekleri (seviyesiyle birlikte) ve **öğrenmek istediği** yetenekleri ekler.
3. Keşfet ekranında uygun kişileri bulur. Sistem karşılıklı uyumu öne çıkarır: A'nın sunduğu şey B'nin istediği, B'nin sunduğu şey A'nın istediği olmalı.
4. Takas isteği gönderir. Karşı taraf kabul ederse eşleşme oluşur.
5. Eşleşen kullanıcılar uygulama içinde mesajlaşır ve takas oturumları planlar.
6. Takas tamamlanınca iki taraf birbirini değerlendirir.

**Dil ve görsel stil:**

- Arayüz dili **Türkçe**dir. Kod, değişken adları, commit mesajları ve kod yorumları **İngilizce** olacak.
- Görsel stil enerjik ve renkli olacak. Palet pembe, mercan, amber, turkuaz ve mor tonlarından oluşur.
- Tüm renk, tipografi ve boşluk değerleri merkezi bir tema/design token yapısından gelmeli. Bileşenlere sabit renk kodu yazma.

**Geliştirme ortamı:** Windows 11 Pro. Terminal komutlarını PowerShell uyumlu ver. Altyapı servisleri Docker Desktop üzerinde çalışacak.

---

## 3. Temel Mimari Kararlar (Değiştirilemez — Değiştirmek İçin ADR ve Onay Gerekir)

### 3.1 API-first
- Backend istemciden bağımsız bir üründür. İlk istemcisi Flutter mobil, ikinci istemcisi ileride web olacak.
- **`contracts/openapi.yaml` tek doğruluk kaynağıdır.** Endpoint önce sözleşmede tanımlanır, sonra uygulanır.
- İstemci API kodları sözleşmeden **otomatik üretilir**. API çağrılarını elle yazma.
- Hiçbir endpoint belirli bir ekrana göre şekillendirilmez. Kaynak odaklı, genel amaçlı tasarla.

### 3.2 Modüler monolit
- Tek deploy edilen backend uygulaması olacak, ama içinde sınırları net modüller bulunacak: `Identity`, `Profiles`, `Skills`, `Matching`, `Messaging`, `Sessions`, `Reviews`, `Notifications`, `Moderation`.
- Bir modül başka bir modülün veritabanı tablolarına doğrudan erişmez. Modüller birbirleriyle uygulama servisleri veya domain event'leri üzerinden konuşur.
- Mikroservis **yok**. Event sourcing **yok**.

### 3.3 Gerçek zamanlılık baştan tasarlanır
- Mesajlar, eşleşme bildirimleri ve durum değişiklikleri SignalR hub'ları üzerinden iletilir.
- Uygulama arka plandayken bildirimler FCM ile gönderilir.
- Web istemcisi geldiğinde aynı hub'ları kullanacak. Bu yüzden hub'lar istemci tipinden bağımsız olmalı.

### 3.4 Dikey dilimlerle geliştirme
- "Önce bütün backend, sonra bütün mobil" şeklinde ilerleme.
- Her özelliği sırasıyla sözleşme, backend, test ve mobil ekran olarak uçtan uca bitir, sonra sıradakine geç.

---

## 4. Teknoloji Yığını

Kurulumdan önce her paketin **güncel kararlı sürümünü** kontrol et. Sürüm numaralarını ezberden yazma.

### Backend
| Alan | Seçim |
|---|---|
| Framework | ASP.NET Core Web API, .NET 10 |
| Mimari | Clean Architecture (Domain / Application / Infrastructure / Api) + modüler yapı |
| Veritabanı | PostgreSQL + EF Core (Npgsql), migration tabanlı şema yönetimi |
| CQRS / mediator | `Mediator` (martinothamar, source-generated). **MediatR kullanma**, çünkü ticari lisansa geçti. |
| Doğrulama | FluentValidation |
| Hata formatı | RFC 9457 ProblemDetails, tüm endpoint'lerde tutarlı |
| Kimlik doğrulama | JWT access token (kısa ömürlü) + rotasyonlu refresh token (veritabanında hash'lenmiş olarak saklanır) |
| Gerçek zamanlı | SignalR |
| Push bildirim | Firebase Cloud Messaging |
| E-posta | Lokalde yakalayıcı bir SMTP sunucusu; gerçek sağlayıcı Faz 3'te ADR ile seçilecek |
| Dosya depolama | S3 uyumlu object storage, istemciye presigned URL ile. Lokalde Garage; canlı ortam Faz 6'da seçilecek (ADR 0010). |
| Loglama | Serilog, yapılandırılmış log |
| Test | xUnit, Testcontainers (gerçek PostgreSQL ile entegrasyon testi) |
| API dokümanı | OpenAPI (sözleşmeyle uyumu CI'da kontrol edilir) |

### Mobil
| Alan | Seçim |
|---|---|
| Framework | Flutter (güncel stable) |
| Klasörleme | Feature-first. Her feature kendi içinde `data/`, `domain/`, `presentation/` katmanlarını taşır. |
| State yönetimi | Riverpod (code generation ile) |
| Yönlendirme | go_router |
| HTTP | dio + OpenAPI'den üretilmiş client |
| Modeller | freezed + json_serializable |
| Güvenli depolama | flutter_secure_storage (token'lar için) |
| Gerçek zamanlı | SignalR client paketi |
| Test | flutter_test, mocktail |

### Web (Faz 7'de, şimdi kurulmayacak)
- Eğilim Next.js yönünde, ama karar Faz 7 başında ADR ile verilecek.
- Şu anki tek görevin, backend'in web istemcisini engelleyecek hiçbir varsayım içermemesini sağlamak (ör. yalnızca mobile özel auth akışı olmamalı).

---

## 5. Repo Yapısı

Proje klasörü `Exparter_Suite` olarak mevcut. Yapı şöyle olacak:

```
Exparter_Suite/
├── AGENTS.md / CLAUDE.md      # Bu brief'in kısa özeti + çalışma kuralları
├── docs/
│   ├── PROJECT_BRIEF.md       # Bu belge
│   ├── adr/                   # 0001-modular-monolith.md, 0002-postgresql.md ...
│   ├── domain/                # Domain modeli, iş kuralları, sözlük
│   ├── erd/                   # ERD (Mermaid)
│   └── roadmap.md             # Faz durumu
├── contracts/
│   └── openapi.yaml
├── backend/
│   ├── Exparter.sln
│   ├── src/
│   │   ├── Exparter.Api/
│   │   ├── Exparter.Application/
│   │   ├── Exparter.Domain/
│   │   └── Exparter.Infrastructure/
│   └── tests/
│       ├── Exparter.Domain.Tests/
│       ├── Exparter.Application.Tests/
│       └── Exparter.Api.IntegrationTests/
├── mobile/                    # Flutter projesi
├── web/                       # Faz 7'ye kadar boş
├── docker-compose.yml         # postgres, mailpit, garage
├── .editorconfig
├── .gitignore
└── .github/workflows/         # backend-ci.yml, mobile-ci.yml
```

---

## 6. Domain Taslağı (Faz 1'de Netleştirilecek)

Aşağıdakiler başlangıç taslağıdır, kesinleşmiş tasarım değildir. Faz 1'de bunları gözden geçir, eksik ve çelişkili yerleri bana raporla.

**Varlıklar:**

| Varlık | Açıklama |
|---|---|
| `User` | Kimlik bilgileri (e-posta, kullanıcı adı, parola hash'i, durum) |
| `RefreshToken` | Kullanıcının refresh token'ları |
| `Device` | Push bildirim için FCM token'ları |
| `Profile` | Ad, biyografi, avatar, şehir/çevrimiçi tercihi |
| `SkillCategory` | Yetenek kategorisi |
| `Skill` | Katalogdaki yetenek, bir kategoriye bağlı |
| `UserSkill` | Kullanıcı–yetenek ilişkisi. Tip: `Offered` / `Wanted`. Seviye: `Beginner` / `Intermediate` / `Advanced` / `Expert`. |
| `SwapRequest` | Takas isteği. Durum: `Pending` / `Accepted` / `Declined` / `Cancelled` / `Expired`. |
| `Match` | Kabul edilmiş takas ilişkisi |
| `Conversation`, `Message` | Eşleşmeye bağlı mesajlaşma |
| `SwapSession` | Planlanan takas oturumu (tarih, süre, durum) |
| `Review` | Takas sonrası karşılıklı değerlendirme |
| `Notification` | Kullanıcı bildirimleri |
| `Report` | Kullanıcı/içerik şikâyeti (moderasyon) |

**Bilinen iş kuralları:**

- Bir kullanıcı kendine takas isteği gönderemez.
- Aynı iki kullanıcı arasında aynı anda yalnızca bir aktif (`Pending`) istek olabilir.
- Mesajlaşma yalnızca eşleşme ile başlar. Eşleşme sona erdikten sonra sohbetin sürmesi kullanıcılara bırakılır.
- Değerlendirme yalnızca tamamlanmış bir takas oturumundan sonra yapılabilir. Bir kullanıcı başka bir kullanıcıyı yalnızca bir kez değerlendirebilir.
- Durum geçişleri domain katmanında korunur. Geçersiz geçiş, domain hatası fırlatır.

**AÇIK SORULAR (karar vermeden bana sor):**

> Bu beş soru 2026-10-06'da karara bağlandı. Kararlar ve hâlâ açık olan sorular `docs/domain/open-questions.md` dosyasındadır.

1. Zaman/kredi bankası sistemi olacak mı, yoksa takas tamamen karşılıklı mı?
2. Takaslar yalnızca çevrimiçi mi, yüz yüze seçeneği de olacak mı? Konum verisi tutulacak mı?
3. Eşleşme önerisi algoritması: MVP'de basit kural tabanlı skor yeterli mi?
4. Moderasyon ve admin işlemleri MVP'de mi, sonrasında mı?
5. Kayıt yöntemleri: yalnızca e-posta mı, Google/Apple girişi de olacak mı?

---

## 7. MVP Kapsamı

**Kapsam içi:**
- Kayıt (18 yaş onayıyla, benzersiz kullanıcı adıyla), e-posta doğrulama, e-posta veya kullanıcı adıyla giriş, çıkış, token yenileme, parola sıfırlama, hesap silme
- Profil oluşturma ve düzenleme, avatar yükleme
- Sunulan/istenen yetenek yönetimi (katalogdan seçim)
- Keşfet: filtreleme, arama, karşılıklı uyuma göre sıralama
- Takas isteği gönderme, kabul etme, reddetme
- Eşleşmeler listesi
- Gerçek zamanlı mesajlaşma
- Takas oturumu planlama ve tamamlandı olarak işaretleme
- Değerlendirme
- Uygulama içi ve push bildirimler

**Kapsam dışı (MVP sonrası):**
- Web istemcisi
- Admin paneli
- Görüntülü görüşme
- Ödeme
- Gelişmiş öneri algoritması
- Çoklu dil desteği
- 18 yaşından küçük kullanıcılar

---

## 8. Kodlama Kuralları

### Genel
- Commit mesajları Conventional Commits formatında olacak (`feat(matching): ...`, `fix(auth): ...`).
- Her faz ayrı bir branch'te geliştirilecek, tamamlanınca main'e merge edilecek.
- Gizli bilgiler (connection string, JWT anahtarı, FCM anahtarı) asla repoya girmez. Lokalde user-secrets veya `.env` kullan, `.env.example` dosyasını repoda tut.

### Backend
- Domain katmanının hiçbir dış bağımlılığı olmaz (EF Core dahil).
- Entity'ler zengin domain modelidir: davranış entity içindedir, setter'lar dışarıya açık değildir.
- Validation Application katmanında, iş kuralları Domain katmanında yer alır.
- Liste endpoint'lerinde sayfalama zorunludur. Keşfet ve mesajlar için cursor tabanlı sayfalama kullan.
- API versiyonlaması URL ile yapılır: `/api/v1/...`
- Her command handler için birim testi, her endpoint için en az bir entegrasyon testi yazılır.
- Yorum satırında "ne yapıldığını" değil, "neden yapıldığını" yaz.

### Mobil
- Widget'lar iş mantığı içermez. Mantık provider/notifier katmanındadır.
- Tüm metinler tek bir yerden gelir (ileride çoklu dil için `l10n` yapısını baştan kur, şimdilik sadece Türkçe).
- Yükleniyor, boş ve hata durumları her ekranda ele alınır.
- Ağ hataları kullanıcıya anlaşılır Türkçe mesajlarla gösterilir. Ham hata metni gösterilmez.
- Tema token'ları `core/theme/` altında tanımlanır, bileşenler yalnızca bunları kullanır.

---

## 9. Fazlar

**Her fazın sonunda dur, yaptıklarını özetle, `docs/roadmap.md` dosyasını güncelle ve onayımı bekle.**

### Faz 0 — Kurulum
- Repo yapısını oluştur, `.gitignore`, `.editorconfig` ve `docker-compose.yml` dosyalarını ekle.
- `AGENTS.md` (veya `CLAUDE.md`) içine bu belgenin kısa özetini ve çalışma kurallarını yaz.
- **Kabul kriteri:** `docker compose up` ile PostgreSQL ayağa kalkar.

### Faz 1 — Domain ve Veri Tasarımı (yalnızca doküman, kod yok)
- `docs/domain/` altına domain modelini, iş kurallarını ve terimler sözlüğünü yaz.
- `docs/erd/` altına Mermaid ERD çiz.
- Bölüm 6'daki açık soruları ve kendi tespit ettiğin belirsizlikleri bana liste halinde sun.
- İlk ADR'leri yaz: modüler monolit, PostgreSQL, auth stratejisi, mobil state yönetimi.
- **Kabul kriteri:** Benim onayladığım domain modeli ve ERD.

### Faz 2 — API Sözleşmesi
- `contracts/openapi.yaml` dosyasını MVP kapsamının tamamı için yaz.
- Ortak standartları tanımla: hata formatı, sayfalama, tarih formatı (ISO 8601, UTC).
- SignalR hub'larını ve event isimlerini `docs/` altında belgele.
- **Kabul kriteri:** Sözleşme lint'ten geçer ve ben onaylarım.

### Faz 3 — Backend İskeleti
- Solution ve katmanları kur, DI yapılandırmasını yap.
- Serilog, ProblemDetails, health check ekle.
- EF Core ve ilk migration'ı oluştur.
- Identity modülünü uçtan uca yaz (kayıt, giriş, refresh, çıkış).
- Testcontainers ile entegrasyon testi altyapısını kur.
- GitHub Actions CI ekle (build + test).
- **Kabul kriteri:** Auth uçtan uca çalışır, testler CI'da geçer.

### Faz 4 — Mobil İskelet
- Flutter projesini feature-first yapıyla kur.
- Tema token'larını, go_router'ı ve Riverpod'u yapılandır.
- OpenAPI'den API client'ı üret.
- Token yönetimini (güvenli depolama + otomatik yenileme interceptor'ı) yaz.
- Kayıt ve giriş ekranlarını gerçek backend'e bağla.
- **Kabul kriteri:** Kullanıcı mobilden kayıt olur, giriş yapar ve oturum uygulama yeniden açılınca korunur.

### Faz 5 — Dikey Dilimler
Her dilim için sıra şudur: sözleşme kontrolü, backend, testler, mobil ekran, uçtan uca deneme.

1. Profil + avatar yükleme (dilimin başında S3 uyumlu nesne depolama ürünü ADR ile seçilir ve `docker-compose.yml` dosyasına eklenir)
2. Yetenek kataloğu + kullanıcı yetenekleri
3. Keşfet (karşılıklı uyum sıralaması)
4. Takas isteği + eşleşme
5. Gerçek zamanlı mesajlaşma (SignalR)
6. Takas oturumları
7. Değerlendirmeler
8. Bildirimler (uygulama içi + FCM)

### Faz 6 — Sertleştirme ve MVP Yayını
- Rate limiting, girdi sınırları, performans kontrolleri (N+1 sorguları, index'ler).
- Hata izleme ve kapalı test sürümü hazırlığı.

### Faz 7 — Web İstemcisi
- Web teknolojisi için ADR yaz ve onayımı al.
- Aynı API ve SignalR hub'ları üzerine web istemcisini geliştir.
- Web için gerekirse cookie tabanlı BFF katmanı ekle.

---

## 10. Yapmaman Gerekenler

- Onay almadan faz atlama.
- Sözleşmede olmayan bir endpoint'i yazma. Önce sözleşmeyi güncelle.
- Açık soruları kendi başına karara bağlama.
- Testsiz backend kodu bırakma.
- Gizli bilgileri koda veya repoya koyma.
- Kullanılmayan paket, kullanılmayan katman veya "ileride lazım olur" kodu ekleme.
- Mevcut bir dosyayı bütünüyle yeniden yazma. Yalnızca gereken kısmı değiştir.

---

## 11. İlk Görevin

1. Bu belgeyi `docs/PROJECT_BRIEF.md` olarak kaydet.
2. `Exparter_Suite` klasörünün mevcut içeriğini incele ve bana raporla.
3. Faz 0 için yapacaklarını listele ve onayımı bekle.
