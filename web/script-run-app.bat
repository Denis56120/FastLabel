@echo off
title Lancement FastLabel - Serveur & Client

set "ROOT_DIR=%~dp0"
if "%ROOT_DIR:~-1%"=="\" set "ROOT_DIR=%ROOT_DIR:~0,-1%"

set "SERVER_DIR=%ROOT_DIR%\server\FastLabel.Backend"
set "CLIENT_DIR=%ROOT_DIR%\client\FastLabel.Client"

echo ========================================================
echo Lancement du Serveur...
echo ========================================================
start "FastLabel - Serveur" cmd /k "cd /d "%SERVER_DIR%" && dotnet watch --launch-profile http -nr"

timeout /t 10 /nobreak >nul

echo ========================================================
echo Lancement du Client...
echo ========================================================
start "FastLabel - Client" cmd /k "cd /d "%CLIENT_DIR%" && dotnet watch --launch-profile http -nr"

echo.
echo ========================================================
echo [INFO] Le serveur et le client ont ete lances dans des
echo        fenetres separees. Vous pouvez fermer celle-ci.
echo ========================================================
pause