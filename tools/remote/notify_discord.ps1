# Publie les patch notes de docs\PATCH_NOTES.md dans le salon Discord « Patch-Notes » (webhook).
#   -All      : publie tous les patches (premier envoi)
#   sans flag  : publie seulement les patches pas encore envoyes (etat dans logs\discord_sent.txt)
# L'URL du webhook vient de $env:AURAFLASHXR_DISCORD_WEBHOOK (jamais en dur dans le depot).
param([switch]$All, [string]$Webhook = '')
$ErrorActionPreference = 'Stop'
$root = if ($env:AURAFLASHXR_ROOT) { $env:AURAFLASHXR_ROOT } else { (Resolve-Path "$PSScriptRoot\..\..").Path }
$notes = "$root\docs\PATCH_NOTES.md"
if (-not $Webhook) { $Webhook = $env:AURAFLASHXR_DISCORD_WEBHOOK }
if (-not $Webhook) { Write-Host "RESULT: FAILED (AURAFLASHXR_DISCORD_WEBHOOK absent)"; exit 1 }
if (-not (Test-Path $notes)) { Write-Host "RESULT: FAILED (docs\PATCH_NOTES.md absent)"; exit 1 }

# decoupe en blocs « ## Patch : Nom | DD/MM/YYYY — HH:MM » + paragraphe
# lecture explicite en UTF-8 (Windows PowerShell lit sinon en ANSI : accents et tirets casses)
$text = [IO.File]::ReadAllText($notes, [Text.Encoding]::UTF8)
$rx = [regex]'(?ms)^## Patch\s*:\s*(.+?)\s*\|\s*(.+?)\s*\r?\n+(.*?)(?=^## Patch\s*:|\z)'
$entries = @()
foreach ($m in $rx.Matches($text)) {
    $entries += [pscustomobject]@{ Title = $m.Groups[1].Value.Trim(); Date = $m.Groups[2].Value.Trim(); Body = $m.Groups[3].Value.Trim() }
}
if ($entries.Count -eq 0) { Write-Host "RESULT: FAILED (aucun patch trouve)"; exit 1 }

$sentFile = "$root\logs\discord_sent.txt"
$sent = @{}
if ((Test-Path $sentFile) -and -not $All) { foreach ($l in Get-Content $sentFile) { if ($l.Trim()) { $sent[$l.Trim()] = $true } } }

$n = 0
foreach ($e in $entries) {
    $key = "$($e.Title) | $($e.Date)"
    if (-not $All -and $sent.ContainsKey($key)) { continue }
    $body = $e.Body
    if ($body.Length -gt 3500) { $body = $body.Substring(0, 3500) + "..." }
    $payload = @{
        username = 'AuraFlashXR'
        embeds   = @(@{
            title       = "Patch : $($e.Title)"
            description = $body
            color       = 15167402
            footer      = @{ text = "$($e.Date)   ·   AuraFlashXR V0" }
        })
    } | ConvertTo-Json -Depth 6 -Compress
    try {
        Invoke-RestMethod -Uri $Webhook -Method Post -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($payload)) | Out-Null
        $n++
        Start-Sleep -Milliseconds 2200   # limite Discord (5 messages / 5 s par webhook, marge pour les gros embeds)
        if (-not $All) { Add-Content -Path $sentFile -Value $key -Encoding UTF8 }
        elseif (-not $sent.ContainsKey($key)) { Add-Content -Path $sentFile -Value $key -Encoding UTF8 }
        Write-Host "sent: $key"
    } catch {
        Write-Host "RESULT: FAILED ($key : $($_.Exception.Message))"
        exit 1
    }
}
Write-Host "RESULT: OK ($n patch(es) envoye(s))"
