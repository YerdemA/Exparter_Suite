<#
.SYNOPSIS
    Lists pending skill proposals and lets an administrator approve or reject them.

.DESCRIPTION
    There is no admin panel in the MVP; this script calls the admin endpoints directly.
    The account must have the Admin role. To grant it (once), run from the repository root:

        docker compose exec postgres psql -U exparter -d exparter -c "UPDATE identity.users SET role = 'Admin' WHERE username = '<kullanıcı adı>'"

    The role is read when signing in, so it takes effect on the next sign-in.

.EXAMPLE
    ./scripts/admin-skill-proposals.ps1 -Identifier ayse
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
    Write-Host "Bu hesap admin değil. Rolü vermek için betiğin başındaki açıklamaya bakın." -ForegroundColor Yellow
    exit 1
}

$categories = @{}
foreach ($category in (Invoke-RestMethod "$api/skill-categories")) {
    $categories[$category.id] = $category.name
}

while ($true) {
    $page = Invoke-RestMethod "$api/admin/skill-proposals?status=PendingApproval&pageSize=50" -Headers $headers
    if ($page.items.Count -eq 0) {
        Write-Host 'Bekleyen yetenek önerisi yok.' -ForegroundColor Green
        break
    }

    Write-Host "`nBekleyen öneriler ($($page.totalCount)):" -ForegroundColor Cyan
    for ($i = 0; $i -lt $page.items.Count; $i++) {
        $item = $page.items[$i]
        Write-Host ("  [{0}] {1}  ({2})" -f ($i + 1), $item.name, $categories[$item.categoryId])
    }

    $answer = Read-Host "`nİşlem: 'o 1' onayla, 'r 1' reddet, 'q' çık"
    if ($answer -eq 'q') { break }
    if ($answer -notmatch '^([or])\s+(\d+)$') {
        Write-Host 'Anlaşılmadı.' -ForegroundColor Yellow
        continue
    }

    $index = [int] $Matches[2] - 1
    if ($index -lt 0 -or $index -ge $page.items.Count) {
        Write-Host 'Böyle bir sıra numarası yok.' -ForegroundColor Yellow
        continue
    }

    $action = if ($Matches[1] -eq 'o') { 'approve' } else { 'reject' }
    $item = $page.items[$index]
    $result = Invoke-RestMethod "$api/admin/skill-proposals/$($item.id)/$action" -Method Post -Headers $headers
    Write-Host ("{0}: {1}" -f $result.name, $result.status) -ForegroundColor Green
}
