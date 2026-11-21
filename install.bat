@echo off
setlocal
title Antigravity Copilot Key Shortcut - Installer

if exist "%~dp0install.ps1" (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -Command "irm 'https://raw.githubusercontent.com/Blecc/AntigravityCopilotKeyShortcut/main/install.ps1' | iex"
)

echo.
pause
