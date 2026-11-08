# Assembles the portable AuraFlashXR folder out\dist\AuraFlashXR (what a user unzips anywhere) and out\dist\AuraFlashXR-<version>.zip.
# Layout: the root holds ONE file, AuraFlashXR.exe (a stub that starts bin\launcher.exe); everything else is in a subfolder:
#   bin\   everything the launcher starts or loads: launcher.exe, streamer.exe, configure.exe (setup wizard and --diagnose), performance.exe (each with its .exe.config), auraflashxr_host.exe,
#          and the OpenXR runtime (LibOVRRT64_1.dll, virtualdesktop-openxr.dll + .json: VDXR loads the shim from its own folder);
#          the OpenXR registration script is embedded in the exes (Paths.RegisterScript extracts it to %TEMP%), no .ps1 ships
#   bench\, profiles\, config\, docs\, logs\ (created on use)
$ErrorActionPreference = 'Continue'
$root = if ($env:AURAFLASHXR_ROOT) { $env:AURAFLASHXR_ROOT } else { (Resolve-Path "$PSScriptRoot\..\..").Path }
$version = if ($env:AURAFLASHXR_VERSION) { $env:AURAFLASHXR_VERSION } else { '1.1.0-pre' }
$pkg = "$root\out\dist\AuraFlashXR"   # a folder no earlier layout used (out\auraflashxr = the old installer package)
# an earlier layout (loose files in the root, or libs\ / licenses\ folders) would hide a broken new one: the old folder is moved aside once, never deleted
$stale = (Test-Path $pkg) -and ((@(Get-ChildItem $pkg -File | Where-Object { $_.Name -ne 'AuraFlashXR.exe' }).Count -gt 0) -or (Test-Path "$pkg\libs") -or (Test-Path "$pkg\licenses") -or (Test-Path "$pkg\bin\logs"))
if ($stale) { Move-Item $pkg "$pkg.old-$((Get-Date).ToString('yyyyMMdd-HHmmss'))" }
New-Item -ItemType Directory -Force $pkg, "$pkg\config", "$pkg\bin", "$pkg\docs" | Out-Null
# presets shipped by an earlier build (e.g. the removed LOW/MID/HIGH/OPTI CONFIG) must not survive in the package
if (Test-Path "$pkg\config") {
    Get-ChildItem "$pkg\config" -Directory | Where-Object { Test-Path "$($_.FullName)\config\session.json" } | Remove-Item -Recurse -Force
}
Copy-Item "$root\out\vdxr-shim\*.dll", "$root\out\vdxr-shim\*.json" "$pkg\bin" -Force                 # virtualdesktop-openxr.dll / .json, LibOVRRT64_1.dll
Copy-Item "$root\src\AuraFlashXR-v0.1\target\release\auraflashxr_host.exe" "$pkg\bin" -Force
foreach ($f in 'streamer.exe', 'configure.exe', 'performance.exe', 'launcher.exe') {
    if (Test-Path "$root\out\gui\$f") { Copy-Item "$root\out\gui\$f" "$pkg\bin" -Force }
    Copy-Item "$root\_tmp\gui\app.exe.config" "$pkg\bin\$f.config" -Force   # WinForms' per-monitor DPI mode (tools/gui/app.exe.config)
}
if (Test-Path "$root\out\mirrordesktop\mirrordesktop.exe") { Copy-Item "$root\out\mirrordesktop\mirrordesktop.exe" "$pkg\bin" -Force }   # the desktop mirror (an OpenXR app the streamer starts)
if (Test-Path "$root\out\gui\AuraFlashXR.exe") { Copy-Item "$root\out\gui\AuraFlashXR.exe" $pkg -Force }   # the stub: the only file in the root (starts bin\launcher.exe)
if (Test-Path "$pkg\bin\register_openxr_runtime.ps1") { Remove-Item "$pkg\bin\register_openxr_runtime.ps1" -Force }   # embedded in the exes since V0: never ship a .ps1
Copy-Item "$root\_tmp\install\README.txt" "$pkg\docs" -Force
Copy-Item "$root\_tmp\install\session.default.json" "$pkg\config\session.default.json" -Force
# licences: notices, the project licence (if present) and the upstream texts
New-Item -ItemType Directory -Force "$pkg\docs\licenses" | Out-Null
Copy-Item "$root\_tmp\install\THIRD_PARTY_NOTICES.md" "$pkg\docs" -Force
if (Test-Path "$root\_tmp\install\LICENSE.txt") { Copy-Item "$root\_tmp\install\LICENSE.txt" "$pkg\docs" -Force }
if (Test-Path "$root\_tmp\install\PATCH_NOTES.md") { Copy-Item "$root\_tmp\install\PATCH_NOTES.md" "$pkg\docs" -Force }   # FIX 2026-11-01: patch notes embarqués au package
# fonds du launcher : docs\images\launcher (scanné par le menu BACKGROUND ; l'utilisateur peut y déposer les siens)
# miroir exact du dossier de fonds : les images retirées du projet disparaissent aussi du package
if (Test-Path "$pkg\docs\images\launcher") { Remove-Item "$pkg\docs\images\launcher" -Recurse -Force }
New-Item -ItemType Directory -Force "$pkg\docs\images\launcher" | Out-Null
if (Test-Path "$root\tools\gui\res\background") { Copy-Item "$root\tools\gui\res\background\*" "$pkg\docs\images\launcher" -Force }
# only the licence texts the repo lists (_tmp\install\licenses.txt, written by run.py): the staging folder and the package folder keep files
# the repo has dropped since, which must not ship
$licenses = @(Get-Content "$root\_tmp\install\licenses.txt" | ForEach-Object { $_.Trim() } | Where-Object { $_ })
foreach ($l in $licenses) { Copy-Item "$root\_tmp\install\licenses\$l" "$pkg\docs\licenses" -Force }
# the benchmark scene (tools/bench_scene/build.sh, uploaded by `run.py build host`) — vit dans tools\bench (comme le driver)
New-Item -ItemType Directory -Force "$pkg\tools\bench" | Out-Null
if (Test-Path "$root\tools\bench\littlest_tokyo.vab") { Copy-Item "$root\tools\bench\littlest_tokyo.vab" "$pkg\tools\bench\littlest_tokyo.vab" -Force }
if (Test-Path "$root\tools\bench\sakura_breeze.mp3") { Copy-Item "$root\tools\bench\sakura_breeze.mp3" "$pkg\tools\bench\sakura_breeze.mp3" -Force }   # the scene's music (credits in THIRD_PARTY_NOTICES.md)
# the OpenXR scene app of the wizard's scene test (renders that scene through the real shim path with the headset's head pose)
if (Test-Path "$root\build\probe\xr_probe.exe") { Copy-Item "$root\build\probe\xr_probe.exe" "$pkg\tools\bench\xr_probe.exe" -Force }
# les langues (french.xml, english.xml) — vit dans tools\language
New-Item -ItemType Directory -Force "$pkg\tools\language" | Out-Null
if (Test-Path "$root\tools\language") { Copy-Item "$root\tools\language\*.xml" "$pkg\tools\language" -Force }
# the wizard's community headset profiles
New-Item -ItemType Directory -Force "$pkg\profiles" | Out-Null
if (Test-Path "$root\_tmp\install\profiles") { Copy-Item "$root\_tmp\install\profiles\*.json" "$pkg\profiles" -Force }
# tools\ de l'app : ADBForwarder (Quest en USB), driver SteamVR auraflashxr (Compatible BattlEye),
# install_apk_quest.bat + scripts micro VB-Cable (configure.exe), APK et driver VB-Audio.
# AdbForwarder vit DANS tools\ (la sonde Paths.AdbForwarder cherche tools\adbforwarder\ADBForwarder.exe).
if (Test-Path "$root\tools\adbforwarder") {
    New-Item -ItemType Directory -Force "$pkg\tools\adbforwarder" | Out-Null
    Copy-Item "$root\tools\adbforwarder\*" "$pkg\tools\adbforwarder\" -Recurse -Force   # contenu, pas le dossier (sinon imbrication)
}
foreach ($t in @('APK', 'vbcable', 'auraflashxr')) {
    # Copy-Item dossier->dossier existant IMBRIQUE le dossier dans lui-même (tools\auraflashxr\auraflashxr\):
    # copier le CONTENU (\*) vers la destination créée au préalable.
    if (Test-Path "$root\tools\$t") {
        New-Item -ItemType Directory -Force "$pkg\tools\$t" | Out-Null
        Copy-Item "$root\tools\$t\*" "$pkg\tools\$t\" -Recurse -Force
    }
}
New-Item -ItemType Directory -Force "$pkg\tools\install" | Out-Null
foreach ($t in @('install_apk_quest.bat', 'install_auraflashxr_mic.bat', 'uninstall_auraflashxr_mic.bat', 'uninstall_auraflashxr_mic.ps1', 'rename_mic.ps1')) {
    if (Test-Path "$root\_tmp\install\$t") { Copy-Item "$root\_tmp\install\$t" "$pkg\tools\install\$t" -Force }
}
# the built-in presets (one per encoder x level) : config\<nom>\config\session.json — listed by the launcher and configure.exe
foreach ($preset in @('OPTI H.264', 'OPTI HEVC', 'OPTI AV1')) {
    $src = "$root\_tmp\install\presets\$preset"
    # per-encoder presets are generated from config-presets\ at build time: never abort the package when one is missing
    if (-not (Test-Path "$src\config\session.json")) { Write-Host "preset skipped (missing): $preset"; continue }
    if (Test-Path "$src\config\session.json") {
        New-Item -ItemType Directory -Force "$pkg\config\$preset\config" | Out-Null
        Copy-Item "$src\config\session.json" "$pkg\config\$preset\config\session.json" -Force
        if (Test-Path "$src\config\auraflashxr.json") { Copy-Item "$src\config\auraflashxr.json" "$pkg\config\$preset\config\auraflashxr.json" -Force }   # settings du preset (sync au chargement)
    }
}
# config\auraflashxr.json racine : les réglages par défaut que le host lit (codec, idle_rgb, gamma…) — toujours embarqué
if (Test-Path "$root\config\auraflashxr.json") { Copy-Item "$root\config\auraflashxr.json" "$pkg\config\auraflashxr.json" -Force }
# personal build (harness/run.py package with AURAFLASHXR_PERSONAL=1): a ready config\auraflashxr.json and a zip suffix; a generic build leaves both empty
$versionFile = "$root\_tmp\install\version.txt"
if ((Test-Path $versionFile) -and ((Get-Content $versionFile -Raw).Trim().Length -gt 0)) { $version = (Get-Content $versionFile -Raw).Trim() }
$suffixFile = "$root\_tmp\install\version_suffix.txt"
if (Test-Path $suffixFile) { $version += (Get-Content $suffixFile -Raw).Trim() }
# the flavour of this build (harness/run.py writes _tmp\flavour.txt: debug = per-frame telemetry, release = none): bin\flavour.txt tells the windows, and a debug zip says so in its name
$flavour = if (Test-Path "$root\_tmp\flavour.txt") { (Get-Content "$root\_tmp\flavour.txt" -Raw).Trim() } else { 'debug' }
Set-Content -Path "$pkg\bin\flavour.txt" -Value $flavour -NoNewline
if ($flavour -ne 'release') { $version += '-debug' }
$persFile = "$root\_tmp\install\auraflashxr.personal.json"
$persShip = @()
if ((Test-Path $persFile) -and ((Get-Content $persFile -Raw).Trim().Length -gt 2)) { Copy-Item $persFile "$pkg\config\auraflashxr.json" -Force; $persShip = @('config\auraflashxr.json') }
if (Test-Path "$pkg\config\auraflashxr.json") { $persShip = @('config\auraflashxr.json') }   # FIX 2026-11-02: embarqué aussi sur un build générique
# personal build: extra files from the build PC (tools of the owner's own session, e.g. an audio switcher) in extras\ (never in the root)
$persFiles = @()
$pfList = "$root\_tmp\install\personal_files.txt"
if (Test-Path $pfList) { foreach ($line in (Get-Content $pfList)) { $f = $line.Trim(); if (-not $f) { continue }; if (-not (Test-Path $f)) { Write-Host "FAILED: personal extra file missing: $f (listed in extra_files)"; exit 1 }; New-Item -ItemType Directory -Force "$pkg\extras" | Out-Null; Copy-Item $f "$pkg\extras" -Force; $persFiles += ("extras\" + (Split-Path $f -Leaf)) } }
$zip = "$root\out\dist\AuraFlashXR-$version.zip"
# the zip holds only what ships (never logs\, config\session.json or results a test run may have left in the folder);
# everything sits under AuraFlashXR\ so it unzips to one folder. logs\ and the user's config files are created on first use.
$ship = @('AuraFlashXR.exe', 'bin\auraflashxr_host.exe', 'bin\streamer.exe', 'bin\configure.exe', 'bin\performance.exe', 'bin\launcher.exe', 'bin\mirrordesktop.exe', 'bin\streamer.exe.config', 'bin\configure.exe.config', 'bin\performance.exe.config', 'bin\launcher.exe.config', 'bin\flavour.txt',
          'bin\LibOVRRT64_1.dll', 'bin\virtualdesktop-openxr.dll', 'bin\virtualdesktop-openxr.json',
          'config\session.default.json', 'tools\bench\littlest_tokyo.vab', 'tools\bench\sakura_breeze.mp3', 'tools\bench\xr_probe.exe', 'tools\language\french.xml', 'tools\language\english.xml', 'docs\README.txt', 'docs\THIRD_PARTY_NOTICES.md', 'docs\LICENSE.txt', 'docs\PATCH_NOTES.md')
$ship += Get-ChildItem "$pkg\docs\images\launcher" -File | ForEach-Object { "docs\images\launcher\$($_.Name)" }   # fonds du launcher (menu BACKGROUND)
$ship += $persShip
$ship += $persFiles
$ship += Get-ChildItem "$pkg\profiles" -File -Filter *.json | ForEach-Object { "profiles\$($_.Name)" }
$ship += Get-ChildItem "$pkg\tools" -Recurse -File | ForEach-Object { "tools\" + $_.FullName.Substring("$pkg\tools\".Length) }   # adbforwarder + driver SteamVR + APK + vbcable + scripts install
$ship += Get-ChildItem "$pkg\config" -Directory | Where-Object { Test-Path "$($_.FullName)\config\session.json" } | ForEach-Object { "config\$($_.Name)\config\session.json"; if (Test-Path "$($_.FullName)\config\auraflashxr.json") { "config\$($_.Name)\config\auraflashxr.json" } }   # built-in presets (session + settings)
$ship += $licenses | ForEach-Object { "docs\licenses\$_" }
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$fs = [IO.File]::Open($zip, [IO.FileMode]::Create)
$za = New-Object IO.Compression.ZipArchive($fs, [IO.Compression.ZipArchiveMode]::Create)
foreach ($f in $ship) {
  if (Test-Path "$pkg\$f") { [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($za, "$pkg\$f", ('AuraFlashXR/' + $f.Replace('\', '/')), [IO.Compression.CompressionLevel]::Optimal) | Out-Null }
}
$za.Dispose(); $fs.Dispose()
Get-ChildItem $pkg -Recurse -File | Select-Object @{n='file';e={$_.FullName.Substring($pkg.Length + 1)}}, Length | Format-Table -AutoSize | Out-String -Width 200
$missing = @('AuraFlashXR.exe', 'bin\auraflashxr_host.exe', 'bin\streamer.exe', 'bin\configure.exe', 'bin\performance.exe', 'bin\launcher.exe', 'bin\launcher.exe.config', 'bin\streamer.exe.config', 'bin\LibOVRRT64_1.dll', 'bin\virtualdesktop-openxr.dll', 'bin\virtualdesktop-openxr.json', 'tools\bench\littlest_tokyo.vab', 'tools\bench\xr_probe.exe', 'docs\THIRD_PARTY_NOTICES.md', 'docs\licenses\AuraFlashXR-LICENSE.txt') | Where-Object { -not (Test-Path "$pkg\$_") }
if (Test-Path "$pkg\bin\register_openxr_runtime.ps1") { Write-Host "RESULT: FAILED bin\register_openxr_runtime.ps1 must not ship (embedded in the exes)"; exit 1 }
$rootFiles = @(Get-ChildItem $pkg -File | ForEach-Object { $_.Name })
if ($rootFiles.Count -ne 1 -or $rootFiles[0] -ne 'AuraFlashXR.exe') { Write-Host "RESULT: FAILED the root must hold only AuraFlashXR.exe, found: $($rootFiles -join ', ')"; exit 1 }
if ($missing) { Write-Host "RESULT: FAILED missing $($missing -join ', ')" } else { Write-Host "RESULT: OK $pkg ($zip)" }
