# Standart sözleşme talepleri

KVKK m. 9 uyarınca yurt dışındaki her hizmet sağlayıcıya, Kurul'un veri sorumlusundan veri işleyene aktarım için yayımladığı standart sözleşmeyi (Modül 2) imzalama talebi gönderilir (`compliance-research.md`, 5. bölüm, 1. seçenek). Talepler ve yanıtlar saklanır; bir sağlayıcı kabul ederse sözleşme imzalanır ve **imzadan sonraki 5 iş günü içinde** Kurul'a bildirilir.

## Nereye gönderilir?

| Sağlayıcı | Kanal | Not |
|---|---|---|
| Cloudflare (R2) | `PrivacyQuestions@cloudflare.com` | Cloudflare'in Article 14 bildiriminde gizlilik ekibinin adresi olarak geçiyor. |
| Oracle Cloud | https://www.oracle.com/legal/data-privacy-inquiry-form/ | Oracle'ın gizlilik politikasından bağlantılı. |
| Brevo | `dpo@brevo.com` | Brevo'nun gizlilik politikasındaki veri koruma görevlisi adresi. |
| Sentry | `compliance@sentry.io` | Sentry'nin gizlilik politikasında gizlilik soruları ve AB temsilcisi için verilen adres. |
| Google Firebase | https://support.google.com/cloud/contact/dpo | Google Cloud sözleşmelerinde veri koruma ekibinin kanalı olarak geçiyor; çalışmadığı doğrulanamadı. Açılmazsa: https://support.google.com/policies/contact/general_privacy_form |

Gönderdiğiniz tarihleri ve gelen yanıtları repoda değil, kendi bilgisayarınızda bir klasörde saklayın (ör. `D:\Yazilim\legal-records`).

## Şablon (İngilizce)

Konu: `Request to sign the Turkish KVKK standard contract (Controller to Processor)`

```
Hello,

I operate Exparter, a skill-exchange mobile app for users in Türkiye, and use
<SERVICE> under account <ACCOUNT ID OR E-MAIL>. Under Article 9 of the Turkish
Personal Data Protection Law (KVKK No. 6698), as amended in 2024, a transfer of
personal data abroad to a processor requires the standard contract published by
the Turkish Personal Data Protection Board (Standard Contract 2: controller to
processor), signed by both parties and notified to the Authority within five
business days.

Could you please let me know:
1. whether <COMPANY> signs the Board's standard contract with its customers, and
   how to request it;
2. if not, which safeguard you offer for transfers from Türkiye.

The Board's texts are published at
https://www.kvkk.gov.tr/Icerik/7938/Standart-Sozlesmeler-ve-Baglayici-Sirket-Kurallarina-Iliskin-Dokumanlar-Hakkinda-Kamuoyu-Duyurusu

Data controller: Yusuf Erdem Aykaç (individual), destek@exparter.app
Data transferred: <WHAT GOES TO THIS PROVIDER>

Thank you,
Yusuf Erdem Aykaç
```

Sağlayıcıya göre `<WHAT GOES TO THIS PROVIDER>`:

- **Oracle:** the application database and server (account data, profiles, messages, traffic logs), hosted in Frankfurt.
- **Cloudflare:** profile photos and database backups in an R2 bucket under EU jurisdiction.
- **Brevo:** e-mail addresses of users, for verification and password reset e-mails.
- **Sentry:** error reports with a random user ID, device model and app version; no IP addresses.
- **Google Firebase:** device push tokens and notification texts that name no one.

## Bir sağlayıcı kabul ederse

1. KVKK'nın yayımladığı Modül 2 Türkçe metni (değiştirmeden) eklerini doldurarak hazırlayın; iki taraf da **Türkçe metni** imzalar, imza tarihleri metinde yazar.
2. İmzadan sonraki 5 iş günü içinde KVKK'nın Standart Sözleşme Bildirim Modülü'nden (e-Devlet ile giriş) bildirin.
3. `compliance-research.md` 5. bölümüne hangi sağlayıcının imzaladığını ekleyin.
