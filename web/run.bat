@echo off
title Lancement FastLabel - Serveur & Client

echo Lancement du Serveur...
start "FastLabel - Serveur" cmd /k "dotnet run --project server/FastLabel.Backend/FastLabel.Backend.csproj"

echo Lancement du Client...
start "FastLabel - Client" cmd /k "dotnet watch --project client/FastLabel.Client/FastLabel.Client.csproj"

echo Ouverture du navigateur...
start http://localhost:5000

pause