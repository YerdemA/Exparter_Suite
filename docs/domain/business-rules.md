# İş Kuralları

Durum: **Taslak, onay bekliyor.** `(V#)` ile işaretli kurallar varsayımdır; bkz. `open-questions.md`.

**Katman** sütunu kuralın nerede korunduğunu söyler:

- **Domain:** Entity kendi içinde korur; ihlalde domain hatası fırlatır.
- **Application:** Handler, başka modülden veri sorarak korur.
- **DB:** Eşzamanlı isteklere karşı son savunma olarak veritabanı kısıtı da vardır.

## Kimlik ve hesap

| No | Kural | Katman |
|---|---|---|
| ID-01 | E-posta benzersizdir; büyük/küçük harf ayrımı yapılmaz. | Application, DB |
| ID-02 | Kayıt yalnızca e-posta ve parola ile yapılır. Harici giriş (Google/Apple) MVP'de yoktur. | — |
| ID-03 | E-postası doğrulanmamış kullanıcı giriş yapabilir, profilini ve yeteneklerini düzenleyebilir, keşfet ekranını görebilir. | Application |
| ID-04 | E-postası doğrulanmamış kullanıcı başkalarıyla etkileşime giremez: takas isteği gönderemez veya kabul edemez, mesaj atamaz, oturum planlayamaz, değerlendirme yapamaz, yetenek öneremez, şikâyet edemez. | Application |
| ID-05 | Refresh token her kullanımda yenilenir. İptal edilmiş bir token yeniden kullanılırsa o token ailesinin tamamı iptal edilir. | Domain |
| ID-06 | Doğrulama ve parola sıfırlama token'ları tek kullanımlıktır ve süreleri dolunca geçersizdir. | Domain |
| ID-07 | Parola sıfırlandığında kullanıcının tüm refresh token'ları iptal edilir. | Domain |
| ID-08 | `Suspended` durumundaki kullanıcı giriş yapamaz ve token yenileyemez. | Domain |

## Profil

| No | Kural | Katman |
|---|---|---|
| PR-01 | Her kullanıcının tam olarak bir profili vardır. | DB |
| PR-02 | Konum olarak yalnızca şehir tutulur; koordinat veya adres tutulmaz. | — |
| PR-03 | Çevrimiçi ve yüz yüze tercihlerinden en az biri seçili olmalıdır (V10). | Domain |
| PR-04 | Yüz yüze tercihi seçiliyse şehir zorunludur (V10). | Domain |

## Yetenekler

| No | Kural | Katman |
|---|---|---|
| SK-01 | Kullanıcı profiline yalnızca katalogdaki onaylı yetenekleri ekleyebilir (V12). | Application |
| SK-02 | Kullanıcı katalogda olmayan bir yeteneği önerebilir. Öneri admin onaylayana kadar katalogda görünmez. | Domain |
| SK-03 | Aynı ada (slug) sahip ikinci bir yetenek oluşturulamaz veya önerilemez. | Application, DB |
| SK-04 | Bir kullanıcı aynı yeteneği yalnızca bir kez ekleyebilir; aynı yetenek hem sunulan hem istenen olamaz (V13). | DB |
| SK-05 | Sunulan yetenekte seviye kullanıcının kendi seviyesidir. İstenen yetenekte seviye, aranan öğreticinin en düşük seviyesidir. | — |
| SK-06 | Aktif bir eşleşmede veya bekleyen bir istekte kullanılan sunulan yetenek profilden kaldırılamaz (V16). | Application |

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

## Eşleşme

| No | Kural | Katman |
|---|---|---|
| MT-01 | Kabul edilen her istek tam olarak bir eşleşme oluşturur. | Domain, DB |
| MT-02 | İki kullanıcı arasında aynı anda yalnızca bir sona ermemiş eşleşme olabilir. | Application, DB |
| MT-03 | Eşleşme karşılıklı onayla sonlanır: bir taraf ister, diğer taraf onaylar. | Domain |
| MT-04 | Sonlandırma isteğini karşı taraf reddedebilir, isteyen taraf geri alabilir; eşleşme `Active` durumuna döner. | Domain |
| MT-05 | Engelleme, eşleşmeyi karşı tarafın onayı olmadan sonlandırır (V7). | Application |
| MT-06 | Sona eren eşleşme yeniden açılamaz. Taraflar yeni bir istekle yeniden eşleşebilir. | Domain |

## Mesajlaşma

| No | Kural | Katman |
|---|---|---|
| MS-01 | Mesajlaşma yalnızca eşleşmiş kullanıcılar arasında mümkündür; her eşleşmenin tek bir sohbeti vardır. | Application, DB |
| MS-02 | Mesaj yalnızca metindir. Boş olamaz ve uzunluk sınırı vardır (sınır Faz 2'de sözleşmede belirlenir). | Domain |
| MS-03 | Yalnızca sohbetin iki tarafı mesaj gönderebilir ve okuyabilir. | Application |
| MS-04 | Eşleşme sona erince sohbet salt okunur olur (V15). | Application |

## Takas oturumu

| No | Kural | Katman |
|---|---|---|
| SS-01 | Oturum yalnızca `Active` durumundaki bir eşleşme için planlanabilir. | Application |
| SS-02 | Oturum tarihi gelecekte olmalıdır. | Domain |
| SS-03 | Oturumu bir taraf önerir, diğer taraf kabul veya reddeder (V8). | Domain |
| SS-04 | Tamamlandı işareti yalnızca planlanan başlangıç zamanından sonra verilebilir. | Domain |
| SS-05 | Oturum karşılıklı onayla tamamlanır: bir taraf işaretler, **diğer** taraf onaylar. | Domain |
| SS-06 | Taraflardan herhangi biri oturumu tek başına iptal edebilir. İptal sebebi zorunludur. | Domain |
| SS-07 | İptal sebebi karşı tarafa bildirim olarak gider ve admin tarafından görülebilir. | Application |
| SS-08 | `Completed`, `Declined` ve `Cancelled` son durumlardır. | Domain |
| SS-09 | Eşleşme sona erince tamamlanmamış oturumlar iptal edilir (V15). | Application |

## Değerlendirme

| No | Kural | Katman |
|---|---|---|
| RV-01 | Bir kullanıcı başka bir kullanıcıyı yalnızca bir kez değerlendirebilir. | Application, DB |
| RV-02 | Değerlendirme için iki kullanıcı arasında en az bir `Completed` oturum olmalıdır. | Application |
| RV-03 | Kullanıcı kendini değerlendiremez. | Domain |
| RV-04 | Puan 1–10 arasında tam sayıdır. Yorum isteğe bağlıdır (V14). | Domain |
| RV-05 | Değerlendirme yapıldığı anda herkese görünür olur. | — |
| RV-06 | Değerlendirme düzenlenemez. Yazarı silebilir. | Domain |
| RV-07 | Silinen değerlendirmenin yerine yenisi yazılamaz (V5). | DB |
| RV-08 | Ortalama puan yalnızca silinmemiş değerlendirmelerden hesaplanır. | Application |

## Moderasyon

| No | Kural | Katman |
|---|---|---|
| MD-01 | Kullanıcı kendini engelleyemez veya şikâyet edemez. | Domain |
| MD-02 | Engelleme tek yönlü kaydedilir ama etkisi iki yönlüdür: taraflar keşfette birbirini görmez ve birbirine istek gönderemez. | Application |
| MD-03 | Engelleme, aradaki bekleyen istekleri iptal eder (V7). | Application |
| MD-04 | Şikâyet bir kullanıcıya, mesaja veya değerlendirmeye yapılabilir. | Domain |

## Keşfet sıralaması (MVP: kural tabanlı)

Ayrıntılı skor formülü Faz 5.3'te belirlenir. Sıralama önceliği:

1. **Karşılıklı uyum:** Benim sunduğum onun istediği **ve** onun sunduğu benim istediğim.
2. **Tek yönlü uyum:** Onun sunduğu benim istediğim.
3. **Tercih uyumu:** Aynı şehir veya iki tarafın da çevrimiçi tercihi.
4. **Puan:** Ortalama değerlendirme puanı.

Kullanıcının kendisi, engellediği/engellendiği kullanıcılar ve askıya alınmış hesaplar listede görünmez.
