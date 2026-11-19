@echo off
title Execution des tests - FastLabel.Backend.Tests

cd /d "%~dp0"

echo ========================================================
echo Lancement des tests unitaires et d'integration...
echo ========================================================
dotnet test server\FastLabel.Backend.Tests --logger "console;verbosity=normal"

if %errorlevel% neq 0 (
    echo.
    echo [ECHEC] Certains tests ont echoue.
    pause
    exit /b %errorlevel%
)

echo.
echo ========================================================
echo [SUCCES] Tous les tests ont reussi avec succes !
echo ========================================================
pause