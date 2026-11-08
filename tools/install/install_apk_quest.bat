@echo off
rem Console en UTF-8 : affichage correct des accents francais sur toutes les machines
chcp 65001 >nul
setlocal EnableDelayedExpansion
title Installation APK AuraFlashXR sur le Casque VR (Quest 3 / Pro / 2)

set "TOOLS_DIR=%~dp0..\"
set "ROOT_DIR=%TOOLS_DIR%..\"

set "ADB_EXE="
if exist "%TOOLS_DIR%adbforwarder\adb\platform-tools\adb.exe" (
    set "ADB_EXE=%TOOLS_DIR%adbforwarder\adb\platform-tools\adb.exe"
) else (
    where adb >nul 2>&1
    if !errorlevel! equ 0 (
        for /f "usebackq delims=" %%I in (`where adb`) do (
            if not defined ADB_EXE set "ADB_EXE=%%I"
        )
    )
)

echo ===============================================================
echo     INSTALLATION DE L'APK AURAFLASHXR SUR LE CASQUE VR
echo ===============================================================
echo.

rem ADB introuvable (ni embarque, ni dans le PATH) : message clair et sortie propre
if not defined ADB_EXE (
    echo [ERREUR] adb.exe introuvable.
    echo          - Soit le dossier tools\adbforwarder\adb\platform-tools est absent du paquet.
    echo          - Soit adb n'est pas installe ni ajoute au PATH.
    echo          - Certains antivirus bloquent aussi adb.exe : verifie ton antivirus.
    pause
    exit /b 1
)

rem APK unique : moteur OpenGL ES 3.2 (affichage) + decodeur Vulkan PyroWave embarque
set "APK_FILE=%TOOLS_DIR%APK\AuraFlashXR-V0.apk"
echo [APK] AuraFlashXR-V0.apk (OpenGL ES 3.2 + PyroWave Vulkan)
echo.

if not exist "%APK_FILE%" (
    echo [ERREUR] Fichier introuvable : %APK_FILE%
    pause
    exit /b 1
)

echo.
echo Verification du casque connecte...

rem Nettoie les connexions TCP residuelles (ex: 127.0.0.1:5037 offline)
"%ADB_EXE%" disconnect >nul 2>&1

rem Compte les appareils USB reels (etat "device", serial sans ":")
set "DEV_COUNT=0"
set "DEV_SERIAL="
set "HAS_UNAUTHORIZED=0"
for /f "usebackq skip=1 tokens=1,2" %%A in (`"%ADB_EXE%" devices`) do (
    if "%%B"=="device" (
        set "CUR_SERIAL=%%A"
        if "!CUR_SERIAL::=!"=="!CUR_SERIAL!" (
            set /a DEV_COUNT+=1
            if not defined DEV_SERIAL set "DEV_SERIAL=%%A"
        )
    )
    if "%%B"=="unauthorized" (
        set "CUR_SERIAL=%%A"
        if "!CUR_SERIAL::=!"=="!CUR_SERIAL!" set "HAS_UNAUTHORIZED=1"
    )
)

if "!HAS_UNAUTHORIZED!"=="1" (
    echo [INFO] Le casque est connecte mais le debogage USB n'est pas autorise.
    echo        - Sur le casque, accepte la fenetre "Autoriser le debogage USB".
    echo        - Coche "Toujours autoriser depuis cet ordinateur" si propose.
)

if !DEV_COUNT! equ 0 (
    echo [ERREUR] Aucun casque detecte en USB.
    echo           - Verifie le cable USB et l'autorisation USB sur le casque.
    echo           - Installe les pilotes USB Quest si le casque n'apparait pas du tout.
    echo           - Si le casque affiche "Autoriser le debogage USB", accepte-le.
    pause
    exit /b 1
)

if !DEV_COUNT! equ 1 (
    echo.
    echo [INFO] Casque detecte : !DEV_SERIAL!
    echo Installation en cours...
    "%ADB_EXE%" -s !DEV_SERIAL! install -r -d "%APK_FILE%"
    goto :result
)

rem Plusieurs casques USB detectes : menu de choix
echo.
echo Plusieurs casques detectes :
set "IDX=1"
for /f "usebackq skip=1 tokens=1,2" %%A in (`"%ADB_EXE%" devices`) do (
    if "%%B"=="device" (
        set "CUR_SERIAL=%%A"
        if "!CUR_SERIAL::=!"=="!CUR_SERIAL!" (
            echo   [!IDX!] !CUR_SERIAL!
            set "DEV_SERIAL_!IDX!=!CUR_SERIAL!"
            set /a IDX+=1
        )
    )
)
set /p "DEV_CHOICE=Numero du casque a utiliser [Defaut: 1]: "
rem Validation : le choix doit etre strictement numerique
echo !DEV_CHOICE!| findstr /r "^[0-9][0-9]*$" >nul
if errorlevel 1 (
    echo [ERREUR] Choix invalide : "!DEV_CHOICE!" n'est pas un numero.
    pause
    exit /b 1
)
if not defined DEV_CHOICE set "DEV_CHOICE=1"
set "TARGET_SERIAL="
call set "TARGET_SERIAL=%%DEV_SERIAL_!DEV_CHOICE!%%"
if not defined TARGET_SERIAL (
    echo [ERREUR] Choix invalide.
    pause
    exit /b 1
)
echo.
echo [INFO] Casque selectionne : !TARGET_SERIAL!
echo Installation en cours...
"%ADB_EXE%" -s !TARGET_SERIAL! install -r -d "%APK_FILE%"

:result
if !errorlevel! equ 0 (
    echo.
    echo ===============================================================
    echo   [SUCCES] APK installe avec succes sur votre casque !
    echo ===============================================================
) else (
    echo.
    echo [ECHEC] Erreur lors de l'installation de l'APK.
)

pause
