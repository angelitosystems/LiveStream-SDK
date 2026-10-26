# LiveStream Plugin SDK

SDK oficial para crear plugins de [LiveStream](https://github.com/angelitosystems/LiveStream), el estudio de directo y
grabación para Windows de **Angelito Systems S.A.C.**

Un plugin es un programa aparte (`.exe` de Windows x64, en cualquier lenguaje) que LiveStream ejecuta en su propio
proceso y con el que habla por **JSON-RPC 2.0 sobre stdin/stdout**. Con los permisos que acepta el usuario, puede
leer y modificar el estudio (escenas, fuentes, audio, transiciones, gráficos y cuenta atrás) mediante las mismas
herramientas validadas y con deshacer que usa el asistente de IA de la aplicación.

| Carpeta | Contenido |
|---|---|
| `src/LiveStream.PluginSdk` | Biblioteca .NET: manifiesto, paquetes `.lsplugin`, firma ECDSA P-256 y `JsonRpcPeer` |
| `examples/HelloPlugin` | Plugin de ejemplo completo (unas 60 líneas) |
| `tools/LiveStream.PluginTool` | `lsplugin`: crear, validar, empaquetar, firmar, verificar y publicar |
| `tools/LiveStream.SdkSetup` | Instalador por usuario: deja `lsplugin` en el PATH, con esquema, docs y NuGet local |
| `plugin.schema.json` | Esquema JSON de `plugin.json` |
| `scripts/` | Empaquetado de la distribución y notas de release |
| `tests/` | Pruebas unitarias del instalador |
| `docs/SDK.md` | Guía completa: ciclo de vida, permisos, protocolo, versiones y errores frecuentes |

## Instalar

Descarga `LiveStream-SDK-Setup-<versión>.exe` de la página de
[releases](https://github.com/angelitosystems/LiveStream-SDK/releases) y ejecútalo: instala solo para tu usuario (sin
permisos de administrador) en `%LOCALAPPDATA%\Programs\LiveStream\sdk`, añade esa carpeta al **PATH** y define
`LIVESTREAM_SDK_DIR`. Desinstalar desde «Aplicaciones instaladas» o `LiveStream-SDK-Setup.exe --uninstall`. También
hay un `.zip` con el mismo contenido, sin instalar.

Con el instalador no necesitas este repositorio para desarrollar plugins.

## Empezar

Requiere el [SDK de .NET 10](https://dotnet.microsoft.com/download) para compilar el plugin en sí. Con `lsplugin`
instalado (o `dotnet run --project tools/LiveStream.PluginTool` desde este repositorio):

```powershell
lsplugin new mi-plugin --id com.empresa.miplugin --name "Mi plugin"
cd mi-plugin
dotnet publish -c Release
lsplugin validate bin/Release/net10.0/win-x64/
lsplugin pack bin/Release/net10.0/win-x64/ -o hello.lsplugin
```

`lsplugin new` genera el proyecto completo: manifiesto, csproj, `Program.cs` ya conectado al protocolo, `NuGet.config`
y `.gitignore`. Sin instalar el SDK, también funciona desde el repositorio:

```powershell
dotnet build
dotnet run --project tools/LiveStream.PluginTool -- validate examples/HelloPlugin/bin/Debug/net10.0/win-x64
dotnet run --project tools/LiveStream.PluginTool -- pack examples/HelloPlugin/bin/Debug/net10.0/win-x64 -o hello.lsplugin
```

Para probarlo en LiveStream (0.9.0 o posterior): **Herramientas → Marketplace de plugins**, activa el **modo
desarrollador** y pulsa **Instalar desde archivo…**.

## Generar la distribución

```powershell
./scripts/package.ps1   # artifacts/release: LiveStream-SDK-Setup-<versión>.exe, .zip y SHA256SUMS.txt
```

Al subir un tag `v*`, GitHub Actions genera y publica la release automáticamente (`.github/workflows/release.yml`).

## Publicación

El marketplace oficial solo distribuye plugins firmados por Angelito Systems. La publicación de terceros todavía no
está abierta.

## Licencia

GPL-2.0-or-later, igual que LiveStream. Consulta `LICENSE`.
