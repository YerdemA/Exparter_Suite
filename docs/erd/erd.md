# ERD

Durum: **Taslak, onay bekliyor.** Alan açıklamaları için `docs/domain/domain-model.md` dosyasına bak.

Bu şema mantıksal modeldir. Tablo ve sütun adları, tipler ve index'ler Faz 3'te EF Core migration'larıyla kesinleşir.

Farklı modüllerdeki tablolar arasındaki ilişkiler yalnızca kimlik referansıdır; kodda navigation property yoktur. Bu ilişkiler diyagramda kesikli çizgiyle gösterilir.

## Tablo sahipliği

| Modül | Tablolar |
|---|---|
| Identity | `USER`, `REFRESH_TOKEN`, `ONE_TIME_TOKEN` |
| Profiles | `PROFILE` |
| Skills | `SKILL_CATEGORY`, `SKILL`, `USER_SKILL` |
| Matching | `SWAP_REQUEST`, `MATCH` |
| Messaging | `CONVERSATION`, `CONVERSATION_PARTICIPANT`, `MESSAGE` |
| Sessions | `SWAP_SESSION` |
| Reviews | `REVIEW`, `USER_RATING_SUMMARY` |
| Notifications | `NOTIFICATION`, `DEVICE` |
| Moderation (onay bekliyor) | `BLOCK`, `REPORT` |

## Diyagram

```mermaid
erDiagram
    USER ||--o{ REFRESH_TOKEN : "has"
    USER ||--o{ ONE_TIME_TOKEN : "has"
    USER ||..|| PROFILE : "has"
    USER ||..o{ USER_SKILL : "lists"
    USER ||..o{ SWAP_REQUEST : "sends"
    USER ||..o{ SWAP_REQUEST : "receives"
    USER ||..o{ MATCH : "participates"
    USER ||..o{ MESSAGE : "sends"
    USER ||..o{ REVIEW : "writes"
    USER ||..o{ REVIEW : "receives"
    USER ||..|| USER_RATING_SUMMARY : "has"
    USER ||..o{ NOTIFICATION : "receives"
    USER ||..o{ DEVICE : "registers"
    USER ||..o{ BLOCK : "blocks"
    USER ||..o{ REPORT : "files"

    SKILL_CATEGORY ||--o{ SKILL : "groups"
    SKILL ||--o{ USER_SKILL : "referenced by"
    SKILL ||..o{ SWAP_REQUEST : "offered or requested in"

    SWAP_REQUEST ||--o| MATCH : "becomes"
    MATCH ||..|| CONVERSATION : "has"
    MATCH ||..o{ SWAP_SESSION : "schedules"
    CONVERSATION ||--|{ CONVERSATION_PARTICIPANT : "has"
    CONVERSATION ||--o{ MESSAGE : "contains"
    SWAP_SESSION ||..o{ REVIEW : "justifies"

    USER {
        uuid id PK
        string email UK
        string password_hash
        string status
        string role
        timestamp email_verified_at
        timestamp created_at
    }
    REFRESH_TOKEN {
        uuid id PK
        uuid user_id FK
        string token_hash UK
        uuid family_id
        timestamp expires_at
        timestamp created_at
        timestamp revoked_at
        uuid replaced_by_token_id
    }
    ONE_TIME_TOKEN {
        uuid id PK
        uuid user_id FK
        string purpose
        string token_hash UK
        timestamp expires_at
        timestamp used_at
    }
    PROFILE {
        uuid user_id PK
        string display_name
        string bio
        string avatar_key
        string city
        boolean accepts_online
        boolean accepts_in_person
        timestamp created_at
        timestamp updated_at
    }
    SKILL_CATEGORY {
        uuid id PK
        string name
        string slug UK
        int sort_order
    }
    SKILL {
        uuid id PK
        uuid category_id FK
        string name
        string slug UK
        string status
        uuid proposed_by_user_id
        timestamp created_at
        timestamp reviewed_at
    }
    USER_SKILL {
        uuid id PK
        uuid user_id
        uuid skill_id FK
        string type
        string level
        timestamp created_at
    }
    SWAP_REQUEST {
        uuid id PK
        uuid requester_id
        uuid recipient_id
        uuid offered_skill_id
        uuid requested_skill_id
        string status
        timestamp created_at
        timestamp expires_at
        timestamp responded_at
    }
    MATCH {
        uuid id PK
        uuid swap_request_id FK
        uuid user_a_id
        uuid user_b_id
        uuid skill_taught_by_a_id
        uuid skill_taught_by_b_id
        string status
        uuid end_requested_by_user_id
        timestamp end_requested_at
        string end_reason
        timestamp created_at
        timestamp ended_at
    }
    CONVERSATION {
        uuid id PK
        uuid match_id UK
        timestamp created_at
    }
    CONVERSATION_PARTICIPANT {
        uuid conversation_id PK
        uuid user_id PK
        timestamp last_read_at
    }
    MESSAGE {
        uuid id PK
        uuid conversation_id FK
        uuid sender_id
        string body
        timestamp sent_at
    }
    SWAP_SESSION {
        uuid id PK
        uuid match_id
        uuid proposed_by_user_id
        timestamp scheduled_at
        int duration_minutes
        string mode
        string note
        string status
        uuid completion_marked_by_user_id
        timestamp completed_at
        uuid cancelled_by_user_id
        string cancel_reason
        timestamp cancelled_at
        timestamp created_at
    }
    REVIEW {
        uuid id PK
        uuid reviewer_id
        uuid reviewee_id
        uuid swap_session_id
        int rating
        string comment
        timestamp created_at
        timestamp deleted_at
    }
    USER_RATING_SUMMARY {
        uuid user_id PK
        int review_count
        decimal average_rating
    }
    NOTIFICATION {
        uuid id PK
        uuid user_id
        string type
        json payload
        timestamp created_at
        timestamp read_at
    }
    DEVICE {
        uuid id PK
        uuid user_id
        string fcm_token UK
        string platform
        timestamp last_seen_at
    }
    BLOCK {
        uuid blocker_id PK
        uuid blocked_id PK
        timestamp created_at
    }
    REPORT {
        uuid id PK
        uuid reporter_id
        string target_type
        uuid target_id
        string reason
        string description
        string status
        timestamp created_at
        timestamp resolved_at
        uuid resolved_by_user_id
    }
```

## Benzersizlik kısıtları

İş kurallarını eşzamanlı isteklere karşı koruyan kısıtlar. Kural numaraları `docs/domain/business-rules.md` dosyasındandır.

| Tablo | Kısıt | Kural |
|---|---|---|
| `USER` | `email` benzersiz (küçük harfe normalize) | ID-01 |
| `SKILL` | `slug` benzersiz | SK-03 |
| `USER_SKILL` | (`user_id`, `skill_id`) benzersiz | SK-04 |
| `SWAP_REQUEST` | Sırasız kullanıcı çifti benzersiz, yalnızca `status = Pending` satırlarda (kısmi index) | RQ-05 |
| `MATCH` | `swap_request_id` benzersiz | MT-01 |
| `MATCH` | Sırasız kullanıcı çifti benzersiz, yalnızca `status <> Ended` satırlarda (kısmi index) | MT-02 |
| `CONVERSATION` | `match_id` benzersiz | MS-01 |
| `REVIEW` | (`reviewer_id`, `reviewee_id`) benzersiz, silinmiş satırlar dahil | RV-01, RV-07 |
| `DEVICE` | `fcm_token` benzersiz | — |

"Sırasız kullanıcı çifti", iki kimliğin küçüğü ve büyüğü üzerinden kurulan ifade index'idir; böylece A→B ve B→A aynı çift sayılır.

## Sorgu index'leri (ilk tahmin)

Kesin liste Faz 6'daki performans kontrolünde gözden geçirilir.

- `USER_SKILL` (`skill_id`, `type`): keşfette "bu yeteneği sunanlar/isteyenler".
- `SWAP_REQUEST` (`recipient_id`, `status`) ve (`requester_id`, `status`): gelen/giden istek listeleri.
- `SWAP_REQUEST` (`expires_at`) yalnızca `Pending` satırlarda: süre aşımı taraması.
- `MATCH` (`user_a_id`), (`user_b_id`): eşleşme listesi.
- `MESSAGE` (`conversation_id`, `sent_at`, `id`): cursor tabanlı sayfalama.
- `SWAP_SESSION` (`match_id`, `scheduled_at`).
- `REVIEW` (`reviewee_id`, `created_at`).
- `NOTIFICATION` (`user_id`, `created_at`).
