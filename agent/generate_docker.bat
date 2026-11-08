@echo off
chcp 65001 >nul
echo Publication de FastLabel.DockerAgent en fichier unique (Linux x64)...

dotnet publish FastLabel.DockerAgent\FastLabel.DockerAgent.csproj ^
    -c Release ^
    -r linux-x64 ^
    -o FastLabel.DockerAgent/publish ^
    --self-contained true ^
    /p:PublishSingleFile=true ^
    /p:PublishReadyToRun=true ^
    /p:UseAppHost=true

echo.
echo Publication terminée avec succès !
echo Le binaire unique se trouve dans : FastLabel.DockerAgent\bin\Release\net10.0\linux-x64\publish\
pause