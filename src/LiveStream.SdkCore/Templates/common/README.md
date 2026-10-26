# __NAME__ — plugin de LiveStream

Plugin de ejemplo generado con el SDK de plugins de LiveStream. __DESC__

## Compilar y empaquetar

```powershell
dotnet publish -c Release
lsplugin validate bin\Release\net10.0\win-x64\
lsplugin pack bin\Release\net10.0\win-x64\
```

## Probarlo

En LiveStream: **Herramientas → Marketplace de plugins → Modo desarrollador**, y «Instalar desde archivo…» con el
`.lsplugin` que genera `lsplugin pack`.

## Probarlo sin LiveStream

Deja el ejecutable en `dotnet run` y habla el protocolo con una terminal de pruebas; o marca los puntos de interés en
[docs del SDK](https://github.com/angelitosystems/LiveStream-SDK/blob/main/docs/SDK.md).
