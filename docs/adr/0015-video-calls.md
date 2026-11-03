# ADR 0015: Görüntülü görüşme

- **Durum:** Kabul edildi
- **Tarih:** 2026-11-03

## Bağlam

Çevrimiçi oturumlar şimdiye kadar yalnızca bir saat ve süreden ibaretti; kullanıcılar başka bir uygulamada buluşuyordu. Kullanıcı kararı (2026-10-13): görüşme uygulamaya gömülü olacak ve yalnızca planlanan oturum saatinde yapılabilecek. İstemci tipinden bağımsız olmalı (mobil şimdi, web Faz 7'de), maliyet sıfıra yakın kalmalı ve kişisel veri yeni bir yurt dışı sağlayıcıya gitmemeli (`docs/legal/compliance-research.md`).

Değerlendirilen seçenekler:

- **Kendi barındırdığımız LiveKit** (açık kaynak WebRTC SFU): resmî Flutter ve web SDK'ları, yeniden bağlanma ve ağa göre kalite ayarı hazır, gömülü TURN sunucusu var, arm64 imajı var.
- **Eşler arası WebRTC + coturn**: medya iki cihaz arasında gider, ama sinyalleşme, yeniden bağlanma ve hata durumları bizim kodumuz olur.
- **Hazır servis (Agora, Daily vb.)**: kurulum en az, ama ücretli kota, yurt dışında işlenen veri ve yeni bir veri işleyen sözleşmesi.

## Karar

- **LiveKit, mevcut Oracle sunucusunda** (kullanıcı kararı, 2026-11-03). Ses ve görüntü Frankfurt'taki sunucudan geçer, hiçbir yerde kaydedilmez. Yerelde `docker-compose.yml`, canlıda `deploy/docker-compose.yml` içinde ayrı bir servistir; sinyalleşme `rtc.exparter.app` adresinden Caddy üzerinden gider.
- **Görüşme penceresi** (kullanıcı kararı, 2026-11-03): planlanan başlangıçtan 10 dakika önce açılır, süre bittikten 15 dakika sonra kapanır. Kural `SwapSession` içindedir; pencere yalnızca kabul edilmiş ve iptal edilmemiş çevrimiçi oturumlar için vardır. Tamamlandı işareti görüşmeyi bitirmez, çünkü kullanıcılar bunu çoğu zaman kapatmadan önce yapar.
- **Backend yalnızca anahtar verir.** `POST /sessions/{id}/call-token` pencere içindeyken iki taraftan birine kısa ömürlü bir LiveKit erişim anahtarı (JWT) döndürür. Oda adı oturum kimliğidir, katılımcı kimliği kullanıcı kimliğidir; ad, fotoğraf gibi bilgiler LiveKit'e gitmez, istemci bunları zaten bilir. Anahtar projede zaten olan JWT kütüphanesiyle imzalanır; LiveKit sunucu SDK'sı eklenmez.
- **Pencere kapanınca oda sunucuda kapatılır.** LiveKit anahtarın süresini yalnızca katılırken denetler. Bu yüzden arka plan işi süresi dolan, iptal edilen veya eşleşmesi biten oturumların odasını LiveKit'in RoomService API'siyle siler. İstemci de pencerenin sonunda kendisi ayrılır; sunucudaki silme bunun güvencesidir.
- **Katılım kaydı tutulmaz** (kullanıcı kararı, 2026-11-03). Tamamlama akışı eskisi gibi iki tarafın onayıyla yürür.
- **Arka planda ses sürer, kamera durur** (kullanıcı kararı, 2026-11-03). Android'de bir ön plan servisi (mikrofon türü) "görüşme sürüyor" bildirimi gösterir.
- **Ağ:** Medya tek bir UDP portundan (7882), UDP'nin engellendiği ağlarda TCP 7881'den gider. Sunucunun genel bir IP'si olduğu için NAT arkasındaki istemciler de doğrudan bağlanabilir, bu yüzden ilk sürümde TURN açılmaz. LiveKit Docker'ın köprü ağında çalışır; portlar `docs/deployment.md` içinde listelenir, Oracle güvenlik listesinde ve sunucu güvenlik duvarında açılır.

## Sonuçlar

- Sunucuda ek bir servis ve açık portlar var. Bire bir görüşmenin trafiği Oracle'ın aylık ücretsiz çıkış kotasının çok altında kalır, ama aynı anda çok sayıda görüşme olursa sunucunun işlemci ve bant genişliği izlenmeli.
- Yalnızca 443/TCP'ye izin veren ağlarda (bazı kurum ve yurt ağları) görüşme kurulamaz. Bunun için TURN/TLS'in 443'ten verilmesi gerekir; 443'ü Caddy kullandığından bu, SNI'ye göre ayrıştırma (ör. Caddy'nin layer4 eklentisi) ister. İlk sürümde yapılmaz; bu tür ağlarda sorun çıkarsa ayrı bir kararla eklenir.
- Gizlilik metni (ses ve görüntü aktarılır, kaydedilmez) ve Play'deki Data safety formu ile kamera, mikrofon ve ön plan servisi izin beyanları güncellenmelidir.
- Web istemcisi aynı uç noktadan anahtar alıp LiveKit'in web SDK'sıyla katılır; backend'de değişiklik gerekmez.
