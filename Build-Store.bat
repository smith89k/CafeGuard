@echo off
rem Builds the Microsoft Store version: dist\store\CafeGuard_<version>_x64.msix (upload this to Partner Center).
rem See docs\store\partner-center-guide.md.
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\build-store.ps1" || exit /b 1
