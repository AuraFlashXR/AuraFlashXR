<#
Builds AuraFlashXR on Windows from this checkout (see docs/DEVELOPMENT.md for the prerequisites):

    powershell -ExecutionPolicy Bypass -File build.ps1                 # everything for the release zip
    powershell -ExecutionPolicy Bypass -File build.ps1 host gui        # only some parts (setup runs first if needed)

Add `debug` to keep the debug traces (default flavour: release). Targets, in build order: vdxr host shim mirrordesktop gui package (default: all five), plus client probe (test tools for the harness).
The first run clones AFXR v0.1 and VirtualDesktop-OpenXR at the commits in deps.lock.json into src\ and builds their
prerequisites (tools\remote\setup_builder.ps1, idempotent). Output: out\dist\AuraFlashXR\ and out\dist\AuraFlashXR-<version>.zip.
Logs: logs\build_<step>.txt. Nothing outside this folder is changed (no registry, no OpenXR runtime registration).
#>
param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Targets)
$ErrorActionPreference = 'Continue'
$repo = $PSScriptRoot
$env:AURAFLASHXR_ROOT = $repo          # the build scripts work in <root>\src, <root>\out, <root>\_tmp, <root>\logs
$all = @('vdxr', 'host', 'shim', 'mirrordesktop', 'gui', 'package')
# flavour: the release build (default) compiles the per-frame debug traces out; `debug` as a target keeps them (harness / developers)
$flavour = if ($Targets -contains 'debug') { 'debug' } else { 'release' }
$Targets = @($Targets | Where-Object { $_ -ne 'debug' -and $_ -ne 'release' })
if (-not $Targets -or $Targets.Count -eq 0) { $Targets = $all }
$known = $all + @('client', 'probe')
foreach ($t in $Targets) { if ($known -notcontains $t) { Write-Host "unknown target '$t' (known: $($known -join ', '))"; exit 3 } }
New-Item -ItemType Directory -Force "$repo\_tmp", "$repo\logs", "$repo\bench" | Out-Null
Set-Content -Path "$repo\_tmp\flavour.txt" -Value $flavour -NoNewline
Write-Host "flavour: $flavour"

function Stage($name, $script, $ok, [string[]]$a = @()) {
    Write-Host "== $name"
    $log = "$repo\logs\build_$name.txt"
    & powershell -NoProfile -ExecutionPolicy Bypass -File "$repo\tools\remote\$script" @a 2>&1 | Out-File -Encoding utf8 $log
    $text = Get-Content $log -Raw
    if ($text -notmatch $ok) {
        Write-Host "FAILED: $name (log: $log)"
        Get-Content $log | Select-Object -Last 25 | ForEach-Object { Write-Host "   $_" }
        exit 2
    }
    Write-Host "   ok"
}
function Put($from, $to) { New-Item -ItemType Directory -Force (Split-Path $to) | Out-Null; Copy-Item $from $to -Force }

# ---- the inputs the build scripts read from _tmp (the harness uploads the same files over SSH)
Put "$repo\deps.lock.json" "$repo\_tmp\deps.lock.json"
Put "$repo\tools\patches\loopback-client.patch" "$repo\_tmp\loopback-client.patch"
Put "$repo\tools\patches\client-stats-log.patch" "$repo\_tmp\client-stats-log.patch"
Put "$repo\tools\patches\client-caps-log.patch" "$repo\_tmp\client-caps-log.patch"
Put "$repo\tools\patches\audio-trace.patch" "$repo\_tmp\audio-trace.patch"
Put "$repo\tools\patches\audio-process-loopback.patch" "$repo\_tmp\audio-process-loopback.patch"
Put "$repo\tools\patches\client-audio-device.patch" "$repo\_tmp\client-audio-device.patch"
Put "$repo\tools\host\Cargo.toml" "$repo\_tmp\host\Cargo.toml"
Put "$repo\tools\host\build.rs" "$repo\_tmp\host\build.rs"
Get-ChildItem "$repo\tools\host\src\*.rs" | ForEach-Object { Put $_.FullName "$repo\_tmp\host\src\$($_.Name)" }
Put "$repo\tools\mock_client\Cargo.toml" "$repo\_tmp\mock_client\Cargo.toml"
Put "$repo\tools\mock_client\src\main.rs" "$repo\_tmp\mock_client\src\main.rs"
Get-ChildItem "$repo\ovrshim" -File | ForEach-Object { Put $_.FullName "$repo\_tmp\ovrshim\$($_.Name)" }
Get-ChildItem "$repo\tools\gui\*.cs", "$repo\tools\gui\app.manifest", "$repo\tools\gui\app.stub.manifest", "$repo\tools\gui\app.exe.config" | ForEach-Object { Put $_.FullName "$repo\_tmp\gui\$($_.Name)" }
Get-ChildItem "$repo\profiles\*.json" | ForEach-Object { Put $_.FullName "$repo\_tmp\install\profiles\$($_.Name)" }
# built-in presets (LOW/MID/HIGH/OPTI CONFIG): shipped as config\<nom>\config\session.json (stage13)
Get-ChildItem "$repo\config-presets" -Directory | ForEach-Object {
    New-Item -ItemType Directory -Force "$repo\_tmp\install\presets\$($_.Name)\config" | Out-Null
    foreach ($f in @('session.json', 'auraflashxr.json')) {   # session + settings: un preset doit embarquer les deux (sync au chargement)
        if (Test-Path "$($_.FullName)\config\$f") { Put "$($_.FullName)\config\$f" "$repo\_tmp\install\presets\$($_.Name)\config\$f" }
    }
}
foreach ($f in 'auraflashxr.ico', 'logo_640.png', 'Logo-allonger.png', 'emblem.png', 'backv2.jpg') { Put "$repo\tools\gui\res\$f" "$repo\_tmp\gui\res\$f" }
# polices du menu POLICE (res\fonts) : embarquées dans les exe GUI
if (Test-Path "$repo\tools\gui\res\fonts") { Get-ChildItem "$repo\tools\gui\res\fonts" -File | ForEach-Object { Put $_.FullName "$repo\_tmp\gui\res\fonts\$($_.Name)" } }
# fonds du launcher (menu BACKGROUND) : embarqués dans launcher.exe et copiés dans docs\images\launcher du package
# miroir exact du dossier de fonds : les images retirées de docs\images\background\launcher disparaissent aussi du build
if (Test-Path "$repo\tools\gui\res\background") { Remove-Item "$repo\tools\gui\res\background" -Recurse -Force }
if (Test-Path "$repo\_tmp\gui\res\background") { Remove-Item "$repo\_tmp\gui\res\background" -Recurse -Force }
if (Test-Path "$repo\docs\images\background\launcher") { Get-ChildItem "$repo\docs\images\background\launcher" -File | ForEach-Object { Put $_.FullName "$repo\tools\gui\res\background\$($_.Name)" } }
foreach ($f in 'register_openxr_runtime.ps1', 'session.default.json', 'README.txt', 'install_auraflashxr_mic.bat', 'uninstall_auraflashxr_mic.bat', 'uninstall_auraflashxr_mic.ps1', 'rename_mic.ps1', 'install_apk_quest.bat') {
    Put "$repo\tools\install\$f" "$repo\_tmp\install\$f"
}
Put "$repo\tools\install\register_openxr_runtime.ps1" "$repo\_tmp\gui\register_openxr_runtime.ps1"   # embedded in the GUI exes as a resource (stage16); no .ps1 ships in bin\
Put "$repo\THIRD_PARTY_NOTICES.md" "$repo\_tmp\install\THIRD_PARTY_NOTICES.md"
Put "$repo\LICENSE" "$repo\_tmp\install\LICENSE.txt"
Put "$repo\docs\PATCH_NOTES.md" "$repo\_tmp\install\PATCH_NOTES.md"
Get-ChildItem "$repo\third_party\licenses\*.txt" | ForEach-Object { Put $_.FullName "$repo\_tmp\install\licenses\$($_.Name)" }
Put "$repo\tools\probe\xr_probe.cpp" "$repo\_tmp\xr_probe.cpp"

# ---- the benchmark scene: converted in Linux by tools\bench_scene\build.sh, else taken from the published release (verified)
$vab = "$repo\tools\build\bench\littlest_tokyo.vab"
$vabSha = 'c2fe8ebe167282079adea9088c90a99e29a28d9f893b80c599e128be3f051a93'
if (Test-Path "$repo\tools\build\bench\littlest_tokyo.vab") { Copy-Item "$repo\tools\build\bench\littlest_tokyo.vab" $vab -Force }
Write-Host "== bench scene (disabled)"

# ---- pinned sources + prerequisites (skipped once done: the clones and the VDXR/OpenXR/server_core builds exist)
$ready = (Test-Path "$repo\src\AuraFlashXR-v0.1\target\release\auraflashxr_server_core.dll") -and (Test-Path "$repo\out\vdxr-null\virtualdesktop-openxr.json") -and
         (Test-Path "$repo\build\oxr-sdk\src\loader\Release\openxr_loader.lib")
if (-not $ready) { Stage 'setup' 'setup_builder.ps1' 'RESULT: OK' }

# ---- the builds, in dependency order
if ($Targets -contains 'host' -or $Targets -contains 'client') { Stage 'afxr_patch' 'stage9_patch.ps1' 'applied' }
if ($Targets -contains 'vdxr')    { Stage 'vdxr'    'stage15_vdxr.ps1'       'RESULT: OK' }
if ($Targets -contains 'host')    { Stage 'host'    'stage8_host.ps1'        'Finished' }
if ($Targets -contains 'client')  { Stage 'client'  'stage7_mockclient.ps1'  'Finished' }
if ($Targets -contains 'shim')    { Stage 'shim'    'stage11_ovrshim.ps1'    'RESULT: OK' }
if ($Targets -contains 'mirrordesktop') { Stage 'mirrordesktop' 'stage20_mirrordesktop.ps1' 'RESULT: OK' }
if ($Targets -contains 'probe')   { Stage 'probe'   'stage12_probe_build.ps1' 'RESULT: OK' }
if ($Targets -contains 'gui')     { Stage 'gui'     'stage16_gui.ps1'        'RESULT: OK' }
if ($Targets -contains 'package') { Stage 'package' 'stage13_package.ps1'    'RESULT: OK' }
Write-Host "RESULT: OK ($($Targets -join ' '))"
if ($Targets -contains 'package') { Write-Host "package: $repo\out\dist\AuraFlashXR (+ zip)" }
