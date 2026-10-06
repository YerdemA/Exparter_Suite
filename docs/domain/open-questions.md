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

**Brief ile çelişen karar:** Brief Bölüm 6'da değerlendirme "her oturum için kişi başı bir kez" idi. Yeni karar "bir kullanıcıyı yalnızca bir kez". Brief buna göre güncellendi.

## Açık sorular

Dokümanlarda `(V#)` ile işaretli her varsayım aşağıdaki bir soruya karşılık gelir. Cevap değişirse ilgili yerler güncellenir.

### Karar gerektirenler

| No | Soru | Dokümandaki varsayım / öneri |
|---|---|---|
| V1 | Admin paneli MVP dışında, ama yetenek onayı, şikâyetler ve iptal sebepleri bir admin gerektiriyor. MVP'de bu işler nasıl yapılacak? | `User.Role` alanı (`User`/`Admin`) ve arayüzsüz admin endpoint'leri. Panel sonradan aynı endpoint'leri kullanır. |
| V2 | `Block` ve `Report` brief'teki sekiz modülden hiçbirine tam oturmuyor. Dokuzuncu modül olarak `Moderation` eklensin mi? | Evet. Modül listesi değiştirilemez kararlar arasında olduğu için onay ve ADR güncellemesi gerekir. |
| V3 | Hesap silme (KVKK) ve yaş sınırı olacak mı? (Önceki turda cevapsız kaldı.) | Modele alınmadı. |
| V4 | E-posta doğrulaması ve parola sıfırlama e-posta gönderimi gerektiriyor; teknoloji yığınında e-posta sağlayıcısı yok. | Lokalde yakalayıcı bir SMTP sunucusu; gerçek sağlayıcı Faz 3'te ADR ile seçilir. |

### Varsayımı onaylaman yeterli olanlar

| No | Soru | Varsayım |
|---|---|---|
| V5 | Değerlendirmesini silen kullanıcı aynı kişiyi yeniden değerlendirebilir mi? | Hayır. Aksi halde "düzenlenemez" kuralı sil-yeniden yaz ile aşılır. |
| V6 | Eşleşmeyi sonlandırma isteğine karşı taraf hiç cevap vermezse? | Süre sınırı yok, istek bekler. Alternatif: 7 gün sonra otomatik sonlanma. |
| V7 | Engelleme aktif eşleşmeyi tek taraflı sonlandırır ve bekleyen istekleri iptal eder mi? | Evet. Aksi halde engellenen kişi mesaj atmaya devam edebilir. |
| V8 | Oturum planlaması karşı tarafın kabulünü gerektirir mi? | Evet: bir taraf önerir, diğeri kabul veya reddeder. |
| V9 | Tamamlandı işaretini karşı taraf hiç onaylamazsa? | Süre sınırı yok, oturum `CompletionPending` kalır. Alternatif: 72 saat sonra otomatik onay. |
| V10 | "Yalnızca şehir" cevabını şöyle yorumladım: hem çevrimiçi hem yüz yüze takas var, konum olarak yalnızca şehir tutuluyor. Doğru mu? | Evet; profilde iki tercih, oturumda `Online`/`InPerson` biçimi. |
| V11 | Şehir sabit listeden mi seçilir, serbest metin mi? | 81 ilden seçim. Filtreleme ve eşleştirme serbest metinle güvenilir olmaz. |
| V12 | Kullanıcı önerdiği yeteneği onaylanmadan kendi profilinde kullanabilir mi? | Hayır, onayı bekler. |
| V13 | Aynı yetenek aynı kullanıcıda hem sunulan hem istenen olabilir mi? | Hayır. |
| V14 | Değerlendirmede yorum zorunlu mu? | İsteğe bağlı. |
| V15 | Eşleşme sona erince sohbet ve planlı oturumlara ne olur? | Sohbet salt okunur kalır, tamamlanmamış oturumlar iptal edilir. |
| V16 | Kullanıcı, aktif bir eşleşmede veya bekleyen bir istekte kullanılan sunulan yeteneğini profilinden kaldırabilir mi? | Hayır; önce eşleşme sonlanmalı veya istek kapanmalı. |

### Sonraki fazlarda netleşecekler

- Keşfet skorunun formülü ve seviye uyumunun (öğretici seviyesi istenenin altındaysa) etkisi: Faz 5.3.
- Modül başına ayrı `DbContext` ve PostgreSQL şeması kullanımı: Faz 3 (bkz. ADR 0001).
- Metin alanlarının uzunluk sınırları: Faz 2 (sözleşme).
