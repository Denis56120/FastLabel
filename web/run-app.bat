@echo off
title Lancement FastLabel - Serveur & Client

cd /d "%~dp0"

echo ========================================================
echo Lancement du Serveur...
echo ========================================================
start "FastLabel - Serveur" cmd /k "cd /d server\FastLabel.Backend && dotnet watch --launch-profile http"

echo ========================================================
echo Lancement du Client...
echo ========================================================
start "FastLabel - Client" cmd /k "cd /d client\FastLabel.Client && dotnet watch --launch-profile http"

echo.
echo ========================================================
echo [INFO] Le serveur et le client ont ete lances dans des 
echo        fenetres separees. Vous pouvez fermer celle-ci.
echo ========================================================
pause