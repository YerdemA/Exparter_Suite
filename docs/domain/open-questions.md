# Kararlar ve Açık Sorular

## Verilen kararlar (2026-10-06)

| Konu | Karar |
|---|---|
| Takas modeli | MVP'de tamamen karşılıklı. Zaman/kredi bankası yok. |
| Konum | Yalnızca şehir tutulur. |
| Eşleşme önerisi | MVP'de kural tabanlı sıralama: karşılıklı uyum, tercih uyumu, puan. |
| Moderasyon | Şikâyet ve engelleme MVP'de. Admin paneli MVP sonrasında. |
| Kayıt | MVP'de yalnızca e-posta. Şema harici girişi engellemeyecek. |
| İsteğin içeriği | Takas isteği yetenek çiftini taşır. |
| Tek yönlü istek | Karşılıklı uyum olmadan da istek gönderilebilir. |
| Eşleşmenin ömrü | Karşılıklı onayla sonlandırılır. İki kullanıcı arasında aynı anda tek eşleşme. |
| İstek süresi | 3 gün sonra `Expired`. Gönderen geri çekebilir. |
| Oturum tamamlama | Karşılıklı onayla. |
| Oturum iptali | Tek taraflı; sebep metni zorunlu, karşı tarafa bildirilir ve admine düşer. |
| Değerlendirme | 1–10 puan ve yorum. Anında görünür, düzenlenemez, silinebilir. Bir kullanıcı diğerini yalnızca bir kez değerlendirir. |
| Yetenek kataloğu | Kullanıcı yeni yetenek önerebilir; admin onayı gerekir. |
| E-posta doğrulaması | Başkalarıyla her türlü etkileşim için zorunlu. |
| Mesaj içeriği | MVP'de yalnızca metin. |
| İstenen yetenekte seviye | Aranan öğreticinin seviyesi. |

## Verilen kararlar (2026-10-07)

| Konu | Karar |
|---|---|
| Admin işleri | `User.Role` (`User`/`Admin`) ve arayüzsüz admin endpoint'leri. Panel sonradan aynı endpoint'leri kullanır. |
| Moderation modülü | Dokuzuncu modül olarak eklendi; `Block` ve `Report` bu modüldedir. |
| Hesap silme | Olacak; 30 gün içinde geri alınabilir. Aynı e-postayla yeniden kayıt için 3 ay bekleme süresi var. Ayrıntılar: Ö3. |
| Yaş | MVP'de kayıtta kullanıcı 18 yaş ve üstü olduğunu onaylar. Reşit olmayan kullanıcılar MVP sonrasına bırakıldı. |
| E-posta gönderimi | Lokalde yakalayıcı SMTP sunucusu; gerçek sağlayıcı Faz 3'te ADR ile. |
| Sonlandırma isteği cevapsız kalırsa | 7 gün sonra eşleşme kendiliğinden sonlanır. Ayrıntılar: Ö1. |
| Tamamlandı işareti cevapsız kalırsa | 72 saat sonra oturum kendiliğinden tamamlanır. Ayrıntılar: Ö2. |
| Aynı yetenek iki tipte | Olabilir; istenen seviye sunulan seviyeden yüksek olmalıdır. |
| Eşleşme sonrası sohbet | Açık kalır; devamı kullanıcılara bırakılır. Her çift arasında tek sohbet olur. Ayrıntılar: Ö4. |
| Silinen değerlendirme | Yerine yenisi yazılamaz. |
| Engelleme | Aktif eşleşmeyi tek taraflı sonlandırır, bekleyen istekleri iptal eder. |
| Oturum planlama | Bir taraf önerir, diğeri kabul veya reddeder. |
| Takas biçimi | Çevrimiçi ve yüz yüze; konum olarak yalnızca şehir. |
| Şehir | 81 il listesinden seçilir. |
| Önerilen yetenek | Onaylanmadan öneren kişinin profilinde de kullanılamaz. |
| Değerlendirme yorumu | İsteğe bağlı. |
| Eşleşme sona erince oturumlar | Tamamlanmamış oturumlar iptal edilir. |
| Kullanılan yeteneği kaldırma | Aktif eşleşmede veya bekleyen istekte kullanılan sunulan yetenek kaldırılamaz. |
| ADR 0003 | Token süreleri ve PBKDF2 `PasswordHasher` onaylandı. |

**Brief ile çelişen karar:** Brief Bölüm 6'da değerlendirme "her oturum için kişi başı bir kez" idi. Yeni karar "bir kullanıcıyı yalnızca bir kez". Brief buna göre güncellendi.

**Brief ile çelişen ikinci karar:** Brief Bölüm 6'da "mesajlaşma yalnızca eşleşmiş kullanıcılar arasında mümkündür" kuralı vardı. Yeni karara göre sohbet eşleşme ile başlar ama eşleşme sona erdikten sonra da sürebilir. Brief buna göre güncellendi.

## Onaylanan ayrıntılar (2026-10-07)

### Ö1 — Eşleşmenin 7 gün sonunda kendiliğinden sonlanması

1. Sonlandırma isteği gelince karşı tarafa bildirim gider; bildirimde son tarih yazar.
2. Karşı taraf 7 gün içinde onaylayabilir (eşleşme hemen sonlanır) veya reddedebilir (eşleşme `Active` durumuna döner). İsteyen taraf bu sürede isteğini geri alabilir.
3. Sürenin bitmesine 24 saat kala karşı tarafa bir hatırlatma bildirimi gider.
4. 7 gün dolduğunda eşleşme `EndReason = EndRequestExpired` ile sonlanır ve iki tarafa bildirim gider.
5. Reddedilen bir isteğin ardından yeni sonlandırma isteği için bekleme süresi yoktur. Karşı taraf sürekli reddederse çıkış yolu engellemedir.
6. Bekleyen sonlandırma isteği varken yeni oturum planlanamaz; mevcut oturumlar etkilenmez.

### Ö2 — Oturumun 72 saat sonunda kendiliğinden tamamlanması

1. Bir taraf tamamlandı işaretleyince karşı tarafa bildirim gider; bildirimde son tarih yazar.
2. Karşı taraf 72 saat içinde onaylayabilir veya itirazı varsa oturumu sebep yazarak iptal edebilir. İptal sebebi her iptalde olduğu gibi işaretleyen tarafa ve admine gider.
3. Sürenin bitmesine 24 saat kala karşı tarafa bir hatırlatma bildirimi gider.
4. 72 saat dolduğunda oturum `Completed` olur ve `CompletedAutomatically = true` kaydedilir. Böylece admin, kendiliğinden tamamlanan oturumları ayırt edebilir.
5. Kendiliğinden tamamlanan oturum da değerlendirme hakkı doğurur.
6. Karşı taraf kabul etmeden başlangıç zamanı geçen `Proposed` oturum kendiliğinden `Declined` olur.

Ö1 ve Ö2 için ortak teknik öneri: Süre sonu, isteğin kendi zaman damgasından hesaplanır ve her okuma ile komutta kontrol edilir; yani doğruluk arka plan işinin zamanlamasına bağlı değildir. Bildirimleri ve durum geçişini tetiklemek için uygulama içinde periyodik çalışan tek bir arka plan servisi kullanılır. Takas isteğinin 3 günlük süre aşımı da aynı servisle işlenir. Ayrı bir zamanlayıcı kütüphanesi eklenmez.

### Ö3 — Hesap silme

1. Kullanıcı silmeyi uygulama içinden başlatır ve parolasını yeniden girerek onaylar.
2. Silme isteği 30 gün içinde geri alınabilir. Bu sürede hesap `PendingDeletion` durumundadır ve gizlidir; kullanıcı giriş yaparsa silme iptal olur. Madde 4'teki ilişkiler istek anında kapanır ve geri alınınca geri gelmez. Madde 3, 5 ve 6 ise 30 gün dolunca uygulanır.
3. Silinen veriler: profil (ad, biyografi, avatar dosyası, şehir), kullanıcının yetenekleri, cihaz kayıtları, bildirimler, tüm token'lar, kullanıcının aldığı değerlendirmeler ve puan özeti.
4. Kapanan ilişkiler: bekleyen istekler iptal edilir, aktif eşleşme `EndReason = AccountDeleted` ile sonlanır, tamamlanmamış oturumlar iptal edilir, sohbetler kapatılır.
5. Anonimleştirilerek kalan veriler: kullanıcının gönderdiği mesajlar ve yazdığı değerlendirmeler karşı tarafta "Silinmiş kullanıcı" adıyla görünmeye devam eder. Geçmiş istek, eşleşme ve oturum kayıtları da kimlik referansıyla kalır.
6. `User` satırı `Status = Deleted` olarak kalır; e-posta ve parola hash'i silinir. Aynı e-postayla yeniden kayıt, kalıcı silmeden sonraki 3 ay boyunca engellenir; bunun için e-postanın yalnızca hash'i saklanır ve 3 ay sonunda o da silinir.
7. Kullanıcı hakkındaki açık şikâyetler sonuçlanana kadar saklanır.

Not: Hangi verinin ne kadar süre saklanabileceği KVKK açısından hukuki bir konudur. Bu öneri teknik bir varsayılandır; yayından önce bir hukukçuyla doğrulanmalıdır.

### Ö4 — Eşleşme sonrası sohbet

1. Sohbet eşleşmeye değil kullanıcı çiftine bağlanır: iki kullanıcı arasında tek bir sohbet olur ve ilk eşleşmede açılır.
2. Eşleşme sona erince sohbet açık kalır ve iki taraf yazmaya devam edebilir.
3. Aktif eşleşme yokken taraflardan biri sohbeti tek başına kapatabilir. Kapatılan sohbet salt okunur olur; geçmiş mesajlar iki tarafta da görünür kalır.
4. Engelleme ve hesap silme sohbeti kapatır.
5. Aynı iki kullanıcı yeniden eşleşirse kapalı sohbet yeniden açılır ve geçmiş korunur.

## Sonraki fazlarda netleşecekler

- Keşfet skorunun formülü ve seviye uyumunun (öğretici seviyesi istenenin altındaysa) etkisi: Faz 5.3.
- Modül başına ayrı `DbContext` ve PostgreSQL şeması kullanımı: Faz 3 (bkz. ADR 0001).
- Metin alanlarının uzunluk sınırları: Faz 2 (sözleşme).
- E-posta sağlayıcısı: Faz 3 (ADR).
- Reşit olmayan (13–17 yaş) kullanıcılar: MVP sonrası. Yetişkinlerle eşleşme, özel mesajlaşma ve yüz yüze oturum için koruma kuralları, yaş bilgisinin toplanması ve veli onayı o zaman kararlaştırılacak.

