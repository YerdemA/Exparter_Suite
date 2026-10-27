# ADR 0010: Nesne depolama

- **Durum:** Kabul edildi
- **Tarih:** 2026-10-09

## Bağlam

Avatarlar (ve ileride başka dosyalar) veritabanında değil nesne depolamada tutulur. Brief S3 uyumlu bir depolama ve istemciye presigned URL öngörüyordu; lokal ürün olarak düşünülen MinIO'nun topluluk sürümü 2025'te imaj yayınlamayı bıraktı ve 2026'da arşivlendi, bu yüzden Faz 0'da kapsamdan çıkarıldı.

Değerlendirilen seçenekler: Garage, RustFS, SeaweedFS ve yönetilen servisler (Cloudflare R2, AWS S3).

## Karar

- **Kod belirli bir ürüne değil S3 API'sine yazılır.** Application katmanı yalnızca `IObjectStorage` arayüzünü bilir; Infrastructure'daki `S3ObjectStorage` her S3 uyumlu servisle çalışır.
- **Lokalde ve testlerde Garage** kullanılır (`docker-compose.yml`). Hafiftir; `--single-node --default-bucket` ile ilk açılışta düzenini, erişim anahtarını ve bucket'ı kendisi kurar, elle kurulum gerekmez.
- **Canlı ortamda Cloudflare R2 kullanılır**, bucket AB yetki alanında açılır (ADR 0014). Geçiş yalnızca yapılandırma değişikliğidir (`Storage:*`). (2026-10-14'te güncellendi; ilk karar seçimin Faz 6'ya bırakılmasıydı.)
- **Dosyalar API'den geçmez.** İstemci, API'den aldığı presigned URL'ye doğrudan yükler; okuma da kısa ömürlü presigned URL ile yapılır. Bucket herkese açık değildir.
- **Sınırlar yüklenen dosya üzerinde denetlenir.** Presigned URL yüklenen içeriğin boyutunu sınırlayamaz; istemci yüklemeyi bitirince API dosyanın gerçek boyutuna ve türüne bakar, kurala uymayanı siler.
- **Sahiplik anahtarın önekiyle kanıtlanır.** Avatar anahtarları `avatars/<kullanıcı kimliği>/` altındadır; kullanıcı başka birinin önekindeki dosyayı avatarı yapamaz.

RustFS, MinIO'ya en yakın deneyimi sunuyor ama henüz çok yeni; SeaweedFS avatar depolamak için gereğinden ağır. Garage'ın AGPL lisansı bizi etkilemez: değiştirilmeden, ayrı bir servis olarak çalışır.

## Sonuçlar

- Presigned URL belirli bir sunucu adına imzalanır. API depolamaya `Storage:Endpoint` ile, istemciler `Storage:PublicEndpoint` ile ulaşır; ikisi farklı olabilir (lokalde API `localhost`, Android emülatörü `10.0.2.2` kullanır).
- Avatar adresi her yanıtta değişir (imza ve süre içerir); istemci onu önbellek anahtarı olarak kullanmamalıdır. Görsel önbelleği gerekirse ayrıca ele alınır.
- Yüklenip avatar yapılmayan dosyalar depolamada kalır. Hacim büyürse yaşam döngüsü kuralıyla temizlenir; şimdilik bir şey yapılmıyor.
- `S3ObjectStorageTests`, gerçek Garage'a karşı presigned yükleme ve indirmeyi, yanlış içerik türünün ve imzasız okumanın reddedildiğini doğrular. API entegrasyon testleri ise bellek içi bir depolama kullanır.
- Garage bazı S3 özelliklerini desteklemez (ör. bucket politikaları). Kullandığımız dört işlem (presigned PUT/GET, HEAD, DELETE) v2.4.1 ile doğrulandı.
