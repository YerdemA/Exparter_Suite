# Google Play mağaza kaydı ve kapalı test

Play Console'da doldurulacak her şey burada: mağaza metinleri, görseller, uygulama içeriği formlarının yanıtları ve kapalı testten üretime geçiş. Sunucu ve yayın derlemesi için: [deployment.md](../deployment.md).

## 1. Uygulamayı oluşturma

**Create app:** ad "Exparter: Yetenek Takası", varsayılan dil **Türkçe – tr-TR**, App, **Free**. Ücretsiz uygulama sonradan ücretliye çevrilemez.

## 2. Mağaza metinleri

**Uygulama adı** (en fazla 30 karakter):

```
Exparter: Yetenek Takası
```

**Kısa açıklama** (en fazla 80 karakter):

```
Bildiğinizi öğretin, öğrenmek istediğinizi öğrenin: karşılıklı yetenek takası.
```

**Tam açıklama** (en fazla 4000 karakter):

```
Exparter, yeteneklerinizi karşılıklı takas edebileceğiniz kişilerle sizi buluşturur. Gitar çalabiliyor ve İngilizce öğrenmek istiyorsanız, İngilizce öğretebilen ve gitar öğrenmek isteyen biriyle eşleşirsiniz: ikiniz de bildiğinizi öğretir, öğrenmek istediğinizi öğrenirsiniz. Para yok, yalnızca takas.

NASIL ÇALIŞIR?
• Sunabildiğiniz yetenekleri seviyesiyle, öğrenmek istediklerinizi de ekleyin.
• Keşfet ekranında karşılıklı uyumlu kişiler öne çıkar: sizin sunduğunuzu isteyen ve sizin istediğinizi sunan kişiler.
• Takas isteği gönderin; kabul edilirse eşleşirsiniz.
• Mesajlaşın, çevrimiçi bir oturum planlayın ya da yüz yüze buluşun.
• Takas bitince birbirinizi değerlendirin.

ÖNE ÇIKANLAR
• Kategorilere ayrılmış yetenek kataloğu; aradığınızı bulamazsanız yeni bir yetenek önerebilirsiniz.
• Yeteneğe, kategoriye, şehre ve takas biçimine (çevrimiçi veya yüz yüze) göre arama.
• Anlık mesajlaşma ve bildirimler.
• Çevrimiçi oturumlardan önce hatırlatma.
• Değerlendirmeler ve tamamlanan takas sayıları, kiminle eşleştiğinizi bilmenize yardım eder.
• Rahatsız eden kullanıcıları engelleme ve şikâyet etme.
• Açık ve koyu tema.

GÜVENLİK VE GİZLİLİK
Exparter 18 yaşından büyükler içindir. Konumunuz değil, yalnızca seçtiğiniz şehir tutulur. E-posta adresiniz hiçbir kullanıcıya gösterilmez. Uygulamada reklam yoktur. Hesabınızı istediğiniz zaman uygulamanın içinden silebilirsiniz.
```

**Kategori:** Eğitim. **İletişim e-postası:** [İLETİŞİM E-POSTASI] (mağazada herkese görünür). **Gizlilik politikası:** `https://<alan-adı>/gizlilik.html`.

## 3. Görseller

| Öğe | Play'in istediği | Dosya |
|---|---|---|
| Uygulama simgesi | 512×512 PNG | [icon-512.png](icon-512.png) |
| Öne çıkan görsel | 1024×500 PNG veya JPEG, saydamlık yok | [feature-graphic.png](feature-graphic.png) |
| Telefon ekran görüntüleri | En az 2, en fazla 8; 9:16, kısa kenar en az 1080 px önerilir | Henüz yok |

Simge ve öne çıkan görsel logonun çizim kodundan üretilir: `mobile/` klasöründen `flutter test tool/generate_store_assets.dart`.

**Ekran görüntüleri** canlı ortamda örnek hesaplarla, emülatörde alınacak. Önerilen sıra: Keşfet (karşılıklı uyumlu kişiler), bir kullanıcının profili ve yetenekleri, takas isteği, sohbet, oturum planlama, değerlendirmeler. Gerçek kullanıcı verisi kullanılmaz.

## 4. Uygulama içeriği (App content)

| Bölüm | Yanıt |
|---|---|
| Privacy policy | `https://<alan-adı>/gizlilik.html` |
| Ads | Uygulamada reklam yok. |
| App access | Tüm özellikler giriş gerektirir. İnceleme ekibi için canlı ortamda bir hesap açıp kullanıcı adı ve parolasını buraya yazın; hesabın profili dolu, en az bir sunulan yeteneği olsun. |
| Content rating | IARC anketi. Kullanıcılar birbiriyle iletişim kurabiliyor mu: **evet** (mesajlaşma). Kullanıcılar konumlarını paylaşabiliyor mu: **hayır** (yalnızca şehir). Şiddet, cinsellik, kumar, uyuşturucu içeriği: **yok**. |
| Target audience | Yalnızca **18 yaş ve üzeri**. |
| News app | Hayır. |
| Data safety | 5. bölüm. |
| Government apps, financial features, health | Hayır. |
| Account deletion | Uygulama içinden: Profil → Hesabımı sil. Web: `https://<alan-adı>/hesap-silme.html` |

## 5. Veri güvenliği (Data safety) formu

**Genel sorular:**

- Uygulama kullanıcı verisi topluyor veya paylaşıyor mu? **Evet.**
- Toplanan tüm veriler aktarım sırasında şifreleniyor mu? **Evet** (HTTPS).
- Kullanıcılar verilerinin silinmesini isteyebilir mi? **Evet** (uygulama içinden ve web sayfasından).

**Paylaşım:** Hiçbir veri "paylaşılmış" sayılmaz. Play'in tanımında, bizim adımıza işleyen hizmet sağlayıcılara (Oracle, Cloudflare, Brevo, Firebase, Sentry) aktarım paylaşım değildir.

**Toplanan veriler** (tümü uygulamanın işlevi için; reklam, pazarlama veya kişiselleştirme amaçlı kullanım yok; hiçbiri geçici olarak işlenmiyor):

| Play'deki tür | Bizde karşılığı | Zorunlu mu | Amaç |
|---|---|---|---|
| Kişisel bilgiler → Ad | Görünen ad | Zorunlu | Uygulama işlevi |
| Kişisel bilgiler → E-posta adresi | E-posta | Zorunlu | Hesap yönetimi |
| Kişisel bilgiler → Kullanıcı kimlikleri | Kullanıcı adı, kullanıcı kimliği | Zorunlu | Uygulama işlevi, hesap yönetimi |
| Kişisel bilgiler → Diğer bilgiler | Biyografi, yetenekler, takas tercihi | İsteğe bağlı (biyografi) | Uygulama işlevi |
| Konum → Yaklaşık konum | Kullanıcının seçtiği şehir | İsteğe bağlı | Uygulama işlevi |
| Fotoğraflar ve videolar → Fotoğraflar | Profil fotoğrafı | İsteğe bağlı | Uygulama işlevi |
| Mesajlar → Diğer uygulama içi mesajlar | Sohbet mesajları | Zorunlu değil (kullanıcı yazarsa) | Uygulama işlevi |
| Uygulama etkinliği → Kullanıcı tarafından oluşturulan diğer içerikler | Değerlendirmeler, şikâyetler | Zorunlu değil | Uygulama işlevi, güvenlik ve dolandırıcılığı önleme |
| Uygulama bilgileri ve performansı → Kilitlenme günlükleri | Sentry çökme raporları | Zorunlu | Analiz (hata ayıklama) |
| Uygulama bilgileri ve performansı → Diğer teşhis verileri | Sentry hata ayrıntıları | Zorunlu | Analiz (hata ayıklama) |
| Cihaz veya diğer kimlikler | Firebase bildirim token'ı | İsteğe bağlı (bildirim izni) | Uygulama işlevi |

Play bu formu uygulamanın gerçek davranışıyla karşılaştırır; uygulamaya yeni bir veri türü eklenirse form da güncellenmelidir.

## 6. Kapalı test

1. **Testing → Closed testing → Create track** (ör. "Kapalı test").
2. **Testers:** bir e-posta listesi oluşturup test kullanıcılarının Google hesaplarını ekleyin. Kişisel hesaplarda üretime geçmek için **en az 12 test kullanıcısının 14 gün boyunca kesintisiz** kapalı testte kalması gerekir; ayrılanlar olabileceği için 15–20 kişiyle başlayın.
3. **Countries:** Türkiye.
4. **Create release:** `app-release.aab` dosyasını yükleyin. İlk yüklemede **Play App Signing**'i kabul edin; Google uygulama imzalama anahtarını tutar, sizin anahtarınız yalnızca yükleme anahtarıdır.
5. Sürüm notu, ör. "İlk kapalı test sürümü."
6. **Review release → Start rollout.** İlk inceleme birkaç gün sürebilir.
7. İnceleme bitince **Testers** sekmesindeki katılım bağlantısını test kullanıcılarına gönderin. Bağlantıyı açıp "Become a tester" dedikten sonra uygulamayı Play Store'dan kurarlar.
8. 14 gün dolunca **Dashboard → Apply for production**. Google test süreci hakkında birkaç soru sorar (kaç kişi test etti, hangi geri bildirimler alındı, neler düzeltildi). Test kullanıcılarının geri bildirimlerini ve yapılan düzeltmeleri not etmeye baştan başlayın.

## 7. Yayından önce hukuki kontrol listesi

Aşağıdakiler teknik değil hukuki konulardır; taslak metinler (`deploy/site/gizlilik.html`, `deploy/site/kvkk.html`) bir hukukçuya gösterilmeden yayımlanmamalıdır.

- [ ] Veri sorumlusunun adı, adresi ve iletişim e-postası (metinlerdeki köşeli parantezler).
- [ ] **Yurt dışına aktarım** (KVKK m. 9): Oracle, Cloudflare, Brevo, Google (Firebase) ve Sentry ile Kurul'un standart sözleşmelerinin imzalanması ve Kurul'a süresi içinde bildirilmesi ya da başka bir dayanak.
- [ ] **5651 sayılı Kanun:** Kullanıcı içeriği barındırdığı için uygulamanın "yer sağlayıcı" sayılıp sayılmadığı ve trafik bilgisi saklama yükümlülüğü. Saklama gerekiyorsa sunucu kayıtlarının süresi ve içeriği buna göre değiştirilmelidir (şu an IP adresi kaydedilmiyor).
- [ ] Şikâyet kayıtlarının ve sunucu kayıtlarının saklama süreleri.
- [ ] Silinen hesabın mesaj ve değerlendirmelerinin "Silinmiş kullanıcı" adıyla kalmasının anonimleştirme sayılıp sayılmadığı (mesaj içeriği kişisel veri içerebilir).
- [ ] VERBİS kaydı yükümlülüğü (çalışan sayısı ve bilanço eşiğinin altındaki gerçek kişi veri sorumluları muaf olabilir).
- [ ] Kullanım koşulları (kullanıcı sözleşmesi): davranış kuralları, askıya alma, sorumluluğun sınırlanması. Henüz taslağı yok.
- [ ] Aydınlatma metninin kayıt ekranında, hesap oluşturulmadan önce gösterilmesi.

## 8. Uygulamada kalan işler

- **Uygulama içinde gizlilik bağlantısı:** Play, gizlilik politikasının uygulamanın içinden de erişilebilir olmasını istiyor. Kayıt ekranına ve profile gizlilik politikası ve aydınlatma metni bağlantıları eklenecek; bunun için alan adı belli olmalı.
- **Ekran görüntüleri** (3. bölüm).
