# ADR 0003: Kimlik doğrulama stratejisi

- **Durum:** Kabul edildi (2026-10-07)
- **Tarih:** 2026-10-06

## Bağlam

İlk istemci Flutter mobil, ikincisi ileride web olacak. Backend hiçbir istemciye özel auth akışı içermemeli. SignalR bağlantıları da aynı kimlikle doğrulanmalı. MVP'de kayıt yalnızca e-posta ve parola ile yapılır; harici giriş sonradan eklenebilmelidir.

## Karar

**Token modeli**

- Kısa ömürlü JWT access token ve rotasyonlu, opak (rastgele) refresh token.
- Süreler: access token 15 dakika, refresh token 30 gün.
- Access token simetrik anahtarla (HS256) imzalanır. Token'ı üreten ve doğrulayan tek uygulama olduğu için asimetrik anahtar gerekmez.
- Token'lar yanıt gövdesinde döner, cookie kullanılmaz. Web istemcisi geldiğinde gerekirse aynı endpoint'lerin önüne cookie tabanlı bir BFF konur (Faz 7).

**Refresh token**

- Veritabanında yalnızca SHA-256 hash'i saklanır. Token yüksek entropili rastgele değer olduğu için yavaş hash gerekmez.
- Her yenilemede eski token iptal edilir ve yenisi verilir (rotasyon).
- İptal edilmiş bir token yeniden kullanılırsa çalınmış sayılır ve aynı `FamilyId`'ye sahip tüm token'lar iptal edilir.
- Çıkış, o cihazın refresh token'ını iptal eder. Access token süresi dolana kadar geçerli kalır; kısa ömür bu yüzden seçildi.

**Parola**

- ASP.NET Core Identity çatısının tamamı kullanılmaz: `User` zengin bir domain entity'sidir ve refresh token modeli bize aittir.
- Parola hash'i için Application katmanında bir `IPasswordHasher` arayüzü tanımlanır; Domain katmanı hash algoritmasını bilmez.
- Uygulama: Microsoft'un `PasswordHasher` sınıfı (PBKDF2). Ek üçüncü taraf paket gerektirmez ve hash formatı sürümlüdür. Alternatif: Argon2id (daha güçlü, ama üçüncü taraf paket gerektirir).

**Tek kullanımlık token'lar**

- E-posta doğrulama ve parola sıfırlama token'ları rastgele üretilir, hash'lenerek saklanır, tek kullanımlıktır.
- Süreler: doğrulama 24 saat, parola sıfırlama 1 saat.
- Parola sıfırlandığında kullanıcının tüm refresh token'ları iptal edilir.
- Parola sıfırlama isteği, e-postanın kayıtlı olup olmadığını açığa çıkarmaz.

**Yetkilendirme**

- E-posta doğrulama durumu access token'da bir claim olarak taşınır; etkileşim endpoint'leri bu claim'i bir policy ile zorunlu kılar.
- Doğrulama tamamlandığında istemci token'ını yeniler; böylece claim güncellenir.

## Sonuçlar

- Access token iptal edilemez; askıya alınan bir kullanıcı en fazla 15 dakika daha erişebilir. Kabul edilen bir ödünleşimdir.
- Rotasyon, istemcide eşzamanlı yenileme isteklerini sıraya koymayı gerektirir (mobilde tek bir yenileme interceptor'ı, Faz 4).
- E-posta gönderimi için bir sağlayıcı gerekir. Lokalde yakalayıcı bir SMTP sunucusu kullanılır; gerçek sağlayıcı Faz 3'te ayrı bir ADR ile seçilir.
- Harici giriş eklendiğinde `User.PasswordHash` boş olabilir hale gelir ve ayrı bir harici kimlik tablosu eklenir; mevcut token modeli değişmez.
