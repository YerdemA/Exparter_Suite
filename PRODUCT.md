# Product

<!-- impeccable:product-schema 1 -->

> Bu dosya tasarım araçlarının (Impeccable) okuduğu ürün özetidir. Kaynak `docs/PROJECT_BRIEF.md` dosyasıdır; çelişki olursa brief geçerlidir. Görsel kurallar `DESIGN.md` dosyasındadır.

## Platform

android

Uygulama Flutter ile yazılıyor ve bugün yalnızca Google Play'de (Android, Material 3) yayımlanıyor. iOS sürümü kararlaştırılmadı; karar verilirse bu değer `adaptive` olur. Web istemcisi ayrı bir iştir (brief Faz 7).

## Users

18 yaşından büyük, Türkçe konuşan, bir şey öğrenmek isteyen ve karşılığında bildiği bir şeyi öğretebilecek kişiler. Uygulamayı telefonda kullanırlar: uygun kişiyi arar, istek gönderir, mesajlaşır, çevrimiçi oturum planlar ve görüntülü görüşmeye katılırlar.

## Product Purpose

Exparter bir yetenek takası platformudur. Kullanıcı sunabildiği ve öğrenmek istediği yetenekleri ekler, karşılıklı uyumlu kişilerle eşleşir, mesajlaşır, oturum planlar ve takas bitince karşı tarafı değerlendirir. Örnek: gitar bilen biri, İngilizce öğretebilecek biriyle eşleşir ve ikisi birbirine bildiğini öğretir.

## Positioning

Para yok, yalnızca takas: iki taraf da hem öğretir hem öğrenir. Keşfet karşılıklı uyumu öne çıkarır (A'nın sunduğu B'nin istediği, B'nin sunduğu A'nın istediği).

## Operating Context

- Temel akış: kayıt → profil ve yetenekler → keşfet → takas isteği → eşleşme → mesajlaşma → oturum → değerlendirme.
- Oturumlar çevrimiçidir ve uygulamaya gömülü, uçtan uca şifreli görüntülü görüşmeyle yapılır; görüşme yalnızca planlanan oturum saatinde açılır.
- Mesajlar ve durum değişiklikleri gerçek zamanlıdır; uygulama arka plandayken telefon bildirimi gelir.
- Açık ve koyu tema vardır; varsayılan sistemi izler, kullanıcı profilden Sistem / Açık / Koyu seçer.

## Capabilities and Constraints

- Arayüz dili Türkçedir ve "siz" hitabı kullanır; düğme etiketleri kısa emir kipindedir ("Giriş yap", "Kaydet"). Çoklu dil ileride gelecek; tüm metinler `l10n` üzerinden okunur.
- Kullanıcı en fazla 5 sunulan ve 5 istenen yetenek ekler; aynı anda en fazla 3 süren eşleşmesi olur.
- Etkileşim koşulu (doğrulanmış e-posta, profil bilgileri, en az bir sunulan yetenek) sağlanmadan gezilebilir; koşul etkileşim anında bir uyarıyla sorulur.
- Her ekranda yükleniyor, boş ve hata durumları bulunur; kullanıcıya ham hata metni gösterilmez.
- Renk, tipografi ve boşluk değerleri yalnızca `mobile/lib/core/theme/` token'larından gelir; bileşenlere sabit değer yazılmaz.
- Henüz yok: ödeme, gönderi akışı, web istemcisi.

## Brand Commitments

- Ad her zaman "Exparter".
- Palet "İndigo ve turkuaz" (kullanıcı kararı, 2026-10-14). Görsel stil enerjik ve renkli.
- Logo: iki dönen ok ve ortada dört köşeli bir yıldız (takas ve öğrenme). Uzatılmaz, döndürülmez, gölge eklenmez.
- Ses: kısa, sıcak ve açık cümleler. Slogan: "Bildiğinizi öğretin, merak ettiğinizi öğrenin." Vurgu cümlesi: "Para yok, yalnızca takas."

## Evidence on Hand

- Mağaza ekran görüntüleri: `docs/store/screenshots/` (kurgusal kişilerle, yerel veritabanından).
- Tema ve logo kodu: `mobile/lib/core/theme/`.
- Gerçek kullanıcı yorumu, basın haberi veya kullanım istatistiği yok; tasarım çalışmalarında uydurulmamalı.

## Product Principles

1. Karşılıklılık görünür olmalı: her eşleşmede iki tarafın ne verip ne aldığı açıkça okunur.
2. Güven önce gelir: istatistikler, değerlendirmeler ve şifreli görüşme tanımadığı biriyle buluşan kullanıcıya güvence verir.
3. Gezmek serbest, etkileşim koşullu: kullanıcı engellenmez, gerektiği anda ve nedeniyle birlikte yönlendirilir.
4. Sade dil: kısa Türkçe cümleler, teknik terim ve ham hata yok.
