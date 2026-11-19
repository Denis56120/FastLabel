@echo off
title Recreation du conteneur PostgreSQL (Dev)

set "DATA_DIR=D:\Docker\Container\postgres"
set "POSTGRES_DATA_PATH=D:/Docker/Container/postgres"

echo ========================================================
echo Verification du repertoire de donnees sur D:...
echo ========================================================
if not exist "%DATA_DIR%" (
    mkdir "%DATA_DIR%"
    echo [INFO] Repertoire cree : %DATA_DIR%
) else (
    echo [INFO] Repertoire deja existant : %DATA_DIR%
)

cd /d "%~dp0\server\FastLabel.Backend"

if %errorlevel% neq 0 (
    echo.
    echo [ERREUR] Le repertoire FastLabel.Backend est introuvable.
    pause
    exit /b %errorlevel%
)

echo.
echo ========================================================
echo Arret et suppression de l'ancien conteneur...
echo ========================================================
docker compose -p fastlabel-dev --env-file .env.dev down

if %errorlevel% neq 0 (
    echo.
    echo [INFO] Aucun conteneur actif ou erreur mineure lors du down. On continue...
)

echo.
echo ========================================================
echo Creation et demarrage de PostgreSQL avec .env.dev...
echo ========================================================
docker compose -p fastlabel-dev --env-file .env.dev up -d --force-recreate

if %errorlevel% neq 0 (
    echo.
    echo [ERREUR] Le demarrage du conteneur PostgreSQL a echoue.
    pause
    exit /b %errorlevel%
)

echo.
echo ========================================================
echo Etat du conteneur :
echo ========================================================
docker compose -p fastlabel-dev --env-file .env.dev ps

echo.
echo ========================================================
echo [SUCCES] Conteneur PostgreSQL recree avec .env.dev !
echo ========================================================
pause