# API Standartları

`contracts/openapi.yaml` tek doğruluk kaynağıdır. Bu belge, sözleşmedeki ortak kuralları ve sözleşmenin tek başına ifade edemediği ayrıntıları (hata kodları gibi) açıklar.

## Genel

- **Sürüm:** URL ile, `/api/v1/...`. Geriye dönük uyumu bozan değişiklik yeni sürüm gerektirir; alan veya endpoint eklemek gerektirmez.
- **Biçim:** İstek ve yanıtlar JSON'dur. Alan adları `camelCase`, enum değerleri `PascalCase` yazılır.
- **Kimlikler:** Tüm kimlikler UUID'dir.
- **Zaman:** Tüm zamanlar ISO 8601 biçiminde ve UTC'dir. Sunucu UTC'yi `+00:00` ofsetiyle yazar (`2026-10-07T14:30:00+00:00`); isteklerde `Z` de kabul edilir. İstemci yerel saate kendisi çevirir.
- **Boş değerler:** İsteğe bağlı alanlar yanıtta `null` olarak döner, atlanmaz.
- **Kimlik doğrulama:** `Authorization: Bearer <access token>`. Herkese açık endpoint'ler sözleşmede `security: []` ile işaretlidir.
- **Durum değiştiren işlemler:** Kabul, ret, iptal gibi geçişler kaynağın altında `POST` ile yapılır (`/swap-requests/{id}/accept`) ve kaynağın güncel halini döner.

## Sayfalama

Liste endpoint'lerinin hepsi sayfalıdır. İki istisna, sabit ve küçük başvuru listeleridir: `/cities` (81 il) ve `/skill-categories`.

| Tür | Parametreler | Yanıt | Kullanıldığı yer |
|---|---|---|---|
| Sayfa tabanlı | `page` (1'den başlar), `pageSize` (1–50, varsayılan 20) | `items`, `page`, `pageSize`, `totalCount` | Varsayılan |
| Cursor tabanlı | `cursor`, `limit` (1–50, varsayılan 20) | `items`, `nextCursor` | Keşfet, mesajlar |

`nextCursor` istemci için anlamsız bir değerdir; olduğu gibi geri gönderilir. `null` ise başka kayıt yoktur.

## Hata biçimi

Tüm hatalar RFC 9457 ProblemDetails biçimindedir ve `application/problem+json` ile döner:

```json
{
  "type": "https://exparter.app/problems/swap-request-already-pending",
  "title": "A pending swap request already exists.",
  "status": 409,
  "code": "swap_request_already_pending",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00"
}
```

- `code`, hatayı tanımlayan kararlı bir değerdir. İstemci kullanıcıya gösterilecek Türkçe mesajı `code` üzerinden seçer; `title` ve `detail` İngilizcedir ve kullanıcıya gösterilmez.
- Doğrulama hatalarında `errors` alanı, alan adına göre hata listesi taşır.

| Durum | Anlamı |
|---|---|
| 400 | İstek hatalı veya doğrulamadan geçmedi (`validation_failed`). |
| 401 | Kimlik bilgisi yok, geçersiz veya süresi dolmuş. |
| 403 | Çağıranın bu işleme yetkisi yok. |
| 404 | Kaynak yok veya çağırana görünür değil. |
| 409 | İstek mevcut durumla çelişiyor veya bir iş kuralını bozuyor. |

Bir kullanıcının görmemesi gereken kaynak için 403 değil 404 döner; böylece kaynağın varlığı açığa çıkmaz.

## Hata kodları

Liste Faz 3 ve Faz 5'te uygulama sırasında genişler; her yeni kod buraya eklenir.

| Kod | Durum | Açıklama |
|---|---|---|
| `validation_failed` | 400 | Doğrulama hatası; ayrıntılar `errors` alanında. |
| `invalid_token` | 400 | Tek kullanımlık token geçersiz, kullanılmış veya süresi dolmuş. |
| `avatar_not_uploaded` | 400 | Verilen anahtar altında yüklenmiş dosya yok. |
| `avatar_invalid` | 400 | Yüklenen dosya izin verilen bir görsel değil veya 5 MB'tan büyük. |
| `invalid_password` | 400 | Hesap silme gibi parola onayı isteyen işlemde parola hatalı. |
| `invalid_credentials` | 401 | E-posta/kullanıcı adı veya parola hatalı. |
| `unauthorized` | 401 | Access token yok veya geçersiz. |
| `token_expired` | 401 | Access token'ın süresi dolmuş; istemci yenilemelidir. |
| `refresh_token_invalid` | 401 | Refresh token geçersiz, iptal edilmiş veya süresi dolmuş; kullanıcı yeniden giriş yapmalıdır. |
| `account_suspended` | 403 | Hesap askıya alınmış. |
| `email_not_verified` | 403 | İşlem doğrulanmış e-posta gerektiriyor (yetenek önerisi). |
| `interaction_requirements_not_met` | 403 | Etkileşim için e-posta doğrulaması, tamamlanmış profil ve en az bir sunulan yetenek gerekiyor. Eksikler `GET /me/interaction-requirements` ile öğrenilir. |
| `admin_required` | 403 | İşlem admin yetkisi gerektiriyor. |
| `not_participant` | 403 | Çağıran bu işlemi yapabilecek taraf değil (ör. isteği gönderen kabul edemez). |
| `not_found` | 404 | Kaynak veya adres bulunamadı. |
| `email_already_registered` | 409 | E-posta kullanımda. |
| `email_recently_deleted` | 409 | Bu e-postaya ait hesap kalıcı olarak silindi; adres 3 ay boyunca yeniden kaydedilemez. |
| `username_already_taken` | 409 | Kullanıcı adı kullanımda. |
| `username_reserved` | 409 | Kullanıcı adı ayrılmış bir ad (ör. `admin`). |
| `username_change_too_soon` | 409 | Kullanıcı adı son 7 gün içinde değiştirilmiş. |
| `invalid_username` | 409 | Kullanıcı adı biçim kurallarına uymuyor. (Normalde doğrulama aşamasında 400 ile yakalanır.) |
| `email_recently_deleted` | 409 | Bu e-postaya ait hesap yakın zamanda silindi; bekleme süresi dolmadı. |
| `email_already_verified` | 409 | E-posta zaten doğrulanmış. |
| `skill_already_exists` | 409 | Aynı ada sahip yetenek katalogda veya öneriler arasında var. |
| `skill_not_approved` | 409 | Yetenek henüz onaylanmamış. |
| `user_skill_already_exists` | 409 | Yetenek bu tipte zaten ekli. |
| `wanted_level_not_higher` | 409 | İstenen seviye, aynı yeteneğin sunulan seviyesinden yüksek olmalı. |
| `skill_limit_reached` | 409 | Bu tipte eklenebilecek en fazla yetenek sayısına (5) ulaşıldı. |
| `user_skill_in_use` | 409 | Yetenek bekleyen bir istekte veya aktif eşleşmede kullanılıyor. |
| `swap_request_to_self` | 409 | Kullanıcı kendine istek gönderemez. |
| `skill_not_offered` | 409 | Seçilen yetenek ilgili kullanıcının sunduğu yetenekler arasında değil. |
| `swap_request_already_pending` | 409 | İki kullanıcı arasında bekleyen bir istek var. |
| `swap_request_cooldown` | 409 | Alıcı, gönderenin önceki isteğini reddetti veya cevaplamadı; yeni istek için 7 gün beklenmeli. |
| `match_already_exists` | 409 | İki kullanıcı arasında sona ermemiş bir eşleşme var. |
| `user_blocked` | 409 | Taraflardan biri diğerini engellemiş. |
| `invalid_state` | 409 | Kaynağın mevcut durumu bu geçişe izin vermiyor. |
| `match_end_pending` | 409 | Bekleyen sonlandırma isteği varken yeni oturum planlanamaz. |
| `conversation_closed` | 409 | Sohbet kapalı. |
| `conversation_has_active_match` | 409 | Aktif eşleşme varken sohbet kapatılamaz. |
| `session_not_started` | 409 | Oturum, başlangıç zamanından önce tamamlandı işaretlenemez. |
| `session_in_past` | 409 | Oturum tarihi gelecekte olmalı. |
| `in_person_already_reported` | 409 | İki kullanıcı arasında iptal edilmemiş bir yüz yüze buluşma bildirimi zaten var. |
| `review_already_exists` | 409 | Bu kullanıcı daha önce değerlendirilmiş. |
| `session_not_completed` | 409 | Değerlendirme için tamamlanmış oturum gerekir. |
| `review_of_self` | 409 | Kullanıcı kendini değerlendiremez. |
| `already_blocked` | 409 | Kullanıcı zaten engellenmiş. |
| `age_not_confirmed` | 409 | 18 yaş onayı verilmemiş. (Normalde doğrulama aşamasında 400 ile yakalanır.) |
| `concurrent_update` | 409 | Kaynak aynı anda başka bir istekle değişti; istemci yeniden deneyebilir. |
| `internal_error` | 500 | Beklenmeyen sunucu hatası. |

## Lint

Sözleşme Redocly CLI ile denetlenir (bkz. ADR 0005):

```powershell
npx --yes @redocly/cli@2.60.0 lint
```

Kurallar kökteki `redocly.yaml` dosyasındadır. `.redocly.lint-ignore.yaml`, bilinçli olarak yok sayılan iki bulguyu içerir: parametresiz ve herkese açık `/cities` ile `/skill-categories` endpoint'lerinin anlamlı bir 4xx yanıtı yoktur.
