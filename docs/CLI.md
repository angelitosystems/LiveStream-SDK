# `lsplugin` — referencia de la CLI

Herramienta de consola del SDK de plugins de LiveStream. Toda la lógica vive en `LiveStream.SdkCore`, el mismo núcleo
que usa la GUI **SDK Studio** (`LiveStream.SdkStudio`), así que ambas comparten exactamente los mismos servicios,
validaciones y formatos.

## Órdenes

| Orden | Función |
|---|---|
| `init` | Crea el proyecto completo de un plugin (manifiesto, csproj, código de la plantilla) |
| `init-templates` | Lista las plantillas disponibles con sus permisos |
| `validate` | Valida una carpeta publicada o un paquete `.lsplugin` |
| `build` | Compila el plugin (`dotnet publish -c Release`) |
| `pack` | Empaqueta la carpeta publicada en `.lsplugin` (reproducible) |
| `install` | Instala un paquete en el almacén de LiveStream (valida firma, compatibilidad y dependencias) |
| `list` | Plugins instalados; con `--server` lista el catálogo del marketplace (propietario) |
| `info` | Detalles de un plugin instalado |
| `enable` / `disable` | Activa o desactiva respetando dependencias |
| `uninstall` | Quita código y datos (se niega si otro plugin depende de él) |
| `doctor` | Diagnóstico del entorno (SDK .NET, app, almacén, feed, clave, marketplace) |
| `keygen` | Crea el par de claves del publicador (ECDSA P-256) |
| `sign` | Firma un paquete con la clave privada |
| `verify` | Comprueba paquete + firma contra una clave pública |
| `publish` | Publica en el marketplace (solo el propietario; token en la variable de entorno) |
| `yank` / `unyank` | Retira o restaura una versión del catálogo |
| `backup` | Descarga el respaldo del marketplace |
| `config` | Configuración guardada (`lsplugin.json`) |
| `completions` | Genera el autocompletado para `pwsh`, `bash` o `zsh` |
| `version` / `help` | Versión y ayuda (general o por orden con `help <orden>`) |

## Banderas globales

| Bandera | Efecto |
|---|---|
| `--json` | Salida estructurada (por defecto indentada y camelCase); silencia las líneas de texto |
| `--quiet` / `-q` | Solo resultados esenciales |
| `--verbose` / `-v` | Detalles técnicos y salida completa de errores de compilación |
| `--no-interactive` | Nunca pregunta: exige `--yes` para acciones destructivas y error de uso si falta un dato |

La decoración con color se desactiva sola si la salida se redirige o si `NO_COLOR` está definida.

## Códigos de salida

| Código | Significado |
|---|---|
| `0` | Correcto |
| `1` | Error de la operación (paquete inválido, firma denegada, compilación fallida, red…) |
| `2` | Uso incorrecto (orden/opción desconocida, falta un argumento o un valor) |

## Flujo típico

```powershell
lsplugin init mi-plugin --id com.empresa.miplugin --name "Mi plugin"
cd mi-plugin
lsplugin build                                  # → bin\Release\net10.0\win-x64\
lsplugin pack bin\Release\net10.0\win-x64\      # → com.empresa.miplugin-0.1.0.lsplugin
lsplugin install .\com.empresa.miplugin-0.1.0.lsplugin --dev   # prueba en Modo desarrollador
```

Detalles de cada orden a continuación.

## `init` (alias `new`)

```
lsplugin init <carpeta> --id com.empresa.plugin --name "Nombre" [opciones]
```

| Opción | Valor |
|---|---|
| `--template <id>` | `basic` (por defecto), `graphics`, `sources`, `filters`, `integrations`, `tools` |
| `--version` | Versión del plugin (`0.1.0` por defecto) |
| `--min-app-version` | LiveStream mínimo requerido (`0.9.0` por defecto; bájalo solo para probar en apps locales antiguas) |
| `--author` / `--description` | Metadatos opcionales |
| `--project` | Nombre del proyecto .NET si no quieres derivarlo del id |

Sin `--id`/`--name` en una terminal normal los pregunta; con `--no-interactive` los exige. La carpeta destino debe
existir y estar vacía (o no existir). Genera: `plugin.json`, `<Proyecto>.csproj`, `Program.cs` (con llamadas reales a
herramientas de la API v1), `README.md`, `.gitignore` y — con el SDK instalado — `NuGet.config` apuntando al feed
local `LIVESTREAM_SDK_DIR\packages`.

## `init-templates`

Lista las plantillas en JSON (`--json`) o tabla. Cada plantilla declara sus permisos: el manifiesto generado los
incluye y la app los mostrará al usuario al instalar.

## `validate`

```
lsplugin validate <carpeta-publicada | paquete.lsplugin>
```

Parsea y valida el manifiesto (identidad, API 1, entry `.exe`, permisos, dependencias, ajustes) y en paquetes además
la estructura completa del ZIP (límites y rutas). Devuelve el detalle en `--json`.

## `build`

```
lsplugin build [carpeta-del-proyecto]
```

Ejecuta `dotnet publish -c Release` y devuelve la carpeta lista para `pack`. Falla con exit 1 y la cola del error del
compilador (completa con `--verbose`).

## `pack`

```
lsplugin pack <carpeta-publicada> [-o salida.lsplugin]
```

Requiere el `.exe` declarado en el manifiesto y produce un `.lsplugin` reproducible (mismo contenido ⇒ mismo SHA-256).
Si junto a la salida hay `<paquete>.sig.json`, avisa en el `--json` (`signed`, `signaturePath`). Un paquete sin
firmar solo se instala en Modo desarrollador.

## `install`

```
lsplugin install <paquete> [--dev | --public-key <pub.pem>] [--yes]
```

Antes de tocar nada valida: firma (con `--public-key`, o sin ella en `--dev`), compatibilidad con la LiveStream
instalada (`min-app-version`) y dependencias. Presenta los permisos en claro y pide confirmación (vale `--yes`).
La app abierta lo ve al reiniciarla; los datos del usuario del plugin se conservan al actualizar.

- `--dev`: modo desarrollador — instala sin firma, como el botón «Instalar desde archivo…» de la app.
- `--public-key plugin-public-key.pem`: instala un paquete firmado verificándolo contra esa clave (el flujo que la
  app enruta con su clave compilada).

## `list` / `info` / `enable` / `disable` / `uninstall`

```
lsplugin list                                              # almacén local
lsplugin list --server https://…                           # catálogo del marketplace (propietario)
lsplugin info com.empresa.plugin                            # manifiesto, permisos, ajustes, rutas
lsplugin enable|disable com.empresa.plugin
lsplugin uninstall com.empresa.plugin [--yes]
```

`uninstall` (como `disable`) se niega con exit 1 si otro plugin instalado (o activo) depende de él.

## `doctor`

```
lsplugin doctor [--json]
```

Seis comprobaciones: SDK de .NET 10, LiveStream instalado (registro por usuario), almacén de plugins y su estado,
feed local del SDK (`LIVESTREAM_SDK_DIR`), clave del publicador y token del marketplace (`LS_MARKETPLACE_TOKEN`).
Exit 0 solo si todo está correcto; cada fallo imprime una sugerencia accionable. Nunca muestra secretos.

## `keygen` / `sign` / `verify`

```
lsplugin keygen [--private ruta.pem] [--public ruta.pem] [--force]
lsplugin sign <paquete> --key <privada.pem>          # escribe <paquete>.sig.json
lsplugin verify <paquete> --public <pub.pem>         # valida paquete + firma
```

La clave privada no puede guardarse dentro de un repositorio git y queda restringida al usuario actual. `keygen`
pide `--force` para sobrescribir (las firmas anteriores dejarían de verificar). La firma cubre id, versión, tamaño y
SHA-256 del paquete (ECDSA P-256).

## `publish` / `yank` / `unyank` / `backup`

Solo el propietario del marketplace. El token va en la variable de entorno `LS_MARKETPLACE_TOKEN` (nunca en la línea
de órdenes) y el servidor con `--server https://…` o guardado con `config`.

```
lsplugin publish <paquete>                # sube paquete + firma
lsplugin yank <id> <versión>              # retira (pide confirmación)
lsplugin unyank <id> <versión>            # restaura
lsplugin backup -o copia.zip              # respaldo descargable
```

## `config`

```
lsplugin config                          # muestra lsplugin.json y su ubicación
lsplugin config --server https://…       # guarda el servidor por omisión
```

Ubicación: `%APPDATA%\LiveStream\lsplugin.json` (formato versionado, schema 1).

## `completions`

```
lsplugin completions pwsh >> $PROFILE       # PowerShell 7+/5.1
lsplugin completions bash >> ~/.bashrc
lsplugin completions zsh > ~/.zfunc/_lsplugin   # y fpath=~/.zfunc + compinit en ~/.zshrc
```

Completa órdenes, opciones y (tras `--template`) los ids de las seis plantillas.
