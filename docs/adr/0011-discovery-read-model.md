# ADR 0011: Keşfet için modüller arası okuma modeli

- **Durum:** Kabul edildi
- **Tarih:** 2026-10-13

## Bağlam

Keşfet, başka kullanıcıları izleyen kullanıcıya göre sıralar: karşılıklı uyum, tek yönlü uyum, tercih uyumu, yenilik. Bunun için üç modülün verisi birlikte gerekir: hesap durumu ve e-posta doğrulaması (`Identity`), profil ve tercihler (`Profiles`), sunulan ve istenen yetenekler (`Skills`).

Modüller birbirinin tablolarına erişmez (ADR 0006). Bu kuralı keşfette harfiyen uygulamak, her modülden veriyi ayrı ayrı çekip bellekte birleştirmek demektir. Sıralama ve sayfalama ise tüm adaylar üzerinde yapılır; kullanıcı sayısı arttıkça her istekte herkesi belleğe almak gerekir.

## Karar

- **Keşfet tek bir SQL sorgusudur.** `IDiscoveryReadModel` (Application/Matching) sözleşmesini Infrastructure'daki `DiscoveryReadModel` uygular; filtreleme, uyum hesabı, sıralama ve sayfalama veritabanında yapılır.
- **Bu, ADR 0006'daki kuralın bilinçli ve tek istisnasıdır.** İstisna yalnızca okuma içindir: sorgu hiçbir tabloya yazmaz, başka modülün entity'sini döndürmez, kendi düz kayıtlarını (`DiscoveredUser`) döndürür. Yazma tarafında kural aynen geçerlidir.
- **Etkileşim koşulu sorguda yinelenir.** "Öneri olarak gösterilebilir" koşulu `IInteractionRequirements` ile aynı üç şartı SQL olarak içerir. İkisinin birlikte değişmesi gerekir; entegrasyon testleri (`DiscoveryApiTests`) her şartı ayrı ayrı sınar.
- **Cursor şimdilik sıradaki konumu taşır.** İstemci için anlamsız bir değerdir (bkz. `docs/api-conventions.md`), dolayısıyla ileride sözleşme değişmeden anahtar tabanlı (keyset) sayfalamaya çevrilebilir.
- **Arama, metni katlayarak yapılır.** Arama metni ve görünen ad küçük harfe ve Türkçe karaktersiz biçime indirgenir (`SearchText.Fold` ve SQL'de `translate`); "IŞIK", "ışık" ve "isik" aynı sonucu verir.
- **`SkillLevel` modüller arası sözleşmeye eklendi.** Keşfet sonucu yetenek seviyesini taşıdığı için `Matching` bu enum'u kullanır.

## Sonuçlar

- Bir modülün tablo veya sütun adı değişirse bu sorgu derleme zamanında değil çalışma zamanında bozulur. Sorgu gerçek PostgreSQL'e karşı entegrasyon testleriyle korunur.
- Konuma dayalı cursor, sayfalar arasında liste değişirse (yeni kullanıcı, güncellenen profil) bir kaydı yineleyebilir veya atlayabilir. Keşfet bir öneri listesi olduğu için MVP'de kabul edilebilir; istemci yinelenen kullanıcıyı eler.
- 10 000 kullanıcıda süzgeçsiz keşfet tek başına yaklaşık 145 ms sürer (2026-10-14, dilim 6/4). PostgreSQL bu sorgunun maliyetini yüksek tahmin ettiği için JIT derlemesini tetikliyordu; API bağlantılarında JIT kapalıdır. Kullanıcı sayısı onlarca bine çıktığında sıralama önceden hesaplanmalıdır.
- Görünen ad araması dizin kullanmaz. Kullanıcı sayısı büyüdüğünde `pg_trgm` dizini veya ayrı bir arama sütunu eklenir.
- Puan (`Reviews`, dilim 7) ve engelleme (`Moderation`, Faz 6 dilim 2) aynı sorguya eklendi.
- Tercih uyumu basamağı ve mod süzgeci sorgudan çıkarıldı (2026-11-12, Faz 8 dilim 11): takas tercihi artık yok, bütün takaslar çevrimiçi.
