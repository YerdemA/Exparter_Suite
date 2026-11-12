# ERD

Durum: **Onaylandı (2026-10-07); 2026-11-03'te migration'lardaki haliyle eşitlendi.** Alan açıklamaları için `docs/domain/domain-model.md` dosyasına bak.

Şemanın kaynağı EF Core migration'larıdır (`backend/src/Exparter.Infrastructure/Persistence/Migrations`); bu belge onların okunur özetidir. Her modülün tabloları kendi PostgreSQL şemasındadır (ör. `identity.users`, ADR 0006).

Farklı modüllerdeki tablolar arasındaki ilişkiler yalnızca kimlik referansıdır; kodda navigation property yoktur. Bu ilişkiler diyagramda kesikli çizgiyle gösterilir.

## Tablo sahipliği

| Modül (şema) | Tablolar |
|---|---|
| Identity (`identity`) | `users`, `refresh_tokens`, `one_time_tokens`, `blocked_emails` |
| Profiles (`profiles`) | `profiles` |
| Skills (`skills`) | `skill_categories`, `skills`, `user_skills` |
| Matching (`matching`) | `swap_requests`, `matches` |
| Messaging (`messaging`) | `conversations`, `messages` |
| Sessions (`sessions`) | `swap_sessions` |
| Reviews (`reviews`) | `reviews` |
| Notifications (`notifications`) | `notifications`, `devices` |
| Moderation (`moderation`) | `blocks`, `reports` |

Okuma durumu ayrı bir katılımcı tablosunda değil, sohbetin kendisinde tutulur; puan özeti tablo değildir, her okumada hesaplanır.

## Diyagram

```mermaid
erDiagram
    USER ||--o{ REFRESH_TOKEN : "has"
    USER ||--o{ ONE_TIME_TOKEN : "has"
    USER ||..|| PROFILE : "has"
    USER ||..o{ USER_SKILL : "lists"
    USER ||..o{ SWAP_REQUEST : "sends or receives"
    USER ||..o{ MATCH : "participates"
    USER ||..o{ CONVERSATION : "participates"
    USER ||..o{ MESSAGE : "sends"
    USER ||..o{ SWAP_SESSION : "proposes or attends"
    USER ||..o{ REVIEW : "writes or receives"
    USER ||..o{ NOTIFICATION : "receives"
    USER ||..o{ DEVICE : "registers"
    USER ||..o{ BLOCK : "blocks"
    USER ||..o{ REPORT : "files"

    SKILL_CATEGORY ||--o{ SKILL : "groups"
    SKILL ||--o{ USER_SKILL : "referenced by"
    SKILL ||..o{ SWAP_REQUEST : "offered or requested in"

    SWAP_REQUEST ||--o| MATCH : "becomes"
    MATCH ||..o{ SWAP_SESSION : "schedules"
    CONVERSATION ||--o{ MESSAGE : "contains"
    SWAP_SESSION ||..o{ REVIEW : "justifies"

    USER {
        uuid id PK
        string email UK
        string username UK
        timestamp username_changed_at
        string password_hash
        string status
        string role
        timestamp email_verified_at
        timestamp age_confirmed_at
        timestamp terms_accepted_at
        string terms_version
        timestamp deletion_requested_at
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
    BLOCKED_EMAIL {
        uuid id PK
        string email_hash
        timestamp blocked_until
    }
    PROFILE {
        uuid user_id PK
        string display_name
        string bio
        string avatar_key
        string city
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
        int level
        timestamp created_at
    }
    SWAP_REQUEST {
        uuid id PK
        uuid requester_id
        uuid recipient_id
        uuid offered_skill_id
        uuid requested_skill_id
        string message
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
        timestamp end_request_expires_at
        timestamp end_reminder_sent_at
        string end_reason
        uuid ended_by_user_id
        timestamp created_at
        timestamp ended_at
    }
    CONVERSATION {
        uuid id PK
        uuid user_low_id
        uuid user_high_id
        timestamp user_low_last_read_at
        timestamp user_high_last_read_at
        timestamp last_activity_at
        uuid last_message_id
        timestamp created_at
        timestamp closed_at
        uuid closed_by_user_id
    }
    MESSAGE {
        uuid id PK
        uuid conversation_id FK
        uuid sender_id
        string body
        uuid client_message_id
        timestamp sent_at
    }
    SWAP_SESSION {
        uuid id PK
        uuid match_id
        uuid proposed_by_user_id
        uuid other_user_id
        timestamp scheduled_at
        int duration_minutes
        string note
        string status
        uuid completion_marked_by_user_id
        timestamp completion_marked_at
        timestamp completion_confirm_deadline
        boolean completed_automatically
        timestamp completed_at
        uuid cancelled_by_user_id
        string cancel_reason
        timestamp cancelled_at
        timestamp day_reminder_sent_at
        timestamp hour_reminder_sent_at
        timestamp completion_reminder_sent_at
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
    NOTIFICATION {
        uuid id PK
        uuid user_id
        string type
        uuid actor_user_id
        uuid swap_request_id
        uuid match_id
        uuid session_id
        uuid review_id
        uuid skill_id
        string reason
        timestamp deadline
        timestamp created_at
        timestamp read_at
    }
    DEVICE {
        uuid id PK
        uuid user_id
        string fcm_token UK
        string platform
        timestamp created_at
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
        uuid target_user_id
        string target_content
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
| `USER` | `username` benzersiz | ID-15 |
| `SKILL` | `slug` benzersiz | SK-03 |
| `USER_SKILL` | (`user_id`, `skill_id`, `type`) benzersiz | SK-04 |
| `SWAP_REQUEST` | Sırasız kullanıcı çifti benzersiz, yalnızca `status = Pending` satırlarda (kısmi index) | RQ-05 |
| `MATCH` | `swap_request_id` benzersiz | MT-01 |
| `MATCH` | Sırasız kullanıcı çifti benzersiz, yalnızca `status <> Ended` satırlarda (kısmi index) | MT-02 |
| `CONVERSATION` | (`user_low_id`, `user_high_id`) benzersiz | MS-01 |
| `MESSAGE` | (`conversation_id`, `sender_id`, `client_message_id`) benzersiz | MS-11 |
| `REVIEW` | (`reviewer_id`, `reviewee_id`) benzersiz, silinmiş satırlar dahil | RV-01, RV-07 |
| `DEVICE` | `fcm_token` benzersiz | NT-10 |
| `BLOCK` | (`blocker_id`, `blocked_id`) birincil anahtar | — |
| `REPORT` | (`reporter_id`, `target_type`, `target_id`) benzersiz, yalnızca `status = Open` satırlarda | MD-08 |

Benzersizlik ihlali API'de iş kuralının hata koduna çevrilir (`UniqueConstraints`); böylece eşzamanlı iki istekten ikincisi 500 değil, kuralın 409'unu alır. Oturum çakışması (SS-20) için veritabanı kısıtı yoktur.

"Sırasız kullanıcı çifti", iki kimliğin küçüğü ve büyüğü üzerinden kurulan ifade index'idir; böylece A→B ve B→A aynı çift sayılır.

## Sorgu index'leri

Faz 6 dilim 4'te (performans) sorguların hepsinin index kullandığı doğrulandı. Benzersizlik index'lerine ek olarak:

- `users` (`deletion_requested_at`), yalnızca `PendingDeletion` satırlarda: kalıcı silme işi.
- `one_time_tokens` (`user_id`, `purpose`); `refresh_tokens` (`user_id`), (`family_id`); `blocked_emails` (`email_hash`).
- `skills` (`category_id`), (`status`, `category_id`); `user_skills` (`skill_id`, `type`): katalog ve keşfet.
- `swap_requests` (`recipient_id`, `created_at`), (`requester_id`, `created_at`), (`status`, `expires_at`): listeler ve süre aşımı.
- `matches` (`user_a_id`, `created_at`), (`user_b_id`, `created_at`), (`status`, `end_request_expires_at`).
- `conversations` (`user_low_id`, `last_activity_at`), (`user_high_id`, `last_activity_at`): sohbet listesi.
- `messages` (`conversation_id`, `sent_at`, `id`): cursor tabanlı sayfalama.
- `swap_sessions` (`match_id`, `created_at`), (`proposed_by_user_id`), (`other_user_id`), (`status`, `scheduled_at`), (`status`, `completion_confirm_deadline`).
- `reviews` (`reviewee_id`, `created_at`).
- `notifications` (`user_id`, `created_at`), (`created_at`): liste ve 90 günlük temizlik.
- `devices` (`user_id`); `blocks` (`blocked_id`); `reports` (`status`, `created_at`).
