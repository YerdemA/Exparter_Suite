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
| Değerlendirme | 1–10 puan ve yorum. Anında görünür, düzenlenemez, silinebilir. Bir kullanıcı diğerini yalnızca bir kez değerlendirir. Eşleşme sürerken de, ilk oturum tamamlanır tamamlanmaz değerlendirilebilir; yorum en fazla 500 karakter; ortalama ilk değerlendirmeden itibaren sayısıyla birlikte görünür; değerlendirilen kişi cevap yazamaz (2026-10-13). |
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
| Kullanıcı adı | Kayıtta benzersiz kullanıcı adı alınır; giriş e-posta veya kullanıcı adıyla yapılabilir (referans: Instagram). Yalnızca küçük harf kullanılır; kullanıcı adı haftada bir kez değiştirilebilir. |
| Admin kapsamı | MVP'de katalog yönetimi ve kullanıcı askıya alma endpoint'i yok; katalog başlangıç verisi migration ile gelir. |
| E-posta bekleme süresi | 3 ay, kalıcı silmeden itibaren sayılır. |

**Brief ile çelişen karar:** Brief Bölüm 6'da değerlendirme "her oturum için kişi başı bir kez" idi. Yeni karar "bir kullanıcıyı yalnızca bir kez". Brief buna göre güncellendi.

**Brief ile çelişen ikinci karar:** Brief Bölüm 6'da "mesajlaşma yalnızca eşleşmiş kullanıcılar arasında mümkündür" kuralı vardı. Yeni karara göre sohbet eşleşme ile başlar ama eşleşme sona erdikten sonra da sürebilir. Brief buna göre güncellendi.

## Verilen kararlar (2026-10-09)

| Konu | Karar |
|---|---|
| E-posta bağlantıları | MVP'de `exparter://app/...` özel şeması; alan adı olduğunda `https://` (ADR 0009). |
| Nesne depolama | Kod S3 API'sine yazılır; lokalde Garage; canlı ortam Faz 6'da seçilir (ADR 0010). |
| Avatar | Galeriden veya kameradan; kırpma ekranı yok. |
| Gezinme | Alt gezinme çubuğu: Keşfet, Eşleşmeler, Mesajlar, Profil. |
| Etkileşim koşulu | Profili tamamlanmamış veya e-postası doğrulanmamış kullanıcı uygulamayı gezebilir, ama etkileşime giremez, istek gönderemez ve keşfette gösterilmez. Zorunlu profil ekranı yoktur; koşul etkileşim anında uyarıyla devreye girer. |
| Tamamlanmış profil | Görünen ad, en az bir takas tercihi (yüz yüze ise şehir) ve en az bir sunulan yetenek. |
| Hitap | Uygulamanın tamamında ve e-postalarda "siz" hitabı kullanılır. Düğme etiketleri kısa emir kipinde kalır ("Giriş yap", "Kaydet"). Etkileşim uyarısı: "Etkileşim için profilinizi doldurup e-posta doğrulaması yapmanız gerekmektedir." |
| Gönderiler | MVP sonrası. Yapısı aşağıda. |
| İstek notu | İstek gönderirken isteğe bağlı, en fazla 300 karakterlik not yazılabilir. |
| Yeniden istek | Reddedilen veya süresi dolan istekten sonra aynı kişiye 7 gün yeniden istek gönderilemez. |
| İstatistik tanımları | "Tamamlanan eşleşme": en az bir tamamlanmış oturumu olan sona ermiş eşleşme. "İptal edilen eşleşme": hiç oturum tamamlanmadan sona eren eşleşme. Sayılar profile dilim 6'da (oturumlar) eklenir. Tanım ileride değişebilir. |
| Mesajlaşma | Mesaj göndermek için ek koşul yok. Karşı tarafın okuduğu, gönderenin son mesajının altında "Görüldü" ile gösterilir. "Yazıyor…" göstergesi, mesaj silme ve düzenleme MVP'de yok. Alt gezinme çubuğunda Mesajlar simgesinde toplam okunmamış sayısı gösterilir. |
| Oturumlar | Yalnızca çevrimiçi oturum planlanır; süre 30/45/60/90/120 dakika, bir eşleşmede aynı anda birden fazla planlı oturum olabilir. Yüz yüze buluşmalar tamamen kullanıcılara bırakılır, sohbette ayarlanır; yer ve tarih tutulmaz. Buluşmadan sonra taraflar "buluştuk" diye işaretleyip onaylar, böylece yüz yüze takaslar da tamamlanmış sayılır ve değerlendirilebilir. İptal sebebini yalnızca iki taraf (ileride admin) görür. |
| Profil istatistikleri | Profilde üçlü kesinlikle yer alır: toplam eşleşme, tamamlanan eşleşme, iptal edilen (tamamlanmamış) eşleşme; ayrıca tamamlanan oturum sayısı. Amaç kaç eşleşmeden kaçında sorun çıkmadığının anlaşılması. Eşleşme tamamen bitmeden "toplam" dahil hiçbir sayaca girmez (2026-10-13). |
| Bildirimler | Mesajlar bildirim listesine girmez, yalnızca telefon bildirimi olur ve gönderenin adıyla mesajın başı görünür. Çevrimiçi oturumdan 24 saat ve 1 saat önce iki tarafa hatırlatma gider. Türlere göre bildirim tercihi ve e-posta bildirimi MVP'de yok. Bildirim izni ilk girişten sonra kısa bir açıklamayla istenir; reddedilirse profilden yeniden açılabilir. Bildirimler 90 gün saklanır. Bildirimler ekranı alt çubukta sekme değil, Keşfet ekranının üst köşesindeki zil simgesidir (2026-10-13). Telefon bildiriminin metnini sunucu üretir, uygulama kapalıyken de görünsün diye (2026-10-14, ADR 0012). |
| Yüz yüze bildirim sınırı | Sahteciliğe karşı her kullanıcı çifti yalnızca bir kez yüz yüze buluşma bildirebilir (2026-10-13). |
| Çevrimiçi görüşme | Çevrimiçi oturumlar uygulamaya gömülecek bir görüntülü görüşme modülüyle yapılacak ve yalnızca planlanan oturum saatlerinde görüşülebilecek (2026-10-13). Brief'te görüntülü görüşme MVP sonrası olarak duruyor; modülün zamanlaması ayrıca kararlaştırılacak. |
| Kullanıcı istatistikleri | Kullanıcı başına istatistik tutulacak ve ileride profilde güven puanının yanında gösterilecek: "Tamamlanan Eşleşme", "İptal Edilen Eşleşme" gibi sayılar. Amaç karşı tarafa güvenip güvenmemeye yardımcı olmak; rozet/başarım modülü de bu veriye dayanacak. Dilim 4 (eşleşmeler), 6 (oturumlar) ve 7 (değerlendirmeler) bu sayıların çıkarılabileceği biçimde tasarlanır: her eşleşme ve oturumun nasıl ve kim tarafından bittiği ile zamanı saklanır. |
| Yetenek kataloğu | MVP'de 10 kategori ve 68 yetenek (başlangıç verisi). Tam sürümde çok daha kapsamlı bir kategori yapısı kurulacak. |
| Yetenek sınırı | 5 sunulan, 5 istenen. İleride ödeme planlarına bağlanacak. |
| Admin | Hesap veritabanında elle admin yapılır; öneriler bir PowerShell betiğiyle listelenip onaylanır. |
| Görünürlük | Etkileşim koşulunu sağlamayan kullanıcı gezebilir ve başkalarına bakabilir. Kendisi keşfette öneri olarak çıkmaz, ama ad veya kullanıcı adıyla aranınca bulunur ve profili açılabilir. Koşul yalnızca başkalarını etkileyen işlemlerde aranır. |
| Keşfet sıralaması | Karşılıklı uyum, tek yönlü uyum, tercih uyumu, puan, yenilik. Seviyesi yetmeyen yetenek uyum sayılmaz. Uyumsuz kullanıcılar listenin sonunda gösterilir. |

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
- Gönderi akışı: MVP sonrası, takas akışı (Faz 5 dilim 2–8) bittikten sonra ayrı bir dilim olarak. Kullanıcı sunduğu yeteneklerden birini seçer ve o yetenekle ilgili paylaşım yapar. Paylaşım metin, görsel veya video olabilir; metin + görsel ve metin + video birlikte de olabilir. Akış, Keşfet'ten bağımsız ayrı bir ekrandır. Beğeni ve yorum etkileşim sayılır; tamamlanmış profil ve doğrulanmış e-posta gerektirir. Ayrıntılar (video sınırları, moderasyon, sıralama) o dilimin başında netleştirilecek.
- Kullanıcı istatistiklerinin profilde gösterimi, güven puanı ve rozet/başarım modülü: hangi sayıların hangi dilimde görünür olacağı dilim 4'ün planında önerilecek; rozetler MVP sonrası.
- Görüntülü görüşme modülü: zamanlaması (MVP içinde mi sonrasında mı), kullanılacak altyapı ve "planlanan saat" penceresinin tanımı (kaç dakika önce açılır, süre bitince ne olur) modül planlanırken kararlaştırılacak.
- Renk paleti: kullanıcı mevcut pembe ağırlıklı paleti beğenmedi (2026-10-13); palet büyük olasılıkla değişecek. Yeni palet kararlaştırılınca yalnızca `mobile/lib/core/theme/` altındaki token'lar değişir; ekranlar renkleri oradan aldığı için başka dosyaya dokunmak gerekmez.
- Kapsamlı yetenek kataloğu (alt kategoriler, çok daha fazla yetenek) ve yetenek sınırının ödeme planlarına bağlanması: MVP sonrası.
- Reşit olmayan (13–17 yaş) kullanıcılar: MVP sonrası. Yetişkinlerle eşleşme, özel mesajlaşma ve yüz yüze oturum için koruma kuralları, yaş bilgisinin toplanması ve veli onayı o zaman kararlaştırılacak.

