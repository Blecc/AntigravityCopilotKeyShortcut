@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build-And-Install.ps1" %*
echo.
pause
