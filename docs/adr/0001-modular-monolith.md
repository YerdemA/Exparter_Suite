# ADR 0001: Modüler monolit

- **Durum:** Kabul edildi
- **Tarih:** 2026-10-06 (Moderation modülü 2026-10-07'de eklendi)

## Bağlam

Exparter tek geliştiriciyle başlayan bir üründür. Dokuz ayrı iş alanı var (kimlik, profil, yetenek, eşleşme, mesajlaşma, oturum, değerlendirme, bildirim, moderasyon) ve bunların sınırlarının zamanla bulanıklaşması en büyük bakım riskidir. Öte yandan dağıtık sistem işletmek (servisler arası ağ, dağıtık transaction, ayrı deploy hatları) bu ölçekte karşılığı olmayan bir maliyettir.

## Karar

Tek deploy edilen bir ASP.NET Core uygulaması, içinde sınırları net modüller: `Identity`, `Profiles`, `Skills`, `Matching`, `Messaging`, `Sessions`, `Reviews`, `Notifications`, `Moderation`.

- Her tablo tek bir modüle aittir. Bir modül başka modülün tablolarını sorgulamaz.
- Modüller arası iletişim iki yolla olur: senkron ihtiyaçlar için karşı modülün uygulama servisi arayüzü, yan etkiler için domain event'leri.
- Modüller arası referanslar yalnızca kimliktir; navigation property kullanılmaz.
- Mikroservis yok, event sourcing yok.

Domain event'leri süreç içinde (in-process) yayınlanır. Kalıcı mesaj kuyruğu veya outbox şimdilik eklenmez.

## Sonuçlar

- Tek transaction, tek deploy, tek veritabanı: geliştirme ve işletme basit kalır.
- Modül sınırları derleyici tarafından değil, kural ve gözden geçirmeyle korunur. Sınır ihlalini yakalayan mimari testleri var: katman bağımlılıkları Faz 3'ten, modüller arası sınır Faz 5 dilim 1'den beri denetlenir.
- Süreç içi event'ler, dinleyici hata verirse kaybolabilir. Bildirim gibi kaybı tolere edilebilir yan etkiler için kabul edilebilir; kritik bir akışta sorun çıkarsa outbox ayrı bir ADR ile eklenir.
- Bir modül ileride ayrı servise çıkarılmak istenirse, tablo sahipliği ve kimlik referansları bunu mümkün kılar.

## Açık noktalar

- **Veritabanı düzeni:** ADR 0006 ile karara bağlandı: tek `DbContext`, modül başına ayrı PostgreSQL şeması.
