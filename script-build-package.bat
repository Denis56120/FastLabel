@echo off
setlocal
chcp 65001 >nul
title Creation du Package Web FastLabel (Backend + Client)

set "ROOT_DIR=%~dp0"
if "%ROOT_DIR:~-1%"=="\" set "ROOT_DIR=%ROOT_DIR:~0,-1%"

set "DIST_DIR=%ROOT_DIR%\dist"
set "PKG_DIR=%DIST_DIR%\fastlabel-package"
set "ARCHIVE_PATH=%DIST_DIR%\fastlabel-package.tar.gz"

set "BACKEND_DOCKER=%ROOT_DIR%\web\server\FastLabel.Backend\docker"
set "CLIENT_DOCKER=%ROOT_DIR%\web\client\FastLabel.Client\docker"

set "BACKEND_BUILD_BAT=%ROOT_DIR%\web\script-build-docker-backend.bat"
set "CLIENT_BUILD_BAT=%ROOT_DIR%\web\script-build-docker-front.bat"

set "BATCH_MODE=1"

echo ========================================================
echo 1. Publication du Backend...
echo ========================================================
if not exist "%BACKEND_BUILD_BAT%" (
    echo [ERREUR] Script introuvable : "%BACKEND_BUILD_BAT%"
    pause
    exit /b 1
)
call "%BACKEND_BUILD_BAT%"
if errorlevel 1 (
    echo [ERREUR] Echec de la publication du Backend.
    pause
    exit /b 1
)

echo.
echo ========================================================
echo 2. Publication du Client...
echo ========================================================
if not exist "%CLIENT_BUILD_BAT%" (
    echo [ERREUR] Script introuvable : "%CLIENT_BUILD_BAT%"
    pause
    exit /b 1
)
call "%CLIENT_BUILD_BAT%"
if errorlevel 1 (
    echo [ERREUR] Echec de la publication du Client.
    pause
    exit /b 1
)

echo.
echo ========================================================
echo 3. Verification des artefacts...
echo ========================================================
if not exist "%BACKEND_DOCKER%\FastLabel.Backend" (
    echo [ERREUR] Binaire backend manquant : "%BACKEND_DOCKER%\FastLabel.Backend"
    pause
    exit /b 1
)
if not exist "%CLIENT_DOCKER%\wwwroot\index.html" (
    echo [ERREUR] Dossier client wwwroot manquant : "%CLIENT_DOCKER%\wwwroot"
    pause
    exit /b 1
)

echo.
echo ========================================================
echo 4. Preparation du dossier de package...
echo ========================================================
if exist "%PKG_DIR%" rmdir /s /q "%PKG_DIR%"
if exist "%ARCHIVE_PATH%" del /f /q "%ARCHIVE_PATH%"

mkdir "%PKG_DIR%\backend"
mkdir "%PKG_DIR%\client"

xcopy "%BACKEND_DOCKER%\*" "%PKG_DIR%\backend\" /e /i /h /y >nul
xcopy "%CLIENT_DOCKER%\*" "%PKG_DIR%\client\" /e /i /h /y >nul

if exist "%ROOT_DIR%\deploy.sh" (
    copy /y "%ROOT_DIR%\deploy.sh" "%PKG_DIR%\deploy.sh" >nul
)

echo.
echo ========================================================
echo 5. Compression en archive .tar.gz...
echo ========================================================
tar -czf "%ARCHIVE_PATH%" -C "%DIST_DIR%" fastlabel-package
if errorlevel 1 (
    echo [ERREUR] Echec de creation de l'archive tar.gz.
    pause
    exit /b 1
)

echo.
echo ========================================================
echo [SUCCES] Package pret :
echo          "%ARCHIVE_PATH%"
echo ========================================================
pause