# ADR 0014: Canlı ortam ve dağıtım

- **Durum:** Kabul edildi
- **Tarih:** 2026-10-14

## Bağlam

Kapalı test için uygulamanın internetten erişilebilir bir backend'e ihtiyacı var: API, PostgreSQL, nesne depolama, e-posta gönderimi ve HTTPS. MVP ve test süreci için maliyet sıfıra yakın olmalı; ama yük büyüdüğünde veya ücretsiz servis yetmediğinde kod değişmeden başka bir sunucuya taşınabilmeli.

Değerlendirilen seçenekler: Oracle Cloud Always Free (ARM sanal makine), Hetzner (aylık yaklaşık 4–6 €), yönetilen platformlar (Render, Fly.io, Railway; ücretsiz katmanları uyuyan servis veya süreli veritabanı sunuyor, SignalR'ın sürekli bağlantısına uygun değil).

## Karar

- **Sunucu: Oracle Cloud Always Free, Frankfurt** (kullanıcı kararı, 2026-10-14). Ampere A1 (ARM64), Ubuntu 24.04. Tek sanal makine.
- **Her şey Docker Compose ile çalışır** (`deploy/docker-compose.yml`): API, PostgreSQL ve Caddy; 2026-11-03'ten beri görüntülü görüşme için LiveKit (ADR 0015). Veritabanı dışarıya port açmaz; içeriye tek giriş Caddy'dir, yalnızca görüşmenin medyası LiveKit'e doğrudan gelir.
- **Caddy** HTTPS sertifikalarını Let's Encrypt'ten kendisi alır ve yeniler. API'yi (WebSocket dahil) ve herkese açık sayfaları (`deploy/site/`) sunar. Gizlilik, KVKK, kullanım koşulları ve hesap silme sayfaları `deploy/site/` altındadır ve dağıtımla yayımlanır (2026-10-27'ye kadar taslak olarak `docs/legal/` altındaydılar). API, Caddy'nin ilettiği istemci adresine güvenir (`ASPNETCORE_FORWARDEDHEADERS_ENABLED`); istek sınırları gerçek istemciyi sayar.
- **Alan adı ve DNS: Cloudflare** (alan adı kullanıcı tarafından alınacak). Kayıtlar yalnızca DNS'tir (gri bulut); trafik Cloudflare'in vekil sunucusundan geçmez, sertifikayı Caddy alır.
- **Nesne depolama: Cloudflare R2**, AB yetki alanında (ADR 0010 güncellendi). Ücretsiz katman avatarlar için yeterlidir ve veri çıkışı ücretsizdir.
- **E-posta: Brevo**, SMTP ile (kullanıcı kararı, ADR 0007 güncellendi). Ücretsiz planda günde 300 e-posta.
- **İmaj:** `backend/Dockerfile`, arm64 imajı x64 makinede emülasyonsuz derler (çapraz derleme). İmaj GitHub Container Registry'de (`ghcr.io`) tutulur. Backend CI her push'ta imajı derler ama yayımlamaz.
- **Dağıtım:** `Deploy` iş akışı `main`'e her birleşmede imajı commit kimliğiyle yayımlar, sonra sunucuya SSH ile bağlanıp önce migration'ı (`--migrate`) ayrı bir adımda çalıştırır, ardından yeni sürümü başlatır. Migration başarısız olursa eski sürüm çalışmaya devam eder. Sunucu tanımlanana kadar (`DEPLOY_HOST` değişkeni boşken) yalnızca imaj yayımlanır. Migration'dan önce `deploy/prepare-server.sh` çalışır: yeni sürümlerin istediği ayarları ve güvenlik duvarı kurallarını, eksikse, sunucuya SSH ile bağlanmadan ekler (2026-11-03).
- **Gizli bilgiler** yalnızca sunucudaki `.env` dosyasında ve `secrets/` klasöründedir (Firebase hizmet hesabı anahtarı); repoda yalnızca `deploy/.env.example` vardır.
- **Yedek:** `deploy/backup.sh` her gece `pg_dump` alır; sunucuda 14 gün tutulur, ayrı bir R2 bucket'ına kopyalanır ve orada 30 gün saklanır.
- **Kayıtlar** Docker'ın JSON dosyalarında, konteyner başına boyut sınırıyla tutulur (ADR 0013 güncellendi). Kapalı testte merkezi kayıt servisi yok.
- **Trafik kayıtları (5651):** Caddy her isteğin IP adresini ve portunu, zamanını, süresini, adresini, sonucunu ve boyutunu `logs/caddy/` altına yazar ve 1 yıl tutar. Başlıklar ve hub'ın adresteki erişim token'ı yazılmaz; dosyalar gece yedeğine girmez. Dağıtım her seferinde Caddy'yi yeniden yükler, böylece Caddyfile değişiklikleri de yürürlüğe girer. (2026-10-27'de eklendi; kullanıcı kararı, `docs/legal/compliance-research.md`.)
- **Android yayın imzası:** Play App Signing kullanılır. Yükleme anahtarı repo dışındadır; `mobile/android/key.properties` dosyası yolunu ve şifresini verir. Dosya yoksa yayın derlemesi debug anahtarıyla imzalanır, böylece CI ve lokal derlemeler çalışmaya devam eder.

## Sonuçlar

- Oracle'ın ücretsiz katmanında Frankfurt'ta kapasite bulunamayabilir ve uzun süre boşta kalan makineler geri alınabilir. Bu olursa aynı `deploy/` klasörü ve yedekle Hetzner'e taşınılır; taşıma bir saatlik iştir ve kod değişmez.
- Tek makine olduğu için bakım ve güncellemelerde kısa kesintiler olur. Kapalı test için kabul edilebilir.
- Brevo'nun günlük sınırı aşılırsa doğrulama e-postaları ertesi güne kalır. Kullanıcı sayısı artınca ücretli plana veya başka bir SMTP sağlayıcısına geçmek yalnızca yapılandırma değişikliğidir.
- Kişisel veriler Türkiye dışında (AB ve Firebase üzerinden ABD) işlenir. Yurt dışına aktarım için karar 2026-10-27'de araştırmayla verildi: sağlayıcılar metinlerde isimle yazılır ve her birine standart sözleşme talebi gönderilir (`docs/legal/compliance-research.md`, 5. bölüm).
- Yükleme anahtarı kaybolursa Play Console üzerinden sıfırlanabilir; uygulama imzalama anahtarını Google tuttuğu için kullanıcılar etkilenmez.
