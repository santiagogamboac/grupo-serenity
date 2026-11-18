@echo off
rem Despliega Grupo Serenity en pruebas por SSH (ver scripts\deploy-remoto.ps1 y scripts\deploy.ambientes.psd1).
rem Acepta los mismos parametros: deploy-pruebas.bat -Rama feature/x -Usuario ubuntu
setlocal
set "PS=powershell"
where pwsh >nul 2>nul && set "PS=pwsh"
%PS% -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\deploy-remoto.ps1" -Ambiente pruebas %*
set "CODIGO=%ERRORLEVEL%"
pause
exit /b %CODIGO%
