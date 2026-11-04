# ADR 0016: Görüntülü görüşmede uçtan uca şifreleme

- **Durum:** Kabul edildi
- **Tarih:** 2026-11-04

## Bağlam

WebRTC sesi ve görüntüyü her cihazla LiveKit sunucusu arasında zorunlu olarak şifreler (DTLS-SRTP), ama sunucu paketleri iletebilmek için bu şifreyi çözer (ADR 0015). Kullanıcı kararıyla (2026-11-04) görüşmenin içeriği yalnızca iki cihazda çözülebilmeli. Mesajlaşmaya uçtan uca şifreleme eklenmez: şikâyet edilen mesajı yöneticinin görebilmesi, web istemcisi ve birden çok cihaz bunu zorlaştırır.

Değerlendirilen seçenekler:

- **Anahtarı backend dağıtır** (görüşme anahtarıyla birlikte ortak bir parola): en kolayı, ama anahtarı sunucu bildiği için uçtan uca şifreleme sayılmaz.
- **Cihazlar anahtarı kendi aralarında üretir:** sunucu yalnızca açık anahtarları taşır, ortak anahtarı bilemez. Seçilen budur.

## Karar

- **Anahtar değişimi:** Her cihaz görüşme ekranına her girişte yeni bir X25519 anahtar çifti üretir ve açık anahtarını odadaki kendi katılımcı özelliği (participant attribute) olarak yayımlar. Veri kanalı kullanılmaz: şifreleme açıkken LiveKit veri mesajlarını da henüz anlaşılmamış anahtarla şifreler, karşı taraf çözemez (emülatör denemesinde görüldü). Özellikler sinyal bağlantısıyla gider ve sonradan giren taraf mevcut anahtarı hemen bulur. Ortak sır X25519 ile hesaplanır, HKDF-SHA256 ile (tuz: oturum kimliği, bilgi: `exparter call e2ee v1`) 32 baytlık görüşme anahtarına dönüştürülür. Gizli anahtar ve görüşme anahtarı bellekten çıkmaz, görüşme bitince atılır. Kriptografi `cryptography` paketiyle yapılır (saf Dart; tek bir anahtar değişimi için yeterince hızlı).
- **Şifreleme:** LiveKit'in yerleşik kare şifrelemesi (AES-GCM, ortak anahtar kipi) kamera, mikrofon ve ekran paylaşımını şifreler. Anahtar anlaşılmadan önce de cihaz rastgele, kimseyle paylaşılmayan bir anahtar kullanır ve hazır olmayan şifreleyici kareleri atar; böylece şifresiz tek kare bile çıkmaz.
- **Yeniden bağlanma:** Açık anahtar odada kaldığı için mesaj kaybı olmaz. Karşı taraf ayrılınca anlaşılan anahtar unutulur; geri gelen taraf yeni bir anahtar çiftiyle gelir ve iki cihaz yeniden anlaşır.
- **Güvenlik kodu:** İki açık anahtarın sıralanmış birleşiminin SHA-256 özetinden 6 haneli bir kod çıkar; iki cihazda aynıdır. Taraflar kodu sesli karşılaştırırsa sunucunun açık anahtarları değiştirip araya girmediğini anlar. Karşılaştırma isteğe bağlıdır (kullanıcı kararı, 2026-11-04).
- **Şifresiz görüşme yoktur** (kullanıcı kararı, 2026-11-04). Şifreleme kurulamazsa görüşme açılmaz ("Görüşme güvenli biçimde başlatılamadı"). Karşı taraf birkaç saniye içinde anahtar göndermezse eski bir sürüm kullanıyordur; görüşme şifresiz sürmez, uygulamasını güncellemesi gerektiği söylenir.
- **Backend:** Görüşme anahtarı artık katılımcının kendi özelliklerini değiştirmesine izin verir (`canUpdateOwnMetadata`); veri kanalı kapalı kalır. Sunucu açık anahtarları görür ama saklamaz.

## Sonuçlar

- Sunucu görüşmenin içeriğini göremez; kimin kiminle, ne zaman ve ne kadar görüştüğünü ve IP adreslerini yine bilir (kaydedilmez).
- Kod karşılaştırılmazsa, ele geçirilmiş bir sunucu açık anahtarları değiştirerek araya girebilir; kod bunu fark ettirmek için vardır.
- 1.0.0 (7) ve öncesindeki uygulamalar şifreli kareleri çözemez; yeni sürümle görüşen eski sürüm karşı tarafı siyah görür. Uygulama içi güncelleme (Faz 6) bu dönemi kısaltır.
- Web istemcisi (Faz 7) LiveKit'in web SDK'sındaki aynı ortak anahtar kipini ve varsayılan ayarları (`LKFrameEncryptionKey` tuzu) kullanmalı, açık anahtarı aynı özellik adı (`exparter.call-key`) ve mesaj biçimiyle yayımlamalıdır.
- Sunucu kareleri çözemediği için kayıt, yapay zekâ özetleri gibi sunucu taraflı özellikler bu görüşmelerde yapılamaz.
