@echo off
setlocal
chcp 65001 >nul
title Build FastLabel.DockerAgent (Linux x64)

set "SCRIPT_DIR=%~dp0"
if "%SCRIPT_DIR:~-1%"=="\" set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"

rem --- Si FastLabel.DockerAgent est dans agent\ : "%SCRIPT_DIR%\FastLabel.DockerAgent"
rem --- Si FastLabel.DockerAgent est a la racine au-dessus de agent\ : "%SCRIPT_DIR%\..\FastLabel.DockerAgent"
set "AGENT_DIR=%SCRIPT_DIR%\FastLabel.DockerAgent"
if not exist "%AGENT_DIR%\FastLabel.DockerAgent.csproj" (
    if exist "%SCRIPT_DIR%\..\FastLabel.DockerAgent\FastLabel.DockerAgent.csproj" (
        set "AGENT_DIR=%SCRIPT_DIR%\..\FastLabel.DockerAgent"
    )
)

set "PROJECT_PATH=%AGENT_DIR%\FastLabel.DockerAgent.csproj"
set "PUBLISH_DIR=%AGENT_DIR%\bin\Release\net10.0\linux-x64\publish"
set "DOCKER_DIR=%AGENT_DIR%\docker"
set "BINARY_NAME=FastLabel.DockerAgent"

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
echo 2. Publication de %BINARY_NAME% (.NET 10 / linux-x64)...
echo ========================================================
if not exist "%PROJECT_PATH%" (
    echo [ERREUR] Projet introuvable : "%PROJECT_PATH%".
    pause
    exit /b 1
)

dotnet publish "%PROJECT_PATH%" ^
    -c Release ^
    -r linux-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:PublishReadyToRun=true ^
    -p:UseAppHost=true ^
    -o "%PUBLISH_DIR%"

if errorlevel 1 (
    echo.
    echo [ERREUR] Echec de dotnet publish.
    pause
    exit /b 1
)

if not exist "%PUBLISH_DIR%\%BINARY_NAME%" (
    echo.
    echo [ERREUR] Binaire "%BINARY_NAME%" introuvable dans "%PUBLISH_DIR%".
    pause
    exit /b 1
)

echo.
echo ========================================================
echo 3. Copie du binaire vers le dossier Docker...
echo ========================================================
if not exist "%DOCKER_DIR%" mkdir "%DOCKER_DIR%"

copy /y "%PUBLISH_DIR%\%BINARY_NAME%" "%DOCKER_DIR%\" >nul
if errorlevel 1 (
    echo.
    echo [ERREUR] Echec de la copie vers "%DOCKER_DIR%".
    pause
    exit /b 1
)

echo.
echo ========================================================
echo [SUCCES] Publication et preparation OK !
echo          Binaire pret dans : "%DOCKER_DIR%\%BINARY_NAME%"
echo ========================================================
if not defined BATCH_MODE pause