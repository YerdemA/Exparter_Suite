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
• Mesajlaşın, çevrimiçi bir oturum planlayıp uygulamanın içinden görüntülü görüşün ya da yüz yüze buluşun.
• Takas bitince birbirinizi değerlendirin.

ÖNE ÇIKANLAR
• Kategorilere ayrılmış yetenek kataloğu; aradığınızı bulamazsanız yeni bir yetenek önerebilirsiniz.
• Yeteneğe, kategoriye, şehre ve takas biçimine (çevrimiçi veya yüz yüze) göre arama.
• Anlık mesajlaşma ve bildirimler.
• Uygulama içinde görüntülü görüşme ve ekran paylaşımı; ses ve görüntü kaydedilmez.
• Çevrimiçi oturumlardan önce hatırlatma.
• Değerlendirmeler ve tamamlanan takas sayıları, kiminle eşleştiğinizi bilmenize yardım eder.
• Rahatsız eden kullanıcıları engelleme ve şikâyet etme.
• Açık ve koyu tema.

GÜVENLİK VE GİZLİLİK
Exparter 18 yaşından büyükler içindir. Konumunuz değil, yalnızca seçtiğiniz şehir tutulur. E-posta adresiniz hiçbir kullanıcıya gösterilmez. Uygulamada reklam yoktur. Hesabınızı istediğiniz zaman uygulamanın içinden silebilirsiniz.
```

**Kategori:** Eğitim. **İletişim e-postası:** `destek@exparter.app` (mağazada herkese görünür). **Gizlilik politikası:** `https://exparter.app/gizlilik.html`.

## 3. Görseller

| Öğe | Play'in istediği | Dosya |
|---|---|---|
| Uygulama simgesi | 512×512 PNG | [icon-512.png](icon-512.png) |
| Öne çıkan görsel | 1024×500 PNG veya JPEG, saydamlık yok | [feature-graphic.png](feature-graphic.png) |
| Telefon ekran görüntüleri | En az 2, en fazla 8; 9:16, kısa kenar en az 1080 px önerilir | [screenshots/](screenshots/) (6 adet, 1080×1920) |

Simge ve öne çıkan görsel logonun çizim kodundan üretilir: `mobile/` klasöründen `flutter test tool/generate_store_assets.dart`.

**Ekran görüntüleri** dosya adlarındaki sırayla yüklenir: Keşfet, bir kullanıcının profili, gelen takas isteği, sohbet (koyu tema), oturumlar, değerlendirmeler. Kurgusal kişilerle, yerel ortamda ve boş bir veritabanında alındı; canlı ortama örnek hesap açılmadı, çünkü gerçek kullanıcılar Keşfet'te bu sahte profilleri görürdü.

Yeniden çekmek için: yerelde boş bir veritabanı oluşturup API'yi ona bağlayın (`ConnectionStrings__Database`, e-posta sınırı için `RateLimits__EmailsPerQuarterHour=1000`), `--migrate` ile şemayı kurun, API'yi çalıştırıp `python scripts/seed-store-screenshots.py` ile verileri ekleyin ve uygulamada `elif_ornek` ile giriş yapın. Emülatörde Play'in en/boy sınırı (uzun kenar kısa kenarın en fazla iki katı) için `adb shell wm size 1080x1920` ve `wm density 360`; durum çubuğu için sistem arayüzü demo modu; saat dilimi `Europe/Istanbul`.

## 4. Uygulama içeriği (App content)

| Bölüm | Yanıt |
|---|---|
| Privacy policy | `https://exparter.app/gizlilik.html` |
| Ads | Uygulamada reklam yok. |
| App access | Tüm özellikler giriş gerektirir. İnceleme ekibi için canlı ortamda bir hesap açıp kullanıcı adı ve parolasını buraya yazın; hesabın profili dolu, en az bir sunulan yeteneği olsun. |
| Content rating | IARC anketi. Kullanıcılar birbiriyle iletişim kurabiliyor mu: **evet** (mesajlaşma, görüntülü görüşme). Kullanıcılar konumlarını paylaşabiliyor mu: **hayır** (yalnızca şehir). Şiddet, cinsellik, kumar, uyuşturucu içeriği: **yok**. |
| Target audience | Yalnızca **18 yaş ve üzeri**. |
| News app | Hayır. |
| Data safety | 5. bölüm. |
| Government apps, financial features, health | Hayır. |
| Account deletion | Uygulama içinden: Profil → Ayarlar (çark simgesi) → Hesabımı sil. Web: `https://exparter.app/hesap-silme.html` |

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
| Mesajlar → Diğer uygulama içi mesajlar | Sohbet mesajları | Zorunlu değil (kullanıcı yazarsa) | Uygulama işlevi; dolandırıcılığı önleme, güvenlik ve uyumluluk (şikâyet edilen mesajlar incelenir) |
| Uygulama etkinliği → Uygulama içi arama geçmişi | Keşfet'teki ad veya kullanıcı adı aramaları (istek adresinde olduğu için trafik kaydında 1 yıl kalır) | Zorunlu değil | Uygulama işlevi; dolandırıcılığı önleme, güvenlik ve uyumluluk |
| Uygulama etkinliği → Kullanıcı tarafından oluşturulan diğer içerikler | Değerlendirmeler, şikâyetler | Zorunlu değil | Uygulama işlevi, güvenlik ve dolandırıcılığı önleme |
| Uygulama bilgileri ve performansı → Kilitlenme günlükleri | Sentry çökme raporları | Zorunlu | Analiz (hata ayıklama) |
| Uygulama bilgileri ve performansı → Diğer teşhis verileri | Sentry hata ayrıntıları | Zorunlu | Analiz (hata ayıklama) |
| Cihaz veya diğer kimlikler | Firebase bildirim token'ı; sunucunun trafik kaydındaki IP adresi | Zorunlu (IP her istekte kaydedilir; token yalnızca bildirim izniyle) | Uygulama işlevi; dolandırıcılığı önleme, güvenlik ve uyumluluk (5651 gereği 1 yıl) |

Hiçbir veri geçici olarak işlenmiyor; hepsi saklanıyor. Form bu yanıtlarla 2026-10-28'de dolduruldu.

**Görüntülü görüşmeyle eklenenler** (Faz 8 dilim 1, ADR 0015; 1.0.0 (6) ile forma girildi, 2026-11-03):

| Play'deki tür | Bizde karşılığı | Zorunlu mu | Amaç |
|---|---|---|---|
| Fotoğraflar ve videolar → Videolar | Görüşmede kameranın görüntüsü ve kullanıcı paylaşırsa ekranının görüntüsü | Zorunlu değil (kullanıcı görüşmeye katılırsa) | Uygulama işlevi; **geçici olarak işlenir** (yalnızca iletilir, saklanmaz) |
| Ses → Ses kayıtları | Görüşmede mikrofonun sesi | Zorunlu değil (kullanıcı görüşmeye katılırsa) | Uygulama işlevi; **geçici olarak işlenir** |

İzin beyanları: kamera ve mikrofon için ayrı beyan gerekmez; **ön plan servisi (Foreground service) beyanı** gerekir, iki tür için: `microphone`, gerekçe "Kullanıcı başlattığı bir görüntülü görüşmede uygulamadan çıkınca ses sürsün diye"; `mediaProjection`, gerekçe "Kullanıcı görüşmede ekranını paylaşmayı seçtiğinde, sistemin izin penceresinden sonra ve yalnızca paylaşım süresince". Beyanda seçilen görev türleri: `microphone` için "Arka planda ses girişi", `mediaProjection` için "Medya ve içerik projeksiyonu, yayın". Play her tür için bir video bağlantısı ister; iki tür için de aynı video kullanıldı (görüşmeye katılma, uygulamadan çıkınca "Görüşme sürüyor" bildirimi, ekranı paylaşma, başka uygulamaya geçme, paylaşımı durdurma). Video emülatörde, yerel ortamda kurgusal hesaplarla çekildi: `marketing/08-play-beyanlari/gorusme-ve-ekran-paylasimi.mp4` (repo dışında), YouTube'da liste dışı. Content rating anketinde "Kullanıcılar birbiriyle iletişim kurabiliyor mu" zaten **evet**; görüntülü görüşme yanıtı değiştirmez.

Sıra (1.0.0 (6), 2026-11-03): önce Data safety formu güncellenir; sonra kapalı test kanalında yeni sürüm oluşturulup AAB yüklenir. Paket ön plan servisi türleri bildirdiği için Play, sürüm incelemeye gönderilmeden önce **App content → Foreground service permissions** beyanını ister. Beyan her tür için görevin ne olduğunu, ertelenirse veya kesilirse kullanıcının ne kaybedeceğini ve özelliği gösteren bir video bağlantısını (YouTube'da liste dışı) sorar.

Play bu formu uygulamanın gerçek davranışıyla karşılaştırır; uygulamaya yeni bir veri türü eklenirse form da güncellenmelidir.

## 6. Kapalı test

1. **Testing → Closed testing → Create track** (ör. "Kapalı test").
2. **Testers:** bir e-posta listesi oluşturup test kullanıcılarının Google hesaplarını ekleyin. Kişisel hesaplarda üretime geçmek için **en az 12 test kullanıcısının 14 gün boyunca kesintisiz** kapalı testte kalması gerekir; ayrılanlar olabileceği için 15–20 kişiyle başlayın.
3. **Countries:** Türkiye.
4. **Create release:** `app-release.aab` dosyasını yükleyin. İlk yüklemede **Play App Signing**'i kabul edin; Google uygulama imzalama anahtarını tutar, sizin anahtarınız yalnızca yükleme anahtarıdır. Yüklemeden sonra **Test and release → App integrity → App signing** bölümündeki "App signing key certificate" SHA-256 parmak izini `deploy/site/.well-known/assetlinks.json` dosyasına ekleyin (yükleme anahtarınınki zaten orada). Eklenmezse Play'den kurulan uygulama e-posta bağlantılarını kendisi açmaz, bağlantılar sitedeki sayfada açılır.
5. Sürüm notu, ör. "İlk kapalı test sürümü."
6. **Review release → Start rollout.** İlk inceleme birkaç gün sürebilir.
7. İnceleme bitince **Testers** sekmesindeki katılım bağlantısını test kullanıcılarına gönderin. Bağlantıyı açıp "Become a tester" dedikten sonra uygulamayı Play Store'dan kurarlar.
8. 14 gün dolunca **Dashboard → Apply for production**. Google test süreci hakkında birkaç soru sorar (kaç kişi test etti, hangi geri bildirimler alındı, neler düzeltildi). Test kullanıcılarının geri bildirimlerini ve yapılan düzeltmeleri not etmeye baştan başlayın.

## 7. Yayından önce hukuki kontrol listesi

Hukukçu görüşü alınmadı (kullanıcı kararı, 2026-10-27); konular benzer uygulamaları inceleyen bir araştırmayla netleştirildi: [compliance-research.md](../legal/compliance-research.md).

- [x] Veri sorumlusu: Yusuf Erdem Aykaç, `destek@exparter.app` (posta adresi yok; başvuru e-postayla).
- [ ] **Yurt dışına aktarım** (KVKK m. 9): Sağlayıcılar metinlerde isimle yazıldı, beşine de standart sözleşme talebi gönderildi ([scc-requests.md](../legal/scc-requests.md)). Kabul eden olursa Türkçe metin imzalanır ve Kurul'a 5 iş günü içinde bildirilir. Türkiye'de barındırma genel yayından önce yeniden değerlendirilecek.
- [ ] **5651 sayılı Kanun:** Trafik kayıtları 1 yıl tutuluyor (ADR 0014). Yer sağlayıcılık bildirimi genel yayından önce yapılacak ([deployment.md](../deployment.md), 11. bölüm).
- [x] Saklama süreleri (araştırmanın 7. bölümü); şikâyetler sonuçlandıktan 2 yıl sonra silinir (MD-12).
- [x] Silinen hesabın mesaj ve değerlendirmeleri "Silinmiş kullanıcı" adıyla kalır; gizlilik politikasında yazıyor.
- [x] VERBİS: Gerçek kişi ve eşiklerin altında olduğu için muaf.
- [x] Kullanım koşulları: `https://exparter.app/kullanim-kosullari.html`; kayıtta kabul edilir, kabul zamanı ve sürümü saklanır (ID-09).
- [x] Aydınlatma metni kayıt ekranında, hesap oluşturulmadan önce bağlantıyla gösteriliyor (açık rıza kutusu yok).

## 8. Uygulamada kalan işler

- **Uygulama içinde gizlilik bağlantısı:** Yapıldı (2026-10-27): kayıt ekranında kullanım koşulları ve aydınlatma metni; üç metnin bağlantısı önce profilde, 1.0.0 (4)'ten beri Ayarlar ekranında.
- **Ekran görüntüleri:** Yapıldı (2026-10-27, 3. bölüm).
