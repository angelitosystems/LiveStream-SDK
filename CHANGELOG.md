# Cambios

Formato basado en [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/); versiones con
[Versionado Semántico](https://semver.org/lang/es/). La versión del SDK describe la biblioteca y el CLI; el protocolo
con LiveStream tiene su propia versión (`apiVersion`, hoy `1`).

## [1.1.0] - 2026-10-26

Ecosistema del SDK reorganizado: CLI profesional y GUI que comparten el mismo núcleo de servicios.

### Añadido
- **Núcleo `LiveStream.SdkCore`** (`src/LiveStream.SdkCore`): scaffold con plantillas, empaquetado, firma, build,
  instalación, diagnóstico y publicación. Es el código que las dos caras del SDK — la CLI y la GUI — ejecutan, de modo
  que una operación da el mismo resultado desde la terminal que en la interfaz gráfica.
- **GUI «SDK Studio»** (`tools/LiveStream.SdkStudio`, Avalonia, misma identidad visual que LiveStream): crear proyectos
  con las seis plantillas, editor de `plugin.json` con validación al instante, compilar/empaquetar/firmar/verificar,
  gestionar los plugins instalados (activar, desinstalar) y el diagnóstico completo con informe copiable.
- **`lsplugin` reescrita sobre SdkCore**: `init` (con `--template`, `--min-app-version`, `init-templates`),
  `doctor` (seis comprobaciones, `--json`), `build`, `install` (permisos visibles antes de confirmar;
  `--dev`/`--public-key`), `enable`/`disable`/`uninstall`, `config`, completions (`pwsh`/`bash`/`zsh`), ayuda por orden
  (`help <orden>`) y códigos de salida documentados (0 correcto, 1 error, 2 uso).
- **Pruebas**: `tests/LiveStream.SdkCore.Tests` (ciclo pack→sign→verify→install→uninstall, dependencias, doctor,
  configuración) y `tests/LiveStream.Cli.Tests` (el binario real de `lsplugin` ejecutado como proceso: ayuda, códigos
  de salida y el flujo init→validate). CI y release corren ahora las tres suites de pruebas.

### Cambiado
- `lsplugin new` pasa a llamarse `init` (sigue aceptando `new` como alias) y por defecto exige que el plugin declare
  `min-app-version 0.9.0`; `--min-app-version` lo ajusta para probar en apps locales anteriores.

## [1.0.1] - 2026-10-25

### Añadido
- `lsplugin keygen` sin argumentos usa las rutas recomendadas: privada en
  `%USERPROFILE%\.livestream-plugins\private.pem` y pública en `plugin-public-key.pem` de la carpeta actual, sin
  sobrescribir si ya existen (aviso con `--force`).

### Cambiado
- Los argumentos que faltan (`--id`, `--name`, `--server`, `--key`, `--public`) explican en el error el uso esperado
  de la orden, en vez de solo «Falta --x».

## [1.0.0] - 2026-10-25

Primera versión pública del SDK.

### Añadido

- **Biblioteca `LiveStream.PluginSdk`** (`src/LiveStream.PluginSdk`): manifiesto `plugin.json` con validación,
  paquetes `.lsplugin` (ZIP reproducible), firma y verificación ECDSA P-256 y el cliente `JsonRpcPeer` del protocolo
  JSON-RPC 2.0 por stdin/stdout.
- **CLI `lsplugin`** (`tools/LiveStream.PluginTool`), autocontenido: `new`, `validate`, `pack`, `keygen`, `sign`,
  `verify`, `publish`, `yank`/`unyank`, `list` y `backup`.
- **`lsplugin new` genera el proyecto completo** del plugin: `plugin.json`, `<Proyecto>.csproj`, `Program.cs` ya
  conectado al protocolo, `NuGet.config` apuntando al feed local del SDK y `.gitignore`. Con el SDK instalado referencia
  el paquete por feed local (sin conexión); desde el repositorio, por `ProjectReference`.
- **Instalador por usuario** (`tools/LiveStream.SdkSetup`): `LiveStream-SDK-Setup-<versión>.exe` instala `lsplugin`
  junto con el esquema, la documentación, el `README.txt` de arranque y el paquete NuGet local, lo **añade al PATH del
  usuario**, define `LIVESTREAM_SDK_DIR` y registra «LiveStream SDK» en «Aplicaciones instaladas» (HKCU). Desinstalar
  borra solo la carpeta marcada por el propio instalador; los proyectos y las claves de quien publica se conservan.
- **Paquete NuGet local**: cada distribución trae `LiveStream.PluginSdk.<versión>.nupkg`; los proyectos del scaffold lo
  restauran desde `LIVESTREAM_SDK_DIR\packages`.
- Plugin de ejemplo completo (`examples/HelloPlugin`) y guía de la API en `docs/SDK.md`.
- **CI** (`.github/workflows/ci.yml`): compila, valida y empaqueta el ejemplo en cada push y PR.
- **Release** (`.github/workflows/release.yml`): al subir un tag `v*` compila, prueba, empaqueta (instalador, `.zip`
  y sumas SHA-256) y publica la release con las notas del `CHANGELOG.md`.
- Pruebas unitarias de la lógica del PATH del instalador (`tests/LiveStream.SdkSetup.Tests`).
