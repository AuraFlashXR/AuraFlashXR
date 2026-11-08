# One-time setup of a builder (the Windows PC that builds and runs AuraFlashXR; see docs/DEVELOPMENT.md). Idempotent:
#  1. checks the toolchain (git, rustup/cargo, cmake, clang, nuget, Visual Studio 2022 with C++),
#  2. clones AuraFlashXR v20.14.1 and VirtualDesktop-OpenXR at the commits pinned in deps.lock.json (<root>\_tmp\deps.lock.json)
#     into <Src> (default <root>\src; root = $env:AURAFLASHXR_ROOT or %USERPROFILE%\openxr) and verifies them (existing clones are verified, never changed),
#  3. builds what the later stages expect: VDXR (unpatched, + out\vdxr-null), the OpenXR loader (build\oxr-sdk), AuraFlashXR's
#     server_core once (fetches the crates).
# -CloneOnly stops after step 2 (used to test the clone step into another folder).
param([string]$Src = '', [switch]$CloneOnly)
$ErrorActionPreference = 'Continue'
$root = if ($env:AURAFLASHXR_ROOT) { $env:AURAFLASHXR_ROOT } else { (Resolve-Path "$PSScriptRoot\..\..").Path }
if (-not $Src) { $Src = "$root\src" }
$env:Path = [System.Environment]::GetEnvironmentVariable('Path','Machine') + ';' + [System.Environment]::GetEnvironmentVariable('Path','User') + ';C:\Program Files\LLVM\bin'
$fails = 0
function Step($name, $ok, $detail) { if (-not $ok) { $script:fails++ }; Write-Host ("SETUP {0} {1}: {2}" -f $(if ($ok) { 'ok  ' } else { 'FAIL' }), $name, $detail) }
New-Item -ItemType Directory -Force $Src, "$root\logs", "$root\out", "$root\build" | Out-Null

# ---- 1. toolchain
$vs = @('C:\Program Files\Microsoft Visual Studio\2022\Community', 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools', 'C:\Program Files\Microsoft Visual Studio\2022\BuildTools') | Where-Object { Test-Path "$_\MSBuild\Current\Bin\MSBuild.exe" } | Select-Object -First 1
foreach ($t in @(@('git', 'git --version'), @('cargo', 'cargo --version'), @('cmake', 'cmake --version'), @('clang', 'clang --version'), @('nuget', 'nuget help'))) {
  $v = (cmd /c "$($t[1]) 2>&1" | Select-Object -First 1)
  Step "tool_$($t[0])" ($LASTEXITCODE -eq 0 -or $v) "$v"
}
Step 'visual_studio_2022' (Test-Path "$vs\MSBuild\Current\Bin\MSBuild.exe") "$vs (Desktop development with C++)"
Step 'dotnet_framework_48' (Test-Path 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Windows.Forms.dll') 'WinForms assemblies (GUIs)'

# ---- 2. local sources (no git, no network)
$a = "$Src/AuraFlashXR-v0.1"
$local = (Resolve-Path "$root\..\SOURCE CODE APK AuraFlashXR" -ErrorAction SilentlyContinue)
if ($local -and -not (Test-Path "$a\Cargo.toml")) {
  New-Item -ItemType Directory -Force $a | Out-Null
  robocopy $local.Path $a /MIR /XD target /NFL /NDL /NJH /NJS /NP | Out-Null
}
Step 'auraflashxr_source' (Test-Path "$a\Cargo.toml") "local workspace $a"
Step 'auraflashxr_openvr_headers' (Test-Path "$a\openvr\headers\openvr_driver.h") 'openvr\headers'
$v = "$Src\VDXR"
Step 'vdxr_source' (Test-Path "$v\VirtualDesktop-OpenXR.sln") "local checkout $v"

# ---- 3. prerequisites of the later stages
$msbuild = "$vs\MSBuild\Current\Bin\MSBuild.exe"
Set-Location $v
nuget restore VirtualDesktop-OpenXR.sln 2>&1 | Out-File -Encoding utf8 "$root\logs\setup_nuget.log"
& $msbuild virtualdesktop-openxr\virtualdesktop-openxr.vcxproj /p:Configuration=Release /p:Platform=x64 "/p:SolutionDir=$v\\" /m /v:minimal /nologo 2>&1 |
  Out-File -Encoding utf8 "$root\logs\setup_vdxr.log"
$vd = "$v\bin\x64\Release\virtualdesktop-openxr.dll"
Step 'vdxr_build' (Test-Path $vd) 'logs\setup_vdxr.log'
New-Item -ItemType Directory -Force "$root\out\vdxr-null" | Out-Null
foreach ($f in 'virtualdesktop-openxr.dll', 'virtualdesktop-openxr.json') { Copy-Item "$v\bin\x64\Release\$f" "$root\out\vdxr-null" -Force -ErrorAction SilentlyContinue }
Step 'vdxr_runtime_manifest' (Test-Path "$root\out\vdxr-null\virtualdesktop-openxr.json") 'out\vdxr-null'
$bld = "$root\build\oxr-sdk"
cmake -S "$v\external\OpenXR-SDK-Source" -B $bld -G "Visual Studio 17 2022" -A x64 -DBUILD_TESTS=ON -DBUILD_CONFORMANCE_TESTS=OFF 2>&1 | Out-File -Encoding utf8 "$root\logs\setup_oxr_sdk.log"
cmake --build $bld --config Release --target openxr_loader hello_xr -j 2>&1 | Out-File -Encoding utf8 -Append "$root\logs\setup_oxr_sdk.log"
Step 'openxr_loader' (Test-Path "$bld\src\loader\Release\openxr_loader.lib") 'logs\setup_oxr_sdk.log'
Set-Location $a
cargo build -p auraflashxr_server_core --release 2>&1 | Out-File -Encoding utf8 "$root\logs\setup_server_core.log"
Step 'auraflashxr_server_core' (Test-Path "$a\target\release\auraflashxr_server_core.dll") 'logs\setup_server_core.log'
if ($fails -eq 0) { Write-Host 'RESULT: OK' } else { Write-Host "RESULT: FAILED ($fails)" }
