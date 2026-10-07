# Yol Haritası

Fazların ayrıntısı için `docs/PROJECT_BRIEF.md` Bölüm 9'a bak.

## Faz durumu

| Faz | Konu | Durum |
|---|---|---|
| 0 | Kurulum | Tamamlandı (2026-10-06) |
| 1 | Domain ve veri tasarımı | Tamamlandı (2026-10-07) |
| 2 | API sözleşmesi | Başlamadı |
| 3 | Backend iskeleti | Başlamadı |
| 4 | Mobil iskelet | Başlamadı |
| 5 | Dikey dilimler | Başlamadı |
| 6 | Sertleştirme ve MVP yayını | Başlamadı |
| 7 | Web istemcisi | Başlamadı |

## Faz 0 — Kurulum

- Repo yapısı, `.gitignore`, `.gitattributes`, `.editorconfig`, `CLAUDE.md` eklendi.
- `docker-compose.yml` yalnızca PostgreSQL içerir. Host portu `.env` içindeki `POSTGRES_PORT` ile ayarlanır (varsayılan 5432).
- MinIO kapsamdan çıkarıldı. S3 uyumlu nesne depolama ürünü Faz 5.1'de (avatar yükleme) ADR ile seçilecek.
- Kabul kriteri doğrulandı (2026-10-06): `docker compose up -d --wait` ile PostgreSQL `healthy` duruma geldi ve sorgu yanıtladı.

## Faz 1 — Domain ve veri tasarımı

- `docs/domain/`: sözlük, domain modeli, iş kuralları, kararlar ve açık sorular.
- `docs/erd/erd.md`: Mermaid ERD, tablo sahipliği, benzersizlik kısıtları.
- `docs/adr/`: 0001 modüler monolit, 0002 PostgreSQL, 0003 auth stratejisi, 0004 mobil state yönetimi.
- Domain modeli ve ERD 2026-10-07'de onaylandı.
- Faz 2'den önce cevaplanması gereken tek açık soru: reşit olmayan kullanıcılar (`docs/domain/open-questions.md`).

## Geliştirme ortamı

2026-10-06 tarihinde ölçülen sürümler:

| Araç | Sürüm |
|---|---|
| İşletim sistemi | Windows 11 Pro 10.0.26300 |
| .NET SDK | 10.0.301 (ayrıca 10.0.103 ve 9.0.312 kurulu) |
| Flutter | 3.41.2 (stable) |
| Android SDK | 36.1.0 |
| Docker Engine | 29.7.2 (Docker Desktop) |
| Docker Compose | v5.4.0 |
| Git | 2.55.0.windows.3 |
| PostgreSQL imajı | `postgres:18.6-alpine` |

Notlar:

- Bu makinede 5432 portunu yerel PostgreSQL 17 servisi (`postgresql-x64-17`) kullanıyor. Bu yüzden lokal `.env` dosyasında `POSTGRES_PORT=5433` ayarlı.
- `flutter doctor` tek uyarı veriyor: Visual Studio'nun "Desktop development with C++" bileşenleri eksik. Bu yalnızca Windows masaüstü derlemesini etkiler; Android ve web hedefleri sorunsuz.
- `flutter doctor` daha yeni bir Flutter sürümü olduğunu bildiriyor. Güncelleme Faz 4'ün başında yapılacak.
