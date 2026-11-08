@echo off
setlocal EnableDelayedExpansion
title Installation AuraFlashXR-MIC
chcp 65001 >nul

:: ============================================================
::  Privileges administrateur (elevation UAC si necessaire)
:: ============================================================
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo Elevation des privileges...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

:: Chemins relatifs universels (fonctionne sur n'importe quel PC)
set "TOOLS_DIR=%~dp0..\"
set "VBCABLE_DIR=%TOOLS_DIR%vbcable\"
set "SETUP_X64=%VBCABLE_DIR%VBCABLE_Setup_x64.exe"
set "INF_FILE=%VBCABLE_DIR%vbMmeCable64_win10.inf"
set "PS_UNINSTALL=%~dp0uninstall_auraflashxr_mic.ps1"
set "PS_RENAME=%~dp0rename_mic.ps1"

if not exist "%PS_UNINSTALL%" (
    echo [ERREUR] uninstall_auraflashxr_mic.ps1 introuvable : %PS_UNINSTALL%
    pause
    exit /b 1
)
if not exist "%PS_RENAME%" (
    echo [ERREUR] rename_mic.ps1 introuvable : %PS_RENAME%
    pause
    exit /b 1
)

echo ===============================================================
echo     INSTALLATION DU DRIVER AUDIO VIRTUEL AURAFLASHXR-MIC
echo ===============================================================
echo.

:: ============================================================
::  1. Nettoyage des anciens drivers VB-CABLE
:: ============================================================
echo 1. Nettoyage des anciens drivers VB-CABLE...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PS_UNINSTALL%"
echo.

:: ============================================================
::  2. Installation silencieuse du pilote VB-CABLE
::     pnputil installe le driver sans aucune fenetre ni clic.
:: ============================================================
echo 2. Installation silencieuse du pilote VB-Audio Cable...
if exist "%INF_FILE%" (
    pnputil.exe /add-driver "%INF_FILE%" /install
    echo    Code retour pnputil : %errorLevel%
) else (
    echo    [ERREUR] Fichier introuvable : %INF_FILE%
    pause
    exit /b 1
)
echo.

:: ============================================================
::  3. Renommage + activation du cable basique, desactivation du 16 canaux
:: ============================================================
echo 3. Configuration des endpoints audio...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PS_RENAME%"
echo.

echo ===============================================================
echo  Installation terminee !
echo.
echo  LECTURE        : AuraFlashXR-CABLE (actif)
echo  ENREGISTREMENT : AuraFlashXR-MIC (actif)
echo  Les cables 16 canaux sont desactives.
echo.
echo  Si les noms ne sont pas encore visibles, REDEMARREZ votre PC.
echo ===============================================================
echo.
pause
exit /b 0
