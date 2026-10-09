# ADR 0009: E-postalardaki uygulama bağlantıları

- **Durum:** Kabul edildi
- **Tarih:** 2026-10-09

## Bağlam

E-posta doğrulama ve parola sıfırlama, kullanıcının e-postadaki bir bağlantıya dokunmasıyla tamamlanır. Bağlantı mobil uygulamayı açmalı ve tek kullanımlık token'ı ilgili ekrana taşımalıdır. İki yol var: uygulamaya özel bir URL şeması veya alan adına bağlı `https://` bağlantıları (Android App Links / iOS Universal Links).

## Karar

- MVP'de özel şema kullanılır: `exparter://app/<yol>?token=<token>`.
  - `exparter://app/verify-email?token=...`
  - `exparter://app/reset-password?token=...`
- `app` kısmı sabit bir "host"tur; böylece bağlantının yolu (`/verify-email`) uygulamadaki rota ile birebir aynıdır ve yönlendirici ek çeviri yapmadan açar.
- Bağlantı adresleri backend yapılandırmasındadır (`Email:EmailVerificationUrl`, `Email:PasswordResetUrl`); mobil taraftaki rotalar `AppRoutes` içinde tanımlıdır. İkisi birlikte değişmelidir.
- Bu iki rota oturum durumundan bağımsız açılır: kullanıcı giriş yapmış da olabilir, yapmamış da. Uygulama kapalıyken bağlantıyla açılırsa açılış ekranına takılmadan doğrudan ilgili ekrana gider.

`https://` bağlantıları, bir alan adı ve o alan adında yayınlanan doğrulama dosyaları gerektirir; alan adı olduğunda eklenir. Geçiş yalnızca yapılandırma ve platform ayarı değişikliğidir, ekranlar aynı kalır.

## Sonuçlar

- Uygulama yüklü değilse bağlantı hiçbir şey açmaz ve bazı e-posta istemcileri özel şemalı bağlantıları tıklanabilir göstermez. MVP'nin kapalı testinde kabul edilebilir; genel yayından önce `https://` bağlantılarına geçilmelidir.
- Özel şemayı başka bir uygulama da kaydedebilir. Token tek kullanımlık ve kısa ömürlü olduğu için risk sınırlıdır, ama bu da `https://` bağlantılarına geçmek için bir nedendir.
- iOS tarafında şema `Info.plist` içine kaydedildi ama denenmedi; iOS doğrulaması MVP sonrasına bırakıldı.
- Android'de 2026-10-09'da doğrulandı: e-postadaki gerçek bağlantı, kapalı uygulamayı açıp e-postayı doğruladı.
