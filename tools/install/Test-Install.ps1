# Exercises register_openxr_runtime.ps1 (portable AuraFlashXR folder) against a THROWAWAY registry key under HKCU (never HKLM)
# and scratch folders: register, re-register, WhatIf, unregister (restores the previous runtime), no previous runtime, a
# foreign runtime activated meanwhile, a moved folder, and taking over an earlier install (AuraFlashXR / AuraFlashXR in Program Files).
$ErrorActionPreference = 'Stop'
$root = if ($env:AuraFlashXR_ROOT) { $env:AuraFlashXR_ROOT } else { "$env:USERPROFILE\openxr" }
$pkg = "$root\out\dist\AuraFlashXR"
$tmp = "$root\runs\install_test"
$testRoot = 'HKCU:\Software\AuraFlashXRTest'
$reg = "$testRoot\Khronos\OpenXR\1"
$state = "$testRoot\State"
$legacyState = "$testRoot\LegacyState"
$inst = "$tmp\AuraFlashXR"                # "unzipped" here
$manifest = "$inst\bin\virtualdesktop-openxr.json"
$fail = 0
function Check($name, $ok, $detail = '') { if ($ok) { Write-Host "CHECK $name PASS $detail" } else { Write-Host "CHECK $name FAIL $detail"; $script:fail++ } }
function Active { (Get-ItemProperty -Path $reg -Name ActiveRuntime -ErrorAction SilentlyContinue).ActiveRuntime }
function Saved { (Get-ItemProperty -Path $state -Name PreviousActiveRuntime -ErrorAction SilentlyContinue).PreviousActiveRuntime }

if (Test-Path $testRoot) { Remove-Item $testRoot -Recurse -Force }
if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
New-Item -ItemType Directory -Force $tmp | Out-Null
Copy-Item $pkg $inst -Recurse
New-Item -Path $reg -Force | Out-Null
New-ItemProperty -Path $reg -Name ActiveRuntime -Value 'C:\fake\previous_runtime.json' -PropertyType String | Out-Null
New-Item -Path "$reg\AvailableRuntimes" | Out-Null
New-ItemProperty -Path "$reg\AvailableRuntimes" -Name 'C:\fake\sibling_runtime.json' -Value 0 -PropertyType DWord | Out-Null   # another runtime next to ours

$oldPf = @("$tmp\pf_AuraFlashXR", "$tmp\pf_AuraFlashXR")
$oldCfg = @("$tmp\lad_AuraFlashXR_host", "$tmp\lad_AuraFlashXR_host")
$common = @{ RegistryRoot = $reg; StateKey = $state; LegacyStateKey = $legacyState; NoFirewall = $true; Quiet = $true; OldInstallDirs = $oldPf; OldConfigDirs = $oldCfg }
# the registration script no longer ships in bin\: it is embedded in the exes (Paths.RegisterScript extracts it to %TEMP%\AuraFlashXR).
# The test extracts it once from launcher.exe the same way, then exercises it directly (the registry parameters stay overridable).
$ps = "$tmp\register_openxr_runtime.ps1"
$bytes = $null
try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem | Out-Null
    $zip = [IO.Compression.ZipFile]::OpenRead("$inst\bin\launcher.exe")
    $entry = $zip.GetEntry('register_openxr_runtime.ps1')
    if ($entry) { $ms = New-Object IO.MemoryStream; $entry.Open().CopyTo($ms); [IO.File]::WriteAllBytes($ps, $ms.ToArray()); $bytes = $ms.ToArray() }
    $zip.Dispose()
} catch { }
Check 'register_script_embedded' ($null -ne $bytes) 'extracted from launcher.exe resources'

Check 'package_layout' ((Test-Path "$inst\AuraFlashXR.exe") -and (Test-Path "$inst\bin\auraflashxr_host.exe") -and (Test-Path "$inst\bin\LibOVRRT64_1.dll") -and (Test-Path "$inst\bin\virtualdesktop-openxr.dll") -and
    (Test-Path $manifest) -and (-not (Test-Path "$inst\bin\register_openxr_runtime.ps1")) -and (Test-Path "$inst\config\session.default.json"))
$rootFiles = @(Get-ChildItem $inst -File | ForEach-Object { $_.Name })
Check 'root_has_only_the_launcher' ($rootFiles.Count -eq 1 -and $rootFiles[0] -eq 'AuraFlashXR.exe') ($rootFiles -join ', ')
& $ps @common | Out-Null
Check 'active_runtime_is_this_folder' ((Active) -eq $manifest) (Active)
Check 'available_runtimes_entry' ($null -ne (Get-ItemProperty "$reg\AvailableRuntimes" -Name $manifest -ErrorAction SilentlyContinue))
Check 'previous_runtime_saved' ((Saved) -eq 'C:\fake\previous_runtime.json') (Saved)
Check 'sibling_runtime_kept' ($null -ne (Get-ItemProperty "$reg\AvailableRuntimes" -Name 'C:\fake\sibling_runtime.json' -ErrorAction SilentlyContinue))

& $ps @common | Out-Null
Check 'reregister_keeps_previous' ((Saved) -eq 'C:\fake\previous_runtime.json') (Saved)

& $ps @common -Unregister -WhatIf | Out-Null
Check 'whatif_changes_nothing' ((Active) -eq $manifest)

& $ps @common -Unregister | Out-Null
Check 'unregister_restores_previous' ((Active) -eq 'C:\fake\previous_runtime.json') (Active)
Check 'unregister_drops_available_entry' ($null -eq (Get-ItemProperty "$reg\AvailableRuntimes" -Name $manifest -ErrorAction SilentlyContinue))
Check 'unregister_keeps_folder' (Test-Path "$inst\bin\auraflashxr_host.exe")

Remove-ItemProperty -Path $reg -Name ActiveRuntime
& $ps @common | Out-Null
& $ps @common -Unregister | Out-Null
Check 'unregister_without_previous_removes_value' ($null -eq (Active))

& $ps @common | Out-Null
Set-ItemProperty -Path $reg -Name ActiveRuntime -Value 'C:\other\runtime.json'
& $ps @common -Unregister | Out-Null
Check 'unregister_leaves_foreign_runtime' ((Active) -eq 'C:\other\runtime.json')

# moved folder: register from the new place; the old place is ours and must not become "the previous runtime"
Set-ItemProperty -Path $reg -Name ActiveRuntime -Value 'C:\fake\previous_runtime.json'
& $ps @common | Out-Null
$moved = "$tmp\moved\AuraFlashXR"
New-Item -ItemType Directory -Force (Split-Path $moved) | Out-Null
Copy-Item $inst $moved -Recurse
& $ps @common -InstallDir $moved | Out-Null
Check 'moved_folder_is_active' ((Active) -eq "$moved\bin\virtualdesktop-openxr.json") (Active)
Check 'moved_folder_keeps_previous' ((Saved) -eq 'C:\fake\previous_runtime.json') (Saved)
Check 'moved_folder_old_entry_removed' ($null -eq (Get-ItemProperty "$reg\AvailableRuntimes" -Name $manifest -ErrorAction SilentlyContinue))
& $ps @common -InstallDir $moved -Unregister | Out-Null

# earlier install: AuraFlashXR in "Program Files" with its marker, its state key, its config with a paired headset
$cx = $oldPf[0]
New-Item -ItemType Directory -Force "$cx\runtime", "$cx\host", $oldCfg[1] | Out-Null
Set-Content "$cx\auraflashxr-install.json" '{}'
Set-Content "$cx\host\auraflashxr_host.exe" ''
Set-Content "$cx\runtime\virtualdesktop-openxr.json" '{}'
Set-Content "$($oldCfg[1])\session.json" '{"client_connections":{"x":{}}}'
Set-Content "$($oldCfg[1])\auraflashxr_views.json" '{}'
if (Test-Path $state) { Remove-Item $state -Recurse -Force }
New-Item -Path $legacyState -Force | Out-Null
New-ItemProperty -Path $legacyState -Name PreviousActiveRuntime -Value 'C:\fake\before_auraflashxr.json' -PropertyType String | Out-Null
Set-ItemProperty -Path $reg -Name ActiveRuntime -Value "$cx\runtime\virtualdesktop-openxr.json"
New-ItemProperty -Path "$reg\AvailableRuntimes" -Name "$cx\runtime\virtualdesktop-openxr.json" -Value 0 -PropertyType DWord -Force | Out-Null
if (Test-Path "$inst\config\session.json") { Remove-Item "$inst\config\session.json" }
& $ps @common | Out-Null
Check 'takeover_active_runtime_is_ours' ((Active) -eq $manifest) (Active)
Check 'takeover_previous_from_auraflashxr' ((Saved) -eq 'C:\fake\before_auraflashxr.json') (Saved)
Check 'takeover_old_entry_removed' ($null -eq (Get-ItemProperty "$reg\AvailableRuntimes" -Name "$cx\runtime\virtualdesktop-openxr.json" -ErrorAction SilentlyContinue))
Check 'takeover_legacy_state_removed' (-not (Test-Path $legacyState))
Check 'takeover_config_copied' ((Test-Path "$inst\config\session.json") -and (Test-Path "$inst\config\auraflashxr_views.json"))
Check 'takeover_old_files_removed' (-not (Test-Path $cx))
& $ps @common -Unregister | Out-Null
Check 'takeover_unregister_restores_pre_auraflashxr' ((Active) -eq 'C:\fake\before_auraflashxr.json') (Active)
Check 'sibling_runtime_kept_at_end' ($null -ne (Get-ItemProperty "$reg\AvailableRuntimes" -Name 'C:\fake\sibling_runtime.json' -ErrorAction SilentlyContinue))

Remove-Item $testRoot -Recurse -Force
Remove-Item $tmp -Recurse -Force
Write-Host "RESULT: $(if ($fail -eq 0) { 'PASS' } else { "FAIL ($fail)" })"
