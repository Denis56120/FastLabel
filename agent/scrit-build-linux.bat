@echo off
chcp 65001 >nul
echo Publication de FastLabel.DockerAgent en fichier unique (Linux x64)...

dotnet publish FastLabel.DockerAgent\FastLabel.DockerAgent.csproj ^
    -c Release ^
    -r linux-x64 ^
    --self-contained true ^
    /p:PublishSingleFile=true ^
    /p:PublishReadyToRun=true ^
    /p:UseAppHost=true

echo Copie du binaire vers le dossier Docker...
copy FastLabel.DockerAgent\bin\Release\net10.0\linux-x64\publish\FastLabel.DockerAgent FastLabel.DockerAgent\docker\ >nul

echo.
echo Publication et preparation OK !
echo Le binaire unique et les scripts sont prets dans le dossier : FastLabel.DockerAgent\docker\
pause