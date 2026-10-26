# Cambios

Formato basado en [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/); versiones con
[Versionado Semántico](https://semver.org/lang/es/). La versión del SDK describe la biblioteca y el CLI; el protocolo
con LiveStream tiene su propia versión (`apiVersion`, hoy `1`).

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
