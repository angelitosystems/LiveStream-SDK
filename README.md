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
| `src/LiveStream.SdkCore` | Núcleo compartido: scaffold, empaquetado, firma, instalación, doctor y publicación |
| `examples/HelloPlugin` | Plugin de ejemplo completo (unas 60 líneas) |
| `tools/LiveStream.PluginTool` | `lsplugin`: init, validate, build, pack, install, list, doctor, firma y publicación |
| `tools/LiveStream.SdkStudio` | GUI del SDK (Avalonia): crear proyecto, manifiesto, paquete, plugins y diagnóstico |
| `tools/LiveStream.SdkSetup` | Instalador por usuario: deja `lsplugin` + SDK Studio en el PATH, con esquema y NuGet local |
| `plugin.schema.json` | Esquema JSON de `plugin.json` |
| `scripts/` | Empaquetado de la distribución y notas de release |
| `tests/` | Pruebas de SdkCore (14), de la CLI como proceso real (11) y del instalador (8) |
| `docs/SDK.md` | Guía completa: ciclo de vida, permisos, protocolo, versiones y errores frecuentes |
| `docs/CLI.md` | Referencia de `lsplugin`: órdenes, banderas, códigos de salida y autocompletado |

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
lsplugin init mi-plugin --id com.empresa.miplugin --name "Mi plugin"
cd mi-plugin
lsplugin build
lsplugin pack bin\Release\net10.0\win-x64\
lsplugin install .\com.empresa.miplugin-0.1.0.lsplugin --dev
```

`lsplugin init` genera el proyecto completo: manifiesto, csproj, `Program.cs` de la plantilla elegida ya conectado al
protocolo (`--template basic|graphics|sources|filters|integrations|tools`, ver detalles con `init-templates`),
`NuGet.config` y `.gitignore`. Referencia de todas las órdenes: `docs/CLI.md` y `lsplugin help <orden>`.

La misma lógica va también en una interfaz gráfica (**SDK Studio**, incluido en el instalador): creación de proyectos,
edición del manifiesto con validación al instante, compilar/empaquetar/firmar, gestión de plugins instalados y
diagnóstico. Ambas comparten el núcleo `LiveStream.SdkCore`, así que lo que se hace en la GUI es exactamente lo mismo
que el comando equivalente de `lsplugin`.

Sin instalar el SDK, también funciona desde el repositorio:

```powershell
dotnet build
dotnet run --project tools/LiveStream.PluginTool -- validate examples/HelloPlugin/bin/Debug/net10.0/win-x64
```

Para probarlo en LiveStream (0.9.0 o posterior): `lsplugin install <paquete> --dev` o **Herramientas → Marketplace de
plugins**, activa el **modo desarrollador** y pulsa **Instalar desde archivo…**.

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
