# Builds streamer.exe, configure.exe, performance.exe, launcher.exe (all go to the package's bin\) and AuraFlashXR.exe (the stub that starts bin\launcher.exe: the only file in the package root) (C# WinForms, .NET Framework 4.8 = part of Windows) with Visual Studio's Roslyn compiler
# against the framework's own assemblies (no SDK needed). Sources: _tmp\gui (uploaded by `run.py build gui`). Output: out\gui.
$ErrorActionPreference = 'Continue'
$root = if ($env:AURAFLASHXR_ROOT) { $env:AURAFLASHXR_ROOT } else { (Resolve-Path "$PSScriptRoot\..\..").Path }
$src = "$root\tools\gui"
$out = "$root\out\gui"
$csc = (@('C:\Program Files\Microsoft Visual Studio\2022\Community', 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools', 'C:\Program Files\Microsoft Visual Studio\2022\BuildTools') | Where-Object { Test-Path "$_\MSBuild\Current\Bin\Roslyn\csc.exe" } | Select-Object -First 1) + '\MSBuild\Current\Bin\Roslyn\csc.exe'
$fw = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
New-Item -ItemType Directory -Force $out | Out-Null
Set-Content -Path "$out\build.log" -Value '' -Encoding UTF8
$refs = 'System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll', 'System.Web.Extensions.dll', 'System.IO.Compression.dll', 'System.IO.Compression.FileSystem.dll', 'System.Xml.dll' | ForEach-Object { "/r:$fw\$_" }
# the OpenXR registration script travels inside the exes (no .ps1 in the package): Paths.RegisterScript writes it to %TEMP%\AuraFlashXR before powershell runs it
$common = @('/nologo', '/noconfig', '/target:winexe', '/platform:x64', '/optimize+', '/langversion:latest', '/nowarn:1701,1702',
            "/win32icon:$src\res\auraflashxr.ico", "/win32manifest:$src\app.manifest", "/resource:$src\res\logo_640.png,logo_640.png", "/resource:$src\res\Logo-allonger.png,Logo-allonger.png", "/resource:$src\res\emblem.png,emblem.png", "/resource:$src\res\auraflashxr.ico,auraflashxr.ico", "/resource:$src\res\Orbitron.ttf,Orbitron.ttf", "/resource:$src\res\backv2.jpg,back.png",
            "/resource:$src\res\fonts\Orbitron-Regular.ttf,fonts/Orbitron-Regular.ttf", "/resource:$src\res\fonts\Orbitron-SemiBold.ttf,fonts/Orbitron-SemiBold.ttf", "/resource:$src\res\fonts\Orbitron-Bold.ttf,fonts/Orbitron-Bold.ttf", "/resource:$src\res\fonts\Orbitron-ExtraBold.ttf,fonts/Orbitron-ExtraBold.ttf",
            "/resource:$src\res\fonts\Montserrat-Regular.otf,fonts/Montserrat-Regular.otf", "/resource:$src\res\fonts\Montserrat-SemiBold.otf,fonts/Montserrat-SemiBold.otf", "/resource:$src\res\fonts\Montserrat-Bold.otf,fonts/Montserrat-Bold.otf", "/resource:$src\res\fonts\Montserrat-ExtraBold.otf,fonts/Montserrat-ExtraBold.otf",
            "/resource:$src\res\fonts\CAS-Astro-Voyager.otf,fonts/CAS-Astro-Voyager.otf", "/resource:$src\res\fonts\Flymax.ttf,fonts/Flymax.ttf", "/resource:$src\res\fonts\Savore.ttf,fonts/Savore.ttf", "/resource:$src\res\fonts\Yotsuba.ttf,fonts/Yotsuba.ttf",
            "/resource:$root\tools\install\register_openxr_runtime.ps1,register_openxr_runtime.ps1") + $refs
$ok = $true
# fonds du menu BACKGROUND : chaque image de res\background devient la ressource bg\<nom>
$bgDir = "$src\res\background"
if (Test-Path $bgDir) {
    $common += Get-ChildItem $bgDir -File | Where-Object { $_.Extension -match '^\.(jpg|jpeg|png|bmp)$' } | ForEach-Object { "/resource:$($_.FullName),bg\$($_.Name)" }
}
foreach ($t in @(@{ exe = 'streamer.exe'; files = @("$src\Common.cs", "$src\Lang.cs", "$src\PresetMenu.cs", "$src\Streamer.cs", "$src\FindHeadset.cs", "$src\AudioRoute.cs") }, @{ exe = 'configure.exe'; files = @("$src\Common.cs", "$src\Lang.cs", "$src\PresetMenu.cs", "$src\AudioRoute.cs", "$src\Configure.cs", "$src\SettingsPanel.cs", "$src\Reference.cs", "$src\TestRunner.cs", "$src\Wizard.cs", "$src\Profiles.cs", "$src\Verdict.cs", "$src\Advice.cs") }, @{ exe = 'launcher.exe'; files = @("$src\Common.cs", "$src\Lang.cs", "$src\PresetMenu.cs", "$src\Launcher.cs") }, @{ exe = 'AuraFlashXR.exe'; files = @("$src\Lang.cs", "$src\Stub.cs"); manifest = 'app.stub.manifest'; plain = $true }, @{ exe = 'performance.exe'; files = @("$src\Common.cs", "$src\Lang.cs", "$src\Performance.cs") })) {
    $c = if ($t.manifest) { $common | ForEach-Object { if ($_ -like '/win32manifest:*') { "/win32manifest:$src\$($t.manifest)" } else { $_ } } } else { $common }   # the stub keeps its own manifest (DPI awareness for its message box)
    $o = & $csc @c "/out:$out\$($t.exe)" @($t.files) 2>&1
    ($o | Out-String) | Add-Content -Path "$out\build.log" -Encoding UTF8
    $o | ForEach-Object { Write-Host ("$_" -replace "[^\x20-\x7E]", '?') }
    if ($LASTEXITCODE -ne 0) { $ok = $false; Write-Host "FAILED: $($t.exe)" } else { Write-Host "built $out\$($t.exe)" }
}
if ($ok) { Write-Host 'RESULT: OK' } else { Write-Host 'RESULT: FAILED' }
