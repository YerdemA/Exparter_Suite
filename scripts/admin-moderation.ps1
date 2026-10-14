<#
.SYNOPSIS
    Lets an administrator work through reports, suspend or restore accounts and read
    session cancellation reasons.

.DESCRIPTION
    There is no admin panel in the MVP; this script calls the admin endpoints directly.
    The account must have the Admin role; see admin-skill-proposals.ps1 for how to grant it.

.EXAMPLE
    ./scripts/admin-moderation.ps1 -Identifier ayse
#>
param(
    [Parameter(Mandatory)]
    [string] $Identifier,

    [string] $ApiBaseUrl = 'http://localhost:5244'
)

$ErrorActionPreference = 'Stop'
$api = "$ApiBaseUrl/api/v1"

$password = Read-Host -Prompt "Parola ($Identifier)" -AsSecureString
$plainPassword = [System.Net.NetworkCredential]::new('', $password).Password
$login = Invoke-RestMethod "$api/auth/login" -Method Post -ContentType 'application/json; charset=utf-8' `
    -Body (@{ identifier = $Identifier; password = $plainPassword } | ConvertTo-Json)
$headers = @{ Authorization = "Bearer $($login.accessToken)" }

if ($login.account.role -ne 'Admin') {
    Write-Host "Bu hesap admin değil. Rolü vermek için admin-skill-proposals.ps1 betiğinin açıklamasına bakın." -ForegroundColor Yellow
    exit 1
}

$reasons = @{
    Spam = 'Spam'
    Harassment = 'Taciz'
    InappropriateContent = 'Uygunsuz içerik'
    FakeProfile = 'Sahte profil'
    Other = 'Diğer'
}
$targets = @{ User = 'Kullanıcı'; Message = 'Mesaj'; Review = 'Değerlendirme' }

function Show-Reports {
    while ($true) {
        $page = Invoke-RestMethod "$api/admin/reports?status=Open&pageSize=50" -Headers $headers
        if ($page.items.Count -eq 0) {
            Write-Host 'Açık şikâyet yok.' -ForegroundColor Green
            return
        }

        Write-Host "`nAçık şikâyetler ($($page.totalCount)), eskiden yeniye:" -ForegroundColor Cyan
        for ($i = 0; $i -lt $page.items.Count; $i++) {
            $item = $page.items[$i]
            Write-Host ("  [{0}] {1} · {2} · {3}" -f ($i + 1), $targets[$item.targetType], $reasons[$item.reason], $item.createdAt)
            Write-Host ("      Şikâyet edilen kullanıcı: {0}   Şikâyet eden: {1}" -f $item.targetUserId, $item.reporterId)
            if ($item.targetContent) { Write-Host ("      İçerik: {0}" -f $item.targetContent) }
            if ($item.description) { Write-Host ("      Açıklama: {0}" -f $item.description) }
        }

        $answer = Read-Host "`nİşlem: 'c 1' sonuçlandır (işlem yapıldı), 'd 1' yersiz bul, 'q' geri"
        if ($answer -eq 'q') { return }
        if ($answer -notmatch '^([cd])\s+(\d+)$') {
            Write-Host 'Anlaşılmadı.' -ForegroundColor Yellow
            continue
        }

        $index = [int] $Matches[2] - 1
        if ($index -lt 0 -or $index -ge $page.items.Count) {
            Write-Host 'Böyle bir sıra numarası yok.' -ForegroundColor Yellow
            continue
        }

        $status = if ($Matches[1] -eq 'c') { 'Resolved' } else { 'Dismissed' }
        $result = Invoke-RestMethod "$api/admin/reports/$($page.items[$index].id)/resolution" -Method Post -Headers $headers `
            -ContentType 'application/json; charset=utf-8' -Body (@{ status = $status } | ConvertTo-Json)
        Write-Host ("Şikâyet kapatıldı: {0}. Şikâyet edene bildirim gitti." -f $result.status) -ForegroundColor Green
    }
}

function Set-Suspension([bool] $suspend) {
    $userId = Read-Host 'Kullanıcı kimliği (userId)'
    $method = if ($suspend) { 'Post' } else { 'Delete' }
    try {
        Invoke-RestMethod "$api/admin/users/$userId/suspension" -Method $method -Headers $headers | Out-Null
        Write-Host $(if ($suspend) { 'Hesap askıya alındı.' } else { 'Askı kaldırıldı.' }) -ForegroundColor Green
    }
    catch {
        Write-Host "Yapılamadı: $($_.ErrorDetails.Message)" -ForegroundColor Yellow
    }
}

function Show-Cancellations {
    $page = Invoke-RestMethod "$api/admin/session-cancellations?pageSize=50" -Headers $headers
    if ($page.items.Count -eq 0) {
        Write-Host 'İptal edilmiş oturum yok.' -ForegroundColor Green
        return
    }

    Write-Host "`nSon iptaller ($($page.totalCount)), yeniden eskiye:" -ForegroundColor Cyan
    foreach ($item in $page.items) {
        Write-Host ("  {0} · iptal eden {1} · karşı taraf {2}" -f $item.cancelledAt, $item.cancelledByUserId, $item.otherUserId)
        Write-Host ("      Sebep: {0}" -f $item.reason)
    }
}

while ($true) {
    $answer = Read-Host "`n1) Şikâyetler  2) Askıya al  3) Askıyı kaldır  4) Oturum iptalleri  q) Çık"
    switch ($answer) {
        '1' { Show-Reports }
        '2' { Set-Suspension $true }
        '3' { Set-Suspension $false }
        '4' { Show-Cancellations }
        'q' { exit 0 }
        default { Write-Host 'Anlaşılmadı.' -ForegroundColor Yellow }
    }
}
