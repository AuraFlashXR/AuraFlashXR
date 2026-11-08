# Script universel de desinstallation complete VB-CABLE / AuraFlashXR-MIC
# Supprime :
#   1. Tous les drivers VB-Audio du DriverStore (oemXX.inf)
#   2. Tous les endpoints audio VB-Audio du registre (Render + Capture)
#   3. Redemarre les services audio
[CmdletBinding()]
param()

$csharp = @"
using System;
using System.Runtime.InteropServices;

public class WinPriv {
    [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
    public static extern bool AdjustTokenPrivileges(IntPtr htok, bool disall, ref TOKEN_PRIVILEGES newst, int len, IntPtr prev, IntPtr relen);
    [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
    public static extern bool OpenProcessToken(IntPtr h, int acc, ref IntPtr phtok);
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern bool LookupPrivilegeValue(string host, string name, ref long pluid);

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TOKEN_PRIVILEGES {
        public int Count;
        public long Luid;
        public int Attr;
    }

    public const int SE_PRIVILEGE_ENABLED = 0x00000002;
    public const int TOKEN_QUERY = 0x00000008;
    public const int TOKEN_ADJUST_PRIVILEGES = 0x00000020;

    public static void Enable(string priv) {
        IntPtr htok = IntPtr.Zero;
        if (OpenProcessToken(System.Diagnostics.Process.GetCurrentProcess().Handle, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, ref htok)) {
            long luid = 0;
            if (LookupPrivilegeValue(null, priv, ref luid)) {
                TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES();
                tp.Count = 1;
                tp.Luid = luid;
                tp.Attr = SE_PRIVILEGE_ENABLED;
                AdjustTokenPrivileges(htok, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
            }
        }
    }
}
"@
Add-Type -TypeDefinition $csharp -ErrorAction SilentlyContinue
[WinPriv]::Enable("SeTakeOwnershipPrivilege")
[WinPriv]::Enable("SeBackupPrivilege")
[WinPriv]::Enable("SeRestorePrivilege")
[WinPriv]::Enable("SeLoadDriverPrivilege")

$adminSid = New-Object System.Security.Principal.SecurityIdentifier("S-1-5-32-544")
$driverKey = '{b3f8fa53-0004-438e-9003-51a46e139bfc},6'

# Sans elevation, les cles MMDevices sont en lecture seule : le nettoyage echoue silencieusement.
$isAdminUninstall = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdminUninstall) {
    Write-Host "  [INFO] Elevation en administrateur..." -ForegroundColor Yellow
    try {
        Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$($MyInvocation.MyCommand.Path)`"") -Verb RunAs -Wait
        exit 0
    } catch {
        Write-Host "  [ERREUR] Elevation refusee : $_" -ForegroundColor Red
        exit 1
    }
}

# Donner les droits FullControl a l'admin sur une cle de registre (chemin relatif HKLM)
function Grant-FullControl {
    param([string]$RelPath)
    try {
        $regKey = [Microsoft.Win32.Registry]::LocalMachine.OpenSubKey($RelPath, [Microsoft.Win32.RegistryKeyPermissionCheck]::ReadWriteSubTree, [System.Security.AccessControl.RegistryRights]::TakeOwnership)
        if ($regKey) {
            $acl = $regKey.GetAccessControl([System.Security.AccessControl.AccessControlSections]::None)
            $acl.SetOwner($adminSid)
            $regKey.SetAccessControl($acl)
            $regKey.Close()
        }
        $regKey = [Microsoft.Win32.Registry]::LocalMachine.OpenSubKey($RelPath, [Microsoft.Win32.RegistryKeyPermissionCheck]::ReadWriteSubTree, [System.Security.AccessControl.RegistryRights]::ChangePermissions)
        if ($regKey) {
            $acl = $regKey.GetAccessControl()
            $rule = New-Object System.Security.AccessControl.RegistryAccessRule($adminSid, [System.Security.AccessControl.RegistryRights]::FullControl, [System.Security.AccessControl.AccessControlType]::Allow)
            $acl.AddAccessRule($rule)
            $regKey.SetAccessControl($acl)
            $regKey.Close()
        }
    } catch {
        Write-Host "    [WARN] Grant-FullControl: $_" -ForegroundColor DarkYellow
    }
}

Write-Host ""
Write-Host "===============================================================" -ForegroundColor Red
Write-Host "  DESINSTALLATION COMPLETE VB-CABLE / AuraFlashXR-MIC" -ForegroundColor Red
Write-Host "===============================================================" -ForegroundColor Red
Write-Host ""

# ============================================================
# Etape 1 : Suppression des entrees "Ajout/Suppression de programmes"
# ============================================================
Write-Host "1. Nettoyage des entrees Ajout/Suppression de programmes..." -ForegroundColor Cyan
Write-Host ""

$uninstallPaths = @(
    "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
    "SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
)
$uninstallRemoved = 0
foreach ($basePath in $uninstallPaths) {
    $fullPath = "HKLM:\$basePath"
    if (-not (Test-Path $fullPath)) { continue }
    Get-ChildItem $fullPath -ErrorAction SilentlyContinue | ForEach-Object {
        $keyPath = $_.PSPath
        try {
            $props = Get-ItemProperty -Path $keyPath -ErrorAction SilentlyContinue
            $displayName = $props.DisplayName
            if ($displayName -and ($displayName -match 'VB-Audio|VB-CABLE|VBCABLE|CABLE')) {
                Write-Host "  Entree trouvee : '$displayName'" -ForegroundColor Yellow
                Grant-FullControl "$basePath\$($_.PSChildName)"
                Remove-Item -Path $keyPath -Recurse -Force -ErrorAction Stop
                Write-Host "  [OK] Supprimee." -ForegroundColor Green
                $uninstallRemoved++
            }
        } catch {
            Write-Host "  [INFO] Impossible de supprimer '$displayName': $_" -ForegroundColor DarkYellow
        }
    }
}
if ($uninstallRemoved -eq 0) {
    Write-Host "  Aucune entree VB-CABLE trouvee dans Ajout/Suppression de programmes." -ForegroundColor Gray
} else {
    Write-Host "  $uninstallRemoved entree(s) supprimee(s)." -ForegroundColor Green
}
Write-Host ""

# ============================================================
# Etape 2 : Suppression des drivers du DriverStore
# ============================================================
Write-Host "2. Recherche et suppression des drivers VB-CABLE..." -ForegroundColor Cyan
Write-Host ""

$drivers = pnputil /enum-drivers 2>&1
if (-not $drivers) {
    Write-Host "  Impossible d'enumerer les drivers." -ForegroundColor Red
} else {
    $text = ($drivers | Out-String)
    $blocks = $text -split "`r`n`r`n"
    $found = 0
    foreach ($block in $blocks) {
        if ($block -match 'VB-Audio|VBCABLE|vbMmeCable|CABLE') {
            if ($block -match '(oem\d+\.inf)') {
                $oem = $matches[1]
                Write-Host "  Driver trouve : $oem" -ForegroundColor Yellow
                Write-Host "  Suppression en cours..." -ForegroundColor Gray

                $null = pnputil /delete-driver $oem /uninstall /force 2>&1
                if ($LASTEXITCODE -eq 0) {
                    Write-Host "  [OK] $oem supprime (avec /uninstall)." -ForegroundColor Green
                    $found++
                } else {
                    $null = pnputil /delete-driver $oem /force 2>&1
                    if ($LASTEXITCODE -eq 0) {
                        Write-Host "  [OK] $oem supprime (sans /uninstall)." -ForegroundColor Green
                        $found++
                    } else {
                        Write-Host "  [INFO] Impossible de supprimer $oem (code $LASTEXITCODE)" -ForegroundColor DarkYellow
                    }
                }
            }
        }
    }
    Write-Host ""
    if ($found -eq 0) {
        Write-Host "  Aucun driver VB-CABLE trouve dans le DriverStore." -ForegroundColor Gray
    } else {
        Write-Host "  $found driver(s) supprime(s) du DriverStore." -ForegroundColor Green
    }
}
Write-Host ""

# ============================================================
# Etape 3 : Suppression des endpoints du registre
# ============================================================
Write-Host "3. Nettoyage des endpoints audio dans le registre..." -ForegroundColor Cyan
Write-Host ""

$renderRoot = "SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render"
$captureRoot = "SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture"
$propsNameKey = '{a45c254e-df1c-4efd-8020-67d146a850e0},2'

function Get-RegProp {
    param([object]$Props, [string]$Name)
    if ($null -eq $Props) { return $null }
    $p = $Props.PSObject.Properties[$Name]
    if ($p) { return $p.Value }
    return $null
}

function Remove-VBEndpoints {
    param([string]$RootKey, [string]$Label)
    $removed = 0
    $toRemove = @()

    Get-ChildItem "HKLM:\$RootKey" -ErrorAction SilentlyContinue | ForEach-Object {
        $subRelPath = "$RootKey\$($_.PSChildName)"
        $propPath = "HKLM:\$subRelPath\Properties"
        if (Test-Path $propPath) {
            $props = Get-ItemProperty -Path $propPath -ErrorAction SilentlyContinue
            $driver = Get-RegProp $props $driverKey
            $name = Get-RegProp $props $propsNameKey
            if ($driver -like '*VB-Audio*') {
                $toRemove += $subRelPath
                Write-Host "  [$Label] Endpoint trouve : '$name'" -ForegroundColor Yellow
            }
        }
    }

    foreach ($path in $toRemove) {
        # DeleteSubKeyTree attend le chemin RELATIF (sans HKLM:) et un seul argument en .NET Framework
        $relPath = $path -replace '^HKLM:\\', ''
        try {
            Grant-FullControl $relPath
            [Microsoft.Win32.Registry]::LocalMachine.DeleteSubKeyTree($relPath)
            Write-Host "  [$Label] [OK] Supprime : $relPath" -ForegroundColor Green
            $removed++
        } catch {
            Write-Host "  [$Label] [ERREUR] $relPath : $_" -ForegroundColor Red
        }
    }
    return $removed
}

$r1 = Remove-VBEndpoints $renderRoot "LECTURE"
$r2 = Remove-VBEndpoints $captureRoot "ENREGISTREMENT"
$totalRemoved = $r1 + $r2

Write-Host ""
if ($totalRemoved -eq 0) {
    Write-Host "  Aucun endpoint VB-Audio trouve dans le registre." -ForegroundColor Gray
} else {
    Write-Host "  $totalRemoved endpoint(s) supprime(s) du registre." -ForegroundColor Green
}
Write-Host ""

# ============================================================
# Etape 4 : Nettoyage des peripheriques dans PnP
# ============================================================
Write-Host "4. Nettoyage des peripheriques audio PnP..." -ForegroundColor Cyan
Write-Host ""

# Rechercher et supprimer les peripheriques VB-Audio via pnputil /enum-devices
$devices = pnputil /enum-devices /class MEDIA 2>&1
if ($devices) {
    $text = ($devices | Out-String)
    $blocks = $text -split "`r`n`r`n"
    $devRemoved = 0
    foreach ($block in $blocks) {
        if ($block -match 'VB-Audio|VBCABLE|vbMmeCable|CABLE') {
            # Extraire l'instance ID (ligne apres "ID d'instance" ou "Instance ID")
            if ($block -match '(?m)^(?:ID d''instance|Instance ID)\s*:\s*(.+)$') {
                $instanceId = $matches[1].Trim()
                Write-Host "  Peripherique trouve : $instanceId" -ForegroundColor Yellow
                $null = pnputil /remove-device $instanceId 2>&1
                if ($LASTEXITCODE -eq 0) {
                    Write-Host "  [OK] Peripherique supprime." -ForegroundColor Green
                    $devRemoved++
                } else {
                    Write-Host "  [INFO] Code retour $LASTEXITCODE (peut-etre deja supprime)" -ForegroundColor DarkYellow
                }
            }
        }
    }
    if ($devRemoved -eq 0) {
        Write-Host "  Aucun peripherique VB-Audio actif trouve." -ForegroundColor Gray
    } else {
        Write-Host "  $devRemoved peripherique(s) supprime(s)." -ForegroundColor Green
    }
}
Write-Host ""

# ============================================================
# Etape 5 : Redemarrage des services audio
# ============================================================
Write-Host "5. Redemarrage des services audio Windows..." -ForegroundColor Cyan
Stop-Service -Name Audiosrv -Force -ErrorAction SilentlyContinue
Stop-Service -Name AudioEndpointBuilder -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1
Start-Service -Name AudioEndpointBuilder -ErrorAction SilentlyContinue
Start-Service -Name Audiosrv -ErrorAction SilentlyContinue
Write-Host "  Services audio redemarres." -ForegroundColor Green
Write-Host ""

# ============================================================
# Resume
# ============================================================
Write-Host "===============================================================" -ForegroundColor Green
Write-Host "  DESINSTALLATION TERMINEE" -ForegroundColor Green
Write-Host "===============================================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Drivers supprimes, endpoints nettoyes, services redemarres." -ForegroundColor Gray
Write-Host "  REDEMARREZ votre ordinateur pour finaliser la suppression." -ForegroundColor Yellow
Write-Host ""

exit 0
