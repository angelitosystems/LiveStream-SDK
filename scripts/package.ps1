<#
.SYNOPSIS
  Genera la distribución del SDK de plugins de LiveStream en artifacts/release:
    LiveStream-SDK-Setup-<versión>.exe      instalador por usuario (lo añade al PATH, no pide administrador)
    LiveStream-PluginSDK-<versión>.zip      el mismo contenido, sin instalar
    SHA256SUMS.txt, LICENSE.txt
  Con -CertificateThumbprint firma los .exe con signtool (certificado del almacén del usuario).

.DESCRIPTION
  El paquete instalado (igual en el instalador y en el .zip): lsplugin.exe autocontenido, plugin.schema.json,
  docs/SDK.md, README.txt de arranque y el paquete NuGet local LiveStream.PluginSdk.<versión>.nupkg, que usan los
  proyectos que `lsplugin new` genera para restaurar sin conexión (variable LIVESTREAM_SDK_DIR del instalador).
#>
param(
    [string] $Configuration = 'Release',
    [string] $CertificateThumbprint
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
[xml]$props = Get-Content (Join-Path $root 'Directory.Build.props')
$version = ($props.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
if (-not $version) { throw 'No se encontró <Version> en Directory.Build.props.' }

$out = Join-Path $root 'artifacts/release'
$work = Join-Path $root 'artifacts/publish/sdk'
$payload = Join-Path $work 'payload'
$sdkSetup = Join-Path $out "LiveStream-SDK-Setup-$version.exe"
$zip = Join-Path $out "LiveStream-PluginSDK-$version.zip"

if (Test-Path $out) { Remove-Item -Recurse -Force $out }
if (Test-Path $work) { Remove-Item -Recurse -Force $work }
New-Item -ItemType Directory -Force $out, $payload, (Join-Path $payload 'docs') | Out-Null

function Invoke-Sign([string[]] $files) {
    if (-not $CertificateThumbprint) { return }
    $signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter signtool.exe |
        Where-Object FullName -match '\\x64\\' | Sort-Object FullName -Descending | Select-Object -First 1
    if (-not $signtool) { throw 'No se encontró signtool.exe (Windows SDK).' }
    & $signtool.FullName sign /sha1 $CertificateThumbprint /fd sha256 /tr http://timestamp.digicert.com /td sha256 /d "LiveStream Plugin SDK" $files
    if ($LASTEXITCODE) { throw 'La firma Authenticode falló.' }
    & $signtool.FullName verify /pa /q $files
    if ($LASTEXITCODE) { throw 'La firma Authenticode no se verifica.' }
}

Write-Host "LiveStream Plugin SDK $version"

New-Item -ItemType Directory -Force $out, $payload, (Join-Path $payload 'docs') | Out-Null

Write-Host '1/4 Paquete NuGet del SDK…'
dotnet pack (Join-Path $root 'src/LiveStream.PluginSdk/LiveStream.PluginSdk.csproj') -c $Configuration `
    -p:PackageOutputPath="$(Join-Path $work 'packages')" -v q --nologo
if ($LASTEXITCODE) { throw 'dotnet pack falló.' }
New-Item -ItemType Directory -Force (Join-Path $payload 'packages') | Out-Null
Get-ChildItem (Join-Path $work 'packages') -Filter '*.nupkg' | Move-Item -Destination (Join-Path $payload 'packages') -Force

Write-Host '2/4 lsplugin (autocontenido)…'
dotnet publish (Join-Path $root 'tools/LiveStream.PluginTool/LiveStream.PluginTool.csproj') -c $Configuration -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -o $work -v q --nologo
if ($LASTEXITCODE) { throw 'La publicación de lsplugin falló.' }
if (-not (Test-Path (Join-Path $work 'lsplugin.exe'))) { throw 'Falta lsplugin.exe.' }
Copy-Item (Join-Path $work 'lsplugin.exe') $payload
Invoke-Sign (Join-Path $payload 'lsplugin.exe')

Write-Host '3/4 Contenido del SDK…'
Copy-Item (Join-Path $root 'plugin.schema.json') $payload
Copy-Item (Join-Path $root 'docs/SDK.md') (Join-Path $payload 'docs')
Copy-Item (Join-Path $root 'LICENSE') (Join-Path $payload 'LICENSE.txt')
$quickstart = @"
SDK de plugins de LiveStream $version
=====================================

1. Abre una terminal nueva (para que el PATH renovado esté disponible).
2. lsplugin new mi-plugin --id com.empresa.miplugin --name "Mi plugin"
3. cd mi-plugin ; dotnet publish -c Release
4. lsplugin validate bin\Release\net10.0\win-x64\
5. lsplugin pack bin\Release\net10.0\win-x64\

En LiveStream: Herramientas -> Marketplace de plugins -> Modo desarrollador (una sola vez)
y luego "Instalar desde archivo..." con el .lsplugin.

Documentación: docs\SDK.md o
https://github.com/angelitosystems/LiveStream-SDK/blob/main/docs/SDK.md
"@
Set-Content (Join-Path $payload 'README.txt') $quickstart -Encoding utf8
Compress-Archive -Path (Join-Path $payload '*') -DestinationPath $zip -CompressionLevel Optimal

Write-Host '4/4 Instalador…'
dotnet publish (Join-Path $root 'tools/LiveStream.SdkSetup/LiveStream.SdkSetup.csproj') -c $Configuration -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    "-p:PayloadZip=$zip" -o $work -v q --nologo
if ($LASTEXITCODE) { throw 'La publicación del instalador falló.' }
Move-Item (Join-Path $work 'LiveStream-SDK-Setup.exe') $sdkSetup
Invoke-Sign $sdkSetup

Write-Host ''
Write-Host 'Sumas de comprobación…'
$sums = Get-ChildItem $out -File | Where-Object { $_.Extension -in '.exe', '.zip' } |
    ForEach-Object { "{0}  {1}" -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower(), $_.Name }
Set-Content (Join-Path $out 'SHA256SUMS.txt') $sums -Encoding ascii
Copy-Item (Join-Path $root 'LICENSE') (Join-Path $out 'LICENSE.txt')

Get-ChildItem $out | ForEach-Object { '{0,-45} {1,8:0.0} MB' -f $_.Name, ($_.Length / 1MB) }
