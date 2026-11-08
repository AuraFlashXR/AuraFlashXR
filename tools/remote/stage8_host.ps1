# Builds auraflashxr_host inside the builder's AuraFlashXR v20.14.1 checkout (workspace member via auraflashxr/* glob).
$ErrorActionPreference = 'Continue'
$root = if ($env:AURAFLASHXR_ROOT) { $env:AURAFLASHXR_ROOT } else { "$env:USERPROFILE\openxr" }
$dst  = "$root\src\AuraFlashXR-v0.1\auraflashxr\host_harness"
New-Item -ItemType Directory -Force "$dst\src" | Out-Null
Copy-Item "$root\_tmp\host\Cargo.toml" "$dst\Cargo.toml" -Force
Copy-Item "$root\_tmp\host\src\*.rs" "$dst\src\" -Force
Copy-Item "$root\_tmp\host\build.rs" "$dst\build.rs" -Force
Set-Location "$root\src\AuraFlashXR-v0.1"
$env:PATH += ";$env:USERPROFILE\.cargo\bin"
# flavour (run.py build --release): the release host drops the per-frame telemetry (cargo feature `telemetry`)
$flavour = if (Test-Path "$root\_tmp\flavour.txt") { (Get-Content "$root\_tmp\flavour.txt" -Raw).Trim() } else { 'debug' }
$cargoArgs = @('build', '--release', '-p', 'auraflashxr_host')
if ($flavour -eq 'release') { $cargoArgs += '--no-default-features' }
# simrig (harness/simrig, docs/DEVELOPMENT.md): the headset-less rig, a separate build that is never deployed as the installed host
if ($env:AURAFLASHXR_SIMRIG_BUILD -eq '1') { $cargoArgs += @('--features', 'simrig') }
Write-Host "flavour: $flavour$(if ($env:AURAFLASHXR_SIMRIG_BUILD -eq '1') { ' + simrig' })"
cargo @cargoArgs 2>&1 | Out-File -Encoding utf8 "$root\logs\host_build.log"
Get-Content "$root\logs\host_build.log" | Select-String -Pattern '^error|^warning: unused|Finished|could not' -Context 0,6 | Select-Object -First 40
Get-Item "$root\src\AuraFlashXR-v0.1\target\release\auraflashxr_host.exe" -ErrorAction SilentlyContinue | Select-Object FullName, Length, LastWriteTime
