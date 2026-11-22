@echo off
rem Store-Test.bat install   = put the Store version on this laptop from dist\store\*.msix (run Build-Store.bat first)
rem Store-Test.bat remove    = take it off again (do this before installing CafeGuard from the Microsoft Store)
if "%~1"=="" (echo Usage: Store-Test.bat install ^| remove & exit /b 1)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\store-test.ps1" %1
pause
