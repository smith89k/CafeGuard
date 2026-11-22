@echo off
rem Runs the CafeGuard tests. The project folder carries a "Low" integrity label (from a coding sandbox);
rem the test runner must not run at Low, so its output folder is labelled Medium first.
cd /d "%~dp0"
if not exist "CafeGuard.Tests\bin" mkdir "CafeGuard.Tests\bin"
icacls "CafeGuard.Tests\bin" /setintegritylevel (OI)(CI)M >nul
dotnet test CafeGuard.sln %*
