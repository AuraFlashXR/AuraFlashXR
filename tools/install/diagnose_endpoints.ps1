# Diagnostic des endpoints VB-Audio dans le registre
$renderRoot = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render'
$captureRoot = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture'
$propsKey = '{a45c254e-df1c-4efd-8020-67d146a850e0},2'
$driverKey = '{b3f8fa53-0004-438e-9003-51a46e139bfc},6'

Write-Host '=== RENDER (Lecture) ===' -ForegroundColor Cyan
Get-ChildItem "$renderRoot" -ErrorAction SilentlyContinue | ForEach-Object {
    $propPath = "$renderRoot\$($_.PSChildName)\Properties"
    if (Test-Path $propPath) {
        $props = Get-ItemProperty -Path $propPath -ErrorAction SilentlyContinue
        $name = $props.$propsKey
        $driver = $props.$driverKey
        if ($driver -like '*VB-Audio*' -or $name -like '*CABLE*' -or $name -like '*Speakers*') {
            Write-Host "  Nom: '$name' | Driver: '$driver'"
        }
    }
}

Write-Host ''
Write-Host '=== CAPTURE (Enregistrement) ===' -ForegroundColor Cyan
Get-ChildItem "$captureRoot" -ErrorAction SilentlyContinue | ForEach-Object {
    $propPath = "$captureRoot\$($_.PSChildName)\Properties"
    if (Test-Path $propPath) {
        $props = Get-ItemProperty -Path $propPath -ErrorAction SilentlyContinue
        $name = $props.$propsKey
        $driver = $props.$driverKey
        if ($driver -like '*VB-Audio*' -or $name -like '*CABLE*') {
            Write-Host "  Nom: '$name' | Driver: '$driver'"
        }
    }
}
