@echo off
setlocal
title Desinstallation AuraFlashXR-MIC / VB-CABLE
chcp 65001 >nul

:: ============================================================
::  Verification des privileges Administrateur
:: ============================================================
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo Ce script necessite des privileges Administrateur.
    echo Re-execution automatique avec elevation UAC...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

:: ============================================================
::  Chemins relatifs universels
:: ============================================================
set "TOOLS_DIR=%~dp0..\"
set "PS_SCRIPT=%~dp0uninstall_auraflashxr_mic.ps1"

if not exist "%PS_SCRIPT%" (
    echo [ERREUR] Script introuvable : %PS_SCRIPT%
    pause
    exit /b 1
)

echo ===============================================================
echo     DESINSTALLATION AURAFLASHXR-MIC / VB-CABLE
echo ================================================================
echo.
echo  Ce script va supprimer :
echo    - Tous les drivers VB-CABLE du DriverStore Windows
echo    - Tous les endpoints audio VB-Audio (Lecture + Enregistrement)
echo    - Tous les peripheriques PnP associes
echo.
echo  Un redemarrage sera necessaire ensuite.
echo.
pause
echo.

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PS_SCRIPT%"

echo.
pause
exit /b 0
