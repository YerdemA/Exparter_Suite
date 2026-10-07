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
- Hesap askıya alındığında veya silme isteği verildiğinde sunucu kullanıcının bağlantılarını kapatır.
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

- Bir bildirim oluştuğunda kullanıcının açık hub bağlantısı varsa yalnızca `NotificationReceived` gönderilir; yoksa kayıtlı cihazlarına FCM ile push gönderilir.
- Yeni mesajlar bildirim listesine yazılmaz (sohbet listesindeki okunmamış sayısı bu işi görür), ama alıcının açık bağlantısı yoksa push gönderilir.
- Push yükü `type` ile ilgili kimlikleri taşır; metni istemci kendi dil dosyasından üretir.
- FCM bir token'ı geçersiz bildirirse o cihaz kaydı silinir.
