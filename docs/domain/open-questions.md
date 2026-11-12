# Kararlar ve Açık Sorular

## Verilen kararlar (2026-10-06)

| Konu | Karar |
|---|---|
| Takas modeli | MVP'de tamamen karşılıklı. Zaman/kredi bankası yok. |
| Konum | Yalnızca şehir tutulur. |
| Eşleşme önerisi | MVP'de kural tabanlı sıralama: karşılıklı uyum, puan. (Tercih uyumu basamağı 2026-11-12'de kalktı.) |
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
| Takas biçimi | Yalnızca çevrimiçi (2026-11-12; önce çevrimiçi ve yüz yüzeydi). Konum olarak yalnızca şehir tutulur, isteğe bağlıdır. |
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
| Tamamlanmış profil | Görünen ad ve en az bir sunulan yetenek. Şehir isteğe bağlı (2026-11-12). |
| Hitap | Uygulamanın tamamında ve e-postalarda "siz" hitabı kullanılır. Düğme etiketleri kısa emir kipinde kalır ("Giriş yap", "Kaydet"). Etkileşim uyarısı: "Etkileşim için profilinizi doldurup e-posta doğrulaması yapmanız gerekmektedir." |
| Gönderiler | MVP sonrası. Yapısı aşağıda. |
| İstek notu | İstek gönderirken isteğe bağlı, en fazla 300 karakterlik not yazılabilir. |
| Yeniden istek | Reddedilen veya süresi dolan istekten sonra aynı kişiye 7 gün yeniden istek gönderilemez. |
| İstatistik tanımları | "Tamamlanan eşleşme": en az bir tamamlanmış oturumu olan sona ermiş eşleşme. "İptal edilen eşleşme": hiç oturum tamamlanmadan sona eren eşleşme. Sayılar profile dilim 6'da (oturumlar) eklenir. Tanım ileride değişebilir. |
| Mesajlaşma | Mesaj göndermek için ek koşul yok. Karşı tarafın okuduğu, gönderenin son mesajının altında "Görüldü" ile gösterilir. "Yazıyor…" göstergesi, mesaj silme ve düzenleme MVP'de yok. Alt gezinme çubuğunda Mesajlar simgesinde toplam okunmamış sayısı gösterilir. |
| Oturumlar | Yalnızca çevrimiçi oturum vardır; süre 30/45/60/90/120 dakika, bir eşleşmede aynı anda birden fazla planlı oturum olabilir. İptal sebebini yalnızca iki taraf ve admin görür. |
| Profil istatistikleri | Profilde üçlü kesinlikle yer alır: toplam eşleşme, tamamlanan eşleşme, iptal edilen (tamamlanmamış) eşleşme; ayrıca tamamlanan oturum sayısı. Amaç kaç eşleşmeden kaçında sorun çıkmadığının anlaşılması. Eşleşme tamamen bitmeden "toplam" dahil hiçbir sayaca girmez (2026-10-13). |
| Bildirimler | Mesajlar bildirim listesine girmez, yalnızca telefon bildirimi olur ve gönderenin adıyla mesajın başı görünür. Çevrimiçi oturumdan 24 saat ve 1 saat önce iki tarafa hatırlatma gider. Türlere göre bildirim tercihi ve e-posta bildirimi MVP'de yok. Bildirim izni ilk girişten sonra kısa bir açıklamayla istenir; reddedilirse profilden yeniden açılabilir. Bildirimler 90 gün saklanır. Bildirimler ekranı alt çubukta sekme değil, Keşfet ekranının üst köşesindeki zil simgesidir (2026-10-13). Telefon bildiriminin metnini sunucu üretir, uygulama kapalıyken de görünsün diye (2026-10-14, ADR 0012). |
| Çevrimiçi görüşme | Çevrimiçi oturumlar uygulamaya gömülecek bir görüntülü görüşme modülüyle yapılacak ve yalnızca planlanan oturum saatlerinde görüşülebilecek (2026-10-13). Modül MVP sonrasında, Faz 8 dilim 1'de yapılır (2026-11-03). Altyapı kendi sunucumuzdaki LiveKit (ADR 0015). Görüşmeye planlanan başlangıçtan 10 dakika önce katılınabilir, süre bittikten 15 dakika sonra görüşme kapanır; 5 dakika kala uyarı gösterilir. Ses ve görüntü kaydedilmez, kimin ne zaman katıldığı da tutulmaz. Uygulama arka plana geçince ses sürer, kamera durur (2026-11-03). Eşleşme sona erince tamamlandı işaretli oturumun görüşmesi de kapanır; sonlandırma isteği beklerken görüşme sürer (2026-11-04). |
| Kullanıcı istatistikleri | Kullanıcı başına istatistik tutulacak ve ileride profilde güven puanının yanında gösterilecek: "Tamamlanan Eşleşme", "İptal Edilen Eşleşme" gibi sayılar. Amaç karşı tarafa güvenip güvenmemeye yardımcı olmak; rozet/başarım modülü de bu veriye dayanacak. Dilim 4 (eşleşmeler), 6 (oturumlar) ve 7 (değerlendirmeler) bu sayıların çıkarılabileceği biçimde tasarlanır: her eşleşme ve oturumun nasıl ve kim tarafından bittiği ile zamanı saklanır. |
| Yetenek kataloğu | MVP'de 10 kategori ve 68 yetenek (başlangıç verisi). Tam sürümde çok daha kapsamlı bir kategori yapısı kurulacak. |
| Yetenek sınırı | 5 sunulan, 5 istenen. İleride ödeme planlarına bağlanacak. |
| Admin | Hesap veritabanında elle admin yapılır; öneriler bir PowerShell betiğiyle listelenip onaylanır. Şikâyetler, askıya alma ve oturum iptal sebepleri de betikle yönetilir (`scripts/admin-moderation.ps1`); admin paneli MVP sonrası (2026-10-14). |
| Engelleme | Engellenen kişi engellendiğini görmez; eşleşmesi sebepsiz sona ermiş görünür (2026-10-14). |
| Askıya alma | Giriş kapanır ve hesap görünmez olur; eşleşme ve sohbetler olduğu gibi kalır (2026-10-14). |
| Şikâyet | Sebepler: Spam, Taciz, Uygunsuz içerik, Sahte profil, Diğer (Diğer'de açıklama zorunlu). Şikâyet kapanınca şikâyet edene ayrıntısız bildirim gider (2026-10-14). |
| Görünürlük | Etkileşim koşulunu sağlamayan kullanıcı gezebilir ve başkalarına bakabilir. Kendisi keşfette öneri olarak çıkmaz, ama ad veya kullanıcı adıyla aranınca bulunur ve profili açılabilir. Koşul yalnızca başkalarını etkileyen işlemlerde aranır. |
| Keşfet sıralaması | Karşılıklı uyum, tek yönlü uyum, puan, yenilik (tercih uyumu 2026-11-12'de kalktı). Seviyesi yetmeyen yetenek uyum sayılmaz. Uyumsuz kullanıcılar listenin sonunda gösterilir. |

## Verilen kararlar (2026-10-14 ve sonrası)

Ayrıntıları ve gerekçeleri `docs/roadmap.md` ve ilgili ADR'lerdedir; burada yalnızca karar kaydı tutulur.

| Konu | Karar |
|---|---|
| Renk paleti | "İndigo ve turkuaz"; tema telefonun ayarını izler, Sistem / Açık / Koyu seçilebilir (2026-10-14). |
| Canlı ortam | Oracle Cloud Always Free (Frankfurt), DNS ve nesne depolama Cloudflare (R2), e-posta Brevo, kişisel Play Console hesabı (2026-10-14, ADR 0014). |
| Hata izleme | Sentry, AB veri bölgesi; kullanıcıdan yalnızca kimlik gider (2026-10-14, ADR 0013). |
| Hukuki uyum | Hukukçu görüşü yerine araştırma raporu; kayıtta kullanım koşulları kabul edilir, bildirim metinleri kimseyi adlandırmaz, trafik kayıtları 1 yıl tutulur, kapanan şikâyetler 2 yıl sonra silinir (2026-10-27, `docs/legal/compliance-research.md`). |
| MVP sonrası sıra | Kapalı test sürerken MVP sonrası modüller Faz 8'de dilim dilim yapılır; web istemcisi (Faz 7) bundan sonra ele alınır (2026-11-03). |
| Görüntülü görüşme ayrıntıları | Ekran paylaşımı var: iki taraf da paylaşabilir, paylaşırken kamera açık kalır, cihazın sesi gitmez (2026-11-03, ADR 0015). |
| Oturum çakışması | Bir kullanıcı aynı anda iki oturumda olamaz, aynı veya farklı eşleşmelerde. Kabul edilmiş oturumlar ve kişinin kendi açık teklifleri zamanı tutar (2026-11-03, SS-20). |
| Kullanıcı adında büyük harf | Sunucu kuralı aynı kalır (yalnızca küçük harf); uygulama yazılan büyük harfleri yazarken küçük harfe çevirir (2026-11-03). |
| Faz 8 planı | Sıra: görüntülü görüşme, görüşmede uçtan uca şifreleme, güven göstergesi ve rozetler, mesajlarda dosya, kapsamlı katalog, gelişmiş öneri, yönetici paneli ve moderatörler, gönderi akışı, Google ile giriş, çoklu dil (2026-11-04, `docs/roadmap.md`). |
| Uçtan uca şifreleme | Yalnızca görüntülü görüşmede; mesajlarda yok (2026-11-04). |
| Yönetici paneli | Mobil uygulamanın içinde, yetkili rollere görünen ekranlar. Admin panelden moderatör rolü verir ve geri alır; moderatörün yetkileri daha dardır ve her işlemi denetim için kaydedilir (2026-11-04). |
| Mesajlarda dosya | Görsel ile not ve kaynak paylaşımı için PDF, Word gibi belgeler gönderilebilecek (2026-11-04); ayrıntılar Faz 8 dilim 4'ün başında. |
| Üyelik ve ödeme | Genel yayından ve uygulama kitle kazandıktan sonra; o zamana kadar Exparter ücretsiz (2026-11-04). |
| Yüz yüze buluşma | Uygulamada yüz yüze buluşmayla ilgili hiçbir şey yoktur: bildirim özelliği, profildeki yüz yüze takas tercihi ve keşfetteki süzgeç kaldırıldı; kayıtlı bildirimler silindi (2026-11-12, Faz 8 dilim 11). Kullanım koşullarındaki "Exparter yüz yüze buluşma düzenlemez" bölümü yerinde durur. |
| Vibe etiketleri | Kullanıcı 24 etiketlik sabit listeden en fazla 3 etiketi profiline ekler; isteğe bağlıdır ve etkileşim koşuluna girmez. Keşfette yetenek uyumu eşitse ortak etiket sayısı fazla olan öne gelir; ortak etiketler kartta ve profilde görünür (2026-11-12, Faz 8 dilim 12). |
| Bilgilerin doğruluğu | Kullanım koşullarında kullanıcılar, diğer kullanıcıların bilgilerinin gerçeği yansıtmayabileceğini ve puanların doğrulama olmadığını kabul eder (2026-11-04, `docs/legal/compliance-research.md`). |
| Eşleşme sınırı | Aynı anda en fazla 3 sona ermemiş eşleşme (2026-11-04, MT-10). |
| Eşleşmeler ekranı | Süren ve biten eşleşmeler ayrı gösterilir (2026-11-04). |
| Test hesapları | Kapalı test bitince bütün test hesapları silinir; genel yayında herkes sıfırdan başlar (2026-11-04). |

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

Not: Hangi verinin ne kadar süre saklanabileceği KVKK açısından hukuki bir konudur. Hukukçu görüşü alınmadı; süreler 2026-10-27'de bir araştırmayla netleştirildi ve kullanıcı tarafından onaylandı (`docs/legal/compliance-research.md`, 7. bölüm).

### Ö4 — Eşleşme sonrası sohbet

1. Sohbet eşleşmeye değil kullanıcı çiftine bağlanır: iki kullanıcı arasında tek bir sohbet olur ve ilk eşleşmede açılır.
2. Eşleşme sona erince sohbet açık kalır ve iki taraf yazmaya devam edebilir.
3. Aktif eşleşme yokken taraflardan biri sohbeti tek başına kapatabilir. Kapatılan sohbet salt okunur olur; geçmiş mesajlar iki tarafta da görünür kalır.
4. Engelleme ve hesap silme sohbeti kapatır.
5. Aynı iki kullanıcı yeniden eşleşirse kapalı sohbet yeniden açılır ve geçmiş korunur.

## Sonraki fazlarda netleşecekler

Çözülenler: keşfet sıralaması ve seviye uyumu (dilim 3 ve 7, ADR 0011); tek `DbContext` ve modül başına şema (ADR 0006); metin alanlarının sınırları (sözleşme); e-posta sağlayıcısı (Brevo, ADR 0007); kullanıcı istatistiklerinin profilde gösterimi (dilim 6); renk paleti (2026-10-14).

Hâlâ açık olanlar:

- Gönderi akışı: MVP sonrası, takas akışı (Faz 5 dilim 2–8) bittikten sonra ayrı bir dilim olarak. Kullanıcı sunduğu yeteneklerden birini seçer ve o yetenekle ilgili paylaşım yapar. Paylaşım metin, görsel veya video olabilir; metin + görsel ve metin + video birlikte de olabilir. Akış, Keşfet'ten bağımsız ayrı bir ekrandır. Beğeni ve yorum etkileşim sayılır; tamamlanmış profil ve doğrulanmış e-posta gerektirir. Ayrıntılar (video sınırları, moderasyon, sıralama) o dilimin başında netleştirilecek.
- Güven puanı ve rozet/başarım modülü: MVP sonrası (istatistikler profilde dilim 6'dan beri görünüyor).
- Kapsamlı yetenek kataloğu (alt kategoriler, çok daha fazla yetenek) ve yetenek sınırının ödeme planlarına bağlanması: MVP sonrası.
- Reşit olmayan (13–17 yaş) kullanıcılar: MVP sonrası. Yetişkinlerle eşleşme, özel mesajlaşma ve yüz yüze oturum için koruma kuralları, yaş bilgisinin toplanması ve veli onayı o zaman kararlaştırılacak.

