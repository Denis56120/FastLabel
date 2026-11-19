@echo off
setlocal
chcp 65001 >nul
title Build FastLabel Agent + Installateur Inno Setup

set "SCRIPT_DIR=%~dp0"
if "%SCRIPT_DIR:~-1%"=="\" set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"

rem --- Detection automatique de FastLabel.Service et FastLabel.Installer ---
set "SERVICE_DIR=%SCRIPT_DIR%\FastLabel.Service"
if not exist "%SERVICE_DIR%\FastLabel.Service.csproj" (
    if exist "%SCRIPT_DIR%\..\FastLabel.Service\FastLabel.Service.csproj" (
        set "SERVICE_DIR=%SCRIPT_DIR%\..\FastLabel.Service"
    )
)

set "INSTALLER_DIR=%SCRIPT_DIR%\FastLabel.Installer"
if not exist "%INSTALLER_DIR%\fastlabel_setup.iss" (
    if exist "%SCRIPT_DIR%\..\FastLabel.Installer\fastlabel_setup.iss" (
        set "INSTALLER_DIR=%SCRIPT_DIR%\..\FastLabel.Installer"
    )
)

set "PROJECT_PATH=%SERVICE_DIR%\FastLabel.Service.csproj"
set "PUBLISH_DIR=%SERVICE_DIR%\bin\Release\net10.0\win-x64\publish"
set "ISS_FILE=%INSTALLER_DIR%\fastlabel_setup.iss"

rem --- Localisation de ISCC.exe (Inno Setup Compiler) ---
set "ISCC_EXE=D:\Program Files\Inno Setup 7\ISCC.exe"

echo ========================================================
echo 1. Nettoyage de l'ancien dossier publish...
echo ========================================================
if not exist "%PUBLISH_DIR%" goto :after_clean
rmdir /s /q "%PUBLISH_DIR%"
if errorlevel 1 (
    echo [ERREUR] Impossible de supprimer "%PUBLISH_DIR%" ^(un fichier est peut-etre verrouille^).
    pause
    exit /b 1
)
:after_clean

echo.
echo ========================================================
echo 2. Publication du service (.NET 10 / win-x64)...
echo ========================================================
if not exist "%PROJECT_PATH%" (
    echo [ERREUR] Projet introuvable : "%PROJECT_PATH%".
    pause
    exit /b 1
)

dotnet publish "%PROJECT_PATH%" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "%PUBLISH_DIR%"
if errorlevel 1 (
    echo.
    echo [ERREUR] Echec de dotnet publish.
    pause
    exit /b 1
)

if not exist "%PUBLISH_DIR%\FastLabel.Service.exe" (
    echo.
    echo [ERREUR] FastLabel.Service.exe introuvable dans "%PUBLISH_DIR%".
    pause
    exit /b 1
)

echo.
echo ========================================================
echo 3. Compilation de l'installateur dans FastLabel.Installer...
echo ========================================================
if not exist "%ISCC_EXE%" (
    echo [ERREUR] ISCC.exe introuvable. Verifie l'installation d'Inno Setup 6.
    pause
    exit /b 1
)

if not exist "%ISS_FILE%" (
    echo [ERREUR] Fichier ISS introuvable : "%ISS_FILE%".
    pause
    exit /b 1
)

if not exist "%INSTALLER_DIR%" mkdir "%INSTALLER_DIR%"

"%ISCC_EXE%" /O"%INSTALLER_DIR%" "%ISS_FILE%"
if errorlevel 1 (
    echo.
    echo [ERREUR] Echec de la compilation Inno Setup.
    pause
    exit /b 1
)

echo.
echo ========================================================
echo [SUCCES] Installateur publie dans :
echo          "%INSTALLER_DIR%\FastLabel_Setup_v1.0.0.exe"
echo ========================================================
if not defined BATCH_MODE pause