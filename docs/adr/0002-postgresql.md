# ADR 0002: PostgreSQL

- **Durum:** Kabul edildi
- **Tarih:** 2026-10-06

## Bağlam

Veri modeli ilişkiseldir: kullanıcılar, yetenekler, istekler, eşleşmeler ve bunlar arasındaki bütünlük kuralları. Bazı iş kuralları eşzamanlı isteklere karşı yalnızca veritabanı kısıtıyla güvenle korunabilir (ör. iki kullanıcı arasında aynı anda tek bekleyen istek).

## Karar

Tek veritabanı olarak PostgreSQL, erişim için EF Core (Npgsql sağlayıcısı). Şema yalnızca migration'larla değişir.

Seçimi belirleyen özellikler:

- **Kısmi ve ifade tabanlı benzersiz index'ler:** "Yalnızca `Pending` satırlarda, sırasız kullanıcı çifti benzersiz" gibi kuralları doğrudan ifade eder.
- **`jsonb`:** Türe göre değişen veriler için ayrı tablo gerektirmez. (Uygulamada gerekmedi: bildirimler türlü sütunlarla saklanıyor, 2026-11-03.)
- **Metin arama:** Yetenek ve kullanıcı araması için ayrı bir arama motoru gerektirmeden yeterlidir.
- Açık kaynak, lisans maliyeti yok, yönetilen servis olarak her bulutta var.

Lokal geliştirmede ve entegrasyon testlerinde (Testcontainers) aynı PostgreSQL ana sürümü kullanılır. Testlerde in-memory sağlayıcı veya SQLite kullanılmaz; kısmi index gibi PostgreSQL'e özgü davranışlar ancak gerçek veritabanıyla doğrulanır.

## Sonuçlar

- İş kurallarının bir kısmı PostgreSQL'e özgü özelliklere dayanır; başka bir veritabanına geçiş bu kuralların yeniden yazılmasını gerektirir. Kabul edilen bir bağımlılıktır.
- Entegrasyon testleri Docker gerektirir ve in-memory testlerden yavaştır.
- Tüm zamanlar UTC olarak `timestamptz` tipinde saklanır.
