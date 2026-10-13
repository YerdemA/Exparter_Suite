# İş Kuralları

Durum: **Onaylandı (2026-10-07).**

**Katman** sütunu kuralın nerede korunduğunu söyler:

- **Domain:** Entity kendi içinde korur; ihlalde domain hatası fırlatır.
- **Application:** Handler, başka modülden veri sorarak korur.
- **DB:** Eşzamanlı isteklere karşı son savunma olarak veritabanı kısıtı da vardır.

## Kimlik ve hesap

| No | Kural | Katman |
|---|---|---|
| ID-01 | E-posta benzersizdir; büyük/küçük harf ayrımı yapılmaz. | Application, DB |
| ID-02 | Kayıt e-posta, kullanıcı adı ve parola ile yapılır. Harici giriş (Google/Apple) MVP'de yoktur. | — |
| ID-15 | Kullanıcı adı benzersizdir. 3–30 karakterdir; yalnızca küçük harf (a–z), rakam, alt çizgi ve nokta içerebilir; noktayla başlayamaz, noktayla bitemez ve art arda iki nokta içeremez. Büyük harf içeren ad reddedilir, küçük harfe çevrilmez. | Domain, DB |
| ID-16 | Ayrılmış adlar (ör. `admin`, `exparter`, `destek`) kullanıcı adı olarak alınamaz. | Domain |
| ID-17 | Giriş, parola ile birlikte e-posta veya kullanıcı adı verilerek yapılır. Girişte büyük/küçük harf ayrımı yapılmaz. | Application |
| ID-18 | Kullanıcı, kullanıcı adını 7 günde bir kez değiştirebilir; kayıtta seçilen ad bu süreyi başlatmaz. Yeni ad ID-15 ve ID-16 kurallarına uymalıdır; bırakılan ad hemen başkası tarafından alınabilir. | Domain |
| ID-03 | E-postası doğrulanmamış kullanıcı giriş yapabilir, profilini ve yeteneklerini düzenleyebilir, keşfet ekranını görebilir. | Application |
| ID-04 | E-postası doğrulanmamış kullanıcı başkalarıyla etkileşime giremez: takas isteği gönderemez veya kabul edemez, mesaj atamaz, oturum planlayamaz, değerlendirme yapamaz, yetenek öneremez, şikâyet edemez. | Application |
| ID-05 | Refresh token her kullanımda yenilenir. İptal edilmiş bir token yeniden kullanılırsa o token ailesinin tamamı iptal edilir. | Domain |
| ID-06 | Doğrulama ve parola sıfırlama token'ları tek kullanımlıktır ve süreleri dolunca geçersizdir. | Domain |
| ID-07 | Parola sıfırlandığında kullanıcının tüm refresh token'ları iptal edilir. | Domain |
| ID-08 | `Suspended` durumundaki kullanıcı giriş yapamaz ve token yenileyemez. | Domain |
| ID-09 | Kayıt olurken kullanıcı 18 yaşında veya daha büyük olduğunu onaylamalıdır. Onay olmadan hesap oluşturulmaz; onay zamanı saklanır, doğum tarihi istenmez. | Domain |
| ID-10 | Kullanıcı parolasını yeniden girerek hesabını silebilir. İstekle birlikte hesap gizlenir, oturumları kapanır, bekleyen istekleri iptal edilir, aktif eşleşmesi sonlanır, tamamlanmamış oturumları iptal edilir ve sohbetleri kapanır. | Application |
| ID-12 | Silme isteği 30 gün içinde geri alınabilir: kullanıcı bu sürede giriş yaparsa hesap yeniden etkinleşir. Kapanan istek, eşleşme, oturum ve sohbetler geri gelmez. | Domain |
| ID-13 | 30 gün dolunca silme kalıcı olur: profil, avatar, yetenekler, cihazlar, bildirimler ve kullanıcının aldığı değerlendirmeler silinir. Gönderdiği mesajlar ve yazdığı değerlendirmeler "Silinmiş kullanıcı" adıyla kalır. | Application |
| ID-14 | Kalıcı olarak silinen hesabın e-postasıyla, kalıcı silmeden sonraki 3 ay boyunca yeni hesap açılamaz. Bunun için e-postanın kendisi değil hash'i saklanır. | Application |
| ID-11 | Admin yetkisi `User.Role` ile belirlenir. Yetenek önerilerini onaylama, şikâyetleri sonuçlandırma ve iptal sebeplerini listeleme yalnızca adminlere açıktır. | Application |

## Profil

| No | Kural | Katman |
|---|---|---|
| PR-01 | Her kullanıcının tam olarak bir profili vardır. | DB |
| PR-02 | Konum olarak yalnızca şehir tutulur; koordinat veya adres tutulmaz. | — |
| PR-03 | Çevrimiçi ve yüz yüze tercihlerinden en az biri seçili olmalıdır. | Domain |
| PR-04 | Yüz yüze tercihi seçiliyse şehir zorunludur. | Domain |
| PR-05 | Profil, eşleşme için gereken bilgiler girildiğinde tamamlanmış sayılır: görünen ad, en az bir takas tercihi (yüz yüze ise şehirle birlikte) ve en az bir sunulan yetenek. (Yetenek koşulu, yeteneklerin geldiği dilim 2'de uygulanır.) | Application |
| PR-06 | Etkileşim koşullarını sağlamayan kullanıcı keşfette öneri olarak gösterilmez. Ama gizli değildir: kullanıcı adı veya görünen adla aranınca bulunur ve profili açılabilir. Askıya alınmış ve silinmekte olan hesaplar hiçbir yerde görünmez. | Application |
| PR-07 | Profili tamamlanmamış veya e-postası doğrulanmamış kullanıcı uygulamayı gezebilir (keşfet dahil), başka kullanıcıların profillerine ve yeteneklerine bakabilir, ama başkalarıyla etkileşime giremez (bkz. ID-04). Koşul yalnızca başka kullanıcıları etkileyen işlemlerde aranır: istek, eşleşme, mesaj, oturum, değerlendirme ve ileride beğeni ile yorum. Etkileşim denemesinde "Etkileşim için profilinizi doldurup e-posta doğrulaması yapmanız gerekmektedir." uyarısı gösterilir. | Application |
| PR-08 | Avatar JPEG, PNG veya WebP olabilir ve en fazla 5 MB'tır. Dosya doğrudan depolamaya yüklenir; sınırlar yüklenen dosya üzerinde denetlenir. | Application |

## Yetenekler

| No | Kural | Katman |
|---|---|---|
| SK-01 | Kullanıcı profiline yalnızca katalogdaki onaylı yetenekleri ekleyebilir. | Application |
| SK-02 | Kullanıcı katalogda olmayan bir yeteneği önerebilir. Öneri admin onaylayana kadar katalogda görünmez. | Domain |
| SK-03 | Aynı ada (slug) sahip ikinci bir yetenek oluşturulamaz veya önerilemez. | Application, DB |
| SK-04 | Bir kullanıcı aynı yeteneği her tipte (sunulan/istenen) en fazla bir kez ekleyebilir. | DB |
| SK-08 | Bir kullanıcı en fazla 5 sunulan ve 5 istenen yetenek ekleyebilir. Sınır ileride ödeme planlarına bağlanacak; şimdilik herkes için aynıdır. | Domain |
| SK-09 | Yetenek önermek için doğrulanmış e-posta yeterlidir; diğer etkileşim koşulları aranmaz (kataloğunda yeteneği olmayan kullanıcı önce öneri yapabilmelidir). | Application |
| SK-07 | Aynı yetenek hem sunulan hem istenen olabilir; bu durumda istenen seviye sunulan seviyeden yüksek olmalıdır. Seviyelerden biri değiştirildiğinde de kural korunur. | Application |
| SK-05 | Sunulan yetenekte seviye kullanıcının kendi seviyesidir. İstenen yetenekte seviye, aranan öğreticinin en düşük seviyesidir. | — |
| SK-06 | Sona ermemiş bir eşleşmede veya bekleyen bir istekte kullanılan sunulan yetenek profilden kaldırılamaz. | Application |

## Takas isteği

| No | Kural | Katman |
|---|---|---|
| RQ-01 | Kullanıcı kendine istek gönderemez. | Domain |
| RQ-02 | İstek bir yetenek çifti taşır: gönderenin öğreteceği ve öğrenmek istediği yetenek. | Domain |
| RQ-03 | Öğretilecek yetenek gönderenin sunduğu, öğrenilecek yetenek alıcının sunduğu yetenekler arasında olmalıdır. | Application |
| RQ-04 | Karşılıklı uyum şart değildir: gönderenin sunduğu yetenek alıcının istedikleri arasında olmasa da istek gönderilebilir. | — |
| RQ-05 | İki kullanıcı arasında, yönden bağımsız olarak, aynı anda yalnızca bir `Pending` istek olabilir. | Application, DB |
| RQ-06 | Aralarında sona ermemiş bir eşleşme olan iki kullanıcı birbirine istek gönderemez. | Application |
| RQ-07 | Taraflardan biri diğerini engellemişse istek gönderilemez. | Application |
| RQ-08 | İsteği yalnızca alıcı kabul veya reddedebilir; yalnızca gönderen geri çekebilir. | Domain |
| RQ-09 | İstek 3 gün içinde cevaplanmazsa `Expired` olur. Süresi dolmuş istek kabul edilemez. | Domain |
| RQ-10 | `Pending` dışındaki durumlar sondur; başka duruma geçilemez. | Domain |
| RQ-11 | İsteği reddedilen veya süresi dolan gönderen, aynı kullanıcıya 7 gün geçmeden yeniden istek gönderemez. Geri çekilen istek bekleme başlatmaz; alıcı ise beklemeden karşı istek gönderebilir. | Domain |
| RQ-12 | İstek, alıcıya isteğe bağlı kısa bir not taşıyabilir (en fazla 300 karakter). | Application |
| RQ-13 | Gönderen etkileşim koşullarını sağlamalıdır. Alıcının koşulları sağlaması istek almak için şart değildir, kabul etmek için şarttır. | Application |
| RQ-14 | Süresi dolmuş istek, arka plan işi kaydetmeden önce de her okumada ve işlemde süresi dolmuş sayılır. | Domain |

## Eşleşme

| No | Kural | Katman |
|---|---|---|
| MT-01 | Kabul edilen her istek tam olarak bir eşleşme oluşturur. | Domain, DB |
| MT-02 | İki kullanıcı arasında aynı anda yalnızca bir sona ermemiş eşleşme olabilir. | Application, DB |
| MT-03 | Eşleşme karşılıklı onayla sonlanır: bir taraf ister, diğer taraf onaylar. | Domain |
| MT-04 | Sonlandırma isteğini karşı taraf reddedebilir, isteyen taraf geri alabilir; eşleşme `Active` durumuna döner. | Domain |
| MT-05 | Engelleme, eşleşmeyi karşı tarafın onayı olmadan sonlandırır. | Application |
| MT-06 | Sona eren eşleşme yeniden açılamaz. Taraflar yeni bir istekle yeniden eşleşebilir. | Domain |
| MT-07 | Sonlandırma isteği 7 gün içinde onaylanmaz, reddedilmez veya geri alınmazsa eşleşme kendiliğinden sonlanır. Sürenin bitmesine 24 saat kala karşı tarafa hatırlatma gider. | Domain |
| MT-08 | Reddedilen sonlandırma isteğinin ardından yeniden istemek için bekleme süresi yoktur. | — |
| MT-09 | Sonlandırma isteği beklerken yeni oturum planlanamaz; mevcut oturumlar etkilenmez. | Application |

## Mesajlaşma

| No | Kural | Katman |
|---|---|---|
| MS-01 | Sohbet yalnızca eşleşme ile başlar; hiç eşleşmemiş iki kullanıcı birbirine mesaj atamaz. İki kullanıcı arasında tek bir sohbet olur. | Application, DB |
| MS-02 | Mesaj yalnızca metindir. Boş olamaz ve uzunluk sınırı vardır (sınır Faz 2'de sözleşmede belirlenir). | Domain |
| MS-03 | Yalnızca sohbetin iki tarafı mesaj gönderebilir ve okuyabilir. | Application |
| MS-04 | Eşleşme sona erince sohbet açık kalır; devam edip etmemek kullanıcılara bırakılır. | — |
| MS-05 | Aktif eşleşme yokken taraflardan biri sohbeti tek başına kapatabilir. Kapatılan sohbet salt okunur olur; geçmiş mesajlar iki tarafta da görünür kalır. | Domain |
| MS-06 | Engelleme ve hesap silme sohbeti kapatır. | Application |
| MS-07 | Aynı iki kullanıcı yeniden eşleşirse kapalı sohbet geçmişiyle birlikte yeniden açılır. | Application |
| MS-08 | Mesaj göndermek için sohbetin tarafı olmak yeterlidir; ayrıca etkileşim koşulu aranmaz (sohbet zaten eşleşmeyle açılır). | Application |
| MS-09 | Gönderilen mesaj düzenlenemez ve silinemez. | — |
| MS-10 | Her taraf için sohbeti en son okuduğu an saklanır; okunmamış sayısı ve "Görüldü" bilgisi bundan türetilir. Mesaj gönderen, o ana kadarki mesajları okumuş sayılır. | Domain |
| MS-11 | Aynı `clientMessageId` ile yinelenen gönderim yeni mesaj oluşturmaz, ilk mesajı döndürür. | Application, DB |

## Takas oturumu

| No | Kural | Katman |
|---|---|---|
| SS-01 | Oturum yalnızca `Active` durumundaki bir eşleşme için planlanabilir. | Application |
| SS-02 | Oturum tarihi gelecekte olmalıdır. | Domain |
| SS-03 | Oturumu bir taraf önerir, diğer taraf kabul veya reddeder. | Domain |
| SS-04 | Tamamlandı işareti yalnızca planlanan başlangıç zamanından sonra verilebilir. | Domain |
| SS-05 | Oturum karşılıklı onayla tamamlanır: bir taraf işaretler, **diğer** taraf onaylar. | Domain |
| SS-06 | Taraflardan herhangi biri oturumu tek başına iptal edebilir. İptal sebebi zorunludur. | Domain |
| SS-07 | İptal sebebi karşı tarafa bildirim olarak gider ve admin tarafından görülebilir. | Application |
| SS-08 | `Completed`, `Declined` ve `Cancelled` son durumlardır. | Domain |
| SS-09 | Eşleşme sona erince tamamlanmamış oturumlar iptal edilir. | Application |
| SS-10 | Tamamlandı işareti 72 saat içinde onaylanmaz ve oturum iptal edilmezse oturum kendiliğinden tamamlanmış sayılır ve bu şekilde işaretlenir. Sürenin bitmesine 24 saat kala karşı tarafa hatırlatma gider. | Domain |
| SS-11 | Tamamlandı işaretine itirazı olan taraf oturumu sebep yazarak iptal eder. | Domain |
| SS-12 | Kendiliğinden tamamlanan oturum da değerlendirme hakkı doğurur. | Application |
| SS-13 | Karşı taraf kabul etmeden başlangıç zamanı geçen oturum önerisi kendiliğinden `Declined` olur. | Domain |
| SS-14 | Uygulamada yalnızca çevrimiçi oturum planlanır (süre: 30, 45, 60, 90 veya 120 dakika). Yüz yüze buluşmalar kullanıcıların inisiyatifindedir: sohbette ayarlanır, tarih ve yer uygulamada tutulmaz. | Domain |
| SS-15 | Yüz yüze buluşma sonradan bildirilir: taraflardan biri "buluştuk" der, oturum doğrudan `CompletionPending` durumunda oluşur ve SS-05, SS-10 ile SS-11 aynen uygulanır. | Domain |
| SS-16 | Bekleyen sonlandırma isteği olan veya sona ermiş eşleşmeye oturum eklenemez (planlama da bildirim de). | Application |
| SS-17 | SS-09'daki iptal yalnızca henüz gerçekleşmemiş (önerilmiş veya planlanmış) oturumları kapsar; onay bekleyen tamamlandı işareti eşleşme bittikten sonra da onaylanabilir. | Domain |
| SS-18 | Süresi geçen öneri ve onay, arka plan işi kaydetmeden önce de her okumada ve işlemde sonuçlanmış sayılır. | Domain |
| SS-19 | Bir kullanıcı çifti, bütün eşleşmeleri boyunca yalnızca bir yüz yüze buluşma bildirebilir (sahte bildirimle istatistik şişirmeye karşı). İptal edilmiş bildirim sayılmaz; onay bekleyen veya tamamlanmış bildirim varken yenisi `in_person_already_reported` ile reddedilir. | Application + veritabanı |

### Kullanıcı istatistikleri

İstatistikler ayrı sayaçlarda tutulmaz; eşleşme ve oturum kayıtlarından hesaplanır (`GET /api/v1/users/{userId}/stats`).

| No | Kural |
|---|---|
| ST-01 | Toplam eşleşme: kullanıcının sona ermiş bütün eşleşmeleri. Devam eden eşleşme, sona erene kadar hiçbir sayaca girmez. |
| ST-02 | Tamamlanan eşleşme: en az bir oturumu tamamlanmış, sona ermiş eşleşme. |
| ST-03 | İptal edilen (tamamlanmamış) eşleşme: hiçbir oturumu tamamlanmadan sona ermiş eşleşme. |
| ST-04 | Tamamlanan oturum: kullanıcının bütün eşleşmelerindeki tamamlanmış oturumlar (kendiliğinden tamamlananlar dahil). |
| ST-05 | Toplam, tamamlanan ile iptal edilenin toplamına eşittir. |

## Değerlendirme

| No | Kural | Katman |
|---|---|---|
| RV-01 | Bir kullanıcı başka bir kullanıcıyı yalnızca bir kez değerlendirebilir. | Application, DB |
| RV-02 | Değerlendirme için iki kullanıcı arasında en az bir `Completed` oturum olmalıdır (kendiliğinden tamamlanan dahil). Eşleşmenin bitmesi beklenmez: ilk oturum tamamlanır tamamlanmaz iki taraf da değerlendirebilir. | Application |
| RV-03 | Kullanıcı kendini değerlendiremez. | Domain |
| RV-04 | Puan 1–10 arasında tam sayıdır. Yorum isteğe bağlıdır ve en fazla 500 karakterdir. | Domain |
| RV-05 | Değerlendirme yapıldığı anda herkese görünür olur. | — |
| RV-06 | Değerlendirme düzenlenemez. Yazarı silebilir. | Domain |
| RV-07 | Silinen değerlendirmenin yerine yenisi yazılamaz. | DB |
| RV-08 | Ortalama puan yalnızca silinmemiş değerlendirmelerden hesaplanır; ayrı bir özet tablosu tutulmaz. İlk değerlendirmeden itibaren değerlendirme sayısıyla birlikte gösterilir. | Application |
| RV-09 | Değerlendirilen kullanıcı değerlendirmeye cevap yazamaz (MVP). | — |
| RV-10 | Değerlendirme yazmak için etkileşim koşulu aranır (bkz. ID-04). | Application |

## Bildirim

| No | Kural | Katman |
|---|---|---|
| NT-01 | Bir kullanıcının işlemi karşı tarafı ilgilendiriyorsa karşı tarafa bildirim üretilir: istek (geldi, kabul, ret, geri çekme, süre dolması), sonlandırma isteği ve cevabı, eşleşmenin bitmesi, oturum (öneri, kabul, ret, iptal ve sebebi, tamamlandı işareti, tamamlanma), değerlendirme, yetenek önerisinin sonucu. İşlemi yapan kişiye kendi işlemi için bildirim gitmez. | Application |
| NT-02 | Bildirim, onu doğuran işlemle aynı işlemde (transaction) kaydedilir; işlem başarısız olursa bildirim de oluşmaz. Açık bağlantılara `NotificationReceived` olayı işlem başarıyla bittikten sonra gider. | Application |
| NT-03 | Kabul edilen istek için istek sahibine tek bildirim gider (`SwapRequestAccepted`, eşleşmenin kimliğiyle); ayrıca "eşleşme oluştu" bildirimi yoktur. | Application |
| NT-04 | Hatırlatmalar: cevaplanmamış sonlandırma isteği ve onaylanmamış tamamlandı işareti için, süre dolmadan 24 saat önce cevap vermesi gereken tarafa bir kez. Planlanmış çevrimiçi oturum için başlangıçtan 24 saat ve 1 saat önce iki tarafa. Hatırlatma süresinin içinde kabul edilen oturum için o hatırlatma atlanır. | Domain |
| NT-05 | Kendiliğinden sonuçlanan süreler de bildirilir: süresi dolan istek sahibine, cevaplanmayan oturum önerisi öneren kişiye, kendiliğinden biten eşleşme ve kendiliğinden tamamlanan oturum iki tarafa. | Application |
| NT-06 | Yeni mesaj bildirim listesine yazılmaz; okunmamış mesaj sayısı sohbet listesinde ve Mesajlar rozetinde görünür. | — |
| NT-07 | Bildirimler 90 gün saklanır, sonra silinir. | Application |
| NT-08 | Bildirimin metni sunucuda üretilmez: sunucu tür ile ilgili kimlikleri verir, metni istemci kendi dil dosyasından kurar. | — |
| NT-09 | Tür bazında bildirim tercihi ve e-posta bildirimi MVP'de yoktur. | — |
| NT-10 | Bir cihaz kaydı tek bir FCM token'ına karşılık gelir; aynı token'la yeniden kayıt, cihazı o an giriş yapmış kullanıcıya taşır. | Domain, DB |

## Moderasyon

| No | Kural | Katman |
|---|---|---|
| MD-01 | Kullanıcı kendini engelleyemez veya şikâyet edemez. | Domain |
| MD-02 | Engelleme tek yönlü kaydedilir ama etkisi iki yönlüdür: taraflar keşfette birbirini görmez ve birbirine istek gönderemez. | Application |
| MD-03 | Engelleme, aradaki bekleyen istekleri iptal eder. | Application |
| MD-04 | Şikâyet bir kullanıcıya, mesaja veya değerlendirmeye yapılabilir. | Domain |

## Keşfet sıralaması (MVP: kural tabanlı)

Ayrıntılı skor formülü Faz 5.3'te belirlenir. Sıralama önceliği:

1. **Karşılıklı uyum:** Benim sunduğum onun istediği **ve** onun sunduğu benim istediğim.
2. **Tek yönlü uyum:** Onun sunduğu benim istediğim.
3. **Tercih uyumu:** Yüz yüze için aynı şehir veya iki tarafın da çevrimiçi tercihi.
4. **Puan:** Ortalama değerlendirme puanı yüksek olan önce; hiç değerlendirmesi olmayan, değerlendirilmiş olanlardan sonra. Değerlendirme sayısı sıralamayı etkilemez.
5. **Yenilik:** Profili daha yeni güncellenen önce.

Seviye uyumu: Bir yetenek, sunulan seviye istenen en düşük seviyeye eşit veya ondan yüksekse uyum sayılır. Seviyesi yetmeyen kullanıcı listeden çıkmaz, yalnızca uyumlu sayılmaz.

Hiç uyumu olmayan kullanıcılar da listenin sonunda gösterilir; yeteneği olmayan biri keşfette boş ekran görmez.

Arama: Ad veya kullanıcı adıyla arama yapıldığında etkileşim koşulu aranmaz (bkz. PR-06); sıralama aynıdır.

Kullanıcının kendisi, engellediği/engellendiği kullanıcılar (engelleme geldiğinde) ve askıya alınmış veya silinmekte olan hesaplar listede görünmez.
