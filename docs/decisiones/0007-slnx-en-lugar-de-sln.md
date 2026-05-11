# ADR 0007 — Usar formato .slnx en lugar de .sln clásico

**Fecha**: 2026-05-11
**Estado**: Aceptado
**Autor**: Saul Gutierrez

---

## Contexto

Al ejecutar `dotnet new sln -n SG` con el SDK .NET 10.0.203, la herramienta creó
`SG.slnx` en lugar del archivo `SG.sln` clásico. El formato `.slnx` es el nuevo
formato de solución XML introducido como predeterminado en .NET 10.

El formato `.sln` clásico es un formato propietario heredado de Visual Studio,
difícil de leer y editar a mano. El `.slnx` es XML estándar, más legible y
alineado con el resto del ecosistema MSBuild.

## Decisión

Se mantiene **`SG.slnx`** como el archivo de solución del proyecto. No se recrea
como `.sln` clásico.

## Justificación

| Criterio | `.sln` clásico | `.slnx` |
|---|---|---|
| Legibilidad | Formato propietario difícil de leer | XML estándar, autodocumentado |
| Soporte VS Code + C# Dev Kit | Sí | Sí (desde versiones recientes) |
| Soporte GitHub Actions / CI | Sí | Sí (`dotnet build`, `dotnet test`) |
| Default SDK .NET 10 | No | **Sí** |
| Edición manual | Propensa a errores | Trivial |

El entorno de desarrollo es VS Code con C# Dev Kit (no Visual Studio IDE), por lo
que la compatibilidad exclusiva de `.sln` con Visual Studio no es un requisito.
Todos los comandos `dotnet build`, `dotnet test`, `dotnet restore` y `dotnet sln`
funcionan idénticamente con `.slnx`.

## Consecuencias

**Positivas**:
- Archivo de solución legible y editable a mano.
- Alineado con el default del SDK elegido (.NET 10).
- Sin deuda técnica por mantener formato heredado.

**Negativas / compromisos**:
- Si en el futuro se requiere abrir el proyecto en Visual Studio IDE (no Code),
  se debe verificar que la versión de VS soporte `.slnx` (VS 2022 17.12+).
- Colaboradores acostumbrados al `.sln` clásico deben actualizar su flujo.

## Referencias

- [.NET 10 release notes — nuevo formato .slnx](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10)
- Comando equivalente para crear `.sln` clásico si fuera necesario:
  `dotnet new sln --format sln -n SG`
