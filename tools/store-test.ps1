# Puts the Store version of CafeGuard on this laptop before (or instead of) downloading it from the Store.
# Needs Developer Mode; no certificate. The app is unpacked from dist\store\*.msix into
# %LOCALAPPDATA%\Programs\CafeGuard Store, so the project folder stays free of .exe files.
#   store-test.ps1 install   — build first with Build-Store.bat
#   store-test.ps1 remove    — take it off again (do this before installing CafeGuard from the Microsoft Store)
param([Parameter(Mandatory)][ValidateSet('install', 'remove')][string]$Action)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$appDir = Join-Path $env:LOCALAPPDATA 'Programs\CafeGuard Store'
$name = (Get-Content (Join-Path $root 'windows\CafeGuard.Store\store-identity.json') -Raw | ConvertFrom-Json).identityName

function Test-Guarding {
    try { $e = [Threading.EventWaitHandle]::OpenExisting('CafeGuard_GuardOn'); $on = $e.WaitOne(0); $e.Dispose(); return $on }
    catch { return $false }
}

# Never close CafeGuard while it is guarding.
if (Test-Guarding) { throw 'CafeGuard is ON. Turn it OFF first, then run this again.' }
Get-Process CafeGuard -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1
foreach ($n in @($name, 'CafeGuard.Dev')) { Get-AppxPackage -Name $n | Remove-AppxPackage }
if (Test-Path $appDir) { Remove-Item $appDir -Recurse -Force }

if ($Action -eq 'install') {
    $msix = Get-ChildItem (Join-Path $root 'dist\store') -Filter *.msix -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $msix) { throw 'Run Build-Store.bat first.' }
    $makeappx = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin\*\x64\makeappx.exe' |
        Sort-Object FullName -Descending | Select-Object -First 1
    & $makeappx.FullName unpack /p $msix.FullName /d $appDir /o | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not unpack the .msix file.' }
    Add-AppxPackage -Register (Join-Path $appDir 'AppxManifest.xml')
    $pkg = Get-AppxPackage -Name $name
    Start-Process explorer.exe "shell:AppsFolder\$($pkg.PackageFamilyName)!CafeGuard"
    Write-Host 'CafeGuard (Store version) installed and started. Look for the shield near the clock.'
}
else {
    Write-Host 'CafeGuard (Store version) removed from this laptop.'
}
