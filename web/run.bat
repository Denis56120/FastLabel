@echo off
title Lancement FastLabel - Serveur & Client

echo Lancement du Serveur...
start "FastLabel - Serveur" cmd /k "cd /d %~dp0server\FastLabel.Backend && dotnet run --launch-profile http"

echo Lancement du Client...
start "FastLabel - Client" cmd /k "cd /d %~dp0client\FastLabel.Client && dotnet watch --launch-profile http"

pause