# Builds the Microsoft Store version of CafeGuard:
#   dist\store\CafeGuard_<ver>_x64.msix   the file you upload to Partner Center (the only file left in dist\store)
# Start it with Build-Store.bat. The Store signs the package itself, so no certificate is needed here.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$storeSrc = Join-Path $root 'windows\CafeGuard.Store'
$dist = Join-Path $root 'dist'
$out = Join-Path $dist 'store'
$layout = Join-Path $out 'layout'

# This project folder carries a "Low" label from a coding sandbox; label dist Medium so its programs run normally.
New-Item -ItemType Directory -Force $dist | Out-Null
icacls $dist /setintegritylevel '(OI)(CI)M' | Out-Null

# Version: AppPaths.Version ("1.0.0") + ".0" — the Store wants four numbers ending in 0.
$appPaths = Get-Content (Join-Path $root 'windows\CafeGuard.Shared\AppPaths.cs') -Raw
if ($appPaths -notmatch 'Version\s*=\s*"(\d+\.\d+\.\d+)"') { throw 'Could not read the version from AppPaths.cs' }
$version = "$($Matches[1]).0"

Write-Host "[1/3] Building CafeGuard $version (with .NET included)..."
if (Test-Path $layout) { Remove-Item -Recurse -Force $layout }
dotnet publish (Join-Path $root 'windows\CafeGuard') -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -p:DebugType=none -o $layout
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

Write-Host '[2/3] Adding pictures and the package description...'
Copy-Item (Join-Path $storeSrc 'Assets') (Join-Path $layout 'Assets') -Recurse
$id = Get-Content (Join-Path $storeSrc 'store-identity.json') -Raw | ConvertFrom-Json
$manifest = (Get-Content (Join-Path $storeSrc 'AppxManifest.template.xml') -Raw).
    Replace('$IdentityName$', $id.identityName).
    Replace('$Publisher$', [Security.SecurityElement]::Escape($id.publisher)).
    Replace('$PublisherDisplayName$', [Security.SecurityElement]::Escape($id.publisherDisplayName)).
    Replace('$Version$', $version)
[IO.File]::WriteAllText((Join-Path $layout 'AppxManifest.xml'), $manifest, (New-Object Text.UTF8Encoding $false))

Write-Host '[3/3] Packing...'
$makeappx = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin\*\x64\makeappx.exe' -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending | Select-Object -First 1
if (-not $makeappx) { throw 'makeappx.exe not found: install the Windows SDK' }
$msix = Join-Path $out "CafeGuard_$($version)_x64.msix"
& $makeappx.FullName pack /d $layout /p $msix /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'makeappx pack failed (run it by hand to see why)' }

Remove-Item $layout -Recurse -Force   # keep only the .msix
$mb = [Math]::Round((Get-Item $msix).Length / 1MB, 1)
Write-Host ''
Write-Host "Done: $msix ($mb MB)"
if ($id.identityName -eq 'CafeGuard.Dev') {
    Write-Host 'Note: test identity. Put your Partner Center values in windows\CafeGuard.Store\store-identity.json before uploading.'
}
