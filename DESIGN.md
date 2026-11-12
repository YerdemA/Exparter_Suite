---
version: alpha
name: Exparter
description: Yetenek takası uygulamasının görsel sistemi; indigo ve turkuaz, Material 3.
colors:
  primary: "#4F46E5"
  on-primary: "#FFFFFF"
  primary-deep: "#3730A3"
  secondary: "#0EA5A4"
  on-secondary: "#042F2E"
  secondary-deep: "#0E7C7B"
  tertiary: "#F59E0B"
  on-tertiary: "#3B2601"
  error: "#C62838"
  on-error: "#FFFFFF"
  surface: "#F8F9FC"
  on-surface: "#1E1B3A"
  on-surface-variant: "#5B5E78"
  surface-container-low: "#FFFFFF"
  surface-container: "#F0F1F8"
  surface-container-high: "#E9EBF5"
  surface-container-highest: "#E4E6F2"
  outline: "#CDD0E2"
  outline-variant: "#E2E4EF"
  primary-dark: "#A5B4FC"
  on-primary-dark: "#1E1B4B"
  secondary-dark: "#5EEAD4"
  tertiary-dark: "#FCD34D"
  error-dark: "#FFA4A9"
  on-error-dark: "#4A0C12"
  surface-dark: "#0F1020"
  on-surface-dark: "#E6E6F0"
  on-surface-variant-dark: "#A6A8C0"
  surface-container-lowest-dark: "#0B0C18"
  surface-container-low-dark: "#1A1B2E"
  surface-container-dark: "#1F2135"
  surface-container-high-dark: "#25273D"
  surface-container-highest-dark: "#2C2E46"
  outline-dark: "#45486A"
  outline-variant-dark: "#2E3049"
typography:
  headline:
    fontFamily: Roboto
    fontSize: 28px
    fontWeight: 800
    lineHeight: 36px
  title-large:
    fontFamily: Roboto
    fontSize: 22px
    fontWeight: 700
    lineHeight: 28px
  title:
    fontFamily: Roboto
    fontSize: 16px
    fontWeight: 500
    lineHeight: 24px
  body-large:
    fontFamily: Roboto
    fontSize: 16px
    fontWeight: 400
    lineHeight: 24px
  body:
    fontFamily: Roboto
    fontSize: 14px
    fontWeight: 400
    lineHeight: 20px
  body-small:
    fontFamily: Roboto
    fontSize: 12px
    fontWeight: 400
    lineHeight: 16px
  label:
    fontFamily: Roboto
    fontSize: 14px
    fontWeight: 700
    lineHeight: 20px
rounded:
  sm: 8px
  md: 14px
  lg: 28px
spacing:
  xs: 4px
  sm: 8px
  md: 16px
  lg: 24px
  xl: 32px
  xxl: 48px
components:
  button-primary:
    backgroundColor: "{colors.primary}"
    textColor: "{colors.on-primary}"
    typography: "{typography.label}"
    rounded: "{rounded.md}"
    height: 52px
  button-outlined:
    textColor: "{colors.primary}"
    typography: "{typography.label}"
    rounded: "{rounded.md}"
    height: 52px
  card:
    backgroundColor: "{colors.surface-container-low}"
    textColor: "{colors.on-surface}"
    rounded: "{rounded.md}"
  input:
    backgroundColor: "{colors.surface-container-high}"
    textColor: "{colors.on-surface}"
    rounded: "{rounded.md}"
    padding: 16px
---

# Design System: Exparter

> Bu dosya bugünkü mobil uygulamanın görsel sistemini kaydeder; yeni bir yön önermez. Değerlerin kaynağı `mobile/lib/core/theme/app_tokens.dart` ve `app_theme.dart` dosyalarıdır: önce kod değişir, sonra bu dosya güncellenir. Ürün bilgisi `PRODUCT.md` dosyasındadır.
>
> **Kullanıcı kararı bekleyenler:** sistemin tek cümlelik yaratıcı yönü (bir "kuzey yıldızı" benzetmesi), esin alınacak referans markalar ve web sitesinin bu sistemi ne ölçüde paylaşacağı. Bunlar kararlaştırılana kadar aşağıdaki metin yalnızca gözlenen durumu anlatır.

## Overview

Exparter enerjik ve renkli bir Material 3 uygulamasıdır (brief Bölüm 2). Kimliği iki renk taşır: indigo eylemi, turkuaz ikinci sesi ve olumlu durumu. Yüzeyler düzdür; derinlik gölgeyle değil, soğuk gri-lila zemin tonlarının basamaklarıyla ve ince çerçevelerle verilir. Giriş ekranlarının üst yarısını dolduran indigo → turkuaz geçişi markanın en belirgin anıdır.

**Key Characteristics:**

- Açık ve koyu tema eşit yurttaştır; her ekran ikisinde de çalışır.
- Gölge yok; ton katmanları ve 1 piksellik çerçeveler var.
- Büyük, rahat dokunma hedefleri (52 px yüksekliğinde denetimler).
- Tek yazı ailesi (platformun varsayılanı, Android'de Roboto); hiyerarşi ağırlıkla kurulur.

## Colors

Soğuk, lila alt tonlu nötrlerin üzerinde doygun indigo ve turkuaz; kehribar yalnızca dikkat çekmek için.

### Primary

- **İndigo** (`primary`): dolu düğmeler, odaklanmış alan çerçevesi, seçili durumlar. Koyu temada açık indigo (`primary-dark`) ve üstünde koyu mürekkep (`on-primary-dark`).
- **Derin indigo** (`primary-deep`): marka geçişinin başlangıcı.

### Secondary

- **Turkuaz** (`secondary`): ikincil vurgu. Üstündeki metin beyaz değil koyu turkuaz mürekkeptir (`on-secondary`); beyaz bu tonda okunmuyor.
- **Derin turkuaz** (`secondary-deep`): başarı rengi ve marka geçişinin bitişi.

### Tertiary

- **Kehribar** (`tertiary`): uyarı ve dikkat çekme. Süs olarak kullanılmaz.

### Neutral

- **Zemin** (`surface`) ve **mürekkep** (`on-surface`): sayfa arka planı ve ana metin. İkincil metin `on-surface-variant`.
- **Kap tonları** (`surface-container-low` → `surface-container-highest`): kartlar beyaz (`-low`), alanlar `-high`; basamak yükseldikçe yüzey zemine göre koyulaşır.
- **Çerçeveler** (`outline`, `outline-variant`): alan çerçevesi ve kart çerçevesi.
- **Hata** (`error`): doğrulama ve yıkıcı işlemler.

### Named Rules

**Tema Kuralı.** Widget'lar rengi yalnızca `Theme.of(context).colorScheme` ve `context.brand` üzerinden alır; `AppPalette` değerlerini yalnızca tema okur. Bileşene sabit renk kodu yazılmaz.

**Marka Geçişi Kuralı.** Geçiş sol üstten sağ ortaya akar: derin indigo → indigo (%45) → derin turkuaz. Koyu temada göz almasın diye daha derin tonlarla kurulur. Giriş ekranlarının başlığı ve tanıtım görselleri içindir; sıradan kart ve düğmelere uygulanmaz.

## Typography

**Yazı ailesi:** platform varsayılanı (Android'de Roboto). Uygulamaya özel yazı tipi paketlenmiyor.

**Karakter:** tek aile, Material 3 ölçeği; fark ağırlıkla yaratılır.

### Hierarchy

- **Headline** (800, 28/36): ekranın ana başlığı; giriş ekranlarında uygulamanın adı.
- **Title large** (700, 22/28): bölüm başlığı.
- **Title** (500, 16/24): kart ve liste öğesi başlığı. En sık kullanılan başlık.
- **Body large / Body** (400, 16/24 ve 14/20): akan metin. `Body` en sık kullanılan stildir.
- **Body small** (400, 12/16): yardımcı metin, zaman damgası.
- **Label** (700, 14/20): düğme etiketi.

### Named Rules

**Ağırlık Kuralı.** Yalnızca üç stil varsayılandan kalındır: headline 800, title large 700, label 700. Başka yerde kalınlık elle artırılmaz.

## Layout

Boşluk ölçeği 4 · 8 · 16 · 24 · 32 · 48 pikseldir (`AppSpacing`); ara değer kullanılmaz. Formlar tablette ve yatay telefonda 440 pikselden fazla genişlemez (`AppSizes.formMaxWidth`). Dokunulabilir denetimler en az 52 piksel yüksekliğindedir ve genişliği doldurur. Ana gezinme alttaki Material 3 gezinme çubuğudur.

## Elevation & Depth

Gölge kullanılmaz. Kartların yüksekliği sıfırdır; zeminden beyaz dolgu ve `outline-variant` çerçeveyle ayrılırlar. Derinlik kap tonlarının basamaklarıyla anlatılır.

### Named Rules

**Düz Yüzey Kuralı.** Yeni bir bileşen ayrışmak için gölge değil, bir üst kap tonu veya çerçeve kullanır.

## Shapes

Üç yarıçap vardır (`AppRadius`): 8 px küçük öğeler, 14 px düğme, kart ve alan, 28 px büyük yüzeyler. Düğme, kart ve giriş alanı aynı 14 px köşeyi paylaşır.

## Components

### Buttons

- **Shape:** 14 px köşe, en az 52 px yükseklik, tam genişlik.
- **Primary (dolu):** indigo zemin, beyaz etiket. Ekranda asıl eylem için.
- **Outlined:** ikincil eylem; aynı ölçüler.
- **Text:** üçüncül eylem ve iletişim kutusu seçenekleri.
- Etiketler Türkçe, kısa ve emir kipinde: "Giriş yap", "Kaydet".

### Chips

Yetenekler ayrı çiplerde gösterilir; filtre ve tek seçimlerde seçim çipi kullanılır.

### Cards / Containers

- **Corner Style:** 14 px.
- **Background:** `surface-container-low` (açık temada beyaz).
- **Shadow Strategy:** yok; bkz. Elevation & Depth.
- **Border:** 1 px `outline-variant`.

### Inputs / Fields

- **Style:** dolgulu (`surface-container-high`), 14 px köşe, `outline` çerçeve, 16 px iç boşluk.
- **Focus:** çerçeve 2 px indigo olur.
- **Error:** çerçeve hata renginde; hata metni en fazla 3, yardım metni en fazla 2 satır.

### Navigation

Altta Material 3 gezinme çubuğu; mesajlarda okunmamış rozeti. İçerik içi geçişlerde sekme çubuğu.

### Feedback

Kısa bildirimler alt bilgi çubuğuyla (snackbar), onay ve uyarılar iletişim kutusuyla, seçim listeleri alt sayfayla verilir.

## Do's and Don'ts

### Do:

- **Do** her ekranı açık ve koyu temada birlikte deneyin.
- **Do** renk, boşluk ve yarıçapı `core/theme/` token'larından alın.
- **Do** her ekranda yükleniyor, boş ve hata durumlarını tasarlayın.
- **Do** turkuaz zeminde koyu turkuaz mürekkep kullanın.

### Don't:

- **Don't** bileşene sabit renk kodu, ara boşluk değeri veya yeni bir yarıçap yazmayın.
- **Don't** gölge eklemeyin.
- **Don't** logoyu uzatmayın, döndürmeyin veya gölgelendirmeyin.
- **Don't** kullanıcıya ham hata metni göstermeyin.
