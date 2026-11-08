# Script universel de desinstallation des drivers VB-CABLE (AuraFlashXR)
# Supprime tous les drivers VB-Audio trouves dans le store de drivers Windows
[CmdletBinding()]
param()

Write-Host ""
Write-Host "Recherche des drivers VB-CABLE existants..." -ForegroundColor Cyan

$drivers = pnputil /enum-drivers 2>&1
if (-not $drivers) {
    Write-Host "  Impossible d'enumerer les drivers." -ForegroundColor Red
    exit 1
}

# Convertir en texte et decouper en blocs (separe par ligne vide)
$text = ($drivers | Out-String)
$blocks = $text -split "`r`n`r`n"

$found = 0
foreach ($block in $blocks) {
    # Verifier si c'est un driver VB-Audio
    if ($block -match 'VB-Audio|VBCABLE|vbMmeCable|CABLE') {
        # Extraire le nom oemXX.inf
        if ($block -match '(oem\d+\.inf)') {
            $oem = $matches[1]
            Write-Host "  Driver trouve : $oem" -ForegroundColor Yellow
            Write-Host "  Suppression en cours..." -ForegroundColor Gray

            $result = pnputil /delete-driver $oem /uninstall /force 2>&1
            $code = $LASTEXITCODE

            if ($code -eq 0) {
                Write-Host "  [OK] $oem supprime." -ForegroundColor Green
                $found++
            } else {
                Write-Host "  [INFO] Code retour $code pour $oem (peut-etre deja supprime ou en cours d'utilisation)" -ForegroundColor DarkYellow
                # Essayer sans /uninstall si /uninstall echoue
                $result2 = pnputil /delete-driver $oem /force 2>&1
                if ($LASTEXITCODE -eq 0) {
                    Write-Host "  [OK] $oem supprime (sans /uninstall)." -ForegroundColor Green
                    $found++
                }
            }
        }
    }
}

Write-Host ""
if ($found -eq 0) {
    Write-Host "  Aucun driver VB-CABLE a supprimer." -ForegroundColor Gray
} else {
    Write-Host "  $found driver(s) supprime(s)." -ForegroundColor Green
}

# Petit delai pour laisser Windows traiter la suppression
Start-Sleep -Seconds 2

exit 0
