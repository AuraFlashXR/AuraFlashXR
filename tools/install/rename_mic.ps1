# Script universel AuraFlashXR-MIC : renomme les endpoints VB-CABLE et desactive le cable 16 canaux.
#   LECTURE  (Render)        : le cable "basique" -> AuraFlashXR-CABLE ; le cable 16 canaux -> AuraFlashXR-CABLE in 16ch, DESACTIVE
#   ENREGISTREMENT (Capture) : le micro "basique" -> AuraFlashXR-MIC ; le micro 16 canaux -> AuraFlashXR-MIC 16ch, DESACTIVE
# Le cable basique reste actif : c'est celui que le host et le casque utilisent.
#
# Methode : PowerShell pur (pas de C# compile a la volee) ; les cles MMDevices appartiennent a SYSTEM avec des
# droits restreints, on prend donc le proprietaire puis on ajoute les droits Administrateurs avant d'ecrire.
[CmdletBinding()]
param([switch]$Quiet)

$ErrorActionPreference = 'Continue'

function Say([string]$text, [string]$color = "Gray") {
    if (-not $Quiet) { Write-Host $text -ForegroundColor $color }
}

# --- Elevation : sans administrateur, les cles MMDevices sont en lecture seule ---
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Say "  [INFO] Elevation en administrateur..." "Yellow"
    $self = $MyInvocation.MyCommand.Path
    $argLine = "-NoProfile -ExecutionPolicy Bypass -File `"$self`""
    if ($Quiet) { $argLine += " -Quiet" }
    try {
        Start-Process -FilePath 'powershell.exe' -ArgumentList $argLine -Verb RunAs -Wait
        exit 0
    } catch {
        Say "  [ERREUR] Elevation refusee : $_" "Red"
        exit 1
    }
}

# --- Privileges de sauvegarde/restauration (necessaires pour prendre possession) ---
$sig = @'
using System;
using System.Runtime.InteropServices;
public static class Priv {
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TOKEN_PRIVILEGES { public int Count; public long Luid; public int Attr; }
    [DllImport("advapi32.dll", SetLastError = true)] public static extern bool OpenProcessToken(IntPtr h, int acc, ref IntPtr phtok);
    [DllImport("advapi32.dll", SetLastError = true)] public static extern bool LookupPrivilegeValue(string host, string name, ref long pluid);
    [DllImport("advapi32.dll", SetLastError = true)] public static extern bool AdjustTokenPrivileges(IntPtr htok, bool disall, ref TOKEN_PRIVILEGES newst, int len, IntPtr prev, IntPtr relen);
    public static void Enable(string name) {
        IntPtr tok = IntPtr.Zero;
        if (!OpenProcessToken(System.Diagnostics.Process.GetCurrentProcess().Handle, 0x28, ref tok)) return;
        long luid = 0;
        if (!LookupPrivilegeValue(null, name, ref luid)) return;
        var tp = new TOKEN_PRIVILEGES { Count = 1, Luid = luid, Attr = 2 };
        AdjustTokenPrivileges(tok, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
    }
}
'@
try { Add-Type -TypeDefinition $sig -ErrorAction Stop } catch { }
foreach ($p in 'SeTakeOwnershipPrivilege', 'SeRestorePrivilege', 'SeBackupPrivilege') { try { [Priv]::Enable($p) } catch { } }

$PROPS_NAME   = '{a45c254e-df1c-4efd-8020-67d146a850e0},2'
$PROPS_DRIVER = '{b3f8fa53-0004-438e-9003-51a46e139bfc},6'
$STATE_ACTIVE = 1
$STATE_DISABLED = 0x10000001
$adminSid = New-Object System.Security.Principal.SecurityIdentifier("S-1-5-32-544")

function Get-RegProp {
    param([object]$Props, [string]$Name)
    if ($null -eq $Props) { return $null }
    $p = $Props.PSObject.Properties[$Name]
    if ($p) { return $p.Value }
    return $null
}

# Les cles MMDevices sont possedees par SYSTEM et n'accordent aux Administrateurs que SetValue : on ouvre donc
# avec le seul droit SetValue (ReadWriteSubTree demande ReadKey et echoue avec "Acces au registre demande non autorise").
function Set-RegValue {
    param([string]$RelPath, [string]$Name, $Value, [Microsoft.Win32.RegistryValueKind]$Kind = [Microsoft.Win32.RegistryValueKind]::String)
    $k = [Microsoft.Win32.Registry]::LocalMachine.OpenSubKey($RelPath, [Microsoft.Win32.RegistryKeyPermissionCheck]::ReadWriteSubTree, [System.Security.AccessControl.RegistryRights]::SetValue)
    if (-not $k) { throw "cle non ouvrable en ecriture : $RelPath" }
    try { $k.SetValue($Name, $Value, $Kind) } finally { $k.Close() }
}

# Seuls les endpoints du driver VB-Audio sont touches (jamais un vrai haut-parleur ni un casque).
# Le cable basique s'appelle "Speakers" (lecture) / "CABLE Output" (capture) chez VB-Audio : c'est le DRIVER qui decide.
function Test-IsVBCable {
    param([string]$Name, [string]$Driver)
    return ($Driver -match 'VB-Audio|VB-Cable|VB Cable|vbMmeCable')
}

function Test-Is16ch {
    param([string]$Name, [string]$Driver)
    return ($Name -match '16' -or $Driver -match '16ch|16 ch|16-channel')
}

Say ""
Say "===============================================================" "Cyan"
Say "  ENDPOINTS VB-CABLE (AuraFlashXR)" "Cyan"
Say "===============================================================" "Cyan"
Say ""

function Set-VBEndpoints {
    param([string]$RootKey, [string]$Label, [string]$BasicName, [string]$Name16ch)
    Say "$Label :" "Yellow"
    $renamed = 0; $stateChanged = 0

    Get-ChildItem "HKLM:\$RootKey" -ErrorAction SilentlyContinue | ForEach-Object {
        $relPath = "$RootKey\$($_.PSChildName)"
        $propPath = "HKLM:\$relPath\Properties"
        if (-not (Test-Path $propPath)) { return }

        $props = Get-ItemProperty -Path $propPath -ErrorAction SilentlyContinue
        $driver = [string](Get-RegProp $props $PROPS_DRIVER)
        $currentName = [string](Get-RegProp $props $PROPS_NAME)
        if (-not $currentName) { return }
        if (-not (Test-IsVBCable -Name $currentName -Driver $driver)) { return }

        $is16 = Test-Is16ch -Name $currentName -Driver $driver
        $newName = if ($is16) { $Name16ch } else { $BasicName }

        # 1) Nom
        if ($currentName -ne $newName) {
            try {
                Set-RegValue "$relPath\Properties" $PROPS_NAME $newName
                Say "  [OK] '$currentName' -> '$newName'" "Green"
                $renamed++
            } catch {
                Say "  [ECHEC] '$currentName' : $($_.Exception.Message)" "Red"
            }
        } else {
            Say "  [DEJA OK] '$currentName'" "DarkGray"
        }

        # 2) Etat : basique actif, 16 canaux desactive
        $state = 0
        $sk = Get-ItemProperty -Path "HKLM:\$relPath" -Name 'DeviceState' -ErrorAction SilentlyContinue
        if ($sk) { $state = [int]$sk.DeviceState }
        $wanted = if ($is16) { $STATE_DISABLED } else { $STATE_ACTIVE }
        if ($state -ne $wanted) {
            try {
                Set-RegValue $relPath 'DeviceState' $wanted ([Microsoft.Win32.RegistryValueKind]::DWord)
                if ($is16) { Say "  [DESACTIVE] '$newName'" "DarkYellow" } else { Say "  [ACTIVE] '$newName'" "Green" }
                $stateChanged++
            } catch {
                Say "  [ECHEC etat] '$newName' : $($_.Exception.Message)" "Red"
            }
        }
    }
    Say ""
    return @{ Renamed = $renamed; State = $stateChanged }
}

$render  = Set-VBEndpoints "SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render"  "LECTURE (Render)"        "AuraFlashXR-CABLE" "AuraFlashXR-CABLE in 16ch"
$capture = Set-VBEndpoints "SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture" "ENREGISTREMENT (Capture)" "AuraFlashXR-MIC"   "AuraFlashXR-MIC 16ch"

# Redemarrage des services audio pour appliquer les changements
Get-Process rundll32 -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -like '*Son*' -or $_.MainWindowTitle -like '*Sound*' } | Stop-Process -Force -ErrorAction SilentlyContinue
Stop-Service -Name Audiosrv -Force -ErrorAction SilentlyContinue
Stop-Service -Name AudioEndpointBuilder -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1
Start-Service -Name AudioEndpointBuilder -ErrorAction SilentlyContinue
Start-Service -Name Audiosrv -ErrorAction SilentlyContinue

$total = $render.Renamed + $capture.Renamed + $render.State + $capture.State
if ($total -eq 0) {
    Say "  [INFO] Aucun endpoint VB-Cable a modifier (driver absent ou deja configure)." "Yellow"
    Say ""
}

Say "===============================================================" "Green"
Say "  LECTURE        : AuraFlashXR-CABLE (actif)  |  AuraFlashXR-CABLE in 16ch (desactive)" "Green"
Say "  ENREGISTREMENT : AuraFlashXR-MIC (actif)    |  AuraFlashXR-MIC 16ch (desactive)" "Green"
Say "===============================================================" "Green"
