@echo off
setlocal

set "ROOT_DIR=%~dp0"

call npx repomix --include "Data/**,Helpers/**,Services/**,Endpoints/**,Models/**" "%ROOT_DIR%server\FastLabel.Backend" -o "%ROOT_DIR%repomix-backend.xml"
if errorlevel 1 exit /b %errorlevel%

call npx repomix --include "Bases/**,Helpers/**,Layouts/**,Pages/**,Services/**" "%ROOT_DIR%client\FastLabel.Client" -o "%ROOT_DIR%repomix-client.xml"
if errorlevel 1 exit /b %errorlevel%

call npx repomix --include "DTOs/**" "%ROOT_DIR%shared\FastLabel.Shared" -o "%ROOT_DIR%repomix-shared.xml"
if errorlevel 1 exit /b %errorlevel%

endlocal