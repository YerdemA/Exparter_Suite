# ADR 0006: Backend proje yapısı ve veritabanı düzeni

- **Durum:** Kabul edildi
- **Tarih:** 2026-10-07

## Bağlam

ADR 0001 modüler monoliti seçti ama iki noktayı Faz 3'e bıraktı: modüllerin projelere nasıl yerleşeceği ve veritabanının modüller arasında nasıl bölüneceği. Brief dört katman projesi öngörüyor (`Domain`, `Application`, `Infrastructure`, `Api`).

## Karar

**Modüller katman projelerinin içinde klasördür.** Her modül dört projede aynı adlı klasörde yaşar (`Exparter.Domain/Identity`, `Exparter.Application/Identity`, `Exparter.Infrastructure/Persistence/Identity`, `Exparter.Api/Identity`). Modül başına ayrı proje açılmaz; dokuz modül için otuzu aşkın proje, tek geliştiricili bir projede karşılığı olmayan bir yüktür.

**Tek `DbContext`, modül başına ayrı PostgreSQL şeması.** Tablolar modülün şemasında durur (`identity.users`). Tek transaction ve tek migration geçmişi kalır. Modüller arası ilişkiler veritabanında da yalnızca kimlik sütunudur; farklı şemalar arasında foreign key tanımlanmaz.

**Katman kuralları:**

- Domain yalnızca .NET temel kütüphanesine bağımlıdır. İş kuralı ihlali `DomainException` fırlatır; hata kodu istisnanın içindedir.
- Application, repository ve servis arayüzlerini tanımlar; EF Core'a veya ASP.NET Core'a bağımlı değildir. Beklenen hatalar `AppException` ile bildirilir. Doğrulama, Mediator hattındaki `ValidationBehavior` içinde FluentValidation ile yapılır.
- Infrastructure arayüzleri uygular. `AppDbContext` aynı zamanda `IUnitOfWork`'tür.
- Api yalnızca HTTP ile komut/sorgu arasında çeviri yapar. Tüm hatalar tek bir exception handler'da ProblemDetails'e dönüşür.

**Zaman:** Kod saati `TimeProvider` üzerinden okur; testler saati bu yolla ileri alır.

## Sonuçlar

- Modül sınırını derleyici korumaz. Katman bağımlılıkları testle denetlenir (`ArchitectureTests`). Modüller arası sınır testi ikinci modül geldiğinde (Faz 5) eklenir; tek modülle denetleyecek bir şey yoktur.
- Bir modül ileride ayrı servise çıkarılırsa şeması ve klasörleri birlikte taşınır.
- Sözleşme ile uygulama arasındaki uyum, entegrasyon testlerindeki `ContractConformanceTests` ile rota düzeyinde denetlenir: uygulanan her rota sözleşmede olmalı, uygulanmış modüllerin sözleşmedeki her operasyonunun rotası olmalı. İstek ve yanıt şemaları bu testle karşılaştırılmaz.
