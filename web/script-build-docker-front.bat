@echo off
setlocal
chcp 65001 >nul
title Publish FastLabel.Client (Blazor WASM + Agents)

set "SCRIPT_DIR=%~dp0"
if "%SCRIPT_DIR:~-1%"=="\" set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"

rem --- Resolution de la racine du repo et des chemins ---
set "CLIENT_DIR=%SCRIPT_DIR%\client\FastLabel.Client"
set "REPO_ROOT=%SCRIPT_DIR%\.."
if not exist "%CLIENT_DIR%\FastLabel.Client.csproj" (
    if exist "%SCRIPT_DIR%\FastLabel.Client\FastLabel.Client.csproj" (
        set "CLIENT_DIR=%SCRIPT_DIR%\FastLabel.Client"
        set "REPO_ROOT=%SCRIPT_DIR%\..\.."
    )
)

set "PROJECT_PATH=%CLIENT_DIR%\FastLabel.Client.csproj"
set "PUBLISH_DIR=%CLIENT_DIR%\bin\Release\net10.0\publish"
set "DOCKER_DIR=%CLIENT_DIR%\docker"
set "WWWROOT_SRC=%PUBLISH_DIR%\wwwroot"
set "WWWROOT_DST=%DOCKER_DIR%\wwwroot"
set "DOWNLOADS_DIR=%WWWROOT_DST%\downloads"

rem --- Chemins cote Agent ---
set "AGENT_DIR=%REPO_ROOT%\agent"
set "WIN_BUILD_BAT=%AGENT_DIR%\script-build-windows.bat"
set "LINUX_BUILD_BAT=%AGENT_DIR%\script-build-linux.bat"
set "WIN_INSTALLER_DIR=%AGENT_DIR%\FastLabel.Installer"
set "LINUX_DOCKER_DIR=%AGENT_DIR%\FastLabel.DockerAgent\docker"

echo ========================================================
echo 1. Nettoyage des anciens dossiers publish et wwwroot...
echo ========================================================
if exist "%PUBLISH_DIR%" (
    rmdir /s /q "%PUBLISH_DIR%"
    if errorlevel 1 (
        echo [ERREUR] Impossible de supprimer "%PUBLISH_DIR%".
        if not defined BATCH_MODE pause
        exit /b 1
    )
)

if exist "%WWWROOT_DST%" (
    rmdir /s /q "%WWWROOT_DST%"
    if errorlevel 1 (
        echo [ERREUR] Impossible de supprimer "%WWWROOT_DST%".
        if not defined BATCH_MODE pause
        exit /b 1
    )
)

echo.
echo ========================================================
echo 2. Publication Release de FastLabel.Client...
echo ========================================================
if not exist "%PROJECT_PATH%" (
    echo [ERREUR] Projet introuvable : "%PROJECT_PATH%".
    if not defined BATCH_MODE pause
    exit /b 1
)

dotnet publish "%PROJECT_PATH%" ^
    -c Release ^
    -o "%PUBLISH_DIR%"

if errorlevel 1 (
    echo.
    echo [ERREUR] Echec de dotnet publish pour le client.
    if not defined BATCH_MODE pause
    exit /b 1
)

if not exist "%WWWROOT_SRC%" (
    echo.
    echo [ERREUR] Dossier wwwroot introuvable dans "%PUBLISH_DIR%".
    if not defined BATCH_MODE pause
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
    if not defined BATCH_MODE pause
    exit /b 1
)

echo.
echo ========================================================
echo 4. Build des Agents (Windows et Linux)...
echo ========================================================
if not exist "%WIN_BUILD_BAT%" (
    echo [ERREUR] Script introuvable : "%WIN_BUILD_BAT%"
    if not defined BATCH_MODE pause
    exit /b 1
)
if not exist "%LINUX_BUILD_BAT%" (
    echo [ERREUR] Script introuvable : "%LINUX_BUILD_BAT%"
    if not defined BATCH_MODE pause
    exit /b 1
)

set "PREV_BATCH_MODE=%BATCH_MODE%"
set "BATCH_MODE=1"

echo --- Execution de script-build-windows.bat ---
pushd "%AGENT_DIR%"
call "script-build-windows.bat"
set "ERR_WIN=%errorlevel%"
popd
if not "%ERR_WIN%"=="0" (
    echo [ERREUR] Echec de script-build-windows.bat.
    if not defined PREV_BATCH_MODE pause
    exit /b 1
)

echo --- Execution de script-build-linux.bat ---
pushd "%AGENT_DIR%"
call "script-build-linux.bat"
set "ERR_LINUX=%errorlevel%"
popd
if not "%ERR_LINUX%"=="0" (
    echo [ERREUR] Echec de script-build-linux.bat.
    if not defined PREV_BATCH_MODE pause
    exit /b 1
)

if not defined PREV_BATCH_MODE set "BATCH_MODE="

echo.
echo ========================================================
echo 5. Integration de l'installeur Windows (.exe) et de l'archive Linux...
echo ========================================================
if not exist "%DOWNLOADS_DIR%" mkdir "%DOWNLOADS_DIR%"

if not exist "%WIN_INSTALLER_DIR%" (
    echo [ERREUR] Dossier installeur Windows introuvable : "%WIN_INSTALLER_DIR%".
    if not defined BATCH_MODE pause
    exit /b 1
)

set "FOUND_EXE="
for %%F in ("%WIN_INSTALLER_DIR%\*.exe") do (
    set "FOUND_EXE=%%~fF"
    copy /y "%%~fF" "%DOWNLOADS_DIR%\" >nul
    if errorlevel 1 (
        echo [ERREUR] Echec de la copie de "%%~nxF".
        if not defined BATCH_MODE pause
        exit /b 1
    )
)

if not defined FOUND_EXE (
    echo [ERREUR] Aucun fichier .exe trouve dans "%WIN_INSTALLER_DIR%".
    if not defined BATCH_MODE pause
    exit /b 1
)

rem Linux : compression du contenu de FastLabel\agent\FastLabel.DockerAgent\docker
if not exist "%LINUX_DOCKER_DIR%\FastLabel.DockerAgent" (
    echo [ERREUR] Binaire Linux introuvable dans "%LINUX_DOCKER_DIR%".
    if not defined BATCH_MODE pause
    exit /b 1
)

tar -czf "%DOWNLOADS_DIR%\FastLabel.DockerAgent.tar.gz" -C "%LINUX_DOCKER_DIR%" .
if errorlevel 1 (
    echo [ERREUR] Echec de la creation de l'archive Linux FastLabel.DockerAgent.tar.gz.
    if not defined BATCH_MODE pause
    exit /b 1
)

echo.
echo ========================================================
echo [SUCCES] Publication Client + Agents OK !
echo          - Client : "%WWWROOT_DST%"
echo          - Downloads : "%DOWNLOADS_DIR%"
echo ========================================================
if not defined BATCH_MODE pause