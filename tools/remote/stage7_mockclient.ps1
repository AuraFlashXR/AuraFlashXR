# Builds the headless mock client inside the builder's AuraFlashXR v20.14.1 checkout (workspace member via auraflashxr/* glob).
$ErrorActionPreference = 'Continue'
$root = if ($env:AURAFLASHXR_ROOT) { $env:AURAFLASHXR_ROOT } else { "$env:USERPROFILE\openxr" }
$dst  = "$root\src\AuraFlashXR-v0.1\auraflashxr\client_headless"
New-Item -ItemType Directory -Force "$dst\src" | Out-Null
Copy-Item "$root\_tmp\mock_client\Cargo.toml" "$dst\Cargo.toml" -Force
Copy-Item "$root\_tmp\mock_client\src\main.rs" "$dst\src\main.rs" -Force
Set-Location "$root\src\AuraFlashXR-v0.1"
$env:PATH += ";$env:USERPROFILE\.cargo\bin"
cargo build --release -p auraflashxr_client_headless 2>&1 | Out-File -Encoding utf8 "$root\logs\mockclient_build.log"
Get-Content "$root\logs\mockclient_build.log" | Select-String 'error|warning: unused|Finished|could not' | Select-Object -First 30
Get-Item "$root\src\AuraFlashXR-v0.1\target\release\auraflashxr_client_headless.exe" -ErrorAction SilentlyContinue | Select-Object FullName, Length, LastWriteTime
