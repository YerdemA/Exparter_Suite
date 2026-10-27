# ADR 0007: E-posta gönderimi

- **Durum:** Kabul edildi
- **Tarih:** 2026-10-07

## Bağlam

E-posta doğrulama ve parola sıfırlama e-posta gönderimi gerektirir. Geliştirme sırasında gerçek adreslere e-posta gitmemeli; canlı ortamdaki sağlayıcı ise henüz seçilmedi ve seçimi kodu etkilememeli.

## Karar

- Uygulama e-postayı **SMTP** ile gönderir (MailKit). SMTP her sağlayıcının desteklediği ortak protokoldür; sağlayıcı değiştirmek yalnızca yapılandırma değişikliğidir.
- Geliştirmede `docker-compose.yml` içindeki **Mailpit** tüm e-postaları yakalar; arayüzü `http://localhost:8025` adresindedir.
- Canlı ortamda **Brevo** kullanılır (`smtp-relay.brevo.com`, 587, STARTTLS); gönderici alan adı SPF, DKIM ve DMARC kayıtlarıyla doğrulanır (ADR 0014, `docs/deployment.md`). (2026-10-14'te güncellendi; ilk karar seçimin Faz 6'ya bırakılmasıydı.)
- Gönderim "elinden geleni yapar": hata loglanır ama isteği başarısız kılmaz. Kayıt, e-posta sunucusu çalışmadığı için reddedilmemeli; kullanıcı yeni doğrulama e-postası isteyebilir.
- E-postadaki bağlantının adresi yapılandırmadan gelir (`Email:EmailVerificationUrl`, `Email:PasswordResetUrl`); `{token}` yer tutucusu token ile değiştirilir.

## Sonuçlar

- E-posta, isteğin içinde eşzamanlı gönderilir. Gönderim yavaşlarsa veya yeniden deneme gerekirse arka plan kuyruğu ayrı bir ADR ile eklenir.
- Bağlantı `exparter://app/...` biçiminde bir uygulama bağlantısıdır (bkz. ADR 0009).
- Metinler Türkçe ve kodun içindedir. Çoklu dil desteği gelirse şablonlar ayrıştırılır.
