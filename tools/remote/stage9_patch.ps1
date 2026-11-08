# Checks the AuraFlashXR patches against the local workspace (no git: the workspace is a plain copy of
# SOURCE CODE APK AuraFlashXR, which already carries them):
#   loopback-client.patch   mock client / harness loopback
#   client-stats-log.patch  server_core logs the headset's raw per-frame statistics (host: client_frames.csv)
#   client-caps-log.patch   server_core logs the capabilities the headset advertised at the handshake (host: client_capabilities event)
#   audio-trace.patch       auraflashxr_audio: a trace hook in the capture callback (host telemetry.rs, debug flavour)
#   audio-process-loopback.patch, client-audio-device.patch  process loopback capture
# Each patch is recognised by a marker of the lines it adds; a missing one fails the stage.
$ErrorActionPreference = 'Continue'
$root = if ($env:AURAFLASHXR_ROOT) { $env:AURAFLASHXR_ROOT } else { "$env:USERPROFILE\openxr" }
Set-Location "$root\src\AuraFlashXR-v0.1"
$markers = @{
    'loopback-client.patch'        = @('auraflashxr/sockets/src/backend/udp.rs', 'AURAFLASHXR_BIND_IP')
    'client-stats-log.patch'       = @('auraflashxr/server_core/src/statistics.rs', 'va_client_stats')
    'client-caps-log.patch'        = @('auraflashxr/server_core/src/connection.rs', 'va_client_caps')
    'audio-trace.patch'            = @('auraflashxr/audio/src/lib.rs', 'pub static TRACE')
    'audio-process-loopback.patch' = @('auraflashxr/audio/src/process_loopback.rs', 'record_process_loopback_blocking')
    'client-audio-device.patch'    = @('auraflashxr/client_core/src/connection.rs', 'CLIENT_AUDIO_DEVICE')
}
$ok = $true
foreach ($name in $markers.Keys) {
    $file, $marker = $markers[$name]
    if ((Test-Path $file) -and (Select-String -Path $file -SimpleMatch $marker -Quiet)) { Write-Host "patch: $name present"; continue }
    Write-Host "patch: $name MISSING ($marker not found in $file)"; $ok = $false
}
if (-not $ok) { exit 1 }
Write-Host 'applied'
