using System.Net;

namespace Exparter.Infrastructure.Email;

/// <summary>What one email says; rendered as HTML with a plain-text twin.</summary>
internal sealed record EmailContent(
    string Subject,
    string Preheader,
    string Title,
    string Body,
    string ButtonLabel,
    string Link,
    string Note);

/// <summary>
/// The branded layout of every email. Tables and inline styles, because that is what
/// mail clients render reliably; the gradient falls back to plain indigo where it is not
/// supported. The logo and the footer links are served by the public site.
/// </summary>
internal static class EmailTemplate
{
    private const string Site = "https://exparter.app";

    public static string Text(EmailContent content) =>
        $"""
         {content.Title}

         {content.Body}

         {content.ButtonLabel}: {content.Link}

         {content.Note}

         --
         Exparter · {Site}
         Destek: destek@exparter.app
         """;

    public static string Html(EmailContent content)
    {
        var title = Encode(content.Title);
        var link = Encode(content.Link);
        return $"""
                <!doctype html>
                <html lang="tr">
                <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <meta name="color-scheme" content="light">
                <title>{Encode(content.Subject)}</title>
                </head>
                <body style="margin:0;padding:0;background:#f0f1f8;">
                <div style="display:none;max-height:0;overflow:hidden;opacity:0;">{Encode(content.Preheader)}</div>
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:#f0f1f8;">
                <tr><td align="center" style="padding:32px 16px;">
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="max-width:560px;background:#ffffff;border-radius:20px;overflow:hidden;font-family:Roboto,'Segoe UI',Helvetica,Arial,sans-serif;">
                <tr><td style="background:#4f46e5;background-image:linear-gradient(135deg,#3730a3 0%,#4f46e5 45%,#0e7c7b 100%);padding:28px 32px;">
                <table role="presentation" cellpadding="0" cellspacing="0" border="0"><tr>
                <td style="vertical-align:middle;"><img src="{Site}/img/email-mark.png" width="40" height="40" alt="" style="display:block;border:0;"></td>
                <td style="vertical-align:middle;padding-left:12px;color:#ffffff;font-size:24px;font-weight:700;letter-spacing:-0.2px;">Exparter</td>
                </tr></table>
                </td></tr>
                <tr><td style="padding:36px 32px 8px;">
                <h1 style="margin:0 0 16px;color:#1e1b3a;font-size:24px;line-height:1.3;font-weight:700;">{title}</h1>
                <p style="margin:0 0 28px;color:#45475f;font-size:16px;line-height:1.6;">{Encode(content.Body)}</p>
                <table role="presentation" cellpadding="0" cellspacing="0" border="0"><tr>
                <td style="border-radius:999px;background:#4f46e5;">
                <a href="{link}" style="display:inline-block;padding:14px 30px;color:#ffffff;font-size:16px;font-weight:700;text-decoration:none;border-radius:999px;">{Encode(content.ButtonLabel)}</a>
                </td></tr></table>
                <p style="margin:28px 0 0;color:#5b5e78;font-size:14px;line-height:1.6;">{Encode(content.Note)}</p>
                </td></tr>
                <tr><td style="padding:24px 32px 32px;">
                <p style="margin:0;padding-top:20px;border-top:1px solid #e2e4ef;color:#8a8ca6;font-size:13px;line-height:1.6;">Düğme çalışmazsa bu bağlantıyı tarayıcınıza yapıştırın:<br><a href="{link}" style="color:#4f46e5;word-break:break-all;">{link}</a></p>
                </td></tr>
                </table>
                <p style="margin:20px 0 0;color:#8a8ca6;font-family:Roboto,'Segoe UI',Helvetica,Arial,sans-serif;font-size:12px;line-height:1.6;">Bu e-postayı Exparter hesabınızla ilgili bir işlem için aldınız.<br><a href="{Site}" style="color:#8a8ca6;">exparter.app</a> · <a href="{Site}/gizlilik.html" style="color:#8a8ca6;">Gizlilik politikası</a> · <a href="mailto:destek@exparter.app" style="color:#8a8ca6;">destek@exparter.app</a></p>
                </td></tr>
                </table>
                </body>
                </html>
                """;
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
