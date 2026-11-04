# Gerçek Zamanlı İletişim

OpenAPI, SignalR hub'larını tarif edemez; bu belge hub sözleşmesinin tek kaynağıdır. Yük şemaları `contracts/openapi.yaml` içindeki şemalara atıfla tanımlanır.

## İlkeler

- **Tek hub:** `/hubs/realtime`. İstemci tek bağlantı açar; mobil ve web aynı hub'ı kullanır.
- **Yazma REST'ten, bildirim hub'dan:** Durumu değiştiren her işlem REST endpoint'iyle yapılır. Hub yalnızca sunucudan istemciye olay iletir. Böylece doğrulama, yetkilendirme ve hata biçimi tek yerde kalır.
- **Olay bir ipucudur, kaynak REST'tir:** İstemci bağlantı koptuğunda olay kaçırabilir. Yeniden bağlandığında ilgili listeleri REST'ten yeniler; hub olaylarını kaçırılanları telafi etmek için kullanmaz.
- **Hedefleme:** Sunucu her olayı ilgili kullanıcının tüm açık bağlantılarına gönderir. İstemcinin gruba katılması gerekmez.

## Bağlantı ve kimlik doğrulama

- Bağlantı access token ile kurulur. WebSocket başlık taşıyamadığı için token `access_token` sorgu parametresiyle gönderilir (SignalR'ın standart yöntemi).
- Bağlantı kurulduktan sonra token'ın süresi dolsa da bağlantı açık kalır. İstemci yeniden bağlanırken güncel token'ı kullanır.
- Hesap askıya alındığında veya silme isteği verildiğinde sunucu kullanıcının bağlantılarını kapatır. Access token birkaç dakika daha geçerli kalsa da hesabı etkin olmayan kullanıcı yeniden bağlanamaz.
- E-postası doğrulanmamış kullanıcı da bağlanabilir; bildirim alır.

## Sunucudan istemciye olaylar

| Olay | Yük | Ne zaman |
|---|---|---|
| `MessageReceived` | `Message` | Kullanıcının taraf olduğu bir sohbete mesaj geldiğinde. Gönderenin diğer cihazlarına da gider. |
| `ConversationRead` | `{ conversationId, userId, readAt }` | Taraflardan biri sohbeti okundu işaretlediğinde. |
| `ConversationUpdated` | `Conversation` | Sohbet kapandığında veya yeniden açıldığında. |
| `NotificationReceived` | `Notification` | Kullanıcı için yeni bir bildirim oluştuğunda. |

`MessageReceived` yükündeki `clientMessageId`, gönderen istemcinin iyimser olarak eklediği mesajı sunucudan gelen kopyayla eşleştirmesini sağlar.

Takas isteği, eşleşme ve oturum değişiklikleri için ayrı olay yoktur. Bunların her biri bir bildirim üretir; istemci `NotificationReceived` yükündeki `type` ve `data` alanlarına bakarak ilgili kaynağı REST'ten yeniler.

## İstemciden sunucuya metotlar

Yok. Mesaj gönderme dahil tüm yazma işlemleri REST üzerinden yapılır.

## Push bildirimleri (FCM)

Karar ve gerekçe: ADR 0012.

- Bir bildirim oluştuğunda `NotificationReceived` her zaman gönderilir; kullanıcının açık hub bağlantısı yoksa kayıtlı cihazlarına ayrıca FCM ile push gider.
- Uygulama arka plana geçince hub bağlantısını kapatır, öne gelince yeniden açar ve listeleri yeniler. Böylece "açık bağlantı yok" ile "uygulama ekranda değil" aynı anlama gelir.
- Yeni mesajlar bildirim listesine yazılmaz (sohbet listesindeki okunmamış sayısı bu işi görür), ama alıcının açık bağlantısı yoksa push gönderilir: başlık "Exparter", gövde "Yeni bir mesajınız var."
- Push'un metnini sunucu üretir (Türkçe başlık ve gövde); telefon uygulama kapalıyken bile gösterir. Metin yurt dışındaki FCM'den geçtiği için kimseyi adlandırmaz ve kullanıcının yazdığı hiçbir şeyi içermez (NT-11, 2026-10-27).
- Push'un `data` alanı, dokununca açılacak ekran içindir. Tüm değerler metindir:

| Anahtar | Ne zaman | Açıklama |
|---|---|---|
| `type` | Her zaman | `NotificationType` değeri veya yeni mesaj için `MessageReceived`. |
| `notificationId` | Bildirimlerde | Dokununca bildirim okundu sayılır. |
| `swapRequestId`, `matchId`, `sessionId`, `reviewId`, `skillId` | Bildirimde varsa | Bildirimin `data` alanındaki kimlikler. |
| `conversationId` | Yeni mesajda | Dokununca sohbet açılır. |

- FCM bir token'ı artık tanımadığını bildirirse o cihaz kaydı silinir. Çıkış yapan uygulama kendi cihaz kaydını siler; hesap silme isteği kullanıcının tüm cihaz kayıtlarını siler.
