@echo off
setlocal
chcp 65001 >nul
title Publish FastLabel.Client (Blazor WASM)

set "SCRIPT_DIR=%~dp0"
if "%SCRIPT_DIR:~-1%"=="\" set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"

rem --- Si le .bat est dans web\ : "%SCRIPT_DIR%\client\FastLabel.Client"
rem --- Si le .bat est dans web\client\ : "%SCRIPT_DIR%\FastLabel.Client"
set "CLIENT_DIR=%SCRIPT_DIR%\client\FastLabel.Client"
if not exist "%CLIENT_DIR%\FastLabel.Client.csproj" (
    if exist "%SCRIPT_DIR%\FastLabel.Client\FastLabel.Client.csproj" (
        set "CLIENT_DIR=%SCRIPT_DIR%\FastLabel.Client"
    )
)

set "PROJECT_PATH=%CLIENT_DIR%\FastLabel.Client.csproj"
set "PUBLISH_DIR=%CLIENT_DIR%\bin\Release\net10.0\publish"
set "DOCKER_DIR=%CLIENT_DIR%\docker"
set "WWWROOT_SRC=%PUBLISH_DIR%\wwwroot"
set "WWWROOT_DST=%DOCKER_DIR%\wwwroot"

echo ========================================================
echo 1. Nettoyage des anciens dossiers publish et wwwroot...
echo ========================================================
if exist "%PUBLISH_DIR%" (
    rmdir /s /q "%PUBLISH_DIR%"
    if errorlevel 1 (
        echo [ERREUR] Impossible de supprimer "%PUBLISH_DIR%".
        pause
        exit /b 1
    )
)

if exist "%WWWROOT_DST%" (
    rmdir /s /q "%WWWROOT_DST%"
    if errorlevel 1 (
        echo [ERREUR] Impossible de supprimer "%WWWROOT_DST%".
        pause
        exit /b 1
    )
)

echo.
echo ========================================================
echo 2. Publication Release de FastLabel.Client...
echo ========================================================
if not exist "%PROJECT_PATH%" (
    echo [ERREUR] Projet introuvable : "%PROJECT_PATH%".
    pause
    exit /b 1
)

dotnet publish "%PROJECT_PATH%" ^
    -c Release ^
    -o "%PUBLISH_DIR%"

if errorlevel 1 (
    echo.
    echo [ERREUR] Echec de dotnet publish pour le client.
    pause
    exit /b 1
)

if not exist "%WWWROOT_SRC%" (
    echo.
    echo [ERREUR] Dossier wwwroot introuvable dans "%PUBLISH_DIR%".
    pause
    exit /b 1
)

echo.
echo ========================================================
echo 3. Copie de wwwroot vers le dossier Docker...
echo ========================================================
if not exist "%DOCKER_DIR%" mkdir "%DOCKER_DIR%"

xcopy "%WWWROOT_SRC%" "%WWWROOT_DST%\" /e /i /h /y >nul
if errorlevel 1 (
    echo.
    echo [ERREUR] Echec de la copie de wwwroot vers "%DOCKER_DIR%".
    pause
    exit /b 1
)

echo.
echo ========================================================
echo [SUCCES] Publication et preparation OK !
echo          Fichiers prets dans : "%WWWROOT_DST%"
echo ========================================================
pause