# ADR 0031 — Deuda técnica: tipos de derecho pendientes en TipoDerecho

**Estado**: Aceptado  
**Fecha**: 2026-05-19  
**Sprint**: 2

---

## Contexto

Durante el Sprint 2 se implementó el enum `TipoDerecho` en
`SG.Domain/Catastro/Enums/TipoDerecho.cs` con los valores
necesarios para los workflows activos:

```csharp
Propietario    = 1
Poseedor       = 2
Usufructuario  = 3
Anticretico    = 4
Comodatario    = 5
```

`Anticretico` y `Comodatario` son figuras válidas en el derecho
boliviano (Código Civil art. 450 y 878 respectivamente) y se
incluyeron porque aparecen en registros catastrales reales de
Caranavi.

Sin embargo, el plan original identificó 4 tipos adicionales
requeridos por workflows del Sprint 3:

| Valor futuro | Nombre | Uso |
|---|---|---|
| 6 | `Ocupante` | Registro de ocupaciones de hecho (catastro informal) |
| 7 | `Copropietario` | Predios con titularidad compartida formal |
| 8 | `Solicitante` | Trámites en proceso, titular no confirmado |
| 9 | `RepresentanteLegal` | Actuación por poder notarial |

## Decisión

Aceptar el estado actual del enum en Sprint 2. Los 4 tipos
faltantes **no se agregan ahora** porque:

1. Ningún handler ni workflow del Sprint 2 los referencia.
2. Agregar valores semánticos sin casos de uso activos introduce
   superficie de error innecesaria en validadores y tests.
3. La adición posterior es no-destructiva: solo amplía el rango
   de valores válidos sin romper datos existentes.

## Consecuencias

- Los scripts de prueba E2E del Sprint 2 deben usar únicamente
  los valores `1`–`5`.
- En Sprint 3, al implementar los workflows que los requieren,
  agregar al enum con valores `6`, `7`, `8`, `9`.
- No se requiere migración de BD: el enum se persiste como
  `integer` y los valores nuevos simplemente amplían el rango.
- Actualizar este ADR a "Resuelto" al cerrar Sprint 3.
