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
| `plugin.schema.json` | Esquema JSON de `plugin.json` |
| `docs/SDK.md` | Guía completa: ciclo de vida, permisos, protocolo, versiones y errores frecuentes |

## Empezar

Requiere el [SDK de .NET 10](https://dotnet.microsoft.com/download) para el ejemplo y la herramienta. El plugin en sí
puede estar escrito en cualquier lenguaje que produzca un `.exe`.

```powershell
dotnet build
dotnet run --project tools/LiveStream.PluginTool -- validate examples/HelloPlugin/bin/Debug/net10.0/win-x64
dotnet run --project tools/LiveStream.PluginTool -- pack examples/HelloPlugin/bin/Debug/net10.0/win-x64 -o hello.lsplugin
```

Para probarlo en LiveStream (0.9.0 o posterior): **Herramientas → Marketplace de plugins**, activa el **modo
desarrollador** y pulsa **Instalar desde archivo…**.

## Publicación

El marketplace oficial solo distribuye plugins firmados por Angelito Systems. La publicación de terceros todavía no
está abierta.

## Licencia

GPL-2.0-or-later, igual que LiveStream. Consulta `LICENSE`.
