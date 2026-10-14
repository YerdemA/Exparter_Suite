# ADR 0012: Telefon bildirimleri (FCM)

- **Durum:** Kabul edildi
- **Tarih:** 2026-10-14

## Bağlam

Brief, uygulama arka plandayken bildirimlerin Firebase Cloud Messaging ile gönderilmesini ister. Uygulama içi bildirimler dilim 8A'da hazırdı: bildirim kaydedilir, açık bağlantılara `NotificationReceived` olayıyla gider. Telefon bildirimi için dört şeyin kararı gerekiyordu: metni kim üretir, kime ne zaman gider, gönderim isteği bekletir mi, cihazlar nasıl tanınır.

## Karar

- **Metni sunucu üretir** (kullanıcı kararı, 2026-10-14). FCM yükü `notification` (başlık ve gövde) ile `data` (tür ve ilgili kimlikler) taşır; uygulama kapalıyken bildirimi Android kendisi gösterir. Metinler `PushTexts` içinde uygulamanın dil dosyasındaki cümlelerle aynıdır. Uygulama içi listede metni hâlâ istemci kurar (NT-08 yalnızca liste için geçerlidir).
- **Açık uygulaması olan kullanıcıya push gitmez.** Hub bağlantıları süreç içinde sayılır (`ConnectionRegistry`). Uygulama arka plana geçince bağlantıyı kapatır, öne gelince yeniden açar ve kaçırdıklarını yeniler.
- **Gönderim arka plandadır.** İstek, push'u bellek içi bir kuyruğa bırakır; `PushWorker` sırayla gönderir. FCM'in yavaşlığı veya hatası isteği etkilemez. Push en iyi çabadır: kuyruk dolarsa en eskiler düşer, süreç durursa kuyrukta kalanlar kaybolur. Bildirim listede yine görünür.
- **Cihazlar FCM registration token'ıyla tanınır.** FirebaseAdmin bunları kullanımdan kaldırılmış (deprecated) sayıp kurulum kimliklerini (FID) öneriyor, ancak Flutter eklentisi henüz yalnızca token veriyor. FCM'in artık tanımadığı token'ların cihaz kaydı silinir.
- **Kimlik bilgisi repoda değildir.** Servis hesabı anahtarının yolu `Firebase:CredentialsPath` ayarıyla verilir (lokalde user-secrets). Ayar yoksa push gönderilmez ve uygulama çalışmaya devam eder (CI ve testler). `google-services.json` da repoya girmez; her geliştirici kendi kopyasını indirir.

## Sonuçlar

- Bağlantı sayımı tek süreçlidir. Birden fazla API örneği çalıştırıldığında (Faz 6) paylaşılan bir kayda (ör. Redis) veya SignalR backplane'ine taşınmalıdır.
- Bildirim cümleleri iki yerde durur (mobil `app_tr.arb`, backend `PushTexts`). Biri değişince diğeri de güncellenmelidir.
- Sunucu telefonun saat dilimini bilmez; oturum hatırlatması saat yerine "yarın bu saatte" / "yaklaşık bir saat sonra" der.
- Flutter eklentisi FID desteği verdiğinde cihaz kaydı FID'ye taşınır.
