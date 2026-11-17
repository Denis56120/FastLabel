@echo off
title Suppression et Reconstitution de la Base de Donnees

cd /d "%~dp0"

echo ========================================================
echo Suppression de la base de donnees...
echo ========================================================
dotnet ef database drop --project server\FastLabel.Backend --force

if %errorlevel% neq 0 (
    echo.
    echo [INFO] La base n'existait peut-etre pas deja. On continue...
)

echo.
echo ========================================================
echo Creation de la base et application des migrations...
echo ========================================================
dotnet ef database update --project server\FastLabel.Backend

if %errorlevel% neq 0 (
    echo.
    echo [ERREUR] L'application des migrations a echoue.
    pause
    exit /b %errorlevel%
)

echo.
echo ========================================================
echo [SUCCES] Base de donnees recree et mise a jour avec succes !
echo ========================================================
pause