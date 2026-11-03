# Canlı ortam rehberi

Canlı ortamın nasıl kurulduğunu, nasıl güncellendiğini ve sorun olduğunda ne yapılacağını anlatır. Kararların gerekçesi: [ADR 0014](adr/0014-deployment.md).

```
Uygulama ──HTTPS──▶ Caddy ──▶ API ──▶ PostgreSQL        (Oracle Cloud, Frankfurt)
                      │        ├──▶ Cloudflare R2        (avatarlar)
                      │        ├──▶ Brevo SMTP           (e-posta)
                      │        ├──▶ Firebase             (telefon bildirimi)
                      │        └──▶ Sentry               (hatalar)
                      └──▶ deploy/site/                  (herkese açık sayfalar)
```

Aşağıda `<alan-adı>` sizin alan adınız (ör. `exparter.app`), `<ip>` sunucunun genel IP adresidir. Bilgisayarınızdaki komutlar PowerShell'de, sunucudakiler SSH oturumunda çalışır.

## 1. Hesaplar

| Servis | Ne için | Not |
|---|---|---|
| Cloudflare | Alan adının DNS'i ve R2 | Alan adı Cloudflare Registrar'dan alınırsa DNS kendiliğinden orada olur. |
| Oracle Cloud | Sunucu | Ana bölge **Germany Central (Frankfurt)**; sonradan değişmez. Kart doğrulaması istenir, ücret alınmaz. |
| Brevo | E-posta | Ücretsiz plan, günde 300 e-posta. |
| Google Play Console | Uygulamanın dağıtımı | Kişisel hesap, 25 $, kimlik doğrulaması birkaç gün sürebilir. |
| Firebase, Sentry | Bildirim, hata izleme | Zaten kurulu (`exparter-5d834`, `exparter` kuruluşu). |

## 2. Sunucu (Oracle Cloud)

1. **Compute → Instances → Create instance**
   - Image: **Canonical Ubuntu 24.04** (aarch64).
   - Shape: **Ampere → VM.Standard.A1.Flex**, 2 OCPU, 12 GB bellek (Always Free sınırı).
   - Networking: genel IPv4 adresi atansın.
   - SSH keys: kendi genel anahtarınızı yükleyin (`~/.ssh/id_ed25519.pub`; yoksa `ssh-keygen -t ed25519`).
   - "Out of capacity" hatası alırsanız başka bir availability domain seçin veya birkaç saat sonra yeniden deneyin.
2. **Ağ:** Instance → Subnet → Security List → Ingress Rules: `0.0.0.0/0` kaynaktan TCP 80, TCP 443 ve UDP 443; görüntülü görüşmenin medyası için TCP 7881 ve UDP 7882 (ADR 0015).
3. **Sunucunun güvenlik duvarı:** Oracle'ın Ubuntu imajı 22 dışındaki portları kendi iptables kurallarıyla kapatır. Bağlanıp açın:

   ```sh
   ssh ubuntu@<ip>
   sudo iptables -I INPUT 6 -m state --state NEW -p tcp --dport 80 -j ACCEPT
   sudo iptables -I INPUT 6 -m state --state NEW -p tcp --dport 443 -j ACCEPT
   sudo iptables -I INPUT 6 -m state --state NEW -p udp --dport 443 -j ACCEPT
   sudo iptables -I INPUT 6 -m state --state NEW -p tcp --dport 7881 -j ACCEPT
   sudo iptables -I INPUT 6 -m state --state NEW -p udp --dport 7882 -j ACCEPT
   sudo netfilter-persistent save
   ```

4. **Docker:**

   ```sh
   sudo apt update && sudo apt upgrade -y
   curl -fsSL https://get.docker.com | sudo sh
   sudo usermod -aG docker ubuntu
   exit   # yeniden bağlanınca docker grubu geçerli olur
   ```

5. **Güncellemeler:** `sudo apt install -y unattended-upgrades` (Ubuntu'da varsayılan olarak açıktır; güvenlik yamaları kendiliğinden kurulur).

Oracle, uzun süre neredeyse hiç işlemci kullanmayan ücretsiz makineleri geri alabilir. Hesabı "Pay As You Go"ya yükseltmek bunu önler; ücretsiz sınırlar içinde kalındıkça ücret çıkmaz. Bu karar sizindir.

## 3. DNS (Cloudflare)

DNS → Records:

| Tür | Ad | İçerik | Proxy |
|---|---|---|---|
| A | `@` | `<ip>` | **DNS only** (gri bulut) |
| A | `api` | `<ip>` | **DNS only** |
| A | `rtc` | `<ip>` | **DNS only** |

Vekil sunucu (turuncu bulut) kapalı kalmalı: sertifikayı Caddy alır ve WebSocket bağlantısı doğrudan sunucuya gelir.

## 4. Depolama (Cloudflare R2)

1. **R2 → Create bucket:** ad `exparter`, Location **Specify jurisdiction → European Union**. Herkese açık erişim kapalı kalır.
2. Aynı şekilde yedekler için `exparter-backups` bucket'ı; Settings → **Object lifecycle rules**: 30 günden eski nesneleri sil.
3. **R2 → Manage API tokens → Create API token:** izin **Object Read & Write**, yalnızca `exparter` bucket'ı. Access Key ID ve Secret Access Key'i not edin (bir kez gösterilir).
4. Yedek için ayrı bir token: yalnızca `exparter-backups`.
5. Endpoint, AB bucket'ları için `https://<hesap-kimliği>.eu.r2.cloudflarestorage.com` biçimindedir (bucket'ın Settings sayfasında görünür).

Uygulama dosyaları API'den değil, presigned URL ile doğrudan R2'den yükler ve indirir. Mobil uygulama tarayıcı olmadığı için CORS ayarı gerekmez.

## 5. E-posta (Brevo)

1. **Senders, Domains & Dedicated IPs → Domains → Add a domain:** `<alan-adı>`. Brevo'nun verdiği kayıtları (Brevo kodu, DKIM, DMARC) Cloudflare DNS'e ekleyin; Brevo Cloudflare için otomatik kurulum da sunar. SPF için Brevo'nun önerdiği kaydı kullanın.
2. Doğrulandıktan sonra **Senders → Add a sender:** `no-reply@<alan-adı>`, ad "Exparter".
3. **SMTP & API → SMTP:** SMTP login'i not edin ve **Generate a new SMTP key** ile bir anahtar üretin.

**Gelen e-posta (destek adresi):** Metinlerde iletişim adresi `destek@<alan-adı>`. Cloudflare → alan adı → **Email → Email Routing → Get started** ile açılır; **Destination addresses** bölümüne kişisel adresiniz eklenip doğrulanır, **Routing rules** bölümünde `destek@` o adrese yönlendirilir. Cloudflare gereken MX ve SPF kayıtlarını kendisi ekler. Brevo yalnızca gönderim yaptığı için çakışmaz; ancak alan adında tek bir `v=spf1` kaydı olmalı: Cloudflare'in eklediği kayda Brevo'nun `include:` ifadesini ekleyin.

## 6. Uygulamayı sunucuya yerleştirme

Bilgisayarınızda, repo kökünden:

```powershell
ssh ubuntu@<ip> "mkdir -p exparter/secrets"
scp deploy/.env.example ubuntu@<ip>:exparter/.env
scp D:\Yazilim\secrets\exparter-5d834-firebase-adminsdk-fbsvc-ee0413f587.json ubuntu@<ip>:exparter/secrets/firebase-adminsdk.json
```

Sunucuda:

```sh
cd ~/exparter
# API, imajın yetkisiz kullanıcısıyla (1654) çalışır ve anahtarı okuyabilmelidir.
sudo chown 1654:1654 secrets/firebase-adminsdk.json && sudo chmod 400 secrets/firebase-adminsdk.json
openssl rand -hex 24      # POSTGRES_PASSWORD için
openssl rand -base64 48   # JWT_SIGNING_KEY için
openssl rand -hex 32      # LIVEKIT_API_SECRET için; LIVEKIT_NODE_IP sunucunun genel IP'si
nano .env                 # tüm alanları doldurun; açıklamalar dosyanın içinde
chmod 600 .env
```

İmaj GitHub'da gizli bir pakettir; sunucunun onu çekebilmesi için GitHub'da **Settings → Developer settings → Personal access tokens (classic)** altında yalnızca `read:packages` yetkili bir token üretin ve sunucuda giriş yapın:

```sh
docker login ghcr.io -u YerdemA   # parola olarak token'ı yapıştırın
```

## 7. Otomatik dağıtımı açma (GitHub Actions)

Bilgisayarınızda dağıtıma özel bir SSH anahtarı üretin ve sunucuya tanıtın:

```powershell
ssh-keygen -t ed25519 -f D:\Yazilim\secrets\exparter-deploy -N '""' -C github-deploy
type D:\Yazilim\secrets\exparter-deploy.pub | ssh ubuntu@<ip> "cat >> ~/.ssh/authorized_keys"
ssh-keyscan -t ed25519 <ip>
```

GitHub'da repo → **Settings → Secrets and variables → Actions**:

| Tür | Ad | Değer |
|---|---|---|
| Secret | `DEPLOY_SSH_KEY` | `D:\Yazilim\secrets\exparter-deploy` dosyasının içeriği |
| Variable | `DEPLOY_HOST` | `<ip>` |
| Variable | `DEPLOY_USER` | `ubuntu` |
| Variable | `DEPLOY_KNOWN_HOSTS` | `ssh-keyscan` çıktısı |

Sonra **Actions → Deploy → Run workflow** (main). İş akışı dosyaları kopyalar, migration'ı çalıştırır ve servisleri başlatır. İlk açılışta Caddy sertifikaları birkaç saniye içinde alır.

**Doğrulama:**

```powershell
curl https://api.<alan-adı>/health
curl -I https://<alan-adı>/gizlilik.html
```

Bundan sonra `main`'e birleşen her backend değişikliği kendiliğinden dağıtılır.

## 8. Yedekler

Sunucuda, her gece 03:30'da (UTC) yedek:

```sh
(crontab -l 2>/dev/null; echo "30 3 * * * sh /home/ubuntu/exparter/backup.sh >> /home/ubuntu/exparter/backup.log 2>&1") | crontab -
sh ~/exparter/backup.sh && ls -la ~/exparter/backups   # ilk yedeği hemen alıp deneyin
```

**Geri yükleme** (önce API durdurulur, yedek seçilir):

```sh
cd ~/exparter
docker compose stop api
docker compose exec -T postgres pg_restore -U exparter -d exparter --clean --if-exists < backups/exparter-<zaman>.dump
docker compose start api
```

R2'deki bir kopyayı geri yüklemek için önce Cloudflare panelinden indirip `backups/` altına koyun. Geri yüklemeyi yayından önce bir kez deneyin.

## 9. Mobil yayın derlemesi

Yükleme anahtarı `D:\Yazilim\secrets\exparter-upload.jks`, şifresi `mobile/android/key.properties` içindedir; ikisi de repoda yoktur. **İkisini güvenli bir yere (ör. parola yöneticisi) yedekleyin.** `google-services.json` da `mobile/android/app/` altında olmalıdır.

1. `mobile/pubspec.yaml` içindeki `version` değerini artırın; `+` sonrasındaki sayı (versionCode) her yüklemede büyümelidir.
2. `mobile/` klasöründen:

   ```powershell
   flutter build appbundle --release `
     --dart-define=API_BASE_URL=https://api.<alan-adı> `
     --dart-define=SENTRY_DSN=https://5619e5b90660bdfb1ba59ee1c230a4d1@o4512255483248640.ingest.de.sentry.io/4512255662882896
   ```

3. Çıktı: `mobile/build/app/outputs/bundle/release/app-release.aab`. Play Console'a bu yüklenir ([mağaza rehberi](store/listing.md)).

## 10. Günlük işler

| İş | Komut (sunucuda, `~/exparter`) |
|---|---|
| Durum | `docker compose ps` |
| API kayıtları | `docker compose logs -f --tail 200 api` |
| Bir hatanın kayıtları | `docker compose logs api \| grep <traceId>` (hata yanıtındaki `traceId`) |
| Yeniden başlatma | `docker compose restart api` |
| Elle dağıtım | GitHub → Actions → Deploy → Run workflow |
| Önceki sürüme dönme | `.env` içinde `API_IMAGE`'ı önceki commit'in etiketiyle değiştirip `docker compose up -d api` (geriye uyumsuz migration varsa önce yedeği geri yükleyin) |
| Disk | `df -h` ve `docker system df` |
| Moderasyon, yetenek önerileri | `scripts/admin-moderation.ps1` ve `scripts/admin-skill-proposals.ps1`, bilgisayarınızdan `-ApiBaseUrl https://api.<alan-adı>` ile |
| Trafik kayıtları (5651) | `~/exparter/logs/caddy/` altında, satır başına bir JSON; 1 yıl sonra Caddy siler. Yetkili makam istediğinde ilgili zaman aralığı buradan verilir. |

## 11. Hukuki yükümlülükler

Gerekçeleri: [compliance-research.md](legal/compliance-research.md).

- **Yer sağlayıcılık bildirimi (5651):** Genel yayından önce e-Devlet'te "BTK Yer Sağlayıcılığı Bildirimi" hizmetinden yapılır. 2026 değişikliğiyle yetkili makam Siber Güvenlik Başkanlığı oldu; başvuru sırasında ekrandaki kurum adını ve formu kontrol edin.
- **Hukuka aykırı içerik bildirimi:** `destek@` adresine gelir; içerik yönetici betiğiyle kaldırılır veya hesap askıya alınır, işlem ve tarih not edilir.
- **Veri ihlali:** Veritabanına, yedeklere veya anahtarlara yetkisiz erişim fark edilirse: anahtarları değiştirin (`.env`, R2 ve Brevo anahtarları, JWT anahtarı; JWT değişince herkes yeniden giriş yapar), ne olduğunu ve kimleri etkilediğini not edin, **72 saat içinde** KVKK'nın Veri İhlali Bildirim formunu doldurun ve etkilenen kullanıcıları e-postayla bilgilendirin.
- **Standart sözleşmeler:** [scc-requests.md](legal/scc-requests.md). İmzalanan her sözleşme 5 iş günü içinde Kurul'a bildirilir.
- **KVKK başvuruları:** `destek@` adresine, kullanıcının kayıtlı e-postasından gelen başvurular en geç 30 gün içinde ücretsiz yanıtlanır.
- **Koşullar değişirse:** `deploy/site/kullanim-kosullari.html` ile birlikte `TermsOfUse.CurrentVersion` değişir; her hesap kabul ettiği sürümü saklar.

## 12. Başka sunucuya taşıma

Oracle makineyi geri alırsa veya yetmezse (ör. Hetzner): yeni makinede 2. bölümdeki Docker kurulumunu yapın (Hetzner'de iptables adımı yok, güvenlik duvarı panelden), 6. bölümü tekrarlayın, son yedeği 8. bölümdeki gibi geri yükleyin, DNS kayıtlarını ve `DEPLOY_HOST`/`DEPLOY_KNOWN_HOSTS` değişkenlerini yeni IP'ye çevirin. Hetzner'in makinesi x64 ise `deploy.yml` içindeki `platforms` değerini `linux/amd64` yapın.
