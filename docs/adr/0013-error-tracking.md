# ADR 0013: Hata izleme ve kayıtlar

- **Durum:** Kabul edildi
- **Tarih:** 2026-10-14

## Bağlam

Kapalı test sürümüne çıkmadan önce, kullanıcıların karşılaştığı hataların onlar haber vermeden görülmesi gerekir: backend'de yakalanmayan istisnalar ve arka plan işlerinin hataları, mobilde uygulamanın çökmeleri. Loglar şimdiye kadar yalnızca konsola düz metin olarak yazılıyordu ve bir hata yanıtını loglarla eşleştirmenin yolu yoktu.

Seçenekler: Sentry (hizmet), GlitchTip (Sentry SDK'larıyla uyumlu, kendi sunucumuzda), mobil için Firebase Crashlytics ile backend'de yalnızca loglar.

## Karar

- **Sentry, AB veri bölgesi** (kullanıcı kararı, 2026-10-14). Backend (.NET) ve mobil (Flutter) aynı araçta izlenir. Ücretsiz Developer planıyla başlanır. Veriler Frankfurt'ta tutulur; bölge kuruluş açılırken seçilir ve sonradan değişmez.
- **Kişisel veri gönderilmez.** `SendDefaultPii` kapalıdır: IP adresi, başlıklar ve istek gövdesi gitmez. Kullanıcıdan yalnızca kimliği (UUID) gider; bir hatanın kaç kişiyi etkilediği görülsün diye. E-posta, kullanıcı adı ve görünen ad gönderilmez.
- **Performans izleme kapalıdır** (`TracesSampleRate = 0`); yalnızca hatalar gönderilir.
- **Backend:** Hata seviyesindeki her log Sentry olayı olur; ondan önceki bilgi seviyesindeki loglar iz (breadcrumb) olarak eklenir. DSN `Sentry:Dsn` ayarıyla verilir (lokalde user-secrets). Boşsa Sentry kapalıdır; CI ve testler böyle çalışır.
- **Mobil:** Yakalanmayan hatalar ve çökmeler raporlanır. DSN derlemede `--dart-define=SENTRY_DSN=...` ile verilir; verilmezse Sentry hiç başlatılmaz. Mobil DSN uygulamanın içinde durur; Sentry bunu açık bilgi sayar, yalnızca olay göndermeye yarar.
- **Kayıtlar:** Geliştirmede okunur metin, diğer ortamlarda satır başına bir JSON nesnesi (Serilog compact JSON). Her kayıt istek izini (trace id) taşır. Hata yanıtındaki `traceId` aynı değerdir; bir kullanıcının bildirdiği hata, loglardaki satırlarına bu değerle bulunur.
- **Log kuralı:** Loglara e-posta, kullanıcı adı, parola, token veya mesaj içeriği yazılmaz. Kimlikler (UUID) yazılabilir.

## Sonuçlar

- Sentry'nin ücretsiz planında aylık olay sınırı vardır; aşılırsa fazlası atılır. Kapalı testte yeterlidir, yayından önce kullanım yeniden değerlendirilir.
- GlitchTip'e geçmek yalnızca DSN değişikliğidir; SDK'lar aynı kalır.
- JSON kayıtların nerede toplanacağı canlı ortamla birlikte (dilim 6) seçilir.
