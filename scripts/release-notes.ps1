<#
.SYNOPSIS
  Escribe las notas de la release de una versión del SDK: cómo instalar + su sección de CHANGELOG.md.
  Lo usa .github/workflows/release.yml; también sirve en local para revisar las notas antes de publicar.
#>
param(
    [Parameter(Mandatory)] [string] $Version,
    [Parameter(Mandatory)] [string] $Output,
    [string] $Repository = 'angelitosystems/LiveStream-SDK'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$changelog = Get-Content (Join-Path $root 'CHANGELOG.md') -Raw -Encoding utf8
$pattern = '(?ms)^## \[' + [regex]::Escape($Version) + '\][^\r\n]*\r?\n(.*?)(?=^## \[|^\[[^\]]+\]:|\z)'
$section = [regex]::Match($changelog, $pattern).Groups[1].Value.Trim()
if (-not $section) { throw "CHANGELOG.md no tiene la sección [$Version]." }

$tag = "v$Version"
$notes = @"
Descarga **``LiveStream-SDK-Setup-$Version.exe``** y ejecútalo: instala para tu usuario (sin permisos de
administrador) la CLI **``lsplugin``** y la GUI **SDK Studio** en el PATH, deja el esquema del manifiesto, la
documentación y el paquete NuGet local del SDK y registra la desinstalación en «Aplicaciones instaladas». También hay
un ``.zip`` con el mismo contenido, sin instalar.

- Requisitos: Windows 10/11 x64. ``lsplugin`` es autocontenido; los proyectos de plugins necesitan el
  [SDK de .NET 10](https://dotnet.microsoft.com/download) para compilar (la GUI usa el mismo runtime).
- ``SHA256SUMS.txt`` contiene el SHA-256 del instalador y del ``.zip``.
- Crear un plugin (en una terminal nueva): ``lsplugin init mi-plugin --id com.empresa.miplugin --name "Mi plugin"``.
  O con la GUI: ``sdkstudio\LiveStream.SdkStudio.exe`` (crear proyecto, manifiesto, compilar, empaquetar y firmar).
- Referencia de la CLI: ``docs\CLI.md`` · guía de la API: ``docs\SDK.md`` (§5 y §6).
- Código fuente de esta versión: tag [``$tag``](https://github.com/$Repository/tree/$tag) · licencia GPL-2.0-or-later.

## Cambios

$section
"@
Set-Content $Output $notes -Encoding utf8
