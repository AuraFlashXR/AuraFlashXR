@echo off
chcp 65001 >nul
setlocal EnableDelayedExpansion

set "TOOLS_DIR=%~dp0..\"
set "ROOT_DIR=%TOOLS_DIR%..\"

echo [DEBUG] TOOLS_DIR = %TOOLS_DIR%
echo [DEBUG] ROOT_DIR  = %ROOT_DIR%
echo.

set "ADB_EXE="
    echo [DEBUG] ADB found via ROOT_DIR
) else if exist "%TOOLS_DIR%adbforwarder\adb\platform-tools\adb.exe" (
    set "ADB_EXE=%TOOLS_DIR%adbforwarder\adb\platform-tools\adb.exe"
    echo [DEBUG] ADB found via TOOLS_DIR
) else (
    echo [DEBUG] ADB NOT found via either path
    where adb >nul 2>&1
    if !errorlevel! equ 0 (
        for /f "usebackq delims=" %%I in (`where adb`) do (
            if not defined ADB_EXE set "ADB_EXE=%%I"
        )
        echo [DEBUG] ADB found via PATH: !ADB_EXE!
    ) else (
        echo [DEBUG] ADB NOT in PATH either
    )
)

echo.
echo [DEBUG] Final ADB_EXE = %ADB_EXE%
if defined ADB_EXE (
    "%ADB_EXE%" version
) else (
    echo [DEBUG] ADB_EXE is EMPTY - this is the bug
)
pause
