# Terimler Sözlüğü

Arayüzde Türkçe terim, kodda İngilizce ad kullanılır. Bu tablo ikisi arasındaki tek eşleşme kaynağıdır.

| Türkçe (arayüz) | Kod adı | Tanım |
|---|---|---|
| Kullanıcı | `User` | Kimlik kaydı: e-posta, parola hash'i, durum. Kişisel bilgi taşımaz. |
| Kullanıcı adı | `Username` | Kullanıcının benzersiz, herkese görünen adı. Girişte e-posta yerine kullanılabilir. |
| E-posta doğrulaması | `EmailVerifiedAt` | Kullanıcının e-postasını doğruladığı an. Doğrulanmamış kullanıcı başkalarıyla etkileşime giremez. |
| Profil | `Profile` | Kullanıcının herkese görünen yüzü: ad, biyografi, avatar, şehir, takas tercihi. |
| Çevrimiçi takas, Yüz yüze takas | `AcceptsOnline`, `AcceptsInPerson` | Profildeki takas tercihi; en az biri seçilir. Yüz yüze takasta şehir zorunludur. |
| Kategori | `SkillCategory` | Yeteneklerin gruplandığı başlık (ör. Müzik, Dil). |
| Yetenek | `Skill` | Katalogdaki tek bir yetenek (ör. Gitar). |
| Yetenek önerisi | `Skill` (`PendingApproval`) | Kullanıcının katalogda olmayan bir yeteneği önermesi. Admin onaylayana kadar katalogda görünmez. |
| Sunulan yetenek | `UserSkill` (`Offered`) | Kullanıcının öğretebildiği yetenek. Seviye, kullanıcının kendi seviyesidir. |
| İstenen yetenek | `UserSkill` (`Wanted`) | Kullanıcının öğrenmek istediği yetenek. Seviye, aradığı öğreticinin en düşük seviyesidir. |
| Seviye | `SkillLevel` | `Beginner`, `Intermediate`, `Advanced`, `Expert`. |
| Karşılıklı uyum | mutual compatibility | A'nın sunduğu B'nin istediği, B'nin sunduğu A'nın istediği olduğunda oluşan durum. |
| Keşfet | discovery | Diğer kullanıcıların uyuma göre sıralı listesi. |
| Takas isteği | `SwapRequest` | Bir kullanıcının diğerine belirli bir yetenek çifti için yaptığı teklif. |
| İsteği gönderen | requester | Takas isteğini oluşturan kullanıcı. |
| İsteği alan | recipient | Takas isteğinin gönderildiği kullanıcı. |
| Geri çekme | `Cancelled` | Gönderenin bekleyen isteğini iptal etmesi. |
| Süre aşımı | `Expired` | İsteğin 3 gün içinde cevaplanmadığı için düşmesi. |
| Eşleşme | `Match` | Kabul edilmiş takas isteğinden doğan, iki kullanıcı arasındaki takas ilişkisi. |
| Eşleşmeyi sonlandırma | match ending | İki tarafın karşılıklı onayıyla eşleşmenin kapanması. |
| Sohbet | `Conversation` | İki kullanıcı arasındaki tek mesajlaşma kanalı. İlk eşleşmede açılır. |
| Mesaj | `Message` | Sohbetteki tek bir metin mesajı. |
| Takas oturumu | `SwapSession` | Eşleşen iki kullanıcının planladığı tek bir buluşma (tarih, süre, biçim). |
| Oturum biçimi | `SessionMode` | `Online` veya `InPerson` (yüz yüze). |
| Yüz yüze buluşma bildirimi | `SwapSession` (`InPerson`) | Uygulama dışında ayarlanan buluşmanın sonradan "Yüz yüze buluştuk" diye bildirilmesi; doğrudan onay bekler. |
| Tamamlama onayı | completion confirmation | Bir tarafın oturumu tamamlandı işaretlemesi ve diğerinin bunu onaylaması. |
| Görüntülü görüşme | call | Kabul edilmiş çevrimiçi oturumun uygulama içindeki görüşmesi (LiveKit odası, oda adı oturum kimliği). |
| Görüşme penceresi | `CallOpensAt`, `CallClosesAt` | Görüşmeye katılınabilen aralık: başlangıçtan 10 dakika önce ile süre bittikten 15 dakika sonrası. |
| Ekran paylaşımı | screen share | Görüşmede kullanıcının ekran görüntüsünü karşı tarafa göndermesi; cihazın sesi gitmez. |
| Değerlendirme | `Review` | Bir kullanıcının diğerine verdiği 1–10 puan ve yorum. |
| Bildirim | `Notification` | Kullanıcıya uygulama içinde gösterilen olay kaydı. |
| Cihaz | `Device` | Push bildirim göndermek için saklanan FCM token'ı. |
| Engelleme | `Block` | Bir kullanıcının diğeriyle tüm etkileşimi kesmesi. |
| Şikâyet | `Report` | Bir kullanıcının, bir kullanıcıyı veya içeriği moderasyona bildirmesi. |
| Yönetici | admin | Yetenek önerilerini onaylayan, şikâyetleri ve iptal sebeplerini inceleyen rol. |
